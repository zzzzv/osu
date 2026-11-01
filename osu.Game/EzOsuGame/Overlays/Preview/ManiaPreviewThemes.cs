// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.EzOsuGame.Configuration;
using osuTK.Graphics;

namespace osu.Game.EzOsuGame.Overlays.Preview
{
    /// <summary>
    /// 谱面预览里 note / LN / 辅助线的外观参数。主题差异全部收敛在这里，绘制代码不判断主题。
    /// </summary>
    public sealed class ManiaPreviewThemeStyle
    {
        /// <summary>取色回调，参数为 (note 类型, 列号, 总列数)。</summary>
        public required Func<ManiaPreviewNoteKind, int, int, Color4> Colour { get; init; }

        /// <summary>note 头宽与列宽的比例。</summary>
        public float NoteWidthRatio { get; init; } = 0.8f;

        /// <summary>LN body 宽度与头宽的比例。</summary>
        public float LnBodyWidthRatio { get; init; } = 1f;

        /// <summary>
        /// note 高度与头宽的比例；null 表示沿用布局按行高算出的厚度。
        /// 设为比例后，不同键数下的 note 长宽比一致，不会因为列变窄而变扁。
        /// </summary>
        public float? NoteHeightRatio { get; init; }

        /// <summary>note 在屏幕上的最小高度（像素），0 表示不设下限。</summary>
        public float MinNoteScreenHeight { get; init; }

        public bool ShowColumnBackground { get; init; } = true;

        /// <summary>整个 playfield（一栏所有轨道）左右两侧的竖线。</summary>
        public bool ShowColumnBorders { get; init; } = true;

        public Color4 ColumnBorderColour { get; init; } = Color4.White.Opacity(0.18f);

        /// <summary>轨道之间的分割线。位置来自 mania 的列分隔判断（只有 12/14/16K 会给出位置）。</summary>
        public bool ShowLaneSeparators { get; init; } = true;

        /// <summary>每隔多少行输出一条横线。</summary>
        public int RowLineInterval { get; init; } = ManiaPreviewFixedLayout.ROWS_PER_BEAT;

        /// <summary>横线粗细与行高的比例，绘制时另有 0.5px 下限。</summary>
        public float RowLineThicknessRatio { get; init; } = 0.08f;

        public Color4 RowLineColour { get; init; } = Color4.White.Opacity(0.2f);

        public Color4 LaneSeparatorColour { get; init; } = Color4.White.Opacity(0.3f);

        /// <summary>预览区域底色；null 表示沿用预览面板自身的材质背景。</summary>
        public Color4? BackgroundColour { get; init; }
    }

    public static class ManiaPreviewThemes
    {
        private static readonly Color4 plain_tap_colour = new Color4(80, 150, 255, 230);
        private static readonly Color4 plain_hold_head_colour = new Color4(255, 80, 80, 235);
        private static readonly Color4 plain_hold_body_colour = new Color4(255, 220, 70, 230);

