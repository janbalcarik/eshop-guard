# Design: E-shopy a onboarding v API

## Technical Approach

### Vrstvy

| Projekt | Co přidá tato změna |
|---|---|
| `src/EshopGuard.Core/Platforms/` | `PlatformDetector` (čistá funkce nad staženou úvodní stránkou), `PlatformSignatures` načtené z `config/platforms.yaml`. |
| `src/EshopGuard.Application/Shops/` | Služby e-shopů, ukázky, míst prodeje, verzí, rozsahu, onboardingu, ověření a nastavení. Rozhraní `IPriceQuoteService` (implementuje změna 12) a `IShopOrderReadiness` (volá změna 12). |
| `src/EshopGuard.Api/Endpoints/` | `ShopEndpoints`, `OnboardingEndpoints`, `MarketEndpoints`, `LanguageEndpoints`, `OwnershipEndpoints`, `ShopSettingsEndpoints`. |
| `src/EshopGuard.Jobs/Shops/` | Obsluha úloh `shop.detect_platform` a `shop.verify_ownership` (P0, `resource_class = fetch`). |
| `src/EshopGuard.Data/` | Migrace: částečný jedinečný index `shops` (jen nesmazané e-shopy), sloupec `shops.detection`. Nárok na ukázku zapisuje `RunService` ze změny 8. |

### Koncové body

Všechny pod `/api/t/{tenantId}`. Role podle matice změny 9: čtení `viewer`, změny `admin`. CSRF u měnících metod.

| Metoda a cesta | Role | Tělo → odpověď | Chybové kódy |
|---|---|---|---|
| `GET /shops` | viewer | → `200 ShopListItemDto[]` | – |
| `POST /shops` | admin | `{ url }` → `201 ShopDto` (+ spuštěné rozpoznání) | `shop.url_invalid`, `shop.url_not_allowed` (`400`), `shop.already_exists` (`409`, `params.shopId`), `rate_limited` |
| `GET /shops/{shopId}` | viewer | → `200 ShopDto` | `shop.not_found` (`404`) |
| `PATCH /shops/{shopId}` | admin | `{ name }` + `If-Match` → `200 ShopDto` | `concurrency.conflict` (`409`) |
| `DELETE /shops/{shopId}` | admin | → `204` (měkké smazání) | `shop.subscription_active`, `shop.run_in_progress` (`409`) |
| `GET /shops/{shopId}/detection` | viewer | → `200 DetectionDto` | – |
| `POST /shops/{shopId}/detection` | admin | → `200 DetectionDto` nebo `202` | `detection.in_progress` (`409`), `rate_limited` |
| `PUT /shops/{shopId}/platform` | admin | `{ platform }` → `200 ShopDto` | `platform.unknown` (`400`) |
| `PUT /shops/{shopId}/source` | admin | `{ mode: web\|feed\|connector, feed?: { url, format: heureka\|google } }` → `200 ShopDto` | `shop.connector_not_connected` (`409`), `feed.url_invalid`, `feed.format_unknown` (`400`) |
| `POST /shops/{shopId}/sample` | admin | → `202 SampleDto` | `sample.already_used_for_domain`, `sample.not_allowed_in_status`, `shop.ownership_not_verified` (`409`), `rate_limited` |
| `GET /shops/{shopId}/sample` | viewer | → `200 SampleDto` | `sample.not_started` (`404`) |
| `GET /shops/{shopId}/markets` | viewer | → `200 ShopMarketsDto` | – |
| `PUT /shops/{shopId}/markets` | admin | `{ active: ["sk","cz"] }` → `200 ShopMarketsDto` | `markets.none_selected`, `markets.unsupported` (`params.code`), `markets.unknown` (`400`), `markets.locked_during_run` (`409`) |
| `GET /shops/{shopId}/languages` | viewer | → `200 LanguageVersionsDto` | – |
| `POST /shops/{shopId}/languages/{language}/confirmation` | admin | `{ belongsToShop: bool }` → `200 LanguageVersionsDto` | `language.not_found` (`404`), `language.not_awaiting_confirmation` (`409`) |
| `PUT /shops/{shopId}/languages/{language}/exclusion` | admin | `{ excluded: bool }` → `200 LanguageVersionsDto` | `language.not_found`, `language.last_checked_version` (`400`), `language.awaiting_confirmation` (`409`) |
| `GET /shops/{shopId}/scope` | viewer | → `200 ScopeDto` (uložený stav) | `scope.basis_missing` (`409`) |
| `POST /shops/{shopId}/quote` | admin | `{ activeMarkets?: [..], excludedLanguages?: [..] }` → `200 QuoteDto` | `quote.sample_not_finished`, `quote.basis_missing` (`409`), `markets.none_selected`, `markets.unsupported` (`400`), `billing.unavailable` (`503`) |
| `GET /shops/{shopId}/onboarding` | viewer | → `200 OnboardingStateDto` | – |
| `GET /shops/{shopId}/ownership` | viewer | → `200 OwnershipDto` | – |
| `POST /shops/{shopId}/ownership/verifications` | admin | `{ method: meta\|dns }` → `201 VerificationDto` | `ownership.method_unknown` (`400`), `ownership.already_verified` (`409`) |
| `POST /shops/{shopId}/ownership/verifications/{verificationId}/check` | admin | → `200 VerificationDto` nebo `202` | `ownership.verification_not_found` (`404`), `rate_limited` |
| `GET /shops/{shopId}/settings` | viewer | → `200 ShopSettingsDto` | – |
| `PATCH /shops/{shopId}/settings` | admin | `{ name?, modules?, checkHiddenOnSave? }` + `If-Match` → `200 ShopSettingsDto` | `settings.no_module`, `settings.module_unavailable` (`params.module`) (`400`), `settings.hidden_check_requires_connector`, `settings.hidden_check_unsupported_platform`, `concurrency.conflict` (`409`) |

