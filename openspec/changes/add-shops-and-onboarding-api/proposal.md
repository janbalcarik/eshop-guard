# Proposal: E-shopy a onboarding v API (připojení, ukázka zdarma, místa prodeje, jazykové verze, cena)

## Intent

**Problém.** Prodejní tok začíná na obrazovkách 3a–3d:
- 3a `Onboarding.dc.html`: adresa e-shopu, „Rozpoznané: Shoptet“, způsob napojení;
- 3b `OnboardingOther.dc.html`: platforma nerozpoznána, výběr platformy, feed nebo web;
- 3c `OnboardingScope.dc.html`: „Kde predávate“, jazykové verze jednou větou, rozsah, „Čo kontrolovať“, objednávka s cenou;
- 3d `VersionDetails.dc.html`: podrobnosti jazykových verzí, „Do ceny“.

Žádná z těchto obrazovek dnes nemá data. Knihovna (změny 5–7) a worker (změna 8) umí ukázku a rozbor míst prodeje a verzí, ale neexistuje API, které by:
- e-shop založilo;
- poznalo platformu;
- spustilo ukázku zdarma jednou na doménu;
- ukázalo výsledek a nechalo klienta potvrdit země a verze;
- spočítalo cenu, která se mění s každým zaškrtnutím.

**Proč teď.** F5. Změna 9 dodala přihlášení, tenanta z adresy a role. Změny 7 a 8 dodávají data (místa prodeje, jazykové verze, výsledky ukázky). Změna 12 (platby) potřebuje hotový rozsah kontroly a stav onboardingu, aby mohla prodat analýzu.

**Přínos.**
- Klient od zadání adresy po cenu bez ruční práce:
  - platforma se pozná sama;
  - ukázka 100 stránek běží na pozadí;
  - země a verze jsou předvyplněné s důvodem.
- Cena je spravedlivá a srozumitelná:
  - do pásma jde součet produktů za každou zaškrtnutou zemi, tedy počet produktů verze kontrolované pro tu zemi (rozhodnutí 2. 10. 2026; dříve jen verze s vlastními texty);
  - odškrtnutí země hned přepočítá cenu;
  - cenu počítá server, frontend ji jen zobrazí;
  - cena z ukázky je garantovaná.
- Klientovi se ukazují jen země a verze, které umíme kontrolovat (SK, CZ). Ostatní se uloží skrytě a nabídnou se, až dostanou podporu.
- Ukázku zdarma nejde opakovat na stejné doméně. Ověření vlastnictví brání kontrole cizích e-shopů na cizí účet; kdy je povinné, rozhodne uživatel (K rozhodnutí 1).

**Fáze:** F5 API (`databaze-a-plan-implementace-2026-10-01.md`, část 8).

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 3: e-shop patří jednomu tenantovi, stejnou doménu můžou mít dva tenanti;
  - část 4: P0 zjištění rozsahu a odhad ceny, P2 bezplatná kontrola;
  - část 6: Čeká na schválení a platbu;
  - část 7: SSRF;
  - část 11: bod 4 ověření vlastnictví, bod 10 druhé místo prodeje a jazykové verze v ceně;
  - část 12: Místa prodeje, Jazykové verze, „Která verze se kontroluje a podle čeho“, Cena rozhodnuto.
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 3.2: `shops`, `shop_markets`, `shop_languages`, `shop_verifications`, `feeds`, `free_sample_claims`;
  - část 3.4: `runs`;
  - část 3.6: `price_lists`, `price_tiers`, `orders`;
  - část 3.9: `ref.markets`;
  - část 9: bod 5 férové užití a garantovaná cena.
- `strategie-a-cenik-2026-09-30.md`: řádek „Bezplatná kontrola“ (100 stránek, jednou na doménu, počty podle závažnosti, 5 nejzávažnějších, 1 ukázka opravy), pásma podle zveřejněných produktů.
- `platby-a-fakturace-2026-10-01.md`: oddíl „Ukázka zdarma → úvodní analýza → sledování“.
- Návrh UI: `Onboarding.dc.html` (3a), `OnboardingOther.dc.html` (3b), `OnboardingScope.dc.html` (3c), `VersionDetails.dc.html` (3d), `Main.dc.html` (pole „Adresa e-shopu“, „Skontrolovať zadarmo“), `Sidebar.dc.html` (přepínač e-shopu), `Monitoring.dc.html` (přepínač „Kontrola pri uložení, aj skrytých produktov“).

