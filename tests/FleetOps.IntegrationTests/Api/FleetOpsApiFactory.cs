namespace FleetOps.IntegrationTests.Api;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using FleetOps.Infrastructure.Persistence;
using FleetOps.IntegrationTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public class FleetOpsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("FLEETOPS_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5433;Database=fleetops_test;Username=postgres;Password=;";

    public TestOidcAuthority Authority { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("POSTGRES_CONNECTION_STRING", ConnectionString);
        builder.UseSetting("Jwt:Authority", Authority.Authority);
        builder.UseSetting("Jwt:Audience", Authority.Audience);
        builder.UseSetting("Jwt:RequireHttpsMetadata", "false");
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:0", "150");
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:1", "300");
        builder.UseSetting("RabbitMQ:RetryDelaysMilliseconds:2", "450");

        builder.ConfigureServices(services =>
        {
            services.Configure<FleetOps.Infrastructure.Configuration.RabbitMqOptions>(options =>
            {
                options.RetryDelaysMilliseconds = [150, 300, 450];
            });

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var httpClient = Authority.CreateHttpClient();
                options.Backchannel = httpClient;
                options.ConfigurationManager = new Microsoft.IdentityModel.Protocols.ConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>(
                    $"{Authority.Authority}/.well-known/openid-configuration",
                    new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfigurationRetriever(),
                    new Microsoft.IdentityModel.Protocols.HttpDocumentRetriever(httpClient) { RequireHttps = false })
                {
                    RefreshInterval = TimeSpan.FromSeconds(1)
                };
            });

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
                        return JwtBearerDefaults.AuthenticationScheme;
                    }

                    return "TestScheme";
                };
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    public string CreateValidOidcToken(
        string username = "admin-user",
        string[]? roles = null,
        string? role = null,
        TimeSpan? lifetime = null,
        RSA? signingRsa = null,
        string? issuer = null,
        string? audience = null,
        string? keyId = null,
        bool tamperSignature = false)
    {
        string[] roleList;
        if (roles != null)
        {
            roleList = roles;
        }
        else
        {
            roleList = [role ?? "Admin"];
        }

        return Authority.CreateToken(
            username: username,
            roles: roleList,
            issuer: issuer,
            audience: audience,
            lifetime: lifetime,
            signingRsa: signingRsa,
            keyId: keyId,
            tamperSignature: tamperSignature);
    }

    public void RequestConfigurationRefresh()
    {
        var options = Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        options.ConfigurationManager?.RequestRefresh();
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
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            await dbContext.Database.MigrateAsync();
            await ResetDatabaseAsync();
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException)
        {
        }
    }

    public async Task ResetDatabaseAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(
                "TRUNCATE TABLE deliveries, maintenances, routes, vehicles, drivers, outbox_messages, processed_messages, maintenance_completion_records CASCADE;");
        }
        catch (Exception ex) when (ex is Npgsql.NpgsqlException or System.Net.Sockets.SocketException)
        {
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Authority.Dispose();
        await base.DisposeAsync();
    }
}
