# FleetOps.Api

The **API Layer** of FleetOps API serves as the presentation boundary and HTTP adapter over the Application layer according to Clean Architecture principles.

---

## 1. Architectural Role & Boundary

The API layer is responsible exclusively for HTTP transport concerns:
- Receiving and binding incoming HTTP requests.
- Validating transport format and parsing JSON payloads into immutable command models.
- Invoking the corresponding Application use case (`ExecuteAsync(command, cancellationToken)`).
- Translating use-case results into semantic HTTP responses (`201 Created`, `200 OK`).
- Translating domain and application exceptions into standardized RFC 9457 `ProblemDetails` (`400 Bad Request`, `404 Not Found`, `409 Conflict`, `500 Internal Server Error`).
- Exposing OpenAPI documentation and service health checks.

### Strict Architectural Boundaries
- Controllers are thin HTTP adapters:
  - **Zero DbContext access**
  - **Zero repository access**
  - **Zero UnitOfWork access**
  - **Zero domain event dispatching**
  - **Zero business logic**
- Persistence concerns and outbox mechanics remain encapsulated in `FleetOps.Infrastructure`.
- Business rules remain encapsulated in `FleetOps.Domain` and `FleetOps.Application`.

```text
       ┌──────────────────────────────┐
       │         FleetOps.Api         │  ◄── (This Layer)
       └───────┬──────────────┬───────┘
               │              │
               │       ┌──────▼───────────────────────┐
               │       │   FleetOps.Infrastructure    │
               │       └──────────────┬───────────────┘
               │                      │
       ┌───────▼──────────────────────▼┐
       │     FleetOps.Application     │
       └──────────────┬───────────────┘
                      │
       ┌──────────────▼───────────────┐
       │       FleetOps.Domain        │
       └──────────────────────────────┘
```

---

## 2. Layer Organization

```text
FleetOps.Api/
├── Contracts/
│   ├── Deliveries/
│   │   ├── AssignDeliveryRequest.cs
│   │   ├── CancelDeliveryRequest.cs
│   │   ├── CompleteDeliveryRequest.cs
│   │   └── CreateDeliveryRequest.cs
│   ├── Drivers/
│   │   ├── RegisterDriverRequest.cs
│   │   └── SuspendDriverRequest.cs
│   ├── Maintenance/
│   │   ├── CancelMaintenanceRequest.cs
│   │   ├── CompleteMaintenanceRequest.cs
│   │   ├── ScheduleMaintenanceRequest.cs
│   │   └── StartMaintenanceRequest.cs
│   ├── Routes/
│   │   ├── AddDeliveryToRouteRequest.cs
│   │   ├── AssignRouteRequest.cs
│   │   ├── CancelRouteRequest.cs
│   │   ├── CompleteRouteRequest.cs
│   │   ├── CreateRouteRequest.cs
│   │   └── StartRouteRequest.cs
│   └── Vehicles/
│       ├── AssignDriverRequest.cs
│       ├── RegisterVehicleRequest.cs
│       └── UpdateVehicleMileageRequest.cs
├── Controllers/
│   ├── DeliveriesController.cs
│   ├── DriversController.cs
│   ├── MaintenancesController.cs
│   ├── RoutesController.cs
│   └── VehiclesController.cs
├── Extensions/
│   ├── ApplicationServiceExtensions.cs
│   └── HealthCheckExtensions.cs
├── Middleware/
│   └── GlobalExceptionHandler.cs
├── Program.cs
└── appsettings.json
```

---

## 3. Complete Endpoint Inventory

### Vehicles (`/api/vehicles`)
| Method | Route | Use Case | Success Status | Main Errors |
|:---|:---|:---|:---:|:---|
| `POST` | `/api/vehicles` | `RegisterVehicleUseCase` | `201 Created` | `400 Bad Request`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/activate` | `ActivateVehicleUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/deactivate` | `DeactivateVehicleUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/assign-driver` | `AssignDriverToVehicleUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/unassign-driver` | `UnassignDriverFromVehicleUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/mileage` | `UpdateVehicleMileageUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found` |
| `POST` | `/api/vehicles/{id}/send-to-maintenance` | `SendVehicleToMaintenanceUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/vehicles/{id}/return-from-maintenance` | `ReturnVehicleFromMaintenanceUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |

