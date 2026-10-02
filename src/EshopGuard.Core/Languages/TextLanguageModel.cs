using System.Text;
using System.Text.Json.Nodes;
using EshopGuard.Core.Markets;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Languages;

/// <summary>A sentence of the main text of a product page of a version, numbered for the model.</summary>
public sealed record LanguageFragment(int Id, string Text, string PageUrl);

/// <summary>Language per fragment (BCP 47 primary subtag, <c>und</c> when the model could not say) and the call.</summary>
public sealed record TextLanguageResult(IReadOnlyDictionary<int, string> Labels, MarketModelResponse? Call);

/// <summary>The language of the texts of a version, decided by the model over sentences, never by lists of words or letters.</summary>
public interface ITextLanguageModel
{
    /// <param name="declaredLanguage">The language the version declares; only the fake model uses it as its answer.</param>
    Task<TextLanguageResult> LabelAsync(IReadOnlyList<LanguageFragment> fragments, string? declaredLanguage, CancellationToken ct);
}

/// <summary>
/// One call of <see cref="IMarketModel"/> per version over about <c>markets.language_fragments_per_version</c> sentences
/// (change 7, design section 6). With <see cref="MockMarketModel"/> every sentence gets the declared language.
/// </summary>
internal sealed class TextLanguageModel(IMarketModel model, IOptions<EshopGuardOptions> options) : ITextLanguageModel
{
    public async Task<TextLanguageResult> LabelAsync(IReadOnlyList<LanguageFragment> fragments, string? declaredLanguage, CancellationToken ct)
    {
        if (fragments.Count == 0)
        {
            return new TextLanguageResult(new Dictionary<int, string>(), null);
        }

        var call = await model.AskAsync(new MarketModelRequest(MarketCall.Language, MarketPrompts.LanguageInstructions,
            Input(fragments, model is MockMarketModel ? declaredLanguage : null), MarketPrompts.LanguageSchema, options.Value.Markets.LanguageReasoningEffort), ct);
        var labels = new Dictionary<int, string>();
        foreach (var label in (JsonNode.Parse(call.Json)?["labels"] as JsonArray ?? []).OfType<JsonObject>())
        {
            if (label["id"] is JsonValue id && id.TryGetValue<int>(out var number) && label["language"] is JsonValue language && language.TryGetValue<string>(out var text))
            {
                labels[number] = LanguageTags.Primary(text) is { Length: > 0 } primary ? primary : "und";
            }
        }

        return new TextLanguageResult(labels, call);
    }

    /// <summary>Numbered sentences, one per line; the fake model also gets the declared language.</summary>
    internal static string Input(IReadOnlyList<LanguageFragment> fragments, string? declaredLanguage)
    {
        var text = new StringBuilder();
        if (declaredLanguage is not null)
        {
            text.Append(MockMarketModel.DeclaredLanguagePrefix).AppendLine(declaredLanguage);
        }

        foreach (var fragment in fragments)
        {
            text.Append(fragment.Id).Append(". ").AppendLine(fragment.Text.Replace('\n', ' '));
        }

        return text.ToString();
    }
}
