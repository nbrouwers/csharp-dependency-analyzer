# Implementation Plan: CSV Export Format

**Feature**: `export --format csv`  
**Status**: Planned  
**Date**: 2026-04-15

---

## 1. Overview

The CSV export adds `--format csv` to the existing `export` subcommand. It produces two files — `nodes.csv` and `relationships.csv` — in a caller-specified `--output-dir` directory. The primary challenge is that several columns in the spec (`file`, `startLine`, `endLine`, `accessibility`, `resolved`) require data the current `DependencyGraph` model does not track. A data-model enrichment phase must come first.

### Output files

**`nodes.csv`**
```
id:ID,name,type,:LABEL,file,startLine:int,endLine:int,accessibility,fullyqualifiedname,resolved:boolean
```

**`relationships.csv`**
```
:START_ID,:END_ID,:TYPE,startLine:int,endLine:int
```

---

## 2. Key Design Decisions

The following decisions were made before implementation:

| # | Decision | Choice |
|---|----------|--------|
| 1 | **Unresolved node scope** | Emit unresolved (out-of-scope) referenced nodes only when their FQN matches at least one in-scope namespace root prefix. BCL types (e.g. `System.Console`) are excluded. |
| 2 | **Relationship line numbers** | Fully implement site-level location tracking for edges. Defaulting to `0,0` is not acceptable; every reference site location must be captured. |
| 3 | **Project root for file relativization** | No `--project-root` CLI option. Derive the root from the directory containing the `--files` filelist. |
| 4 | **Record node type label** | `ElementKind.Record` maps to type label `class` (consistent with Doxygen). |

---

## 3. Gap Analysis

| CSV Column | Currently Available | Gap |
|---|---|---|
| `id:ID` | `DoxygenRefIdHelper.ToRefId` (Doxygen string) | Need 16-char hex ID — new `CsvIdHelper` |
| `name` | Short name extractable from FQN | Trivial: last segment after last `.` |
| `type` / `:LABEL` | `ElementKind` enum | Need `ElementKind` → CSV label mapping |
| `file` | Not in `DependencyGraph` | **Missing** — capture at build time from `SyntaxTree.FilePath` |
| `startLine` / `endLine` (nodes) | Not in `DependencyGraph` | **Missing** — type declaration location |
| `accessibility` | Not in `DependencyGraph` | **Missing** — `INamedTypeSymbol.DeclaredAccessibility` |
| `resolved:boolean` | Not tracked | **Missing** — requires unresolved-node tracking |
| `:START_ID` / `:END_ID` | Derivable from FQN | Trivial from `CsvIdHelper` |
| `:TYPE` (relationships) | Partially — `DependencyReason` exists | Need reason → CSV type mapping |
| `startLine` / `endLine` (rels) | Not in `TypeDependency` | **Missing** — reference site location in `DependencyVisitor` |

---

## 4. Implementation Phases

### Phase 1 — New Model Types

**`src/DependencyAnalyzer/Models/TypeLocation.cs`** *(new)*

```csharp
namespace DependencyAnalyzer.Models;

/// <summary>
/// Source location and accessibility of a type declaration.
/// Captured during graph build and stored in DependencyGraph.TypeLocations.
/// </summary>
public sealed record TypeLocation(
    string FilePath,       // Absolute path at build time; relativized at export time.
    int StartLine,         // 1-based line of the opening type declaration keyword.
    int EndLine,           // 1-based line of the closing brace.
    string Accessibility); // "public", "internal", "protected", "private", "protected internal"
```

**`src/DependencyAnalyzer/Models/DependencyLocation.cs`** *(new)*

```csharp
namespace DependencyAnalyzer.Models;

/// <summary>
/// Source location of the syntax node that introduces a dependency edge.
/// Captured in DependencyVisitor at the reference site.
/// </summary>
public sealed record DependencyLocation(int StartLine, int EndLine);
```

---

