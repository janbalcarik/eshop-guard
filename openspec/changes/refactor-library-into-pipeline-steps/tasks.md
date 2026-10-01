# Tasks

Cesty jsou po přejmenování ze změny 1 (`eshop-guard/`, `EshopGuard.*`).

## 1. Zachycení dnešního stavu (před první úpravou logiky)
- [ ] 1.1 Spustit `dotnet test -- --filter-not-trait "Category=Jev"` na kódu po změně 1 a zapsat skutečný počet testů do tohoto souboru (podklady uvádějí 192, proposal K rozhodnutí 9). Hotovo: číslo zapsané, všechny testy zelené.
- [ ] 1.2 Ověřit, že je kód v repozitáři git (proposal K rozhodnutí 8); pokud ne, počkat na rozhodnutí uživatele a nic dalšího nezačínat.
- [ ] 1.3 Přidat `src/EshopGuard.Core/Crawl/PageRecording.cs` (formát `index.jsonl`: URL, stav, hlavičky `Content-Type`, `ETag`, `Last-Modified`, `Location`, `Retry-After`, odkaz na tělo; těla v `bodies/{sha256}.bin`), `RecordingPageFetcher` (dekorátor `IPageFetcher`, zapisuje každou odpověď) a `ReplayPageFetcher` (vrací uložené odpovědi, neznámá URL = 404 a záznam do logu). Test `RecordReplayTests`: nahrávka `Fixtures/site-sk` přes `FileSystemPageFetcher` a přehrání dají shodný `ScanResult` (po normalizaci časů).
- [ ] 1.4 Přidat do `src/EshopGuard.Cli/Commands/ScanCommand.cs` volby `--record <dir>` a `--replay <dir>` (vzájemně výlučné) a jejich registraci v `CliHost.BuildServices` před `AddEshopGuard`; do `.gitignore` doplnit `snapshots/` a `baselines/` (proposal K rozhodnutí 7). Test: `ScanSettings` odmítne obě volby najednou.
- [ ] 1.5 **Souhlas uživatele (síť, bez placených volání):** nahrát `eshopguard scan https://vegis.sk --mock --record snapshots/vegis.sk` a totéž pro `https://naturfyt.sk`. Odhad: 0 USD (Jev ani OpenAI se nevolají, `--mock` nevytváří profily), řádově 110 a 50 požadavků na cizí weby podle skenů z 30. 9. 2026, robots.txt a User-Agent `EshopGuard/0.1` s kontaktem z `config/settings.yaml`. Hotovo: obě nahrávky existují a `index.jsonl` obsahuje robots.txt, sitemap a stránky.
- [ ] 1.6 Dnešním kódem vytvořit referenční výstupy: pomocný test `DumpBaselines` (kategorie `Baseline`, v běžném běhu přeskočený, `FileSystemPageFetcher`, mock) zapíše `tests/EshopGuard.Core.Tests/Baselines/site/*` a `Baselines/site-sk/*` (do repozitáře); `scan --replay --mock` zapíše `baselines/vegis.sk/*` a `baselines/naturfyt.sk/*` (lokálně). Soubory: `findings.json`, `pages.jsonl`, `segments.csv`, `sieve.csv`, `profiles.json`, `report.md`.
- [ ] 1.7 Dnešním kódem zapsat referenční seznam klíčů cache Jevu `tests/EshopGuard.Core.Tests/Baselines/jev-legacy-keys.txt` (pro každý segment a úsek síta testovacích e-shopů: druh, otisk segmentu nebo úseku, modul, výsledek `JevCacheKey.Create`). Pomocný test `DumpLegacyKeys` má kategorii `Baseline`.
- [ ] 1.8 Přidat `src/EshopGuard.Cli/Commands/BenchExtractCommand.cs` (`bench-extract --replay <dir>`, sestavení Release, `Process.TotalProcessorTime` za extrakci, klasifikaci a přiřazení profilu všech stránek nahrávky) a dnešním kódem změřit obě nahrávky. Hotovo: čas procesoru na stránku pro vegis.sk a naturfyt.sk zapsaný do tohoto souboru.
- [ ] 1.9 **Placené, odhad ceny + souhlas uživatele:** referenční ostrý běh dnešního kódu nad nahrávkami s Jevem a cache (`scan https://vegis.sk --replay snapshots/vegis.sk`, totéž naturfyt.sk; nové profily jen se souhlasem i s jejich cenou). Před spuštěním vypsat odhad z dotazu CLI (volání mimo cache × 0,042 USD za milion vstupních tokenů; nový profil 0,07–0,13 USD podle sondy 1. 10. 2026) a počkat na souhlas. Výstupy uložit do `baselines/<host>-jev/`.

