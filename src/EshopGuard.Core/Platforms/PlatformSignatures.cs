using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EshopGuard.Core.Platforms;

/// <summary>Kinds of technical signatures of a platform; the text of a page is never one of them.</summary>
public static class SignatureKinds
{
    /// <summary><c>&lt;meta name="…" content="…"&gt;</c>, e.g. <c>generator</c>.</summary>
    public const string Meta = "meta";

    /// <summary>Host of a file the page loads (script, style, image), e.g. <c>cdn.myshoptet.com</c>.</summary>
    public const string Host = "host";

    /// <summary>Path of a file the page loads or of the API of the platform, e.g. <c>/wp-content/plugins/woocommerce/</c>.</summary>
    public const string Path = "path";

    /// <summary>Header of the response, e.g. <c>x-shopid</c>.</summary>
    public const string Header = "header";

    /// <summary>Beginning of the name of a cookie the response sets, e.g. <c>_shopify_y</c>.</summary>
    public const string Cookie = "cookie";

    /// <summary>Every kind.</summary>
    public static readonly IReadOnlyList<string> All = [Meta, Host, Path, Header, Cookie];
}

/// <summary>
/// Kinds of the links a platform switches the currency or the language with (change 7, Shoptet: <c>/action/Currency/changeCurrency/</c>).
/// They never recognize a platform; they are read only on a page whose platform is certain (<see cref="PlatformLinkReader"/>).
/// </summary>
public static class PlatformLinkKinds
{
    /// <summary>A link that switches the currency of the prices; <c>parameter</c> holds the ISO 4217 code.</summary>
    public const string CurrencySwitch = "currency_switch";

    /// <summary>A link that switches the language of the shop (a cookie or a session, same address); <c>parameter</c> holds the language.</summary>
    public const string LanguageSwitch = "language_switch";

    /// <summary>Every kind of a switch.</summary>
    public static readonly IReadOnlyList<string> All = [CurrencySwitch, LanguageSwitch];
}

/// <summary>A link of a platform from <c>config/platforms.yaml</c>: its kind, the path of the link and the parameter with the value.</summary>
public sealed record PlatformLink(string Code, string Kind, string Path, string Parameter);

/// <summary>One technical signature of a platform from <c>config/platforms.yaml</c>.</summary>
/// <param name="Code">Code of the signal in the answer (<c>shoptet.cdn_host</c>).</param>
/// <param name="Kind">One of <see cref="SignatureKinds"/>.</param>
/// <param name="Value">Host, path or the beginning of a cookie name; for <c>meta</c> and <c>header</c> the name.</param>
/// <param name="Contains">Part of the value of the meta or the header (any case); null when the name is enough.</param>
public sealed record PlatformSignature(string Code, string Kind, string Value, string? Contains);

