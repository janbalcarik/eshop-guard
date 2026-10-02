# Tasks

## 1. E-shopy: založení, seznam, úprava, smazání

- [x] 1.1 `Application/Shops/ShopUrlNormalizer.cs`:
  - schéma, port a `user:heslo@`;
  - IP v4 a v6, vnitřní jména;
  - IDN → punycode, `www.`, `base_path`;
  - `Shops:AllowedDevHosts` jen v Development a Test.
- [x] 1.2 Migrace `Data/Migrations/*_ShopsUniqueActive` (částečný jedinečný index `ux_shops_tenant_domain_path_active … WHERE deleted_at IS NULL`) a `*_ShopsDetection` (sloupec `shops.detection jsonb`).
- [x] 1.3 `Application/Shops/ShopService.cs`:
  - `CreateAsync`: dostupné moduly jako výchozí, `source_mode = web`, kbelík `shops:create:tenant:*`, úloha `shop.detect_platform` ve stejné transakci, audit;
  - `RenameAsync` (`If-Match` / `xmin`);
  - `SoftDeleteAsync` s kontrolou předplatného a běžícího běhu.
- [x] 1.4 `Api/Endpoints/ShopEndpoints.cs`: `GET /shops`, `POST /shops`, `GET /shops/{shopId}`, `PATCH /shops/{shopId}`, `DELETE /shops/{shopId}`. DTO `ShopListItemDto`, `ShopDto` v `Contracts/Shops/`. Role podle matice změny 9.
- [x] 1.5 `Application/Shops/ShopStatusTransitions.cs` (sdílené se změnou 12) a test `ShopStatusTransitionsTests` (povolené a zakázané přechody).
- [x] 1.6 Test `ShopUrlNormalizerTests`:
  - `https://www.Bylinkovo.sk/` → `bylinkovo.sk` + `/`;
  - `http://169.254.169.254/`, `http://[::1]/`, `http://intranet/`, `https://eshop.sk:8443/`, `https://user:pw@eshop.sk/` → odmítnuto;
  - `https://kvetináč.sk` → punycode.
- [x] 1.7 Test `ShopCrudTests`:
  - založení s úlohou v téže transakci;
  - duplicita → `409` s `shopId`;
  - stejná doména u druhého tenanta → `201`;
  - smazání s předplatným → `409`;
  - po smazání jde stejnou doménu přidat znovu.

## 2. Rozpoznání platformy

- [x] 2.1 `Core/Platforms/PlatformDetector.cs`, `PlatformDetection`, `PlatformSignatures` a `config/platforms.yaml`:
  - jen technické podpisy (meta `generator`, hostitelé, hlavičky, cookies, cesty API);
  - `certain` při 2 a více podpisech, `likely` při 1;
  - konflikt → `unknown`.
- [x] 2.2 Uložené úvodní stránky `tests/EshopGuard.Core.Tests/Fixtures/platforms/{shoptet,upgates,biznisweb,woocommerce,shopify,unknown,conflict}.html` + hlavičky. Bez textů zákazníků navíc, jen veřejné HTML úvodní stránky.
- [x] 2.3 Test `PlatformDetectorTests`: každá platforma poznaná, `unknown` u obyčejné stránky, `unknown` při konfliktu, text stránky se slovem „Shopify“ v článku nezmění výsledek.
- [x] 2.4 `Jobs/Shops/ShopDetectPlatformHandler.cs`:
  - stahovač se SSRF ochranou a robots.txt ze změny 5;
  - strop `Shops:Detection:MaxBytes` a 10 s;
  - zápis `shops.platform`, `base_url` a `detection`;
  - přesměrování na jinou doménu jen jako `redirectedToOtherDomain`.
- [x] 2.5 `Application/Jobs/JobCompletionAwaiter.cs`: dotaz na řádek `ops.jobs` každých 250 ms do `Api:InteractiveWaitSeconds`.
- [x] 2.6 `GET /shops/{shopId}/detection`, `POST /shops/{shopId}/detection` (kbelík `shops:detect:shop:*`, `409 detection.in_progress`), `PUT /shops/{shopId}/platform` (`platformSource = user`, audit `shop.platform_set`).
- [x] 2.7 Test `ShopDetectPlatformHandlerTests`:
  - testovací stahovač: Shoptet, timeout → `failed/timeout`;
  - robots.txt zakazuje `/` → `failed/robots_blocked`;
  - přesměrování `bylinkovo.cz` → `bylinkovo.sk`.
- [x] 2.8 Test `DetectionTests` (API): odpověď do 8 s s výsledkem; pomalá úloha → `pending` a pozdější `GET` s výsledkem (`FakeTimeProvider`, ručně dokončená úloha).

