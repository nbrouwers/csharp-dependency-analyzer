using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for Neo4jExporter helper logic that can be executed without a live Neo4j server.
/// Covers node parameter building, edge operation collection, relationship type mapping,
/// and FQN decomposition.
/// Traces to FR-7.1 – FR-7.7.
/// </summary>
public sealed class Neo4jExporterTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static DependencyGraph BuildGraph()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        graph.SetElementKind("Acme.Core.IOrderRepository", ElementKind.Interface);
        graph.SetElementKind("Acme.Core.OrderStatus", ElementKind.Enum);
        graph.SetElementKind("Acme.Data.OrderEntity", ElementKind.Struct);
        graph.SetElementKind("Acme.Core.OrderRecord", ElementKind.Record);
        graph.SetElementKind("Acme.Core.OrderCreatedHandler", ElementKind.Delegate);

        // Structural edges
        graph.AddEdge(new TypeDependency("Acme.Core.OrderService", "Acme.Core.IOrderRepository", "Implements interface"));
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Inherits from"));

        // Usage edges
        graph.AddEdge(new TypeDependency("Acme.Core.OrderService", "Acme.Core.OrderStatus", "Field type"));
        graph.AddEdge(new TypeDependency("Acme.Core.OrderRecord", "Acme.Core.OrderService", "Constructor parameter type"));

        return graph;
    }

    // =========================================================================
    // NJ-01 – NJ-04 : ExtractShortName
    // =========================================================================

    [Fact] // NJ-01
    public void ExtractShortName_MultiSegment_ReturnsLastSegment()
    {
        Assert.Equal("OrderService", Neo4jExporter.ExtractShortName("Acme.Core.OrderService"));
    }

    [Fact] // NJ-02
    public void ExtractShortName_SingleSegment_ReturnsSelf()
    {
        Assert.Equal("Foo", Neo4jExporter.ExtractShortName("Foo"));
    }

    [Fact] // NJ-03
    public void ExtractShortName_TwoSegments_ReturnsSecond()
    {
        Assert.Equal("Bar", Neo4jExporter.ExtractShortName("Foo.Bar"));
    }

    [Fact] // NJ-04
    public void ExtractShortName_GenericTypeName_TreatsAngleBracketsAsLiteral()
    {
        // Angle brackets are part of the name token; '.' is the separator
        Assert.Equal("IRepository<T>", Neo4jExporter.ExtractShortName("Acme.Core.IRepository<T>"));
    }

    // =========================================================================
    // NJ-05 – NJ-08 : ExtractNamespace
    // =========================================================================

    [Fact] // NJ-05
    public void ExtractNamespace_MultiSegment_ReturnsAllButLast()
    {
        Assert.Equal("Acme.Core", Neo4jExporter.ExtractNamespace("Acme.Core.OrderService"));
    }

    [Fact] // NJ-06
    public void ExtractNamespace_SingleSegment_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, Neo4jExporter.ExtractNamespace("Foo"));
    }

    [Fact] // NJ-07
    public void ExtractNamespace_TwoSegments_ReturnsFirst()
    {
        Assert.Equal("Foo", Neo4jExporter.ExtractNamespace("Foo.Bar"));
    }

    [Fact] // NJ-08
    public void ExtractNamespace_DeepNesting_ReturnsCorrectNamespace()
    {
        Assert.Equal("A.B.C.D", Neo4jExporter.ExtractNamespace("A.B.C.D.MyClass"));
    }

    // =========================================================================
    // NJ-09 – NJ-11 : GetRelationshipType
    // =========================================================================

    [Fact] // NJ-09
    public void GetRelationshipType_Inheritance_ReturnsInheritsFrom()
    {
        Assert.Equal("INHERITS_FROM", Neo4jExporter.GetRelationshipType(DoxygenEdgeKind.Inheritance));
    }

    [Fact] // NJ-10
    public void GetRelationshipType_InterfaceImplementation_ReturnsImplements()
    {
        Assert.Equal("IMPLEMENTS", Neo4jExporter.GetRelationshipType(DoxygenEdgeKind.InterfaceImplementation));
    }

    [Fact] // NJ-11
    public void GetRelationshipType_Usage_ReturnsDependsOn()
    {
        Assert.Equal("DEPENDS_ON", Neo4jExporter.GetRelationshipType(DoxygenEdgeKind.Usage));
    }

    // =========================================================================
    // NJ-12 – NJ-16 : BuildNodeParameters
    // =========================================================================

    [Fact] // NJ-12
    public void BuildNodeParameters_ContainsFqn()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("Acme.Core.OrderService", p["fqn"]);
    }

    [Fact] // NJ-13
    public void BuildNodeParameters_ContainsShortName()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("OrderService", p["name"]);
    }

    [Fact] // NJ-14
    public void BuildNodeParameters_ContainsNamespace()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("Acme.Core", p["namespace"]);
    }

    [Theory] // NJ-15
    [InlineData(ElementKind.Class, "Class")]
    [InlineData(ElementKind.Interface, "Interface")]
    [InlineData(ElementKind.Struct, "Struct")]
    [InlineData(ElementKind.Enum, "Enum")]
    [InlineData(ElementKind.Record, "Record")]
    [InlineData(ElementKind.Delegate, "Delegate")]
    public void BuildNodeParameters_KindMatchesElementKindString(ElementKind kind, string expected)
    {
        var p = Neo4jExporter.BuildNodeParameters("X.Y", kind);
        Assert.Equal(expected, p["kind"]);
    }

    [Fact] // NJ-16
    public void BuildNodeParameters_NoNamespace_NamespaceIsEmpty()
    {
        var p = Neo4jExporter.BuildNodeParameters("MyClass", ElementKind.Class);
        Assert.Equal(string.Empty, p["namespace"]);
        Assert.Equal("MyClass", p["name"]);
        Assert.Equal("MyClass", p["fqn"]);
    }

    // =========================================================================
    // NJ-17 – NJ-22 : CollectEdgeOperations
    // =========================================================================

    [Fact] // NJ-17
    public void CollectEdgeOperations_EmptyGraph_ReturnsEmpty()
    {
        var ops = Neo4jExporter.CollectEdgeOperations(new DependencyGraph());
        Assert.Empty(ops);
    }

    [Fact] // NJ-18
    public void CollectEdgeOperations_InheritanceEdge_RelTypeIsInheritsFrom()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Child", ElementKind.Class);
        graph.SetElementKind("A.Parent", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Child", "A.Parent", "Inherits from"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("INHERITS_FROM", ops[0].RelationshipType);
    }

    [Fact] // NJ-19
    public void CollectEdgeOperations_InterfaceEdge_RelTypeIsImplements()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Service", ElementKind.Class);
        graph.SetElementKind("A.IService", ElementKind.Interface);
        graph.AddEdge(new TypeDependency("A.Service", "A.IService", "Implements interface"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("IMPLEMENTS", ops[0].RelationshipType);
    }

    [Fact] // NJ-20
    public void CollectEdgeOperations_UsageEdge_RelTypeIsDependsOn()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("DEPENDS_ON", ops[0].RelationshipType);
        Assert.Equal("Field type", ops[0].Reason);
    }

    [Fact] // NJ-21
    public void CollectEdgeOperations_OutOfScopeTarget_EdgeSkipped()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        // Target "External.Bar" is NOT in ElementKinds — simulates an out-of-scope reference
        graph.AddEdge(new TypeDependency("A.Foo", "External.Bar", "Field type"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Empty(ops);
    }

    [Fact] // NJ-22
    public void CollectEdgeOperations_MultipleEdges_AllReturnedExceptOutOfScope()
    {
        var graph = BuildGraph();
        var ops = Neo4jExporter.CollectEdgeOperations(graph);

        // Graph has 4 edges, all targets are in-scope
        Assert.Equal(4, ops.Count);
    }

    // =========================================================================
    // NJ-23 – NJ-25 : EdgeOperation properties
    // =========================================================================

    [Fact] // NJ-23
    public void CollectEdgeOperations_UsageEdge_ParametersContainReason()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Method return type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal("Method return type", op.Parameters["reason"]);
    }

    [Fact] // NJ-24
    public void CollectEdgeOperations_EdgeContainsSourceAndTargetFqn()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal("A.Foo", op.SourceFqn);
        Assert.Equal("A.Bar", op.TargetFqn);
        Assert.Equal("A.Foo", op.Parameters["sourceFqn"]);
        Assert.Equal("A.Bar", op.Parameters["targetFqn"]);
    }

    [Fact] // NJ-25
    public void CollectEdgeOperations_InheritanceEdge_QueryDoesNotContainReasonPlaceholder()
    {
        // INHERITS_FROM and IMPLEMENTS queries have no $reason placeholder
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Child", ElementKind.Class);
        graph.SetElementKind("A.Parent", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Child", "A.Parent", "Inherits from"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.DoesNotContain("$reason", op.Query);
    }
}