### DTO

- `ShopListItemDto { id, domain, name, platform, sourceMode, connectorStatus?, status, lastRunAt?, languagesChecked }`.
- `ShopDto`:
  - `{ id, domain, baseUrl, basePath, name, homeCountry, language, platform, platformSource (detected|user), sourceMode, status }`;
  - `{ productCount?, pageCount?, tierCode?, modules[], checkHiddenOnSave, ownershipVerifiedAt?, verificationMethod?, feed?: { url, format }, version (ETag z xmin) }`.
- `DetectionDto { status: pending|done|failed, platform: shoptet|upgates|biznisweb|woocommerce|shopify|other|unknown, confidence: certain|likely|unknown, signals: [code], finalUrl?, redirectedToOtherDomain?: { domain }, failureCode?: robots_blocked|fetch_failed|timeout|not_html, connector: { platform, available }, recommendedSource: connector|feed|web }`.
- `SampleDto { runId, status (stav běhu ze změny 8), startedAt?, finishedAt?, progress: { pagesPlanned, pagesFetched, pagesProcessed }, queue: { position?, estimatedFinishAt? }, result?: SampleResultDto }`.
- `SampleResultDto`:
  - `{ pagesPlanned, pagesChecked, notChecked: { robotsBlocked, textNotLoaded, tooLarge, fetchError, other }, versions: [language], jurisdictions: [code] }`;
  - `findingCounts: { total, byCheckability: { text, assess, verify }, bySeverity: { high, medium, low } }`;
  - `topFindings: [≤5 { findingId, ruleSetId, ruleId, module, scope, strictest: VerdictDto, verdicts: [VerdictDto], text, page: { id, title, url, language }, params }]`;
  - `exampleFix: { proposalId, findingId, originalText, proposedText, recheckStatus } | null`.
- `VerdictDto { jurisdiction, checkability: text|assess|verify, severity, band, legalRefs }` (tvar ze změny 6, odkazy na zákon v jazyce zákona).
- `ShopMarketsDto { markets: [ShopMarketDto], confirmedAt?, confirmedBy? }`.
- `ShopMarketDto`:
  - `{ countryCode, marketCode, isHome, status: suggested|active|declined, preselected, evidenceLevel: strong|delivery|generic|null, source: detected|user }`;
  - `evidence: [{ kind: signal|citation, code, params, quote?, pageUrl? }]`;
  - `checksStatus: limited|full`.
