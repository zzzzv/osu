// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaHoldTailJudgementTest
    {
        [TestCase(EzEnumHitMode.O2Jam)]
        [TestCase(EzEnumHitMode.IIDX_HD)]
        [TestCase(EzEnumHitMode.EZ2AC)]
        public void TestPerfectHoldSessionHasZeroComboBreak(EzEnumHitMode hitMode)
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateHoldPerfectForHitMode(hitMode);
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.True);
        }

        [Test]
        public void TestMalodyPerfectHoldSessionHasZeroComboBreak()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldPerfect();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.EqualTo(0));
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is HeadNote), Is.True);
            Assert.That(result.ScoreInfo.HitEvents.Any(e => e.HitObject is TailNote), Is.False);
        }

        [Test]
        public void TestEarlyReleaseSessionProducesComboBreak()
        {
            var (score, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldEarlyRelease();
            var result = ManiaReplaySession.Run(score, beatmap, environment);

            Assert.That(result.ScoreInfo.Statistics.TryGetValue(HitResult.ComboBreak, out int comboBreak) ? comboBreak : 0, Is.GreaterThan(0));
        }

        [Test]
        public void TestMalodyTailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = HitModeReplayFixtures.CreateMalodyHoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.ManiaHitMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement, Is.TypeOf<MalodyTailJudgement>());
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.IgnoreHit));
        }

        [Test]
        public void TestO2TailJudgementAfterEnvironmentApply()
        {
            var (_, beatmap, environment) = HitModeReplayFixtures.CreateO2HoldPerfect();
            ManiaEnvironmentJudgements.ApplyToBeatmap(beatmap, environment.ManiaHitMode);

            var tail = beatmap.HitObjects.OfType<HoldNote>().Single().Tail;
            Assert.That(tail.Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
            Assert.That(tail.Judgement.MinResult, Is.EqualTo(HitResult.Miss));
        }
    }
}
