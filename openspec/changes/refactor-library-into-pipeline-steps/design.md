# Design: Knihovna rozdělená na kroky analýzy

Cesty jsou po přejmenování ze změny 1 (kořen `eshop-guard/`). V závorce je dnešní soubor v `eshop-checker/`.

## Technical Approach

### Kroky a jejich smlouvy

Nový jmenný prostor `EshopGuard.Core.Pipeline`. Každý krok je třída registrovaná jako singleton, bez stavu mezi voláními. Vstup a výstup jsou `sealed record` serializovatelné přes `PipelineJson.Options` (System.Text.Json, `snake_case`, `schema_version` v každém kořenovém záznamu). Krok, který dostane neznámou verzi schématu, vyhodí `PipelineSchemaException`.

| Krok | Vstup | Výstup | Dávka ve workeru | Z dnešního kódu |
|---|---|---|---|---|
| `DiscoveryStep` | `DiscoveryInput` (`SiteScope`, `CrawlLimits`) | `DiscoveryResult`: `RobotsSnapshot`, `SitemapEntry[]` (URL, `LastModified`, `ProductHint`), `LinkMode`, `UrlFrontierState`, `SitemapUrlCount`, `CrawlCounters`, upozornění | celý e-shop | `Crawler.Run.LoadRobotsAsync`, `ReadSitemapsAsync`, první část `CrawlAsync` |
| `FetchStep` | `FetchBatchInput`: `SiteScope`, `RobotsSnapshot`, `UrlFrontierState`, `PaceState`, validátory (`ConditionalHeaders` po URL), `MaxPages` (100), `MaxDuration` (60 s) | `FetchBatchResult`: `FetchedPage[]`, nový `UrlFrontierState`, `PaceState`, `CrawlCounters`, `FrontierExhausted` | do 100 stránek nebo 60 s, jedna doména | `Crawler.Run.ProcessAsync` (bez extrakce), `FetchHtmlAsync`, `FetchPacedAsync`, `Consider`, `TryDequeue` |
| `ExtractStep` | `ExtractInput`: `SiteScope`, `FetchedPage[]`, uložené profily | `ExtractedPageRecord[]`: `PageInfo`, `PageExtract`, `StructureTokens`, `ProfileFit?`, `TextHash`, `ExtractionStatus`, `DiscoveredLink[]` | do 100 stránek | `ContentExtractor.Extract`, `PageClassifier.Classify`, `PageProfiler.Fit` |
| `ProfileStep.PlanAsync` | `ProfilePlanInput`: `SiteKey`, `ProfileCandidate[]` (URL, tokeny, způsobilost, přiřazený profil) | `ProfilePlan`: plánované profily s cenou a vzorovými URL, `UnavailableReason` | celý e-shop | `PageProfiler.PrepareAsync`, `Plan` |
| `ProfileStep.CreateAsync` | `ProfilePlan` + přístup k HTML přes `IPageContentStore` | `ProfileCreateResult`: nové profily, spotřeba, upozornění | po jednom profilu | `PageProfiler.CreateAsync`, `Validate` |
| `ProfileStep.RefitAsync` | stránky bez profilu + nové profily | `ProfileFit[]` a upravené `PageExtract` | do 100 stránek | `PageProfiler.Fit` |
| `SegmentStep` | `SegmentInput`: zkontrolované stránky (`ExtractedPageRecord`), `SieveDefinition?`, `SegmentationOptions` | `SegmentResult`: unikátní `Segment[]`, `SieveChunkInput[]`, po stránkách `SentenceCount`, `ParagraphCount`, `SentenceFingerprints` | celý e-shop | `EshopGuardService.BuildSegments`, `SegmentAggregator.Aggregate` |
| `EstimateStep` | `EstimateInput`: `SegmentResult`, sady pravidel, síto, jazyk otázek, `ProfilePlan` | `RunEstimate` (dnešní `JevCallEstimate` + část profilů), `RequiresConfirmation` | celý e-shop, P0 | `EshopGuardService.EstimateAsync`, `WithProfiles`, `SegmentEvaluator.EstimateAsync`, `PageSieve.PrepareAsync` |
| `SieveStep` | `SieveBatchInput`: do 200 úseků s otázkami síta | `SieveBatchResult`: `SieveChunkResult[]`, spotřeba | 200 úseků | `PageSieve.EvaluateAsync` |
| `EvaluateStep` | `EvaluateBatchInput`: do 200 `SegmentState` (otisk, druh, věta, kontext, klíče sad pravidel), sady otázek, jazyk | `EvaluateBatchResult`: počty volání, z cache, chyb, vstupní tokeny, model, `NotEvaluated[]` | 200 vět | `SegmentEvaluator.EvaluateAsync` |
| `RulesStep` | `RulesInput`: sady pravidel, značky, zákonné požadavky, segmenty s pravděpodobnostmi (načtené z `IJevCache`), `PageSignals`, texty stránek, kategorie, nečtené dokumenty, nenačtené stránky | `RuleEngineOutput` (dnešní) | celý e-shop | `RuleEngine.Evaluate` a sestavení vstupu v `EshopGuardService.ScanSiteAsync` |
| `RewriteStep` | `RewriteBatchInput`: stránky s nálezy | `RewriteResult` (dnešní) | do 25 stránek | `PageRewriter` |

