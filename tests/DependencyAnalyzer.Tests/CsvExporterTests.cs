using System.Text;
using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;
using Xunit;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="CsvExporter"/>.
/// </summary>
public sealed class CsvExporterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "CsvExporterTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    // ── CV-01: Creates output directory and both files ────────────────────────

    [Fact]
    public void Export_CreatesOutputDirectoryAndBothFiles()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class A {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        Assert.True(File.Exists(Path.Combine(_outputDir, "nodes.csv")));
        Assert.True(File.Exists(Path.Combine(_outputDir, "relationships.csv")));
    }

    // ── CV-02: nodes.csv has correct header ───────────────────────────────────

    [Fact]
    public void Export_NodesCsvHasCorrectHeader()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class A {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        Assert.Equal("id:ID,name,type,:LABEL,file,startLine:int,endLine:int,accessibility,fullyqualifiedname,resolved:boolean",
            lines[0]);
    }

    // ── CV-03: relationships.csv has correct header ───────────────────────────

    [Fact]
    public void Export_RelsCsvHasCorrectHeader()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class A {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines = File.ReadAllLines(Path.Combine(_outputDir, "relationships.csv"));
        Assert.Equal(":START_ID,:END_ID,:TYPE,startLine:int,endLine:int", lines[0]);
    }

    // ── CV-04: Returns correct node count ─────────────────────────────────────

    [Fact]
    public void Export_ReturnsCorrectNodeCount()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class A {}
                public class B {}
            }
            """);

        var (nodes, _) = new CsvExporter().Export(graph, _outputDir, _outputDir);

        Assert.Equal(2, nodes);
    }

    // ── CV-05: Returns correct relationship count ─────────────────────────────

    [Fact]
    public void Export_ReturnsCorrectRelationshipCount()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class A { public B Field; }
                public class B {}
            }
            """);

        var (_, rels) = new CsvExporter().Export(graph, _outputDir, _outputDir);

        Assert.True(rels >= 1, $"Expected at least 1 relationship, got {rels}");
    }

    // ── CV-06: Only header line when no types or edges ────────────────────────

    [Fact]
    public void Export_EmptyGraph_OnlyHeaderLines()
    {
        var graph = new DependencyGraph();

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var nodeLines = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        var relLines  = File.ReadAllLines(Path.Combine(_outputDir, "relationships.csv"));
        Assert.Single(nodeLines);
        Assert.Single(relLines);
    }

    // ── CV-07: Node row has 10 fields ─────────────────────────────────────────

    [Fact]
    public void Export_NodeRow_HasTenFields()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class A {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        // line[0] = header, line[1] = first data row
        Assert.True(lines.Length >= 2);
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal(10, fields.Length);
    }

    // ── CV-08: Relationship row has 5 fields ──────────────────────────────────

    [Fact]
    public void Export_RelRow_HasFiveFields()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class A { public B Field; }
                public class B {}
            }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines = File.ReadAllLines(Path.Combine(_outputDir, "relationships.csv"));
        Assert.True(lines.Length >= 2, "Expected at least one relationship row");
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal(5, fields.Length);
    }

    // ── CV-09: FQN appears in nodes.csv fullyqualifiedname column (col 9) ─────

    [Fact]
    public void Export_NodeRow_ContainsFqn()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class Alpha {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "nodes.csv"));
        Assert.Contains("Ns.Alpha", content);
    }

    // ── CV-10: Simple name in name column ────────────────────────────────────

    [Fact]
    public void Export_NodeRow_SimpleNameInNameColumn()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class Alpha {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        Assert.True(lines.Length >= 2);
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal("Alpha", fields[1]); // name column
    }

    // ── CV-11: resolved=true for in-scope types, false for unresolved ─────────

    [Fact]
    public void Export_ResolvedFlag_CorrectValues()
    {
        // Build graph manually so we can inject an unresolved reference
        var graph = new DependencyGraph();
        graph.SetElementKind("Ns.A", ElementKind.Class);
        graph.AddUnresolvedReference("Ns.Missing");

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "nodes.csv"));
        // Ns.A should be resolved=true
        Assert.Contains("true", content);
        // Ns.Missing should be resolved=false
        Assert.Contains("false", content);
    }

    // ── CV-12: Unresolved reference appears in nodes.csv ─────────────────────

    [Fact]
    public void Export_UnresolvedReference_AppearsInNodesCsv()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Ns.A", ElementKind.Class);
        graph.AddUnresolvedReference("Ns.UnknownType");

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "nodes.csv"));
        Assert.Contains("Ns.UnknownType", content);
    }

    // ── CV-13: Unresolved in-scope duplicate is not double-written ────────────

    [Fact]
    public void Export_UnresolvedDuplicatesInScope_NotDoubleWritten()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Ns.A", ElementKind.Class);
        // Add "Ns.A" as both in-scope and unresolved (shouldn't happen but must be guarded)
        graph.AddUnresolvedReference("Ns.A");

        var (nodes, _) = new CsvExporter().Export(graph, _outputDir, _outputDir);

        // Should only have 1 node row (in-scope wins; unresolved duplicate is skipped)
        Assert.Equal(1, nodes);
    }

    // ── CV-14: Inheritance edge gets rel type 'basecompoundref' ───────────────

    [Fact]
    public void Export_InheritanceEdge_GetsBaseCompoundRef()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class Base {}
                public class Child : Base {}
            }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "relationships.csv"));
        Assert.Contains("basecompoundref", content);
    }

    // ── CV-15: Interface implementation edge gets rel type 'basecompoundref' ──

    [Fact]
    public void Export_InterfaceImplementationEdge_GetsBaseCompoundRef()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public interface IService {}
                public class Service : IService {}
            }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "relationships.csv"));
        Assert.Contains("basecompoundref", content);
    }

    // ── CV-16: General dependency edge gets rel type 'ref' ────────────────────

    [Fact]
    public void Export_GeneralDependencyEdge_GetsRef()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class A { public B Field; }
                public class B {}
            }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var content = File.ReadAllText(Path.Combine(_outputDir, "relationships.csv"));
        Assert.Contains(",ref,", content);
    }

    // ── CV-17: Node IDs are 16-char hex and match between nodes and rels ──────

    [Fact]
    public void Export_NodeIdsInRelsMatchNodesCsv()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns {
                public class A { public B Field; }
                public class B {}
            }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var nodeLines = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        var relLines  = File.ReadAllLines(Path.Combine(_outputDir, "relationships.csv"));

        // Collect all IDs from nodes
        var nodeIds = nodeLines.Skip(1)
            .Select(l => SplitCsvLine(l)[0])
            .ToHashSet();

        // All start/end IDs in rels must exist in nodeIds
        foreach (var relLine in relLines.Skip(1))
        {
            var fields = SplitCsvLine(relLine);
            Assert.Contains(fields[0], nodeIds); // :START_ID
            Assert.Contains(fields[1], nodeIds); // :END_ID
        }
    }

    // ── CV-18: EscapeCsvField handles commas ─────────────────────────────────

    [Theory]
    [InlineData("hello,world",   "\"hello,world\"")]
    [InlineData("say \"hi\"",    "\"say \"\"hi\"\"\"")]
    [InlineData("line\nbreak",   "\"line\nbreak\"")]
    [InlineData("plain",         "plain")]
    public void EscapeCsvField_HandlesSpecialChars(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.EscapeCsvField(input));
    }

    // ── CV-19: Interface type label is 'interface' ────────────────────────────

    [Fact]
    public void Export_InterfaceType_HasInterfaceLabel()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public interface IService {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines  = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal("interface", fields[2]); // type column
        Assert.Equal("interface", fields[3]); // :LABEL column
    }

    // ── CV-20: Struct type label is 'struct' ──────────────────────────────────

    [Fact]
    public void Export_StructType_HasStructLabel()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public struct Point {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines  = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal("struct", fields[2]);
    }

    // ── CV-21: Enum type label is 'enum' ──────────────────────────────────────

    [Fact]
    public void Export_EnumType_HasEnumLabel()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public enum Status { Active, Inactive } }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        var lines  = File.ReadAllLines(Path.Combine(_outputDir, "nodes.csv"));
        var fields = SplitCsvLine(lines[1]);
        Assert.Equal("enum", fields[2]);
    }

    // ── CV-22: File is UTF-8 without BOM ──────────────────────────────────────

    [Fact]
    public void Export_OutputFiles_AreUtf8WithoutBom()
    {
        var graph = TestHelper.BuildGraph("""
            namespace Ns { public class A {} }
            """);

        new CsvExporter().Export(graph, _outputDir, _outputDir);

        // Read raw bytes and check no UTF-8 BOM (EF BB BF)
        CheckNoBom(Path.Combine(_outputDir, "nodes.csv"));
        CheckNoBom(Path.Combine(_outputDir, "relationships.csv"));
    }

    // ── CV-23: End-to-end with SampleCodebase produces non-empty output ────────

    [Fact]
    public void Export_SampleCodebase_ProducesNonEmptyOutput()
    {
        var sampleDir = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "samples", "SampleCodebase");
        sampleDir = Path.GetFullPath(sampleDir);

        if (!Directory.Exists(sampleDir))
        {
            // Skip gracefully if sample not present in this environment
            return;
        }

        var fileListPath = Path.Combine(sampleDir, "filelist.txt");
        if (!File.Exists(fileListPath)) return;

        var sourceFiles = File.ReadAllLines(fileListPath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith('#'))
            .Select(l => Path.IsPathRooted(l) ? l : Path.Combine(sampleDir, l))
            .ToList();

        var graph = TestHelper.BuildGraph(sourceFiles.Select(File.ReadAllText).ToArray());

        var (nodes, rels) = new CsvExporter().Export(graph, _outputDir, sampleDir);

        Assert.True(nodes > 0, "Expected at least one node");
        Assert.True(rels > 0,  "Expected at least one relationship");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void CheckNoBom(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3)
            Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                $"File {path} unexpectedly starts with UTF-8 BOM");
    }

    /// <summary>
    /// Minimal RFC 4180 CSV splitter sufficient for test assertions
    /// (handles quoted fields with embedded commas and doubled quotes).
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        fields.Add(sb.ToString());
        return [.. fields];
    }
}
