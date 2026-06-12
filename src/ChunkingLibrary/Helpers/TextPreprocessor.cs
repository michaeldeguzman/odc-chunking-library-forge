using System.Text.RegularExpressions;

namespace ChunkingLibrary.Helpers;

public static class TextPreprocessor
{
    private static readonly Regex _horizontalRuleRegex =
        new(@"^\s*[-_*]{3,}\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    public static string StripHorizontalRules(string text) =>
        _horizontalRuleRegex.Replace(text, string.Empty);
}
