// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;

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
            var drawable = new DrawableNote();
            drawable.Apply(new Note { StartTime = 1000 });

            controller.RegisterIfNeeded(drawable);
            controller.RegisterIfNeeded(drawable);

            Assert.That(controller.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestUnregisterByHitObject()
        {
            var controller = new ManiaLaneController();
            var note = new Note { StartTime = 1000 };
            var drawable = new DrawableNote();
            drawable.Apply(note);

            controller.Register(drawable);
            controller.UnregisterByHitObject(note);

            Assert.That(controller.Entries, Is.Empty);
        }
    }
}
