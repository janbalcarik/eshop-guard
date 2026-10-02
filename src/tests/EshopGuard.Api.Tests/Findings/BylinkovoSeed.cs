using EshopGuard.Api.Tests.Shops;
using EshopGuard.Storage;
using Microsoft.Extensions.DependencyInjection;
using static EshopGuard.Api.Tests.Findings.ShopSeed;

namespace EshopGuard.Api.Tests.Findings;

/// <summary>
/// „bylinkovo.sk“ after the initial analysis, as the design of the screens shows it (change 11, task 1.3), in the shape of
/// change 8: 28 items with a finding (26 pages, the whole site's template and its obligations), 43 findings (14 violations,
/// 23 to assess, 6 to verify; 9 fixed), tabs 24 / 12 / 12 / 4; versions sk and cs; the page „Zubná pasta + bambusová kefka“
/// (Shoptet product 2429) with 5 changes (one of the group „Všetky naše produkty balíme ekologicky.“ on 38 pages, one
/// with a question); „Sviečka Vodnár“ on 4 pages with one question; COSMOS; „Vegan“ on 5 pages; the cart of the whole site.
/// The group on 38 pages is counted in the group (<c>page_count</c>); its own test builds 38 pages in another e-shop.
/// </summary>
internal sealed class BylinkovoSeed
{
    public const long GroupHash = 7_001;
    public const long VodnarHash = 7_002;
    public const long VeganHash = 7_003;
    public const long TemplateHash = 7_004;
    public const long CosmosHash = 7_005;

    public required ShopSeed Seed { get; init; }

    public required string Domain { get; init; }

    public Guid ShopId => Seed.ShopId;

