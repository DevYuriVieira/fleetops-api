# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy centralized package management and directory build properties
COPY Directory.Build.props Directory.Packages.props ./

# Copy project files for dependency caching
COPY src/FleetOps.Domain/FleetOps.Domain.csproj src/FleetOps.Domain/
COPY src/FleetOps.Application/FleetOps.Application.csproj src/FleetOps.Application/
COPY src/FleetOps.Infrastructure/FleetOps.Infrastructure.csproj src/FleetOps.Infrastructure/
COPY src/FleetOps.Api/FleetOps.Api.csproj src/FleetOps.Api/

# Restore dependencies
RUN dotnet restore src/FleetOps.Api/FleetOps.Api.csproj

# Copy source trees
COPY src/FleetOps.Domain/ src/FleetOps.Domain/
COPY src/FleetOps.Application/ src/FleetOps.Application/
COPY src/FleetOps.Infrastructure/ src/FleetOps.Infrastructure/
COPY src/FleetOps.Api/ src/FleetOps.Api/

# Build and publish release binaries
RUN dotnet publish src/FleetOps.Api/FleetOps.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for container healthchecks
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root user
USER $APP_UID

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetOps.Api.dll"]