## Scope

In scope:
- **E-shopy (CRUD):**
  - založení z adresy s normalizací (doména bez `www`, IDN, `base_path`) a předběžnou kontrolou adresy proti SSRF;
  - jedinečnost (`tenant_id`, `domain`, `base_path`) mezi nesmazanými e-shopy;
  - seznam pro přepínač e-shopu, detail, přejmenování, měkké smazání.
- **Rozpoznání platformy z adresy:**
  - úloha P0 ve workeru stáhne úvodní stránku přes stahovač s ochranou SSRF;
  - knihovní `PlatformDetector` pozná platformu podle technických znaků: meta `generator`, hostitelé souborů, hlavičky, cookies;
  - API počká nejvýš `Api:InteractiveWaitSeconds`, jinak vrátí `202`;
  - ruční volba platformy na 3b;
  - doporučený způsob napojení.
- **Způsob napojení:** `web` (výchozí, funguje vždy), `feed` (adresa a formát Heureka nebo Google, `shop.feeds`), `connector` (jen s připojeným konektorem ze změny 15, jinak `409`).
- **Ukázka zdarma:**
  - spuštění přes `RunService` ze změny 8, který v jedné transakci zapíše nárok domény (`shop.free_sample_claims`), běh i první úlohu;
  - opakovaný nárok → `409 sample.already_used_for_domain` bez prozrazení, kdo doménu použil;
  - strop ukázek na tenanta.
- **Stav a výsledky ukázky** (`GET …/sample`):
  - stav, průběh a pozice ve frontě;
  - co nebylo zkontrolováno a proč;
  - počty nálezů podle skupiny a závažnosti, 5 nejzávažnějších nálezů (kódy a parametry) a 1 ukázka opravy.
- **Místa prodeje** (`shop.shop_markets`):
  - zobrazit jen podporované země (`ref.markets.checks_status` ≠ `none`);
  - důvod ze silných a doručovacích znaků a jen ověřené citace;
  - předvyplnění silného důkazu a doručení, obecný důkaz ne;
  - potvrzení klientem, ruční přidání podporované země, aspoň jedna země.
- **Jazykové verze** (`shop.shop_languages`):
  - souhrn pro 3c a podrobnosti pro 3d: jazyk popisů produktů a podíl přeložených produktů, počet produktů, pro které země a podle kterých zemí se verze kontroluje;
  - potvrzení verze na jiné doméně;
  - vyloučení a vrácení verze v nastavení;
  - nepodporované verze se neukazují.
- **Rozsah kontroly a dynamická cena:**
  - `ShopScopeCalculator` (jaké verze se kontrolují, podle kterých zemí, kolik produktů jde do pásma);
  - `GET …/scope` (uložený stav) a `POST …/quote` (přepočet pro libovolnou kombinaci zaškrtnutých zemí a vyloučených verzí, nic neukládá);
  - částky z ceníku přes rozhraní `IPriceQuoteService`, které implementuje změna 12;
  - garantovaný základ z ukázky.
- **Připravenost k objednávce:**
  - `GET …/onboarding` (krok a blokující kódy);
  - rozhraní `IShopOrderReadiness` pro objednávku ve změně 12;
  - kontrola ověření vlastnictví pro `RunService.CreateFullAnalysisAsync` (změna 8).
- **Ověření vlastnictví:**
  - metody `meta` a `dns` s tokenem;
  - kontrola úlohou P0 ve workeru;
  - stav `ownership_verified_at`;
  - metodu `connector` nastaví změna 15;
  - povinnost podle konfigurace `Shops:Ownership:RequiredBefore`.
- **Nastavení e-shopu:** název, moduly „Čo kontrolovať“ (jen dostupné pro aktivní země), „Kontrola pri uložení, aj skrytých produktov“ (jen s konektorem a podporovanou platformou), výjimky verzí.
- **Audit** změn e-shopu, míst prodeje, verzí, ověření a nastavení.
- **Testy:** oprávnění rolí, izolace přes API (dva tenanti se stejnou doménou), výpočet rozsahu (tabulkové testy), SSRF předběžná kontrola, nárok na ukázku.

