namespace SampleApp.Core;

/// <summary>
/// Class with a field of type OrderService.
/// EXPECTED: Direct fan-in (Field type).
/// </summary>
public class OrderValidator
{
    private readonly OrderService _orderService;

    public OrderValidator(OrderService orderService)
    {
        _orderService = orderService;
    }

    public bool Validate()
    {
        return _orderService.OrderCount > 0;
    }
}
