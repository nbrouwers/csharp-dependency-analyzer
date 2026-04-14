# Dependency Analysis Report

## Target: `SampleApp.Core.OrderService`

## Date: 2026-04-14 13:07:03 UTC

## Fan-In Elements

| # | Fully Qualified Name | Kind | Justification |
|---|----------------------|------|---------------|
| 1 | `SampleApp.Core.IOrderRepository` | Interface | Generic type argument `OrderService` |
| 2 | `SampleApp.Core.OrderCreatedHandler` | Delegate | Delegate parameter type `OrderService` |
| 3 | `SampleApp.Core.OrderRecord` | Record | Record primary constructor parameter type `OrderService` |
| 4 | `SampleApp.Core.OrderValidator` | Class | Field type `OrderService` |
| 5 | `SampleApp.Data.OrderEntity` | Struct | typeof() expression `OrderService` |
| 6 | `SampleApp.Services.AdminController` | Class | Inherits from `OrderController` (which is a transitive fan-in of `OrderService`) |
| 7 | `SampleApp.Services.OrderController` | Class | Field type `OrderService` |
| 8 | `SampleApp.Services.OrderFactory` | Class | Method return type `OrderService` |
| 9 | `SampleApp.Services.OrderInspector` | Class | Type check (is/as) `OrderService` |
| 10 | `SampleApp.Services.OrderLogger` | Class | Local variable type `OrderService` |
| 11 | `SampleApp.Services.OrderPipeline` | Class | Constructor parameter type `OrderService` |

## Metrics

| Kind | Count |
|------|-------|
| Class | 7 |
| Interface | 1 |
| Struct | 1 |
| Record | 1 |
| Delegate | 1 |
| **Total** | **11** |
| **Max Transitive Depth** | **2** |

## Dependency Graph

```mermaid
graph LR
    n0["OrderService"]:::target
    n1["IOrderRepository"]:::interface
    n2["OrderCreatedHandler"]:::delegate
    n3["OrderRecord"]:::record
    n4["OrderValidator"]:::class
    n5["OrderEntity"]:::struct
    n6["AdminController"]:::class
    n7["OrderController"]:::class
    n8["OrderFactory"]:::class
    n9["OrderInspector"]:::class
    n10["OrderLogger"]:::class
    n11["OrderPipeline"]:::class
    n1 --> n0
    n4 --> n0
    n11 --> n0
    n3 --> n0
    n2 --> n0
    n5 --> n0
    n7 --> n0
    n8 --> n0
    n9 --> n0
    n10 --> n0
    n6 --> n7
    classDef target fill:#f96,stroke:#333,stroke-width:2px
    classDef class fill:#bbf,stroke:#333
    classDef interface fill:#bfb,stroke:#333
    classDef struct fill:#fbf,stroke:#333
    classDef enum fill:#ffb,stroke:#333
    classDef record fill:#bff,stroke:#333
    classDef delegate fill:#fbb,stroke:#333
```

