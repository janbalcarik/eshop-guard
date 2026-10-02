namespace EshopGuard.Cli.Tests;

/// <summary>
/// Jurisdictions, modules and the language of texts are checked against the rules before anything is downloaded (change 6);
/// the CLI knows no list of countries. <c>rules check-texts</c> reports the state of the translations.
/// </summary>
public sealed class RulesCommandTests
{
    [Fact]
    public async Task UnknownJurisdiction_StopsTheScanBeforeAnyDownload()
    {
        var folder = CliProcess.NewWorkingFolder();
        var (url, requests, stop) = CliProcess.StartFixture();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder, new Dictionary<string, string?>(),
                "scan", url, "--allow-private-network", "--mock", "--jurisdictions", "sk,de", "--out", "out");

            Assert.Equal(1, exitCode);
            Assert.Contains("Pro jurisdikci de nejsou zapnutá pravidla. Známé jurisdikce: cz, sk", output.ReplaceLineEndings(" "), StringComparison.Ordinal);
            Assert.Empty(requests);
        }
        finally
        {
            await stop.CancelAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task LanguageWithoutReviewedTexts_StopsTheScan()
    {
        var folder = CliProcess.NewWorkingFolder();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder, new Dictionary<string, string?>(),
                "check-text", "Tento šampón je ekologický.", "--mock", "--lang", "sk");

            Assert.Equal(1, exitCode);
            Assert.Contains("Texty pravidel pro jazyk sk nejsou úplné nebo zkontrolované", output.ReplaceLineEndings(" "), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task CheckText_ForTwoCountries_ShowsTheJurisdictionOfEveryRule()
    {
        var folder = CliProcess.NewWorkingFolder();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder, new Dictionary<string, string?>(),
                "check-text", "Za hodnotenie 5 hviezdičkami vám vrátime 5 €.", "--mock", "--jurisdictions", "sk,cz", "--modules", "ucp");

            Assert.Equal(0, exitCode);
            var text = output.ReplaceLineEndings(" ");
            Assert.Contains("ucp_review_reward_positive (SK)", text, StringComparison.Ordinal);
            Assert.Contains("ucp_review_reward_positive (CZ)", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task CheckTexts_ListsTheLanguagesAndWhatIsMissing()
    {
        var folder = CliProcess.NewWorkingFolder();
        try
        {
            var (exitCode, output) = await CliProcess.RunAsync(folder, new Dictionary<string, string?>(), "rules", "check-texts");

            Assert.Equal(0, exitCode);
            var text = output.ReplaceLineEndings(" ");
            Assert.Contains("zpráva jde napsat v: cs", text, StringComparison.Ordinal);
            Assert.Contains("sk/eco.yaml: návrh překladu čeká na kontrolu člověkem", text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
