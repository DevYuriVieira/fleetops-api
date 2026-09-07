using System.Text.Json.Serialization;
using FleetOps.Api.Extensions;
using FleetOps.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationUseCases();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddFleetOpsTelemetry(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddExceptionHandler<FleetOps.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddApiHealthChecks();
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapApiHealthChecks();
app.MapControllers().RequireRateLimiting(RateLimitingExtensions.DefaultPolicyName);

app.Run();

public partial class Program;
