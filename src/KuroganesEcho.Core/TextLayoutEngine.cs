using System;
using System.Collections.Generic;

namespace KuroganesEcho.Core;

internal readonly record struct UiPoint(int X, int Y);

internal readonly record struct UiRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public UiPoint Center => new(Left + Width / 2, Top + Height / 2);

    public bool Contains(UiPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}

internal readonly record struct PositionedWord(string Text, float X, float Y, float Width);

internal sealed class TextLineLayout
{
    public TextLineLayout(
        string text,
        IReadOnlyList<PositionedWord> words,
        float left,
        float right,
        float top,
        float height,
        bool isJustified,
        bool isParagraphEnd)
    {
        Text = text;
        Words = words;
        Left = left;
        Right = right;
        Top = top;
        Height = height;
        IsJustified = isJustified;
        IsParagraphEnd = isParagraphEnd;
    }

    public string Text { get; }
    public IReadOnlyList<PositionedWord> Words { get; }
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Height { get; }
    public float Bottom => Top + Height;
    public float Width => Math.Max(0f, Right - Left);
    public bool IsJustified { get; }
    public bool IsParagraphEnd { get; }
}

internal sealed class TextBlockLayout
{
    public TextBlockLayout(
        IReadOnlyList<TextLineLayout> lines,
        UiRect containerBounds,
        float left,
        float right,
        float top,
        float bottom)
    {
        Lines = lines;
        ContainerBounds = containerBounds;
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public IReadOnlyList<TextLineLayout> Lines { get; }
    public UiRect ContainerBounds { get; }
    public float Left { get; }
    public float Right { get; }
    public float Top { get; }
    public float Bottom { get; }
    public float Width => Math.Max(0f, Right - Left);
    public float Height => Math.Max(0f, Bottom - Top);
}

internal readonly record struct TextSectionSpec(
    string Text,
    float LineHeight,
    float TopSpacing,
    Func<string, float> MeasureWidth,
    bool JustifyMultiline);

internal sealed class StackedTextLayout
{
    public StackedTextLayout(
        IReadOnlyList<TextBlockLayout> sections,
        UiRect containerBounds,
        float contentTop,
        float contentBottom)
    {
        Sections = sections;
        ContainerBounds = containerBounds;
        ContentTop = contentTop;
        ContentBottom = contentBottom;
    }

    public IReadOnlyList<TextBlockLayout> Sections { get; }
    public UiRect ContainerBounds { get; }
    public float ContentTop { get; }
    public float ContentBottom { get; }
}

internal sealed class IntroParagraphLine
{
    public IntroParagraphLine(
        bool isEmpty,
        string text,
        IReadOnlyList<PositionedWord> words,
        float blockWidth,
        float lineLeft,
        float lineRight,
        bool isJustified)
    {
        IsEmpty = isEmpty;
        Text = text;
        Words = words;
        BlockWidth = blockWidth;
        LineLeft = lineLeft;
        LineRight = lineRight;
        IsJustified = isJustified;
    }

    public bool IsEmpty { get; }
    public string Text { get; }
    public IReadOnlyList<PositionedWord> Words { get; }
    public float BlockWidth { get; }
    public float LineLeft { get; }
    public float LineRight { get; }
    public bool IsJustified { get; }
}

/// <summary>
/// Width-aware text wrapping, horizontal justification, and container-safe centering
/// driven by actual font measurement delegates.
/// </summary>
internal static class TextLayoutEngine
{
    /// <summary>
    /// Wraps text into lines that never exceed <paramref name="maxWidth"/> according to
    /// <paramref name="measureWidth"/>. Oversized single words are split by character.
    /// </summary>
    public static List<string> WrapText(string text, float maxWidth, Func<string, float> measureWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || measureWidth is null)
            return lines;

        var safeMaxWidth = Math.Max(1f, maxWidth);
        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;

