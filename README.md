# FleetOps API

A production-grade fleet and logistics management REST API built with ASP.NET Core, .NET 10, PostgreSQL, Redis, and Clean Architecture.

## Current Status

**Foundational Setup Completed**:
- Clean Architecture solution structure configured.
- Central Package Management (CPM) enabled via `Directory.Packages.props`.
- Enterprise build properties and code style analyzers enabled via `Directory.Build.props` and `.editorconfig`.
- Strict project dependency boundaries established.
- Business features, entities, data access, and API endpoints are slated for subsequent implementation phases.

## Architecture

The project adheres to Clean Architecture principles, ensuring domain independence and strict inwards-pointing dependency flow.

```text
FleetOps.Api
    ↓
FleetOps.Application
    ↓
FleetOps.Domain

FleetOps.Infrastructure
    ↓
FleetOps.Application
    ↓
FleetOps.Domain
```

### Layer Responsibilities

* **`FleetOps.Domain`**: Core business domain logic, entities, value objects, domain events, domain exceptions, and domain service abstractions. Has zero dependencies on other projects or external frameworks.
* **`FleetOps.Application`**: Application orchestration, use cases, commands, queries, DTOs, behaviors, and application interfaces. References `FleetOps.Domain`.
* **`FleetOps.Infrastructure`**: Implementation of persistence, external services, messaging, caching, and infrastructure concerns. References `FleetOps.Application` and `FleetOps.Domain`.
* **`FleetOps.Api`**: HTTP entrypoint, dependency injection composition root, middleware, configuration, and API routing. References `FleetOps.Application` and `FleetOps.Infrastructure`.
* **`FleetOps.UnitTests`**: Isolated unit tests for domain behaviors and application logic. References `FleetOps.Application` and `FleetOps.Domain`.
* **`FleetOps.IntegrationTests`**: Integration tests verifying API behavior and infrastructure integration. References `FleetOps.Api` and `FleetOps.Infrastructure`.

## Solution Structure

```text
FleetOps.sln
├── src/
│   ├── FleetOps.Api/
│   │   ├── Configuration/
│   │   ├── Controllers/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── FleetOps.Api.csproj
│   │   └── Program.cs
│   ├── FleetOps.Application/
│   │   ├── Abstractions/
│   │   ├── Behaviors/
│   │   ├── DTOs/
│   │   ├── Exceptions/
│   │   └── FleetOps.Application.csproj
│   ├── FleetOps.Domain/
│   │   ├── Entities/
│   │   ├── Events/
│   │   ├── Exceptions/
│   │   ├── Services/
│   │   ├── ValueObjects/
│   │   └── FleetOps.Domain.csproj
│   └── FleetOps.Infrastructure/
│       ├── Configuration/
│       ├── Persistence/
│       ├── Services/
│       └── FleetOps.Infrastructure.csproj
└── tests/
    ├── FleetOps.UnitTests/
    │   ├── ArchitectureTests.cs
    │   └── FleetOps.UnitTests.csproj
    └── FleetOps.IntegrationTests/
        ├── IntegrationSmokeTests.cs
        └── FleetOps.IntegrationTests.csproj
```

## Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting Started

### Restore Dependencies
```bash
dotnet restore
```

### Build Solution
```bash
dotnet build --no-restore
```

### Run Tests
```bash
dotnet test --no-build
```

### Run API
```bash
dotnet run --project src/FleetOps.Api
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