## 3. Způsob napojení

- [x] 3.1 `Application/Shops/SourceModeService.cs`:
  - `web`;
  - `feed` (normalizace adresy, `format` `heureka`/`google`, `shop.feeds`);
  - `connector` jen se `shop.connectors.status = connected`;
  - `409 shop.run_in_progress` při běhu.
- [x] 3.2 `PUT /shops/{shopId}/source` a audit `shop.source_changed`.
- [x] 3.3 Test `SourceModeTests`: feed uložen, neplatný formát → `400 feed.format_unknown`, konektor bez připojení → `409`, změna při běžící analýze → `409`.

## 4. Ukázka zdarma

- [x] 4.1 `Application/Shops/SampleService.StartAsync`:
  - stav `draft`;
  - `IShopOwnershipPolicy.EnsureAsync(gate: sample)`;
  - kbelík `shops:sample:tenant:*`;
  - `RunService.CreateFreeSampleAsync` (změna 8);
  - `shops.status = sample`;
  - audit `sample.started`.
- [x] 4.2 Převod konfliktu nároku ze změny 8 na `409 sample.already_used_for_domain` bez údajů o druhém tenantovi.
- [x] 4.3 `Application/Shops/SampleResultReader.cs`:
  - průběh a fronta (`IRunQueueEstimator`, když chybí, `null`);
  - `notChecked` po důvodech;
  - počty podle skupiny a závažnosti;
  - 5 nálezů podle `VerdictStrictness` (změna 6);
  - 1 ukázka opravy s `recheck_status = ok`.
- [x] 4.4 `POST /shops/{shopId}/sample` a `GET /shops/{shopId}/sample` (`SampleDto`, `SampleResultDto`, `VerdictDto`).
- [x] 4.5 Test `SampleTests`:
  - první ukázka → `202`, nárok a běh v jedné transakci (při chybě po nároku se nic neuloží);
  - druhý tenant na stejné doméně → `409` bez ID tenanta;
  - stav `sample` → `409 sample.not_allowed_in_status`;
  - 6. ukázka za den → `429`.
- [x] 4.6 Test `SampleResultTests` nad daty ve tvaru ze změny 8:
  - `finished` se 11 nálezy;
  - `partial` s výčtem nezkontrolovaných;
  - odpověď neobsahuje texty vysvětlení, jen `ruleId` a `params`;
  - bez běhu → `404 sample.not_started`.

## 5. Místa prodeje

- [x] 5.1 `Application/Shops/MarketEvidenceMapper.cs`: z `shop_markets.evidence` jen ověřené citace a kódy znaků (`seat`, `tld`, `currency`, `language_version`, `delivery_terms`, `local_authority`) s parametry.
- [x] 5.2 `Application/Shops/MarketService.cs`:
  - `GetAsync`: jen `checks_status` ≠ `none`, `preselected` podle síly důkazu;
  - `ConfirmAsync`: transakce, `source = user` u nové země, `confirmed_by`/`at`, aspoň jedna země, `409 markets.locked_during_run`, audit `markets.confirmed`.
- [x] 5.3 `GET /shops/{shopId}/markets` a `PUT /shops/{shopId}/markets` (`ShopMarketsDto`, `ShopMarketDto`).
- [x] 5.4 Test `MarketsTests`:
  - SK a CZ předvybrané se znaky;
  - obecný důkaz nepředvybraný;
  - neověřená citace chybí;
  - Polsko (`checks_status = none`) chybí;
  - `active: []` → `400`;
  - `active: ["pl"]` → `400 markets.unsupported`;
  - ruční přidání CZ → `source = user`.

## 6. Jazykové verze

- [x] 6.1 `Application/Shops/LanguageVersionService.cs` a `LanguageSummaryBuilder.cs`:
  - verze bez `unsupported`;
  - `checked`, `checkedReason`, `jurisdictions` z `ShopScopeCalculator`;
  - `countedReason`, `comparison` s příklady jako kódy;
  - `summary.kind`.
- [x] 6.2 `POST /shops/{shopId}/languages/{language}/confirmation` (jen `needs_confirmation`, audit `language.confirmed` / `language.rejected_other_domain`).
- [x] 6.3 `PUT /shops/{shopId}/languages/{language}/exclusion` (`400 language.last_checked_version`, `409 language.awaiting_confirmation`, audit `language.excluded` / `language.included`).
- [x] 6.4 Test `LanguageVersionsTests`:
  - bylinkovo (sk + cs 96 %) → `all_checked_own_texts`;
  - cs s 3 % → `below_threshold`;
  - goodie.cz čeká na potvrzení a po `false` je `excluded`;
  - polská verze se nevrací;
  - vyloučení poslední verze → `400`.

