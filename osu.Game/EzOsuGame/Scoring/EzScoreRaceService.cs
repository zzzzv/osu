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
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 全局 ghost 角逐服务。
    ///
    /// - 默认仅在有角逐 HUD 消费者（interest &gt; 0）时查询元数据 / 构建 timeline
    /// - 进局（osu.Game.Screens.Play.PlayerLoader）或 HUD 首次注册时后台构建 timeline
    /// - 可通过实验性开关 <see cref="Ez2Setting.EzScoreRaceServiceEnabled"/> 整服务 no-op
    /// </summary>
    public partial class EzScoreRaceService : Component, IEzScoreRaceStateLookup
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> currentBeatmap { get; set; } = null!;

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

            subscribeScreenHooks();
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

            // HUD 可能在本服务 LoadComplete / Resolved 注入之前拿到 DI 缓存实例。
            // 此时仅累加计数；LoadComplete 里的 BindValueChanged(true) 会在就绪后拉起查询。
            if (LoadState < LoadState.Ready)
                return;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            if (awaitingPlayerLoaderBuild || game.ScreenStack.CurrentScreen is PlayerLoader)
                requestTimelineBuild(priority: true);
        }

        /// <summary>角逐 HUD 卸载时注销兴趣；归零后取消进行中的 build 并清空 States。</summary>
        public void UnregisterInterest()
        {
            if (ConsumerInterestCount <= 0)
                return;

            ConsumerInterestCount--;

            if (ConsumerInterestCount > 0)
                return;

            cancelTimelineBuild();
            awaitingPlayerLoaderBuild = false;
            publishStatesDiff(Array.Empty<EzScoreRaceState>());
        }

        private bool isServiceActive => serviceEnabled.Value;

        private bool hasConsumers => ConsumerInterestCount > 0;

        private void onServiceEnabledChanged(ValueChangedEvent<bool> e)
        {
            if (!e.NewValue)
            {
                cancelTimelineBuild();
                awaitingPlayerLoaderBuild = false;
                publishStatesDiff(Array.Empty<EzScoreRaceState>());
                return;
            }

            if (!hasConsumers)
                return;

            if (currentBeatmap.Value?.BeatmapInfo != null)
                refreshMetadata(currentBeatmap.Value);

            if (awaitingPlayerLoaderBuild || game.ScreenStack.CurrentScreen is PlayerLoader)
                requestTimelineBuild(priority: true);
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
        }

        private void requestTimelineBuild(bool priority)
        {
            if (!isServiceActive)
                return;

            // PlayerLoader 时常早于 HUD LoadComplete；记录等待态，待首次 RegisterInterest 补 build。
            if (!hasConsumers)
            {
                awaitingPlayerLoaderBuild = true;
                return;
            }

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
                    Schedule(() => IsTimelineBuildInProgress = false);
                    return;
                }

                var rulesetInfo = beatmapInfo.Ruleset;
                var results = new EzScoreTimeline?[scoreInfos.Count];

                // 同一谱面只转一次 playable，各 ghost 只读共享，避免 Parallel 内重复转谱。
                IBeatmap? sharedPlayable = null;

                bool anyNeedsBuild = false;

                for (int i = 0; i < scoreInfos.Count; i++)
                {
                    if (states.TryGetValue(scoreInfos[i].ID.ToString(), out var existing) && existing.Timeline != null)
                    {
                        results[i] = existing.Timeline;
                        continue;
                    }

                    anyNeedsBuild = true;
                }

                if (anyNeedsBuild)
                    sharedPlayable = workingBeatmap.GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>());

                await Task.Run(() =>
                {
                    Parallel.For(0, scoreInfos.Count, new ParallelOptions
                    {
                        CancellationToken = token,
                        MaxDegreeOfParallelism = Math.Max(1, Math.Min(2, Environment.ProcessorCount / 2)),
                    }, i =>
                    {
                        if (results[i] != null)
                            return;

                        results[i] = EzScoreTimelineBuilder.TryBuild(
                            scoreManager,
                            beatmaps,
                            scoreInfos[i],
                            sharedPlayable,
                            timelineCache,
                            token);
                    });
                }, token).ConfigureAwait(false);

                if (token.IsCancellationRequested || version != timelineBuildVersion)
                    return;

                Schedule(() =>
                {
                    if (token.IsCancellationRequested || version != timelineBuildVersion)
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

                    IsTimelineBuildInProgress = false;
                    Logger.Log($"[EzScoreRaceService] Timeline build complete for {scoreInfos.Count} ghosts", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
                });
            }
            catch (OperationCanceledException)
            {
                Schedule(() => IsTimelineBuildInProgress = false);
                Logger.Log("[EzScoreRaceService] Timeline build cancelled", Ez2ConfigManager.LOGGER_NAME, LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Schedule(() => IsTimelineBuildInProgress = false);
                Logger.Error(ex, "[EzScoreRaceService] Timeline build failed", Ez2ConfigManager.LOGGER_NAME);
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

                    // 保留已有实例引用，避免 HUD processor 绑定失效。
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
