# Tasks

## 0. Předpoklady mimo kód

- [ ] 0.1 Založit účet Stripe pro slovenskou s.r.o.:
  - testovací režim;
  - v Dashboardu povolit karty, Apple Pay a Google Pay;
  - zapnout Stripe Tax s registrací SK;
  - nastavit opakování neúspěšných plateb (Smart Retries) a po posledním pokusu „cancel subscription“.

  Hotovo: snímek nastavení v interní poznámce bez klíčů.
- [ ] 0.2 Ověřit u SuperFaktúry:
  - tarif s API a e-faktúrou;
  - sandbox;
  - endpointy pro doklad, zápis platby (`CARD`), odeslání, PDF, dobropis, e-faktúru a její stav;
  - vyhledání dokladu podle vlastního klíče;
  - měnu CZK a jazyk dokladu.

  Výsledek zapsat do `design.md` (sekce Doklady) a chybějící body do proposal, K rozhodnutí 16.
- [ ] 0.3 Předat účetní otevřené body z proposal, K rozhodnutí 3. Do jejich potvrzení zůstávají případy `undetermined` zablokované.

  S uživatelem rozhodnout body 1, 2, 4, 5 a 7 v K rozhodnutí:
  - měnu pro CZ;
  - výpovědní lhůtu;
  - slevu od 3. e-shopu;
  - délku zkušební doby;
  - předstih upozornění u pásma.

  Výsledek zapsat do `BillingSeed.sql` a `appsettings.json`.

## 1. Projekt a konfigurace

- [x] 1.1 Založit `src/EshopGuard.Billing/EshopGuard.Billing.csproj`:
  - reference na `EshopGuard.Data` a `EshopGuard.Jobs`, balíček `Stripe.net` v pevné verzi;
  - přidat do `EshopGuard.sln`.

  Test: `dotnet build` projde.
- [x] 1.2 Přidat `BillingOptions`:
  - sekce `Billing:Stripe`, `Billing:SuperFaktura`, `TrialMode`, `TierChangeNoticeDays`, `CheckoutExpiresMinutes`, `PublicAppUrl`;
  - `ToString()` s maskou;
  - čtení z user-secrets a proměnných prostředí;
  - doplnit prázdné klíče do `.env.example`.

  Test: `BillingOptionsTests.ToString_masks_secrets`.
- [x] 1.3 Přidat `BillingOptionsValidator`:
  - shoda `Mode` s předponou klíče (`sk_test_`/`rk_test_` × `sk_live_`/`rk_live_`);
  - živý klíč mimo `Production` znamená chybu;
  - chybějící `WebhookSecret` znamená chybu;
  - `SuperFaktura:Sandbox` mimo `Production` musí být `true`.

  Testy `BillingOptionsValidatorTests` (4 případy z požadavku „Testovací režim a ochrana platebních klíčů“).
- [x] 1.4 Přidat `IStripeGateway` a `StripeGateway`:
  - jen metody potřebné v této změně;
  - každé zápisové volání s parametrem `idempotencyKey`;
  - vypnuté logování těl a hlaviček.

  Test: `StripeGatewayTests.Write_calls_require_idempotency_key` (analyzátor nebo reflexe).
- [x] 1.5 Přidat `ServiceCollectionExtensions.AddEshopGuardBilling` a napojit ho v `EshopGuard.Api/Program.cs` a `EshopGuard.Worker/Program.cs`. Test: start API s testovací konfigurací ve `WebApplicationFactory`.

## 2. Datový model

- [x] 2.1 Porovnat stav schématu `billing` ze změny 3 se seznamem v `design.md`, File Changes. Zapsat rozdíly do komentáře migrace.
- [x] 2.2 Migrace `AddBilling` v `src/EshopGuard.Data/Migrations`:
  - doplněné sloupce `price_lists`, `price_tiers` (tři `lookup_key_*`), `volume_discounts`, `orders`, `subscriptions`, `subscription_changes`, `invoices`, `stripe_events` a `iam.tenants`;
  - částečné jedinečné indexy (otevřená objednávka na e-shop, běžící předplatné na e-shop, `invoices.source_key`).

  Test: migrace proběhne jako `eshopguard_owner` na prázdné i na naplněné databázi.
