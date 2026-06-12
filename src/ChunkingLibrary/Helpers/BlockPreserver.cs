using System.Text.RegularExpressions;

namespace ChunkingLibrary.Helpers;

public static class BlockPreserver
{
    private static readonly Regex _tableSeparatorRowRegex = new(@"^\s*\|[\s\-:|]+\|", RegexOptions.Compiled);

    /// <summary>
    /// Replaces fenced code blocks and Markdown tables in <paramref name="text"/> with
    /// placeholder tokens, storing the originals in <paramref name="preserved"/>.
    /// Placeholders contain no separator characters so they survive splitting intact.
    /// </summary>
    public static string SubstituteBlocks(string text, Dictionary<string, string> preserved)
    {
        var outputLines = new List<string>();
        var lines = text.Split('\n');
        int index = 0;
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();

            if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
            {
                string fence = trimmed[..3];
                var blockLines = new List<string> { line };
                i++;
                while (i < lines.Length)
                {
                    blockLines.Add(lines[i]);
                    if (lines[i].TrimStart().StartsWith(fence))
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                string key = $"___CHUNKLIBBLOCK{index++}___";
                preserved[key] = string.Join("\n", blockLines);
                outputLines.Add(key);
            }
            else if (trimmed.StartsWith("|"))
            {
                var tableLines = new List<string> { line };
                i++;
                while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                {
                    tableLines.Add(lines[i]);
                    i++;
                }
                // Only treat as a table if there is a separator row (e.g. | --- | --- |)
                if (tableLines.Count >= 2 && tableLines.Any(l => _tableSeparatorRowRegex.IsMatch(l)))
                {
                    string key = $"___CHUNKLIBBLOCK{index++}___";
                    preserved[key] = string.Join("\n", tableLines);
                    outputLines.Add(key);
                }
                else
                {
                    outputLines.AddRange(tableLines);
                }
            }
            else
            {
                outputLines.Add(line);
                i++;
            }
        }

        return string.Join("\n", outputLines);
    }

    public static string RestoreBlocks(string text, Dictionary<string, string> preserved)
    {
        foreach (var (key, original) in preserved)
            text = text.Replace(key, original);
        return text;
    }
}
