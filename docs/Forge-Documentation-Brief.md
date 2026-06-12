# ChunkingLibrary — Forge Component Documentation Brief

## Purpose of this document

This is a technical reference for generating end-user documentation (Forge component description, README, usage guide) for the **ChunkingLibrary** ODC External Logic component. It captures every action, parameter, output field, constraint, and design decision needed to write accurate, complete documentation. It is not itself the published documentation — use it as source material.

---

## 1. Component Overview

**Name:** ChunkingLibrary (OSInterface name: `ChunkingLibrary`)

**What it does:** Splits plain text and Markdown strings into vector-ready chunks for Retrieval-Augmented Generation (RAG) pipelines, semantic search, and AI document ingestion. It exposes four chunking strategies as OutSystems Service Actions, each producing a consistent output contract (chunk text + metadata + summary stats).

**Where it fits:**

```
[Raw Documents]
      │
      ▼
[OmniDoc2MD / PdfContentChunker]   ← document → text/Markdown conversion (upstream)
      │
      ▼
[Plain Text / Markdown String]
      │
      ▼
[ChunkingLibrary]                  ← THIS COMPONENT
      │
      ▼
[Vector-Ready Chunks]
      │
      ▼
[Vector store / semantic search index (any vendor)]
```

**Platform:** OutSystems Developer Cloud (ODC), External Logic, C# / .NET 8. Stateless — no persistent storage, file I/O, or database access. Input and output are plain strings and structs only (vendor-neutral JSON-serializable shape).

**Scope:** Accepts only `string` input (plain text or Markdown). Does not parse binary documents (PDF, DOCX, etc.) — that is handled upstream by OmniDoc2MD or PdfContentChunker.

---

## 2. Why this component exists

ODC's built-in chunking methods (available on entity attributes configured for Semantic Search) have structural gaps for documents containing code blocks, tables, Markdown headings, or hierarchical structure:

| Native ODC method | Gap |
|---|---|
| **Fixed-size** | Cuts at an absolute character index — every boundary can land mid-word, mid-sentence, or inside a code block/table, with no awareness of structure. |
| **Smart** (recursive, zero-config) | Splits on blank lines (`\n\n`) regardless of context — fenced code blocks get split across multiple chunks. |
| **Sentence-based** | Splits on terminal punctuation only. Numbered list items without punctuation (`"1."`, `"2."`) become isolated single-line chunks; a document that a recursive split produces 17 chunks from can balloon to 65+ chunks. |
| **Recursive** (with custom separators) | The empty-string fallback at the end of the separator cascade produces garbage micro-chunks from punctuation-heavy lines (backticks, `\r\n`). |
| **All four** | No heading/section context is attached to chunks — a chunk has no breadcrumb showing where it sits in the document hierarchy. |

ChunkingLibrary addresses each of these:

| Gap | How ChunkingLibrary solves it |
|---|---|
| Mid-word cuts | `SplitByCharacter` cuts at the last word boundary before the limit, falling back to a hard cut only when no space exists in the window. |
| Code block / table rupture | `SplitRecursively` and `SplitBySentence` replace fenced code blocks and Markdown tables with placeholder tokens before splitting, so they are never broken apart, then restore them afterward. |
| Garbage micro-chunks | The separator cascade ends at `" "` (space) — there is no empty-string character-level fallback. |
| Heading context absence | `SplitMarkdown` tracks the heading hierarchy (H1–H6) and can prepend a breadcrumb (e.g. `# Guide > Installation > Requirements`) to each chunk. |
| Unpredictable sentence batching | `SplitBySentence` uses a punctuation-aware regex that ignores numbered-list markers and decimals, and packs whole sentences up to a target size. |
| No overlap boundary snapping | `SplitRecursively` and `SplitBySentence` snap the overlap region to the nearest sentence or word boundary instead of cutting mid-sentence. |

---

## 3. Installation

1. Download `ChunkingLibrary.zip` from the Forge component page.
2. In ODC Portal, go to **External Logic → Upload**.
3. Upload the zip. The component registers as `ChunkingLibrary` with four Service Actions.
4. Reference the actions from any ODC module's logic (Service Studio).

---

## 4. Actions Reference

All four actions return a `ChunkingResponse` (see §5). All four share these common behaviors:

- If the input string is empty or whitespace-only, the action returns an empty chunk list (zero chunks) — this is **not** an error.
- Standalone horizontal rule lines (`---`, `___`, `***`) are stripped from the input before chunking, since they carry no semantic value for retrieval.
- Every chunk gets a `ChunkId` formatted as `"{documentId}-{sequenceNo:D4}"`, e.g. `POLICY-2024-0001`.

### 4.1 `SplitByCharacter`

**Description:** Splits text into fixed-size character-based chunks with optional overlap. Best for plain, unstructured text.

