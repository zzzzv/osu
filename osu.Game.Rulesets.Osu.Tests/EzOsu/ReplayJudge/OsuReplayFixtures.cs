// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Scoring;
using osu.Game.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.EzOsu.ReplayJudge
{
    internal static class OsuReplayFixtures
    {
        public static (Score score, IBeatmap beatmap, IGameplayEnvironment environment) CreateTwoCircleTap()
        {
            var ruleset = new OsuRuleset();
            var testBeatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                HitObjects = new List<HitObject>
                {
                    new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) },
                    new HitCircle { StartTime = 2000, Position = new Vector2(300, 200) },
                },
            };

            var beatmap = ruleset.CreateBeatmapConverter(testBeatmap).Convert();
            var beatmapProcessor = ruleset.CreateBeatmapProcessor(beatmap);
            beatmapProcessor.PreProcess();

            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty);

            beatmapProcessor.PostProcess();
            var circle1 = (HitCircle)beatmap.HitObjects[0];
            var circle2 = (HitCircle)beatmap.HitObjects[1];

            var scoreInfo = new ScoreInfo
            {
                Ruleset = ruleset.RulesetInfo,
                BeatmapInfo = beatmap.BeatmapInfo,
            };

            var replay = new Replay
            {
                Frames = new List<ReplayFrame>
                {
                    new OsuReplayFrame(1000, circle1.StackedPosition, OsuAction.LeftButton),
                    new OsuReplayFrame(1100, circle1.StackedPosition),
                    new OsuReplayFrame(2000, circle2.StackedPosition, OsuAction.LeftButton),
                },
            };

            var score = new Score { ScoreInfo = scoreInfo, Replay = replay };
            var environment = GlobalConfigStore.EzConfig.ResolveForReplay(scoreInfo, ReplayRunPurpose.ForStored);

            return (score, beatmap, environment);
        }
    }
}
