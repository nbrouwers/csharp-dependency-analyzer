# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

---

## [4.0.0] — 2026-04-15

### Changed (breaking)
- **Neo4j export schema — breaking change**: `export --format neo4j` now uses an entirely different graph schema, incompatible with graphs produced by earlier versions.
  - Node label changed from `:Compound` to type labels (`:class`, `:interface`, `:struct`, `:enum`).
  - Node `id` property changed from a Doxygen refid string to a stable 16-character SHA-256-derived hex string matching the CSV export `id:ID` column.
  - Node properties changed from `{id, kind, name, fqn, language}` to `{id, name, type, label, file, startLine, endLine, accessibility, fullyqualifiedname, resolved}` — identical to CSV export columns.
  - Relationship types changed from `:BASECOMPOUNDREF {prot, virt}` / `:REFERENCES {kind, reason}` to `:basecompoundref {startLine, endLine}` / `:ref {startLine, endLine}` — identical to CSV export relationship types.
  - Namespace compound nodes (`:Compound {kind: "namespace"}`) and `:INNERCLASS` relationships have been removed.
  - Unresolved references are now imported as `resolved = false` nodes instead of being omitted.
- **Database cleared before import**: `export --format neo4j` now executes `MATCH (n) DETACH DELETE n` before writing any data, ensuring the target database contains exactly the exported graph after each run.

### Fixed
- `PortableExeTests` now resolves the `dotnet` executable path from the running test-host process instead of relying on PATH, eliminating `CommandNotFoundException` when dotnet was not in the process-inherited PATH.

### Removed
- `BuildNamespaceParameters`, `GetVirt`, `CollectInnerClassOperations` helpers on `Neo4jExporter`.

---

## [3.2.0] — 2026-04-15

### Added
- **CSV export (`--format csv`)**: `export --format csv --output-dir <dir>` now produces two RFC 4180 CSV files — `nodes.csv` and `relationships.csv` — suitable for bulk import into Neo4j via `neo4j-admin import`, graph analysis, or spreadsheet tooling.
  - `nodes.csv` columns: `id:ID`, `name`, `type`, `:LABEL`, `file`, `startLine:int`, `endLine:int`, `accessibility`, `fullyqualifiedname`, `resolved:boolean`.
  - `relationships.csv` columns: `:START_ID`, `:END_ID`, `:TYPE`, `startLine:int`, `endLine:int`.
  - Node IDs are stable 16-character lowercase hex strings derived from `SHA-256("{fqn}:{typeLabel}")[..16]`.
  - Doxygen-compatible type labels: `class`, `interface`, `struct`, `enum` (records → `class`, delegates → `interface`).
  - Relationship types: `basecompoundref` for inheritance/interface-implementation; `ref` for all other edges.
  - Unresolved references (referenced in code but not found in source, sharing a namespace root with in-scope types) appear as extra nodes with `resolved:boolean=false`.
  - Source file paths are relativized to the project root (directory of the file list), using forward slashes.
  - `startLine`/`endLine` reflect 1-based Roslyn source locations for both node declarations and edge call sites (best-effort; 0 if unavailable).
  - Output files are UTF-8 without BOM.
- **`CsvIdHelper`** (`Models/CsvIdHelper.cs`): static helpers `ToNodeId`, `ToTypeLabel`, `ToRelationshipType` used by `CsvExporter` and testable independently.
- **Source location enrichment**: `DependencyGraph` now stores `TypeLocations` (file path, start/end line, accessibility per FQN) and `EdgeLocations` (call-site start/end line per `TypeDependency` edge). `DependencyGraphBuilder` populates `TypeLocations` from the Roslyn semantic model during Pass 1 and merges `EdgeLocations` and `UnresolvedReferences` from `DependencyVisitor` during Pass 2.
- **Unresolved reference tracking**: `DependencyVisitor` now tracks FQNs that are referenced in code, not found in scope, but share a namespace root with at least one in-scope type. These are surfaced in the graph as `UnresolvedReferences` and written as `resolved:boolean=false` rows in `nodes.csv`.
- **Call-site locations**: All `RecordDependency` call sites in `DependencyVisitor` now pass the relevant `SyntaxNode` as the `site` parameter, enabling edge-level line number capture.
- 28 new tests: `CsvIdHelperTests.cs` (CI-01–CI-05, 10 theory cases) and `CsvExporterTests.cs` (CV-01–CV-23).

