using EshopGuard.Application.Problems;
using EshopGuard.Application.Security;
using EshopGuard.Data.Entities.Iam;

namespace EshopGuard.Application.Identity.Validators;

/// <summary>Checks of single fields of requests; each adds codes to a <see cref="ValidationResult"/>, never a sentence.</summary>
public static class FieldValidators
{
    /// <summary>Longest name of a person or of a tenant.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Longest password (a longer one only costs hashing time).</summary>
    public const int MaxPasswordLength = 256;

    /// <summary>The normalized e-mail address, or <c>null</c> with <c>email.invalid_format</c> (or <c>value.required</c>).</summary>
    public static string? Email(ValidationResult result, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, ProblemCodes.Fields.Required);
            return null;
        }

        var normalized = EmailNormalizer.Normalize(value);
        if (normalized is null)
        {
            result.Add(field, ProblemCodes.Fields.EmailInvalidFormat);
        }

        return normalized;
    }

    /// <summary>A new password: at least <paramref name="minLength"/> characters and not the e-mail address (nor its local part).</summary>
    public static void NewPassword(ValidationResult result, string field, string? password, string? email, int minLength)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrEmpty(password))
        {
            result.Add(field, ProblemCodes.Fields.Required);
            return;
        }

        if (password.Length < minLength)
        {
            result.Add(field, ProblemCodes.Fields.PasswordTooShort);
        }
        else if (password.Length > MaxPasswordLength)
        {
            result.Add(field, ProblemCodes.Fields.PasswordTooLong);
        }

        if (email is not null && (string.Equals(password.Trim(), email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(password.Trim(), email.Split('@')[0], StringComparison.OrdinalIgnoreCase)))
        {
            result.Add(field, ProblemCodes.Fields.PasswordSameAsEmail);
        }
    }

    /// <summary>A required name of at most <see cref="MaxNameLength"/> characters; returns it trimmed.</summary>
    public static string? Name(ValidationResult result, string field, string? value, bool required = true)
    {
        ArgumentNullException.ThrowIfNull(result);
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required)
            {
                result.Add(field, ProblemCodes.Fields.Required);
            }

            return null;
        }

        if (trimmed.Length > MaxNameLength)
        {
            result.Add(field, ProblemCodes.Fields.NameTooLong);
        }

        return trimmed;
    }

    /// <summary>A role of a membership by its code (<c>owner</c>, <c>admin</c>, <c>editor</c>, <c>viewer</c>).</summary>
    public static MembershipRole? Role(ValidationResult result, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, ProblemCodes.Fields.Required);
            return null;
        }

        if (Tenants.TenantRoles.TryParse(value, out var role))
        {
            return role;
        }

        result.Add(field, ProblemCodes.Fields.RoleInvalid);
        return null;
    }

    /// <summary>A code of a language or a market: 2 to 3 lower-case letters (whether it is enabled is checked against <c>ref</c>).</summary>
    public static string? Code(ValidationResult result, string field, string? value, bool required)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                result.Add(field, ProblemCodes.Fields.Required);
            }

            return null;
        }

        var code = value.Trim().ToLowerInvariant();
        if (code.Length is < 2 or > 3 || !code.All(char.IsAsciiLetterLower))
        {
            result.Add(field, ProblemCodes.Fields.CodeInvalid);
            return null;
        }

        return code;
    }

    /// <summary>A token of a link (it is only checked against the database; a wrong format is just an invalid token).</summary>
    public static string? Token(ValidationResult result, string field, string? value)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, ProblemCodes.Fields.Required);
            return null;
        }

        return value.Trim();
    }
}
