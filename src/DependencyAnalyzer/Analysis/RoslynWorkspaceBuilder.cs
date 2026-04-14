using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DependencyAnalyzer.Analysis;

public sealed class RoslynWorkspaceBuilder
{
    private readonly Action<string> _log;

    public RoslynWorkspaceBuilder(Action<string>? log = null)
    {
        _log = log ?? Console.WriteLine;
    }

    /// <summary>
    /// Builds a CSharpCompilation from the given source file paths.
    /// Skips files that don't exist or can't be read (with a warning).
    /// </summary>
    public CSharpCompilation BuildCompilation(IEnumerable<string> filePaths)
    {
        var syntaxTrees = new List<SyntaxTree>();
        int loaded = 0;
        int skipped = 0;

        foreach (var filePath in filePaths)
        {
            var normalized = Path.GetFullPath(filePath.Trim());
            if (!File.Exists(normalized))
            {
                _log($"WARNING: File not found, skipping: {normalized}");
                skipped++;
                continue;
            }

            try
            {
                var source = File.ReadAllText(normalized);
                var tree = CSharpSyntaxTree.ParseText(source, path: normalized);
                syntaxTrees.Add(tree);
                loaded++;
            }
            catch (Exception ex)
            {
                _log($"WARNING: Could not read file, skipping: {normalized} ({ex.Message})");
                skipped++;
            }
        }

        _log($"Loaded {loaded} source file(s), skipped {skipped}.");

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "DependencyAnalysis",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    /// <summary>
    /// Validates that the given FQN resolves to exactly one named type in the compilation.
    /// Returns the symbol if found, or throws with a descriptive message.
    /// </summary>
    public INamedTypeSymbol ResolveTargetClass(CSharpCompilation compilation, string targetFqn)
    {
        var candidates = GetAllNamedTypes(compilation)
            .Where(t => GetFullyQualifiedName(t) == targetFqn)
            .ToList();

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                $"Target class '{targetFqn}' was not found in the source scope.");

        if (candidates.Count > 1)
            throw new InvalidOperationException(
                $"Target class '{targetFqn}' is ambiguous — found {candidates.Count} definitions.");

        return candidates[0];
    }

    public static string GetFullyQualifiedName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "");
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(CSharpCompilation compilation)
    {
        return GetAllNamedTypes(compilation.GlobalNamespace);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypes(type))
                yield return nested;
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            foreach (var type in GetAllNamedTypes(childNs))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deep in GetNestedTypes(nested))
                yield return deep;
        }
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var references = new List<MetadataReference>();
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;

        if (trustedAssemblies != null)
        {
            foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
            {
                if (File.Exists(path))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                    catch
                    {
                        // Skip assemblies that can't be loaded
                    }
                }
            }
        }

        return references;
    }
}
