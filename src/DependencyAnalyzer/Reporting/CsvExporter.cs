using System.Text;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Exports a <see cref="DependencyGraph"/> to a pair of CSV files suitable for
/// import into Neo4j (<c>nodes.csv</c> and <c>relationships.csv</c>).
///
/// Column layouts:
/// <list type="bullet">
/// <item><c>nodes.csv</c>: id:ID, name, type, :LABEL, file, startLine:int, endLine:int,
///   accessibility, fullyqualifiedname, resolved:boolean</item>
/// <item><c>relationships.csv</c>: :START_ID, :END_ID, :TYPE, startLine:int, endLine:int</item>
/// </list>
/// </summary>
public sealed class CsvExporter
{
    private static readonly string[] NodeHeader =
    [
        "id:ID", "name", "type", ":LABEL",
        "file", "startLine:int", "endLine:int",
        "accessibility", "fullyqualifiedname", "resolved:boolean"
    ];

    private static readonly string[] RelHeader =
    [
        ":START_ID", ":END_ID", ":TYPE", "startLine:int", "endLine:int"
    ];

    /// <summary>
    /// Writes <c>nodes.csv</c> and <c>relationships.csv</c> to <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="graph">The dependency graph to export.</param>
    /// <param name="outputDirectory">Destination folder (created if absent).</param>
    /// <param name="projectRoot">
    /// Absolute path used to relativize source file paths in the output.
    /// Typically the directory that contains the file-list used during analysis.
    /// </param>
    /// <returns>A tuple of (nodes written, relationships written).</returns>
    public (int NodesWritten, int RelsWritten) Export(
        DependencyGraph graph,
        string outputDirectory,
        string projectRoot)
    {
        Directory.CreateDirectory(outputDirectory);
        var normalizedRoot = NormalizePath(Path.GetFullPath(projectRoot));

        var nodesPath = Path.Combine(outputDirectory, "nodes.csv");
        var relsPath  = Path.Combine(outputDirectory, "relationships.csv");

        int nodesWritten = 0;
        int relsWritten  = 0;

        // ── nodes.csv ────────────────────────────────────────────────────────
        using (var nodesWriter = new StreamWriter(nodesPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            nodesWriter.WriteLine(string.Join(",", NodeHeader));

            // In-scope resolved types
            foreach (var fqn in graph.ElementKinds.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var kind  = graph.ElementKinds[fqn];
                var label = CsvIdHelper.ToTypeLabel(kind);
                var id    = CsvIdHelper.ToNodeId(fqn, label);
                var name  = SimpleName(fqn);

                graph.TypeLocations.TryGetValue(fqn, out var loc);
                var file        = loc != null ? RelativizePath(normalizedRoot, loc.FilePath) : "";
                var startLine   = loc?.StartLine ?? 0;
                var endLine     = loc?.EndLine   ?? 0;
                var access      = loc?.Accessibility ?? "";

                nodesWriter.WriteLine(BuildNodeRow(id, name, label, label, file, startLine, endLine, access, fqn, resolved: true));
                nodesWritten++;
            }

            // Unresolved references (referenced in code but not found in source)
            foreach (var fqn in graph.UnresolvedReferences.OrderBy(k => k, StringComparer.Ordinal))
            {
                // Skip if somehow also in scope (shouldn't happen but guard anyway)
                if (graph.ElementKinds.ContainsKey(fqn))
                    continue;

                var label = "class";    // unknown kind → default to class
                var id    = CsvIdHelper.ToNodeId(fqn, label);
                var name  = SimpleName(fqn);

                nodesWriter.WriteLine(BuildNodeRow(id, name, label, label, "", 0, 0, "", fqn, resolved: false));
                nodesWritten++;
            }
        }

        // ── relationships.csv ────────────────────────────────────────────────
        using (var relsWriter = new StreamWriter(relsPath, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            relsWriter.WriteLine(string.Join(",", RelHeader));

            // Collect all edges in deterministic order
            foreach (var sourceFqn in graph.Edges.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var edges = graph.Edges[sourceFqn];
                foreach (var dep in edges.OrderBy(e => e.TargetFqn, StringComparer.Ordinal)
                                         .ThenBy(e => e.DependencyReason, StringComparer.Ordinal))
                {
                    var srcKind   = graph.ElementKinds.TryGetValue(dep.SourceFqn, out var sk) ? sk : ElementKind.Class;
                    var tgtKind   = graph.ElementKinds.TryGetValue(dep.TargetFqn, out var tk) ? tk : ElementKind.Class;
                    var srcLabel  = CsvIdHelper.ToTypeLabel(srcKind);
                    var tgtLabel  = CsvIdHelper.ToTypeLabel(tgtKind);
                    var startId   = CsvIdHelper.ToNodeId(dep.SourceFqn, srcLabel);
                    var endId     = CsvIdHelper.ToNodeId(dep.TargetFqn, tgtLabel);
                    var relType   = CsvIdHelper.ToRelationshipType(dep.DependencyReason);

                    graph.EdgeLocations.TryGetValue(dep, out var edgeLoc);
                    var startLine = edgeLoc?.StartLine ?? 0;
                    var endLine   = edgeLoc?.EndLine   ?? 0;

                    relsWriter.WriteLine(BuildRelRow(startId, endId, relType, startLine, endLine));
                    relsWritten++;
                }
            }
        }

        return (nodesWritten, relsWritten);
    }

    // ── Row builders ─────────────────────────────────────────────────────────

    private static string BuildNodeRow(
        string id, string name, string type, string label,
        string file, int startLine, int endLine,
        string accessibility, string fqn, bool resolved)
    {
        return string.Join(",",
            EscapeCsvField(id),
            EscapeCsvField(name),
            EscapeCsvField(type),
            EscapeCsvField(label),
            EscapeCsvField(file),
            startLine.ToString(),
            endLine.ToString(),
            EscapeCsvField(accessibility),
            EscapeCsvField(fqn),
            resolved ? "true" : "false");
    }

    private static string BuildRelRow(
        string startId, string endId, string relType,
        int startLine, int endLine)
    {
        return string.Join(",",
            EscapeCsvField(startId),
            EscapeCsvField(endId),
            EscapeCsvField(relType),
            startLine.ToString(),
            endLine.ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// RFC 4180 CSV field escaping: wraps the value in double-quotes if it contains
    /// a comma, double-quote, or newline; doubles any embedded double-quotes.
    /// </summary>
    public static string EscapeCsvField(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>Returns the simple name (last segment after the last dot), or the full FQN for unqualified names.</summary>
    private static string SimpleName(string fqn)
    {
        var dot = fqn.LastIndexOf('.');
        return dot >= 0 ? fqn[(dot + 1)..] : fqn;
    }

    /// <summary>Normalizes a path to use forward slashes.</summary>
    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimEnd('/');

    /// <summary>
    /// Returns a forward-slash path relative to <paramref name="root"/>.
    /// Falls back to the normalized absolute path if it is not under <paramref name="root"/>.
    /// </summary>
    private static string RelativizePath(string root, string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "";

        var normalized = NormalizePath(Path.GetFullPath(absolutePath));
        if (normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            return normalized[(root.Length + 1)..];

        return normalized;
    }
}
