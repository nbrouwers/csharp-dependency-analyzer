# Dependency Analysis Report

## Target: `SampleApp.Core.OrderService`

## Date: 2026-04-14 13:19:46 UTC

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
    n1["IOrderRepository"]:::kind_interface
    n2["OrderCreatedHandler"]:::kind_delegate
    n3["OrderRecord"]:::kind_record
    n4["OrderValidator"]:::kind_class
    n5["OrderEntity"]:::kind_struct
    n6["AdminController"]:::kind_class
    n7["OrderController"]:::kind_class
    n8["OrderFactory"]:::kind_class
    n9["OrderInspector"]:::kind_class
    n10["OrderLogger"]:::kind_class
    n11["OrderPipeline"]:::kind_class
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
    classDef kind_class fill:#bbf,stroke:#333
    classDef kind_interface fill:#bfb,stroke:#333
    classDef kind_struct fill:#fbf,stroke:#333
    classDef kind_enum fill:#ffb,stroke:#333
    classDef kind_record fill:#bff,stroke:#333
    classDef kind_delegate fill:#fbb,stroke:#333
```