---

## [3.1.1] — 2026-04-15

### Fixed
- **Reflection detection in self-contained single-file executable**: `Type.GetType`, `Assembly.GetType`, and `Module.GetType` string-literal reflection edges were silently dropped when running as a published single-file exe. In that mode `TRUSTED_PLATFORM_ASSEMBLIES` contains bundle-internal paths that are not physical files on disk, so `MetadataReference.CreateFromFile` failed silently, leaving Roslyn without BCL metadata. Without BCL metadata the semantic model could not resolve `System.Type`, `System.Reflection.Assembly`, or `System.Reflection.Module`, so the reflection detection visitor path never fired. Fixed by adding a syntactic fallback that detects the same three patterns using receiver-text inspection and the Roslyn error-type model (which retains the declared-type name from source annotations even without full BCL metadata).

---

## [3.1.0] — 2026-04-15

### Added
- **Reflection dependency detection (Strategy 1)**: `Type.GetType("FQN")`, `Assembly.GetType("FQN")`, and `Module.GetType("FQN")` calls whose first argument is a compile-time string literal matching an in-scope fully qualified type name are now detected and emitted as dependency edges. The emitted reason strings are `"Reflection: Type.GetType string literal"`, `"Reflection: Assembly.GetType string literal"`, and `"Reflection: Module.GetType string literal"` respectively. Dynamic strings (variables, interpolated strings) are intentionally not detected — they require runtime tracing and are out of scope for static analysis.
- `OrderTypeRegistry` sample class added to `samples/SampleCodebase/` demonstrating all three reflection detection patterns; fan-in count for `SampleApp.Core.OrderService` increases from 11 to 12.
- 8 new tests (`ReflectionDependencyTests.cs`, RF-01–RF-08) covering: string-literal `Type.GetType`, `Assembly.GetType`, and `Module.GetType` detection; out-of-scope suppression; non-literal suppression; self-loop suppression; and reason string verification.

### Notes on reflection support strategy

Four strategies were evaluated (see README — *Reflection Dependency Detection* section for a full comparison table):

| # | Strategy | Decision |
|---|----------|----------|
| 1 | Static string-literal scanning (`Type.GetType` / `Assembly.GetType` / `Module.GetType`) | **Implemented** — covers ~80 % of hand-written reflection at ~50 lines of code |
| 2 | `typeof(T)` and DI generic registrations | Already covered by existing `typeof` and generic-type-argument visitors |
| 3 | Source-generator outputs | Already covered when generated `.cs` files are included in the file list |
| 4 | Runtime tracing via EventSource / profiler | Not implemented — natural next milestone for complete coverage |

Strategy 1 was selected for its high coverage-to-cost ratio. Strategy 4 remains available as a future `trace` subcommand if 100 % reflection coverage is required.

## [3.0.0] — 2026-04-15

### Changed
- **Breaking change — Neo4j graph schema**: the Neo4j export now uses a schema that mirrors the Doxygen compound model (compound.xsd version 1.9.1). Node label changed from `:Type` to `:Compound`; the MERGE key is now the Doxygen `refid` (`id` property) instead of `fqn`. Node properties changed from `{fqn, name, kind, namespace}` to `{id, kind, name, fqn, language}`, where `kind` is now a lowercase Doxygen kind string (`class`, `interface`, `struct`, `enum`) and `name` uses `::` as namespace separator (mirroring `<compoundname>`). Relationship types changed from `:INHERITS_FROM` / `:IMPLEMENTS` / `:DEPENDS_ON {reason}` to `:BASECOMPOUNDREF {prot, virt}` / `:REFERENCES {kind, reason}` mirroring the Doxygen XML equivalents.
- **Namespace compound nodes**: namespace prefixes found in the in-scope type FQNs are now imported as `:Compound {kind: "namespace"}` nodes, mirroring the Doxygen namespace compound files. Each namespace node is connected to the types it directly contains via `:INNERCLASS {prot: "public"}` relationships, mirroring `<innerclass>` elements in Doxygen XML.
- README updated with a dedicated "Neo4j Graph Schema" section documenting nodes, relationships, and example Cypher queries.