        // mania-image 默认调色板。
        private static readonly Color4 classic_scheme_a = new Color4(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color4 classic_scheme_b = new Color4(0x5E, 0xAE, 0xFF, 0xFF);
        private static readonly Color4 classic_scheme_c = new Color4(0xFF, 0xEC, 0x5E, 0xFF);
        private static readonly Color4 classic_scheme_d = new Color4(0xFF, 0x3F, 0x00, 0xFF);

        private static readonly Color4 classic_body_colour = new Color4(0xA0, 0xA0, 0xA0, 0xFF);
        private static readonly Color4 classic_barline_colour = new Color4(0x85, 0xF0, 0x00, 0xFF);
        private static readonly Color4 classic_background_colour = new Color4(0x00, 0x00, 0x00, 0xFF);

        /// <summary>
        /// mania-image 的默认 layouts：键数 → 每列取用的配色键，字符串顺序即列顺序。
        /// 12 / 14 / 16 / 18 的字符串比键数长（上游数据如此），按列索引等比映射；未定义键数（11 / 13 / 15 / 17 / 19+）取最近方案。
        /// </summary>
        private static readonly IReadOnlyDictionary<int, string> classic_layouts = new Dictionary<int, string>
        {
            [1] = "a",
            [2] = "aa",
            [3] = "aba",
            [4] = "abba",
            [5] = "abcba",
            [6] = "abaaba",
            [7] = "abacaba",
            [8] = "abaccaba",
            [9] = "dabacabad",
            [10] = "dabaccabad",
            [12] = "dbabaccabadbd",
            [14] = "dababaccabadabd",
            [16] = "dbababaccabadababd",
            [18] = "dabababaccabadababad",
        };

        /// <summary>与改造前完全一致的默认观感。</summary>
        public static ManiaPreviewThemeStyle Plain { get; } = new ManiaPreviewThemeStyle
        {
            Colour = (kind, _, _) => kind switch
            {
                ManiaPreviewNoteKind.Tap => plain_tap_colour,
                ManiaPreviewNoteKind.HoldHead => plain_hold_head_colour,
                _ => plain_hold_body_colour,
            },
        };

        /// <summary>mania-image 项目默认风格：按列配色、头宽身窄、纯黑底、绿色小节线、playfield 两侧灰色竖线。</summary>
        public static ManiaPreviewThemeStyle Classic { get; } = new ManiaPreviewThemeStyle
        {
            Colour = (kind, column, totalColumns) => kind is ManiaPreviewNoteKind.Tap or ManiaPreviewNoteKind.HoldHead
                ? GetHeadColour(column, totalColumns)
                : classic_body_colour,
            LnBodyWidthRatio = 0.45f,
            // mania-image 的 note 是 20×6，头宽与高度 3.3:1。
            NoteHeightRatio = 0.3f,
            MinNoteScreenHeight = 2f,
            ShowColumnBackground = false,
            // 只在每组 playfield（一栏所有轨道）左右两侧画灰色竖线，轨道之间不画。
            ShowColumnBorders = true,
            ColumnBorderColour = Color4.White.Opacity(0.5f),
            ShowLaneSeparators = false,
            RowLineInterval = ManiaPreviewFixedLayout.ROWS_PER_MEASURE,
            RowLineColour = classic_barline_colour,
            BackgroundColour = classic_background_colour,
        };

        public static ManiaPreviewThemeStyle Resolve(EzBeatmapPreviewNoteTheme theme)
            => theme == EzBeatmapPreviewNoteTheme.Classic ? Classic : Plain;

        /// <summary>
        /// 经典主题下某一列 note 头的颜色。键数在调色表里时按列直接取，否则取最接近的已定义方案并等比映射列索引。
        /// </summary>
        public static Color4 GetHeadColour(int column, int totalColumns)
        {
            if (totalColumns <= 1)
                return classic_scheme_a;

            string layout = resolveLayout(totalColumns);
            int index = (int)Math.Round(column * (layout.Length - 1) / (double)(totalColumns - 1), MidpointRounding.AwayFromZero);

            return colourFromScheme(layout[Math.Clamp(index, 0, layout.Length - 1)]);
        }

        private static string resolveLayout(int totalColumns)
        {
            if (classic_layouts.TryGetValue(totalColumns, out string? exact))
                return exact;

            int bestKey = 1;
            int bestDistance = int.MaxValue;

            foreach (int key in classic_layouts.Keys)
            {
                int distance = Math.Abs(key - totalColumns);

                if (distance < bestDistance || (distance == bestDistance && key < bestKey))
                {
                    bestDistance = distance;
                    bestKey = key;
                }
            }

            return classic_layouts[bestKey];
        }

        private static Color4 colourFromScheme(char scheme) => scheme switch
        {
            'b' => classic_scheme_b,
            'c' => classic_scheme_c,
            'd' => classic_scheme_d,
            _ => classic_scheme_a,
        };
    }
}
