# Tasks

## 1. Migrace a úložiště dat

- [ ] 1.1 `src/EshopGuard.Data/Migrations/Sql/change_monitoring.sql`: `content.pages.sitemap_lastmod timestamptz`, indexy (`shop_id`, `rotation_bucket`) a (`shop_id`, `next_check_at`) WHERE `status = 'active'`.
- [ ] 1.2 Tamtéž: `checks.run_urls.reason text` s `CHECK` (`sitemap_new`, `sitemap_lastmod`, `sitemap_removed`, `home`, `legal`, `template_sample`, `rotation`, `retry`, `overdue`) a rozšíření `state` o `not_modified`.
- [ ] 1.3 Tamtéž: `ops.schedules` PK (`shop_id`, `kind`) (K rozhodnutí 3); `fixes.decision_memory.rule_id text` a `rule_set_id uuid` (K rozhodnutí 4).
- [ ] 1.4 Tamtéž: `iam.notifications.dedupe_key` a `ops.outbox.dedupe_key` s jedinečným indexem (null povolen), jedinečný index `checks.page_changes (run_id, page_id, change_kind)` (K rozhodnutí 19).
- [ ] 1.5 Tamtéž: doplnění `pages.rotation_bucket = RotationBucket.Of(id)` stávajícím stránkám a `shops.monitor_slot_minute` sledovaným e-shopům (dávkami po 10 000 řádcích).
- [ ] 1.6 `src/EshopGuard.Data/Migrations/20261201_ChangeMonitoring.cs`: EF migrace spouštějící SQL z 1.1–1.5; úpravy entit `Page`, `RunUrl`, `Schedule`, `DecisionMemory`, `Notification`, `OutboxMessage`.
- [ ] 1.7 `src/EshopGuard.Data/Stores/ScheduleStore.cs`, `DecisionMemoryStore.cs`, `PageChangeStore.cs`, `NotificationStore.cs`, `OutboxStore.cs` (`ON CONFLICT (dedupe_key) DO NOTHING`).
- [ ] 1.8 `src/EshopGuard.Data/Stores/MonitoringPageQueries.cs`: stránky noci podle `rotation_bucket`, `page_type`, `next_check_at`, `last_fetched_at`; počet aktuálních verzí s otiskem věty přes GIN (`segment_hashes @> ARRAY[@hash]`).
- [ ] 1.9 Test `tests/EshopGuard.Data.Tests/MonitoringIsolationTests.cs`: dva tenanti se stejnou doménou; `decision_memory`, `page_changes`, `notifications`, `schedules` přes EF i čistým SQL jako `eshopguard_app` vrací jen vlastní řádky.

## 2. Čistá logika v knihovně (`EshopGuard.Core/Monitoring`)

- [ ] 2.1 `MonitorSlot.cs`: stabilní hash (prvních 8 bajtů SHA-256 z bajtů `Guid`) % (`window` − `reserve`), převod místního času `Europe/Bratislava` na UTC, posun minuty v neexistující hodině.
- [ ] 2.2 `RotationBucket.cs`: `Of(pageId)` (0–6) a `ForNight(DateOnly)` = `DayNumber % 7`.
- [ ] 2.3 `SitemapDiff.cs`: vstup minulý snímek (`url_hash` → `lastmod`) a aktuální položky; výstup nové, zmizelé, se změněným `lastmod`, nezměněné; nedostupná sitemapa = žádný rozdíl.
- [ ] 2.4 `NightPlanBuilder.cs` a `NightPlanReason.cs`: spojení zdrojů podle tabulky v designu bez duplicit (u URL s více důvody zůstává první podle pořadí tabulky), plány `nightly`, `connector_retry` (jen stránky k opakování) a `weekly_web`.
- [ ] 2.5 `SentenceDelta.cs`: přidané, odebrané a zachované otisky vět mezi verzemi stránky.
- [ ] 2.6 `DecisionMemoryMatcher.cs`: výsledky `apply_replacement`, `keep`, `keep_with_evidence`, `evidence_expired`, `stale_rule_set`, `none`; konkrétnější záznam (e-shop) má přednost před záznamem tenanta.
- [ ] 2.7 `MarketSignalDetector.cs`: nové jazykové verze, domény verzí, měny a země z technických znaků změny 7 proti `shop_markets` a `shop_languages`; síla důkazu; odmítnuté trhy se stejnou nebo slabší silou se nevrací. Žádné seznamy klíčových slov.
- [ ] 2.8 Testy `tests/EshopGuard.Core.Tests/Monitoring/MonitorSlotTests.cs` (stabilita mezi procesy přes pevné ID, interval, noc 25. 10. 2026 a noc posunu vpřed), `RotationBucketTests.cs` (rovnoměrnost na 5 000 ID ±10 %, každá stránka v 7 nocích), `SitemapDiffTests.cs`, `NightPlanBuilderTests.cs`, `SentenceDeltaTests.cs`, `DecisionMemoryMatcherTests.cs`, `MarketSignalDetectorTests.cs`.

