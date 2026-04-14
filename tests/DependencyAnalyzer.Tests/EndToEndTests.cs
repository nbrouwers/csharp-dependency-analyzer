using DependencyAnalyzer.Models;

namespace DependencyAnalyzer.Tests;

public class EndToEndTests
{
    [Fact]
    public void SampleScenario_InheritanceAndFieldType_CorrectFanIn()
    {
        var target = @"
namespace SampleApp.Core
{
    public class OrderService
    {
        public int OrderCount { get; set; }
        public void ProcessOrder(string orderId) {}
        public static OrderService CreateDefault() => new OrderService();
    }
}";
        var validator = @"
namespace SampleApp.Core
{
    public class OrderValidator
    {
        private readonly OrderService _orderService;
        public OrderValidator(OrderService orderService) { _orderService = orderService; }
        public bool Validate() => _orderService.OrderCount > 0;
    }
}";
        var pipeline = @"
namespace SampleApp.Services
{
    using SampleApp.Core;
    public class OrderPipeline : OrderValidator
    {
        public OrderPipeline(OrderService os) : base(os) {}
    }
}";
        var controller = @"
namespace SampleApp.Services
{
    using SampleApp.Core;
    public class OrderController
    {
        private readonly OrderService _service;
        public OrderController() { _service = new OrderService(); }
    }
}";
        var admin = @"
namespace SampleApp.Services
{
    public class AdminController : OrderController
    {
    }
}";
        var unrelated = @"
namespace SampleApp.Utils
{
    public class Helpers
    {
        public static string Format(decimal d) => d.ToString();
    }
}";

        var result = TestHelper.Analyze("SampleApp.Core.OrderService",
            target, validator, pipeline, controller, admin, unrelated);

        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();

        // Direct fan-in
        Assert.Contains("SampleApp.Core.OrderValidator", fqns);
        Assert.Contains("SampleApp.Services.OrderController", fqns);

        // Transitive fan-in
        Assert.Contains("SampleApp.Services.OrderPipeline", fqns);
        Assert.Contains("SampleApp.Services.AdminController", fqns);

        // Not fan-in
        Assert.DoesNotContain("SampleApp.Core.OrderService", fqns);
        Assert.DoesNotContain("SampleApp.Utils.Helpers", fqns);
    }

    [Fact]
    public void SampleScenario_AllDependencyTypes()
    {
        var target = @"
namespace N
{
    public class Target
    {
        public static int Value = 42;
    }
}";
        var sources = new[]
        {
            target,
            "namespace N { public class ViaField { private Target _t; } }",
            "namespace N { public class ViaInheritance : Target {} }",
            "namespace N { public interface ViaReturn { Target Get(); } }",
            "namespace N { public class ViaNew { void M() { var t = new Target(); } } }",
            "namespace N { public class ViaTypeOf { System.Type T() { return typeof(Target); } } }",
            "namespace N { public class ViaStatic { int M() { return Target.Value; } } }",
            "namespace N { public class ViaLocal { void M() { Target t = null; } } }",
            "namespace N { public class NoRef { void M() { } } }",
        };

        var result = TestHelper.Analyze("N.Target", sources);
        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();

        Assert.Contains("N.ViaField", fqns);
        Assert.Contains("N.ViaInheritance", fqns);
        Assert.Contains("N.ViaReturn", fqns);
        Assert.Contains("N.ViaNew", fqns);
        Assert.Contains("N.ViaTypeOf", fqns);
        Assert.Contains("N.ViaStatic", fqns);
        Assert.Contains("N.ViaLocal", fqns);
        Assert.DoesNotContain("N.NoRef", fqns);
        Assert.Equal(7, result.TotalFanInCount);
    }

    [Fact]
    public void SampleScenario_DelegateAndRecord_AreDetected()
    {
        var target = @"namespace N { public class Target {} }";
        var record = @"namespace N { public record MyRecord(Target T, string S); }";
        var del = @"namespace N { public delegate void MyHandler(Target t); }";

        var result = TestHelper.Analyze("N.Target", target, record, del);
        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();

        Assert.Contains("N.MyRecord", fqns);
        Assert.Contains("N.MyHandler", fqns);

        var rec = result.FanInElements.First(e => e.FullyQualifiedName == "N.MyRecord");
        Assert.Equal(ElementKind.Record, rec.Kind);

        var handler = result.FanInElements.First(e => e.FullyQualifiedName == "N.MyHandler");
        Assert.Equal(ElementKind.Delegate, handler.Kind);
    }

    [Fact]
    public void SampleScenario_StructWithTypeOf_Detected()
    {
        var target = @"namespace N { public class Target {} }";
        var entity = @"namespace N { public struct MyEntity { public System.Type Get() => typeof(Target); } }";

        var result = TestHelper.Analyze("N.Target", target, entity);
        var fqns = result.FanInElements.Select(e => e.FullyQualifiedName).ToHashSet();

        Assert.Contains("N.MyEntity", fqns);
        var el = result.FanInElements.First(e => e.FullyQualifiedName == "N.MyEntity");
        Assert.Equal(ElementKind.Struct, el.Kind);
    }
}
