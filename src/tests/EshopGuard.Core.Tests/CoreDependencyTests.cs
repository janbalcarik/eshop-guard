using System.Xml.Linq;

namespace EshopGuard.Core.Tests;

/// <summary>
/// The library does not depend on the database (task 3.9): storage goes through the interfaces of
/// <c>EshopGuard.Core.Storage</c>, the web application brings its implementations.
/// </summary>
public sealed class CoreDependencyTests
{
    private static readonly string[] Forbidden = ["EshopGuard.Data", "Npgsql", "Microsoft.EntityFrameworkCore"];

    [Fact]
    public void CoreAssembly_ReferencesNoDatabaseAssembly()
    {
        var references = typeof(IEshopGuard).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, r => Forbidden.Any(f => r.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void CoreProject_HasNoDatabasePackageOrProject()
    {
        var project = XDocument.Load(Path.Combine(SourceRoot(), "EshopGuard.Core", "EshopGuard.Core.csproj"));
        var includes = project.Descendants()
            .Where(e => e.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value ?? "")
            .ToList();

        Assert.DoesNotContain(includes, i => Forbidden.Any(f => i.Contains(f, StringComparison.Ordinal)));
    }

    private static string SourceRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("EshopGuard.sln not found above the test output.");
    }
}
