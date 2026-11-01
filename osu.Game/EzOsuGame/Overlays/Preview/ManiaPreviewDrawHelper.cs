// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// Bottom of the drawable is map start; time progresses upward (mania scroll direction).
    /// </summary>
    public static class ManiaPreviewDrawHelper
    {
        public static void AddLaneLines(List<PreviewQuad> quads, ManiaPreviewData data, float originX, float width, float height, float thickness, Color4 colour)
        {
            int totalColumns = data.TotalColumns;
            if (totalColumns <= 1)
                return;

            float laneWidth = width / totalColumns;

            for (int lane = 1; lane < totalColumns; lane++)
            {
                bool hasSeparator = lane - 1 < data.SeparatorAfterColumns.Count && data.SeparatorAfterColumns[lane - 1];
                if (!hasSeparator)
                    continue;

                float x = originX + lane * laneWidth - thickness * 0.5f;
                quads.Add(new PreviewQuad(x, 0, thickness * 1.8f, height, colour));
            }
        }

        public static void AddLayoutEntries(
            List<PreviewQuad> quads,
            IReadOnlyList<ManiaPreviewLayoutEntry> entries,
            int totalColumns,
            float originX,
            float width,
            float height,
            float rowStep,
            float noteHeight,
            ManiaPreviewThemeStyle theme,
            double timeOriginMs = double.NaN,
            double msPerRow = 0)
        {
            float laneWidth = width / Math.Max(1, totalColumns);
            float headWidth = laneWidth * theme.NoteWidthRatio;
            float headInset = laneWidth * (1f - theme.NoteWidthRatio) * 0.5f;
            // 主题比例为 1 时 body 与头同宽，即改造前的观感。
            float bodyWidth = headWidth * theme.LnBodyWidthRatio;
            float bodyInset = headInset + headWidth * (1f - theme.LnBodyWidthRatio) * 0.5f;
            bool mapTime = msPerRow > 0 && !double.IsNaN(timeOriginMs);

            float yAtTime(double time) => height - (float)((time - timeOriginMs) / msPerRow) * rowStep;

            foreach (ManiaPreviewLayoutEntry entry in entries)
            {
                float laneX = originX + entry.Column * laneWidth;
                float anchorY = mapTime ? yAtTime(entry.StartTime) : GetSlotBottomY(entry.Row, rowStep, height);
                Color4 colour = theme.Colour(entry.Kind, entry.Column, totalColumns);

                switch (entry.Kind)
                {
                    case ManiaPreviewNoteKind.Tap:
                    case ManiaPreviewNoteKind.HoldHead:
                    {
                        quads.Add(new PreviewQuad(laneX + headInset, anchorY - noteHeight, headWidth, noteHeight, colour));
                        break;
                    }

                    case ManiaPreviewNoteKind.HoldBody:
                    {
                        // 身体停在结束时刻。不要铺到格子远端，否则尾巴会和下一个头贴死，滚动映射比例也拉不开。
                        float bodyTop = mapTime ? yAtTime(entry.EndTime) : GetSlotBottomY(entry.EndRow, rowStep, height);
                        float bodyBottom = anchorY - noteHeight;
                        float h = Math.Max(1f, bodyBottom - bodyTop);
                        quads.Add(new PreviewQuad(laneX + bodyInset, bodyTop, bodyWidth, h, colour));
                        break;
                    }
                }
            }
        }

        /// <summary>Bottom edge of the row slot (map start is at drawable bottom).</summary>
        public static float GetSlotBottomY(int row, float rowStep, float height) => height - row * rowStep;
    }
}
