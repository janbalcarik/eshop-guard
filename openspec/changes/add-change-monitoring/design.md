# Design: Noční sledování změn e-shopů

## Technical Approach

### Vrstvy

| Vrstva | Projekt | Co obsahuje |
|---|---|---|
| Čistá logika | `EshopGuard.Core/Monitoring` (nové) | minuta v okně, rotační skupina, rozdíl sitemap, plán noci, rozdíl otisků vět, porovnání s pamětí rozhodnutí, rozpoznání nového místa prodeje nebo verze; bez databáze |
| Plánovač a obsluhy | `EshopGuard.Jobs/Monitoring` (nové) | `MonitorScheduler`, `MonitorWindowWatchdog`, `WeeklySummaryScheduler`, obsluhy `monitor.*`, rozdílový režim kroků ze změny 8, zápis změn a upozornění |
| Úložiště | `EshopGuard.Data/Stores` (nové třídy) | `ops.schedules`, `fixes.decision_memory`, `checks.page_changes`, `iam.notifications`, `ops.outbox`, dotazy na stránky noci |
| Hostitel | `EshopGuard.Worker` | registrace plánovačů (zámek plánovače ze změny 4) a obsluh |

### Plánovač

- `MonitorScheduler : IScheduledTask` (rozhraní plánovače ze změny 4) běží každou minutu jen v instanci, která drží zámek plánovače.
- **Minuta v okně:** `MonitorSlot.Compute(shopId, window, reserve)` = stabilní hash ID e-shopu (prvních 8 bajtů SHA-256 z bajtů `Guid`, nikdy `GetHashCode`, které se mezi procesy liší) modulo (`window_minutes` − `slot_reserve_minutes`). Ukládá se do `shops.monitor_slot_minute` při aktivaci sledování a nemění se.
- **Řádky `ops.schedules`** (PK `shop_id, kind`, K rozhodnutí 3):
  - `nightly`: každý sledovaný e-shop, `next_run_at` = místní datum + minuta v okně, převedené z `Europe/Bratislava` do UTC;
  - `weekly_web`: e-shop s konektorem, den v týdnu = hash % 7;
  - `reconcile`: zakládá a obsluhuje změna 15 (každou hodinu a ve 2:00); návrh posunout noční dorovnání a `connector.sync_content` do minuty e-shopu je v K rozhodnutí 21.
- **Založení:** v jedné transakci `checks.runs` (`kind = monitoring`, `trigger = schedule`, `priority = 3`, `jurisdictions` a `modules` z `IRunScopeResolver` změny 8 (implementace nad `ShopScopeCalculator` změny 10) nad aktivními místy prodeje v tu chvíli) a úloha `monitor.plan` s `dedupe_key = monitor:{shop}:{místní datum}`; `schedules.next_run_at` se posune na další noc.
- **Kdo se sleduje:** `shops.status = active`, `shops.last_full_run_id` není `null`, `billing.subscriptions.status` je `trialing`, `active` nebo `past_due` (K rozhodnutí 16). Ostatním se běh nezaloží a `schedules.next_run_at` se jen posune.
- **Dotažení zpožděných:** po výpadku plánovače se pro e-shop s `next_run_at` v minulosti založí jen běh pro poslední zmeškanou noc (žádná lavina). Rotace to dožene sama: stránky, které se nekontrolovaly 7 dní, jdou do plánu vždy (důvod `overdue`).
- **Změna času:** `dedupe_key` je podle místního data, takže noc s hodinou navíc (25. 10. 2026) dá jeden běh. Minuta, která v noci posunu času vpřed neexistuje, se posune na první platnou minutu (`TimeZoneInfo.IsInvalidTime`).
- **Překryv nocí:** `monitor.plan` nejdřív ověří, že e-shop nemá jiný běh `monitoring` v nekonečném stavu; pokud ano, odloží se přes `not_before` o `monitoring.overlap_retry_minutes` (návrh 10). Plán nové noci tak vždy vychází z výsledku předchozí.
- **`MonitorWindowWatchdog`** v okamžiku konce okna najde běhy `monitoring` té noci, které nejsou v konečném stavu: běhy nezruší (dokončí se s P3), zapíše provozní metriku `eshopguard.monitoring.window_missed` s počtem a do `runs.stats.finished_after_window = true` pro souhrn.

