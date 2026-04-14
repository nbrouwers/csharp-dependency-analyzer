namespace DependencyAnalyzer.Models;

/// <summary>
/// Provides deterministic Doxygen identifier generation and edge/kind conversion helpers.
/// All members are pure functions with no side effects.
/// </summary>
public static class DoxygenRefIdHelper
{
    // The two DependencyReason strings produced by DependencyVisitor for structural edges.
    private const string ReasonInherits = "Inherits from";
    private const string ReasonImplements = "Implements interface";

    /// <summary>
    /// Converts a fully qualified C# name and its <see cref="ElementKind"/> into a
    /// Doxygen <c>refid</c> attribute value using the standard Doxygen convention:
    /// <c>{kindPrefix}{fqn-with-dots-as-_1_1}</c>.
    /// </summary>
    /// <example>
    /// <c>ToRefId("Acme.Core.OrderService", ElementKind.Class)</c>
    /// returns <c>"classAcme_1_1Core_1_1OrderService"</c>.
    /// </example>
    public static string ToRefId(string fqn, ElementKind kind)
    {
        var prefix = ToKindPrefix(kind);
        var encoded = EncodeFqn(fqn);
        return $"{prefix}{encoded}";
    }

    /// <summary>
    /// Returns the Doxygen <c>kind</c> attribute string for a <see cref="ElementKind"/>.
    /// </summary>
    public static string ToDoxygenKind(ElementKind kind) => kind switch
    {
        ElementKind.Interface => "interface",
        ElementKind.Struct    => "struct",
        ElementKind.Enum      => "enum",
        _                     => "class"   // Class, Record, Delegate
    };

    /// <summary>
    /// Converts a fully qualified C# name to a Doxygen <c>&lt;compoundname&gt;</c> value
    /// by replacing <c>.</c> separators with <c>::</c>.
    /// </summary>
    public static string ToCompoundName(string fqn) => fqn.Replace(".", "::");

    /// <summary>
    /// Returns all unique namespace strings implied by the collection of fully qualified
    /// type names — every dot-delimited prefix of length ≥ 1.
    /// </summary>
    public static IEnumerable<string> ExtractNamespaces(IEnumerable<string> fqns)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var fqn in fqns)
        {
            var parts = fqn.Split('.');
            for (int i = 1; i < parts.Length; i++)
            {
                namespaces.Add(string.Join(".", parts[..i]));
            }
        }
        return namespaces;
    }

    /// <summary>
    /// Returns the Doxygen <c>refid</c> for a namespace string.
    /// </summary>
    public static string NamespaceRefId(string ns) =>
        $"namespace{EncodeFqn(ns)}";

    /// <summary>
    /// Classifies a <see cref="TypeDependency.DependencyReason"/> string into a
    /// <see cref="DoxygenEdgeKind"/>, mapping the two structural reasons produced by
    /// <c>DependencyVisitor</c> to their first-class Doxygen XML elements and everything
    /// else to <see cref="DoxygenEdgeKind.Usage"/>.
    /// </summary>
    public static DoxygenEdgeKind ClassifyEdge(string dependencyReason) =>
        dependencyReason switch
        {
            ReasonInherits   => DoxygenEdgeKind.Inheritance,
            ReasonImplements => DoxygenEdgeKind.InterfaceImplementation,
            _                => DoxygenEdgeKind.Usage
        };

    // --- private helpers ---

    private static string ToKindPrefix(ElementKind kind) => kind switch
    {
        ElementKind.Interface => "interface",
        ElementKind.Struct    => "struct",
        ElementKind.Enum      => "enum",
        _                     => "class"
    };

    /// <summary>
    /// Encodes a fully qualified name for use inside a Doxygen <c>refid</c>:
    /// both <c>.</c> and <c>::</c> are replaced by <c>_1_1</c>.
    /// Generic type arguments (<c>&lt;</c>, <c>&gt;</c>, <c>,</c>, space) are
    /// encoded using Doxygen's standard template-argument encoding so the result
    /// is valid as a file name on all platforms.
    /// </summary>
    private static string EncodeFqn(string fqn) =>
        fqn.Replace("::", "_1_1")
           .Replace(".", "_1_1")
           .Replace("<", "_3_0")
           .Replace(">", "_3_1")
           .Replace(",", "_00")
           .Replace(" ", "_01");
}
