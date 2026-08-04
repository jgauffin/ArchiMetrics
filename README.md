# ArchiMetrics (Fork)

This is a fork of [jjrdk/ArchiMetrics](https://github.com/jjrdk/ArchiMetrics), a Roslyn-based C# code analysis toolkit.

## Purpose

This fork exists to provide **code quality metrics to AI agents** via the [csharp-language-server](https://github.com/jgauffin/csharp-language-server) MCP server. By exposing metrics like complexity, coupling, and duplication through a language server, agents can reason about code quality when reviewing, refactoring, or generating code.

## What this fork adds

### CodeAnalysisAgent (main facade)

`CodeAnalysisAgent` is the primary entry point for tools and agents consuming ArchiMetrics. It wraps a Roslyn `Workspace` and exposes all analysis capabilities through a paged API:

```csharp
var agent = new CodeAnalysisAgent(workspace, rootFolder);

// Metrics
var metrics = await agent.CalculateMetrics(projectName: "MyProject");
var worstNamespaces = await agent.GetWorstNamespaces(take: 10);
var worstTypes = await agent.GetWorstTypes(take: 10);
var worstMethods = await agent.GetWorstMethods(take: 10);

// Duplication (requires embedding provider for semantic layer)
var clones = await agent.DetectDuplication(similarityThreshold: 0.85);

// ISO 5055 report (no embedding provider needed)
var inspector = new NodeReviewer(syntaxRules, symbolRules);
var report = await agent.GenerateIso5055Report(inspector, allRules);
```

### Code duplication detection

Two-layer approach:

1. **AST Fingerprinting** — Normalizes Roslyn syntax trees (replacing identifiers/literals with generic tokens), hashes them, and groups by hash. Catches exact copies and renamed-variable clones instantly.
2. **Embedding Similarity** (optional) — Feeds normalized method text through a local ONNX model ([UniXcoder](https://github.com/microsoft/CodeBERT/tree/main/UniXcoder)) and compares cosine similarity. Catches semantic clones that differ structurally.

Layer 1 runs first as a fast pass. Layer 2 runs on remaining methods. The embedding layer is optional — if no model is provided, only AST fingerprinting runs.

#### Setting up the embedding model

```bash
cd models
python -m venv .venv
.venv\Scripts\activate        # Windows
# source .venv/bin/activate   # Linux/macOS
pip install -r requirements.txt
python download-unixcoder.py
```

Creates `models/unixcoder/` with `model.onnx`, `vocab.json`, and `merges.txt`.

### Partial ISO/IEC 5055 support

ArchiMetrics can generate reports aligned with the [ISO/IEC 5055](https://www.iso.org/standard/80623.html) standard, which maps CWE (Common Weakness Enumeration) patterns to four quality categories. The report does **not** require an embedding provider — it uses only the rule engine and basic metrics.

**How it works:**

1. Existing and new rules implement the optional `ICweMapping` interface, mapping each rule to CWE identifiers and an ISO 5055 category.
2. `NodeReviewer` runs the rules and populates CWE metadata on each `EvaluationResult`.
3. `Iso5055ReportGenerator` aggregates violations by category and normalises as violations/KLOC.

**Categories and current coverage:**

| Category | Focus | Coverage |
|----------|-------|----------|
| **Security** | Exploitable vulnerabilities | Low — pattern-match rules only (unsafe code, CWE-242). No taint analysis. |
| **Reliability** | Crash/corruption risks | Moderate — dispose pattern, stack trace destruction, event leaks, weak identity locks, virtual calls in constructors |
| **Performance Efficiency** | Resource waste | Low — sync-over-async detection (CWE-1049) |
| **Maintainability** | Structural decay | Good — cyclomatic complexity, deep nesting, large classes/methods, too many parameters, lack of cohesion, class instability, goto statements |

**Limitations:** This is not a SAST replacement. Taint-analysis-dependent CWEs (SQL injection, XSS, command injection) require dedicated tools like CodeQL or Semgrep. The report includes `CoveredCweIds` so consumers know exactly which CWEs are in scope.

```csharp
// Generate an ISO 5055 report
var syntaxRules = AllRules.GetSyntaxRules(spellChecker: null);
var symbolRules = AllRules.GetSymbolRules();
var inspector = new NodeReviewer(syntaxRules, symbolRules);
var allRules = syntaxRules.Cast<IEvaluation>().Concat(symbolRules);

var report = await agent.GenerateIso5055Report(inspector, allRules);

// report.Security.Passes       — true if zero critical security violations
// report.Reliability.Passes    — true if zero critical reliability violations
// report.Maintainability.ViolationsPerKloc — density metric
// report.CoveredCweIds          — which CWEs the loaded rules can detect
```

## Metrics

ArchiMetrics calculates metrics at four levels of granularity — project, namespace, type and member. Values
are measured at member level; everything above is an aggregate.

### Scales

| Metric | Range | Direction | Notes |
|---|---|---|---|
| Maintainability index | 0–100 | Higher is better | Normalised and clamped at 0, not the raw formula that can go negative. A member with no measurable body scores 100. |
| Cyclomatic complexity | 1 and up | Lower is better | Independent paths through the code. A lower bound on the test cases needed for branch coverage. |
| Lines of code | 0 and up | Neither | Source lines carrying code. Blank lines, comments and documentation excluded; brace-only lines included. |
| Executable statements | 0 and up | Neither | Size in units of work, unaffected by formatting. The size term in the maintainability index. |
| Depth of inheritance | 0 and up | Lower is better | Base types above a type. |
| Efferent coupling | 0 and up | Lower is better | Dependencies going out. |
| Afferent coupling | 0 and up | Neither | Dependencies coming in. High means risky to change, not badly written. |
| Instability | 0.0–1.0 | Neither | `Ce / (Ca + Ce)`. 0 = stable and widely depended on; 1 = free to change. |
| Abstractness | 0.0–1.0 | Neither | Share of types that are abstract. Read against how much depends on the namespace or project. |
| Relational cohesion | 0.0 and up | Higher, within reason | Average internal relationships per type — do these types belong together? |

### Ratings

Values are turned into a 1–5 rating by `MetricThresholds`, where **lower is better** — the opposite direction
to the maintainability index, so never present the two as the same kind of number. An element is rated by its
*worst* metric, not its average, because one unmaintainable method is a real cost however tidy the rest is.

| Rating | Maintainability | Complexity | Inheritance depth | Efferent coupling |
|---|---|---|---|---|
| 1 Healthy | ≥ 70 | ≤ 10 | ≤ 3 | ≤ 10 |
| 2 Acceptable | ≥ 50 | ≤ 20 | ≤ 5 | ≤ 20 |
| 3 Concerning | ≥ 30 | ≤ 30 | ≤ 6 | ≤ 30 |
| 4 Problematic | ≥ 15 | ≤ 50 | ≤ 8 | ≤ 40 |
| 5 Fix ASAP | < 15 | > 50 | > 8 | > 40 |

Every boundary lives in `MetricThresholds` and nowhere else, so a report and a review rule cannot disagree
about the same method. Classify values with `MetricThresholds.RateMaintainability` and friends rather than
writing your own comparisons. `GenerateWorkspaceSummary()` prints this legend above its output.

**Not comparable to Visual Studio.** Complexity and both size metrics are counted differently from the
Visual Studio metrics of the same names. Use them to compare elements within a solution, not across tools.

## Code review rules

61 rules across syntax, semantic, and trivia analysis — covering code quality, maintainability, testability, modifiability, conformance, security, and performance. Rules are discovered via reflection and run through `NodeReviewer`.

## Original project

ArchiMetrics was created by [Jacob Reimers](https://github.com/jjrdk). See the [original repository](https://github.com/jjrdk/ArchiMetrics) for history and license information.
