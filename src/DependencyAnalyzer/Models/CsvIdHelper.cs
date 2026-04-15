using System.Security.Cryptography;
using System.Text;

namespace DependencyAnalyzer.Models;

/// <summary>
/// Generates stable, deterministic identifiers and labels for CSV export.
/// </summary>
public static class CsvIdHelper
{
    /// <summary>
    /// Produces a stable 16-character hex node ID from a type's FQN and label.
    /// Uses the first 8 bytes (16 hex chars) of SHA-256(<paramref name="fqn"/>:<paramref name="typeLabel"/>).
    /// </summary>
    public static string ToNodeId(string fqn, string typeLabel)
    {
        var input = Encoding.UTF8.GetBytes($"{fqn}:{typeLabel}");
        var hash  = SHA256.HashData(input);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Maps an <see cref="ElementKind"/> to the Doxygen-compatible type label used in the CSV.
    /// </summary>
    public static string ToTypeLabel(ElementKind kind) => kind switch
    {
        ElementKind.Interface => "interface",
        ElementKind.Struct    => "struct",
        ElementKind.Enum      => "enum",
        ElementKind.Delegate  => "interface",   // delegates map to interface in Doxygen XML
        _                     => "class"         // Class, Record, and unknown → class
    };

    /// <summary>
    /// Maps a dependency reason string to the Doxygen-compatible relationship type
    /// used in the relationships CSV.
    /// </summary>
    public static string ToRelationshipType(string dependencyReason) => dependencyReason switch
    {
        "Inherits from"         => "basecompoundref",
        "Implements interface"  => "basecompoundref",
        _                       => "ref"
    };
}
