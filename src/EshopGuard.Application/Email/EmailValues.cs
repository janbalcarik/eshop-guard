using System.Globalization;
using System.Text.Json.Nodes;

namespace EshopGuard.Application.Email;

/// <summary>
/// Values of a notification as the recipient reads them (change 12): amounts (<c>amount</c>, <c>oldAmount</c>, <c>newAmount</c>
/// with <c>currency</c>) as <c>59 €</c> / <c>1 490 Kč</c>, dates (<c>date</c>, ISO <c>yyyy-MM-dd</c>) as <c>1. 12. 2026</c>.
/// The notification itself carries codes and numbers only.
/// </summary>
internal static class EmailValues
{
    private static readonly string[] Amounts = ["amount", "oldAmount", "newAmount"];

    public static void Format(Dictionary<string, object?> values, JsonObject parameters, string locale)
    {
        var currency = (string?)parameters["currency"];
        foreach (var name in Amounts.Where(values.ContainsKey))
        {
            if (values[name] is { } raw && decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                values[name] = Money(amount, currency ?? string.Empty, locale);
            }
        }

        if (values.TryGetValue("date", out var date) && DateOnly.TryParseExact(Convert.ToString(date, CultureInfo.InvariantCulture), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day))
        {
            values["date"] = string.Create(CultureInfo.InvariantCulture, $"{day.Day}. {day.Month}. {day.Year}");
        }
    }

    public static string Money(decimal amount, string currency, string locale)
    {
        var culture = CultureInfo.GetCultureInfo(locale.StartsWith("cs", StringComparison.Ordinal) ? "cs-CZ" : "sk-SK");
        var number = (amount == decimal.Truncate(amount) ? amount.ToString("#,0", culture) : amount.ToString("#,0.00", culture))
            .Replace(culture.NumberFormat.NumberGroupSeparator, " ", StringComparison.Ordinal);
        return currency.ToUpperInvariant() switch
        {
            "EUR" => number + " €",
            "CZK" => number + " Kč",
            "" => number,
            var other => number + " " + other,
        };
    }
}