### Phase 2 — Extend `DependencyGraph`

**`src/DependencyAnalyzer/Models/DependencyGraph.cs`** *(modified)*

Add three new collections:

```csharp
/// <summary>File location and accessibility per in-scope FQN.</summary>
public Dictionary<string, TypeLocation> TypeLocations { get; } = new();

/// <summary>Reference-site location per dependency edge instance.</summary>
public Dictionary<TypeDependency, DependencyLocation> EdgeLocations { get; } = new();

/// <summary>
/// FQNs that appeared as dependency targets but were not in scope.
/// Only populated for FQNs that share a namespace root with at least one in-scope type.
/// These are exported as nodes with resolved=false.
/// </summary>
public HashSet<string> UnresolvedReferences { get; } = new();
```

Add helper methods:

```csharp
public void SetTypeLocation(string fqn, TypeLocation location)
    => TypeLocations[fqn] = location;

public void SetEdgeLocation(TypeDependency edge, DependencyLocation location)
    => EdgeLocations[edge] = location;

public void AddUnresolvedReference(string fqn)
    => UnresolvedReferences.Add(fqn);
```

---

### Phase 3 — Capture Locations in `DependencyGraphBuilder`

**`src/DependencyAnalyzer/Analysis/DependencyGraphBuilder.cs`** *(modified)*

**Pass 1 extension** — capture `TypeLocation` for every discovered symbol:

```csharp
var declRef   = namedType.DeclaringSyntaxReferences.FirstOrDefault();
var location  = namedType.Locations.FirstOrDefault(l => l.IsInSource);
var startLine = location?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
var endLine   = declRef?.GetSyntax().GetLocation().GetLineSpan().EndLinePosition.Line + 1 ?? 0;
var access    = MapAccessibility(namedType.DeclaredAccessibility);
var filePath  = location?.SourceTree?.FilePath ?? "";

graph.SetTypeLocation(fqn, new TypeLocation(filePath, startLine, endLine, access));
```

Add `MapAccessibility`:

```csharp
private static string MapAccessibility(Accessibility a) => a switch
{
    Accessibility.Public             => "public",
    Accessibility.Private            => "private",
    Accessibility.Protected          => "protected",
    Accessibility.Internal           => "internal",
    Accessibility.ProtectedOrInternal => "protected internal",
    _                                => "internal"
};
```

**Pass 2 extension** — after visiting each tree, read edge locations from the visitor:

```csharp
foreach (var (dep, loc) in visitor.EdgeLocations)
    graph.SetEdgeLocation(dep, loc);

foreach (var fqn in visitor.UnresolvedReferences)
    graph.AddUnresolvedReference(fqn);
```

---

### Phase 4 — Capture Edge Locations in `DependencyVisitor`

**`src/DependencyAnalyzer/Analysis/DependencyVisitor.cs`** *(modified)*

Add two new outputs:

```csharp
private readonly Dictionary<TypeDependency, DependencyLocation> _edgeLocations = new();
private readonly List<string> _unresolvedReferences = new();

public IReadOnlyDictionary<TypeDependency, DependencyLocation> EdgeLocations => _edgeLocations;
public IReadOnlyList<string> UnresolvedReferences => _unresolvedReferences;
```

Extend `RecordDependency(INamedTypeSymbol, string)` with an optional `SyntaxNode?` parameter for the reference site:

```csharp
private void RecordDependency(INamedTypeSymbol typeSymbol, string reason, SyntaxNode? site = null)
{
    if (_currentTypeFqn == null) return;

    var resolved = typeSymbol.OriginalDefinition ?? typeSymbol;
    var fqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(resolved);

    if (fqn != _currentTypeFqn && _inScopeTypes.Contains(fqn))
    {
        var dep = new TypeDependency(_currentTypeFqn, fqn, reason);
        _dependencies.Add(dep);

        if (site != null)
        {
            var span = site.GetLocation().GetLineSpan();
            _edgeLocations[dep] = new DependencyLocation(
                span.StartLinePosition.Line + 1,
                span.EndLinePosition.Line + 1);
        }
    }
    else if (fqn != _currentTypeFqn && !_inScopeTypes.Contains(fqn)
             && SharesNamespaceRoot(fqn))
    {
        _unresolvedReferences.Add(fqn);
    }
}
```

