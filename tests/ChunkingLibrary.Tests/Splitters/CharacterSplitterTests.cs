using ChunkingLibrary.Splitters;

namespace ChunkingLibrary.Tests.Splitters;

public class CharacterSplitterTests
{
    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = CharacterSplitter.SplitByCharacter("", 100, 0, false, 1_000_000, "doc");
        Assert.Empty(result);
    }

    [Fact]
    public void WhitespaceOnlyInput_ReturnsEmptyList()
    {
        var result = CharacterSplitter.SplitByCharacter("   \t\n  ", 100, 0, false, 1_000_000, "doc");
        Assert.Empty(result);
    }

    [Fact]
    public void TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var result = CharacterSplitter.SplitByCharacter("Hello", 100, 0, false, 1_000_000, "doc");
        Assert.Single(result);
        Assert.Equal("Hello", result[0].Text);
    }

    [Fact]
    public void TextExactlyChunkSize_ReturnsSingleChunk()
    {
        var text = new string('a', 100);
        var result = CharacterSplitter.SplitByCharacter(text, 100, 0, false, 1_000_000, "doc");
        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
    }

    [Fact]
    public void ExceedsMaxTotalChars_Throws()
    {
        var text = new string('x', 200);
        Assert.Throws<ArgumentException>(() =>
            CharacterSplitter.SplitByCharacter(text, 100, 0, false, 100, "doc"));
    }

    [Fact]
    public void OverlapSizeEqualToChunkSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterSplitter.SplitByCharacter("Hello world", 10, 10, false, 1_000_000, "doc"));
    }

    [Fact]
    public void OverlapSizeGreaterThanChunkSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CharacterSplitter.SplitByCharacter("Hello world", 10, 15, false, 1_000_000, "doc"));
    }

    [Fact]
    public void BasicSplit_ProducesExpectedChunks()
    {
        // 30 chars split into chunks of 10 with no overlap → 3 chunks
        var text = "0123456789ABCDEFGHIJKLMNOPQRST"; // 30 chars
        var result = CharacterSplitter.SplitByCharacter(text, 10, 0, false, 1_000_000, "doc");
        Assert.Equal(3, result.Count);
        Assert.Equal("0123456789", result[0].Text);
        Assert.Equal("KLMNOPQRST", result[2].Text);
    }

    [Fact]
    public void OverlapIsApplied()
    {
        // text = "AAAAAABBBBB" (11 chars), chunkSize=6, overlap=2
        // chunk[0] = "AAAAAA" (0..5), chunk[1] starts with "AA" (last 2 of chunk[0])
        var text = "AAAAAABBBBB";
        var result = CharacterSplitter.SplitByCharacter(text, 6, 2, false, 1_000_000, "doc");
        Assert.True(result.Count >= 2);
        var lastTwoOfFirst = result[0].Text[^2..];
        Assert.StartsWith(lastTwoOfFirst, result[1].Text);
    }

    [Fact]
    public void NormalizeWhitespace_CollapsesTabs()
    {
        // Tabs and newlines should collapse to single space
        var text = "hello\t\tworld\n\nfoo";
        var result = CharacterSplitter.SplitByCharacter(text, 100, 0, true, 1_000_000, "doc");
        Assert.Single(result);
        Assert.Equal("hello world foo", result[0].Text);
    }

    [Fact]
    public void ChunkId_HasCorrectFormat()
    {
        var text = new string('a', 25);
        var result = CharacterSplitter.SplitByCharacter(text, 10, 0, false, 1_000_000, "mydoc");
        Assert.Equal("mydoc-0001", result[0].ChunkId);
        Assert.Equal("mydoc-0002", result[1].ChunkId);
    }

    [Fact]
    public void SequenceNoIsOneBased()
    {
        var text = new string('a', 25);
        var result = CharacterSplitter.SplitByCharacter(text, 10, 0, false, 1_000_000, "doc");
        Assert.Equal(1, result[0].SequenceNo);
    }

    [Fact]
    public void HorizontalRules_StrippedBeforeChunking()
    {
        // Standalone --- lines must not appear in any chunk output
        var text = "First paragraph.\n\n---\n\nSecond paragraph.\n\n---\n\nThird paragraph.";
        var result = CharacterSplitter.SplitByCharacter(text, 1000, 0, false, 1_000_000, "doc");
        Assert.All(result, c => Assert.False(
            System.Text.RegularExpressions.Regex.IsMatch(c.Text, @"^\s*---\s*$", System.Text.RegularExpressions.RegexOptions.Multiline),
            $"Chunk contains a horizontal rule: {c.Text}"));
        Assert.Contains(result, c => c.Text.Contains("First paragraph."));
        Assert.Contains(result, c => c.Text.Contains("Third paragraph."));
    }

    [Fact]
    public void Metadata_StrategyIsCharacter()
    {
        var result = CharacterSplitter.SplitByCharacter("Hello world", 100, 0, false, 1_000_000, "doc");
        Assert.Equal("Character", result[0].Metadata.Strategy);
    }

    [Fact]
    public void Metadata_HashStartsWithSha256Prefix()
    {
        var result = CharacterSplitter.SplitByCharacter("Hello world", 100, 0, false, 1_000_000, "doc");
        Assert.StartsWith("sha256-", result[0].Metadata.Hash);
    }

    [Fact]
    public void LastChunk_WhenShorterThanChunkSize_DoesNotProduceExtraOverlapChunk()
    {
        // 25 chars, chunkSize=10, overlapSize=3, step=7
        // Windows: [0:10], [7:17], [14:24], [21:25] (4 chars, shorter than chunkSize)
        // Without the fix, the loop would advance to [22:25] (3 chars = overlapSize),
        // producing a 5th chunk that is a pure subset of chunk 4 with zero new content.
        var text = "0123456789ABCDEFGHIJKLMNO"; // 25 chars
        var result = CharacterSplitter.SplitByCharacter(text, 10, 3, false, 1_000_000, "doc");

        Assert.Equal(4, result.Count);
        Assert.Equal(text[21..], result[3].Text); // last 4 chars, nothing more
    }
}