`ScanResultAssembler` složí z výstupů kroků dnešní `ScanResult` a `ScanStats` (kód statistik se přesune z `EshopGuardService.ScanSiteAsync` beze změny výpočtu).

### Fronta URL

`UrlFrontier` převezme logiku `Crawler.Run`: tři fronty (právní, produktové podle sitemap, ostatní), střídání produkt/ostatní (`_takeProductNext`), množiny `visited` a `queued`, čítače `fetched` a `productsIncluded`, režim odkazů (`LinkMode`), limity stránek a produktů. Stav je `UrlFrontierState` (pole `FrontierItem` s URL, hloubkou, nápovědou produktu a prioritou, klíče navštívených, čítače). `UrlFrontier.Consider(...)` dělá to, co dnes `Crawler.Run.Consider`: stejná stránka, robots.txt, filtr URL, PDF právních stránek do `UncheckedDocuments`.

Odkazy z úvodní stránky a v režimu odkazů potřebují extrakci. `FetchStep` proto u stránek, kde `UrlFrontier.NeedsLinks(page)` (úvodní stránka, režim odkazů), zavolá `ExtractStep` hned a výsledek předá dál (`FetchedPage.Extract`). `ExtractStep` takovou stránku nečte znovu. V CLI to dává přesně dnešní pořadí: stažení, extrakce, odkazy, další stránka.

Limit produktů (`sample_products`) potřebuje typ stránky, který zná až extrakce (`PageClassifier.Classify` podle JSON-LD a `og:type`); dnes ho `Crawler.Run.ProcessAsync` počítá hned po extrakci a `TryDequeue` podle něj přeskakuje další produkty. Proto `FetchBatchInput.ExtractInline`:
- `true` (CLI, ukázka zdarma, jakýkoli běh s limitem produktů): `FetchStep` vytěží každou stránku hned a čítač `UrlFrontierState.ProductsIncluded` se mění jako dnes;
- `false` (úvodní analýza celého e-shopu bez limitu produktů): `FetchStep` jen stahuje a extrakce běží v samostatných úlohách `cpu` (databázový návrh, část 5 bod 2).

### Podmíněné stažení

```csharp
public sealed record FetchRequest(Uri Url)
{
    public string? IfNoneMatch { get; init; }
    public DateTimeOffset? IfModifiedSince { get; init; }
}
```

- `IPageFetcher.FetchAsync(FetchRequest, CancellationToken)`; dnešní `FetchAsync(Uri, CancellationToken)` zůstane jako výchozí metoda rozhraní, aby testovací `FileSystemPageFetcher` fungoval beze změny.
- `FetchResponse` dostane `ETag`, `LastModified` a `NotModified` (true pro 304). `HttpPageFetcher` ošetří 304 **před** větví 3xx (dnes by 304 skončil jako přesměrování bez cíle a stránka jako chyba).
- `FetchStep` vezme validátory z `IPageStore.GetValidatorsAsync`. Výsledek 304 je `FetchOutcome.NotModified`: stránka se nestahuje, nevytěžuje ani nepočítá a zůstává její poslední verze. V CLI validátory nejsou, chování je stejné jako dnes.

