using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Analysis;

/// <summary>
/// Computes the transitive fan-in of a target type from a DependencyGraph
/// using BFS on the reversed dependency edges.
/// </summary>
public sealed class TransitiveFanInAnalyzer
{
    /// <summary>
    /// Returns an AnalysisResult containing all elements that are (transitively) fan-in to the target.
    /// </summary>
    public AnalysisResult Analyze(string targetFqn, DependencyGraph graph)
    {
        // Build reverse graph: for each edge A→B, record A as a dependent of B
        var reverseGraph = new Dictionary<string, List<(string Dependor, string Reason)>>();

        foreach (var (_, edges) in graph.Edges)
        {
            foreach (var edge in edges)
            {
                if (!reverseGraph.TryGetValue(edge.TargetFqn, out var list))
                {
                    list = new List<(string, string)>();
                    reverseGraph[edge.TargetFqn] = list;
                }

                list.Add((edge.SourceFqn, edge.DependencyReason));
            }
        }

        // BFS from target on reverse graph
        var visited = new Dictionary<string, string>(); // FQN → justification
        var depth = new Dictionary<string, int>(); // FQN → depth (1 = direct)
        var fanInEdges = new List<(string SourceFqn, string TargetFqn)>();
        var queue = new Queue<(string Fqn, int Depth)>();
        queue.Enqueue((targetFqn, 0));

        while (queue.Count > 0)
        {
            var (current, currentDepth) = queue.Dequeue();

            if (!reverseGraph.TryGetValue(current, out var dependors))
                continue;

            // Deduplicate dependors for this node
            var seen = new HashSet<string>();
            foreach (var (dependor, reason) in dependors)
            {
                if (!seen.Add(dependor))
                    continue;

                if (dependor == targetFqn || visited.ContainsKey(dependor))
                    continue;

                var justification = BuildJustification(dependor, current, reason, targetFqn);
                visited[dependor] = justification;
                depth[dependor] = currentDepth + 1;
                fanInEdges.Add((dependor, current));
                queue.Enqueue((dependor, currentDepth + 1));
            }
        }

        // Build result
        var fanInElements = visited
            .Select(kvp => new FanInElement(
                FullyQualifiedName: kvp.Key,
                Kind: graph.ElementKinds.TryGetValue(kvp.Key, out var kind)
                    ? kind
                    : ElementKind.Class,
                Justification: kvp.Value))
            .OrderBy(e => e.FullyQualifiedName)
            .ToList();

        return new AnalysisResult
        {
            TargetFqn = targetFqn,
            FanInElements = fanInElements,
            MaxTransitiveDepth = depth.Count > 0 ? depth.Values.Max() : 0,
            FanInEdges = fanInEdges
        };
    }

    private static string BuildJustification(
        string dependor, string dependedUpon, string reason, string targetFqn)
    {
        var shortDependor = GetShortName(dependor);
        var shortDependedUpon = GetShortName(dependedUpon);

        if (dependedUpon == targetFqn)
        {
            return $"{reason} `{shortDependedUpon}`";
        }

        return $"{reason} `{shortDependedUpon}` (which is a transitive fan-in of `{GetShortName(targetFqn)}`)";
    }

    private static string GetShortName(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot >= 0 ? fqn[(lastDot + 1)..] : fqn;
    }
}
