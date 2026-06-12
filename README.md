# ChunkingLibrary

**Advanced AI chunking strategies for RAG pipelines on OutSystems Developer Cloud (ODC).**

ChunkingLibrary is an ODC External Logic component that splits plain text and Markdown into vector-ready chunks for Retrieval-Augmented Generation (RAG), semantic search, and AI document ingestion. It provides four chunking strategies — from simple fixed-size splitting to heading-aware Markdown splitting — each returning a consistent, vendor-neutral output contract with metadata, hashes, and token estimates.

![ChunkingLibrary](ChunkingLibrary.png)

---

## Why use this component?

ODC's built-in chunking methods (Fixed-size, Smart, Sentence-based, Recursive) work well for plain prose, but struggle with structured content:

- **Mid-word and mid-sentence cuts** with no awareness of word or sentence boundaries
- **Code blocks and tables split across chunks**, breaking syntax and structure
- **No heading or section context** — a chunk has no breadcrumb showing where it sits in the document
- **Sentence-based splitting can explode chunk counts** — numbered list items without terminal punctuation become isolated single-line chunks

ChunkingLibrary is built specifically for documents that combine prose with code blocks, tables, and Markdown heading hierarchies — the typical output of document-to-Markdown converters such as OmniDoc2MD.

```
[Raw Documents]
      │
      ▼
[OmniDoc2MD / PdfContentChunker]   ← document → text/Markdown conversion
      │
      ▼
[Plain Text / Markdown String]
      │
      ▼
[ChunkingLibrary]                  ← this component
      │
      ▼
[Vector-Ready Chunks] → [Your vector store / semantic search index]
```

---

## Installation

### Option 1: OutSystems Forge (recommended)

Install **ChunkingLibrary** from the OutSystems Forge into your ODC environment, then add a reference to it from Service Studio.

### Option 2: Download from GitHub Releases

1. Download `ChunkingLibrary.zip` from this repository's [Releases](../../releases) page.
2. In **ODC Portal**, go to **External Logic → Upload** and upload the zip. The component registers as **ChunkingLibrary** with four Service Actions.
3. In Service Studio, add a reference to the ChunkingLibrary module and call any of the four actions from your logic.

### Option 3: Build from source

1. Clone this repository.
2. Build and package the External Logic library:

   ```bash
   dotnet build
   dotnet publish src/ChunkingLibrary -c Release -o publish/
   cd publish && zip -r ../ChunkingLibrary.zip .
   ```

3. In **ODC Portal**, go to **External Logic → Upload** and upload `ChunkingLibrary.zip`.
4. In Service Studio, add a reference to the ChunkingLibrary module and call any of the four actions from your logic.

---

## Actions at a Glance

