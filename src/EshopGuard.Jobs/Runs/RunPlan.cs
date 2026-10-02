using System.Globalization;
using System.Text.Json;
using EshopGuard.Data.Configurations.Conventions;
using EshopGuard.Data.Entities.Checks;
using EshopGuard.Data.Entities.Ops;
using EshopGuard.Jobs.Queue;
using EshopGuard.Jobs.Workers;

namespace EshopGuard.Jobs.Runs;

/// <summary>
/// The jobs of the steps of a run (design of change 8, "Plán kroků"): kind, resource class, priority, <c>dedupe_key</c> (a
/// second job of the same step and batch is never created) and <c>concurrency_key</c>. Every job that downloads from the
/// e-shop (discovery, places of sale, batches of downloads) has the key of the domain, so one domain is downloaded by one job
/// at a time across all runs and tenants. The steps over the whole site run once per run at a time.
/// </summary>
public static class RunPlan
{
    /// <summary>Attempts of a batch that calls a paid service (Jev, OpenAI) or the e-shop.</summary>
    public const int BatchMaxAttempts = 6;

    public static JobRequest Discover(RunRow run) =>
        Job(run, RunJobKinds.Discover, JobResourceClass.Fetch, JobPriority.P0, "discover", JobKeys.Domain(run.ShopDomain),
            new { run_kind = SnakeCaseEnumConverter<RunKind>.ToText(run.Kind) });

    public static JobRequest Markets(RunRow run) =>
        Job(run, RunJobKinds.Markets, JobResourceClass.Llm, JobPriority.P0, "markets", JobKeys.Domain(run.ShopDomain), null);

    /// <summary>Batch <paramref name="batch"/> of downloads of scope <paramref name="scopeIndex"/> (on the domain of the scope).</summary>
    public static JobRequest Fetch(RunRow run, int scopeIndex, string scopeKey, string domain, int batch, DateTimeOffset? notBefore = null) =>
        Job(run, RunJobKinds.Fetch, JobResourceClass.Fetch, StepPriority(run), Invariant($"fetch:{scopeIndex}:{batch}"), JobKeys.Domain(domain),
            new { scope = scopeKey, index = scopeIndex, batch }) with { NotBefore = notBefore };

    public static JobRequest Profile(RunRow run) =>
        Job(run, RunJobKinds.Profile, JobResourceClass.Llm, StepPriority(run), "profile", JobKeys.Run(run.Id, "profile"), null);

    public static JobRequest Segment(RunRow run) =>
        Job(run, RunJobKinds.Segment, JobResourceClass.Cpu, StepPriority(run), "segment", JobKeys.Run(run.Id, "segment"), null);

    public static JobRequest Sieve(RunRow run, int batch) =>
        Job(run, RunJobKinds.Sieve, JobResourceClass.Jev, StepPriority(run), Invariant($"sieve:{batch}"), null, new { batch });

    public static JobRequest PlanEvaluate(RunRow run) =>
        Job(run, RunJobKinds.PlanEvaluate, JobResourceClass.Cpu, StepPriority(run), "plan_evaluate", JobKeys.Run(run.Id, "plan_evaluate"), null);

    public static JobRequest Evaluate(RunRow run, int batch) =>
        Job(run, RunJobKinds.Evaluate, JobResourceClass.Jev, StepPriority(run), Invariant($"evaluate:{batch}"), null, new { batch });

    public static JobRequest Rules(RunRow run) =>
        Job(run, RunJobKinds.Rules, JobResourceClass.Cpu, StepPriority(run), "rules", JobKeys.Run(run.Id, "rules"), null);

    /// <summary>A batch of rewrites (full analysis), or the example fix of the free sample (<paramref name="batch"/> 0 with candidates).</summary>
    public static JobRequest Rewrite(RunRow run, int batch, IReadOnlyList<Guid>? candidates = null) =>
        Job(run, RunJobKinds.Rewrite, JobResourceClass.Llm, StepPriority(run), Invariant($"rewrite:{batch}"), null, new { batch, candidates });

    public static JobRequest Finalize(RunRow run) =>
        Job(run, RunJobKinds.Finalize, JobResourceClass.System, StepPriority(run), "finalize", JobKeys.Run(run.Id, "finalize"), null);

    /// <summary>
    /// P0 for what the user of a free sample waits for (discovery, places of sale), P2 for the rest. Continuations of batches are
    /// added at the end of the queue (id order), so batches of different tenants take turns.
    /// </summary>
    public static JobPriority StepPriority(RunRow run) => JobPriority.P2;

    private static JobRequest Job(RunRow run, string kind, JobResourceClass resourceClass, JobPriority priority, string step, string? concurrencyKey, object? payload) =>
        new(
            kind,
            resourceClass,
            priority,
            payload is null ? JobRequest.EmptyPayload() : JsonSerializer.SerializeToDocument(payload),
            TenantId: run.TenantId,
            ShopId: run.ShopId,
            RunId: run.Id,
            DedupeKey: Invariant($"run:{run.Id:D}:{step}"),
            ConcurrencyKey: concurrencyKey,
            MaxAttempts: BatchMaxAttempts);

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
