# ArchiMetrics (Fork)

This is a fork of [jjrdk/ArchiMetrics](https://github.com/jjrdk/ArchiMetrics), a Roslyn-based C# code analysis toolkit.

## Purpose

This fork exists to provide **code quality metrics to AI agents** via the [csharp-language-server](https://github.com/jgauffin/csharp-language-server) MCP server. By exposing metrics like complexity, coupling, and duplication through a language server, agents can reason about code quality when reviewing, refactoring, or generating code.

## What changed in this fork

The original project provides code metrics (complexity, coupling, Halstead, etc.). This fork adds **code duplication detection** using a layered approach:

1. **AST Fingerprinting** — Normalizes Roslyn syntax trees (replacing identifiers/literals with generic tokens), hashes them, and groups by hash. Catches exact copies and renamed-variable clones instantly with zero dependencies.
2. **Embedding Similarity** — Feeds normalized method text through a local ONNX model ([UniXcoder](https://github.com/microsoft/CodeBERT/tree/main/UniXcoder)) and compares cosine similarity between embedding vectors. Catches semantic clones that differ structurally but do the same thing.

Layer 1 runs first as a fast pass. Layer 2 runs on the remaining methods, skipping pairs already detected by Layer 1. The embedding layer is optional — if no model is provided, only AST fingerprinting runs.

### New files

| File | Purpose |
|------|---------|
| `Metrics/DuplicationDetector.cs` | Public facade — layers both approaches |
| `Metrics/SyntaxFingerprintAnalyzer.cs` | Layer 1: hash-based clone detection |
| `Metrics/SyntaxNormalizer.cs` | Replaces identifiers/literals with generic tokens |
| `Metrics/MethodExtractor.cs` | Walks syntax trees, extracts method bodies |
| `Metrics/EmbeddingSimilarityAnalyzer.cs` | Layer 2: cosine similarity on embeddings |
| `Metrics/OnnxEmbeddingProvider.cs` | ONNX Runtime + CodeGenTokenizer integration |
| `Common/Metrics/IEmbeddingProvider.cs` | Pluggable interface for embedding backends |
| `Common/Metrics/CloneType.cs` | Enum: Exact, Renamed, Semantic |
| `Common/Metrics/CloneInstance.cs` | A code location that's part of a clone |
| `Common/Metrics/ClonePair.cs` | Two instances + similarity score |
| `Common/Metrics/CloneClass.cs` | A group of clone instances |
| `Common/Metrics/DuplicationResult.cs` | Top-level result container |
| `models/download-unixcoder.py` | Downloads and quantizes UniXcoder to ONNX |

### New dependencies

- `Microsoft.ML.OnnxRuntime` 1.22.0 — local ONNX model inference
- `Microsoft.ML.Tokenizers` 1.0.2 — BPE tokenization (CodeGenTokenizer)

---

## Original project

ArchiMetrics is a collection of code analysis tools using Roslyn. It calculates code metrics which can be queried using normal LINQ syntax.

The project calculates the following metrics:

### Project Level

- Cyclomatic Complexity
- LinesOfCode
- Maintainability Index
- Project Dependencies
- Type Couplings
- Abstractness
- Afferent Coupling
- Efferent Coupling
- RelationalCohesion

### Namespace Level

- Cyclomatic Complexity
- LinesOfCode
- Maintainability Index
- Project Dependencies
- Type Couplings
- Depth of Inheritance
- Abstractness

### Type Level

- Cyclomatic Complexity
- LinesOfCode
- Maintainability Index
- Project Dependencies
- Type Couplings
- Depth Of Inheritance
- Type Coupling
- Afferent Coupling
- Efferent Coupling
- Instability

### Member Level

- Cyclomatic Complexity
- Lines Of Code
- Maintainability Index
- Project Dependencies
- Type Couplings
- Number Of Parameters
- Number Of Local Variables
- Afferent Coupling
- Halstead Metrics

### Code Duplication Detection

ArchiMetrics includes a layered code duplication detector:

- **Layer 1 — AST Fingerprinting**: Fast, deterministic detection of exact and renamed-variable clones using Roslyn syntax tree hashing.
- **Layer 2 — Embedding Similarity** (optional): Semantic clone detection using a local ONNX embedding model (UniXcoder).

#### Setting up the embedding model

Layer 2 requires a local ONNX model. To download and export UniXcoder (~125 MB quantized):

```bash
cd models
python -m venv .venv
.venv\Scripts\activate        # Windows
# source .venv/bin/activate   # Linux/macOS
pip install -r requirements.txt
python download-unixcoder.py
```

This creates `models/unixcoder/` with `model.onnx`, `vocab.json`, and `merges.txt`.

#### Usage

```csharp
// Layer 1 only (no model needed)
var detector = new DuplicationDetector(rootFolder);
var result = await detector.Detect(syntaxTrees);

// Both layers (with embedding model)
using var provider = OnnxEmbeddingProvider.Create(
    modelPath: @"models\unixcoder\model.onnx",
    vocabPath: @"models\unixcoder\vocab.json",
    mergesPath: @"models\unixcoder\merges.txt");

var detector = new DuplicationDetector(rootFolder, provider,
    minimumTokens: 50, similarityThreshold: 0.85);
var result = await detector.Detect(syntaxTrees);

foreach (var clone in result.Clones)
    Console.WriteLine($"{clone.CloneType}: {clone.Instances.Count} instances, similarity {clone.Similarity:P0}");
```

## Using project

If you are going to use metrics, you must install

[Microsoft Build Tools 2015 RC](http://www.microsoft.com/en-us/download/details.aspx?id=46882&WT.mc_id=rss_alldownloads_all)

You also may need to install this package (included in latest nuget package)

```
Install-Package Microsoft.Composition
```

See this sample that loads your solution and prints the cyclomatic complexity for each namespace that belongs to your solution

````csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using ArchiMetrics.Analysis;
using ArchiMetrics.Common;

namespace ConsoleApplication
{
    class Program
    {
        static void Main(string[] args)
        {
            var task = Run();
            task.Wait();
        }

        private static async Task Run()
        {
            Console.WriteLine("Loading Solution");
            var solutionProvider = new SolutionProvider();
            var solution = await solutionProvider.Get(@"MyFullPathSolutionFile.sln");
            Console.WriteLine("Solution loaded");

            var projects = solution.Projects.ToList();

            Console.WriteLine("Loading metrics, wait it may take a while.");
            var metricsCalculator = new CodeMetricsCalculator();
            var calculateTasks = projects.Select(p => metricsCalculator.Calculate(p, solution));
            var metrics = (await Task.WhenAll(calculateTasks)).SelectMany(nm => nm);
            foreach (var metric in metrics)
                Console.WriteLine("{0} => {1}", metric.Name, metric.CyclomaticComplexity);
        }
    }
}
````
