namespace FleetOps.Api.Extensions;

using FleetOps.Infrastructure.Messaging;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly FleetOpsDbContext _dbContext;

    public DatabaseHealthCheck(FleetOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy.")
                : HealthCheckResult.Unhealthy("Unable to connect to database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Database connection check failed: {ex.Message}");
        }
    }
}

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IRabbitMqConnection? _connection;

    public RabbitMqHealthCheck(IRabbitMqConnection? connection = null)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            return HealthCheckResult.Degraded("RabbitMQ connection service is not registered.");
        }

        try
        {
            await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ connection is healthy.");
        }
        catch (Exception ex)
        {
            // Under the Transactional Outbox pattern, RabbitMQ unavailability does NOT prevent
            // command ingestion, as business writes commit atomically to PostgreSQL outbox.
            // Therefore, the dependency status is reported as Degraded rather than Unhealthy.
            return HealthCheckResult.Degraded(
                $"RabbitMQ is temporarily unavailable. Transactional Outbox will accumulate events: {ex.Message}");
        }
    }
}

public static class HealthCheckExtensions
{
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "dependency"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["dependency"]);

        return services;
    }

    public static IEndpointRouteBuilder MapApiHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        // Liveness probe: Returns 200 if the process/Kestrel is alive
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        // Readiness probe: Returns 200 only if PostgreSQL is available to accept write commands
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        // Detailed dependency health: Inspects all infrastructure dependencies (PG + RabbitMQ)
        endpoints.MapHealthChecks("/health/dependencies", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("dependency"),
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    totalDurationMs = report.TotalDuration.TotalMilliseconds,
                    dependencies = report.Entries.ToDictionary(
                        e => e.Key,
                        e => new
                        {
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description,
                            durationMs = e.Value.Duration.TotalMilliseconds
                        })
                };
                await context.Response.WriteAsJsonAsync(response);
            }
        });

        return endpoints;
    }
}
