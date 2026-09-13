namespace FleetOps.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = "fleetops-api";
    public string? ValidIssuer { get; set; }
    public string? MetadataAddress { get; set; }
    public bool RequireHttpsMetadata { get; set; }
}
