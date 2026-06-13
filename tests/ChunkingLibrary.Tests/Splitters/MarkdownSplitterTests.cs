using ChunkingLibrary.Splitters;

namespace ChunkingLibrary.Tests.Splitters;

public class MarkdownSplitterTests
{
    // ── Guards ───────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = MarkdownSplitter.SplitMarkdown(
            "", 100, 0, false, false, false, 1_000_000, "doc");
        Assert.Empty(result);
    }

    [Fact]
    public void ExceedsMaxTotalChars_Throws()
    {
        var text = new string('x', 200);
        Assert.Throws<ArgumentException>(() =>
            MarkdownSplitter.SplitMarkdown(text, 100, 0, false, false, false, 100, "doc"));
    }

    [Fact]
    public void OverlapSizeEqualToChunkSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MarkdownSplitter.SplitMarkdown("# Hello\n\nContent.", 10, 10, false, false, false, 1_000_000, "doc"));
    }

    [Fact]
    public void ChunkSizeZero_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MarkdownSplitter.SplitMarkdown("# Hello\n\nContent.", 0, -1, false, false, false, 1_000_000, "doc"));
    }

    [Fact]
    public void ChunkSizeNegative_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            MarkdownSplitter.SplitMarkdown("# Hello\n\nContent.", -5, -10, false, false, false, 1_000_000, "doc"));
    }

    // ── Heading structure ────────────────────────────────────────────────────

    [Fact]
    public void MarkdownWithNoHeadings_ReturnsContentChunks()
    {
        var markdown = "Just plain text without any headings.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 100, 0, false, false, false, 1_000_000, "doc");
        Assert.NotEmpty(result);
        Assert.Contains(result, c => c.Text.Contains("plain text"));
    }

    [Fact]
    public void SingleH1_ChunkHasHeadingPath()
    {
        var markdown = "# Introduction\n\nSome content here.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0, false, false, false, 1_000_000, "doc");
        Assert.NotEmpty(result);
        // The chunk that contains the section body should have HeadingPath = "Introduction"
        var chunk = result.First(c => c.Text.Contains("Some content"));
        Assert.Equal("Introduction", chunk.Metadata.HeadingPath);
    }

    [Fact]
    public void NestedHeadings_PathIncludesAllLevels()
    {
        var markdown = "# Guide\n\n## Setup\n\nInstall the dependencies.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0, false, false, false, 1_000_000, "doc");
        Assert.NotEmpty(result);
        var chunk = result.First(c => c.Text.Contains("Install"));
        Assert.Equal("Guide > Setup", chunk.Metadata.HeadingPath);
    }

    [Fact]
    public void PreserveHeadingContext_PrependsBreadcrumb()
    {
        var markdown = "# Guide\n\n## Setup\n\nInstall the dependencies.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0,
            preserveHeadingContext: true,
            preserveCodeBlocks: false,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");
        Assert.NotEmpty(result);
        var chunk = result.First(c => c.Text.Contains("Install"));
        Assert.StartsWith("# Guide > Setup\n\n", chunk.Text);
    }

    // ── Code block preservation ──────────────────────────────────────────────

    [Fact]
    public void PreserveCodeBlocks_CodeNotSplit()
    {
        // A large code block that would normally be split should stay as one chunk
        var codeLines = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"var x{i} = {i};"));
        var markdown = $"# Example\n\n```csharp\n{codeLines}\n```";

        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 50, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: true,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");

        // There should be one chunk containing the full code block (not split into pieces)
        var codeChunk = result.FirstOrDefault(c => c.Text.Contains("```csharp"));
        Assert.False(string.IsNullOrEmpty(codeChunk.Text), "Expected a chunk containing the code block");
        Assert.Contains("var x30 = 30;", codeChunk.Text);
    }

    [Fact]
    public void PreserveCodeBlocks_False_CodeBlockStillIntact()
    {
        // When preserveCodeBlocks=false the code block is not extracted as an atomic segment at
        // the MarkdownSplitter level, but RecursiveSplitter still preserves its internal boundaries.
        // The entire block must appear in a single chunk regardless of chunkSize.
        var codeLines = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"var x{i} = {i};"));
        var markdown = $"```csharp\n{codeLines}\n```";

        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 40, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: false,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains("```csharp", result[0].Text);
        Assert.Contains("var x30 = 30;", result[0].Text);
    }

    // ── Table preservation ───────────────────────────────────────────────────

    [Fact]
    public void PreserveTables_TableIsAtomic()
    {
        // Table = 43 chars (fits in chunkSize=50); surrounding text forces a split so the
        // table lands in its own chunk. Demonstrates that table lines are kept together.
        var table = "| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |";
        var intro = "Some introductory text that is long enough to be its own chunk when split.";
        var markdown = $"{intro}\n\n{table}";

        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 50, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: false,
            preserveTables: true,
            maxTotalChars: 1_000_000, "doc");

        // All four table lines should appear together in a single chunk
        var tableChunk = result.FirstOrDefault(c => c.Text.Contains("| A | B |") && c.Text.Contains("| 3 | 4 |"));
        Assert.False(string.IsNullOrEmpty(tableChunk.Text), "Expected a chunk containing the full table");
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void CodeBlockAtDocumentStart_HandledCorrectly()
    {
        var markdown = "```python\nprint('hello')\n```\n\nSome text after.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: true,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");

        Assert.NotEmpty(result);
        // First chunk should be the code block
        Assert.Contains(result, c => c.Text.Contains("print('hello')"));
    }

    [Fact]
    public void CodeBlockAtDocumentEnd_HandledCorrectly()
    {
        var markdown = "Some text before.\n\n```python\nprint('hello')\n```";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: true,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");

        Assert.NotEmpty(result);
        Assert.Contains(result, c => c.Text.Contains("print('hello')"));
        Assert.Contains(result, c => c.Text.Contains("Some text before."));
    }

    [Fact]
    public void UnclosedCodeBlock_TreatedAsCodeBlock()
    {
        var markdown = "# Title\n\nSome intro.\n\n```python\nprint('no closing fence')";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0,
            preserveHeadingContext: false,
            preserveCodeBlocks: true,
            preserveTables: false,
            maxTotalChars: 1_000_000, "doc");

        Assert.NotEmpty(result);
        // The unclosed code block content should appear somewhere in the results
        Assert.Contains(result, c => c.Text.Contains("print('no closing fence')"));
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    [Fact]
    public void HorizontalRules_StrippedBeforeChunking()
    {
        var markdown = "# Section 1\n\nContent A.\n\n---\n\n# Section 2\n\nContent B.\n\n___\n\n# Section 3\n\nContent C.";
        var result = MarkdownSplitter.SplitMarkdown(markdown, 1000, 0, false, false, false, 1_000_000, "doc");
        Assert.All(result, c => Assert.False(
            System.Text.RegularExpressions.Regex.IsMatch(c.Text, @"^\s*[-_*]{3,}\s*$", System.Text.RegularExpressions.RegexOptions.Multiline),
            $"Chunk contains a horizontal rule: {c.Text}"));
        Assert.Contains(result, c => c.Text.Contains("Content A."));
        Assert.Contains(result, c => c.Text.Contains("Content C."));
    }

    [Fact]
    public void Metadata_StrategyIsMarkdown_SourceTypeIsMarkdown()
    {
        var result = MarkdownSplitter.SplitMarkdown(
            "# Title\n\nContent.", 1000, 0, false, false, false, 1_000_000, "doc");
        Assert.NotEmpty(result);
        Assert.All(result, c =>
        {
            Assert.Equal("Markdown", c.Metadata.Strategy);
            Assert.Equal("Markdown", c.Metadata.SourceType);
        });
    }

    [Fact]
    public void EmptyHeadingSection_IsNotEmittedAsStandaloneChunk()
    {
        // H2 with no body immediately followed by H3 must not produce a heading-only chunk
        var markdown = "# Guide\n\n## Setup\n\n### Installation\n\nInstall the package.";
        var result = MarkdownSplitter.SplitMarkdown(markdown, 1000, 0, false, false, false, 1_000_000, "doc");

        Assert.All(result, c =>
        {
            var nonEmptyLines = c.Text.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            bool isHeadingOnly = nonEmptyLines.Count == 1 && nonEmptyLines[0].TrimStart().StartsWith("#");
            Assert.False(isHeadingOnly, $"Heading-only chunk found: '{c.Text}'");
        });
    }

    [Fact]
    public void EmptyHeadingSection_HeadingPathPreservedInFollowingChunk()
    {
        // The content chunk's heading path must include the skipped empty H2 ancestor
        var markdown = "# Guide\n\n## Setup\n\n### Installation\n\nInstall the package.";
        var result = MarkdownSplitter.SplitMarkdown(markdown, 1000, 0, false, false, false, 1_000_000, "doc");

        var contentChunk = result.FirstOrDefault(c => c.Text.Contains("Install the package."));
        Assert.False(string.IsNullOrEmpty(contentChunk.Text));
        Assert.Contains("Setup", contentChunk.Metadata.HeadingPath);
        Assert.Contains("Installation", contentChunk.Metadata.HeadingPath);
    }

    [Fact]
    public void SequenceNo_IsContinuousAcrossAllChunks()
    {
        var markdown = "# Section 1\n\nContent 1.\n\n# Section 2\n\nContent 2.\n\n# Section 3\n\nContent 3.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0, false, false, false, 1_000_000, "doc");

        Assert.NotEmpty(result);
        // SequenceNos should be 1, 2, 3, ... with no gaps
        for (int i = 0; i < result.Count; i++)
        {
            Assert.Equal(i + 1, result[i].SequenceNo);
        }
    }

    [Fact]
    public void ChunkId_HasCorrectFormat()
    {
        var markdown = "# Section 1\n\nContent 1.\n\n# Section 2\n\nContent 2.";
        var result = MarkdownSplitter.SplitMarkdown(
            markdown, 1000, 0, false, false, false, 1_000_000, "testdoc");

        Assert.NotEmpty(result);
        Assert.Equal("testdoc-0001", result[0].ChunkId);
        if (result.Count > 1)
            Assert.Equal("testdoc-0002", result[1].ChunkId);
    }
}
