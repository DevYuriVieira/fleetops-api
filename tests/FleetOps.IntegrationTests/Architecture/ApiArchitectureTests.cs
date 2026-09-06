namespace FleetOps.IntegrationTests.Architecture;

using System.Reflection;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.UseCases.Vehicles;
using FleetOps.Domain.Entities;
using FleetOps.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class ApiArchitectureTests
{
    [Fact]
    public void DomainAssembly_ShouldNotReferenceApiInfrastructureOrAspNetCore()
    {
        var domainAssembly = typeof(Vehicle).Assembly;
        var references = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        var forbidden = new[]
        {
            "FleetOps.Api",
            "FleetOps.Infrastructure",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "RabbitMQ",
            "MassTransit",
            "MediatR"
        };

        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(references, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ApplicationAssembly_ShouldNotReferenceApiInfrastructureOrAspNetCore()
    {
        var appAssembly = typeof(RegisterVehicleUseCase).Assembly;
        var references = appAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        var forbidden = new[]
        {
            "FleetOps.Api",
            "FleetOps.Infrastructure",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "RabbitMQ",
            "MassTransit",
            "MediatR"
        };

        foreach (var f in forbidden)
        {
            Assert.DoesNotContain(references, r => r != null && r.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void InfrastructureAssembly_ShouldNotReferenceApi()
    {
        var infraAssembly = typeof(FleetOpsDbContext).Assembly;
        var references = infraAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain(references, r => r != null && r.StartsWith("FleetOps.Api", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r != null && r.StartsWith("Microsoft.AspNetCore.Mvc", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r != null && r.StartsWith("MassTransit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => r != null && r.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Controllers_ShouldNotDependOnDbContextRepositoriesOrUnitOfWork()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(controllerTypes);

        var forbiddenTypes = new[]
        {
            typeof(DbContext),
            typeof(FleetOpsDbContext),
            typeof(IUnitOfWork),
            typeof(IVehicleRepository),
            typeof(IDriverRepository),
            typeof(IDeliveryRepository),
            typeof(IRouteRepository),
            typeof(IMaintenanceRepository)
        };

        foreach (var controller in controllerTypes)
        {
            var constructors = controller.GetConstructors();
            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    foreach (var forbidden in forbiddenTypes)
                    {
                        Assert.False(
                            forbidden.IsAssignableFrom(param.ParameterType),
                            $"Controller '{controller.Name}' constructor parameter '{param.Name}' has forbidden type '{param.ParameterType.Name}'.");
                    }
                }
            }

            var fields = controller.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var field in fields)
            {
                foreach (var forbidden in forbiddenTypes)
                {
                    Assert.False(
                        forbidden.IsAssignableFrom(field.FieldType),
                        $"Controller '{controller.Name}' field '{field.Name}' has forbidden type '{field.FieldType.Name}'.");
                }
            }
        }
    }

    [Fact]
    public void AllControllerActionMethods_MustAcceptCancellationToken()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();

            foreach (var action in actions)
            {
                var hasCancellationToken = action.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken));

                Assert.True(
                    hasCancellationToken,
                    $"Action '{controller.Name}.{action.Name}' must accept a CancellationToken.");
            }
        }
    }

    [Fact]
    public void AllControllerActionMethods_MustReturnTaskOfActionResult()
    {
        var apiAssembly = typeof(Program).Assembly;
        var controllerTypes = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        foreach (var controller in controllerTypes)
        {
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();

            foreach (var action in actions)
            {
                var returnType = action.ReturnType;
                var isTask = typeof(Task).IsAssignableFrom(returnType);

                Assert.True(
                    isTask,
                    $"Action '{controller.Name}.{action.Name}' must return a Task.");
            }
        }
    }
}
