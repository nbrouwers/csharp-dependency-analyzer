namespace DependencyAnalyzer.Models;

public sealed record TypeDependency(
    string SourceFqn,
    string TargetFqn,
    string DependencyReason);
