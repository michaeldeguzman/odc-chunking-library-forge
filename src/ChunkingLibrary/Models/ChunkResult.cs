using OutSystems.ExternalLibraries.SDK;

namespace ChunkingLibrary.Models;

[OSStructure(Description = "A single text chunk with its sequence number and metadata.")]
public struct ChunkResult
{
    [OSStructureField(Description = "Unique identifier for this chunk. Format: '{DocumentId}-{SequenceNo:D4}' (e.g. 'DOC-001-0001').")]
    public string ChunkId { get; set; } = string.Empty;

    [OSStructureField(Description = "1-based position of this chunk within the document.")]
    public int SequenceNo { get; set; }

    [OSStructureField(Description = "The chunk text content, ready for embedding.")]
    public string Text { get; set; } = string.Empty;

    [OSStructureField(Description = "Metadata for this chunk including hash, token estimate, and heading path.")]
    public ChunkMetadata Metadata { get; set; } = new();

    public ChunkResult() { }
}
