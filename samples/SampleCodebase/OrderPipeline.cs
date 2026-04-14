namespace SampleApp.Services;

using SampleApp.Core;

/// <summary>
/// Class that inherits from OrderValidator (which has a direct dependency on OrderService).
/// EXPECTED: Transitive fan-in (inherits OrderValidator, which depends on OrderService).
/// </summary>
public class OrderPipeline : OrderValidator
{
    public OrderPipeline(OrderService orderService) : base(orderService)
    {
    }

    public void RunPipeline()
    {
        if (Validate())
        {
            // Pipeline logic
        }
    }
}
