# Proposal: Ceník, platby přes Stripe a faktury v SuperFaktúře

## Intent

**Problém.** EshopGuard zatím nejde prodat. Úvodní analýza se po změně 8 zastaví ve stavu „Čeká na schválení a platbu“ (`runs.status = awaiting_payment`), ale chybí:
- ceník v databázi, ze kterého by se počítala cena e-shopu;
- platba v aplikaci;
- předplatné sledování;
- daňové doklady.

Zákazník přitom podle upřesnění z 1. 10. 2026 platí „přímo v aplikaci jako v běžném e-shopu“: tlačítko „Zaplatiť“ → platební stránka → návrat do aplikace.

**Proč teď.**
- README plánu: „12 je potřeba před prvním prodejem“.
- Ceny čtou obrazovky aplikace (změna 13) i prezentační web (změna 14) z `billing.price_tiers`. CMS ceny nedrží, aby nevznikly dvě verze (architektura, část 12, Prezentační web).
- Od 1. 1. 2027 platí na Slovensku povinná e-faktúra (zákon 385/2025 Z. z.). Doklady proto musí od prvního prodeje vznikat v systému, který umí Peppol.

**Přínos.**
- Zákazník vidí cenu svého e-shopu hned po ukázce zdarma, ještě před platbou. Cena se počítá ze součtu produktů za každou zaškrtnutou zemi (rozhodnutí 2. 10. 2026) a přepočítá se při změně míst prodeje. Hranice pásem jsou v databázi (`billing.price_tiers`).
- Zaplatí kartou, Apple Pay nebo Google Pay na stránce Stripe. Analýza se po potvrzení platby spustí sama.
- Sledování se strhává automaticky za každý e-shop ode dne jeho analýzy. První měsíc je v ceně analýzy.
- Ceny se mění v databázi bez nasazení. Běžící předplatná se převedou sama, po výpovědní lhůtě a s e-mailem.
- Daňové doklady mají jednu číselnou řadu v SuperFaktúře. E-faktúru slovenským firmám od 2027 odesílá SuperFaktúra sama, aplikace Peppol neřeší (rozhodnutí 3. 10. 2026).

Konverzi ani podíl neúspěšných plateb zatím nejde změřit, protože se nic neprodává (neměřeno). Měřit se začne po spuštění, viz strategie, část 5 „Měřit průběžně“.

**Fáze:** F6 (databaze-a-plan-implementace-2026-10-01.md, část 8).

**Podklad:**
- `platby-a-fakturace-2026-10-01.md`: Doporučení v kostce, Varianta B, Jak to zapojit, Ukázka zdarma → úvodní analýza → sledování, Opakovaná platba a fakturace předplatného, Ceny v databázi a jejich změna, E-faktúra na Slovensku, Otevřené body pro účetní;
- `strategie-a-cenik-2026-09-30.md`, část 3: ceník, jednotka pásem (zveřejněné produkty 500 / 2 000 / 5 000 / 20 000), úprava 1. 10. 2026 (sledování po e-shopech), CZ ceny v Kč;
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.1 (`iam.tenants`), 3.2 (`shop.shop_languages.counted`), 3.6 (schéma `billing`), 3.8 (`ops.outbox`, `ops.audit_log`), 3.9 (`ref.markets`), část 8 (F6) a část 9 (body 3 a 5);
- `architektura-multitenant-worker-2026-10-01.md`: část 6 (Čeká na schválení a platbu), část 11 (body 5, 7 a 10), část 12 (Jazykové verze → Cena: garantovaná cena z ukázky, férové užití, dynamický přepočet);
- návrh UI: `Billing.dc.html`, `BillingMobile.dc.html`, `OnboardingScope.dc.html` (blok Objednávka, „Dnes zaplatíte“), `Main.dc.html` (Cenník), `Sidebar.dc.html` („Ďalšia platba“), `VersionDetails.dc.html` (sloupec „Do ceny“).

## Scope

In scope:
- **Ceník v databázi:**
  - verze ceníku po trhu a měně (`billing.price_lists`), pásma (`billing.price_tiers`) a slevy za počet e-shopů (`billing.volume_discounts`);
  - stavy koncept → zveřejněno → vyřazeno, výchozí data SK v EUR a koncept CZ v Kč;
  - admin API pro koncept, úpravu, náhled dopadu a zveřejnění.
- **Synchronizace do Stripe:**
  - Product pro analýzu a pro sledování;
  - Price pro analýzu, měsíc a rok v každém pásmu s `lookup_key` a `transfer_lookup_key`;
  - kupóny slev omezené na produkt sledování;
  - archivace nepoužívaných Price.