            foreach (var rawWord in words)
            {
                if (string.IsNullOrEmpty(current))
                {
                    if (measureWidth(rawWord) <= safeMaxWidth)
                    {
                        current = rawWord;
                    }
                    else
                    {
                        AppendBrokenWord(rawWord, safeMaxWidth, measureWidth, lines, out current);
                    }
                }
                else
                {
                    var candidate = $"{current} {rawWord}";
                    if (measureWidth(candidate) <= safeMaxWidth)
                    {
                        current = candidate;
                    }
                    else
                    {
                        lines.Add(current);
                        if (measureWidth(rawWord) <= safeMaxWidth)
                        {
                            current = rawWord;
                        }
                        else
                        {
                            AppendBrokenWord(rawWord, safeMaxWidth, measureWidth, lines, out current);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(current))
                lines.Add(current);
        }

        return lines;
    }

    /// <summary>
    /// Lays out a text block horizontally centered inside <paramref name="containerBounds"/>
    /// with optional horizontal justification for multiline descriptive text.
    /// </summary>
    public static TextBlockLayout LayoutBlock(
        string text,
        UiRect containerBounds,
        float horizontalPadding,
        float topY,
        float lineHeight,
        Func<string, float> measureWidth,
        bool justifyMultiline = false)
    {
        var safePadding = Math.Max(0f, horizontalPadding);
        var contentWidth = Math.Max(1f, containerBounds.Width - safePadding * 2f);
        var blockLeft = containerBounds.Left + (containerBounds.Width - contentWidth) / 2f;
        var blockRight = blockLeft + contentWidth;

        var lineLayouts = new List<TextLineLayout>();
        if (string.IsNullOrWhiteSpace(text) || measureWidth is null)
            return new TextBlockLayout(lineLayouts, containerBounds, blockLeft, blockLeft, topY, topY);

        var paragraphs = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var y = topY;
        var minLeft = float.PositiveInfinity;
        var maxRight = float.NegativeInfinity;

        foreach (var paragraph in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            var wrapped = WrapText(paragraph, contentWidth, measureWidth);
            for (var index = 0; index < wrapped.Count; index++)
            {
                var lineText = wrapped[index];
                var isParagraphEnd = index == wrapped.Count - 1;
                var shouldJustify = justifyMultiline && !isParagraphEnd;
                var lineLayout = BuildLineLayout(
                    lineText,
                    blockLeft,
                    contentWidth,
                    y,
                    lineHeight,
                    measureWidth,
                    shouldJustify,
                    isParagraphEnd);

                lineLayouts.Add(lineLayout);
                minLeft = Math.Min(minLeft, lineLayout.Left);
                maxRight = Math.Max(maxRight, lineLayout.Right);
                y += lineHeight;
            }
        }

        if (lineLayouts.Count == 0)
            return new TextBlockLayout(lineLayouts, containerBounds, blockLeft, blockLeft, topY, topY);

        return new TextBlockLayout(
            lineLayouts,
            containerBounds,
            Math.Max(containerBounds.Left, minLeft),
            Math.Min(blockRight, maxRight),
            topY,
            y);
    }

    /// <summary>
    /// Vertically stacks and centers multiple text sections inside <paramref name="containerBounds"/>,
    /// compacting inter-section spacing if needed so content stays strictly inside the container.
    /// </summary>
    public static StackedTextLayout LayoutVerticalStack(
        UiRect containerBounds,
        float horizontalPadding,
        float verticalPadding,
        IReadOnlyList<TextSectionSpec> sections)
    {
        var safeVerticalPadding = Math.Max(0f, verticalPadding);
        var availableTop = containerBounds.Top + safeVerticalPadding;
        var availableBottom = containerBounds.Bottom - safeVerticalPadding;
        var availableHeight = Math.Max(1f, availableBottom - availableTop);

        if (sections is null || sections.Count == 0)
            return new StackedTextLayout(Array.Empty<TextBlockLayout>(), containerBounds, availableTop, availableTop);

        // First pass: measure each section at Y=0 to determine natural heights.
        var measured = new List<(TextBlockLayout Block, float TopSpacing, TextSectionSpec Spec)>(sections.Count);
        var totalLinesHeight = 0f;
        var totalSpacing = 0f;

        for (var i = 0; i < sections.Count; i++)
        {
            var spec = sections[i];
            var block = LayoutBlock(
                spec.Text,
                containerBounds,
                horizontalPadding,
                topY: 0f,
                spec.LineHeight,
                spec.MeasureWidth,
                spec.JustifyMultiline);
            var spacing = i == 0 ? 0f : Math.Max(0f, spec.TopSpacing);
            measured.Add((block, spacing, spec));
            totalLinesHeight += block.Height;
            totalSpacing += spacing;
        }

        var spacingScale = 1f;
        if (totalLinesHeight + totalSpacing > availableHeight && totalSpacing > 0f)
        {
            var remainingForSpacing = Math.Max(0f, availableHeight - totalLinesHeight);
            spacingScale = Math.Clamp(remainingForSpacing / totalSpacing, 0f, 1f);
        }

        var lineHeightScale = 1f;
        var scaledSpacingTotal = totalSpacing * spacingScale;
        if (totalLinesHeight + scaledSpacingTotal > availableHeight && totalLinesHeight > 0f)
        {
            lineHeightScale = Math.Clamp((availableHeight - scaledSpacingTotal) / totalLinesHeight, 0.5f, 1f);
        }

        var finalContentHeight = totalLinesHeight * lineHeightScale + scaledSpacingTotal;
        var startY = availableTop + Math.Max(0f, (availableHeight - finalContentHeight) / 2f);

        var finalBlocks = new List<TextBlockLayout>(sections.Count);
        var cursorY = startY;

        for (var i = 0; i < measured.Count; i++)
        {
            var (_, spacing, spec) = measured[i];
            cursorY += spacing * spacingScale;
            var block = LayoutBlock(
                spec.Text,
                containerBounds,
                horizontalPadding,
                cursorY,
                spec.LineHeight * lineHeightScale,
                spec.MeasureWidth,
                spec.JustifyMultiline);
            finalBlocks.Add(block);
            cursorY = block.Bottom;
        }

        return new StackedTextLayout(finalBlocks, containerBounds, startY, cursorY);
    }

    /// <summary>
    /// Reflows localized intro lines into paragraphs (blank entries act as paragraph breaks),
    /// wrapping and justifying each paragraph to <paramref name="maxWidth"/> so no line overflows
    /// regardless of language length.
    /// </summary>
    public static List<IntroParagraphLine> LayoutIntroParagraphs(
        IReadOnlyList<string> rawLines,
        float maxWidth,
        Func<string, float> measureWidth)
    {
        var result = new List<IntroParagraphLine>();
        if (rawLines is null || rawLines.Count == 0 || measureWidth is null)
            return result;

        var safeMaxWidth = Math.Max(1f, maxWidth);
        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();

        foreach (var raw in rawLines)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                if (currentParagraph.Count > 0)
                {
                    paragraphs.Add(string.Join(" ", currentParagraph));
                    currentParagraph.Clear();
                }
            }
            else
            {
                currentParagraph.Add(raw.Trim());
            }
        }

