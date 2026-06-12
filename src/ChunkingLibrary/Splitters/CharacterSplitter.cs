using System.Text.RegularExpressions;
using ChunkingLibrary.Models;
using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Splitters;

public static class CharacterSplitter
{
    private static readonly Regex _whitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static List<ChunkResult> SplitByCharacter(
        string text,
        int chunkSize,
        int overlapSize,
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

        // Normalize whitespace
        if (normalizeWhitespace)
            text = _whitespaceRegex.Replace(text, " ").Trim();

        var results = new List<ChunkResult>();
        int startIndex = 0;
        int sequenceNo = 1;

        while (startIndex < text.Length)
        {
            string chunkText = text.Substring(startIndex, Math.Min(chunkSize, text.Length - startIndex));

            results.Add(new ChunkResult
            {
                ChunkId = $"{documentId}-{sequenceNo:D4}",
                SequenceNo = sequenceNo,
                Text = chunkText,
                Metadata = new ChunkMetadata
                {
                    DocumentId = documentId,
                    Strategy = "Character",
                    SourceType = "PlainText",
                    StartCharIndex = startIndex,
                    EndCharIndex = startIndex + chunkText.Length - 1,
                    TokenEstimate = TokenEstimator.EstimateTokens(chunkText),
                    Hash = HashHelper.GenerateSha256Hash(chunkText),
                    HeadingPath = string.Empty,
                    EmbeddingReady = true
                }
            });

            // The last window is always shorter than chunkSize — no further content remains.
            // Without this guard, the loop would produce one more chunk covering only the
            // overlap tail of this chunk (pure duplicate, zero new content).
            if (chunkText.Length < chunkSize)
                break;

            // Advance start: end of this chunk minus overlap
            int nextStart = startIndex + chunkText.Length - overlapSize;

            // Safety net: stop if the window doesn't advance
            if (nextStart <= startIndex)
                break;

            startIndex = nextStart;
            sequenceNo++;
        }

        return results;
    }
}
