# ODC Advanced AI Chunking Strategy Library — Design Brief

## Purpose of This Document

This document is the complete briefing for building the C# External Logic layer of the **Advanced AI Chunking Strategy Library** for OutSystems Developer Cloud (ODC). It captures the technical report, architectural decisions, ecosystem context, and all decisions made during planning. Use this as the single source of truth before writing any code.

---

## Project Summary

Build a reusable, ODC-native chunking strategy framework for Retrieval-Augmented Generation (RAG), semantic search, and AI document ingestion pipelines.

**This is not just a text splitter.** The goal is to give ODC developers a reusable way to prepare documents properly before sending them into vector stores, semantic search indexes, or AI agents — with a consistent, vendor-neutral output contract.

---

## Ecosystem Context (Critical — Read Before Building)

### Existing Forge Components — Do Not Duplicate These

Three existing components define what already exists in the ODC ecosystem. The chunking library must position itself *downstream* of these, not in competition with them.

#### 1. `PdfContentChunker` (OutSystems Labs)
- Extracts raw PDF binary to normalised UTF-8 text
- Splits using **Level 1 Character Splitting** logic only
- Configurable: character size, overlap size, whitespace cleanup
- Output: flat array of character-bounded chunks with page number mapping, hash fingerprinting, token estimation
- **Where it stops:** Strictly mechanical character-count splitting. No semantic awareness, no structure preservation, no strategy selection

#### 2. `OmniDoc2MD` (OutSystems Labs)
- Converts multiple document formats (PDF, DOCX, PPTX, XLSX, HTML, plain text) into structured Markdown plus metadata
- Preserves headers (H1–H6), bold/italic, lists, tables
- Output: unified structured Markdown string
- **Where it stops:** Conversion only. Does not chunk. Does not split. Does not produce vector-ready output.

#### 3. OutSystems Recursive Character Text Splitter
- Exists but is **O11-only**, not ODC-native
- Follows LangChain-style recursive splitting with separator fallback
- **Not reusable in ODC** — this is the gap this library fills

#### 4. ODC RAG Tools
- Chunks PDFs, adds chunks to Azure AI Search, provides RAG benchmarking
- Azure-specific, not a general-purpose strategy framework
- Not the same problem space

### The Pipeline This Library Fits Into

```
[Raw Documents]
      │
      ▼
[OmniDoc2MD]           ← Converts PDF/DOCX/PPTX/XLSX/HTML → Markdown
      │
      ▼
[Unified Markdown / Plain Text String]
      │
      ▼
[Advanced AI Chunking Strategy Library]   ← THIS IS WHAT WE ARE BUILDING
      │
      ▼
[Vector-Ready Chunks (vendor-neutral JSON)]
      │
      ▼
[ODC Semantic Search / Supabase pgvector / Pinecone / Milvus / Azure AI Search / Custom]
```

This diagram is the core positioning. The library is the **Advanced Processing Engine** that sits between document normalisation and vector indexing.

---

## Confirmed Architectural Decisions

These decisions are locked. Do not reopen them.

### ✅ Input Contract: Plain Text and Markdown Strings Only
- The library accepts **only `string` input** — plain text or Markdown
- It does **not** accept binary documents, PDFs, or file uploads
- Document-to-text conversion is handled upstream by OmniDoc2MD or PdfContentChunker
- This keeps the component composable and vendor-neutral

### ✅ MVP Scope: Levels 1–3 Only
- **Level 1:** Character Splitting
- **Level 2:** Recursive Character Splitting
- **Level 3:** Markdown-Aware / Document-Type Splitting
- Levels 4 (Semantic) and 5 (Agentic) are Phase 2 and Phase 3
- Do not build semantic or agentic chunking yet

### ✅ Output Contract Must Be Locked First
- The `ChunkResult` and `ChunkMetadata` structures must be defined before any splitting logic is written
- These structures cannot change between levels without breaking consuming apps

### ✅ Architecture Split: ODC Orchestrates, C# Computes
- Heavy text processing lives in **C# External Logic**
- ODC Library exposes public Service Actions that call into C#
- No persistent storage inside the library — that belongs in consuming apps
- ODC constraint: the Library stays stateless