### Plán noci (`monitor.plan`, třída `fetch`, P3)

`NightPlanBuilder.Build` spojí bez duplicit tyto zdroje a ke každé URL zapíše důvod do `run_urls` (`queue` a nový sloupec `reason`):

| Důvod | Zdroj | E-shop bez konektoru (každou noc) | S konektorem: noc | S konektorem: `weekly_web` |
|---|---|---|---|---|
| `sitemap_new` | URL ze sitemap (`DiscoveryResult.SitemapEntry`, změna 5), kterou `pages` nemá | ano | – | ano |
| `sitemap_lastmod` | `SitemapEntry.LastModified` novější než `pages.sitemap_lastmod` | ano | – | ano |
| `sitemap_removed` | URL v `pages` (`status = active`, `source = crawl`) zmizela ze sitemap; ověří se stažením | ano | – | ano |
| `home`, `legal` | `pages.page_type in ('home','legal')` | ano | – | ano |
| `template_sample` | první vzorová stránka každého aktivního profilu (`page_profiles.sample_urls`, K rozhodnutí 15) | ano | – | ano |
| `rotation` | `pages.rotation_bucket = (místní datum).DayNumber % 7` | ano | – | všechny stránky webu |
| `retry` | `pages.next_check_at <= now()` (minule selhalo) | ano | ano (běh vznikne jen tehdy, když takové stránky jsou) | ano |
| `overdue` | `pages.last_fetched_at` starší než 7 dní | ano | – | ano |

Texty z konektoru (produkty, stránky a články ze změny 15) se v noci nestahují z webu; jejich změny přicházejí běhy `connector_check`.

- Sitemapa se stahuje podmíněně, snímek (URL, `lastmod`) se uloží do `tenants/{t}/shops/{s}/sitemaps/{datum}.json.gz` a `pages.sitemap_lastmod` se aktualizuje až po úspěšném běhu (jinak by se změna ztratila).
- Když sitemapa nejde stáhnout, rozdíl se nepočítá (nic se nepovažuje za zmizelé), běh pokračuje s ostatními zdroji a výčet nezkontrolovaného uvede `sitemap_unavailable`.
- Zmizelá URL se stáhne: 404/410 → `pages.status = gone`, výskyty nálezů na stránce se odeberou, `page_changes.change_kind = removed`. Jiná chyba → stránka zůstává a jde do `retry`.
- Nové URL dostanou `rotation_bucket = RotationBucket.Of(pageId)` (stabilní hash % 7) při vložení do `pages`; migrace doplní skupinu stávajícím stránkám.

### Podmíněné stažení a nové verze

- `run.fetch` ze změny 8 v režimu `monitoring` posílá `If-None-Match` (`pages.http_etag`) a `If-Modified-Since` (`pages.http_last_modified`).
- 304: `pages.last_fetched_at`, `last_seen_at`, `next_check_at = null`, `run_urls.state = not_modified`; žádná extrakce ani Jev.
- 200 se stejným `text_hash`: extrakce proběhne (je potřeba otisk), verze nevznikne.
- 200 s jiným `text_hash`: nová `page_versions` (`is_current` se přepne), `page_changes.change_kind = text_changed`.
- Chyba: `pages.next_check_at` = další noc, `run_urls.state = failed`; po `monitoring.max_failed_nights` (návrh 3) po sobě jde stránka do upozornění „stránka se nedá zkontrolovat“.

### Jen změněné věty