- `LanguageVersionsDto`:
  - `summary: { kind: single_version|all_checked_own_texts|some_menu_only|needs_confirmation, versionsFound, checkedLanguages[], countedLanguages[], mutualJurisdictions: bool }`;
  - `versions: [{ language, baseUrl, isMain, switchMethod, source, status: active|excluded|needs_confirmation, productCount, languageShare: { "sk": 1.0 }, otherLanguageItems, ownTextShare?, counted, countedReason, checked, checkedReason, jurisdictions: [code], comparison?, legalPagesDiffer? }]`;
  - `countedReason`: `main_version`, `own_texts_above_threshold`, `below_threshold`, `uncertain`, `sample_insufficient`;
  - `checkedReason`: `market_language`, `main_fallback`, `not_needed_by_markets`, `excluded`, `awaiting_confirmation`;
  - `comparison: { pairs, translated, shortenedOrDifferent, untranslated, examples: [{ productTitle, kind, code, params }] }`.
- `ScopeDto`:
  - `{ checkedVersions: [{ language, baseUrl, isMain, productCount, otherPageCount, counted, countedReason, jurisdictions[] }], notCheckedVersions: [{ language, reason }] }`;
  - `{ jurisdictions[], productTotal, otherPagesTotal, basis: { sampleRunId, finishedAt }, scopeHash }`.
- `QuoteDto { scope: ScopeDto, price: PriceQuoteDto }`.
- `PriceQuoteDto` (tvar dodá změna 12, minimálně):
  - `{ quoteId, priceListId, currency, tierCode?, tierMaxProducts?, isCustom, fairUse: { otherPagesLimit, exceeded } }`;
  - `{ analysisNet?, monitoringMonthlyNet?, monitoringDiscountPercent?, todayNet?, firstMonitoringChargeAt?, vatPreview?, validUntil }`.
- `OnboardingStateDto { step: connect|sample|scope|payment|analysis|done, blocking: [code], sample: { status }, marketsConfirmed, languagesAwaitingConfirmation: [language], ownership: { required: bool, verified: bool } }`.
- `OwnershipDto { verified, verifiedAt?, method?, requiredBefore: [sample|full_analysis], verifications: [VerificationDto] }`.
- `VerificationDto { id, method, status: pending|verified|failed, token, instructions: { metaTag? , dnsName?, dnsValue? }, checkedAt?, failureCode? }`.
- `ShopSettingsDto { name, modules: [{ module, enabled, available, jurisdictions[] }], checkHiddenOnSave, checkHiddenOnSaveAvailable, excludedLanguages[], version }`.

## Architecture Decisions

**AD 1. Normalizace a předběžná kontrola adresy (`ShopUrlNormalizer`).**
- Povoleno jen `http` a `https`. Port prázdný, 80 nebo 443. Žádné `user:heslo@`.
- Hostitel nesmí být adresa IP (v4 ani v6), `localhost`, jméno bez tečky ani doména `.local`, `.internal`, `.lan`, `.home.arpa`. IDN se převede na punycode. Délka nejvýš 2 048 znaků.
- `domain` = hostitel malými písmeny bez úvodního `www.`. `base_path` = cesta normalizovaná na `/` nebo `/cesta/`, bez dotazu a fragmentu.
- V prostředích Development a Test povoluje `Shops:AllowedDevHosts` (např. `localhost:8000` testovacího e-shopu).
- Rozlišení DNS a kontrola cílové IP (i po přesměrování) jsou ve stahovači workeru (změna 5). API adresu nikdy samo nestahuje.

**AD 2. Rozpoznání platformy jako úloha P0.**
- `POST /shops` a `POST …/detection` založí úlohu `shop.detect_platform`:
  - `priority = 0`, `resource_class = fetch`, `dedupe_key = detect:{shopId}`;
  - payload jen `shopId`, žádné texty.
- API čeká na dokončení úlohy dotazem na řádek `ops.jobs` každých 250 ms nejvýš `Api:InteractiveWaitSeconds` (návrh 8 s, `IJobCompletionAwaiter`). Pak vrátí stav `pending` a frontend se zeptá `GET …/detection`.
- Obsluha `ShopDetectPlatformHandler`:
  1. respektuje robots.txt;
  2. stáhne úvodní stránku přes stahovač se SSRF ochranou (nejvýš `Shops:Detection:MaxBytes`, návrh 1 MB, a 10 s);
  3. zavolá `PlatformDetector.Detect(finalUrl, headers, cookies, html)`;
  4. výsledek zapíše do `shops.platform` a `shops.base_url` (konečná adresa po přesměrování na stejné doméně). Signály, jistotu, kód selhání a čas uloží do nového sloupce `shops.detection jsonb` (migrace této změny; `ops.jobs` sloupec pro výsledek nemá). Stav `pending` plyne z nedokončené úlohy `shop.detect_platform`.
