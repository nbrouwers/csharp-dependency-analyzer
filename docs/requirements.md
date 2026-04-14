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
- **FR-4.4**: The report shall be written to a file in a structured, human-readable format (Markdown).

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

## 5. Input / Output Specification

### 5.1 Input

| Parameter | Required | Description |
|-----------|----------|-------------|
| `--target` | Yes | Fully qualified name of the target class. |
| `--files` | Yes | Path to a text file containing one source file path per line. |
| `--output` | No | Path for the output report file. Default: `dependency-report.md` in the current directory. |

### 5.2 Output

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
