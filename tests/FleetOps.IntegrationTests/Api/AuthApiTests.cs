namespace FleetOps.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FleetOps.Api.Contracts.Auth;
using FleetOps.Api.Contracts.Vehicles;
using Xunit;

public class AuthApiTests : IClassFixture<FleetOpsApiFactory>
{
    private readonly HttpClient _client;
    private readonly FleetOpsApiFactory _factory;

    public AuthApiTests(FleetOpsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostToken_WithValidCredentials_ReturnsJwtAccessToken()
    {
        var request = new TokenRequest("dispatcher1", "Dispatcher");

        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(tokenResponse);
        Assert.Equal("Bearer", tokenResponse.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse.AccessToken));
        Assert.True(tokenResponse.ExpiresIn > 0);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenResponse.AccessToken);
        Assert.Equal("FleetOps.Api", jwt.Issuer);
        Assert.Contains("FleetOps.Clients", jwt.Audiences);

        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role");
        Assert.NotNull(roleClaim);
        Assert.Equal("Dispatcher", roleClaim.Value);
    }

    [Fact]
    public async Task PostToken_WithInvalidRole_ReturnsBadRequest()
    {
        var request = new TokenRequest("hacker", "SuperAdminInvalid");

        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostToken_WithEmptyUsername_ReturnsBadRequest()
    {
        var request = new TokenRequest("", "Admin");

        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenUnauthenticated_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("X-Anonymous", "true");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "AUTH001",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenRoleInsufficient_ReturnsForbidden()
    {
        // VehiclesController requires Admin or FleetManager. Role Driver should be Forbidden (403).
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("X-Test-Role", "Driver");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "AUTH002",
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
    public async Task ProtectedEndpoint_WhenAuthorizedRole_Succeeds()
    {
        await _factory.ResetDatabaseAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("X-Test-Role", "FleetManager");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "AUTH003",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsPubliclyAccessibleWithoutAuthentication()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add("X-Anonymous", "true");

        var response = await _client.SendAsync(request);

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }
}
