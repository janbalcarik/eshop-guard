using System.Text.Json;
using System.Text.RegularExpressions;

namespace EshopGuard.Core.Fix;

/// <summary>
/// Test double of the rewrite model: removes generic environmental words from the block of each finding, or keeps the
/// finding when there is nothing to remove. Deterministic, no network, nothing paid.
/// </summary>
internal sealed partial class MockRewriteClient : IRewriteClient
{
    public Task<RewriteResponse> RewriteAsync(RewriteRequest request, CancellationToken ct)
    {
        var blocks = request.Blocks.ToDictionary(b => b.Key, b => b.Value);
        var changes = new List<object>();
        var kept = new List<object>();
        foreach (var group in request.Findings.GroupBy(f => f.Blocks.FirstOrDefault() ?? ""))
        {
            var original = blocks.GetValueOrDefault(group.Key, "");
            var rewritten = Spaces().Replace(GenericWords().Replace(original, ""), " ").Replace(" .", ".", StringComparison.Ordinal).Trim();
            if (group.Key.Length > 0 && rewritten != original)
            {
                changes.Add(new
                {
                    block_ids = new[] { group.Key },
                    original,
                    rewritten,
                    finding_ids = group.Select(f => f.Id).ToArray(),
                    placeholders = Array.Empty<string>(),
                    reason_cs = "Falešný klient: vypuštěno obecné environmentální slovo.",
                });
            }
            else
            {
                kept.AddRange(group.Select(f => new { finding_id = f.Id, reason_cs = "Falešný klient: nic k vypuštění." }));
            }
        }

        var json = JsonSerializer.Serialize(new { changes, kept });
        return Task.FromResult(new RewriteResponse
        {
            Json = json,
            Model = "mock",
            InputTokens = (request.SharedPart.Length + request.PagePart.Length) / 3,
            OutputTokens = json.Length / 3,
        });
    }

    [GeneratedRegex(@"\b(ekologick\w*|eco-friendly|udržateľn\w*|udržiteln\w*|(a )?šetrn\w*( k (prírode|přírodě|životnému prostrediu|životnímu prostředí))?)", RegexOptions.IgnoreCase)]
    private static partial Regex GenericWords();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
