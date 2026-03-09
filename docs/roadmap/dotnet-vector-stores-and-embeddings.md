# Agent Framework .NET — Vector Stores and Embeddings

## Overview

The .NET side of Agent Framework does **not** re-implement the vector store or embedding abstractions from scratch.
Instead, it delegates entirely to **`Microsoft.Extensions.AI` (MEAI)** and **`Microsoft.Extensions.VectorData`**,
which are the canonical .NET ecosystem abstractions — the same abstractions that Semantic Kernel's .NET implementation
also builds on.

AF .NET's job is to build **higher-level agent memory and RAG components** on top of those existing foundations,
not to redefine the primitives.

| Capability               | Python AF (custom)                              | .NET AF (delegates to ecosystem)                                                |
|--------------------------|-------------------------------------------------|---------------------------------------------------------------------------------|
| Embedding abstraction    | `BaseEmbeddingClient` / `SupportsGetEmbeddings` | `IEmbeddingGenerator<TInput, TEmbedding>` (MEAI)                                |
| Embedding types          | `Embedding[T]`, `GeneratedEmbeddings[T]`        | `GeneratedEmbedding<T>`, `GeneratedEmbeddings<T>` (MEAI)                        |
| Embedding options        | `EmbeddingGenerationOptions` TypedDict          | `EmbeddingGenerationOptions` record (MEAI)                                      |
| Embedding telemetry      | `EmbeddingTelemetryLayer` (MRO-based)           | `OpenTelemetryEmbeddingGenerator<TInput, TEmbedding>` (MEAI built-in)           |
| Vector store abstraction | `BaseVectorStore` / `BaseVectorCollection`      | `VectorStore` / `VectorStoreCollection<TKey, TRecord>` (VectorData)             |
| Vector search            | `BaseVectorSearch` / `SupportsVectorSearch`     | `VectorStoreCollection<TKey, TRecord>.SearchAsync()` (VectorData)               |
| Data model decorator     | `@vectorstoremodel`                             | `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]` attributes       |
| Collection definition    | `VectorStoreCollectionDefinition`               | `VectorStoreCollectionDefinition` (same name, VectorData)                       |
| Search options           | `SearchOptions`                                 | `VectorSearchOptions` (VectorData)                                              |
| Search result            | `SearchResponse`                                | `VectorSearchResult<TRecord>` (VectorData)                                      |
| Filter options           | `RecordFilterOptions`                           | `VectorSearchFilter` (VectorData)                                               |
| Enums (IndexKind etc.)   | `IndexKind`, `DistanceFunction`, `FieldTypes`   | `IndexKind`, `DistanceFunction` (same names, VectorData)                        |
| Protocols                | `SupportsVectorUpsert`, `SupportsVectorSearch`  | Interface methods on `VectorStoreCollection<TKey, TRecord>`                     |
| Hybrid search            | `search(search_type="keyword_hybrid")`          | `IKeywordHybridSearchable<TRecord>.HybridSearchAsync()` (VectorData)            |
| In-memory store          | `InMemoryCollection` / `InMemoryStore` in core  | `InMemoryVectorStore` (`Microsoft.Extensions.VectorData.InMemory` NuGet)        |
| Connectors               | Planned in `packages/` (13+)                    | SK connector packages (all implement VectorData interfaces)                     |
| Agent search tool        | `create_search_tool` → `FunctionTool`           | `TextSearchProvider` (AF ships this)                                            |
| Agent upsert tool        | `create_upsert_tool` → `FunctionTool`           | `TextSearchStore.UpsertDocumentsAsync` / `UpsertTextAsync` (AF ships this)      |
| Agent get/delete tools   | `create_get_tool`, `create_delete_tool`         | Direct `VectorStoreCollection` methods (no AF wrapper yet)                      |
| TextSearch abstraction   | `TextSearch` base class, `TextSearchResult`     | `TextSearchProvider` + `TextSearchResult` (AF ships its own)                    |
| TextSearch impls         | Brave, Google Search                            | Plug any `Func<string, CancellationToken, Task<IEnumerable<TextSearchResult>>>` |
| Exceptions               | `VectorStoreException` hierarchy                | `VectorStoreOperationException` etc. (VectorData)                               |

---

