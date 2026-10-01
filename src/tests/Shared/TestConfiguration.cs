using Microsoft.Extensions.Configuration;

namespace EshopGuard.Tests.Shared;

/// <summary>
/// Configuration of tests that need PostgreSQL: user-secrets <c>eshopguard-tests</c> (written by
/// <c>deploy/dev/setup-local.ps1</c>), overridable by environment variables <c>ESHOPGUARD_TEST_…</c>
/// (e.g. <c>ESHOPGUARD_TEST_ConnectionStrings__App</c>). A missing key fails the test with its name; nothing is skipped.
/// </summary>
internal static class TestConfiguration
{
    /// <summary>Prefix of the overriding environment variables.</summary>
    public const string EnvironmentPrefix = "ESHOPGUARD_TEST_";

    private static readonly Lazy<IConfiguration> Configuration = new(() => new ConfigurationBuilder()
        .AddUserSecrets("eshopguard-tests")
        .AddEnvironmentVariables(EnvironmentPrefix)
        .Build());

    /// <summary>The loaded configuration.</summary>
    public static IConfiguration Current => Configuration.Value;

    /// <summary>Returns the value, or throws with the key name (never the value) when it is missing or empty.</summary>
    public static string Require(string key)
    {
        var value = Current[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Chybí konfigurace testů: {key} (user-secrets eshopguard-tests z deploy/dev/setup-local.ps1, nebo proměnná {EnvironmentPrefix}{key.Replace(":", "__", StringComparison.Ordinal)}).");
        }

        return value;
    }

    /// <summary>Connection string <c>ConnectionStrings:{name}</c> to the test database <c>eshopguard_test</c>.</summary>
    public static string ConnectionString(string name) => Require("ConnectionStrings:" + name);
}
