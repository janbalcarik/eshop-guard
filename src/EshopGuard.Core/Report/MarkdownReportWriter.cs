using System.Text;
using EshopGuard.Core.Models;
using EshopGuard.Core.Rules;
using EshopGuard.Core.Rules.Texts;

namespace EshopGuard.Core.Report;

/// <summary>
/// Writes <c>report.md</c>, the report for people: header, statistics, summary table, findings by rule, images for manual
/// review, what was not checked and the screening disclaimer. Texts of rules, notes and warnings are in the language of the
/// run (<c>--lang</c>); headings of the CLI report stay Czech. A run for several jurisdictions adds the verdicts of every
/// jurisdiction, the obligations of the whole site and the coverage of the jurisdictions.
/// </summary>
internal sealed class MarkdownReportWriter(ReportTexts texts) : IReportWriter
{
    private const int MaxUrlsPerFinding = 5;

    private const string Disclaimer =
        "Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; "
        + "právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.";

    /// <summary>Groups of findings by how far the text of the law decides them.</summary>
    private static readonly (string Checkability, string Heading, string Intro)[] Groups =
    [
        ("text", "Porušení podle textu zákona",
            "Text webu splňuje znaky zákazu tak, jak je popisuje zákon nebo jeho odůvodnění; výjimky (například ekoznačka EU) uvádí vysvětlení u pravidla."),
        ("assess", "K posouzení",
            "Zda jde o zakázanou praktiku, záleží na tom, jak text chápe průměrný spotřebitel; zákon tyto výrazy nejmenuje a Komise je posuzuje případ od případu. Rozhodne člověk."),
        ("verify", "K ověření",
            "Tvrzení nebo chybějící informace je na webu vidět, ale zda jde o porušení, záleží na faktech mimo web (certifikace značky, pravdivost údaje, košík a pokladna, které nástroj nestahuje)."),
        ("not_checkable", "Z textu nelze posoudit", "Pravidla, která z textu webu rozhodnout nejde."),
        (UpcomingGroup, "Platí později",
            "Povinnost zatím neplatí: pravidlo má datum účinnosti v budoucnu, nález je upozornění dopředu. Skupinu, do které po účinnosti patří, uvádí řádek pravidla."),
    ];

    /// <summary>Group of findings whose every verdict applies only later.</summary>
    private const string UpcomingGroup = "upcoming";

    /// <summary>Group of a finding in the report: its group of checkability, or the upcoming one before the rule applies.</summary>
    private static string Group(Finding finding) => finding.Strictest.Status == VerdictStatus.Upcoming ? UpcomingGroup : finding.Checkability;

    private static int GroupRank(string group) => group == UpcomingGroup ? 4 : ReportFormat.CheckabilityRank(group);

    public string FileName => "report.md";

