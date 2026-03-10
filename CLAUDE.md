# ArchiMetrics Project Instructions

## Documentation Style

When writing or updating code comments and documentation, target a junior developer audience. Explain **what the code is trying to achieve from a code quality perspective** — not just what it does mechanically, but *why* it matters for maintainability, readability, or correctness. Help the reader understand the quality goal behind the design choice.

## Project Overview

- .NET 9.0 C# static code analysis tool (metrics, code review, duplication detection)
- Test framework: xUnit — run with `dotnet test` from repo root
- `models/` contains Python ML code (.venv with torch, etc.) — don't read internals

## Key Structure

- `src/ArchiMetrics.Analysis/Metrics/` — DuplicationDetector, EmbeddingSimilarityAnalyzer, SyntaxFingerprintAnalyzer, MethodExtractor, SyntaxNormalizer
- `src/ArchiMetrics.Analysis/Common/Metrics/` — DTOs: CloneClass, CloneInstance, ClonePair, DuplicationResult, CloneType
- `tests/ArchiMetrics.Analysis.Tests/` — test project, uses nested classes for grouping

## CodeAnalysisAgent (Primary API)

`CodeAnalysisAgent` is the main facade — it's designed as the entry point for coding agents and tools consuming ArchiMetrics. It wraps Roslyn's `Workspace` and exposes:

- `CalculateMetrics()` — namespace-level code metrics (maintainability index, cyclomatic complexity, etc.)
- `DetectDuplication()` — two-layer clone detection (AST + embeddings)
- `FindNeedsDocsOrRefactor()` — finds methods that are opaque and likely need documentation or refactoring (requires an embedding provider)
- `GenerateWorkspaceSummary()` — text summary of workspace metrics

All methods support pagination (`skip`/`take`) and optional `projectName` filtering. Factory method `WithOnnxModel()` creates an agent with a local ONNX embedding model. The agent owns and disposes the embedding provider when created via factory.

## Duplication Detection (Two-Layer)

1. **Layer 1 (AST)**: `SyntaxFingerprintAnalyzer` — exact/renamed clones via fingerprinting
2. **Layer 2 (Embeddings)**: `EmbeddingSimilarityAnalyzer` — semantic clones via cosine similarity
- Union-Find groups pairs into clusters; `maxClusterSize` (default 15) filters false-positive pattern clusters
- Tests use `FakeEmbeddingProvider` for controlled cosine similarity

## Testing Conventions

- Test naming: `When<Condition>Then<Expected>` (e.g., `WhenTwoMethodsAreIdenticalThenDetectsClone`)
- Group tests in nested classes by scenario (e.g., `GivenAstFingerprinting`, `GivenEmbeddingLayer`)
- No shared test fixtures — each test constructs its own data
- **Always reproduce a bug with a failing test before fixing it**