## 2. Smlouvy kroků a serializace
- [ ] 2.1 Přidat `src/EshopGuard.Core/Pipeline/PipelineJson.cs` (`PipelineJson.Options`: `snake_case`, konvertory `Uri` a `TextBlock`; `PipelineSchemaException`; `PipelineSchema.Version = 1`) a záznamy v `Pipeline/Contracts/` podle designu: `SiteScope`, `CrawlLimits`, `DiscoveryInput`, `DiscoveryResult`, `RobotsSnapshot`, `SitemapEntry`, `FetchBatchInput` (včetně `ExtractInline`), `FetchBatchResult`, `FetchedPage`, `FetchOutcome`, `ConditionalHeaders`, `PaceState`, `CrawlCounters`, `ExtractInput`, `ExtractedPageRecord`, `PageExtract`, `ExtractionStatus`, `DiscoveredLink`, `ProfileFit`, `ProfilePlanInput`, `ProfileCandidate`, `ProfilePlan`, `PlannedProfile`, `ProfileCreateResult`, `SegmentInput`, `SegmentResult`, `PageSegmentation`, `SieveChunkInput`, `SegmentState`, `EstimateInput`, `RunEstimate`, `SieveBatchInput`, `SieveBatchResult`, `EvaluateBatchInput`, `EvaluateBatchResult`, `RulesInput`.
- [ ] 2.2 Převod `ExtractedPage` ↔ `PageExtract` v `src/EshopGuard.Core/Extract/ExtractedPage.cs` (bez ztráty: bloky s úrovní nadpisu, oblasti rámu, odkazy, obrázky, `RenderCheck.Result`, `ProfileSkippedBlocks`).
- [ ] 2.3 Test `Pipeline/StepContractSerializationTests.cs`: každý záznam z 2.1 naplněný daty z `Fixtures/site-sk` projde JSON tam a zpět beze změny; `schema_version` = 2 vyhodí `PipelineSchemaException`.

## 3. Úložiště za rozhraním
- [ ] 3.1 Založit jmenný prostor `EshopGuard.Core.Storage` a přesunout do něj `IJevCache` (z `Cache/IJevCache.cs`), `IRewriteCache` (z `Fix/RewriteCache.cs`) a `IPageProfileStore` (z `Profiles/PageProfileStore.cs`); implementace `SqliteJevCache`, `SqliteRewriteCache`, `SqlitePageProfileStore`, `NullJevCache`, `NullRewriteCache` zůstanou na svých místech.
- [ ] 3.2 Nahradit `Cache/JevCacheKey.cs` záznamem `JevCacheKey(Kind, QuestionSetHash, StateHash, LegacyKey)` a továrnou `JevCacheKeys.Create(kind, model, version, language, questions, state)`; `LegacyKey` počítat přesně jako dnešní `Create`. Do `IJevCache` přidat `GetManyAsync` s výchozí implementací po jednom; `SqliteJevCache` čte a píše jen `LegacyKey` a `GetManyAsync` dělá jedním dotazem `IN`.
- [ ] 3.3 Test `Cache/JevCacheKeyCompatibilityTests.cs`: klíče všech segmentů a úseků síta testovacích e-shopů se shodují s `Baselines/jev-legacy-keys.txt`; síto a podrobné otázky mají různý `Kind`; změna textu pravidla (`title`) nezmění `QuestionSetHash`.
- [ ] 3.4 Přidat `Storage/IPageContentStore.cs`, `Storage/PageContentKey.cs` a `Storage/InMemoryPageContentStore.cs` (gzip v paměti jako dnes `CrawledPage.Compress`).
- [ ] 3.5 Přidat `Storage/IPageStore.cs`, `Storage/PageRecord.cs` (URL, finální URL, stav, typ, `ETag`, `LastModified`, `TextHash`, poslední stažení), `Storage/PageVersionRecord.cs` (stránka, `TextHash`, otisky vět `long[]`, počty znaků z `PageInfo`, metoda extrakce, `ScriptApp`, `TextNotLoaded`) a `Storage/InMemoryPageStore.cs` (`AddVersionAsync` jen při změně `TextHash`, `FindByFingerprintAsync`).
- [ ] 3.6 Přidat `Storage/IUrlFrontierStore.cs` a `Storage/InMemoryUrlFrontierStore.cs`.
- [ ] 3.7 Přidat `Storage/IRateLimiter.cs` (`RateResource.Jev`, `RateResource.OpenAi`, `RequestPriority.P0`–`P4`) a `Storage/LocalRateLimiter.cs` (token bucket z `JevClient` po zdrojích); upravit `Jev/JevClient.cs` a `Fix/OpenAiRewriteClient.cs`, aby žádaly povolení přes `IRateLimiter`. Test `Storage/LocalRateLimiterTests.cs`: 1200/min dá nejvýš 20 povolení za sekundu; zrušení během čekání vyhodí `OperationCanceledException`.
- [ ] 3.8 Přidat abstraktní `tests/EshopGuard.Core.Tests/Storage/StoreContractTests.cs` (pro `IJevCache`, `IPageStore`, `IPageContentStore`, `IUrlFrontierStore`: idempotentní zápis, dávkové čtení, verze jen při změně textu, uložení a obnova stavu fronty) a odvozené `InMemoryStoreTests.cs` a `SqliteJevCacheContractTests.cs`.
- [ ] 3.9 Test `CoreDependencyTests.cs`: `EshopGuard.Core` neodkazuje `EshopGuard.Data`, `Npgsql` ani `Microsoft.EntityFrameworkCore` (kontrola `Assembly.GetReferencedAssemblies()` a `PackageReference` v `EshopGuard.Core.csproj`).

