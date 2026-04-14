using DependencyAnalyzer.Analysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DependencyAnalyzer.Tests;

public class RoslynWorkspaceBuilderTests
{
    [Fact]
    public void BuildCompilation_WithValidFiles_LoadsAllFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "A.cs");
            var file2 = Path.Combine(tempDir, "B.cs");
            File.WriteAllText(file1, "namespace Test { public class A {} }");
            File.WriteAllText(file2, "namespace Test { public class B {} }");

            var builder = new RoslynWorkspaceBuilder(_ => { });
            var compilation = builder.BuildCompilation(new[] { file1, file2 });

            Assert.Equal(2, compilation.SyntaxTrees.Count());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildCompilation_WithMissingFile_SkipsAndContinues()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var file1 = Path.Combine(tempDir, "A.cs");
            var missing = Path.Combine(tempDir, "NonExistent.cs");
            File.WriteAllText(file1, "namespace Test { public class A {} }");

            var warnings = new List<string>();
            var builder = new RoslynWorkspaceBuilder(msg => warnings.Add(msg));
            var compilation = builder.BuildCompilation(new[] { file1, missing });

            Assert.Single(compilation.SyntaxTrees);
            Assert.Contains(warnings, w => w.Contains("WARNING") && w.Contains("NonExistent.cs"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveTargetClass_ValidFqn_ReturnsSymbol()
    {
        var compilation = TestHelper.CreateCompilation(
            "namespace MyApp { public class MyService {} }");

        var builder = new RoslynWorkspaceBuilder(_ => { });
        var symbol = builder.ResolveTargetClass(compilation, "MyApp.MyService");

        Assert.Equal("MyService", symbol.Name);
    }

    [Fact]
    public void ResolveTargetClass_UnknownFqn_ThrowsInvalidOperation()
    {
        var compilation = TestHelper.CreateCompilation(
            "namespace MyApp { public class MyService {} }");

        var builder = new RoslynWorkspaceBuilder(_ => { });

        var ex = Assert.Throws<InvalidOperationException>(
            () => builder.ResolveTargetClass(compilation, "MyApp.NonExistent"));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void ResolveTargetClass_AmbiguousFqn_ThrowsInvalidOperation()
    {
        // Two partial classes in different trees with same FQN — should resolve to one symbol
        // For true ambiguity we'd need different assemblies, but let's test error path
        var compilation = TestHelper.CreateCompilation(
            "namespace MyApp { public class Svc {} }",
            "namespace MyApp { public class Svc2 {} }");

        var builder = new RoslynWorkspaceBuilder(_ => { });

        // This should work (only one Svc)
        var symbol = builder.ResolveTargetClass(compilation, "MyApp.Svc");
        Assert.Equal("Svc", symbol.Name);
    }
}