- Přesměrování na jinou doménu se nezapíše jako `base_url`. Vrátí se `redirectedToOtherDomain` a klient rozhodne.
- `PlatformDetector` porovnává technické podpisy z `config/platforms.yaml`: meta `generator`, hostitelé souborů, hlavičky (`x-shopid`, `x-powered-by`), jména cookies, odkazy na API platformy (`/wp-json/wc/`). Nevyhodnocuje text stránky.
  - Výsledek `certain`: aspoň 2 nezávislé podpisy.
  - Výsledek `likely`: 1 podpis.
  - Jinak `unknown`.
  - Konflikt dvou platforem → `unknown` (fail-closed).
- `connector.available` dává `IConnectorCatalog` (změna 15). Do té doby vrací `false` a doporučený způsob je `web` (u `unknown` také `web`).

**AD 3. Způsob napojení.**
- Výchozí `source_mode = web`.
- `feed` uloží `shop.feeds` (`url` přes `ShopUrlNormalizer`, `format`). Feed se stahuje až ve workeru.
- `connector` je přípustný jen když `shop.connectors.status = connected` (zapíše změna 15), jinak `409 shop.connector_not_connected`.
- Změna způsobu u e-shopu s běžící analýzou → `409 shop.run_in_progress`.

**AD 4. Ukázka zdarma.**
- `SampleService.StartAsync`:
  1. stav e-shopu `draft` (jinak `409 sample.not_allowed_in_status`);
  2. politika vlastnictví (AD 9);
  3. kbelík `shops:sample:tenant:{id}` (návrh 5 za den);
  4. `RunService.CreateFreeSampleAsync(shopId, requestedBy)` ze změny 8, který v jedné transakci vloží `shop.free_sample_claims` (globální, bez RLS, přes funkci `SECURITY DEFINER`), `checks.runs` (`kind = free_sample`) a první úlohu;
  5. `shops.status = sample`;
  6. audit `sample.started`.
- Konflikt nároku → `409 sample.already_used_for_domain` bez `tenant_id` nebo jiného údaje o tom, kdo doménu použil.
- `GET …/sample` čte poslední běh `free_sample` e-shopu:
  - průběh z `runs.progress`;
  - pozici ve frontě z `IRunQueueEstimator` (změna 8; když chybí, `null`);
  - výsledek ze souhrnu ukázky (`runs.stats`/`runs.estimate`, změna 8) a z `checks.findings` s `first_run_id` ukázky;
  - pět nejzávažnějších nálezů řadí `VerdictStrictness` ze změny 6;
  - ukázka opravy je první `fixes.fix_proposals` běhu s `recheck_status = ok`.
- Vysvětlení nálezů API neposílá. Frontend je složí z katalogu textů pravidel (změna 11) podle `ruleId` a `params`.

**AD 5. Místa prodeje.**
- `GET` vrací jen řádky, jejichž `ref.markets.checks_status` ≠ `none`. Řádky `unsupported` (a země bez podpory) se nevrací ani nepočítají.
- `preselected = true` pro `evidence_level` `strong` a `delivery`, `false` pro `generic` a ruční.
- Citace se vrací jen s příznakem `verified = true` z `shop_markets.evidence` (změna 7). Technické znaky se vrací jako kódy s parametry (`seat` `{ city }`, `tld` `{ tld }`, `currency` `{ currency }`, `language_version` `{ language, url }`, `delivery_terms`, `local_authority`).
- `PUT` v jedné transakci:
  - země ze seznamu `active` dostanou `status = active`, ostatní podporované `declined`;
  - nová podporovaná země bez řádku dostane řádek se `source = user`;
  - všude `confirmed_by` a `confirmed_at`;
  - audit `markets.confirmed` se seznamem.
- Ověření: aspoň jedna země, každá musí existovat v `ref.markets` a mít podporu kontrol.
- Během běhu analýzy nebo sledování (`runs.status` není konečný) → `409 markets.locked_during_run`.

