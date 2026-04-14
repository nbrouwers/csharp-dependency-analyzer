using System.Xml.Linq;
using DependencyAnalyzer.Models;
using DependencyAnalyzer.Reporting;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for DoxygenXmlExporter, DoxygenRefIdHelper, and DoxygenEdgeKind classification.
/// Traces to FR-6.1 – FR-6.7 and Phase 11/12/14 of the implementation plan.
/// </summary>
public sealed class DoxygenXmlExporterTests : IDisposable
{
    private readonly string _tempDir;

    public DoxygenXmlExporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"doxygen-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helper: build a simple graph in-memory without touching the file system
    // -------------------------------------------------------------------------

    private static DependencyGraph BuildSimpleGraph()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        graph.SetElementKind("Acme.Core.IOrderRepository", ElementKind.Interface);
        graph.SetElementKind("Acme.Core.OrderStatus", ElementKind.Enum);
        graph.SetElementKind("Acme.Data.OrderEntity", ElementKind.Struct);
        graph.SetElementKind("Acme.Core.OrderRecord", ElementKind.Record);
        graph.SetElementKind("Acme.Core.OrderCreatedHandler", ElementKind.Delegate);
        graph.AddEdge(new TypeDependency("Acme.Core.IOrderRepository", "Acme.Core.OrderService", "Implements interface"));
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Field type"));
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Method parameter type"));
        return graph;
    }

    // -------------------------------------------------------------------------
    // DX-01  Export creates output directory
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_CreatesOutputDirectory()
    {
        var dir = Path.Combine(_tempDir, "subdir");
        Assert.False(Directory.Exists(dir));

        new DoxygenXmlExporter().Export(new DependencyGraph(), dir);

        Assert.True(Directory.Exists(dir));
    }

    // -------------------------------------------------------------------------
    // DX-02  index.xml is generated
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_CreatesIndexXml()
    {
        new DoxygenXmlExporter().Export(BuildSimpleGraph(), _tempDir);
        Assert.True(File.Exists(Path.Combine(_tempDir, "index.xml")));
    }

    // -------------------------------------------------------------------------
    // DX-03  index.xml has one <compound> per type
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_IndexXml_ContainsOneEntryPerType()
    {
        var graph = BuildSimpleGraph();
        new DoxygenXmlExporter().Export(graph, _tempDir);

        var index = XDocument.Load(Path.Combine(_tempDir, "index.xml"));
        var typeRefIds = graph.ElementKinds
            .Select(kvp => DoxygenRefIdHelper.ToRefId(kvp.Key, kvp.Value))
            .ToHashSet();

        var compoundRefIds = index.Root!
            .Elements("compound")
            .Select(e => (string)e.Attribute("refid")!)
            .ToHashSet();

        foreach (var refId in typeRefIds)
            Assert.Contains(refId, compoundRefIds);
    }

    // -------------------------------------------------------------------------
    // DX-04  One {refid}.xml file per type
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_CreatesOneXmlFilePerType()
    {
        var graph = BuildSimpleGraph();
        new DoxygenXmlExporter().Export(graph, _tempDir);

        foreach (var (fqn, kind) in graph.ElementKinds)
        {
            var expectedFile = Path.Combine(_tempDir, $"{DoxygenRefIdHelper.ToRefId(fqn, kind)}.xml");
            Assert.True(File.Exists(expectedFile), $"Missing file for {fqn}");
        }
    }

    // -------------------------------------------------------------------------
    // DX-05 through DX-10  ToRefId prefix per ElementKind
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData(ElementKind.Class,     "classAcme_1_1Core_1_1OrderService")]
    [InlineData(ElementKind.Interface, "interfaceAcme_1_1Core_1_1IOrderRepository")]
    [InlineData(ElementKind.Struct,    "structAcme_1_1Data_1_1OrderEntity")]
    [InlineData(ElementKind.Enum,      "enumAcme_1_1Core_1_1OrderStatus")]
    [InlineData(ElementKind.Record,    "classAcme_1_1Core_1_1OrderRecord")]    // DX-09
    [InlineData(ElementKind.Delegate,  "classAcme_1_1Core_1_1OrderCreatedHandler")] // DX-10
    public void ToRefId_ProducesExpectedPrefix(ElementKind kind, string expectedRefId)
    {
        var fqn = kind switch
        {
            ElementKind.Interface => "Acme.Core.IOrderRepository",
            ElementKind.Struct    => "Acme.Data.OrderEntity",
            ElementKind.Enum      => "Acme.Core.OrderStatus",
            ElementKind.Record    => "Acme.Core.OrderRecord",
            ElementKind.Delegate  => "Acme.Core.OrderCreatedHandler",
            _                     => "Acme.Core.OrderService"
        };

        Assert.Equal(expectedRefId, DoxygenRefIdHelper.ToRefId(fqn, kind));
    }

    // -------------------------------------------------------------------------
    // DX-11  <compoundname> uses :: separators
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_ContainsCompoundName()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "classAcme_1_1Core_1_1OrderService.xml"));
        var name = doc.Root!.Element("compounddef")!.Element("compoundname")!.Value;
        Assert.Equal("Acme::Core::OrderService", name);
    }

    // -------------------------------------------------------------------------
    // DX-12  Inheritance edge → <basecompoundref virt="non-virtual">
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_InheritanceEdge_EmitsBaseCompoundRef_NonVirtual()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        graph.SetElementKind("Acme.Core.OrderServiceEx", ElementKind.Class);
        graph.AddEdge(new TypeDependency("Acme.Core.OrderServiceEx", "Acme.Core.OrderService", "Inherits from"));

        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "classAcme_1_1Core_1_1OrderServiceEx.xml"));
        var baseRef = doc.Root!.Element("compounddef")!.Element("basecompoundref");
        Assert.NotNull(baseRef);
        Assert.Equal("non-virtual", (string)baseRef!.Attribute("virt")!);
        Assert.Equal("classAcme_1_1Core_1_1OrderService", (string)baseRef.Attribute("refid")!);
    }

    // -------------------------------------------------------------------------
    // DX-13  Interface edge → <basecompoundref virt="virtual">
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_InterfaceEdge_EmitsBaseCompoundRef_Virtual()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.IRepo", ElementKind.Interface);
        graph.SetElementKind("Acme.Core.Impl", ElementKind.Class);
        graph.AddEdge(new TypeDependency("Acme.Core.Impl", "Acme.Core.IRepo", "Implements interface"));

        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "classAcme_1_1Core_1_1Impl.xml"));
        var baseRef = doc.Root!.Element("compounddef")!.Element("basecompoundref");
        Assert.NotNull(baseRef);
        Assert.Equal("virtual", (string)baseRef!.Attribute("virt")!);
    }

    // -------------------------------------------------------------------------
    // DX-14  Usage edge → <memberdef kind="variable"> with <references>
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_UsageEdge_EmitsMemberdefWithReferences()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        graph.SetElementKind("Acme.Data.OrderEntity", ElementKind.Struct);
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Field type"));

        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "structAcme_1_1Data_1_1OrderEntity.xml"));
        var memberDef = doc.Descendants("memberdef").FirstOrDefault();
        Assert.NotNull(memberDef);
        Assert.Equal("variable", (string)memberDef!.Attribute("kind")!);

        var references = memberDef.Element("references");
        Assert.NotNull(references);
        Assert.Equal("classAcme_1_1Core_1_1OrderService", (string)references!.Attribute("refid")!);
    }

    // -------------------------------------------------------------------------
    // DX-15  Every compound has a <location> stub
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_ContainsLocationStub()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "classAcme_1_1Core_1_1OrderService.xml"));
        var location = doc.Root!.Element("compounddef")!.Element("location");
        Assert.NotNull(location);
        Assert.Equal("", (string)location!.Attribute("file")!);
        Assert.Equal("0", (string)location.Attribute("line")!);
        Assert.Equal("0", (string)location.Attribute("column")!);
    }

    // -------------------------------------------------------------------------
    // DX-16  Two edges same target but different reasons → two memberdefs
    // -------------------------------------------------------------------------
    [Fact]
    public void CompoundXml_MultipleEdgesToSameTarget_OneRefPerReason()
    {
        var graph = new DependencyGraph();
        graph.SetElementKind("Acme.Core.OrderService", ElementKind.Class);
        graph.SetElementKind("Acme.Data.OrderEntity", ElementKind.Struct);
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Field type"));
        graph.AddEdge(new TypeDependency("Acme.Data.OrderEntity", "Acme.Core.OrderService", "Method parameter type"));

        new DoxygenXmlExporter().Export(graph, _tempDir);

        var doc = XDocument.Load(Path.Combine(_tempDir, "structAcme_1_1Data_1_1OrderEntity.xml"));
        var memberDefs = doc.Descendants("memberdef").ToList();
        Assert.Equal(2, memberDefs.Count);
    }

    // -------------------------------------------------------------------------
    // DX-17  Namespace compound files are generated
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_CreatesNamespaceCompounds()
    {
        new DoxygenXmlExporter().Export(BuildSimpleGraph(), _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, $"{DoxygenRefIdHelper.NamespaceRefId("Acme.Core")}.xml")),
            "Namespace compound for Acme.Core missing");
        Assert.True(File.Exists(Path.Combine(_tempDir, $"{DoxygenRefIdHelper.NamespaceRefId("Acme.Data")}.xml")),
            "Namespace compound for Acme.Data missing");
        Assert.True(File.Exists(Path.Combine(_tempDir, $"{DoxygenRefIdHelper.NamespaceRefId("Acme")}.xml")),
            "Namespace compound for Acme missing");
    }

    // -------------------------------------------------------------------------
    // DX-18  Empty graph → only index.xml, no compound files
    // -------------------------------------------------------------------------
    [Fact]
    public void Export_EmptyGraph_ProducesOnlyIndexXml()
    {
        new DoxygenXmlExporter().Export(new DependencyGraph(), _tempDir);

        var files = Directory.GetFiles(_tempDir, "*.xml");
        Assert.Single(files);
        Assert.Equal("index.xml", Path.GetFileName(files[0]));
    }

    // -------------------------------------------------------------------------
    // DX-19 through DX-21  ClassifyEdge
    // -------------------------------------------------------------------------
    [Theory]
    [InlineData("Inherits from",         DoxygenEdgeKind.Inheritance)]
    [InlineData("Implements interface",  DoxygenEdgeKind.InterfaceImplementation)]
    [InlineData("Field type",            DoxygenEdgeKind.Usage)]
    [InlineData("Method parameter type", DoxygenEdgeKind.Usage)]
    [InlineData("Object creation",       DoxygenEdgeKind.Usage)]
    [InlineData("Property type",         DoxygenEdgeKind.Usage)]
    public void ClassifyEdge_ReturnsExpectedKind(string reason, DoxygenEdgeKind expected)
    {
        Assert.Equal(expected, DoxygenRefIdHelper.ClassifyEdge(reason));
    }

    // -------------------------------------------------------------------------
    // DX-22  All generated files are well-formed XML
    // -------------------------------------------------------------------------
    [Fact]
    public void XmlOutput_IsWellFormed()
    {
        new DoxygenXmlExporter().Export(BuildSimpleGraph(), _tempDir);

        foreach (var file in Directory.GetFiles(_tempDir, "*.xml"))
        {
            var ex = Record.Exception(() => XDocument.Load(file));
            Assert.Null(ex); // no XmlException
        }
    }

    // -------------------------------------------------------------------------
    // DX-23  Full-graph integration: all types present in output
    // -------------------------------------------------------------------------
    [Fact]
    public void IntegrationExport_SampleGraph_AllTypesPresent()
    {
        var graph = BuildSimpleGraph();
        new DoxygenXmlExporter().Export(graph, _tempDir);

        foreach (var (fqn, kind) in graph.ElementKinds)
        {
            // Each type has its own compound file
            var file = Path.Combine(_tempDir, $"{DoxygenRefIdHelper.ToRefId(fqn, kind)}.xml");
            Assert.True(File.Exists(file), $"Compound file missing for {fqn}");

            // Each type appears in index.xml
            var index = XDocument.Load(Path.Combine(_tempDir, "index.xml"));
            var refId = DoxygenRefIdHelper.ToRefId(fqn, kind);
            Assert.Contains(index.Root!.Elements("compound"),
                e => (string)e.Attribute("refid")! == refId);
        }

        // Edges are preserved in the compound files
        var entityDoc = XDocument.Load(Path.Combine(_tempDir, "structAcme_1_1Data_1_1OrderEntity.xml"));
        Assert.NotEmpty(entityDoc.Descendants("memberdef"));

        var repoDoc = XDocument.Load(Path.Combine(_tempDir, "interfaceAcme_1_1Core_1_1IOrderRepository.xml"));
        Assert.NotNull(repoDoc.Root!.Element("compounddef")!.Element("basecompoundref"));
    }

    // -------------------------------------------------------------------------
    // Additional helper-level tests
    // -------------------------------------------------------------------------

    [Fact]
    public void ToCompoundName_ReplacesDotWithDoubleColon()
    {
        Assert.Equal("Acme::Core::OrderService",
            DoxygenRefIdHelper.ToCompoundName("Acme.Core.OrderService"));
    }

    [Fact]
    public void ExtractNamespaces_ReturnsAllPrefixes()
    {
        var namespaces = DoxygenRefIdHelper
            .ExtractNamespaces(["Acme.Core.OrderService", "Acme.Data.Entity"])
            .ToHashSet();

        Assert.Contains("Acme", namespaces);
        Assert.Contains("Acme.Core", namespaces);
        Assert.Contains("Acme.Data", namespaces);
    }

    [Fact]
    public void NamespaceRefId_EncodesCorrectly()
    {
        Assert.Equal("namespaceAcme_1_1Core", DoxygenRefIdHelper.NamespaceRefId("Acme.Core"));
    }
}
