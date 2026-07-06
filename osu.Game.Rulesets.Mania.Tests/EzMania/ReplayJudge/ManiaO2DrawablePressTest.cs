// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.EzCurrentHitObject;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaO2DrawablePressTest
    {
        [Test]
        public void TestInitializeRuntimeBeforeWindowBakeUsesTimingPointBpm()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                BeatmapInfo = { BPM = 120 },
                ControlPointInfo = new ControlPointInfo(),
            };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 1000 });

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            ManiaWindowBaker.Align(beatmap, environment);

            Assert.That(O2HitModeExtension.GetBPMAtTime(1000), Is.EqualTo(75).Within(1e-3));
        }

        [Test]
        public void TestDrawablePressUsesHitWindowsAfterPressTimeBpmSync()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                BeatmapInfo = { BPM = 120 },
                ControlPointInfo = new ControlPointInfo(),
            };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var note = new Note { StartTime = 1000, Column = 0 };
            note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            beatmap.HitObjects.Add(note);

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            ManiaWindowBaker.Align(beatmap, environment);

            var windows = (ManiaHitWindows)note.HitWindows!;
            windows.UpdateO2JamBpmFromTime(1000);

            var outcome = O2HitModeJudgement.Instance.EvaluateDrawableNotePress(
                0,
                windows,
                new O2HitModeJudgement.DrawableNoteContext
                {
                    CurrentTime = 1000,
                    PillCheckPassed = true,
                },
                new ManiaReplayJudgementState());

            Assert.That(outcome, Is.Not.Null);
            Assert.That(outcome!.Value.Kind, Is.EqualTo(ManiaNoteJudgementOutcomeKind.Apply));
            Assert.That(outcome.Value.Result, Is.EqualTo(HitResult.Perfect));
        }

        [Test]
        public void TestSessionPressUsesPressTimeBpmExtension()
        {
            var ruleset = new ManiaRuleset();
            var beatmap = new TestBeatmap(ruleset.RulesetInfo)
            {
                BeatmapInfo = { BPM = 120 },
                ControlPointInfo = new ControlPointInfo(),
            };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var note = new Note { StartTime = 1000, Column = 0 };
            note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var environment = ReplayJudgeTestConfig.Create(EzEnumHitMode.O2Jam, EzEnumHealthMode.O2JamNormal);
            ManiaWindowBaker.Align(beatmap, environment);

            var outcome = O2HitModeJudgement.Instance.EvaluatePress(
                0,
                note.HitWindows!,
                new O2HitModeJudgement.NotePressContext
                {
                    RawOffset = 0,
                    Bpm = 240,
                    UsePressTimeBpmForJudgement = true,
                    PillModeEnabled = true,
                    State = new ManiaReplayJudgementState(),
                });

            Assert.That(outcome.Kind, Is.EqualTo(ManiaNoteJudgementOutcomeKind.Apply));
            Assert.That(outcome.Result, Is.EqualTo(HitResult.Perfect));
        }
    }
}
