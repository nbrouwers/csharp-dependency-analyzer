namespace DependencyAnalyzer.Models;

/// <summary>
/// Source location of a dependency edge (call-site / reference).
/// Stored in <see cref="DependencyGraph.EdgeLocations"/> keyed by <see cref="TypeDependency"/>.
/// </summary>
public sealed record DependencyLocation(
    /// <summary>1-based line the reference begins on.</summary>
    int StartLine,
    /// <summary>1-based line the reference ends on (same line for most single-token sites).</summary>
    int EndLine);