## Key Design Differences

### 1. No custom abstractions — delegate to MEAI + VectorData

Python AF is building its own `BaseEmbeddingClient`, `BaseVectorCollection`, `BaseVectorSearch`, `@vectorstoremodel`
etc. because Python has no ecosystem-standard equivalent.

.NET already has all of these in `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData`. AF .NET does
**not** re-implement them. It consumes them.

```csharp
// Python: BaseEmbeddingClient.get_embeddings(values, options)
// .NET:   IEmbeddingGenerator<string, Embedding<float>>.GenerateAsync(values, options)

IEmbeddingGenerator<string, Embedding<float>> generator =
    azureOpenAIClient.GetEmbeddingClient("text-embedding-3-small").AsIEmbeddingGenerator();

GeneratedEmbeddings<Embedding<float>> result = await generator.GenerateAsync(["Hello, world!"]);
```

```csharp
// Python: @vectorstoremodel + VectorStoreField(field_type=FieldTypes.VECTOR, dimensions=1536)
// .NET:   Attributes on the record class

public class MyDocument
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Text { get; set; }

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

### 2. AF .NET adds value at the agent layer, not the abstraction layer

AF .NET ships these higher-level components that have no direct ecosystem equivalent:

- **`ChatHistoryMemoryProvider`** — semantic chat history storage + retrieval using any `VectorStore`
- **`TextSearchProvider`** — wraps any search function as an `AIContextProvider` for agents
- **`TextSearchStore`** — opinionated upsert/search helper over `VectorStoreCollection`
- **`Microsoft.Agents.AI.CosmosNoSql`** — Cosmos DB NoSQL vector store connector (AF owns this)
- **`Microsoft.Agents.AI.FoundryMemory`** — Foundry-backed memory provider

### 3. Hybrid search via interface detection, not a parameter

Python AF uses `search(search_type="keyword_hybrid")` — a single method with a `Literal` parameter.

.NET VectorData uses a separate interface `IKeywordHybridSearchable<TRecord>`. `TextSearchStore` detects this at
runtime via `GetService()` and falls back gracefully:

```csharp
// Automatically uses hybrid search if the vector store supports it
var hybridSearchCollection =
    collection.GetService(typeof(IKeywordHybridSearchable<Dictionary<string, object?>>))
    as IKeywordHybridSearchable<Dictionary<string, object?>>;

var results = hybridSearchCollection is null
    ? collection.SearchAsync(query, top)          // vector-only fallback
    : hybridSearchCollection.HybridSearchAsync(   // hybrid if available
        query, wordSegmenter(query), top);
```

### 4. Dynamic collections vs. typed records

Python AF uses `@vectorstoremodel` to define typed records. .NET VectorData supports both:

```csharp
// Option 1: Strongly-typed record (equivalent to @vectorstoremodel)
VectorStoreCollection<string, MyDocument> typed =
    vectorStore.GetCollection<string, MyDocument>("my-collection");

// Option 2: Dynamic/schemaless (used by TextSearchStore and ChatHistoryMemoryProvider)
VectorStoreCollection<object, Dictionary<string, object?>> dynamic =
    vectorStore.GetDynamicCollection("my-collection", definition);
