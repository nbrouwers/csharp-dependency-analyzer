# Implementation Plan: C# Dependency Analyzer

This plan implements the requirements defined in [requirements.md](requirements.md).

## Solution Structure

```
csharp-dependency-analyzer/
├── docs/
│   ├── requirements.md
│   └── implementation-plan.md
├── src/
│   └── DependencyAnalyzer/              # Console application (NFR-1.3)
│       ├── DependencyAnalyzer.csproj
│       ├── Program.cs                   # Entry point, CLI parsing
│       ├── Models/
│       │   ├── ElementKind.cs           # Enum: Class, Interface, Struct, Enum, Record, Delegate
│       │   ├── TypeDependency.cs        # Represents a dependency edge (source → target, reason)
│       │   ├── FanInElement.cs          # Result element: FQN, kind, justification chain
│       │   └── AnalysisResult.cs        # Full result: target FQN, list of FanInElements, metrics
│       ├── Analysis/
│       │   ├── RoslynWorkspaceBuilder.cs    # Builds Roslyn compilation from source files
│       │   ├── DependencyGraphBuilder.cs    # Extracts type→type dependency edges via Roslyn
│       │   ├── TransitiveFanInAnalyzer.cs   # Computes transitive fan-in closure on the graph
│       │   └── DependencyVisitor.cs         # SyntaxWalker that collects type references
│       └── Reporting/
│           └── MarkdownReportGenerator.cs   # Generates the Markdown report
├── tests/
│   └── DependencyAnalyzer.Tests/        # xUnit test project (VR-2)
│       ├── DependencyAnalyzer.Tests.csproj
│       ├── RoslynWorkspaceBuilderTests.cs
│       ├── DependencyGraphBuilderTests.cs
│       ├── TransitiveFanInAnalyzerTests.cs
│       ├── MarkdownReportGeneratorTests.cs
│       └── EndToEndTests.cs
├── samples/
│   └── SampleCodebase/                  # Artificial validation codebase (VR-1)
│       ├── README.md                    # Documents expected fan-in results
│       └── *.cs                         # Sample source files
└── CSharpDependencyAnalyzer.sln
```

## Phases

### Phase 1: Project Scaffolding

**Goal**: Set up solution, projects, and NuGet references.

| Step | Action | Covers |
|------|--------|--------|
| 1.1 | Create solution `CSharpDependencyAnalyzer.sln` | — |
| 1.2 | Create console project `src/DependencyAnalyzer/DependencyAnalyzer.csproj` targeting `net8.0` | NFR-1.1, NFR-1.3 |
| 1.3 | Add NuGet references: `Microsoft.CodeAnalysis.CSharp` (4.x), `Microsoft.CodeAnalysis.CSharp.Workspaces` (4.x), `System.CommandLine` (for CLI parsing) | NFR-1.2 |
| 1.4 | Create xUnit test project `tests/DependencyAnalyzer.Tests/DependencyAnalyzer.Tests.csproj` with references to `xunit`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, and a project reference to `DependencyAnalyzer` | VR-2.1 |
| 1.5 | Add both projects to the solution | — |

### Phase 2: Domain Models

**Goal**: Define the data structures used throughout the tool.

| Step | Action | Covers |
|------|--------|--------|
| 2.1 | `ElementKind.cs` — enum with values: `Class`, `Interface`, `Struct`, `Enum`, `Record`, `Delegate` | FR-4.2 |
| 2.2 | `TypeDependency.cs` — represents a single dependency edge: `SourceFqn`, `TargetFqn`, `DependencyReason` (string, e.g. "Inherits from") | FR-3.2 |
| 2.3 | `FanInElement.cs` — result element: `FullyQualifiedName`, `Kind` (ElementKind), `Justification` (string) | FR-4.2 |
| 2.4 | `AnalysisResult.cs` — aggregated result: `TargetFqn`, `List<FanInElement>`, computed metrics (count per kind) | FR-4.3 |

### Phase 3: Roslyn Workspace Builder

**Goal**: Load source files into a Roslyn compilation with a semantic model.

| Step | Action | Covers |
|------|--------|--------|
| 3.1 | `RoslynWorkspaceBuilder.cs` — `BuildCompilation(IEnumerable<string> filePaths)` method: reads each file, creates `SyntaxTree` instances, creates a `CSharpCompilation` with BCL metadata references, returns the `Compilation` object | FR-3.1, FR-2.2 |
| 3.2 | Handle missing/unreadable files: skip with warning logged to console | FR-2.3 |
| 3.3 | Validate target class FQN against the compilation's symbol table; return error if not found or ambiguous | FR-1.2 |

**Implementation notes**:
- Use `CSharpCompilation.Create()` with `MetadataReference.CreateFromFile()` for `System.Runtime`, `System.Collections`, `netstandard`, etc. to resolve BCL types.
- File reading uses `File.ReadAllText` + `CSharpSyntaxTree.ParseText`.
- Log number of files loaded vs. skipped (NFR-3.2).

