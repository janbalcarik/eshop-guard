using System.Text;
using System.Text.Json;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes the profiles of page templates used in the scan: every region with its selector, action and reason (rejected
/// regions with the reason they are not used), the sample pages the model saw, how many pages used the profile and how
/// many characters it left out by role. With pages.jsonl (profile_id, profile_skipped_text) it shows what was not sent
/// to Jev and why.
/// </summary>
internal sealed class ProfilesJsonWriter : IReportWriter
{
    public string FileName => "profiles.json";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        if (result.Profiles.Count == 0)
        {
            return;
        }

        var json = JsonSerializer.Serialize(
            result.Profiles.Select(use => new
            {
                use.Profile.Id,
                use.CreatedInThisScan,
                use.Pages,
                use.SkippedCharsByRole,
                use.Profile.CreatedAt,
                use.Profile.Model,
                use.Profile.PromptVersion,
                use.Profile.SampleUrls,
                use.Profile.Regions,
            }),
            ReportFormat.Json);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, FileName), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
    }
}
