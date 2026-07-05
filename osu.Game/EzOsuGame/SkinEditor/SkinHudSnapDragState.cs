// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.EzOsuGame.SkinEditor
{
    internal enum SkinHudSnapStage
    {
        None,
        Gap,
        Flush,
    }

    internal enum SkinHudSnapEdge
    {
        Min,
        Mid,
        Max,
    }

    internal enum SkinHudSnapGapMode
    {
        /// <summary>
        /// gap = selection edge - target line.
        /// </summary>
        SelectionMinusTarget,

        /// <summary>
        /// gap = target line - selection edge.
        /// </summary>
        TargetMinusSelection,
    }

    internal struct SkinHudSnapAxisSession
    {
        public SkinHudSnapStage Stage;
        public float IntentDelta;
        public int ClosingDirection;
        public float TargetLine;
        public SkinHudSnapEdge SelectionEdge;
        public SkinHudSnapGapMode GapMode;

        public void Reset()
        {
            Stage = SkinHudSnapStage.None;
            IntentDelta = 0;
            ClosingDirection = 0;
            TargetLine = 0;
            SelectionEdge = SkinHudSnapEdge.Min;
            GapMode = SkinHudSnapGapMode.SelectionMinusTarget;
        }

        public bool IsActive => Stage != SkinHudSnapStage.None;
    }

    internal partial class SkinHudSnapDragState
    {
        public SkinHudSnapAxisSession X;
        public SkinHudSnapAxisSession Y;

        public void Reset()
        {
            X.Reset();
            Y.Reset();
        }
    }
}