```

`TextSearchStore` and `ChatHistoryMemoryProvider` use the dynamic approach with a
`VectorStoreCollectionDefinition` so the caller doesn't need to define a record type — equivalent to Python's
`@vectorstoremodel` but with runtime-provided dimensions.

---

## Phase-by-Phase .NET Status

### Phase 1: Core Embedding Abstractions ✅ DONE (ecosystem)

| Python deliverable           | .NET equivalent                                                     | Location                       |
|------------------------------|---------------------------------------------------------------------|--------------------------------|
| `EmbeddingInputT` TypeVar    | Generic type parameter `TInput`                                     | MEAI                           |
| `EmbeddingT` TypeVar         | Generic type parameter `TEmbedding`                                 | MEAI                           |
| `Embedding[EmbeddingT]`      | `Embedding<TEmbedding>`                                             | MEAI                           |
| `GeneratedEmbeddings[T]`     | `GeneratedEmbeddings<TEmbedding>`                                   | MEAI                           |
| `EmbeddingGenerationOptions` | `EmbeddingGenerationOptions`                                        | MEAI                           |
| `SupportsGetEmbeddings`      | `IEmbeddingGenerator<TInput, TEmbedding>`                           | MEAI                           |
| `BaseEmbeddingClient`        | `IEmbeddingGenerator<TInput, TEmbedding>` (interface, not ABC)      | MEAI                           |
| `EmbeddingTelemetryLayer`    | `OpenTelemetryEmbeddingGenerator<TInput, TEmbedding>`               | MEAI                           |
| `OpenAIEmbeddingClient`      | `openAIClient.GetEmbeddingClient(...).AsIEmbeddingGenerator()`      | OpenAI + MEAI adapter          |
| `AzureOpenAIEmbeddingClient` | `azureOpenAIClient.GetEmbeddingClient(...).AsIEmbeddingGenerator()` | Azure.AI.OpenAI + MEAI adapter |

**No AF .NET work needed** — all shipped by MEAI.

---

### Phase 2: Embedding Generators for Existing Providers ✅ DONE (ecosystem)

| Python package        | .NET equivalent                                                |
|-----------------------|----------------------------------------------------------------|
| `packages/azure-ai/`  | `Azure.AI.Inference` → `.AsIEmbeddingGenerator()`              |
| `packages/ollama/`    | `Microsoft.Extensions.AI.Ollama` → `OllamaEmbeddingGenerator`  |
| `packages/anthropic/` | Not applicable (Anthropic does not offer embeddings)           |
| `packages/bedrock/`   | `AWSSDK.BedrockRuntime` + custom `IEmbeddingGenerator` wrapper |

**No AF .NET work needed** — adapters ship with the respective SDK packages.

---

### Phase 3: Core Vector Store Abstractions ✅ DONE (ecosystem)

| Python deliverable (`_vectors.py`)   | .NET equivalent                                                  | NuGet package                     |
|--------------------------------------|------------------------------------------------------------------|-----------------------------------|
| `FieldTypes` enum                    | Separate property types (`VectorStoreKeyProperty` etc.)          | `Microsoft.Extensions.VectorData` |
| `IndexKind` enum                     | `IndexKind` (same name)                                          | `Microsoft.Extensions.VectorData` |
| `DistanceFunction` enum              | `DistanceFunction` (same name)                                   | `Microsoft.Extensions.VectorData` |
| `VectorStoreField`                   | `VectorStoreProperty` base + subtypes                            | `Microsoft.Extensions.VectorData` |
| `VectorStoreCollectionDefinition`    | `VectorStoreCollectionDefinition` (same name)                    | `Microsoft.Extensions.VectorData` |
| `@vectorstoremodel` decorator        | `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`   | `Microsoft.Extensions.VectorData` |
| `SearchOptions`                      | `VectorSearchOptions`                                            | `Microsoft.Extensions.VectorData` |
| `score_threshold` in `SearchOptions` | `VectorSearchOptions.Filter` + post-filtering                    | `Microsoft.Extensions.VectorData` |
| `SearchResponse`                     | `VectorSearchResult<TRecord>`                                    | `Microsoft.Extensions.VectorData` |
| `RecordFilterOptions`                | `VectorSearchFilter`                                             | `Microsoft.Extensions.VectorData` |
| `SupportsVectorUpsert` Protocol      | Methods on `VectorStoreCollection<TKey, TRecord>`                | `Microsoft.Extensions.VectorData` |
| `SupportsVectorSearch` Protocol      | `IVectorSearchable<TRecord>` / `IKeywordHybridSearchable<T>`     | `Microsoft.Extensions.VectorData` |
| `BaseVectorCollection`               | `VectorStoreCollection<TKey, TRecord>` (abstract)                | `Microsoft.Extensions.VectorData` |
| `BaseVectorStore`                    | `VectorStore` (abstract)                                         | `Microsoft.Extensions.VectorData` |
| `BaseVectorSearch`                   | `VectorStoreCollection<TKey, TRecord>` (search is on collection) | `Microsoft.Extensions.VectorData` |
| `VectorStoreRecordHandler`           | `IVectorStoreRecordMapper<TRecord, TStorageModel>`               | `Microsoft.Extensions.VectorData` |
| `VectorStoreException` hierarchy     | `VectorStoreOperationException` etc.                             | `Microsoft.Extensions.VectorData` |
| `create_search_tool`                 | `TextSearchProvider` (**AF ships this**)                         | `Microsoft.Agents.AI`             |
| `DISTANCE_FUNCTION_DIRECTION_HELPER` | Built into `DistanceFunction` semantics in VectorData            | `Microsoft.Extensions.VectorData` |

**No AF .NET work needed for the abstractions** — all shipped by VectorData.
**AF .NET ships `TextSearchProvider`** as the `create_search_tool` equivalent.

---

### Phase 4: In-Memory Vector Store ✅ DONE (ecosystem)

| Python deliverable   | .NET equivalent       | NuGet package                              |
|----------------------|-----------------------|--------------------------------------------|
| `InMemoryCollection` | `InMemoryVectorStore` | `Microsoft.Extensions.VectorData.InMemory` |
| `InMemoryStore`      | `InMemoryVectorStore` | `Microsoft.Extensions.VectorData.InMemory` |
| FAISS extension      | No official NuGet yet | Community or custom                        |

**No AF .NET work needed.**

---

### Phases 5–7: Vector Store Connectors ✅ DONE (ecosystem)

All SK connector packages implement `VectorStore` / `VectorStoreCollection<TKey, TRecord>` from VectorData and
work directly in AF .NET without any AF-specific work.

| Python planned package      | .NET NuGet package                                   | Status     |
|-----------------------------|------------------------------------------------------|------------|
| `packages/azure-ai-search/` | `Microsoft.SemanticKernel.Connectors.AzureAISearch`  | ✅ Exists   |
| `packages/qdrant/`          | `Microsoft.SemanticKernel.Connectors.Qdrant`         | ✅ Exists   |
| `packages/redis/`           | `Microsoft.SemanticKernel.Connectors.Redis`          | ✅ Exists   |
| `packages/postgres/`        | `Microsoft.SemanticKernel.Connectors.Postgres`       | ✅ Exists   |
| `packages/mongodb/`         | `Microsoft.SemanticKernel.Connectors.MongoDB`        | ✅ Exists   |
| `packages/azure-cosmos-db/` | `Microsoft.Agents.AI.CosmosNoSql` (**AF owns this**) | ✅ AF ships |
| `packages/pinecone/`        | `Microsoft.SemanticKernel.Connectors.Pinecone`       | ✅ Exists   |
| `packages/chroma/`          | `Microsoft.SemanticKernel.Connectors.Chroma`         | ✅ Exists   |
| `packages/weaviate/`        | `Microsoft.SemanticKernel.Connectors.Weaviate`       | ✅ Exists   |
| `packages/sql-server/`      | `Microsoft.SemanticKernel.Connectors.SqlServer`      | ✅ Exists   |
| `packages/oracle/`          | Community / custom                                   | ⚠️ Gap     |
| `packages/faiss/`           | No official NuGet yet                                | ⚠️ Gap     |

**No AF .NET work needed** — except `Microsoft.Agents.AI.CosmosNoSql` which AF owns.

---

### Phase 8: Vector Store CRUD + Search Tools ✅ DONE (AF ships these)

This is where AF .NET adds its own value on top of the ecosystem primitives.

| Python deliverable   | .NET AF equivalent                                | Location                       | Status     |
|----------------------|---------------------------------------------------|--------------------------------|------------|
| `create_search_tool` | `TextSearchProvider`                              | `Microsoft.Agents.AI`          | ✅ AF ships |
| `create_upsert_tool` | `TextSearchStore.UpsertDocumentsAsync`            | AF sample → promoted to core   | ✅ AF ships |
| `create_get_tool`    | `VectorStoreCollection<TKey,TRecord>.GetAsync`    | VectorData (no AF wrapper yet) | ⚠️ Gap     |
| `create_delete_tool` | `VectorStoreCollection<TKey,TRecord>.DeleteAsync` | VectorData (no AF wrapper yet) | ⚠️ Gap     |

#### `TextSearchProvider` — the `create_search_tool` equivalent

Wraps any `Func<string, CancellationToken, Task<IEnumerable<TextSearchResult>>>` and exposes it
as an `AIContextProvider` on an agent. Supports:

- `BeforeAIInvoke` — auto-search before every model call (pre-fetched RAG)
- `OnDemand` — exposed as a callable `AITool` to the model
- `RecentMessageMemoryLimit` — rolling context window to construct the search query

```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    AIContextProviders =
    [
        new TextSearchProvider(
            searchFunction: MySearchAsync,
            options: new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                RecentMessageMemoryLimit = 6,
            })
    ]
});
```

#### `TextSearchStore` — the `create_upsert_tool` + search equivalent

Opinionated helper over `VectorStoreCollection<object, Dictionary<string, object?>>`:

```csharp
// Upsert (equivalent to create_upsert_tool)
await textSearchStore.UpsertDocumentsAsync(documents);
await textSearchStore.UpsertTextAsync(textChunks);

