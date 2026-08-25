using System.Xml.Linq;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Architecture;

public sealed class ProjectDependencyTests
{
    private static readonly string BackendRoot = FindBackendRoot();

    [Theory]
    [InlineData("Domain/Domain.csproj")]
    public void Domain_has_no_production_project_references(string project)
    {
        Assert.Empty(ReadProductionReferences(project));
    }

    [Theory]
    [InlineData("Application/Application.csproj", "Domain/Domain.csproj")]
    [InlineData("Infrastructure/Infrastructure.csproj", "Application/Application.csproj", "Domain/Domain.csproj")]
    [InlineData("Web/Web.csproj", "Application/Application.csproj", "Infrastructure/Infrastructure.csproj")]
    public void Production_project_references_match_the_approved_graph(
        string project,
        params string[] expected)
    {
        Assert.Equal(expected.Order(), ReadProductionReferences(project).Order());
    }

    [Fact]
    public void Deliberately_invalid_fixture_is_rejected()
    {
        var invalid = new[] { "../Infrastructure/Infrastructure.csproj" };
        Assert.False(IsAllowed("Application/Application.csproj", invalid));
    }

    private static IReadOnlyCollection<string> ReadProductionReferences(string project)
    {
        var projectPath = Path.Combine(BackendRoot, project.Replace('/', Path.DirectorySeparatorChar));
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        return XDocument.Load(projectPath)
            .Descendants("ProjectReference")
            .Select(node => node.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Path.GetRelativePath(BackendRoot, Path.GetFullPath(Path.Combine(projectDirectory, value!)))
                .Replace('\\', '/'))
            .Where(value => !value.StartsWith("Tests/", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsAllowed(string project, IEnumerable<string> references)
    {
        var allowed = project switch
        {
            "Domain/Domain.csproj" => Array.Empty<string>(),
            "Application/Application.csproj" => ["../Domain/Domain.csproj"],
            "Infrastructure/Infrastructure.csproj" => ["../Application/Application.csproj", "../Domain/Domain.csproj"],
            "Web/Web.csproj" => ["../Application/Application.csproj", "../Infrastructure/Infrastructure.csproj"],
            _ => Array.Empty<string>(),
        };
        return references.Order().SequenceEqual(allowed.Order());
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "KeplerTalento.slnx")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate backend solution root.");
    }
}