### ✅ Forge-First, Article Second
- Build the working component before writing the article
- The article is written from something real, not speculative

---

## C# External Logic — What to Build

This is the layer implemented by this library.

### Target Framework
- **.NET 8** (supported by OutSystems External Libraries SDK)
- Follow ODC External Logic SDK conventions for method signatures, input/output types, and attribute decoration

### Methods to Implement for MVP (Levels 1–3)

#### `SplitByCharacter`
**Level 1 — Baseline strategy**

```
Input:
  string text                  // Raw normalised text
  int chunkSize                // Maximum character length per chunk
  int overlapSize              // Characters repeated between consecutive chunks
  bool normalizeWhitespace     // Optional cleanup flag
  int maxTotalChars            // Safety guardrail — reject input exceeding this

Output:
  List<ChunkResult>
```

Behaviour:
- Split text into fixed-length character segments
- Apply overlap by repeating the last `overlapSize` characters at the start of the next chunk
- If `normalizeWhitespace` is true, collapse multiple whitespace/newlines before splitting
- If input exceeds `maxTotalChars`, throw a descriptive exception
- Each chunk gets a sequence number, start/end char index, and SHA-256 hash

---

#### `SplitRecursively`
**Level 2 — Practical default strategy**

```
Input:
  string text
  int chunkSize
  int overlapSize
  List<string> separators      // Ordered list of separators to try (see below)
  bool normalizeWhitespace
  int maxTotalChars

Output:
  List<ChunkResult>
```

Behaviour:
- Attempt to split using separators in order, falling back to the next if chunks are still too large
- Default separator order (use if none provided):
  1. `\n\n` (paragraph break)
  2. `\n` (line break)
  3. `. ` (sentence end)
  4. `, ` (clause break)
  5. ` ` (word boundary)
  6. Character-count fallback (same as Level 1)
- A chunk is "too large" if its character length exceeds `chunkSize`
- Apply overlap after splitting
- Each chunk gets sequence number, start/end char index, hash

---

#### `SplitMarkdown`
**Level 3 — Markdown-aware strategy**

```
Input:
  string markdown              // Structured Markdown string (e.g. output of OmniDoc2MD)
  int chunkSize
  int overlapSize
  bool preserveHeadingContext  // If true, prepend nearest heading to each chunk
  bool preserveCodeBlocks      // If true, never split inside a fenced code block
  bool preserveTables          // If true, never split inside a Markdown table
  int maxTotalChars

Output:
  List<ChunkResult>            // Each chunk includes heading_path in metadata
```