    public Dictionary<string, (Guid PageId, Guid VersionId)> Pages { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Guid> Findings { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Guid> Questions { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Guid> Proposals { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Guid> Groups { get; } = new(StringComparer.Ordinal);

    public static readonly string ZubnaText = string.Join('\n',
        "Zubná pasta + bambusová kefka",
        "Okamžitý komfort pre citlivé zuby v ekologickom sete.",
        "Všetky naše produkty balíme ekologicky.",
        "Bambusová kefka – ekologická alternatíva",
        "Biologicky rozložiteľná rukoväť",
        "Set sme pripravili pre ekologicky zmýšľajúcich zákazníkov",
        "Zloženie: voda, glycerín, mäta.",
        "Doprava zadarmo od 39 €.");

    /// <summary>The e-shop of the owner with everything above; status <c>active</c>, source Shoptet connector, SK and CZ active.</summary>
    public static async Task<BylinkovoSeed> SeedAsync(ApiFactory factory, Person owner)
    {
        var created = await ShopTestBase.CreateShopAsync(owner);
        var shopId = created.GetProperty("id").GetGuid();
        var domain = created.GetProperty("domain").GetString()!;
        await ExecuteAsync("UPDATE shop.shops SET status = 'active', platform = 'shoptet', source_mode = 'connector', home_country = 'SK' WHERE id = $1", shopId);
        foreach (var country in new[] { "SK", "CZ" })
        {
            await ExecuteAsync(
                "INSERT INTO shop.shop_markets (tenant_id, shop_id, country_code, is_home, status, evidence_level, source, created_at, updated_at) VALUES ($1, $2, $3, $4, 'active', 'strong', 'detected', now(), now())",
                owner.TenantId, shopId, country, country == "SK");
        }

        var seed = new ShopSeed(owner.TenantId, shopId, factory.Services.GetRequiredService<IBlobStore>());
        await seed.RunAsync();
        await seed.ConnectorAsync();
        var data = new BylinkovoSeed { Seed = seed, Domain = domain };
        await data.FillAsync();
        return data;
    }

    private async Task FillAsync()
    {
        var eco = await RuleSetAsync("eco", "eco");
        var legal = await RuleSetAsync("legal_sk", "legal");
        var s = Seed;

        // --- Needs an answer: 11 pages and the whole site's obligations ---------------------------------------------------
        var zubna = Pages["zubna"] = await s.PageAsync("Zubná pasta + bambusová kefka", "/zubna-pasta-bambusova-kefka/", "sk", ZubnaText, source: "connector", externalId: "2429");
        Findings["zubna.1"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high"), Verdict("cz", "assess", "high")), "proposed",
            "Okamžitý komfort pre citlivé zuby v ekologickom sete.", 7_101, [zubna.PageId]);
        Findings["group"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high"), Verdict("cz", "assess", "medium")), "proposed",
            "Všetky naše produkty balíme ekologicky.", GroupHash, [zubna.PageId]);
        Findings["zubna.3"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high"), Verdict("cz", "assess", "high")), "proposed",
            "Bambusová kefka – ekologická alternatíva", 7_103, [zubna.PageId]);
        Findings["zubna.4"] = await s.FindingAsync(eco, "eco_label_unrecognized", "eco", Verdicts(Verdict("sk", "verify", "medium"), Verdict("cz", "verify", "low")), "needs_answer",
            "Biologicky rozložiteľná rukoväť", 7_104, [zubna.PageId]);
        Findings["zubna.5"] = await s.FindingAsync(eco, "eco_generic_claim_implied", "eco", Verdicts(Verdict("sk", "assess", "medium"), Verdict("cz", "assess", "low")), "proposed",
            "Set sme pripravili pre ekologicky zmýšľajúcich zákazníkov", 7_105, [zubna.PageId]);
        Groups["group"] = await s.GroupAsync("repeated_text", GroupHash, "Všetky naše produkty balíme ekologicky.", "Všetky naše produkty balíme do [materiál obalu].", 38,
            placeholders: """["materiál obalu"]""", status: "needs_value");
        Proposals["zubna.1"] = await s.ProposalAsync(zubna, [Findings["zubna.1"]], "Okamžitý komfort pre citlivé zuby v ekologickom sete.",
            "Okamžitý komfort pre citlivé zuby v sete s bambusovou kefkou.", 2,
            alternatives: """[{"key":"with_detail","text":"Okamžitý komfort pre citlivé zuby v sete s kefkou z bambusu namiesto plastu.","recheck_status":"ok"},{"key":"without_word","text":"Okamžitý komfort pre citlivé zuby.","recheck_status":"ok"}]""");
        Proposals["group"] = await s.ProposalAsync(zubna, [Findings["group"]], "Všetky naše produkty balíme ekologicky.",
            "Všetky naše produkty balíme do [doplňte: materiál obalu].", 3, recheck: "pending", placeholders: """["materiál obalu"]""", groupId: Groups["group"]);
        Proposals["zubna.3"] = await s.ProposalAsync(zubna, [Findings["zubna.3"]], "Bambusová kefka – ekologická alternatíva",
            "Bambusová kefka – rukoväť z bambusu namiesto plastu", 4);
        Proposals["zubna.4"] = await s.ProposalAsync(zubna, [Findings["zubna.4"]], "Biologicky rozložiteľná rukoväť", "Rukoväť z bambusu", 5,
            alternatives: """[{"key":"answer_no","text":"","recheck_status":"ok"}]""");
        Proposals["zubna.5"] = await s.ProposalAsync(zubna, [Findings["zubna.5"]], "Set sme pripravili pre ekologicky zmýšľajúcich zákazníkov",
            "Set sme pripravili pre zákazníkov, ktorí chcú kefku bez plastu", 6);
        Questions["zubna"] = await s.QuestionAsync(Findings["zubna.4"], "material_evidence", """{"material":"bambus"}""");

        var vodnar = new List<Guid>();
        foreach (var scent in new[] { "levanduľa", "pomaranč", "škorica", "vanilka" })
        {
            var page = Pages["vodnar." + scent] = await s.PageAsync($"Sviečka Vodnár – {scent}", $"/sviecka-vodnar-{scent}/", "sk", $"Sviečka Vodnár – {scent}\nDoba horenia: 22 hodín\nRučne liata sviečka.");
            vodnar.Add(page.PageId);
        }

        Findings["vodnar"] = await s.FindingAsync(eco, "eco_generic_claim_open", "eco", Verdicts(Verdict("sk", "verify", "medium")), "needs_answer", "Doba horenia: 22 hodín", VodnarHash, vodnar);
        foreach (var scent in new[] { "levanduľa", "pomaranč", "škorica", "vanilka" })
        {
            Proposals["vodnar." + scent] = await s.ProposalAsync(Pages["vodnar." + scent], [Findings["vodnar"]], "Doba horenia: 22 hodín",
                "Doba horenia: 22 hodín (podľa testu výrobcu)", 2, recheck: "pending", alternatives: """[{"key":"answer_no","text":"","recheck_status":"ok"}]""");
        }

        Questions["vodnar"] = await s.QuestionAsync(Findings["vodnar"], "burn_time_evidence", """{"hours":22}""");

        var serum = Pages["serum"] = await s.PageAsync("Rozjasňujúce sérum 30 ml", "/rozjasnujuce-serum-30-ml/", "sk", "Rozjasňujúce sérum 30 ml\nCertifikovaná prírodná kozmetika COSMOS\nPleť je žiarivá.");
        Findings["serum"] = await s.FindingAsync(eco, "eco_label_unrecognized", "eco", Verdicts(Verdict("sk", "verify", "high")), "needs_answer",
            "Certifikovaná prírodná kozmetika COSMOS", CosmosHash, [serum.PageId]);
        Questions["cosmos"] = await s.QuestionAsync(Findings["serum"], "certificate_evidence", """{"certificate":"COSMOS"}""");

        var vegan = new List<Guid>();
        foreach (var product in new[] { "Šampón", "Kondicionér", "Mydlo", "Krém", "Balzam" })
        {
            var page = Pages["vegan." + product] = await s.PageAsync($"{product} Vegan", $"/{product.ToLowerInvariant()}-vegan/", "sk", $"{product} Vegan\nVegan\nBez parabénov.");
            vegan.Add(page.PageId);
        }

        Findings["vegan"] = await s.FindingAsync(eco, "eco_label_unrecognized", "eco", Verdicts(Verdict("sk", "verify", "medium")), "needs_answer", "Vegan", VeganHash, vegan);
        Questions["vegan"] = await s.QuestionAsync(Findings["vegan"], "vegan_evidence");

        Findings["site"] = await s.FindingAsync(legal, "legal_withdrawal_form_missing", "legal", Verdicts(Verdict("sk", "assess", "medium")), "needs_answer", null, null, [], scope: "site");
        Questions["site"] = await s.QuestionAsync(Findings["site"], "cart_withdrawal_button", scope: "site");

        // --- To approve: 11 pages (3 in Czech) and the whole site's template ----------------------------------------------
        var approve = new List<(Guid PageId, Guid VersionId)>();
        for (var i = 1; i <= 11; i++)
        {
            var czech = i > 8;
            var title = czech ? $"Bylinný čaj č. {i}" : $"Bylinný čaj č. {i:00}";
            var first = czech ? $"Čaj šetrný k přírodě č. {i}." : $"Čaj šetrný k prírode č. {i}.";
            var second = czech ? $"Udržitelné balení č. {i}." : $"Udržateľné balenie č. {i}.";
            var page = Pages["approve." + i] = await s.PageAsync(title, $"/{(czech ? "cs/" : "")}bylinny-caj-{i}/", czech ? "cs" : "sk", $"{title}\n{first}\n{second}\nZloženie: mäta.");
            approve.Add(page);
            // 6 violations (text/medium) on the first six pages, the rest to assess; two findings on pages 1–7.
            var firstVerdict = i <= 6 ? Verdicts(Verdict("sk", "text", "medium"), Verdict("cz", "assess", "medium")) : Verdicts(Verdict("sk", "assess", "medium"), Verdict("cz", "assess", "low"));
            Findings[$"approve.{i}.a"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", firstVerdict, "proposed", first, 7_200 + i, [page.PageId]);
            Proposals[$"approve.{i}.a"] = await s.ProposalAsync(page, [Findings[$"approve.{i}.a"]], first, first.Replace(czech ? "šetrný k přírodě" : "šetrný k prírode", "z bylín z vlastnej záhrady", StringComparison.Ordinal), 2);
            if (i <= 7)
            {
                Findings[$"approve.{i}.b"] = await s.FindingAsync(eco, "eco_sustainable_claim", "eco", Verdicts(Verdict("sk", "assess", "low")), "proposed", second, 7_300 + i, [page.PageId]);
                Proposals[$"approve.{i}.b"] = await s.ProposalAsync(page, [Findings[$"approve.{i}.b"]], second, czech ? $"Balení z papíru č. {i}." : $"Balenie z papiera č. {i}.", 3);
            }

            if (i <= 6)
            {
                // Open findings without a proposal yet: two to verify, four to assess.
                var verdict = i <= 2 ? Verdicts(Verdict("sk", "verify", "low")) : Verdicts(Verdict("sk", "assess", "low", "review"));
                Findings[$"approve.{i}.open"] = await s.FindingAsync(eco, "eco_generic_claim_open", "eco", verdict, "open", $"Prírodné zloženie č. {i}.", 7_400 + i, [page.PageId]);
            }
        }

        Findings["template"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", Verdicts(Verdict("sk", "text", "high"), Verdict("cz", "assess", "medium")), "proposed",
            "Ekologický obchod s prírodnou kozmetikou", TemplateHash, [approve[0].PageId, approve[1].PageId]);
        Groups["template"] = await s.GroupAsync("template", TemplateHash, "Ekologický obchod s prírodnou kozmetikou", "Obchod s prírodnou kozmetikou", 120, recheck: "ok");

        // --- Published: 4 pages, 9 fixed findings (6 published, 3 resolved) ---------------------------------------------
        var fixedLayout = new[] { ("published", "published", "resolved"), ("published", "published", null), ("published", "resolved", null), ("published", "resolved", null) };
        var text = 0;
        for (var i = 0; i < fixedLayout.Length; i++)
        {
            var page = Pages["published." + i] = await s.PageAsync($"Levanduľový olej {i + 1}", $"/levandulovy-olej-{i + 1}/", "sk", $"Levanduľový olej {i + 1}\nOlej z levandule.");
            var (a, b, c) = fixedLayout[i];
            foreach (var (status, n) in new[] { (a, 0), (b, 1), (c, 2) })
            {
                if (status is null)
                {
                    continue;
                }

                var verdict = text++ < 4 ? Verdicts(Verdict("sk", "text", "low")) : Verdicts(Verdict("sk", "assess", "low"));
                Findings[$"published.{i}.{n}"] = await s.FindingAsync(eco, "eco_generic_claim", "eco", verdict, status, $"Prírodný olej {i}.{n}.", 7_500 + (i * 10) + n, [page.PageId]);
            }
        }
    }
}
