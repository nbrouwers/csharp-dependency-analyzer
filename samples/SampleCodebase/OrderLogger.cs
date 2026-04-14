namespace SampleApp.Services;

using SampleApp.Core;

/// <summary>
/// Class with a local variable of type OrderService.
/// EXPECTED: Direct fan-in (Local variable type).
/// </summary>
public class OrderLogger
{
    public void LogOrder()
    {
        OrderService service = new OrderService();
        Console.WriteLine($"Logging order count: {service.OrderCount}");
    }
}
