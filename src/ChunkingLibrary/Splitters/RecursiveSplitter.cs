using System.Text.RegularExpressions;
using ChunkingLibrary.Models;
using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Splitters;

public static class RecursiveSplitter
{
    private static readonly Regex _whitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly List<string> _defaultSeparators = ["\n\n", "\n", ". ", ", ", " "];

    public static List<ChunkResult> SplitRecursively(
        string text,
        int chunkSize,
        int overlapSize,
        List<string>? separators,
        bool normalizeWhitespace,
        int maxTotalChars,
        string documentId = "")
    {
        // Guard: maxTotalChars
        if (text.Length > maxTotalChars)
            throw new ArgumentException(
                $"Input text length ({text.Length}) exceeds maxTotalChars limit ({maxTotalChars}).");

        // Guard: overlapSize
        if (overlapSize >= chunkSize)
            throw new ArgumentException(
                $"overlapSize ({overlapSize}) must be less than chunkSize ({chunkSize}).");

        // Guard: empty input
        if (string.IsNullOrWhiteSpace(text))
            return [];

        text = TextPreprocessor.StripHorizontalRules(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Pre-pass: replace fenced code blocks and Markdown tables with placeholders so the
        // recursive separator cascade cannot split them internally.
        var preserved = new Dictionary<string, string>();
        string workingText = BlockPreserver.SubstituteBlocks(text, preserved);

        if (normalizeWhitespace)
            workingText = _whitespaceRegex.Replace(workingText, " ").Trim();

        var effectiveSeparators = (separators is { Count: > 0 }) ? separators : _defaultSeparators;

        // Recursively split into leaf pieces each <= chunkSize
        var pieces = SplitTextRecursively(workingText, effectiveSeparators, chunkSize);

        // Build ChunkResult list with overlap applied
        var results = new List<ChunkResult>();
        int sequenceNo = 1;
        int cursor = 0;
        bool hasPreserved = preserved.Count > 0;
        string? prevChunkRestoredText = null;

        for (int i = 0; i < pieces.Count; i++)
        {
            string piece = pieces[i];

            // Locate the piece in the working text starting from cursor.
            // Note: IndexOf may find the wrong occurrence if the same substring appears multiple times
            // in the remaining text (e.g. repeated phrases). StartCharIndex/EndCharIndex are approximate
            // in that case — chunk text is always correct, only the index metadata may be off.
            int pieceStartInText = workingText.IndexOf(piece, cursor, StringComparison.Ordinal);
            if (pieceStartInText < 0)
                pieceStartInText = cursor; // fallback: shouldn't happen for well-formed input

            // Apply overlap: prepend tail of the restored previous chunk.
            // Using the restored text (not the raw placeholder) ensures oversized atomic blocks
            // contribute at most overlapSize chars — not the full restored block content.
            string chunkText;
            int chunkStartInText;

            if (i == 0)
            {
                chunkText = piece;
                chunkStartInText = pieceStartInText;
            }
            else
            {
                string overlapPrefix = OverlapSnapper.GetSentenceSnappedOverlap(prevChunkRestoredText!, overlapSize);
                chunkText = OverlapSnapper.JoinOverlapWithPiece(overlapPrefix, piece);
                chunkStartInText = Math.Max(0, pieceStartInText - overlapPrefix.Length);
            }

            // Post-pass: restore original code block / table content into this chunk
            if (hasPreserved)
                chunkText = BlockPreserver.RestoreBlocks(chunkText, preserved);

            prevChunkRestoredText = chunkText;

            results.Add(new ChunkResult
            {
                ChunkId = $"{documentId}-{sequenceNo:D4}",
                SequenceNo = sequenceNo,
                Text = chunkText,
                Metadata = new ChunkMetadata
                {
                    DocumentId = documentId,
                    Strategy = "Recursive",
                    SourceType = "PlainText",
                    StartCharIndex = chunkStartInText,
                    EndCharIndex = chunkStartInText + chunkText.Length - 1,
                    TokenEstimate = TokenEstimator.EstimateTokens(chunkText),
                    Hash = HashHelper.GenerateSha256Hash(chunkText),
                    HeadingPath = string.Empty,
                    EmbeddingReady = true
                }
            });

            // Advance cursor past the current piece
            cursor = pieceStartInText + piece.Length;
            sequenceNo++;
        }

        return results;
    }

    /// <summary>
    /// Recursively splits <paramref name="text"/> using the ordered <paramref name="separators"/>
    /// until every leaf piece is at most <paramref name="chunkSize"/> characters long.
    /// Adjacent small pieces at each separator level are coalesced via <see cref="MergeSplits"/>
    /// so that standalone heading lines and other short fragments do not become tiny pieces
    /// that produce near-duplicate "transition" chunks when overlap is applied.
    /// </summary>
    private static List<string> SplitTextRecursively(string text, List<string> separators, int chunkSize)
    {
        if (text.Length <= chunkSize)
            return [text];

        string separator = separators[0];
        string[] rawPieces = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        // If the separator wasn't found, Split returns the original string as the only element.
        // In that case, try the next separator instead of recursing with the same text.
        if (rawPieces.Length == 1 && rawPieces[0] == text)
        {
            if (separators.Count > 1)
                return SplitTextRecursively(text, separators[1..], chunkSize);

            // No separators left — character fallback
            return CharacterFallback(text, chunkSize);
        }

        var result = new List<string>();
        var pending = new List<string>(); // small pieces waiting to be merged

        foreach (string piece in rawPieces)
        {
            // Placeholder tokens must never be merged with adjacent pieces — their restored
            // content may be much larger than their token length suggests (e.g. a 21-char
            // placeholder that expands to a 1000-char code block after restoration).
            bool isPlaceholder = piece.StartsWith("___CHUNKLIBBLOCK");

            if (!isPlaceholder && piece.Length <= chunkSize)
            {
                pending.Add(piece);
            }
            else
            {
                // Flush accumulated small pieces before handling this piece
                if (pending.Count > 0)
                {
                    result.AddRange(MergeSplits(pending, separator, chunkSize));
                    pending.Clear();
                }

                if (isPlaceholder)
                    result.Add(piece);
                else if (separators.Count > 1)
                    result.AddRange(SplitTextRecursively(piece, separators[1..], chunkSize));
                else
                    result.AddRange(CharacterFallback(piece, chunkSize));
            }
        }

        if (pending.Count > 0)
            result.AddRange(MergeSplits(pending, separator, chunkSize));

        return result;
    }

    /// <summary>
    /// Coalesces adjacent <paramref name="splits"/> into the fewest pieces that each fit within
    /// <paramref name="chunkSize"/>, joining with <paramref name="separator"/>.
    /// Prevents standalone heading lines and other short fragments from becoming isolated tiny pieces.
    /// </summary>
    private static List<string> MergeSplits(List<string> splits, string separator, int chunkSize)
    {
        var result = new List<string>();
        var current = new List<string>();
        int currentLen = 0;

        foreach (string split in splits)
        {
            int addLen = split.Length + (current.Count > 0 ? separator.Length : 0);

            if (currentLen + addLen > chunkSize && current.Count > 0)
            {
                result.Add(string.Join(separator, current));
                current.Clear();
                currentLen = 0;
            }

            current.Add(split);
            currentLen += current.Count == 1 ? split.Length : split.Length + separator.Length;
        }

        if (current.Count > 0)
            result.Add(string.Join(separator, current));

        return result;
    }

    /// <summary>
    /// Slices <paramref name="text"/> into pieces each at most <paramref name="chunkSize"/> characters.
    /// When the text contains spaces, cuts at the last space before the limit so words are never split.
    /// Falls back to a hard character cut only when no space exists in the window (e.g. a URL or long token).
    /// Overlap is applied at the outer level; this method produces non-overlapping base pieces.
    /// </summary>
    private static List<string> CharacterFallback(string text, int chunkSize)
    {
        var pieces = new List<string>();

        while (text.Length > 0)
        {
            if (text.Length <= chunkSize)
            {
                pieces.Add(text);
                break;
            }

            // Find the last space at or before the chunk size limit.
            int splitIndex = text.LastIndexOf(' ', chunkSize - 1);

            // No space within the limit — hard character cut.
            if (splitIndex <= 0)
                splitIndex = chunkSize;

            string piece = text[..splitIndex].TrimEnd();
            if (piece.Length > 0)
                pieces.Add(piece);

            text = text[splitIndex..].TrimStart();
        }

        return pieces;
    }
}