- [x] 2.3 Tabulka `billing.price_quotes` s RLS (`FORCE ROW LEVEL SECURITY`, politika `tenant_id`), konfigurace EF `PriceQuoteConfiguration`. Test v `EshopGuard.Data.Tests/RlsIsolationTests`: tenant A nevidí nabídky tenanta B přes EF ani čistým SQL jako `eshopguard_app`.
- [x] 2.4 `src/EshopGuard.Data/Seed/BillingSeed.sql`:
  - ceník SK/EUR a CZ/CZK jako koncept s pásmy podle `design.md`, `notice_days = 30`, `fair_use_other_pages_factor = 2`;
  - `volume_discounts` prázdné;
  - práva: `eshopguard_app` jen čte globální ceník.

  Test: `BillingSeedTests` ověří 5 pásem na ceník a hranice bez mezer a překryvů.

## 3. Ceník a synchronizace do Stripe

- [x] 3.1 Implementovat `TierResolver.Resolve(priceList, countedProducts)` s hranicemi včetně a pásmem `custom`. Testy `TierResolverTests`: 0, 1, 500, 501, 20 000 a 20 001 produktů.
- [x] 3.2 Implementovat `PriceListService`:
  - koncept jako kopie aktivního ceníku;
  - úpravy pásem a slev jen ve stavu `draft`;
  - kontrola souvislosti pásem;
  - audit každé změny.

  Testy: úprava zveřejněného ceníku vrátí `billing.price_list_not_editable`.
- [x] 3.3 Implementovat `PriceListService.PreviewImpactAsync`: počty předplatných se zdražením, zlevněním a beze změny a nejbližší `effective_at`. Test na 40 předplatných podle scénáře „Náhled dopadu před zveřejněním“.
- [x] 3.4 Implementovat `StripeCatalogSync.SyncAsync(priceListId)`:
  - Product „analýza“ a „sledování“ (ID v `ops.system_settings` `billing:stripe_products:{mode}`);
  - 3 Price na pásmo (`tax_behavior = exclusive`, `lookup_key` bez převodu);
  - kupóny `percent_off`, `duration = forever`, `applies_to.products`;
  - klíče idempotence `price:{priceListId}:{tier}:{kind}` a `coupon:{priceListId}:{from}`;
  - stav `published` až po uložení všech ID.

  Testy s `FakeStripeGateway`: úspěch (12 Price, 1 kupón) a pád po 5 Price s dokončením při opakování.
- [x] 3.5 Implementovat `PriceListService.ActivateAsync` (úloha `billing.activate_price_list`, `not_before = valid_from`):
  - převod `lookup_key` s `transfer_lookup_key = true`;
  - `ref.markets.price_list_id`;
  - předchozí ceník `retired`;
  - založení `billing.schedule_price_list_transfer`.

  Test: aktivace dvakrát za sebou nic nezdvojí.
- [x] 3.6 Admin API `AdminPriceListEndpoints`:
  - koncept, úpravy, náhled dopadu, zveřejnění s `valid_from`;
  - jen administrátorská role, audit.

  Testy oprávnění v `EshopGuard.Api.Tests/Billing/AdminPriceListEndpointsTests`.
- [x] 3.7 Veřejný endpoint `GET /api/public/prices?market=sk`: aktivní ceník bez ID Stripe, s měnou a pásmy, cache 5 min. Test: koncept se nevrací a chybějící ceník vrátí 404 `billing.price_list_missing`.
- [x] 3.8 Úloha `billing.archive_unused_prices`: archivuje Price bez běžícího předplatného a bez otevřené objednávky. Test: Price s běžícím předplatným se nearchivuje.

## 4. Ocenění rozsahu

- [x] 4.1 Implementovat `PriceQuoteService : IPriceQuoteService` (rozhraní ze změny 10) nad `ShopScope`:
  - výběr ceníku (měna trhu, dokud `tenants.currency` je NULL);
  - pásmo, sleva podle pořadí, `vat_preview`;
  - důvody nezapočtení verzí převzaté beze změny.

  Testy `PriceQuoteServiceTests`: rozsah 11 668 → `t20000`, rozsah po odškrtnutí CZ, chybějící ceník.