## 4. Jedno čtení HTML a extrakce
- [ ] 4.1 Přidat `src/EshopGuard.Core/Extract/ParsedPage.cs` (jedno `HtmlParser.ParseDocument`, text HTML pro SmartReader, základní URL z `<base>`, testovací počítadlo `HtmlParserProbe`) a upravit `Extract/ContentExtractor.cs`: `Extract(Uri, ParsedPage)`; `ReadRemainder` a záložní větev `ExtractMain` bez `element.Remove()` přes množinu vynechaných uzlů a predikát `skip` pro `HtmlText.ExtractBlocks`. Staré metody nechat dočasně jako `ExtractLegacy` pro test 4.3.
- [ ] 4.2 Upravit `Profiles/ProfileMatcher.cs`: `ApplyNonDestructive(ParsedPage, PageExtract, PageProfile)` místo `Apply(Parse(html), ...)`, `StructureTokens` nad `ParsedPage`.
- [ ] 4.3 Test `Extract/SingleParseEquivalenceTests.cs`: pro všechny stránky `Fixtures/site`, `Fixtures/site-sk` a (kategorie `Snapshot`) nahrávek dá stará a nová cesta shodné bloky, počty znaků, `Render` a po profilu `ProfileSkippedBlocks`; `HtmlParserProbe` ukáže jedno naše čtení na stránku. Po zelené smazat `ExtractLegacy` a starý `Apply`.
- [ ] 4.4 Časový limit extrakce v `ExtractStep` (`CrawlOptions.ExtractTimeoutSeconds`, `ExtractionStatus.NotProcessed`, `ScanResult.NotProcessedPages`, upozornění ve zprávě). Test `Extract/ExtractTimeoutTests.cs` s extraktorem, který čeká déle než limit.
- [ ] 4.5 Ověřit v balíčku SmartReader 0.11.1, zda `SmartReader.Reader` přijme hotový `IDocument`; výsledek zapsat do `design.md` (oddíl Jedno čtení HTML). Pokud ano, předat `ParsedPage.Document` a zopakovat test 4.3.
- [ ] 4.6 Přidat `Segmentation/SentenceFingerprint.cs` a vlastnost `Segment.Fingerprint`. Test `Segmentation/SentenceFingerprintTests.cs`: stejný otisk při jiné velikosti písmen a mezerách, různý při jiném textu, stejný u stejné věty v jiném kontextu.