- `SentenceDelta.Compute(staré segment_hashes, nové segment_hashes)` dá přidané, odebrané a zachované otisky vět stránky.
- Do Jevu jdou **stavy** (věta s kontextem podle `segmentation.context_sentences`, jako `SentenceState`), které nejsou v `checks.jev_answers` tenanta. Změna jedné věty tak znovu vyhodnotí i sousední věty, jejichž kontext se změnil (až 2 před a 2 za). Ostatní odpovědi se vezmou z cache.
- Příznak „rámec webu“ (boilerplate) se pro větu počítá z počtu aktuálních verzí, které ji obsahují (`GIN (segment_hashes) WHERE is_current`), stejným prahem `segmentation.boilerplate_page_share` jako `SegmentAggregator`.
- **Pravidla v rozdílovém režimu** (`run.rules` s `RuleScope.Delta`): pravidla na větách se vyhodnotí jen pro věty změněných stránek; pravidla za stránku jen pro změněné stránky; pravidla za celý web jen tehdy, když se změnila úvodní nebo právní stránka. Test `DeltaRulesEquivalenceTests` ověří, že rozdílový režim dá stejné nálezy jako plné vyhodnocení nad stejnými daty.
- **Výskyty:** u změněné stránky se výskyty přepočítají; nález, kterému nezbude žádný výskyt, dostane `status = resolved`, `resolved_run_id`, `resolved_at`; když k němu existuje zveřejněná oprava (`fixes.publications.status = published`), `page_changes.result = fix_confirmed`, jinak `ok`.

### Paměť rozhodnutí

`DecisionMemoryMatcher.Match(nález, záznamy paměti, aktuální sady pravidel)` (čistá funkce) vrací jedno z:

| Výsledek | Podmínka | Co se stane |
|---|---|---|
| `apply_replacement` | `decision = replace`, stejný otisk textu (`segment_hash`) a stejná `rule_set_id` modulu | `fix_proposals` s `proposed_text = replacement_text`, `model = decision_memory`, bez OpenAI; kontrola nového textu Jevem v okolí nové stránky (`provider = jev`, `operation = recheck`); prošlo → `recheck_status = ok`, nálezu `status = proposed`; neprošlo → `still_finding` a nález `open` |
| `keep` | `decision = keep`, stejná verze sady pravidel | nález `status = kept`, bez otázky a návrhu |
| `keep_with_evidence` | `decision = keep_with_evidence`, doklad `evidence_items.status in (valid, expiring)` | nález `kept_with_evidence`, `evidence_links` pro novou stránku |
| `evidence_expired` | doklad `expired` nebo `claim_removed` | nález `needs_answer`, otázka znovu |
| `stale_rule_set` | jiná verze sady pravidel modulu než v záznamu | paměť se nepoužije, nález `open` (znovu rozhodnout) |
| `none` | žádný záznam | běžný postup (návrh opravy podle K rozhodnutí 8) |

- Záznam paměti platí pro e-shop, nebo pro všechny e-shopy tenanta (`shop_id = null`). Platí ten konkrétnější.
- `DecisionMemoryWriter.RecordAsync` zapíše záznam při rozhodnutí obchodníka (volá ho změna 11, K rozhodnutí 7): přijetí nebo úprava návrhu → `replace` s `replacement_text`; „ponechat“ → `keep`; „ponechat s dokladem“ → `keep_with_evidence`; starší záznam stejného otisku se označí `superseded_at`.
- Paměť se nikdy nepoužije napříč tenanty (RLS).

### Nové místo prodeje nebo jazyková verze

- Po extrakci úvodní a právních stránek `monitor.signals` (třída `cpu`) vezme technické znaky ze změny 7 (`html lang`, `hreflang` na stránkách a v sitemap, odkazy přepínače, měny ve strukturovaných datech, telefonní předvolby) a `MarketSignalDetector.Compare` je porovná s `shop_markets` a `shop_languages`.
- **Nová jazyková verze nebo doména verze:** řádek `shop_languages` se `status = needs_confirmation` a `source = hreflang | switcher`; verze se nekontroluje, dokud ji klient nepotvrdí (K rozhodnutí 20).
- **Nový silný znak země** (vlastní verze nebo doména, měna): u podporovaného trhu (`ref.markets.checks_status != none`) se spustí rozbor ze změny 7 s ověřenými citacemi (`monitor.market_check`, třída `llm`, odhad ceny před voláním, nejvýš jednou za `monitoring.market_check_days` na e-shop) a vznikne `shop_markets.status = suggested`, `source = detected`, `evidence`, `detection_run_id`; u nepodporovaného trhu `status = unsupported` bez upozornění.
- **Upozornění:** `iam.notifications.kind = market_suggested` nebo `language_suggested` s `params = {shop_id, country | language, evidence_level}`; text („Vyzerá to, že predávate aj …“) skládá frontend a e-mail v jazyce uživatele.
- `runs.jurisdictions` dalších běhů se mění jen potvrzením klienta (`status = active`). Odmítnutý trh (`declined`) se stejnou nebo slabší silou důkazu znovu nenabízí (K rozhodnutí 13).

