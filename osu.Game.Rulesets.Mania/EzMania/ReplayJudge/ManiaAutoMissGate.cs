// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mania.EzMania.Diagnostics;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// auto-miss 早退：物件尚未进入 miss 早窗时不跑 Ez 判定链（AUTO-MISS-GATE）。
    /// </summary>
    internal static class ManiaAutoMissGate
    {
        internal static bool ShouldEvaluateAutoMiss(HitObject hitObject, double timeOffset)
        {
            // Hold / Body 等 Empty 窗：官方也不用 miss 早窗判定父物体；未到 EndTime 前跳过 UpdateResult。
            if (hitObject.HitWindows == null || ReferenceEquals(hitObject.HitWindows, HitWindows.Empty))
            {
                if (timeOffset < 0)
                {
                    if (ManiaJudgeHotPathTrace.Enabled)
                        ManiaJudgeHotPathTrace.RecordAutoMissSkipped();

                    return false;
                }

                return true;
            }

            if (hitObject.HitWindows is not ManiaHitWindows maniaWindows)
                return true;

            double missEarly = maniaWindows.WindowFor(HitResult.Miss, true);

            if (timeOffset < -missEarly)
            {
                if (ManiaJudgeHotPathTrace.Enabled)
                    ManiaJudgeHotPathTrace.RecordAutoMissSkipped();

                return false;
            }

            return true;
        }
    }
}