- **Ocenění rozsahu e-shopu** (implementace `IPriceQuoteService`, rozhraní je ze změny 10, záznam v `billing.price_quotes`):
  - rozsah a počet produktů do pásma spočítá `ShopScopeCalculator` (změna 10) ze základu z ukázky (součet produktů ve verzích s `shop_languages.counted`, které potřebují aktivní místa prodeje);
  - tato změna k rozsahu přidá pásmo, ceny, slevu, odhad DPH a férové užití (ostatní stránky nejvýš 2× produkty, jinak individuální nabídka; nad 20 000 produktů dohodou);
  - přepočet při změně zemí nebo verzí volá endpoint `POST …/quote` změny 10;
  - garantovaná cena z ukázky se uloží k objednávce. Objednávka znovu spočítá rozsah a při rozdílu `scopeHash` nebo ceníku vrátí `409 quote.stale`.
- **Objednávka a Stripe Checkout** v režimu předplatného:
  - jednorázová položka „Úvodná analýza“ a předplatné sledování se zkušební dobou;
  - karta, Apple Pay a Google Pay, 3-D Secure;
  - návrat do aplikace a uvolnění běhu analýzy ze stavu `awaiting_payment`.
- **Další e-shop uloženou kartou účtu:** tlačítko „Zaplatiť kartou •••• 4242“, předplatné založené přes API, 3-D Secure v aplikaci, když ho banka chce.
- **Webhooky Stripe:**
  - ověření podpisu, deduplikace v `billing.stripe_events`, zpracování ve workeru;
  - vždy se načte aktuální stav objektu ze Stripe;
  - noční dorovnání se Stripe jako pojistka.
- **Předplatné po e-shopech:**
  - jedno předplatné Stripe na e-shop;
  - připomenutí 7 dní před první platbou;
  - neúspěšná platba (upozornění, pozastavení sledování po posledním pokusu, data zůstanou);
  - zrušení ke konci období, obnovení před koncem období, nové zapnutí po skončení.
- **Jedna karta na účet:** výchozí karta zákazníka Stripe pro všechna předplatná. Změna karty přes zákaznický portál Stripe (tok `payment_method_update`), náhradní cesta je SetupIntent.
- **Změny od dalšího období přes Subscription Schedule** (tabulka `billing.subscription_changes`):
  - přechod pásma, když e-shop přeroste pásmo nebo se zmenší;
  - přepočet slevy při přidání nebo zrušení e-shopu;
  - nový ceník u běžících předplatných: zdražení po výpovědní lhůtě, zlevnění od příští platby;
  - e-mail se starou a novou cenou a datem, „Nová cena od …“ v přehledu;
  - výjimka zamčené ceny pro `tenants.founder_until`.
- **Daňový režim odběratele:**
  - slovenská DPH, nebo přenesení daňové povinnosti u ověřeného IČ DPH z jiného státu EU;
  - IČ DPH se ověřuje ve VIES přes Stripe (`customer.tax_id.updated`);
  - nerozhodnuté případy jsou zablokované (fail-closed), dokud je nepotvrdí účetní.
- **Daňové doklady v SuperFaktúře:**
  - faktura se zapsanou platbou kartou za analýzu a za každé zaplacené období;
  - odeslání PDF e-mailem (e-faktúru přes Peppol zajišťuje SuperFaktúra, aplikace ji neposílá ani nesleduje);
  - dobropis při vrácení peněz;
  - PDF v úložišti.
- **Přehled pro obrazovku Predplatné a platby:**
  - e-shopy, stav předplatného, další platba, karta a pásma;
  - faktury s filtrem e-shop a rok, naplánované platby, PDF a ZIP.
- **Testovací režim** Stripe (test klíče, testovací hodiny) a SuperFaktúry (sandbox). Kontrola při startu, že klíč odpovídá režimu.
- **Ochrana klíčů:** maskování v logu a test, který klíče v logu hledá.

