using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for Neo4jExporter helper logic that can be executed without a live Neo4j server.
/// The graph model mirrors the Doxygen compound schema (compound.xsd version 1.9.1):
///   (:Compound {id, kind, name, fqn, language})
///   -[:BASECOMPOUNDREF {prot, virt}]->  (inheritance / interface impl)
///   -[:REFERENCES {kind: "variable", reason}]->  (all other usage edges)
///   -[:INNERCLASS {prot}]->  (namespace → member types)
/// Traces to FR-7.1 – FR-7.8.
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

        graph.AddEdge(new TypeDependency("Acme.Core.OrderService", "Acme.Core.IOrderRepository", "Implements interface"));
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Inherits from"));
        graph.AddEdge(new TypeDependency("Acme.Core.OrderService", "Acme.Core.OrderStatus", "Field type"));
        graph.AddEdge(new TypeDependency("Acme.Core.OrderRecord", "Acme.Core.OrderService", "Constructor parameter type"));

        return graph;
    }

    // =========================================================================
    // NJ-01 – NJ-06 : BuildNodeParameters
    // =========================================================================

    [Fact] // NJ-01
    public void BuildNodeParameters_IdIsDoxygenRefId()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal(DoxygenRefIdHelper.ToRefId("Acme.Core.OrderService", ElementKind.Class), p["id"]);
    }

    [Fact] // NJ-02
    public void BuildNodeParameters_KindIsDoxygenKindString()
    {
        // "class" not "Class" — aligns with Doxygen <compounddef kind="class">
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("class", p["kind"]);
    }

    [Fact] // NJ-03
    public void BuildNodeParameters_NameUsesDoubleColonSeparators()
    {
        // Mirrors <compoundname>Acme::Core::OrderService</compoundname>
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("Acme::Core::OrderService", p["name"]);
    }

    [Fact] // NJ-04
    public void BuildNodeParameters_ContainsFqn()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("Acme.Core.OrderService", p["fqn"]);
    }

    [Fact] // NJ-05
    public void BuildNodeParameters_ContainsLanguageCSharp()
    {
        var p = Neo4jExporter.BuildNodeParameters("Acme.Core.OrderService", ElementKind.Class);
        Assert.Equal("C#", p["language"]);
    }

    [Theory] // NJ-06
    [InlineData(ElementKind.Class, "class")]
    [InlineData(ElementKind.Record, "class")]
    [InlineData(ElementKind.Delegate, "class")]
    [InlineData(ElementKind.Interface, "interface")]
    [InlineData(ElementKind.Struct, "struct")]
    [InlineData(ElementKind.Enum, "enum")]
    public void BuildNodeParameters_AllKindsMappedToDoxygenKindString(ElementKind kind, string expected)
    {
        var p = Neo4jExporter.BuildNodeParameters("X.Y", kind);
        Assert.Equal(expected, p["kind"]);
    }

    // =========================================================================
    // NJ-07 – NJ-09 : BuildNamespaceParameters
    // =========================================================================

    [Fact] // NJ-07
    public void BuildNamespaceParameters_IdIsNamespaceRefId()
    {
        var p = Neo4jExporter.BuildNamespaceParameters("Acme.Core");
        Assert.Equal(DoxygenRefIdHelper.NamespaceRefId("Acme.Core"), p["id"]);
    }

    [Fact] // NJ-08
    public void BuildNamespaceParameters_KindIsNamespace()
    {
        // Mirrors <compounddef kind="namespace"> in Doxygen XML
        var p = Neo4jExporter.BuildNamespaceParameters("Acme.Core");
        Assert.Equal("namespace", p["kind"]);
    }

    [Fact] // NJ-09
    public void BuildNamespaceParameters_NameUsesDoubleColonSeparators()
    {
        // Mirrors <compoundname>Acme::Core</compoundname>
        var p = Neo4jExporter.BuildNamespaceParameters("Acme.Core");
        Assert.Equal("Acme::Core", p["name"]);
    }

    // =========================================================================
    // NJ-10 – NJ-12 : GetVirt
    // =========================================================================

    [Fact] // NJ-10
    public void GetVirt_Inheritance_ReturnsNonVirtual()
    {
        // Mirrors <basecompoundref virt="non-virtual">
        Assert.Equal("non-virtual", Neo4jExporter.GetVirt(DoxygenEdgeKind.Inheritance));
    }

    [Fact] // NJ-11
    public void GetVirt_InterfaceImplementation_ReturnsVirtual()
    {
        // Mirrors <basecompoundref virt="virtual">
        Assert.Equal("virtual", Neo4jExporter.GetVirt(DoxygenEdgeKind.InterfaceImplementation));
    }

    [Fact] // NJ-12
    public void GetVirt_Usage_ReturnsNonVirtual()
    {
        // Usage edges don't use virt, but GetVirt fallback is "non-virtual"
        Assert.Equal("non-virtual", Neo4jExporter.GetVirt(DoxygenEdgeKind.Usage));
    }

    // =========================================================================
    // NJ-13 – NJ-21 : CollectEdgeOperations
    // =========================================================================

    [Fact] // NJ-13
    public void CollectEdgeOperations_EmptyGraph_ReturnsEmpty()
    {
        Assert.Empty(Neo4jExporter.CollectEdgeOperations(new DependencyGraph()));
    }

    [Fact] // NJ-14
    public void CollectEdgeOperations_InheritanceEdge_RelTypeIsBaseCompoundRef()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Child", ElementKind.Class);
        graph.SetElementKind("A.Parent", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Child", "A.Parent", "Inherits from"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("BASECOMPOUNDREF", ops[0].RelationshipType);
    }

    [Fact] // NJ-15
    public void CollectEdgeOperations_InheritanceEdge_VirtParameterIsNonVirtual()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Child", ElementKind.Class);
        graph.SetElementKind("A.Parent", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Child", "A.Parent", "Inherits from"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal("non-virtual", op.Parameters["virt"]);
        Assert.Equal("public", op.Parameters["prot"]);
    }

    [Fact] // NJ-16
    public void CollectEdgeOperations_InterfaceEdge_RelTypeIsBaseCompoundRef()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Service", ElementKind.Class);
        graph.SetElementKind("A.IService", ElementKind.Interface);
        graph.AddEdge(new TypeDependency("A.Service", "A.IService", "Implements interface"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("BASECOMPOUNDREF", ops[0].RelationshipType);
    }

    [Fact] // NJ-17
    public void CollectEdgeOperations_InterfaceEdge_VirtParameterIsVirtual()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Service", ElementKind.Class);
        graph.SetElementKind("A.IService", ElementKind.Interface);
        graph.AddEdge(new TypeDependency("A.Service", "A.IService", "Implements interface"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal("virtual", op.Parameters["virt"]);
    }

    [Fact] // NJ-18
    public void CollectEdgeOperations_UsageEdge_RelTypeIsReferences()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var ops = Neo4jExporter.CollectEdgeOperations(graph);
        Assert.Single(ops);
        Assert.Equal("REFERENCES", ops[0].RelationshipType);
    }

    [Fact] // NJ-19
    public void CollectEdgeOperations_UsageEdge_ReasonInParameters()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Method return type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal("Method return type", op.Parameters["reason"]);
    }

    [Fact] // NJ-20
    public void CollectEdgeOperations_OutOfScopeTarget_EdgeSkipped()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.AddEdge(new TypeDependency("A.Foo", "External.Bar", "Field type"));

        Assert.Empty(Neo4jExporter.CollectEdgeOperations(graph));
    }

    [Fact] // NJ-21
    public void CollectEdgeOperations_EdgeIdsAreDerivedFromRefIds()
    {
        // SourceId and TargetId must be Doxygen refids, not FQNs
        var graph = new DependencyGraph();
        graph.SetElementKind("A.Foo", ElementKind.Class);
        graph.SetElementKind("A.Bar", ElementKind.Enum);
        graph.AddEdge(new TypeDependency("A.Foo", "A.Bar", "Field type"));

        var op = Neo4jExporter.CollectEdgeOperations(graph).Single();
        Assert.Equal(DoxygenRefIdHelper.ToRefId("A.Foo", ElementKind.Class), op.SourceId);
        Assert.Equal(DoxygenRefIdHelper.ToRefId("A.Bar", ElementKind.Enum), op.TargetId);
        Assert.Equal(op.SourceId, op.Parameters["sourceId"]);
        Assert.Equal(op.TargetId, op.Parameters["targetId"]);
    }

    // =========================================================================
    // NJ-22 – NJ-25 : CollectInnerClassOperations
    // =========================================================================

    [Fact] // NJ-22
    public void CollectInnerClassOperations_EmptyGraph_ReturnsEmpty()
    {
        var ops = Neo4jExporter.CollectInnerClassOperations(new DependencyGraph(), []);
        Assert.Empty(ops);
    }

    [Fact] // NJ-23
    public void CollectInnerClassOperations_TypeInNamespace_ProducesInnerClassEdge()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);

        var ns = new[] { "Acme.Core" };
        var ops = Neo4jExporter.CollectInnerClassOperations(graph, ns);

        Assert.Single(ops);
        Assert.Equal("INNERCLASS", ops[0].RelationshipType);
    }

    [Fact] // NJ-24
    public void CollectInnerClassOperations_InnerClassEdge_IdsAreNamespaceRefIdAndTypeRefId()
    {
        // SourceId = namespace refid, TargetId = type refid
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);

        var ops = Neo4jExporter.CollectInnerClassOperations(graph, ["Acme.Core"]);
        var op = ops.Single();

        Assert.Equal(DoxygenRefIdHelper.NamespaceRefId("Acme.Core"), op.SourceId);
        Assert.Equal(DoxygenRefIdHelper.ToRefId("Acme.Core.OrderService", ElementKind.Class), op.TargetId);
        Assert.Equal(op.SourceId, op.Parameters["nsId"]);
        Assert.Equal(op.TargetId, op.Parameters["typeId"]);
    }

    [Fact] // NJ-25
    public void CollectInnerClassOperations_TopLevelType_NotLinkedToAnyNamespace()
    {
        // A type with no namespace (no dot in FQN) should not be linked to any namespace
        var graph = new DependencyGraph();
        graph.SetElementKind("MyClass", ElementKind.Class);

        // "Acme" is a namespace, but "MyClass" doesn't belong to it
        var ops = Neo4jExporter.CollectInnerClassOperations(graph, ["Acme"]);
        Assert.Empty(ops);
    }
}
