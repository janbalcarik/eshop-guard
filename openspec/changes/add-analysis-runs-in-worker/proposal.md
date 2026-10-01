# Proposal: Běhy ukázky zdarma a úvodní analýzy ve workeru

## Intent

**Problém.** Knihovna dnes umí celý sken jen jako jedno volání v paměti (`EshopGuardService.ScanSiteAsync`, po změně 5 `InMemoryPipelineRunner`): stáhne stránky, vytvoří profily, rozdělí text na věty, zeptá se Jevu, vyhodnotí pravidla a výsledek vrátí najednou. Potvrzení ceny je callback uprostřed skenu (`ScanOptions.ConfirmJevCalls`). Pro web to nestačí:
- pád procesu uprostřed skenu e-shopu s 5 000 stránkami zahodí celou práci i zaplacená volání Jevu;
- velký e-shop by zablokoval ostatní zákazníky;
- uživatel nevidí průběh a zaplacení analýzy nemá kde „počkat“;
- výsledky nejsou v databázi, takže je API (změny 10 a 11) nemá odkud číst.

**Proč teď.** Fronta s leasy (změna 4), knihovna rozdělená na kroky s datovými smlouvami (změna 5), verdikty po zemích (změna 6) a rozbor míst prodeje a jazykových verzí (změna 7) jsou hotové stavební kameny. Tato změna je spojí do dvou skutečných běhů, které prodejní tok potřebuje: **ukázka zdarma** a **úvodní analýza po zaplacení**.

**Přínos.**
- Prodejní tok „ukázka zdarma → zaplacení → úvodní analýza“ má hotové jádro, na které navážou API (10, 11) a platby (12).
- Pád workeru stojí nejvýš jednu dávku a nic se nezdvojí (nálezy, verze stránek ani spotřeba).
- Velký e-shop neblokuje malé: dávky různých zákazníků se ve frontě střídají.
- Co nebylo zkontrolováno, je vždy vyjmenované (stav `partial`), nic se tiše nevynechá.
- Interní náklady (Jev, OpenAI) jsou zapsané po dávkách v `usage.usage_records` a sedí s cenou, kterou spočítá CLI.

