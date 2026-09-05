namespace FleetOps.UnitTests;

using FleetOps.Domain.Entities;

public class ArchitectureTests
{
    [Fact]
    public void DomainAssembly_ShouldNotReferenceOtherFleetOpsProjects()
    {
        var domainAssembly = typeof(Vehicle).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        var forbiddenPrefixes = new[]
        {
            "FleetOps.Application",
            "FleetOps.Infrastructure",
            "FleetOps.Api",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "StackExchange.Redis",
            "RabbitMQ"
        };

        foreach (var referenced in referencedAssemblies)
        {
            var isForbidden = forbiddenPrefixes.Any(prefix =>
                referenced.Name != null && referenced.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            Assert.False(isForbidden, $"Domain assembly references forbidden dependency: {referenced.Name}");
        }
    }

    [Fact]
    public void DomainAssembly_ShouldOnlyReferenceRuntimeSystemAssemblies()
    {
        var domainAssembly = typeof(Vehicle).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        foreach (var referenced in referencedAssemblies)
        {
            Assert.True(
                referenced.Name?.StartsWith("System", StringComparison.OrdinalIgnoreCase) == true ||
                referenced.Name?.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) == true ||
                referenced.Name?.StartsWith("Microsoft.CSharp", StringComparison.OrdinalIgnoreCase) == true,
                $"Unexpected non-runtime reference in Domain: {referenced.Name}");
        }
    }
}
