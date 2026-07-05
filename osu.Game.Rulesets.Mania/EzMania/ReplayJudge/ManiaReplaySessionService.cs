// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// Mania 统一 replay 入口：负责 async、环境解析与共享 cache。
    /// Panel / Graph / Race 通过此接口获取 replay 判定结果，禁止自建解析或缓存。
    /// </summary>
    public sealed class ManiaReplaySessionService : EzReplaySession
    {
        protected override (Score Score, EzScoreTimeline Timeline) RunWithTimeline(
            Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
            => ManiaReplaySession.RunWithTimeline(score, beatmap, environment, cancellationToken);
    }
}
