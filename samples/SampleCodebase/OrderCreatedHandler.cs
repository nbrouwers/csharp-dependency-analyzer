namespace SampleApp.Core;

/// <summary>
/// Delegate whose signature includes OrderService.
/// EXPECTED: Direct fan-in (Delegate parameter type).
/// </summary>
public delegate void OrderCreatedHandler(OrderService service, string orderId);