## 7. Rozsah a dynamická cena

- [x] 7.1 `Application/Shops/Scope/ShopScopeCalculator.cs` (čistá funkce podle AD 7), `ShopScope`, `ScopeHasher` (SHA-256 kanonického JSON), konfigurace `Markets:ReadableLanguages` (`productTotal` = součet produktů za zaškrtnuté země, rozhodnutí 2. 10. 2026; `Pricing:OwnTextShareThreshold` zrušen).
- [x] 7.2 `Application/Shops/Scope/ScopeInputsLoader.cs`: uložené země a verze, základ ze souhrnu dokončené ukázky (`runs.estimate`, změna 8), `quote.basis_missing` bez ukázky a bez nároku.
- [x] 7.3 Test `ShopScopeCalculatorTests`: případy A–J z `design.md` jako tabulkový test včetně `scopeHash`, který se změní se zeměmi i s vyloučením.
- [x] 7.4 `Application/Shops/Pricing/IPriceQuoteService.cs`, `PriceQuoteRequest`, `PriceQuote` (smlouva pro změnu 12). Registrace bez implementace → `quote` vrátí `503 billing.unavailable`.
- [x] 7.5 `GET /shops/{shopId}/scope` a `POST /shops/{shopId}/quote` v `Api/Endpoints/OnboardingEndpoints.cs`:
  - kombinace zemí a vyloučených verzí z těla, nic se neukládá;
  - kódy `quote.sample_not_finished`, `quote.basis_missing`, `markets.none_selected`, `markets.unsupported`.
- [x] 7.6 `tests/EshopGuard.Api.Tests/Fakes/FakePriceQuoteService.cs` (pásma 500 / 2 000 / 5 000 / 20 000, nad 20 000 `isCustom`; jen pro testy, ne pro produkci).
- [x] 7.7 Test `QuoteTests`:
  - SK + CZ → 11 668, SK → 5 834 a jiný `scopeHash`;
  - `shop_markets` se nezmění;
  - ukázka nedoběhla → `409`;
  - bez implementace → `503` a `GET …/scope` dál `200`;
  - viewer → `403`.

## 8. Onboarding a připravenost k objednávce

- [x] 8.1 `Application/Shops/Onboarding/OnboardingStateService.cs`: krok (`connect`, `sample`, `scope`, `payment`, `analysis`, `done`) a blokující kódy z požadavku „Připravenost k objednávce“.
- [x] 8.2 `IShopOrderReadiness` + `ShopOrderReadiness.CheckAsync`: přepočet rozsahu z uloženého stavu, `scopeHash`, blokující kódy (pro změnu 12).
- [x] 8.3 `GET /shops/{shopId}/onboarding` (`OnboardingStateDto`).
- [x] 8.4 Test `OnboardingStateTests`:
  - po založení `connect`, po ukázce `scope`;
  - nepotvrzené země → `markets.not_confirmed`;
  - verze čeká → `languages.confirmation_pending`;
  - po uložení jiných zemí se `scopeHash` liší od nabídky.

## 9. Ověření vlastnictví

- [x] 9.1 `Application/Shops/Ownership/OwnershipPolicyOptions.cs` + `OwnershipPolicyOptionsValidator` (`ValidateOnStart`; chybějící `Shops:Ownership:RequiredBefore` zastaví API i worker).
- [x] 9.2 `IShopOwnershipPolicy` + `ShopOwnershipPolicy.EnsureAsync(shopId, gate)` → `409 shop.ownership_not_verified`. Zapojit do `SampleService` a předat změně 8 pro `RunService.CreateFullAnalysisAsync`.
- [x] 9.3 `Application/Shops/Ownership/OwnershipService.cs`: vytvoření ověření (token 22 znaků, instrukce `meta` a `dns`), `409 ownership.already_verified`, kbelík `shops:verify:shop:*`.
- [x] 9.4 `Jobs/Shops/ShopVerifyOwnershipHandler.cs`:
  - `meta` přes stahovač (jen `<head>`, robots.txt);
  - `dns` přes `DnsClient` (TXT `_eshopguard.{domain}`);
  - kódy selhání;
  - zápis `shops.ownership_verified_at` a `verification_method`.
- [x] 9.5 `GET /shops/{shopId}/ownership`, `POST /shops/{shopId}/ownership/verifications`, `POST /shops/{shopId}/ownership/verifications/{verificationId}/check` (čekání přes `JobCompletionAwaiter`).
- [x] 9.6 Test `ShopVerifyOwnershipHandlerTests`: značka nalezena, značka chybí, jiný token, TXT nalezen, DNS bez záznamu.
- [x] 9.7 Test `OwnershipTests` (API):
  - politika `[sample]` → ukázka neověřeného e-shopu `409`;
  - politika `[full_analysis]` → blokující kód v `IShopOrderReadiness`;
  - start bez nastavení selže.