**AD 6. Jazykové verze.**
- `GET` vrací verze se `status` `active`, `excluded` a `needs_confirmation`. `unsupported` se nevrací.
- `checked` a `checkedReason` počítá `ShopScopeCalculator` (AD 7) z uložených zemí, takže 3c a 3d říkají totéž co cena.
- `confirmation` je jen pro `needs_confirmation`:
  - `belongsToShop = true` → `active`;
  - `false` → `excluded` a audit `language.rejected_other_domain`. Frontend nabídne přidat doménu jako samostatný e-shop přes `POST /shops`.
- `exclusion`: `excluded = true` → `excluded`, `false` → `active`. Když by po vyloučení nezůstala žádná kontrolovaná verze → `400 language.last_checked_version`. Verze čekající na potvrzení → `409 language.awaiting_confirmation`.

**AD 7. Rozsah kontroly (`ShopScopeCalculator`, čistá funkce).**

Vstup:
- aktivní země `M` (uložené, nebo z `POST …/quote`);
- verze `V` (`language`, `status`, `isMain`, `productCount`, `otherPageCount`, `counted`) se základem z ukázky;
- `ref.markets` (`default_locale` → jazyk, `checks_status`);
- `Markets:ReadableLanguages` (`sk: [sk, cs]`, `cz: [cs, sk]`);
- vyloučené verze `X` (uložené, nebo z požadavku).

Postup:
1. `M' = M` jen s podporou kontrol. Prázdné `M'` → `markets.none_selected`.
2. Pro každou zemi `m ∈ M'`: verze v jazyce `m`, která je `active` a není v `X`. Jinak hlavní verze, pokud je `active` a není v `X`. Jinak `scope.no_checkable_version`.
3. `C` = množina vybraných verzí. Každá `v ∈ C` dostane `jurisdictions(v) = { m ∈ M' : v.language ∈ ReadableLanguages[m] } ∪ { m, které v vybraly }`.
4. Verze, které nejsou v `C`, jdou do `notCheckedVersions`:
   - důvod `excluded`, `awaiting_confirmation`, nebo `not_needed_by_markets`;
   - `unsupported` se nevypisuje.
5. `productTotal = Σ productCount` přes `v ∈ C` s `counted = true`. Hlavní verze je vždy `counted`. `counted` u ostatních je `own_text_share ≥ Pricing:OwnTextShareThreshold`, při nejistotě nebo nedostatečném vzorku `false` (hodnotu zapisuje změna 7, kalkulátor ji jen čte).
6. `otherPagesTotal = Σ otherPageCount` přes `v ∈ C`.
7. `scopeHash = SHA-256` kanonického JSON (`M'`, `C` s jazyky a počty, `X`, `basis.sampleRunId`).

Tabulka případů (`ShopScopeCalculatorTests`):

| Případ | Vstup | Kontrolované verze (jurisdikce) | Do pásma |
|---|---|---|---|
| A | SK + CZ; verze sk (hlavní, 5 834), cs (5 834, 96 % vlastních) | sk (sk, cz), cs (sk, cz) | 11 668 |
| B | jako A, CZ odškrtnuto | sk (sk) | 5 834 |
| C | SK + CZ; cs má přeložené jen menu (3 %) | sk (sk, cz), cs (sk, cz) | 5 834 (cs `below_threshold`) |
| D | SK + CZ; cs nejistá | sk, cs | 5 834 (cs `uncertain`) |
| E | SK + CZ; cs na jiné doméně `needs_confirmation` | sk (sk, cz) jako záloha pro CZ | 5 834; cs `awaiting_confirmation` |
| F | jako A, cs vyloučená | sk (sk, cz) | 5 834 |
| G | SK; verze sk, pl (pl `unsupported`) | sk (sk) | 5 834; pl se nevypíše |
| H | CZ; verze sk (hlavní), cs | cs (cz) | jen cs, pokud `counted`, jinak hlavní |
| I | SK + CZ; 2 × 12 000 produktů s vlastními texty | sk, cs | 24 000 → změna 12 vrátí `isCustom` |
| J | CZ; jediná verze sk (hlavní) | sk (cz, protože `sk ∈ ReadableLanguages[cz]`) | hlavní |

