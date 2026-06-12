using ChunkingLibrary.Helpers;

namespace ChunkingLibrary.Tests.Helpers;

public class HashHelperTests
{
    [Fact]
    public void EmptyString_ReturnsKnownHash()
    {
        // SHA-256("") = e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        var result = HashHelper.GenerateSha256Hash("");
        Assert.Equal("sha256-e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", result);
    }

    [Fact]
    public void KnownInput_ReturnsCorrectHash()
    {
        // SHA-256("hello") = 2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
        var result = HashHelper.GenerateSha256Hash("hello");
        Assert.Equal("sha256-2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", result);
    }

    [Fact]
    public void ResultAlwaysPrefixedWithSha256()
    {
        var result = HashHelper.GenerateSha256Hash("any input string");
        Assert.StartsWith("sha256-", result);
    }

    [Fact]
    public void OutputIsLowercase()
    {
        var result = HashHelper.GenerateSha256Hash("Test Input");
        // Strip the "sha256-" prefix and verify all hex chars are lowercase
        var hexPart = result["sha256-".Length..];
        Assert.Equal(hexPart, hexPart.ToLowerInvariant());
    }
}
