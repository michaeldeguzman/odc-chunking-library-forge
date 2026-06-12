using System.Text.Json;
using System.Text.Json.Serialization;
using ChunkingLibrary;
using ChunkingLibrary.Models;
using Xunit;
using Xunit.Abstractions;

namespace ChunkingLibrary.Tests.Integration;

public class SmokeTests(ITestOutputHelper output)
{
    private static readonly string SpecFilePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ODC_Chunking_Library.md");

    private static readonly string TestFilePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "TestFile.txt");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ChunkingActions _actions = new();

    // ── ODC_Chunking_Library.md (Spec file) ─────────────────────────────────

    [Fact]
    public void SplitByCharacter_OnSpecFile_ProducesValidOutput()
    {
        string markdown = File.ReadAllText(SpecFilePath);
        output.WriteLine($"Input length: {markdown.Length} chars");

        var response = _actions.SplitByCharacter(
            text: markdown,
            chunkSize: 1000,
            overlapSize: 100,
            normalizeWhitespace: false,
            maxTotalChars: 200_000,
            documentId: "ODC-SPEC");

        PrintAndAssert(response, "Character", "ODC-SPEC");
    }

    [Fact]
    public void SplitRecursively_OnSpecFile_ProducesValidOutput()
    {
        string markdown = File.ReadAllText(SpecFilePath);

        var response = _actions.SplitRecursively(
            text: markdown,
            chunkSize: 1000,
            overlapSize: 100,
            separators: [],
            normalizeWhitespace: false,
            maxTotalChars: 200_000,
            documentId: "ODC-SPEC");

        PrintAndAssert(response, "Recursive", "ODC-SPEC");
    }

    [Fact]
    public void SplitMarkdown_OnSpecFile_ProducesValidOutput()
    {
        string markdown = File.ReadAllText(SpecFilePath);

        var response = _actions.SplitMarkdown(
            markdown: markdown,
            chunkSize: 1000,
            overlapSize: 100,
            preserveHeadingContext: true,
            preserveCodeBlocks: true,
            preserveTables: true,
            maxTotalChars: 200_000,
            documentId: "ODC-SPEC");

        PrintAndAssert(response, "Markdown", "ODC-SPEC");
        PrintHeadingPaths(response);
    }

    // ── TestFile.txt ─────────────────────────────────────────────────────────

    [Fact]
    public void SplitByCharacter_OnTestFile_ProducesValidOutput()
    {
        string text = File.ReadAllText(TestFilePath);
        output.WriteLine($"Input length: {text.Length} chars");

        var response = _actions.SplitByCharacter(
            text: text,
            chunkSize: 1000,
            overlapSize: 200,
            normalizeWhitespace: false,
            maxTotalChars: 200_000,
            documentId: "TEST");

        PrintAndAssert(response, "Character", "TEST");
    }

    [Fact]
    public void SplitMarkdown_OnTestFile_ProducesValidOutput()
    {
        string text = File.ReadAllText(TestFilePath);

        var response = _actions.SplitMarkdown(
            markdown: text,
            chunkSize: 1000,
            overlapSize: 200,
            preserveHeadingContext: true,
            preserveCodeBlocks: true,
            preserveTables: true,
            maxTotalChars: 200_000,
            documentId: "TEST");

        PrintAndAssert(response, "Markdown", "TEST");
        PrintHeadingPaths(response);
    }

    [Fact]
    public void SplitBySentence_OnTestFile_ProducesValidOutput()
    {
        string text = File.ReadAllText(TestFilePath);
        output.WriteLine($"Input length: {text.Length} chars");

        var response = _actions.SplitBySentence(
            text: text,
            chunkSize: 1000,
            overlapSize: 200,
            sentencesPerChunk: 0,
            normalizeWhitespace: false,
            maxTotalChars: 200_000,
            documentId: "TEST");

        PrintAndAssert(response, "Sentence", "TEST");
    }

    [Fact]
    public void SplitRecursively_OnTestFile_NoChunkStartsMidSentence()
    {
        string text = File.ReadAllText(TestFilePath);
        output.WriteLine($"Input length: {text.Length} chars");

        var response = _actions.SplitRecursively(
            text: text,
            chunkSize: 1000,
            overlapSize: 200,
            separators: [],
            normalizeWhitespace: false,
            maxTotalChars: 200_000,
            documentId: "TEST");

        output.WriteLine($"Total chunks: {response.Stats.ChunkCount}");
        output.WriteLine("");

        var sourceWords = new HashSet<string>(
            text.Split(new[] { ' ', '\n', '\r', '.', ',', ':', ';', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        for (int i = 0; i < response.Chunks.Count; i++)
        {
            string chunkText = response.Chunks[i].Text;
            string preview = chunkText.Length > 80 ? chunkText[..80] : chunkText;
            output.WriteLine($"[{i + 1:D3}] {preview}");
        }

        // Verify no chunk starts with a word fragment: every first word of every chunk
        // must be a complete token present in the source text.
        var wordSeparators = new[] { ' ', '\n', '\r', '.', ',', ':', ';', '!' };
        for (int i = 1; i < response.Chunks.Count; i++)
        {
            string chunkText = response.Chunks[i].Text;
            string firstToken = chunkText
                .Split(wordSeparators, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "";

            // Skip tokens that are code/special syntax (contain non-alpha chars beyond basic punctuation)
            bool isCodeToken = firstToken.Any(c => c is '{' or '}' or '(' or ')' or '<' or '>' or '[' or ']' or '|' or '`' or '*' or '#');
            if (isCodeToken || firstToken.Length < 2)
                continue;

            Assert.True(sourceWords.Contains(firstToken),
                $"Chunk {i + 1} starts with word fragment '{firstToken}'. Full start: \"{(chunkText.Length > 60 ? chunkText[..60] : chunkText)}\"");
        }

        output.WriteLine("\nAll chunks start at clean word boundaries.");
    }

    private void PrintAndAssert(ChunkingResponse response, string expectedStrategy, string expectedDocumentId)
    {
        // Print first 2 chunks as JSON
        var preview = new
        {
            response.DocumentId,
            response.Strategy,
            response.Stats,
            FirstTwoChunks = response.Chunks.Take(2).ToList()
        };
        output.WriteLine(JsonSerializer.Serialize(preview, JsonOptions));

        // Stats summary
        output.WriteLine($"\n--- {expectedStrategy} ({expectedDocumentId}) Summary ---");
        output.WriteLine($"Chunks:          {response.Stats.ChunkCount}");
        output.WriteLine($"Avg chunk size:  {response.Stats.AverageChunkSize:F0} chars");
        output.WriteLine($"Total tokens:    {response.Stats.TotalTokenEstimate}");

        // Assertions — validate the output contract
        Assert.Equal(expectedDocumentId, response.DocumentId);
        Assert.Equal(expectedStrategy, response.Strategy);
        Assert.NotEmpty(response.Chunks);
        Assert.Equal(response.Chunks.Count, response.Stats.ChunkCount);

        var first = response.Chunks[0];

        // ChunkId format: "{documentId}-0001"
        Assert.Equal($"{expectedDocumentId}-0001", first.ChunkId);
        Assert.Equal(1, first.SequenceNo);

        // Hash prefix
        Assert.StartsWith("sha256-", first.Metadata.Hash);

        // Strategy
        Assert.Equal(expectedStrategy, first.Metadata.Strategy);

        // SequenceNo is continuous with no gaps
        for (int i = 0; i < response.Chunks.Count; i++)
            Assert.Equal(i + 1, response.Chunks[i].SequenceNo);

        // No empty chunk text
        Assert.All(response.Chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Text)));

        output.WriteLine($"\nAll assertions passed for {expectedStrategy} ({expectedDocumentId}).");
    }

    private void PrintHeadingPaths(ChunkingResponse response)
    {
        var headingPaths = response.Chunks
            .Where(c => !string.IsNullOrEmpty(c.Metadata.HeadingPath))
            .Select(c => c.Metadata.HeadingPath)
            .Distinct()
            .ToList();

        output.WriteLine($"\nHeading paths found ({headingPaths.Count}):");
        foreach (var path in headingPaths)
            output.WriteLine($"  {path}");
    }
}