Out of scope:
- obrazovky Predplatné a platby, Objednávka a ceník v aplikaci (změna 13);
- ceník na prezentačním webu (změna 14), tato změna k tomu dodá jen veřejný endpoint cen;
- roční platba převodem přes zálohovou fakturu SuperFaktúry s `callback_payment` (Varianta A), SEPA inkaso a virtuální IBAN;
- objednávka ročního sledování: roční Price se synchronizují, ale objednávka nabízí jen měsíc (viz K rozhodnutí);
- varianta „sledování zapnu později“ (Checkout jen na analýzu se `setup_future_usage=off_session`);
- zakládající zákazníci a partnerský program (−30 %, provize, protokol s logem partnera) kromě výjimky zamčené ceny při změně ceníku;
- vynucování férového užití u sledování (N změněných stránek měsíčně doplní pilot), zde se jen zaznamenává;
- vracení peněz z aplikace: vrácení provádí provoz ručně ve Stripe, aplikace na ně jen reaguje dobropisem;
- administrátorské UI ceníku (jen admin API);
- prodej spotřebitelům (B2C), Merchant of Record a jiné brány (Besteron, Barion, Comgate).

## Approach

1. **Nový projekt `src/EshopGuard.Billing`.** Obsahuje logiku. `EshopGuard.Api` a `EshopGuard.Worker` jsou tenké vrstvy (zásada „logika v knihovně“).
2. **Kdo je zdrojem pravdy:**
   - ceny: naše databáze, Stripe je jen zrcadlo;
   - platby: Stripe, naše databáze je zrcadlo z webhooků a z nočního dorovnání;
   - daňové doklady: SuperFaktúra, v databázi je snímek a PDF.
3. **Checkout dostane konkrétní ID objektů Price ze snímku objednávky, ne `lookup_key`.** Zákazník tak zaplatí přesně cenu, kterou viděl v souhrnu „Dnes zaplatíte“. Když se ceník mezitím změní, objednávka nevznikne a ukáže se nová cena.
4. **Webhook zapíše událost a založí úlohu v jedné transakci, pak hned odpoví.** Worker vždy načte aktuální objekt ze Stripe, takže na pořadí webhooků nezáleží. Klíčem dokladu je ID faktury Stripe, takže dvojí doručení dá jen jeden doklad.
5. **Změny od dalšího období jdou jen přes záznamy `subscription_changes`.** Jeden skladač z nich postaví Subscription Schedule. Přechod pásma, slevy a nového ceníku se tak navzájem nepřepíšou.
6. **Fail-closed:** bez zveřejněného ceníku, se zastaralou nabídkou, s nerozhodnutým daňovým režimem nebo s klíčem v nesprávném režimu se neplatí. Když nesedí částka zaplacená ve Stripe s očekávaným daňovým režimem, doklad se nevystaví a jde k ruční kontrole.

## Dependencies

- **10 `add-shops-and-onboarding-api`:**
  - e-shopy, `shop.shop_markets`, `shop.shop_languages` (`counted`, `product_count`);
  - `ShopScopeCalculator` a `scopeHash`;
  - endpoint `POST /api/t/{tenantId}/shops/{shopId}/quote`, který volá `IPriceQuoteService`;
  - `IShopOrderReadiness` (blokující kódy před objednávkou), kód `quote.basis_missing`.
- **9 `add-identity-and-tenants-api`:**
  - přihlášení a role (`viewer` < `editor` < `admin` < `owner`, `.RequireTenantRole`, `403 auth.forbidden_role`);
  - fakturační údaje v `iam.tenants` (`currency` je NULL do první platby);
  - e-maily přes `ops.outbox` a `EmailComposer`.
- **8 `add-analysis-runs-in-worker`:**
  - běh `full_analysis` ve stavu `awaiting_payment`;
  - `RunService.CreateFullAnalysisAsync` a `RunService.MarkOrderPaidAsync`;
  - rozhraní `IRunPaymentGate`, které tato změna implementuje nad `billing.orders`;
  - základ garantované ceny v `runs.estimate`.
- **4 `add-job-queue-and-worker`:** `IJobQueue`, plánovač, `dedupe_key`, `concurrency_key`, opakování s odstupem.
- **3 `add-multitenant-data-model`:** schéma `billing`, `ref.markets`, RLS a role databáze. Tato změna doplní jen chybějící sloupce a tabulku `billing.price_quotes`.
- **2 `add-solution-foundation`:** konfigurace přes user-secrets a proměnné prostředí.
- **Externí:**
  - účet Stripe slovenské s.r.o. s testovacím režimem;
  - účet SuperFaktúra s API, e-faktúrou a sandboxem (tarif Prémiový, jen ten má API);
  - účetní, která potvrdí daňové body;
  - právník, který potvrdí obchodní podmínky (výpovědní lhůta, zrušení).
