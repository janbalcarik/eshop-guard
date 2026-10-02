using EshopGuard.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EshopGuard.Application.Shops;

/// <summary>Settings of the e-shops in the API (<c>Shops</c>).</summary>
public sealed class ShopsOptions
{
    public const string SectionName = "Shops";

    /// <summary>
    /// Hosts (with a port) the check of an address allows although they are internal, e.g. <c>localhost:8000</c> of the test
    /// e-shop. Only in the environments Development and Testing; elsewhere the start is refused.
    /// </summary>
    public string[] AllowedDevHosts { get; set; } = [];
}

/// <summary>How long the API waits for an interactive job (<c>Api:InteractiveWaitSeconds</c>, proposal 8 s).</summary>
public sealed class InteractiveOptions
{
    public const string SectionName = "Api";

    public int InteractiveWaitSeconds { get; set; } = 8;
}

/// <summary>
/// Before which steps the ownership of an e-shop must be verified (<c>Shops:Ownership:RequiredBefore</c>: <c>sample</c>,
/// <c>full_analysis</c>, or <c>none</c> alone). No default (K rozhodnutí 1): without the setting the API and the worker do
/// not start, so nothing is decided silently.
/// </summary>
public sealed class OwnershipPolicyOptions
{
    public const string SectionName = "Shops:Ownership";
    public const string Sample = "sample";
    public const string FullAnalysis = "full_analysis";
    public const string None = "none";

    public string[]? RequiredBefore { get; set; }

    public bool Requires(string gate) => RequiredBefore?.Contains(gate, StringComparer.Ordinal) == true;
}

/// <summary>Refuses the start (<c>config.application_invalid</c>) for missing or unknown values; never prints a value.</summary>
internal sealed class ShopsOptionsValidator(IHostEnvironment environment) : IValidateOptions<ShopsOptions>, IValidateOptions<OwnershipPolicyOptions>, IValidateOptions<InteractiveOptions>
{
    private static readonly string[] Gates = [OwnershipPolicyOptions.Sample, OwnershipPolicyOptions.FullAnalysis];

    public ValidateOptionsResult Validate(string? name, ShopsOptions options) =>
        options.AllowedDevHosts.Length > 0 && !(environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            ? ValidateOptionsResult.Fail($"{ApplicationOptionsValidator.Code}: Shops:AllowedDevHosts (only in Development and Testing)")
            : ValidateOptionsResult.Success;

    public ValidateOptionsResult Validate(string? name, OwnershipPolicyOptions options)
    {
        var values = options.RequiredBefore;
        var valid = values is { Length: > 0 }
            && (values is [OwnershipPolicyOptions.None] || values.All(v => Gates.Contains(v, StringComparer.Ordinal)));
        return valid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{ApplicationOptionsValidator.Code}: Shops:Ownership:RequiredBefore (sample, full_analysis, or none)");
    }

    public ValidateOptionsResult Validate(string? name, InteractiveOptions options) =>
        options.InteractiveWaitSeconds is < 0 or > 30
            ? ValidateOptionsResult.Fail($"{ApplicationOptionsValidator.Code}: Api:InteractiveWaitSeconds")
            : ValidateOptionsResult.Success;
}
