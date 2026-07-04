// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge
{
    /// <summary>
    /// Osu replay press 匹配 → 一遍 <see cref="ScoreProcessor.ApplyResult"/>。
    /// MVP：circle + nested slider tick；slider 主体 / spinner 完整判定为已知限制。
    /// </summary>
    internal static class OsuReplaySessionSimulator
    {
        private readonly struct ReplayPressEvent
        {
            public readonly double Time;
            public readonly Vector2 Position;

            public ReplayPressEvent(double time, Vector2 position)
            {
                Time = time;
                Position = position;
            }
        }

        internal static void Simulate(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment environment,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            OsuReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score.Replay);

            var osuFrames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (osuFrames.Count == 0)
                return;

            var targets = collectJudgementTargets(beatmap, cancellationToken);

            if (targets.Count == 0)
                return;

            var replayPresses = collectPressEvents(osuFrames);
            bool[] pressConsumed = new bool[replayPresses.Count];
            int firstRelevantPressIndex = 0;

            timelineRecorder?.RecordInitial(scoreProcessor, gameplayRate);

            foreach (var target in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (target is not OsuHitObject osuTarget || target.HitWindows == null || ReferenceEquals(target.HitWindows, HitWindows.Empty))
                    continue;

                double targetTime = target.StartTime;
                double missWindow = target.HitWindows.WindowFor(HitResult.Miss);

                while (firstRelevantPressIndex < replayPresses.Count && replayPresses[firstRelevantPressIndex].Time < targetTime - missWindow)
                    firstRelevantPressIndex++;

                int matchedPressIndex = -1;

                for (int i = firstRelevantPressIndex; i < replayPresses.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (pressConsumed[i])
                        continue;

                    ReplayPressEvent press = replayPresses[i];

                    if (press.Time > targetTime + missWindow)
                        break;

                    if (Vector2.Distance(press.Position, osuTarget.StackedPosition) > osuTarget.Radius)
                        continue;

                    matchedPressIndex = i;
                    break;
                }

                double timeOffsetForJudgement = 0;
                HitResult result = HitResult.Miss;

                if (matchedPressIndex >= 0)
                {
                    pressConsumed[matchedPressIndex] = true;

                    ReplayPressEvent press = replayPresses[matchedPressIndex];
                    timeOffsetForJudgement = press.Time - targetTime;
                    result = target.HitWindows.ResultFor(timeOffsetForJudgement);

                    if (result == HitResult.None)
                        result = HitResult.Miss;
                }

                var judgementResult = new JudgementResult(target, target.Judgement) { Type = result };
                JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffsetForJudgement, gameplayRate);
                scoreProcessor.ApplyResult(judgementResult);

                double clockTime = getJudgementClockTime(target, timeOffsetForJudgement, gameplayRate);
                timelineRecorder?.Record(scoreProcessor, clockTime, gameplayRate);
            }
        }

        private static double getJudgementClockTime(HitObject target, double timeOffset, double gameplayRate)
        {
            double offset = timeOffset / gameplayRate;
            double startTime = target.StartTime;
            double judgementTime = startTime + offset;

            if (target.HitWindows != null && !ReferenceEquals(target.HitWindows, HitWindows.Empty))
            {
                double missWindow = target.HitWindows.WindowFor(HitResult.Miss);

                if (missWindow > 0)
                    judgementTime = Math.Max(startTime - missWindow, judgementTime);
            }

            return judgementTime;
        }

        private static List<HitObject> collectJudgementTargets(IBeatmap beatmap, CancellationToken cancellationToken)
        {
            var targets = new List<HitObject>();

            foreach (var hitObject in beatmap.HitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                {
                    if (hitObject.NestedHitObjects.Count == 0)
                        hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, cancellationToken);
                }

                if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
                    continue;

                if (hitObject.Judgement.MaxResult == HitResult.IgnoreHit)
                    continue;

                targets.Add(hitObject);
                collectNestedJudgementTargets(hitObject, targets, cancellationToken);
            }

            return targets.OrderBy(h => h.StartTime).ToList();
        }

        private static void collectNestedJudgementTargets(HitObject hitObject, List<HitObject> targets, CancellationToken cancellationToken)
        {
            foreach (var nested in hitObject.NestedHitObjects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (nested.HitWindows != null && !ReferenceEquals(nested.HitWindows, HitWindows.Empty) && nested.Judgement.MaxResult != HitResult.IgnoreHit)
                    targets.Add(nested);

                collectNestedJudgementTargets(nested, targets, cancellationToken);
            }
        }

        private static List<ReplayPressEvent> collectPressEvents(List<OsuReplayFrame> frames)
        {
            var presses = new List<ReplayPressEvent>();
            var previousActions = new HashSet<OsuAction>();

            foreach (var frame in frames)
            {
                var currentActions = new HashSet<OsuAction>(frame.Actions.Where(isPressAction));

                foreach (var action in currentActions)
                {
                    if (previousActions.Contains(action))
                        continue;

                    presses.Add(new ReplayPressEvent(frame.Time, frame.Position));
                }

                previousActions = currentActions;
            }

            return presses;
        }

        private static bool isPressAction(OsuAction action) => action == OsuAction.LeftButton || action == OsuAction.RightButton;
    }
}
