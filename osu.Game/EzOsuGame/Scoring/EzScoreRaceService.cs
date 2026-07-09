// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Performance;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局 ghost 角逐服务。
    ///
    /// - 仅在有角逐 HUD 消费者（interest &gt; 0）时查询元数据 / 构建 timeline
    /// - timeline 在专用后台线程串行构建；局内暂停构建，避免与 Update/Draw 争用 CPU
    /// - 可通过实验性开关 <see cref="Ez2Setting.EzScoreRaceServiceEnabled"/> 整服务 no-op
    /// </summary>
    public partial class EzScoreRaceService : Component, IEzScoreRaceStateLookup
    {
        private const int time_to_sleep_during_gameplay_ms = 30_000;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private ILocalUserPlayInfo? localUserPlayInfo { get; set; }

        [Resolved(CanBeNull = true)]
        private IHighPerformanceSessionManager? highPerformanceSessionManager { get; set; }

        /// <summary>Mod 过滤策略（HUD ModFilterSetting 绑定到此）。</summary>
        public Bindable<EzScoreModFilter> ModFilter { get; } = new Bindable<EzScoreModFilter>(EzScoreModFilter.Any);

        /// <summary>ghost 条目上限（HUD MaxEntriesSetting 绑定到此）。</summary>
        public BindableNumber<int> MaxEntries { get; } = new BindableNumber<int>(5)
        {
            MinValue = 1,
            MaxValue = 10,
        };

        public IBindableDictionary<string, EzScoreRaceState> States => states;

        /// <summary>进局后 ghost timeline 是否仍在后台构建。</summary>
        public bool IsTimelineBuildInProgress { get; private set; }

        /// <summary>当前角逐 HUD 消费者数量；为 0 时不做 Realm 查询与 timeline 构建。</summary>
        public int ConsumerInterestCount { get; private set; }

        private readonly BindableDictionary<string, EzScoreRaceState> states = new BindableDictionary<string, EzScoreRaceState>();

        private readonly IEzScoreTimelineCache timelineCache = EzScoreTimelineBuilder.CreateSessionCache();

        /// <summary>metadata 缓存：queryKey → ghost 元数据列表（timeline 可能 null 或部分就绪）。</summary>
        private readonly Dictionary<string, List<EzScoreRaceState>> metadataCache = new Dictionary<string, List<EzScoreRaceState>>();

        private readonly LinkedList<string> metadataCacheLru = new LinkedList<string>();

        private const int metadata_cache_capacity = 3;

        private string? activeQueryKey;
        private Guid? activeBeatmapId;

        private CancellationTokenSource? timelineBuildCts;
        private int timelineBuildVersion;

        private Bindable<bool> serviceEnabled = new Bindable<bool>(true);
        private bool awaitingPlayerLoaderBuild;

        [BackgroundDependencyLoader]
        private void load(Ez2ConfigManager config)
        {
            serviceEnabled = config.GetBindable<bool>(Ez2Setting.EzScoreRaceServiceEnabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            serviceEnabled.BindValueChanged(onServiceEnabledChanged, true);

            ModFilter.BindValueChanged(_ => onQueryContextChanged());
            MaxEntries.BindValueChanged(_ => onQueryContextChanged());
            currentBeatmap.BindValueChanged(onBeatmapChanged, true);

            if (hasConsumers && isServiceActive)
                activateConsumerSession();
        }

        /// <summary>
        /// 角逐 HUD 在可用时注册兴趣。首次从 0→1 时若已有谱面则立刻刷新元数据，
        /// 并在仍处于 / 刚经过 <see cref="PlayerLoader"/> 等待态时补触发 timeline build。
        /// </summary>
        public void RegisterInterest()
        {
            ConsumerInterestCount++;

            if (ConsumerInterestCount != 1 || !isServiceActive)
                return;

            if (LoadState < LoadState.Ready)
                return;

            activateConsumerSession();
        }

        /// <summary>角逐 HUD 卸载时注销兴趣；归零后进入静默态并释放 screen hooks。</summary>
        public void UnregisterInterest()
        {
            if (ConsumerInterestCount <= 0)
                return;

            ConsumerInterestCount--;

            if (ConsumerInterestCount > 0)
                return;

            enterQuiescentState(releaseScreenHooks: true);
        }

        private bool isServiceActive => serviceEnabled.Value;

        private bool hasConsumers => ConsumerInterestCount > 0;

        private void activateConsumerSession()
        {
            ensureScreenHooksSubscribed();

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            if (awaitingPlayerLoaderBuild || game.ScreenStack.CurrentScreen is PlayerLoader)
                scheduleTimelineBuildIfNeeded();
        }

        private void enterQuiescentState(bool releaseScreenHooks)
        {
            cancelTimelineBuild();
            timelineBuildVersion++;
            timelineCache.Clear();
            metadataCache.Clear();
            metadataCacheLru.Clear();
            activeQueryKey = null;
            awaitingPlayerLoaderBuild = false;
            publishStatesDiff(Array.Empty<EzScoreRaceState>());

            if (releaseScreenHooks)
                unsubscribeScreenHooks();
        }

        private void onServiceEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (!e.NewValue)
            {
                enterQuiescentState(releaseScreenHooks: true);
                return;
            }

            if (!hasConsumers)
                return;

            activateConsumerSession();
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> e)
        {
            if (!isServiceActive)
                return;

            var beatmapInfo = e.NewValue.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            if (activeBeatmapId != beatmapInfo.ID)
            {
                activeBeatmapId = beatmapInfo.ID;
                cancelTimelineBuild();
                awaitingPlayerLoaderBuild = false;
            }

            if (!hasConsumers)
                return;

            refreshMetadata(e.NewValue);
        }

        private void onQueryContextChanged()
        {
            if (!isServiceActive || !hasConsumers)
                return;

            cancelTimelineBuild();

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);
        }

        /// <summary>
        /// 通知 Mod 组合变化（与 <see cref="ModFilter"/> 变更等效，并 evict 当前谱面 metadata 缓存）。
        /// </summary>
        public void NotifyModsChanged()
        {
            if (activeQueryKey != null)
                evictMetadataCache(activeQueryKey);

            onQueryContextChanged();
        }

        private void refreshMetadata(WorkingBeatmap workingBeatmap)
        {
            if (!isServiceActive || !hasConsumers)
                return;

            var beatmapInfo = workingBeatmap.BeatmapInfo;

            if (beatmapInfo == null)
                return;

            if (!EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
            {
                publishStatesDiff(Array.Empty<EzScoreRaceState>());
                return;
            }

            string queryKey = buildQueryKey(beatmapInfo.ID);
            activeQueryKey = queryKey;

            if (metadataCache.TryGetValue(queryKey, out var cached))
            {
                touchMetadataCacheLru(queryKey);
                publishStatesDiff(cached);
                scheduleTimelineBuildIfNeeded();
                return;
            }

            var rulesetInfo = beatmapInfo.Ruleset;
            var allLocalScores = EzLocalScoreQueries.GetLocalScoresWithReplay(realm, beatmapInfo, rulesetInfo);
            var ghostScores = EzLocalScoreQueries.SelectGhostCandidates(
                allLocalScores,
                getCurrentMods(),
                ModFilter.Value,
                MaxEntries.Value);

            var metadataStates = ghostScores
                                 .Select(s => new EzScoreRaceState(s, timeline: null))
                                 .ToList();

            storeMetadataCache(queryKey, metadataStates);
            publishStatesDiff(metadataStates);
            scheduleTimelineBuildIfNeeded();
        }

        /// <summary>
        /// 选歌 / PlayerLoader 阶段后台构建 timeline；进 <see cref="Player"/> 后由 screen hook 硬停。
        /// </summary>
        private void scheduleTimelineBuildIfNeeded()
        {
            if (!isServiceActive || !hasConsumers || IsTimelineBuildInProgress)
                return;

            if (isActiveGameplayScreen())
                return;

            if (states.Values.All(s => s.Timeline != null))
                return;

            Schedule(() => requestTimelineBuild(priority: false));
        }

        private void requestTimelineBuild(bool priority)
        {
            if (!isServiceActive)
                return;

            if (!hasConsumers)
            {
                awaitingPlayerLoaderBuild = true;
                return;
            }

            if (isActiveGameplayScreen())
                return;

            awaitingPlayerLoaderBuild = false;

            var workingBeatmap = currentBeatmap.Value;
            var beatmapInfo = workingBeatmap?.BeatmapInfo;

            if (beatmapInfo == null || !EzScoreRaceRulesetSupport.SupportsGhostRace(beatmapInfo.Ruleset))
                return;

            if (states.Count == 0)
                refreshMetadata(workingBeatmap!);

            cancelTimelineBuild();

            timelineBuildCts = new CancellationTokenSource();
            var token = timelineBuildCts.Token;
            int version = ++timelineBuildVersion;
            IsTimelineBuildInProgress = true;

            performTimelineBuildAsync(workingBeatmap!, beatmapInfo, token, version);
        }

        private async void performTimelineBuildAsync(WorkingBeatmap workingBeatmap, BeatmapInfo beatmapInfo, CancellationToken token, int version)
        {
            try
            {
                var scoreInfos = states.Values.Select(s => s.ScoreInfo).ToList();

                if (scoreInfos.Count == 0)
                {
                    Schedule(() => finishTimelineBuild(version));
                    return;
                }

                var rulesetInfo = beatmapInfo.Ruleset;
                var results = new EzScoreTimeline?[scoreInfos.Count];

                for (int i = 0; i < scoreInfos.Count; i++)
                {
                    if (states.TryGetValue(scoreInfos[i].ID.ToString(), out var existing) && existing.Timeline != null)
                        results[i] = existing.Timeline;
                }

                await Task.Factory.StartNew(() =>
                {
                    IBeatmap? sharedPlayable = null;

                    for (int i = 0; i < scoreInfos.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!isBuildStillValid(version) || isActiveGameplayScreen())
                            return;

                        waitWhileGameplayCpuSensitive(token);

                        if (results[i] != null)
                            continue;

                        sharedPlayable ??= workingBeatmap.GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>());

                        results[i] = EzScoreTimelineBuilder.TryBuild(
                            scoreManager,
                            beatmaps,
                            scoreInfos[i],
                            sharedPlayable,
                            timelineCache,
                            token);

                        if (results[i] != null)
                        {
                            int index = i;
                            var builtTimeline = results[i];
                            Schedule(() => applySingleTimelineResult(scoreInfos[index], builtTimeline, version));
                        }
                    }
                }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);

                if (!isBuildStillValid(version))
                    return;

                Schedule(() => applyTimelineBuildResults(scoreInfos, results, version));
            }
            catch (OperationCanceledException)
            {
                Schedule(() => finishTimelineBuild(version));
                Logger.Log("[EzScoreRaceService] Timeline build cancelled", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Schedule(() => finishTimelineBuild(version));
                Logger.Error(ex, "[EzScoreRaceService] Timeline build failed", Ez2ConfigManager.LOGGER_NAME);
            }
        }

        private void applySingleTimelineResult(ScoreInfo scoreInfo, EzScoreTimeline? timeline, int version)
        {
            if (!isBuildStillValid(version))
                return;

            string id = scoreInfo.ID.ToString();

            if (states.TryGetValue(id, out var state))
                state.Timeline = timeline;

            if (activeQueryKey != null && metadataCache.TryGetValue(activeQueryKey, out var cached))
            {
                foreach (var cachedState in cached)
                {
                    if (cachedState.ScoreInfo.ID == scoreInfo.ID)
                        cachedState.Timeline = timeline;
                }
            }
        }

        private void applyTimelineBuildResults(IReadOnlyList<ScoreInfo> scoreInfos, EzScoreTimeline?[] results, int version)
        {
            if (!isBuildStillValid(version))
                return;

            for (int i = 0; i < scoreInfos.Count; i++)
            {
                string id = scoreInfos[i].ID.ToString();

                if (!states.TryGetValue(id, out var state))
                    continue;

                state.Timeline = results[i];
            }

            if (activeQueryKey != null && metadataCache.TryGetValue(activeQueryKey, out var cached))
            {
                foreach (var cachedState in cached)
                {
                    if (states.TryGetValue(cachedState.ScoreInfo.ID.ToString(), out var live))
                        cachedState.Timeline = live.Timeline;
                }
            }

            finishTimelineBuild(version);
            Logger.Log($"[EzScoreRaceService] Timeline build complete for {scoreInfos.Count} ghosts", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
        }

        private void finishTimelineBuild(int version)
        {
            if (version != timelineBuildVersion)
                return;

            IsTimelineBuildInProgress = false;
        }

        private bool isBuildStillValid(int version)
            => version == timelineBuildVersion && isServiceActive && hasConsumers;

        private bool isActiveGameplayScreen()
            => game.ScreenStack.CurrentScreen is Player;

        private void waitWhileGameplayCpuSensitive(CancellationToken cancellationToken)
        {
            while (isActiveGameplayScreen()
                   || localUserPlayInfo?.PlayingState.Value is LocalUserPlayingState.Playing or LocalUserPlayingState.Break
                   || highPerformanceSessionManager?.IsSessionActive == true)
            {
                cancellationToken.WaitHandle.WaitOne(time_to_sleep_during_gameplay_ms);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private void publishStatesDiff(IReadOnlyList<EzScoreRaceState> incoming)
        {
            var incomingIds = new HashSet<string>(incoming.Select(s => s.ScoreInfo.ID.ToString()));

            foreach (string key in states.Keys.ToList())
            {
                if (!incomingIds.Contains(key))
                    states.Remove(key);
            }

            foreach (var state in incoming)
            {
                string id = state.ScoreInfo.ID.ToString();

                if (states.TryGetValue(id, out var existing))
                {
                    if (ReferenceEquals(existing, state))
                        continue;

                    if (state.Timeline != null)
                        existing.Timeline = state.Timeline;

                    continue;
                }

                states[id] = state;
            }
        }

        private string buildQueryKey(Guid beatmapId)
            => $"{beatmapId}|{ModFilter.Value}|{EzLocalScoreQueries.GetModFilterCacheFingerprint(ModFilter.Value, getCurrentMods())}|{MaxEntries.Value}";

        private void storeMetadataCache(string queryKey, List<EzScoreRaceState> statesToStore)
        {
            metadataCache[queryKey] = statesToStore;
            touchMetadataCacheLru(queryKey);

            while (metadataCacheLru.Count > metadata_cache_capacity)
            {
                string evictedKey = metadataCacheLru.Last!.Value;
                metadataCacheLru.RemoveLast();
                metadataCache.Remove(evictedKey);
            }
        }

        private void touchMetadataCacheLru(string queryKey)
        {
            if (metadataCacheLru.First is { Value: var headKey } && headKey == queryKey)
                return;

            for (var node = metadataCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != queryKey)
                    continue;

                metadataCacheLru.Remove(node);
                metadataCacheLru.AddFirst(node);
                return;
            }

            metadataCacheLru.AddFirst(queryKey);
        }

        private void evictMetadataCache(string queryKey)
        {
            metadataCache.Remove(queryKey);

            for (var node = metadataCacheLru.First; node != null; node = node.Next)
            {
                if (node.Value != queryKey)
                    continue;

                metadataCacheLru.Remove(node);
                return;
            }
        }

        private void cancelTimelineBuild()
        {
            var cts = timelineBuildCts;
            if (cts == null)
                return;

            timelineBuildCts = null;
            IsTimelineBuildInProgress = false;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                cts.Dispose();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                unsubscribeScreenHooks();
                cancelTimelineBuild();
                serviceEnabled.UnbindAll();
            }

            base.Dispose(isDisposing);
        }
    }
}
