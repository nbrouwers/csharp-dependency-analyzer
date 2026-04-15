# Requirements: C# Dependency Analyzer

## 1. Purpose

Determine the complete transitive fan-in of a specified C# class using Roslyn-based static analysis. The results support moving the target class to a new project/assembly, ensuring the new assembly contains all transitive fan-in dependencies — preventing undesired cross-assembly references that would violate architectural rules.

## 2. Definitions

| Term | Definition |
|------|-----------|
| **Target class** | The C# class specified by the user (via fully qualified name) for which transitive fan-in is computed. |
| **Fan-in** | The set of code elements that directly or transitively depend on the target class. An element is fan-in if removing the target class would cause a compilation error in that element, or in an element it transitively depends on. |
| **Transitive fan-in** | The full closure: if element A depends on target T, and element B depends on A, then both A and B are in the transitive fan-in of T. |
| **Element** | A discrete code construct: class, interface, struct, enum, record, or delegate. |
| **Source scope** | The user-defined set of source files to include in the analysis. Files outside this scope are ignored. |

## 3. Functional Requirements

### FR-5: Subcommand-Based CLI

- **FR-5.1**: The tool shall expose its functionality through named subcommands. The current subcommands are `analyze` and `export`.
- **FR-5.2**: The `analyze` subcommand shall compute the dependency analysis for a specified target class and produce a structured report. It shall support a `--mode` option (`fan-in` by default) to select the analysis direction; unknown mode values shall produce a clear error.
- **FR-5.3**: The `export` subcommand shall extract the full dependency graph of the source scope and write it to an external format. It shall not require a target class. It shall support a `--format` option; supported values are `doxygen` and `neo4j`.
- **FR-5.4**: Both subcommands shall share the same `--files` file-list mechanism (FR-2.1).

### FR-7: Neo4j Direct Import

- **FR-7.1**: The `export --format neo4j` command shall connect to a running Neo4j database server using the Bolt protocol and import the full dependency graph directly — no intermediate files are produced.
- **FR-7.2**: Each in-scope type shall be imported as a Neo4j node with the label `Compound` and the following properties (mirroring the Doxygen `<compounddef>` attributes): `id` (Doxygen refid — unique and deterministic, serves as the MERGE key), `kind` (Doxygen kind string: `class`, `interface`, `struct`, `enum`), `name` (compound name using `::` as namespace separator, mirroring `<compoundname>`), `fqn` (original .NET fully qualified name), `language` (always `"C#"`).
- **FR-7.3**: The import shall use `MERGE` statements on the `id` (Doxygen refid) property to be idempotent — re-running the import on the same database shall not produce duplicate nodes or relationships.
- **FR-7.4**: Dependency edges shall be mapped to typed Neo4j relationships (mirroring the Doxygen compound schema):
  - `BASECOMPOUNDREF {prot: "public", virt: "non-virtual"}` for inheritance edges (mirrors `<basecompoundref virt="non-virtual">`).
  - `BASECOMPOUNDREF {prot: "public", virt: "virtual"}` for interface-implementation edges (mirrors `<basecompoundref virt="virtual">`).
  - `REFERENCES {kind: "variable", reason: "..."}` for all other dependency edges, with the `DependencyReason` string stored as a `reason` property (mirrors `<memberdef kind="variable"><references>`).
- **FR-7.5**: The Neo4j connection shall be configurable via CLI options: `--neo4j-uri` (default `bolt://localhost:7687`), `--neo4j-user` (default `neo4j`), `--neo4j-password` (required — may also be supplied via the `NEO4J_PASSWORD` environment variable), `--neo4j-database` (default `neo4j`).
- **FR-7.6**: The tool shall not log or persist the Neo4j password. Error messages involving connection failures shall not include credential values.
- **FR-7.7**: After import, the tool shall report the number of nodes written and the number of relationships written to the console.
- **FR-7.8**: Each unique namespace prefix found within the in-scope type FQNs shall be imported as a `(:Compound {kind: "namespace"})` node, mirroring the Doxygen `<compounddef kind="namespace">` compound. A `INNERCLASS {prot: "public"}` relationship shall be written from each namespace compound to each type it directly contains, mirroring the Doxygen `<innerclass>` element.

### FR-6: Doxygen XML Export

- **FR-6.1**: The `export --format doxygen` command shall produce a directory of UTF-8 XML files whose structure conforms to the Doxygen compound schema (compound.xsd version 1.9.1).
- **FR-6.2**: Each in-scope type shall be represented as a `<compounddef>` element in its own file, named `{refid}.xml`. The `refid` shall be derived deterministically from the type's fully qualified name and its `ElementKind`.
- **FR-6.3**: Inheritance and interface-implementation edges shall be emitted as `<basecompoundref>` elements on the source compound.
- **FR-6.4**: All other dependency edges (field type, method parameter, object creation, etc.) shall be emitted as `<memberdef kind="variable">` elements within a `<sectiondef>`, each carrying a `<references>` child pointing to the target compound.
- **FR-6.5**: A top-level `index.xml` shall list every exported compound with its `refid`, `kind`, and `name`.
- **FR-6.6**: A `<compounddef kind="namespace">` shall be generated for every unique namespace prefix found in the source scope.
- **FR-6.7**: Where source location data is unavailable (the graph does not store file paths or line numbers), the tool shall emit a stub `<location file="" line="0" column="0"/>` element.

