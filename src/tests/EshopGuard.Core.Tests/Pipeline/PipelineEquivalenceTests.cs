namespace EshopGuard.Core.Tests;

/// <summary>
/// The steps of the pipeline give the same outputs as the code before change 5 (<c>Baselines/</c>, written by
/// <see cref="BaselineDumpTests"/>): findings, pages, segments, sieve, profiles and report, without times and pace.
/// </summary>
public sealed class PipelineEquivalenceTests
{
    public static TheoryData<string> Scenarios => new(BaselineScenarios.All.Select(s => s.Name));

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task Outputs_AreTheSameAsBeforeChange5(string name)
    {
        var (_, fetcher, options) = BaselineScenarios.All.Single(s => s.Name == name);
        var expected = OutputNormalizer.Read(Path.Combine(BaselineScenarios.OutputBaselines, name));

        var (actual, _) = await BaselineScenarios.RunAsync(name, fetcher, options);

        Assert.NotEmpty(expected);
        var differences = OutputNormalizer.Differences(expected, actual);
        Assert.True(differences.Length == 0, differences);
    }
}
