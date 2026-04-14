# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

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
