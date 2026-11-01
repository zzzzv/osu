// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    public static class ManiaPreviewColumnRenderer
    {
        public static void BuildColumnPanels(
            List<PreviewQuad> quads,
            IReadOnlyList<ManiaPreviewLayoutEntry> layoutEntries,
            ManiaPreviewColumnLayout layout,
            ManiaPreviewData data,
            int totalRows,
            float panelHeight,
            ManiaPreviewThemeStyle theme)
        {
            float laneLineThickness = Math.Max(0.5f, layout.ColumnWidth * 0.004f);
            float rowLineThickness = Math.Max(0.5f, layout.RowStep * theme.RowLineThicknessRatio);
            int rowLineInterval = Math.Max(1, theme.RowLineInterval);
            float noteHeight = resolveNoteHeight(layout, theme, data.TotalColumns);

            for (int col = 0; col < layout.ColumnCount; col++)
            {
                float panelX = col * (layout.ColumnWidth + ManiaPreviewColumnLayout.COLUMN_SPACING);
                int rowStart = col * layout.RowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + layout.RowsPerColumn);

                // 轨道区底色：轻微提亮，与预览背景区分。
                if (theme.ShowColumnBackground)
                    quads.Add(new PreviewQuad(panelX, 0, layout.ColumnWidth, panelHeight, Color4.White.Opacity(0.01f)));

                if (theme.ShowColumnBorders)
                {
                    quads.Add(new PreviewQuad(panelX, 0, 1f, panelHeight, theme.ColumnBorderColour));
                    quads.Add(new PreviewQuad(panelX + layout.ColumnWidth - 1f, 0, 1f, panelHeight, theme.ColumnBorderColour));
                }

                if (theme.ShowLaneSeparators)
                    ManiaPreviewDrawHelper.AddLaneLines(quads, data, panelX, layout.ColumnWidth, panelHeight, laneLineThickness, theme.LaneSeparatorColour);

                for (int row = rowStart + rowLineInterval; row <= rowEnd; row += rowLineInterval)
                {
                    int localRow = row - rowStart;
                    float y = ManiaPreviewDrawHelper.GetSlotBottomY(localRow, layout.RowStep, panelHeight) - rowLineThickness * 0.5f;
                    quads.Add(new PreviewQuad(panelX, y, layout.ColumnWidth, rowLineThickness, theme.RowLineColour));
                }
            }

            foreach (ManiaPreviewLayoutEntry entry in layoutEntries)
            {
                int col = entry.Row / layout.RowsPerColumn;
                if (col >= layout.ColumnCount)
                    continue;

                int rowStart = col * layout.RowsPerColumn;
                int rowEnd = Math.Min(totalRows, rowStart + layout.RowsPerColumn);

                if (entry.Row >= rowEnd)
                    continue;

                float panelX = col * (layout.ColumnWidth + ManiaPreviewColumnLayout.COLUMN_SPACING);
                var localEntry = new ManiaPreviewLayoutEntry(
                    entry.Column,
                    entry.Row - rowStart,
                    Math.Min(entry.EndRow, rowEnd - 1) - rowStart,
                    entry.Kind,
                    entry.StartTime,
                    entry.EndTime);

                double msPerRow = 0;
                double columnStart = double.NaN;

                if (layout.TimeMapped && data.MsPerMeasure > 0)
                {
                    msPerRow = data.MsPerMeasure / ManiaPreviewFixedLayout.ROWS_PER_MEASURE;
                    columnStart = data.StartTimeMs + rowStart * msPerRow;
                }

                ManiaPreviewDrawHelper.AddLayoutEntries(
                    quads,
                    new[] { localEntry },
                    data.TotalColumns,
                    panelX,
                    layout.ColumnWidth,
                    panelHeight,
                    layout.RowStep,
                    noteHeight,
                    theme,
                    columnStart,
                    msPerRow);
            }
        }

        /// <summary>
        /// 内容空间里的 note 厚度。主题给了高度比例时按「头宽 × 比例」算，不同键数下长宽比一致；
        /// 再按内容→屏幕的纵向缩放保证不低于主题要求的屏幕高度。
        /// </summary>
        private static float resolveNoteHeight(in ManiaPreviewColumnLayout layout, ManiaPreviewThemeStyle theme, int totalColumns)
        {
            // 这里的 ColumnWidth 是整个小节栏的宽度，单列宽度要再除以键数。
            float headWidth = layout.ColumnWidth / Math.Max(1, totalColumns) * theme.NoteWidthRatio;

            float noteHeight = theme.NoteHeightRatio is float ratio
                ? headWidth * ratio
                : layout.NoteHeight;

            if (theme.MinNoteScreenHeight <= 0)
                return noteHeight;

            float verticalScale = layout.FitScale > 0 ? layout.FitScale : 1f;
            return Math.Max(noteHeight, theme.MinNoteScreenHeight / verticalScale);
        }
    }
}
