namespace FleetOps.IntegrationTests.Common;

using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public sealed class TestOidcAuthority : IDisposable
{
    public const string DefaultAuthority = "http://localhost:8080/realms/fleetops";
    public const string DefaultAudience = "fleetops-api";

    private readonly List<(string KeyId, RSA RsaKey)> _publishedKeys = new();
    private bool _isAvailable = true;

    public string Authority { get; }
    public string Audience { get; }
    public RSA PrimaryKey { get; private set; }
    public string PrimaryKeyId { get; private set; }

    public TestOidcAuthority(
        string authority = DefaultAuthority,
        string audience = DefaultAudience)
    {
        Authority = authority;
        Audience = audience;
        PrimaryKey = RSA.Create(2048);
        PrimaryKeyId = "primary-test-key-1";
        _publishedKeys.Add((PrimaryKeyId, PrimaryKey));
    }

    public void SetAvailable(bool isAvailable)
    {
        _isAvailable = isAvailable;
    }

    public RSA RotateKey(string newKeyId)
    {
        var newKey = RSA.Create(2048);
        _publishedKeys.Add((newKeyId, newKey));
        return newKey;
    }

    public string CreateToken(
        string username = "test-user",
        string[]? roles = null,
        string? issuer = null,
        string? audience = null,
        TimeSpan? lifetime = null,
        RSA? signingRsa = null,
        string? keyId = null,
        bool tamperSignature = false)
    {
        roles ??= ["Admin"];
        var rsa = signingRsa ?? PrimaryKey;
        var kid = keyId ?? PrimaryKeyId;
        var iss = issuer ?? Authority;
        var aud = audience ?? Audience;

        var signingKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var tokenLifetime = lifetime ?? TimeSpan.FromHours(1);
        var expires = now.Add(tokenLifetime);

        var resourceAccessDict = new Dictionary<string, object>
        {
            [aud] = new Dictionary<string, object>
            {
                ["roles"] = roles
            }
        };

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = username,
            [JwtRegisteredClaimNames.PreferredUsername] = username,
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            ["resource_access"] = resourceAccessDict
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = iss,
            Audience = aud,
            Claims = claims,
            NotBefore = now.AddMinutes(-1),
            Expires = expires,
            SigningCredentials = credentials
        };

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(descriptor);

        if (tamperSignature)
        {
            var parts = token.Split('.');
            if (parts.Length == 3)
            {
                var corruptedSig = parts[2].Length > 4
                    ? parts[2][..^4] + "XXXX"
                    : "invalid_sig";
                token = $"{parts[0]}.{parts[1]}.{corruptedSig}";
            }
        }

        return token;
    }

    public HttpClient CreateHttpClient()
    {
        return new HttpClient(new TestAuthorityHandler(this));
    }

    public void Dispose()
    {
        foreach (var (_, rsaKey) in _publishedKeys)
        {
            rsaKey.Dispose();
        }
    }

    private sealed class TestAuthorityHandler : HttpMessageHandler
    {
        private readonly TestOidcAuthority _authority;

        public TestAuthorityHandler(TestOidcAuthority authority)
        {
            _authority = authority;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!_authority._isAvailable)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith("/.well-known/openid-configuration", StringComparison.OrdinalIgnoreCase))
            {
                var config = new
                {
                    issuer = _authority.Authority,
                    jwks_uri = $"{_authority.Authority}/protocol/openid-connect/certs",
                    id_token_signing_alg_values_supported = new[] { "RS256" },
                    response_types_supported = new[] { "code" }
                };

                var json = JsonSerializer.Serialize(config);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            if (path.EndsWith("/protocol/openid-connect/certs", StringComparison.OrdinalIgnoreCase))
            {
                var keysList = new List<object>();

                foreach (var (kid, rsaKey) in _authority._publishedKeys)
                {
                    var parameters = rsaKey.ExportParameters(false);
                    keysList.Add(new
                    {
                        kty = "RSA",
                        use = "sig",
                        alg = "RS256",
                        kid = kid,
                        n = WebEncoders.Base64UrlEncode(parameters.Modulus!),
                        e = WebEncoders.Base64UrlEncode(parameters.Exponent!)
                    });
                }

                var jwks = new { keys = keysList };
                var json = JsonSerializer.Serialize(jwks);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
