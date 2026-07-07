// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.Rulesets.Mania.EzMania.Helper;
using osu.Game.Rulesets.Mania.EzMania.ReplayJudge.Mappings;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 列内 note-lock 状态机：有序目标 + 游标。全 JudgePrecedence 下 Drawable / Session 共用判定语义。
    /// </summary>
    public sealed class ManiaLaneController
    {
        private readonly List<ManiaLaneEntry> entries = new List<ManiaLaneEntry>();
        private readonly Dictionary<DrawableHitObject, int> drawableIndices = new Dictionary<DrawableHitObject, int>();
        private int cursor;

        public IReadOnlyList<ManiaLaneEntry> Entries => entries;

        public void Register(DrawableHitObject drawable)
        {
            if (!TryCreateEntry(drawable, out var entry))
                return;

            if (drawableIndices.ContainsKey(drawable))
                Unregister(drawable);

            int insertIndex = entries.BinarySearch(entry, ManiaLaneEntry.StartTimeComparer.INSTANCE);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;

            entries.Insert(insertIndex, entry);
            rebuildDrawableIndices();

            if (insertIndex < cursor)
                cursor++;
        }

        public void RegisterIfNeeded(DrawableHitObject drawable)
        {
            if (drawableIndices.ContainsKey(drawable))
                return;

            Register(drawable);
        }

        public void Unregister(DrawableHitObject drawable)
        {
            if (!drawableIndices.TryGetValue(drawable, out int index))
                return;

            entries.RemoveAt(index);
            rebuildDrawableIndices();

            if (index < cursor)
                cursor--;
        }

        public void UnregisterByHitObject(HitObject hitObject)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Drawable.HitObject == hitObject)
                    Unregister(entries[i].Drawable);
            }
        }

        public void NotifyJudged(DrawableHitObject drawable)
        {
            drawable = resolveJudgedDrawable(drawable);

            if (!drawableIndices.ContainsKey(drawable))
                return;

            advanceCursor();
        }

        private static DrawableHitObject resolveJudgedDrawable(DrawableHitObject drawable)
        {
            if (drawable is DrawableHoldNoteHead head && head.ParentHold != null)
                return head.ParentHold;

            return drawable;
        }

        /// <summary>
        /// Earliest note-lock：仅游标处未判定物件可击打，且不得越过更晚物件的 StartTime。
        /// </summary>
        public bool IsHittableEarliest(DrawableHitObject drawable, double time)
        {
            if (!drawableIndices.TryGetValue(drawable, out int index))
                return false;

            if (drawable.Judged || index != cursor)
                return false;

            return IsHittableEarliestIndex(index, time);
        }

        public bool IsHittableEarliestIndex(int index, double time)
            => IsHittableEarliest(entries, index, time, static e => e.IsJudged, static e => e.StartTime);

        public bool IsHittable(DrawableHitObject drawable, double time, EzEnumJudgePrecedence precedence, bool bmsMode)
        {
            if (precedence == EzEnumJudgePrecedence.Earliest)
                return IsHittableEarliest(drawable, time);

            var entry = SelectPressEntry(time, precedence, bmsMode);

            if (entry == null)
                return true;

            return ReferenceEquals(entry.RoutedObject, drawable);
        }

        public bool TryGetEntry(DrawableHitObject drawable, out ManiaLaneEntry entry)
        {
            if (drawableIndices.TryGetValue(drawable, out int index))
            {
                entry = entries[index];
                return true;
            }

            entry = null!;
            return false;
        }

        public DrawableHoldNote? ActiveHold { get; private set; }

        internal void SetActiveHold(DrawableHoldNote? hold) => ActiveHold = hold;

        /// <summary>
        /// Session：<see cref="LaneTargetState"/> 列版本。
        /// </summary>
        internal static bool IsHittableEarliest(IReadOnlyList<LaneTargetState> column, int index, double time)
            => IsHittableEarliest(column, index, time, static s => s.Judged, static s => s.Target.StartTime);

        internal static bool IsHittableEarliest<T>(
            IReadOnlyList<T> column,
            int index,
            double time,
            Func<T, bool> isJudged,
            Func<T, double> startTime)
        {
            for (int i = index + 1; i < column.Count; i++)
            {
                if (isJudged(column[i]))
                    continue;

                if (time >= startTime(column[i]))
                    return false;
            }

            return true;
        }

        public IEnumerable<ManiaLaneEntry> EnumerateForceMissBefore(double targetStartTime)
        {
            foreach (var entry in entries)
            {
                if (entry.IsPressJudged)
                    continue;

                if (entry.StartTime >= targetStartTime)
                    break;

                yield return entry;
            }
        }

        /// <summary>
        /// 收集 miss 窗内、未判定的列内 press 候选（O(log n + k)）。
        /// </summary>
        public List<ManiaLaneEntry> CollectOverlappingEntries(double time)
        {
            var result = new List<ManiaLaneEntry>();

            if (entries.Count == 0)
                return result;

            double maxEarly = 0;
            double maxLate = 0;

            foreach (var entry in entries)
            {
                if (entry.PressWindows == null)
                    continue;

                maxEarly = Math.Max(maxEarly, entry.PressWindows.WindowFor(HitResult.Miss, true));
                maxLate = Math.Max(maxLate, entry.PressWindows.WindowFor(HitResult.Miss, false));
            }

            if (maxEarly == 0 && maxLate == 0)
                return result;

            double searchLowerBound = time - maxLate;
            int lo = 0;
            int hi = entries.Count;

            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;

                if (entries[mid].StartTime < searchLowerBound)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            double searchUpperBound = time + maxEarly;

            for (int i = lo; i < entries.Count; i++)
            {
                var entry = entries[i];

                if (entry.StartTime > searchUpperBound)
                    break;

                if (entry.IsPressJudged || entry.PressWindows == null)
                    continue;

                double early = entry.PressWindows.WindowFor(HitResult.Miss, true);
                double late = entry.PressWindows.WindowFor(HitResult.Miss, false);

                if (time >= entry.StartTime - early && time <= entry.StartTime + late)
                    result.Add(entry);
            }

            return result;
        }

        /// <summary>
        /// 按 JudgePrecedence 选择本列 press 目标（含 BMS post-Bad KPoor）。
        /// </summary>
        public ManiaLaneEntry? SelectPressEntry(double time, EzEnumJudgePrecedence precedence, bool allowBmsFallbackToEarliest)
        {
            if (allowBmsFallbackToEarliest && trySelectPostBadEntry(time, out var postBad))
                return postBad;

            if (precedence == EzEnumJudgePrecedence.Earliest)
            {
                if (cursor >= entries.Count)
                    return null;

                var entry = entries[cursor];

                if (entry.IsPressJudged || !isWithinMissWindow(entry, time))
                    return null;

                if (!IsHittableEarliestIndex(cursor, time))
                    return null;

                return entry;
            }

            var overlapping = CollectOverlappingEntries(time);

            if (overlapping.Count == 0)
                return null;

            if (overlapping.Count == 1)
                return overlapping[0];

            overlapping.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            bool combo = precedence == EzEnumJudgePrecedence.Combo;

            var picked = OrderedHitPolicyHelper.SelectFold(
                overlapping,
                e => e.IsPressJudged,
                e => e.StartTime,
                e => e.PressWindows,
                time,
                combo);

            if (picked != null)
                return picked;

            return overlapping[0];
        }

        private bool trySelectPostBadEntry(double time, out ManiaLaneEntry? selected)
        {
            selected = null;
            ManiaLaneEntry? postBadCandidate = null;
            double postBadDistance = double.PositiveInfinity;

            foreach (var entry in entries)
            {
                if (!isPostBadKPoorRoutable(entry) || !isWithinMissWindow(entry, time))
                    continue;

                double distance = distanceToNonBadWindow(entry.RoutedObject, time);

                if (postBadCandidate == null || distance < postBadDistance
                                             || (distance == postBadDistance && entry.StartTime < postBadCandidate.StartTime))
                {
                    postBadCandidate = entry;
                    postBadDistance = distance;
                }
            }

            if (postBadCandidate == null)
                return false;

            double unjudgedMin = double.PositiveInfinity;

            foreach (var entry in entries)
            {
                if (entry.IsPressJudged || !isWithinMissWindow(entry, time))
                    continue;

                unjudgedMin = Math.Min(unjudgedMin, distanceToNonBadWindow(entry.RoutedObject, time));
            }

            if (postBadDistance > unjudgedMin || postBadCandidate.BmsRoute.HasLateKPoor)
                return false;

            selected = postBadCandidate;
            return true;
        }

        private static bool isWithinMissWindow(ManiaLaneEntry entry, double time)
        {
            if (entry.PressWindows == null)
                return false;

            double early = entry.PressWindows.WindowFor(HitResult.Miss, true);
            double late = entry.PressWindows.WindowFor(HitResult.Miss, false);
            return time >= entry.StartTime - early && time <= entry.StartTime + late;
        }

        private static bool isPostBadKPoorRoutable(ManiaLaneEntry entry)
        {
            if (!entry.IsPressJudged || !entry.BmsRoute.CanRouteToKPoor)
                return false;

            if (entry.RoutedObject is DrawableNote note)
                return note.CanRouteToKPoor;

            if (entry.RoutedObject is DrawableHoldNote hold && hold.Tail.Judged)
                return hold.Tail.CanRouteToKPoor;

            return false;
        }

        private static double distanceToNonBadWindow(DrawableHitObject obj, double pressTime)
        {
            var windows = obj.HitObject.HitWindows;

            if (windows == null)
                return double.PositiveInfinity;

            double early = windows.WindowFor(HitResult.Good);
            double late = windows.WindowFor(HitResult.Good);

            if (windows is ManiaHitWindows maniaWindows)
            {
                early = maniaWindows.WindowFor(HitResult.Good, true);
                late = maniaWindows.WindowFor(HitResult.Good, false);
            }

            double start = obj.HitObject.StartTime - early;
            double end = obj.HitObject.StartTime + late;

            if (pressTime < start)
                return start - pressTime;

            if (pressTime > end)
                return pressTime - end;

            return 0;
        }

        public static bool TryCreateEntry(DrawableHitObject drawable, out ManiaLaneEntry entry)
        {
            entry = null!;

            if (drawable.HitObject == null)
                return false;

            if (drawable is DrawableHoldNoteHead or DrawableHoldNoteTail)
                return false;

            if (drawable is DrawableHoldNote hold)
            {
                if (hold.Judged || hold.Head.Judged)
                    return false;

                if (hold.Head.HitObject.HitWindows is not ManiaHitWindows headWindows || headWindows.WindowFor(HitResult.Miss) == 0)
                    return false;

                entry = new ManiaLaneEntry(hold, hold.Head, hold.Head.HitObject.StartTime, headWindows);
                return true;
            }

            if (drawable.Judged)
                return false;

            if (drawable.HitObject.HitWindows is not ManiaHitWindows windows || windows.WindowFor(HitResult.Miss) == 0)
                return false;

            entry = new ManiaLaneEntry(drawable, drawable, drawable.HitObject.StartTime, windows);
            return true;
        }

        private void advanceCursor()
        {
            while (cursor < entries.Count && entries[cursor].IsJudged)
                cursor++;
        }

        private void rebuildDrawableIndices()
        {
            drawableIndices.Clear();

            for (int i = 0; i < entries.Count; i++)
                drawableIndices[entries[i].Drawable] = i;
        }
    }

    public sealed class ManiaLaneEntry
    {
        public DrawableHitObject Drawable { get; }

        public DrawableHitObject RoutedObject { get; }

        public DrawableHitObject JudgementObject { get; }

        public double StartTime { get; }

        public ManiaHitWindows? PressWindows { get; }

        public BmsHitModeJudgement.BmsRouteState BmsRoute { get; } = new BmsHitModeJudgement.BmsRouteState();

        public bool IsJudged => Drawable.Judged;

        public bool IsPressJudged => RoutedObject.Judged || JudgementObject.Judged;

        public ManiaLaneEntry(DrawableHitObject routedObject, DrawableHitObject judgementObject, double startTime, ManiaHitWindows? pressWindows)
        {
            Drawable = routedObject;
            RoutedObject = routedObject;
            JudgementObject = judgementObject;
            StartTime = startTime;
            PressWindows = pressWindows;
        }

        internal sealed class StartTimeComparer : IComparer<ManiaLaneEntry>
        {
            public static readonly StartTimeComparer INSTANCE = new StartTimeComparer();

            public int Compare(ManiaLaneEntry? x, ManiaLaneEntry? y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                if (x is null)
                    return -1;

                if (y is null)
                    return 1;

                return x.StartTime.CompareTo(y.StartTime);
            }
        }
    }
}
