using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace EshopGuard.Core.Extract;

/// <summary>
/// Reads JSON-LD blocks and finds a <c>Product</c> node (description, name, category, identifiers), the currencies of
/// prices and breadcrumb names. Invalid JSON is ignored.
/// </summary>
internal static partial class JsonLdReader
{
    private const int MaxDepth = 12;

    private static readonly HashSet<string> ProductTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Product", "ProductGroup", "IndividualProduct", "ProductModel", "SomeProducts",
    };

    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
        MaxDepth = 64,
    };

    /// <summary>Identifiers of a product in schema.org, in the order they are read.</summary>
    private static readonly string[] IdentifierProperties = ["gtin", "gtin8", "gtin12", "gtin13", "gtin14", "sku", "mpn", "productID"];

    /// <param name="HasProduct">A Product node exists.</param>
    /// <param name="ProductDescription">Description of the first product that has one.</param>
    /// <param name="CategoryHints">Product names, product categories and breadcrumb names: where the product category is read from.</param>
    public sealed record Result(bool HasProduct, string? ProductDescription, IReadOnlyList<string> CategoryHints)
    {
        /// <summary>Currencies of prices (<c>priceCurrency</c> anywhere, e.g. in offers), ISO 4217 upper case.</summary>
        public IReadOnlyList<string> Currencies { get; init; } = [];

        /// <summary>Identifiers of the products (<c>gtin*</c>, <c>sku</c>, <c>mpn</c>, <c>productID</c>).</summary>
        public IReadOnlyList<Models.ProductIdentifier> ProductIds { get; init; } = [];
    }

    private sealed class State
    {
        public bool HasProduct;
        public string? Description;
        public readonly List<string> CategoryHints = [];
        public readonly List<string> Currencies = [];
        public readonly List<Models.ProductIdentifier> ProductIds = [];
    }

    public static Result Read(IDocument document)
    {
        var state = new State();
        foreach (var script in document.QuerySelectorAll("script"))
        {
            if (!string.Equals(script.GetAttribute("type")?.Trim(), "application/ld+json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using var json = JsonDocument.Parse(script.TextContent, ParseOptions);
                Visit(json.RootElement, 0, state);
            }
            catch (JsonException)
            {
                // Broken JSON-LD is common on real sites; the page is classified by other signals.
            }
        }

        return new Result(state.HasProduct, state.Description, state.CategoryHints.Distinct().ToList())
        {
            Currencies = state.Currencies.Distinct().ToList(),
            ProductIds = state.ProductIds.Distinct().ToList(),
        };
    }

    private static void Visit(JsonElement element, int depth, State state)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Visit(item, depth + 1, state);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (HasType(element, ProductTypes.Contains))
        {
            state.HasProduct = true;
            if (state.Description is null
                && element.TryGetProperty("description", out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                var text = CleanValue(value.GetString());
                state.Description = text.Length > 0 ? text : null;
            }

            AddNames(element, "name", state.CategoryHints);
            AddNames(element, "category", state.CategoryHints);
            foreach (var property in IdentifierProperties)
            {
                if (ScalarText(element, property) is { Length: > 0 } id)
                {
                    state.ProductIds.Add(new Models.ProductIdentifier(property, id));
                }
            }
        }
        else if (HasType(element, type => type.Equals("BreadcrumbList", StringComparison.OrdinalIgnoreCase))
            && element.TryGetProperty("itemListElement", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            // Only breadcrumb items: other item lists on category pages name the products listed there.
            foreach (var listItem in items.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
            {
                AddNames(listItem, "name", state.CategoryHints);
                if (listItem.TryGetProperty("item", out var node) && node.ValueKind == JsonValueKind.Object)
                {
                    AddNames(node, "name", state.CategoryHints);
                }
            }
        }

        if (ScalarText(element, "priceCurrency") is { Length: 3 } currency && currency.All(char.IsAsciiLetter))
        {
            state.Currencies.Add(currency.ToUpperInvariant());
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                Visit(property.Value, depth + 1, state);
            }
        }
    }

    /// <summary>A string, an array of strings, or an object with a name (schema.org Thing or CategoryCode).</summary>
    private static void AddNames(JsonElement element, string property, List<string> names)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return;
        }

        foreach (var item in value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value])
        {
            var text = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String => name.GetString(),
                _ => null,
            };
            var clean = CleanValue(text);
            if (clean.Length > 0)
            {
                names.Add(clean);
            }
        }
    }

    /// <summary>A string or a number of the property, trimmed; null when it is something else.</summary>
    private static string? ScalarText(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static string CleanValue(string? value) => TextTools.Clean(WebUtility.HtmlDecode(Tags().Replace(value ?? "", "\n")));

    private static bool HasType(JsonElement element, Func<string, bool> matches)
    {
        if (!element.TryGetProperty("@type", out var type))
        {
            return false;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => matches(ShortType(type.GetString())),
            JsonValueKind.Array => type.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && matches(ShortType(t.GetString()))),
            _ => false,
        };
    }

    /// <summary>"https://schema.org/Product" and "schema:Product" give "Product".</summary>
    private static string ShortType(string? type)
    {
        var name = (type ?? "").Trim().TrimEnd('/');
        var slash = name.LastIndexOfAny(['/', '#', ':']);
        return slash >= 0 ? name[(slash + 1)..] : name;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
