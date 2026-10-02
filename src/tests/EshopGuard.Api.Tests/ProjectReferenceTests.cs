using System.Xml.Linq;

namespace EshopGuard.Api.Tests;

/// <summary>
/// Direction of dependencies between projects (design of change 2, table "Směr závislostí"). Reads ProjectReference
/// from every *.csproj under src/, because the compiler drops unused references from the assemblies.
/// </summary>
public sealed class ProjectReferenceTests
{
    internal static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>
    {
        ["EshopGuard.Core"] = [],
        ["EshopGuard.Cli"] = ["EshopGuard.Core", "EshopGuard.Data"],
        ["EshopGuard.Storage"] = [],
        ["EshopGuard.Data"] = ["EshopGuard.Core"],
        ["EshopGuard.Jobs"] = ["EshopGuard.Core", "EshopGuard.Data", "EshopGuard.Storage"],
        ["EshopGuard.Billing"] = ["EshopGuard.Data", "EshopGuard.Jobs"],
        ["EshopGuard.Connectors"] = ["EshopGuard.Core", "EshopGuard.Data", "EshopGuard.Storage", "EshopGuard.Jobs"],
        ["EshopGuard.Application"] = ["EshopGuard.Data", "EshopGuard.Jobs"],
        ["EshopGuard.Api"] = ["EshopGuard.Data", "EshopGuard.Storage", "EshopGuard.Jobs", "EshopGuard.Billing", "EshopGuard.Connectors", "EshopGuard.Application"],
        ["EshopGuard.Worker"] = ["EshopGuard.Core", "EshopGuard.Data", "EshopGuard.Storage", "EshopGuard.Jobs", "EshopGuard.Billing", "EshopGuard.Connectors", "EshopGuard.Application"],
    };

    [Fact]
    public void Solution_FollowsTheDependencyDirection()
    {
        var root = RepositoryRoot.Find();
        var projects = RepositoryRoot.Files(root, "src", "*.csproj")
            .ToDictionary(p => Path.GetFileNameWithoutExtension(p), References);

        Assert.Empty(Violations(projects));
        Assert.Contains("EshopGuard.Api.Tests", projects.Keys);
        Assert.Equal(Allowed.Count, projects.Keys.Count(p => !p.EndsWith(".Tests", StringComparison.Ordinal)));
    }

    [Fact]
    public void CoreReferencingData_IsReported()
    {
        var projects = Allowed.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
        projects["EshopGuard.Core"] = ["EshopGuard.Data"];
        Assert.Contains("EshopGuard.Core → EshopGuard.Data", Violations(projects));
    }

    [Fact]
    public void UnknownProject_IsReported()
    {
        var projects = Allowed.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
        projects["EshopGuard.Reports"] = ["EshopGuard.Core"];
        Assert.Contains("EshopGuard.Reports: project without a dependency rule", Violations(projects));
    }

    [Fact]
    public void ReferenceToApi_IsReported()
    {
        var projects = Allowed.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value);
        projects["EshopGuard.Worker"] = [.. Allowed["EshopGuard.Worker"], "EshopGuard.Api"];
        Assert.Contains("EshopGuard.Worker → EshopGuard.Api", Violations(projects));
    }

    /// <summary>Forbidden edges; a test project may reference its project and what that project may reference.</summary>
    internal static List<string> Violations(IReadOnlyDictionary<string, IReadOnlyList<string>> projects)
    {
        var violations = new List<string>();
        foreach (var (project, references) in projects)
        {
            string[] allowed;
            if (Allowed.TryGetValue(project, out var direct))
            {
                allowed = direct;
            }
            else if (project.EndsWith(".Tests", StringComparison.Ordinal) && Allowed.TryGetValue(project[..^".Tests".Length], out var tested))
            {
                allowed = [project[..^".Tests".Length], .. tested];
            }
            else
            {
                violations.Add($"{project}: project without a dependency rule");
                continue;
            }

            violations.AddRange(references.Where(r => !allowed.Contains(r)).Select(r => $"{project} → {r}"));
        }

        return violations;
    }

    private static IReadOnlyList<string> References(string csproj) =>
        XDocument.Load(csproj).Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();
}
