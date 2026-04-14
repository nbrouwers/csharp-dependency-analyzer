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
│       ├── Program.cs                   # Entry point; registers 'analyze' and 'export' subcommands
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

**Goal**: Wire everything together via a subcommand-based command-line interface that clearly separates the two high-level use cases.

#### CLI shape

```
DependencyAnalyzer <command> [options]

Commands:
  analyze   Compute dependency analysis for a target class (fan-in, and future fan-out / combined)
  export    Export the full dependency graph to an external format (currently: Doxygen XML for Neo4j)

DependencyAnalyzer analyze --target <FQN> --files <path> [--output <path>] [--mode <fan-in>]
DependencyAnalyzer export  --files <path> --format doxygen --output-dir <dir>
```

#### `analyze` subcommand

Answers: *"What depends on this class, and what does it depend on?"* Produces a structured report.

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--target` | Yes | — | Fully qualified name of the target class |
| `--files` | Yes | — | Path to a text file with one source file path per line |
| `--output` | No | `dependency-report.md` | Path for the generated Markdown report |
| `--mode` | No | `fan-in` | Analysis mode: `fan-in` (implemented); `fan-out`, `combined` reserved for future use |

#### `export` subcommand

Answers: *"Give me the full dependency graph in a format consumable by downstream tooling."* No analysis, no target class — operates on the complete graph.

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--files` | Yes | — | Path to a text file with one source file path per line |
| `--format` | Yes | — | Export format; currently only `doxygen` is supported |
| `--output-dir` | Yes | — | Directory to write output files into |

#### Implementation steps

| Step | Action | Covers |
|------|--------|--------|
| 7.1 | Restructure `Program.cs` to use `System.CommandLine` subcommands: register `analyze` and `export` as `Command` objects on the `RootCommand` | §5.1 |
| 7.2 | `analyze` handler: read file list → build compilation → validate target → build graph → run fan-in analyzer → generate Markdown report | All FR |
| 7.3 | `export` handler: read file list → build compilation → build graph → delegate to the format-specific exporter (Phase 12) | — |
| 7.4 | Shared file-list reading extracted to a private helper so both handlers reuse the same logic | FR-2.1 |
| 7.5 | Log progress to console in each handler; `export` logs at `[Doxygen]`-prefixed steps | NFR-3.2 |
| 7.6 | Return exit code 0 on success, 1 on user error, 2 on unexpected error — same contract for both subcommands | NFR-3.1 |

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

---

## Doxygen XML Export — Phases 11–14

This feature extends the tool with a second output format: Doxygen-compatible XML. The XML is consumed by downstream tooling that imports dependency graphs into Neo4j.

### Motivation