### E-shopy s konektorem (napojení na změnu 15)

- Změna 15 načte aktuální text přes API (`connector.fetch_products`, `connector.sync_content`), zapíše `pages` a `page_versions` se `source = connector` a založí přes `IRunService` (změna 8) běh `connector_check` (P1; produkt uložený jako skrytý nebo koncept P0, jen při `shops.check_hidden_on_save`). Zapisuje i `page_changes` se `source = webhook` nebo `save_hidden`.
- `ConnectorChangeProcessor` (tato změna) se připojí ke kroku `run.finalize` běhů `connector_check` a přidá stejné zpracování jako u noci: `SentenceDelta`, paměť rozhodnutí (`monitor.memory`), výsledek změny (`page_changes.result`), upozornění. U skrytého produktu jde upozornění hned po běhu, ne až v souhrnu.
- E-shop s konektorem nemá noční běh webu (změny přicházejí z konektoru). Plánovač mu jednou týdně založí běh `monitoring` s plánem `weekly_web` (všechny stránky webu podmíněně, kvůli šabloně, odznakům a stránkám, které API nevrací); po události změny šablony ze změny 15 (Shoptet `eshop:design`) se `weekly_web` naplánuje na nejbližší noc.
- Když změna 15 hlásí, že webhooky nechodí (`connectors.health`), noční běh webu se e-shopu zapne, dokud se konektor neobnoví (fail-closed: změny se nesmí ztratit).

### Priority

| Práce | Úloha | Priorita | Třída |
|---|---|---|---|
| kontrola uloženého skrytého produktu | běh `connector_check` změny 15 a jeho `monitor.memory` | 0 | podle kroku |
| změny z webhooků a dorovnání | úlohy změny 15, běh `connector_check` a jeho `monitor.memory` | 1 | podle kroku |
| noční běh, týdenní procházení webu | `monitor.plan` a kroky běhu `monitoring` | 3 | podle kroku |
| rozbor nového trhu | `monitor.market_check` | 3 | `llm` |
| týdenní souhrn, úklid snímků sitemap | `monitor.weekly_summary`, `monitor.cleanup` | 4 | `system` |

Podíly limitu Jevu: P0–P1 celý limit, P2–P4 nejvýš 80 % (změna 4); vyhrazení pro P3 v okně je K rozhodnutí 18.

### Odhad nákladů noci

- Po rozdílové segmentaci `InternalCostEstimator` (změna 8) spočítá horní mez Jevu pro nové stavy a OpenAI pro návrhy a uloží ji do `runs.estimate.internal` před první dávkou síta.
- Nad `monitoring.alert_usd_per_shop_night` jde provozní metrika `eshopguard.monitoring.cost_over_threshold` a log Warning s `shop_id` a `run_id`. Noc se nezastavuje ani nezmenšuje (nic se tiše nevynechá).
- Měsíční součet změněných stránek na e-shop se počítá z `page_changes` pro budoucí férové užití (K rozhodnutí 14).

### Upozornění a týdenní souhrn

- **Nové porušení:** `run.finalize` běhu `monitoring` nebo `connector_check` s aspoň jedním `page_changes.result = new_violation` založí jeden řádek `iam.notifications` (`user_id = null`, `kind = new_violation`, `params = {shop_id, run_id, by_severity}`, `dedupe_key = new_violation:{run}`) a pro členy s `notification_settings.email_new_violation = true` řádek `ops.outbox` (`kind = email`, šablona `new_violation`, jazyk `users.locale`).
- **Stránky, které se nedají zkontrolovat:** `kind = monitoring_unchecked` po `monitoring.max_failed_nights` nocích.
- **Týdenní souhrn:** `WeeklySummaryScheduler` (pondělí po okně, K rozhodnutí 12) založí pro každého tenanta úlohu `monitor.weekly_summary`. `WeeklySummaryBuilder` spočítá za uplynulý týden po e-shopech:

