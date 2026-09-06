namespace FleetOps.IntegrationTests.Api;

using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public abstract class BaseApiTest : IClassFixture<FleetOpsApiFactory>, IAsyncLifetime
{
    protected readonly FleetOpsApiFactory Factory;
    protected readonly HttpClient Client;

    protected BaseApiTest(FleetOpsApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    protected static async Task<ProblemDetails?> ReadProblemDetailsAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
    }
}
