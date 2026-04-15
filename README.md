# C# Dependency Analyzer

[![CI](https://github.com/nbrouwers/csharp-dependency-analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/nbrouwers/csharp-dependency-analyzer/actions/workflows/ci.yml)

A Roslyn-based static analysis tool with four primary functions:

**Fan-in analysis** — Given a target class and a set of source files, identifies every type (class, interface, struct, enum, record, delegate) that directly or transitively depends on the target. Produces a Markdown report with justifications and metrics. Useful when planning to move a class to a separate project or assembly: the fan-in set tells you exactly which types must move along with it to avoid breaking compilation.

**Doxygen XML export** — Exports the complete type dependency graph of a codebase to Doxygen-conformant XML files. Each type becomes a compound XML file with structured edges for inheritance, interface implementation, and usage relationships. Ready for downstream tooling that understands the Doxygen compound schema.

**Neo4j direct import** — Connects directly to a running Neo4j database server (via Bolt) and imports the full dependency graph using a schema that mirrors the Doxygen compound model (`(:Compound)` nodes keyed on Doxygen refids, with `:BASECOMPOUNDREF`, `:REFERENCES`, and `:INNERCLASS` relationships) — no intermediate files. Designed for loading C# codebase graphs into Neo4j for further analysis, visualisation, or graph-query-based impact assessment.

**CSV export** — Exports the dependency graph to a pair of CSV files (`nodes.csv` and `relationships.csv`) with stable SHA-256-derived node IDs and Doxygen-compatible type labels. Ready for bulk import into Neo4j via `neo4j-admin import`, graph databases, or spreadsheet tools.

## How It Works

Both subcommands share the same first two steps:

1. **Roslyn compilation** — All listed source files are parsed and compiled in-memory using the Roslyn Compiler Platform. No `.csproj` or `.sln` is needed; the tool works directly on raw `.cs` files.
2. **Dependency graph extraction** — A `CSharpSyntaxWalker` visits every syntax tree and records type-to-type dependency edges (inheritance, field types, method signatures, object creation, casts, pattern matching, attributes, generics, and ~40 other C# constructs).

From there, each subcommand follows its own path:

**`analyze` (fan-in):**

3. **Transitive closure** — A BFS on the reversed adjacency list computes the full set of types that depend on the target, directly or transitively.
4. **Report generation** — Results are written to a Markdown file with a fan-in table, per-kind metrics, and a Mermaid dependency diagram.

**`export --format neo4j` (Neo4j import):**

3. **Graph import** — The graph is imported using a schema that mirrors the Doxygen compound model: each type and namespace becomes a `(:Compound {id, kind, name, fqn, language})` node, MERGE-keyed on the Doxygen refid. Structural edges (inheritance, interface implementation) become `:BASECOMPOUNDREF {prot, virt}` relationships; usage edges become `:REFERENCES {kind: "variable", reason}` relationships; namespace-to-type membership becomes `:INNERCLASS {prot: "public"}`. All writes use `MERGE`, making the import fully idempotent.
4. **Output** — The tool reports the number of nodes and relationships written to the console. No files are produced.

**`export --format doxygen` (Doxygen XML):**

3. **Graph serialisation** — The full dependency graph is serialised to Doxygen-conformant XML: one compound file per type, one compound file per namespace, and an `index.xml` catalogue. Inheritance and interface-implementation edges become `<basecompoundref>` elements; usage edges become `<memberdef>/<references>` elements.
4. **Output** — Files are written to the specified output directory, ready for ingestion into downstream tooling such as Neo4j.

**`export --format csv` (CSV):**

3. **Graph serialisation** — The full dependency graph is serialised to two RFC 4180 CSV files: `nodes.csv` (one row per type, including unresolved references) and `relationships.csv` (one row per dependency edge). Node IDs are stable 16-character hex strings derived from SHA-256(`{fqn}:{typeLabel}`). Source file paths are relativized to the project root.
4. **Output** — Files are written to the specified output directory.

## Prerequisites

**To run the portable executable:** No prerequisites. The published `.exe` is fully self-contained and runs on any Windows 11 (x64) machine without the .NET SDK or runtime installed.

**To build from source:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later. All NuGet dependencies are restored automatically.

## Building

```bash
git clone <this-repo-url>
cd csharp-dependency-analyzer
dotnet build
```

The build automatically targets `win-x64` and produces a self-contained output. The compiled executable will be at:

```
src/DependencyAnalyzer/bin/Debug/net8.0/win-x64/DependencyAnalyzer.exe
```

### Publishing a Portable Executable

To produce a single-file portable `.exe` (~41 MB) that requires **no .NET SDK or runtime** on the target machine:

```bash
dotnet publish src/DependencyAnalyzer -c Release
```

The output is at:

```
src/DependencyAnalyzer/bin/Release/net8.0/win-x64/publish/DependencyAnalyzer.exe
```

This single file can be copied to any Windows 11 (x64) machine and run directly — no installation required. The executable bundles the .NET runtime, Roslyn compiler libraries, and all dependencies into one compressed file.

## Usage

### 1. Create a file list

Create a plain text file listing every `.cs` file you want the analyzer to consider — one path per line. Paths can be absolute or relative to the directory containing the file list. Lines starting with `#` and blank lines are ignored.

Example `filelist.txt`:

```
# Core domain
src/Core/OrderService.cs
src/Core/IOrderRepository.cs
src/Core/OrderValidator.cs

# Services
src/Services/OrderController.cs
src/Services/OrderPipeline.cs
```

> **Tip:** On Linux/macOS, generate a file list with `find`:
> ```bash
> find src -name '*.cs' > filelist.txt
> ```
> On Windows PowerShell:
> ```powershell
> Get-ChildItem -Recurse -Filter *.cs src | ForEach-Object { $_.FullName } | Set-Content -Encoding UTF8 filelist.txt
> ```

### 2. Run the analyzer

The tool exposes two subcommands:

#### `analyze` — fan-in analysis (produces a Markdown report)

```bash
dotnet run --project src/DependencyAnalyzer -- analyze \
  --target "MyCompany.Core.Services.OrderService" \
  --files  filelist.txt \
  --output report.md
```

Or, using the published executable:

```bash
./DependencyAnalyzer analyze \
  --target "MyCompany.Core.Services.OrderService" \
  --files  filelist.txt \
  --output report.md
```

#### `export` — export full dependency graph

**Doxygen XML** (file-based, for tooling that reads Doxygen output):

```bash
./DependencyAnalyzer export \
  --files      filelist.txt \
  --format     doxygen \
  --output-dir ./doxygen-xml/
```

**Neo4j direct import** (connects to a running Neo4j instance; no files produced):

```bash
./DependencyAnalyzer export \
  --files          filelist.txt \
  --format         neo4j \
  --neo4j-uri      bolt://localhost:7687 \
  --neo4j-user     neo4j \
  --neo4j-password secret
```

Or supply the password via environment variable to avoid it appearing in shell history:

```bash
export NEO4J_PASSWORD=secret
./DependencyAnalyzer export --files filelist.txt --format neo4j
```

**CSV export** (produces `nodes.csv` and `relationships.csv`):

```bash
./DependencyAnalyzer export \
  --files      filelist.txt \
  --format     csv \
  --output-dir ./csv-output/
```

### CLI Reference

#### `analyze` options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--target` | Yes | — | Fully qualified name of the target class (e.g. `MyCompany.Core.OrderService`). |
| `--files` | Yes | — | Path to a text file containing one source file path per line. |
| `--output` | No | `dependency-report.md` | Path for the generated Markdown report. |
| `--mode` | No | `fan-in` | Analysis direction. Currently only `fan-in` is implemented. |

#### `export` options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--files` | Yes | — | Path to a text file containing one source file path per line. |
| `--format` | Yes | — | Export format: `doxygen`, `neo4j`, or `csv`. |
| `--output-dir` | When `doxygen` or `csv` | — | Directory to write output files into (created if absent). |
| `--neo4j-uri` | No | `bolt://localhost:7687` | Bolt URI of the Neo4j server. |
| `--neo4j-user` | No | `neo4j` | Neo4j username. |
| `--neo4j-password` | When `neo4j` | `NEO4J_PASSWORD` env var | Neo4j password. Not logged or persisted. |
| `--neo4j-database` | No | `neo4j` | Target Neo4j database name. |

### 3. Read the report

The output is a Markdown file containing:

- A **fan-in table** listing every type that depends (directly or transitively) on the target, with its kind and a human-readable justification chain.
- A **metrics summary** with counts per element kind and the total.
- A **dependency graph** rendered as a [Mermaid](https://mermaid.js.org/) diagram showing the target node (highlighted in orange), all fan-in elements (color-coded by kind), and directed edges representing dependency relationships. GitHub renders Mermaid diagrams natively in Markdown files.

Example output:

```markdown
# Dependency Analysis Report

## Target: MyCompany.Core.OrderService

Generated: 2026-04-14 10:30:00 UTC

## Fan-In Elements

| # | Fully Qualified Name | Kind | Justification |
|---|----------------------|------|---------------|
| 1 | MyCompany.Core.OrderValidator | Class | Directly references target via field type |
| 2 | MyCompany.Services.OrderPipeline | Class | Transitively depends via MyCompany.Core.OrderValidator |

## Metrics

| Kind | Count |
|------|-------|
| Class | 2 |
| **Total** | **2** |
| **Max Transitive Depth** | **2** |
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | User error (target class not found, file list missing, invalid input) |
| 2 | Unexpected internal error |

## Doxygen XML Schema Mapping

The `export` subcommand serialises the internal `DependencyGraph` model to Doxygen-conformant XML (compound schema version 1.9.1). The table below documents the complete mapping.

### File layout

| What is generated | Filename |
|---|---|
| One compound file per discovered type | `{refid}.xml` |
| One compound file per unique namespace prefix | `namespace{encoded-ns}.xml` |
| Master catalogue of all compounds | `index.xml` |

### `ElementKind` → Doxygen `kind` attribute

| Internal `ElementKind` | `<compounddef kind="…">` |
|---|---|
| `Class`, `Record`, `Delegate` | `class` |
| `Interface` | `interface` |
| `Struct` | `struct` |
| `Enum` | `enum` |
| *(namespace compounds)* | `namespace` |

### Fully qualified name → `refid`

Each `refid` is `{kindPrefix}{encodedFqn}`, where the kind prefix matches the table above (`class`, `interface`, `struct`, `enum`, `namespace`) and the FQN is encoded as follows:

| Character sequence | Encoded form |
|---|---|
| `.` or `::` | `_1_1` |
| `<` | `_3_0` |
| `>` | `_3_1` |
| `,` | `_00` |
| ` ` (space) | `_01` |

Example: `SampleApp.Core.IRepository<T>` + `Interface` → `interfaceSampleApp_1_1Core_1_1IRepository_3_0T_3_1`

### Dependency edges → XML elements

Each `TypeDependency` (edge) carries a `DependencyReason` string. The exporter classifies it into one of three `DoxygenEdgeKind` values and maps it to the corresponding XML structure:

| `DependencyReason` | `DoxygenEdgeKind` | XML output |
|---|---|---|
| `"Inherits from"` | `Inheritance` | `<basecompoundref prot="public" virt="non-virtual" refid="…">` |
| `"Implements interface"` | `InterfaceImplementation` | `<basecompoundref prot="public" virt="virtual" refid="…">` |
| Any other reason | `Usage` | `<sectiondef kind="public-attrib">` containing a `<memberdef kind="variable">` whose `<name>` is the reason string, with a `<references refid="…">` child pointing to the target type |

### Namespace membership

Each namespace compound lists the types it contains as `<innerclass refid="…" prot="public">` child elements inside its `<compounddef>`.

### Location stub

Because `DependencyGraph` does not store source-file positions, every `<compounddef>` includes a stub `<location file="" line="0" column="0"/>` to satisfy the schema.

### Index file

`index.xml` is a `<doxygenindex version="1.9.1">` document containing one `<compound refid="…" kind="…"><name>…</name></compound>` entry per type and per namespace compound.

## Neo4j Graph Schema

The `export --format neo4j` subcommand imports the internal `DependencyGraph` model into Neo4j using a schema that deliberately mirrors the Doxygen compound model (compound.xsd version 1.9.1). This makes the two export formats semantically equivalent: anything that can be expressed in the Doxygen XML can be queried in the Neo4j graph with the same concepts and terminology.

### Node label and properties

All nodes carry the label **`:Compound`**.

| Property | Type | Description | Doxygen equivalent |
|----------|------|-------------|-------------------|
| `id` | string | Doxygen refid — unique and deterministic; used as the `MERGE` key. Example: `classAcme_1_1Core_1_1OrderService` | `compounddef/@id` |
| `kind` | string | Doxygen kind string: `class`, `interface`, `struct`, `enum`, `namespace` | `compounddef/@kind` |
| `name` | string | Compound name using `::` as namespace separator. Example: `Acme::Core::OrderService` | `<compoundname>` |
| `fqn` | string | Original .NET fully qualified name. Example: `Acme.Core.OrderService` | — (extension) |
| `language` | string | Always `"C#"` | `compounddef/@language` |

> **`ElementKind` → `kind` mapping** — `Class`, `Record`, and `Delegate` all map to `"class"` (matching Doxygen); `Interface` → `"interface"`, `Struct` → `"struct"`, `Enum` → `"enum"`.

> **Namespace nodes** — Every unique namespace prefix found within the in-scope type FQNs is also written as a `:Compound {kind: "namespace"}` node, mirroring the separate namespace compound files produced by the Doxygen export.

### Relationships

| Relationship | Properties | When written | Doxygen equivalent |
|---|---|---|---|
| `-[:BASECOMPOUNDREF {prot, virt}]->` | `prot: "public"`, `virt: "non-virtual"` | Inheritance edge | `<basecompoundref prot="public" virt="non-virtual">` |
| `-[:BASECOMPOUNDREF {prot, virt}]->` | `prot: "public"`, `virt: "virtual"` | Interface-implementation edge | `<basecompoundref prot="public" virt="virtual">` |
| `-[:REFERENCES {kind, reason}]->` | `kind: "variable"`, `reason: "…"` | All other usage edges (field type, method param, …) | `<memberdef kind="variable"><references>` |
| `-[:INNERCLASS {prot}]->` | `prot: "public"` | Namespace compound → each type it directly contains | `<innerclass prot="public">` |

### Example Cypher queries

```cypher
// Find all types in a namespace
MATCH (ns:Compound {kind: "namespace", name: "Acme::Core"})-[:INNERCLASS]->(t)
RETURN t.name, t.kind

// Find all types that inherit from or implement a given type
MATCH (child)-[:BASECOMPOUNDREF]->(parent:Compound {name: "Acme::Core::OrderService"})
RETURN child.name, child.kind

// Find all usage dependencies of a type
MATCH (src:Compound {name: "Acme::Core::OrderService"})-[r:REFERENCES]->(tgt)
RETURN tgt.name, r.reason

// Transitive fan-in: all types that directly or indirectly depend on a target
MATCH p = (dependent)-[:BASECOMPOUNDREF|REFERENCES*1..]->(target:Compound {name: "Acme::Core::OrderService"})
RETURN DISTINCT dependent.name, dependent.kind
```

## Using on a Different Codebase

To analyze a codebase in a completely separate repository or environment:

### Option A: Portable Executable (recommended)

No .NET SDK needed on the target machine.

1. **Copy** `DependencyAnalyzer.exe` (from the `publish/` folder) to the target machine.

2. **Generate a file list** pointing to the `.cs` files you want to analyze:
   ```powershell
   Get-ChildItem -Recurse -Filter *.cs C:\path\to\other-repo\src | ForEach-Object { $_.FullName } | Set-Content -Encoding UTF8 filelist.txt
   ```

3. **Run the fan-in analyzer:**
   ```powershell
   .\DependencyAnalyzer.exe analyze --target "OtherRepo.Domain.SomeService" --files filelist.txt --output report.md
   ```

4. **Read** `report.md` — it contains the full transitive fan-in set.

   Or, **export the full dependency graph to Doxygen XML:**
   ```powershell
   .\DependencyAnalyzer.exe export --files filelist.txt --format doxygen --output-dir .\doxygen-xml
   ```

### Option B: From Source

Requires .NET 8.0 SDK.

1. **Clone or copy this repository** to the machine:
   ```bash
   git clone <this-repo-url>
   cd csharp-dependency-analyzer
   dotnet build
   ```

2. **Generate a file list** pointing to the `.cs` files you want to analyze. The paths in the file list are resolved relative to the directory containing the file list itself, so you can place it anywhere:
   ```bash
   # Example: analyze everything under another repo's src/ folder
   find /path/to/other-repo/src -name '*.cs' > /path/to/other-repo/filelist.txt
   ```

3. **Run the fan-in analyzer** with the fully qualified name of the class you care about:
   ```bash
   dotnet run --project src/DependencyAnalyzer -- analyze \
     --target "OtherRepo.Domain.SomeService" \
     --files  /path/to/other-repo/filelist.txt \
     --output /path/to/other-repo/fan-in-report.md
   ```

4. **Read** `fan-in-report.md` — it contains the full transitive fan-in set.

   Or, **export the full dependency graph to Doxygen XML:**
   ```bash
   dotnet run --project src/DependencyAnalyzer -- export \
     --files      /path/to/other-repo/filelist.txt \
     --format     doxygen \
     --output-dir /path/to/other-repo/doxygen-xml
   ```

### Important Notes for External Codebases

- **No `.csproj` or `.sln` required.** The tool compiles raw `.cs` files in isolation using Roslyn. It does not invoke `dotnet build` on the target codebase.
- **NuGet package types are not resolved.** The compilation references only the .NET BCL (Base Class Library) assemblies from the SDK. Types coming from third-party NuGet packages will not be in the semantic model. This means:
  - Dependencies *between your own source files* are fully tracked.
  - Dependencies on types defined in NuGet packages (e.g. `Microsoft.EntityFrameworkCore.DbContext`) are not tracked, by design — the tool focuses on in-scope source code.
- **Include all relevant files.** If your codebase spans multiple projects, include `.cs` files from all projects you want analyzed in the file list. Only files listed will be considered.
- **Generated files.** If your build produces generated `.cs` files (source generators, T4, etc.), you may want to include those in the file list as well. Run `dotnet build` on the target codebase first to produce them, then include the generated files from the `obj/` directories.
- **Target must exist in scope.** The `--target` FQN must resolve to exactly one type definition within the listed source files. If the class is not found, the tool exits with code 1 and an error message.

## Running the Sample

A self-contained sample codebase is included under `samples/SampleCodebase/` with a pre-built `filelist.txt`.

### Fan-in analysis

```bash
dotnet run --project src/DependencyAnalyzer -- analyze \
  --target "SampleApp.Core.OrderService" \
  --files  samples/SampleCodebase/filelist.txt \
  --output samples/SampleCodebase/report.md
```

This produces a report identifying 11 fan-in elements (7 classes, 1 interface, 1 struct, 1 record, 1 delegate). See `samples/SampleCodebase/README.md` for the expected results.

### Doxygen XML export

```bash
dotnet run --project src/DependencyAnalyzer -- export \
  --files      samples/SampleCodebase/filelist.txt \
  --format     doxygen \
  --output-dir samples/SampleCodebase/doxygen-xml
```

This discovers 17 types and 24 dependency edges, writing 24 XML files to `samples/SampleCodebase/doxygen-xml/`. The committed output is already present in that directory and can be used as a reference.

### Neo4j direct import

With a local Neo4j instance running (default Bolt URI `bolt://localhost:7687`):

```bash
dotnet run --project src/DependencyAnalyzer -- export \
  --files          samples/SampleCodebase/filelist.txt \
  --format         neo4j \
  --neo4j-password secret
```

This connects to Neo4j, discovers 17 types and 24 dependency edges, then writes 17 `(:Type)` nodes and 24 relationships. The password can also be set via `NEO4J_PASSWORD` to avoid it appearing in shell history.

## Versioning

This project follows [Semantic Versioning 2.0.0](https://semver.org/). The version is defined in `src/DependencyAnalyzer/DependencyAnalyzer.csproj` and embedded into the assembly at build time.

- **CLI**: `DependencyAnalyzer --version` prints the current version.
- **Reports**: Each generated Markdown report includes the tool version in its header.
- **Releases**: See [`.github/prompts/new-release.prompt.md`](.github/prompts/new-release.prompt.md) for the release procedure.

## Running Tests

```bash
dotnet test
```

The test suite contains 208 tests covering:

- Roslyn workspace building and target resolution
- Individual dependency type detection (inheritance, fields, generics, patterns, etc.)
- Transitive fan-in computation (direct, transitive, circular, diamond dependencies)
- Markdown report generation
- Doxygen XML export (`DoxygenXmlExporter`, `DoxygenRefIdHelper`, edge classification)
- Neo4j direct import (`Neo4jExporter` — node parameters, relationship types, edge collection without live server)
- End-to-end pipeline scenarios
- Comprehensive C# construct coverage verified across 4 rounds of cross-checking against the language specification
- Portable executable build verification (single-file output, help, sample analysis)
- CI workflow structure validation
- Test report document structure validation

For a detailed catalog of every test case with traceability to requirements, see [docs/test-report.md](docs/test-report.md).

## Project Structure

```
csharp-dependency-analyzer/
├── .github/
│   ├── prompts/
│   │   ├── new-feature.prompt.md           # New feature procedure (Copilot prompt)
│   │   └── new-release.prompt.md           # Release procedure (Copilot prompt)
│   └── workflows/
│       └── ci.yml                          # GitHub Actions CI pipeline
├── CSharpDependencyAnalyzer.sln
├── nuget.config
├── docs/
│   ├── requirements.md
│   ├── implementation-plan.md
│   └── test-report.md                      # Test inventory and traceability matrix
├── src/DependencyAnalyzer/
│   ├── DependencyAnalyzer.csproj
│   ├── Program.cs                          # CLI entry point
│   ├── Models/
│   │   ├── ElementKind.cs                  # Enum: Class, Interface, Struct, ...
│   │   ├── TypeDependency.cs               # Edge record (source, target, reason)
│   │   ├── FanInElement.cs                 # Result item with justification
│   │   ├── AnalysisResult.cs               # Full analysis output
│   │   ├── DependencyGraph.cs              # Adjacency list + element kinds
│   │   ├── DoxygenEdgeKind.cs              # Classifies edges for Doxygen export
│   │   └── DoxygenRefIdHelper.cs           # refid generation, kind/name conversion
│   ├── Analysis/
│   │   ├── RoslynWorkspaceBuilder.cs       # Builds CSharpCompilation from files
│   │   ├── DependencyGraphBuilder.cs       # Discovers types, runs visitor
│   │   ├── DependencyVisitor.cs            # CSharpSyntaxWalker (~45 overrides)
│   │   └── TransitiveFanInAnalyzer.cs      # BFS transitive closure
│   └── Reporting/
│       ├── MarkdownReportGenerator.cs      # Markdown report output
│       ├── DoxygenXmlExporter.cs           # Doxygen XML export
│       └── Neo4jExporter.cs                # Neo4j direct import (Bolt protocol)
├── tests/DependencyAnalyzer.Tests/
│   ├── TestHelper.cs
│   ├── RoslynWorkspaceBuilderTests.cs
│   ├── DependencyGraphBuilderTests.cs
│   ├── TransitiveFanInAnalyzerTests.cs
│   ├── MarkdownReportGeneratorTests.cs
│   ├── EndToEndTests.cs
│   ├── ComprehensiveDependencyTests.cs
│   ├── GapProbeTests.cs
│   ├── Round3AuditProbeTests.cs
│   ├── Round4FinalSweepTests.cs
│   ├── DoxygenXmlExporterTests.cs          # Tests for Doxygen export (DX-01 – DX-23)
│   ├── Neo4jExporterTests.cs               # Tests for Neo4j import (NJ-01 – NJ-25)
│   ├── PortableExeTests.cs
│   ├── CiWorkflowTests.cs
│   ├── VersionTests.cs
│   └── TestReportDocTests.cs
└── samples/SampleCodebase/
    ├── filelist.txt
    ├── README.md                           # Expected results
    └── *.cs                                # 16 sample source files
```

## Supported C# Constructs

The dependency visitor detects type references across all mainstream C# constructs, including but not limited to:

- Inheritance and interface implementation
- Field, property, event, indexer, and operator types
- Method parameters, return types, and local variables
- Generic type arguments and constraints
- Object creation (`new T()` and target-typed `new()`)
- Array, pointer, and ref type unwrapping
- Casts, `is`/`as` expressions, and full pattern matching (declaration, type, recursive, constant, list, positional)
- `typeof`, `sizeof`, `default`, and `nameof` expressions
- Attributes (on types, methods, parameters, return values, properties, assembly)
- Static member access and `using static` bare references
- Primary constructors (C# 12), record constructors, and partial methods
- LINQ `from`/`join` clauses with explicit types
- Lambda and local function parameters
- Extension method `this` parameters
- Delegate signatures and function pointer types
- Async return types, yield iterators, expression-bodied members
- Catch clauses, foreach, for, using, and fixed statements
- Reflection API calls: `Type.GetType("FQN")`, `Assembly.GetType("FQN")`, and `Module.GetType("FQN")` with string literal arguments

## Reflection Dependency Detection

The .NET reflection API (`Type.GetType`, `Assembly.GetType`, `Activator.CreateInstance`, etc.) hides type usage from the compiler — these calls carry type names as runtime strings and are therefore invisible to Roslyn's type system. The analyzer uses **Strategy 1 (static string-literal scanning)** to recover a subset of these hidden dependencies.

### What is detected

When a string literal whose value exactly matches an in-scope fully qualified type name is passed as the first argument to `Type.GetType(...)`, `Assembly.GetType(...)`, or `Module.GetType(...)`, the analyzer emits a dependency edge with an appropriate reason string.

```csharp
Type.GetType("Acme.Core.OrderService")     // → edge: Consumer —[Reflection: Type.GetType string literal]-→ OrderService
assembly.GetType("Acme.Core.OrderService") // → edge: Consumer —[Reflection: Assembly.GetType string literal]-→ OrderService
module.GetType("Acme.Core.OrderService")   // → edge: Consumer —[Reflection: Module.GetType string literal]-→ OrderService
```

Calls where `typeof(T)` is already used (the dominant DI pattern) are covered by the existing `typeof` visitor and do not need this feature.

### What is not detected

| Pattern | Why not detected |
|---------|------------------|
| `Type.GetType(typeName)` — variable | String value not known at compile time |
| `Type.GetType($"Acme.{name}")` — interpolated | Composite expression, not a literal |
| `Activator.CreateInstance(type)` — computed `Type` | No string literal to inspect |
| Convention-based scanning (`services.Scan(...)`) | No string literal; resolved at runtime |

### Strategy comparison

Four strategies were evaluated when deciding how to add reflection support:

| # | Strategy | Coverage | Cost | Chosen |
|---|----------|----------|------|--------|
| 1 | **Static string-literal scanning** — detect `Type.GetType("literal")` using Roslyn's `InvocationExpressionSyntax` | ~80 % of hand-written reflection in typical codebases | ~50 lines; no new dependencies | **✔ Yes** |
| 2 | `typeof(T)` and DI generic registrations | Already fully covered by existing `typeof` and generic-type-argument visitors | Zero additional cost | ✔ Already done |
| 3 | Source-generator outputs | Already covered when generated `.cs` files are included in the file list | Zero additional cost | ✔ Already done |
| 4 | **Runtime tracing** — instrument BCL `Type.GetType` / `Activator` via EventSource during a test run, feed resolved names back as synthetic edges | 100 % of runtime reflection | Requires a separate `trace` subcommand, a runnable application, and post-processing | ✘ Not implemented |

Strategy 1 was chosen because it covers the dominant pattern at negligible cost, with no runtime dependency. Strategy 4 remains the natural next milestone if complete reflection coverage is required.

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | 4.12.0 | Roslyn C# compiler APIs |
| [Microsoft.CodeAnalysis.CSharp.Workspaces](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Workspaces) | 4.12.0 | Roslyn workspace support |
| [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) | 2.0.0-beta4 | CLI argument parsing |
| [xUnit](https://www.nuget.org/packages/xunit) | 2.5.3 | Test framework (test project only) |

## CI / CD

A GitHub Actions workflow runs automatically on every push and pull request:

1. **Test** — Restores, builds, and runs the full test suite on `windows-latest`.
2. **Publish** — If tests pass, publishes the self-contained single-file executable.
3. **Artifact** — Uploads `DependencyAnalyzer.exe` as a downloadable build artifact (retained for 90 days).

To download the latest artifact: go to the [Actions tab](https://github.com/nbrouwers/csharp-dependency-analyzer/actions), select the latest successful run, and download **DependencyAnalyzer-win-x64** from the Artifacts section.

## License

This project is provided as-is for internal use.