## 10. Nastavení e-shopu

- [x] 10.1 `Application/Shops/Settings/ModuleAvailability.cs`: dostupné moduly z `checks.rule_sets` (`enabled`, nejnovější verze) podle průniku `jurisdictions` s aktivními zeměmi. Test `ModuleAvailabilityTests` (CZ bez `eco` a `dur`, SK se všemi).
- [x] 10.2 `Application/Shops/Settings/ShopSettingsService.cs`:
  - název, moduly (aspoň jeden, jen dostupné);
  - `checkHiddenOnSave` jen s konektorem a ne BiznisWeb;
  - `If-Match`;
  - audit `settings.changed`.
- [x] 10.3 `GET /shops/{shopId}/settings` a `PATCH /shops/{shopId}/settings` (`ShopSettingsDto`).
- [x] 10.4 Test `ShopSettingsTests`:
  - `eco` u CZ → `400 settings.module_unavailable`;
  - prázdné moduly → `400`;
  - kontrola při uložení u `web` → `409`;
  - souběžná úprava → `409 concurrency.conflict`.

## 11. Ověření

- [x] 11.1 Test `ShopsRoleAndIsolationTests`:
  - všechny koncové body této změny pro role viewer, editor, admin a owner podle matice;
  - dva tenanti s e-shopem `vegis.sk`;
  - e-shop tenanta B pod adresou tenanta A → `404` na každém koncovém bodu;
  - zápisy do `shop_markets`, `shop_languages` a `shop_verifications` tenanta B se nezmění.
- [x] 11.2 Doplnit nové koncové body do `RoleMatrixTests` a snímku `Snapshots/openapi-v1.json` (změna 9) a ověřit `x-problem-codes`.
- [x] 11.3 Kontrola logů (`LogRedactionTests`): logy rozpoznání, ukázky a ověření neobsahují citace z webu ani HTML.
- [x] 11.4 Lokální proklik bez placených služeb:
  1. testovací e-shop (`serve-fixture`, `Fixtures/site-sk`) přes `Shops:AllowedDevHosts`;
  2. založení a rozpoznání;
  3. ukázka s `MockJevClient` a `MockRewriteClient` (změna 8);
  4. místa prodeje, verze, `quote` s `FakePriceQuoteService`.
  - *Provedeno 2. 10. 2026 s odchylkou (design, odchylka 16):* worker vnitřní síť nestahuje (změna 8), rozpoznání a ukázka na `localhost:8000` proto skončily kódem chyby; zbytek nad daty ve tvaru rozboru ukázky. `quote` bez `FakePriceQuoteService` (ta je jen v testech) vrátil `503 billing.unavailable`.
- [x] 11.5 Živá ukázka na skutečném e-shopu (Jev a OpenAI jsou placené):
  - před spuštěním odhad ceny (strategie: ~0,5–1 USD na ukázku; rozbor zemí a verzí ~0,05 USD na e-shop podle měření 1. 10. 2026) a souhlas uživatele;
  - bez souhlasu se nespouští. *Souhlas 2. 10. 2026, spouští uživatel lokálně (krok 3.12 v `LOKALNI-OVERENI.md`, skript `deploy/dev/live-sample.ps1`, e-shop www.naturfyt.sk, strop 1,00 USD). Provedeno 2. 10. 2026 (`deploy\dev\run-live-sample.cmd`):*
    - *rozpoznání `shoptet`, `certain`, signály `shoptet.cdn_host` a `shoptet.web_author`;*
    - *ukázka `finished` za 2 min 46 s: 100 stránek zkontrolováno, 3 937 nezkontrolováno (`over_limit`), 31 nálezů (5 vysoká, 25 střední, 1 nízká závažnost), ukázka opravy s `recheckStatus = ok`;*
    - *země jen SK (`strong`, citace sídla a dopravy jen v rámci SR), jedna verze `sk`;*
    - *rozsah: 4 036 ostatních stránek, počet produktů neznámý (`scope.product_count_unknown`; jedna sitemap bez produktové, počet dodá konektor změny 15, K-ROZHODNUTI A21);*
    - *skutečná cena 0,317 USD (Jev 0,169, OpenAI 0,148), strop 1,00 USD;*
    - *v logech API a workeru nejsou klíče, hesla ani tokeny.*
- [x] 11.6 `dotnet build` a `dotnet test` projdou.
- [x] 11.7 `openspec validate add-shops-and-onboarding-api` projde.
