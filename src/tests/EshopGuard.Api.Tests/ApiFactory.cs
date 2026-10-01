using EshopGuard.Data.Connections;
using EshopGuard.Tests.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EshopGuard.Api.Tests;

/// <summary>
/// The API in environment <c>Testing</c> (user-secrets <c>eshopguard-api</c> with the development database are not loaded),
/// with an explicit <c>ConnectionStrings:App</c>, a temporary file store and captured logs.
/// </summary>
internal sealed class ApiFactory(string? appConnectionString, bool withStartupGuard = true) : WebApplicationFactory<Program>
{
    private readonly DirectoryInfo _blobs = Directory.CreateTempSubdirectory("eshopguard-api-");

    public InMemoryLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", appConnectionString ?? string.Empty);
        builder.UseSetting("Storage:Provider", "FileSystem");
        builder.UseSetting("Storage:FileSystem:Root", _blobs.FullName);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILoggerProvider>(Logs);
            if (!withStartupGuard)
            {
                services.Remove(services.Single(d => d.ImplementationType == typeof(DatabaseStartupGuard)));
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _blobs.Exists)
        {
            _blobs.Delete(recursive: true);
        }
    }
}