### Ochrana proti SSRF

- `SsrfGuard.IsBlocked(IPAddress)`: rozsahy z architektury (10/8, 172.16/12, 192.168/16, 127/8, 169.254/16 a obdoby v IPv6) a navržené navíc (proposal, K rozhodnutí 3). Adresy IPv4 mapované do IPv6 se převedou a zkontrolují jako IPv4.
- `HttpPageFetcher` před požadavkem: schéma `http`/`https`, port 80 nebo 443, žádné jméno a heslo v adrese. Jinak `FetchResponse { Error = "ssrf_blocked" }` bez spojení.
- `SocketsHttpHandler.ConnectCallback`: `IHostAddressResolver.ResolveAsync(host)` (výchozí `Dns.GetHostAddressesAsync`), když je **kterákoli** adresa blokovaná, spojení se nenaváže. Jinak se socket připojí na první ověřenou adresu. Nové DNS mezi kontrolou a připojením není, takže DNS rebinding neprojde. `UseProxy = false`.
- Přesměrování řeší crawler sám (`AllowAutoRedirect = false`), každý krok jde znovu přes `FetchAsync` a znovu přes kontrolu. Totéž robots.txt a sitemap.
- `CrawlOptions.AllowPrivateNetwork` (výchozí `false`) povolí vnitřní adresy a jiné porty. Nastaví ho jen volba CLI `--allow-private-network` pro `serve-fixture`. Worker ji nikdy nenastaví (test ve změně 8).
- Blokovaná stránka: `FetchOutcome.Blocked`, čítač `CrawlCounters.SsrfBlocked`, upozornění se seznamem adres. Stránka se nikdy nevydává za zkontrolovanou.

### Jedno čtení HTML

`ParsedPage` = `IDocument` z jednoho `HtmlParser.ParseDocument` + text HTML (pro SmartReader). `ContentExtractor.Extract(Uri, ParsedPage)` z něj spočítá:
- `RenderCheck.Inspect` (beze změny, jen čte);
- rám stránky, obrázky, odkazy, meta, JSON-LD, kategorii (dnes stejně, jen čtou);
- `ReadRemainder`: místo `element.Remove()` nad čerstvou kopií se sestaví `HashSet<INode> excluded` (rám, `FallbackRemovedSelector`, dlaždice) a `HtmlText.ExtractBlocks(body, skip)` dostane predikát `excluded.Contains`;
- `ExtractMain` záložní heuristika stejně bez mazání;
- `ProfileMatcher.StructureTokens(body)` pro profily;
- přiřazení uloženého profilu: `ProfileMatcher.ApplyNonDestructive(ParsedPage, PageExtract, PageProfile)` místo `Apply(Parse(html), ...)`.

Pořadí bloků musí zůstat stejné. `HtmlText.ExtractBlocks` už predikát `skip` umí, mění se jen to, kdo ho skládá. Test `SingleParseEquivalenceTests` porovná pro každou stránku nahrávek a testovacích e-shopů bloky staré a nové cesty (staré metody zůstanou do konce změny jako `internal` pro test, pak se smažou).

Počítadlo čtení `ParsedPage.ParseCount` (jen v testech přes `HtmlParserProbe`) ověří jedno naše čtení na stránku v `ExtractStep` a žádné v `ProfileStep.PlanAsync`.

### Časový limit extrakce

`ExtractStep` spustí extrakci jedné stránky přes `Task.Run` s `CrawlOptions.ExtractTimeoutSeconds` (návrh 30 s). Po limitu má stránka `ExtractionStatus.NotProcessed`, `PageInfo.TextNotLoaded` zůstává `false` a stránka jde do `ScanResult.NotProcessedPages` s důvodem `extract_timeout`. Úloha SmartReaderu doběhne na pozadí (nejde přerušit).

### Úložiště za rozhraním

Nový jmenný prostor `EshopGuard.Core.Storage`. Core nezávisí na `EshopGuard.Data`, Npgsql ani EF Core (test `CoreDependencyTests`).

