using OutSystems.ExternalLibraries.SDK;
using ChunkingLibrary.Models;
using ChunkingLibrary.Splitters;

namespace ChunkingLibrary;

[OSInterface(
    Name = "ChunkingLibrary",
    Description = "Advanced AI chunking strategy library for RAG pipelines. Splits plain text and Markdown into vector-ready chunks.",
    IconResourceName = "ChunkingLibrary.ChunkingLibrary.png"
)]
public interface IChunkingActions
{
    [OSAction(
        Description = "Splits text into fixed-size character-based chunks with optional overlap. Best for plain text with no structure.",
        ReturnName = "Response",
        ReturnDescription = "The chunking result containing all chunks and summary statistics.")]
    ChunkingResponse SplitByCharacter(
        [OSParameter(Description = "The plain text or Markdown content to split.")] string text,
        [OSParameter(Description = "Maximum number of characters per chunk.")] int chunkSize,
        [OSParameter(Description = "Number of characters to overlap between consecutive chunks. Must be less than ChunkSize.")] int overlapSize,
        [OSParameter(Description = "When true, collapses all whitespace sequences to a single space before splitting.")] bool normalizeWhitespace,
        [OSParameter(Description = "Maximum allowed input length in characters. Throws if exceeded.")] int maxTotalChars,
        [OSParameter(Description = "Identifier for the source document. Used as a prefix in ChunkId (e.g. 'DOC-001').")] string documentId);

    [OSAction(
        Description = "Splits text using a recursive separator cascade (paragraph → line → sentence → comma → word). Produces more semantically coherent chunks than fixed-size splitting.",
        ReturnName = "Response",
        ReturnDescription = "The chunking result containing all chunks and summary statistics.")]
    ChunkingResponse SplitRecursively(
        [OSParameter(Description = "The plain text or Markdown content to split.")] string text,
        [OSParameter(Description = "Target maximum number of characters per chunk.")] int chunkSize,
        [OSParameter(Description = "Number of characters to overlap between consecutive chunks. Must be less than ChunkSize.")] int overlapSize,
        [OSParameter(Description = "Custom list of separator strings to try in order. Leave empty to use the built-in defaults: paragraph break, newline, period, comma, space.")] List<string> separators,
        [OSParameter(Description = "When true, collapses all whitespace sequences to a single space before splitting.")] bool normalizeWhitespace,
        [OSParameter(Description = "Maximum allowed input length in characters. Throws if exceeded.")] int maxTotalChars,
        [OSParameter(Description = "Identifier for the source document. Used as a prefix in ChunkId (e.g. 'DOC-001').")] string documentId);

    [OSAction(
        Description = "Splits Markdown text with awareness of headings, fenced code blocks, and tables. Each chunk carries the heading breadcrumb path for richer retrieval context.",
        ReturnName = "Response",
        ReturnDescription = "The chunking result containing all chunks and summary statistics.")]
    ChunkingResponse SplitMarkdown(
        [OSParameter(Description = "The Markdown content to split.")] string markdown,
        [OSParameter(Description = "Target maximum number of characters per chunk.")] int chunkSize,
        [OSParameter(Description = "Number of characters to overlap between consecutive chunks. Must be less than ChunkSize.")] int overlapSize,
        [OSParameter(Description = "When true, prepends the heading breadcrumb path (e.g. '# Guide > Setup') to each chunk for richer embedding context.")] bool preserveHeadingContext,
        [OSParameter(Description = "When true, keeps fenced code blocks (``` or ~~~) as a single atomic chunk rather than splitting them mid-block.")] bool preserveCodeBlocks,
        [OSParameter(Description = "When true, keeps Markdown tables as a single atomic chunk rather than splitting them mid-row.")] bool preserveTables,
        [OSParameter(Description = "Maximum allowed input length in characters. Throws if exceeded.")] int maxTotalChars,
        [OSParameter(Description = "Identifier for the source document. Used as a prefix in ChunkId (e.g. 'DOC-001').")] string documentId);

    [OSAction(
        Description = "Splits text into sentence-aware chunks, packing whole sentences up to chunkSize. Avoids mid-sentence cuts and isolates fewer fragments than fixed-size or naive sentence splitting.",
        ReturnName = "Response",
        ReturnDescription = "The chunking result containing all chunks and summary statistics.")]
    ChunkingResponse SplitBySentence(
        [OSParameter(Description = "The plain text or Markdown content to split.")] string text,
        [OSParameter(Description = "Target maximum number of characters per chunk.")] int chunkSize,
        [OSParameter(Description = "Number of characters to overlap between consecutive chunks. Must be less than ChunkSize.")] int overlapSize,
        [OSParameter(Description = "Maximum number of sentences per chunk. A chunk ends when either this count or ChunkSize is reached, whichever comes first. Use 0 for no sentence-count cap (ChunkSize alone governs chunk boundaries).")] int sentencesPerChunk,
        [OSParameter(Description = "When true, collapses all whitespace sequences to a single space before splitting.")] bool normalizeWhitespace,
        [OSParameter(Description = "Maximum allowed input length in characters. Throws if exceeded.")] int maxTotalChars,
        [OSParameter(Description = "Identifier for the source document. Used as a prefix in ChunkId (e.g. 'DOC-001').")] string documentId);
}

public class ChunkingActions : IChunkingActions
{
    public ChunkingResponse SplitByCharacter(string text, int chunkSize, int overlapSize, bool normalizeWhitespace, int maxTotalChars, string documentId)
    {
        var chunks = CharacterSplitter.SplitByCharacter(text, chunkSize, overlapSize, normalizeWhitespace, maxTotalChars, documentId);
        return BuildResponse(documentId, "Character", chunks);
    }

    public ChunkingResponse SplitRecursively(string text, int chunkSize, int overlapSize, List<string> separators, bool normalizeWhitespace, int maxTotalChars, string documentId)
    {
        var chunks = RecursiveSplitter.SplitRecursively(text, chunkSize, overlapSize, separators.Count == 0 ? null : separators, normalizeWhitespace, maxTotalChars, documentId);
        return BuildResponse(documentId, "Recursive", chunks);
    }

    public ChunkingResponse SplitMarkdown(string markdown, int chunkSize, int overlapSize, bool preserveHeadingContext, bool preserveCodeBlocks, bool preserveTables, int maxTotalChars, string documentId)
    {
        var chunks = MarkdownSplitter.SplitMarkdown(markdown, chunkSize, overlapSize, preserveHeadingContext, preserveCodeBlocks, preserveTables, maxTotalChars, documentId);
        return BuildResponse(documentId, "Markdown", chunks);
    }

    public ChunkingResponse SplitBySentence(string text, int chunkSize, int overlapSize, int sentencesPerChunk, bool normalizeWhitespace, int maxTotalChars, string documentId)
    {
        var chunks = SentenceSplitter.SplitBySentence(text, chunkSize, overlapSize, sentencesPerChunk, normalizeWhitespace, maxTotalChars, documentId);
        return BuildResponse(documentId, "Sentence", chunks);
    }

    private static ChunkingResponse BuildResponse(string documentId, string strategy, List<ChunkResult> chunks)
    {
        return new ChunkingResponse
        {
            DocumentId = documentId,
            Strategy = strategy,
            Chunks = chunks,
            Stats = new ChunkStats
            {
                ChunkCount = chunks.Count,
                AverageChunkSize = chunks.Count == 0 ? 0 : chunks.Average(c => (double)c.Text.Length),
                TotalTokenEstimate = chunks.Sum(c => c.Metadata.TokenEstimate),
                Warnings = []
            }
        };
    }
}
