using DependencyAnalyzer.Analysis;
using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Tests;

public class TransitiveFanInAnalyzerTests
{
    [Fact]
    public void DirectFanIn_SingleDependor()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A { private Target _t; } }");

        Assert.Single(result.FanInElements);
        Assert.Equal("N.A", result.FanInElements[0].FullyQualifiedName);
    }

    [Fact]
    public void TransitiveFanIn_TwoLevelsDeep()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }",
            "namespace N { public class B : A {} }");

        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();
        Assert.Contains("N.A", fqns);
        Assert.Contains("N.B", fqns);
        Assert.Equal(2, result.TotalFanInCount);
    }

    [Fact]
    public void TransitiveFanIn_ThreeLevelsDeep()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }",
            "namespace N { public class B : A {} }",
            "namespace N { public class C : B {} }");

        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();
        Assert.Contains("N.A", fqns);
        Assert.Contains("N.B", fqns);
        Assert.Contains("N.C", fqns);
        Assert.Equal(3, result.TotalFanInCount);
    }

    [Fact]
    public void NoFanIn_WhenNoDependers()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class Unrelated {} }");

        Assert.Empty(result.FanInElements);
    }

    [Fact]
    public void TargetExcluded_FromResults()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }");

        Assert.DoesNotContain(result.FanInElements,
            e => e.FullyQualifiedName == "N.Target");
    }

    [Fact]
    public void CircularDependency_DoesNotCauseInfiniteLoop()
    {
        // A depends on Target via field, B depends on A, A also depends on B
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A { private Target _t; private B _b; } }",
            "namespace N { public class B { private A _a; } }");

        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();
        Assert.Contains("N.A", fqns);
        Assert.Contains("N.B", fqns);
    }

    [Fact]
    public void ElementKinds_AreCorrectlyMapped()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public interface IFoo { Target Get(); } }",
            "namespace N { public struct Bar { public Target Field; } }");

        var iface = result.FanInElements.First(e => e.FullyQualifiedName == "N.IFoo");
        Assert.Equal(ElementKind.Interface, iface.Kind);

        var strukt = result.FanInElements.First(e => e.FullyQualifiedName == "N.Bar");
        Assert.Equal(ElementKind.Struct, strukt.Kind);
    }

    [Fact]
    public void Justification_ContainsReasonForDirectFanIn()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }");

        var element = result.FanInElements.First(e => e.FullyQualifiedName == "N.A");
        Assert.Contains("Target", element.Justification);
    }

    [Fact]
    public void Justification_ContainsTransitiveChain()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }",
            "namespace N { public class B : A {} }");

        var element = result.FanInElements.First(e => e.FullyQualifiedName == "N.B");
        Assert.Contains("transitive", element.Justification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Metrics_CorrectCountPerKind()
    {
        var result = TestHelper.Analyze("N.Target",
            "namespace N { public class Target {} }",
            "namespace N { public class A : Target {} }",
            "namespace N { public class B : Target {} }",
            "namespace N { public interface IC { Target Get(); } }");

        Assert.Equal(2, result.MetricsByKind[ElementKind.Class]);
        Assert.Equal(1, result.MetricsByKind[ElementKind.Interface]);
        Assert.Equal(3, result.TotalFanInCount);
    }
}
