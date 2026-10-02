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