```json
{
  "week": "2026-W41",
  "shops": [{
    "shop_id": "…", "name": "…",
    "nights": {"planned": 7, "finished": 6, "partial": 1, "missed": 0, "finished_after_window": 0},
    "pages": {"checked": 5210, "not_modified": 4400, "changed": 96, "new": 12, "removed": 3},
    "findings": {"new_by_severity": {"high": 1, "medium": 4, "low": 0}, "resolved": 6, "waiting_for_decision": 9},
    "unchecked": {"failed": 4, "not_loaded": 2, "sitemap_unavailable": 0, "unconfirmed_language_versions": 1},
    "pages_unchecked_over_7_days": 0,
    "market_suggestions": ["cz"]
  }]
}
```

  Čísla v příkladu jsou ilustrační. Pro každého člena s `email_weekly_summary = true` (pro e-shop nebo pro všechny) vznikne řádek `ops.outbox` s `dedupe_key = weekly:{tenant}:{user}:{týden}`; text a čísla formátuje šablona v `users.locale`. Souhrn se pošle i v týdnu bez změn (aby bylo vidět, že sledování běží) a vždy uvádí nezkontrolované.

### Události průběhu

Běh `monitoring` píše stejné kódy jako změna 8 a navíc `monitor.plan` (`{reasons: {sitemap_new: n, rotation: n, …}}`) a `monitor.not_modified` (`{count}`). Žádná událost neobsahuje text stránek ani částky.

## Architecture Decisions

1. **Sledování je běh se stejnými kroky jako úvodní analýza, jen s jiným plánem a rozdílovým režimem.** Alternativa samostatného kódu by se časem rozešla s úvodní analýzou a výsledky by si neodpovídaly. Rozdílový režim hlídá test rovnocennosti s plným vyhodnocením.
2. **Plánovač jen zakládá úlohy** (architektura část 2). Práce noci běží jako běžné úlohy fronty, takže se dělí mezi workery a pád plánovače nic nerozbije; po obnovení zámku pokračuje jiná instance.
3. **Stabilní hash místo `GetHashCode`.** `string.GetHashCode` a `Guid.GetHashCode` nejsou v .NET stabilní mezi procesy; minuta e-shopu a rotační skupina stránky se nesmí měnit při restartu.
4. **Rotace podle stránky, ne podle URL v sitemap.** Skupina se uloží jednou do `pages.rotation_bucket`; nové stránky se rozdělí stejně. Zmeškané noci dožene důvod `overdue`, takže záruka „každá stránka aspoň jednou týdně“ platí i po výpadku.
5. **Změněná věta = změněný stav pro Jev.** Cache Jevu je podle věty s kontextem (`JevCacheKey`), proto se po změně souseda věta vyhodnotí znovu. Je to dražší než „jen nové věty“, ale výsledek je stejný jako v úvodní analýze. Nález zůstává tentýž (identita podle otisku textu, změna 8, K rozhodnutí 3), takže stav a rozhodnutí se neztratí.
6. **Paměť rozhodnutí podle otisku normalizovaného textu a verze sady pravidel.** Stejný text u nového produktu dostane stejnou opravu bez modelu; kontrola Jevem v novém okolí zůstává, protože okolí může smysl věty změnit.
7. **Trh ani verze se nepřidají samy** (architektura část 12). Upozornění je jediný účinek; jurisdikce běhu mění jen klient.
8. **Fail-closed u nedostupné sitemap a stránky.** Chyba nikdy neznamená „beze změny“ ani „zmizelo“: stránka zůstává s nálezy, jde do dalších nocí a do souhrnu.
9. **Týdenní souhrn i bez změn.** Prázdný týden je informace („sledování běží a nic se nezměnilo“); vynechaný e-mail by se nedal odlišit od nefunkčního sledování.

## Data Flow

