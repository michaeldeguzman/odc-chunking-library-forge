using ChunkingLibrary.Splitters;

namespace ChunkingLibrary.Tests.Splitters;

public class CodeBlockPreserverTests
{
    [Fact]
    public void EmptyInput_ReturnsEmptyList()
    {
        var result = CodeBlockPreserver.PreserveCodeBlocks("");
        Assert.Empty(result);
    }

    [Fact]
    public void NoCodeBlocks_ReturnsSingleNonCodeSegment()
    {
        var result = CodeBlockPreserver.PreserveCodeBlocks("Just plain text here.");
        Assert.Single(result);
        Assert.False(result[0].IsCode);
        Assert.Equal("Just plain text here.", result[0].Text);
    }

    [Fact]
    public void SingleCodeBlock_ReturnsThreeSegments()
    {
        var markdown = "Before\n```\ncode here\n```\nAfter";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        Assert.Equal(3, result.Count);
        Assert.False(result[0].IsCode);
        Assert.True(result[1].IsCode);
        Assert.False(result[2].IsCode);
    }

    [Fact]
    public void CodeBlockAtStart_NoLeadingNonCodeSegment()
    {
        var markdown = "```\ncode here\n```\nAfter";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        // First segment should be code, not a blank/empty non-code segment
        Assert.True(result[0].IsCode);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void CodeBlockAtEnd_NoTrailingNonCodeSegment()
    {
        var markdown = "Before\n```\ncode here\n```";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        // Last segment should be the code block, no empty trailing segment
        Assert.True(result[^1].IsCode);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void LanguageHint_IsExtracted()
    {
        var markdown = "```csharp\nvar x = 1;\n```";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        Assert.Single(result);
        Assert.True(result[0].IsCode);
        Assert.Equal("csharp", result[0].Language);
    }

    [Fact]
    public void NoLanguageHint_LanguageIsNull()
    {
        var markdown = "```\nvar x = 1;\n```";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        Assert.Single(result);
        Assert.True(result[0].IsCode);
        Assert.Null(result[0].Language);
    }

    [Fact]
    public void UnclosedFence_TreatedAsCodeBlock()
    {
        var markdown = "Before\n```\ncode without closing fence";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        // Should produce a non-code segment and a code segment (unclosed)
        var codeSegment = result.FirstOrDefault(s => s.IsCode);
        Assert.NotNull(codeSegment);
    }

    [Fact]
    public void TildeFence_IsRecognized()
    {
        var markdown = "~~~\ncode here\n~~~";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        Assert.Single(result);
        Assert.True(result[0].IsCode);
    }

    [Fact]
    public void FenceLinesIncludedInCodeText()
    {
        var markdown = "```csharp\nvar x = 1;\n```";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        Assert.Single(result);
        Assert.Contains("```csharp", result[0].Text);
        Assert.Contains("```", result[0].Text);
    }

    [Fact]
    public void EmptyNonCodeSegments_NotIncluded()
    {
        // Two consecutive code blocks with no text between them — no empty non-code segment
        var markdown = "```\nfirst block\n```\n```\nsecond block\n```";
        var result = CodeBlockPreserver.PreserveCodeBlocks(markdown);
        // Should only have the two code segments (possibly with a newline-only non-code between,
        // but that should be filtered out as whitespace-only)
        Assert.DoesNotContain(result, s => !s.IsCode && string.IsNullOrWhiteSpace(s.Text));
        Assert.Equal(2, result.Count(s => s.IsCode));
    }
}
