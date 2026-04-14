using DependencyAnalyzer.Analysis;
using DependencyAnalyzer.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Helper methods for creating Roslyn compilations from inline C# source code in tests.
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// Builds a CSharpCompilation from inline source strings (no file system needed).
    /// </summary>
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToList();

        var references = new List<Microsoft.CodeAnalysis.MetadataReference>();
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (trustedAssemblies != null)
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(path))
                {
                    try { references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path)); }
                    catch { }
                }
            }
        }

        return CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Builds a DependencyGraph from inline source strings.
    /// </summary>
    public static DependencyGraph BuildGraph(params string[] sources)
    {
        var compilation = CreateCompilation(sources);
        var builder = new DependencyGraphBuilder(_ => { });
        return builder.Build(compilation);
    }

    /// <summary>
    /// Runs end-to-end analysis: compile → build graph → compute fan-in.
    /// </summary>
    public static AnalysisResult Analyze(string targetFqn, params string[] sources)
    {
        var compilation = CreateCompilation(sources);
        var graphBuilder = new DependencyGraphBuilder(_ => { });
        var graph = graphBuilder.Build(compilation);
        var analyzer = new TransitiveFanInAnalyzer();
        return analyzer.Analyze(targetFqn, graph);
    }
}
