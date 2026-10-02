using EshopGuard.Api.Problems;
using EshopGuard.Application.Problems;

namespace EshopGuard.Api.Contracts;

public sealed record CsrfTokenDto(string Token);

public sealed record LoginLinkRequest(string? Email, string? Market);

public sealed record TokenRequest(string? Token) : IValidatableRequest
{
    public void Validate(ValidationResult result) => Require(result, "token", Token);

    internal static void Require(ValidationResult result, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, ProblemCodes.Fields.Required);
        }
    }
}

public sealed record ConsumeLoginLinkRequest(string? Token, string? Market) : IValidatableRequest
{
    public void Validate(ValidationResult result) => TokenRequest.Require(result, "token", Token);
}

public sealed record PasswordLoginRequest(string? Email, string? Password);

public sealed record ForgotPasswordRequest(string? Email, string? Market);

public sealed record ResetPasswordRequest(string? Token, string? NewPassword) : IValidatableRequest
{
    public void Validate(ValidationResult result) => TokenRequest.Require(result, "token", Token);
}

public sealed record UpdateMeRequest(string? DisplayName);

public sealed record SetLocaleRequest(string? Locale);

public sealed record SetPasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record CreateTenantRequest(string? Name, string? Market);

public sealed record RenameTenantRequest(string? Name, uint? Version);

public sealed record ChangeRoleRequest(string? Role);

public sealed record TransferOwnershipRequest(Guid? UserId);

public sealed record CreateInvitationRequest(string? Email, string? Role, string? Locale);

public sealed record AcceptInvitationRequest(string? Token, string? Market) : IValidatableRequest
{
    public void Validate(ValidationResult result) => TokenRequest.Require(result, "token", Token);
}

public sealed record CreateShopRequest(string? Url);

public sealed record RenameShopRequest(string? Name, uint? Version);

public sealed record SetPlatformRequest(string? Platform);

public sealed record FeedRequest(string? Url, string? Format);

public sealed record SetSourceRequest(string? Mode, FeedRequest? Feed);

public sealed record ConfirmMarketsRequest(string[]? Active);

public sealed record LanguageConfirmationRequest(bool? BelongsToShop);

public sealed record LanguageExclusionRequest(bool? Excluded);

public sealed record QuoteRequest(string[]? ActiveMarkets, string[]? ExcludedLanguages);

public sealed record CreateVerificationRequest(string? Method);

/// <summary>A field left out keeps its value; <c>name: null</c> clears the name.</summary>
public sealed record UpdateShopSettingsRequest(System.Text.Json.JsonElement? Name, string[]? Modules, bool? CheckHiddenOnSave, uint? Version);
public sealed record FindingDecisionRequest(string? ReasonCode);

public sealed record SelectAlternativeRequest(string? Key);

public sealed record EditProposalTextRequest(string? Text);

public sealed record ProposalPlaceholdersRequest(Dictionary<string, string?>? Values);

/// <summary><c>PUT S/fix-groups/{groupId}/values</c>: the facts of the placeholders; an empty value clears the fact.</summary>
public sealed record FixGroupValuesRequest(Dictionary<string, string?>? Values);

/// <summary><c>PUT S/fix-groups/{groupId}/mode</c>: <c>replace</c>, <c>remove</c> or <c>custom</c> with its own wording.</summary>
public sealed record FixGroupModeRequest(string? Mode, string? CustomText);

/// <summary><c>PUT S/fix-groups/{groupId}/excluded-pages</c>: the pages the fix of the group is not written to.</summary>
public sealed record FixGroupExcludedPagesRequest(Guid[]? PageIds);

/// <summary><c>POST S/fix-groups/{groupId}/approve-page</c>: „Len na tejto stránke“.</summary>
public sealed record FixGroupPageRequest(Guid? PageId);

/// <summary><c>POST S/publications</c>: the accepted changes of these pages, proposals or group (one of them at least).</summary>
public sealed record PublicationRequest(Guid[]? PageIds, Guid[]? ProposalIds, Guid? GroupId);

/// <summary><c>POST S/protocols</c>: the period (days in <c>Localization:TimeZone</c>) and the language (otherwise the one of the home market).</summary>
public sealed record ProtocolRequest(DateOnly? PeriodFrom, DateOnly? PeriodTo, string? Locale);

/// <summary><c>POST S/questions/{questionId}/answer</c>: <c>yes</c> („Áno“) or <c>no</c> („Nie“).</summary>
public sealed record AnswerQuestionRequest(string? Answer);

/// <summary><c>POST T/notifications/read-all</c>: all, or only those of one e-shop.</summary>
public sealed record ReadAllNotificationsRequest(Guid? ShopId);

/// <summary><c>PUT T/notification-settings</c>: the account (<c>shopId</c> null) or one e-shop.</summary>
public sealed record NotificationSettingsRequest(Guid? ShopId, bool? EmailNewViolation, bool? EmailWeeklySummary, bool? EmailRunFinished);

/// <summary>The metadata of <c>POST T/evidence</c> (the form field <c>metadata</c>, JSON).</summary>
public sealed record CreateEvidenceRequest(
    string? ClaimText, string? SubjectKind, string? SubjectLabel, string? Kind, string? Title, DateOnly? ValidFrom, DateOnly? ValidUntil, string? RegistryRef);

/// <summary><c>PATCH T/evidence/{id}</c>: only the given fields change.</summary>
public sealed record UpdateEvidenceRequest(string? ClaimText, string? SubjectKind, string? SubjectLabel, string? Title, DateOnly? ValidFrom, DateOnly? ValidUntil);

/// <summary><c>POST T/evidence/{id}/links</c>.</summary>
public sealed record EvidenceLinksRequest(IReadOnlyList<Guid>? FindingIds);
