namespace SampleApp.Services;

using SampleApp.Core;

/// <summary>
/// Class that creates a new OrderService instance.
/// EXPECTED: Direct fan-in (Object creation).
/// </summary>
public class OrderController
{
    private readonly OrderService _service;

    public OrderController()
    {
        _service = new OrderService();
    }

    public void HandleRequest()
    {
        _service.ProcessOrder("ORD-001");
    }
}
