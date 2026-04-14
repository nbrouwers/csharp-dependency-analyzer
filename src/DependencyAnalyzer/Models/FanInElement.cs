namespace DependencyAnalyzer.Models;

public sealed record FanInElement(
    string FullyQualifiedName,
    ElementKind Kind,
    string Justification);
