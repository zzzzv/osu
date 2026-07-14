// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Statistics
{
    /// <summary>
    /// Obsolete 薄壳：请改用 <see cref="ManiaReplaySessionService.RunHitEventsAsync"/> /
    /// <see cref="osu.Game.EzOsuGame.Scoring.IEzReplaySession.RunHitEventsAsync"/>。
    /// </summary>
    [Obsolete("Use ManiaReplaySessionService.RunHitEventsAsync / IEzReplaySession.RunHitEventsAsync instead.")]
    public sealed class ManiaScoreHitEventGenerator
    {
        public static ManiaScoreHitEventGenerator Instance { get; } = new ManiaScoreHitEventGenerator();

        private static readonly ManiaReplaySessionService session_service = new ManiaReplaySessionService();

        public bool Validate(Score score) => ManiaReplayFrameEdgeParser.IsManiaReplay(score.Replay)
                                            && score.ScoreInfo.Ruleset.OnlineID == 3;

        public List<HitEvent> Generate(Score score, IBeatmap playableBeatmap, CancellationToken cancellationToken = default)
        {
            return session_service.RunHitEventsAsync(score, playableBeatmap, cancellationToken: cancellationToken).GetAwaiter().GetResult()
                   ?? new List<HitEvent>();
        }
    }
}
