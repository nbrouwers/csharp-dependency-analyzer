# Sample Codebase — Expected Fan-In Results

## Target Class

`SampleApp.Core.OrderService`

## Expected Fan-In Elements

The following elements should be identified as (transitive) fan-in of `OrderService`:

| # | Fully Qualified Name | Kind | Dependency Type | Direct/Transitive | Justification |
|---|----------------------|------|----------------|-------------------|---------------|
| 1 | `SampleApp.Core.IOrderRepository` | Interface | Generic type argument + method parameter | Direct | Extends `IRepository<OrderService>` and has method parameter of type `OrderService` |
| 2 | `SampleApp.Core.OrderValidator` | Class | Field type, constructor parameter | Direct | Has field `_orderService` of type `OrderService` |
| 3 | `SampleApp.Services.OrderPipeline` | Class | Inheritance | Transitive | Inherits from `OrderValidator` (which depends on `OrderService`) |
| 4 | `SampleApp.Core.OrderRecord` | Record | Constructor parameter type | Direct | Primary constructor parameter of type `OrderService` |
| 5 | `SampleApp.Core.OrderCreatedHandler` | Delegate | Delegate parameter type | Direct | Delegate signature includes `OrderService` parameter |
| 6 | `SampleApp.Data.OrderEntity` | Struct | typeof() expression | Direct | Uses `typeof(OrderService)` |
| 7 | `SampleApp.Services.OrderController` | Class | Object creation, field type | Direct | Creates `new OrderService()` and has field of type `OrderService` |
| 8 | `SampleApp.Services.AdminController` | Class | Inheritance | Transitive | Inherits from `OrderController` (which depends on `OrderService`) |
| 9 | `SampleApp.Services.OrderFactory` | Class | Static member access, method return type | Direct | Calls `OrderService.CreateDefault()` and returns `OrderService` |
| 10 | `SampleApp.Services.OrderInspector` | Class | Type check (is/as), pattern matching | Direct | Uses `is OrderService` and `as OrderService` |
| 11 | `SampleApp.Services.OrderLogger` | Class | Local variable type, object creation | Direct | Declares local variable of type `OrderService` |

## Expected Non-Fan-In Elements

The following elements should NOT be in the fan-in set:

| Fully Qualified Name | Kind | Reason for Exclusion |
|----------------------|------|---------------------|
| `SampleApp.Core.OrderService` | Class | Target class itself (excluded by definition) |
| `SampleApp.Core.OrderStatus` | Enum | Used BY `OrderService`, does not depend on it |
| `SampleApp.Attributes.OrderAttribute` | Class | No dependency on `OrderService` |
| `SampleApp.Utils.Helpers` | Class | No dependency on `OrderService` |
| `SampleApp.Services.NotificationService` | Class | No dependency on `OrderService` |
| `SampleApp.Core.IRepository<T>` | Interface | Generic interface, no direct reference to `OrderService` in its definition |

## Summary Metrics

| Kind | Count |
|------|-------|
| Class | 7 |
| Interface | 1 |
| Struct | 1 |
| Record | 1 |
| Delegate | 1 |
| Enum | 0 |
| **Total** | **11** |