/// <summary>
/// The technical signatures of the platforms (<c>config/platforms.yaml</c>): a platform is a block of data, another platform
/// is a new block, never code. Unknown fields and kinds are errors, so a typo never silently disables a signature.
/// </summary>
public sealed class PlatformSignatures
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private PlatformSignatures(IReadOnlyDictionary<string, IReadOnlyList<PlatformSignature>> byPlatform, IReadOnlyDictionary<string, IReadOnlyList<PlatformLink>> links)
    {
        ByPlatform = byPlatform;
        LinksByPlatform = links;
    }

    /// <summary>Signatures by code of the platform (<c>shoptet</c>, <c>woocommerce</c> …), in the order of the file.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PlatformSignature>> ByPlatform { get; }

    /// <summary>The links of the platforms that switch the currency or the language (<see cref="PlatformLinkKinds"/>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PlatformLink>> LinksByPlatform { get; }

    /// <summary>Reads the file; throws <see cref="InvalidDataException"/> with every error found.</summary>
    public static async Task<PlatformSignatures> LoadAsync(string file, CancellationToken ct = default)
    {
        if (!File.Exists(file))
        {
            throw new InvalidDataException($"Soubor s podpisy platforem „{System.IO.Path.GetFullPath(file)}“ neexistuje.");
        }

        return Parse(await File.ReadAllTextAsync(file, ct), System.IO.Path.GetFileName(file));
    }

    /// <summary>Parses the YAML text; <paramref name="name"/> is used in the errors.</summary>
    public static PlatformSignatures Parse(string yaml, string name = "platforms.yaml")
    {
        Dictionary<string, List<SignatureYaml>>? raw;
        try
        {
            raw = Deserializer.Deserialize<Dictionary<string, List<SignatureYaml>>?>(yaml);
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"{name}: {ex.Message}", ex);
        }

        var errors = new List<string>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var result = new Dictionary<string, IReadOnlyList<PlatformSignature>>(StringComparer.Ordinal);
        var links = new Dictionary<string, IReadOnlyList<PlatformLink>>(StringComparer.Ordinal);
        foreach (var (platform, items) in raw ?? [])
        {
            if (platform.Length == 0 || platform != platform.ToLowerInvariant() || platform is "other" or "unknown")
            {
                errors.Add($"{name}: kód platformy „{platform}“ musí být malými písmeny a nesmí být other ani unknown.");
            }

            var list = new List<PlatformSignature>();
            var platformLinks = new List<PlatformLink>();
            foreach (var item in items ?? [])
            {
                var code = $"{platform}.{item.Code}";
                if (string.IsNullOrWhiteSpace(item.Code) || !codes.Add(code))
                {
                    errors.Add($"{name}: {platform} má podpis bez kódu nebo se stejným kódem „{item.Code}“.");
                }

                if (PlatformLinkKinds.All.Contains(item.Kind))
                {
                    var path = item.Value?.Trim();
                    if (string.IsNullOrEmpty(path) || !path.StartsWith('/') || string.IsNullOrWhiteSpace(item.Parameter))
                    {
                        errors.Add($"{name}: {code} druhu {item.Kind} potřebuje value (cestu od /) a parameter.");
                        continue;
                    }

                    platformLinks.Add(new PlatformLink(code, item.Kind, path, item.Parameter.Trim()));
                    continue;
                }

                if (item.Parameter is not null)
                {
                    errors.Add($"{name}: {code} druhu {item.Kind} nemá mít parameter.");
                }

                if (!SignatureKinds.All.Contains(item.Kind))
                {
                    errors.Add($"{name}: {code} má neznámý druh „{item.Kind}“ (povolené: {string.Join(", ", SignatureKinds.All.Concat(PlatformLinkKinds.All))}).");
                }

                var value = (item.Kind is SignatureKinds.Meta or SignatureKinds.Header ? item.Name : item.Value)?.Trim();
                if (string.IsNullOrEmpty(value))
                {
                    errors.Add($"{name}: {code} nemá {(item.Kind is SignatureKinds.Meta or SignatureKinds.Header ? "name" : "value")}.");
                    continue;
                }

                if (item.Kind is SignatureKinds.Host or SignatureKinds.Path or SignatureKinds.Cookie && item.Contains is not null)
                {
                    errors.Add($"{name}: {code} druhu {item.Kind} nemá mít contains.");
                }

                list.Add(new PlatformSignature(code, item.Kind, item.Kind is SignatureKinds.Cookie ? value : value.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(item.Contains) ? null : item.Contains.Trim().ToLowerInvariant()));
            }

            if (list.Count == 0)
            {
                errors.Add($"{name}: platforma {platform} nemá žádný podpis.");
            }

            result[platform] = list;
            links[platform] = platformLinks;
        }

        if (result.Count == 0)
        {
            errors.Add($"{name}: žádná platforma.");
        }

        return errors.Count > 0 ? throw new InvalidDataException(string.Join(Environment.NewLine, errors)) : new PlatformSignatures(result, links);
    }

    private sealed class SignatureYaml
    {
        public string Code { get; set; } = "";

        public string Kind { get; set; } = "";

        public string? Name { get; set; }

        public string? Value { get; set; }

        public string? Contains { get; set; }

        /// <summary>Of a link of the platform: the query parameter with the currency or the language.</summary>
        public string? Parameter { get; set; }

        /// <summary>Where the signature was seen (documentation of the data, not used).</summary>
        public string? Source { get; set; }
    }
}
