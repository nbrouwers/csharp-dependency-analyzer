namespace DependencyAnalyzer.Models;

/// <summary>
/// Classifies a <see cref="TypeDependency.DependencyReason"/> string into one of the
/// structural categories that Doxygen's compound XML schema expresses natively.
/// </summary>
public enum DoxygenEdgeKind
{
    /// <summary>Class inherits from another class.</summary>
    Inheritance,

    /// <summary>Class or struct implements an interface.</summary>
    InterfaceImplementation,

    /// <summary>Any other usage dependency (field, parameter, object creation, etc.).</summary>
    Usage
}
