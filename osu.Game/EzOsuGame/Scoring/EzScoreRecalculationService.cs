// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Mania 成绩 Session 重算并写回 Realm；非 Mania 或无 replay 时回退 vanilla <see cref="ScoreManager.Recalculate"/>。
    /// </summary>
    public static class EzScoreRecalculationService
    {
        public static async Task RecalculateAsync(
            ScoreManager scoreManager,
            BeatmapManager beatmapManager,
            IEzReplaySession replaySession,
            ScoreInfo scoreInfo,
            ReplayRunPurpose purpose,
            CancellationToken cancellationToken = default)
        {
            if (scoreInfo.Ruleset.OnlineID != 3)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var databasedScore = scoreManager.GetScore(scoreInfo);

            if (databasedScore?.Replay == null || databasedScore.Replay.Frames.Count == 0)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var workingBeatmap = beatmapManager.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            if (workingBeatmap is DummyWorkingBeatmap)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var playableBeatmap = workingBeatmap.GetPlayableBeatmap(scoreInfo.Ruleset, scoreInfo.Mods);

            if (playableBeatmap.HitObjects.Count == 0)
            {
                scoreManager.Recalculate(scoreInfo);
                return;
            }

            var environment = GlobalConfigStore.EzConfig.ResolveForReplay(scoreInfo, purpose) with { OffsetPlusMania = 0 };

            var resultScore = await replaySession.RunAsync(
                databasedScore.DeepClone(),
                playableBeatmap,
                environment,
                purpose,
                cancellationToken).ConfigureAwait(false);

            scoreManager.ApplyEzSessionRecalculation(scoreInfo, resultScore.ScoreInfo, purpose, environment);
        }
    }
}
