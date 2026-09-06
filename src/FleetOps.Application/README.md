# FleetOps.Application

The **Application Layer** of FleetOps API contains enterprise business orchestration, use cases, persistence/event abstractions, DTOs, and application-specific exceptions according to Clean Architecture principles.

---

## 1. Architectural Role & Dependencies

Following Clean Architecture:
- `FleetOps.Application` depends **strictly and solely** on `FleetOps.Domain` and .NET System runtime libraries.
- It contains **zero** references to:
  - `FleetOps.Infrastructure`
  - `FleetOps.Api`
  - Entity Framework Core / DbContext / ORMs
  - ASP.NET Core / HTTP / MVC
  - PostgreSQL / Npgsql
  - RabbitMQ / Messaging
  - MediatR (use cases are explicit, typed service classes)

```text
       ┌──────────────────────────────┐
       │         FleetOps.Api         │
       └──────────────┬───────────────┘
                      │
       ┌──────────────▼───────────────┐
       │   FleetOps.Infrastructure    │
       └──────────────┬───────────────┘
                      │
       ┌──────────────▼───────────────┐
       │     FleetOps.Application     │  ◄── (This Layer)
       └──────────────┬───────────────┘
                      │
       ┌──────────────▼───────────────┐
       │       FleetOps.Domain        │
       └──────────────────────────────┘
```

---

## 2. Layer Structure & Organization

```text
FleetOps.Application/
├── Abstractions/
│   ├── Events/
│   │   └── IDomainEventDispatcher.cs
│   └── Persistence/
│       ├── IDeliveryRepository.cs
│       ├── IDriverRepository.cs
│       ├── IMaintenanceRepository.cs
│       ├── IRouteRepository.cs
│       ├── IUnitOfWork.cs
│       └── IVehicleRepository.cs
├── DTOs/
│   ├── AddressDto.cs
│   ├── DeliveryDto.cs
│   ├── DriverDto.cs
│   ├── MaintenanceDto.cs
│   ├── MappingExtensions.cs
│   ├── RouteDto.cs
│   └── VehicleDto.cs
├── Exceptions/
│   ├── ApplicationException.cs
│   ├── ConflictException.cs
│   ├── NotFoundException.cs
│   └── ValidationException.cs
└── UseCases/
    ├── Deliveries/
    │   ├── AssignDelivery.cs
    │   ├── CancelDelivery.cs
    │   ├── CompleteDelivery.cs
    │   ├── CreateDelivery.cs
    │   └── StartDelivery.cs
    ├── Drivers/
    │   ├── ActivateDriver.cs
    │   ├── DeactivateDriver.cs
    │   ├── RegisterDriver.cs
    │   └── SuspendDriver.cs
    ├── Maintenance/
    │   ├── CancelMaintenance.cs
    │   ├── CompleteMaintenance.cs
    │   ├── ScheduleMaintenance.cs
    │   └── StartMaintenance.cs
    ├── Routes/
    │   ├── AddDeliveryToRoute.cs
    │   ├── AssignRoute.cs
    │   ├── CancelRoute.cs
    │   ├── CompleteRoute.cs
    │   ├── CreateRoute.cs
    │   ├── RemoveDeliveryFromRoute.cs
    │   └── StartRoute.cs
    └── Vehicles/
        ├── ActivateVehicle.cs
        ├── AssignDriverToVehicle.cs
        ├── DeactivateVehicle.cs
        ├── RegisterVehicle.cs
        ├── ReturnVehicleFromMaintenance.cs
        ├── SendVehicleToMaintenance.cs
        ├── UnassignDriverFromVehicle.cs
        └── UpdateVehicleMileage.cs
```

---

## 3. Implemented Use Cases

### Vehicle Operations (`UseCases/Vehicles`)
* **`RegisterVehicleUseCase`**: Validates license plate uniqueness and vehicle type, constructs `Vehicle`, persists and commits via `IUnitOfWork`.
* **`ActivateVehicleUseCase`**: Validates existence and transitions vehicle to `Active`.
* **`DeactivateVehicleUseCase`**: Unassigns active driver (if any) and transitions vehicle to `Inactive`.
* **`AssignDriverToVehicleUseCase`**: Validates that driver exists and is in `Active` status; assigns driver to vehicle (`Vehicle` is the single source of truth for assignment).
* **`UnassignDriverFromVehicleUseCase`**: Clears driver assignment from vehicle.
* **`UpdateVehicleMileageUseCase`**: Validates non-decreasing mileage and updates aggregate.
* **`SendVehicleToMaintenanceUseCase`**: Unassigns driver and transitions vehicle to `UnderMaintenance`.
* **`ReturnVehicleFromMaintenanceUseCase`**: Validates that no active maintenance record (`Scheduled` or `InProgress`) is associated with the vehicle before transitioning from `UnderMaintenance` back to `Active`.

