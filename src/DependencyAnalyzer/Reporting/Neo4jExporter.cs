using Neo4j.Driver;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Imports a <see cref="DependencyGraph"/> directly into a running Neo4j database
/// using the Bolt protocol, with no intermediate files produced.
///
/// The graph schema matches the CSV export exactly (same node IDs, labels, properties,
/// and relationship types), so graphs produced via direct import and via
/// <c>neo4j-admin import</c> from <see cref="CsvExporter"/> output are identical:
///
///   Nodes  (:<em>typeLabel</em> {id, name, type, label, file, startLine, endLine,
///                                accessibility, fullyqualifiedname, resolved})
///     id                — SHA-256[..16] hex derived from fqn + typeLabel
///     name              — simple (unqualified) type name
///     type / label      — "class", "interface", "struct", or "enum" (records → class;
///                         delegates → interface)
///     file              — source file path relativized to the project root
///     startLine/endLine — 1-based declaration line range (0 if unavailable)
///     accessibility     — "public", "internal", "protected", "private", etc.
///     fullyqualifiedname — original .NET fully qualified name
///     resolved          — true for in-scope types; false for unresolved references
///
///   Relationships  (no namespace compounds; no :INNERCLASS edges)
///     -[:`basecompoundref` {startLine, endLine}]->
///         Inheritance and interface-implementation edges.
///
///     -[:`ref` {startLine, endLine}]->
///         All other dependency edges (field, method param, object creation, etc.).
///
/// All node writes use MERGE (keyed on <c>id</c>), making the import idempotent.
/// Before any writes, the database is cleared with <c>MATCH (n) DETACH DELETE n</c>
/// so that the database contains exactly the exported graph — no stale data from
/// previous runs is retained.
/// </summary>
public sealed class Neo4jExporter
{
    // ---------------------------------------------------------------------------
    // Cypher query builders — the label/reltype tokens come from CsvIdHelper
    // (controlled enum→string mapping), so string interpolation is safe here.
    // All property values travel through parameterised $param bindings.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Cypher statement that removes every node (and all attached relationships)
    /// from the target database before the import writes begin. This ensures the
    /// database contains exactly the exported graph — no stale data is retained.
    /// </summary>
    public const string ClearDatabaseQuery = "MATCH (n) DETACH DELETE n";

    private static string NodeMergeQuery(string typeLabel) =>
        $"MERGE (n:`{typeLabel}` {{id: $id}}) " +
        "SET n.name = $name, n.type = $type, n.label = $label, " +
        "n.file = $file, n.startLine = $startLine, n.endLine = $endLine, " +
        "n.accessibility = $accessibility, n.fullyqualifiedname = $fullyqualifiedname, " +
        "n.resolved = $resolved";

    private static string EdgeMergeQuery(string relType) =>
        "MATCH (a {id: $sourceId}), (b {id: $targetId}) " +
        $"MERGE (a)-[r:`{relType}`]->(b) " +
        "SET r.startLine = $startLine, r.endLine = $endLine";

    // ---------------------------------------------------------------------------
    // Public entry point
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Connects to Neo4j, imports the graph and returns
    /// (nodes written, relationships written).
    /// </summary>
    public async Task<(int NodesWritten, int RelationshipsWritten)> ExportAsync(
        DependencyGraph graph,
        string uri,
        string user,
        string password,
        string database)
    {
        await using var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
        await driver.VerifyConnectivityAsync();

        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        // --- 0. Clear the database so the result contains exactly the exported graph ---
        await session.ExecuteWriteAsync(tx => tx.RunAsync(ClearDatabaseQuery));

        // --- 1. Write in-scope type nodes + unresolved reference nodes ---
        var unresolvedToWrite = graph.UnresolvedReferences
            .Where(fqn => !graph.ElementKinds.ContainsKey(fqn))
            .ToList();

        int nodesWritten = graph.ElementKinds.Count + unresolvedToWrite.Count;

        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var (fqn, kind) in graph.ElementKinds)
            {
                graph.TypeLocations.TryGetValue(fqn, out var loc);
                var p = BuildNodeParameters(fqn, kind, loc);
                await tx.RunAsync(NodeMergeQuery(CsvIdHelper.ToTypeLabel(kind)), p);
            }

