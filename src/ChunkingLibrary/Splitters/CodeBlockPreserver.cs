using ChunkingLibrary.Models;

namespace ChunkingLibrary.Splitters;

public static class CodeBlockPreserver
{
    public static List<TextSegment> PreserveCodeBlocks(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return [];

        var segments = new List<TextSegment>();
        var lines = markdown.Split('\n');
        var currentNonCodeLines = new List<string>();
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];

            if (line.StartsWith("```") || line.StartsWith("~~~"))
            {
                // Flush any accumulated non-code lines
                if (currentNonCodeLines.Count > 0)
                {
                    var nonCodeText = string.Join("\n", currentNonCodeLines);
                    if (!string.IsNullOrWhiteSpace(nonCodeText))
                    {
                        segments.Add(new TextSegment
                        {
                            Text = nonCodeText,
                            IsCode = false,
                            Language = null
                        });
                    }
                    currentNonCodeLines.Clear();
                }

                var openingFenceMarker = line[..3]; // "```" or "~~~"
                var languageHint = line[3..].Trim();
                string? language = string.IsNullOrEmpty(languageHint) ? null : languageHint;

                var codeLines = new List<string> { line };
                i++;

                // Collect lines until closing fence or end of input (unclosed fence case)
                while (i < lines.Length)
                {
                    codeLines.Add(lines[i]);
                    if (lines[i].TrimStart().StartsWith(openingFenceMarker))
                        break;
                    i++;
                }

                segments.Add(new TextSegment
                {
                    Text = string.Join("\n", codeLines),
                    IsCode = true,
                    Language = language
                });
            }
            else
            {
                currentNonCodeLines.Add(line);
            }

            i++;
        }

        // Flush remaining non-code lines
        if (currentNonCodeLines.Count > 0)
        {
            var nonCodeText = string.Join("\n", currentNonCodeLines);
            if (!string.IsNullOrWhiteSpace(nonCodeText))
            {
                segments.Add(new TextSegment
                {
                    Text = nonCodeText,
                    IsCode = false,
                    Language = null
                });
            }
        }

        return segments;
    }
}
