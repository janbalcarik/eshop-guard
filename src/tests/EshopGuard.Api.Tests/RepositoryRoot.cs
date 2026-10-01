namespace EshopGuard.Api.Tests;

/// <summary>Locates the repository from the test output folder (the folder above <c>src/EshopGuard.sln</c>).</summary>
internal static class RepositoryRoot
{
    public static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "EshopGuard.sln")))
            {
                return dir.FullName;
            }

            if (File.Exists(Path.Combine(dir.FullName, "EshopGuard.sln")) && dir.Parent is not null)
            {
                return dir.Parent.FullName;
            }
        }

        throw new InvalidOperationException("EshopGuard.sln not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Files under <paramref name="relative"/> without build output.</summary>
    public static IEnumerable<string> Files(string root, string relative, string pattern) =>
        Directory.Exists(Path.Combine(root, relative))
            ? Directory.EnumerateFiles(Path.Combine(root, relative), pattern, SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or "node_modules"))
            : [];
}