Extend `RecordDependencyByFqn(string, string)` similarly to track the reference site when available.

Every call to `RecordDependency` and `RecordTypeReference` that already has an enclosing syntax node in scope (e.g. `VisitInvocationExpression`, `VisitMemberAccessExpression`, `VisitObjectCreationExpression`, etc.) passes `node` as the `site` argument. Structural references (base lists, parameter lists) pass the corresponding `TypeSyntax` node.

**Unresolved node namespace-root filter**:

```csharp
private bool SharesNamespaceRoot(string fqn)
{
    // Accept if the FQN starts with any in-scope namespace root (first segment(s) before first dot)
    foreach (var inScope in _inScopeTypes)
    {
        var root = inScope.Split('.')[0];
        if (fqn.StartsWith(root + ".", StringComparison.Ordinal) || fqn == root)
            return true;
    }
    return false;
}
```

---

### Phase 5 — `CsvIdHelper`

**`src/DependencyAnalyzer/Models/CsvIdHelper.cs`** *(new)*

```csharp
using System.Security.Cryptography;
using System.Text;

namespace DependencyAnalyzer.Models;

/// <summary>
/// Provides deterministic 16-character hex node IDs and CSV label/type mappings.
/// </summary>
public static class CsvIdHelper
{
    /// <summary>
    /// Returns a deterministic 16-character lowercase hex node ID
    /// derived from SHA-256("{fqn}:{typeLabel}").
    /// </summary>
    public static string ToNodeId(string fqn, string typeLabel)
    {
        var input = Encoding.UTF8.GetBytes($"{fqn}:{typeLabel}");
        var hash  = SHA256.HashData(input);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>Maps ElementKind to the CSV type/:LABEL string.</summary>
    public static string ToTypeLabel(ElementKind kind) => kind switch
    {
        ElementKind.Interface => "interface",
        ElementKind.Struct    => "struct",
        ElementKind.Enum      => "enum",
        _                     => "class"  // Class, Record, Delegate all → "class"
    };

    /// <summary>
    /// Maps a DependencyReason string to the CSV relationship :TYPE value.
    /// </summary>
    public static string ToRelationshipType(string dependencyReason) =>
        dependencyReason switch
        {
            "Inherits from"        => "basecompoundref",
            "Implements interface" => "basecompoundref",
            _                      => "ref"
        };
}
```

The 16-character length constraint is satisfied by the first 16 hex digits of the SHA-256 output. For the expected scale of C# codebases (< 100k types), the collision probability is negligible (~1 in 10¹⁸ per pair).

---

### Phase 6 — `CsvExporter`

**`src/DependencyAnalyzer/Reporting/CsvExporter.cs`** *(new)*

```csharp
namespace DependencyAnalyzer.Reporting;

public sealed class CsvExporter
{
    private const string NodeHeader =
        "id:ID,name,type,:LABEL,file,startLine:int,endLine:int," +
        "accessibility,fullyqualifiedname,resolved:boolean";

    private const string RelHeader =
        ":START_ID,:END_ID,:TYPE,startLine:int,endLine:int";

    /// <summary>
    /// Writes nodes.csv and relationships.csv to <paramref name="outputDirectory"/>.
    /// File paths in the nodes file are relativized to <paramref name="projectRoot"/>,
    /// using forward slashes.
    /// </summary>
    /// <returns>(nodesWritten, relationshipsWritten)</returns>
    public (int NodesWritten, int RelsWritten) Export(
        DependencyGraph graph,
        string outputDirectory,
        string projectRoot)
    { ... }
}
```

