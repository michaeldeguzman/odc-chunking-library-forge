namespace ChunkingLibrary.Models;

public class TextSegment
{
    public string Text { get; set; } = string.Empty;
    public bool IsCode { get; set; }
    public string? Language { get; set; }    // Optional — from fenced code block hint
}
