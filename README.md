# C# Dependency Analyzer

[![CI](https://github.com/nbrouwers/csharp-dependency-analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/nbrouwers/csharp-dependency-analyzer/actions/workflows/ci.yml)

A Roslyn-based static analysis tool that computes the **transitive fan-in** of a specified C# class. Given a target class and a set of source files, it identifies every type (class, interface, struct, enum, record, delegate) that directly or transitively depends on the target — producing a Markdown report with justifications and metrics.

This is useful when planning to move a class to a separate project or assembly: the fan-in set tells you exactly which types must move along with it to avoid breaking compilation.

## How It Works

1. **Roslyn compilation** — All listed source files are parsed and compiled in-memory using the Roslyn Compiler Platform. No `.csproj` or `.sln` is needed; the tool works directly on raw `.cs` files.
2. **Dependency graph extraction** — A `CSharpSyntaxWalker` visits every syntax tree and records type-to-type dependency edges (inheritance, field types, method signatures, object creation, casts, pattern matching, attributes, generics, and ~40 other C# constructs).
3. **Transitive closure** — A BFS on the reversed adjacency list computes the full set of types that depend on the target, di
rectly or transitively.
4. **Report generation** — Results are written to a Markdown file with a fan-in table and per-kind metrics.

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

```bash
dotnet run --project src/DependencyAnalyzer -- \
  --target "MyCompany.Core.Services.OrderService" \
  --files  filelist.txt \
  --output report.md
```

Or, if you built/published the executable directly:

```bash
./DependencyAnalyzer \
  --target "MyCompany.Core.Services.OrderService" \
  --files  filelist.txt \
  --output report.md
```

### CLI Options

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--target` | Yes | — | Fully qualified name of the target class (e.g. `MyCompany.Core.OrderService`). |
| `--files` | Yes | — | Path to a text file containing one source file path per line. |
| `--output` | No | `dependency-report.md` | Path for the generated Markdown report. |

### 3. Read the report

The output is a Markdown file containing:

- A **fan-in table** listing every type that depends (directly or transitively) on the target, with its kind and a human-readable justification chain.
- A **metrics summary** with counts per element kind and the total.

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
```

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | User error (target class not found, file list missing, invalid input) |
| 2 | Unexpected internal error |

## Using on a Different Codebase

To analyze a codebase in a completely separate repository or environment:

### Option A: Portable Executable (recommended)

No .NET SDK needed on the target machine.

1. **Copy** `DependencyAnalyzer.exe` (from the `publish/` folder) to the target machine.

2. **Generate a file list** pointing to the `.cs` files you want to analyze:
   ```powershell
   Get-ChildItem -Recurse -Filter *.cs C:\path\to\other-repo\src | ForEach-Object { $_.FullName } | Set-Content -Encoding UTF8 filelist.txt
   ```

3. **Run the tool:**
   ```powershell
   .\DependencyAnalyzer.exe --target "OtherRepo.Domain.SomeService" --files filelist.txt --output report.md
   ```

4. **Read** `report.md` — it contains the full transitive fan-in set.

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

3. **Run the tool** with the fully qualified name of the class you care about:
   ```bash
   dotnet run --project src/DependencyAnalyzer -- \
     --target "OtherRepo.Domain.SomeService" \
     --files  /path/to/other-repo/filelist.txt \
     --output /path/to/other-repo/fan-in-report.md
   ```

4. **Read** `fan-in-report.md` — it contains the full transitive fan-in set.

### Important Notes for External Codebases

- **No `.csproj` or `.sln` required.** The tool compiles raw `.cs` files in isolation using Roslyn. It does not invoke `dotnet build` on the target codebase.
- **NuGet package types are not resolved.** The compilation references only the .NET BCL (Base Class Library) assemblies from the SDK. Types coming from third-party NuGet packages will not be in the semantic model. This means:
  - Dependencies *between your own source files* are fully tracked.
  - Dependencies on types defined in NuGet packages (e.g. `Microsoft.EntityFrameworkCore.DbContext`) are not tracked, by design — the tool focuses on in-scope source code.
- **Include all relevant files.** If your codebase spans multiple projects, include `.cs` files from all projects you want analyzed in the file list. Only files listed will be considered.
- **Generated files.** If your build produces generated `.cs` files (source generators, T4, etc.), you may want to include those in the file list as well. Run `dotnet build` on the target codebase first to produce them, then include the generated files from the `obj/` directories.
- **Target must exist in scope.** The `--target` FQN must resolve to exactly one type definition within the listed source files. If the class is not found, the tool exits with code 1 and an error message.

## Running the Sample

A self-contained sample codebase is included under `samples/SampleCodebase/` with a pre-built `filelist.txt`:

```bash
dotnet run --project src/DependencyAnalyzer -- \
  --target "SampleApp.Core.OrderService" \
  --files  samples/SampleCodebase/filelist.txt \
  --output samples/SampleCodebase/report.md
```

This produces a report identifying 11 fan-in elements (7 classes, 1 interface, 1 struct, 1 record, 1 delegate). See `samples/SampleCodebase/README.md` for the expected results.

## Running Tests

```bash
dotnet test
```

The test suite contains 160 tests covering:

- Roslyn workspace building and target resolution
- Individual dependency type detection (inheritance, fields, generics, patterns, etc.)
- Transitive fan-in computation (direct, transitive, circular, diamond dependencies)
- Markdown report generation
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
│   │   └── new-feature.prompt.md           # New feature procedure (Copilot prompt)
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
│   │   └── DependencyGraph.cs              # Adjacency list + element kinds
│   ├── Analysis/
│   │   ├── RoslynWorkspaceBuilder.cs       # Builds CSharpCompilation from files
│   │   ├── DependencyGraphBuilder.cs       # Discovers types, runs visitor
│   │   ├── DependencyVisitor.cs            # CSharpSyntaxWalker (~45 overrides)
│   │   └── TransitiveFanInAnalyzer.cs      # BFS transitive closure
│   └── Reporting/
│       └── MarkdownReportGenerator.cs      # Markdown report output
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
│   ├── PortableExeTests.cs
│   ├── CiWorkflowTests.cs
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
