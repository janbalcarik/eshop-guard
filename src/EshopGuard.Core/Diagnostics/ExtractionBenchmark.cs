using System.Diagnostics;
using System.Text.Json;
using EshopGuard.Core.Crawl;
using EshopGuard.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace EshopGuard.Core.Diagnostics;

/// <summary>Processor time of extraction, classification and profile preparation per page (<c>eshopguard bench-extract</c>).</summary>
/// <param name="Pages">HTML pages of the recording.</param>
/// <param name="CpuMsPerPage">Median over the runs of the processor time per page, in milliseconds.</param>
/// <param name="WallMsPerPage">Median over the runs of the elapsed time per page, in milliseconds.</param>
/// <param name="Runs">Measured runs (after one warm-up).</param>
public sealed record ExtractionBenchmarkResult(int Pages, double CpuMsPerPage, double WallMsPerPage, int Runs);

/// <summary>
/// Measures the per-page work of a scan that does not depend on Jev: reading the HTML of every page of a recording
/// (<see cref="PageRecording"/>), extraction, classification and the profiles of page templates (structure tokens,
/// matching to stored profiles, the plan of new ones), the same steps as a scan (<see cref="ExtractStep"/>,
/// <see cref="ProfileStep.PlanAsync"/>). No network, no model.
/// </summary>
public static class ExtractionBenchmark
{
    /// <summary>One warm-up run, then <paramref name="runs"/> measured runs over all HTML pages of the recording.</summary>
    public static async Task<ExtractionBenchmarkResult> RunAsync(IServiceProvider services, string recordingDirectory, int runs = 3, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runs);
        var pages = ReadHtmlPages(recordingDirectory);
        if (pages.Count == 0)
        {
            throw new InvalidOperationException($"The recording in {recordingDirectory} has no HTML page.");
        }

        var extractStep = services.GetRequiredService<ExtractStep>();
        var profileStep = services.GetRequiredService<ProfileStep>();
        var home = pages[0].Url;
        var site = new SiteScope(home);

        async Task WorkAsync(IReadOnlyList<(Uri Url, string Html)> batch)
        {
            var stored = await profileStep.LoadStoredAsync(site, ct);
            var extracted = new List<ExtractedPageRecord>(batch.Count);
            foreach (var (url, html) in batch)
            {
                ct.ThrowIfCancellationRequested();
                extracted.Add(await extractStep.ExtractPageAsync(site, url, html, url == home, stored, ct));
            }

            await profileStep.PlanAsync(ProfileStep.PlanInput(site, extracted, stored), ct);
        }

        await WorkAsync(pages.Take(10).ToList());
        var cpu = new List<double>();
        var wall = new List<double>();
        using var process = Process.GetCurrentProcess();
        for (var run = 0; run < runs; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            process.Refresh();
            var cpuBefore = process.TotalProcessorTime;
            var clock = Stopwatch.StartNew();
            await WorkAsync(pages);
            clock.Stop();
            process.Refresh();
            cpu.Add((process.TotalProcessorTime - cpuBefore).TotalMilliseconds / pages.Count);
            wall.Add(clock.Elapsed.TotalMilliseconds / pages.Count);
        }

        return new ExtractionBenchmarkResult(pages.Count, Median(cpu), Median(wall), runs);
    }

    /// <summary>Successful HTML answers of the recording in recorded order, one per URL.</summary>
    internal static List<(Uri Url, string Html)> ReadHtmlPages(string directory)
    {
        var index = Path.Combine(directory, PageRecording.IndexFile);
        var bodies = Path.Combine(directory, PageRecording.BodiesFolder);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pages = new List<(Uri, string)>();
        foreach (var line in File.ReadLines(index))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<PageRecording.Entry>(line, PageRecording.Json)!;
            var isHtml = entry.MediaType is "text/html" or "application/xhtml+xml";
            if (entry.Status is >= 200 and < 300 && isHtml && entry.Body is { } body && seen.Add(entry.Url))
            {
                pages.Add((new Uri(entry.Url), HtmlDecoding.Decode(File.ReadAllBytes(Path.Combine(bodies, body + ".bin")), entry.Charset)));
            }
        }

        return pages;
    }

    private static double Median(List<double> values) => values.Order().ElementAt(values.Count / 2);
}