| Rozhraní | Metody | Výchozí pro CLI | Budoucí PostgreSQL/S3 (změna 8) |
|---|---|---|---|
| `IJevCache` (přesun z `Cache/`) | `GetAsync(JevCacheKey)`, `GetManyAsync(IReadOnlyList<JevCacheKey>)`, `SetAsync(JevCacheKey, JevResult)` | `SqliteJevCache` (klíč `LegacyKey`), `NullJevCache` | `checks.jev_answers` / `checks.sieve_answers` podle `Kind` |
| `IRewriteCache` (z `Fix/RewriteCache.cs`) | beze změny | `SqliteRewriteCache` | `fixes.rewrite_cache` |
| `IPageProfileStore` (z `Profiles/PageProfileStore.cs`) | beze změny (`GetAsync(site)`, `AddAsync`) | `SqlitePageProfileStore` | `shop.page_profiles`; instance pro jednu úlohu s tenantem a e-shopem, `site` jen ověří |
| `IPageContentStore` (nové) | `PutHtmlAsync(PageContentKey, byte[] gzip)`, `GetHtmlAsync`, `PutExtractAsync(PageContentKey, PageExtract)`, `GetExtractAsync` | `InMemoryPageContentStore` (gzip v paměti jako dnes `CrawledPage.CompressedHtml`) | `IBlobStore`, klíče `tenants/{t}/shops/{s}/pages/{page}/{version}.html.gz` |
| `IPageStore` (nové) | `GetValidatorsAsync(urls)`, `UpsertPageAsync(PageRecord)`, `AddVersionAsync(PageVersionRecord)` (jen při změně `TextHash`), `FindByFingerprintAsync(long)` | `InMemoryPageStore` | `content.pages`, `content.page_versions` (`segment_hashes`) |
| `IUrlFrontierStore` (nové) | `LoadAsync(scopeKey)`, `SaveAsync(scopeKey, UrlFrontierState)` | `InMemoryUrlFrontierStore` | tabulka fronty URL běhu (změna 8 navrhuje `checks.run_urls`) |
| `IRateLimiter` (nové) | `AcquireAsync(RateResource, int permits, RequestPriority, CancellationToken)` → `IAsyncDisposable` | `LocalRateLimiter` (dnešní `TokenBucketRateLimiter` z `JevClient` po zdrojích) | `ops.rate_limit_buckets` |

Společné testy chování `StoreContractTests<TStore>` (abstraktní třídy v `tests/EshopGuard.Core.Tests/Storage/`): každá implementace, i budoucí PostgreSQL, je zdědí a musí projít (idempotentní zápis, dávkové čtení, verze jen při změně textu).

### Klíč cache Jevu

```csharp
public readonly record struct JevCacheKey(
    JevCacheKind Kind,          // Detail nebo Sieve
    string QuestionSetHash,     // SHA-256(model ␟ verze sady ␟ jazyk ␟ kanonický JSON otázek)
    string StateHash,           // SHA-256(kanonický JSON stavu: věta a kontext, nebo text úseku)
    string LegacyKey);          // SHA-256(model ␟ verze ␟ jazyk ␟ otázky ␟ stav) = dnešní JevCacheKey.Create
```

`JevCacheKeys.Create(kind, model, version, language, questions, state)` vrátí všechny části. `SqliteJevCache` čte a píše jen `LegacyKey`, takže dnešní soubor cache zůstane platný. Test `JevCacheKeyCompatibilityTests` porovná `LegacyKey` všech segmentů testovacích e-shopů s referenčním seznamem zachyceným před změnou.

Vztah k databázi (změna 3): otisky jsou v knihovně hex řetězce SHA-256, v `checks.jev_answers` a `checks.sieve_answers` sloupce `bytea` (32 B); u druhu `Sieve` je `StateHash` = `chunk_hash`. Sloupec `probabilities real[]` unese jen odpovědi „ano/ne“ (`JevAnswer.Noul`) v pořadí id otázek sady; zapnutá pravidla jiné typy (`choice`, `score`) nepoužívají (proposal, K rozhodnutí 12).

### Otisk věty

