namespace SampleApp.Core;

/// <summary>
/// Interface that uses OrderService as a generic constraint.
/// EXPECTED: Direct fan-in (Generic type argument referencing OrderService).
/// </summary>
public interface IOrderRepository : IRepository<OrderService>
{
    void Save(OrderService order);
}

public interface IRepository<T>
{
    // Generic base interface
}