| Action | Best for | Strategy |
|---|---|---|
| [`SplitByCharacter`](#splitbycharacter) | Plain, unstructured text (logs, transcripts) | Fixed-size sliding window, word-boundary aware |
| [`SplitRecursively`](#splitrecursively) | General-purpose text/Markdown | Recursive separator cascade (paragraph → line → sentence → word) |
| [`SplitMarkdown`](#splitmarkdown) | Markdown with headings, code blocks, tables | Heading-aware, atomic code blocks/tables, breadcrumb metadata |
| [`SplitBySentence`](#splitbysentence) | FAQs, definitions, short Q&A | Sentence-aware packing with optional sentence-count cap |

All four actions:
- Return a `ChunkingResponse` containing the chunk list and summary statistics (see [Output Structure](#output-structure)).
- Treat empty or whitespace-only input as valid — they return zero chunks, not an error.
- Strip standalone horizontal rule lines (`---`, `___`, `***`) before chunking.
- Stamp every chunk with a `ChunkId` formatted as `"{documentId}-{sequenceNo:D4}"` (e.g. `DOC-001-0001`).

---

## Action Reference

### `SplitByCharacter`

Splits text into fixed-size character-based chunks with optional overlap. The simplest, most predictable strategy — ideal for plain text with no structure.

| Parameter | Type | Description |
|---|---|---|
| `text` | Text | The plain text or Markdown content to split. |
| `chunkSize` | Integer | Maximum number of characters per chunk. |
| `overlapSize` | Integer | Characters repeated at the start of the next chunk. Must be less than `chunkSize`. |
| `normalizeWhitespace` | Boolean | When `true`, collapses all whitespace runs to a single space and trims the input before splitting. |
| `maxTotalChars` | Integer | Maximum allowed input length. Throws an error if exceeded — protects against runaway inputs. |
| `documentId` | Text | Identifier stamped onto every chunk (used in `ChunkId` and metadata). |

Cuts land on word boundaries (`LastIndexOf(' ')`) whenever possible — a hard character cut only occurs when no space exists within the chunk window (e.g. a long URL).

---

### `SplitRecursively`

Splits text using a recursive separator cascade, producing more semantically coherent chunks than fixed-size splitting. The practical default for general-purpose text and Markdown.

| Parameter | Type | Description |
|---|---|---|
| `text` | Text | The plain text or Markdown content to split. |
| `chunkSize` | Integer | Target maximum number of characters per chunk. |
| `overlapSize` | Integer | Characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `separators` | List of Text | Custom ordered separators to try. Leave empty to use the built-in defaults. |
| `normalizeWhitespace` | Boolean | When `true`, collapses whitespace runs to a single space and trims before splitting. |
| `maxTotalChars` | Integer | Maximum allowed input length. Throws an error if exceeded. |
| `documentId` | Text | Identifier stamped onto every chunk. |

**Default separator cascade:** paragraph break (`\n\n`) → line break (`\n`) → sentence end (`. `) → clause break (`, `) → word boundary (` `). If a piece is still too large after all separators, it falls back to a word-boundary character cut. There is no empty-string fallback, so punctuation-heavy text never produces garbage micro-chunks.

**Structure-aware:** Fenced code blocks and Markdown tables are detected and treated as atomic — they are never split internally, even if that makes a chunk larger than `chunkSize`. Overlap text is snapped to the nearest sentence or word boundary instead of landing mid-sentence.

> `HeadingPath` is always empty for this strategy. If section context matters for retrieval quality, use `SplitMarkdown`.

---

### `SplitMarkdown`

Splits Markdown with awareness of headings, fenced code blocks, and tables. Each chunk carries a heading breadcrumb in its metadata for richer retrieval context — ideal for technical documentation, guides, and knowledge base articles.

| Parameter | Type | Description |
|---|---|---|
| `markdown` | Text | The Markdown content to split. |
| `chunkSize` | Integer | Target maximum number of characters per chunk. |
| `overlapSize` | Integer | Characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `preserveHeadingContext` | Boolean | When `true`, prepends the heading breadcrumb (e.g. `# Guide > Setup`) to each chunk's text. |
| `preserveCodeBlocks` | Boolean | When `true`, keeps fenced code blocks (` ``` ` or `~~~`) atomic. |
| `preserveTables` | Boolean | When `true`, keeps Markdown tables atomic. |
| `maxTotalChars` | Integer | Maximum allowed input length. Throws an error if exceeded. |
| `documentId` | Text | Identifier stamped onto every chunk. |

**How it works:**
1. Parses ATX headings (`#` through `######`) and tracks an ancestor heading path (e.g. `Guide > Installation > Requirements`) through the document.
2. Splits at heading boundaries first. A heading with no body text underneath doesn't become its own chunk — its title still appears in the following chunk's heading path.
3. If a section is still larger than `chunkSize`, it's split further using the same cascade as `SplitRecursively`.
4. With `preserveHeadingContext = true`, each chunk is prefixed with `# H1 > H2 > H3` followed by the section body — giving the embedding model full ancestor context while preserving the original heading line for downstream rendering.

> Character offsets (`StartCharIndex`/`EndCharIndex`) are approximate for this strategy (`StartCharIndex` is always `0`) — only the chunk **text** is guaranteed accurate.

---

### `SplitBySentence`

Splits text into sentence-aware chunks, packing whole sentences up to `chunkSize`. Avoids mid-sentence cuts and produces far fewer fragments than naive sentence-based splitting — well suited to FAQs, definitions, and short Q&A content.

| Parameter | Type | Description |
|---|---|---|
| `text` | Text | The plain text or Markdown content to split. |
| `chunkSize` | Integer | Target maximum number of characters per chunk. |
| `overlapSize` | Integer | Characters to overlap between consecutive chunks. Must be less than `chunkSize`. |
| `sentencesPerChunk` | Integer | Maximum sentences per chunk — a chunk ends at this count or `chunkSize`, whichever comes first. Use `0` for no sentence cap. |
| `normalizeWhitespace` | Boolean | When `true`, collapses whitespace runs to a single space and trims before splitting. |
| `maxTotalChars` | Integer | Maximum allowed input length. Throws an error if exceeded. |
| `documentId` | Text | Identifier stamped onto every chunk. |

**Sentence detection** is punctuation-aware: a sentence ends at `.`, `!`, or `?` followed by whitespace and a capital letter, digit, quote, or bracket — numbered-list markers (`"1."`) and decimals (`"3.14"`) are correctly **not** treated as sentence endings. A blank line is always a hard boundary regardless of punctuation.

Fenced code blocks and tables are treated as atomic units, just as in `SplitRecursively`.

> **Note on `sentencesPerChunk = 1`:** overlap is automatically skipped in this mode (regardless of `overlapSize`), since character-based overlap against a single short sentence would either duplicate the previous chunk or produce an incoherent fragment. Each chunk starts at a clean sentence boundary.

---

## Output Structure

All four actions return a `ChunkingResponse`:

```
ChunkingResponse
├── DocumentId        Text             — echoes the documentId you provided
├── Strategy          Text             — "Character" | "Recursive" | "Markdown" | "Sentence"
├── Chunks            List<ChunkResult>
│     ├── ChunkId       Text           — "{documentId}-{sequenceNo:D4}", e.g. "DOC-001-0001"
│     ├── SequenceNo    Integer        — 1-based position in the document
│     ├── Text          Text           — the chunk content, ready for embedding
│     └── Metadata      ChunkMetadata
│           ├── DocumentId       Text
│           ├── Strategy         Text     — same as above
│           ├── SourceType       Text     — "PlainText" | "Markdown"
│           ├── StartCharIndex   Integer  — 0 for the Markdown strategy
│           ├── EndCharIndex     Integer
│           ├── TokenEstimate    Integer  — characters ÷ 4 (approximation)
│           ├── Hash             Text     — "sha256-{hex}", for dedup/idempotency
│           ├── HeadingPath      Text     — e.g. "Guide > Installation > Requirements", "" if none
│           └── EmbeddingReady   Boolean  — always true
└── Stats             ChunkStats
      ├── ChunkCount          Integer
      ├── AverageChunkSize    Decimal    — average chunk length in characters
      ├── TotalTokenEstimate  Integer
      └── Warnings            List<Text> — reserved for future use
```

---

## Quick Start Example

```
Action: SplitMarkdown
Input:
  markdown:               "# Guide\n\n## Installation\n\n### Requirements\n\n.NET 8 SDK is required.\n\n..."
  chunkSize:               1000
  overlapSize:             200
  preserveHeadingContext:  true
  preserveCodeBlocks:      true
  preserveTables:          true
  maxTotalChars:           200000
  documentId:              "GUIDE-001"

Output (excerpt):
  Chunks[2].ChunkId:               "GUIDE-001-0003"
  Chunks[2].Text:                  "# Guide > Installation > Requirements\n\n### Requirements\n\n.NET 8 SDK is required..."
  Chunks[2].Metadata.HeadingPath:  "Guide > Installation > Requirements"
  Chunks[2].Metadata.Hash:         "sha256-..."
```

---

## Choosing a Strategy

| Your content is... | Use |
|---|---|
| Plain text with no meaningful structure (logs, transcripts) | `SplitByCharacter` |
| Plain text or Markdown where word/sentence boundaries matter but section context doesn't | `SplitRecursively` |
| Markdown with headings, code blocks, or tables, and retrieval needs section context | `SplitMarkdown` |
| Content where each chunk should be one or a few complete sentences (FAQs, definitions) | `SplitBySentence` |

---

## Parameter Guidelines

- **`chunkSize`**: always use `1` or greater — typical values are 500–2000 characters depending on your embedding model's context window.
- **`overlapSize`**: must be `0` or greater and strictly less than `chunkSize`. A common starting point is roughly `chunkSize / 5` (e.g. 1000 / 200).
- **`maxTotalChars`**: must be at least the length of your input text. A safe default is `200,000`; raise it for larger documents.
- **`documentId`**: always provide a non-empty value — it's used to build `ChunkId` (e.g. `DOC-001-0001`). An empty value produces malformed IDs like `-0001`.

---

## Known Limitations

- `SplitMarkdown` always reports `StartCharIndex = 0` — character offsets across structural splits are approximate; the chunk **text** is always correct.
- `SplitRecursively` and `SplitBySentence` character offsets can occasionally point at the wrong occurrence if identical text repeats in the document — chunk text itself is unaffected.
- At an overlap seam in `SplitRecursively`, two adjacent headings can occasionally end up on the same line in the output text. Use `SplitMarkdown` when preserving heading hierarchy across overlaps is important.
- `TokenEstimate` is an approximation (characters ÷ 4), not an exact tokenizer count — useful for budgeting, not for hard token limits.

---

## Support

Found an issue or have a feature request? Please open an issue in this repository.
