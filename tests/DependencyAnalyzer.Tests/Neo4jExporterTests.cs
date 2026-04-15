using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for Neo4jExporter helper logic that can be executed without a live Neo4j server.
/// The graph schema matches the CSV export exactly:
///   (:<em>typeLabel</em> {id, name, type, label, file, startLine, endLine,
///                         accessibility, fullyqualifiedname, resolved})
///   -[:`basecompoundref` {startLine, endLine}]->  (inheritance / interface impl)
///   -[:`ref` {startLine, endLine}]->  (all other usage edges)
/// Traces to FR-7.1 – FR-7.7.
/// </summary>
public sealed class Neo4jExporterTests
{
    // =========================================================================
    // NJ-01 – NJ-08 : BuildNodeParameters
    // =========================================================================

    [Fact] // NJ-01
    public void BuildNodeParameters_IdMatchesCsvIdHelper()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal(CsvIdHelper.ToNodeId("Acme.Core.OrderService", "class"), p["id"]);
    }

    [Fact] // NJ-02
    public void BuildNodeParameters_TypeAndLabelAreTypeLabel()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.IOrderRepository", ElementKind.Interface);
        Assert.Equal("interface", p["type"]);
        Assert.Equal("interface", p["label"]);
    }

    [Fact] // NJ-03
    public void BuildNodeParameters_NameIsSimpleName()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("OrderService", p["name"]);
    }

    [Fact] // NJ-04
    public void BuildNodeParameters_ContainsFqn()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("Acme.Core.OrderService", p["fullyqualifiedname"]);
    }

    [Fact] // NJ-05
    public void BuildNodeParameters_ResolvedIsTrue()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal(true, p["resolved"]);
    }

    [Theory] // NJ-06
    [InlineData(ElementKind.Class,     "class")]
    [InlineData(ElementKind.Record,    "class")]
    [InlineData(ElementKind.Delegate,  "interface")]
    [InlineData(ElementKind.Interface, "interface")]
    [InlineData(ElementKind.Struct,    "struct")]
    [InlineData(ElementKind.Enum,      "enum")]
    public void BuildNodeParameters_AllKindsMappedToTypeLabel(ElementKind kind, string expected)
    {
        var p = Neo4jExporter.BuildNodeParameters("X.Y", kind);
        Assert.Equal(expected, p["type"]);
    }

    [Fact] // NJ-07
    public void BuildNodeParameters_WithTypeLocation_PopulatesLocationFields()
    {
        var loc = new TypeLocation("C:/src/OrderService.cs", 10, 42, "public");
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class, loc);

        Assert.Equal("C:/src/OrderService.cs", p["file"]);
        Assert.Equal(10, p["startLine"]);
        Assert.Equal(42, p["endLine"]);
        Assert.Equal("public", p["accessibility"]);
    }

    [Fact] // NJ-08
    public void BuildNodeParameters_WithoutTypeLocation_DefaultsToZeroAndEmpty()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);

        Assert.Equal("", p["file"]);
        Assert.Equal(0, p["startLine"]);
        Assert.Equal(0, p["endLine"]);
        Assert.Equal("", p["accessibility"]);
    }

    // =========================================================================
    // NJ-09 – NJ-11 : BuildUnresolvedNodeParameters
    // =========================================================================

    [Fact] // NJ-09
    public void BuildUnresolvedNodeParameters_ResolvedIsFalse()
    {
        var p = Neo4jExporter.BuildUnresolvedNodeParameters("Acme.Core.Missing");
        Assert.Equal(false, p["resolved"]);
    }

    [Fact] // NJ-10
    public void BuildUnresolvedNodeParameters_IdMatchesCsvIdHelperWithClassLabel()
    {
        var p = Neo4jExporter.BuildUnresolvedNodeParameters("Acme.Core.Missing");
        Assert.Equal(CsvIdHelper.ToNodeId("Acme.Core.Missing", "class"), p["id"]);
    }

    [Fact] // NJ-11
    public void BuildUnresolvedNodeParameters_TypeIsClass()
    {
        var p = Neo4jExporter.BuildUnresolvedNodeParameters("Acme.Core.Missing");
        Assert.Equal("class", p["type"]);
        Assert.Equal("class", p["label"]);
    }

    // =========================================================================
    // NJ-12 – NJ-19 : CollectEdgeOperations
    // =========================================================================

    [Fact] // NJ-12
    public void CollectEdgeOperations_EmptyGraph_ReturnsEmpty()
    {
        Assert.Empty(Neo4jExporter.CollectEdgeOperations(new DependencyGraph()));
    }

    [Fact] // NJ-13
    public void CollectEdgeOperations_InheritanceEdge_RelTypeIsBaseCompoundRef()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Child", ElementKind.Class);
        graph.SetElementKind("A.Parent", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Child", "A.Parent", "Inherits from"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("basecompoundref", ops[0].RelationshipType);
    }

    [Fact] // NJ-14
    public void CollectEdgeOperations_InterfaceEdge_RelTypeIsBaseCompoundRef()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Service", ElementKind.Class);
        graph.SetElementKind("A.IService", ElementKind.Interface);
        graph.AddEdge(new TypeDependency("A.Service", "A.IService", "Implements interface"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("basecompoundref", ops[0].RelationshipType);
    }

    [Fact] // NJ-15
    public void CollectEdgeOperations_UsageEdge_RelTypeIsRef()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("ref", ops[0].RelationshipType);
    }

    [Fact] // NJ-16
    public void CollectEdgeOperations_OutOfScopeTarget_EdgeSkipped()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "External.Bar", "Field type"));

        Assert.Empty(Neo4jExporter.CollectEdgeOperations(graph));
    }

    [Fact] // NJ-17
    public void CollectEdgeOperations_EdgeIdsMatchCsvIdHelper()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Enum);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();

        var expectedSrc = CsvIdHelper.ToNodeId("A.Foo", "class");
        var expectedTgt = CsvIdHelper.ToNodeId("A.Bar", "enum");
        Assert.Equal(expectedSrc, op.SourceId);
        Assert.Equal(expectedTgt, op.TargetId);
        Assert.Equal(op.SourceId, op.Parameters["sourceId"]);
        Assert.Equal(op.TargetId, op.Parameters["targetId"]);
    }

    [Fact] // NJ-18
    public void CollectEdgeOperations_WithEdgeLocation_PopulatesLineCols()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        var dep = new TypeDependency("A.Foo", "A.Bar", "Field type");
        graph.AddEdge(dep);
        graph.SetEdgeLocation(dep, new DependencyLocation(15, 15));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal(15, op.Parameters["startLine"]);
        Assert.Equal(15, op.Parameters["endLine"]);
    }

    [Fact] // NJ-19
    public void CollectEdgeOperations_WithoutEdgeLocation_LineColsDefaultToZero()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal(0, op.Parameters["startLine"]);
        Assert.Equal(0, op.Parameters["endLine"]);
    }

    // =========================================================================
    // NJ-20 : ClearDatabaseQuery
    // =========================================================================

    [Fact] // NJ-20
    public void ClearDatabaseQuery_IsDetachDeleteAll()
    {
        // The query must detach-delete every node so the database contains
        // exactly the exported graph after import (FR-7.8).
        Assert.Equal("MATCH (n) DETACH DELETE n", Neo4jExporter.ClearDatabaseQuery);
    }
}