- [x] 4.2 Implementovat `FairUsePolicy` (ostatní stránky z rozsahu > faktor × produkty → `individual_offer`, `billing.fair_use_exceeded`). Testy na hranici přesně 2× a 2× + 1.
- [x] 4.3 Záznam ceny v `billing.price_quotes`: `INSERT … ON CONFLICT (shop_id, scope_hash, price_list_id) DO NOTHING`, vrácení `quoteId`. Test „Opakovaný dotaz se stejným rozsahem“.
- [x] 4.4 Napojení na změnu 10:
  - registrace `PriceQuoteService` místo `FakePriceQuoteService`;
  - `POST …/quote` vrací ceny, `quoteId` a `scopeHash` jako kódy a parametry, žádné hotové věty (architektura, část 12, Aplikace);
  - bez ceníku `billing.price_list_missing`, bez základu `quote.basis_missing` ze změny 10.

  Test kontraktu v `OrderFlowTests`.
- [x] 4.5 Sladit se změnou 8 (proposal, K rozhodnutí 15): `AnalysisPriceEstimator` ukládá jen základ rozsahu a pásmo a částku bere z `IPriceQuoteService`. Test: `runs.estimate` ukázky a `POST …/quote` dají stejné pásmo.

## 5. Objednávka a Checkout

- [x] 5.1 Implementovat `OrderService.CreateAsync`:
  - `IShopOrderReadiness` (změna 10);
  - nový výpočet rozsahu a porovnání `scopeHash` a ceníku (409 `quote.stale` s `reason` a novou nabídkou), `billing.quote_not_payable`;
  - daňový režim;
  - snímek částek, `scope_hash` a ID objektů Price;
  - vrácení existující otevřené objednávky;
  - `orders.run_id` = běh `full_analysis` ve stavu `awaiting_payment` z `RunService.CreateFullAnalysisAsync` (změna 8).

  Testy podle scénářů požadavku „Garantovaná cena objednávky“.
- [x] 5.2 Implementovat zákazníka Stripe pro tenanta (`customer:{tenantId}`):
  - jméno, adresa, `preferred_locales`, e-mail z `billing_email`;
  - IČ DPH přes `TaxIdService` z údajů tenanta, ne z Checkoutu.

  Test: druhé volání zákazníka nezaloží.
- [x] 5.3 Implementovat `CheckoutService.CreateSessionAsync` podle `design.md`:
  - položky, `trial_end`, metadata, `automatic_tax`, `locale`, `custom_text`, `discounts`, `expires_at`;
  - opakované použití otevřené session.

  Testy `CheckoutServiceTests`: parametry session, `calendar_month` 31. 1. → 28. 2., `days_30`.
- [x] 5.4 Endpointy `POST /orders`, `POST /orders/{o}/checkout` a `GET /orders/{o}` (stav `awaiting_confirmation` po návratu před webhookem). Testy `OrderFlowTests`.
- [ ] 5.5 Ověřit v dokumentaci Stripe a testem s testovacími hodinami (`IntegrationTests/CheckoutTrialTests`):
  - jednorázová položka se zaplatí hned i se zkušební dobou;
  - kupón s `applies_to` se nepoužije na analýzu;
  - minimální odstup `trial_end`;
  - Apple Pay a Google Pay se nabídnou.

  Výsledek zapsat do `design.md`.

## 6. Webhooky Stripe

- [x] 6.1 `StripeWebhookEndpoint`:
  - syrové tělo do 512 kB;
  - `EventUtility.ConstructEvent` s tolerancí 300 s;
  - kontrola `livemode`;
  - `INSERT … ON CONFLICT DO NOTHING` a úloha `billing.process_stripe_event` v jedné transakci, 200.

  Testy `StripeWebhookEndpointTests`: neplatný podpis 400, duplicita 200 bez úlohy, nesoulad režimu 400.
- [x] 6.2 `StripeEventProcessor` s obsluhami v `Stripe/Handlers/`:
  - `CheckoutSessionHandler` (completed, expired);
  - `InvoiceHandler` (paid, payment_failed, payment_action_required);
  - `SubscriptionHandler` (created, updated, deleted);
  - `CustomerHandler` (customer.updated, payment_method.*, tax_id.*);
  - `RefundHandler` (charge.refunded);
  - ostatní `ignored`.

  Každá obsluha načte aktuální objekt přes `IStripeGateway`.

  Testy:
  - `StripeEventProcessorTests.Out_of_order_updates_keep_latest_state`;
  - deduplikace napříč událostmi: webhook i dorovnání dají jeden doklad.