## 3. Plánovač a noční okno

- [ ] 3.1 `src/EshopGuard.Jobs/Monitoring/MonitoringOptions.cs` a `system_settings` klíče `monitoring.*` (okno, rezerva, časové pásmo, `max_failed_nights`, `alert_usd_per_shop_night`, `market_check_days`, `overlap_retry_minutes`, `frequency_by_tier`).
- [ ] 3.2 `src/EshopGuard.Jobs/Monitoring/MonitorScheduler.cs` (`IScheduledTask`, každou minutu, jen se zámkem plánovače): výběr `ops.schedules` s `next_run_at <= now()`, kontrola `shops.status`, `last_full_run_id` a `billing.subscriptions.status`, založení běhu `monitoring` a úlohy `monitor.plan` s `dedupe_key monitor:{shop}:{místní datum}` v jedné transakci, posun `next_run_at`.
- [ ] 3.3 Tamtéž: po výpadku jen poslední zmeškaná noc; e-shop s konektorem dostane noční běh jen se stránkami k opakování (`connector_retry`), nebo plný noční běh, když změna 15 hlásí nezdravý konektor (`connectors.health`); řádek `weekly_web` pro e-shopy s konektorem; aktivace sledování (`shops.monitor_slot_minute`, řádky `ops.schedules`) po dokončení úvodní analýzy.
- [ ] 3.4 `src/EshopGuard.Jobs/Monitoring/MonitorWindowWatchdog.cs`: v konci okna najde nedokončené běhy noci, zapíše `stats.finished_after_window`, metriku `eshopguard.monitoring.window_missed`; běhy neruší.
- [ ] 3.5 `MonitorPlanHandler`: odložení přes `not_before`, když e-shop má jiný běh `monitoring` v nekonečném stavu.
- [ ] 3.6 Test `tests/EshopGuard.Jobs.Tests/Monitoring/MonitorSchedulerTests.cs` (simulovaný čas): dvě instance a restart dají jeden běh na e-shop a datum; e-shop se zrušeným předplatným a e-shop bez úvodní analýzy se nesledují; výpadek dvou nocí dá jeden běh; noc změny času dá jeden běh.
- [ ] 3.7 Test `tests/EshopGuard.Jobs.Tests/Monitoring/WindowWatchdogTests.cs`: běh nedokončený v 6:00 doběhne, má `finished_after_window` a metriku; překrývající se noc čeká na předchozí.

## 4. Plán noci a podmíněné stažení

