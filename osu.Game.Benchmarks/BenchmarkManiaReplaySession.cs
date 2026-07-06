// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Replays;
using osu.Game.Scoring;

namespace osu.Game.Benchmarks
{
    /// <summary>
    /// P2-B: ManiaReplaySessionService 性能基准测试
    /// Phase 3 备忘：默认采用 timeline + QueryAtTime（EzScoreRaceService）；
    /// 若需对比增量 Session（方案 B），可在此增加 AdvanceTo 整局基准。
    /// </summary>
    public class BenchmarkManiaReplaySession : BenchmarkTest
    {
        private ManiaReplaySessionService sessionService = null!;
        private IBeatmap beatmap = null!;
        private Score score = null!;

        public override void SetUp()
        {
            base.SetUp();

            sessionService = new ManiaReplaySessionService();
            beatmap = createTestBeatmap();
            score = createTestScore(beatmap);

            var environment = GlobalConfigStore.EzConfig.ResolveEnvironment(ReplayRunPurpose.ForStored, score.ScoreInfo);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHitMode, environment.ManiaHitMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.ManiaHealthMode, environment.ManiaHealthMode);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.JudgePrecedence, environment.JudgePrecedence);
            GlobalConfigStore.EzConfig.SetValue(Ez2Setting.BmsPoorHitResultEnable, environment.BmsPoorHitResultEnable);
        }

        [Benchmark]
        public async Task<Score> BenchmarkRunAsync()
        {
            return await sessionService.RunAsync(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        [Benchmark]
        public async Task<EzScoreTimeline> BenchmarkRunTimelineAsync()
        {
            return await sessionService.RunTimelineAsync(
                score.DeepClone(),
                beatmap,
                ReplayRunPurpose.ForStored,
                CancellationToken.None
            ).ConfigureAwait(true);
        }

        private static IBeatmap createTestBeatmap()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new Beatmap
            {
                BeatmapInfo = new BeatmapInfo
                {
                    Ruleset = ruleset.RulesetInfo,
                    Difficulty = new BeatmapDifficulty
                    {
                        DrainRate = 5,
                        OverallDifficulty = 8,
                    }
                }
            };

            // 添加一些测试 note（模拟中等长度谱面）
            for (int i = 0; i < 200; i++)
            {
                beatmap.HitObjects.Add(new Note
                {
                    StartTime = i * 500, // 每 500ms 一个 note，总共 100 秒
                    Column = i % 4 // 4K 模式
                });
            }

            return beatmap;
        }

        private static Score createTestScore(IBeatmap beatmap)
        {
            var scoreInfo = new ScoreInfo
            {
                Ruleset = new ManiaRuleset().RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
                Accuracy = 0.95,
                TotalScore = 1000000,
            };

            // 创建 replay frames（与 beatmap hit objects 对应）
            var replay = new Replay();

            foreach (var hitObject in beatmap.HitObjects)
            {
                if (hitObject is Note note)
                {
                    replay.Frames.Add(new ManiaReplayFrame(note.StartTime));
                }
            }

            return new Score { ScoreInfo = scoreInfo, Replay = replay };
        }
    }
}
