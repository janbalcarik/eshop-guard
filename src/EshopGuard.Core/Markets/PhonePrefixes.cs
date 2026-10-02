namespace EshopGuard.Core.Markets;

/// <summary>
/// Country calling code of an international number by the structure of the E.164 numbering plan: codes are prefix-free,
/// 1 and 7 have one digit, the codes below have two and every other code has three. Only the shape of the number is read;
/// which country a code belongs to is left to the analysis (the model sees the code), so no country is named in code.
/// </summary>
internal static class PhonePrefixes
{
    /// <summary>Two-digit country calling codes of E.164 (zones 2 to 9); all other codes of these zones have three digits.</summary>
    private static readonly HashSet<string> TwoDigit =
    [
        "20", "27", "30", "31", "32", "33", "34", "36", "39", "40", "41", "43", "44", "45", "46", "47", "48", "49",
        "51", "52", "53", "54", "55", "56", "57", "58", "60", "61", "62", "63", "64", "65", "66",
        "81", "82", "84", "86", "90", "91", "92", "93", "94", "95", "98",
    ];

    /// <summary>The calling code of a number written as <c>+</c> and digits; null when it is too short.</summary>
    public static string? CallingCode(string number)
    {
        var digits = number.TrimStart('+');
        if (digits.Length < 4 || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        if (digits[0] is '1' or '7')
        {
            return digits[..1];
        }

        return TwoDigit.Contains(digits[..2]) ? digits[..2] : digits[..3];
    }
}
