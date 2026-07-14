// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Diagnostics;

namespace osu.Game.Rulesets.Mania.Tests.EzMania.ReplayJudge
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：10 列叠 LN 热路径，PeakKps 20→100。
    /// CI 烟测 + 打印剖面；不替代实机 SwapBuffer。
    /// </summary>
    [TestFixture]
    public class ManiaLaneHotPathMicroBenchTest
    {
        [TestCase(20)]
        [TestCase(50)]
        [TestCase(100)]
        public void TestTenKeyDenseLnPeakKpsSweep(int peakKps)
        {
            var result = new ManiaLaneHotPathWorkload
            {
                Keys = 10,
                PeakKps = peakKps,
                ConcurrentAlivePerColumn = 8,
                DurationMs = 2000,
                FrameStepMs = 1,
                Precedence = EzEnumJudgePrecedence.Combo,
            }.Run();

            TestContext.WriteLine(result.ToString());

            Assert.That(result.FrameCount, Is.EqualTo(2000));
            Assert.That(result.PressCount, Is.GreaterThan(0));
            Assert.That(result.SelectPressCalls, Is.EqualTo(result.PressCount));
            // 2000 帧 × (10*8 note + 10*8 hold) gate ≈ 320k+；墙钟应远小于实时。
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(2_000),
                $"Hot path too slow for peakKps={peakKps}: {result}");
        }

        [Test]
        public void TestPeakKpsScaleIsNearLinearOnSelectCalls()
        {
            var results = new Dictionary<int, ManiaLaneHotPathWorkloadResult>();

            foreach (int kps in new[] { 20, 50, 100 })
            {
                results[kps] = new ManiaLaneHotPathWorkload
                {
                    Keys = 10,
                    PeakKps = kps,
                    ConcurrentAlivePerColumn = 8,
                    DurationMs = 2000,
                    FrameStepMs = 1,
                    Precedence = EzEnumJudgePrecedence.Combo,
                }.Run();

                TestContext.WriteLine(results[kps].ToString());
            }

            // 齐按 chord：pressCount ∝ PeakKps（Duration 固定）。
            double ratio50 = (double)results[50].PressCount / results[20].PressCount;
            double ratio100 = (double)results[100].PressCount / results[20].PressCount;

            Assert.That(ratio50, Is.EqualTo(2.5).Within(0.2));
            Assert.That(ratio100, Is.EqualTo(5.0).Within(0.3));

            // 墙钟应随 Select 增加，但 gate 占主导时斜率有限；抓「100kps 爆炸式」回退。
            Assert.That(results[100].ElapsedMilliseconds, Is.LessThan(results[20].ElapsedMilliseconds * 8 + 50),
                $"Non-linear blow-up 20→100 kps: {results[20]} | {results[100]}");
        }

        [TestCase(EzEnumJudgePrecedence.Earliest)]
        [TestCase(EzEnumJudgePrecedence.Combo)]
        [TestCase(EzEnumJudgePrecedence.Duration)]
        public void TestPrecedenceVariantsAt100Kps(EzEnumJudgePrecedence precedence)
        {
            var result = new ManiaLaneHotPathWorkload
            {
                Keys = 10,
                PeakKps = 100,
                ConcurrentAlivePerColumn = 8,
                DurationMs = 2000,
                Precedence = precedence,
            }.Run();

            TestContext.WriteLine(result.ToString());
            Assert.That(result.ElapsedMilliseconds, Is.LessThan(2_000), result.ToString());
        }
    }
}