    public async Task WriteAsync(ScanResult result, string outputDirectory, CancellationToken ct = default)
    {
        var renderer = await texts.RendererAsync();
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, FileName), Render(result, renderer, texts.Locale), new UTF8Encoding(false), ct);
    }

    internal static string Render(ScanResult result, RuleTextRenderer renderer, string locale) =>
        new Writer(result, renderer, locale).Render();

    /// <summary>One report: the result, its texts and its language.</summary>
    private sealed class Writer(ScanResult result, RuleTextRenderer renderer, string locale)
    {
        private bool Several => result.Jurisdictions.Count > 1;

        public string Render()
        {
            var md = new StringBuilder();
            var site = new Uri(result.SiteUrl);
            var isMock = result.JevModel == "mock";

            md.AppendLine($"# Kontrola textů e-shopu {site.Host}");
            md.AppendLine();
            md.AppendLine($"> {Disclaimer}");
            md.AppendLine();
            if (isMock)
            {
                md.AppendLine("> **Běh s falešným klientem (mock):** pravděpodobnosti nepocházejí z Jevu, nálezy slouží jen k ověření průchodu aplikací.");
                md.AppendLine();
            }

            AppendHeader(md, result);
            AppendStatistics(md, result, isMock);
            AppendSummary(md, result);
            AppendFindings(md, result);
            if (Several)
            {
                AppendObligations(md);
                AppendCoverage(md);
            }

            AppendImages(md, result);
            AppendNotChecked(md, result);

            if (result.Warnings.Count > 0)
            {
                md.AppendLine("## Upozornění");
                md.AppendLine();
                foreach (var warning in result.Warnings)
                {
                    md.AppendLine($"- {renderer.Warning(warning, locale)}");
                }

                md.AppendLine();
            }

            md.AppendLine("---");
            md.AppendLine();
            md.AppendLine(Disclaimer);
            return md.ToString();
        }

        private static void AppendHeader(StringBuilder md, ScanResult result)
        {
            md.AppendLine("| | |");
            md.AppendLine("| --- | --- |");
            md.AppendLine($"| Web | {result.SiteUrl} |");
            md.AppendLine($"| Datum | {result.FinishedAt.ToLocalTime().ToString("d. M. yyyy H:mm", ReportFormat.Czech)} |");
            md.AppendLine($"| Moduly | {string.Join(", ", result.Modules)} |");
            md.AppendLine($"| Země | {string.Join(", ", (result.Jurisdictions.Count > 0 ? result.Jurisdictions : [result.Country]).Select(j => j.ToUpperInvariant()))} |");
            md.AppendLine($"| Sady otázek | {(result.RuleSets.Count == 0 ? "žádné" : string.Join(", ", result.RuleSets.Select(r => $"{r.Version} ({r.File})")))} |");
            md.AppendLine($"| Model | {result.JevModel ?? "nevolán"} |");
            md.AppendLine($"| Jazyk otázek | {(result.QuestionLanguage == "cs" ? "čeština" : "angličtina")} |");
            md.AppendLine();
        }

        private static void AppendStatistics(StringBuilder md, ScanResult result, bool isMock)
        {
            var stats = result.Stats;
            md.AppendLine("## Statistika");
            md.AppendLine();
            var byType = string.Join(", ", new[] { PageType.Home, PageType.Product, PageType.Legal, PageType.Content }
                .Select(t => $"{ReportFormat.PageTypeName(t)} {stats.PagesByType.GetValueOrDefault(t)}"));
            md.AppendLine($"- Stránky: {stats.PagesFetched} ({byType})");
            if (stats.PagesTextNotLoaded > 0)
            {
                md.AppendLine($"- Stránky, jejichž text se nenačetl (JavaScript): {stats.PagesTextNotLoaded}, nejsou zkontrolované");
            }

            if (stats.VisibleTextChars > 0)
            {
                double Share(long chars) => (double)chars / stats.VisibleTextChars;
                var notChecked = Math.Max(0, stats.VisibleTextChars - stats.CheckedTextChars - stats.NavigationTextChars - stats.ListingTextChars - stats.ProfileSkippedTextChars);
                md.AppendLine($"- Viditelný text: zkontrolováno {Share(stats.CheckedTextChars).ToString("0 %", ReportFormat.Czech)} (hlavní text, hlavička a patička, ostatní text stránky), "
                    + $"navigace a filtry {Share(stats.NavigationTextChars).ToString("0 %", ReportFormat.Czech)} záměrně vynechané, "
                    + $"výpisy jiných produktů {Share(stats.ListingTextChars).ToString("0 %", ReportFormat.Czech)} (kontrolují se na stránce produktu), "
                    + (stats.ProfileSkippedTextChars > 0 ? $"ovládací prvky podle profilu šablony {Share(stats.ProfileSkippedTextChars).ToString("0 %", ReportFormat.Czech)}, " : "")
                    + $"jinak nezkontrolováno {Share(notChecked).ToString("0 %", ReportFormat.Czech)}"
                    + (stats.PagesWithUncheckedText > 0 ? $"; stránky s větším nezkontrolovaným podílem: {stats.PagesWithUncheckedText}" : ""));
            }
            if (stats.ProfilesEnabled && (stats.ProfilesUsed > 0 || stats.ProfilesPlanned > 0))
            {
                var notCreated = stats.ProfilesPlanned > 0 && stats.ProfileCalls == 0 ? $", plánováno nových {stats.ProfilesPlanned}, nevytvořeny" : "";
                md.AppendLine($"- Profily šablon stránek: použito {stats.ProfilesUsed} (z toho nových {stats.ProfilesCreated}{notCreated}), "
                    + $"stránky podle profilu {stats.PagesWithProfile}, bez profilu {stats.PagesWithoutProfile} (kontrolované celé)"
                    + (stats.ProfileCalls > 0 ? $"; model {stats.ProfileCalls}× za {ReportFormat.Usd(stats.ProfileCostUsd)}" : ""));
            }

            var pace = stats.CrawlSeconds > 0 ? stats.CrawlRequests / stats.CrawlSeconds : 0;
            md.AppendLine($"- Stahování: {stats.CrawlRequests} požadavků za {ReportFormat.Number(stats.CrawlSeconds, "0.0")} s "
                + $"(průměr {ReportFormat.Number(pace, "0.0")}/s, na konci {ReportFormat.Number(stats.CrawlFinalRate, "0.0")}/s"
                + (stats.CrawlDelaySeconds > 0 ? $", Crawl-delay {ReportFormat.Number(stats.CrawlDelaySeconds, "0.#")} s" : "")
                + (stats.CrawlThrottled > 0 ? $", server {stats.CrawlThrottled}× požádal o zpomalení" : "") + ")");
            md.AppendLine($"- Segmenty: {stats.SegmentOccurrences} výskytů, {stats.UniqueSegments} unikátních ({stats.SentenceSegments} vět, {stats.LegalParagraphSegments} právních odstavců, {stats.BoilerplateSegments} šablonových)");
            md.AppendLine($"- Volání Jevu: {stats.JevCalls}, odpovědi z cache: {stats.JevCacheHits}, chyby: {stats.JevErrors}");
            if (stats.SieveEnabled)
            {
                var share = stats.SievePairs == 0 ? 0 : (double)stats.SieveSkippedPairs / stats.SievePairs;
                md.AppendLine($"- Síto po odstavcích (úseky do {stats.SieveChunkChars} znaků, práh {ReportFormat.Number(stats.SieveThreshold, "0.0#")}): "
                    + $"{stats.SieveChunks} úseků, volání {stats.SieveCalls}, z cache {stats.SieveCacheHits}, nevyhodnoceno {stats.SieveErrors + stats.SieveTooLong}; "
                    + $"z podrobné kontroly vynechalo {stats.SieveSkippedPairs} z {stats.SievePairs} dvojic věta × modul ({share.ToString("0 %", ReportFormat.Czech)})");
            }
            else
            {
                md.AppendLine("- Síto po odstavcích: vypnuté, všechny věty prošly všemi moduly");
            }

            md.AppendLine($"- Vstupní tokeny: {ReportFormat.Number(stats.InputTokens)}{(isMock ? " (odhad falešného klienta)" : "")}");
            md.AppendLine($"- Odhad ceny: {ReportFormat.Usd(stats.EstimatedCostUsd)}{(isMock ? " (nic se neplatilo)" : "")}");
            md.AppendLine($"- Doba běhu: {ReportFormat.Number(stats.DurationSeconds, "0.0")} s");
            md.AppendLine();
        }

        private void AppendSummary(StringBuilder md, ScanResult result)
        {
            md.AppendLine("## Souhrn");
            md.AppendLine();
            var notLoaded = result.Stats.PagesTextNotLoaded;
            if (notLoaded > 0)
            {
                md.AppendLine($"**Pozor: text {notLoaded} z {result.Stats.PagesFetched} stránek se nenačetl** (web ho nejspíš vykresluje JavaScriptem). "
                    + "Tyto stránky nejsou zkontrolované a chybějící nálezy u nich neznamenají, že jsou v pořádku; seznam je v části „Co nebylo zkontrolováno“.");
                md.AppendLine();
            }

            if (result.Findings.Count == 0)
            {
                md.AppendLine(result.EvaluationSkipped ? "Vyhodnocení neproběhlo."
                    : notLoaded > 0 ? "Na stránkách, jejichž text se načetl, nejsou žádné nálezy." : "Žádné nálezy.");
                md.AppendLine();
                return;
            }

            md.AppendLine(string.Join(", ", Groups
                .Select(g => (g.Heading, Count: result.Findings.Count(f => Group(f) == g.Checkability)))
                .Where(g => g.Count > 0)
                .Select(g => $"{g.Heading}: {g.Count}")) + ".");
            md.AppendLine();
            md.AppendLine("| Skupina | Pravidlo | Závažnost | Vysoká jistota | Nižší jistota |");
            md.AppendLine("| --- | --- | --- | ---: | ---: |");
            foreach (var rule in OrderedRules(result.Findings))
            {
                var first = rule.First();
                md.AppendLine($"| {GroupHeading(Group(first))} | {renderer.Render(first, locale).Title} | {ReportFormat.Severity(first.Severity)} | {rule.Count(f => f.Band == FindingBand.High)} | {rule.Count(f => f.Band == FindingBand.Review)} |");
            }

            md.AppendLine();
            md.AppendLine("Jistota říká, jak si je Jev jistý, že text odpovídá popisu pravidla; zda jde o porušení, určuje skupina.");
            md.AppendLine();
        }

        private static string GroupHeading(string checkability) =>
            Groups.FirstOrDefault(g => g.Checkability == checkability).Heading ?? checkability;

        private void AppendFindings(StringBuilder md, ScanResult result)
        {
            if (result.Findings.Count == 0)
            {
                return;
            }

            foreach (var group in OrderedRules(result.Findings).GroupBy(r => Group(r[0])))
            {
                var heading = Groups.FirstOrDefault(g => g.Checkability == group.Key);
                md.AppendLine($"## {heading.Heading ?? group.Key} ({group.Sum(r => r.Count)})");
                md.AppendLine();
                if (heading.Intro is not null)
                {
                    md.AppendLine(heading.Intro);
                    md.AppendLine();
                }

                AppendRules(md, group);
            }
        }

        private void AppendRules(StringBuilder md, IEnumerable<List<Finding>> rules)
        {
            foreach (var rule in rules)
            {
                var first = rule.First();
                var verdicts = first.Verdicts.Select(v => (Verdict: v, Texts: renderer.Render(first, v, locale))).ToList();
                var strictest = verdicts.First(v => ReferenceEquals(v.Verdict, first.Strictest)).Texts;
                md.AppendLine($"### {strictest.Title}");
                md.AppendLine();
                md.AppendLine($"Pravidlo `{first.RuleId}`, závažnost {ReportFormat.Severity(first.Severity)}, {ReportFormat.Checkability(first.Checkability)}.");
                md.AppendLine();
                if (Several)
                {
                    md.AppendLine($"**Verdikty:** {string.Join("; ", verdicts.Select(v => Verdict(v.Verdict)))}.");
                    md.AppendLine();
                }

                AppendByJurisdiction(md, verdicts, t => t.Explanation, "");
                AppendByJurisdiction(md, verdicts, t => t.Recommendation, "**Doporučení:** ");
                var references = verdicts.SelectMany(v => v.Texts.LegalRefs).Distinct().ToList();
                if (references.Count > 0)
                {
                    md.AppendLine("**Předpisy:**");
                    md.AppendLine();
                    foreach (var reference in references)
                    {
                        md.AppendLine($"- {reference.Jurisdiction.ToUpperInvariant()}: {reference.Ref} (status: {reference.Status})");
                    }

                    md.AppendLine();
                }

                // The same text with different context is one line of the report.
                var byText = rule
                    .GroupBy(f => f.Scope == "site" ? f.RuleId : f.Text ?? "")
                    .Select(g => g.OrderByDescending(f => f.Score).ToList())
                    .OrderByDescending(g => g[0].Score);
                var number = 1;
                foreach (var group in byText)
                {
                    AppendFinding(md, number++, group);
                }
            }
        }

        private void AppendFinding(StringBuilder md, int number, List<Finding> group)
        {
            var best = group[0];
            var urls = group.SelectMany(f => f.Urls).Distinct().ToList();
            if (best.Scope == "site")
            {
                md.AppendLine($"{number}. **Informace na webu nenalezena** – skóre {ReportFormat.Number(best.Score)}, {ReportFormat.Band(best.Band)}");
                if (!string.IsNullOrWhiteSpace(best.Text))
                {
                    md.AppendLine($"   - Nejbližší odstavec: „{Shorten(best.Text, 300)}“");
                }
            }
            else
            {
                md.AppendLine($"{number}. „{best.Text}“ – skóre {ReportFormat.Number(best.Score)}, {ReportFormat.Band(best.Band)}");
                if (best.ContextBefore.Length + best.ContextAfter.Length > 0)
                {
                    md.AppendLine($"   - Kontext: …{Shorten(best.ContextBefore, 150, fromEnd: true)} **[věta]** {Shorten(best.ContextAfter, 150)}…");
                }

                md.AppendLine($"   - Pravděpodobnosti: {ReportFormat.Questions(best.QuestionProbs)}");
            }

            if (urls.Count > 0)
            {
                var shown = string.Join(", ", urls.Take(MaxUrlsPerFinding));
                md.AppendLine($"   - Stránky ({urls.Count}): {shown}{(urls.Count > MaxUrlsPerFinding ? $" a dalších {urls.Count - MaxUrlsPerFinding}" : "")}");
            }

            if (group.Any(f => f.Boilerplate))
            {
                md.AppendLine("   - Šablonový text (patička, menu nebo opakovaný blok)");
            }

            foreach (var note in group.SelectMany(f => FindingDocument.RenderNotes(f, renderer, locale, Several)).Distinct())
            {
                md.AppendLine($"   - Poznámka: {note}");
            }

            md.AppendLine();
        }

        private static void AppendImages(StringBuilder md, ScanResult result)
        {
            md.AppendLine("## Obrázky k ruční kontrole");
            md.AppendLine();
            if (result.ImagesForReview.Count == 0)
            {
                md.AppendLine("Žádný alt text ani název souboru obrázku neobsahuje sledovaná slova.");
                md.AppendLine();
                return;
            }

            md.AppendLine("Jev obrázky nevidí. Tyto obrázky mají v alt textu nebo názvu souboru environmentální slovo, zkontrolujte je ručně.");
            md.AppendLine();
            md.AppendLine("| Stránka | Soubor | Alt text | Slovo |");
            md.AppendLine("| --- | --- | --- | --- |");
            foreach (var image in result.ImagesForReview)
            {
                md.AppendLine($"| {image.PageUrl} | {image.FileName} | {image.Alt ?? ""} | {image.Keyword} |");
            }

            md.AppendLine();
        }

        private static void AppendNotChecked(StringBuilder md, ScanResult result)
        {
            var stats = result.Stats;
            md.AppendLine("## Co nebylo zkontrolováno");
            md.AppendLine();
            md.AppendLine("- Text v obrázcích (Jev obrázky nevidí; podezřelé obrázky jsou vypsané výše).");
            var notLoaded = result.Pages.Where(p => p.TextNotLoaded).ToList();
            if (notLoaded.Count == 0)
            {
                md.AppendLine("- Části stránek, které web dotahuje až JavaScriptem (widgety recenzí, odpočty, záložky načítané po kliknutí). "
                    + "Stránky, které by byly bez JavaScriptu celé prázdné, nástroj hlásí zvlášť; v tomto běhu žádné nebyly.");
            }
            else
            {
                md.AppendLine("- Části stránek, které web dotahuje až JavaScriptem (widgety recenzí, odpočty, záložky načítané po kliknutí).");
                md.AppendLine($"- Stránky, jejichž text se nenačetl ({notLoaded.Count}); zkontrolovaný je jen titulek, meta popis a popis z JSON-LD, pokud je stránka má:");
                foreach (var page in notLoaded)
                {
                    var sign = page.ScriptApp is null ? "bez znaku aplikace" : ReportFormat.ScriptAppName(page.ScriptApp);
                    md.AppendLine($"  - {page.Url} ({ReportFormat.PageTypeName(page.Type)}, {page.VisibleTextChars} znaků čitelného textu, {sign})");
                }
            }
            if (result.NotProcessedPages.Count > 0)
            {
                md.AppendLine($"- Stránky, které se nepodařilo přečíst v časovém limitu ({result.NotProcessedPages.Count}); nezkontrolovalo se na nich nic:");
                foreach (var page in result.NotProcessedPages)
                {
                    md.AppendLine($"  - {page.Url}");
                }
            }

            if (result.BlockedUrls.Count > 0)
            {
                md.AppendLine($"- Adresy, které vedou do vnitřní nebo místní sítě, se nestáhly (ochrana proti SSRF) ({result.BlockedUrls.Count}):");
                foreach (var url in result.BlockedUrls)
                {
                    md.AppendLine($"  - {url}");
                }
            }

            md.AppendLine("- Navigace a filtry (menu, seznamy kategorií, drobečková navigace, volby filtrů): jde o odkazy a volby, ne o tvrzení.");
            md.AppendLine("- Výpisy jiných produktů na stránce (podobné produkty, dlaždice s cenou a odkazem na jiný produkt): jejich texty se kontrolují na stránce toho produktu; při kontrole jen vzorku stránek se produkty mimo vzorek nekontrolují.");
            if (stats.ProfileSkippedTextChars > 0)
            {
                var roles = result.Profiles
                    .SelectMany(u => u.SkippedCharsByRole)
                    .GroupBy(r => r.Key)
                    .Select(g => (Role: g.Key, Chars: g.Sum(r => r.Value)))
                    .OrderByDescending(r => r.Chars)
                    .Select(r => $"{ReportFormat.ProfileRoleName(r.Role)} {ReportFormat.Number(r.Chars)} znaků");
                md.AppendLine($"- Ovládací prvky stránek podle profilu šablony ({string.Join(", ", roles)}): nejde o tvrzení obchodu. "
                    + "Hlavní text stránky profil nikdy nevynechává; co přesně se vynechalo, je v pages.jsonl (profile_skipped_text), profily v profiles.json. "
                    + "Povinné údaje (kontakt, reklamace, odstoupení) se hledají v celé stránce včetně vynechaných částí.");
            }

            var uncheckedPages = result.Pages.Where(p => p.HasUncheckedText && !p.TextNotLoaded).ToList();
            if (uncheckedPages.Count > 0)
            {
                md.AppendLine($"- Stránky, kde nástroj nezkontroloval aspoň {PageInfo.UncheckedReportShare.ToString("0 %", ReportFormat.Czech)} viditelného textu mimo navigaci ({uncheckedPages.Count}); zkontrolujte je ručně:");
                foreach (var page in uncheckedPages.Take(20))
                {
                    md.AppendLine($"  - {page.Url} ({page.UncheckedTextChars} z {page.VisibleTextChars} znaků)");
                }

                if (uncheckedPages.Count > 20)
                {
                    md.AppendLine($"  - a dalších {uncheckedPages.Count - 20} (seznam je v pages.jsonl, pole unchecked_text_chars)");
                }
            }

            md.AppendLine("- Procesní povinnosti, které z textu webu nevyplývají (např. zda se reklamace skutečně vyřizují včas).");
            md.AppendLine($"- Stránky nad limit: {stats.PagesOverLimit} nestažených, {stats.ProductPagesOverLimit} produktových nad limit vzorku; {stats.PagesBlockedByRobots} zakázaných v robots.txt.");
            if (stats.SieveSkippedPairs > 0)
            {
                md.AppendLine($"- Věty v úsecích, kde síto nenašlo téma modulu: {stats.SieveSkippedPairs} dvojic věta × modul bez podrobných otázek; úseky a pravděpodobnosti síta jsou v sieve.csv, bez síta běží sken s volbou --no-sieve.");
            }
            if (result.UncheckedDocuments.Count == 0)
            {
                md.AppendLine("- Právní dokumenty v PDF: žádné odkazy nenalezeny.");
            }
            else
            {
                md.AppendLine("- Právní dokumenty v PDF (nástroj je nečte):");
                foreach (var document in result.UncheckedDocuments)
                {
                    md.AppendLine($"  - {document.Url}{(document.LinkText is null ? "" : $" („{document.LinkText}“)")}, nalezeno na {document.FoundOn}");
                }
            }

            md.AppendLine();
        }

        /// <summary>
        /// A text of the verdicts: once when all verdicts have the same, otherwise one paragraph per jurisdiction.
        /// </summary>
        private static void AppendByJurisdiction(StringBuilder md, List<(JurisdictionVerdict Verdict, RenderedFinding Texts)> verdicts, Func<RenderedFinding, string> text, string prefix)
        {
            var distinct = verdicts.Select(v => text(v.Texts)).Distinct().ToList();
            if (distinct.Count == 1)
            {
                md.AppendLine(prefix + distinct[0]);
                md.AppendLine();
                return;
            }

            foreach (var (verdict, texts) in verdicts)
            {
                md.AppendLine($"{prefix}**{verdict.Jurisdiction.ToUpperInvariant()}:** {text(texts)}");
                md.AppendLine();
            }
        }

        private static string Verdict(JurisdictionVerdict verdict) =>
            $"{verdict.Jurisdiction.ToUpperInvariant()} {ReportFormat.Checkability(verdict.Checkability)}, závažnost {ReportFormat.Severity(verdict.Severity)}, {ReportFormat.Band(verdict.Band)}"
            + (verdict.Status == VerdictStatus.Upcoming && verdict.EffectiveFrom is { } from ? $", platí od {from.ToString("d. M. yyyy", ReportFormat.Czech)}" : "");

        /// <summary>Obligations of the whole site by jurisdiction (runs for several jurisdictions).</summary>
        private void AppendObligations(StringBuilder md)
        {
            md.AppendLine("## Povinnosti za celý web");
            md.AppendLine();
            if (result.SiteObligations.Count == 0)
            {
                md.AppendLine("Žádná pravidla pro celý web neběžela.");
                md.AppendLine();
                return;
            }

            md.AppendLine("| Země | Pravidlo | Stav povinnosti | Platí od |");
            md.AppendLine("| --- | --- | --- | --- |");
            foreach (var obligation in result.SiteObligations.OrderBy(o => o.Jurisdiction, StringComparer.Ordinal))
            {
                var title = renderer.Title(obligation.RuleSet, obligation.RuleId, obligation.Jurisdiction, locale);
                var status = obligation.Status switch
                {
                    ObligationStatus.Met => "splněno",
                    ObligationStatus.Missing => "chybí",
                    ObligationStatus.Upcoming => "chybí, povinnost platí později",
                    _ => $"nezkontrolováno ({renderer.Code(obligation.Reason ?? EngineCodes.EvaluationSkipped, null, locale)})",
                };
                md.AppendLine($"| {obligation.Jurisdiction.ToUpperInvariant()} | {title} | {status} | {obligation.EffectiveFrom?.ToString("d. M. yyyy", ReportFormat.Czech) ?? ""} |");
            }

            md.AppendLine();
        }

        /// <summary>Which modules ran for which jurisdiction (runs for several jurisdictions).</summary>
        private void AppendCoverage(StringBuilder md)
        {
            md.AppendLine("## Pokrytí zemí");
            md.AppendLine();
            foreach (var coverage in result.JurisdictionCoverage)
            {
                var notRun = coverage.NotRun.Select(n => $"{n.Module} ({renderer.Code(n.Reason, null, locale)})").ToList();
                md.AppendLine($"- {coverage.Jurisdiction.ToUpperInvariant()}: moduly {(coverage.Modules.Count == 0 ? "žádné" : string.Join(", ", coverage.Modules))}"
                    + (notRun.Count > 0 ? $"; neběžely: {string.Join(", ", notRun)}" : ""));
            }

            md.AppendLine();
        }

        private static IEnumerable<List<Finding>> OrderedRules(IEnumerable<Finding> findings) =>
            findings.GroupBy(f => (f.RuleId, Upcoming: Group(f) == UpcomingGroup))
                .Select(g => g.ToList())
                .OrderBy(g => GroupRank(Group(g[0])))
                .ThenBy(g => ReportFormat.SeverityRank(g[0].Severity))
                .ThenByDescending(g => g.Max(f => f.Score));

        private static string Shorten(string text, int maxLength, bool fromEnd = false)
        {
            // Legal paragraphs start with their heading on its own line.
            var singleLine = text.Replace("\n", " – ");
            if (singleLine.Length <= maxLength)
            {
                return singleLine;
            }

            return fromEnd ? "…" + singleLine[^maxLength..] : singleLine[..maxLength] + "…";
        }
    }
}
