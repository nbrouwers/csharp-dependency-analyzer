namespace DependencyAnalyzer.Models;

/// <summary>
/// Holds the full dependency graph: edges and element kinds for all in-scope types.
/// </summary>
public sealed class DependencyGraph
{
    /// <summary>
    /// Adjacency list: source FQN → list of dependency edges originating from that source.
    /// </summary>
    public Dictionary<string, List<TypeDependency>> Edges { get; } = new();

    /// <summary>
    /// Element kind per FQN for all discovered types.
    /// </summary>
    public Dictionary<string, ElementKind> ElementKinds { get; } = new();

    /// <summary>
    /// Source file location and accessibility per FQN for all in-scope types.
    /// </summary>
    public Dictionary<string, TypeLocation> TypeLocations { get; } = new();

    /// <summary>
    /// Source location of the reference site for each dependency edge (best-effort; not all edges have a site).
    /// </summary>
    public Dictionary<TypeDependency, DependencyLocation> EdgeLocations { get; } = new();

    /// <summary>
    /// FQNs that were referenced by in-scope code but could not be resolved to an in-scope type.
    /// Only populated for FQNs that share a namespace root with at least one in-scope type.
    /// </summary>
    public HashSet<string> UnresolvedReferences { get; } = new();

    public void AddEdge(TypeDependency dependency)
    {
        if (!Edges.TryGetValue(dependency.SourceFqn, out var list))
        {
            list = new List<TypeDependency>();
            Edges[dependency.SourceFqn] = list;
        }
        list.Add(dependency);
    }

    public void SetElementKind(string fqn, ElementKind kind)
    {
        ElementKinds[fqn] = kind;
    }

    public void SetTypeLocation(string fqn, TypeLocation location)
    {
        TypeLocations[fqn] = location;
    }

    public void SetEdgeLocation(TypeDependency edge, DependencyLocation location)
    {
        EdgeLocations[edge] = location;
    }

    public void AddUnresolvedReference(string fqn)
    {
        UnresolvedReferences.Add(fqn);
    }

    /// <summary>
    /// SyntaxKind names (from <c>SyntaxKind.ToString()</c>) for syntax node types encountered
    /// during the walk that have no explicit <c>Visit*</c> override in <c>DependencyVisitor</c>.
    /// Aggregated across all syntax trees in the compilation. Populated as a diagnostic to
    /// surface potential coverage gaps in the visitor.
    /// </summary>
    public HashSet<string> UnvisitedNodeKinds { get; } = new();

    public void AddUnvisitedNodeKind(string kindName)
    {
        UnvisitedNodeKinds.Add(kindName);
    }
}
