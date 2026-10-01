using EshopGuard.Data.Entities.Ops;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Jobs.Processing;

/// <summary>
/// Handlers by kind, from <c>IEnumerable&lt;IJobHandler&gt;</c> in DI. Two handlers of one kind fail the start
/// (<c>job.duplicate_kind</c>); a job of an unknown kind fails permanently when taken (<c>job.unknown_kind</c>).
/// </summary>
public sealed class JobHandlerRegistry
{
    private readonly Dictionary<string, (Type Type, JobResourceClass ResourceClass)> _handlers = new(StringComparer.Ordinal);

    /// <summary>Reads the registered handlers once (in a temporary scope, since handlers may be scoped).</summary>
    public JobHandlerRegistry(IServiceScopeFactory scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        using var scope = scopes.CreateScope();
        foreach (var handler in scope.ServiceProvider.GetServices<IJobHandler>())
        {
            if (!_handlers.TryAdd(handler.Kind, (handler.GetType(), handler.ResourceClass)))
            {
                throw new JobQueueException(JobErrorCodes.DuplicateKind, $"two handlers for kind {handler.Kind}");
            }
        }
    }

    /// <summary>Registered kinds.</summary>
    public IReadOnlyCollection<string> Kinds => _handlers.Keys;

    /// <summary>Resource class of the kind's handler, or <c>null</c> for an unknown kind.</summary>
    public JobResourceClass? ResourceClassOf(string kind) => _handlers.TryGetValue(kind, out var h) ? h.ResourceClass : null;

    /// <summary>The handler of the kind from the job's scope, or <c>null</c> for an unknown kind.</summary>
    public IJobHandler? Resolve(string kind, IServiceProvider scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!_handlers.TryGetValue(kind, out var registration))
        {
            return null;
        }

        return scope.GetServices<IJobHandler>().First(h => h.GetType() == registration.Type);
    }
}