### Phase 4: Dependency Graph Builder

**Goal**: Walk the syntax/semantic model and extract all type-to-type dependency edges.

| Step | Action | Covers |
|------|--------|--------|
| 4.1 | `DependencyVisitor.cs` — a `CSharpSyntaxWalker` that, for each type declaration, collects all referenced type symbols. Override visitors for: `BaseList` (inheritance, interfaces), `FieldDeclaration`, `PropertyDeclaration`, `EventDeclaration`, `MethodDeclaration` (params, return type), `LocalDeclarationStatement`, `ObjectCreationExpression`, `GenericName`, `AttributeList`, `MemberAccessExpression` (static access), `BinaryExpression` / `IsPatternExpression` (type checks), `TypeOfExpression` | FR-3.2 |
| 4.2 | For each type reference found, resolve via `SemanticModel.GetSymbolInfo()` or `SemanticModel.GetTypeInfo()` to obtain the `INamedTypeSymbol`. Map to FQN. Record the dependency reason. | FR-3.2 |
| 4.3 | `DependencyGraphBuilder.cs` — orchestrates: iterate all type declarations in the compilation, run the visitor on each, collect all `TypeDependency` edges into a graph (adjacency list: `Dictionary<string, List<TypeDependency>>`). Also record element kind per FQN. | FR-3.2 |
| 4.4 | Filter: only include edges where both source and target FQN are defined within the source scope. | FR-2.2 |

**Implementation notes**:
- Unwrap generic types to their definitions (e.g., `List<OrderService>` → dependency on `OrderService`).
- Unwrap nullable types (`OrderService?` → `OrderService`).
- Ignore built-in types (`string`, `int`, etc.) and types not defined in the source scope.
- Map `INamedTypeSymbol.TypeKind` to `ElementKind`.

### Phase 5: Transitive Fan-In Analyzer

**Goal**: Compute the transitive closure of fan-in from the dependency graph.

| Step | Action | Covers |
|------|--------|--------|
| 5.1 | `TransitiveFanInAnalyzer.cs` — `Analyze(string targetFqn, DependencyGraph graph)` method | FR-3.3 |
| 5.2 | Build a **reverse adjacency list** (dependedUpon → dependsOn) from the graph. For each edge A→B, add A to reverse[B]. | FR-3.3 |
| 5.3 | BFS/DFS from the target FQN on the reverse graph. All reachable nodes are the transitive fan-in. | FR-3.3 |
| 5.4 | Exclude the target class itself from the result set. | FR-3.4 |
| 5.5 | For each fan-in element, construct a justification string tracing the dependency chain back to the target (e.g., "Uses `ServiceHelper` → which inherits from `OrderService`"). | FR-4.2 |

**Algorithm detail**:
```
reverse_graph = invert(dependency_graph)
visited = {}
queue = [targetFqn]
while queue not empty:
    current = queue.dequeue()
    for each (dependor, reason) in reverse_graph[current]:
        if dependor not in visited:
            visited[dependor] = build_justification(dependor, current, reason)
            queue.enqueue(dependor)
return visited (excluding targetFqn)
```

### Phase 6: Report Generator

**Goal**: Produce the Markdown report file.

| Step | Action | Covers |
|------|--------|--------|
| 6.1 | `MarkdownReportGenerator.cs` — `Generate(AnalysisResult result, string outputPath)` | FR-4.1, FR-4.4 |
| 6.2 | Write header: target FQN, timestamp | FR-4.1 |
| 6.3 | Write fan-in table: #, FQN, Kind, Justification — sorted by FQN | FR-4.2 |
| 6.4 | Write metrics table: count per ElementKind + total | FR-4.3 |

### Phase 7: CLI Entry Point

**Goal**: Wire everything together via command-line interface.

| Step | Action | Covers |
|------|--------|--------|
| 7.1 | `Program.cs` — parse CLI args using `System.CommandLine`: `--target`, `--files`, `--output` | §5.1 |
| 7.2 | Read file list from `--files` text file (one path per line, trim, skip empty/comment lines) | FR-2.1 |
| 7.3 | Orchestrate: build compilation → build dependency graph → run fan-in analysis → generate report | All FR |
| 7.4 | Log progress to console: files loaded, types found, analysis started, fan-in count, report written | NFR-3.2 |
| 7.5 | Return exit code 0 on success, non-zero on error with message | NFR-3.1 |

### Phase 8: Sample Codebase

**Goal**: Create an artificial C# codebase for validation.

| Step | Action | Covers |
|------|--------|--------|
| 8.1 | Design a dependency graph on paper first (documented in `samples/SampleCodebase/README.md`) | VR-1.3 |
| 8.2 | Create ~15-20 source files covering all element kinds and dependency types: | VR-1.2 |

