// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Session 列级 press 目标选择（与 <see cref="ManiaLaneController.SelectPressEntry"/> 共用 fold 语义）。
    /// </summary>
    internal static class ManiaLanePressSelector
    {
        internal static LaneTargetState? SelectSessionTarget(
            IReadOnlyList<LaneTargetState> candidates,
            double time,
            EzEnumJudgePrecedence precedence,
            bool allowFallbackToEarliest)
        {
            if (candidates.Count == 0)
                return null;

            var overlapping = new List<LaneTargetState>(candidates);
            overlapping.Sort((a, b) => a.Target.StartTime.CompareTo(b.Target.StartTime));

            return precedence switch
            {
                EzEnumJudgePrecedence.Duration => OrderedHitPolicyHelper.SelectFold(
                    overlapping,
                    s => s.Judged,
                    s => s.Target.StartTime,
                    s => s.Target.HitWindows as ManiaHitWindows,
                    time,
                    comboAlgorithm: false) ?? overlapping[0],
                EzEnumJudgePrecedence.Combo => OrderedHitPolicyHelper.SelectFold(
                    overlapping,
                    s => s.Judged,
                    s => s.Target.StartTime,
                    s => s.Target.HitWindows as ManiaHitWindows,
                    time,
                    comboAlgorithm: true) ?? overlapping[0],
                _ => overlapping[0],
            };
        }
    }
}
