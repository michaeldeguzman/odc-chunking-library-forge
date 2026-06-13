using ChunkingLibrary.Models;
using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Splitters;

public static class MarkdownSplitter
{
    public static List<ChunkResult> SplitMarkdown(
        string markdown,
        int chunkSize,
        int overlapSize,
        bool preserveHeadingContext,
        bool preserveCodeBlocks,
        bool preserveTables,
        int maxTotalChars,
        string documentId = "")
    {
        // Guard: maxTotalChars
        if (markdown.Length > maxTotalChars)
            throw new ArgumentException(
                $"Input text length ({markdown.Length}) exceeds maxTotalChars limit ({maxTotalChars}).");

        // Guard: chunkSize
        if (chunkSize <= 0)
            throw new ArgumentException(
                $"chunkSize ({chunkSize}) must be greater than zero.");

        // Guard: overlapSize
        if (overlapSize >= chunkSize)
            throw new ArgumentException(
                $"overlapSize ({overlapSize}) must be less than chunkSize ({chunkSize}).");

        // Guard: empty input
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        markdown = TextPreprocessor.StripHorizontalRules(markdown);
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        // Step A — Extract code blocks
        List<TextSegment> segments = preserveCodeBlocks
            ? CodeBlockPreserver.PreserveCodeBlocks(markdown)
            : [new TextSegment { Text = markdown, IsCode = false }];

        var results = new List<ChunkResult>();
        int sequenceNo = 1;

        // Heading state persists across segments to handle interleaved code/non-code
        var headingLevels = new string?[7]; // index 1-6 maps to H1-H6
        string currentHeadingPath = "";

        foreach (var segment in segments)
        {
            if (segment.IsCode)
            {
                // Emit code block as a single atomic chunk
                string codeText = segment.Text;
                string chunkText = (preserveHeadingContext && !string.IsNullOrEmpty(currentHeadingPath))
                    ? $"# {currentHeadingPath}\n\n{codeText}"
                    : codeText;

                results.Add(BuildChunkResult(chunkText, documentId, sequenceNo, currentHeadingPath));
                sequenceNo++;
            }
            else
            {
                // Step B — Split non-code segment by heading structure
                var sections = SplitIntoSections(segment.Text, headingLevels, ref currentHeadingPath);

                foreach (var (headingPath, sectionText) in sections)
                {
                    // Step C — Handle tables (if enabled): split section into table/non-table sub-segments
                    var subSegments = preserveTables
                        ? SplitByTables(sectionText)
                        : [sectionText];

                    foreach (string subText in subSegments)
                    {
                        if (string.IsNullOrWhiteSpace(subText))
                            continue;

                        // Step D — Split by chunkSize
                        List<string> pieces;
                        if (subText.Length <= chunkSize)
                        {
                            pieces = [subText];
                        }
                        else
                        {
                            var subChunks = RecursiveSplitter.SplitRecursively(
                                subText, chunkSize, overlapSize, null, false, int.MaxValue, "");
                            pieces = subChunks.Select(c => c.Text).ToList();
                        }

                        // Step E & F — Apply heading context and build ChunkResult
                        foreach (string piece in pieces)
                        {
                            if (string.IsNullOrWhiteSpace(piece))
                                continue;

                            string chunkText = (preserveHeadingContext && !string.IsNullOrEmpty(headingPath))
                                ? $"# {headingPath}\n\n{piece}"
                                : piece;

                            results.Add(BuildChunkResult(chunkText, documentId, sequenceNo, headingPath));
                            sequenceNo++;
                        }
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Splits a non-code segment's text into sections at heading boundaries.
    /// Updates the shared headingLevels array and currentHeadingPath reference.
    /// Returns a list of (headingPath, sectionText) tuples.
    /// </summary>
    private static List<(string headingPath, string sectionText)> SplitIntoSections(
        string text,
        string?[] headingLevels,
        ref string currentHeadingPath)
    {
        var sections = new List<(string headingPath, string sectionText)>();
        var lines = text.Split('\n');

        string sectionHeadingPath = currentHeadingPath;
        var currentLines = new List<string>();

        foreach (string line in lines)
        {
            if (IsHeadingLine(line, out int headingLevel, out string headingTitle))
            {
                // Flush accumulated lines as previous section — skip if heading-only (no body text)
                if (currentLines.Count > 0)
                {
                    bool startsWithHeading = IsHeadingLine(currentLines[0], out _, out _);
                    bool hasBody = !startsWithHeading || currentLines.Skip(1).Any(l => !string.IsNullOrWhiteSpace(l));
                    if (hasBody)
                    {
                        string sectionText = string.Join("\n", currentLines);
                        if (!string.IsNullOrWhiteSpace(sectionText))
                            sections.Add((sectionHeadingPath, sectionText));
                    }
                    currentLines.Clear();
                }

                // Update heading path
                currentHeadingPath = UpdateHeadingPath(headingLevels, headingLevel, headingTitle);
                sectionHeadingPath = currentHeadingPath;
                // The heading line is included in the section body so the raw Markdown is preserved.
                // When preserveHeadingContext=true the heading path is prepended as a breadcrumb,
                // which means the heading appears twice — once as "# path" and once as the raw line.
                // This is intentional: the breadcrumb gives full ancestor context; the raw line
                // preserves the original structure for downstream renderers.
                currentLines.Add(line);
            }
            else
            {
                currentLines.Add(line);
            }
        }

        // Flush final section — same heading-only guard as mid-document flushes
        if (currentLines.Count > 0)
        {
            bool startsWithHeading = IsHeadingLine(currentLines[0], out _, out _);
            bool hasBody = !startsWithHeading || currentLines.Skip(1).Any(l => !string.IsNullOrWhiteSpace(l));
            if (hasBody)
            {
                string sectionText = string.Join("\n", currentLines);
                if (!string.IsNullOrWhiteSpace(sectionText))
                    sections.Add((sectionHeadingPath, sectionText));
            }
        }

        return sections;
    }

    /// <summary>
    /// Determines whether a line is an ATX heading. Allows up to 3 leading spaces (CommonMark).
    /// </summary>
    private static bool IsHeadingLine(string line, out int level, out string title)
    {
        level = 0;
        title = string.Empty;

        // Skip up to 3 leading spaces (CommonMark ATX heading rule)
        int start = 0;
        while (start < 3 && start < line.Length && line[start] == ' ')
            start++;

        int i = start;
        while (i < line.Length && line[i] == '#')
            i++;

        int hashes = i - start;
        if (hashes == 0 || hashes > 6)
            return false;

        if (i >= line.Length || line[i] != ' ')
            return false;

        level = hashes;
        title = line[(i + 1)..].Trim();
        return true;
    }

    /// <summary>
    /// Updates the heading levels array and returns the new heading path string.
    /// Clears all heading levels deeper than the current heading level.
    /// </summary>
    private static string UpdateHeadingPath(string?[] headingLevels, int level, string title)
    {
        // Set the current level
        headingLevels[level] = title;

        // Clear all deeper levels
        for (int i = level + 1; i <= 6; i++)
            headingLevels[i] = null;

        // Build path from levels 1-6 that are non-null
        var pathParts = new List<string>();
        for (int i = 1; i <= 6; i++)
        {
            if (headingLevels[i] != null)
                pathParts.Add(headingLevels[i]!);
        }

        return string.Join(" > ", pathParts);
    }

    /// <summary>
    /// Splits a section's text into alternating non-table and table sub-segments.
    /// Table blocks are sequences of consecutive lines that start with '|'.
    /// </summary>
    private static List<string> SplitByTables(string text)
    {
        var subSegments = new List<string>();
        var lines = text.Split('\n');

        var tableLines = new List<string>();
        var nonTableLines = new List<string>();

        foreach (string line in lines)
        {
            bool isTableLine = line.TrimStart().StartsWith("|");

            if (isTableLine)
            {
                // Flush non-table lines
                if (nonTableLines.Count > 0)
                {
                    string nonTableText = string.Join("\n", nonTableLines);
                    if (!string.IsNullOrWhiteSpace(nonTableText))
                        subSegments.Add(nonTableText);
                    nonTableLines.Clear();
                }
                tableLines.Add(line);
            }
            else
            {
                // Flush table lines as an atomic block
                if (tableLines.Count > 0)
                {
                    subSegments.Add(string.Join("\n", tableLines));
                    tableLines.Clear();
                }
                nonTableLines.Add(line);
            }
        }

        // Flush remaining lines
        if (tableLines.Count > 0)
            subSegments.Add(string.Join("\n", tableLines));

        if (nonTableLines.Count > 0)
        {
            string nonTableText = string.Join("\n", nonTableLines);
            if (!string.IsNullOrWhiteSpace(nonTableText))
                subSegments.Add(nonTableText);
        }

        return subSegments;
    }

    private static ChunkResult BuildChunkResult(
        string chunkText,
        string documentId,
        int sequenceNo,
        string? headingPath)
    {
        return new ChunkResult
        {
            ChunkId = $"{documentId}-{sequenceNo:D4}",
            SequenceNo = sequenceNo,
            Text = chunkText,
            Metadata = new ChunkMetadata
            {
                DocumentId = documentId,
                Strategy = "Markdown",
                SourceType = "Markdown",
                StartCharIndex = 0,
                EndCharIndex = chunkText.Length - 1,
                TokenEstimate = TokenEstimator.EstimateTokens(chunkText),
                Hash = HashHelper.GenerateSha256Hash(chunkText),
                HeadingPath = headingPath ?? string.Empty,
                EmbeddingReady = true
            }
        };
    }
}
