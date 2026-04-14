namespace DependencyAnalyzer.Tests;

/// <summary>
/// Tests covering all C# language constructs that create type dependencies.
/// Each test targets a specific construct to ensure comprehensive coverage.
/// </summary>
public class ComprehensiveDependencyTests
{
    // ==========================================
    // ARRAY TYPES
    // ==========================================

    [Fact]
    public void Detects_ArrayFieldType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private Target[] _items; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_JaggedArrayType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private Target[][] _items; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_ArrayMethodReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public Target[] Get() => null; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_ArrayMethodParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do(Target[] items) {} } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_ArrayCreationExpression()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do() { var arr = new Target[5]; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    // ==========================================
    // GENERIC TYPE CONSTRAINTS
    // ==========================================

    [Fact]
    public void Detects_GenericTypeConstraint_OnClass()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer<T> where T : Target {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer<T>" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("constraint"));
    }

    [Fact]
    public void Detects_GenericTypeConstraint_OnMethod()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do<T>() where T : Target {} } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("constraint"));
    }

    [Fact]
    public void Detects_GenericTypeConstraint_Interface()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public interface ITarget {} }",
            "namespace N { public class Consumer<T> where T : ITarget {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer<T>" && d.TargetFqn == "N.ITarget"
                 && d.DependencyReason.Contains("constraint"));
    }

    // ==========================================
    // CATCH CLAUSE
    // ==========================================

    [Fact]
    public void Detects_CatchClauseExceptionType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class MyException : System.Exception {} }",
            "namespace N { public class Consumer { public void Do() { try {} catch (MyException) {} } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.MyException"
                 && d.DependencyReason.Contains("Catch"));
    }

    // ==========================================
    // FOREACH
    // ==========================================

    [Fact]
    public void Detects_ForEachVariableType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do(System.Collections.Generic.List<Target> items) { 
                    foreach (Target item in items) {} 
                } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("ForEach"));
    }

    // ==========================================
    // PRIMARY CONSTRUCTORS (C# 12)
    // ==========================================

    [Fact]
    public void Detects_ClassPrimaryConstructor()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer(Target t) {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Primary constructor"));
    }

    [Fact]
    public void Detects_StructPrimaryConstructor()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public struct Consumer(Target t) {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Primary constructor"));
    }

    // ==========================================
    // EVENT PROPERTY DECLARATIONS
    // ==========================================

    [Fact]
    public void Detects_EventPropertyDeclaration()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public delegate void MyHandler(); }",
            @"namespace N { public class Consumer { 
                public event MyHandler OnSomething { add {} remove {} } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.MyHandler"
                 && d.DependencyReason.Contains("Event property"));
    }

    // ==========================================
    // INDEXER DECLARATIONS
    // ==========================================

    [Fact]
    public void Detects_IndexerReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public Target this[int i] { get => null; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Indexer return"));
    }

    [Fact]
    public void Detects_IndexerParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public int this[Target t] { get => 0; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Indexer parameter"));
    }

    // ==========================================
    // OPERATOR DECLARATIONS
    // ==========================================

    [Fact]
    public void Detects_OperatorParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public static Consumer operator +(Consumer a, Target b) => a; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Operator parameter"));
    }

    [Fact]
    public void Detects_OperatorReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public static Target operator +(Consumer a, Consumer b) => null; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Operator return"));
    }

    // ==========================================
    // CONVERSION OPERATOR DECLARATIONS
    // ==========================================

    [Fact]
    public void Detects_ImplicitConversionOperrator()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public static implicit operator Target(Consumer c) => null; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Conversion operator"));
    }

    [Fact]
    public void Detects_ExplicitConversionOperator()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public static explicit operator Consumer(Target t) => null; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Conversion operator"));
    }

    // ==========================================
    // LOCAL FUNCTIONS
    // ==========================================

    [Fact]
    public void Detects_LocalFunctionReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do() { Target Local() => null; Local(); } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Local function return"));
    }

    [Fact]
    public void Detects_LocalFunctionParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do() { void Local(Target t) {} Local(null); } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Local function parameter"));
    }

    // ==========================================
    // LAMBDA EXPRESSIONS
    // ==========================================

    [Fact]
    public void Detects_LambdaParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do() { 
                    System.Action<Target> a = (Target t) => {}; 
                } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Lambda parameter"));
    }

    // ==========================================
    // IMPLICIT OBJECT CREATION (target-typed new)
    // ==========================================

    [Fact]
    public void Detects_ImplicitObjectCreation()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private Target _t = new(); } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Implicit object creation"));
    }

    // ==========================================
    // DEFAULT EXPRESSION
    // ==========================================

    [Fact]
    public void Detects_DefaultExpression()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do() { var t = default(Target); } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("default"));
    }

    // ==========================================
    // PATTERN MATCHING (comprehensive)
    // ==========================================

    [Fact]
    public void Detects_DeclarationPattern_InSwitch()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do(object obj) { 
                    switch (obj) { case Target t: break; } 
                } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Pattern matching"));
    }

    [Fact]
    public void Detects_TypePattern_InSwitch()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public void Do(object obj) { 
                    var result = obj switch { Target => 1, _ => 0 }; 
                } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("Pattern matching"));
    }

    [Fact]
    public void Detects_RecursivePattern()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Value { get; set; } } }",
            @"namespace N { public class Consumer { 
                public bool Check(object obj) { 
                    return obj is Target { Value: > 0 }; 
                } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("recursive pattern"));
    }

    [Fact]
    public void Detects_NegatedPattern_IsNot()
    {
        // `is not Target` — the walker visits the inner TypePattern
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public bool Check(object obj) { return obj is not Target; } 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_SwitchExpressionArm_WithDeclarationPattern()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Value { get; set; } } }",
            @"namespace N { public class Consumer { 
                public int Get(object obj) => obj switch { 
                    Target t => t.Value, 
                    _ => 0 
                }; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    // ==========================================
    // TUPLE TYPES (verified via generic unwrapping)
    // ==========================================

    [Fact]
    public void Detects_TupleFieldWithTargetElement()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private (Target, string) _pair; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void Detects_TupleReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public (Target item, int count) Get() => default; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    // ==========================================
    // NULLABLE REFERENCE TYPES
    // ==========================================

    [Fact]
    public void Detects_NullableReferenceTypeField()
    {
        var compilation = TestHelper.CreateCompilation(
            "#nullable enable\nnamespace N { public class Target {} }",
            "#nullable enable\nnamespace N { public class Consumer { private Target? _t; } }");

        var builder = new Analysis.DependencyGraphBuilder(_ => { });
        var graph = builder.Build(compilation);

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    // ==========================================
    // NESTED GENERICS
    // ==========================================

    [Fact]
    public void Detects_NestedGenericType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<Target>> _map; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    // ==========================================
    // RECORD STRUCT
    // ==========================================

    [Fact]
    public void Detects_RecordStructPrimaryConstructor()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public record struct Consumer(Target T); }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void RecordStruct_IsClassifiedCorrectly()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public record struct MyRecordStruct(); }");

        Assert.Equal(Models.ElementKind.Record, graph.ElementKinds["N.MyRecordStruct"]);
    }

    // ==========================================
    // COMPLEX / COMBINED SCENARIOS
    // ==========================================

    [Fact]
    public void Detects_GenericArrayOfTargetInConstrainedMethod()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            @"namespace N { public class Consumer { 
                public T[] GetAll<T>() where T : Target => null; 
              } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target"
                 && d.DependencyReason.Contains("constraint"));
    }

    [Fact]
    public void Detects_MultipleConstructsInOneClass()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public int Value { get; set; } } }",
            @"namespace N { 
                public class Consumer { 
                    private Target _field;              // field
                    public Target Prop { get; set; }    // property
                    public Target Get() => null;        // return type
                    public void Set(Target t) {}        // parameter
                    public void Check(object o) {
                        if (o is Target t) {}           // pattern match
                        var x = (Target)o;              // cast
                        var y = typeof(Target);         // typeof
                        var z = new Target();           // new
                    }
                } 
              }");

        var edges = graph.Edges.Values.SelectMany(e => e)
            .Where(d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target")
            .Select(d => d.DependencyReason)
            .ToHashSet();

        Assert.Contains(edges, r => r.Contains("Field"));
        Assert.Contains(edges, r => r.Contains("Property"));
        Assert.Contains(edges, r => r.Contains("return"));
        Assert.Contains(edges, r => r.Contains("parameter"));
        Assert.Contains(edges, r => r.Contains("Pattern matching"));
        Assert.Contains(edges, r => r.Contains("cast"));
        Assert.Contains(edges, r => r.Contains("typeof"));
        Assert.Contains(edges, r => r.Contains("creation"));
    }
}
