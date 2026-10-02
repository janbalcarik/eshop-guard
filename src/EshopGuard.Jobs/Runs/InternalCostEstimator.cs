using System.Text.Json.Nodes;
using EshopGuard.Core.Options;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The internal estimate of the cost of a run (<c>runs.estimate.internal</c>; the customer never sees it): after discovery
/// roughly (pages × Jev calls per page from the settings), after segmentation as the upper bound of <c>EstimateStep</c>, the
/// profiles from their plan, the rewrites from the estimate of the rewriter, the places of sale from their estimate. Rates
/// are the same as the CLI's (<c>cost.usd_per_million_input_tokens</c>, <c>rewrite.*_usd_per_million</c>). Every part is
/// stored before its first paid call.
/// </summary>
public static class InternalCostEstimator
{
    /// <summary>The rough estimate after discovery.</summary>
    public static JsonObject Rough(int pages, RunsOptions runs, EshopGuardOptions guard, DateTimeOffset now)
    {
        var calls = (long)Math.Ceiling(pages * runs.FullAnalysis.RoughJevCallsPerPage);
        var tokens = calls * runs.FullAnalysis.RoughTokensPerCall;
        var estimate = new JsonObject
        {
            ["basis"] = "discovery",
            ["pages_planned"] = pages,
            ["jev_calls_upper"] = calls,
            ["jev_input_tokens"] = tokens,
            ["jev_usd"] = Math.Round(tokens * guard.Cost.UsdPerMillionInputTokens / 1_000_000m, 6),
            ["sieve_calls"] = 0,
            ["openai"] = new JsonObject { ["market_usd"] = 0m, ["profiles_usd"] = 0m, ["rewrite_usd"] = 0m },
        };
        return WithTotal(estimate, now);
    }

    /// <summary>The Jev part after segmentation: the upper bound of the sieve and the detailed questions.</summary>
    public static JsonObject Segmented(JsonObject estimate, long calls, long sieveCalls, long inputTokens, decimal usd, DateTimeOffset now)
    {
        estimate["basis"] = "segmented";
        estimate["jev_calls_upper"] = calls;
        estimate["sieve_calls"] = sieveCalls;
        estimate["jev_input_tokens"] = inputTokens;
        estimate["jev_usd"] = Math.Round(usd, 6);
        return WithTotal(estimate, now);
    }

    /// <summary>One part of the OpenAI estimate (<c>market_usd</c>, <c>profiles_usd</c>, <c>rewrite_usd</c>).</summary>
    public static JsonObject OpenAi(JsonObject estimate, string part, decimal usd, DateTimeOffset now)
    {
        RunStore.Section(estimate, "openai")[part] = Math.Round(usd, 6);
        return WithTotal(estimate, now);
    }

    /// <summary>The total of the estimate in USD.</summary>
    public static decimal Total(JsonObject estimate)
    {
        var openai = estimate["openai"] as JsonObject ?? [];
        return RunStore.Decimal(estimate, "jev_usd") + RunStore.Decimal(openai, "market_usd") + RunStore.Decimal(openai, "profiles_usd") + RunStore.Decimal(openai, "rewrite_usd");
    }

    /// <summary>Whether the free sample may pay the estimate (the cap is the consent to the price of a sample nobody pays for).</summary>
    public static bool WithinSampleBudget(JsonObject estimate, RunsOptions runs) => Total(estimate) <= runs.FreeSample.MaxInternalUsd;

    private static JsonObject WithTotal(JsonObject estimate, DateTimeOffset now)
    {
        estimate["total_usd"] = Math.Round(Total(estimate), 6);
        estimate["computed_at"] = now;
        return estimate;
    }
}