**`projectRoot`** is derived by the caller (in `Program.cs`) as:
```csharp
var projectRoot = Path.GetDirectoryName(Path.GetFullPath(filesPath)) ?? ".";
```

**Node rows** are emitted in two groups, both sorted by FQN for deterministic output:
1. In-scope types from `graph.ElementKinds` — `resolved=true`
2. Unresolved references from `graph.UnresolvedReferences` — `resolved=false`, type label `class`, empty `file`/`accessibility`, `startLine=0`, `endLine=0`

**Relationship rows** are emitted for every edge in `graph.Edges`, sorted by `(START_ID, END_ID)`.  Line numbers come from `graph.EdgeLocations[dep]` when present; unresolved-target edges will still have a location because the reference site is always in an in-scope file.

**CSV field escaping** (`EscapeCsvField`): if a field contains a comma, double quote, or newline, wrap it in double quotes and escape any internal double quotes as `""`.

---

### Phase 7 — CLI Integration

**`src/DependencyAnalyzer/Program.cs`** *(modified)*

**7.1** Update `--format` description:
```
"Export format. Supported values: doxygen, neo4j, csv"
```

**7.2** Add `csv` to the `--output-dir` required-check:
```csharp
if (format is "doxygen" or "csv" && outputDir is null)
{
    Console.Error.WriteLine(
        $"ERROR: --output-dir is required when --format is {format}.");
    Environment.ExitCode = 1;
    return;
}
```

**7.3** Add CSV branch in `RunExport`:
```csharp
else if (format == "csv")
{
    var projectRoot = Path.GetDirectoryName(Path.GetFullPath(filesPath)) ?? ".";
    Console.WriteLine($"[CSV] Writing to: {Path.GetFullPath(outputDir!)}");
    var exporter = new CsvExporter();
    var (nodes, rels) = exporter.Export(graph, outputDir!, projectRoot);
    Console.WriteLine($"[CSV] Wrote {nodes} node(s) and {rels} relationship(s).");
}
```

---

### Phase 8 — Tests

**`tests/DependencyAnalyzer.Tests/CsvExporterTests.cs`** *(new)*

| ID | Test Method | Description |
|----|------------|-------------|
| CV-01 | `Export_CreatesNodesAndRelationshipsFiles` | Both `nodes.csv` and `relationships.csv` exist after export |
| CV-02 | `Nodes_HeaderIsCorrect` | First line matches the specified column header exactly |
| CV-03 | `Nodes_ContainsRowForEachInScopeType` | Resolved row count equals `graph.ElementKinds.Count` |
| CV-04 | `Nodes_IdIs16LowerHexChars` | Every `id:ID` field is exactly 16 lowercase hex characters |
| CV-05 | `Nodes_IdIsDeterministic` | Exporting the same graph twice yields identical IDs |
| CV-06 | `Nodes_TypeAndLabelColumnsAlwaysEqual` | `type` and `:LABEL` columns always contain the same value per row |
| CV-07 | `Nodes_TypeLabelClass_ForClassRecordDelegate` | `class`, `record`, and `delegate` kinds all produce `class` label |
| CV-08 | `Nodes_TypeLabelMatchesSpec_ForInterfaceStructEnum` | `interface`, `struct`, `enum` kinds produce matching labels |
| CV-09 | `Nodes_ResolvedIsTrueForInScopeTypes` | All in-scope type rows have `resolved=true` |
| CV-10 | `Nodes_UnresolvedNode_AppearsWithResolvedFalse` | Out-of-scope type in same namespace root → `resolved=false` row |
| CV-11 | `Nodes_UnresolvedNode_BclTypesExcluded` | `System.*` types do not appear in nodes file |
| CV-12 | `Nodes_FilePathIsRelativeWithForwardSlashes` | `file` column contains no backslashes |
| CV-13 | `Nodes_AccessibilityIsCorrectString` | `public`, `internal`, etc. match expected values |
| CV-14 | `Nodes_StartLineAndEndLineArePositive` | Line numbers are > 0 for all resolved types |
| CV-15 | `Relationships_HeaderIsCorrect` | First line matches the specified column header exactly |
| CV-16 | `Relationships_ContainsRowForEachEdge` | Row count equals total edge count in graph |
| CV-17 | `Relationships_Inheritance_TypeIsBasecompoundref` | `Inherits from` reason → `:TYPE=basecompoundref` |
| CV-18 | `Relationships_TypeReference_TypeIsRef` | Field type and other reasons → `:TYPE=ref` |
| CV-19 | `Relationships_StartAndEndIdMatchNodeIds` | Every `:START_ID` and `:END_ID` appears in the nodes file |
| CV-20 | `Relationships_LineNumbersArePositive` | `startLine` and `endLine` in relationships are > 0 |
| CV-21 | `Export_FieldsWithCommasAreQuoted` | FQN string containing a comma is properly CSV-escaped |
| CV-22 | `Export_IsIdempotent` | Exporting twice produces byte-identical files |
| CV-23 | `CsvIdHelper_AllIdsUniqueInSampleGraph` | No two nodes share an ID in the sample codebase graph |

