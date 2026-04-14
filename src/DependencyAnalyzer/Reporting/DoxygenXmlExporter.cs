using System.Text;
using System.Xml.Linq;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Reporting;

/// <summary>
/// Exports a <see cref="DependencyGraph"/> as a directory of Doxygen-compatible XML files
/// conforming to the Doxygen compound schema (compound.xsd, version 1.9.1).
///
/// Output structure:
///   {outputDirectory}/index.xml           — lists all compounds
///   {outputDirectory}/{refid}.xml         — one file per in-scope type
///   {outputDirectory}/namespace{ns}.xml   — one file per unique namespace prefix
/// </summary>
public sealed class DoxygenXmlExporter
{
    private static readonly XNamespace Xsi =
        "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Exports the full dependency graph and returns the number of XML files written.
    /// </summary>
    public int Export(DependencyGraph graph, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        int fileCount = 0;

        // Collect all edge lists indexed by source FQN for quick lookup
        var edgesBySource = graph.Edges;

        // --- 1. Generate one compound file per type ---
        foreach (var (fqn, kind) in graph.ElementKinds)
        {
            var doc = BuildCompoundDocument(fqn, kind, edgesBySource, graph.ElementKinds);
            var refId = DoxygenRefIdHelper.ToRefId(fqn, kind);
            SaveDocument(doc, Path.Combine(outputDirectory, $"{refId}.xml"));
            fileCount++;
        }

        // --- 2. Generate one namespace compound per unique namespace prefix ---
        var namespaceFqns = DoxygenRefIdHelper
            .ExtractNamespaces(graph.ElementKinds.Keys)
            .OrderBy(ns => ns);

        foreach (var ns in namespaceFqns)
        {
            var typesInNs = graph.ElementKinds
                .Where(kvp => GetNamespace(kvp.Key) == ns)
                .Select(kvp => kvp.Key)
                .OrderBy(f => f)
                .ToList();

            var doc = BuildNamespaceDocument(ns, typesInNs, graph.ElementKinds);
            SaveDocument(doc, Path.Combine(outputDirectory, $"{DoxygenRefIdHelper.NamespaceRefId(ns)}.xml"));
            fileCount++;
        }

        // --- 3. Generate index.xml ---
        var indexDoc = BuildIndexDocument(graph.ElementKinds, namespaceFqns);
        SaveDocument(indexDoc, Path.Combine(outputDirectory, "index.xml"));
        fileCount++;

        return fileCount;
    }

    // -------------------------------------------------------------------------
    // Compound document builders
    // -------------------------------------------------------------------------

    private static XDocument BuildCompoundDocument(
        string fqn,
        ElementKind kind,
        Dictionary<string, List<TypeDependency>> edgesBySource,
        Dictionary<string, ElementKind> allKinds)
    {
        var refId = DoxygenRefIdHelper.ToRefId(fqn, kind);
        var doxygenKind = DoxygenRefIdHelper.ToDoxygenKind(kind);
        var compoundName = DoxygenRefIdHelper.ToCompoundName(fqn);

        var compoundDef = new XElement("compounddef",
            new XAttribute("id", refId),
            new XAttribute("kind", doxygenKind),
            new XAttribute("language", "C#"),
            new XElement("compoundname", compoundName));

        // Structural edges (inheritance / interface implementation) → <basecompoundref>
        edgesBySource.TryGetValue(fqn, out var outEdges);
        var structuralEdges = (outEdges ?? [])
            .Where(e => DoxygenRefIdHelper.ClassifyEdge(e.DependencyReason)
                        != DoxygenEdgeKind.Usage);

        foreach (var edge in structuralEdges)
        {
            if (!allKinds.TryGetValue(edge.TargetFqn, out var targetKind))
                continue;

            var edgeKind = DoxygenRefIdHelper.ClassifyEdge(edge.DependencyReason);
            var virt = edgeKind == DoxygenEdgeKind.InterfaceImplementation
                ? "virtual"
                : "non-virtual";

            compoundDef.Add(new XElement("basecompoundref",
                new XAttribute("refid", DoxygenRefIdHelper.ToRefId(edge.TargetFqn, targetKind)),
                new XAttribute("prot", "public"),
                new XAttribute("virt", virt),
                DoxygenRefIdHelper.ToCompoundName(edge.TargetFqn)));
        }

        // Usage edges → <sectiondef> with one <memberdef> per distinct reason group
        var usageEdges = (outEdges ?? [])
            .Where(e => DoxygenRefIdHelper.ClassifyEdge(e.DependencyReason)
                        == DoxygenEdgeKind.Usage)
            .GroupBy(e => (e.TargetFqn, e.DependencyReason))
            .ToList();

        if (usageEdges.Count > 0)
        {
            var sectionDef = new XElement("sectiondef",
                new XAttribute("kind", "public-attrib"));

            int memberIndex = 0;
            foreach (var group in usageEdges)
            {
                var (targetFqn, reason) = group.Key;
                if (!allKinds.TryGetValue(targetFqn, out var targetKind))
                    continue;

                var memberId = $"{refId}_1dep_{memberIndex++}";
                var memberDef = new XElement("memberdef",
                    new XAttribute("kind", "variable"),
                    new XAttribute("id", memberId),
                    new XElement("name", reason),
                    new XElement("briefdescription",
                        new XElement("para", $"Dependency on {DoxygenRefIdHelper.ToCompoundName(targetFqn)}")),
                    new XElement("references",
                        new XAttribute("refid", DoxygenRefIdHelper.ToRefId(targetFqn, targetKind)),
                        new XAttribute("compoundref", DoxygenRefIdHelper.ToRefId(targetFqn, targetKind)),
                        DoxygenRefIdHelper.ToCompoundName(targetFqn)));

                sectionDef.Add(memberDef);
            }

            if (sectionDef.HasElements)
                compoundDef.Add(sectionDef);
        }

        // Stub location (source location data not stored in DependencyGraph)
        compoundDef.Add(new XElement("location",
            new XAttribute("file", ""),
            new XAttribute("line", "0"),
            new XAttribute("column", "0")));

        return WrapInDoxygen(compoundDef);
    }

