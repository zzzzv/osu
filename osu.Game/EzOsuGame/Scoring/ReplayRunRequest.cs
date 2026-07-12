// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// Replay Session 运行请求：由 <see cref="ReplayRunPurpose"/> 驱动环境解析。
    /// </summary>
    public sealed class ReplayRunRequest
    {
        public Score Score { get; }
        public IBeatmap Beatmap { get; }
        public ReplayRunPurpose Purpose { get; }
        public bool IncludeGlobalManiaOffset { get; init; }

        public ReplayRunRequest(Score score, IBeatmap beatmap, ReplayRunPurpose purpose)
        {
            Score = score;
            Beatmap = beatmap;
            Purpose = purpose;
        }
    }
}