**Planned sample structure** (target: `SampleApp.Core.OrderService`):

```
SampleApp.Core.OrderService          (class)        ← TARGET
├── SampleApp.Core.IOrderRepository  (interface)    → uses OrderService as generic constraint
├── SampleApp.Core.OrderStatus       (enum)         → used by OrderService (NOT fan-in)
├── SampleApp.Core.OrderValidator    (class)        → field of type OrderService
│   └── SampleApp.Services.OrderPipeline (class)    → inherits OrderValidator (transitive)
├── SampleApp.Core.OrderRecord       (record)       → has OrderService parameter
├── SampleApp.Core.OrderCreatedHandler (delegate)   → signature includes OrderService
├── SampleApp.Data.OrderEntity       (struct)       → has typeof(OrderService)
├── SampleApp.Services.OrderController (class)      → creates new OrderService()
│   └── SampleApp.Services.AdminController (class)  → inherits OrderController (transitive)
├── SampleApp.Attributes.OrderAttribute (class)     → not a fan-in
├── SampleApp.Utils.Helpers          (class)        → no dependency (NOT fan-in)
└── SampleApp.Services.NotificationService (class)  → no dependency (NOT fan-in)
```

| Step | Action | Covers |
|------|--------|--------|
| 8.3 | Document expected fan-in: list all expected elements with kind and justification in `README.md` | VR-1.3 |
| 8.4 | Create `samples/SampleCodebase/filelist.txt` listing all sample `.cs` files | — |

### Phase 9: Unit Tests

**Goal**: Comprehensive test coverage.

| Step | Action | Test Cases | Covers |
|------|--------|------------|--------|
| 9.1 | `RoslynWorkspaceBuilderTests.cs` | Loads valid files; warns on missing files; resolves target FQN; error on unknown FQN | FR-1.2, FR-2.3 |
| 9.2 | `DependencyGraphBuilderTests.cs` | Detects each dependency type from FR-3.2 (inheritance, field type, object creation, etc.) — one test per dependency kind | FR-3.2 |
| 9.3 | `TransitiveFanInAnalyzerTests.cs` | Direct fan-in; transitive fan-in (2+ levels deep); circular dependency handling; no fan-in; target excluded from results | FR-3.3, FR-3.4 |
| 9.4 | `MarkdownReportGeneratorTests.cs` | Output contains expected headers, table rows, and metrics | FR-4 |
| 9.5 | `EndToEndTests.cs` | Run full pipeline against sample codebase snippets, assert expected fan-in set | VR-2.2 |

### Phase 10: Integration & Validation

**Goal**: Run the tool on the sample codebase and validate results.

| Step | Action | Covers |
|------|--------|--------|
| 10.1 | Run the tool: `dotnet run -- --target SampleApp.Core.OrderService --files samples/SampleCodebase/filelist.txt --output samples/SampleCodebase/report.md` | — |
| 10.2 | Compare report output against expected results documented in `README.md` | VR-1.3 |
| 10.3 | Run all unit tests: `dotnet test` — ensure all pass | VR-2 |
| 10.4 | Cross-LLM validation: provide the sample source code and target class to a different LLM, ask it to independently determine the transitive fan-in, compare results | VR-3 |

## Implementation Order & Dependencies

```
Phase 1 (Scaffolding)
  │
  ├── Phase 2 (Models) ─────────────────────┐
  │     │                                    │
  │     ├── Phase 3 (Workspace Builder)      │
  │     │     │                              │
  │     │     └── Phase 4 (Graph Builder)    │
  │     │           │                        │
  │     │           └── Phase 5 (Fan-In)     │
  │     │                 │                  │
  │     └── Phase 6 (Report Generator) ──────┤
  │                                          │
  │           Phase 7 (CLI) ◄────────────────┘
  │
  ├── Phase 8 (Sample Codebase) ── can start after Phase 2
  │
  ├── Phase 9 (Unit Tests) ── one test file per phase 3-6, after each phase
  │
  └── Phase 10 (Integration) ── after all above
```

## Risk & Mitigation

| Risk | Mitigation |
|------|-----------|
| Roslyn semantic model missing BCL references → unresolved types | Include core BCL metadata references (`System.Runtime.dll`, etc.) when creating the compilation. Test early with a minimal file. |
| Generic type unwrapping misses nested generics | Add specific tests for `Dictionary<string, List<OrderService>>` and similar. |
| Circular dependencies cause infinite loop in transitive closure | BFS with visited-set prevents revisiting nodes. Add explicit test case. |
| Extension methods hard to detect as dependency | Use `SemanticModel.GetSymbolInfo` on invocation expressions to resolve the actual method and its `this` parameter type. |
| Large codebases slow down analysis | Roslyn compilation is inherently incremental; no unnecessary re-parsing. Profile if performance issues arise. |
