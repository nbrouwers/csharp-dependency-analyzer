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
}