Out of scope:
- konektory (OAuth Shoptet, klíče WooCommerce, Upgates, BiznisWeb, Shopify) a jejich stav: změna 15;
- stahování, rozbor míst prodeje a jazykových verzí, plán 100 stránek, zápis výsledků ukázky: změny 7 a 8;
- ceník, částky, DPH, uložení nabídky (`billing.price_quotes`), objednávka, Stripe Checkout: změna 12;
- nálezy, opravy a průběh běhů přes SSE: změna 11 (stav ukázky tu jde číst dotazem);
- opakovaná kontrola po změně míst prodeje u aktivního e-shopu, upozornění „Vyzerá to, že predávate aj …“ a „Odteraz kontrolujeme aj …“: změny 16 a 7;
- obrazovky: změna 13.

## Approach

1. **API tenké, logika v `EshopGuard.Application/Shops` a v knihovně:**
   - `ShopService`, `ShopUrlNormalizer`, `SampleService`, `MarketService`, `LanguageVersionService`;
   - `ShopScopeCalculator` (čistá funkce, tabulkové testy), `OnboardingStateService`, `OwnershipService`, `ShopSettingsService`;
   - `PlatformDetector` je čistá funkce v `EshopGuard.Core/Platforms` a běží ve workeru.
2. **Síťová práce jen ve workeru.** Rozpoznání platformy a kontrola ověření vlastnictví jsou úlohy P0 (`resource_class = fetch`) přes stahovač s ochranou SSRF ze změny 5. API jen zkontroluje tvar adresy (schéma, port, žádná IP, žádné vnitřní jméno). Pak počká na výsledek nejvýš `Api:InteractiveWaitSeconds` (návrh 8 s), jinak vrátí `202` a frontend se zeptá znovu.
3. **Jedno pravidlo rozsahu pro cenu i kontrolu (architektura, část 12):**
   - kontroluje se verze v jazyce každé zaškrtnuté země, když pro ni verze není, hlavní verze;
   - každá kontrolovaná verze se posuzuje podle všech zaškrtnutých zemí, jejichž zákazníci ji můžou číst (čeština a slovenština navzájem, `Markets:ReadableLanguages`);
   - do pásma jde součet produktů za každou zaškrtnutou zemi: počet produktů verze kontrolované pro tu zemi (rozhodnutí 2. 10. 2026; dřívější `counted` a práh vlastních textů zrušeny);
   - `ShopScopeCalculator` používají API (přepočet na 3c), změna 12 (nabídka a objednávka) i změna 8 (`runs.jurisdictions`, `runs.modules`).
4. **Server je autoritativní:**
   - `POST …/quote` se volá při každém zaškrtnutí nebo odškrtnutí země a vrací rozsah, cenu a `scopeHash`;
   - objednávka ve změně 12 spočítá rozsah znovu z uloženého stavu a se `scopeHash` nabídky ho porovná, při rozdílu vrátí `409 quote.stale`.
5. **Garantovaná cena z ukázky:**
   - základ rozsahu (počty produktů a ostatních stránek po verzích) se bere ze souhrnu dokončené ukázky (`runs.estimate`, změna 8), ne z pozdějších běhů;
   - když analýza najde víc produktů, zkontroluje se vše bez doplatku (hlídá změna 8).
6. **Fail-closed:**
   - neznámá platforma → klient volí ručně;
   - napojení `connector` bez připojeného konektoru → `409`;
   - nepodporovaná země → `400`;
   - neověřené citace se nevrací;
   - bez implementace `IPriceQuoteService` vrátí `POST …/quote` `503 billing.unavailable`, rozsah ale zůstává čitelný;
   - chybějící konfigurace povinnosti ověření vlastnictví → API nenastartuje.
7. **Texty skládá frontend.** API vrací kódy a parametry: kódy znaků místa prodeje (`seat`, `tld`, `currency`, `language_version`, `delivery_terms`…), kódy důvodů „Do ceny“, kódy souhrnu verzí. Citace z webu klienta jsou doslovné a ověřené.

## Dependencies

