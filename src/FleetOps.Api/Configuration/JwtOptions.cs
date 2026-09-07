namespace FleetOps.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = "FleetOps_Super_Secret_Jwt_Signing_Key_2026_Minimum_256_Bits!";
    public string Issuer { get; set; } = "FleetOps.Api";
    public string Audience { get; set; } = "FleetOps.Clients";
    public int ExpirationHours { get; set; } = 8;
}