**AD 8. Nabídka ceny.**
- `POST …/quote` nic neukládá ve schématu `shop`. Spočítá `ScopeDto` pro zadanou kombinaci (chybějící pole = uložený stav) a zavolá `IPriceQuoteService.QuoteAsync(PriceQuoteRequest { tenantId, shopId, scope, requestedBy })`.
- Změna 12 z rozsahu určí pásmo, částky, slevy a DPH a nabídku uloží (`billing.price_quotes` s `scopeHash`).
- Frontend volá `quote` při každé změně zaškrtnutí země nebo vyloučení verze (s odstupem 300 ms). Žádnou cenu nepočítá sám.
- Před objednávkou frontend uloží zaškrtnuté země přes `PUT …/markets`.
- Objednávka (změna 12) zavolá `IShopOrderReadiness.CheckAsync(shopId)`. Ta přepočítá rozsah z uloženého stavu a vrátí `{ ready, blocking[], scope }`. Změna 12 porovná `scope.scopeHash` se `scopeHash` nabídky, při rozdílu vrátí `409 quote.stale`.
- Bez registrované implementace `IPriceQuoteService` vrátí `quote` `503 billing.unavailable` (fail-closed). `GET …/scope` dál funguje.
- Základ je vždy dokončená ukázka (`basis.sampleRunId`). Bez ní `409 quote.sample_not_finished`, bez nároku na ukázku `409 quote.basis_missing` (K rozhodnutí 5).

**AD 9. Ověření vlastnictví.**
- Metody a tokeny:
  - `meta`: `<meta name="eshopguard-site-verification" content="{token}">` v `<head>` úvodní stránky `base_url`;
  - `dns`: záznam TXT `_eshopguard.{domain}` s hodnotou `eshopguard-site-verification={token}`;
  - token je 22 znaků base64url z `RandomNumberGenerator`.
- Kontrola je úloha P0 `shop.verify_ownership` (`resource_class = fetch`). Meta přes stahovač se SSRF ochranou, DNS přes resolver workeru (`DnsClient`). Výsledek je `shop_verifications.status` `verified` / `failed` s kódem `meta_not_found`, `dns_record_not_found`, `token_mismatch`, `fetch_failed`.
- Při úspěchu `shops.ownership_verified_at` a `verification_method`, audit `ownership.verified`.
- `connector` zapíše změna 15 po připojení konektoru.
- `IShopOwnershipPolicy.EnsureAsync(shopId, gate)`:
  - `gate` je `sample` nebo `full_analysis`;
  - vrací `409 shop.ownership_not_verified`, když je `gate` v `Shops:Ownership:RequiredBefore` a e-shop ověřený není;
  - volá ho `SampleService` a `RunService.CreateFullAnalysisAsync` (změna 8);
  - chybějící nastavení zastaví start API a workeru (`OwnershipPolicyOptionsValidator`), K rozhodnutí 1.

**AD 10. Nastavení e-shopu.**
- `modules`:
  - dostupné moduly = `checks.rule_sets` s `enabled = true` (nejnovější verze modulu), jejichž `jurisdictions` se protínají s aktivními zeměmi;
  - nový e-shop dostane všechny dostupné;
  - prázdný seznam → `400 settings.no_module`;
  - nedostupný modul → `400 settings.module_unavailable`.
- `checkHiddenOnSave` vyžaduje `source_mode = connector`. Platforma `biznisweb` ho nepodporuje (architektura, část 5, Kontrola „při uložení“).
- Souběžnost: `If-Match` s `xmin`. Rozdíl → `409 concurrency.conflict` s aktuální verzí.

**AD 11. Stavový automat e-shopu.**
- `ShopStatusTransitions` (čistá funkce sdílená se změnou 12):
  - `draft → sample` (tato změna);
  - `sample → awaiting_payment → analyzing → active` (změny 12 a 8);
  - `active ↔ paused`, `* → canceled` (změna 12).
- Smazání e-shopu s předplatným `trialing`, `active` nebo `past_due` → `409 shop.subscription_active`. Smazání s běžícím během → `409 shop.run_in_progress`.

**AD 12. Limity a audit.**
- Kbelíky (návrhy, K rozhodnutí 11):
  - `shops:create:tenant:{id}` 20 za hodinu;
  - `shops:sample:tenant:{id}` 5 za den;
  - `shops:detect:shop:{id}` 10 za hodinu;
  - `shops:verify:shop:{id}` 20 za hodinu.