            foreach (var fqn in unresolvedToWrite)
            {
                var p = BuildUnresolvedNodeParameters(fqn);
                await tx.RunAsync(NodeMergeQuery("class"), p);
            }
        });

        // --- 2. Write basecompoundref and ref relationships ---
        var edgeOps = CollectEdgeOperations(graph);
        int relsWritten = edgeOps.Count;

        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var op in edgeOps)
                await tx.RunAsync(EdgeMergeQuery(op.RelationshipType), op.Parameters);
        });

        return (nodesWritten, relsWritten);
    }

    // ---------------------------------------------------------------------------
    // Public helpers — testable without a live Neo4j connection
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds the Cypher parameter map for an in-scope type node MERGE.
    /// Property names and values match the CSV export columns exactly.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildNodeParameters(
        string fqn,
        ElementKind kind,
        TypeLocation? loc = null)
    {
        var label = CsvIdHelper.ToTypeLabel(kind);
        return new Dictionary<string, object>
        {
            ["id"]                 = CsvIdHelper.ToNodeId(fqn, label),
            ["name"]               = SimpleName(fqn),
            ["type"]               = label,
            ["label"]              = label,
            ["file"]               = loc?.FilePath ?? "",
            ["startLine"]          = loc?.StartLine ?? 0,
            ["endLine"]            = loc?.EndLine ?? 0,
            ["accessibility"]      = loc?.Accessibility ?? "",
            ["fullyqualifiedname"] = fqn,
            ["resolved"]           = true
        };
    }

    /// <summary>
    /// Builds the Cypher parameter map for an unresolved reference node.
    /// <c>resolved</c> is <see langword="false"/>; all location fields are empty / 0.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildUnresolvedNodeParameters(string fqn) =>
        new Dictionary<string, object>
        {
            ["id"]                 = CsvIdHelper.ToNodeId(fqn, "class"),
            ["name"]               = SimpleName(fqn),
            ["type"]               = "class",
            ["label"]              = "class",
            ["file"]               = "",
            ["startLine"]          = 0,
            ["endLine"]            = 0,
            ["accessibility"]      = "",
            ["fullyqualifiedname"] = fqn,
            ["resolved"]           = false
        };

    /// <summary>
    /// Translates in-scope dependency edges to <c>:basecompoundref</c> and
    /// <c>:ref</c> edge operations. Edges with out-of-scope source or target FQNs
    /// are silently skipped.
    /// </summary>
    public static IReadOnlyList<EdgeOperation> CollectEdgeOperations(DependencyGraph graph)
    {
        var ops = new List<EdgeOperation>();

        foreach (var (_, edges) in graph.Edges)
        {
            foreach (var edge in edges)
            {
                if (!graph.ElementKinds.TryGetValue(edge.SourceFqn, out var srcKind))
                    continue;
                if (!graph.ElementKinds.TryGetValue(edge.TargetFqn, out var tgtKind))
                    continue;

                var sourceId = CsvIdHelper.ToNodeId(edge.SourceFqn, CsvIdHelper.ToTypeLabel(srcKind));
                var targetId = CsvIdHelper.ToNodeId(edge.TargetFqn, CsvIdHelper.ToTypeLabel(tgtKind));
                var relType  = CsvIdHelper.ToRelationshipType(edge.DependencyReason);

                graph.EdgeLocations.TryGetValue(edge, out var edgeLoc);
                var startLine = edgeLoc?.StartLine ?? 0;
                var endLine   = edgeLoc?.EndLine   ?? 0;

                ops.Add(new EdgeOperation(
                    sourceId, targetId, relType,
                    new Dictionary<string, object>
                    {
                        ["sourceId"]  = sourceId,
                        ["targetId"]  = targetId,
                        ["startLine"] = startLine,
                        ["endLine"]   = endLine
                    }));
            }
        }

        return ops;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static string SimpleName(string fqn)
    {
        var dot = fqn.LastIndexOf('.');
        return dot >= 0 ? fqn[(dot + 1)..] : fqn;
    }
}

/// <summary>
/// A resolved relationship write operation ready to be sent to Neo4j.
/// </summary>
public sealed record EdgeOperation(
    string SourceId,
    string TargetId,
    /// <summary>"basecompoundref" or "ref" — matches the CSV :TYPE column.</summary>
    string RelationshipType,
    IReadOnlyDictionary<string, object> Parameters);
