namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FleetOps.Api.Contracts.Vehicles;
using Xunit;

public class KeycloakLiveIntegrationTests : IClassFixture<KeycloakLiveApiFactory>
{
    private static readonly string KeycloakBaseUrl = KeycloakLiveApiFactory.KeycloakBaseUrl;

    private const string Realm = "fleetops";
    private const string ClientId = "fleetops-api";
    private const string ClientSecret = "fleetops-api-dev-secret";

    private readonly HttpClient _client;
    private readonly KeycloakLiveApiFactory _factory;

    public KeycloakLiveIntegrationTests(KeycloakLiveApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<bool> IsKeycloakAvailableAsync()
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            var response = await httpClient.GetAsync($"{KeycloakBaseUrl}/realms/{Realm}/.well-known/openid-configuration");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RequestKeycloakAccessTokenAsync(string username, string password = "test1234")
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var tokenEndpoint = $"{KeycloakBaseUrl}/realms/{Realm}/protocol/openid-connect/token";

        var payload = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password
        };

        var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(payload));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token not found in Keycloak response.");
    }

    private static void AssertAuthorized(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LiveKeycloak_AdminUser_IsAuthorizedOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        await _factory.ResetDatabaseAsync();

        var token = await RequestKeycloakAccessTokenAsync("admin@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE001",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task LiveKeycloak_FleetManagerUser_IsAuthorizedOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        await _factory.ResetDatabaseAsync();

        var token = await RequestKeycloakAccessTokenAsync("manager@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE002",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task LiveKeycloak_MultiRoleUser_IsAuthorizedOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        await _factory.ResetDatabaseAsync();

        var token = await RequestKeycloakAccessTokenAsync("multirole@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE003",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        AssertAuthorized(response);
    }

    [Fact]
    public async Task LiveKeycloak_DispatcherUser_Receives403ForbiddenOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        var token = await RequestKeycloakAccessTokenAsync("dispatcher@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE004",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LiveKeycloak_DriverUser_Receives403ForbiddenOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        var token = await RequestKeycloakAccessTokenAsync("driver@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE005",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LiveKeycloak_NoRoleUser_Receives403ForbiddenOnVehicles()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        var token = await RequestKeycloakAccessTokenAsync("norole@fleetops.local");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "LIVE006",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LiveKeycloak_InvalidPassword_FailsAtTokenEndpoint()
    {
        if (!await IsKeycloakAvailableAsync())
        {
            return;
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var tokenEndpoint = $"{KeycloakBaseUrl}/realms/{Realm}/protocol/openid-connect/token";

        var payload = new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["grant_type"] = "password",
            ["username"] = "admin@fleetops.local",
            ["password"] = "wrong_password"
        };

        var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(payload));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
