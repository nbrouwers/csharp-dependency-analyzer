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
}
