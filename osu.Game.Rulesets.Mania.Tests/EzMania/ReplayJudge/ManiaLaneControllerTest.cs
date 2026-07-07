// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaLaneControllerTest
    {
        [Test]
        public void TestTryCreateEntryRejectsNullHitObject()
        {
            var drawable = new DrawableNote();

            Assert.That(ManiaLaneController.TryCreateEntry(drawable, out _), Is.False);
        }

        [Test]
        public void TestRegisterIfNeededSkipsDuplicate()
        {
            var controller = new ManiaLaneController();
            var drawable = createNote(1000);

            controller.RegisterIfNeeded(drawable);
            controller.RegisterIfNeeded(drawable);

            Assert.That(controller.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestUnregisterByHitObject()
        {
            var controller = new ManiaLaneController();
            var drawable = createNote(1000);

            controller.Register(drawable);
            controller.UnregisterByHitObject(drawable.HitObject);

            Assert.That(controller.Entries, Is.Empty);
        }

        [Test]
        public void TestSelectPressEntryComboMatchesSelectFold()
        {
            var controller = new ManiaLaneController();
            var early = createNote(1000);
            var late = createNote(1100);
            controller.Register(early);
            controller.Register(late);

            double pressTime = 1050;
            var overlapping = controller.CollectOverlappingEntries(pressTime);

            var expected = OrderedHitPolicyHelper.SelectFold(
                overlapping,
                e => e.IsPressJudged,
                e => e.StartTime,
                e => e.PressWindows!,
                pressTime,
                comboAlgorithm: true);

            var selected = controller.SelectPressEntry(pressTime, EzEnumJudgePrecedence.Combo, allowBmsFallbackToEarliest: false);

            Assert.That(selected, Is.SameAs(expected));
        }

        [Test]
        public void TestSelectPressEntryDurationMatchesSelectFold()
        {
            var controller = new ManiaLaneController();
            var early = createNote(1000);
            var late = createNote(1200);
            controller.Register(early);
            controller.Register(late);

            double pressTime = 1150;
            var overlapping = controller.CollectOverlappingEntries(pressTime);

            var expected = OrderedHitPolicyHelper.SelectFold(
                overlapping,
                e => e.IsPressJudged,
                e => e.StartTime,
                e => e.PressWindows!,
                pressTime,
                comboAlgorithm: false);

            var selected = controller.SelectPressEntry(pressTime, EzEnumJudgePrecedence.Duration, allowBmsFallbackToEarliest: false);

            Assert.That(selected, Is.SameAs(expected));
        }

        private static DrawableNote createNote(double startTime)
        {
            var note = new Note
            {
                StartTime = startTime,
                HitWindows = new ManiaHitWindows(EzEnumHitMode.Lazer)
            };

            var drawable = new DrawableNote();
            drawable.Apply(note);
            return drawable;
        }
    }
}
