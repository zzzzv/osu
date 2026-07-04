// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 从本地 replay 构建分数时间线。
    /// Mania：<see cref="IEzReplaySession.RunTimelineDirectAsync"/>（replay 一遍 SP 快照）。
    /// Osu：过渡路径 <see cref="EzScoreTimelineHitEventsLegacy"/>（F 类，待 OsuReplaySession）。
    /// 架构详见 <see cref="EZ_SR_TL_REGISTRY"/>（同目录 EZ-SR-TL-REGISTRY.md）。
    /// </summary>
    public static class EzScoreTimelineBuilder
    {
        private static Func<Score, IBeatmap, CancellationToken, (System.Collections.Generic.List<HitEvent>? hitEvents, bool offsetsRelativeToEnd)>? hitEventFallback;

        /// <summary>
        /// 注册 HitEvents 生成回退（Osu 过渡）。见 OsuScoreHitEventGenerator 静态构造。
        /// </summary>
        public static void RegisterHitEventFallback(Func<Score, IBeatmap, CancellationToken, (System.Collections.Generic.List<HitEvent>? hitEvents, bool offsetsRelativeToEnd)> fallback)
        {
            hitEventFallback = fallback;
        }

        /// <summary>
        /// 创建一个进程内 in-memory 缓存实例，绑定到调用方生命周期。
        /// </summary>
        public static IEzScoreTimelineCache CreateSessionCache() => new EzScoreTimelineCache();

        /// <summary>架构注册表 markdown 文件名（文档用）。</summary>
        public const string EZ_SR_TL_REGISTRY = "EZ-SR-TL-REGISTRY.md";

        public static EzScoreTimeline? TryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap = null,
                                                IEzScoreTimelineCache? cache = null, IGameplayEnvironment? environment = null, CancellationToken cancellationToken = default)
            => tryBuild(scoreManager, beatmaps, scoreInfo, sharedPlayableBeatmap, cache ?? NullEzScoreTimelineCache.INSTANCE, environment, cancellationToken);

        internal static EzScoreTimeline BuildFromHitEventsForTesting(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, System.Collections.Generic.IReadOnlyList<HitEvent> hitEvents,
                                                                     bool offsetsRelativeToEnd = false)
            => EzScoreTimelineHitEventsLegacy.BuildFromHitEventsForTesting(ruleset, beatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);

        private static EzScoreTimeline? tryBuild(ScoreManager scoreManager, BeatmapManager beatmaps, ScoreInfo scoreInfo, IBeatmap? sharedPlayableBeatmap,
                                                 IEzScoreTimelineCache cache, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scoreManager);
            ArgumentNullException.ThrowIfNull(beatmaps);
            ArgumentNullException.ThrowIfNull(scoreInfo);

            var timelineMode = EzScoreRaceRulesetSupport.GetGhostTimelineMode(scoreInfo.Ruleset);

            if (timelineMode == EzScoreRaceGhostTimelineMode.None)
                return null;

            IBeatmap? beatmapForFingerprint;

            if (sharedPlayableBeatmap != null)
            {
                beatmapForFingerprint = sharedPlayableBeatmap;
            }
            else
            {
                var workingBeatmap = beatmaps.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

                if (workingBeatmap is DummyWorkingBeatmap)
                    beatmapForFingerprint = null;
                else
                    beatmapForFingerprint = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
            }

            string? cacheKey = beatmapForFingerprint != null
                ? getCacheKey(scoreInfo, timelineMode, environment ?? GlobalConfigStore.EzConfig.ResolveForReplay(scoreInfo, ReplayRunPurpose.ForLive), beatmapForFingerprint)
                : null;

            if (!string.IsNullOrEmpty(cacheKey) && cache.TryGet(cacheKey, out var cached))
                return cached == EzScoreTimeline.EMPTY ? null : cached;

            cancellationToken.ThrowIfCancellationRequested();

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            var ruleset = scoreInfo.Ruleset.CreateInstance();
            IBeatmap playableBeatmap;

            if (sharedPlayableBeatmap != null)
            {
                playableBeatmap = sharedPlayableBeatmap;
            }
            else
            {
                var workingBeatmap = beatmaps.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

                if (workingBeatmap is DummyWorkingBeatmap)
                {
                    if (!string.IsNullOrEmpty(cacheKey))
                        cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                    return null;
                }

                playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);
            }

            if (playableBeatmap.HitObjects.Count == 0)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            EzScoreTimeline? timeline;

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                {
                    var session = ruleset.CreateEzReplaySession();

                    if (session == null)
                    {
                        timeline = null;
                        break;
                    }

                    timeline = session.RunTimelineDirectAsync(databasedScore, playableBeatmap, environment, ReplayRunPurpose.ForLive, cancellationToken)
                                      .ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
                }

                case EzScoreRaceGhostTimelineMode.HitEvents:
                {
                    // TODO(EZ-SR-TL-007): blocked: Osu Session 架构就绪后改 OsuReplaySession.RunTimelineDirectAsync。
                    var (hitEvents, offsetsRelativeToEnd) = EzScoreTimelineHitEventsLegacy.ResolveHitEvents(
                        databasedScore, playableBeatmap, hitEventFallback, cancellationToken);

                    if (hitEvents == null || hitEvents.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(cacheKey))
                            cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                        return null;
                    }

                    timeline = EzScoreTimelineHitEventsLegacy.BuildFromHitEvents(ruleset, playableBeatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);
                    break;
                }

                default:
                    if (!string.IsNullOrEmpty(cacheKey))
                        cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                    return null;
            }

            if (timeline == null)
            {
                if (!string.IsNullOrEmpty(cacheKey))
                    cache.Store(cacheKey, EzScoreTimeline.EMPTY);
                return null;
            }

            if (!string.IsNullOrEmpty(cacheKey))
                cache.Store(cacheKey, timeline);

            return timeline;
        }

        private static string? getCacheKey(ScoreInfo? scoreInfo, EzScoreRaceGhostTimelineMode timelineMode, IGameplayEnvironment environment, IBeatmap? beatmap)
        {
            string? identity = getScoreIdentity(scoreInfo);

            if (identity == null || scoreInfo == null)
                return null;

            string modFp = getModFingerprint(scoreInfo.Mods);
            string beatmapFp = beatmap != null
                ? $"b:{beatmap.BeatmapInfo.ID}:od{beatmap.BeatmapInfo.Difficulty.OverallDifficulty:F2}:hp{beatmap.BeatmapInfo.Difficulty.DrainRate:F2}"
                : "b:?";

            switch (timelineMode)
            {
                case EzScoreRaceGhostTimelineMode.ManiaSession:
                    return $"{identity}|m|{modFp}|{beatmapFp}|hm{(int)environment.ManiaHitMode}|hh{(int)environment.ManiaHealthMode}|jp{(int)environment.JudgePrecedence}";

                case EzScoreRaceGhostTimelineMode.HitEvents:
                    // TODO(EZ-SR-TL-008): blocked: Osu Session 对齐后定义 Osu 缓存键策略。
                    return $"{identity}|h|{modFp}|{beatmapFp}";

                default:
                    return null;
            }
        }

        private static string getModFingerprint(System.Collections.Generic.IReadOnlyList<Mod> mods)
            => string.Join(',', mods.OrderBy(m => m.Acronym).Select(m => m.Acronym));

        private static string? getScoreIdentity(ScoreInfo? scoreInfo)
        {
            if (scoreInfo == null)
                return null;

            if (!string.IsNullOrEmpty(scoreInfo.Hash))
                return $"hash:{scoreInfo.Hash}";

            if (scoreInfo.ID != Guid.Empty)
                return $"id:{scoreInfo.ID}";

            return null;
        }
    }
}
