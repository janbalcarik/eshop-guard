using System.Net;
using System.Text;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>The export of findings (change 11, task 3.8; AD 15).</summary>
public sealed class CsvExportTests : FindingsTestBase
{
    [Fact]
    public async Task Export_HasBom_Separator_CzechHeaders_RuleNames_AndVerdictsByCountry()
    {
        var (factory, owner, data) = await BylinkovoAsync();
        await using var _ = factory;
        using var __ = owner;

        using var response = await owner.Browser.GetAsync(S(owner, data.ShopId) + "/findings/export.csv?locale=cs&checkability=text");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Text na stránce;Stránka;Adresa;Jazyková verze;Skupina;Verdikt po zemích;Ustanovení;Stav;Pravidlo", lines[0]);
        Assert.Equal(15, lines.Length);
        var group = lines.Single(l => l.StartsWith("Všetky naše produkty balíme ekologicky.", StringComparison.Ordinal)).Split(';');
        Assert.Equal("Porušení", group[4]);
        Assert.Equal("SK · Porušení (vysoká), CZ · K posouzení (střední)", group[5]);
        Assert.Contains("108/2024 Z. z.", group[6], StringComparison.Ordinal);
        Assert.NotEqual("eco_generic_claim", group[8]);
        var audit = await AdminScalarAsync<long>("SELECT count(*) FROM ops.audit_log WHERE tenant_id = $1 AND action = 'findings.exported'", owner.TenantId);
        Assert.Equal(1, audit);
    }
}
