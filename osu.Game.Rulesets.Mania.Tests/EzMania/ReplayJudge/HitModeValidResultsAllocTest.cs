// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    [TestFixture]
    public class HitModeValidResultsAllocTest
    {
        [Test]
        public void IsHitResultValidForModeDoesNotAllocateHotPath()
        {
            // warm
            HitModeHelper.IsHitResultValidForMode(EzEnumHitMode.Lazer, HitResult.Perfect);

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 50_000; i++)
            {
                HitModeHelper.IsHitResultValidForMode(EzEnumHitMode.Lazer, HitResult.Perfect);
                HitModeHelper.IsHitResultValidForMode(EzEnumHitMode.IIDX_HD, HitResult.Poor);
                HitModeHelper.IsHitResultValidForMode(EzEnumHitMode.O2Jam, HitResult.Good);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.That(after - before, Is.EqualTo(0), $"Expected zero alloc, got {after - before}B");
        }

        [Test]
        public void GetHitModeValidHitResultsReturnsCachedInstance()
        {
            var a = HitModeHelper.GetHitModeValidHitResults(EzEnumHitMode.Lazer);
            var b = HitModeHelper.GetHitModeValidHitResults(EzEnumHitMode.Lazer);
            Assert.That(ReferenceEquals(a, b), Is.True);
        }
    }
}
