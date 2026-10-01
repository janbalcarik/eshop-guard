using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EshopGuard.Storage.Health;

/// <summary>
/// <c>/health</c> check of the file store through <see cref="IBlobStoreProbe"/> (file system: writes and deletes <c>health/probe</c>).
/// The result carries only a code in <c>data["code"]</c>.
/// </summary>
public sealed class StorageHealthCheck(IServiceProvider services) : IHealthCheck
{
    /// <summary>Key of the error code in <see cref="HealthCheckResult.Data"/>.</summary>
    public const string CodeKey = "code";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string? code;
        try
        {
            var store = (IBlobStore?)services.GetService(typeof(IBlobStore));
            code = store is IBlobStoreProbe probe ? await probe.ProbeAsync(cancellationToken).ConfigureAwait(false) : StorageErrorCodes.Unreachable;
        }
        catch (StorageConfigurationException ex)
        {
            code = ex.Code;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The store could not be created, e.g. the root folder cannot be made.
            code = StorageErrorCodes.Unreachable;
        }

        return code is null
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy(data: new Dictionary<string, object> { [CodeKey] = code });
    }
}
