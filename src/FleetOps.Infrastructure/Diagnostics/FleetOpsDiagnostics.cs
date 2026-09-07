namespace FleetOps.Infrastructure.Diagnostics;

using System.Diagnostics;

public static class FleetOpsDiagnostics
{
    public const string ServiceName = "FleetOps.Api";
    public const string ActivitySourceName = "FleetOps";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");
}
