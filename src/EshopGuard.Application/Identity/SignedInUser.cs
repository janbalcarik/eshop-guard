using EshopGuard.Data.Entities.Iam;

namespace EshopGuard.Application.Identity;

/// <summary>
/// A user who proved who he is; the API signs him in (cookie) with <see cref="Method"/> as the claim <c>amr</c>. The services
/// never sign in themselves: <c>EshopGuard.Application</c> does not depend on ASP.NET Core.
/// </summary>
public sealed record SignedInUser(User User, string Method, bool IsNewAccount, Guid? CreatedTenantId);
