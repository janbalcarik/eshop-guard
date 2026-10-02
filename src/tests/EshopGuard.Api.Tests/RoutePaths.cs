using System.Text.RegularExpressions;

namespace EshopGuard.Api.Tests;

/// <summary>Fills the parameters of a route pattern for the tests that walk every endpoint.</summary>
internal static partial class RoutePaths
{
    /// <summary>
    /// <c>{tenantId:guid}</c> by <paramref name="tenantId"/>, other parameters by <paramref name="values"/>, otherwise a new
    /// GUID (<c>:guid</c>) or <c>sk</c> (a language).
    /// </summary>
    public static string Fill(string pattern, Guid tenantId, IReadOnlyDictionary<string, string>? values = null) =>
        Parameter().Replace(pattern, m =>
        {
            var name = m.Groups["name"].Value;
            if (name == "tenantId")
            {
                return tenantId.ToString("D");
            }

            if (values is not null && values.TryGetValue(name, out var value))
            {
                return value;
            }

            return m.Groups["guid"].Success ? Guid.NewGuid().ToString("D") : "sk";
        });

    [GeneratedRegex(@"\{(?<name>\w+)(?<guid>:guid)?\}")]
    private static partial Regex Parameter();
}