- **9 `add-identity-and-tenants-api`:** `/api/t/{tenantId}`, role, CSRF, ProblemDetails, OpenAPI, audit, `EshopGuard.Application`.
- **8 `add-analysis-runs-in-worker`:**
  - `RunService` (`CreateFreeSampleAsync`, `CreateFullAnalysisAsync`);
  - nárok na ukázku v jedné transakci;
  - souhrn ukázky a garantovaný základ v `runs.estimate` / `runs.stats`;
  - stavy běhu.
- **7 `add-places-of-sale-and-language-versions`:** zápis `shop_markets` (síla důkazu, ověřené citace), `shop_languages` (`own_text_share`, `counted`, `comparison`, `language_share`, `needs_confirmation`), číselník čitelných jazyků.
- **6 `add-multi-jurisdiction-rules-and-rule-texts`:** `rule_sets.jurisdictions`, pořadí podle nejpřísnějšího verdiktu (pro 5 nejzávažnějších nálezů).
- **5 `refactor-library-into-pipeline-steps`:** stahovač s ochranou SSRF a dodržením robots.txt.
- **4 `add-job-queue-and-worker`:** úlohy P0 (`shop.detect_platform`, `shop.verify_ownership`), `ops.rate_limit_buckets`.
- **3 `add-multitenant-data-model`:** schéma `shop`, `checks.runs`, `ref.markets`, RLS.
- **Navazují:**
  - 12 (implementace `IPriceQuoteService`, objednávka volá `IShopOrderReadiness`);
  - 15 (konektor nastaví `source_mode = connector` a ověření `connector`);
  - 13 (obrazovky 3a–3d);
  - 16 (změny míst prodeje u aktivního e-shopu).

## Done when

- `tests/EshopGuard.Api.Tests/Shops/*` projdou:
  - založení, duplicita, smazání;
  - rozpoznání platformy proti uloženým úvodním stránkám (Shoptet, WooCommerce, Shopify, Upgates, BiznisWeb, neznámá) přes testovací stahovač;
  - ukázka a druhý nárok na stejnou doménu;
  - místa prodeje a jazykové verze nad daty ve tvaru ze změny 7;
  - přepočet ceny s `FakePriceQuoteService`.
- `ShopScopeCalculatorTests` pokrývá tabulku případů z `design.md`:
  - SK + CZ se dvěma verzemi;
  - odškrtnutí CZ;
  - verze jen s přeloženým menu;
  - nejistý podíl;
  - verze na jiné doméně čekající na potvrzení;
  - vyloučená verze;
  - verze pro nepodporovaný trh;
  - nad 20 000 produktů.
- **Matice rolí a izolace přes API:** dva tenanti s e-shopem `vegis.sk`. Žádný koncový bod nevrátí data druhého tenanta a čtenář nic nezmění.
- `PlatformDetectorTests` v `EshopGuard.Core.Tests` projdou nad uloženými HTML bez sítě.
- `openspec validate add-shops-and-onboarding-api` projde.

## K rozhodnutí

1. **Kdy je ověření vlastnictví povinné** (architektura, část 11, bod 4: „před plnou analýzou a vždy před publikováním … Bezplatný vzorek bez ověření?“). Změna staví metody `meta`, `dns` a stav `connector`. Povinnost řídí `Shops:Ownership:RequiredBefore` (`sample`, `full_analysis`).
   - Nastavení nemá výchozí hodnotu. Bez něj API nenastartuje, aby se nerozhodlo tiše.
   - Publikování ověření vždy má, protože vyžaduje připojený konektor.
   - Otevřené také: musí se ověřit i verze na jiné doméně (goodie.cz × goodie.sk), nebo stačí potvrzení „Patrí goodie.sk k tomuto e-shopu?“? A má se ověření po čase opakovat?
2. **Překryv se změnou 12 (nabídka ceny a objednávka).** Zadání této změny obsahuje endpoint nabídky ceny a napojení objednávky. Změna 12 má ve svém rozsahu „Nabídka ceny e-shopu (`billing.price_quotes`)“ i výpočet pásma a změna 13 čeká `POST …/quote` ve změně 12. Návrh rozdělení:
   - **10:** pravidlo rozsahu (`ShopScopeCalculator`), koncový bod `POST /api/t/{tenantId}/shops/{shopId}/quote`, stav onboardingu a `IShopOrderReadiness`;
   - **12:** částky z ceníku, slevy, DPH, uložení nabídky (`IPriceQuoteService`), koncový bod objednávky a Checkout.

   Potvrdit a sladit se změnou 12. Změna 8 navíc ukládá „odhad ceny pro zákazníka (pásmo)“ do `runs.estimate`. Návrh: změna 8 ukládá jen základ (počty po verzích), pásmo a částku vždy počítá 12.
