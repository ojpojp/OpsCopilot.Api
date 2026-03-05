using System.Text;
using System.Text.RegularExpressions;

namespace OpsCopilot.Api.Kb;

public sealed record ChunkPreview(int ChunkIndex, string? Section, int ContentLength, string Content);

public sealed class MarkdownChunker
{
    /// <summary>
    /// Matches Markdown ATX headings (H1-H6) with up to 3 leading spaces.
    /// Captures:
    /// - Group 1: the hash prefix (e.g., "#", "##", "###")
    /// - Group 2: the heading text (used as the current section name)
    ///
    /// Examples (matched):
    /// - "# Title"
    /// - "## Section A"
    /// - "   ### Indented heading"
    /// Examples (not matched):
    /// - "####### Too many hashes"
    /// - "#Title" (missing space)
    /// </summary>
    private static readonly Regex HeadingRegex = new(@"^\s{0,3}(#{1,6})\s+(.+?)\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Chunks Markdown text into roughly-sized pieces suitable for retrieval.
    /// Strategy:
    /// - Split into units by headings and blank lines.
    /// - If a unit is too large, split it further by punctuation/newlines.
    /// - Pack units into chunks with optional character overlap.
    /// </summary>
    public IReadOnlyList<ChunkPreview> Chunk(string markdown, KbChunkingOptions options)
    {
        if (options.ChunkSizeChars <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkSizeChars), "ChunkSizeChars must be > 0");
        }

        if (options.ChunkOverlapChars < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkOverlapChars), "ChunkOverlapChars must be >= 0");
        }

        if (options.ChunkOverlapChars >= options.ChunkSizeChars)
        {
            throw new ArgumentOutOfRangeException(nameof(options.ChunkOverlapChars), "ChunkOverlapChars must be < ChunkSizeChars");
        }

        var normalized = Normalize(markdown);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<ChunkPreview>();
        }

        var units = SplitToUnits(normalized);
        var expandedUnits = ExpandOversizedUnits(units, options.ChunkSizeChars);

        return PackWithOverlap(expandedUnits, options.ChunkSizeChars, options.ChunkOverlapChars);
    }

    /// <summary>
    /// Normalizes newlines to <c>\n</c> and trims leading/trailing whitespace.
    /// </summary>
    private static string Normalize(string markdown)
    {
        return markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    /// <summary>
    /// Splits Markdown into paragraph-like units tagged with the latest seen heading (section).
    /// A unit ends when a blank line or a new heading is encountered.
    /// </summary>
    private static List<(string? Section, string Text)> SplitToUnits(string markdown)
    {
        var units = new List<(string? Section, string Text)>();

        string? currentSection = null;
        var paragraph = new StringBuilder();

        /// <summary>
        /// Commits the currently buffered paragraph as a unit (if non-empty), then clears the buffer.
        /// Called when we encounter a heading, a blank line, or reach end-of-file.
        /// </summary>
        void FlushParagraph()
        {
            var text = paragraph.ToString().Trim();
            paragraph.Clear();

            if (!string.IsNullOrWhiteSpace(text))
            {
                units.Add((currentSection, text));
            }
        }

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd();

            var headingMatch = HeadingRegex.Match(line);
            if (headingMatch.Success)
            {
                FlushParagraph();
                currentSection = headingMatch.Groups[2].Value.Trim();
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append('\n');
            }

            paragraph.Append(line);
        }

        FlushParagraph();
        return units;
    }

    /// <summary>
    /// Ensures no unit exceeds <paramref name="chunkSizeChars"/> by splitting oversized units.
    /// </summary>
    private static List<(string? Section, string Text)> ExpandOversizedUnits(
        List<(string? Section, string Text)> units,
        int chunkSizeChars)
    {
        var expanded = new List<(string? Section, string Text)>(capacity: units.Count);

        foreach (var unit in units)
        {
            if (unit.Text.Length <= chunkSizeChars)
            {
                expanded.Add(unit);
                continue;
            }

            foreach (var part in SplitLongText(unit.Text, chunkSizeChars))
            {
                expanded.Add((unit.Section, part));
            }
        }

        return expanded;
    }

    /// <summary>
    /// Splits a long text into smaller parts, preferring to cut at punctuation/newlines.
    /// </summary>
    private static IEnumerable<string> SplitLongText(string text, int chunkSizeChars)
    {
        var remaining = text.Trim();
        while (remaining.Length > chunkSizeChars)
        {
            var slice = remaining[..chunkSizeChars];

            var cut = LastCutPosition(slice);
            if (cut <= 0)
            {
                cut = chunkSizeChars;
            }

            yield return remaining[..cut].Trim();
            remaining = remaining[cut..].TrimStart();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            yield return remaining;
        }
    }

    /// <summary>
    /// Finds the best cut position within <paramref name="slice"/> by searching from the end
    /// for a delimiter (newline or sentence punctuation). Returns the cut index (exclusive),
    /// or -1 when no delimiter is found.
    /// </summary>
    private static int LastCutPosition(string slice)
    {
        var delimiters = new[] { '\n', '。', '.', '!', '?', '！', '？', ';', '；' };

        var last = -1;
        foreach (var delimiter in delimiters)
        {
            last = Math.Max(last, slice.LastIndexOf(delimiter));
        }

        return last >= 0 ? last + 1 : -1;
    }

    /// <summary>
    /// Packs units into chunks up to <paramref name="chunkSizeChars"/> and applies a simple character overlap.
    /// Overlap is implemented by prepending the last <paramref name="chunkOverlapChars"/> characters of the
    /// previous chunk to the next chunk.
    /// </summary>
    private static IReadOnlyList<ChunkPreview> PackWithOverlap(
        List<(string? Section, string Text)> units,
        int chunkSizeChars,
        int chunkOverlapChars)
    {
        var chunks = new List<ChunkPreview>();
        var index = 0;

        string overlapPrefix = string.Empty;
        var i = 0;

        while (i < units.Count)
        {
            var section = units[i].Section;
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(overlapPrefix))
            {
                sb.Append(overlapPrefix);
            }

            while (i < units.Count)
            {
                var unitText = units[i].Text;
                var separator = sb.Length == 0 ? string.Empty : "\n\n";

                if (sb.Length + separator.Length + unitText.Length > chunkSizeChars)
                {
                    break;
                }

                sb.Append(separator);
                sb.Append(unitText);
                i++;
            }

            var content = sb.ToString().Trim();
            if (content.Length == 0)
            {
                // Fallback: a single unit is too large (should be prevented by ExpandOversizedUnits).
                var forced = units[i].Text;
                var take = Math.Min(chunkSizeChars, forced.Length);
                content = forced[..take].Trim();
                i++;
            }

            overlapPrefix = chunkOverlapChars == 0 || content.Length <= chunkOverlapChars
                ? string.Empty
                : content[^chunkOverlapChars..].TrimStart();

            chunks.Add(new ChunkPreview(index++, section, content.Length, content));
        }

        return chunks;
    }
}
