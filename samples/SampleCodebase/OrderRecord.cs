namespace SampleApp.Core;

/// <summary>
/// Record with OrderService as a constructor parameter type.
/// EXPECTED: Direct fan-in (Method parameter type via primary constructor).
/// </summary>
public record OrderRecord(OrderService Service, string Description);
