namespace SampleApp.Core;

/// <summary>
/// Enum used BY OrderService — OrderService depends on this, not the other way around.
/// EXPECTED: NOT a fan-in (OrderService uses OrderStatus, not the reverse).
/// </summary>
public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}