3. **Co klient z ukázky uvidí.** Strategie (30. 9.) a změna 8: počty podle závažnosti, 5 nejzávažnějších s vysvětlením, 1 ukázka opravy. Návrh UI 3c (1. 10.) ale odkazuje „Pozrieť výsledky ukážky“ na obrazovku Opravy (všechny nálezy). Tato změna vrací souhrn podle strategie. Zda změna 11 ukáže všechny nálezy ukázky, je otevřené.
4. **Moduly „Čo kontrolovať“ a cena.** Návrh UI dovoluje moduly odškrtnout. Podklady neříkají, jestli to mění cenu. Návrh: cenu nemění (pásmo je podle produktů). Potvrdit.
5. **Garantovaná cena bez ukázky.** Když doménu už nárokoval jiný tenant, nebo e-shop smazal a znovu přidal stejný tenant, ukázka nebude a základ ceny chybí. Návrh změny 8: zjištění rozsahu bez ukázky, pásmo jen podle hlavní verze. Do rozhodnutí vrací `quote` kód `quote.basis_missing` a objednávka není možná (fail-closed).
6. **Rozpoznání platformy podle technických znaků.** Zásada projektu zakazuje slovníky klíčových slov pro klasifikaci. `PlatformDetector` porovnává technické podpisy platformy (meta `generator`, hostitel souborů `cdn.myshoptet.com`, `cdn.shopify.com`, cesta `wp-content/plugins/woocommerce`, hlavičky a cookies). Klasifikace textu to není, při nejistotě vrací `unknown` a rozhodne klient (3b). Potvrdit, že je to v souladu se zásadou.
7. *(Zrušeno 2. 10. 2026: cena za každou zemi, texty verzí se neporovnávají.)* **Práh vlastních textů 20 %** je „návrh, neměřeno“ (architektura, část 12). Je v `Pricing:OwnTextShareThreshold`. Pilot ho má změřit.
8. **Čitelnost jazyků mezi zeměmi.** Rozhodnuto je jen „čeština a slovenština navzájem“. Pro další trhy (de-DE × de-AT) je třeba pravidlo doplnit do `Markets:ReadableLanguages`, nebo do `ref.markets`. Návrh: do konfigurace teď, do číselníku s prvním dalším trhem.
9. **Domovská země a odškrtnutí.** Může klient odškrtnout i domovskou zemi (např. český e-shop, který prodává jen na Slovensko)? Architektura říká „místo prodeje = každá země, kde e-shop prodává, včetně domovské“. Návrh: lze odškrtnout kteroukoli zemi, ale aspoň jedna musí zůstat.
10. **Nesrovnalosti s datovým modelem (změna 3):**
    - Jedinečnost `shops` (`tenant_id`, `domain`, `base_path`) bez podmínky brání znovu přidat smazaný e-shop. Návrh: částečný jedinečný index `WHERE deleted_at IS NULL` (migrace této změny).
    - `shop_languages` nemá počet ostatních stránek po verzích (pro férové užití). Návrh: brát ze souhrnu ukázky v `runs.estimate`.
    - `shops.source_mode` nemá popsanou výchozí hodnotu. Návrh: `web`.
    - Výsledek rozpoznání platformy (signály, jistota, kód selhání, ruční volba) nemá kam uložit a `ops.jobs` nemá sloupec pro výsledek. Návrh: nový sloupec `shops.detection jsonb`.
    - `shop_verifications.token` je čitelný. Je veřejný (stojí v meta značce nebo DNS), proto bez otisku.
11. **Strop ukázek na tenanta** (návrh 5 za den) a strop zakládání e-shopů (návrh 20 za hodinu) proti zneužití ukázek zdarma přes mnoho domén. Neměřeno, potvrdit.
12. **„Preskočiť“ v onboardingu** vede na Přehled bez ukázky. Tato změna pro to nic nepotřebuje, prázdný Přehled řeší změna 13.