## 5. Stahování: fronta, dávky, podmíněné stažení
- [ ] 5.1 Přidat `Crawl/FetchRequest.cs`; změnit `Crawl/IPageFetcher.cs` na `FetchAsync(FetchRequest, ...)` s výchozí metodou pro `Uri`; doplnit `FetchResponse.ETag`, `LastModified`, `NotModified`; v `Crawl/HttpPageFetcher.cs` posílat `If-None-Match` a `If-Modified-Since`, číst `ETag` a `Last-Modified` a ošetřit 304 před větví 3xx.
- [ ] 5.2 Upravit `Crawl/SitemapParser.cs`: `SitemapLocation(Url, LastModified?)` z `<lastmod>`; `DiscoveryResult.SitemapEntries` je nese dál. Test v `CrawlTests.cs`: `lastmod` se načte, neplatné datum = `null`.
- [ ] 5.3 Přidat `Pipeline/UrlFrontier.cs`, `UrlFrontierState.cs`, `FrontierItem.cs` s logikou `Crawler.Run.Consider`, `TryDequeue`, limity stránek a produktů, režim odkazů, `NeedsLinks`.
- [ ] 5.4 Přidat `Pipeline/DiscoveryStep.cs` (robots.txt, Crawl-delay, sitemap a index do hloubky 3, `max_sitemap_urls`, režim odkazů, upozornění jako dnes).
- [ ] 5.5 Přidat `Pipeline/FetchStep.cs` (limity dávky, přesměrování ve stejném webu, robots.txt u cíle přesměrování, `AdaptiveGate` se stavem `PaceState`, `ExtractInline`, sběr odkazů přes `ExtractStep` u úvodní stránky a v režimu odkazů, `FetchOutcome`).
- [ ] 5.6 Test `Pipeline/FrontierResumeTests.cs`: nad `Fixtures/site-sk` s dávkou 3 stránek uložit `UrlFrontierState` do JSON po každé dávce, vytvořit nový `FetchStep` z JSON a pokračovat; výsledné stránky a nálezy se shodují s během bez přerušení; žádná URL se nestáhne dvakrát.
- [ ] 5.7 Upravit `tests/EshopGuard.Core.Tests/FileSystemPageFetcher.cs` (volitelný `ETag` a 304 na `If-None-Match`) a přidat `Crawl/ConditionalFetchTests.cs` (304 → `NotModified` bez extrakce; 200 se stejným textem → žádná nová verze; bez validátorů žádné hlavičky).
- [ ] 5.8 Upravit `CrawlTests.cs` a `CrawlPaceTests.cs` na `DiscoveryStep` a `FetchStep` bez změny očekávání; všechny projdou.

## 6. Ochrana proti SSRF
- [ ] 6.1 Přidat `Crawl/SsrfGuard.cs` (rozsahy z architektury a navržené navíc podle K rozhodnutí 3, převod mapovaných IPv4). Test `Crawl/SsrfGuardTests.cs` pro každý rozsah a hraniční adresy (172.15.255.255 povolena, 172.16.0.0 blokována, `::ffff:10.0.0.1` blokována).
- [ ] 6.2 Upravit `HttpPageFetcher`: odmítnout jiné porty než 80 a 443, adresu se jménem a heslem a jiné schéma (`ssrf_blocked`) bez spojení.
- [ ] 6.3 Přidat `Crawl/IHostAddressResolver.cs` a `DnsHostAddressResolver`; v `ServiceCollectionExtensions.AddEshopGuard` nastavit u klienta `EshopGuard.Crawl` `SocketsHttpHandler.ConnectCallback` (překlad přes `IHostAddressResolver`, odmítnutí při kterékoli blokované adrese, připojení na ověřenou adresu) a `UseProxy = false`.
- [ ] 6.4 Doplnit `FetchOutcome.Blocked`, `CrawlCounters.SsrfBlocked`, `ScanResult.BlockedUrls` a upozornění se seznamem; zpráva `report.md` je uvede v části „Co nebylo zkontrolováno“.
- [ ] 6.5 Přidat `CrawlOptions.AllowPrivateNetwork` a volbu CLI `--allow-private-network`; upravit příklad v `README.md` (`scan http://localhost:8000 --allow-private-network`).
- [ ] 6.6 Test `Crawl/HttpPageFetcherSsrfTests.cs` s falešným `IHostAddressResolver`: doména na 10.0.0.5, přesměrování na 169.254.169.254, DNS s 93.184.216.34 a ::1, port 8443; s `AllowPrivateNetwork` stažení z místního `HttpListener` projde, bez něj ne.

