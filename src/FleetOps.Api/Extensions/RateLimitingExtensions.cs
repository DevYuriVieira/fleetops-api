namespace FleetOps.Api.Extensions;

using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class RateLimitingExtensions
{
    public const string DefaultPolicyName = "FleetOpsRateLimit";
    public const string TestStrictPolicyName = "StrictTestLimit";

    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";
                context.HttpContext.Response.Headers.RetryAfter = "1";

                var problemDetails = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Rate limit exceeded. Local per-process capacity reached. Please retry after the specified backoff period.",
                    Instance = context.HttpContext.Request.Path
                };

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problemDetails,
                    type: typeof(ProblemDetails),
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };

            // Standard production-ready fixed window policy
            options.AddFixedWindowLimiter(DefaultPolicyName, limiterOptions =>
            {
                limiterOptions.PermitLimit = permitLimit;
                limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });

            // Strict policy for deterministic adversarial testing (3 requests per 2 seconds)
            options.AddFixedWindowLimiter(TestStrictPolicyName, limiterOptions =>
            {
                limiterOptions.PermitLimit = 3;
                limiterOptions.Window = TimeSpan.FromSeconds(2);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 0;
            });
        });

        return services;
    }
}
