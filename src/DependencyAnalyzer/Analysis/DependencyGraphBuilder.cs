using DependencyAnalyzer.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace DependencyAnalyzer.Analysis;

/// <summary>
/// Builds a DependencyGraph from a CSharpCompilation by visiting all type declarations
/// and collecting type-to-type dependency edges.
/// </summary>
public sealed class DependencyGraphBuilder
{
    private readonly Action<string> _log;

    public DependencyGraphBuilder(Action<string>? log = null)
    {
        _log = log ?? Console.WriteLine;
    }

    public DependencyGraph Build(CSharpCompilation compilation)
    {
        var graph = new DependencyGraph();

        // First pass: discover all in-scope type FQNs and their kinds
        var inScopeTypes = new HashSet<string>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                if (symbol is INamedTypeSymbol namedType)
                {
                    var fqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(namedType);
                    inScopeTypes.Add(fqn);
                    graph.SetElementKind(fqn, MapElementKind(namedType));
                    graph.SetTypeLocation(fqn, CaptureTypeLocation(namedType));
                }
            }

            foreach (var delegateDecl in root.DescendantNodes().OfType<DelegateDeclarationSyntax>())
            {
                var symbol = semanticModel.GetDeclaredSymbol(delegateDecl);
                if (symbol is INamedTypeSymbol namedType)
                {
                    var fqn = RoslynWorkspaceBuilder.GetFullyQualifiedName(namedType);
                    inScopeTypes.Add(fqn);
                    graph.SetElementKind(fqn, ElementKind.Delegate);
                    graph.SetTypeLocation(fqn, CaptureTypeLocation(namedType));
                }
            }
        }

        _log($"Discovered {inScopeTypes.Count} type(s) in source scope.");

        // Second pass: collect dependency edges
        int edgeCount = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var visitor = new DependencyVisitor(semanticModel, inScopeTypes);
            visitor.Visit(tree.GetRoot());

            foreach (var dep in visitor.Dependencies)
            {
                graph.AddEdge(dep);
                edgeCount++;
            }

            foreach (var (dep, loc) in visitor.EdgeLocations)
                graph.SetEdgeLocation(dep, loc);

            foreach (var fqn in visitor.UnresolvedReferences)
                graph.AddUnresolvedReference(fqn);
        }

        _log($"Collected {edgeCount} dependency edge(s).");
        return graph;
    }

    private static ElementKind MapElementKind(INamedTypeSymbol symbol)
    {
        if (symbol.IsRecord)
            return ElementKind.Record;

        return symbol.TypeKind switch
        {
            TypeKind.Class => ElementKind.Class,
            TypeKind.Interface => ElementKind.Interface,
            TypeKind.Struct => ElementKind.Struct,
            TypeKind.Enum => ElementKind.Enum,
            TypeKind.Delegate => ElementKind.Delegate,
            _ => ElementKind.Class
        };
    }

    private static TypeLocation CaptureTypeLocation(INamedTypeSymbol namedType)
    {
        var location  = namedType.Locations.FirstOrDefault(l => l.IsInSource);
        var declRef   = namedType.DeclaringSyntaxReferences.FirstOrDefault();
        var startLine = location?.GetLineSpan().StartLinePosition.Line + 1 ?? 0;
        var endLine   = declRef?.GetSyntax().GetLocation().GetLineSpan().EndLinePosition.Line + 1 ?? 0;
        var access    = MapAccessibility(namedType.DeclaredAccessibility);
        var filePath  = location?.SourceTree?.FilePath ?? "";
        return new TypeLocation(filePath, startLine, endLine, access);
    }

    private static string MapAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public              => "public",
        Accessibility.Private             => "private",
        Accessibility.Protected           => "protected",
        Accessibility.Internal            => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        _                                 => "internal"
    };
}
