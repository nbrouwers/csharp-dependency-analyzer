namespace DependencyAnalyzer.Tests;

/// <summary>
/// Round 4 final sweep — targeting every remaining C# construct not explicitly confirmed.
/// If all pass, coverage is complete.
/// </summary>
public class Round4FinalSweepTests
{
    private static bool HasEdge(Models.DependencyGraph graph, string source, string target)
        => graph.Edges.Values.SelectMany(e => e).Any(d => d.SourceFqn == source && d.TargetFqn == target);

    // === YIELD RETURN / ASYNC STREAMS ===

    [Fact]
    public void Sweep_YieldReturnInIterator()
    {
        // IEnumerable<Target> method using yield return — return type has Target
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public System.Collections.Generic.IEnumerable<Target> GetAll() {
                    yield return new Target();
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === ASYNC METHOD RETURN TYPES ===

    [Fact]
    public void Sweep_AsyncMethodReturningTarget()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public async System.Threading.Tasks.Task<Target> GetAsync() {
                    return await System.Threading.Tasks.Task.FromResult(new Target());
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === EXPRESSION-BODIED MEMBERS ===

    [Fact]
    public void Sweep_ExpressionBodiedProperty()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public Target Value => new Target();
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    [Fact]
    public void Sweep_ExpressionBodiedMethod()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public Target Get() => null;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === COLLECTION / OBJECT INITIALIZERS ===

    [Fact]
    public void Sweep_CollectionInitializerWithTargetType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do() { 
                    var list = new System.Collections.Generic.List<Target> { new Target() };
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === CONDITIONAL (TERNARY) WITH CASTS ===

    [Fact]
    public void Sweep_TernaryCastExpression()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public Target Maybe(bool b, object o) => b ? (Target)o : null;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === PARTIAL METHODS ===

    [Fact]
    public void Sweep_PartialMethodSignature()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public partial class Consumer {
                partial void Do(Target t);
            }
            namespace N { public partial class Consumer {
                partial void Do(Target t) {}
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === EXPLICIT INTERFACE IMPLEMENTATION ===

    [Fact]
    public void Sweep_ExplicitInterfaceMethod()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public interface IProducer { Target Create(); } }",
            @"namespace N { public class Consumer : IProducer {
                Target IProducer.Create() => null;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
        Assert.True(HasEdge(graph, "N.IProducer", "N.Target"));
    }

    // === LOCK STATEMENT (expression type) ===

    [Fact]
    public void Sweep_LockOnTargetTypeField()
    {
        // lock itself doesn't reference a type, but the field does
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                private Target _lock = new Target();
                public void Do() { lock (_lock) {} }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === INTERPOLATED STRING WITH EXPRESSION ===

    [Fact]
    public void Sweep_InterpolatedStringWithObjectCreation()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public override string ToString() => \"T\"; } }",
            @"namespace N { public class Consumer {
                public string Do() => $""value: {new Target()}"";
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === EXTENSION METHOD CALLING ON TARGET ===

    [Fact]
    public void Sweep_ExtensionMethodDefinition()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public static class Extensions {
                public static void DoSomething(this Target t) {}
            } }");
        Assert.True(HasEdge(graph, "N.Extensions", "N.Target"));
    }

    // === GOTO / LABEL / BREAK (no type refs) ===

    // No test needed — these don't reference types

    // === UNSAFE: SIZEOF, ADDRESS-OF ===

    [Fact]
    public void Sweep_SizeofExpression()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target { public int X; } }",
            @"namespace N { public unsafe class Consumer {
                public int Do() => sizeof(Target);
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === STATIC LOCAL FUNCTIONS ===

    [Fact]
    public void Sweep_StaticLocalFunction()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do() { 
                    static Target Create() => null;
                    Create();
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === GENERIC METHOD WITH CONSTRAINT IN LOCAL FUNCTION ===

    [Fact]
    public void Sweep_LocalFunctionGenericConstraint()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do() { 
                    T Create<T>() where T : Target, new() => new T();
                    Create<Target>();
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === DEFAULT LITERAL vs DEFAULT EXPRESSION ===

    [Fact]
    public void Sweep_DefaultLiteralInAssignment()
    {
        // `Target t = default;` — default literal, type inferred from left side
        // The type reference is on the local variable declaration, not default
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do() { Target t = default; }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === DISCARD PATTERN IN SWITCH ===

    [Fact]
    public void Sweep_DiscardInSwitchExpression()
    {
        // The _ discard doesn't reference a type — no gap expected
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public int Do(object o) => o switch { Target t => 1, _ => 0 };
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }

    // === RECORD INHERITANCE ===

    [Fact]
    public void Sweep_RecordInheritsRecord()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public record BaseRecord(int Id); }",
            "namespace N { public record DerivedRecord(int Id, string Name) : BaseRecord(Id); }");
        Assert.True(HasEdge(graph, "N.DerivedRecord", "N.BaseRecord"));
    }

    // === INTERFACE DEFAULT METHOD ===

    [Fact]
    public void Sweep_InterfaceDefaultMethod()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public interface IConsumer {
                Target Create() => null;
            } }");
        Assert.True(HasEdge(graph, "N.IConsumer", "N.Target"));
    }

    // === COVARIANT RETURN TYPE (C# 9) ===

    [Fact]
    public void Sweep_CovariantReturn()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Base { public virtual object Create() => null; } }",
            @"namespace N { public class Consumer : Base {
                public override Target Create() => null;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"));
    }
}
