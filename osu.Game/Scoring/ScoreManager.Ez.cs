// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Scoring
{
    public partial class ScoreManager
    {
        /// <summary>
        /// Ez2Lazer: 用户显式「成绩重算」— 将 Session 产出写回 Realm 与调用方快照。
        /// </summary>
        public void ApplyEzSessionRecalculation(ScoreInfo scoreInfo, ScoreInfo sessionInfo, ReplayRunPurpose purpose, GameplayEnvironment environment)
        {
            double? accuracy = null;
            ScoreRank? rank = null;
            int? maxCombo = null;
            long? totalScore = null;
            Dictionary<HitResult, int>? statistics = null;
            Dictionary<HitResult, int>? maximumStatistics = null;
            int? maniaHitMode = null;
            int? maniaHealthMode = null;

            Realm.Write(realm =>
            {
                var managed = realm.Find<ScoreInfo>(scoreInfo.ID);
                if (managed == null)
                    return;

                managed.Statistics.Clear();
                foreach (var kvp in sessionInfo.Statistics)
                    managed.Statistics[kvp.Key] = kvp.Value;

                managed.MaximumStatistics.Clear();
                foreach (var kvp in sessionInfo.MaximumStatistics)
                    managed.MaximumStatistics[kvp.Key] = kvp.Value;

                managed.Accuracy = sessionInfo.Accuracy;
                managed.Rank = sessionInfo.Rank;
                managed.MaxCombo = sessionInfo.MaxCombo;
                managed.TotalScore = sessionInfo.TotalScore;
                managed.TotalScoreWithoutMods = sessionInfo.TotalScoreWithoutMods;
                managed.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;

                if (purpose == ReplayRunPurpose.ForLive)
                {
                    managed.ManiaHitMode = (int)environment.ManiaHitMode;
                    managed.ManiaHealthMode = (int)environment.ManiaHealthMode;
                }

                if (managed.OnlineID > 0 || managed.LegacyOnlineID > 0)
                {
                    managed.OnlineID = -1;
                    managed.LegacyOnlineID = -1;
                }

                accuracy = managed.Accuracy;
                rank = managed.Rank;
                maxCombo = managed.MaxCombo;
                totalScore = managed.TotalScore;
                statistics = managed.Statistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                maximumStatistics = managed.MaximumStatistics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                maniaHitMode = managed.ManiaHitMode;
                maniaHealthMode = managed.ManiaHealthMode;
            });

            if (accuracy == null)
                return;

            scoreInfo.Accuracy = accuracy.Value;
            scoreInfo.Rank = rank!.Value;
            scoreInfo.MaxCombo = maxCombo!.Value;
            scoreInfo.TotalScore = totalScore!.Value;
            scoreInfo.TotalScoreWithoutMods = sessionInfo.TotalScoreWithoutMods;
            scoreInfo.TotalScoreVersion = LegacyScoreEncoder.LATEST_VERSION;

            scoreInfo.Statistics.Clear();
            foreach (var kvp in statistics!)
                scoreInfo.Statistics[kvp.Key] = kvp.Value;

            scoreInfo.MaximumStatistics.Clear();
            foreach (var kvp in maximumStatistics!)
                scoreInfo.MaximumStatistics[kvp.Key] = kvp.Value;

            if (purpose == ReplayRunPurpose.ForLive)
            {
                scoreInfo.ManiaHitMode = maniaHitMode!.Value;
                scoreInfo.ManiaHealthMode = maniaHealthMode!.Value;
            }

            if (scoreInfo.OnlineID > 0 || scoreInfo.LegacyOnlineID > 0)
            {
                scoreInfo.OnlineID = -1;
                scoreInfo.LegacyOnlineID = -1;
            }
        }
    }
}
