using Neo4j.Driver;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Imports a <see cref="DependencyGraph"/> directly into a running Neo4j database
/// using the Bolt protocol, with no intermediate files produced.
///
/// The graph model mirrors the Doxygen compound schema (compound.xsd version 1.9.1):
///
///   Nodes  (:Compound {id, kind, name, fqn, language})
///     id       — Doxygen refid (deterministic from FQN + ElementKind)
///     kind     — Doxygen kind string: "class", "interface", "struct", "enum", "namespace"
///     name     — compound name using "::" separators  (mirrors &lt;compoundname&gt;)
///     fqn      — original .NET fully qualified name
///     language — always "C#"
///
///   Relationships
///     -[:BASECOMPOUNDREF {prot: "public", virt: "non-virtual"|"virtual"}]->
///         Mirrors &lt;basecompoundref&gt;: virt="non-virtual" for inheritance,
///         virt="virtual" for interface implementation.
///
///     -[:REFERENCES {kind: "variable", reason: "..."}]->
///         Mirrors &lt;memberdef kind="variable"&gt;&lt;references&gt;:
///         used for all other dependency edges (field types, method params, etc.).
///
///     -[:INNERCLASS {prot: "public"}]->
///         Mirrors &lt;innerclass&gt;: edges from namespace compounds to the
///         types they contain.
///
/// All write operations use MERGE, making the import fully idempotent.
/// </summary>
public sealed class Neo4jExporter
{
    // ---------------------------------------------------------------------------
    // Cypher queries — parameterised; never interpolated with user data
    // ---------------------------------------------------------------------------

    // MERGE keyed on the unique Doxygen refid ("id")
    private const string MergeNodeQuery =
        "MERGE (n:Compound {id: $id}) " +
        "SET n.kind = $kind, n.name = $name, n.fqn = $fqn, n.language = $language";

    // Mirrors <basecompoundref prot="..." virt="..."> in Doxygen XML
    private const string MergeBaseCompoundRefQuery =
        "MATCH (a:Compound {id: $sourceId}), (b:Compound {id: $targetId}) " +
        "MERGE (a)-[:BASECOMPOUNDREF {prot: $prot, virt: $virt}]->(b)";

    // Mirrors <memberdef kind="variable"><references refid="..."> in Doxygen XML
    private const string MergeReferencesQuery =
        "MATCH (a:Compound {id: $sourceId}), (b:Compound {id: $targetId}) " +
        "MERGE (a)-[:REFERENCES {kind: \"variable\", reason: $reason}]->(b)";

    // Mirrors <innerclass refid="..." prot="public"> in Doxygen XML
    private const string MergeInnerClassQuery =
        "MATCH (ns:Compound {id: $nsId}), (t:Compound {id: $typeId}) " +
        "MERGE (ns)-[:INNERCLASS {prot: \"public\"}]->(t)";

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

        var namespaceFqns = DoxygenRefIdHelper
            .ExtractNamespaces(graph.ElementKinds.Keys)
            .ToList();

