using System.Text.Json.Serialization;
using FleetOps.Api.Extensions;
using FleetOps.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationUseCases();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseStatusCodePages();

app.MapControllers();

app.Run();

public partial class Program;
