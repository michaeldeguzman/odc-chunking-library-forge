using OutSystems.ExternalLibraries.SDK;

namespace ChunkingLibrary.Models;

[OSStructure(Description = "Summary statistics for a chunking operation.")]
public struct ChunkStats
{
    [OSStructureField(Description = "Total number of chunks produced.")]
    public int ChunkCount { get; set; }

    [OSStructureField(Description = "Average chunk size in characters across all chunks.")]
    public double AverageChunkSize { get; set; }

    [OSStructureField(Description = "Total estimated token count across all chunks (characters ÷ 4).")]
    public int TotalTokenEstimate { get; set; }

    [OSStructureField(Description = "Any warnings generated during chunking (e.g. input truncated, empty result).")]
    public List<string> Warnings { get; set; } = new();

    public ChunkStats() { }
}