**Fáze:** F4 Běhy ve workeru.

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`: část 4 (Druhy kontrol a priority), část 5 (Worker a fronta, Úlohy po dávkách, Globální limity), část 6 (Průběh analýzy a schválení ceny), část 8 (Kapacita), část 12 (Místa prodeje, Jazykové verze, Rozbor verzí v ukázce zdarma, Cena rozhodnuto);
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.3 `content`, 3.4 `checks`, 3.5 `fixes`, 3.7 `usage`, 3.8 `ops`; část 5 (Paralelní workery), část 6 (Objem dat), část 8 fáze F4;
- `platby-a-fakturace-2026-10-01.md`: oddíl „Ukázka zdarma → úvodní analýza → sledování“;
- `strategie-a-cenik-2026-09-30.md`: řádek „Bezplatná kontrola“ (vzorek do 100 stránek, jednou na doménu, počty nálezů podle závažnosti, 5 nejzávažnějších s vysvětlením, 1 ukázka opravy);
- kód: `src/EshopGuard.Core/EshopGuardService.cs` (dnešní pořadí kroků), `Crawl/Crawler.cs`, `Profiles/PageProfiler.cs`, `Rules/PageSieve.cs`, `Rules/SegmentEvaluator.cs`, `Rules/RuleEngine.cs`, `Fix/PageRewriter.cs`;
- souběžné změny: `refactor-library-into-pipeline-steps/design.md` (kroky, úložiště, plán úloh workeru), `add-multi-jurisdiction-rules-and-rule-texts/design.md` (`VerdictOrder`), `add-shops-and-onboarding-api` (`ShopScopeCalculator`, `IShopOwnershipPolicy`), `add-billing-and-invoicing/design.md` (`IPriceQuoteService`, `MarkOrderPaidAsync`), `add-shoptet-connector/design.md` (běhy `connector_check` přes `IRunService`).

## Scope

In scope:
- Stavový automat `checks.runs.status`: `queued → discovering → awaiting_payment → crawling → profiling → segmenting → evaluating → ruling → rewriting → finished | partial | failed | canceled`, povolené přechody, zápis přechodu jako událost.
- `IRunService` / `RunService` v `EshopGuard.Jobs` (sdílí API i worker): `CreateFreeSampleAsync(shopId, requestedBy)`, `CreateFullAnalysisAsync`, `MarkOrderPaidAsync`, `ApproveWithoutPaymentAsync`, `RequestCancelAsync`.
- Založení ukázky zdarma (`runs.kind = free_sample`) jednou na doménu přes globální `shop.free_sample_claims` (funkce `SECURITY DEFINER`) v jedné transakci s během a první úlohou; odmítnutí kódem `sample.already_used_for_domain`.
- Rozdělení 100 stránek ukázky mezi jazykové verze podle plánu ze změny 7 a v ukázce rozbor míst prodeje a verzí (zápis do `shop.shop_markets`, `shop.shop_languages`).
- Výstup ukázky: počty nálezů podle závažnosti, 5 nejzávažnějších nálezů podle `VerdictOrder` (kódy a parametry, vysvětlení skládá API z textů pravidel), 1 ukázka opravy (`fixes.fix_proposals`).
- **Základ rozsahu z ukázky** v `runs.estimate.basis` (po verzích počty produktů a ostatních stránek, `counted` a důvod nezapočtení). Pásmo a cenu z něj počítají změna 10 (`ShopScopeCalculator`) a změna 12 (`IPriceQuoteService`).
- Úvodní analýza (`runs.kind = full_analysis`) celého e-shopu po dávkách přes frontu ze změny 4 s kroky změny 5: `run.discover` → `run.fetch` → `run.extract` → `run.profile` → `run.refit` → `run.segment` → `run.estimate` → `run.sieve` → `run.plan_evaluate` → `run.evaluate` → `run.rules` → `run.rewrite` → `run.finalize`.
- Odhad interních nákladů před každým placeným voláním a jeho uložení do `runs.estimate.internal`. Cena z ukázky je garantovaná (objednávka změny 12 nese snímek částek): při překročení se analýza dokončí bez zastavení a jen se zaznamená interní náklad a upozornění provozu.
- Stav `awaiting_payment` a pokračování po zaplacení objednávky (`IRunPaymentGate`) nebo po schválení administrátorem (pilot) se zápisem do `ops.audit_log`; ověření vlastnictví přes `IShopOwnershipPolicy` ze změny 10 při založení úvodní analýzy.
- Zápis `content.pages`, `content.page_versions` (včetně `segment_hashes` z `SentenceFingerprint`), `checks.findings` s `verdicts` po zemích, `checks.finding_occurrences`, `fixes.fix_proposals`, `usage.usage_records`, `checks.run_events` s `pg_notify` pro SSE.
- PostgreSQL implementace `IUrlFrontierStore` ze změny 5: nová tabulka `checks.run_urls` (uložená fronta URL běhu a stav každé URL) a chybějící jedinečné indexy pro idempotentní zápis.
- Pozice ve frontě a odhad dokončení (`IRunQueueEstimator`) pro obrazovku ukázky (změna 10).
- Idempotence každého kroku, odolnost proti pádu workeru, samooprava dočasných chyb opakováním v kroku, pozastavení při došlém kreditu nebo odmítnutém klíči.
- Stav `partial` s výčtem nezkontrolovaného podle důvodu (fail-closed); `failed`, když nejde zkontrolovat nic.
- Zrušení běhu mezi dávkami (`runs.cancel_requested`).
- Vývojový příkaz workeru pro založení běhu bez API (jen prostředí Development).
- Testy: celý běh nad testovacím e-shopem `Fixtures/site-sk`, zabití workeru, shoda s cestou CLI (`InMemoryPipelineRunner`), shoda `usage_records` s cenou CLI, worker nikdy nepovolí vnitřní síť (`CrawlOptions.AllowPrivateNetwork`, úkol ze změny 5).

Out of scope:
- Koncové body API (zakládání běhů, SSE, čtení nálezů): změny 10 a 11. Tady jen knihovní služby, které API zavolá.
- Výpočet rozsahu, pásma a ceny: `ShopScopeCalculator` (změna 10) a `IPriceQuoteService` (změna 12). Tady jen základ v `runs.estimate.basis`.
- Platby, objednávky, Stripe a vrácení peněz při zrušení: změna 12. Tady jen `IRunPaymentGate` a `MarkOrderPaidAsync`.
- Hromadné opravy (`fixes.fix_groups`) a oprava mezery `PageRewriter.BuildWork` (přepis opakovaného nálezu jen na první stránce, ostatní v `AlsoOn`): K rozhodnutí 9.
- Noční sledování, rozdíl sitemap, rotace, paměť rozhodnutí: změna 16.
- Konektory a texty z API e-shopu: změna 15. Zpracování běhů `connector_check`: K rozhodnutí 15.
- E-mailová infrastruktura (`OutboxEmailDispatcher`, šablony): změna 9. Tady se jen zapíše řádek do `ops.outbox`.
- Protokol PDF, otázky na doklady (`checks.questions`): změna 11.
- Vykreslení v Chromiu: mimo plán (návrh rozvoje 21).

## Approach

1. **Logika v knihovnách, worker tenký.** Stavový automat, plán kroků, odhady, výběr ukázky a zápis výsledků jsou v `EshopGuard.Jobs` (sdílí API i worker) a `EshopGuard.Data`. Samotné kroky (`DiscoveryStep`, `FetchStep`, `ExtractStep`, `ProfileStep`, `SegmentStep`, `EstimateStep`, `SieveStep`, `EvaluateStep`, `RulesStep`, `RewriteStep`) jsou čisté služby knihovny `EshopGuard.Core.Pipeline` ze změny 5. `EshopGuard.Worker` jen registruje obsluhy úloh a sloty.
2. **Běh = řetěz malých úloh.** Každá úloha zpracuje jednu dávku (do 100 stránek nebo 60 s u stahování, do 100 stránek u extrakce, 200 vět u Jevu, 25 stránek u přepisu), výsledek zapíše s jedinečným klíčem a zařadí pokračování na konec fronty. Celoplošné kroky (`run.segment`, `run.estimate`, `run.plan_evaluate`, `run.rules`, `run.finalize`) mají `concurrency_key = run:{id}:{krok}`, takže běží jednou.
3. **Mezivýsledky v úložišti, ne v paměti.** HTML a extrakce jdou přes `IPageContentStore` (změna 5), pracovní dávky běhu do `IBlobStore` pod `tenants/{tenant}/shops/{shop}/runs/{run}/`. Paměť workeru nezávisí na velikosti e-shopu, kromě celoplošných kroků (viz design).
4. **Ukázka zdarma** běží stejnými obsluhami jako úvodní analýza, jen s plánem 100 stránek ze změny 7, extrakcí přímo ve stahování (`FetchBatchInput.ExtractInline = true`, změna 5), bez čekání na platbu, s vlastním stropem souběhu a se stropem interních nákladů ze `system_settings`. Na konci uloží souhrn ukázky a základ rozsahu pro cenu.
5. **Úvodní analýza** nejdřív zjistí rozsah (P0, rychle), uloží interní odhad a čeká ve stavu `awaiting_payment`. Stahování se založí až po zaplacení nebo schválení (architektura, část 6).
6. **Idempotence.** Zápis s jedinečnými klíči (běh a stránka, běh a dávka, tenant a klíč cache Jevu), dokončení úlohy jen s vlastním leasem a číslem pokusu (změna 4), průběh se mění jen přičítáním ve stejné transakci jako výsledek dávky. Odpovědi Jevu a OpenAI se ukládají do cache tenanta průběžně, takže opakovaná dávka platí jen volání, která byla v letu.
7. **Fail-closed.** Každá stránka nebo věta, která měla být zkontrolována a nebyla, má v `checks.run_urls` nebo ve statistice běhu důvod. Běh s čímkoli nezkontrolovaným (mimo robots.txt a filtr URL, které se vyjmenují také) skončí `partial`, nikdy `finished`.

## Dependencies

- **Změna 3 `add-multitenant-data-model`:** tabulky `checks.runs`, `checks.run_events`, `content.pages`, `content.page_versions`, `checks.findings`, `checks.finding_occurrences`, `fixes.fix_proposals`, `usage.usage_records`, `shop.free_sample_claims`, `shop.shop_markets`, `shop.shop_languages`, RLS, `ITenantContext`.
- **Změna 4 `add-job-queue-and-worker`:** `IJobQueue`, `IWorkerStore`, `ops.jobs` (leasy, `dedupe_key`, `concurrency_key`, `resource_class`, `not_before`), sloty po druzích, `ops.domains` (zámky domén), `ops.rate_limit_buckets`, pozastavení druhu úloh při fatální chybě služby, strop souběžných analýz na tenanta.
- **Změna 5 `refactor-library-into-pipeline-steps`:** kroky `EshopGuard.Core.Pipeline` a jejich smlouvy, `UrlFrontier` a `UrlFrontierState`, `InMemoryPipelineRunner`, rozhraní `IPageContentStore`, `IPageStore`, `IUrlFrontierStore`, `IJevCache` s `JevCacheKey` (`Kind`, `QuestionSetHash`, `StateHash`, `LegacyKey`), `IRewriteCache`, `IPageProfileStore`, `IRateLimiter`, `SentenceFingerprint`, podmíněné stažení, ochrana proti SSRF, časový limit extrakce.
- **Změna 6 `add-multi-jurisdiction-rules-and-rule-texts`:** vyhodnocení pro víc jurisdikcí, `Finding.Verdicts`, `Finding.Strictest`, `VerdictOrder.Compare`, kódy a parametry místo hotových vět.
- **Změna 7 `add-places-of-sale-and-language-versions`:** rozbor míst prodeje (technické znaky + LLM s ověřenými citacemi), nalezení a přepnutí jazykových verzí, plán 100 stránek ukázky, porovnání verzí (podíl vlastních textů, `counted`).
- **Změna 10 `add-shops-and-onboarding-api`:** `ShopScopeCalculator` (jurisdikce a moduly běhu) a `ShopOwnershipPolicy` (ověření vlastnictví před úvodní analýzou). Změna 10 je v pořadí až po této změně a sama volá `RunService`; proto tato změna používá jen rozhraní `IRunScopeResolver` a `IShopOwnershipPolicy` v `EshopGuard.Jobs` a implementace dodá změna 10 (K rozhodnutí 16).
- Navazují: změna 10 (zakládání ukázky, `SampleDto`, pozice ve frontě), 11 (nálezy a opravy), 12 (`IPriceQuoteService` čte základ, Stripe webhook volá `MarkOrderPaidAsync`), 15 (běhy `connector_check` přes `IRunService`), 16 (sledování staví na zapsaných `pages` a `page_versions`).

## Done when

- Úvodní analýza vegis.sk přes workery dá stejné nálezy jako cesta CLI (`InMemoryPipelineRunner`) nad stejným snímkem HTML a stejnými odpověďmi Jevu (porovnání jako množiny podle pravidla, otisku textu, rozsahu, množiny URL, verdiktů a skóre).
- Zabití workeru uprostřed každého druhu dávky (stahování, extrakce, Jev, pravidla, přepis) nic nezdvojí: počty řádků `pages`, `page_versions`, `findings`, `finding_occurrences`, `fix_proposals` jsou stejné jako v běhu bez pádu a součet `usage_records.calls` se liší nejvýš o volání v letu.
- `usage_records` sedí s cenou z CLI v běhu bez pádu: součty volání a tokenů po službách se rovnají přesně a cena spočítaná ze součtu tokenů se rovná `ScanStats.EstimatedCostUsd`, `ScanStats.ProfileCostUsd` a `RewriteStats.CostUsd` + `RewriteStats.CheckCostUsd` na 6 desetinných míst (zaokrouhlení jednotlivých záznamů viz K rozhodnutí 5).
- Ukázka zdarma nad `Fixtures/site-sk` vrátí počty podle závažnosti, 5 nejzávažnějších nálezů, 1 ukázku opravy a základ rozsahu; druhý nárok na stejnou doménu je odmítnut.
- Běh, ve kterém část stránek nejde stáhnout, skončí `partial` s výčtem nezkontrolovaných URL podle důvodu.
- `dotnet test` projde včetně nových testů v `EshopGuard.Jobs.Tests` a `EshopGuard.Worker.Tests`.

## K rozhodnutí

1. **Rozpor mezi částí 6 a částí 12 architektury.** Část 6 říká, že když odhad interních nákladů překročí stanovený podíl ceny, běh se zastaví. Část 12 (rozhodnuto 1. 10. 2026) a zadání této změny říkají, že cena z ukázky je garantovaná a analýza se dokončí bez zastavení. Změna se řídí částí 12: nezastavuje, zapíše interní náklad a pošle upozornění provozu. Potvrdit, že věta v části 6 neplatí, a opravit ji.
2. **Strop interních nákladů ukázky zdarma** (`system_settings.free_sample.max_internal_usd`). Strategie uvádí náklad ~0,5–1 USD na jednu ukázku. Výchozí hodnota stropu není rozhodnutá. Nad stropem se placené kroky ukázky nespustí a ukázka skončí `failed` s kódem `sample_budget_exceeded` (fail-closed). Potvrdit hodnotu i chování.
3. **Identita nálezu a otisk věty.** Knihovna má `Segment.Hash` = SHA-256 druhu, věty a kontextu; změna 5 přidává `Segment.Fingerprint` = `SentenceFingerprint.Of(text)` (64 bitů, bez kontextu), který jde do `page_versions.segment_hashes`. Databáze má `findings` jedinečné podle (`shop_id`, `rule_id`, `segment_hash`). Návrh: `findings.segment_hash` = `Fingerprint`, aby úprava sousední věty nezaložila nový nález a nezahodila jeho stav. Stejná věta s různým kontextem pak dá jeden nález; výskyty jen ze stránek, kde pravidlo nález skutečně dalo, verdikty a skóre nejpřísnější. To je sloučení, proto potřebuje souhlas. Alternativa: nález po kontextu (víc řádků, stav se ztratí při změně souseda). Do rozhodnutí design počítá s návrhem.
4. **Nová tabulka `checks.run_urls`.** Změna 5 ji uvádí jako úložiště `IUrlFrontierStore` („změna 8 navrhuje“), databázový návrh ji nemá. Bez ní nejde pokračovat po pádu ani vyjmenovat nezkontrolované stránky. Potvrdit název a schéma.
5. **Chybějící jedinečné indexy a hodnoty výčtů** pro idempotentní zápis:
   - `page_versions` (`shop_id`, `page_id`, `run_id`), `findings` (`shop_id`, `rule_id`, `page_id`) WHERE `scope='page'` a (`shop_id`, `rule_id`) WHERE `scope='site'`, `fix_proposals` (`shop_id`, `created_run_id`, `page_id`, `field`, `block_index`);
   - `usage_records.operation` nemá hodnoty pro rozbor míst prodeje a jazyka verzí (změna 7): návrh `market_analysis` a `version_language`;
   - `usage_records.cost_usd numeric(14,6)` zaokrouhluje cenu jednoho záznamu (Jev stojí 0,042 USD za milion tokenů, tedy 4,2·10⁻⁸ USD za token). Součet zaokrouhlených záznamů se proto od ceny CLI liší nejvýš o 0,000001 USD na záznam. Návrh: shodu ověřovat přes součet tokenů (přesně) a cenu spočítanou ze součtu; nebo rozšířit sloupec na `numeric(18,10)`.

   Doplní je migrace této změny, pokud je nemá změna 3.
6. **Nárok na ukázku zdarma.**
   - Má nárok zablokovat i domény jazykových verzí (např. goodie.sk a goodie.cz), nebo jen hlavní doménu?
   - Smí stejný e-shop dostat novou ukázku, když první skončila `failed` naší chybou? Do rozhodnutí: nárok zůstává, celý běh se automaticky neopakuje, provoz může nárok uvolnit ručně se zápisem do auditu.
   - E-shop bez ukázky (doménu už nárokoval jiný tenant) nemá základ rozsahu; změna 10 do rozhodnutí vrací `quote.basis_missing` a objednávka není možná (fail-closed). Dřívější návrh této změny (zjištění rozsahu bez ukázky, pásmo jen podle hlavní verze) čeká na rozhodnutí.
7. **Hranice `finished` a `partial`.** Návrh: `partial`, když nebyla zkontrolována kterákoli plánovaná stránka nebo věta z jiného důvodu než robots.txt a filtr URL (tedy i stránka s textem vykreslovaným JavaScriptem, `text_not_loaded`). Stránky zakázané v robots.txt se vyjmenují i u `finished`.
8. **Jak dlouho čekat na nedostupný e-shop** (`system_settings.runs.site_outage_max_hours`), než běh skončí `partial`. Hodnota není rozhodnutá (návrh 24 h).
9. **Hromadné opravy.** `PageRewriter.BuildWork` dnes přepíše opakovaný nález jen na první stránce. Úvodní analýza tak dá návrh jen tam. Který krok plánu postaví `fix_groups` a skupinové náhrady (návrh rozvoje 1, varianta b)? Změna 11 je API, změna 8 jen spouští dnešní přepis. Návrh: samostatná změna mezi 8 a 11.
10. **Kde čeká úvodní analýza na schválení.** Architektura (část 6) a stavový automat: po zjištění rozsahu, před stahováním. Změna 5 v plánu úloh workeru uvádí „`run.estimate` → čekání na schválení“, tedy až po stažení a segmentaci. Tato změna se řídí architekturou a `run.estimate` jen ukládá interní odhad. Sladit text změny 5.
11. **Živé ověření na vegis je placené.** Jeden běh celého vegis.sk (5 872 URL v sitemap) stojí podle extrapolace ~5,5 USD na 1 000 stránek asi 32 USD (na celém webu neměřeno). Návrh postupu tak, aby se Jev platil jen jednou: běh workeru se zaplatí, cesta CLI pak čte stejný snímek HTML z úložiště a stejné odpovědi z cache tenanta. Alternativa: srovnání na 500 stránkách (~2,75 USD). Před spuštěním odhad ceny a souhlas.
12. **Zrušení zaplacené analýzy:** vrácení peněz řeší změna 12. Tady běh jen skončí `canceled` a výsledky do té doby zůstanou.
13. **Názvy ze změny 7** (`MarketAnalyzer`, `SampleAllocator`, `VersionComparer`) jsou v této změně pracovní, změna 7 je zatím bez návrhu. Sladit při kontrole plánu. Názvy ze změn 5, 6, 10, 12 a 15 jsou převzaté.
14. **Sloupec `run_events.message`:** API vrací kódy a parametry, text skládá frontend. Návrh: `message` se pro uživatelské události nevyplňuje a slouží jen provozu (bez textů stránek).
15. **Běhy `connector_check`.** Změna 15 zakládá přes `IRunService` běhy `connector_check` s texty jako `TextInput` (priorita 1, skrytý produkt 0). Tato změna dodá `IRunService.CreateConnectorCheckAsync` a stavový automat, ale obsluhu kroků nad texty z konektoru (bez stahování, `AnalyzeTextsAsync` po dávkách) neplánuje. Rozhodnout, zda patří sem, do změny 15, nebo do 16.
16. **Cyklus závislostí se změnou 10.** Změna 10 umisťuje `ShopScopeCalculator` a `IShopOwnershipPolicy` do `EshopGuard.Application` a sama volá `RunService` ze změny 8; kdyby `EshopGuard.Jobs` odkazoval na `EshopGuard.Application`, vznikne cyklus projektů i pořadí změn. Návrh: rozhraní `IRunScopeResolver` (jurisdikce a moduly běhu) a `IShopOwnershipPolicy` patří do `EshopGuard.Jobs`, implementace do `EshopGuard.Application` (změna 10), hostitelé API a workeru je zaregistrují. Do hotové změny 10 platí v testech `TestRunScopeResolver` (aktivní `shop_markets` a zapnuté moduly) a v hostiteli `DenyAllOwnershipPolicy` (úvodní analýza bez implementace se nezaloží, fail-closed). Alternativa: `ShopScopeCalculator` jako čistá funkce do `EshopGuard.Core`. Sladit se změnou 10.
17. **Velikost změny.** Změna má víc úkolů, než je v plánu obvyklé, hlavně kvůli PostgreSQL implementacím úložišť, které jí přenechala změna 5 (její K rozhodnutí 1). Návrh: pokud se to potvrdí, oddělit skupinu 1 (úložiště a jejich testy chování) jako samostatnou změnu mezi 5 a 8.
