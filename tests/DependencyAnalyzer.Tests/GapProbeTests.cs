namespace DependencyAnalyzer.Tests;

/// <summary>
/// Probe tests to discover remaining gaps in DependencyVisitor coverage.
/// Each test targets a specific C# construct that could reference a type.
/// Any failure here = a gap in the analyzer.
/// </summary>
public class GapProbeTests
{
    private static bool HasEdge(Models.DependencyGraph graph, string source, string target)
    {
        return graph.Edges.Values.SelectMany(e => e)
            .Any(d => d.SourceFqn == source && d.TargetFqn == target);
    }

    // === VARIABLE DECLARATIONS IN NON-LOCAL CONTEXTS ===

    [Fact]
    public void Probe_UsingBlockForm()
    {
        // using (Target t = ...) {} — VariableDeclaration inside UsingStatement
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target : System.IDisposable { public void Dispose() {} } }",
            @"namespace N { public class Consumer { 
                public void Do() { using (Target t = null) {} } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: using block form variable type");
    }

    [Fact]
    public void Probe_ForStatementInitializer()
    {
        // for (Target t = ...; ...) — VariableDeclaration inside ForStatement
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do() { for (Target t = null; t == null; ) { break; } } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: for statement initializer type");
    }

    // === DECLARATION EXPRESSIONS ===

    [Fact]
    public void Probe_OutVariableDeclaration()
    {
        // Method(out Target t) — DeclarationExpression
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                static void Get(out Target t) { t = null; }
                public void Do() { Get(out Target t); } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: out variable declaration type");
    }

    [Fact]
    public void Probe_TupleDeconstruction()
    {
        // (Target a, int b) = ... — DeclarationExpression in deconstruction
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                static (Target, int) Get() => (null, 0);
                public void Do() { (Target a, int b) = Get(); } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: tuple deconstruction type");
    }

    // === LINQ ===

    [Fact]
    public void Probe_LinqFromClauseType()
    {
        // from Target t in collection — explicit type in LINQ from clause
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { 
                using System.Linq;
                public class Consumer { 
                    public void Do(object[] items) { 
                        var q = from Target t in items select t; 
                    } 
                } 
              }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: LINQ from clause type");
    }

    // === REF TYPES ===

    [Fact]
    public void Probe_RefReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public static Target Instance; } }",
            @"namespace N { public class Consumer { 
                private Target _t;
                public ref Target GetRef() => ref _t; 
              } }");
        // Check specifically for "Method return type" reason to verify ref unwrapping
        var edges = graph.Edges.Values.SelectMany(e => e)
            .Where(d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target").ToList();
        Assert.True(edges.Any(e => e.DependencyReason.Contains("return")), "MISSING: ref return type");
    }

    [Fact]
    public void Probe_RefParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do(ref Target t) {} 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: ref parameter type");
    }

    [Fact]
    public void Probe_InParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do(in Target t) {} 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: in parameter type");
    }

    // === POINTER TYPES ===

    [Fact]
    public void Probe_PointerFieldType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target {} }",
            @"namespace N { public unsafe class Consumer { 
                private Target* _ptr; 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: pointer field type");
    }

    // === STACKALLOC ===

    [Fact]
    public void Probe_StackAllocType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target {} }",
            @"namespace N { public class Consumer { 
                public unsafe void Do() { Target* p = stackalloc Target[5]; } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: stackalloc type");
    }

    // === FIXED STATEMENT ===

    [Fact]
    public void Probe_FixedStatement()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target { public int Value; } }",
            @"namespace N { public class Consumer { 
                public unsafe void Do() { 
                    Target t = new Target();
                    fixed (int* p = &t.Value) {} 
                } 
              } }");
        // This specific case uses int* not Target*, so it's fine — 
        // but Target is referenced via local variable type which we catch.
        // The real gap would be fixed (Target* p = ...) which needs a Target array.
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: fixed statement");
    }

    // === EXPLICIT INTERFACE IMPLEMENTATION ===

    [Fact]
    public void Probe_ExplicitInterfaceImplementation()
    {
        // The interface type in the explicit member name: ITarget.Method
        // This is already covered because Consumer must implement ITarget (base list)
        var graph = TestHelper.BuildGraph(
            "namespace N { public interface ITarget { void Do(); } }",
            @"namespace N { public class Consumer : ITarget { 
                void ITarget.Do() {} 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.ITarget"), "MISSING: explicit interface impl");
    }

    // === USING DECLARATION (not block) ===

    [Fact]
    public void Probe_UsingDeclarationForm()
    {
        // using Target t = ...; — LocalDeclarationStatement with using keyword
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target : System.IDisposable { public void Dispose() {} } }",
            @"namespace N { public class Consumer { 
                public void Do() { using Target t = null; } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: using declaration form");
    }

    // === DISCARD WITH TYPE ===

    [Fact]
    public void Probe_DiscardWithType()
    {
        // _ = SomeMethod() — no explicit type, not a gap
        // But: Method(out Target _) — IS a type reference
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                static void Get(out Target t) { t = null; }
                public void Do() { Get(out Target _); } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: discard with explicit type");
    }

    // === GENERIC METHOD INVOCATION WITH EXPLICIT TYPE ARGS ===

    [Fact]
    public void Probe_GenericMethodInvocationTypeArg()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                static T Create<T>() where T : new() => new T();
                public void Do() { var t = Create<Target>(); } 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: generic method type argument");
    }

    // === COLLECTION EXPRESSION / SPREAD ===

    [Fact]
    public void Probe_ExplicitArrayTypeInDeclaration()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                private Target[] _arr = System.Array.Empty<Target>(); 
              } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "MISSING: array field + generic call");
    }
}
