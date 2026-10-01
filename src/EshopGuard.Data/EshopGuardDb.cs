using Microsoft.EntityFrameworkCore;

namespace EshopGuard.Data;

/// <summary>
/// Database context of the web application. Without entities in F0; tables, RLS and tenants come with change 3.
/// </summary>
public sealed class EshopGuardDb(DbContextOptions<EshopGuardDb> options) : DbContext(options)
{
    /// <summary>Schema of the migration history and operational tables.</summary>
    public const string OpsSchema = "ops";

    /// <summary>Table of the EF Core migration history (in <see cref="OpsSchema"/>, not in <c>public</c>).</summary>
    public const string MigrationsHistoryTable = "__ef_migrations_history";
}