- **Navazují:**
  - 13 (obrazovky);
  - 14 (ceník na webu);
  - 16 (sledování běží jen u e-shopů s `billing.subscriptions.status` `trialing`, `active` nebo `past_due`; čte to přímo změna 16);
  - 17 (tajné klíče v `.env` na serveru).

## Done when

- **Celý tok v testovacím režimu Stripe a sandboxu SuperFaktúry** projde podle skupiny „Ověření“ v `tasks.md`:
  1. ukázka;
  2. nabídka ceny;
  3. objednávka;
  4. Checkout s testovací kartou včetně 3-D Secure;
  5. webhook;
  6. běh analýzy pokračuje;
  7. faktura se zapsanou platbou kartou;
  8. zkušební měsíc přes testovací hodiny Stripe;
  9. připomenutí;
  10. první měsíční platba a faktura za sledování;
  11. zveřejnění nového ceníku a e-mail se starou a novou cenou;
  12. po lhůtě nová cena na další faktuře.
- **Opakované doručení:** dvakrát doručený webhook `invoice.paid` dá jeden doklad a dvakrát doručený `checkout.session.completed` jedno uvolnění běhu (automatický test).
- **Sleva:** 3. e-shop v účtu dostane kupón jen na sledování, analýza zůstane bez slevy.
- **Neúspěšná platba:** po neúspěšné platbě aplikace ukáže upozornění a po posledním pokusu se sledování pozastaví. Doklad nevznikne.
- **Testy:** `dotnet test` projde včetně `tests/EshopGuard.Billing.Tests` a `tests/EshopGuard.Api.Tests` (billing). Test logů nenajde žádný klíč Stripe ani SuperFaktúry.
- **Validace:** `openspec validate add-billing-and-invoicing` projde.

## K rozhodnutí

1. **Měna pro české zákazníky.** Doporučení je Kč (architektura, část 11, bod 7). Fakturaci v Kč ze slovenské s.r.o. potvrdí účetní.
   - Tato změna umí obojí: ceník se vede pro trh a měnu a `ref.markets.price_list_id` určí, který platí.
   - Do rozhodnutí je ceník CZ v Kč jen koncept. Český tenant bez zveřejněného ceníku nemůže platit (`billing.price_list_missing`).
2. **Výpovědní lhůta při zdražení.** Návrh 30 dní (`price_lists.notice_days`), včetně práva zrušit před změnou. Do obchodních podmínek, ověří právník (databáze, část 9, bod 3).
3. **Daňové body pro účetní** (platby, Otevřené body 1–8):
   - firma bez DIČ z jiného státu, v ČR „identifikovaná osoba“: do rozhodnutí je Checkout zablokovaný kódem `billing.tax_treatment_undetermined`;
   - je předplatné elektronicky poskytovaná služba a je potřeba OSS?
   - datum zdanitelného plnění a 15denní lhůta e-faktúry u platby předem;
   - dobropisy jako e-faktúra;
   - jedna číselná řada v SuperFaktúře a doklady Stripe jen jako interní záznam;
   - přesné znění textu o přenesení daňové povinnosti na faktuře;
   - DPH z poplatků Stripe;
   - zda se týká zákon 384/2025 Z. z.
4. **Sleva od 3. e-shopu.** Návrh UI i strategie mají „[X] %“, číslo chybí. Do rozhodnutí je `volume_discounts` prázdné a sleva vypnutá. API ji nevrací a UI ji nesmí ukázat.
5. **Délka zkušební doby (nesrovnalost).** Platby uvádějí „30denní zkušební dobu“. Návrh UI ale ukazuje platbu 1. 10. → první platba 1. 11. (31 dní), zatímco obrazovka Monitoring „prvý mesiac v cene do 31. 10.“. Strategie navíc říká „fakturační den = den analýzy“.
   - Doporučení: zkušební doba do stejného dne příštího měsíce přes `subscription_data.trial_end`.
   - Tato změna to má jako nastavení `Billing:TrialMode` (`calendar_month` / `days_30`), výchozí `calendar_month`.
