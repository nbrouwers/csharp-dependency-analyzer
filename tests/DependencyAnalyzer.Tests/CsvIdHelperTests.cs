using DependencyAnalyzer.Models;
using Xunit;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for <see cref="CsvIdHelper"/>.
/// </summary>
public sealed class CsvIdHelperTests
{
    // ── CI-01: ToNodeId produces a 16-char lowercase hex string ──────────────

    [Fact]
    public void ToNodeId_ReturnsSixteenCharLowercaseHex()
    {
        var id = CsvIdHelper.ToNodeId("MyNamespace.MyClass", "class");

        Assert.Equal(16, id.Length);
        Assert.Matches("^[0-9a-f]{16}$", id);
    }

    // ── CI-02: ToNodeId is stable (same input → same output) ─────────────────

    [Fact]
    public void ToNodeId_IsDeterministic()
    {
        var id1 = CsvIdHelper.ToNodeId("MyNamespace.MyClass", "class");
        var id2 = CsvIdHelper.ToNodeId("MyNamespace.MyClass", "class");

        Assert.Equal(id1, id2);
    }

    // ── CI-03: ToNodeId differs when FQN or label differs ────────────────────

    [Theory]
    [InlineData("Ns.A", "class", "Ns.B", "class")]
    [InlineData("Ns.A", "class", "Ns.A", "interface")]
    public void ToNodeId_DiffersForDifferentInputs(string fqn1, string label1, string fqn2, string label2)
    {
        var id1 = CsvIdHelper.ToNodeId(fqn1, label1);
        var id2 = CsvIdHelper.ToNodeId(fqn2, label2);

        Assert.NotEqual(id1, id2);
    }

    // ── CI-04: ToTypeLabel maps ElementKind correctly ─────────────────────────

    [Theory]
    [InlineData(ElementKind.Interface, "interface")]
    [InlineData(ElementKind.Struct,    "struct")]
    [InlineData(ElementKind.Enum,      "enum")]
    [InlineData(ElementKind.Class,     "class")]
    [InlineData(ElementKind.Record,    "class")]   // records → class
    [InlineData(ElementKind.Delegate,  "interface")] // delegates → interface
    public void ToTypeLabel_MapsElementKindCorrectly(ElementKind kind, string expected)
    {
        Assert.Equal(expected, CsvIdHelper.ToTypeLabel(kind));
    }

    // ── CI-05: ToRelationshipType maps dependency reasons correctly ───────────

    [Theory]
    [InlineData("Inherits from",         "basecompoundref")]
    [InlineData("Implements interface",  "basecompoundref")]
    [InlineData("Object creation (new)", "ref")]
    [InlineData("Field type",            "ref")]
    [InlineData("Method return type",    "ref")]
    public void ToRelationshipType_MapsReasonCorrectly(string reason, string expected)
    {
        Assert.Equal(expected, CsvIdHelper.ToRelationshipType(reason));
    }
}