Behaviour:
- Parse Markdown structure: detect headings (H1–H6), code fences (``` or ~~~), tables, lists, blockquotes
- Split at heading boundaries first (H1 > H2 > H3 priority)
- Never split inside a fenced code block — treat the entire block as atomic
- Never split inside a Markdown table — treat the entire table as atomic
- If a section exceeds `chunkSize`, apply recursive splitting within that section
- If `preserveHeadingContext` is true, prepend the heading path (e.g. `# Architecture > ## Chunking Strategy`) to each chunk's text
- Track and return `heading_path` in chunk metadata
- Apply overlap after structural splitting

---

#### `PreserveCodeBlocks`
**Helper — Used internally by `SplitMarkdown`**

```
Input:
  string markdown

Output:
  List<TextSegment>   // Each segment is marked as code or non-code
```

Behaviour:
- Scan for fenced code blocks (``` or ~~~) and extract them as atomic segments
- Return interleaved list: [non-code text, code block, non-code text, code block, ...]
- Non-code segments can be split further; code segments must not be split

---

#### `EstimateTokens`
**Helper — Used across all levels**

```
Input:
  string text
  string model     // Optional: "gpt-4", "claude", "generic" — affects multiplier

Output:
  int estimatedTokenCount
```

Behaviour:
- Use character-to-token ratio approximation
- For generic/unknown models: 1 token ≈ 4 characters (English text)
- For code: 1 token ≈ 3 characters
- This is an estimate, not a precise tokeniser — label it as such in output

---

#### `GenerateSha256Hash`
**Helper — Used across all levels**

```
Input:
  string text

Output:
  string   // Lowercase hex SHA-256 hash of the UTF-8 encoded text
```

Behaviour:
- Standard SHA-256 over UTF-8 bytes
- Return lowercase hex string prefixed with `sha256-`
- Used for chunk deduplication and idempotency

---

#### `BuildChunkMetadata`
**Helper — Used across all levels**

```
Input:
  string chunkText
  int sequenceNo
  int startCharIndex
  int endCharIndex
  string strategy          // "Character", "Recursive", "Markdown"
  string sourceType        // "PlainText", "Markdown"
  string headingPath       // Optional — Markdown-aware only
  string documentId        // Caller-provided document identifier

Output:
  ChunkMetadata
```

Behaviour:
- Assemble the standard metadata object
- Call `EstimateTokens` and `GenerateSha256Hash` internally
- Set `embeddingReady: true` always for MVP (semantic chunking will change this later)

---

#### `CalculateCosineDistance`
**Phase 2 — Do not build yet**
Placeholder for semantic chunking. Define the method signature now but leave unimplemented or throw `NotImplementedException`.

```
Input:
  float[] vectorA
  float[] vectorB

Output:
  float   // Cosine distance (0 = identical, 1 = orthogonal)
```

---

### Output Data Structures (C# Classes)

These must match the ODC Library structures exactly. Define them as POCOs in a shared `Models` namespace.

```csharp
public class ChunkResult
{
    public string ChunkId { get; set; }           // "{documentId}-{sequenceNo:D4}"
    public int SequenceNo { get; set; }
    public string Text { get; set; }
    public ChunkMetadata Metadata { get; set; }
}

public class ChunkMetadata
{
    public string DocumentId { get; set; }
    public string Strategy { get; set; }          // "Character" | "Recursive" | "Markdown"
    public string SourceType { get; set; }        // "PlainText" | "Markdown"
    public int StartCharIndex { get; set; }
    public int EndCharIndex { get; set; }
    public int TokenEstimate { get; set; }
    public string Hash { get; set; }              // "sha256-{hex}"
    public string HeadingPath { get; set; }       // Null for non-Markdown strategies
    public bool EmbeddingReady { get; set; }
}

public class ChunkingResponse
{
    public string DocumentId { get; set; }
    public string Strategy { get; set; }
    public List<ChunkResult> Chunks { get; set; }
    public ChunkStats Stats { get; set; }
}

public class ChunkStats
{
    public int ChunkCount { get; set; }
    public double AverageChunkSize { get; set; }  // In characters
    public int TotalTokenEstimate { get; set; }
    public List<string> Warnings { get; set; }
}

public class TextSegment
{
    public string Text { get; set; }
    public bool IsCode { get; set; }
    public string Language { get; set; }          // Optional — from fenced code block hint
}
```

---

### Standard Output Contract (JSON Shape)

The JSON below is the canonical output shape. The C# objects above must serialise to this exactly.

```json
{
  "document_id": "DOC-001",
  "strategy": "RecursiveCharacter",
  "chunks": [
    {
      "chunk_id": "DOC-001-0001",
      "sequence_no": 1,
      "text": "The chunk text goes here.",
      "metadata": {
        "document_id": "DOC-001",
        "strategy": "RecursiveCharacter",
        "source_type": "Markdown",
        "start_char_index": 0,
        "end_char_index": 842,
        "token_estimate": 214,
        "hash": "sha256-abc123...",
        "heading_path": "Architecture > Chunking Strategy",
        "embedding_ready": true
      }
    }
  ],
  "stats": {
    "chunk_count": 1,
    "average_chunk_size": 842,
    "total_token_estimate": 214,
    "warnings": []
  }
}
```

---

## ODC External Logic SDK Conventions

The C# project must follow OutSystems ODC External Logic conventions:

- Methods exposed to ODC must be decorated with `[Action]` attribute
- Input/output types must use ODC-compatible primitive types or ODC-serialisable classes
- The SDK targets **.NET 8**
- The library must be packaged as a `.zip` containing the compiled assembly and dependencies, uploaded via ODC Portal
- External Logic is **stateless** — no static state, no file I/O, no database access inside C# methods
- Exceptions thrown from C# surface as ODC errors — use descriptive exception messages

Refer to the OutSystems External Libraries SDK documentation for attribute decoration and packaging requirements.

---

## What NOT to Build in C#

| Concern | Where it belongs |
|---|---|
| Persistent chunk storage | Consuming ODC app |
| Job history / audit logs | Consuming ODC app or demo app |
| Embedding API calls | ODC AI Gateway / Service Actions (Phase 2) |
| LLM calls for agentic chunking | ODC AI Gateway / Service Actions (Phase 3) |
| Document binary parsing | OmniDoc2MD or PdfContentChunker (upstream) |
| UI / comparison screen | ODC demo app (separate from library) |

---

## MVP Build Order

Build in this sequence. Do not skip steps.

1. **Define and lock all C# model classes** — `ChunkResult`, `ChunkMetadata`, `ChunkingResponse`, `ChunkStats`, `TextSegment`
2. **Implement helpers first** — `GenerateSha256Hash`, `EstimateTokens`
3. **Implement `SplitByCharacter`** — simplest, establishes the output contract in practice
4. **Implement `SplitRecursively`** — builds on character splitting with separator fallback
5. **Implement `PreserveCodeBlocks`** — needed before Markdown splitter
6. **Implement `SplitMarkdown`** — most complex, depends on helpers and code block preservation
7. **Implement `BuildChunkMetadata`** — assembles final metadata, calls hash + token helpers
8. **Write unit tests** for each method — especially edge cases: empty input, single-word input, oversized input, input exceeding `maxTotalChars`, Markdown with no headings, code blocks at document start/end
9. **Stub `CalculateCosineDistance`** with `NotImplementedException` — Phase 2 placeholder

---

## Key Technical Risks to Design Against

| Risk | Mitigation |
|---|---|
| Markdown parser edge cases | Test with OmniDoc2MD output specifically — it's the primary upstream |
| Code blocks split incorrectly | Treat fenced blocks as atomic — scan for opening/closing fence pairs |
| Overlap causing duplicate content | Ensure overlap is applied consistently and tracked in char indices |
| Token estimation misleading | Label estimates clearly, add warning to stats if estimate confidence is low |
| Input exceeding safe limits | Enforce `maxTotalChars` guardrail — throw descriptive exception, not silent truncation |
| ODC serialisation mismatches | Keep output types simple — avoid nested generics, use `List<T>` not `IEnumerable<T>` |

---

## Positioning Statement (For README and Article)

> The OutSystems ecosystem already has useful document extraction and chunking utilities. OmniDoc2MD normalises documents into structured Markdown. PdfContentChunker handles basic character splitting for PDFs. This library is the next step: it takes structured text and produces retrieval-optimised chunks through recursive, Markdown-aware, semantic, and eventually agentic strategies — with a consistent, vendor-neutral output contract that works with any vector store.

---

## Phase Roadmap (For Context Only — Not MVP Scope)

| Phase | Features |
|---|---|
| **MVP (now)** | Character splitting, Recursive splitting, Markdown-aware splitting, standard output contract, token estimation, hashing, demo/comparison app |
| **Phase 2** | Semantic chunking — embedding provider abstraction, cosine distance, breakpoint threshold, sentence splitter |
| **Phase 3** | Agentic chunking — LLM proposition extraction, thematic grouping, JSON schema validation, cost guardrails, review screen |

---

## Questions Deferred (Not Blocking MVP)

- Should the library formally declare a dependency on OmniDoc2MD, or remain pipeline-agnostic? *(Current decision: pipeline-agnostic — accept any string input)*
- Which vector stores to demo against first? *(Current decision: vendor-neutral JSON output only for MVP — adapter demos in article)*
- Should semantic chunking be included in v1? *(Current decision: No — Phase 2)*

---

*Document compiled from planning conversation and technical report. Last updated prior to Amsterdam conference (June 2026). Build starts post-conference.*