        // --- 1. Write type nodes and namespace compound nodes ---
        int nodesWritten = graph.ElementKinds.Count + namespaceFqns.Count;
        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var (fqn, kind) in graph.ElementKinds)
                await tx.RunAsync(MergeNodeQuery, BuildNodeParameters(fqn, kind));
            foreach (var ns in namespaceFqns)
                await tx.RunAsync(MergeNodeQuery, BuildNamespaceParameters(ns));
        });

        // --- 2. Write BASECOMPOUNDREF, REFERENCES, and INNERCLASS relationships ---
        var edgeOps = CollectEdgeOperations(graph);
        var innerClassOps = CollectInnerClassOperations(graph, namespaceFqns);
        int relsWritten = edgeOps.Count + innerClassOps.Count;

        await session.ExecuteWriteAsync(async tx =>
        {
            foreach (var op in edgeOps)
                await tx.RunAsync(op.Query, op.Parameters);
            foreach (var op in innerClassOps)
                await tx.RunAsync(op.Query, op.Parameters);
        });

        return (nodesWritten, relsWritten);
    }

    // ---------------------------------------------------------------------------
    // Public helpers — testable without a live Neo4j connection
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds the Cypher parameter map for a type <c>(:Compound)</c> node MERGE.
    /// Properties mirror the Doxygen <c>&lt;compounddef&gt;</c> attributes.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildNodeParameters(string fqn, ElementKind kind) =>
        new Dictionary<string, object>
        {
            ["id"]       = DoxygenRefIdHelper.ToRefId(fqn, kind),
            ["kind"]     = DoxygenRefIdHelper.ToDoxygenKind(kind),
            ["name"]     = DoxygenRefIdHelper.ToCompoundName(fqn),
            ["fqn"]      = fqn,
            ["language"] = "C#"
        };

    /// <summary>
    /// Builds the Cypher parameter map for a namespace <c>(:Compound)</c> node MERGE.
    /// Mirrors the Doxygen <c>&lt;compounddef kind="namespace"&gt;</c> compound.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildNamespaceParameters(string namespaceFqn) =>
        new Dictionary<string, object>
        {
            ["id"]       = DoxygenRefIdHelper.NamespaceRefId(namespaceFqn),
            ["kind"]     = "namespace",
            ["name"]     = DoxygenRefIdHelper.ToCompoundName(namespaceFqn),
            ["fqn"]      = namespaceFqn,
            ["language"] = "C#"
        };

    /// <summary>
    /// Returns the Doxygen <c>virt</c> attribute value for a structural edge:
    /// <c>"virtual"</c> for interface implementation, <c>"non-virtual"</c> for
    /// inheritance. Mirrors the <c>virt</c> attribute on <c>&lt;basecompoundref&gt;</c>.
    /// </summary>
    public static string GetVirt(DoxygenEdgeKind edgeKind) =>
        edgeKind == DoxygenEdgeKind.InterfaceImplementation ? "virtual" : "non-virtual";

    /// <summary>
    /// Translates in-scope dependency edges to <c>:BASECOMPOUNDREF</c> and
    /// <c>:REFERENCES</c> edge operations. Edges with out-of-scope source or
    /// target FQNs are silently skipped.
    /// </summary>
    public static IReadOnlyList<EdgeOperation> CollectEdgeOperations(DependencyGraph graph)
    {
        var ops = new List<EdgeOperation>();

        foreach (var (_, edges) in graph.Edges)
        {
            foreach (var edge in edges)
            {
                if (!graph.ElementKinds.TryGetValue(edge.SourceFqn, out var sourceKind))
                    continue;
                if (!graph.ElementKinds.TryGetValue(edge.TargetFqn, out var targetKind))
                    continue;

                var edgeKind = DoxygenRefIdHelper.ClassifyEdge(edge.DependencyReason);
                var sourceId = DoxygenRefIdHelper.ToRefId(edge.SourceFqn, sourceKind);
                var targetId = DoxygenRefIdHelper.ToRefId(edge.TargetFqn, targetKind);

                string relType;
                string query;
                Dictionary<string, object> parameters;

                if (edgeKind != DoxygenEdgeKind.Usage)
                {
                    // Structural edge → :BASECOMPOUNDREF {prot, virt}
                    relType = "BASECOMPOUNDREF";
                    query = MergeBaseCompoundRefQuery;
                    parameters = new Dictionary<string, object>
                    {
                        ["sourceId"] = sourceId,
                        ["targetId"] = targetId,
                        ["prot"]     = "public",
                        ["virt"]     = GetVirt(edgeKind)
                    };
                }
                else
                {
                    // Usage edge → :REFERENCES {kind: "variable", reason}
                    relType = "REFERENCES";
                    query = MergeReferencesQuery;
                    parameters = new Dictionary<string, object>
                    {
                        ["sourceId"] = sourceId,
                        ["targetId"] = targetId,
                        ["reason"]   = edge.DependencyReason
                    };
                }

                ops.Add(new EdgeOperation(sourceId, targetId, relType, edge.DependencyReason, query, parameters));
            }
        }

        return ops;
    }

    /// <summary>
    /// Produces <c>:INNERCLASS</c> edge operations from each namespace compound
    /// to the types it directly contains — mirroring the Doxygen
    /// <c>&lt;innerclass&gt;</c> element inside a namespace <c>&lt;compounddef&gt;</c>.
    /// </summary>
    public static IReadOnlyList<EdgeOperation> CollectInnerClassOperations(
        DependencyGraph graph,
        IEnumerable<string> namespaceFqns)
    {
        var ops = new List<EdgeOperation>();

        foreach (var ns in namespaceFqns)
        {
            var nsId = DoxygenRefIdHelper.NamespaceRefId(ns);

            foreach (var (fqn, kind) in graph.ElementKinds)
            {
                if (GetDirectNamespace(fqn) != ns)
                    continue;

                var typeId = DoxygenRefIdHelper.ToRefId(fqn, kind);
                ops.Add(new EdgeOperation(
                    nsId, typeId, "INNERCLASS", string.Empty,
                    MergeInnerClassQuery,
                    new Dictionary<string, object>
                    {
                        ["nsId"]   = nsId,
                        ["typeId"] = typeId
                    }));
            }
        }

        return ops;
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    private static string GetDirectNamespace(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot < 0 ? string.Empty : fqn[..lastDot];
    }
}

/// <summary>
/// A resolved relationship write operation ready to be sent to Neo4j.
/// <c>SourceId</c> and <c>TargetId</c> are Doxygen refids.
/// </summary>
public sealed record EdgeOperation(
    string SourceId,
    string TargetId,
    string RelationshipType,
    string Reason,
    string Query,
    IReadOnlyDictionary<string, object> Parameters);
