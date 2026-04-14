namespace DependencyAnalyzer.Tests;

public class DependencyGraphBuilderTests
{
    [Fact]
    public void Detects_Inheritance()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Base {} }",
            "namespace N { public class Derived : Base {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Derived" && d.TargetFqn == "N.Base" && d.DependencyReason.Contains("Inherits"));
    }

    [Fact]
    public void Detects_InterfaceImplementation()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public interface IFoo {} }",
            "namespace N { public class Foo : IFoo {} }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Foo" && d.TargetFqn == "N.IFoo" && d.DependencyReason.Contains("interface"));
    }

    [Fact]
    public void Detects_FieldType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private Target _t; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("Field"));
    }

    [Fact]
    public void Detects_PropertyType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public Target Prop { get; set; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("Property"));
    }

    [Fact]
    public void Detects_MethodParameterType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do(Target t) {} } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("parameter"));
    }

    [Fact]
    public void Detects_MethodReturnType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public Target Get() => null; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("return"));
    }

    [Fact]
    public void Detects_LocalVariableType()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do() { Target t = null; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("Local"));
    }

    [Fact]
    public void Detects_ObjectCreation()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public void Do() { object t = new Target(); } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("creation"));
    }

    [Fact]
    public void Detects_GenericTypeArgument()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { private System.Collections.Generic.List<Target> _list; } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" &&
                 (d.DependencyReason.Contains("Generic") || d.DependencyReason.Contains("Field")));
    }

    [Fact]
    public void Detects_TypeOfExpression()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public System.Type Get() { return typeof(Target); } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("typeof"));
    }

    [Fact]
    public void Detects_IsTypeCheck()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target {} }",
            "namespace N { public class Consumer { public bool Check(object o) { return o is Target; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" &&
                 (d.DependencyReason.Contains("check") || d.DependencyReason.Contains("is")));
    }

    [Fact]
    public void Detects_StaticMemberAccess()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { public static int Value = 1; } }",
            "namespace N { public class Consumer { public int Get() { return Target.Value; } } }");

        Assert.Contains(graph.Edges.Values.SelectMany(e => e),
            d => d.SourceFqn == "N.Consumer" && d.TargetFqn == "N.Target" && d.DependencyReason.Contains("Static"));
    }

    [Fact]
    public void Records_ElementKinds_Correctly()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class MyClass {} }",
            "namespace N { public interface IMyInterface {} }",
            "namespace N { public struct MyStruct {} }",
            "namespace N { public enum MyEnum { A } }",
            "namespace N { public record MyRecord(); }",
            "namespace N { public delegate void MyDelegate(); }");

        Assert.Equal(Models.ElementKind.Class, graph.ElementKinds["N.MyClass"]);
        Assert.Equal(Models.ElementKind.Interface, graph.ElementKinds["N.IMyInterface"]);
        Assert.Equal(Models.ElementKind.Struct, graph.ElementKinds["N.MyStruct"]);
        Assert.Equal(Models.ElementKind.Enum, graph.ElementKinds["N.MyEnum"]);
        Assert.Equal(Models.ElementKind.Record, graph.ElementKinds["N.MyRecord"]);
        Assert.Equal(Models.ElementKind.Delegate, graph.ElementKinds["N.MyDelegate"]);
    }

    [Fact]
    public void DoesNotRecord_SelfReference()
    {
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Target { private Target _self; } }");

        var allEdges = graph.Edges.Values.SelectMany(e => e).ToList();
        Assert.DoesNotContain(allEdges,
            d => d.SourceFqn == "N.Target" && d.TargetFqn == "N.Target");
    }

    [Fact]
    public void DoesNotRecord_OutOfScopeTypes()
    {
        // string is not in scope
        var graph = TestHelper.BuildGraph(
            "namespace N { public class Consumer { private string _name; } }");

        var allEdges = graph.Edges.Values.SelectMany(e => e).ToList();
        Assert.DoesNotContain(allEdges, d => d.TargetFqn.Contains("String"));
    }
}
