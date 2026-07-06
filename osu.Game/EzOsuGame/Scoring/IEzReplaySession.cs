// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.EzOsuGame.Scoring
{
    /// <summary>
    /// 统一 Replay Session 入口：负责 async、环境解析与共享 cache。
    /// 进程内由 <see cref="EzReplaySessionRouter"/> 按 <see cref="ScoreInfo.Ruleset"/> 分发；Panel / Graph / Race 注入本接口即可。
    /// <para>环境由 Session 按 <see cref="ReplayRunPurpose"/> 经 <see cref="Configuration.Ez2ConfigManager.ResolveEnvironment"/> 解析。</para>
    /// </summary>
    public interface IEzReplaySession
    {
        /// <summary>
        /// 运行 replay 判定并回填 Score（HitEvents、Statistics 等）。
        /// </summary>
        Task<Score> RunAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 运行 replay 判定并构建时间线（用于角逐 ghost）。
        /// </summary>
        Task<EzScoreTimeline> RunTimelineAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 构建时间线，不经 Service 层 TimelineCache（<see cref="EzScoreTimelineBuilder"/> 角逐热路径）。
        /// </summary>
        Task<EzScoreTimeline> RunTimelineDirectAsync(Score score, IBeatmap beatmap, ReplayRunPurpose purpose, CancellationToken cancellationToken = default);

        /// <summary>
        /// 统一请求入口：支持更复杂的场景（如同时需要 Score + Timeline）。
        /// </summary>
        Task<ReplayRunResult> RunRequestAsync(ReplayRunRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// 从 replay 生成 HitEvents（不含 Statistics），供「补空 HitEvents」场景使用。
        /// 规则集没有 Session 时（如 OsuReplaySession 尚未实现）返回 null。
        /// </summary>
        Task<List<HitEvent>> RunHitEventsAsync(Score score, IBeatmap beatmap, CancellationToken cancellationToken = default);
    }
}
