// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.EzOsuGame.Configuration;
using osu.Game.EzOsuGame.Overlays.Preview;
using osuTK.Graphics;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ManiaPreviewThemeTest
    {
        private static readonly Color4 scheme_a = new Color4(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color4 scheme_b = new Color4(0x5E, 0xAE, 0xFF, 0xFF);
        private static readonly Color4 scheme_c = new Color4(0xFF, 0xEC, 0x5E, 0xFF);
        private static readonly Color4 scheme_d = new Color4(0xFF, 0x3F, 0x00, 0xFF);

        [Test]
        public void TestClassicFourKeyPalette()
        {
            Assert.That(ManiaPreviewThemes.GetHeadColour(0, 4), Is.EqualTo(scheme_a));
            Assert.That(ManiaPreviewThemes.GetHeadColour(1, 4), Is.EqualTo(scheme_b));
            Assert.That(ManiaPreviewThemes.GetHeadColour(2, 4), Is.EqualTo(scheme_b));
            Assert.That(ManiaPreviewThemes.GetHeadColour(3, 4), Is.EqualTo(scheme_a));
        }

        [Test]
        public void TestClassicPaletteIsSymmetric()
        {
            // 1-10 键的 layouts 是回文字符串；12/14/16/18 上游给的字符串比键数长，单独覆盖。
            int[] keyCounts = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            foreach (int columns in keyCounts)
            {
                for (int column = 0; column < columns / 2; column++)
                {
                    Assert.That(ManiaPreviewThemes.GetHeadColour(column, columns),
                        Is.EqualTo(ManiaPreviewThemes.GetHeadColour(columns - 1 - column, columns)),
                        $"columns={columns} column={column}");
                }
            }
        }

        [Test]
        public void TestClassicPaletteHandlesOverlongUpstreamLayouts()
        {
            // 12/14/16/18 的 layouts 字符串长度分别是 13/15/18/20，两端仍取到橙色。
            int[] keyCounts = { 12, 14, 16, 18 };

            foreach (int columns in keyCounts)
            {
                Assert.That(ManiaPreviewThemes.GetHeadColour(0, columns), Is.EqualTo(scheme_d), $"columns={columns}");
                Assert.That(ManiaPreviewThemes.GetHeadColour(columns - 1, columns), Is.EqualTo(scheme_d), $"columns={columns}");
            }
        }

        [Test]
        public void TestClassicEightKeyPalette()
        {
            Color4[] expected = { scheme_a, scheme_b, scheme_a, scheme_c, scheme_c, scheme_a, scheme_b, scheme_a };

            for (int column = 0; column < expected.Length; column++)
                Assert.That(ManiaPreviewThemes.GetHeadColour(column, expected.Length), Is.EqualTo(expected[column]), $"column={column}");
        }

        [Test]
        public void TestClassicPaletteFallsBackToNearestLayout()
        {
            // 11 键未定义，与 10 和 12 的距离相同，取较小的 10 键方案（dabaccabad），两端仍是橙色。
            Assert.That(ManiaPreviewThemes.GetHeadColour(0, 11), Is.EqualTo(scheme_d));
            Assert.That(ManiaPreviewThemes.GetHeadColour(10, 11), Is.EqualTo(scheme_d));
            // 中间列来自 10 键方案的中轴（c = 黄）。
            Assert.That(ManiaPreviewThemes.GetHeadColour(5, 11), Is.EqualTo(scheme_c));

            for (int column = 0; column < 11 / 2; column++)
                Assert.That(ManiaPreviewThemes.GetHeadColour(column, 11), Is.EqualTo(ManiaPreviewThemes.GetHeadColour(10 - column, 11)));
        }

        [Test]
        public void TestSingleColumnFallsBackToWhite()
        {
            Assert.That(ManiaPreviewThemes.GetHeadColour(0, 1), Is.EqualTo(scheme_a));
        }

        [Test]
        public void TestPlainThemeKeepsLegacyColours()
        {
            var plain = ManiaPreviewThemes.Plain;

            Assert.That(plain.Colour(ManiaPreviewNoteKind.Tap, 2, 4), Is.EqualTo(new Color4(80, 150, 255, 230)));
            Assert.That(plain.Colour(ManiaPreviewNoteKind.HoldHead, 2, 4), Is.EqualTo(new Color4(255, 80, 80, 235)));
            Assert.That(plain.Colour(ManiaPreviewNoteKind.HoldBody, 2, 4), Is.EqualTo(new Color4(255, 220, 70, 230)));
            Assert.That(plain.Colour(ManiaPreviewNoteKind.HoldTail, 2, 4), Is.EqualTo(new Color4(255, 220, 70, 230)));
        }

        [Test]
        public void TestPlainThemeKeepsLegacyGeometryAndDecorations()
        {
            var plain = ManiaPreviewThemes.Plain;

            Assert.That(plain.NoteWidthRatio, Is.EqualTo(0.8f));
            Assert.That(plain.LnBodyWidthRatio, Is.EqualTo(1f));
            Assert.That(plain.ShowColumnBackground, Is.True);
            Assert.That(plain.ShowColumnBorders, Is.True);
            Assert.That(plain.ShowLaneSeparators, Is.True);
            Assert.That(plain.RowLineInterval, Is.EqualTo(ManiaPreviewFixedLayout.ROWS_PER_BEAT));
            Assert.That(plain.BackgroundColour, Is.Null);
            Assert.That(plain.NoteHeightRatio, Is.Null);
            Assert.That(plain.MinNoteScreenHeight, Is.EqualTo(0f));
        }

        [Test]
        public void TestClassicThemeGeometryAndDecorations()
        {
            var classic = ManiaPreviewThemes.Classic;

            Assert.That(classic.NoteWidthRatio, Is.EqualTo(0.8f));
            Assert.That(classic.LnBodyWidthRatio, Is.EqualTo(0.45f));
            Assert.That(classic.ShowColumnBackground, Is.False);
            Assert.That(classic.ShowColumnBorders, Is.True);
            Assert.That(classic.ShowLaneSeparators, Is.False);
            Assert.That(classic.RowLineInterval, Is.EqualTo(ManiaPreviewFixedLayout.ROWS_PER_MEASURE));
            Assert.That(classic.BackgroundColour, Is.EqualTo(new Color4(0, 0, 0, 255)));
            Assert.That(classic.NoteHeightRatio, Is.EqualTo(0.3f));
            Assert.That(classic.MinNoteScreenHeight, Is.EqualTo(2f));

            Assert.That(classic.Colour(ManiaPreviewNoteKind.Tap, 1, 4), Is.EqualTo(scheme_b));
            Assert.That(classic.Colour(ManiaPreviewNoteKind.HoldHead, 1, 4), Is.EqualTo(scheme_b));
            Assert.That(classic.Colour(ManiaPreviewNoteKind.HoldBody, 1, 4), Is.EqualTo(new Color4(0xA0, 0xA0, 0xA0, 0xFF)));
            Assert.That(classic.Colour(ManiaPreviewNoteKind.HoldTail, 1, 4), Is.EqualTo(new Color4(0xA0, 0xA0, 0xA0, 0xFF)));
        }

        [Test]
        public void TestResolve()
        {
            Assert.That(ManiaPreviewThemes.Resolve(EzBeatmapPreviewNoteTheme.Plain), Is.SameAs(ManiaPreviewThemes.Plain));
            Assert.That(ManiaPreviewThemes.Resolve(EzBeatmapPreviewNoteTheme.Classic), Is.SameAs(ManiaPreviewThemes.Classic));
        }
    }
}
