using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests for static reflection dependency detection (Strategy 1).
/// Covers <c>Type.GetType("FQN")</c> and <c>Assembly.GetType("FQN")</c> calls
/// where the first argument is a string literal that matches an in-scope FQN.
/// Dynamic string arguments (variables, concatenations) are intentionally not
/// detected — they require runtime tracing instead.
/// Traces to <b>FR-3.2</b>.
/// </summary>
public class ReflectionDependencyTests
{
    private static bool HasEdge(DependencyGraph graph, string source, string target)
        => graph.Edges.Values.SelectMany(e => e).Any(d => d.SourceFqn == source && d.TargetFqn == target);

    [Fact] // RF-01
    public void Detects_TypeGetType_FullyQualifiedStringLiteral()
    {
        // Type.GetType("N.Target") with a string literal matching an in-scope FQN
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N {
                public class Consumer {
                    public void Do() { var t = System.Type.GetType(""N.Target""); }
                }
            }");

        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    [Fact] // RF-02
    public void TypeGetType_TypeNotInScope_NoEdge()
    {
        // "External.Bar" is not in the source scope — must not produce a spurious edge
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N {
                public class Consumer {
                    public void Do() { var t = System.Type.GetType(""External.Bar""); }
                }
            }");

        Assert.Empty(graph.Edges.Values.SelectMany(e => e)
            .Where(d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "External.Bar"));
    }

    [Fact] // RF-03
    public void Detects_AssemblyGetType_FullyQualifiedStringLiteral()
    {
        // assembly.GetType("N.Target") on a System.Reflection.Assembly instance
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N {
                using System.Reflection;
                public class Consumer {
                    public void Do(Assembly asm) { var t = asm.GetType(""N.Target""); }
                }
            }");

        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    [Fact] // RF-04
    public void TypeGetType_NonLiteralStringArgument_NoEdge()
    {
        // Dynamic/variable string — not statically detectable; no edge should be added
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N {
                public class Consumer {
                    public void Do(string typeName) { var t = System.Type.GetType(typeName); }
                }
            }");

        Assert.False(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    [Fact] // RF-05
    public void TypeGetType_DependencyReason_ContainsReflection()
    {
        // The dependency reason string should clearly identify this as a reflection edge
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N {
                public class Consumer {
                    public void Do() { var t = System.Type.GetType(""N.Target""); }
                }
            }");

        var edge = graph.Edges.Values.SelectMany(e => e)
            .Single(d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");

        Assert.Contains("Reflection", edge.DependencyReason);
    }

    [Fact] // RF-06
    public void TypeGetType_SelfReference_NoSelfEdge()
    {
        // A type calling Type.GetType with its own FQN must not produce a self-loop
        var graph = TestHelper.BuildGraph(
            @"namespace N {
                public class Consumer {
                    public void Do() { var t = System.Type.GetType(""N.Consumer""); }
                }
            }");

        Assert.Empty(graph.Edges.Values.SelectMany(e => e)
            .Where(d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Consumer"));
    }
}
