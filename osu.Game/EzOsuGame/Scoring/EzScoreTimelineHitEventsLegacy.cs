// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// OSU-TRANSITIONAL: HitEvents 作 SP 输入建 Timeline（F 类）。Mania 生产禁止；Osu 角逐至 OsuReplaySession。
// 详见 EZ-SR-TL-REGISTRY.md §1.6b。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Osu 角逐过渡：从 HitEvents 列表二次喂 SP 构建 <see cref="EzScoreTimeline"/>。
    /// Mania 不得调用；Mania 使用 <see cref="IEzReplaySession.RunTimelineDirectAsync"/>。
    /// </summary>
    // TODO(EZ-SR-TL-010): Osu Session 完成后删除本模块。
    // TODO(EZ-SR-TL-011): 删除 BuildFromHitEventsForTesting 及 BuildFromHitEvents。
    internal static class EzScoreTimelineHitEventsLegacy
    {
        // TODO(EZ-SR-TL-010): Osu Session 完成后删除。
        internal static (List<HitEvent>? hitEvents, bool offsetsRelativeToEnd) ResolveHitEvents(
            Score databasedScore,
            IBeatmap playableBeatmap,
            Func<Score, IBeatmap, CancellationToken, (List<HitEvent>? hitEvents, bool offsetsRelativeToEnd)>? hitEventFallback,
            CancellationToken cancellationToken)
        {
            if (databasedScore.ScoreInfo.HitEvents.Count > 0)
                return (databasedScore.ScoreInfo.HitEvents.ToList(), true);

            var ruleset = databasedScore.ScoreInfo.Ruleset.CreateInstance();
            var session = ruleset.CreateEzReplaySession();

            if (session != null)
            {
                var task = session.RunHitEventsAsync(databasedScore, playableBeatmap, cancellationToken);
                var hitEvents = Task.Run(() => task, cancellationToken).GetAwaiter().GetResult();
                return (hitEvents, false);
            }

            if (hitEventFallback != null)
                return hitEventFallback(databasedScore, playableBeatmap, cancellationToken);

            return (null, false);
        }

        internal static EzScoreTimeline BuildFromHitEventsForTesting(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents,
                                                                     bool offsetsRelativeToEnd = false)
            => BuildFromHitEvents(ruleset, beatmap, scoreInfo, hitEvents, offsetsRelativeToEnd);

        internal static EzScoreTimeline BuildFromHitEvents(Ruleset ruleset, IBeatmap beatmap, ScoreInfo scoreInfo, IReadOnlyList<HitEvent> hitEvents, bool offsetsRelativeToEnd)
        {
            double fallbackMissWindow = resolveFallbackMissWindow(beatmap);

            var scoreProcessor = ruleset.CreateScoreProcessor();
            applyScoreProcessorContext(scoreProcessor, scoreInfo);
            scoreProcessor.ApplyBeatmap(beatmap);
            scoreProcessor.Mods.Value = scoreInfo.Mods;

            foreach (var mod in scoreInfo.Mods.OfType<IApplicableToScoreProcessor>())
                mod.ApplyToScoreProcessor(scoreProcessor);

            var hitObjectMap = buildHitObjectReferenceMap(beatmap);

            var snapshots = new List<EzScoreTimelineSnapshot>();
            double lastClockTime = double.NegativeInfinity;

            foreach (var hitEvent in hitEvents.OrderBy(e => getJudgementTime(e, offsetsRelativeToEnd, beatmap, fallbackMissWindow, null, hitObjectMap)))
            {
                var beatmapHitObject = findBeatmapHitObject(beatmap, hitEvent.HitObject, hitObjectMap);
                ensureHitWindows(beatmap, beatmapHitObject);

                scoreProcessor.ApplyResult(new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = hitEvent.Result,
                    TimeOffset = hitEvent.TimeOffset,
                });

                double clockTime = getJudgementTime(hitEvent, offsetsRelativeToEnd, beatmap, fallbackMissWindow, beatmapHitObject, hitObjectMap);

                if (clockTime <= lastClockTime)
                    clockTime = lastClockTime + 0.001;

                lastClockTime = clockTime;
                snapshots.Add(EzScoreTimelineSnapshot.Create(clockTime, scoreProcessor, hitEvent.GameplayRate ?? 1.0));
            }

            if (snapshots.Count == 0)
                snapshots.Add(EzScoreTimelineSnapshot.Create(0, scoreProcessor));
            else if (snapshots[0].ClockTime > 0)
                snapshots.Insert(0, new EzScoreTimelineSnapshot { ClockTime = 0 });

            return new EzScoreTimeline(snapshots);
        }

        private static Dictionary<HitObject, HitObject> buildHitObjectReferenceMap(IBeatmap beatmap)
        {
            var map = new Dictionary<HitObject, HitObject>(ReferenceEqualityComparer.Instance);

            foreach (var ho in beatmap.HitObjects)
                collectHitObjectReferences(ho, map);

            return map;
        }

        private static void collectHitObjectReferences(HitObject hitObject, Dictionary<HitObject, HitObject> map)
        {
            map.TryAdd(hitObject, hitObject);

            foreach (var nested in hitObject.NestedHitObjects)
                collectHitObjectReferences(nested, map);
        }

        // TODO(EZ-SR-TL-012): Osu Session 完成后删除。
        private static double getJudgementTime(HitEvent hitEvent, bool offsetsRelativeToEnd, IBeatmap beatmap, double fallbackMissWindow, HitObject? beatmapHitObject = null,
                                               Dictionary<HitObject, HitObject>? hitObjectMap = null)
        {
            beatmapHitObject ??= findBeatmapHitObject(beatmap, hitEvent.HitObject, hitObjectMap);
            return EzScoreTimelineJudgementTime.Get(hitEvent, offsetsRelativeToEnd, beatmapHitObject, fallbackMissWindow);
        }

        // TODO(EZ-SR-TL-013): Osu Session 完成后删除。
        private static HitObject findBeatmapHitObject(IBeatmap beatmap, HitObject hitObject, Dictionary<HitObject, HitObject>? hitObjectMap = null)
        {
            if (hitObjectMap != null && hitObjectMap.TryGetValue(hitObject, out var mapped))
                return mapped;

            foreach (var candidate in beatmap.HitObjects)
            {
                if (ReferenceEquals(candidate, hitObject))
                    return candidate;

                var nested = findNestedBeatmapHitObject(candidate, hitObject);
                if (nested != null)
                    return nested;
            }

            foreach (var candidate in beatmap.HitObjects)
            {
                if (objectsMatchForLookup(candidate, hitObject))
                    return candidate;

                var nested = findNestedBeatmapHitObject(candidate, hitObject);
                if (nested != null)
                    return nested;
            }

            return hitObject;
        }

        private static HitObject? findNestedBeatmapHitObject(HitObject parent, HitObject hitObject)
        {
            foreach (var nested in parent.NestedHitObjects)
            {
                if (ReferenceEquals(nested, hitObject))
                    return nested;

                var deeper = findNestedBeatmapHitObject(nested, hitObject);
                if (deeper != null)
                    return deeper;
            }

            foreach (var nested in parent.NestedHitObjects)
            {
                if (objectsMatchForLookup(nested, hitObject))
                    return nested;
            }

            return null;
        }

        private static bool objectsMatchForLookup(HitObject candidate, HitObject hitObject)
        {
            if (candidate.StartTime != hitObject.StartTime || candidate.GetType() != hitObject.GetType())
                return false;

            if (hitObject is IHasColumn hitColumn)
            {
                if (candidate is IHasColumn candidateColumn)
                    return candidateColumn.Column == hitColumn.Column;

                return false;
            }

            return true;
        }

        private static void applyScoreProcessorContext(ScoreProcessor scoreProcessor, ScoreInfo scoreInfo)
        {
            if (scoreInfo.IsLegacyScore)
                scoreProcessor.IsLegacyScore = true;
        }

        // TODO(EZ-SR-TL-015): Osu Session 完成后删除。
        private static void ensureHitWindows(IBeatmap? beatmap, HitObject hitObject)
        {
            if (beatmap == null)
                return;

            if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                return;

            if (hitObject.NestedHitObjects.Count > 0)
                return;

            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);
        }

        private static double resolveFallbackMissWindow(IBeatmap? beatmap)
        {
            if (beatmap == null)
                return 0;

            foreach (var hitObject in beatmap.HitObjects)
            {
                var windows = findFirstNonEmptyHitWindows(hitObject);
                if (windows != null)
                    return windows.WindowFor(HitResult.Miss);
            }

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject.NestedHitObjects.Count > 0)
                    continue;

                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

                if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                    return hitObject.HitWindows.WindowFor(HitResult.Miss);
            }

            return 0;
        }

        private static HitWindows? findFirstNonEmptyHitWindows(HitObject hitObject)
        {
            if (hitObject.HitWindows != null && hitObject.HitWindows != HitWindows.Empty)
                return hitObject.HitWindows;

            foreach (var nested in hitObject.NestedHitObjects)
            {
                var windows = findFirstNonEmptyHitWindows(nested);
                if (windows != null)
                    return windows;
            }

            return null;
        }
    }
}