The internal `DependencyGraph` holds the full set of in-scope types and all directed edges. Exporting it as Doxygen XML lets existing Neo4j importers (which already understand Doxygen's compound/member schema) ingest C# dependency data without modification.

### Scope

- **Input**: `DependencyGraph` (all types and edges produced after Phase 4, before Phase 5).
- **Output**: A directory containing one XML file per type compound plus `index.xml`.
- **Conformance target**: Doxygen `compound.xsd` (the schema shipped with Doxygen 1.9+).
- **No new NuGet packages**: `System.Xml.Linq` is part of the .NET 8 BCL.
- **Existing behaviour unchanged**: all previous phases, tests, and CLI options remain unmodified.

---

### Phase 11: Model Helpers

**Goal**: Provide deterministic, schema-conformant identifier generation and edge classification without changing any existing model types.

| Step | Action | Detail |
|------|--------|--------|
| 11.1 | Create `Models/DoxygenEdgeKind.cs` — enum with values `Inheritance`, `InterfaceImplementation`, `Usage` | Used to classify a `TypeDependency.DependencyReason` string into a Doxygen structural category |
| 11.2 | Create `Models/DoxygenRefIdHelper.cs` — static class with `ToRefId(string fqn, ElementKind kind) : string` | Converts `Acme.Core.OrderService` + `Class` → `classAcme_1_1Core_1_1OrderService` using the standard Doxygen convention: lowercase kind prefix + FQN with `.` and `::` replaced by `_1_1` |
| 11.3 | Add `ClassifyEdge(string dependencyReason) : DoxygenEdgeKind` to `DoxygenRefIdHelper` | Returns `Inheritance` when reason is `"Inherits from"`, `InterfaceImplementation` when `"Implements interface"`, `Usage` for everything else. Maps every `DependencyVisitor` reason string to a structural Doxygen category |
| 11.4 | Add `ToDoxygenKind(ElementKind kind) : string` to `DoxygenRefIdHelper` | Returns the Doxygen `kind` attribute string: `class` for Class/Record/Delegate; `interface` for Interface; `struct` for Struct; `enum` for Enum |
| 11.5 | Add `ToCompoundName(string fqn) : string` to `DoxygenRefIdHelper` | Replaces `.` with `::` for the `<compoundname>` element (Doxygen convention for C++ namespaces, adopted for C# export) |
| 11.6 | Add `ExtractNamespaces(IEnumerable<string> fqns) : IEnumerable<string>` to `DoxygenRefIdHelper` | Returns all unique namespace prefixes (every dot-delimited prefix of each FQN) for namespace compound generation |

**Dependency reason strings currently used by `DependencyVisitor`** (for reference):

| Reason string | `DoxygenEdgeKind` |
|---|---|
| `"Inherits from"` | `Inheritance` |
| `"Implements interface"` | `InterfaceImplementation` |
| Everything else (Field type, Method parameter type, Object creation, etc.) | `Usage` |

---

### Phase 12: `DoxygenXmlExporter`

**Goal**: Produce a directory of schema-conformant XML files from a `DependencyGraph`.

**File**: `src/DependencyAnalyzer/Reporting/DoxygenXmlExporter.cs`

**Public API**:

```csharp
public sealed class DoxygenXmlExporter
{
    public void Export(DependencyGraph graph, string outputDirectory);
}
```

| Step | Action | Detail |
|------|--------|--------|
| 12.1 | Create output directory if absent | `Directory.CreateDirectory(outputDirectory)` |
| 12.2 | Generate `index.xml` | One `<compound refid="..." kind="..."><name>...</name></compound>` entry per type in `DependencyGraph.ElementKinds` plus one entry per unique namespace. Root element: `<doxygenindex version="1.9.1" xml:lang="en-US">` |
| 12.3 | For each type in `DependencyGraph.ElementKinds`, generate `{refid}.xml` | Root: `<doxygen><compounddef id="{refid}" kind="{doxygenKind}" language="C#"><compoundname>{compoundName}</compoundname>…</compounddef></doxygen>` |
| 12.4 | Emit inheritance edges as `<basecompoundref>` | For every outbound edge from type T where `DoxygenEdgeKind == Inheritance`: `<basecompoundref refid="{targetRefId}" prot="public" virt="non-virtual">{targetCompoundName}</basecompoundref>` appended to the source compound's `<compounddef>` |
| 12.5 | Emit interface-implementation edges as `<basecompoundref>` | Same as 12.4 but `virt="virtual"` (Doxygen convention for interface implementation) |
| 12.6 | Emit all `Usage` edges as a single `<sectiondef kind="public-attrib">` containing one `<memberdef kind="variable">` per distinct `DependencyReason` group | Each memberdef: `id="{sourceRefId}_1dep_{index}"`, `<name>{DependencyReason}</name>`, `<references refid="{targetRefId}" compoundref="{targetRefId}">{targetCompoundName}</references>`. Group edges by reason to minimise memberdef count per type |
| 12.7 | Emit stub `<location file="" line="0" column="0"/>` on every `<compounddef>` | Source location data is not stored in the graph; stub satisfies the XSD requirement |
| 12.8 | Generate a `<compounddef kind="namespace">` file for each unique namespace prefix | `id="namespace{encoded}"`, `<compoundname>{namespace::name}</compoundname>`, with `<innerclass>` elements listing the types that belong to it |
| 12.9 | Write each XML document with `XDocument.Save(path, SaveOptions.None)` with UTF-8 encoding and XML declaration | Ensures well-formed output; `System.Xml.Linq` is used throughout, no third-party serializer needed |

**Output directory structure**:

```
{outputDirectory}/
  index.xml
  classAcme_1_1Core_1_1OrderService.xml
  classAcme_1_1Orders_1_1OrderValidator.xml
  namespaceAcme_1_1Core.xml
  namespaceAcme_1_1Orders.xml
  ...
```

**Concrete compound XML template**:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<doxygen version="1.9.1" xml:lang="en-US">
  <compounddef id="classAcme_1_1Orders_1_1OrderValidator" kind="class" language="C#">
    <compoundname>Acme::Orders::OrderValidator</compoundname>

    <!-- Inheritance/interface edges (DoxygenEdgeKind.Inheritance / .InterfaceImplementation) -->
    <basecompoundref refid="classAcme_1_1Core_1_1OrderService"
                     prot="public" virt="non-virtual">
      Acme::Core::OrderService
    </basecompoundref>

    <!-- Usage edges grouped by reason (DoxygenEdgeKind.Usage) -->
    <sectiondef kind="public-attrib">
      <memberdef kind="variable" id="classAcme_1_1Orders_1_1OrderValidator_1dep_0">
        <name>Field type</name>
        <references refid="classAcme_1_1Core_1_1OrderService"
                    compoundref="classAcme_1_1Core_1_1OrderService">
          Acme::Core::OrderService
        </references>
      </memberdef>
    </sectiondef>

    <location file="" line="0" column="0"/>
  </compounddef>
</doxygen>
```

---

### Phase 13: CLI Integration

**Goal**: Register the `export` subcommand (defined in Phase 7) and wire it to `DoxygenXmlExporter` (Phase 12). The `analyze` subcommand already exists from Phase 7 — this phase extends it with the `--mode` option guard so future modes (`fan-out`, `combined`) can be added without touching the subcommand structure.

| Step | Action | Detail |
|------|--------|--------|
| 13.1 | Register the `export` subcommand in `Program.cs` with options `--files`, `--format`, `--output-dir` | `--format` is an `Option<string>` with allowed values validated against `["doxygen"]`; unknown values produce a user-friendly error and exit code 1 |
| 13.2 | `export` handler: read source files → `RoslynWorkspaceBuilder.BuildCompilation()` → `DependencyGraphBuilder.Build()` → `new DoxygenXmlExporter().Export(graph, outputDir)` | No fan-in analysis is performed; the full graph is exported as-is |
| 13.3 | Log export progress | `[1/2] Building Roslyn compilation...` → `[2/2] Exporting to Doxygen XML...` → `Wrote N file(s) to: <dir>` |
| 13.4 | Add `--mode` option to the `analyze` subcommand | `Option<string>` with default `"fan-in"` and description `"Analysis mode: fan-in (default). Reserved: fan-out, combined."`; validate and reject unknown values with exit code 1; only `fan-in` is dispatched to `TransitiveFanInAnalyzer` at this time |
| 13.5 | Ensure `--version` and `--help` work at both the root and subcommand level | `System.CommandLine` handles this automatically when subcommands are registered correctly |

**CLI invocation examples**:

```bash
# Fan-in analysis → Markdown report
DependencyAnalyzer analyze \
  --target "Acme.Core.OrderService" \
  --files  filelist.txt \
  --output report.md

# Export full graph → Doxygen XML (for Neo4j ingestion)
DependencyAnalyzer export \
  --files      filelist.txt \
  --format     doxygen \
  --output-dir ./doxygen-xml/

# Future (not yet implemented — reserved by --mode guard)
DependencyAnalyzer analyze \
  --target "Acme.Core.OrderService" \
  --files  filelist.txt \
  --mode   fan-out
```

---

### Phase 14: Tests

**Goal**: Verify correctness and XSD conformance of the Doxygen XML output.

**New test file**: `tests/DependencyAnalyzer.Tests/DoxygenXmlExporterTests.cs`

| ID | Test Method | Description | Level |
|----|------------|-------------|-------|
| DX-01 | `Export_CreatesOutputDirectory` | Directory is created when it does not exist | Unit |
| DX-02 | `Export_CreatesIndexXml` | `index.xml` is present after export | Unit |
| DX-03 | `Export_IndexXml_ContainsOneEntryPerType` | `index.xml` has a `<compound>` for every type in the graph | Unit |
| DX-04 | `Export_CreatesOneXmlFilePerType` | One `{refid}.xml` file per type in `ElementKinds` | Unit |
| DX-05 | `ToRefId_Class_UsesClassPrefix` | `classAcme_1_1Core_1_1OrderService` for class kind | Unit |
| DX-06 | `ToRefId_Interface_UsesInterfacePrefix` | `interfaceAcme_1_1Core_1_1IOrderRepository` | Unit |
| DX-07 | `ToRefId_Struct_UsesStructPrefix` | `structAcme_1_1Data_1_1OrderEntity` | Unit |
| DX-08 | `ToRefId_Enum_UsesEnumPrefix` | `enumAcme_1_1Core_1_1OrderStatus` | Unit |
| DX-09 | `ToRefId_Record_UsesClassPrefix` | Records exported as `class` kind refid | Unit |
| DX-10 | `ToRefId_Delegate_UsesClassPrefix` | Delegates exported as `class` kind refid | Unit |
| DX-11 | `CompoundXml_ContainsCompoundName` | `<compoundname>` element matches FQN with `::` separators | Unit |
| DX-12 | `CompoundXml_InheritanceEdge_EmitsBaseCompoundRef` | `"Inherits from"` edge → `<basecompoundref virt="non-virtual">` | Unit |
| DX-13 | `CompoundXml_InterfaceEdge_EmitsBaseCompoundRef_Virtual` | `"Implements interface"` edge → `<basecompoundref virt="virtual">` | Unit |
| DX-14 | `CompoundXml_UsageEdge_EmitsMemberdefWithReferences` | Non-inheritance edge → `<memberdef>` with `<references refid="...">` | Unit |
| DX-15 | `CompoundXml_ContainsLocationStub` | `<location file="" line="0" column="0"/>` present on every compound | Unit |
| DX-16 | `CompoundXml_MultipleEdgesToSameTarget_OneRefPerReason` | Two edges with different reasons produce two memberdefs | Unit |
| DX-17 | `Export_CreatesNamespaceCompounds` | Namespace compound files are generated for all unique namespace prefixes | Unit |
| DX-18 | `Export_EmptyGraph_ProducesOnlyIndexXml` | Empty `DependencyGraph` produces `index.xml` with no entries, no compound files | Unit |
| DX-19 | `ClassifyEdge_InheritsFrom_ReturnsInheritance` | `DoxygenRefIdHelper.ClassifyEdge` returns `Inheritance` | Unit |
| DX-20 | `ClassifyEdge_ImplementsInterface_ReturnsInterfaceImplementation` | Returns `InterfaceImplementation` | Unit |
| DX-21 | `ClassifyEdge_FieldType_ReturnsUsage` | Returns `Usage` | Unit |
| DX-22 | `XmlOutput_IsWellFormed` | All generated files parse without `XmlException` | Unit |
| DX-23 | `IntegrationExport_SampleGraph_AllTypesPresent` | Export of a realistic multi-type graph produces a file for each type, edges intact | Integration |

**XSD validation note**: The test suite will validate XML well-formedness (DX-22) and structural correctness via direct element/attribute assertions (DX-11 through DX-17). Full XSD schema validation can be added as a separate test class `DoxygenXmlSchemaTests.cs` once the XSD file is added to the repository at `docs/compound.xsd`.

---

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

Phase 11 (Model Helpers) ── after Phase 10; no impact on existing code
  │
  └── Phase 12 (DoxygenXmlExporter) ── after Phase 11
        │
        ├── Phase 13 (CLI Integration) ── after Phase 12
        │
        └── Phase 14 (Tests) ── parallel with Phase 12/13
```

## Risk & Mitigation

| Risk | Mitigation |
|------|-----------|
| Roslyn semantic model missing BCL references → unresolved types | Include core BCL metadata references (`System.Runtime.dll`, etc.) when creating the compilation. Test early with a minimal file. |
| Generic type unwrapping misses nested generics | Add specific tests for `Dictionary<string, List<OrderService>>` and similar. |
| Circular dependencies cause infinite loop in transitive closure | BFS with visited-set prevents revisiting nodes. Add explicit test case. |
| Extension methods hard to detect as dependency | Use `SemanticModel.GetSymbolInfo` on invocation expressions to resolve the actual method and its `this` parameter type. |
| Large codebases slow down analysis | Roslyn compilation is inherently incremental; no unnecessary re-parsing. Profile if performance issues arise. |
