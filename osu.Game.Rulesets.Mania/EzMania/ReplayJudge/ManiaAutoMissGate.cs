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
    /// 热路径必须零分配、无 helper 查询（用 <see cref="ManiaHitWindows.MissEarlyWindow"/> 烘焙值）。
    /// </summary>
    internal static class ManiaAutoMissGate
    {
        internal static bool ShouldEvaluateAutoMiss(HitObject hitObject, double timeOffset)
        {
            var windows = hitObject.HitWindows;

            // Hold / Body 等 Empty 窗：官方也不用 miss 早窗判定父物体；未到 EndTime 前跳过 UpdateResult。
            if (windows == null || ReferenceEquals(windows, HitWindows.Empty))
            {
                if (timeOffset < 0)
                {
                    if (ManiaJudgeHotPathTrace.Enabled)
                        ManiaJudgeHotPathTrace.RecordAutoMissSkipped();

                    return false;
                }

                return true;
            }

            if (windows is ManiaHitWindows maniaWindows)
            {
                if (timeOffset < -maniaWindows.MissEarlyWindow)
                {
                    if (ManiaJudgeHotPathTrace.Enabled)
                        ManiaJudgeHotPathTrace.RecordAutoMissSkipped();

                    return false;
                }

                return true;
            }

            return true;
        }
    }
}
