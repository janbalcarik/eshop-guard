using EshopGuard.Data.Entities.Usage;
using EshopGuard.Data.Stores;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The tenant, e-shop, run and job a step of a run works for. The steps and stores of the library are singletons (the CLI
/// builds them once); in the worker they serve many tenants, so the job sets this context for the time of its handler
/// (<see cref="RunAmbient.Enter"/>) and the stores read it. A store used without it fails: data of a tenant is never read or
/// written without knowing whose it is.
/// </summary>
public sealed class RunAmbientScope(Guid tenantId, Guid shopId, Guid runId, long jobId)
{
    public Guid TenantId { get; } = tenantId;

    public Guid ShopId { get; } = shopId;

    public Guid RunId { get; } = runId;

    public long JobId { get; } = jobId;

    /// <summary>What the Jev calls of the handler are for (sentence evaluation, sieve, check of a rewrite).</summary>
    public UsageOperation JevOperation { get; set; } = UsageOperation.SentenceEval;

    /// <summary>Usage of paid calls not written yet.</summary>
    internal UsageBuffer Usage { get; } = new();
}

/// <summary>The <see cref="RunAmbientScope"/> of the current job (flows with the async context into the steps).</summary>
public static class RunAmbient
{
    private static readonly AsyncLocal<RunAmbientScope?> Scope = new();

    /// <summary>The context of the running job, or null outside of a job.</summary>
    public static RunAmbientScope? Current => Scope.Value;

    /// <summary>The context; throws outside of a job.</summary>
    public static RunAmbientScope Required =>
        Scope.Value ?? throw new InvalidOperationException("No run context: a store of a run was used outside of a job of the run.");

    /// <summary>Sets the context until the result is disposed.</summary>
    public static IDisposable Enter(RunAmbientScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var previous = Scope.Value;
        Scope.Value = scope;
        return new Exit(previous);
    }

    private sealed class Exit(RunAmbientScope? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Scope.Value = previous;
            }
        }
    }
}

/// <summary>The tenant and e-shop of the stores of the library (<c>PgJevCache</c>, <c>PgPageProfileStore</c>…) in the worker: those of the running job.</summary>
public sealed class AmbientStoreTenant : IStoreTenant
{
    public Guid TenantId => RunAmbient.Required.TenantId;

    public Guid? ShopId => RunAmbient.Required.ShopId;
}
