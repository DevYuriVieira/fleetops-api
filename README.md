# FleetOps API

A production-grade fleet and logistics management REST API built with ASP.NET Core, .NET 10, PostgreSQL, and Clean Architecture.

## Architecture

The project adheres to Clean Architecture principles, ensuring domain independence and strict inwards-pointing dependency flow:

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

* **`FleetOps.Domain`**: Core business domain logic, entities (`Vehicle`, `Driver`, `Delivery`, `Route`, `Maintenance`), value objects, domain events, domain exceptions, and domain service abstractions. Zero dependencies.
* **`FleetOps.Application`**: Application orchestration, 28 use cases, commands, DTOs, interfaces, and transaction boundaries. References `FleetOps.Domain`.
* **`FleetOps.Infrastructure`**: Persistence via EF Core 10, PostgreSQL 18, Transactional Outbox processor, repositories, and unit of work. References `FleetOps.Application` and `FleetOps.Domain`.
* **`FleetOps.Api`**: HTTP entrypoint, dependency injection composition root, thin controllers, RFC 9457 ProblemDetails global exception handler, OpenAPI 3.1, and health checks. References `FleetOps.Application` and `FleetOps.Infrastructure`.
* **`FleetOps.UnitTests`**: Isolated unit tests for domain behaviors, value objects, and application use cases.
* **`FleetOps.IntegrationTests`**: End-to-end API tests, PostgreSQL persistence tests, concurrency tests, and architecture tests.

## Solution Structure

```text
FleetOps.sln
├── compose.yaml
├── Dockerfile
├── .dockerignore
├── .env.example
├── src/
│   ├── FleetOps.Api/
│   │   ├── Contracts/
│   │   ├── Controllers/
│   │   ├── Extensions/
│   │   ├── Middleware/
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Dockerfile
│   │   ├── FleetOps.Api.csproj
│   │   └── Program.cs
│   ├── FleetOps.Application/
│   ├── FleetOps.Domain/
│   └── FleetOps.Infrastructure/
└── tests/
    ├── FleetOps.UnitTests/
    └── FleetOps.IntegrationTests/
```

## Local Development with Docker

A local containerized environment is provided via Docker Compose (`compose.yaml`).

### Topology

```text
                 Docker Host
                      │
     ┌────────────────┴────────────────┐
     │ :5000                     :5432 │
     ▼                                 ▼
┌──────────────┐                 ┌────────────┐
│ fleetops-api │ ──[network]───> │  postgres  │
│  ASP.NET 10  │                 │ PostgreSQL │
└──────────────┘                 └────────────┘
                                       │
                                   [volume]
                                       ▼
                             fleetops-postgres-data
```

### Services & Port Mappings

| Service | Container Name | Image / Build | Container Port | Host Port | Purpose |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `fleetops-api` | `fleetops-api` | `./Dockerfile` (ASP.NET 10) | `8080` | `5000` | FleetOps REST API |
| `postgres` | `fleetops-postgres` | `postgres:18-alpine` | `5432` | `5432` | PostgreSQL Development Database |

### Infrastructure Status

```text
Current infrastructure:
PostgreSQL

Planned distributed infrastructure:
RabbitMQ

Redis:
Not currently required; will only be introduced when a concrete caching use case exists.
```

### Prerequisites

* [Docker](https://docs.docker.com/get-docker/) & Docker Compose v2+
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for local CLI migration execution and host testing)

### Quick Start Workflow

1. **Clone the repository**:
   ```bash
   git clone https://github.com/DevYuriVieira/fleetops-api.git
   cd fleetops-api
   ```

2. **Configure optional environment overrides**:
   Copy `.env.example` to `.env` if custom ports or database credentials are desired:
   ```bash
   cp .env.example .env
   ```

3. **Build the container images**:
   ```bash
   docker compose build
   ```

4. **Start the infrastructure**:
   ```bash
   docker compose up -d
   ```

5. **Apply Database Migrations**:
   Run the EF Core migration update against the local database:
   ```bash
   dotnet ef database update --project src/FleetOps.Infrastructure --startup-project src/FleetOps.Api
   ```

6. **Access the API**:
   * API root: `http://localhost:5000`
   * OpenAPI Specification: `http://localhost:5000/openapi/v1.json`
   * Liveness Probe: `http://localhost:5000/health/live`
   * Readiness Probe: `http://localhost:5000/health/ready`

### Health Endpoints & Inter-service Dependencies

* **PostgreSQL Healthcheck**: Evaluates `pg_isready -U fleetops_dev -d fleetops`.
* **API Startup Ordering**: `fleetops-api` declares `depends_on: postgres: condition: service_healthy`, ensuring the database accepts connections before the API starts.
* **API Healthcheck**: Evaluates `curl -f http://localhost:8080/health/ready`. The readiness check verifies database connectivity via EF Core's `CanConnectAsync()`.

### Useful Docker Compose Commands

```bash
# Start all services with live logs
docker compose up

# Start all services in the background
docker compose up -d

# View container status and health
docker compose ps

# View aggregate logs
docker compose logs

# Follow API service logs
docker compose logs -f fleetops-api

# Stop containers without losing data
docker compose down

# Stop containers and destroy the persistent database volume (DATA RESET)
docker compose down -v

# Rebuild containers
docker compose build
```

> [!WARNING]
> Running `docker compose down -v` permanently removes the `fleetops-postgres-data` volume, deleting all local database records.

---

## Getting Started (Local Host Development)

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
