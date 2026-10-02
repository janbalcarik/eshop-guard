using EshopGuard.Application.Security;
using EshopGuard.Data;
using EshopGuard.Data.Entities.Iam;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Application.Identity;

/// <summary>
/// Store of ASP.NET Core Identity over <c>iam.users</c> and <c>iam.user_logins</c> (AD 1): the user name is the e-mail
/// address, normalized by <see cref="EmailNormalizer"/> (lower case), so <c>Jana@Bylinkovo.SK</c> finds
/// <c>jana@bylinkovo.sk</c> through the unique index on <c>lower(email)</c>. A deleted account (<c>deleted_at</c>) is not
/// found. Lockout is always enabled. The tables have no RLS (identity is global), so no tenant is needed.
/// </summary>
public sealed class EgUserStore(EshopGuardDb db) :
    IUserEmailStore<User>, IUserPasswordStore<User>, IUserSecurityStampStore<User>, IUserLockoutStore<User>, IUserLoginStore<User>
{
    public const string GoogleProvider = "google";

    public void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(Require(user).Id.ToString("D"));

    public Task<string?> GetUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(Require(user).Email);

    public Task SetUserNameAsync(User user, string? userName, CancellationToken cancellationToken) => SetEmailAsync(user, userName, cancellationToken);

    public Task<string?> GetNormalizedUserNameAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(Require(user).Email);

    public Task SetNormalizedUserNameAsync(User user, string? normalizedName, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<IdentityResult> CreateAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(Require(user));
        return await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IdentityResult> UpdateAsync(User user, CancellationToken cancellationToken)
    {
        Require(user);
        if (db.Entry(user).State == EntityState.Detached)
        {
            db.Users.Attach(user);
            db.Entry(user).State = EntityState.Modified;
        }

        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        return await SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Accounts are never deleted here (a deletion of personal data is a job of change 16).</summary>
    public Task<IdentityResult> DeleteAsync(User user, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Accounts are deleted by the job of the deletion of personal data.");

    public async Task<User?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        Guid.TryParse(userId, out var id) ? await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false) : null;

    public Task<User?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
        FindByEmailAsync(normalizedUserName, cancellationToken);

    public Task SetEmailAsync(User user, string? email, CancellationToken cancellationToken)
    {
        Require(user).Email = EmailNormalizer.Normalize(email) ?? throw new ArgumentException("email.invalid_format", nameof(email));
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(Require(user).Email);

    public Task<bool> GetEmailConfirmedAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).EmailConfirmed);

    public Task SetEmailConfirmedAsync(User user, bool confirmed, CancellationToken cancellationToken)
    {
        Require(user).EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(normalizedEmail);
        return email is null
            ? null
            : await db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> GetNormalizedEmailAsync(User user, CancellationToken cancellationToken) => Task.FromResult<string?>(Require(user).Email);

    public Task SetNormalizedEmailAsync(User user, string? normalizedEmail, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SetPasswordHashAsync(User user, string? passwordHash, CancellationToken cancellationToken)
    {
        Require(user).PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).PasswordHash);

    public Task<bool> HasPasswordAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).PasswordHash is not null);

    public Task SetSecurityStampAsync(User user, string stamp, CancellationToken cancellationToken)
    {
        Require(user).SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).SecurityStamp);

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).LockoutEnd);

    public Task SetLockoutEndDateAsync(User user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        Require(user).LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(User user, CancellationToken cancellationToken) =>
        Task.FromResult(++Require(user).AccessFailedCount);

    public Task ResetAccessFailedCountAsync(User user, CancellationToken cancellationToken)
    {
        Require(user).AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(User user, CancellationToken cancellationToken) => Task.FromResult(Require(user).AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(User user, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task SetLockoutEnabledAsync(User user, bool enabled, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AddLoginAsync(User user, UserLoginInfo login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        db.UserLogins.Add(new UserLogin { UserId = Require(user).Id, Provider = login.LoginProvider, ProviderKey = login.ProviderKey });
        return Task.CompletedTask;
    }

    public async Task RemoveLoginAsync(User user, string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        var id = Require(user).Id;
        var login = await db.UserLogins.FirstOrDefaultAsync(l => l.UserId == id && l.Provider == loginProvider && l.ProviderKey == providerKey, cancellationToken)
            .ConfigureAwait(false);
        if (login is not null)
        {
            db.UserLogins.Remove(login);
        }
    }

    public async Task<IList<UserLoginInfo>> GetLoginsAsync(User user, CancellationToken cancellationToken)
    {
        var id = Require(user).Id;
        return await db.UserLogins.Where(l => l.UserId == id)
            .Select(l => new UserLoginInfo(l.Provider, l.ProviderKey, l.Provider))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
    {
        var userId = await db.UserLogins.Where(l => l.Provider == loginProvider && l.ProviderKey == providerKey)
            .Select(l => (Guid?)l.UserId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return userId is null ? null : await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
    }

    private static User Require(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user;
    }

    private async Task<IdentityResult> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return IdentityResult.Success;
        }
        catch (DbUpdateConcurrencyException)
        {
            return IdentityResult.Failed(new IdentityErrorDescriber().ConcurrencyFailure());
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            return IdentityResult.Failed(new IdentityErrorDescriber().DuplicateEmail(string.Empty));
        }
    }
}

/// <summary>Lookup normalization of Identity = <see cref="EmailNormalizer"/> (lower case, punycode), not upper case.</summary>
public sealed class EmailLookupNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name) => EmailNormalizer.Normalize(name) ?? name?.Trim().ToLowerInvariant();

    public string? NormalizeEmail(string? email) => EmailNormalizer.Normalize(email) ?? email?.Trim().ToLowerInvariant();
}
