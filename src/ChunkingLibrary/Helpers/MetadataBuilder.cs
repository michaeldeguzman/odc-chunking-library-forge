using ChunkingLibrary.Models;

namespace ChunkingLibrary.Helpers;

public static class MetadataBuilder
{
    public static ChunkMetadata BuildChunkMetadata(
        string chunkText,
        int sequenceNo,
        int startCharIndex,
        int endCharIndex,
        string strategy,
        string sourceType,
        string? headingPath,
        string documentId)
    {
        return new ChunkMetadata
        {
            DocumentId = documentId,
            Strategy = strategy,
            SourceType = sourceType,
            StartCharIndex = startCharIndex,
            EndCharIndex = endCharIndex,
            TokenEstimate = TokenEstimator.EstimateTokens(chunkText),
            Hash = HashHelper.GenerateSha256Hash(chunkText),
            HeadingPath = headingPath ?? string.Empty,
            EmbeddingReady = true
        };
    }
}
