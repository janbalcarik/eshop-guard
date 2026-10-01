using EshopGuard.Core.Rules;
using System.Text.Json;
using System.Text.Json.Nodes;
using EshopGuard.Core.Fix;
using EshopGuard.Core.Options;
using Microsoft.Extensions.Options;

namespace EshopGuard.Core.Profiles;

/// <summary>
/// The language model that writes a profile from outlines of sample pages.
/// </summary>
public interface IProfileModel
{
    /// <summary>False when no model can be called (mock run, missing key); stored profiles are still used.</summary>
    bool IsAvailable { get; }

    /// <summary>Code of the reason why the model is not available (<c>model_mock</c>, <c>model_missing_key</c>); null when it is.</summary>
    string? UnavailableReason { get; }

    /// <summary>Price of one profile from the outlines it would get, in USD.</summary>
    decimal EstimateUsd(IReadOnlyList<string> outlines);

    /// <summary>Asks for the regions of the template shown by the outlines.</summary>
    Task<ProfileAnswer> AskAsync(IReadOnlyList<(string Url, string Outline)> samples, CancellationToken ct);
}

/// <summary>
/// Regions returned by the model and the price of the request.
/// </summary>
public sealed class ProfileAnswer
{
    /// <summary>Regions as returned, not yet validated.</summary>
    public required IReadOnlyList<ProfileRegion> Regions { get; init; }

    /// <summary>Model that answered.</summary>
    public string? Model { get; init; }

    /// <summary>Input tokens.</summary>
    public long InputTokens { get; init; }

    /// <summary>Output tokens, reasoning included.</summary>
    public long OutputTokens { get; init; }

    /// <summary>Price in USD.</summary>
    public decimal CostUsd { get; init; }
}

/// <summary>
/// Profile model over the OpenAI client of the rewrite (same model, key and prices, <c>store: false</c>).
/// </summary>
internal sealed class OpenAiProfileModel(IRewriteClient client, IOptions<EshopGuardOptions> options) : IProfileModel
{
    /// <summary>The outline has about 3.3 characters per token (HTML outlines of 1. 10. 2026: 66 481 characters were 19 694 tokens with the instructions).</summary>
    private const double CharsPerToken = 3.3;

    private RewriteOptions Rewrite => options.Value.Rewrite;

    public bool IsAvailable => UnavailableReason is null;

    public string? UnavailableReason =>
        Rewrite.UseMock ? EngineCodes.ModelMock
        : string.IsNullOrWhiteSpace(Rewrite.ApiKey) ? EngineCodes.ModelMissingKey
        : null;

    public decimal EstimateUsd(IReadOnlyList<string> outlines)
    {
        var input = (ProfilePrompt.Instructions.Length + outlines.Sum(o => o.Length + 40)) / CharsPerToken;
        return Price((long)input, options.Value.Profiles.EstimatedOutputTokens);
    }

    public async Task<ProfileAnswer> AskAsync(IReadOnlyList<(string Url, string Outline)> samples, CancellationToken ct)
    {
        var pages = string.Join("\n\n", samples.Select((s, i) => $"=== PAGE {i + 1} ===\n{s.Outline}"));
        var response = await client.RewriteAsync(new RewriteRequest
        {
            SharedPart = ProfilePrompt.Instructions,
            PagePart = pages,
            PromptVersion = ProfilePrompt.Version,
            Schema = ProfilePrompt.Schema,
        }, ct);

        return new ProfileAnswer
        {
            Regions = Parse(response.Json),
            Model = response.Model,
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            CostUsd = Price(response.InputTokens - response.CachedTokens, response.OutputTokens)
                + response.CachedTokens * Rewrite.CachedInputUsdPerMillion / 1_000_000m,
        };
    }