### Drivers (`/api/drivers`)
| Method | Route | Use Case | Success Status | Main Errors |
|:---|:---|:---|:---:|:---|
| `POST` | `/api/drivers` | `RegisterDriverUseCase` | `201 Created` | `400 Bad Request`, `409 Conflict` |
| `POST` | `/api/drivers/{id}/activate` | `ActivateDriverUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/drivers/{id}/deactivate` | `DeactivateDriverUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/drivers/{id}/suspend` | `SuspendDriverUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |

### Deliveries (`/api/deliveries`)
| Method | Route | Use Case | Success Status | Main Errors |
|:---|:---|:---|:---:|:---|
| `POST` | `/api/deliveries` | `CreateDeliveryUseCase` | `201 Created` | `400 Bad Request`, `409 Conflict` |
| `POST` | `/api/deliveries/{id}/assign` | `AssignDeliveryUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/deliveries/{id}/start` | `StartDeliveryUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/deliveries/{id}/complete` | `CompleteDeliveryUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/deliveries/{id}/cancel` | `CancelDeliveryUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |

### Routes (`/api/routes`)
| Method | Route | Use Case | Success Status | Main Errors |
|:---|:---|:---|:---:|:---|
| `POST` | `/api/routes` | `CreateRouteUseCase` | `201 Created` | `400 Bad Request`, `409 Conflict` |
| `POST` | `/api/routes/{id}/assign` | `AssignRouteUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/routes/{id}/deliveries` | `AddDeliveryToRouteUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `DELETE` | `/api/routes/{id}/deliveries/{deliveryId}` | `RemoveDeliveryFromRouteUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/routes/{id}/start` | `StartRouteUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/routes/{id}/complete` | `CompleteRouteUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/routes/{id}/cancel` | `CancelRouteUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |

### Maintenances (`/api/maintenances`)
| Method | Route | Use Case | Success Status | Main Errors |
|:---|:---|:---|:---:|:---|
| `POST` | `/api/maintenances` | `ScheduleMaintenanceUseCase` | `201 Created` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/maintenances/{id}/start` | `StartMaintenanceUseCase` | `200 OK` | `404 Not Found`, `409 Conflict` |
| `POST` | `/api/maintenances/{id}/complete` | `CompleteMaintenanceUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/maintenances/{id}/cancel` | `CancelMaintenanceUseCase` | `200 OK` | `400 Bad Request`, `404 Not Found`, `409 Conflict` |

### Health Checks & OpenAPI
| Method | Route | Purpose | Response |
|:---|:---|:---|:---|
| `GET` | `/health/live` | Liveness probe (server alive) | `200 OK` ("Healthy") |
| `GET` | `/health/ready` | Readiness probe (database connectivity) | `200 OK` ("Healthy") / `503 Service Unavailable` |
| `GET` | `/openapi/v1.json` | OpenAPI 3.1 specification document | `200 OK` (application/json) |

---

## 4. Centralized Exception Handling & RFC 9457 ProblemDetails

All exceptions thrown during request execution are processed through `GlobalExceptionHandler` implementing `IExceptionHandler`.

| Exception Type | HTTP Status | ProblemDetails Title | Description |
|:---|:---:|:---|:---|
| `NotFoundException` | `404 Not Found` | "Not Found" | Entity was not found for given key |
| `ConflictException` | `409 Conflict` | "Conflict" | Business conflict, duplicate key, concurrency violation |
| `DomainException` | `409 Conflict` | "Conflict" | Aggregate state transition violation |
| `DomainValidationException` | `400 Bad Request` | "Bad Request" | Domain rule invariant validation failure |
| `ValidationException` | `400 Bad Request` | "Bad Request" | Application input validation failure |
| `ArgumentException` | `400 Bad Request` | "Bad Request" | Argument validation failure |
| `BadHttpRequestException` / `JsonException` | `400 Bad Request` | "Bad Request" | Malformed JSON request body |
| Unhandled Exceptions | `500 Internal Server Error` | "Internal Server Error" | Sanitized generic message; no stack traces or database info leaked |

---

## 5. Security & Reliability Baseline

1. **Information Leakage Prevention**:
   - Internal stack traces, SQL queries, Npgsql details, and connection strings are strictly suppressed from all HTTP responses.
   - 500 responses return a generic sanitized detail message while logging the full exception server-side with correlation trace IDs.
2. **Cancellation Propagation**:
   - `CancellationToken` is accepted by all controller actions and propagated to Application, Infrastructure, EF Core, and PostgreSQL.
   - Client disconnects (`OperationCanceledException`) are handled cleanly without triggering 500 errors.
3. **Immutability**:
   - Transport contracts are defined as immutable C# `record` types.
