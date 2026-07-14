// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class ManiaAutoMissGateTest
    {
        [Test]
        public void EmptyWindowsSkipsBeforeEndTime()
        {
            var hold = new HoldNote { StartTime = 1000, Duration = 500, Column = 0 };
            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });

            Assert.That(hold.HitWindows, Is.SameAs(HitWindows.Empty));
            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(hold, -1), Is.False);
            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(hold, 0), Is.True);
            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(hold, 10), Is.True);
        }

        [Test]
        public void ManiaWindowsSkipsBeforeMissEarly()
        {
            var note = new Note
            {
                StartTime = 1000,
                HitWindows = new ManiaHitWindows(EzEnumHitMode.Lazer)
            };
            note.HitWindows.SetDifficulty(8);

            double missEarly = ((ManiaHitWindows)note.HitWindows).WindowFor(HitResult.Miss, true);
            Assert.That(missEarly, Is.GreaterThan(0));

            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(note, -missEarly - 1), Is.False);
            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(note, -missEarly), Is.True);
            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(note, 0), Is.True);
            Assert.That(((ManiaHitWindows)note.HitWindows).MissEarlyWindow, Is.EqualTo(missEarly));
        }

        [Test]
        public void EmptyHoldsStayDeferredAcrossEntireBody()
        {
            var hold = new HoldNote { StartTime = 0, Duration = 2000, Column = 0 };
            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });

            for (int t = 0; t < 2000; t += 50)
            {
                double timeOffset = t - hold.EndTime;
                Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(hold, timeOffset), Is.False,
                    $"t={t} offset={timeOffset} should defer Empty automiss");
            }

            Assert.That(ManiaAutoMissGate.ShouldEvaluateAutoMiss(hold, 0), Is.True);
        }

        [Test]
        public void MissEarlyWindowMatchesWindowForApi()
        {
            var note = new Note
            {
                StartTime = 1000,
                HitWindows = new ManiaHitWindows(EzEnumHitMode.IIDX_HD)
            };
            note.HitWindows.SetDifficulty(8);

            var mania = (ManiaHitWindows)note.HitWindows;
            Assert.That(mania.MissEarlyWindow, Is.EqualTo(mania.WindowFor(HitResult.Miss, true)));
            Assert.That(mania.MissLateWindow, Is.EqualTo(mania.WindowFor(HitResult.Miss, false)));

            // Drawables.Apply 仅用于确认烘焙值在 Apply 后仍可用。
            var drawable = new DrawableNote();
            drawable.Apply(note);
            Assert.That(((ManiaHitWindows)note.HitWindows).MissEarlyWindow, Is.GreaterThan(0));
        }
    }
}
