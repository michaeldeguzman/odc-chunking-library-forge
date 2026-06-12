using System.Text.RegularExpressions;
using ChunkingLibrary.Splitters;

namespace ChunkingLibrary.Tests.Splitters;

public class SentenceSplitterTests
{
    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = SentenceSplitter.SplitBySentence("", 100, 0, 0, false, 1_000_000, "doc");
        Assert.Empty(result);
    }

    [Fact]
    public void ExceedsMaxTotalChars_Throws()
    {
        var text = new string('x', 200);
        Assert.Throws<ArgumentException>(() =>
            SentenceSplitter.SplitBySentence(text, 100, 0, 0, false, 100, "doc"));
    }

    [Fact]
    public void OverlapSizeEqualToChunkSize_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SentenceSplitter.SplitBySentence("Hello world.", 10, 10, 0, false, 1_000_000, "doc"));
    }

    [Fact]
    public void NegativeSentencesPerChunk_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SentenceSplitter.SplitBySentence("Hello world.", 100, 0, -1, false, 1_000_000, "doc"));
    }

    [Fact]
    public void TextShorterThanChunkSize_ReturnsSingleChunk()
    {
        var result = SentenceSplitter.SplitBySentence("Hello world.", 100, 0, 0, false, 1_000_000, "doc");
        Assert.Single(result);
        Assert.Equal("Hello world.", result[0].Text);
    }

    [Fact]
    public void Metadata_StrategyIsSentence()
    {
        var result = SentenceSplitter.SplitBySentence("Hello world.", 100, 0, 0, false, 1_000_000, "doc");
        Assert.Equal("Sentence", result[0].Metadata.Strategy);
    }

    [Fact]
    public void ChunkId_HasCorrectFormat()
    {
        var result = SentenceSplitter.SplitBySentence("Hello world.", 100, 0, 0, false, 1_000_000, "DOC");
        Assert.Equal("DOC-0001", result[0].ChunkId);
        Assert.Equal(1, result[0].SequenceNo);
    }

    [Fact]
    public void HeadingPath_IsAlwaysEmpty()
    {
        var result = SentenceSplitter.SplitBySentence("# Heading\n\nSome text here.", 1000, 0, 0, false, 1_000_000, "doc");
        Assert.All(result, c => Assert.Equal(string.Empty, c.Metadata.HeadingPath));
    }

    [Fact]
    public void SourceType_IsPlainText()
    {
        var result = SentenceSplitter.SplitBySentence("Hello world.", 100, 0, 0, false, 1_000_000, "doc");
        Assert.Equal("PlainText", result[0].Metadata.SourceType);
    }

    [Fact]
    public void MultipleSentences_PackedIntoSingleChunk_WhenUnderChunkSize()
    {
        var text = "First sentence. Second sentence. Third sentence.";

        var result = SentenceSplitter.SplitBySentence(text, 100, 0, 0, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Equal(text, result[0].Text);
    }

    [Fact]
    public void MultipleSentences_SplitAcrossChunks_WhenExceedingChunkSize()
    {
        var s1 = "Alpha sentence number one is here.";
        var s2 = "Beta sentence number two is here.";
        var s3 = "Gamma sentence three is here.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 40, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void SentencesPerChunk_LimitsSentenceCountPerChunk()
    {
        var s1 = "Alpha sentence one.";
        var s2 = "Beta sentence two.";
        var s3 = "Gamma sentence three.";
        var s4 = "Delta sentence four.";
        var text = $"{s1} {s2} {s3} {s4}";

        // chunkSize is large enough that all 4 sentences would fit in a single chunk;
        // sentencesPerChunk=2 forces a split every 2 sentences instead.
        var result = SentenceSplitter.SplitBySentence(text, 1000, 0, 2, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal($"{s1} {s2}", result[0].Text);
        Assert.Equal($"{s3} {s4}", result[1].Text);
    }

    [Fact]
    public void ChunkSizeStillEnforced_WhenSentencesPerChunkNotReached()
    {
        var s1 = "Alpha sentence number one is here.";
        var s2 = "Beta sentence number two is here.";
        var s3 = "Gamma sentence three is here.";
        var text = $"{s1} {s2} {s3}";

        // sentencesPerChunk=10 never triggers; chunkSize=40 still forces a split per sentence.
        var result = SentenceSplitter.SplitBySentence(text, 40, 0, 10, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void NumberedListMarker_DoesNotSplitAfterDigitPeriod()
    {
        // "1." is not a sentence boundary (digit-prefixed punctuation excluded), so
        // the only valid split is between "1. First item." and "2. Second item."
        var text = "1. First item. 2. Second item.";

        var result = SentenceSplitter.SplitBySentence(text, 20, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal("1. First item.", result[0].Text);
        Assert.Equal("2. Second item.", result[1].Text);
    }

    [Fact]
    public void AbbreviationHeuristic_DoesNotSplitAfterAbbreviationPeriod()
    {
        // "e.g." is not treated as a sentence boundary because the next character
        // ("additional") is lowercase, so the only valid split is after "text."
        var text = "See e.g. additional text. Next sentence.";

        var result = SentenceSplitter.SplitBySentence(text, 30, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal("See e.g. additional text.", result[0].Text);
        Assert.Equal("Next sentence.", result[1].Text);
    }

    [Fact]
    public void ParagraphBreak_IsAlwaysAHardBoundary_EvenWithoutTerminalPunctuation()
    {
        var text = "Heading without punctuation\n\nFollowing paragraph text here.";

        var result = SentenceSplitter.SplitBySentence(text, 30, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal("Heading without punctuation", result[0].Text);
        Assert.Equal("Following paragraph text here.", result[1].Text);
    }

    [Fact]
    public void FencedCodeBlock_IsNotSplitAcrossChunks()
    {
        var codeLines = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"var x{i} = {i};"));
        var intro = "Some introductory prose that is long enough to fill its own chunk easily.";
        var text = $"{intro}\n\n```csharp\n{codeLines}\n```\n\nSome closing prose after the code block.";

        var result = SentenceSplitter.SplitBySentence(text, 100, 0, 0, false, 1_000_000, "doc");

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
        var code = "```csharp\npublic void Main() { Console.WriteLine(\"Hello\"); }\n```";
        var intro = "Some introductory text that is long enough to occupy its own chunk when split.";
        var text = $"{intro}\n\n{code}";

        var result = SentenceSplitter.SplitBySentence(text, 50, 0, 0, false, 1_000_000, "doc");

        Assert.All(result, c => Assert.DoesNotContain("___CHUNKLIBBLOCK", c.Text));
        Assert.Contains(result, c => c.Text.Contains("Console.WriteLine"));
    }

    [Fact]
    public void OversizeChunk_AcceptedWhenContainsAtomicCodeBlock()
    {
        // A code block exceeding chunkSize must remain intact in one chunk (atomicity > size limit)
        var codeLines = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"var variable{i} = value{i};"));
        var text = $"```csharp\n{codeLines}\n```";

        var result = SentenceSplitter.SplitBySentence(text, 100, 0, 0, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains("variable50", result[0].Text);
    }

    [Fact]
    public void ChunkContainingAtomicCodeBlock_DoesNotExceedChunkSizeByMoreThan10Percent()
    {
        var intro = string.Join(" ", Enumerable.Repeat("alpha", 110)) + ".";
        var codeLines = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"var v{i} = {i};"));
        var code = $"```csharp\n{codeLines}\n```";
        var text = $"{intro}\n\n{code}";

        var result = SentenceSplitter.SplitBySentence(text, 700, 0, 0, false, 1_000_000, "doc");

        double limit = 700 * 1.1;
        Assert.All(result, c => Assert.True(c.Text.Length <= limit,
            $"Chunk length {c.Text.Length} exceeds limit {limit}: {c.Text[..Math.Min(80, c.Text.Length)]}..."));

        Assert.Contains(result, c => c.Text.Contains("v40") && c.Text.Contains("```csharp"));
    }

    [Fact]
    public void ChunkContainingAtomicTable_DoesNotExceedChunkSizeByMoreThan10Percent()
    {
        var table =
            "| Strategy | Description | Use case |\n" +
            "| --- | --- | --- |\n" +
            "| STRAT-01 | Fixed window | Simple text |\n" +
            "| STRAT-02 | Recursive | Mixed text |\n" +
            "| STRAT-03 | Markdown | Structured docs |\n" +
            "| STRAT-04 | Semantic | NLP-heavy |\n" +
            "| STRAT-05 | Hybrid | Best quality |\n" +
            "| STRAT-06 | Custom | Special cases |";

        var intro = string.Join(" ", Enumerable.Repeat("alpha", 40)) + ".";
        int chunkSize = table.Length + 100;
        var text = $"{intro}\n\n{table}";

        var result = SentenceSplitter.SplitBySentence(text, chunkSize, 0, 0, false, 1_000_000, "doc");

        double limit = chunkSize * 1.1;
        Assert.All(result, c => Assert.True(c.Text.Length <= limit,
            $"Chunk length {c.Text.Length} exceeds limit {limit}: {c.Text[..Math.Min(80, c.Text.Length)]}..."));

        Assert.Contains(result, c => Regex.Matches(c.Text, @"\| STRAT-\d\d \|").Count == 6);
    }

    [Fact]
    public void ChunkContainingBothCodeBlockAndTable_SplitIntoSeparateChunks_WhenCombinedSizeExceedsLimit()
    {
        var codeLines = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"var v{i} = {i};"));
        var code = $"```csharp\n{codeLines}\n```";

        var table =
            "| Strategy | Description | Use case |\n" +
            "| --- | --- | --- |\n" +
            "| STRAT-01 | Fixed window | Simple text |\n" +
            "| STRAT-02 | Recursive | Mixed text |\n" +
            "| STRAT-03 | Markdown | Structured docs |\n" +
            "| STRAT-04 | Semantic | NLP-heavy |\n" +
            "| STRAT-05 | Hybrid | Best quality |\n" +
            "| STRAT-06 | Custom | Special cases |";

        var text = $"{code}\n\n{table}";

        var result = SentenceSplitter.SplitBySentence(text, 600, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Contains("v40", result[0].Text);
        Assert.DoesNotContain("STRAT-01", result[0].Text);
        Assert.Equal(6, Regex.Matches(result[1].Text, @"\| STRAT-\d\d \|").Count);
        Assert.DoesNotContain("```csharp", result[1].Text);
    }

    [Fact]
    public void AtomicBlock_AloneExceedsChunkSize_EmittedAsOversizedChunk_NotSplit()
    {
        var codeLines = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"var v{i} = {i};"));
        var text = $"```csharp\n{codeLines}\n```";

        var result = SentenceSplitter.SplitBySentence(text, 100, 0, 0, false, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Contains("v40", result[0].Text);
        Assert.DoesNotContain("___CHUNKLIBBLOCK", result[0].Text);
    }

    [Fact]
    public void RestoredSizeMap_CorrectlyCalculatesNetDelta_ForMultiplePlaceholders()
    {
        var codeA = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"var aItem{i} = {i};"));
        var blockA = $"```csharp\n{codeA}\n```";

        var codeB = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"var bItem{i} = {i};"));
        var blockB = $"```csharp\n{codeB}\n```";

        var text = $"{blockA}\n\n{blockB}";

        var result = SentenceSplitter.SplitBySentence(text, 600, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Contains("aItem20", result[0].Text);
        Assert.DoesNotContain("bItem", result[0].Text);
        Assert.Contains("bItem40", result[1].Text);
        Assert.DoesNotContain("aItem", result[1].Text);
    }

    [Fact]
    public void MarkdownTable_IsNotSplitAcrossChunks()
    {
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

        var result = SentenceSplitter.SplitBySentence(text, 80, 0, 0, false, 1_000_000, "doc");

        // Every chunk must contain either none of the table rows or all 6 — never a partial table.
        Assert.All(result, c =>
        {
            int rowCount = Regex.Matches(c.Text, @"\| STRAT-\d\d \|").Count;
            Assert.True(rowCount == 0 || rowCount == 6,
                $"Table split across chunks ({rowCount}/6 rows in chunk): {c.Text[..Math.Min(80, c.Text.Length)]}...");
        });
    }

    [Fact]
    public void OverlapIsApplied()
    {
        var s1 = "Alpha bravo charlie delta.";
        var s2 = "Echo foxtrot golf hotel.";
        var s3 = "India juliet kilo lima.";
        var text = $"{s1} {s2} {s3}";

        var noOverlap = SentenceSplitter.SplitBySentence(text, 55, 0, 0, false, 1_000_000, "doc");
        var withOverlap = SentenceSplitter.SplitBySentence(text, 55, 15, 0, false, 1_000_000, "doc");

        Assert.True(withOverlap[1].Text.Length > noOverlap[1].Text.Length);
    }

    [Fact]
    public void OverlapPrefix_SnapsToSentenceBoundary()
    {
        var s1 = "Alpha bravo charlie delta.";
        var s2 = "Echo foxtrot golf hotel.";
        var s3 = "India juliet kilo lima.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 55, 15, 0, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        Assert.Equal($"{s1} {s2}", result[0].Text);
        Assert.StartsWith(s2, result[1].Text);
    }

    [Fact]
    public void SentencesPerChunk1_NoOverlapPrefix_Regardless_Of_OverlapSize()
    {
        var s1 = "Alpha sentence here.";
        var s2 = "Beta sentence here.";
        var s3 = "Gamma sentence here.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 200, 1, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void SentencesPerChunk1_OverlapSize200_EachChunkStartsAtSentenceBoundary()
    {
        var s1 = "Alpha sentence here.";
        var s2 = "Beta sentence here.";
        var s3 = "Gamma sentence here.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 200, 1, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void SentencesPerChunk1_OverlapSize50_EachChunkStartsAtSentenceBoundary()
    {
        var s1 = "Alpha sentence here.";
        var s2 = "Beta sentence here.";
        var s3 = "Gamma sentence here.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 50, 1, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void SentencesPerChunk1_OverlapSize0_EachChunkStartsAtSentenceBoundary()
    {
        var s1 = "Alpha sentence here.";
        var s2 = "Beta sentence here.";
        var s3 = "Gamma sentence here.";
        var text = $"{s1} {s2} {s3}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 0, 1, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.Equal(s1, result[0].Text);
        Assert.Equal(s2, result[1].Text);
        Assert.Equal(s3, result[2].Text);
    }

    [Fact]
    public void SentencesPerChunk2_OverlapStillApplied_Normally()
    {
        var s1 = "First sentence here.";
        var s2 = "Second sentence here.";
        var s3 = "Third sentence here.";
        var s4 = "Fourth sentence here.";
        var text = $"{s1} {s2} {s3} {s4}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 10, 2, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        var expectedPiece = $"{s3} {s4}";
        Assert.True(result[1].Text.Length > expectedPiece.Length);
        Assert.EndsWith(expectedPiece, result[1].Text);
    }

    [Fact]
    public void SentencesPerChunk3_OverlapStillApplied_Normally()
    {
        var s1 = "Sentence one here.";
        var s2 = "Sentence two here.";
        var s3 = "Sentence three here.";
        var s4 = "Sentence four here.";
        var s5 = "Sentence five here.";
        var s6 = "Sentence six here.";
        var text = $"{s1} {s2} {s3} {s4} {s5} {s6}";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 10, 3, false, 1_000_000, "doc");

        Assert.Equal(2, result.Count);
        var expectedPiece = $"{s4} {s5} {s6}";
        Assert.True(result[1].Text.Length > expectedPiece.Length);
        Assert.EndsWith(expectedPiece, result[1].Text);
    }

    [Fact]
    public void OversizedSingleSentence_FallsBackToWordBoundaryCuts()
    {
        var words = Enumerable.Range(1, 30).Select(i => $"word{i}");
        var text = string.Join(" ", words) + ".";

        var result = SentenceSplitter.SplitBySentence(text, 50, 0, 0, false, 1_000_000, "doc");

        Assert.True(result.Count > 1);

        var sourceWords = new HashSet<string>(text.TrimEnd('.').Split(' '));
        foreach (var chunk in result)
        {
            foreach (var token in chunk.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.Contains(token.TrimEnd('.'), sourceWords);
            }
        }
    }

    [Fact]
    public void OversizedSingleSentence_NoSpaces_HardCharacterCut()
    {
        var text = new string('a', 150);

        var result = SentenceSplitter.SplitBySentence(text, 50, 0, 0, false, 1_000_000, "doc");

        Assert.Equal(3, result.Count);
        Assert.All(result, c => Assert.True(c.Text.Length <= 50));
        Assert.Equal(text, string.Concat(result.Select(c => c.Text)));
    }

    [Fact]
    public void HorizontalRules_StrippedBeforeChunking()
    {
        var text = "First paragraph.\n\n---\n\nSecond paragraph.\n\n***\n\nThird paragraph.";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 0, 0, false, 1_000_000, "doc");

        Assert.All(result, c => Assert.False(
            Regex.IsMatch(c.Text, @"^\s*[-*_]{3,}\s*$", RegexOptions.Multiline),
            $"Chunk contains a horizontal rule: {c.Text}"));
        Assert.Contains(result, c => c.Text.Contains("First paragraph."));
        Assert.Contains(result, c => c.Text.Contains("Third paragraph."));
    }

    [Fact]
    public void NormalizeWhitespace_CollapsesRunsAndTrims()
    {
        var text = "  Hello   world.\n\n  This  is   fine.  ";

        var result = SentenceSplitter.SplitBySentence(text, 1000, 0, 0, true, 1_000_000, "doc");

        Assert.Single(result);
        Assert.Equal("Hello world. This is fine.", result[0].Text);
    }
}
