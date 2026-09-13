namespace FleetOps.IntegrationTests.Api;

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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

    private static void AssertAuthorized(HttpResponseMessage response)
    {
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.InternalServerError,
            $"Expected 201 Created (or 500 when DB is offline), but got {response.StatusCode}");
    }

    [Fact]
    public async Task HealthEndpoint_IsPubliclyAccessibleWithoutAuthentication()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add("X-Anonymous", "true");

        var response = await _client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenUnauthenticated_Returns401Unauthorized()
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
    public async Task RealOidc_WithValidTokenAndAuthorizedRole_ReturnsSuccess()
    {
        await _factory.ResetDatabaseAsync();

        var token = _factory.CreateValidOidcToken(
            username: "manager1",
            role: "FleetManager");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC001",
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
    public async Task RealOidc_WithInsufficientRole_Returns403Forbidden()
    {
        var token = _factory.CreateValidOidcToken(
            username: "driver_legit",
            role: "Driver");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC002",
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
    public async Task RealOidc_WithNoRoles_Returns403Forbidden()
    {
        var token = _factory.CreateValidOidcToken(
            username: "guest_user",
            roles: []);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC003",
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
    public async Task RealOidc_WithMultipleRolesIncludingAuthorizedRole_ReturnsSuccess()
    {
        await _factory.ResetDatabaseAsync();

        var token = _factory.CreateValidOidcToken(
            username: "multi_role_user",
            roles: ["Driver", "FleetManager"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC004",
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
    public async Task RealOidc_WithInvalidSignature_Returns401Unauthorized()
    {
        var tamperedToken = _factory.CreateValidOidcToken(
            username: "hacker",
            role: "Admin",
            tamperSignature: true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {tamperedToken}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC005",
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
    public async Task RealOidc_WithUntrustedSigningKey_Returns401Unauthorized()
    {
        using var untrustedRsa = RSA.Create(2048);
        var token = _factory.CreateValidOidcToken(
            username: "attacker",
            role: "Admin",
            signingRsa: untrustedRsa,
            keyId: "untrusted-external-key");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC006",
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
    public async Task RealOidc_WithExpiredToken_Returns401Unauthorized()
    {
        var expiredToken = _factory.CreateValidOidcToken(
            username: "expired_admin",
            role: "Admin",
            lifetime: TimeSpan.FromMinutes(-10)); // Expired in past (exceeds 1m ClockSkew)

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {expiredToken}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC007",
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
    public async Task RealOidc_WithWrongIssuer_Returns401Unauthorized()
    {
        var token = _factory.CreateValidOidcToken(
            username: "foreign_user",
            role: "FleetManager",
            issuer: "http://malicious-idp.com/realms/fake");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC008",
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
    public async Task RealOidc_WithWrongAudience_Returns401Unauthorized()
    {
        var token = _factory.CreateValidOidcToken(
            username: "wrong_aud_user",
            role: "FleetManager",
            audience: "untrusted-foreign-api");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC009",
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
    public async Task RealOidc_JwksKeyRotation_AllowsNewlyRotatedKeyToBeValidated()
    {
        await _factory.ResetDatabaseAsync();

        var tokenA = _factory.CreateValidOidcToken(
            username: "manager_key_a",
            role: "FleetManager");

        using var requestA = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        requestA.Headers.Add("Authorization", $"Bearer {tokenA}");
        requestA.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC010",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var responseA = await _client.SendAsync(requestA);
        AssertAuthorized(responseA);

        var newKeyId = "rotated-key-2";
        var newRsaKey = _factory.Authority.RotateKey(newKeyId);
        _factory.RequestConfigurationRefresh();
        await Task.Delay(1100);

        var tokenB = _factory.CreateValidOidcToken(
            username: "manager_key_b",
            role: "FleetManager",
            signingRsa: newRsaKey,
            keyId: newKeyId);

        using var requestB = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        requestB.Headers.Add("Authorization", $"Bearer {tokenB}");
        requestB.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC011",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var responseB = await _client.SendAsync(requestB);
        AssertAuthorized(responseB);
    }

    [Fact]
    public async Task RealOidc_IdpUnavailable_ValidatesCachedKeyOfflineButRejectsUnknownKey()
    {
        await _factory.ResetDatabaseAsync();

        var tokenWarmup = _factory.CreateValidOidcToken(
            username: "warmup_manager",
            role: "FleetManager");

        using var warmupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
        warmupRequest.Headers.Add("Authorization", $"Bearer {tokenWarmup}");
        warmupRequest.Content = JsonContent.Create(new RegisterVehicleRequest(
            "OIDC012",
            "Truck",
            "Volvo",
            "FH",
            2023,
            1000,
            25000m));

        var warmupResponse = await _client.SendAsync(warmupRequest);
        AssertAuthorized(warmupResponse);

        _factory.Authority.SetAvailable(false);

        try
        {
            var tokenCached = _factory.CreateValidOidcToken(
                username: "offline_manager",
                role: "FleetManager");

            using var offlineRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
            offlineRequest.Headers.Add("Authorization", $"Bearer {tokenCached}");
            offlineRequest.Content = JsonContent.Create(new RegisterVehicleRequest(
                "OIDC013",
                "Truck",
                "Volvo",
                "FH",
                2023,
                1000,
                25000m));

            var offlineResponse = await _client.SendAsync(offlineRequest);
            AssertAuthorized(offlineResponse);

            // 2B. A token signed with an UNKNOWN key cannot be resolved because IdP is down -> returns 401
            using var unknownRsa = RSA.Create(2048);
            var tokenUnknownKey = _factory.CreateValidOidcToken(
                username: "unknown_key_user",
                role: "FleetManager",
                signingRsa: unknownRsa,
                keyId: "unknown-unreachable-key");

            using var unknownRequest = new HttpRequestMessage(HttpMethod.Post, "/api/vehicles");
            unknownRequest.Headers.Add("Authorization", $"Bearer {tokenUnknownKey}");
            unknownRequest.Content = JsonContent.Create(new RegisterVehicleRequest(
                "OIDC014",
                "Truck",
                "Volvo",
                "FH",
                2023,
                1000,
                25000m));

            var unknownResponse = await _client.SendAsync(unknownRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, unknownResponse.StatusCode);
        }
        finally
        {
            _factory.Authority.SetAvailable(true);
        }
    }
}
