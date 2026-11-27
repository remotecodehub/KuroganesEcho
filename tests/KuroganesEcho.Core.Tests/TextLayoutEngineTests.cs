using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KuroganesEcho.Core;
using KuroganesEcho.Core.Localization;
using Xunit;

namespace KuroganesEcho.Core.Tests;

[Collection("Localization")]
public sealed class TextLayoutEngineTests
{
    private static Func<string, float> FixedGlyphMeasure(float pixelsPerChar) =>
        text => string.IsNullOrEmpty(text) ? 0f : text.Length * pixelsPerChar;

    [Fact]
    public void WrapText_RespectsMaximumContainerWidth_IncludingOversizedSingleWords()
    {
        var measure = FixedGlyphMeasure(6f);
        const float maxWidth = 60f; // 10 characters max per line

        var text = "Short words plus Supercalifragilisticexpialidocious and trailing words";
        var lines = TextLayoutEngine.WrapText(text, maxWidth, measure);

        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            Assert.True(
                measure(line) <= maxWidth + 0.001f,
                $"Line '{line}' width {measure(line)} exceeded maxWidth {maxWidth}.");
        }
    }

    [Fact]
    public void LayoutBlock_CentersTextBlockAndJustifiesMultilineDescriptiveTextWithinBounds()
    {
        var container = new UiRect(16, 12, 288, 156);
        const float paddingX = 12f;
        var expectedLeft = container.Left + paddingX;
        var expectedRight = container.Right - paddingX;
        var measure = FixedGlyphMeasure(5.5f);

        var text = "Defeat Minotaur III and the three arcane guardians to restore the Ash Seal and protect the Kurogane clan village.";
        var block = TextLayoutEngine.LayoutBlock(
            text,
            container,
            paddingX,
            topY: 40f,
            lineHeight: 10f,
            measure,
            justifyMultiline: true);

        Assert.True(block.Lines.Count >= 2, "Expected descriptive text to wrap into multiple lines.");
        Assert.True(block.Left >= expectedLeft - 0.01f);
        Assert.True(block.Right <= expectedRight + 0.01f);

        // Non-final lines with multiple words must be horizontally justified across the content box
        for (var i = 0; i < block.Lines.Count - 1; i++)
        {
            var line = block.Lines[i];
            Assert.True(line.IsJustified, $"Expected line {i} ('{line.Text}') to be justified.");
            Assert.True(line.Words.Count >= 2);
            Assert.InRange(line.Words[0].X, expectedLeft - 0.01f, expectedLeft + 0.01f);
            var lastWord = line.Words[^1];
            Assert.InRange(lastWord.X + lastWord.Width, expectedRight - 0.01f, expectedRight + 0.01f);

            // Words must not overlap and must remain strictly inside container bounds
            for (var w = 0; w < line.Words.Count; w++)
            {
                var word = line.Words[w];
                Assert.True(word.X >= expectedLeft - 0.01f);
                Assert.True(word.X + word.Width <= expectedRight + 0.01f);
                if (w > 0)
                    Assert.True(word.X >= line.Words[w - 1].X + line.Words[w - 1].Width - 0.01f);
            }
        }

        // Final line is centered inside the content box, not stretched awkwardly
        var finalLine = block.Lines[^1];
        Assert.False(finalLine.IsJustified);
        var containerCenterX = container.Left + container.Width / 2f;
        var finalLineCenterX = (finalLine.Left + finalLine.Right) / 2f;
        Assert.InRange(finalLineCenterX, containerCenterX - 0.5f, containerCenterX + 0.5f);
    }

    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    public void AllLevelBriefingsAndIntroParagraphsStayWithinContainerAndSafeAreasAcrossAllLanguages(string cultureCode)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            Assert.True(LocalizationManager.TrySetCulture(cultureCode));

            var panel = new UiRect(16, 12, 288, 156);
            var contentRect = new UiRect(panel.Left, panel.Top, panel.Width, panel.Height - 18);
            const float paddingX = 12f;
            const float paddingY = 8f;

            // Approximate Array 14pt glyph widths at each preserved UI scale
            var measureTitle = FixedGlyphMeasure(8.2f * 0.68f);
            var measureHeading = FixedGlyphMeasure(7.5f * 0.50f);
            var measureObjective = FixedGlyphMeasure(7.5f * 0.54f);
            var measureTutorial = FixedGlyphMeasure(7.5f * 0.46f);

            var levelBriefingPrefixes = new[]
            {
                "Act01A", "Act01B", "Act01C",
                "Act02A", "Act02B", "Act02C",
                "Act03A", "Act03B", "Act03C"
            };

            foreach (var prefix in levelBriefingPrefixes)
            {
                var titleKey = $"{prefix}Title";
                var objectiveKey = $"{prefix}Objective";
                var tutorialKey = $"{prefix}Tutorial";
                var stacked = TextLayoutEngine.LayoutVerticalStack(
                    contentRect,
                    paddingX,
                    paddingY,
                    new[]
                    {
                        new TextSectionSpec(LocalizedText.Get(titleKey), 11f, 0f, measureTitle, false),
                        new TextSectionSpec(LocalizedText.Get("Objective"), 8f, 6f, measureHeading, false),
                        new TextSectionSpec(LocalizedText.Get(objectiveKey), 9.5f, 2f, measureObjective, true),
                        new TextSectionSpec(LocalizedText.Get("Tutorial"), 8f, 6f, measureHeading, false),
                        new TextSectionSpec(LocalizedText.Get(tutorialKey), 8.5f, 2f, measureTutorial, true)
                    });

                Assert.True(stacked.ContentTop >= contentRect.Top, $"{cultureCode}/{prefix}: top overflow");
                Assert.True(stacked.ContentBottom <= contentRect.Bottom + 0.01f, $"{cultureCode}/{prefix}: bottom overflow");

                foreach (var section in stacked.Sections)
                {
                    Assert.NotEmpty(section.Lines);
                    Assert.True(section.Left >= contentRect.Left + paddingX - 0.05f, $"{cultureCode}/{prefix}: left overflow");
                    Assert.True(section.Right <= contentRect.Right - paddingX + 0.05f, $"{cultureCode}/{prefix}: right overflow");
                    Assert.True(section.Top >= contentRect.Top - 0.05f);
                    Assert.True(section.Bottom <= contentRect.Bottom + 0.05f);
                }
            }

            // Verify intro paragraphs also stay strictly within virtual safe area (320px minus 16px margins)
            var introKeys = Enumerable.Range(1, 13).Select(i => $"IntroLine{i:00}").ToArray();
            var rawIntro = introKeys.Select(LocalizedText.Get).ToArray();
            const float safeWidth = 320f - 32f;
            const float maxHorizontalScale = 1.03f;
            var unscaledMaxWidth = safeWidth / maxHorizontalScale;
            var measureWide = FixedGlyphMeasure(9.5f);

            var introLines = TextLayoutEngine.LayoutIntroParagraphs(rawIntro, unscaledMaxWidth, measureWide);
            Assert.NotEmpty(introLines);
            foreach (var line in introLines.Where(l => !l.IsEmpty))
            {
                foreach (var word in line.Words)
                {
                    var leftAtMaxDepth = 160f + (word.X - line.BlockWidth / 2f) * maxHorizontalScale;
                    var rightAtMaxDepth = 160f + (word.X + word.Width - line.BlockWidth / 2f) * maxHorizontalScale;
                    Assert.True(leftAtMaxDepth >= 16f - 0.05f, $"{cultureCode}: intro left overflow ({leftAtMaxDepth})");
                    Assert.True(rightAtMaxDepth <= 304f + 0.05f, $"{cultureCode}: intro right overflow ({rightAtMaxDepth})");
                }
            }
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previousCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentCulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousUiCulture;
        }
    }
}