`SentenceFingerprint.Of(string text)` = prvních 8 bajtů SHA-256 z `TextTools.NormalizeForHash(text)` jako `long` (big-endian). `Segment` dostane vlastnost `Fingerprint`; `SegmentResult` vrátí pro každou stránku seřazené unikátní otisky všech jejích segmentů (hlavní text, rám, ostatní text, titulek, meta popis, JSON-LD). Do výstupů CLI (`segments.csv`) se nepřidává, aby zůstaly stejné.

### Odhad a potvrzení

`EstimateStep` jen počítá (čte cache, nic neplatí) a vrátí `RunEstimate`. Callback `ScanOptions.ConfirmJevCalls` volá jen `InMemoryPipelineRunner` mezi `EstimateStep` a `ProfileStep.CreateAsync`, stejně jako dnes `EshopGuardService` (podmínka `asksConfirmation` beze změny). Worker uloží `RunEstimate` do `runs.estimate` a čeká na schválení (změna 8).

### Limit volání

`JevClient` a `OpenAiRewriteClient` dostanou `IRateLimiter` místo vlastního `TokenBucketRateLimiter`. `LocalRateLimiter` má pro `RateResource.Jev` stejné nastavení jako dnes (`JevOptions.RequestsPerMinute` rozložené po sekundách) a pro `RateResource.OpenAi` bez omezení (dnes také žádné; souběh drží `RewriteOptions.Concurrency`). `RequestPriority` (P0–P4) lokální implementace ignoruje, databázová ho použije pro vyhrazený podíl.

## Architecture Decisions

1. **Kroky jako služby s datovými smlouvami, ne jeden stavový objekt.** Worker potřebuje spustit libovolný krok na libovolném stroji po pádu jiného. Proto žádný stav mimo vstup a úložiště. Alternativa „uložit celý `ScanResult` a pokračovat“ zamítnuta: paměť by rostla s e-shopem.
2. **CLI běží stejné kroky v paměti.** Jediná cesta kódu znamená, že shoda CLI a workeru (Done when změny 8) se ověřuje porovnáním výstupů, ne dvou implementací.
3. **Kontrola SSRF v `ConnectCallback`, ne před požadavkem podle DNS.** Kontrola předem by šla obejít změnou DNS mezi kontrolou a připojením. Callback připojí přesně ověřenou adresu.
4. **Nedestruktivní čtení dokumentu místo kopií.** `IDocument.Clone(true)` by čtení jen přesunul. Množina vynechaných uzlů je levná a zachová pořadí bloků.
5. **`LegacyKey` v klíči cache.** Bez něj by změna znehodnotila 66 159 uložených odpovědí a první ostrý běh by platil znovu.
6. **Otisk věty bez nového balíčku.** SHA-256 už knihovna používá (`TextTools.Sha256`), 64 bitů stačí pro ~300 000 vět e-shopu (pravděpodobnost kolize zanedbatelná, neměřeno).
7. **Úložiště stránek odděleně od cache.** `IPageStore` nese stav e-shopu (stránky, verze), cache jen odpovědi služeb. Smazání cache tak nikdy nesmaže historii stránek.
8. **Nahrávání a přehrávání odpovědí v knihovně.** Je to `IPageFetcher` jako každý jiný, takže porovnání starého a nového kódu běží přes stejnou cestu jako ostrý sken.

## Data Flow

### CLI (`eshopguard scan`, v paměti)

```
ScanCommand
  └─ IEshopGuard.ScanSiteAsync  = InMemoryPipelineRunner
       1. YamlRuleSetProvider.LoadAsync            (pravidla se načtou první, chyba = nic se nestáhne)
       2. DiscoveryStep          → RobotsSnapshot, sitemap, UrlFrontierState
       3. smyčka: FetchStep (1 stránka) → ExtractStep → UrlFrontier.Consider(odkazy)   (pořadí jako dnes)
       4. ProfileStep.PlanAsync  → ProfilePlan (uložené profily už přiřazené v ExtractStep)
       5. SegmentStep            → SegmentResult
       6. EstimateStep           → RunEstimate → ScanOptions.ConfirmJevCalls (callback CLI)
       7. ProfileStep.CreateAsync + RefitAsync → SegmentStep znovu (jen když vznikly profily)
       8. SieveStep (všechny úseky) → EvaluateStep (všechny segmenty, include podle síta)
       9. RulesStep              → nálezy
      10. ScanResultAssembler    → ScanResult (stejný tvar jako dnes) → IReportWriter
```