- Audit:
  - `shop.created`, `shop.renamed`, `shop.deleted`, `shop.platform_set`, `shop.source_changed`;
  - `sample.started`, `markets.confirmed`, `language.confirmed`, `language.rejected_other_domain`, `language.excluded`, `language.included`;
  - `ownership.verification_created`, `ownership.verified`, `settings.changed` (jen kódy a ID, žádné texty z webu).

## Data Flow

### 3a → 3b: adresa a platforma

```
POST /api/t/{t}/shops {url: "https://www.bylinkovo.sk/"}
  ShopUrlNormalizer → domain "bylinkovo.sk", base_path "/"
  duplicita mezi nesmazanými → 409 shop.already_exists {shopId}
  INSERT shop.shops (status draft, platform unknown, source_mode web, modules = dostupné)
  IJobQueue.Enqueue(shop.detect_platform, P0, dedupe detect:{shopId})      ─┐ stejná transakce
  audit shop.created                                                        ─┘
  IJobCompletionAwaiter.Wait(≤ 8 s)
worker: ShopDetectPlatformHandler → stahovač (SSRF, robots) → PlatformDetector → shops.platform = shoptet
  ◀── 201 ShopDto + DetectionDto {platform: shoptet, confidence: certain, connector: {available: false}, recommendedSource: web}
3b (unknown): PUT …/platform {platform: woocommerce}; PUT …/source {mode: feed, feed: {...}} | {mode: web}
```

### Ukázka zdarma a 3c

```
POST …/sample
  IShopOwnershipPolicy.EnsureAsync(gate: sample)
  RunService.CreateFreeSampleAsync (změna 8: claim + run + job v transakci)  → 409 sample.already_used_for_domain
  shops.status = sample
  ◀── 202 SampleDto {status: queued}
frontend dotazuje GET …/sample (nebo SSE ze změny 11)
worker (změny 7 a 8): stáhne 100 stránek, rozbor zemí a verzí → shop_markets, shop_languages, findings, runs.estimate
GET …/sample → result {pagesChecked: 100, findingCounts: {total: 11, byCheckability: {text: 4, …}}, topFindings[5], exampleFix}
GET …/markets → SK (strong, preselected), CZ (strong, preselected)
GET …/languages → summary all_checked_own_texts; cs 96 %, counted
POST …/quote {activeMarkets: [sk, cz]} → scope (11 668 produktů, 2 verze) + price (změna 12)
uživatel odškrtne CZ → POST …/quote {activeMarkets: [sk]} → scope (5 834, 1 verze) + nová price
uživatel klikne „Zaplatiť“ → PUT …/markets {active: [sk, cz]} → objednávka (změna 12):
  IShopOrderReadiness.CheckAsync → {ready: true, scope.scopeHash} → porovnání se scopeHash nabídky
```

### Ověření vlastnictví

```
POST …/ownership/verifications {method: dns} → token, instrukce (_eshopguard.bylinkovo.sk TXT …)
klient doplní záznam v DNS
POST …/ownership/verifications/{id}/check → úloha shop.verify_ownership (P0) → čekání ≤ 8 s
worker: TXT dotaz → shoda → shop_verifications.status verified, shops.ownership_verified_at
  ◀── 200 VerificationDto {status: verified}
```

## File Changes

**`src/EshopGuard.Core/`:**
- `Platforms/PlatformDetector.cs`, `Platforms/PlatformDetection.cs`, `Platforms/PlatformSignatures.cs`.
- `config/platforms.yaml`: podpisy pro shoptet, upgates, biznisweb, woocommerce, shopify. Jen technické znaky: meta `generator`, hostitelé, hlavičky, cookies, cesty API.

