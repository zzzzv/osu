// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Extensions.EnumExtensions;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Extensions;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Mania HitMode 统一工具：元数据、有效判定集合、成绩 Statistics 展示（按 Score 嵌入环境）。
    /// </summary>
    public static class ManiaHitModeCatalog
    {
        public static EzEnumHitMode ResolveHitMode(ScoreInfo score)
        {
            if (score.TryGetManiaGameplayModes(out int hitMode, out _))
                return (EzEnumHitMode)hitMode;

            return EzEnumHitMode.Lazer;
        }

        public static bool IsBMSHitMode(EzEnumHitMode hitMode)
        {
            return hitMode == EzEnumHitMode.IIDX_HD ||
                   hitMode == EzEnumHitMode.LR2_HD ||
                   hitMode == EzEnumHitMode.Raja_NM;
        }

        /// <summary>
        /// 在指定 HitMode 下，<see cref="HitResult.Meh"/> 是否打断 Combo。
        /// BMS / O2Jam / EZ2AC 三种模式下 Meh 断 Combo，其余不断。
        /// </summary>
        public static bool MehBreaksCombo(EzEnumHitMode hitMode)
        {
            switch (hitMode)
            {
                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                case EzEnumHitMode.O2Jam:
                case EzEnumHitMode.EZ2AC:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 早按侧 KPoor（空出し）：是否仍在机台规定的「note 判定时刻前可识别」范围内。
        /// osu 中 <paramref name="timeOffsetFromHit"/> = 当前时间 - 物件判定时刻，负值为提前按下。
        /// IIDX 无提前上限；LR2 为判定时刻前 1000ms 内；Raja 为前 500ms 内。
        /// 晚按侧 KPoor 不由本方法约束，由 Bad 外与 <c>badLate</c> 衔接的逻辑单独处理。
        /// </summary>
        public static bool IsWithinEarlyKPoorRecognitionWindow(EzEnumHitMode mode, double timeOffsetFromHit)
        {
            if (timeOffsetFromHit >= 0)
                return true;

            if (!IsBMSHitMode(mode))
                return true;

            double maxEarlyMs = mode switch
            {
                EzEnumHitMode.LR2_HD => 1000,
                EzEnumHitMode.Raja_NM => 500,
                _ => double.PositiveInfinity,
            };

            return timeOffsetFromHit >= -maxEarlyMs;
        }

        public static IEnumerable<HitResult> GetValidHitResults(EzEnumHitMode mode)
        {
            switch (mode)
            {
                case EzEnumHitMode.O2Jam:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Good,
                        HitResult.Meh,
                        HitResult.Miss,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };

                case EzEnumHitMode.EZ2AC:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Great,
                        HitResult.Good,
                        HitResult.Meh,
                        HitResult.Miss,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };

                case EzEnumHitMode.IIDX_HD:
                case EzEnumHitMode.LR2_HD:
                case EzEnumHitMode.Raja_NM:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Great,
                        HitResult.Good,
                        HitResult.Meh,
                        HitResult.Miss,
                        HitResult.Poor,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };

                case EzEnumHitMode.Malody_E:
                case EzEnumHitMode.Malody_B:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Great,
                        HitResult.Good,
                        HitResult.Miss,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };

                case EzEnumHitMode.Lazer:
                case EzEnumHitMode.Classic:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Great,
                        HitResult.Good,
                        HitResult.Ok,
                        HitResult.Meh,
                        HitResult.Miss,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };

                default:
                    return new[]
                    {
                        HitResult.Perfect,
                        HitResult.Great,
                        HitResult.Good,
                        HitResult.Ok,
                        HitResult.Meh,
                        HitResult.Miss,
                        HitResult.IgnoreHit,
                        HitResult.ComboBreak,
                        HitResult.IgnoreMiss,
                    };
            }
        }

        public static IEnumerable<HitResult> GetValidHitResults()
        {
            var mode = GlobalConfigStore.EzConfig.Get<EzEnumHitMode>(Ez2Setting.ManiaHitMode);
            return GetValidHitResults(mode);
        }

        /// <summary>
        /// 与 vanilla <see cref="ScoreInfo.GetStatisticsForDisplay"/> 相同规则，但 HitMode 取自 Score 嵌入环境。
        /// </summary>
        public static IEnumerable<HitResultDisplayStatistic> GetStatisticsForDisplay(ScoreInfo score)
        {
            if (score.Ruleset.OnlineID != 3)
            {
                foreach (var stat in score.GetStatisticsForDisplay())
                    yield return stat;

                yield break;
            }

            var hitMode = ResolveHitMode(score);
            var validResults = GetValidHitResults(hitMode).ToHashSet();

            foreach (var result in EnumExtensions.GetValuesInOrder<HitResult>())
            {
                switch (result)
                {
                    case HitResult.None:
                    case HitResult.IgnoreHit:
                    case HitResult.IgnoreMiss:
                    case HitResult.ComboBreak:
                    case HitResult.LargeTickMiss:
                    case HitResult.SmallTickMiss:
                        continue;
                }

                if (result != HitResult.Miss && !validResults.Contains(result))
                    continue;

                int value = score.Statistics.GetValueOrDefault(result);

                switch (result)
                {
                    case HitResult.SmallTickHit:
                    case HitResult.LargeTickHit:
                    case HitResult.SliderTailHit:
                    case HitResult.LargeBonus:
                    case HitResult.SmallBonus:
                        if (score.MaximumStatistics.TryGetValue(result, out int count) && count > 0)
                            yield return new HitResultDisplayStatistic(result, value, count, result.GetHitModeDisplayName(hitMode));

                        break;

                    default:
                        yield return new HitResultDisplayStatistic(result, value, null, result.GetHitModeDisplayName(hitMode));

                        break;
                }
            }
        }

        public static Dictionary<HitResult, int> StatisticsToCounts(IEnumerable<HitResultDisplayStatistic> statistics)
        {
            var counts = new Dictionary<HitResult, int>();

            foreach (var stat in statistics)
            {
                if (stat.Count > 0)
                    counts[stat.Result] = stat.Count;
            }

            return counts;
        }
    }
}
