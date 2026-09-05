# FleetOps.Domain

The core domain layer for the FleetOps fleet and logistics management system. This project contains business entities, value objects, domain events, domain exceptions, and enums following Domain-Driven Design (DDD) principles with zero external dependencies.

## Aggregate Roots

The domain model is partitioned into 5 independent Aggregate Roots, referenced across boundaries strictly by identifier (`Guid`):

1. **`Vehicle`**: Represents physical fleet assets and owns operational readiness status, odometer readings, and driver assignment.
2. **`Driver`**: Represents operators holding credentials and contact information.
3. **`Delivery`**: Represents cargo shipment orders, delivery lifecycles, and priority handling.
4. **`Route`**: Represents logistical paths, stop schedules, and delivery manifests.
5. **`Maintenance`**: Represents individual maintenance and repair service records.

---

## Aggregate Boundary Contracts

### Vehicle ↔ Driver Relationship
* `Vehicle` is the sole owner of the driver assignment relationship via `Vehicle.CurrentDriverId`.
* `Driver` does not hold a reverse reference (`CurrentVehicleId`), preventing duplicate sources of truth.
* Assigning, replacing, or unassigning a driver is executed exclusively through `Vehicle.AssignDriver` and `Vehicle.UnassignDriver`.
* Reassignment explicitly emits `DriverUnassignedFromVehicleDomainEvent` for the previous driver prior to emitting `DriverAssignedToVehicleDomainEvent` for the newly assigned driver.
* Reverse lookups ("which vehicle is assigned to driver X?") are handled via Application/Infrastructure query services.

### Vehicle ↔ Maintenance Coordination Contract
`Vehicle` and `Maintenance` are intentionally modeled as independent Aggregate Roots:

#### 1. Responsibilities
* **`Vehicle`**:
  * Owns the vehicle's operational availability state (`Active`, `Inactive`, `UnderMaintenance`).
  * Enforces state transitions via `SendToMaintenance()` and `ReturnFromMaintenance()`.
  * Unassigns current drivers upon entering maintenance to protect operational invariants.
* **`Maintenance`**:
  * Owns the lifecycle and audit trail of a specific maintenance order (`Scheduled`, `InProgress`, `Completed`, `Cancelled`).
  * Maintains the association to a vehicle via `VehicleId` (`Guid`).
  * Enforces service timestamps, descriptions, maintenance categories, and monetary costs (`Money`).

#### 2. Cross-Aggregate Decoupling
* `Vehicle.SendToMaintenance()` does **not** automatically instantiate or start a `Maintenance` record.
* `Maintenance.Start()` does **not** automatically mutate the state of `Vehicle`.
* The Domain layer does not internally coordinate or couple multiple Aggregate Roots.

#### 3. Application Layer Orchestration
* The **Application Layer** is responsible for coordinating cross-aggregate lifecycles and workflows.
* When scheduling or executing maintenance, Application use cases orchestrate the transaction:
  * Mutating `Vehicle` operational readiness.
  * Mutating or creating `Maintenance` service records.
* Consistency across aggregate boundaries is governed at the Application boundary either:
  * **Transactionally**: within an application use case / unit of work when immediate consistency is required.
  * **Eventually**: driven by Domain Events (`VehicleSentToMaintenanceDomainEvent`, `MaintenanceCompletedDomainEvent`) dispatched to integration handlers.