## 7. Kroky profilů, segmentů, odhadu, Jevu, pravidel a přepisu
- [ ] 7.1 Přidat `Pipeline/ExtractStep.cs` (`ParsedPage`, `ContentExtractor`, `PageClassifier`, přiřazení uložených profilů, `TextHash`, `IPageContentStore`, `IPageStore`, časový limit).
- [ ] 7.2 Přidat `Pipeline/ProfileStep.cs` (`PlanAsync`, `CreateAsync`, `RefitAsync`) z `Profiles/PageProfiler.cs`; HTML vzorových stránek číst z `IPageContentStore`. Upravit `ProfileTests.cs`; všechny projdou se stejnými očekáváními.
- [ ] 7.3 Přidat `Pipeline/SegmentStep.cs` (dnešní `BuildSegments` a `SegmentAggregator.Aggregate`, otisky vět po stránkách).
- [ ] 7.4 Přidat `Pipeline/EstimateStep.cs` (dnešní `EstimateAsync` a `WithProfiles`). Test: falešný klient Jevu a falešný `IRewriteClient` nedostanou během odhadu žádný požadavek.
- [ ] 7.5 Přidat `Pipeline/SieveStep.cs` (dávky po 200 úsecích nad `PageSieve`).
- [ ] 7.6 Přidat `Pipeline/EvaluateStep.cs` (dávky po 200 větách nad `SegmentEvaluator`, `GetManyAsync`, `NotEvaluated`). Test `Pipeline/EvaluateBatchIdempotenceTests.cs`: dávka přerušená po 120 odpovědích a spuštěná znovu pošle jen 80 požadavků; fatální chyba zastaví dávku a uložené odpovědi zůstanou.
- [ ] 7.7 Přidat `Pipeline/RulesStep.cs` (sestavení `RuleEngineInput` z dnešního `ScanSiteAsync`, `SelectRuleSets`, `PageText`, `Signals`, `NotLoadedWarnings`).
- [ ] 7.8 Přidat `Pipeline/RewriteStep.cs` (dávky po 25 stránkách nad `PageRewriter`). Upravit `RewriteTests.cs`.
- [ ] 7.9 Přidat `Pipeline/ScanResultAssembler.cs` (statistiky a pořadí upozornění přesně jako dnes).
- [ ] 7.10 Přidat `Pipeline/InMemoryPipelineRunner.cs` jako `IEshopGuard` (`ScanSiteAsync` podle Data Flow v designu, callback `ConfirmJevCalls` na dnešním místě, `AnalyzeTextsAsync` nad `SegmentStep`, `EvaluateStep`, `RulesStep`) a smazat `EshopGuardService.cs`. Upravit `ServiceCollectionExtensions.cs`.

## 8. CLI a nastavení
- [ ] 8.1 Doplnit do `config/settings.yaml` klíče `crawl.extract_timeout_seconds` (30), `crawl.fetch_batch_max_pages` (100), `crawl.fetch_batch_max_seconds` (60) a jejich načtení v `src/EshopGuard.Cli/CliConfiguration.cs`; `Commands/SettingsValidation.cs` odmítne nulu a záporné hodnoty. Test: `settings.yaml` s `fetch_batch_max_pages: 0` skončí chybou načtení s názvem klíče.
- [ ] 8.2 Doplnit `README.md`: volby `--record`, `--replay`, `--allow-private-network`, příkaz `bench-extract`, ochrana proti SSRF, změřený čas procesoru (z úkolu 9.4).

## 9. Ověření
- [ ] 9.1 `dotnet build` a `dotnet test -- --filter-not-trait "Category=Jev"` projdou; počet testů ≥ číslo z úkolu 1.1 plus nové testy.
- [ ] 9.2 `PipelineEquivalenceTests` (testovací e-shopy) projdou proti `Baselines/site` a `Baselines/site-sk`.
- [ ] 9.3 Test kategorie `Snapshot` projde nad `snapshots/vegis.sk` a `snapshots/naturfyt.sk` proti `baselines/` (lokálně, výsledek zapsat sem).
- [ ] 9.4 `bench-extract` novým kódem nad oběma nahrávkami; zapsat čas procesoru na stránku vedle čísla z úkolu 1.8 a do `README.md`. Hotovo: nový čas je nižší než referenční; když není, změna se nevydá a rozdíl se rozebere.
- [ ] 9.5 `JevCacheKeyCompatibilityTests` projdou; `scan https://vegis.sk --replay snapshots/vegis.sk` nad `cache/jev-cache.sqlite` ukáže v dotazu po odhadu stejný počet odpovědí z cache jako dnešní kód v úkolu 1.9. Na dotaz odpovědět „ne“, takže se nic neplatí.
- [ ] 9.6 Ruční kontrola: `eshopguard scan http://127.0.0.1:8000` bez `--allow-private-network` skončí chybou `ssrf_blocked`, se volbou proběhne.
- [ ] 9.7 **Placené, odhad ceny + souhlas uživatele:** ostrý běh nového kódu nad nahrávkami s Jevem a cache (stejné příkazy jako 1.9). Očekávání: všechna volání z cache, nová cena 0 USD; před spuštěním vypsat odhad a počkat na souhlas. Porovnat nálezy s `baselines/<host>-jev/` (pravidlo, text, URL, pásmo, skóre); každý rozdíl zapsat a vysvětlit.
- [ ] 9.8 Spustit `openspec validate refactor-library-into-pipeline-steps` a opravit chyby formátu.
