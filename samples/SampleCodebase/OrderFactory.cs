namespace SampleApp.Services;

using SampleApp.Core;

/// <summary>
/// Class that accesses a static member of OrderService.
/// EXPECTED: Direct fan-in (Static member access).
/// </summary>
public class OrderFactory
{
    public OrderService Create()
    {
        return OrderService.CreateDefault();
    }
}