### Driver Operations (`UseCases/Drivers`)
* **`RegisterDriverUseCase`**: Validates license number uniqueness, creates driver in `Active` status, persists and commits.
* **`ActivateDriverUseCase`**: Activates an inactive driver.
* **`SuspendDriverUseCase`**: Transitions driver to `Suspended` with mandatory reason.
* **`DeactivateDriverUseCase`**: Deactivates an active driver.

### Delivery Operations (`UseCases/Deliveries`)
* **`CreateDeliveryUseCase`**: Validates tracking code uniqueness, parses priority and addresses, creates delivery in `Pending` status.
* **`AssignDeliveryUseCase`**: Verifies vehicle and driver exist, are `Active`, and validates vehicle capacity against delivery weight.
* **`StartDeliveryUseCase`**: Transitions delivery from `Assigned` to `InTransit`.
* **`CompleteDeliveryUseCase`**: Transitions delivery to `Delivered` with timestamp.
* **`CancelDeliveryUseCase`**: Cancels delivery with reason if not already delivered.

### Route Operations (`UseCases/Routes`)
* **`CreateRouteUseCase`**: Validates planned departure and estimated arrival chronologies, creates route in `Planned` status.
* **`AssignRouteUseCase`**: Validates vehicle and driver active statuses and assigns them to the planned route.
* **`AddDeliveryToRouteUseCase`**: Validates delivery status is eligible (`Pending` or `Assigned`) and associates delivery with planned route.
* **`RemoveDeliveryFromRouteUseCase`**: Disassociates delivery from planned route.
* **`StartRouteUseCase`**: Enforces that route has assigned vehicle, assigned driver, and at least one delivery before transitioning to `InProgress`.
* **`CompleteRouteUseCase`**: Enforces completion chronological consistency and transitions route to `Completed`.
* **`CancelRouteUseCase`**: Cancels route with reason.

### Maintenance Operations (`UseCases/Maintenance`)
* **`ScheduleMaintenanceUseCase`**: Validates vehicle existence, verifies no active maintenance is in progress/scheduled via `IMaintenanceRepository.GetActiveByVehicleIdAsync`, and creates maintenance in `Scheduled` status.
* **`StartMaintenanceUseCase`**: Coordinates aggregate state: transitions vehicle to `UnderMaintenance` and maintenance to `InProgress` atomically.
* **`CompleteMaintenanceUseCase`**: Finalizes maintenance with monetary cost and optionally transitions vehicle back to `Active`.
* **`CancelMaintenanceUseCase`**: Cancels maintenance with reason and optionally returns vehicle to `Active`.

---

## 4. Conventions and Architectural Guidelines

1. **Explicit Use Cases**: Each business workflow is represented by a pair of `[Action][Aggregate]Command` (sealed record) and `[Action][Aggregate]UseCase` (sealed class).
2. **Persistence & Transaction Boundary**: Repositories operate strictly on aggregate roots. Multi-aggregate mutations (e.g. `StartMaintenance`, `CompleteMaintenance`) are committed atomically through `IUnitOfWork.SaveChangesAsync`.
3. **Domain Events Lifecycle**:
   - Aggregates record domain events internally (`AggregateRoot.DomainEvents`).
   - Use cases execute domain logic and invoke `IUnitOfWork.SaveChangesAsync()`.
   - The persistence boundary (`IUnitOfWork`) commits database transactions first, and subsequently delegates event publishing to `IDomainEventDispatcher` (or outbox storage) before clearing aggregate events (`ClearDomainEvents()`).
   - This architectural contract guarantees that events are never dispatched if persistence fails or rolls back, preventing dual-write hazards without coupling Application to messaging infrastructure.
4. **Cross-Aggregate Coordination**:
   - `Vehicle ↔ Driver`: `Vehicle` is the sole owner of driver assignment (`Vehicle.CurrentDriverId`). Reassignment emits proper unassignment/assignment events and is idempotent.
   - `Vehicle ↔ Maintenance`: Coordinated by Application layer use cases. Direct return from maintenance is blocked if an active maintenance record exists.
   - `Route ↔ Delivery`: Deliveries can only be added to a route if they are in eligible lifecycle states (`Pending` or `Assigned`).
5. **CancellationToken Propagation**: All asynchronous methods across use cases, repositories, and dispatchers accept and propagate `CancellationToken`.
6. **Pure Application Exceptions**:
   - `NotFoundException`: Resource does not exist (maps to 404 in API layer).
   - `ConflictException`: Business rule conflict, duplicate unique identifier, or cross-aggregate invalid state (maps to 409 in API layer).
   - `ValidationException`: Input format or enum parsing error (maps to 400 in API layer).
7. **No Comments in C#**: Codebase adheres to self-documenting clean code practices with zero comments.
