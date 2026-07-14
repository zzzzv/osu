// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.Diagnostics
{
    /// <summary>
    /// DRAWABLE-MICRO-BENCH：无 Host 的列热路径合成负载。
    /// 覆盖 SelectPress / CollectOverlapping / AutoMissGate / pressTimes 列表增减，
    /// 不覆盖 SwapBuffer / 选歌 BDSP（那些需实机或其它 harness）。
    /// </summary>
    public sealed class ManiaLaneHotPathWorkload
    {
        public int Keys { get; init; } = 10;

        /// <summary>全键盘峰值按键吞吐（键次/秒）。10 列齐按 chord 的周期 = Keys*1000/PeakKps ms。</summary>
        public int PeakKps { get; init; } = 50;

        /// <summary>每列同时存活的叠 LN/note（进 miss 窗重叠区）。</summary>
        public int ConcurrentAlivePerColumn { get; init; } = 8;

        public int DurationMs { get; init; } = 2000;

        public int FrameStepMs { get; init; } = 1;

        public EzEnumJudgePrecedence Precedence { get; init; } = EzEnumJudgePrecedence.Combo;

        public ManiaLaneHotPathWorkloadResult Run()
        {
            if (Keys < 1)
                throw new ArgumentOutOfRangeException(nameof(Keys));
            if (PeakKps < 1)
                throw new ArgumentOutOfRangeException(nameof(PeakKps));

            var lanes = new ManiaLaneController[Keys];
            var pressHistories = new List<double>[Keys];
            var gateObjects = new List<HitObject>(Keys * ConcurrentAlivePerColumn * 2);

            double mid = DurationMs * 0.5;
            double retentionMs = 15_000;

            for (int col = 0; col < Keys; col++)
            {
                var lane = new ManiaLaneController();
                lane.ConfigureMissCollection(EzEnumHitMode.Lazer, overallDifficulty: 8);
                pressHistories[col] = new List<double>(PeakKps);

                for (int i = 0; i < ConcurrentAlivePerColumn; i++)
                {
                    double start = mid - 180 - i * 28;
                    var note = createNote(start);
                    lane.Register(note);

                    // Empty-窗 LN 父体：只喂 AutoMissGate（与 Register 路径分离）。
                    var hold = createHold(start, duration: 400);
                    gateObjects.Add(hold);
                    gateObjects.Add(note.HitObject);
                }

                lanes[col] = lane;
            }

            int chordIntervalMs = Math.Max(1, (int)Math.Round(Keys * 1000.0 / PeakKps));
            int nextChordAt = 0;

            int frames = 0;
            int presses = 0;
            int gateCalls = 0;
            int selectCalls = 0;

            var sw = Stopwatch.StartNew();

            for (int t = 0; t < DurationMs; t += FrameStepMs)
            {
                frames++;

                for (int i = 0; i < gateObjects.Count; i++)
                {
                    var obj = gateObjects[i];
                    double end = (obj as IHasDuration)?.EndTime ?? obj.StartTime;
                    ManiaAutoMissGate.ShouldEvaluateAutoMiss(obj, t - end);
                    gateCalls++;
                }

                if (t >= nextChordAt)
                {
                    for (int col = 0; col < Keys; col++)
                    {
                        lanes[col].SelectPressEntry(t, Precedence, allowBmsFallbackToEarliest: false, poorEnabled: false);
                        selectCalls++;
                        presses++;

                        var history = pressHistories[col];
                        history.Add(t);
                        trimPressHistory(history, t, retentionMs);
                    }

                    nextChordAt += chordIntervalMs;
                }
            }

            sw.Stop();

            return new ManiaLaneHotPathWorkloadResult(
                sw.ElapsedMilliseconds,
                frames,
                presses,
                gateCalls,
                selectCalls,
                Keys,
                PeakKps,
                ConcurrentAlivePerColumn,
                Precedence);
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

        private static HoldNote createHold(double startTime, double duration)
        {
            var hold = new HoldNote
            {
                StartTime = startTime,
                Duration = duration,
                Column = 0,
            };

            // Nested Head/Body/Tail for Empty-窗语义。
            hold.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { OverallDifficulty = 8 });
            return hold;
        }

        private static void trimPressHistory(List<double> pressTimes, double now, double retentionMs)
        {
            double cutoff = now - retentionMs;
            int removeCount = 0;

            while (removeCount < pressTimes.Count && pressTimes[removeCount] < cutoff)
                removeCount++;

            if (removeCount > 0)
                pressTimes.RemoveRange(0, removeCount);
        }
    }

    public readonly struct ManiaLaneHotPathWorkloadResult
    {
        public long ElapsedMilliseconds { get; }
        public int FrameCount { get; }
        public int PressCount { get; }
        public int AutoMissGateCalls { get; }
        public int SelectPressCalls { get; }
        public int Keys { get; }
        public int PeakKps { get; }
        public int ConcurrentAlivePerColumn { get; }
        public EzEnumJudgePrecedence Precedence { get; }

        public double MillisecondsPerFrame => FrameCount == 0 ? 0 : (double)ElapsedMilliseconds / FrameCount;

        public double EffectivePressesPerSecond =>
            ElapsedMilliseconds <= 0 ? 0 : PressCount * 1000.0 / ElapsedMilliseconds;

        public ManiaLaneHotPathWorkloadResult(
            long elapsedMilliseconds,
            int frameCount,
            int pressCount,
            int autoMissGateCalls,
            int selectPressCalls,
            int keys,
            int peakKps,
            int concurrentAlivePerColumn,
            EzEnumJudgePrecedence precedence)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            FrameCount = frameCount;
            PressCount = pressCount;
            AutoMissGateCalls = autoMissGateCalls;
            SelectPressCalls = selectPressCalls;
            Keys = keys;
            PeakKps = peakKps;
            ConcurrentAlivePerColumn = concurrentAlivePerColumn;
            Precedence = precedence;
        }

        public override string ToString()
            => $"keys={Keys} peakKps={PeakKps} alive/col={ConcurrentAlivePerColumn} prec={Precedence} "
               + $"elapsed={ElapsedMilliseconds}ms frames={FrameCount} presses={PressCount} "
               + $"gate={AutoMissGateCalls} select={SelectPressCalls} "
               + $"ms/frame={MillisecondsPerFrame:F4} press/s(wall)={EffectivePressesPerSecond:F0}";
    }
}
