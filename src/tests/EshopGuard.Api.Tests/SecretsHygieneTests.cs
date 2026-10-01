using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EshopGuard.Api.Tests;

/// <summary>
/// No literal password in configuration files of the repository. Allowed are only references to a variable
/// (<c>$…</c>, <c>${…}</c>, <c>{…}</c>) and empty values. Source <c>*.cs</c> files are not scanned
/// (<see cref="LogRedactionTests"/> holds a test password on purpose); <c>deploy/.env</c> is outside the repository.
/// </summary>
public sealed partial class SecretsHygieneTests
{
    private static readonly string Root = RepositoryRoot.Find();

    public static TheoryData<string> ConfigurationFiles()
    {
        string[] deployPatterns = ["*.json", "*.yml", "*.yaml", "*.sql", "*.ps1", "*.sh", ".env.example"];
        var files = RepositoryRoot.Files(Root, "src", "appsettings*.json")
            .Concat(RepositoryRoot.Files(Root, "src", "launchSettings.json"))
            .Concat(deployPatterns.SelectMany(p => RepositoryRoot.Files(Root, "deploy", p)))
            .Concat(deployPatterns.SelectMany(p => RepositoryRoot.Files(Root, Path.Combine("src", "tests"), p)))
            .Where(f => !string.Equals(Path.GetFileName(f), ".env", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Distinct()
            .Order(StringComparer.Ordinal);
        return [.. files];
    }

    [Fact]
    public void Scan_FindsTheConfigurationFiles()
    {
        var files = ConfigurationFiles().Select(row => row.Data).ToList();
        Assert.Contains("deploy/sql/00_roles.sql", files);
        Assert.Contains("deploy/dev/setup-local.ps1", files);
        Assert.Contains("src/EshopGuard.Api/appsettings.json", files);
    }

    [Theory]
    [MemberData(nameof(ConfigurationFiles))]
    public void File_HasNoLiteralPassword(string relativePath)
    {
        var lines = File.ReadAllLines(Path.Combine(Root, relativePath));
        var offending = lines
            .Select((line, index) => (line, number: index + 1))
            .Where(l => LiteralPassword().IsMatch(l.line))
            .Select(l => $"{relativePath}:{l.number}")
            .ToList();
        Assert.True(offending.Count == 0, "Literal password in: " + string.Join(", ", offending));
    }

    [Fact]
    public void DeployEnv_IsIgnoredAndNotTracked()
    {
        var ignore = File.ReadAllLines(Path.Combine(Root, ".gitignore")).Select(l => l.Trim());
        Assert.Contains("deploy/.env", ignore);

        if (TryGit("ls-files --error-unmatch deploy/.env", out var exitCode))
        {
            Assert.NotEqual(0, exitCode);
        }
    }

    // Password=value or PASSWORD: value, where value is not empty and not a variable reference ($x, ${x}, {x}).
    [GeneratedRegex("""(?i)(password\s*=|password:)\s*(?![\s;"',]|$|\$|\{|"\$|"\{|'\$|'\{)""")]
    private static partial Regex LiteralPassword();

    private static bool TryGit(string arguments, out int exitCode)
    {
        exitCode = -1;
        try
        {
            using var git = Process.Start(new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (git is null)
            {
                return false;
            }

            git.WaitForExit(10_000);
            exitCode = git.ExitCode;
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