- [x] 6.3 Uvolnění běhu po `checkout.session.completed`:
  - `orders` jen z `created`/`checkout_open` na `paid`;
  - `RunService.MarkOrderPaidAsync(order)` (změna 8, idempotentní);
  - `BillingRunPaymentGate : IRunPaymentGate` místo `OrderTablePaymentGate`;
  - audit `order.paid`.

  Test: dvakrát zpracovaná událost uvolní běh jednou a nezaplacená objednávka vrátí `order_not_paid`.
- [x] 6.4 Úloha `billing.reconcile_stripe` (denně 4:00): faktury zaplacené a předplatná změněná za 72 h, doplnění přes stejný procesor. Test: chybějící `invoice.paid` vytvoří platbu a úlohu dokladu se stejným `dedupe_key`.

## 7. Předplatné, karta a zrušení

- [x] 7.1 Synchronizace `subscriptions`:
  - stav, období, `trial_end`, `cancel_at_period_end`, `stripe_price_id`, `stripe_coupon_id`, `shop_ordinal`;
  - jedinečnost běžícího předplatného na e-shop.

  Test: druhé běžící předplatné pro e-shop selže na omezení a obsluha zapíše upozornění provozu.
- [x] 7.2 `AccountCardService.ApplyDefaultAsync`:
  - výchozí karta zákazníka;
  - `default_payment_method` všem běžícím předplatným;
  - `detach` staré karty;
  - `payment_methods` s jedním `is_default`.

  Test podle scénáře „Změna karty platí pro všechna předplatná“.
- [x] 7.3 `SavedCardPaymentService.PayAsync` a endpoint `POST /orders/{o}/pay-with-saved-card`:
  - `add_invoice_items`, `trial_end`, `payment_behavior = default_incomplete`;
  - při `requires_action` vrátit `client_secret`;
  - vypršení zruší předplatné `incomplete`.

  Testy s `FakeStripeGateway`.
- [x] 7.4 Endpoint `POST /billing/card/portal-session`:
  - konfigurace portálu: jen změna karty, bez zrušení a změny tarifu, faktury vypnuté, pokud to jde;
  - náhradní cesta `POST /billing/card/setup-intent`.

  Test: odpověď obsahuje jen URL, ne ID zákazníka.
- [x] 7.5 `SubscriptionService`:
  - `CancelAsync` (`cancel_at_period_end`);
  - `ResumeAsync` (jen před koncem období);
  - `StartAgainAsync`: nové předplatné bez zkušební doby, nejdřív odpověď `confirm_required` s částkou a datem, pak potvrzení.

  Endpointy a testy podle scénářů požadavku „Předplatné sledování po e-shopech“.
- [x] 7.6 Úloha `billing.trial_reminder` (`trial_end − 7 dní`) a šablony `billing.trial_reminder`, `billing.payment_failed`, `billing.subscription_ended`, `billing.price_change` a `billing.tier_change` (sk, cs). E-maily jdou do `ops.outbox` (`kind = email`) a skládá je `EmailComposer` změny 9. Test úplnosti klíčů sk a cs a test jednoho odeslání.
- [x] 7.7 Napojení na sledování:
  - `customer.subscription.deleted` → `subscriptions.status = canceled`, `shops.status` `paused`/`canceled`;
  - změna 16 podle `billing.subscriptions.status` přestane zakládat noční běhy (čte stav sama, tato změna nic nevolá);
  - data e-shopu se nemažou.

  Test: po konci předplatného zůstanou nálezy a doklady čitelné.
- [x] 7.8 Endpoint `GET /billing/overview`:
  - e-shopy, pásmo a počet produktů, stav (Aktívne, „Prvý mesiac v cene do …“, Zrušené k …), další platba s částkou, „Nová cena od …“;
  - karta, pásma ceníku, součet sledování za měsíc se slevou.

  Test kontraktu proti datům návrhu Billing (3 e-shopy).

## 8. Změny pásma, slevy a ceníku

- [x] 8.1 `SubscriptionChangePlanner`:
  - založení, zrušení a nahrazení změn `tier`, `discount` a `price_list` s pravidly `effective_at` z `design.md`;
  - u zdražení výjimka `tenants.founder_until`.

  Testy na každý řádek tabulky pravidel (`SubscriptionChangeTests`).
