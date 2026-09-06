using System.Reflection;
using FleetOps.Infrastructure.Persistence;
using Xunit;

namespace FleetOps.IntegrationTests.Architecture;

public class InfrastructureArchitectureTests
{
    [Fact]
    public void InfrastructureAssembly_ShouldNotReferenceApiOrPresentationLayer()
    {
        var infraAssembly = typeof(FleetOpsDbContext).Assembly;
        var referencedAssemblies = infraAssembly.GetReferencedAssemblies();

        var forbiddenPrefixes = new[]
        {
            "FleetOps.Api",
            "Microsoft.AspNetCore.Mvc",
            "Microsoft.AspNetCore.Components"
        };

        foreach (var referenced in referencedAssemblies)
        {
            var isForbidden = forbiddenPrefixes.Any(prefix =>
                referenced.Name != null && referenced.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            Assert.False(isForbidden, $"Infrastructure assembly references forbidden dependency: {referenced.Name}");
        }
    }

    [Fact]
    public void InfrastructureAssembly_ShouldReferenceApplicationAndDomain()
    {
        var infraAssembly = typeof(FleetOpsDbContext).Assembly;
        var referencedAssemblies = infraAssembly.GetReferencedAssemblies();

        var references = referencedAssemblies.Select(a => a.Name).ToList();

        Assert.Contains("FleetOps.Domain", references);
        Assert.Contains("FleetOps.Application", references);
    }
}
