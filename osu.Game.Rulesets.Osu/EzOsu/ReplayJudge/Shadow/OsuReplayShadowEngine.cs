// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.EzOsu.ReplayJudge.Shadow
{
    /// <summary>
    /// Osu 影子判定主循环：replay 时钟 → Shadow 状态 → 一遍 <see cref="JudgementProcessor.ApplyResult"/>。
    /// </summary>
    /// <remarks>
    /// OSL-010：S1 Circle + nested tick；S2 Slider；S3 Spinner。设计见 REPLAY_JUDGE_SHADOW.md。
    /// </remarks>
    // TODO(EZ-SR-OSL-010-S2): Slider head/tail/tick/repeat tracking — ShadowSliderState。
    // TODO(EZ-SR-OSL-010-S3): Spinner 转速与 tick — ShadowSpinnerState。
    internal static class OsuReplayShadowEngine
    {
        internal static void Run(
            Score score,
            IBeatmap beatmap,
            ScoreProcessor scoreProcessor,
            double gameplayRate,
            OsuReplayTimelineRecorder? timelineRecorder,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(score.Replay);

            var frames = score.Replay.Frames.OfType<OsuReplayFrame>().OrderBy(f => f.Time).ToList();

            if (frames.Count == 0)
                return;

            var cursor = new OsuShadowReplayCursor(frames);
            var scheduler = OsuReplayObjectScheduler.Create(beatmap, cancellationToken);
            var pressEdges = OsuShadowReplayCursor.CollectPressEdges(frames);
            int nextPressIndex = 0;

            var simulationTimes = OsuShadowReplayCursor.CollectSimulationTimes(frames, scheduler.CollectMissDeadlines());

            timelineRecorder?.RecordInitial(scoreProcessor, gameplayRate);

            foreach (double time in simulationTimes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor.Seek(time);

                while (nextPressIndex < pressEdges.Count && pressEdges[nextPressIndex].Time <= time)
                {
                    var edge = pressEdges[nextPressIndex++];
                    scheduler.ProcessPress(edge.Time, edge.Position, (target, result, timeOffset, hitPosition) =>
                        applyJudgement(target.HitObject, result, timeOffset, hitPosition, gameplayRate, scoreProcessor, timelineRecorder));
                }

                scheduler.ProcessExpiredMisses(time, (target, result, timeOffset, hitPosition) =>
                    applyJudgement(target.HitObject, result, timeOffset, hitPosition, gameplayRate, scoreProcessor, timelineRecorder));
            }

            scheduler.FinalizeRemainingMisses((target, result, timeOffset, hitPosition) =>
                applyJudgement(target.HitObject, result, timeOffset, hitPosition, gameplayRate, scoreProcessor, timelineRecorder));
        }

        private static void applyJudgement(
            HitObject hitObject,
            HitResult result,
            double timeOffset,
            Vector2? cursorPositionAtHit,
            double gameplayRate,
            ScoreProcessor scoreProcessor,
            OsuReplayTimelineRecorder? timelineRecorder)
        {
            JudgementResult judgementResult = hitObject is HitCircle
                ? new OsuHitCircleJudgementResult(hitObject, hitObject.Judgement)
                : new JudgementResult(hitObject, hitObject.Judgement);

            judgementResult.Type = result;

            if (judgementResult is OsuHitCircleJudgementResult circleResult)
                circleResult.CursorPositionAtHit = cursorPositionAtHit;

            JudgementResultTimingHelper.ApplyTiming(judgementResult, timeOffset, gameplayRate);
            scoreProcessor.ApplyResult(judgementResult);

            double clockTime = getJudgementClockTime(hitObject, timeOffset, gameplayRate);
            timelineRecorder?.Record(scoreProcessor, clockTime, gameplayRate);
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
    }
}