    private static XDocument BuildNamespaceDocument(
        string ns,
        IReadOnlyList<string> typeFqns,
        Dictionary<string, ElementKind> allKinds)
    {
        var refId = DoxygenRefIdHelper.NamespaceRefId(ns);
        var compoundName = DoxygenRefIdHelper.ToCompoundName(ns);

        var compoundDef = new XElement("compounddef",
            new XAttribute("id", refId),
            new XAttribute("kind", "namespace"),
            new XAttribute("language", "C#"),
            new XElement("compoundname", compoundName));

        foreach (var fqn in typeFqns)
        {
            if (!allKinds.TryGetValue(fqn, out var kind))
                continue;

            compoundDef.Add(new XElement("innerclass",
                new XAttribute("refid", DoxygenRefIdHelper.ToRefId(fqn, kind)),
                new XAttribute("prot", "public"),
                DoxygenRefIdHelper.ToCompoundName(fqn)));
        }

        compoundDef.Add(new XElement("location",
            new XAttribute("file", ""),
            new XAttribute("line", "0"),
            new XAttribute("column", "0")));

        return WrapInDoxygen(compoundDef);
    }

    private static XDocument BuildIndexDocument(
        Dictionary<string, ElementKind> allKinds,
        IEnumerable<string> namespaces)
    {
        var root = new XElement("doxygenindex",
            new XAttribute("version", "1.9.1"),
            new XAttribute(XNamespace.Xml + "lang", "en-US"));

        foreach (var (fqn, kind) in allKinds.OrderBy(k => k.Key))
        {
            root.Add(new XElement("compound",
                new XAttribute("refid", DoxygenRefIdHelper.ToRefId(fqn, kind)),
                new XAttribute("kind", DoxygenRefIdHelper.ToDoxygenKind(kind)),
                new XElement("name", DoxygenRefIdHelper.ToCompoundName(fqn))));
        }

        foreach (var ns in namespaces.OrderBy(n => n))
        {
            root.Add(new XElement("compound",
                new XAttribute("refid", DoxygenRefIdHelper.NamespaceRefId(ns)),
                new XAttribute("kind", "namespace"),
                new XElement("name", DoxygenRefIdHelper.ToCompoundName(ns))));
        }

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static XDocument WrapInDoxygen(XElement compoundDef) =>
        new(new XDeclaration("1.0", "UTF-8", null),
            new XElement("doxygen",
                new XAttribute("version", "1.9.1"),
                new XAttribute(XNamespace.Xml + "lang", "en-US"),
                compoundDef));

    private static void SaveDocument(XDocument doc, string path)
    {
        using var writer = new StreamWriter(path, append: false, encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        doc.Save(writer);
    }

    /// <summary>Returns the namespace portion of a fully qualified name (everything before the last dot).</summary>
    private static string GetNamespace(string fqn)
    {
        var lastDot = fqn.LastIndexOf('.');
        return lastDot >= 0 ? fqn[..lastDot] : string.Empty;
    }
}
