namespace FleetOps.IntegrationTests.Architecture;

using System.IO;
using Xunit;

public sealed class DockerInfrastructureTests
{
    private static string GetSolutionRoot()
    {
        var currentDir = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(currentDir);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FleetOps.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    [Fact]
    public void DockerComposeFile_ExistsAndDefinesRequiredServices()
    {
        var root = GetSolutionRoot();
        var composePath = Path.Combine(root, "compose.yaml");

        Assert.True(File.Exists(composePath), "compose.yaml must exist at the solution root.");

        var content = File.ReadAllText(composePath);

        Assert.Contains("fleetops-api:", content);
        Assert.Contains("postgres:", content);
        Assert.Contains("fleetops-network", content);
        Assert.Contains("postgres-data", content);
    }

    [Fact]
    public void DockerComposeFile_ConfiguresInterContainerCommunicationViaServiceName()
    {
        var root = GetSolutionRoot();
        var composePath = Path.Combine(root, "compose.yaml");
        var content = File.ReadAllText(composePath);

        Assert.Contains("Host=postgres", content);
        Assert.DoesNotContain("Host=localhost", content);
    }

    [Fact]
    public void DockerComposeFile_ConfiguresHealthchecksAndDependencyOrder()
    {
        var root = GetSolutionRoot();
        var composePath = Path.Combine(root, "compose.yaml");
        var content = File.ReadAllText(composePath);

        Assert.Contains("pg_isready", content);
        Assert.Contains("/health/ready", content);
        Assert.Contains("service_healthy", content);
    }

    [Fact]
    public void Dockerfile_UsesMultiStageBuildAndNonRootUser()
    {
        var root = GetSolutionRoot();
        var dockerfilePath = Path.Combine(root, "Dockerfile");

        Assert.True(File.Exists(dockerfilePath), "Dockerfile must exist at the solution root.");

        var content = File.ReadAllText(dockerfilePath);

        Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build", content);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime", content);
        Assert.Contains("USER $APP_UID", content);
        Assert.Contains("ENTRYPOINT [\"dotnet\", \"FleetOps.Api.dll\"]", content);
    }

    [Fact]
    public void DockerignoreAndEnvExample_Exist()
    {
        var root = GetSolutionRoot();
        var dockerignorePath = Path.Combine(root, ".dockerignore");
        var envExamplePath = Path.Combine(root, ".env.example");

        Assert.True(File.Exists(dockerignorePath), ".dockerignore must exist at the solution root.");
        Assert.True(File.Exists(envExamplePath), ".env.example must exist at the solution root.");

        var dockerignoreContent = File.ReadAllText(dockerignorePath);
        Assert.Contains("**/bin", dockerignoreContent);
        Assert.Contains("**/obj", dockerignoreContent);

        var envContent = File.ReadAllText(envExamplePath);
        Assert.Contains("POSTGRES_DB", envContent);
        Assert.Contains("POSTGRES_USER", envContent);
        Assert.Contains("POSTGRES_PASSWORD", envContent);
    }
}
