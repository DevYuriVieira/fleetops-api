namespace FleetOps.IntegrationTests.Api;

using System.Security.Claims;
using System.Text.Encodings.Web;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class FleetOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("FLEETOPS_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5433;Database=fleetops_test;Username=postgres;Password=;";

    public const string TestJwtSecret = "FleetOps_Super_Secret_Jwt_Signing_Key_2026_Minimum_256_Bits!";
    public const string TestJwtIssuer = "FleetOps.Api";
    public const string TestJwtAudience = "FleetOps.Clients";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("POSTGRES_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("Jwt:SecretKey", TestJwtSecret);
        builder.UseSetting("Jwt:Issuer", TestJwtIssuer);
        builder.UseSetting("Jwt:Audience", TestJwtAudience);
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:0", "150");
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:1", "300");
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:2", "450");

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
                var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
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

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "DynamicAuthScheme";
                options.DefaultChallengeScheme = "DynamicAuthScheme";
            })
            .AddPolicyScheme("DynamicAuthScheme", "Dynamic Test Scheme", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (context.Request.Headers.ContainsKey("Authorization") ||
                        context.Request.Headers.ContainsKey("X-Real-Jwt"))
                    {
                        return Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                    }

                    return "TestScheme";
                };
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    public static string CreateValidJwtToken(
        string username = "admin-user",
        string role = "Admin",
        TimeSpan? lifetime = null,
        string? secretKey = null,
        string? issuer = null,
        string? audience = null)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(secretKey ?? TestJwtSecret));
        var credentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key,
            Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(1));

        var claims = new[]
        {
            new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, username),
            new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName, username),
            new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        var tokenDescriptor = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer ?? TestJwtIssuer,
            audience: audience ?? TestJwtAudience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
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