- [x] 8.2 `SubscriptionScheduleComposer`:
  - fáze z platných změn, `from_subscription`, `end_behavior = release`, `proration_behavior = none`;
  - otisk fází jako klíč idempotence, beze změny žádné volání.

  Test podle scénáře „Souběh nového ceníku a vyššího pásma“.
- [x] 8.3 Úloha `billing.evaluate_tiers` (denně 3:30 a po změně `product_count`) se zrušením změny při návratu do pásma. Testy podle scénářů požadavku „Změna pásma a slevy od dalšího období“.
- [x] 8.4 `VolumeDiscountResolver`: pořadí e-shopů podle začátku předplatného a přepočet při přidání a zrušení. Test „Zrušení e-shopu odebere slevu třetímu“.
- [x] 8.5 Úloha `billing.schedule_price_list_transfer`:
  - po dávkách 200 předplatných, `concurrency_key = price-transfer`;
  - e-mail `billing.price_change` se starou a novou cenou a datem.

  Testy „Zdražení po výpovědní lhůtě“ a „Zlevnění bez lhůty“.
- [x] 8.6 Promítnutí použité změny: `customer.subscription.updated` s novou Price → `subscription_changes.applied_at`, `subscriptions.unit_price`, audit `subscription.price_changed`. Test nad falešnou bránou Stripe v `SubscriptionChangeTests`.
- [ ] 8.7 Test 8.6 s testovacími hodinami Stripe v `IntegrationTests/PriceChangeTests`. Potřebuje testovací účet Stripe, čeká se skupinou 0 (jako 5.5).

## 9. Daňový režim

- [x] 9.1 `TaxTreatmentResolver` podle tabulky v `design.md`. Testy `TaxTreatmentResolverTests`: SK s IČ DPH i bez, CZ `verified`, CZ `pending`, CZ bez IČ DPH, mimo EU.
- [ ] 9.2 Synchronizace `tenants.tax_id_status` z `customer.tax_id.created/updated`. Zablokování Checkoutu (`billing.tax_id_pending`, `billing.tax_treatment_undetermined`) a povinné IČO (`billing.company_id_required`). Testy endpointů.
- [ ] 9.3 Kontrola souladu daně při vystavení dokladu (`total_tax_amounts`, přenesení daňové povinnosti) proti `orders.tax_treatment` → `needs_review` s důvodem `tax_mismatch` a upozornění provozu. Test podle scénáře „Nesoulad daně ve Stripe“.

## 10. Doklady v SuperFaktúře

- [ ] 10.1 `SuperFakturaClient`:
  - typovaný `HttpClient`, autorizační hlavička podle dokumentace (ověřeno v 0.2);
  - `RedactLoggedHeaders`;
  - timeout 20 s;
  - rozlišení dočasných (5xx, timeout, 429) a trvalých (4xx) chyb.

  Testy s `FakeSuperFakturaHandler`.
- [ ] 10.2 `InvoiceBuilder`:
  - snímek odběratele;
  - položky z řádků faktury Stripe s texty `Invoicing/Texts/{sk,cs}.json` (analýza, období sledování, pásmo, e-shop);
  - sleva, DPH nebo text o přenesení daňové povinnosti, měna.

  Testy `InvoiceBuilderTests` (analýza, obnova se slevou, CZ přenesení daňové povinnosti, Kč).
- [ ] 10.3 `InvoiceIssuer` (úloha `billing.issue_invoice`):
  - `source_key`, vyhledání v SuperFaktúře před založením;
  - založení a zápis platby `CARD` s datem přijetí platby;
  - odeslání, PDF do úložiště, audit;
  - upozornění provozu, když úloha vyčerpá pokusy nebo doklad visí v `creating` déle než 6 h.

  Testy:
  - pád po založení nezaloží druhý doklad;
  - opakování po 5xx;
  - po 12 pokusech úloha skončí `failed` a vznikne upozornění.
- [ ] 10.4 E-faktúra:
  - `einvoice_required` (odběratel SK, datum ≥ 1. 1. 2027);
  - odeslání přes Peppol;
  - úloha `billing.check_einvoice_status` (každé 2 h) s upozorněním při `failed` nebo po 3 dnech.

  Test s pevným časem 31. 12. 2026 a 1. 1. 2027.
