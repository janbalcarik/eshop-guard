namespace EshopGuard.Application.Contracts;

/// <summary>The signed-in user (design of change 9, „DTO“); no token, hash or password, ever.</summary>
public sealed record MeDto(
    Guid Id,
    string Email,
    string? DisplayName,
    string? Locale,
    string EffectiveLocale,
    string LocaleSource,
    bool HasPassword,
    bool GoogleLinked,
    IReadOnlyList<MeMembershipDto> Memberships,
    IReadOnlyList<PendingInvitationDto> PendingInvitations);

public sealed record MeMembershipDto(Guid TenantId, string TenantName, string? LegalName, string Role, string Status);

public sealed record PendingInvitationDto(Guid InvitationId, string TenantName, string Role, DateTimeOffset ExpiresAt);

/// <summary>Answer of a sign-in: the user, whether the account was created now, and the tenant created for it.</summary>
public sealed record SessionDto(MeDto Me, bool IsNewAccount, Guid? CreatedTenantId);

/// <summary>Answer of an accepted invitation: the session and the new membership.</summary>
public sealed record InvitationSessionDto(MeDto Me, bool IsNewAccount, MembershipDto Membership);

public sealed record MembershipDto(Guid TenantId, string TenantName, string Role);

public sealed record TenantDto(
    Guid Id, string Name, string? LegalName, string CountryCode, string MarketCode, string Locale, string? Currency, string Status, string MyRole, uint Version);

public sealed record MemberDto(Guid UserId, string Email, string? DisplayName, string Role, Guid? InvitedBy, DateTimeOffset JoinedAt, DateTimeOffset? LastLoginAt);

public sealed record InvitationDto(Guid Id, string Email, string Role, Guid InvitedBy, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt, string Status);

public sealed record InvitationInfoDto(string TenantName, string? InviterDisplayName, string Role, string Email, DateTimeOffset ExpiresAt, bool AccountExists);

public sealed record LoginLinkRequestedDto(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record LoginLinkInfoDto(string Email, bool IsNewAccount, DateTimeOffset ExpiresAt);

public sealed record ResetLinkInfoDto(string Email, DateTimeOffset ExpiresAt);

public sealed record LocaleDto(string Code, string Name);

public sealed record MarketDto(
    string Code, string CountryCode, string DefaultLocale, IReadOnlyList<string> UiLocales, string Currency, string WebStatus, string ChecksStatus);