### FR-1: Specify Target Class

- **FR-1.1**: The user shall provide the fully qualified name (FQN) of the target class (e.g. `MyCompany.Core.Services.OrderService`).
- **FR-1.2**: The tool shall validate that the FQN resolves to exactly one class definition within the source scope. If not found or ambiguous, the tool shall report a clear error.

### FR-2: Define Source Scope

- **FR-2.1**: The user shall provide a list of source file paths that define the analysis scope.
- **FR-2.2**: Only source files in this list shall be parsed and analyzed. All other files shall be excluded.
- **FR-2.3**: The tool shall report a warning for any listed file that does not exist or cannot be parsed.

### FR-3: Dependency Analysis

- **FR-3.1**: The tool shall use the Roslyn Compiler Platform (`Microsoft.CodeAnalysis`) to build a semantic model of all in-scope source files.
- **FR-3.2**: The tool shall identify all type-level dependencies between elements within the source scope. A dependency exists when an element references the target class (or a transitive fan-in element) through any of the following:
  - Inheritance (base class)
  - Interface implementation
  - Field, property, or event type
  - Method parameter type or return type
  - Local variable type
  - Generic type argument
  - Attribute usage
  - Object creation (`new T()`)
  - Static member access
  - Type cast or type check (`is`, `as`, pattern matching)
  - `typeof(T)` expressions
  - Extension method target type
  - `Type.GetType("FQN")` or `Assembly.GetType("FQN")` calls where the first argument is a string literal whose value matches an in-scope fully qualified type name (Strategy 1 reflection detection)
- **FR-3.3**: The tool shall compute the transitive closure of all fan-in elements — i.e., if element A depends on the target, and element B depends on A, then B is also included.
- **FR-3.4**: The target class itself shall not be listed as a fan-in element.

### FR-4: Report Generation

- **FR-4.1**: After analysis, the tool shall generate a dependency analysis report.
- **FR-4.2**: The report shall contain a **fan-in element list** with the following columns per element:
  - Fully qualified name
  - Kind/type (class, interface, struct, enum, record, delegate)
  - Justification: a human-readable explanation of why this element is fan-in (e.g., "Directly inherits from `OrderService`" or "Uses `IOrderRepository` which is a direct fan-in of `OrderService`")
- **FR-4.3**: The report shall contain a **metrics overview**:
  - Total number of fan-in elements
  - Count per element kind (e.g., 5 classes, 2 interfaces, 1 enum)
  - Maximum transitive depth (the longest chain of dependency layers from any fan-in element back to the target)
- **FR-4.4**: The report shall be written to a file in a structured, human-readable format (Markdown).
- **FR-4.5**: The console output shall also report the maximum transitive depth alongside the fan-in element count.
- **FR-4.6**: The report shall contain a **dependency graph** rendered as a Mermaid diagram showing the target, all fan-in elements, and edges between them. The target node shall be visually distinct from fan-in elements.

## 4. Non-Functional Requirements

### NFR-1: Technology

- **NFR-1.1**: The tool shall be implemented in C# targeting .NET 8.0 or later.
- **NFR-1.2**: Dependency analysis shall use the Roslyn Compiler Platform (`Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.CSharp.Workspaces`).
- **NFR-1.3**: The tool shall be a console application invocable from the command line.

### NFR-4: Distribution

- **NFR-4.1**: The build shall produce a self-contained, single-file portable executable for Windows (x64) that does not require the .NET SDK or runtime to be installed on the target machine.
- **NFR-4.2**: The portable executable shall run on any Windows 11 system without additional prerequisites.
- **NFR-4.3**: The portable executable shall be produced automatically as part of every build (both `dotnet build` and `dotnet publish`).

### NFR-5: Continuous Integration

- **NFR-5.1**: A GitHub Actions workflow shall run automatically on every push to any branch and on every pull request to `main`.
- **NFR-5.2**: The CI pipeline shall execute the full test suite; the build shall not proceed if any test fails.
- **NFR-5.3**: Upon successful tests, the pipeline shall publish the self-contained portable executable as a downloadable build artifact.
- **NFR-5.4**: The workflow shall use a pinned .NET SDK version to ensure reproducible builds.

### NFR-2: Performance

- **NFR-2.1**: The tool shall handle source scopes of at least 1,000 files within a reasonable time frame.
- **NFR-2.2**: Memory consumption shall remain bounded for typical large codebases.

### NFR-3: Usability

- **NFR-3.1**: The tool shall provide clear error messages for invalid inputs (missing target class, unparseable files, etc.).
- **NFR-3.2**: The tool shall log progress during analysis (e.g., number of files parsed, analysis phase).

### NFR-6: Versioning