- [ ] 4.1 `src/EshopGuard.Jobs/Monitoring/Handlers/MonitorPlanHandler.cs` (`monitor.plan`, třída `fetch`, P3): podmíněné stažení sitemap se zámkem domény, snímek do `tenants/{t}/shops/{s}/sitemaps/{datum}.json.gz`, `NightPlanBuilder`, zápis `run_urls` s `reason`, událost `monitor.plan` s počty podle důvodů.
- [ ] 4.2 `src/EshopGuard.Jobs/Monitoring/DeltaMode.cs` a úprava `FetchBatchHandler` ze změny 8: `If-None-Match`, `If-Modified-Since`, stav `not_modified` bez extrakce, `next_check_at` po chybě, ověření zmizelé URL (404/410 → `gone`, jiná chyba → `retry`), aktualizace `sitemap_lastmod` až v `run.finalize` úspěšného běhu.
- [ ] 4.3 Úprava `ExtractBatchHandler`: nové stránky dostanou `rotation_bucket`; stejný `text_hash` → žádná verze; jiný → nová verze a `page_changes (text_changed)`.
- [ ] 4.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/NightRunTests.cs`: rozdíl sitemap (12 nových, 30 `lastmod`, 3 zmizelé) dá správné důvody; 7 nocí pokryje všechny stránky; úvodní, právní a šablonové stránky každou noc; nedostupná sitemapa nic neoznačí za zmizelé a uvede `sitemap_unavailable`.
- [ ] 4.5 Test tamtéž: 304 nespustí extrakci ani Jev; server bez `ETag` se stejným textem nevytvoří verzi; 410 → `gone` a odebrané výskyty; 500 → stránka zůstane s nálezy a `next_check_at`.

## 5. Kontrola jen změněných vět

- [ ] 5.1 Úprava `SegmentHandler` (změna 8) v rozdílovém režimu: segmenty jen ze změněných verzí, `SentenceDelta`, příznak rámce webu z počtu aktuálních verzí přes GIN, dávky jen pro stavy bez odpovědi v `jev_answers`.
- [ ] 5.2 Úprava `RulesHandler` (změna 8): `RuleScope.Delta` (pravidla na větách jen pro věty změněných stránek, za stránku jen změněné stránky, za celý web jen při změně úvodní nebo právní stránky) a `RuleScope.Full`.
- [ ] 5.3 `src/EshopGuard.Jobs/Monitoring/MonitoringChangesWriter.cs`: přepočet výskytů změněných stránek, `resolved` nálezy bez výskytu, `page_changes.result` (`new_violation` podle nejpřísnějšího verdiktu `text`, `new_assess`, `fix_confirmed` při zveřejněné opravě, `ok`).
- [ ] 5.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/ChangedSentencesOnlyTests.cs`: změna jedné věty ze 40 → nejvýš 5 stavů pro Jev; 3 změněné stránky z 500 → ostatní nálezy beze změny; zmizelá věta se zveřejněnou opravou → `resolved` a `fix_confirmed`.
- [ ] 5.5 Test `tests/EshopGuard.Worker.Tests/Monitoring/DeltaRulesEquivalenceTests.cs`: 10 nocí s náhodnými změnami (pevné semínko); nálezy a výskyty po rozdílových během = plné vyhodnocení nad stejnými daty.

## 6. Paměť rozhodnutí

- [ ] 6.1 `src/EshopGuard.Jobs/Monitoring/DecisionMemoryWriter.cs`: `RecordAsync` z rozhodnutí obchodníka (přijetí nebo úprava návrhu → `replace`, ponechat → `keep`, ponechat s dokladem → `keep_with_evidence`) s `rule_id`, `rule_set_id`, `normalized_text`, `segment_hash`; starší záznam stejného otisku `superseded_at`; napojení do obsluh rozhodnutí změny 11 (K rozhodnutí 7).
- [ ] 6.2 `src/EshopGuard.Jobs/Monitoring/Handlers/MemoryHandler.cs` (`monitor.memory`, třída `jev`, před `run.rewrite`): `DecisionMemoryMatcher` nad novými a znovu nalezenými nálezy, `fix_proposals` s `model = decision_memory` bez OpenAI, kontrola náhrady Jevem v okolí stránky (`operation = recheck`), stavy `kept`, `kept_with_evidence` s `evidence_links`, `needs_answer` při propadlém dokladu, `open` při jiné verzi pravidel.
- [ ] 6.3 Úprava `RewriteBatchHandler` (změna 8) v rozdílovém režimu: LLM jen pro nálezy bez výsledku paměti a podle K rozhodnutí 8 (do rozhodnutí jen nové nálezy skupiny porušení).
- [ ] 6.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/DecisionMemoryTests.cs`: stejná věta na novém produktu → návrh z paměti, 0 volání `DeterministicTestRewriteClient`, 1 kontrola Jevem; ponechat → `kept` bez otázky; propadlý doklad → `needs_answer`; jiná verze sady `eco` → paměť nepoužita; náhrada neprojde v novém okolí → `still_finding`.

## 7. Nové místo prodeje a jazyková verze

- [ ] 7.1 `src/EshopGuard.Jobs/Monitoring/Handlers/MonitorSignalsHandler.cs` (`monitor.signals`, třída `cpu`): technické znaky úvodní a právních stránek, `MarketSignalDetector`, zápis `shop_languages (needs_confirmation)` a `shop_markets (unsupported)`, založení `monitor.market_check` u silného znaku podporovaného trhu (nejvýš jednou za `market_check_days`).
- [ ] 7.2 `src/EshopGuard.Jobs/Monitoring/Handlers/MarketCheckHandler.cs` (`monitor.market_check`, třída `llm`, P3): odhad ceny do `runs.estimate.internal` před voláním, rozbor změny 7 s ověřenými citacemi, `shop_markets (suggested, detected, evidence, detection_run_id)`, upozornění `market_suggested`; neověřená citace se nepoužije.
- [ ] 7.3 Úprava `MonitorScheduler`: `runs.jurisdictions` jen z `shop_markets.status = active`; verze `needs_confirmation` se nekontrolují a jdou do `unchecked.unconfirmed_language_versions`.
- [ ] 7.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/NewMarketSignalTests.cs`: nová `hreflang="cs"` → `language_suggested`, verze nekontrolovaná; PLN a `hreflang="pl"` → `unsupported` bez upozornění a bez LLM; Kč a doména `.cz` → `market_suggested` a jurisdikce dalšího běhu jen `sk`; odmítnutý `cz` se stejným důkazem → nic.