### Removed
- `ExtractShortName`, `ExtractNamespace`, and `GetRelationshipType` internal helpers on `Neo4jExporter` (superseded by `DoxygenRefIdHelper`).

## [2.1.0] — 2026-04-14

### Added
- **Neo4j direct import** (`export --format neo4j`): connects directly to a running Neo4j database server via the Bolt protocol and imports the full type dependency graph — no intermediate files produced. Each type is written as a `(:Type {fqn, name, kind, namespace})` node; dependency edges become typed relationships (`:INHERITS_FROM`, `:IMPLEMENTS`, `:DEPENDS_ON {reason}`). All writes use `MERGE`, making the import fully idempotent.
- New CLI options on the `export` subcommand: `--neo4j-uri` (default `bolt://localhost:7687`), `--neo4j-user` (default `neo4j`), `--neo4j-password` (or `NEO4J_PASSWORD` environment variable), `--neo4j-database` (default `neo4j`). The password is never logged.
- `Neo4j.Driver` 6.0.0 added as a dependency.

### Changed
- `--output-dir` is no longer a required option at parse time; it is validated at runtime and only required when `--format doxygen` is used.
- `--format` on the `export` subcommand now accepts `doxygen` (existing) and `neo4j` (new).

## [2.0.0] — 2026-04-14

### Added
- **Doxygen XML export** (`export` subcommand): exports the complete type dependency graph to a directory of Doxygen-conformant XML files (compound schema version 1.9.1), one file per type plus namespace compounds and an `index.xml` catalogue. Designed for downstream ingestion into Neo4j or other graph tooling.
- **Subcommand-based CLI**: the tool now exposes two subcommands — `analyze` for fan-in analysis and `export` for Doxygen XML export — replacing the previous flat option set.
- `DoxygenEdgeKind` model: classifies dependency edges as Inheritance, InterfaceImplementation, or Usage.
- `DoxygenRefIdHelper`: deterministic Doxygen `refid` generation with correct encoding for generic types (`<T>` → `_3_0T_3_1`), namespaces, and special characters.
- `DoxygenXmlExporter`: serialises `DependencyGraph` to XML using `System.Xml.Linq` (no new NuGet dependencies).
- 29 new tests covering Doxygen export (DX-01 – DX-26), refid generation, edge classification, and integration.
- Sample Doxygen XML output committed to `samples/SampleCodebase/doxygen-xml/` (17 types, 24 dependency edges, 24 XML files).

### Changed
- **Breaking change**: the CLI interface has changed from a flat argument set to subcommands. Existing scripts calling the tool directly must be updated to use `analyze --target … --files … --output …` instead of `--target … --files … --output …`.
- `README.md` updated: Doxygen export presented as a primary function equal to fan-in analysis; new "Doxygen XML Schema Mapping" section documents the complete internal-model-to-XML mapping.

### Fixed
- Generic type names containing `<` and `>` (e.g. `IRepository<T>`) no longer produce invalid filenames on Windows. Angle brackets are now encoded as `_3_0` / `_3_1` in Doxygen `refid` values.

---

## [1.0.0] — 2026-04-08

### Added
- Initial release.
- Roslyn-based static analysis of C# source files without requiring a `.csproj` or `.sln`.
- Transitive fan-in computation via BFS on the reversed dependency adjacency list.
- Dependency graph extraction covering ~45 C# syntax constructs (inheritance, field types, method signatures, generics, patterns, attributes, LINQ, primary constructors, and more).
- Markdown report output with fan-in table, per-kind metrics, and Mermaid dependency diagram.
- Self-contained single-file portable executable for Windows x64 (no .NET runtime required on target machine).
- 179 tests covering all analysis components, end-to-end pipeline, comprehensive C# construct coverage (4 audit rounds), portable executable verification, CI workflow validation, and test-report document structure.
