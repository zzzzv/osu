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

        /// <summary>
        /// Session 判定用 offset（毫秒）。0 与未传等价；Graph 落定后传当前滑条值。
        /// </summary>
        public double OffsetPlusMania { get; }

        public ReplayRunRequest(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, double offsetPlusMania = 0)
        {
            Score = score;
            Beatmap = beatmap;
            Purpose = purpose;
            OffsetPlusMania = offsetPlusMania;
        }
    }
}