**`tests/DependencyAnalyzer.Tests/CsvIdHelperTests.cs`** *(new)*

| ID | Test Method | Description |
|----|------------|-------------|
| CI-01 | `ToNodeId_IsExactly16Chars` | Result is always 16 characters |
| CI-02 | `ToNodeId_IsLowercaseHex` | Result contains only `[0-9a-f]` |
| CI-03 | `ToNodeId_IsDeterministic` | Same inputs always produce the same output |
| CI-04 | `ToNodeId_DiffersForDifferentFqn` | Different FQNs produce different IDs |
| CI-05 | `ToNodeId_DiffersForDifferentLabel` | Same FQN, different type label → different ID |

---

## 5. File Change Summary

| File | Change |
|---|---|
| `Models/TypeLocation.cs` | **New** |
| `Models/DependencyLocation.cs` | **New** |
| `Models/CsvIdHelper.cs` | **New** |
| `Models/DependencyGraph.cs` | Add `TypeLocations`, `EdgeLocations`, `UnresolvedReferences` + helpers |
| `Analysis/DependencyVisitor.cs` | Add `EdgeLocations`, `UnresolvedReferences` outputs; extend `RecordDependency` with `site` parameter; add `SharesNamespaceRoot` filter |
| `Analysis/DependencyGraphBuilder.cs` | Pass 1: capture `TypeLocation`; Pass 2: merge edge locations and unresolved references from visitor |
| `Reporting/CsvExporter.cs` | **New** |
| `Program.cs` | Add `csv` to format option; extend output-dir guard; add CSV branch in `RunExport` |
| `tests/.../CsvExporterTests.cs` | **New** (CV-01–CV-23) |
| `tests/.../CsvIdHelperTests.cs` | **New** (CI-01–CI-05) |
| `docs/requirements.md` | Add FR-5.4 (CSV export subcommand option) and FR-8.x (CSV format spec) |
| `docs/test-report.md` | Add section 3.17 (CsvExporterTests) and 3.18 (CsvIdHelperTests); update totals |
| `README.md` | Add `csv` to the export section; document output files and column semantics |
| `CHANGELOG.md` | Add entry to `[Unreleased]` |

---

## 6. Out of Scope

- Line-number tracking for namespace-compound nodes (namespaces have no single declaration site in C#).
- Relationship `:TYPE` values beyond `basecompoundref` and `ref` (e.g. `member`, `memberdef`, `declared_in`). These require structural membership edges not currently produced by the graph builder.
- A `--project-root` CLI option (root is derived from the filelist directory).
- Streaming / chunked write for very large graphs (a future performance concern).