// Search (equivalent to create_search_tool's underlying behavior)
IEnumerable<TextSearchDocument> results = await textSearchStore.SearchAsync(query, top: 5);
```

---

### Phase 9: Additional Embedding Providers ⚠️ PARTIAL (ecosystem)

| Python package               | .NET equivalent                                                   | Status         |
|------------------------------|-------------------------------------------------------------------|----------------|
| `packages/huggingface-onnx/` | `Microsoft.ML.OnnxRuntime` + custom `IEmbeddingGenerator` wrapper | ⚠️ No official |
| `packages/mistral/`          | `Mistral.SDK` + custom `IEmbeddingGenerator` wrapper              | ⚠️ No official |
| `packages/google/`           | MEAI Google adapter (in progress)                                 | ⚠️ In progress |
| `packages/nvidia/`           | Custom `IEmbeddingGenerator` over Nvidia NIM endpoints            | ⚠️ No official |

The plug point (`IEmbeddingGenerator<TInput, TEmbedding>`) is well-defined. The gap is that not all providers
have first-party .NET MEAI adapters yet — mirroring the Python situation.

---

### Phase 10: TextSearch Abstractions ✅ DONE (AF ships its own)

AF .NET ships `TextSearchProvider` as its own TextSearch abstraction — not relying on
`Microsoft.SemanticKernel.Data.ITextSearch`. It is more agent-native than SK's version:

| Python deliverable              | .NET AF equivalent                                         |
|---------------------------------|------------------------------------------------------------|
| `TextSearch` base class         | `TextSearchProvider` (AF)                                  |
| `TextSearchResult`              | `TextSearchProvider.TextSearchResult` (AF)                 |
| `SearchOptions`                 | `TextSearchProviderOptions` (AF)                           |
| Brave Search impl               | Plug `TextSearchProvider(BraveSearchAsync, options)`       |
| Google Search impl              | Plug `TextSearchProvider(GoogleSearchAsync, options)`      |
| Vector store text search bridge | `TextSearchProvider(textSearchStore.SearchAsync, options)` |

The bridge between `TextSearchStore` (vector search) and `TextSearchProvider` (agent context) is
intentionally a single line — pass `textSearchStore.SearchAsync` as the search function.

---

## What AF .NET Still Needs to Build

| Gap                                                     | Recommendation                                                                                |
|---------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| `create_get_tool` equivalent                            | Wrap `VectorStoreCollection.GetAsync` as an `AIFunction` via `AIFunctionFactory.Create()`     |
| `create_delete_tool` equivalent                         | Wrap `VectorStoreCollection.DeleteAsync` as an `AIFunction` via `AIFunctionFactory.Create()`  |
| HuggingFace / ONNX embeddings                           | Contribute `IEmbeddingGenerator` adapter to MEAI ecosystem or ship in `Microsoft.Agents.AI.*` |
| Mistral / Nvidia embeddings                             | Same as above                                                                                 |
| `TextSearchStore` → promoted from sample to first-class | Currently in samples namespace; candidate for `Microsoft.Agents.AI` core                      |
| `score_threshold` in `VectorSearchOptions`              | Track SK .NET PR #13501 pattern for post-filtering                                            |
| Oracle connector                                        | Either contribute to SK connectors or ship `Microsoft.Agents.AI.Oracle`                       |
| FAISS connector                                         | Community package or `Microsoft.Agents.AI.FAISS` extending `InMemoryVectorStore`              |
