namespace FleetOps.Api.Contracts.Drivers;

public sealed record RegisterDriverRequest(
    string FullName,
    string LicenseNumber,
    string Email,
    string PhoneNumber);