## 8. Změny z konektoru (napojení na změnu 15)

- [ ] 8.1 `src/EshopGuard.Jobs/Monitoring/ConnectorChangeProcessor.cs`: běhy `connector_check` změny 15 (P1 i P0) projdou `SentenceDelta`, `monitor.memory` s prioritou běhu, `MonitoringChangesWriter` (výsledek u `page_changes` se `source = webhook | save_hidden`) a `MonitoringNotifier`; u `save_hidden` upozornění hned po běhu.
- [ ] 8.2 `MonitorScheduler`: běh `weekly_web` jednou týdně (den = hash % 7) se všemi aktivními stránkami podmíněně; po události změny šablony ze změny 15 naplánovat `weekly_web` na nejbližší noc (`dedupe_key weekly_web:{shop}:{datum}`).
- [ ] 8.3 Sladit se změnou 15 (K rozhodnutí 9 a 21): kdo obsluhuje kroky běhu `connector_check` nad texty z API a zda noční `connector.reconcile_changes` a `connector.sync_content` poběží v `shops.monitor_slot_minute`; výsledek zapsat do obou změn před implementací.
- [ ] 8.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/ConnectorChangeTests.cs` (testovací konektor ze změny 15): změna produktu → Jev jen pro změněné věty a výsledek v `page_changes (webhook)`; týden se zdravým konektorem → 1 `weekly_web` a žádný noční běh webu; uložený skrytý produkt → upozornění hned po běhu P0; nezdravý konektor → noční běh webu, po obnovení zase vypnutý.

## 9. Priority a náklady

- [ ] 9.1 `src/EshopGuard.Jobs/Monitoring/MonitoringPriorities.cs`: priority a třídy podle tabulky v designu, použité ve všech obsluhách `monitor.*` a v `RunPlan` pro `monitoring`.
- [ ] 9.2 Úprava `InternalCostEstimator` (změna 8) pro rozdílový režim; uložení odhadu před první dávkou síta nebo Jevu; metrika `eshopguard.monitoring.cost_over_threshold` nad `alert_usd_per_shop_night`; měsíční součet změněných stránek na e-shop do `runs.stats` pro férové užití.
- [ ] 9.3 Test `tests/EshopGuard.Worker.Tests/Monitoring/MonitoringPriorityTests.cs`: běh `connector_check` s prioritou 0 a jeho `monitor.memory` s plnou frontou P3 hotové do 30 s; noční dávky nerezervují víc než 80 % limitu Jevu; P4 souhrn až po P3.
- [ ] 9.4 Test `tests/EshopGuard.Worker.Tests/Monitoring/NightCostTests.cs`: odhad uložený před prvním voláním; hromadný přepis 3 000 popisů → metrika, běh zkontroluje vše; vše z cache a paměti → OpenAI `calls = 0`.

## 10. Upozornění a týdenní souhrn

- [ ] 10.1 `src/EshopGuard.Jobs/Monitoring/MonitoringNotifier.cs`: jedno upozornění `new_violation` na běh (`dedupe_key new_violation:{run}`), e-mail podle `email_new_violation` v `users.locale`; `monitoring_unchecked` po `max_failed_nights`; `market_suggested`, `language_suggested`.
- [ ] 10.2 `src/EshopGuard.Jobs/Monitoring/WeeklySummaryBuilder.cs` a `src/EshopGuard.Core/Monitoring/WeeklySummary.cs`: obsah podle designu z `runs`, `page_changes`, `findings`, `fix_proposals`, `run_urls` a `shop_markets` za týden; i týden bez změn.
- [ ] 10.3 `src/EshopGuard.Jobs/Monitoring/WeeklySummaryScheduler.cs` a `Handlers/WeeklySummaryHandler.cs` (`monitor.weekly_summary`, P4): řádek `ops.outbox` na člena s `email_weekly_summary` a `dedupe_key weekly:{tenant}:{user}:{týden}`.
- [ ] 10.4 Šablony e-mailů `weekly_summary` a `new_violation` ve slovenštině a češtině (úložiště šablon změny 9), množné tvary přes ICU, odkazy na zákon v jazyce zákona; test úplnosti klíčů obou jazyků.
- [ ] 10.5 Test `tests/EshopGuard.Worker.Tests/Monitoring/NotificationTests.cs`: 3 nová porušení → 1 upozornění a 1 e-mail na příjemce; jen `new_assess` → nic; opakované `run.finalize` → stále 1; tři noci s 500 → `monitoring_unchecked`.
- [ ] 10.6 Test `tests/EshopGuard.Worker.Tests/Monitoring/WeeklySummaryTests.cs`: souhrn uvádí 4 nestažené stránky a 1 nepotvrzenou verzi; týden beze změn odejde; uživatel `cs` u slovenského e-shopu dostane češtinu; dvojí zpracování → 1 e-mail; vypnuté nastavení → nic.

## 11. Ověření

- [ ] 11.1 `tests/EshopGuard.LoadTests/Support/SyntheticShopFetcher.cs`: deterministické e-shopy v paměti (pevné semínko), 2 % změněných stránek za noc, `lastmod` v sitemap, `ETag` a 304 jen u poloviny e-shopů, robots.txt s Crawl-delay u části e-shopů.
- [ ] 11.2 `tests/EshopGuard.LoadTests/Support/LatencyJevClient.cs`: latence 0,33 s, globální limit 1 200 požadavků/min přes `ops.rate_limit_buckets`, bez placených volání.
- [ ] 11.3 `tests/EshopGuard.LoadTests/NightWindowLoadTests.cs` (kategorie `Load`): zkrácená varianta 10 e-shopů po 500 stránkách v okně 30 minut; varianta s limitem 100/min ověří `window_missed` bez zrušení běhů.
- [ ] 11.4 Plný zátěžový test: 100 e-shopů (60 × 500, 30 × 2 000, 10 × 5 000 stránek), okno 360 minut, jeden worker na stroji velikosti cílového serveru (architektura 7.1); všechny běhy `finished` před koncem okna; zapsat dobu nejdelšího běhu, volání Jevu, procesor na stránku a podíl 304 do `CHANGELOG.md` a porovnat s odhadem v architektuře, část 8.
- [ ] 11.5 Test `tests/EshopGuard.Worker.Tests/Monitoring/CrashDuringNightTests.cs`: zabití workeru v dávce `run.fetch`, `run.evaluate` a `monitor.memory` noci nic nezdvojí (`page_versions`, `page_changes`, `fix_proposals`, `notifications`, `outbox`).
- [ ] 11.6 Odhad ceny + souhlas uživatele pro živé měření na pilotu: 7 nocí sledování 2–3 pilotních e-shopů (vegis.sk, naturfyt.sk) proti skutečnému Jevu; odhad z podílu změn 2 % podle architektury, část 8 (~3 500 volání na noc u 5 000 stránek, podíl změn neměřen); vysvětlit uživateli a počkat na souhlas.
- [ ] 11.7 Po souhlasu: změřit skutečný podíl změněných stránek, podíl 304, volání Jevu na noc a cenu na e-shop a noc; zapsat do `CHANGELOG.md` a do K rozhodnutí 17.
- [ ] 11.8 `dotnet build` a `dotnet test` projdou včetně testů změny 8 a nových testů v `EshopGuard.Core.Tests`, `EshopGuard.Jobs.Tests`, `EshopGuard.Worker.Tests`.
