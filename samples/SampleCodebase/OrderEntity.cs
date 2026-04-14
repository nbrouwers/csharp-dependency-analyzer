namespace SampleApp.Data;

using SampleApp.Core;

/// <summary>
/// Struct that uses typeof(OrderService).
/// EXPECTED: Direct fan-in (typeof expression).
/// </summary>
public struct OrderEntity
{
    public string Id { get; set; }
    public string Name { get; set; }

    public Type GetServiceType()
    {
        return typeof(OrderService);
    }
}
