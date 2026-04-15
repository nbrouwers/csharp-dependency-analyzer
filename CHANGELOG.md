# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
