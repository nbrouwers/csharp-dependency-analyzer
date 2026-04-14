namespace SampleApp.Core;

/// <summary>
/// TARGET CLASS for fan-in analysis.
/// This is the class we want to move to a new assembly.
/// </summary>
public class OrderService
{
    public int OrderCount { get; set; }

    public void ProcessOrder(string orderId)
    {
        // Business logic
    }

    public static OrderService CreateDefault()
    {
        return new OrderService();
    }
}
