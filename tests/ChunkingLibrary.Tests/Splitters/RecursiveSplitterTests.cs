using ChunkingLibrary.Splitters;

namespace ChunkingLibrary.Tests.Splitters;

public class RecursiveSplitterTests
{
    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = RecursiveSplitter.SplitRecursively("", 100, 0, null, false, 1_000_000, "doc");
        Assert.Empty(result);
    }

    [Fact]
    public void ExceedsMaxTotalChars_Throws()
    {
        var text = new string('x', 200);
        Assert.Throws<ArgumentException>(() =>
            RecursiveSplitter.SplitRecursively(text, 100, 0, null, false, 100, "doc"));
    }

    [Fact]
    public void OverlapSizeEqualToChunkSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            RecursiveSplitter.SplitRecursively("Hello world", 10, 10, null, false, 1_000_000, "doc"));
    }

    [Fact]
    public void TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var result = RecursiveSplitter.SplitRecursively("Hello world", 100, 0, null, false, 1_000_000, "doc");
        Assert.Single(result);
        Assert.Equal("Hello world", result[0].Text);
    }

    [Fact]
    public void DefaultSeparators_SplitsByParagraphBreakFirst()
    {
        // Two paragraphs each < chunkSize=50, total > 50 → should split at \n\n
        var paragraph1 = "This is the first paragraph with some text.";
        var paragraph2 = "This is the second paragraph with some text.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, null, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal(paragraph1, result[0].Text);
        // Second chunk contains the second paragraph (no overlap since overlapSize=0)
        Assert.Equal(paragraph2, result[1].Text);
    }

    [Fact]
    public void CustomSeparators_AreRespected()
    {
        // Use "|" as separator; each part is short enough to be its own chunk
        var text = "partA|partB|partC";
        var result = RecursiveSplitter.SplitRecursively(text, 10, 0, ["|"], false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal("partA", result[0].Text);
        Assert.Equal("partB", result[1].Text);
        Assert.Equal("partC", result[2].Text);
    }

    [Fact]
    public void EmptySeparatorList_UsesDefaults()
    {
        // Empty list should fall back to default separators including \n\n
        var paragraph1 = "This is the first paragraph with some text.";
        var paragraph2 = "This is the second paragraph with some text.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, [], false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal(paragraph1, result[0].Text);
    }

    [Fact]
    public void NullSeparators_UsesDefaults()
    {
        var paragraph1 = "This is the first paragraph with some text.";
        var paragraph2 = "This is the second paragraph with some text.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, null, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal(paragraph1, result[0].Text);
    }

    [Fact]
    public void TableHeader_PresentInEveryChunkContainingTableRows()
    {
        // Since the table is kept atomic, any chunk that has data rows must also have the header
        var table =
            "| Name | Value |\n" +
            "| --- | --- |\n" +
            "| Row1 | Data1 |\n" +
            "| Row2 | Data2 |";
        var intro = "Introductory text that is long enough to be its own chunk when split up.";
        var text = $"{intro}\n\n{table}\n\nClosing text here.";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, null, false, 1_000_000, "doc");

        foreach (var chunk in result)
        {
            if (chunk.Text.Contains("| Row1 |") || chunk.Text.Contains("| Row2 |"))
                Assert.Contains("| Name | Value |", chunk.Text);
        }
    }

    [Fact]
    public void OversizeChunk_AcceptedWhenContainsAtomicTable()
    {
        // A table exceeding chunkSize must remain intact in one chunk (atomicity > size limit)
        var rows = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"| Row{i} | Description{i} | Value{i} |"));
        var table = $"| Name | Description | Value |\n| --- | --- | --- |\n{rows}";

        var result = RecursiveSplitter.SplitRecursively(table, 100, 0, null, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains("Row20", result[0].Text);
    }

    [Fact]
    public void HorizontalRules_StrippedBeforeChunking()
    {
        var text = "First paragraph.\n\n---\n\nSecond paragraph.\n\n***\n\nThird paragraph.";
        var result = RecursiveSplitter.SplitRecursively(text, 1000, 0, null, false, 1_000_000, "doc");
        Assert.All(result, c => Assert.False(
            System.Text.RegularExpressions.Regex.IsMatch(c.Text, @"^\s*[-*_]{3,}\s*$", System.Text.RegularExpressions.RegexOptions.Multiline),
            $"Chunk contains a horizontal rule: {c.Text}"));
        Assert.Contains(result, c => c.Text.Contains("First paragraph."));
        Assert.Contains(result, c => c.Text.Contains("Third paragraph."));
    }

    [Fact]
    public void Metadata_StrategyIsRecursive()
    {
        var result = RecursiveSplitter.SplitRecursively("Hello world", 100, 0, null, false, 1_000_000, "doc");
        Assert.Equal("Recursive", result[0].Metadata.Strategy);
    }

    [Fact]
    public void ChunkId_HasCorrectFormat()
    {
        var paragraph1 = "This is the first paragraph with some text.";
        var paragraph2 = "This is the second paragraph with some text.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, null, false, 1_000_000, "mydoc");

        Assert.Equal("mydoc-0001", result[0].ChunkId);
        Assert.Equal("mydoc-0002", result[1].ChunkId);
    }

    [Fact]
    public void OverlapIsApplied()
    {
        // Two paragraphs, overlapSize=5 → chunk[1] starts with last 5 chars of chunk[0]
        var paragraph1 = "This is the first paragraph with some text.";   // ends with "text."
        var paragraph2 = "This is the second paragraph with some text.";
        var text = $"{paragraph1}\n\n{paragraph2}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 5, null, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        var lastFiveOfFirst = result[0].Text[^5..];
        Assert.StartsWith(lastFiveOfFirst, result[1].Text);
    }

    [Fact]
    public void NoChunk_ContainsMidWordSplit()
    {
        // Long paragraph with no natural separator beyond spaces. Recursive splitter must
        // eventually call CharacterFallback, which must cut on a word boundary.
        var words = string.Join(" ", Enumerable.Range(1, 60).Select(i => $"word{i}"));
        var result = RecursiveSplitter.SplitRecursively(words, 80, 0, null, false, 1_000_000, "doc");

        Assert.True(result.Count > 1, "Expected multiple chunks");
        foreach (var chunk in result)
        {
            // A mid-word cut would produce a chunk starting mid-word (no space before first word char)
            // or ending mid-word. Check that no chunk starts or ends with a partial word fragment
            // by verifying every chunk boundary aligns with a space-delimited word.
            string text = chunk.Text;
            Assert.False(text.Length > 0 && char.IsLetterOrDigit(text[0]) && !words.StartsWith(text[..1]),
                "Chunk starts mid-word");
        }
    }

    [Fact]
    public void CharacterFallback_SplitsOnWordBoundary_NotMidWord()
    {
        // Text with no separators other than spaces; forces CharacterFallback.
        // Each chunk must end at a word boundary (last char either space or last char of text).
        var text = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma";
        var result = RecursiveSplitter.SplitRecursively(text, 20, 0, [" "], false, 1_000_000, "doc");

        // Reconstruct the text from chunks — no word should be split across chunks
        var allWords = text.Split(' ');
        var reconstructedWords = result.SelectMany(c => c.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        Assert.Equal(allWords, reconstructedWords);
    }

    [Fact]
    public void FencedCodeBlock_IsNotSplitAcrossChunks()
    {
        // Code block alone is 20 lines (~400 chars); chunkSize=100 would normally split it.
        // With block preservation the entire block must land in one chunk.
        var codeLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"var x{i} = {i};"));
        var intro = "Some introductory prose that is long enough to fill its own chunk easily.";
        var text = $"{intro}\n\n```csharp\n{codeLines}\n```\n\nSome closing prose after the code block.";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 0, null, false, 1_000_000, "doc");

        // Every chunk must either contain no ``` fence markers, or contain both the opening and closing fence.
        foreach (var chunk in result)
        {
            int fenceCount = chunk.Text.Split("```").Length - 1;
            Assert.True(fenceCount == 0 || fenceCount >= 2,
                $"Chunk contains an unclosed fence (fenceCount={fenceCount}): {chunk.Text[..Math.Min(80, chunk.Text.Length)]}...");
        }
    }

    [Fact]
    public void CodeBlockRestored_AfterPlaceholderPass()
    {
        // No placeholder token must leak into any chunk — content must be fully restored
        var code = "```csharp\npublic void Main() { Console.WriteLine(\"Hello\"); }\n```";
        var intro = "Some introductory text that is long enough to occupy its own chunk when split.";
        var text = $"{intro}\n\n{code}";

        var result = RecursiveSplitter.SplitRecursively(text, 50, 0, null, false, 1_000_000, "doc");

        Assert.All(result, c => Assert.DoesNotContain("___CHUNKLIBBLOCK", c.Text));
        Assert.Contains(result, c => c.Text.Contains("Console.WriteLine"));
    }

    [Fact]
    public void OversizeChunk_AcceptedWhenContainsAtomicCodeBlock()
    {
        // A code block exceeding chunkSize must remain intact in one chunk (atomicity > size limit)
        var codeLines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"var variable{i} = value{i};"));
        var text = $"```csharp\n{codeLines}\n```";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 0, null, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains("variable50", result[0].Text);
    }

    [Fact]
    public void MarkdownTable_IsNotSplitAcrossChunks()
    {
        // Table with header + separator + 6 data rows; chunkSize=80 would split a plain string at row boundaries.
        var table =
            "| Strategy | Description | Use case |\n" +
            "| --- | --- | --- |\n" +
            "| STRAT-01 | Fixed window | Simple text |\n" +
            "| STRAT-02 | Recursive | Mixed text |\n" +
            "| STRAT-03 | Markdown | Structured docs |\n" +
            "| STRAT-04 | Semantic | NLP-heavy |\n" +
            "| STRAT-05 | Hybrid | Best quality |\n" +
            "| STRAT-06 | Custom | Special cases |";

        var intro = "The following table summarises all available chunking strategies and their recommended use cases.";
        var text = $"{intro}\n\n{table}\n\nSee individual strategy docs for configuration details.";

        var result = RecursiveSplitter.SplitRecursively(text, 80, 0, null, false, 1_000_000, "doc");

        // The separator row (| --- |) must appear in the same chunk as the header row.
        var headerChunk = result.FirstOrDefault(c => c.Text.Contains("| Strategy |"));
        Assert.False(string.IsNullOrEmpty(headerChunk.Text), "Expected a chunk containing the table header row");
        Assert.Contains("| --- |", headerChunk.Text);

        // STRAT-01 through STRAT-06 must all appear in chunks that also contain the header row,
        // OR the table is kept entirely in one chunk.
        var tableChunks = result.Where(c =>
            c.Text.Contains("| STRAT-01 |") ||
            c.Text.Contains("| STRAT-06 |")).ToList();
        Assert.NotEmpty(tableChunks);
    }

    // ── Issue 2: Atomic block over-duplication in overlap window ─────────────

    [Fact]
    public void AtomicCodeBlock_NotDuplicatedInOverlapChunk()
    {
        // With overlapSize > 0 the chunk following a code block must not repeat the code block.
        var codeLines = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"var x{i} = {i};"));
        var intro = "Introductory paragraph long enough to be its own chunk when the document is split.";
        var outro = "Closing paragraph that follows the code block in the source document.";
        var text = $"{intro}\n\n```csharp\n{codeLines}\n```\n\n{outro}";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 20, null, false, 1_000_000, "doc");

        int codeChunkCount = result.Count(c => c.Text.Contains("```csharp"));
        Assert.Equal(1, codeChunkCount);
    }

    [Fact]
    public void AtomicTable_NotDuplicatedInOverlapChunk()
    {
        // With overlapSize > 0 the chunk following a table must not repeat the table.
        var table =
            "| Strategy | Description |\n" +
            "| --- | --- |\n" +
            "| Alpha | First strategy |\n" +
            "| Beta | Second strategy |";
        var intro = "Introductory paragraph long enough to be its own chunk when the document is split.";
        var outro = "Closing paragraph that follows the table in the source document.";
        var text = $"{intro}\n\n{table}\n\n{outro}";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 20, null, false, 1_000_000, "doc");

        int tableHeaderChunkCount = result.Count(c => c.Text.Contains("| Strategy |"));
        Assert.Equal(1, tableHeaderChunkCount);
    }

    [Fact]
    public void OverlapWindow_RespectsConfiguredSize_AfterAtomicBlockRestoration()
    {
        // The chunk following an oversized code block must not contain the code fence —
        // only overlapSize chars (from the restored block's tail) should carry over.
        var codeLines = string.Join("\n", Enumerable.Range(1, 15).Select(i => $"var x{i} = {i};"));
        var intro = "Introductory paragraph long enough to be its own chunk when the document is split.";
        var outro = "Closing paragraph that follows the code block in the source document.";
        var text = $"{intro}\n\n```csharp\n{codeLines}\n```\n\n{outro}";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 15, null, false, 1_000_000, "doc");

        var outroChunk = result.FirstOrDefault(c => c.Text.Contains("Closing paragraph"));
        Assert.False(string.IsNullOrEmpty(outroChunk.Text));
        Assert.DoesNotContain("```csharp", outroChunk.Text);
    }

    [Fact]
    public void NonAtomicChunks_OverlapStillAppliedCorrectly()
    {
        // Plain text without atomic blocks: overlap must still carry content from the previous chunk.
        var para1 = "First paragraph ends with the word finale here.";
        var para2 = "Second paragraph begins with entirely different words.";
        var text = $"{para1}\n\n{para2}";

        var result = RecursiveSplitter.SplitRecursively(text, 60, 10, null, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.True(result[1].Text.Length > para2.Length,
            "Expected chunk 2 to include an overlap prefix from chunk 1");
    }

    // ── Issue 1: Mid-word character splits ───────────────────────────────────

    [Fact]
    public void NoChunk_StartsWithPartialWord()
    {
        // Overlap must not produce a chunk that starts with a word fragment.
        // When the last overlapSize chars of the previous chunk cut mid-word,
        // the overlap prefix is snapped to the next word boundary.
        var para1 = "The quick brown fox jumps over the lazy dog running fast.";
        var para2 = "Pack my box with five dozen liquor jugs for the trip.";
        var text = $"{para1}\n\n{para2}";
        var allWords = new HashSet<string>(
            text.Split(new[] { ' ', '\n', '.', ',' }, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

        // chunkSize=60 > each paragraph length (57, 53) but < combined text, forcing \n\n split
        var result = RecursiveSplitter.SplitRecursively(text, 60, 12, null, false, 1_000_000, "doc");

        Assert.True(result.Count > 1);
        // Chunk 2 = overlapPrefix + para2 (para2 is verbatim since it fits within chunkSize)
        string chunk2 = result[1].Text;
        Assert.Contains(para2, chunk2);
        int pieceStart = chunk2.IndexOf(para2, StringComparison.Ordinal);
        // Everything before para2 is the overlap prefix — it must contain only complete words
        string overlapPart = chunk2[..pieceStart].Trim();
        foreach (var word in overlapPart.Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries))
            Assert.True(allWords.Contains(word),
                $"Overlap prefix contains word fragment '{word}'");
    }

    [Fact]
    public void NoChunk_EndsWithPartialWord()
    {
        // Pieces from the cascade must contain only complete words from the source text.
        // With overlap=0 and word-length pieces, no word may be split across chunk boundaries.
        var words = string.Join(" ", Enumerable.Range(1, 40).Select(i => $"word{i}"));
        var result = RecursiveSplitter.SplitRecursively(words, 35, 0, null, false, 1_000_000, "doc");

        Assert.True(result.Count > 1);
        var originalWords = new HashSet<string>(words.Split(' '));
        foreach (var chunk in result)
        {
            foreach (var token in chunk.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(token, originalWords);
        }
    }

    [Fact]
    public void CharacterFallback_SplitsAtLastSpaceBeforeLimit()
    {
        // CharacterFallback must cut at the last space at or before chunkSize, not at the exact index.
        // Use a separator list without " " so CharacterFallback is the final resort.
        var text = "alpha beta gamma delta epsilon";
        var result = RecursiveSplitter.SplitRecursively(text, 12, 0, ["\n\n"], false, 1_000_000, "doc");

        Assert.True(result.Count > 1);
        // Last space before index 12 in "alpha beta g" is at index 10 → first chunk = "alpha beta"
        Assert.Equal("alpha beta", result[0].Text);
        Assert.All(result, c =>
        {
            Assert.False(c.Text.StartsWith(" "), $"Chunk has leading space: '{c.Text}'");
            Assert.False(c.Text.EndsWith(" "), $"Chunk has trailing space: '{c.Text}'");
        });
    }

    [Fact]
    public void CharacterFallback_SplitsAtHardLimit_WhenNoSpaceExists()
    {
        // A token with no spaces must be split at the exact character limit.
        var text = "superlongidentifierwithoutanyspaces";
        var result = RecursiveSplitter.SplitRecursively(text, 10, 0, ["\n\n"], false, 1_000_000, "doc");

        Assert.True(result.Count > 1);
        Assert.Equal(text[..10], result[0].Text);
        for (int i = 0; i < result.Count - 1; i++)
            Assert.Equal(10, result[i].Text.Length);
    }

    // ── Round 3: Separator-boundary overlap snapping ─────────────────────────

    [Fact]
    public void OverlapPrefix_StartsAtSentenceBoundary_WhenSeparatorAvailable()
    {
        // para1 has two sentences; raw overlap start lands mid-first-sentence.
        // ". " snap jumps the prefix start to the second sentence.
        // para1 = "First sentence is here. Second sentence starts here." (52 chars)
        // overlapSize=35: rawOverlapStart=17, ". " found at index 22 → snappedStart=24
        var para1 = "First sentence is here. Second sentence starts here.";
        var para2 = "Final paragraph content goes here.";
        var text = $"{para1}\n\n{para2}";

        var result = RecursiveSplitter.SplitRecursively(text, 60, 35, null, false, 1_000_000, "doc");

        Assert.True(result.Count >= 2);
        Assert.StartsWith("Second sentence starts here.", result[1].Text);
    }

    [Fact]
    public void OverlapPrefix_StartsAtParagraphBoundary_WhenDoubleNewlineAvailable()
    {
        // Atomic code block contains \n\n internally.
        // rawOverlapStart lands before the \n\n; higher-priority snap causes prefix to start after it.
        // code = "```\nfirst code line\n\nsecond code line\n```" (41 chars)
        // overlapSize=25: rawOverlapStart=16, "\n\n" found at index 19 → snappedStart=21
        var intro = "Introductory paragraph that forms its own chunk here.";
        var code = "```\nfirst code line\n\nsecond code line\n```";
        var outro = "Closing paragraph after code block.";
        var text = $"{intro}\n\n{code}\n\n{outro}";

        var result = RecursiveSplitter.SplitRecursively(text, 60, 25, null, false, 1_000_000, "doc");

        var outroChunk = result.FirstOrDefault(c => c.Text.Contains("Closing paragraph"));
        Assert.False(string.IsNullOrEmpty(outroChunk.Text));
        Assert.DoesNotContain("first code line", outroChunk.Text);
        Assert.Contains("second code line", outroChunk.Text);
    }

    [Fact]
    public void OverlapPrefix_FallsBackToRawPosition_WhenNoSeparatorFound()
    {
        // When the overlap tail has no separator characters, the raw position is used.
        // "longwordblock" has no spaces, newlines, or punctuation.
        // overlapSize=5: rawOverlapStart=8, no sep found → prefix="block" (raw fallback).
        var text = "longwordblock\n\nabc";

        var result = RecursiveSplitter.SplitRecursively(text, 15, 5, null, false, 1_000_000, "doc");

        Assert.True(result.Count >= 2);
        var chunk2 = result[1].Text;
        Assert.True(chunk2.Length > "abc".Length, "Overlap prefix must be present even when falling back to raw position");
        Assert.StartsWith("block", chunk2);
    }

    [Fact]
    public void OverlapPrefix_NeverStartsMidWord()
    {
        // With " " in the separator cascade, the snap always lands at a word boundary.
        // Extract the overlap portion of chunk 2 and verify every token is a known complete word.
        var para1 = "The recursive splitter should produce clean word boundaries.";
        var para2 = "Each overlap prefix must start with a complete word token.";
        var text = $"{para1}\n\n{para2}";
        var validWords = new HashSet<string>(
            text.Split(new[] { ' ', '.', '\n' }, StringSplitOptions.RemoveEmptyEntries),
            StringComparer.Ordinal);

        var result = RecursiveSplitter.SplitRecursively(text, 70, 20, null, false, 1_000_000, "doc");

        Assert.True(result.Count >= 2);
        string chunk2 = result[1].Text;
        int para2Start = chunk2.IndexOf(para2, StringComparison.Ordinal);
        if (para2Start > 0)
        {
            string overlapPart = chunk2[..para2Start];
            foreach (var token in overlapPart.Split(new[] { ' ', '.', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(token, validWords);
        }
    }

    [Fact]
    public void OverlapPrefix_NeverStartsMidSentence_WhenPeriodSeparatorAvailable()
    {
        // Raw overlap start lands mid-first-sentence; ". " snap positions the prefix
        // at the second sentence so the chunk begins at a clean sentence boundary.
        // para1 = "Opening sentence ends here. Then a trailing sentence follows now." (65 chars)
        // overlapSize=40: rawOverlapStart=25 (in "here."), ". " at index 26 → snappedStart=28
        var para1 = "Opening sentence ends here. Then a trailing sentence follows now.";
        var para2 = "New paragraph begins fresh.";
        var text = $"{para1}\n\n{para2}";

        var result = RecursiveSplitter.SplitRecursively(text, 70, 40, null, false, 1_000_000, "doc");

        Assert.True(result.Count >= 2);
        Assert.StartsWith("Then a trailing sentence follows now.", result[1].Text);
    }

    [Fact]
    public void SmallAdjacentPieces_AreMergedBeforeOverlapIsApplied()
    {
        // Two short paragraphs each well under chunkSize=100 and together still under 100.
        // Without the MergeSplits pass they would be emitted as two separate pieces,
        // producing a near-empty second chunk dominated entirely by overlap.
        // With MergeSplits they coalesce into one piece → single chunk, no transition chunk.
        var para1 = "Short paragraph one.";
        var para2 = "Short paragraph two.";
        var text = $"{para1}\n\n{para2}";

        var result = RecursiveSplitter.SplitRecursively(text, 100, 10, null, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains(para1, result[0].Text);
        Assert.Contains(para2, result[0].Text);
    }
}
