using System.Text;
using System.Text.RegularExpressions;
using ChunkingLibrary.Models;
using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Splitters;

public static class SentenceSplitter
{
    private static readonly Regex _whitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // A sentence boundary is "[.!?]" not preceded by a digit (excludes numbered-list
    // markers like "1." and decimals like "3.14"), followed by whitespace, followed by
    // an uppercase letter, digit, quote, opening bracket, or end of text. "\n\n" is
    // always a hard boundary regardless of punctuation.
    private static readonly Regex _sentenceBoundaryRegex = new(
        @"((?<=[.!?])(?<!\d[.!?])\s+(?=[A-Z0-9""'(\[]|$)|\n\n)",
        RegexOptions.Compiled);

    public static List<ChunkResult> SplitBySentence(
        string text,
        int chunkSize,
        int overlapSize,
        int sentencesPerChunk,
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

        // Guard: sentencesPerChunk
        if (sentencesPerChunk < 0)
            throw new ArgumentException(
                $"sentencesPerChunk ({sentencesPerChunk}) must be zero or greater. Use 0 for no sentence-count cap.");

        // Guard: empty input
        if (string.IsNullOrWhiteSpace(text))
            return [];

        text = TextPreprocessor.StripHorizontalRules(text);
        if (string.IsNullOrWhiteSpace(text))
            return [];

        // Pre-pass: replace fenced code blocks and Markdown tables with placeholders so
        // sentence detection cannot split them internally.
        var preserved = new Dictionary<string, string>();
        string workingText = BlockPreserver.SubstituteBlocks(text, preserved);

        if (normalizeWhitespace)
            workingText = _whitespaceRegex.Replace(workingText, " ").Trim();

        // Split on sentence boundaries, reattaching each delimiter to its preceding piece
        // so the original text can be reconstructed by simple concatenation.
        string[] rawPieces = _sentenceBoundaryRegex.Split(workingText);
        var sentencePieces = new List<string>();
        for (int i = 0; i < rawPieces.Length; i += 2)
        {
            string piece = rawPieces[i];
            if (i + 1 < rawPieces.Length)
                piece += rawPieces[i + 1];

            if (piece.Length > 0)
                sentencePieces.Add(piece);
        }

        // Net size delta each placeholder contributes once restored to its original content,
        // so the packing loop below can budget against chunkSize using restored sizes rather
        // than the short ___CHUNKLIBBLOCKn___ token lengths.
        var restoredSizeDeltas = preserved.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Length - kvp.Key.Length);

        // Greedily pack sentence pieces into chunks up to chunkSize.
        var packedPieces = new List<string>();
        var current = new StringBuilder();
        int currentSentenceCount = 0;

        foreach (string piece in sentencePieces)
        {
            int restoredPieceLength = GetRestoredLength(piece, restoredSizeDeltas);
            bool isOversizedPlaceholder = piece.Contains("___CHUNKLIBBLOCK") && restoredPieceLength > chunkSize;

            if (isOversizedPlaceholder)
            {
                if (current.Length > 0)
                {
                    packedPieces.Add(current.ToString().Trim());
                    current.Clear();
                    currentSentenceCount = 0;
                }
                packedPieces.Add(piece.Trim());
                continue;
            }

            if (piece.Length > chunkSize)
            {
                if (current.Length > 0)
                {
                    packedPieces.Add(current.ToString().Trim());
                    current.Clear();
                    currentSentenceCount = 0;
                }
                packedPieces.AddRange(CharacterFallback(piece, chunkSize));
                continue;
            }

            int restoredRunningTotal = GetRestoredLength(current.ToString(), restoredSizeDeltas);
            bool exceedsSize = restoredRunningTotal + restoredPieceLength > chunkSize && current.Length > 0;
            bool exceedsSentenceCount = sentencesPerChunk > 0 && currentSentenceCount >= sentencesPerChunk && current.Length > 0;

            if (exceedsSize || exceedsSentenceCount)
            {
                packedPieces.Add(current.ToString().Trim());
                current.Clear();
                currentSentenceCount = 0;
            }

            current.Append(piece);
            currentSentenceCount++;
        }

        if (current.Length > 0)
            packedPieces.Add(current.ToString().Trim());

        // Build ChunkResult list with overlap applied
        var results = new List<ChunkResult>();
        int sequenceNo = 1;
        int cursor = 0;
        bool hasPreserved = preserved.Count > 0;
        string? prevChunkRestoredText = null;

        for (int i = 0; i < packedPieces.Count; i++)
        {
            string piece = packedPieces[i];

            // Note: IndexOf may find the wrong occurrence if the same substring appears
            // multiple times in the remaining text. StartCharIndex/EndCharIndex are
            // approximate in that case — chunk text is always correct, only the index
            // metadata may be off.
            int pieceStartInText = workingText.IndexOf(piece, cursor, StringComparison.Ordinal);
            if (pieceStartInText < 0)
                pieceStartInText = cursor; // fallback: shouldn't happen for well-formed input

            string chunkText;
            int chunkStartInText;

            // Character-based overlap is semantically mismatched with sentencesPerChunk=1:
            // against a single short sentence it either reproduces nearly the whole previous
            // chunk (overlapSize >= chunk length) or lands mid-sentence (overlapSize < chunk
            // length). Skip overlap injection entirely in that case, regardless of overlapSize.
            if (i == 0 || sentencesPerChunk == 1)
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
                    Strategy = "Sentence",
                    SourceType = "PlainText",
                    StartCharIndex = chunkStartInText,
                    EndCharIndex = chunkStartInText + chunkText.Length - 1,
                    TokenEstimate = TokenEstimator.EstimateTokens(chunkText),
                    Hash = HashHelper.GenerateSha256Hash(chunkText),
                    HeadingPath = string.Empty,
                    EmbeddingReady = true
                }
            });

            cursor = pieceStartInText + piece.Length;
            sequenceNo++;
        }

        return results;
    }

    /// <summary>
    /// Returns the length <paramref name="piece"/> would have after placeholder restoration,
    /// by adding each placeholder's net size delta for every placeholder it contains.
    /// </summary>
    private static int GetRestoredLength(string piece, Dictionary<string, int> restoredSizeDeltas)
    {
        int length = piece.Length;
        foreach (var (placeholder, delta) in restoredSizeDeltas)
        {
            if (piece.Contains(placeholder, StringComparison.Ordinal))
                length += delta;
        }
        return length;
    }

    /// <summary>
    /// Slices <paramref name="text"/> into pieces each at most <paramref name="chunkSize"/> characters.
    /// When the text contains spaces, cuts at the last space before the limit so words are never split.
    /// Falls back to a hard character cut only when no space exists in the window (e.g. a URL or long token).
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

            int splitIndex = text.LastIndexOf(' ', chunkSize - 1);

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