**`src/EshopGuard.Application/Shops/`:**
- `ShopService.cs`, `ShopUrlNormalizer.cs`, `ShopStatusTransitions.cs`, `PlatformService.cs`, `SourceModeService.cs`.
- `SampleService.cs`, `SampleResultReader.cs`.
- `MarketService.cs`, `MarketEvidenceMapper.cs` (jen ověřené citace, kódy znaků).
- `LanguageVersionService.cs`, `LanguageSummaryBuilder.cs`.
- `Scope/ShopScopeCalculator.cs`, `Scope/ShopScope.cs`, `Scope/ScopeHasher.cs`, `Scope/ScopeInputsLoader.cs` (uložený stav + základ z ukázky).
- `Pricing/IPriceQuoteService.cs`, `Pricing/PriceQuoteRequest.cs`, `Pricing/PriceQuote.cs` (smlouva pro změnu 12).
- `Onboarding/OnboardingStateService.cs`, `Onboarding/IShopOrderReadiness.cs`, `Onboarding/ShopOrderReadiness.cs`.
- `Ownership/OwnershipService.cs`, `Ownership/IShopOwnershipPolicy.cs`, `Ownership/ShopOwnershipPolicy.cs`, `Ownership/OwnershipPolicyOptions.cs` + `OwnershipPolicyOptionsValidator.cs`.
- `Settings/ShopSettingsService.cs`, `Settings/ModuleAvailability.cs`.
- `Jobs/IJobCompletionAwaiter.cs` + implementace dotazem na `ops.jobs`.

**`src/EshopGuard.Api/`:**
- `Endpoints/ShopEndpoints.cs`, `Endpoints/OnboardingEndpoints.cs` (`sample`, `scope`, `quote`, `onboarding`), `Endpoints/MarketEndpoints.cs`, `Endpoints/LanguageEndpoints.cs`, `Endpoints/OwnershipEndpoints.cs`, `Endpoints/ShopSettingsEndpoints.cs`.
- `Contracts/Shops/*.cs`: DTO z oddílu výše.
- `appsettings.json`:
  - `Api:InteractiveWaitSeconds`, `Pricing:OwnTextShareThreshold`, `Markets:ReadableLanguages`;
  - `Shops:Detection:MaxBytes`, `Shops:AllowedDevHosts` (jen Development);
  - `Shops:Ownership:RequiredBefore` bez výchozí hodnoty.

**`src/EshopGuard.Jobs/Shops/`:** `ShopDetectPlatformHandler.cs`, `ShopVerifyOwnershipHandler.cs`.

**`src/EshopGuard.Data/Migrations/`:**
- `*_ShopsUniqueActive`: `DROP` jedinečnosti (`tenant_id`, `domain`, `base_path`) a `CREATE UNIQUE INDEX ux_shops_tenant_domain_path_active … WHERE deleted_at IS NULL`.
- `*_ShopsDetection`: `ALTER TABLE shop.shops ADD COLUMN detection jsonb` (signály, jistota, kód selhání, `detected_at`, `platform_source`).

**Testy:**
- `tests/EshopGuard.Core.Tests/Platforms/PlatformDetectorTests.cs` + `Fixtures/platforms/{shoptet,upgates,biznisweb,woocommerce,shopify,unknown,conflict}.html` (uložené úvodní stránky, bez sítě).
- `tests/EshopGuard.Application.Tests/Shops/`: `ShopScopeCalculatorTests.cs` (případy A–J), `ShopUrlNormalizerTests.cs`, `ShopStatusTransitionsTests.cs`, `ModuleAvailabilityTests.cs`.
- `tests/EshopGuard.Api.Tests/Shops/`:
  - `ShopCrudTests.cs`, `DetectionTests.cs`, `SourceModeTests.cs`, `SampleTests.cs`;
  - `MarketsTests.cs`, `LanguageVersionsTests.cs`, `QuoteTests.cs` (s `FakePriceQuoteService`);
  - `OnboardingStateTests.cs`, `OwnershipTests.cs`, `ShopSettingsTests.cs`;
  - `ShopsRoleAndIsolationTests.cs`.
- `tests/EshopGuard.Jobs.Tests/Shops/`: `ShopDetectPlatformHandlerTests.cs`, `ShopVerifyOwnershipHandlerTests.cs` (testovací stahovač a resolver).

**Tabulky:**
- čtení a zápis: `shop.shops`, `shop.shop_markets`, `shop.shop_languages`, `shop.shop_verifications`, `shop.feeds`;
- přes `RunService` ze změny 8: `shop.free_sample_claims`, `checks.runs`, `ops.jobs`;
- čtení: `checks.findings`, `fixes.fix_proposals`, `checks.rule_sets`, `ref.markets`, `shop.connectors`, `billing.subscriptions`;
- zápis: `ops.audit_log`, `ops.rate_limit_buckets`.
