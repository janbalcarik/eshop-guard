using EshopGuard.Core.Fix;
using EshopGuard.Core.Options;
using EshopGuard.Core.Rules;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Markets;

/// <summary>Kind of a call of the model in the analysis of markets and versions.</summary>
public enum MarketCall
{
    /// <summary>Pick of the pages about sales and delivery from the links.</summary>
    Pick,

    /// <summary>Analysis of the places of sale.</summary>
    Sales,

    /// <summary>Language of the texts of a version.</summary>
    Language,
}

/// <summary>One call: its kind, the instructions (the same for every shop), the input of this shop, the schema and the effort.</summary>
public sealed record MarketModelRequest(MarketCall Call, string Instructions, string Input, object Schema, string ReasoningEffort);

/// <summary>The JSON answer of the model with its usage and price.</summary>
public sealed record MarketModelResponse(string Json, string? Model, long InputTokens, long CachedTokens, long OutputTokens, decimal CostUsd);

/// <summary>
/// The model of the analysis of the places of sale and of the language of texts (change 7). The default is the OpenAI client of
/// the rewrite (the same model, key and prices, <c>store: false</c>); <see cref="MockMarketModel"/> answers without network.
/// </summary>
public interface IMarketModel
{
    /// <summary>Code why no real model is called (<c>model_mock</c>, <c>model_missing_key</c>); null when it is.</summary>
    string? UnavailableReason { get; }

    /// <summary>Sends the call; throws <see cref="RewriteApiException"/> when the model fails.</summary>
    Task<MarketModelResponse> AskAsync(MarketModelRequest request, CancellationToken ct);
}

/// <summary>The model over <see cref="IRewriteClient"/> (OpenAI).</summary>
internal sealed class OpenAiMarketModel(IRewriteClient client, IOptions<EshopGuardOptions> options) : IMarketModel
{
    private RewriteOptions Rewrite => options.Value.Rewrite;

    public string? UnavailableReason =>
        Rewrite.UseMock ? EngineCodes.ModelMock
        : string.IsNullOrWhiteSpace(Rewrite.ApiKey) ? EngineCodes.ModelMissingKey
        : null;

    public async Task<MarketModelResponse> AskAsync(MarketModelRequest request, CancellationToken ct)
    {
        var response = await client.RewriteAsync(new RewriteRequest
        {
            SharedPart = request.Instructions,
            PagePart = request.Input,
            PromptVersion = MarketPrompts.Version + "-" + request.Call.ToString().ToLowerInvariant(),
            Schema = request.Schema,
            ReasoningEffort = request.ReasoningEffort,
        }, ct);
        var uncached = response.InputTokens - response.CachedTokens;
        var cost = uncached * Rewrite.InputUsdPerMillion / 1_000_000m
            + response.CachedTokens * Rewrite.CachedInputUsdPerMillion / 1_000_000m
            + response.OutputTokens * Rewrite.OutputUsdPerMillion / 1_000_000m;
        return new MarketModelResponse(response.Json, response.Model, response.InputTokens, response.CachedTokens, response.OutputTokens, cost);
    }
}
