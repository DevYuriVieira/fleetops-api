namespace FleetOps.Api.Controllers;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FleetOps.Api.Configuration;
using FleetOps.Api.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "FleetManager",
        "Dispatcher",
        "Driver"
    };

    private readonly JwtOptions _jwtOptions;

    public AuthController(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GenerateToken(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return Task.FromResult<IActionResult>(BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "Username is required.",
                Status = StatusCodes.Status400BadRequest
            }));
        }

        if (string.IsNullOrWhiteSpace(request.Role) || !ValidRoles.Contains(request.Role))
        {
            return Task.FromResult<IActionResult>(BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = $"Invalid role '{request.Role}'. Supported roles: {string.Join(", ", ValidRoles)}.",
                Status = StatusCodes.Status400BadRequest
            }));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddHours(_jwtOptions.ExpirationHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, request.Username),
            new Claim(JwtRegisteredClaimNames.UniqueName, request.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, request.Username),
            new Claim(ClaimTypes.Role, request.Role)
        };

        var tokenDescriptor = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.WriteToken(tokenDescriptor);

        return Task.FromResult<IActionResult>(Ok(new TokenResponse(
            AccessToken: jwt,
            TokenType: "Bearer",
            ExpiresIn: (int)TimeSpan.FromHours(_jwtOptions.ExpirationHours).TotalSeconds)));
    }
}
