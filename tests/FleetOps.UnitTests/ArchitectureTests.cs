namespace FleetOps.UnitTests;

using System.Reflection;
using FleetOps.Application.UseCases.Vehicles;
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

    [Fact]
    public void ApplicationAssembly_ShouldOnlyReferenceDomainAndRuntimeAssemblies()
    {
        var applicationAssembly = typeof(RegisterVehicleUseCase).Assembly;
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

        var allowedPrefixes = new[]
        {
            "FleetOps.Domain",
            "System",
            "mscorlib",
            "Microsoft.CSharp"
        };

        foreach (var referenced in referencedAssemblies)
        {
            var isAllowed = allowedPrefixes.Any(prefix =>
                referenced.Name != null && referenced.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            Assert.True(isAllowed, $"Application assembly references unapproved dependency: {referenced.Name}");
        }
    }

    [Fact]
    public void ApplicationAssembly_ShouldNotReferenceForbiddenDependencies()
    {
        var applicationAssembly = typeof(RegisterVehicleUseCase).Assembly;
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

        var forbiddenPrefixes = new[]
        {
            "FleetOps.Infrastructure",
            "FleetOps.Api",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "StackExchange.Redis",
            "RabbitMQ",
            "MassTransit",
            "Npgsql",
            "MediatR"
        };

        foreach (var referenced in referencedAssemblies)
        {
            var isForbidden = forbiddenPrefixes.Any(prefix =>
                referenced.Name != null && referenced.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            Assert.False(isForbidden, $"Application assembly references forbidden dependency: {referenced.Name}");
        }
    }

    [Fact]
    public void ApplicationAssembly_AllUseCases_ShouldHaveExecuteAsyncMethodAcceptingCancellationToken()
    {
        var applicationAssembly = typeof(RegisterVehicleUseCase).Assembly;
        var useCaseTypes = applicationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(useCaseTypes);

        foreach (var type in useCaseTypes)
        {
            var executeMethod = type.GetMethod("ExecuteAsync");
            Assert.NotNull(executeMethod);

            var parameters = executeMethod.GetParameters();
            Assert.Contains(parameters, p => p.ParameterType == typeof(CancellationToken));
        }
    }
}
