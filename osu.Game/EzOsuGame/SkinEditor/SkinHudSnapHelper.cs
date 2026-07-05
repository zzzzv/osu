// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.EzOsuGame.SkinEditor
{
    internal static class SkinHudSnapHelper
    {
        public const float STICKY_THRESHOLD = 5f;
        public const float CAPTURE_TOLERANCE = 1.5f;

        private readonly struct Bounds
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Top;
            public readonly float Bottom;

            public Bounds(float left, float right, float top, float bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public float HCenter => (Left + Right) * 0.5f;
            public float VCenter => (Top + Bottom) * 0.5f;

            public Bounds Offset(float dx, float dy) => new Bounds(Left + dx, Right + dx, Top + dy, Bottom + dy);
        }

        private readonly struct SnapCandidate
        {
            public readonly SkinHudSnapEdge SelectionEdge;
            public readonly SkinHudSnapGapMode GapMode;
            public readonly float TargetLine;
            public readonly float GapDelta;

            public SnapCandidate(SkinHudSnapEdge selectionEdge, SkinHudSnapGapMode gapMode, float targetLine, float gapDelta)
            {
                SelectionEdge = selectionEdge;
                GapMode = gapMode;
                TargetLine = targetLine;
                GapDelta = gapDelta;
            }
        }

        public static Vector2 ApplySnap(
            SkinnableContainer container,
            IReadOnlyList<Drawable> selection,
            Vector2 rawParentDelta,
            float snapDistance,
            ref SkinHudSnapDragState state)
        {
            if (selection.Count == 0)
                return rawParentDelta;

            var currentBounds = getCombinedBounds(selection);
            var proposedBounds = currentBounds.Offset(rawParentDelta.X, rawParentDelta.Y);
            var others = getSnapTargets(container, selection);

            float correctedX = processAxis(
                isHorizontal: true,
                currentBounds,
                proposedBounds,
                rawParentDelta.X,
                snapDistance,
                container.DrawSize,
                others,
                ref state.X);

            float correctedY = processAxis(
                isHorizontal: false,
                currentBounds,
                proposedBounds,
                rawParentDelta.Y,
                snapDistance,
                container.DrawSize,
                others,
                ref state.Y);

            return new Vector2(correctedX, correctedY);
        }

        private static float processAxis(
            bool isHorizontal,
            Bounds currentBounds,
            Bounds proposedBounds,
            float rawDelta,
            float snapDistance,
            Vector2 containerDrawSize,
            IReadOnlyList<Bounds> others,
            ref SkinHudSnapAxisSession session)
        {
            if (session.IsActive)
                return processActiveSession(isHorizontal, currentBounds, proposedBounds, rawDelta, snapDistance, containerDrawSize, ref session);

            if (tryBeginSession(isHorizontal, proposedBounds, rawDelta, snapDistance, containerDrawSize, others, ref session))
            {
                float targetEdge = getTargetEdgeValue(isHorizontal, proposedBounds, session);
                float desiredEdge = computeEdgeForGap(isHorizontal, session, session.Stage == SkinHudSnapStage.Flush ? 0 : snapDistance);
                return desiredEdge - getSelectionEdgeValue(isHorizontal, currentBounds, session.SelectionEdge);
            }

            return rawDelta;
        }

        private static float processActiveSession(
            bool isHorizontal,
            Bounds currentBounds,
            Bounds proposedBounds,
            float rawDelta,
            float snapDistance,
            Vector2 containerDrawSize,
            ref SkinHudSnapAxisSession session)
        {
            float openingDirection = -session.ClosingDirection;

            if (rawDelta * openingDirection > 0)
            {
                session.IntentDelta = Math.Max(0, session.IntentDelta - Math.Abs(rawDelta));

                if (session.IntentDelta <= 0 && computeGap(isHorizontal, proposedBounds, session) > snapDistance + STICKY_THRESHOLD)
                {
                    session.Reset();
                    return rawDelta;
                }
            }

            if (session.Stage == SkinHudSnapStage.Gap)
            {
                if (rawDelta * session.ClosingDirection < 0)
                    session.IntentDelta += Math.Abs(rawDelta);

                if (session.IntentDelta >= STICKY_THRESHOLD)
                    session.Stage = SkinHudSnapStage.Flush;

                float gapTarget = session.Stage == SkinHudSnapStage.Flush ? 0 : snapDistance;
                float desiredEdge = computeEdgeForGap(isHorizontal, session, gapTarget);
                return desiredEdge - getSelectionEdgeValue(isHorizontal, currentBounds, session.SelectionEdge);
            }

            // Flush
            float desiredFlushEdge = computeEdgeForGap(isHorizontal, session, 0);
            float flushDelta = desiredFlushEdge - getSelectionEdgeValue(isHorizontal, currentBounds, session.SelectionEdge);

            var flushProposed = offsetBounds(isHorizontal, currentBounds, flushDelta);
            var testBounds = isHorizontal ? flushProposed.Offset(rawDelta, 0) : flushProposed.Offset(0, rawDelta);

            if (rawDelta * openingDirection > 0 && computeGap(isHorizontal, testBounds, session) > snapDistance + STICKY_THRESHOLD)
            {
                session.Reset();
                return rawDelta;
            }

            return flushDelta;
        }

        private static bool tryBeginSession(
            bool isHorizontal,
            Bounds proposedBounds,
            float rawDelta,
            float snapDistance,
            Vector2 containerDrawSize,
            IReadOnlyList<Bounds> others,
            ref SkinHudSnapAxisSession session)
        {
            SnapCandidate? best = null;

            foreach (var candidate in enumerateCandidates(isHorizontal, containerDrawSize, others))
            {
                float gap = computeGap(isHorizontal, proposedBounds, candidate);
                if (gap < 0)
                    continue;

                float deltaFromTarget = Math.Abs(gap - snapDistance);
                if (deltaFromTarget > CAPTURE_TOLERANCE)
                    continue;

                if (best == null || deltaFromTarget < best.Value.GapDelta)
                    best = new SnapCandidate(candidate.SelectionEdge, candidate.GapMode, candidate.TargetLine, deltaFromTarget);
            }

            if (best == null)
                return false;

            session.Stage = SkinHudSnapStage.Gap;
            session.IntentDelta = 0;
            session.SelectionEdge = best.Value.SelectionEdge;
            session.GapMode = best.Value.GapMode;
            session.TargetLine = best.Value.TargetLine;
            session.ClosingDirection = Math.Abs(rawDelta) > float.Epsilon
                ? Math.Sign(rawDelta)
                : Math.Sign(getSelectionEdgeValue(isHorizontal, proposedBounds, session.SelectionEdge) - computeEdgeForGap(isHorizontal, best.Value, snapDistance));

            if (session.ClosingDirection == 0)
                session.ClosingDirection = -1;

            return true;
        }

        private static IEnumerable<SnapCandidate> enumerateCandidates(bool isHorizontal, Vector2 containerDrawSize, IReadOnlyList<Bounds> others)
        {
            if (isHorizontal)
            {
                foreach (var other in others)
                {
                    yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Right, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Left, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Left, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Right, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, other.HCenter, 0);
                }

                float width = containerDrawSize.X;
                yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, 0);
                yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, width, 0);
                yield return new SnapCandidate(SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, width * 0.5f, 0);
            }
            else
            {
                foreach (var other in others)
                {
                    yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Bottom, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Top, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, other.Top, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, other.Bottom, 0);
                    yield return new SnapCandidate(SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, other.VCenter, 0);
                }

                float height = containerDrawSize.Y;
                yield return new SnapCandidate(SkinHudSnapEdge.Min, SkinHudSnapGapMode.SelectionMinusTarget, 0, 0);
                yield return new SnapCandidate(SkinHudSnapEdge.Max, SkinHudSnapGapMode.TargetMinusSelection, height, 0);
                yield return new SnapCandidate(SkinHudSnapEdge.Mid, SkinHudSnapGapMode.SelectionMinusTarget, height * 0.5f, 0);
            }
        }

        private static float computeGap(bool isHorizontal, Bounds bounds, SnapCandidate candidate) =>
            computeGap(isHorizontal, bounds, candidate.SelectionEdge, candidate.GapMode, candidate.TargetLine);

        private static float computeGap(bool isHorizontal, Bounds bounds, SkinHudSnapAxisSession session) =>
            computeGap(isHorizontal, bounds, session.SelectionEdge, session.GapMode, session.TargetLine);

        private static float computeGap(bool isHorizontal, Bounds bounds, SkinHudSnapEdge edge, SkinHudSnapGapMode mode, float targetLine)
        {
            float selectionEdge = getSelectionEdgeValue(isHorizontal, bounds, edge);
            return mode == SkinHudSnapGapMode.SelectionMinusTarget
                ? selectionEdge - targetLine
                : targetLine - selectionEdge;
        }

        private static float computeEdgeForGap(bool isHorizontal, SkinHudSnapAxisSession session, float gap) =>
            computeEdgeForGap(isHorizontal, session.SelectionEdge, session.GapMode, session.TargetLine, gap);

        private static float computeEdgeForGap(bool isHorizontal, SnapCandidate candidate, float gap) =>
            computeEdgeForGap(isHorizontal, candidate.SelectionEdge, candidate.GapMode, candidate.TargetLine, gap);

        private static float computeEdgeForGap(bool isHorizontal, SkinHudSnapEdge edge, SkinHudSnapGapMode mode, float targetLine, float gap) => mode == SkinHudSnapGapMode.SelectionMinusTarget
            ? targetLine + gap
            : targetLine - gap;

        private static float getSelectionEdgeValue(bool isHorizontal, Bounds bounds, SkinHudSnapEdge edge)
        {
            if (isHorizontal)
            {
                return edge switch
                {
                    SkinHudSnapEdge.Min => bounds.Left,
                    SkinHudSnapEdge.Max => bounds.Right,
                    _ => bounds.HCenter,
                };
            }

            return edge switch
            {
                SkinHudSnapEdge.Min => bounds.Top,
                SkinHudSnapEdge.Max => bounds.Bottom,
                _ => bounds.VCenter,
            };
        }

        private static float getTargetEdgeValue(bool isHorizontal, Bounds bounds, SkinHudSnapAxisSession session) => getSelectionEdgeValue(isHorizontal, bounds, session.SelectionEdge);

        private static Bounds offsetBounds(bool isHorizontal, Bounds bounds, float delta) => isHorizontal ? bounds.Offset(delta, 0) : bounds.Offset(0, delta);

        private static Bounds getCombinedBounds(IReadOnlyList<Drawable> drawables)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            foreach (var drawable in drawables)
            {
                var bounds = getContainerSpaceBounds(drawable);
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }

            return new Bounds(minX, maxX, minY, maxY);
        }

        private static Bounds getContainerSpaceBounds(Drawable drawable)
        {
            var aabb = drawable.Parent!.ToLocalSpace(drawable.ScreenSpaceDrawQuad).AABBFloat;
            return new Bounds(aabb.Left, aabb.Right, aabb.Top, aabb.Bottom);
        }

        private static List<Bounds> getSnapTargets(SkinnableContainer container, IReadOnlyList<Drawable> selection)
        {
            var selectionSet = new HashSet<Drawable>(selection);
            var result = new List<Bounds>();

            foreach (var component in container.Components)
            {
                if (component is not Drawable drawable)
                    continue;

                if (!component.IsEditable || !drawable.IsPresent || selectionSet.Contains(drawable))
                    continue;

                result.Add(getContainerSpaceBounds(drawable));
            }

            return result;
        }
    }
}
