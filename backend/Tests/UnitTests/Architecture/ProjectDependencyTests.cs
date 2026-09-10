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
    [InlineData("Tools/DataMigration/DataMigration.csproj", "Infrastructure/Infrastructure.csproj")]
    public void Production_project_references_match_the_approved_graph(
        string project,
        params string[] expected)
    {
        Assert.Equal(expected.Order(), ReadProductionReferences(project).Order());
    }

    /// <summary>
    /// The migration tool moves the whole candidate dataset. It is an operator-run
    /// executable on purpose: if the API could reference it, the bulk-data path would be one
    /// endpoint away from being reachable over HTTP.
    /// </summary>
    [Theory]
    [InlineData("Domain/Domain.csproj")]
    [InlineData("Application/Application.csproj")]
    [InlineData("Infrastructure/Infrastructure.csproj")]
    [InlineData("Web/Web.csproj")]
    public void No_production_project_references_the_migration_tool(string project)
    {
        Assert.DoesNotContain(
            ReadProductionReferences(project),
            reference => reference.StartsWith("Tools/", StringComparison.Ordinal));
    }

    /// <summary>
    /// KTL-8 retired the KTL-5 reference candidate slice. Its purpose — proving the read
    /// path — is served by the real read slice, which travels the same boundaries under
    /// per-operation authorization, and a second, less-guarded route to candidate personal
    /// data must not survive.
    /// </summary>
    [Theory]
    [InlineData("Web/Features/Candidates/ReferenceCandidateEndpoints.cs")]
    [InlineData("Application/Features/Candidates/GetReferenceCandidate.cs")]
    [InlineData("Application/Abstractions/Persistence/ICandidateReader.cs")]
    [InlineData("Infrastructure/Persistence/CandidateReader.cs")]
    public void No_reference_candidate_slice_remains(string path)
    {
        Assert.False(
            File.Exists(Path.Combine(BackendRoot, path.Replace('/', Path.DirectorySeparatorChar))),
            $"{path} is part of the retired reference slice and must not exist.");
    }

    [Fact]
    public void No_production_source_mentions_the_reference_candidate_slice()
    {
        // Tests are excluded because this file names the retired types in order to assert
        // their absence, which would otherwise make the check find itself.
        var offenders = Directory
            .EnumerateFiles(BackendRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(BackendRoot, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("Tests/", StringComparison.Ordinal)
                && !path.Contains("/bin/", StringComparison.Ordinal)
                && !path.Contains("/obj/", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(Path.Combine(BackendRoot, path))
                .Contains("ReferenceCandidate", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Document_slices_follow_the_existing_project_dependency_direction()
    {
        var applicationDocuments = Path.Combine(BackendRoot, "Application", "Features", "Documents");
        var webDocuments = Path.Combine(BackendRoot, "Web", "Features", "Documents");
        Assert.True(Directory.Exists(applicationDocuments));
        Assert.True(Directory.Exists(webDocuments));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(applicationDocuments, "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains("KeplerTalento.Infrastructure", StringComparison.Ordinal));
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
            "Tools/DataMigration/DataMigration.csproj" => ["../../Infrastructure/Infrastructure.csproj"],
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