### Worker (změna 8, po dávkách)

```
run.discover   DiscoveryStep                     → ops.domains (robots, sitemap), fronta URL, odhad počtu stránek
run.fetch      FetchStep (100 stránek / 60 s)    → IPageContentStore (HTML), IPageStore (validátory, stav), fronta URL; pokračování na konec fronty
run.extract    ExtractStep (100 stránek)         → IPageContentStore (extrakce), IPageStore (verze, otisky vět)
run.profile    ProfileStep.PlanAsync / CreateAsync / RefitAsync
run.segment    SegmentStep (celý e-shop)         → dávky SegmentState po 200
run.estimate   EstimateStep                      → runs.estimate, čekání na schválení
run.sieve      SieveStep (200 úseků)             → IJevCache (Sieve)
run.evaluate   EvaluateStep (200 vět)            → IJevCache (Detail)
run.rules      RulesStep (celý e-shop)           → nálezy
run.rewrite    RewriteStep (25 stránek)          → IRewriteCache, návrhy
```

Každý zápis má jedinečný klíč, takže opakovaná dávka nic nezdvojí. Pořadí stránek ve workeru se může od CLI lišit v režimu odkazů (dávky). Shoda se proto vyžaduje jen pro CLI; změna 8 porovnává nálezy jako množiny.

### Porovnání starého a nového kódu

```
(dnešní kód + RecordingPageFetcher)  scan https://vegis.sk --mock --record snapshots/vegis.sk      [síť, zdarma, souhlas]
(dnešní kód + ReplayPageFetcher)     scan https://vegis.sk --mock --replay snapshots/vegis.sk  → baselines/vegis.sk/*
(nový kód)                           scan https://vegis.sk --mock --replay snapshots/vegis.sk  → out/... 
PipelineEquivalenceTests             normalizace (bez časů) → porovnání souborů → rozdíl = selhání s výpisem
```

## File Changes

### Nové soubory (`src/EshopGuard.Core/`)

- `Pipeline/PipelineJson.cs`: `PipelineJson.Options`, `PipelineSchemaException`, konvertory pro `Uri` a `TextBlock`.
- `Pipeline/Contracts/SiteScope.cs`: `SiteScope` (`SiteUrl`, `SiteKey`), `CrawlLimits`.
- `Pipeline/Contracts/DiscoveryContracts.cs`: `DiscoveryInput`, `DiscoveryResult`, `RobotsSnapshot`, `SitemapEntry`.
- `Pipeline/Contracts/FetchContracts.cs`: `FetchBatchInput`, `FetchBatchResult`, `FetchedPage`, `FetchOutcome` (`Ok`, `NotModified`, `Failed`, `NotHtml`, `TooLarge`, `RedirectOffSite`, `RobotsBlocked`, `Blocked`), `ConditionalHeaders`, `PaceState`, `CrawlCounters`.
- `Pipeline/Contracts/ExtractContracts.cs`: `ExtractInput`, `ExtractedPageRecord`, `PageExtract` (serializovatelná podoba `ExtractedPage`), `ExtractionStatus`, `DiscoveredLink`, `ProfileFit`.
- `Pipeline/Contracts/ProfileContracts.cs`: `ProfilePlanInput`, `ProfileCandidate`, `ProfilePlan`, `PlannedProfile` (přesun z `Profiles/PageProfiler.cs`), `ProfileCreateResult`.
- `Pipeline/Contracts/SegmentContracts.cs`: `SegmentInput`, `SegmentResult`, `PageSegmentation`, `SieveChunkInput`, `SegmentState`.
- `Pipeline/Contracts/EvaluationContracts.cs`: `EstimateInput`, `RunEstimate`, `SieveBatchInput`, `SieveBatchResult`, `EvaluateBatchInput`, `EvaluateBatchResult`.
- `Pipeline/Contracts/RulesContracts.cs`: `RulesInput`.
- `Pipeline/UrlFrontier.cs`, `Pipeline/UrlFrontierState.cs`, `Pipeline/FrontierItem.cs`.
- `Pipeline/DiscoveryStep.cs`, `FetchStep.cs`, `ExtractStep.cs`, `ProfileStep.cs`, `SegmentStep.cs`, `EstimateStep.cs`, `SieveStep.cs`, `EvaluateStep.cs`, `RulesStep.cs`, `RewriteStep.cs`.
- `Pipeline/InMemoryPipelineRunner.cs`: implementace `IEshopGuard.ScanSiteAsync` a `AnalyzeTextsAsync` nad kroky.
- `Pipeline/ScanResultAssembler.cs`: `ScanResult` a `ScanStats` z výstupů kroků.
- `Extract/ParsedPage.cs`: jedno čtení dokumentu.
- `Crawl/FetchRequest.cs`.
- `Crawl/SsrfGuard.cs`, `Crawl/IHostAddressResolver.cs` (`DnsHostAddressResolver`).
- `Crawl/RecordingPageFetcher.cs`, `Crawl/ReplayPageFetcher.cs`, `Crawl/PageRecording.cs` (formát: `index.jsonl` + těla odpovědí `bodies/{sha256}.bin`).
- `Storage/IPageContentStore.cs`, `Storage/InMemoryPageContentStore.cs`, `Storage/PageContentKey.cs`.
- `Storage/IPageStore.cs`, `Storage/InMemoryPageStore.cs`, `Storage/PageRecord.cs`, `Storage/PageVersionRecord.cs`.
- `Storage/IUrlFrontierStore.cs`, `Storage/InMemoryUrlFrontierStore.cs`.
- `Storage/IRateLimiter.cs` (`RateResource`, `RequestPriority`), `Storage/LocalRateLimiter.cs`.
- `Segmentation/SentenceFingerprint.cs`.

