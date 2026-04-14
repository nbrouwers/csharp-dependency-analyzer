namespace DependencyAnalyzer.Tests;

/// <summary>
/// Round 3 audit: systematic probe against the full C# language specification.
/// Every test here targets a specific construct. Failures = analyzer gaps.
/// </summary>
public class Round3AuditProbeTests
{
    private static bool HasEdge(Models.DependencyGraph graph, string source, string target)
        => graph.Edges.Values.SelectMany(e => e).Any(d => d.SourceFqn == source && d.TargetFqn == target);

    private static bool HasFanIn(Models.AnalysisResult result, string fqn)
        => result.FanInElements.Any(e => e.FullyQualifiedName == fqn);

    // =====================================================================
    // A. EXPRESSION-LEVEL TYPE REFERENCES — NOT COVERED BY EXPLICIT VISITORS
    // =====================================================================

    [Fact]
    public void Probe_NameOfExpression()
    {
        // nameof(Target) — references the type but is an invocation expression
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public string Name => nameof(Target);
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: nameof(Target) expression");
    }

    [Fact]
    public void Probe_NameOfWithMember()
    {
        // nameof(Target.Member) — references the type via member access
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Value { get; set; } } }",
            @"namespace N { public class Consumer {
                public string Name => nameof(Target.Value);
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: nameof(Target.Member) expression");
    }

    [Fact]
    public void Probe_UsingStaticBareMethodCall()
    {
        // using static Target; then bare Method() call — no MemberAccessExpression
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public static void DoWork() {} } }",
            @"using static N.Target;
              namespace N { public class Consumer {
                public void M() { DoWork(); }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: using static + bare method call");
    }

    [Fact]
    public void Probe_UsingStaticBarePropertyAccess()
    {
        // using static + static property access without qualifier
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public static int Value => 42; } }",
            @"using static N.Target;
              namespace N { public class Consumer {
                public int M() { return Value; }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: using static + bare property access");
    }

    // =====================================================================
    // B. LINQ QUERY EXPRESSIONS
    // =====================================================================

    [Fact]
    public void Probe_LinqJoinClauseWithExplicitType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Id { get; set; } } }",
            @"namespace N {
                using System.Linq;
                public class Consumer {
                    public void Do(object[] items, object[] others) {
                        var q = from object a in items 
                                join Target b in others on 1 equals 1 
                                select a;
                    }
                }
            }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: LINQ join clause explicit type");
    }

    // =====================================================================
    // C. FUNCTION POINTER TYPES (unsafe)
    // =====================================================================

    [Fact]
    public void Probe_FunctionPointerType()
    {
        // delegate*<Target, void> — function pointer with Target parameter
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public unsafe class Consumer {
                private delegate*<Target, void> _fp;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: function pointer type");
    }

    // =====================================================================
    // D. PATTERN MATCHING — EXHAUSTIVE PATTERNS
    // =====================================================================

    [Fact]
    public void Probe_ListPatternWithType()
    {
        // C# 11 list pattern: `is [Target a, ..]`
        // Actually, list patterns don't reference types directly in the syntax.
        // The element pattern can be a DeclarationPattern which we handle.
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public bool Check(object[] items) {
                    return items is [Target a, ..];
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: list pattern with declaration");
    }

    [Fact]
    public void Probe_PositionalPatternWithType()
    {
        // (Deconstruct) pattern: `is Target(var x, var y)`
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                public class Target { 
                    public int A { get; set; }
                    public void Deconstruct(out int a, out int b) { a = A; b = 0; }
                } 
            }",
            @"namespace N { public class Consumer {
                public bool Check(object obj) { return obj is Target(var a, var b); }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: positional pattern with type");
    }

    [Fact]
    public void Probe_CombinedOrPatternWithTypes()
    {
        // `is Target1 or Target2`
        var graph = TestHelper.BuildGraph(
            "namespace N { public class T1 {} }",
            "namespace N { public class T2 {} }",
            @"namespace N { public class Consumer {
                public bool Check(object obj) { return obj is T1 or T2; }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.T1") && HasEdge(graph, "N.Consumer", "N.T2"),
            "GAP: or-pattern with multiple types");
    }

    [Fact]
    public void Probe_SwitchStatementCaseLabel()
    {
        // Traditional switch with type pattern in case label
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do(object obj) {
                    switch (obj) {
                        case Target t: break;
                        default: break;
                    }
                }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: switch case declaration pattern");
    }

    // =====================================================================
    // E. SCOPED / REF PARAMS
    // =====================================================================

    [Fact]
    public void Probe_ScopedRefParam()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target {} }",
            @"namespace N { public class Consumer {
                public void Do(scoped ref Target t) { }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: scoped ref parameter");
    }

    [Fact]
    public void Probe_RefReadonlyParam()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do(ref readonly Target t) { }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: ref readonly parameter");
    }

    [Fact]
    public void Probe_ParamsArray()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer {
                public void Do(params Target[] items) { }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: params array type");
    }

    // =====================================================================
    // F. ATTRIBUTES — VARIOUS POSITIONS
    // =====================================================================

    [Fact]
    public void Probe_AttributeOnMethod()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public class TargetAttr : System.Attribute {} 
            }",
            @"namespace N { public class Consumer {
                [TargetAttr]
                public void Do() {}
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.TargetAttr"), "GAP: attribute on method");
    }

    [Fact]
    public void Probe_AttributeOnParameter()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                [System.AttributeUsage(System.AttributeTargets.Parameter)]
                public class TargetAttr : System.Attribute {} 
            }",
            @"namespace N { public class Consumer {
                public void Do([TargetAttr] string s) {}
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.TargetAttr"), "GAP: attribute on parameter");
    }

    [Fact]
    public void Probe_AttributeOnReturnValue()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                [System.AttributeUsage(System.AttributeTargets.ReturnValue)]
                public class TargetAttr : System.Attribute {} 
            }",
            @"namespace N { public class Consumer {
                [return: TargetAttr]
                public string Do() => """";
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.TargetAttr"), "GAP: attribute on return value");
    }

    [Fact]
    public void Probe_AttributeOnProperty()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                [System.AttributeUsage(System.AttributeTargets.Property)]
                public class TargetAttr : System.Attribute {} 
            }",
            @"namespace N { public class Consumer {
                [TargetAttr]
                public int Value { get; set; }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.TargetAttr"), "GAP: attribute on property");
    }

    [Fact]
    public void Probe_AttributeOnAssembly()
    {
        // Assembly-level attributes are outside any type — should NOT produce an edge
        // (no _currentTypeFqn). This tests that we don't crash.
        var graph = TestHelper.BuildGraph(
            @"[assembly: System.Reflection.AssemblyTitle(""test"")]
              namespace N { public class Target {} }");
        // Should not throw, and no edges from assembly level
        Assert.NotNull(graph);
    }

    [Fact]
    public void Probe_TypeOfInAttribute()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { 
                [System.ComponentModel.TypeConverter(typeof(Target))]
                public class Consumer {} 
            }");
        // typeof(Target) inside an attribute argument — the walker visits it
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: typeof() in attribute argument");
    }

    // =====================================================================
    // G. NESTED TYPES
    // =====================================================================

    [Fact]
    public void Probe_NestedTypeDependency()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                public class Outer { 
                    public class Target {} 
                } 
            }",
            @"namespace N { public class Consumer {
                private Outer.Target _t;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Outer.Target"), "GAP: nested type reference");
    }

    [Fact]
    public void Probe_InnerTypeDependsOnOuter()
    {
        var graph = TestHelper.BuildGraph(
            @"namespace N { 
                public class Target {} 
                public class Outer { 
                    public class Inner { 
                        private Target _t; 
                    } 
                } 
            }");
        Assert.True(HasEdge(graph, "N.Outer.Inner", "N.Target"), "GAP: inner type depends on outer scope type");
    }

    // =====================================================================
    // H. DELEGATE / EVENT SCENARIOS
    // =====================================================================

    [Fact]
    public void Probe_DelegateReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public delegate Target MyDelegate(); }");
        Assert.True(HasEdge(graph, "N.MyDelegate", "N.Target"), "GAP: delegate return type");
    }

    [Fact]
    public void Probe_EventWithDelegateType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public delegate void Handler(int x); }",
            @"namespace N { public class Consumer {
                public event Handler OnEvent;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Handler"), "GAP: event with delegate type");
    }

    // =====================================================================
    // I. STATIC ABSTRACT / VIRTUAL INTERFACE MEMBERS (C# 11)
    // =====================================================================

    [Fact]
    public void Probe_StaticAbstractInterfaceMember()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public interface IConsumer {
                static abstract Target Create();
            } }");
        Assert.True(HasEdge(graph, "N.IConsumer", "N.Target"), "GAP: static abstract interface member return type");
    }

    // =====================================================================
    // J. THROW EXPRESSIONS
    // =====================================================================

    [Fact]
    public void Probe_ThrowExpressionWithNew()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class TargetEx : System.Exception { public TargetEx() {} } }",
            @"namespace N { public class Consumer {
                public string M(string s) => s ?? throw new TargetEx();
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.TargetEx"), "GAP: throw expression with new");
    }

    // =====================================================================
    // K. REF STRUCT / REF FIELDS (C# 11)
    // =====================================================================

    [Fact]
    public void Probe_RefFieldType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target {} }",
            @"namespace N { public ref struct Consumer {
                private ref Target _t;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: ref field type in ref struct");
    }

    // =====================================================================
    // L. GENERIC BASE CLASSES / INTERFACES
    // =====================================================================

    [Fact]
    public void Probe_GenericBaseClassWithTypeArg()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Base<T> {} }",
            "namespace N { public class Consumer : Base<Target> {} }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: generic base class type argument");
    }

    [Fact]
    public void Probe_GenericInterfaceWithTypeArg()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public interface IGeneric<T> {} }",
            "namespace N { public class Consumer : IGeneric<Target> {} }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: generic interface type argument");
    }

    // =====================================================================
    // M. MISCELLANEOUS EXPRESSIONS
    // =====================================================================

    [Fact]
    public void Probe_ConditionalAccessExpression()
    {
        // This doesn't create a new type dep — the instance type is caught via its declaration
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Value { get; set; } } }",
            @"namespace N { public class Consumer {
                public int? M(Target t) => t?.Value;
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: conditional access expression");
    }

    [Fact]
    public void Probe_ImplicitStackAllocSpan()
    {
        // Span<Target> = stackalloc Target[n]
        var graph = TestHelper.BuildGraph(
            "namespace N { public struct Target {} }",
            @"namespace N { public class Consumer {
                public void Do() { System.Span<Target> s = stackalloc Target[5]; }
            } }");
        Assert.True(HasEdge(graph, "N.Consumer", "N.Target"), "GAP: Span stackalloc");
    }

    // =====================================================================
    // N. TRANSITIVE FAN-IN EDGE CASES
    // =====================================================================

    [Fact]
    public void Probe_DiamondDependency()
    {
        // A → Target, B → Target, C → A + B (transitive via two paths)
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }",
            "namespace N { public class B { private Target _t; } }",
            "namespace N { public class C { private A _a; private B _b; } }");

        Assert.True(HasFanIn(result, "N.A"));
        Assert.True(HasFanIn(result, "N.B"));
        Assert.True(HasFanIn(result, "N.C"));
    }

    [Fact]
    public void Probe_LongChainTransitive()
    {
        var result = TestHelper.Analyze("N.T",
            "namespace N { public class T {} }",
            "namespace N { public class A : T {} }",
            "namespace N { public class B : A {} }",
            "namespace N { public class C : B {} }",
            "namespace N { public class D : C {} }",
            "namespace N { public class E : D {} }");

        Assert.True(HasFanIn(result, "N.E"));
        Assert.Equal(5, result.TotalFanInCount);
    }

    [Fact]
    public void Probe_MutualDependencyWithTarget()
    {
        // A depends on Target, B depends on A, A also depends on B (circular among non-target)
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A { private Target _t; private B _b; } }",
            "namespace N { public class B { private A _a; } }");

        Assert.True(HasFanIn(result, "N.A"));
        Assert.True(HasFanIn(result, "N.B"));
    }
}