```mermaid
sequenceDiagram
  participant SCH as MonitorScheduler (1 instance)
  participant DB as PostgreSQL
  participant W as Workery
  participant SH as E-shop
  participant JEV as Jev
  participant OUT as ops.outbox / notifications

  SCH->>DB: e-shopy s next_run_at <= now (aktivní předplatné, hotová úvodní analýza)
  SCH->>DB: runs(monitoring, P3) + job monitor.plan (dedupe monitor:{shop}:{datum}); posun next_run_at
  W->>SH: sitemap (podmíněně)
  W->>DB: run_urls podle důvodů (sitemap, home, legal, šablona, rotace, retry, overdue)
  loop dávky
    W->>SH: GET s If-None-Match / If-Modified-Since
    alt 304
      W->>DB: last_fetched_at, not_modified
    else 200
      W->>DB: page_versions jen při změně text_hash, page_changes(text_changed)
    end
  end
  W->>DB: monitor.signals → shop_markets / shop_languages (suggested, needs_confirmation)
  W->>DB: rozdíl otisků vět, odhad noci
  W->>JEV: jen stavy, které nejsou v cache tenanta
  W->>DB: pravidla v rozdílovém režimu, výskyty, resolved
  W->>DB: paměť rozhodnutí → fix_proposals(model=decision_memory), kept, kept_with_evidence
  W->>JEV: kontrola náhrady v novém okolí
  W->>DB: finalize: stav, stats, page_changes.result
  W->>OUT: new_violation (1 za běh), e-mail podle nastavení
  Note over SCH,OUT: pondělí: monitor.weekly_summary → outbox (1 za uživatele a týden)
```

## File Changes

Cesty jsou relativní ke kořeni řešení (`eshop-guard/` po změně 1).

### `src/EshopGuard.Core/Monitoring` (nové, bez databáze)

- `MonitorSlot.cs`: stabilní hash, minuta v okně, posun v noci změny času.
- `RotationBucket.cs`: skupina stránky (0–6) a skupina noci podle místního data.
- `SitemapDiff.cs`: nové, zmizelé, změněné podle `lastmod`, nezměněné.
- `NightPlanBuilder.cs`, `NightPlanReason.cs`: plán noci s důvody a bez duplicit (plány `nightly`, `connector_retry`, `weekly_web`).
- `SentenceDelta.cs`: přidané, odebrané a zachované otisky vět stránky.
- `DecisionMemoryMatcher.cs`, `DecisionMemoryEntry.cs`, `MemoryOutcome.cs`.
- `MarketSignalDetector.cs`: nové jazykové verze, domény verzí, měny a země proti známým.
- `WeeklySummary.cs`: model souhrnu (data pro šablonu).

### `src/EshopGuard.Jobs/Monitoring` (nové)

- `MonitorScheduler.cs`, `MonitorWindowWatchdog.cs`, `WeeklySummaryScheduler.cs` (`IScheduledTask` ze změny 4).
- `MonitoringOptions.cs`: `WindowStart` (00:00), `WindowMinutes` (360), `SlotReserveMinutes`, `TimeZone` (`Europe/Bratislava`), `MaxFailedNights`, `AlertUsdPerShopNight`, `MarketCheckDays`, `OverlapRetryMinutes`, `FrequencyByTier`.
- `Handlers/MonitorPlanHandler.cs` (`monitor.plan`), `MonitorSignalsHandler.cs` (`monitor.signals`), `MarketCheckHandler.cs` (`monitor.market_check`), `MemoryHandler.cs` (`monitor.memory`, priorita podle běhu: 0, 1 nebo 3), `WeeklySummaryHandler.cs` (`monitor.weekly_summary`), `CleanupHandler.cs` (`monitor.cleanup`).
- `DeltaMode.cs`: přepínače rozdílového režimu pro `run.fetch`, `run.segment`, `run.rules`, `run.rewrite` a `run.finalize` ze změny 8.
- `MonitoringChangesWriter.cs`: `page_changes` a výsledky (`new_violation`, `new_assess`, `fix_confirmed`, `ok`), resolved nálezy.
- `MonitoringNotifier.cs`: `iam.notifications` a `ops.outbox` s `dedupe_key`.
- `DecisionMemoryWriter.cs`: zápis paměti z rozhodnutí obchodníka (pro změnu 11).
- `WeeklySummaryBuilder.cs`.
- `ConnectorChangeProcessor.cs`: napojení běhů `connector_check` změny 15 na stejné zpracování.
- Úpravy kódu změny 8: `Runs/RunPlan.cs` (druh `monitoring`, plán `nightly` a `weekly_web`, `monitor.memory` před `run.rewrite`), `Runs/Handlers/FetchBatchHandler.cs` a `ExtractBatchHandler.cs` (podmíněné stažení, `not_modified`, `rotation_bucket`), `SegmentHandler.cs` (rozdílový režim), `RulesHandler.cs` (`RuleScope.Delta`), `RewriteBatchHandler.cs` (jen nálezy bez výsledku paměti), `FinalizeHandler.cs` (volání `MonitoringChangesWriter`, `MonitoringNotifier`, `ConnectorChangeProcessor`).

