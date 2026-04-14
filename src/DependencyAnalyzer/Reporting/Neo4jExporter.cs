using Neo4j.Driver;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Imports a <see cref="DependencyGraph"/> directly into a running Neo4j database
/// using the Bolt protocol, with no intermediate files produced.
///
/// Graph model:
///   Nodes:         (:Type {fqn, name, kind, namespace})
///   Relationships: -[:INHERITS_FROM]->
///                  -[:IMPLEMENTS]->
///                  -[:DEPENDS_ON {reason}]->
///
/// All write operations use MERGE, making the import fully idempotent.
/// </summary>
public sealed class Neo4jExporter
{
    // ---------------------------------------------------------------------------
    // Cypher queries — parameterised; never interpolated with user data
    // ---------------------------------------------------------------------------

    private const string MergeNodeQuery =
        "MERGE (n:Type {fqn: $fqn}) " +
        "SET n.name = $name, n.kind = $kind, n.namespace = $namespace";

    private const string MergeInheritsFromQuery =
        "MATCH (a:Type {fqn: $sourceFqn}), (b:Type {fqn: $targetFqn}) " +
        "MERGE (a)-[:INHERITS_FROM]->(b)";

    private const string MergeImplementsQuery =
        "MATCH (a:Type {fqn: $sourceFqn}), (b:Type {fqn: $targetFqn}) " +
        "MERGE (a)-[:IMPLEMENTS]->(b)";

    private const string MergeDependsOnQuery =
        "MATCH (a:Type {fqn: $sourceFqn}), (b:Type {fqn: $targetFqn}) " +
        "MERGE (a)-[r:DEPENDS_ON {reason: $reason}]->(b)";

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

        // --- 1. Write all nodes (one batched transaction) ---
        int nodesWritten = graph.ElementKinds.Count;
        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var (fqn, kind) in graph.ElementKinds)
                await tx.RunAsync(MergeNodeQuery, BuildNodeParameters(fqn, kind));
        });

        // --- 2. Collect edges linking only in-scope types ---
        var edgeOps = CollectEdgeOperations(graph);
        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var op in edgeOps)
                await tx.RunAsync(op.Query, op.Parameters);
        });

        return (nodesWritten, edgeOps.Count);
    }

    // ---------------------------------------------------------------------------
    // Internal helpers — exposed for unit testing without a live server
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Extracts the unqualified (short) name: last segment after the last dot.
    /// </summary>
    public static string ExtractShortName(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot < 0 ? fqn : fqn[(lastDot + 1)..];
    }

    /// <summary>
    /// Extracts the namespace: everything before the last dot (empty string if none).
    /// </summary>
    public static string ExtractNamespace(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot < 0 ? string.Empty : fqn[..lastDot];
    }

    /// <summary>
    /// Maps a <see cref="DoxygenEdgeKind"/> to the Neo4j relationship type string.
    /// </summary>
    public static string GetRelationshipType(DoxygenEdgeKind edgeKind) => edgeKind switch
    {
        DoxygenEdgeKind.Inheritance => "INHERITS_FROM",
        DoxygenEdgeKind.InterfaceImplementation => "IMPLEMENTS",
        _ => "DEPENDS_ON"
    };

    /// <summary>
    /// Builds the Cypher parameter map for a Type node MERGE.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildNodeParameters(string fqn, ElementKind kind) =>
        new Dictionary<string, object>
        {
            ["fqn"] = fqn,
            ["name"] = ExtractShortName(fqn),
            ["kind"] = kind.ToString(),
            ["namespace"] = ExtractNamespace(fqn)
        };

    /// <summary>
    /// Translates all in-scope dependency edges to a flat list of query operations.
    /// Edges whose target FQN is not in scope are silently skipped.
    /// Exposed for testing without a live Neo4j connection.
    /// </summary>
    public static IReadOnlyList<EdgeOperation> CollectEdgeOperations(DependencyGraph graph)
    {
        var ops = new List<EdgeOperation>();

        foreach (var (_, edges) in graph.Edges)
        {
            foreach (var edge in edges)
            {
                if (!graph.ElementKinds.ContainsKey(edge.TargetFqn))
                    continue;

                var edgeKind = DoxygenRefIdHelper.ClassifyEdge(edge.DependencyReason);
                var relType = GetRelationshipType(edgeKind);

                var query = edgeKind switch
                {
                    DoxygenEdgeKind.Inheritance => MergeInheritsFromQuery,
                    DoxygenEdgeKind.InterfaceImplementation => MergeImplementsQuery,
                    _ => MergeDependsOnQuery
                };

                var parameters = new Dictionary<string, object>
                {
                    ["sourceFqn"] = edge.SourceFqn,
                    ["targetFqn"] = edge.TargetFqn,
                    ["reason"] = edge.DependencyReason
                };

                ops.Add(new EdgeOperation(edge.SourceFqn, edge.TargetFqn, relType, edge.DependencyReason, query, parameters));
            }
        }

        return ops;
    }
}

/// <summary>
/// A resolved edge write operation ready to be sent to Neo4j.
/// </summary>
public sealed record EdgeOperation(
    string SourceFqn,
    string TargetFqn,
    string RelationshipType,
    string Reason,
    string Query,
    IReadOnlyDictionary<string, object> Parameters);