        if (currentParagraph.Count > 0)
            paragraphs.Add(string.Join(" ", currentParagraph));

        for (var pIndex = 0; pIndex < paragraphs.Count; pIndex++)
        {
            if (pIndex > 0)
            {
                result.Add(new IntroParagraphLine(
                    isEmpty: true,
                    text: string.Empty,
                    words: Array.Empty<PositionedWord>(),
                    blockWidth: safeMaxWidth,
                    lineLeft: 0f,
                    lineRight: 0f,
                    isJustified: false));
            }

            var wrapped = WrapText(paragraphs[pIndex], safeMaxWidth, measureWidth);
            for (var lIndex = 0; lIndex < wrapped.Count; lIndex++)
            {
                var isParagraphEnd = lIndex == wrapped.Count - 1;
                var lineLayout = BuildLineLayout(
                    wrapped[lIndex],
                    blockLeft: 0f,
                    contentWidth: safeMaxWidth,
                    y: 0f,
                    lineHeight: 0f,
                    measureWidth: measureWidth,
                    shouldJustify: !isParagraphEnd,
                    isParagraphEnd: isParagraphEnd);

                result.Add(new IntroParagraphLine(
                    isEmpty: false,
                    text: lineLayout.Text,
                    words: lineLayout.Words,
                    blockWidth: safeMaxWidth,
                    lineLeft: lineLayout.Left,
                    lineRight: lineLayout.Right,
                    isJustified: lineLayout.IsJustified));
            }
        }

        return result;
    }

    private static TextLineLayout BuildLineLayout(
        string lineText,
        float blockLeft,
        float contentWidth,
        float y,
        float lineHeight,
        Func<string, float> measureWidth,
        bool shouldJustify,
        bool isParagraphEnd)
    {
        var words = lineText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return new TextLineLayout(
                string.Empty,
                Array.Empty<PositionedWord>(),
                blockLeft,
                blockLeft,
                y,
                lineHeight,
                isJustified: false,
                isParagraphEnd: isParagraphEnd);
        }

        if (shouldJustify && words.Length >= 2)
        {
            var wordWidths = new float[words.Length];
            var totalWordWidth = 0f;
            for (var i = 0; i < words.Length; i++)
            {
                wordWidths[i] = Math.Max(0f, measureWidth(words[i]));
                totalWordWidth += wordWidths[i];
            }

            var extraSpace = contentWidth - totalWordWidth;
            if (extraSpace >= 0f)
            {
                var gap = extraSpace / (words.Length - 1);
                var positioned = new List<PositionedWord>(words.Length);
                var x = blockLeft;
                for (var i = 0; i < words.Length; i++)
                {
                    if (i == words.Length - 1)
                        x = Math.Max(x, blockLeft + contentWidth - wordWidths[i]);

                    positioned.Add(new PositionedWord(words[i], x, y, wordWidths[i]));
                    x += wordWidths[i] + gap;
                }

                var right = positioned[^1].X + positioned[^1].Width;
                return new TextLineLayout(
                    lineText,
                    positioned,
                    blockLeft,
                    Math.Min(blockLeft + contentWidth, right),
                    y,
                    lineHeight,
                    isJustified: true,
                    isParagraphEnd: isParagraphEnd);
            }
        }

        var lineWidth = Math.Min(contentWidth, Math.Max(0f, measureWidth(lineText)));
        var centeredLeft = blockLeft + Math.Max(0f, (contentWidth - lineWidth) / 2f);
        var singleEntry = new[] { new PositionedWord(lineText, centeredLeft, y, lineWidth) };
        return new TextLineLayout(
            lineText,
            singleEntry,
            centeredLeft,
            centeredLeft + lineWidth,
            y,
            lineHeight,
            isJustified: false,
            isParagraphEnd: isParagraphEnd);
    }

    private static void AppendBrokenWord(
        string word,
        float maxWidth,
        Func<string, float> measureWidth,
        List<string> lines,
        out string remainder)
    {
        remainder = string.Empty;
        var start = 0;
        while (start < word.Length)
        {
            var length = 1;
            while (start + length < word.Length &&
                   measureWidth(word.Substring(start, length + 1)) <= maxWidth)
            {
                length++;
            }

            var chunk = word.Substring(start, length);
            start += length;
            if (start < word.Length)
                lines.Add(chunk);
            else
                remainder = chunk;
        }
    }
}
