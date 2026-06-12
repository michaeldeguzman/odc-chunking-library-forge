namespace ChunkingLibrary.Helpers;

public static class TokenEstimator
{
    public static int EstimateTokens(string text, string? model = null)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var ratio = model?.Equals("code", StringComparison.OrdinalIgnoreCase) == true ? 3.0 : 4.0;
        return (int)Math.Ceiling(text.Length / ratio);
    }
}
