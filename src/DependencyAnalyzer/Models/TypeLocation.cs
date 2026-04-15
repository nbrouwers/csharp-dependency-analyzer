namespace DependencyAnalyzer.Models;

/// <summary>
/// Source location and accessibility of a type declaration.
/// Captured during graph build and stored in <see cref="DependencyGraph.TypeLocations"/>.
/// </summary>
public sealed record TypeLocation(
    /// <summary>Absolute path at build time; relativized at export time.</summary>
    string FilePath,
    /// <summary>1-based line of the opening type declaration keyword.</summary>
    int StartLine,
    /// <summary>1-based line of the closing brace.</summary>
    int EndLine,
    /// <summary>"public", "internal", "protected", "private", or "protected internal".</summary>
    string Accessibility);