### `src/EshopGuard.Data`

- `Stores/ScheduleStore.cs`, `Stores/DecisionMemoryStore.cs`, `Stores/PageChangeStore.cs`, `Stores/NotificationStore.cs`, `Stores/OutboxStore.cs`, `Stores/MonitoringPageQueries.cs` (stránky noci podle `rotation_bucket`, `page_type`, `next_check_at`, `last_fetched_at`; počet aktuálních verzí s otiskem přes GIN).
- `Migrations/20261201_ChangeMonitoring.cs` a `Migrations/Sql/change_monitoring.sql`:
  - `content.pages.sitemap_lastmod timestamptz`; indexy (`shop_id`, `rotation_bucket`) a (`shop_id`, `next_check_at`) WHERE `status = 'active'`;
  - `checks.run_urls.reason text` s `CHECK` podle tabulky Plán noci a stav `not_modified`;
  - `ops.schedules` PK (`shop_id`, `kind`) (K rozhodnutí 3);
  - `fixes.decision_memory.rule_id text`, `rule_set_id uuid` (K rozhodnutí 4);
  - `iam.notifications.dedupe_key`, `ops.outbox.dedupe_key` s jedinečným indexem, jedinečnost `checks.page_changes (run_id, page_id, change_kind)` (K rozhodnutí 19);
  - doplnění `pages.rotation_bucket` stávajícím stránkám a `shops.monitor_slot_minute` sledovaným e-shopům.

### `src/EshopGuard.Api` / e-maily

- Šablony e-mailů `weekly_summary` a `new_violation` ve slovenštině a češtině v úložišti šablon změny 9 (`Emails/Templates/sk/weekly_summary.*`, `cs/…`); test úplnosti klíčů obou jazyků.

### `src/EshopGuard.Worker`

- `Program.cs`: `AddChangeMonitoring()`, registrace plánovačů a obsluh `monitor.*`.

### `tests`

- `tests/EshopGuard.Core.Tests/Monitoring/MonitorSlotTests.cs`, `RotationBucketTests.cs`, `SitemapDiffTests.cs`, `NightPlanBuilderTests.cs`, `SentenceDeltaTests.cs`, `DecisionMemoryMatcherTests.cs`, `MarketSignalDetectorTests.cs`.
- `tests/EshopGuard.Jobs.Tests/Monitoring/MonitorSchedulerTests.cs`, `WindowWatchdogTests.cs`, `WeeklySummaryBuilderTests.cs`.
- `tests/EshopGuard.Worker.Tests/Monitoring/NightRunTests.cs`, `ChangedSentencesOnlyTests.cs`, `DeltaRulesEquivalenceTests.cs`, `DecisionMemoryTests.cs`, `NewMarketSignalTests.cs`, `ConnectorChangeTests.cs`, `NotificationTests.cs`, `WeeklySummaryTests.cs`.
- `tests/EshopGuard.LoadTests/NightWindowLoadTests.cs` (nový projekt, kategorie `Load`), `Support/SyntheticShopFetcher.cs` (deterministické e-shopy v paměti, změny po nocích, `ETag` a 304 jen u části e-shopů, `lastmod` v sitemap), `Support/LatencyJevClient.cs` (latence 0,33 s, globální limit 1 200/min).
