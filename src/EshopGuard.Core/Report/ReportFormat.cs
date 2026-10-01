using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EshopGuard.Core.Models;

namespace EshopGuard.Core.Report;

/// <summary>
/// Shared formatting of output files: JSON options and Czech labels.
/// </summary>
internal static class ReportFormat
{
    public static readonly CultureInfo Czech = CultureInfo.GetCultureInfo("cs-CZ");

    /// <summary>snake_case like the specification, enums as snake_case strings, readable Czech characters.</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    /// <summary>Czech name of a role of a skip region of a page template profile.</summary>
    public static string ProfileRoleName(string role) => role switch
    {
        "navigation" => "navigace",
        "breadcrumb" => "drobečková navigace",
        "pagination" => "stránkování",
        "filters" => "filtry a řazení",
        "related_products" => "podobné produkty",
        "product_listing" => "výpisy produktů",
        "cookie_bar" => "cookie lišta",
        "login_form" => "přihlášení",
        "newsletter_form" => "pole formuláře newsletteru",
        "comment_form" => "formulář komentářů",
        "search" => "vyhledávání",
        "cart" => "košík",
        "social_share" => "sdílení",
        _ => role,
    };

    public static int SeverityRank(string severity) => severity switch
    {
        "high" => 0,
        "medium" => 1,
        "low" => 2,
        _ => 3,
    };

    public static string Severity(string severity) => severity switch
    {
        "high" => "vysoká",
        "medium" => "střední",
        "low" => "nízká",
        _ => severity,
    };

    /// <summary>How sure Jev is that the text is what the rule describes (not whether it is a violation).</summary>
    public static string Band(FindingBand band) => band == FindingBand.High ? "vysoká jistota" : "nižší jistota";

    public static string Checkability(string checkability) => checkability switch
    {
        "text" => "porušení podle textu zákona",
        "assess" => "k posouzení, záleží na tom, jak text chápe průměrný spotřebitel",
        "verify" => "k ověření, záleží na faktech mimo web",
        "not_checkable" => "z textu nelze posoudit",
        _ => checkability,
    };

    /// <summary>Order of the groups in the report.</summary>
    public static int CheckabilityRank(string checkability) => checkability switch
    {
        "text" => 0,
        "assess" => 1,
        "verify" => 2,
        _ => 3,
    };

    public static string PageTypeName(PageType type) => type switch
    {
        PageType.Home => "úvodní",
        PageType.Product => "produktová",
        PageType.Legal => "právní",
        _ => "ostatní",
    };

    /// <summary>Czech name of a sign from <see cref="Extract.RenderCheck"/>; framework names stay as they are.</summary>
    public static string ScriptAppName(string app) => app switch
    {
        Extract.RenderCheck.NoscriptMessage => "stránka žádá zapnout JavaScript",
        Extract.RenderCheck.AppState => "stav aplikace v JavaScriptu",
        Extract.RenderCheck.EmptyAppRoot => "prázdný kontejner aplikace",
        _ => $"aplikace {app}",
    };

    public static string Number(double value, string format = "0.00") => value.ToString(format, Czech);

    public static string Number(long value) => value.ToString("N0", Czech);

    public static string Usd(decimal value) => value.ToString("0.000000", Czech) + " USD";

    public static string Questions(IReadOnlyDictionary<string, double> probabilities) =>
        string.Join("; ", probabilities.Select(p => $"{p.Key}={p.Value.ToString("0.000", CultureInfo.InvariantCulture)}"));
}
