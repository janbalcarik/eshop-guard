namespace EshopGuard.Application.Options;

/// <summary>
/// Settings under <c>Admin</c>: the users who run EshopGuard (the admin API <c>/api/admin/*</c>, change 12). There is no such
/// role in a tenant; the list is empty by default, so nobody is an administrator until the operator names the ids.
/// </summary>
public sealed class PlatformAdminOptions
{
    public const string SectionName = "Admin";

    public List<Guid> UserIds { get; set; } = [];

    public bool IsAdmin(Guid userId) => UserIds.Contains(userId);
}
