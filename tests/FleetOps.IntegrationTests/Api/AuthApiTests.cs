namespace FleetOps.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FleetOps.Api.Contracts.Auth;
using FleetOps.Api.Contracts.Vehicles;
using Microsoft.AspNetCore.Hosting;
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

    [Fact]
    public async Task RealJwt_WithValidToken_ReturnsSuccess()
    {
        await _factory.ResetDatabaseAsync();

        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "manager1",
            role: "FleetManager");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT001",
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
    public async Task RealJwt_WithExpiredToken_Returns401Unauthorized()
    {
        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "expired_user",
            role: "Admin",
            lifetime: TimeSpan.FromMinutes(-10)); // Expired in the past (exceeds 1m ClockSkew)

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT002",
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
    public async Task RealJwt_WithWrongSigningKey_Returns401Unauthorized()
    {
        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "hacker",
            role: "Admin",
            secretKey: "Attacker_Crafted_Secret_Key_Minimum_256_Bits_Long!");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT003",
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
    public async Task RealJwt_WithInvalidIssuer_Returns401Unauthorized()
    {
        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "manager2",
            role: "FleetManager",
            issuer: "Untrusted.Foreign.Issuer");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT004",
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
    public async Task RealJwt_WithInvalidAudience_Returns401Unauthorized()
    {
        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "manager3",
            role: "FleetManager",
            audience: "Untrusted.Foreign.Audience");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT005",
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
    public async Task RealJwt_WithTamperedPayload_Returns401Unauthorized()
    {
        var validToken = FleetOpsApiFactory.CreateValidJwtToken(
            username: "driver_user",
            role: "Driver");

        // Split token into header.payload.signature
        var parts = validToken.Split('.');
        Assert.Equal(3, parts.Length);

        // Tamper with payload (replace "Driver" with "Admin")
        var decodedPayload = System.Text.Encoding.UTF8.GetString(Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(parts[1]));
        var tamperedPayload = decodedPayload.Replace("Driver", "Admin");
        var encodedTamperedPayload = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(tamperedPayload));

        var tamperedToken = $"{parts[0]}.{encodedTamperedPayload}.{parts[2]}";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {tamperedToken}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT006",
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
    public async Task RealJwt_WithInsufficientRole_Returns403Forbidden()
    {
        // VehiclesController requires Admin or FleetManager. Valid JWT with role Driver must yield 403 Forbidden.
        var token = FleetOpsApiFactory.CreateValidJwtToken(
            username: "driver_legit",
            role: "Driver");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "JWT007",
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
    public async Task TokenEndpoint_WhenInProductionEnvironment_Returns404NotFound()
    {
        // Create an API factory simulating Production environment
        using var prodFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:DefaultConnection", FleetOpsApiFactory.ConnectionString);
            builder.UseSetting("Jwt:SecretKey", FleetOpsApiFactory.TestJwtSecret);
        });

        using var prodClient = prodFactory.CreateClient();
        var request = new TokenRequest("attacker", "Admin");

        var response = await prodClient.PostAsJsonAsync("/api/auth/token", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