6. **Zvýšení tarifu (nesrovnalost).** Platby v oddíle „Opakovaná platba“ uvádějí „Zvýšení: hned, doplatek (`always_invoice`)“. Novější oddíl „Ceny v databázi“ a obrazovka Billing („vyššie pásmo platí od ďalšieho obdobia“) říkají „od dalšího období“. Tato změna dělá vše od dalšího období, bez poměrného přepočtu a bez dobropisů.
7. **Předstih upozornění při přechodu do vyššího pásma.** Návrh 7 dní (`Billing:TierChangeNoticeDays`). Když další období začíná dřív, platí změna až od období následujícího.
8. **Roční sledování kartou.** `price_tiers.monitoring_yearly` existuje a strategie uvádí „ročně 2 měsíce zdarma“, návrh UI rok nenabízí. Tato změna roční Price jen synchronizuje, objednávka je měsíční.
9. **Zamčená cena zakládajících zákazníků.** Zamyká jen verzi ceníku (zdražení se neuplatní do `founder_until`), nebo i pásmo? Tato změna zamyká jen ceník, přechod pásma platí.
10. **Férové užití.** Hranice „ostatní stránky nejvýš 2× počet produktů“ je návrh (databáze, část 9, bod 5) a upřesní ji pilot. Je v `price_lists.fair_use_other_pages_factor`. N změněných stránek měsíčně u sledování zatím chybí.
11. **Vrácení peněz při selhání analýzy po zaplacení.** Návrh: ručně ve Stripe, aplikace vystaví dobropis. Automatické vracení se zde nestaví.
12. **Cena s DPH v souhrnu objednávky.** Návrh UI ukazuje jen „[CENA] € bez DPH“, částku s DPH zákazník uvidí až na stránce Stripe, ale vždy před potvrzením platby. Doporučení: v souhrnu ukázat i DPH podle daňového režimu. Tato změna ji vrací v nabídce jako `vat_preview`.
13. **Zákaznický portál Stripe.** Zda jde vypnout seznam faktur Stripe, je neověřené (platby, Jak to zapojit). Když nejde, změna karty poběží přes SetupIntent v aplikaci a portál se nepoužije.
14. **Dvě slevy v jednom Checkoutu** (zakládající zákazník na analýzu a sleva od 3. e-shopu na sledování). Ověřit v dokumentaci Stripe, zda Checkout přijme víc než jednu slevu. Týká se to až zakládajících zákazníků, kteří tu nejsou.
15. **Překryv se změnami 3, 8 a 10:**
    - **Změna 3:** tabulky schématu `billing` zakládá změna 3. Tato změna k nim doplní sloupce, které chybí (seznam v `design.md`, File Changes), a novou tabulku `billing.price_quotes`. Při implementaci se porovná se skutečným stavem migrací.
    - **Změna 10:** přijímá se rozdělení z jejího K rozhodnutí 2. Pravidlo rozsahu, `POST …/quote` a `IShopOrderReadiness` patří změně 10. Částky, slevy, DPH, uložení nabídky, objednávka a Checkout patří této změně. `POST …/quote` stav e-shopu nemění, ale `IPriceQuoteService` zapíše do `billing.price_quotes` záznam ceny, kterou zákazník viděl, s jedinečností (`shop_id`, `scope_hash`, `price_list_id`), aby šla dohledat garantovaná cena.
    - **Změna 8:** `AnalysisPriceEstimator` ukládá do `runs.estimate` pásmo a částku. Návrh shodný se změnou 10: změna 8 ukládá jen základ (počty po verzích a ostatní stránky), pásmo a částku počítá vždy `IPriceQuoteService`. Jinak by existovaly dva výpočty pásma.
16. **SuperFaktúra:** veřejná dokumentace API je ověřená (2. 10. 2026, `design.md`, sekce Doklady): sandbox, autorizace, doklad, platba, odeslání, PDF, ochrana proti duplicitě (`checksum`), měna CZK a jazyk dokladu. Otevřené zůstává:
    - **E-faktúra přes Peppol:** rozhodnuto 3. 10. 2026, aplikace ji neřeší; zajišťuje ji SuperFaktúra interně. Před 1. 1. 2027 provoz ověří u SuperFaktúry, že doklady založené přes API slovenským firmám jako e-faktúru skutečně odcházejí.
    - **Tarif našeho účtu u SuperFaktúry:** přístup k API má jen Prémiový (16,99 € měsíčně), zjištěno 3. 10. 2026. Účet se zakládá s tímto tarifem (úkol 0.1).
    - **K ověření v sandboxu:** dobropis (`type = cancel` a `parent_id`), název pole data platby (`date`, nebo `created`), stav HTTP při překročení limitu.
17. **Sémantika `dedupe_key` ve frontě (změna 4).** Tato změna počítá s tím, že `dedupe_key` je jedinečný, dokud úloha existuje (hotové se mažou po 7 dnech). Proto klíče opakovatelných úloh nesou okno nebo otisk (`schedule:{subscriptionId}:{otisk fází}`, `price-sync:{priceListId}:{publishRequestId}`). Sladit se změnou 4.
