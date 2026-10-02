using Microsoft.Extensions.Options;

namespace EshopGuard.Jobs.Runs;

/// <summary>Settings under <c>Runs</c>: sizes of batches, limits of the free sample and of the full analysis.</summary>
public sealed class RunsOptions
{
    /// <summary>Configuration section.</summary>
    public const string SectionName = "Runs";

    /// <summary>Pages of one batch of <c>run.fetch</c>.</summary>
    public int FetchBatchPages { get; set; } = 100;

    /// <summary>Longest batch of <c>run.fetch</c> in seconds.</summary>
    public double FetchBatchSeconds { get; set; } = 60;

    /// <summary>Chunks of one batch of <c>run.sieve</c>.</summary>
    public int SieveBatchChunks { get; set; } = 200;

    /// <summary>Segments of one batch of <c>run.evaluate</c>.</summary>
    public int EvaluateBatchSegments { get; set; } = 200;

    /// <summary>Pages of one batch of <c>run.rewrite</c>.</summary>
    public int RewriteBatchPages { get; set; } = 25;

    /// <summary>Jev calls written into <c>usage.usage_records</c> at once (or after <see cref="UsageFlushSeconds"/>).</summary>
    public int UsageFlushEvery { get; set; } = 50;

    /// <summary>Longest wait before the usage of Jev calls is written.</summary>
    public double UsageFlushSeconds { get; set; } = 2;

    /// <summary>How long a job holds the lease of a domain (prolonged by its heartbeat).</summary>
    public double DomainLeaseSeconds { get; set; } = 120;

    /// <summary>Hours the home page may be unreachable before the rest of the run's addresses fails (K rozhodnutí 8).</summary>
    public double SiteOutageMaxHours { get; set; } = 24;

    /// <summary>Wait before the next attempt while the home page is unreachable.</summary>
    public double SiteOutageRetrySeconds { get; set; } = 900;

    /// <summary>The run's real cost over the internal estimate × this ratio sends an operational warning (the run goes on).</summary>
    public double CostAlertRatio { get; set; } = 1.5;

    /// <summary>Language of the questions sent to Jev (as <c>--question-language</c> of the CLI).</summary>
    public string QuestionLanguage { get; set; } = "en";

    /// <summary>The sieve of blocks before the detailed questions (as the CLI without <c>--no-sieve</c>).</summary>
    public bool UseSieve { get; set; } = true;

    /// <summary>Modules of a run when the e-shop has none chosen; empty runs every enabled module of the jurisdictions (as the CLI).</summary>
    public List<string> DefaultModules { get; set; } = [];

    /// <summary>Limits of the free sample.</summary>
    public FreeSampleOptions FreeSample { get; set; } = new();

    /// <summary>Limits of the full analysis.</summary>
    public FullAnalysisOptions FullAnalysis { get; set; } = new();
}

/// <summary>Settings under <c>Runs:FreeSample</c>.</summary>
public sealed class FreeSampleOptions
{
    /// <summary>Pages of the sample, divided among the checked versions (change 7, <c>markets.sample_pages</c> plans them).</summary>
    public int MaxPages { get; set; } = 100;

    /// <summary>
    /// Our internal cost of one sample in USD (Jev, OpenAI). The sample is free for the customer, so this cap is the consent to
    /// the price: an estimate over it fails the run with <c>sample_budget_exceeded</c> before any paid call (K rozhodnutí, default 1.00).
    /// </summary>
    public decimal MaxInternalUsd { get; set; } = 1.00m;

    /// <summary>Findings tried for the example fix of the sample.</summary>
    public int MaxExampleAttempts { get; set; } = 3;
}

/// <summary>Settings under <c>Runs:FullAnalysis</c>.</summary>
public sealed class FullAnalysisOptions
{
    /// <summary>Pages of one crawl scope (a language version); the analysis checks everything it finds up to this limit.</summary>
    public int MaxPages { get; set; } = 20_000;

    /// <summary>Product pages included in the analysis per scope.</summary>
    public int SampleProducts { get; set; } = 20_000;

    /// <summary>Jev calls per page for the rough estimate after discovery (35 by the sample of vegis.sk, architecture part 8).</summary>
    public double RoughJevCallsPerPage { get; set; } = 35;

    /// <summary>Input tokens of one Jev call for the rough estimate.</summary>
    public int RoughTokensPerCall { get; set; } = 180;
}

/// <summary>Validation of <see cref="RunsOptions"/> (<c>config.runs_invalid</c>).</summary>
internal sealed class RunsOptionsValidator : IValidateOptions<RunsOptions>
{
    public ValidateOptionsResult Validate(string? name, RunsOptions o)
    {
        var failures = new List<string>();
        void Positive(double value, string key)
        {
            if (value <= 0)
            {
                failures.Add($"config.runs_invalid: Runs:{key} (must be positive)");
            }
        }

        Positive(o.FetchBatchPages, nameof(o.FetchBatchPages));
        Positive(o.FetchBatchSeconds, nameof(o.FetchBatchSeconds));
        Positive(o.SieveBatchChunks, nameof(o.SieveBatchChunks));
        Positive(o.EvaluateBatchSegments, nameof(o.EvaluateBatchSegments));
        Positive(o.RewriteBatchPages, nameof(o.RewriteBatchPages));
        Positive(o.UsageFlushEvery, nameof(o.UsageFlushEvery));
        Positive(o.DomainLeaseSeconds, nameof(o.DomainLeaseSeconds));
        Positive(o.SiteOutageMaxHours, nameof(o.SiteOutageMaxHours));
        Positive(o.CostAlertRatio, nameof(o.CostAlertRatio));
        Positive(o.FreeSample.MaxPages, "FreeSample:MaxPages");
        Positive((double)o.FreeSample.MaxInternalUsd, "FreeSample:MaxInternalUsd");
        Positive(o.FreeSample.MaxExampleAttempts, "FreeSample:MaxExampleAttempts");
        Positive(o.FullAnalysis.MaxPages, "FullAnalysis:MaxPages");
        Positive(o.FullAnalysis.SampleProducts, "FullAnalysis:SampleProducts");
        if (o.QuestionLanguage is not ("en" or "cs"))
        {
            failures.Add("config.runs_invalid: Runs:QuestionLanguage (en or cs)");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
