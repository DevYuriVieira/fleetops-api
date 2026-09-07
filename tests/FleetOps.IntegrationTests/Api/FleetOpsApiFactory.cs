namespace FleetOps.IntegrationTests.Api;

using System.Security.Claims;
using System.Text.Encodings.Web;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class FleetOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("FLEETOPS_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5433;Database=fleetops_test;Username=postgres;Password=;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FleetOpsDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<FleetOpsDbContext>((_, options) =>
            {
                options.UseNpgsql(ConnectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(FleetOpsDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers.ContainsKey("X-Anonymous"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var role = Request.Headers.TryGetValue("X-Test-Role", out var roleHeader) && !string.IsNullOrWhiteSpace(roleHeader)
                ? roleHeader.ToString()
                : "Admin";

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "test-user"),
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        await dbContext.Database.MigrateAsync();
        await ResetDatabaseAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE deliveries, maintenances, routes, vehicles, drivers, outbox_messages, processed_messages, maintenance_completion_records CASCADE;");
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