**Strategy label:** `"Character"`

| Parameter | Type | Description |
|---|---|---|
| `text` | string | The plain text or Markdown content to split. |
| `chunkSize` | int | Maximum number of characters per chunk. |
| `overlapSize` | int | Number of characters repeated at the start of the next chunk. Must be less than `chunkSize`. |
| `normalizeWhitespace` | bool | When true, collapses all whitespace sequences to a single space and trims the input before splitting. |
| `maxTotalChars` | int | Maximum allowed input length in characters. Throws if exceeded. |
| `documentId` | string | Identifier for the source document. Used as a prefix in `ChunkId` (e.g. `"DOC-001"`). |

**Behavior:**
- Produces a sliding window of fixed-length segments. Each window after the first repeats the trailing `overlapSize` characters of the previous window.
- Cuts occur at the literal character index — word boundaries are **not** respected by this strategy (that's the trade-off for predictable, uniform chunk sizes). Use `SplitRecursively` if word-boundary cuts matter.
- The final chunk is shorter than `chunkSize` when the remaining text runs out; the loop stops there rather than emitting a trailing chunk that would be pure overlap duplication.
- `HeadingPath` is always `""`.

### 4.2 `SplitRecursively`

**Description:** Splits text using a recursive separator cascade (paragraph → line → sentence → comma → word). Produces more semantically coherent chunks than fixed-size splitting.

**Strategy label:** `"Recursive"`

| Parameter | Type | Description |
|---|---|---|
| `text` | string | The plain text or Markdown content to split. |
| `chunkSize` | int | Target maximum number of characters per chunk. |
| `overlapSize` | int | Number of characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `separators` | List\<string\> | Custom ordered list of separator strings to try. Leave empty (`[]`) to use the built-in defaults. |
| `normalizeWhitespace` | bool | When true, collapses whitespace sequences to a single space and trims before splitting. |
| `maxTotalChars` | int | Maximum allowed input length in characters. Throws if exceeded. |
| `documentId` | string | Identifier for the source document. |

**Behavior:**
- Default separator cascade (used when `separators` is empty): `"\n\n"` (paragraph) → `"\n"` (line) → `". "` (sentence) → `", "` (clause) → `" "` (word). If a piece is still larger than `chunkSize` after trying all separators, it falls back to a word-boundary-aware character cut.
- **Fenced code blocks and Markdown tables (rows containing a `| --- |` separator row) are treated as atomic units** — they are never split internally, even if that makes a chunk larger than `chunkSize`.
- Adjacent small pieces produced by the separator cascade are merged back together (up to `chunkSize`) so that, for example, a standalone heading line doesn't become its own tiny chunk.
- Overlap text is snapped to the nearest sentence or word boundary rather than cutting mid-sentence.
- `HeadingPath` is always `""`. If section/heading context matters for retrieval quality, use `SplitMarkdown` instead.

### 4.3 `SplitMarkdown`

**Description:** Splits Markdown text with awareness of headings, fenced code blocks, and tables. Each chunk carries the heading breadcrumb path for richer retrieval context.

**Strategy label:** `"Markdown"`

| Parameter | Type | Description |
|---|---|---|
| `markdown` | string | The Markdown content to split. |
| `chunkSize` | int | Target maximum number of characters per chunk. |
| `overlapSize` | int | Number of characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `preserveHeadingContext` | bool | When true, prepends the heading breadcrumb path (e.g. `"# Guide > Setup"`) to each chunk's text for richer embedding context. |
| `preserveCodeBlocks` | bool | When true, keeps fenced code blocks (` ``` ` or `~~~`) as a single atomic chunk rather than splitting them mid-block. |
| `preserveTables` | bool | When true, keeps Markdown tables as a single atomic chunk rather than splitting them mid-row. |
| `maxTotalChars` | int | Maximum allowed input length in characters. Throws if exceeded. |
| `documentId` | string | Identifier for the source document. |

**Behavior:**
- Parses ATX headings (`#` through `######`, with up to 3 leading spaces per CommonMark) and tracks an ancestor heading path (H1 > H2 > H3 ...) through the document.
- Splits at heading boundaries first. A heading line with no body text underneath it (e.g. an H2 immediately followed by an H3) does not produce its own chunk — its heading is still reflected in the following chunk's `HeadingPath`.
- If a section exceeds `chunkSize`, it is split further using the same recursive cascade as `SplitRecursively`.
- When `preserveHeadingContext=true`, the heading breadcrumb is prepended as `"# H1 > H2 > H3"` followed by a blank line, then the chunk body. The raw heading line also remains in the body — the breadcrumb gives full ancestor context while the raw line preserves the original Markdown structure for downstream renderers.
- `StartCharIndex` is always `0` for this strategy — character offset tracking across structural splits is approximate by design; only the chunk **text** is guaranteed correct.

### 4.4 `SplitBySentence`

**Description:** Splits text into sentence-aware chunks, packing whole sentences up to `chunkSize`. Avoids mid-sentence cuts and isolates fewer fragments than fixed-size or naive sentence-based splitting.

**Strategy label:** `"Sentence"`

| Parameter | Type | Description |
|---|---|---|
| `text` | string | The plain text or Markdown content to split. |
| `chunkSize` | int | Target maximum number of characters per chunk. |
| `overlapSize` | int | Number of characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `sentencesPerChunk` | int | Maximum number of sentences per chunk. A chunk ends when either this count or `chunkSize` is reached, whichever comes first. Use `0` for no sentence-count cap (chunkSize alone governs boundaries). |
| `normalizeWhitespace` | bool | When true, collapses whitespace sequences to a single space and trims before splitting. |
| `maxTotalChars` | int | Maximum allowed input length in characters. Throws if exceeded. |
| `documentId` | string | Identifier for the source document. |

**Behavior:**
- A sentence boundary is detected as terminal punctuation (`.`, `!`, `?`) followed by whitespace and an uppercase letter, digit, quote, or opening bracket — numbered-list markers (`"1."`) and decimals (`"3.14"`) are not treated as sentence ends. A blank line (`\n\n`) is always a hard boundary.
- Sentences are packed greedily into chunks up to `chunkSize`. A fenced code block or table is treated as an atomic unit; if it alone exceeds `chunkSize`, it is emitted as its own oversized chunk rather than being split.
- An oversized plain-text sentence (no code/table) that exceeds `chunkSize` on its own is broken at word boundaries.
- **Overlap and `sentencesPerChunk=1` interact specially:** because character-based overlap against a single short sentence would either duplicate nearly the whole previous chunk or produce an incoherent mid-sentence fragment, overlap is **skipped entirely** when `sentencesPerChunk=1` — each chunk starts at a clean sentence boundary regardless of `overlapSize`. For `sentencesPerChunk >= 2` (or `0`), overlap applies normally and is snapped to a sentence/word boundary.
- Known heuristic limits: a sentence followed by a lowercase-leading word (e.g. an unusual capitalization like "iOS") is not detected as a new sentence boundary; a sentence ending immediately after a decimal number (e.g. "...roughly 3.14.") is also not split there.
- `HeadingPath` is always `""`.

---

## 5. Output Contract

All four actions return the same `ChunkingResponse` shape.

### `ChunkingResponse`

| Field | Type | Description |
|---|---|---|
| `DocumentId` | string | The document identifier provided by the caller. |
| `Strategy` | string | The chunking strategy used: `"Character"`, `"Recursive"`, `"Markdown"`, or `"Sentence"`. |
| `Chunks` | List\<ChunkResult\> | The ordered list of chunks produced from the input text. |
| `Stats` | ChunkStats | Summary statistics for the chunking operation. |

### `ChunkResult`

| Field | Type | Description |
|---|---|---|
| `ChunkId` | string | Unique identifier for this chunk. Format: `"{DocumentId}-{SequenceNo:D4}"` (e.g. `"DOC-001-0001"`). |
| `SequenceNo` | int | 1-based position of this chunk within the document. |
| `Text` | string | The chunk text content, ready for embedding. |
| `Metadata` | ChunkMetadata | Metadata for this chunk. |

### `ChunkMetadata`

| Field | Type | Description |
|---|---|---|
| `DocumentId` | string | The document identifier provided by the caller. |
| `Strategy` | string | The chunking strategy used: `"Character"`, `"Recursive"`, `"Markdown"`, or `"Sentence"`. |
| `SourceType` | string | The type of source content: `"PlainText"` or `"Markdown"`. |
| `StartCharIndex` | int | Character offset of the first character of this chunk in the (possibly normalized) input text. `0` for the Markdown strategy. |
| `EndCharIndex` | int | Character offset of the last character of this chunk in the (possibly normalized) input text. |
| `TokenEstimate` | int | Estimated number of tokens in this chunk (characters ÷ 4). An approximation, not a precise tokenizer count. |
| `Hash` | string | SHA-256 hash of the chunk text, prefixed with `"sha256-"`. Use for deduplication / idempotency checks. |
| `HeadingPath` | string | Heading breadcrumb path for Markdown chunks (e.g. `"Guide > Installation > Requirements"`). Empty string `""` (never null) for Character, Recursive, and Sentence strategies, and for Markdown chunks with no ancestor heading. |
| `EmbeddingReady` | bool | Always `true` — indicates the chunk is clean and ready for embedding. |

### `ChunkStats`

| Field | Type | Description |
|---|---|---|
| `ChunkCount` | int | Total number of chunks produced. |
| `AverageChunkSize` | double | Average chunk size in characters across all chunks. `0` if `ChunkCount` is `0`. |
| `TotalTokenEstimate` | int | Total estimated token count across all chunks. |
| `Warnings` | List\<string\> | Reserved for future use — currently always empty. |

---

## 6. Parameter Constraints & Errors

All four actions validate input and throw a descriptive error (`ArgumentException`) under these conditions:

| Condition | Error |
|---|---|
| `text.Length` (or `markdown.Length`) exceeds `maxTotalChars` | "Input text length (N) exceeds maxTotalChars limit (M)." |
| `overlapSize >= chunkSize` | "overlapSize (N) must be less than chunkSize (M)." |
| `sentencesPerChunk < 0` (SplitBySentence only) | "sentencesPerChunk (N) must be zero or greater. Use 0 for no sentence-count cap." |

**Recommended minimums:**
- `chunkSize` should always be `1` or greater. (`chunkSize <= 0` is not validated and produces unreliable results — degenerate output or a runtime error depending on the strategy. Always set `chunkSize` to a sensible value, typically 500–2000.)
- `overlapSize` should be `0` or greater, and strictly less than `chunkSize`. A common starting point is `overlapSize ≈ chunkSize / 5` (e.g. 1000/200).
- `maxTotalChars` must be at least as large as the input text length. A safe default is `200,000` characters, which can be raised for larger documents.
- Empty or whitespace-only input is valid and returns zero chunks (not an error).

---

## 7. Usage Examples

### Example: `SplitByCharacter`

```
Input:
  text: "The quick brown fox jumps over the lazy dog. ..."
  chunkSize: 500
  overlapSize: 100
  normalizeWhitespace: false
  maxTotalChars: 200000
  documentId: "DOC-001"

Output:
  Strategy: "Character"
  Chunks: [
    { ChunkId: "DOC-001-0001", SequenceNo: 1, Text: "...", Metadata: { ... HeadingPath: "" } },
    { ChunkId: "DOC-001-0002", SequenceNo: 2, Text: "...", Metadata: { ... } },
    ...
  ]
  Stats: { ChunkCount: N, AverageChunkSize: ~500, TotalTokenEstimate: ~N*125 }
```

### Example: `SplitMarkdown` with heading context

```
Input:
  markdown: "# Guide\n\n## Installation\n\n### Requirements\n\n.NET 8 SDK is required.\n\n..."
  chunkSize: 1000
  overlapSize: 200
  preserveHeadingContext: true
  preserveCodeBlocks: true
  preserveTables: true
  maxTotalChars: 200000
  documentId: "GUIDE-001"

Output (chunk excerpt):
  {
    ChunkId: "GUIDE-001-0003",
    Text: "# Guide > Installation > Requirements\n\n### Requirements\n\n.NET 8 SDK is required...",
    Metadata: {
      Strategy: "Markdown",
      SourceType: "Markdown",
      HeadingPath: "Guide > Installation > Requirements",
      ...
    }
  }
```

### Example: `SplitBySentence` with a sentence cap

```
Input:
  text: "First sentence here. Second sentence here. Third sentence here."
  chunkSize: 1000
  overlapSize: 0
  sentencesPerChunk: 1
  normalizeWhitespace: false
  maxTotalChars: 200000
  documentId: "FAQ-001"

Output:
  3 chunks, one sentence each, no overlap (overlap is skipped when sentencesPerChunk=1).
```

---

## 8. Choosing a Strategy

| If your content is... | Use |
|---|---|
| Plain text with no meaningful structure (logs, transcripts) | `SplitByCharacter` |
| Plain text or Markdown where word/sentence boundaries should be respected, but section context doesn't matter | `SplitRecursively` |
| Markdown with headings, code blocks, or tables where retrieval needs section context | `SplitMarkdown` |
| Content where each chunk should map to one or a small number of complete sentences (FAQs, short Q&A, definitions) | `SplitBySentence` |

---

## 9. Known Limitations

- **Markdown char indices are approximate.** `SplitMarkdown` always reports `StartCharIndex = 0`; only the chunk text itself is authoritative.
- **`SplitRecursively`/`SplitBySentence` char indices can mis-map for repeated text.** Index lookup uses simple substring search, so if identical text appears more than once, the reported indices may point at the wrong occurrence. Chunk text is always correct.
- **Heading run-on at overlap seams (`SplitRecursively`).** If an overlap region snaps to the end of one heading and the next chunk begins with another heading, both headings can appear on the same line in the output (content is intact, but the visual hierarchy at that seam is flattened). Use `SplitMarkdown` if preserving heading hierarchy across overlaps matters.
- **`documentId` is required for well-formed chunk IDs.** If left empty, `ChunkId` values will look like `"-0001"` rather than `"DOC-001-0001"`.
- **Token counts are estimates** (characters ÷ 4), not exact tokenizer output — useful for budgeting, not for hard token limits.
