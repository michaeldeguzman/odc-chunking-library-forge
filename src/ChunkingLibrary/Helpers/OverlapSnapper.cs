namespace ChunkingLibrary.Helpers;

public static class OverlapSnapper
{
    // Only ". ", "\n", "\n\n" are sentence boundaries — commas and spaces are
    // not and are excluded from this search.
    private static readonly string[] _sentenceSeparators = ["\n\n", "\n", ". "];

    /// <summary>
    /// Returns the suffix of <paramref name="prevChunkText"/> to prepend as overlap to the
    /// next chunk, snapped to the nearest sentence-level boundary so the next chunk never
    /// begins mid-sentence.
    ///
    /// Phase 1: search forward from (prevChunkText.Length - overlapSize) for the next
    /// "\n\n", "\n", or ". " boundary.
    /// Phase 2: if none found forward, search backward up to overlapSize chars for the
    /// most recent such boundary.
    /// Phase 3: fall back to the nearest word boundary (" ").
    /// </summary>
    public static string GetSentenceSnappedOverlap(string prevChunkText, int overlapSize)
    {
        int rawOverlapStart = prevChunkText.Length - overlapSize;
        if (rawOverlapStart < 0) rawOverlapStart = 0;

        int snappedStart = -1;

        // Phase 1: search forward from rawOverlapStart for the next sentence boundary.
        foreach (string sep in _sentenceSeparators)
        {
            int idx = prevChunkText.IndexOf(sep, rawOverlapStart, StringComparison.Ordinal);
            if (idx >= 0 && idx + sep.Length < prevChunkText.Length)
            {
                snappedStart = idx + sep.Length;
                break;
            }
        }

        // Phase 2: if none found forward, search backward for the most recent sentence
        // boundary within overlapSize chars before rawOverlapStart.
        if (snappedStart < 0 && rawOverlapStart > 0)
        {
            int count = Math.Min(rawOverlapStart, overlapSize);
            foreach (string sep in _sentenceSeparators)
            {
                int idx = prevChunkText.LastIndexOf(
                    sep, rawOverlapStart - 1, count, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    snappedStart = idx + sep.Length;
                    break;
                }
            }
        }

        // Phase 3: no sentence boundary found — fall back to nearest word boundary.
        if (snappedStart < 0 || snappedStart >= prevChunkText.Length)
        {
            int spaceIdx = prevChunkText.IndexOf(' ', rawOverlapStart);
            snappedStart = spaceIdx >= 0 ? spaceIdx + 1 : rawOverlapStart;
        }

        return prevChunkText[snappedStart..];
    }

    /// <summary>
    /// Joins an overlap prefix to the following piece, inserting a single space at the seam
    /// if neither side already has whitespace there. Both <see cref="GetSentenceSnappedOverlap"/>
    /// and the packing/merge passes can trim away the original separator at this boundary,
    /// which would otherwise produce joins like "...here.Next sentence...".
    /// </summary>
    public static string JoinOverlapWithPiece(string overlapPrefix, string piece)
    {
        if (overlapPrefix.Length == 0 || piece.Length == 0)
            return overlapPrefix + piece;

        bool needsSpace = !char.IsWhiteSpace(overlapPrefix[^1]) && !char.IsWhiteSpace(piece[0]);
        return needsSpace ? overlapPrefix + " " + piece : overlapPrefix + piece;
    }
}
