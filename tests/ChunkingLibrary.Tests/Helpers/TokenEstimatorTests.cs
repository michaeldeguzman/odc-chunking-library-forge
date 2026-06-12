using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Tests.Helpers;

public class TokenEstimatorTests
{
    [Fact]
    public void NullModel_UsesFourCharRatio()
    {
        // 8 chars / 4.0 ratio = 2 tokens
        var result = TokenEstimator.EstimateTokens("12345678", null);
        Assert.Equal(2, result);
    }

    [Fact]
    public void GenericModel_UsesFourCharRatio()
    {
        // "gpt-4" is not "code" so uses 4.0 ratio
        // 8 chars / 4.0 = 2 tokens
        var result = TokenEstimator.EstimateTokens("12345678", "gpt-4");
        Assert.Equal(2, result);
    }

    [Fact]
    public void CodeModel_UsesThreeCharRatio()
    {
        // "code" model uses 3.0 ratio
        // 9 chars / 3.0 = 3 tokens
        var result = TokenEstimator.EstimateTokens("123456789", "code");
        Assert.Equal(3, result);
    }

    [Fact]
    public void CodeModel_IsCaseInsensitive()
    {
        // "CODE" should behave same as "code"
        var lower = TokenEstimator.EstimateTokens("123456789", "code");
        var upper = TokenEstimator.EstimateTokens("123456789", "CODE");
        Assert.Equal(lower, upper);
        Assert.Equal(3, upper);
    }

    [Fact]
    public void EmptyText_ReturnsZero()
    {
        var result = TokenEstimator.EstimateTokens("", null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ExactDivision_ReturnsCorrectCount()
    {
        // 8 chars / 4 = exactly 2 tokens
        var result = TokenEstimator.EstimateTokens("12345678");
        Assert.Equal(2, result);
    }

    [Fact]
    public void FractionalDivision_CeilsUp()
    {
        // 9 chars / 4 = 2.25 → ceiling = 3 tokens
        var result = TokenEstimator.EstimateTokens("123456789");
        Assert.Equal(3, result);
    }
}
