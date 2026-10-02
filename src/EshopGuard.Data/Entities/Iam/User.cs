using EshopGuard.Data.Entities.Common;

namespace EshopGuard.Data.Entities.Iam;

/// <summary>Global identity of a person (ASP.NET Core Identity store in change 9). Table <c>iam.users</c>.</summary>
public sealed class User : GlobalEntity, ISoftDeletable
{
    public required string Email { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? PasswordHash { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>Language chosen by the user; <c>null</c> = automatic (browser, then the market, change 9 AD 10).</summary>
    public string? Locale { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public string? SecurityStamp { get; set; }

    public string? ConcurrencyStamp { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAt { get; set; }
}
