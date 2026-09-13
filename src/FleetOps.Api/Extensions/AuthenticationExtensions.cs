namespace FleetOps.Api.Extensions;

using System.Security.Claims;
using System.Text.Json;
using FleetOps.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);

        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Authority))
        {
            throw new InvalidOperationException(
                "Fatal: 'Jwt:Authority' must be explicitly configured with the Identity Provider realm URL.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
        {
            throw new InvalidOperationException(
                "Fatal: 'Jwt:Audience' must be explicitly configured with the target resource audience.");
        }

        var canonicalIssuer = !string.IsNullOrWhiteSpace(jwtOptions.ValidIssuer)
            ? jwtOptions.ValidIssuer
            : jwtOptions.Authority;

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.Authority = jwtOptions.Authority;
            options.Audience = jwtOptions.Audience;
            if (!string.IsNullOrWhiteSpace(jwtOptions.MetadataAddress))
            {
                options.MetadataAddress = jwtOptions.MetadataAddress;
            }

            options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
            options.SaveToken = true;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = canonicalIssuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    MapResourceAccessRolesToClaimsPrincipal(context, jwtOptions.Audience);
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetService<ILoggerFactory>()?
                        .CreateLogger("FleetOps.Api.Authentication");

                    logger?.LogWarning(
                        "Authentication failed for request '{Path}': {Reason}",
                        context.Request.Path,
                        context.Exception.Message);

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    private static void MapResourceAccessRolesToClaimsPrincipal(TokenValidatedContext context, string clientId)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var resourceAccessClaim = identity.FindFirst("resource_access");
        if (resourceAccessClaim is null || string.IsNullOrWhiteSpace(resourceAccessClaim.Value))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(resourceAccessClaim.Value);
            if (!document.RootElement.TryGetProperty(clientId, out var clientElement))
            {
                return;
            }

            if (!clientElement.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var roleItem in rolesElement.EnumerateArray())
            {
                var roleName = roleItem.GetString();
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }
        catch (JsonException)
        {
        }
    }
}
