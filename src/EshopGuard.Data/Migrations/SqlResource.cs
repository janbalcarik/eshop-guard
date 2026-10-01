namespace EshopGuard.Data.Migrations;

/// <summary>SQL scripts of migrations, embedded from <c>Migrations/Sql/**/*.sql</c>.</summary>
public static class SqlResource
{
    /// <summary>Reads e.g. <c>F1/04_rls.sql</c>; a missing script is an error naming the file.</summary>
    public static string Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var suffix = ".Migrations.Sql." + path.Replace('/', '.').Replace('\\', '.');
        var assembly = typeof(SqlResource).Assembly;
        var name = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded migration script not found: {path}");
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
