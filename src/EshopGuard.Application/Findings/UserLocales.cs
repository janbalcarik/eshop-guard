using EshopGuard.Data;
using EshopGuard.Data.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Findings;

/// <summary>The language of a user (<c>iam.users.locale</c>) for documents the backend writes; <c>sk</c> when it is not set.</summary>
public sealed class UserLocales(EshopGuardDb db)
{
    public const string Default = "sk";

    public async Task<string> LocaleAsync(Guid userId, CancellationToken ct) =>
        await db.ExecuteInTenantTransactionAsync(async () =>
            await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Locale).FirstOrDefaultAsync(ct).ConfigureAwait(false) ?? Default, ct)
            .ConfigureAwait(false);
}