    internal static List<ProfileRegion> Parse(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new JsonException("Empty profile.");
        return (root["regions"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(r => new ProfileRegion
            {
                Role = r["role"]?.GetValue<string>() ?? "other",
                Action = r["action"]?.GetValue<string>() ?? ProfileRegion.Check,
                Selector = r["selector"]?.GetValue<string>() ?? "",
                Example = r["example"]?.GetValue<string>() ?? "",
                Reason = r["reason"]?.GetValue<string>() ?? "",
            })
            .ToList();
    }

    private decimal Price(long input, long output) =>
        input * Rewrite.InputUsdPerMillion / 1_000_000m + output * Rewrite.OutputUsdPerMillion / 1_000_000m;
}

/// <summary>
/// Instructions and answer schema of the profile model. Tried on vegis.sk and naturfyt.sk on 1. 10. 2026 (the probe
/// version told the model the page types; this one does not need them): page types 16 of 16, no selector without a match
/// on 10 unseen pages, no sentence of the shop lost. The probe skipped the whole newsletter box with the shop's own
/// invitation; forms now skip only their fields.
/// </summary>
internal static class ProfilePrompt
{
    public const string Version = "profile-2026-10-01";

    public const string Instructions = """
        You analyze the HTML template of an e-shop for a consumer-law text checker.
        The checker must read EVERY text the shop itself shows to customers about its products or itself: product names on the
        product's own page, descriptions, short descriptions, badges and labels, parameters, prices and delivery terms, banners,
        benefit bars, category descriptions, headings, header and footer texts, article teasers, published reviews.
        It may skip ONLY these interface parts:
        - navigation: menus, category trees, footer link lists, breadcrumbs, pagination, filters and sorting, tab labels;
        - tiles that list OTHER products: related, recommended, recently viewed, bestsellers, accessories, category product grids;
        - cookie consent bar, login and registration, search, cart widget and add-to-cart controls, social share buttons;
        - form fields, buttons and consent checkboxes of newsletter and comment forms. The heading and the invitation text of
          a newsletter or comment box are the shop's own text: keep them in a separate "check" region or do not cover them.
        Everything else must be checked. When unsure whether a region contains the shop's own texts, use action "check".

        You get simplified outlines of sample pages of the same shop: elements with id and class, short texts, links as paths;
        "more similar siblings follow" marks cut repetitions. Return the regions of the template:
        - role from the list, action "check" or "skip", a CSS selector, a short example text from the outline and a reason;
        - action "skip" only with the roles navigation, breadcrumb, pagination, filters, related_products, product_listing,
          cookie_bar, login_form, newsletter_form, comment_form, search, cart, social_share;
        - cover the whole page: every part of the outline that has text should belong to some region, checked or skipped;
        - selectors must work on every page of the same template: tag names, classes, ids, descendant and child combinators,
          attribute selectors such as [class*=x], selector lists with commas; no :has, no :contains, no :nth-child, no ids or
          numbers specific to one product, category or page;
        - prefer the outermost element of a region; a skip region must never contain a region that is checked.
        """;

    private static readonly string[] Roles =
    [
        "main_description", "product_title", "product_box", "badges", "price", "short_description", "parameters", "reviews",
        "category_description", "banner", "benefits_bar", "header", "footer", "related_products", "product_listing",
        "navigation", "breadcrumb", "pagination", "filters", "cookie_bar", "login_form", "newsletter_form", "comment_form",
        "search", "cart", "social_share", "other",
    ];

    /// <summary>JSON schema of the answer (strict structured output).</summary>
    public static object Schema { get; } = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["regions"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["role"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = Roles },
                        ["action"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { ProfileRegion.Check, ProfileRegion.Skip } },
                        ["selector"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["example"] = new Dictionary<string, object> { ["type"] = "string" },
                        ["reason"] = new Dictionary<string, object> { ["type"] = "string" },
                    },
                    ["required"] = new[] { "role", "action", "selector", "example", "reason" },
                    ["additionalProperties"] = false,
                },
            },
        },
        ["required"] = new[] { "regions" },
        ["additionalProperties"] = false,
    };
}
