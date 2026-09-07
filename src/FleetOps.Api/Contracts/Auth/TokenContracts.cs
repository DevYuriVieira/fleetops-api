namespace FleetOps.Api.Contracts.Auth;

public sealed record TokenRequest(
    string Username,
    string Role);

public sealed record TokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn);