### Měněné soubory

- `src/EshopGuard.Core/EshopGuardService.cs` (`EshopGuardService.cs`): smazán; logiku převezmou `InMemoryPipelineRunner`, kroky a `ScanResultAssembler`. `SelectRuleSets`, `NotLoadedWarnings`, `PageText`, `Signals`, `ImagesForReview` se přesunou do `RulesStep` a `ScanResultAssembler` beze změny výpočtu.
- `src/EshopGuard.Core/Crawl/Crawler.cs`: zbyde `CrawledPage` a pomocné metody (`IsHtml`, `LooksLikeHtml`, `DescribeFailure`, `ProductToken`); třída `Crawler` se rozpadne do `DiscoveryStep`, `FetchStep` a `UrlFrontier`.
- `src/EshopGuard.Core/Crawl/IPageFetcher.cs`: `FetchAsync(FetchRequest, ...)`, výchozí metoda pro `Uri`; `FetchResponse.ETag`, `LastModified`, `NotModified`.
- `src/EshopGuard.Core/Crawl/HttpPageFetcher.cs`: validátory v požadavku, 304, kontrola portu a schématu, `SsrfGuard`; čtení `ETag` a `Last-Modified`.
- `src/EshopGuard.Core/Crawl/SitemapParser.cs`: `Result` dostane `SitemapLocation(Url, LastModified?)` (dnes se `lastmod` zahazuje).
- `src/EshopGuard.Core/Extract/ContentExtractor.cs`: `Extract(Uri, ParsedPage)`, nedestruktivní `ReadRemainder` a `ExtractMain`.
- `src/EshopGuard.Core/Extract/ExtractedPage.cs`: převod na `PageExtract` a zpět.
- `src/EshopGuard.Core/Profiles/PageProfiler.cs`: rozdělí se do `ProfileStep`; `ProfilingRun` a `PageEntry` zmizí.
- `src/EshopGuard.Core/Profiles/ProfileMatcher.cs`: `ApplyNonDestructive`; `StructureTokens` nad `ParsedPage`.
- `src/EshopGuard.Core/Profiles/PageProfileStore.cs`: rozhraní `IPageProfileStore` přesunuto do `Storage/`, `SqlitePageProfileStore` zůstává.
- `src/EshopGuard.Core/Cache/IJevCache.cs`, `JevCacheKey.cs`, `SqliteJevCache.cs`: strukturovaný klíč, `GetManyAsync`, SQLite přes `LegacyKey`.
- `src/EshopGuard.Core/Rules/PageSieve.cs`, `Rules/SegmentEvaluator.cs`: klíč `JevCacheKey`, práce po dávkách (logika `Include`, `Group`, `Estimate` beze změny).
- `src/EshopGuard.Core/Jev/JevClient.cs`: `IRateLimiter` místo vlastního `TokenBucketRateLimiter`.
- `src/EshopGuard.Core/Fix/OpenAiRewriteClient.cs`: `IRateLimiter` (`RateResource.OpenAi`).
- `src/EshopGuard.Core/Options/EshopGuardOptions.cs` (`EshopGuardOptions.cs`): `CrawlOptions.AllowPrivateNetwork`, `ExtractTimeoutSeconds`, `FetchBatchMaxPages` (100), `FetchBatchMaxSeconds` (60); `JevOptions` beze změny.
- `src/EshopGuard.Core/Models/ScanResult.cs`: `NotProcessedPages`, `BlockedUrls` (SSRF); jinak beze změny.
- `src/EshopGuard.Core/ServiceCollectionExtensions.cs`: registrace kroků, `InMemoryPipelineRunner` jako `IEshopGuard`, výchozí úložiště přes `TryAdd`, `ConnectCallback` a `UseProxy = false` u klienta `EshopGuard.Crawl`.
- `src/EshopGuard.Cli/Commands/ScanCommand.cs`: volby `--record <dir>`, `--replay <dir>`, `--allow-private-network`.
- `src/EshopGuard.Cli/Commands/BenchExtractCommand.cs` (nový): `bench-extract --replay <dir>` změří čas procesoru extrakce a přiřazení profilu na stránku (`Process.TotalProcessorTime`, sestavení Release).
- `src/EshopGuard.Cli/CliHost.cs`: registrace `RecordingPageFetcher`/`ReplayPageFetcher` před knihovnou.
- `config/settings.yaml`: `crawl.extract_timeout_seconds`, `crawl.fetch_batch_max_pages`, `crawl.fetch_batch_max_seconds`.
- `.gitignore`: `snapshots/`, `baselines/`.
- `README.md`: nové volby CLI, ochrana SSRF, změřený čas procesoru.

