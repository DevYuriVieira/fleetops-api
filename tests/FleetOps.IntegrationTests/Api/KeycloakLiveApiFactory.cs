namespace FleetOps.IntegrationTests.Api;

using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class KeycloakLiveApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string KeycloakBaseUrl =
        Environment.GetEnvironmentVariable("KEYCLOAK_URL")
        ?? Environment.GetEnvironmentVariable("KEYCLOAK_BASE_URL")
        ?? "http://127.0.0.1:8080";

    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("FLEETOPS_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5432;Database=fleetops_test;Username=postgres;Password=postgres;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("POSTGRES_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("Jwt:Authority", $"{KeycloakBaseUrl}/realms/fleetops");
        builder.UseSetting("Jwt:Audience", "fleetops-api");
        builder.UseSetting("Jwt:RequireHttpsMetadata", "false");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FleetOpsDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<FleetOpsDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connString = config.GetConnectionString("DefaultConnection")
                    ?? config["POSTGRES_CONNECTION_STRING"]
                    ?? ConnectionString;

                options.UseNpgsql(connString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(FleetOpsDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            });
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            await db.Database.MigrateAsync();
        }
        catch
        {
        }
    }

    public async Task ResetDatabaseAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE vehicles, outbox_messages, idempotency_records RESTART IDENTITY CASCADE;");
        }
        catch
        {
        }
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}
