// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Mania.EzMania.ReplayJudge
{
    /// <summary>
    /// 列内 note-lock 状态机：有序目标 + 游标。Earliest 模式下 Drawable / Session 共用判定语义。
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
            if (!drawableIndices.ContainsKey(drawable))
                return;

            advanceCursor();
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
                if (entry.IsJudged)
                    continue;

                if (entry.StartTime >= targetStartTime)
                    break;

                yield return entry;
            }
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
                entry = new ManiaLaneEntry(hold, hold.HitObject.StartTime);
                return true;
            }

            if (drawable.Judged)
                return false;

            entry = new ManiaLaneEntry(drawable, drawable.HitObject.StartTime);
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

        public double StartTime { get; }

        public bool IsJudged => Drawable.Judged;

        public ManiaLaneEntry(DrawableHitObject drawable, double startTime)
        {
            Drawable = drawable;
            StartTime = startTime;
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