- **NFR-6.1**: The project shall follow [Semantic Versioning 2.0.0](https://semver.org/) (MAJOR.MINOR.PATCH).
- **NFR-6.2**: The version shall be defined in the `.csproj` file using `<Version>` and embedded into the assembly at build time.
- **NFR-6.3**: The CLI shall display the version when invoked with `--version`.
- **NFR-6.4**: The generated Markdown report shall include the tool version in its header.

## 5. Input / Output Specification

### 5.1 Input — `analyze` subcommand

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `--target` | Yes | — | Fully qualified name of the target class. |
| `--files` | Yes | — | Path to a text file containing one source file path per line. |
| `--output` | No | `dependency-report.md` | Path for the output report file. |
| `--mode` | No | `fan-in` | Analysis direction. Currently only `fan-in` is implemented. `fan-out` and `combined` are reserved. |

### 5.2 Input — `export` subcommand

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `--files` | Yes | — | Path to a text file containing one source file path per line. |
| `--format` | Yes | — | Export format. Supported values: `doxygen`, `neo4j`. |
| `--output-dir` | When `--format doxygen` | — | Directory to write the exported XML files into. Created if absent. |
| `--neo4j-uri` | No | `bolt://localhost:7687` | Bolt URI of the Neo4j server. Used when `--format neo4j`. |
| `--neo4j-user` | No | `neo4j` | Neo4j username. Used when `--format neo4j`. |
| `--neo4j-password` | When `--format neo4j` | `NEO4J_PASSWORD` env var | Neo4j password. Not logged. |
| `--neo4j-database` | No | `neo4j` | Target Neo4j database name. Used when `--format neo4j`. |

### 5.3 Output — `analyze` subcommand

A Markdown report file with the following structure:

```
# Dependency Analysis Report
## Target: <FQN>
## Date: <timestamp>

## Fan-In Elements

| # | Fully Qualified Name | Kind | Justification |
|---|----------------------|------|---------------|
| 1 | Namespace.ClassName  | Class | Directly references target via field type |
| ...                                                          |

## Metrics

| Kind      | Count |
|-----------|-------|
| Class     | N     |
| Interface | N     |
| ...       |       |
| **Total** | **N** |
```

```
## Dependency Graph

` ` `mermaid
graph LR
    target["TargetClass"]:::target
    node1["DependorA"] --> target
    node2["DependorB"] --> node1
    classDef target fill:#f96,stroke:#333,stroke-width:2px
` ` `
```

### 5.5 Output — `export --format neo4j` subcommand

No files are produced. The dependency graph is imported directly into the Neo4j database. Console output reports:

```
C# Dependency Analyzer v2.x.x
Mode:   export / neo4j

Source files listed: N

[1/2] Building Roslyn compilation...
[2/2] Building dependency graph...

Discovered M type(s), P dependency edge(s).
[Neo4j] Connecting to bolt://...
[Neo4j] Wrote M node(s) and Q relationship(s).
```

### 5.4 Output — `export --format doxygen` subcommand

A directory of UTF-8 XML files conforming to the Doxygen compound schema:

```
{output-dir}/
  index.xml                                   ← lists all compounds
  class{Namespace}_1_1{TypeName}.xml          ← one file per type
  namespace{Namespace}.xml                    ← one file per namespace
```

Each compound file contains a `<compounddef>` with:
- Inheritance/interface edges as `<basecompoundref>` elements
- All other dependency edges as `<memberdef kind="variable">` with `<references>` children
- A stub `<location file="" line="0" column="0"/>` element

## 6. Validation Requirements

### VR-1: Sample Codebase

- **VR-1.1**: An artificial but representative C# codebase shall be created for validation purposes.
- **VR-1.2**: The sample codebase shall include: classes, interfaces, structs, enums, records, and delegates with a mix of direct and transitive dependencies, as well as elements with no dependency on the target.
- **VR-1.3**: The expected transitive fan-in for at least one target class shall be manually determined and documented.

### VR-2: Unit Tests

- **VR-2.1**: Unit tests shall be created using xUnit.
- **VR-2.2**: Tests shall cover:
  - Correct identification of direct fan-in elements.
  - Correct computation of transitive fan-in closure.
  - Correct exclusion of elements outside the source scope.
  - Correct element kind classification.
  - Correct handling of edge cases (circular dependencies, self-references, no fan-in).
  - Error handling (invalid FQN, missing files).
- **VR-2.3**: A test report document (`docs/test-report.md`) shall catalog every test case with its identifier, description, test level, and traceability to requirements. The report shall be kept up to date when tests are added or removed.

### VR-3: Cross-LLM Validation

- **VR-3.1**: The tool's analysis results on the sample codebase shall be independently validated using a different code-capable LLM than the one used for development.
- **VR-3.2**: The validation LLM shall be given the sample source code and target class, and asked to independently determine the transitive fan-in. Results shall be compared for discrepancies.

## 7. Assumptions

1. All source files are syntactically valid C# (may contain compilation errors, but must be parseable).
2. External/NuGet dependencies are not in scope — only types defined within the provided source files are analyzed.
3. The analysis is purely static; runtime behavior (reflection, dynamic dispatch) is not considered.
