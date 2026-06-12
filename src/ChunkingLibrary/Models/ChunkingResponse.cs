using OutSystems.ExternalLibraries.SDK;

namespace ChunkingLibrary.Models;

[OSStructure(Description = "Response from a chunking operation: all chunks plus summary statistics.")]
public struct ChunkingResponse
{
    [OSStructureField(Description = "The document identifier provided by the caller.")]
    public string DocumentId { get; set; } = string.Empty;

    [OSStructureField(Description = "The chunking strategy used: 'Character', 'Recursive', or 'Markdown'.")]
    public string Strategy { get; set; } = string.Empty;

    [OSStructureField(Description = "The ordered list of chunks produced from the input text.")]
    public List<ChunkResult> Chunks { get; set; } = new();

    [OSStructureField(Description = "Summary statistics for the chunking operation.")]
    public ChunkStats Stats { get; set; } = new();

    public ChunkingResponse() { }
}