- [ ] 10.5 `CreditNoteIssuer` (úloha `billing.issue_credit_note` z `charge.refunded`): `credit_note_for`, částka vrácení, stejná cesta odeslání. Test: dvojí událost dá jeden dobropis.

## 11. Seznam faktur a ZIP

- [ ] 11.1 Endpoint `GET /invoices?shopId=&year=`:
  - doklady a naplánované platby z běžících předplatných (stav „Naplánovaná“, bez PDF);
  - řazení od nejnovějšího.

  Test podle scénáře „Filtr podle e-shopu a roku“.
- [ ] 11.2 Endpoint `GET /invoices/{id}/pdf`: podepsaný odkaz na 5 minut přes `IBlobStore`. Pro cizí doklad 404. Test izolace tenantů.
- [ ] 11.3 `InvoiceZipWriter` a endpoint `GET /invoices/zip`:
  - streamování bez načtení všeho do paměti;
  - strop 500 dokladů (nad ním `billing.zip_too_large`);
  - hlavička `X-EshopGuard-Skipped`.

  Test ZIPu se 6 PDF a 1 vynechaným.
- [ ] 11.4 Oprávnění přes `.RequireTenantRole(TenantRole.Admin)` (změna 9): owner a admin ano, editor a viewer `403 auth.forbidden_role`, cizí tenant 404. Testy `BillingEndpointsAuthorizationTests` pro všechny endpointy skupin 5, 7 a 11.

## 12. Ověření (celý tok v testovacím režimu)

- [ ] 12.1 Automatické kontroly:
  - test `SecretsNotLoggedTests`: celý tok s logem na úrovni Debug a chybou SuperFaktúry 401, žádný klíč v logu, odpovědích ani `needs_review_reason`;
  - `dotnet test` (všechny projekty) a `openspec validate add-billing-and-invoicing` projdou.
- [ ] 12.2 Připravit testovací prostředí:
  - lokální API a worker s testovacími klíči Stripe v user-secrets;
  - přeposílání webhooků přes Stripe CLI (`stripe listen --forward-to localhost:…/api/webhooks/stripe`);
  - sandbox SuperFaktúry;
  - `MockJevClient` a `MockRewriteClient`, aby tok nic neplatil za Jev ani OpenAI.
- [ ] 12.3 Odhad ceny a souhlas uživatele, jen pokud se má analýza v toku spustit naostro s Jevem a OpenAI:
  - odhad z `JevCallEstimate`;
  - bez souhlasu zůstávají mock klienti.
- [ ] 12.4 Celý tok s testovacími hodinami Stripe:
  1. ukázka;
  2. nabídka (bylinkovo.sk, 2 verze, pásmo `t20000`);
  3. objednávka;
  4. Checkout s kartou 4242 a s kartou vyžadující 3-D Secure;
  5. webhook;
  6. běh opustí `awaiting_payment`;
  7. doklad za analýzu v sandboxu SuperFaktúry se zapsanou platbou kartou;
  8. posun hodin o 24 dní a připomenutí;
  9. posun na konec zkušební doby, obnova a doklad za sledování s obdobím.

  Výsledek zapsat do protokolu ověření v `tasks.md` (datum, ID testovacích objektů bez klíčů).
- [ ] 12.5 Druhý a třetí e-shop uloženou kartou:
  - 3. e-shop dostane kupón jen na sledování;
  - změna karty v portálu se projeví u všech tří předplatných;
  - zrušení druhého e-shopu přepočte slevu třetího od dalšího období.
- [ ] 12.6 Změna ceníku:
  1. koncept;
  2. náhled dopadu;
  3. zveřejnění;
  4. aktivace;
  5. e-mail se starou a novou cenou v `ops.outbox`;
  6. „Nová cena od …“ v přehledu;
  7. posun hodin za výpovědní lhůtu;
  8. doklad nese novou cenu.
- [ ] 12.7 Chybové cesty:
  - dvakrát poslaná událost (`stripe events resend`) dá jeden doklad;
  - neúspěšná platba (karta 4000 0000 0000 0341) dá upozornění bez dokladu a po posledním pokusu pozastavené sledování;
  - vrácení peněz v Dashboardu dá dobropis;
  - výpadek sandboxu SuperFaktúry vede k opakování a doklad doběhne.