### Testy (`tests/EshopGuard.Core.Tests/`)

- `Pipeline/PipelineEquivalenceTests.cs`: testovací e-shopy proti `Baselines/site/*`, `Baselines/site-sk/*`; nahrávky (kategorie `Snapshot`) proti `baselines/` mimo repozitář.
- `Pipeline/OutputNormalizer.cs`: vynechání časů, dob a tempa, stabilní pořadí.
- `Pipeline/FrontierResumeTests.cs`: přerušení po každé dávce a obnova z JSON.
- `Pipeline/StepContractSerializationTests.cs`: každý vstup a výstup projde JSON tam a zpět; neznámá `schema_version` → `PipelineSchemaException`.
- `Pipeline/EvaluateBatchIdempotenceTests.cs`.
- `Crawl/SsrfGuardTests.cs`, `Crawl/HttpPageFetcherSsrfTests.cs` (falešný `IHostAddressResolver`, místní `HttpListener` jen pro test povolené výjimky).
- `Crawl/ConditionalFetchTests.cs` (falešný server s ETag a 304).
- `Extract/SingleParseEquivalenceTests.cs`, `Extract/ExtractTimeoutTests.cs`.
- `Storage/StoreContractTests.cs` (abstraktní), `Storage/InMemoryStoreTests.cs`, `Storage/SqliteJevCacheContractTests.cs`.
- `Cache/JevCacheKeyCompatibilityTests.cs` + `Baselines/jev-legacy-keys.txt`.
- `Segmentation/SentenceFingerprintTests.cs`.
- `CoreDependencyTests.cs`.
- `Baselines/` (referenční výstupy testovacích e-shopů z dnešního kódu).
- Úprava `FileSystemPageFetcher.cs` (ETag a 304 pro testy podmíněného stažení) a `TestServices.cs` (nová úložiště).
