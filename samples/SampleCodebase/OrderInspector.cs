namespace SampleApp.Services;

using SampleApp.Core;

/// <summary>
/// Class that uses pattern matching / type check with OrderService.
/// EXPECTED: Direct fan-in (Type check via is/as and pattern matching).
/// </summary>
public class OrderInspector
{
    public bool IsOrderService(object obj)
    {
        return obj is OrderService;
    }

    public OrderService? AsOrderService(object obj)
    {
        return obj as OrderService;
    }

    public string Describe(object obj)
    {
        if (obj is OrderService svc)
        {
            return $"OrderService with {svc.OrderCount} orders";
        }
        return "Unknown";
    }
}
