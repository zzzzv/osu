// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
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
        private readonly ConcurrentDictionary<string, Lazy<Task<(Score Score, EzScoreTimeline Timeline)>>> sessionRunCache =
            new ConcurrentDictionary<string, Lazy<Task<(Score Score, EzScoreTimeline Timeline)>>>();

        protected override async Task<Score> RunScoreAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            var (resultScore, _) = await getOrRunSession(score, beatmap, environment, ReplayRunPurpose.ForStored, cancellationToken).ConfigureAwait(false);
            return resultScore;
        }

        protected override async Task<EzScoreTimeline> RunTimelineAsyncFunc(Score score, IBeatmap beatmap, IGameplayEnvironment? environment, CancellationToken cancellationToken)
        {
            var (_, timeline) = await getOrRunSession(score, beatmap, environment, ReplayRunPurpose.ForStored, cancellationToken).ConfigureAwait(false);
            return timeline;
        }

        protected override async Task<ReplayRunResult> RunCombinedAsyncFunc(ReplayRunRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var (score, timeline) = await getOrRunSession(
                    request.Score,
                    request.Beatmap,
                    request.Environment,
                    request.Purpose,
                    cancellationToken).ConfigureAwait(false);

                return new ReplayRunResult(score, timeline, hitCache: false, isValidReplay: true);
            }
            catch (OperationCanceledException)
            {
                return ReplayRunResult.Cancelled();
            }
            catch
            {
                return ReplayRunResult.InvalidReplay(request.Score);
            }
        }

        private Task<(Score Score, EzScoreTimeline Timeline)> getOrRunSession(
            Score score,
            IBeatmap beatmap,
            IGameplayEnvironment? environment,
            ReplayRunPurpose purpose,
            CancellationToken cancellationToken)
        {
            var resolvedEnv = environment ?? GlobalConfigStore.EzConfig.ResolveForReplay(score.ScoreInfo, purpose);

            return GetOrCreate(
                sessionRunCache,
                BuildCacheKey("session", score, beatmap, resolvedEnv),
                () => runSessionAsync(score, beatmap, resolvedEnv, cancellationToken));
        }

        private Task<(Score Score, EzScoreTimeline Timeline)> runSessionAsync(Score score, IBeatmap beatmap, IGameplayEnvironment environment, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (resultScore, timeline) = ManiaReplaySession.RunWithTimeline(score.DeepClone(), beatmap, environment, cancellationToken);

                score.ScoreInfo.HitEvents = resultScore.ScoreInfo.HitEvents;
                score.ScoreInfo.Statistics.Clear();

                foreach (var kvp in resultScore.ScoreInfo.Statistics)
                    score.ScoreInfo.Statistics[kvp.Key] = kvp.Value;

                score.ScoreInfo.TotalScore = resultScore.ScoreInfo.TotalScore;
                score.ScoreInfo.Accuracy = resultScore.ScoreInfo.Accuracy;
                score.ScoreInfo.MaxCombo = resultScore.ScoreInfo.MaxCombo;

                return (score, timeline);
            }, cancellationToken);
        }
    }
}
