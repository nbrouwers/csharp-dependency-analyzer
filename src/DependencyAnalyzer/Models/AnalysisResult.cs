namespace DependencyAnalyzer.Models;

public sealed class AnalysisResult
{
    public required string TargetFqn { get; init; }
    public required IReadOnlyList<FanInElement> FanInElements { get; init; }
    public required int MaxTransitiveDepth { get; init; }
    public required IReadOnlyList<(string SourceFqn, string TargetFqn)> FanInEdges { get; init; }

    public IReadOnlyDictionary<ElementKind, int> MetricsByKind =>
        FanInElements
            .GroupBy(e => e.Kind)
            .ToDictionary(g => g.Key, g => g.Count());

    public int TotalFanInCount => FanInElements.Count;
}
