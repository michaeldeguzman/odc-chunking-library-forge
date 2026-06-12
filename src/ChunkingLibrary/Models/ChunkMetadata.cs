using OutSystems.ExternalLibraries.SDK;

namespace ChunkingLibrary.Models;

[OSStructure(Description = "Metadata attached to each chunk: hash, token estimate, char indices, heading path.")]
public struct ChunkMetadata
{
    [OSStructureField(Description = "The document identifier provided by the caller.")]
    public string DocumentId { get; set; } = string.Empty;

    [OSStructureField(Description = "The chunking strategy used: 'Character', 'Recursive', 'Markdown', or 'Sentence'.")]
    public string Strategy { get; set; } = string.Empty;

    [OSStructureField(Description = "The type of source content: 'PlainText' or 'Markdown'.")]
    public string SourceType { get; set; } = string.Empty;

    [OSStructureField(Description = "Character offset of the first character of this chunk in the (possibly normalised) input text.")]
    public int StartCharIndex { get; set; }

    [OSStructureField(Description = "Character offset of the last character of this chunk in the (possibly normalised) input text.")]
    public int EndCharIndex { get; set; }

    [OSStructureField(Description = "Estimated number of tokens in this chunk (characters ÷ 4).")]
    public int TokenEstimate { get; set; }

    [OSStructureField(Description = "SHA-256 hash of the chunk text, prefixed with 'sha256-'. Use for deduplication.")]
    public string Hash { get; set; } = string.Empty;

    [OSStructureField(Description = "Heading breadcrumb path for Markdown chunks (e.g. 'Guide > Installation > Requirements'). Empty for non-Markdown strategies.")]
    public string HeadingPath { get; set; } = string.Empty;

    [OSStructureField(Description = "Always true — indicates the chunk is clean and ready for embedding.")]
    public bool EmbeddingReady { get; set; }

    public ChunkMetadata() { }
}
