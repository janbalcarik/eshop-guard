## Technical Approach

### Projekty a vrstvy

- **`src/EshopGuard.Billing`** (nový, knihovna bez ASP.NET) obsahuje všechnu logiku: ceník, nabídky, objednávky, Stripe, daňový režim, SuperFaktúru a e-maily o cenách.
- **`src/EshopGuard.Api`** přidá endpointy a příjem webhooku Stripe. Nedělá žádnou dlouhou práci: zapíše řádek a úlohu do fronty a odpoví.
- **`src/EshopGuard.Worker`** přidá obsluhy úloh `billing.*`. Úlohy volající Stripe a SuperFaktúru mají `resource_class = io`, plánovací úlohy `system`.
- **`src/EshopGuard.Data`** dostane migraci `AddBilling` s doplněnými sloupci, tabulkou `billing.price_quotes` a výchozími daty ceníku SK.

### Vnější služby za rozhraním (testy bez sítě)

| Rozhraní | Implementace | Testovací náhrada |
|---|---|---|
| `IStripeGateway` | `StripeGateway` nad knihovnou Stripe.net (`StripeClient`, služby `PriceService`, `CouponService`, `Checkout.SessionService`, `SubscriptionService`, `SubscriptionScheduleService`, `BillingPortal.SessionService`, `InvoiceService`, `CustomerService`, `TaxIdService`) | `FakeStripeGateway` v `tests/EshopGuard.Billing.Tests/Fakes` (záznam volání, nastavitelné odpovědi) |
| `ISuperFakturaClient` | `SuperFakturaClient` (typovaný `HttpClient`). Endpointy a autorizační hlavička jsou ověřené ve veřejné dokumentaci API (sekce Doklady, úkol 0.2); platba má typ `card` | `FakeSuperFakturaHandler` (`HttpMessageHandler`) |
| `IClock` | systémový čas | pevný čas v testech |

Stripe.net se používá v pevně zvolené verzi API (`StripeConfiguration.ApiVersion` podle verze balíčku). Verze se zapíše do `BillingOptions.StripeApiVersion` a kontroluje se při startu.

### Zdroje pravdy

| Údaj | Zdroj pravdy | Zrcadlo |
|---|---|---|
| Ceny, pásma, slevy | `billing.price_lists`, `price_tiers`, `volume_discounts` | Stripe Price, Coupon, `lookup_key` |
| Platby a stav předplatného | Stripe | `billing.payments`, `billing.subscriptions` (z webhooků a nočního dorovnání) |
| Daňové doklady | SuperFaktúra | `billing.invoices` (snímek, číslo, PDF v úložišti) |
| Nabídka, kterou zákazník viděl | `billing.price_quotes` | `billing.orders` (snímek při vytvoření) |

### Ceny a pásma

Výchozí data se zakládají migrací jako koncept a zveřejní se admin API (úkol 3.6):

| Pásmo (`code`) | Produkty | Analýza EUR | Měsíc EUR | Rok EUR (10× měsíc) | Analýza Kč | Měsíc Kč | Rok Kč |
|---|---|---|---|---|---|---|---|
| `t500` | 1–500 | 39 | 9 | 90 | 990 | 249 | 2 490 |
| `t2000` | 501–2 000 | 69 | 19 | 190 | 1 790 | 490 | 4 900 |
| `t5000` | 2 001–5 000 | 99 | 29 | 290 | 2 490 | 750 | 7 500 |
| `t20000` | 5 001–20 000 | 199 | 59 | 590 | 4 990 | 1 490 | 14 900 |
| `custom` | nad 20 000 | dohodou | dohodou | dohodou | dohodou | dohodou | dohodou |

- Zdroj cen: strategie, část 3. Roční cena „2 měsíce zdarma“ je 10× měsíční.
- Ceník SK/EUR se zveřejní. Ceník CZ/CZK zůstane konceptem, dokud se nerozhodne měna (proposal, K rozhodnutí 1).
- Pásmo `custom` nemá objekty Price ve Stripe. Nabídka v něm má stav `individual_offer`.
- Hranice pásem jsou včetně: 500 produktů je ještě `t500`, 501 už `t2000`.
- E-shop s 0 započtenými produkty (jen stránky) spadá do `t500`.
- Započtené produkty (`counted_products`) = součet produktů za každou zaškrtnutou zemi, tedy počet produktů verze, kterou pro tu zemi kontrolujeme (rozhodnutí 2. 10. 2026; dříve jen verze s vlastními texty). E-shop s jednou verzí a dvěma zeměmi má dvojnásobek.
- Hranice pásem (`min_products`, `max_products`) a ceny jsou řádky `billing.price_tiers` po cenících; mění se v databázi (admin API) bez nového nasazení, kód je nezná.

### Ocenění rozsahu (`PriceQuoteService : IPriceQuoteService`)

**Rozdělení se změnou 10** (její K rozhodnutí 2):
- Změna 10 spočítá rozsah (`ShopScopeCalculator`): které verze se kontrolují, podle kterých zemí, `counted_products` (součet za země), důvody nekontrolování verzí a ostatní stránky ze základu ukázky v `runs.estimate`. Vrátí `ShopScope` se `scopeHash` a vlastní endpoint `POST /api/t/{tenantId}/shops/{shopId}/quote`.
- Tato změna implementuje `IPriceQuoteService.QuoteAsync(tenantId, shopId, ShopScope scope, ct)`.

Důvody, proč se verze nekontroluje, předává rozsah ze změny 10. V nabídce se jen přenesou (`menu_only_translation` a `sample_insufficient` zrušeny 2. 10. 2026, texty verzí se neporovnávají):
- `excluded_by_user`: vyloučeno v nastavení;
- `not_needed_by_markets`: verzi nepotřebuje žádné zaškrtnuté místo prodeje;
- `unsupported_market`: verze pro nepodporovaný trh, klientovi se neukazuje.

Když rozsah nemá základ z ukázky, vrací změna 10 `quote.basis_missing` a `IPriceQuoteService` se nevolá. Pásmo analýzy se určí z `ShopScope.PriceCount` v jednotce `PriceUnit` (rozhodnutí 2. 10. 2026): `products`, když jsou známé produkty všech kontrolovaných verzí, jinak `pages` = stránky ke kontrole ze sitemap (stejné hranice pásem, v nabídce i na faktuře „stránok na kontrolu“). Sledování od 2. měsíce se pak počítá podle skutečného počtu produktů z úvodní analýzy (`billing.evaluate_tiers`). Když chybí i stránky, vrací `scope.product_count_unknown` a nabídka se nevytvoří.

**Výpočet:**
1. **Ceník:** zveřejněný s `valid_from ≤ now`, nejnovější pro `tenants.market_code` a `tenants.currency`. Když měna tenanta ještě není pevná (`currency = NULL` do první platby, změna 9), vezme se měna trhu z `ref.markets.currency`. Když ceník chybí, nabídka má stav `unavailable` a kód `billing.price_list_missing`.
2. **Pásmo:** `TierResolver` podle `min_products ≤ scope.CountedProducts ≤ max_products`. Nad posledním pásmem platí `individual_offer`.
3. **Férové užití:** `other_pages` ze `scope`. Když `other_pages > fair_use_other_pages_factor × max(counted_products, 1)`, nabídka má stav `individual_offer` a kód `billing.fair_use_exceeded`.
4. **Pořadí e-shopu** = 1 + počet jiných e-shopů tenanta s předplatným ve stavu `trialing`, `active` nebo `past_due`. Sleva platí, když pořadí ≥ `from_shop_number` a pro ceník existuje řádek `volume_discounts`.
5. **Odhad DPH** (`vat_preview`) z `TaxTreatmentResolver`: sazba nebo přenesení daňové povinnosti, nebo `undetermined`.
6. **Záznam ceny, kterou zákazník viděl:** `INSERT … ON CONFLICT (shop_id, scope_hash, price_list_id) DO NOTHING` do `billing.price_quotes`. Stav e-shopu se nemění. Odpověď nese `quoteId`.

**Kdy se přepočítá:** při každém volání `POST …/quote` změny 10 (zaškrtnutí země, vyloučení verze, otevření obrazovky 3c) a při objednávce. Změna `product_count` z konektoru a aktivace nového ceníku se projeví při dalším volání. Pásmo běžícího předplatného hlídá `billing.evaluate_tiers` (Flow 4).

**Garantovaná cena:** objednávka nese `price_quote_id` a snímek částek. Když plná analýza najde víc produktů, cena analýzy se nemění (hlídá změna 8). Sledování se upraví od dalšího období (Flow 4).

### Objednávka a platba

**Vznik objednávky.** `OrderService.CreateAsync(shopId, quoteId, scopeHash, termsVersion)`:
1. Zavolá `IShopOrderReadiness` (změna 10). Blokující kódy, například nepotvrzená místa prodeje nebo neověřené vlastnictví, vrátí jako `409` s jejich kódy.
2. Spočítá rozsah znovu z uloženého stavu přes `ShopScopeCalculator`. Když se `scopeHash` liší, nebo když ceník nabídky už není aktivní, vrátí `409 quote.stale` (`reason`: `scope_changed` nebo `price_list_changed`) s novou nabídkou.
3. Ověří daňový režim.
4. Založí `orders` se snímkem částek, `scope_hash` a ID objektů Price.
5. Převezme běh `full_analysis` ve stavu `awaiting_payment` založený přes `RunService.CreateFullAnalysisAsync` (změna 8) a uloží `orders.run_id`.

Jedinečnost (`shop_id`) WHERE `status IN ('created','checkout_open')` brání dvěma otevřeným objednávkám.

**Platba přes Checkout.** `CheckoutService.CreateSessionAsync(orderId)` založí Checkout Session:
- `mode = subscription`;
- `line_items`: Price analýzy (jednorázově, 1 ks) a Price měsíčního sledování;
- `subscription_data.trial_end` (podle `Billing:TrialMode`) a `subscription_data.metadata` {`tenant_id`, `shop_id`, `order_id`};
- `discounts`: kupón slevy, když nabídka slevu má;
- `customer`: `tenants.stripe_customer_id`, zákazník se založí s klíčem idempotence `customer:{tenantId}`;
- `automatic_tax.enabled = true`, `locale` podle `tenants.locale`;
- `custom_text.submit.message`: věta o měsíční platbě po zkušební době a o možnosti kdykoli zrušit, v jazyce tenanta;
- `success_url` s `{CHECKOUT_SESSION_ID}`, `cancel_url`, `expires_at` = nyní + 60 min.

Klíč idempotence je `checkout:{orderId}:{attempt}`. Otevřená a nevypršelá session se použije znovu, nová se nezakládá.

**Platba uloženou kartou.** `SavedCardPaymentService.PayAsync(orderId)` je pro tenanta, který už kartu má. Založí předplatné přes API:
- `items`: Price sledování;
- `add_invoice_items`: Price analýzy;
- `trial_end`;
- `default_payment_method` = karta účtu;
- `payment_behavior = default_incomplete`.

Když první faktura potřebuje 3-D Secure, API vrátí `client_secret` a frontend platbu potvrdí přes Stripe.js. Chování `add_invoice_items` se zkušební dobou (jednorázová položka zaplacená hned) se ověří v dokumentaci Stripe a testem s testovacími hodinami.

### Webhook Stripe

**Příjem** (`POST /api/webhooks/stripe`, `StripeWebhookEndpoint`):
1. přečte syrové tělo (strop 512 kB);
2. ověří hlavičku `Stripe-Signature` tajemstvím webhooku s tolerancí 300 s (`EventUtility.ConstructEvent`);
3. porovná `livemode` s nastaveným režimem;
4. v jedné transakci provede `INSERT INTO billing.stripe_events … ON CONFLICT (id) DO NOTHING` a, když se řádek vložil, založí úlohu `billing.process_stripe_event` s `dedupe_key = stripe:{evt}`;
5. vrátí 200.

Chybný podpis vrátí 400 a nic se neuloží. Nesoulad režimu vrátí 400 a zapíše se metrika.

**Zpracování** (`StripeEventProcessor`) se řídí typem události a vždy načte aktuální objekt přes `IStripeGateway`:

| Událost | Akce |
|---|---|
| `checkout.session.completed` | objednávka `paid` (když `payment_status = paid`), `subscriptions` upsert, výchozí karta účtu, `RunService.MarkOrderPaidAsync(order)` (změna 8) |
| `checkout.session.expired` | objednávka `expired`, běh zůstane `awaiting_payment` |
| `invoice.paid` | `payments` upsert. Když `amount_paid > 0`, úloha `billing.issue_invoice` s klíčem ID faktury Stripe. Když je 0 (zkušební doba), doklad nevzniká |
| `invoice.payment_failed`, `invoice.payment_action_required` | `subscriptions.status` podle Stripe, upozornění v aplikaci a e-mail, doklad nevzniká |
| `customer.subscription.created`, `customer.subscription.updated` | synchronizace stavu, období, `cancel_at_period_end` a aktuální Price. Použité `subscription_changes` → `applied` |
| `customer.subscription.deleted` | `subscriptions.status = canceled`, `shops.status` `paused` nebo `canceled`. Změna 16 podle stavu předplatného přestane zakládat noční běhy. Data zůstanou |
| `customer.updated`, `payment_method.attached`, `payment_method.detached` | synchronizace `payment_methods`, výchozí karta |
| `setup_intent.succeeded` | karta z náhradní cesty (SetupIntent s `purpose = account_card`) se stane výchozí kartou zákazníka, pak jako `customer.updated` |
| `customer.tax_id.created`, `customer.tax_id.updated` | `tenants.tax_id_status` (`pending` / `verified` / `unverified`) |
| `charge.refunded` | `payments.refunded_amount`, úloha `billing.issue_credit_note` s klíčem ID vrácení |
| ostatní | `stripe_events.status = ignored` |

**Noční dorovnání** (`billing.reconcile_stripe`) načte ze Stripe faktury zaplacené za posledních 72 h a předplatná změněná za 72 h. Co chybí v `payments` nebo `subscriptions`, doplní stejným procesorem.

### Daňový režim (`TaxTreatmentResolver`)

**Vstup:**
- země fakturační adresy tenanta (`tenants.country_code`);
- `ic_dph` a jeho stav ověření (`tenants.tax_id_status`);
- datum.

Dodavatel je slovenská s.r.o., plátce DPH.

| Odběratel | Výsledek |
|---|---|
| Sídlo SK (s IČ DPH i bez) | `domestic_vat` (sazbu dodá Stripe Tax) |
| Jiný stát EU, IČ DPH `verified` | `reverse_charge` |
| Jiný stát EU, IČ DPH `pending`, nebo `none` (ještě neodesláno do Stripe) | `pending_verification`: Checkout nejdřív odešle IČ DPH do Stripe (spustí ověření) a čeká, UI ukáže „Overujeme IČ DPH“ |
| Jiný stát EU, IČ DPH `unverified`, nebo bez IČ DPH | `undetermined` až do potvrzení účetní (proposal, K rozhodnutí 3) |
| Mimo EU | `undetermined` (mimo rozsah) |

Výsledek se uloží do `orders.tax_treatment`. Při placení (Checkout, uložená karta) se režim i DPH objednávky přepočítají podle
aktuálního stavu tenanta.

**Kontrola po zaplacení.** Při vystavení dokladu se porovná daň z faktury Stripe (`total_tax_amounts`, `customer_tax_exempt`, `reverse_charge`) s `tax_treatment`. Nesoulad znamená `invoices.status = needs_review`, nic se neodešle a provoz dostane upozornění.

### Doklady (`InvoiceIssuer`)

**Úloha `billing.issue_invoice`** (`io`, `dedupe_key = invoice:{stripe_invoice_id}`, `max_attempts = 12`, rostoucí odstup až 1 h):
1. Najde nebo založí `invoices` se `source_key = stripe_invoice_id` (jedinečné) ve stavu `creating`.
2. V SuperFaktúře vyhledá doklad s naším klíčem (`checksum` = `source_key` přes `getResponseByChecksum`, viz tabulka níže). Když existuje, převezme ho a nezakládá nový.
3. Sestaví doklad (`InvoiceBuilder`):
   - snímek odběratele z `tenants` (`buyer`);
   - položky z řádků faktury Stripe s texty v jazyce `tenants.locale` (`EshopGuard.Billing/Invoicing/Texts/{sk,cs}.json`), například „Sledovanie zmien · pásmo do 2 000 produktov · bylinkovo.sk · obdobie 1. 11.–30. 11. 2026“;
   - slevu, DPH nebo text o přenesení daňové povinnosti;
   - měnu.
4. Založí doklad a zapíše k němu platbu kartou s datem přijetí platby.
5. Odešle PDF e-mailem na `tenants.billing_email` (`POST /invoices/send`). E-faktúru přes Peppol aplikace neposílá ani nesleduje; zajišťuje ji SuperFaktúra sama ve svém účtu (rozhodnutí 3. 10. 2026). Sloupce `einvoice_*` v `billing.invoices` zůstávají s hodnotami `false` a `not_required`.
6. Stáhne PDF do úložiště `tenants/{tenantId}/invoices/{invoiceId}.pdf` a uloží `pdf_blob_key`.
7. Stav `issued`, audit `invoice.issued`.

**E-faktúra (rozhodnutí 3. 10. 2026, uživatel):** „Peppol vůbec neřešit v rámci naší aplikace“. SuperFaktúra ho má interně: doklad založený přes API odešle do Peppolu sama, jakmile to bude umět. Úloha `billing.check_einvoice_status` ani sledování doručení proto nevzniknou. Zda SuperFaktúra slovenským firmám od 1. 1. 2027 e-faktúru skutečně odesílá, ověří provoz před tímto datem (proposal, K rozhodnutí 16).

**Dobropis** (`billing.issue_credit_note`) vznikne stejnou cestou s `kind = credit_note` a `credit_note_for`.

#### API SuperFaktúry podle veřejné dokumentace (úkol 0.2, ověřeno 2. 10. 2026)

Zdroj: repozitáře `superfaktura/docs` (`intro.md`, `invoice.md`, `value-lists.md`, `faq.md`) a `superfaktura/apiclient`, větev `master`, a stránky superfaktura.sk. Bez účtu a bez volání API.

| Bod | Zjištění |
|---|---|
| Adresy | Produkce SK `https://moja.superfaktura.sk`, CZ `https://moje.superfaktura.cz`. Sandbox SK `https://sandbox.superfaktura.sk`, CZ `https://sandbox.superfaktura.cz`. Registrace do sandboxu je samoobslužná a bez platby (`/registracia`); limity sandboxu dokumentace neuvádí. |
| Autorizace | Hlavička `Authorization: SFAPI email=…&apikey=…&company_id=…&module=…`. Hodnoty jsou URL-kódované, `module` je náš název integrace (`EshopGuard 0.1`), `company_id` je volitelné. Hlavička obsahuje klíč, proto ji logování HTTP klienta musí skrýt. |
| Formát | `POST` s `Content-Type: application/json` (přímo objekt) nebo formulář s polem `data`. Používáme JSON. Chyby hlásí hlavně tělo odpovědi (`error` ≠ 0, `error_message` jako text nebo objekt), jen některé i stavem HTTP (403 bez oprávnění, 404 nenalezeno). |
| Založení dokladu | `POST /invoices/create` s objekty `Invoice`, `InvoiceItem[]`, `Client`, `InvoiceSetting`. `Invoice`: `name`, `order_no`, `variable`, `created`, `delivery` (datum zdanitelného plnění), `due`, `invoice_currency` (ISO 4217, CZK podporována), `vat_transfer` (přenesení daňové povinnosti 0/1), `type`, `parent_id`. Položka: `name`, `description`, `quantity`, `unit_price`, `tax` (sazba v %), `discount`. Odběratel: `name`, `ico`, `dic`, `ic_dph`, `address`, `city`, `zip`, `country_iso_id`, `email`. Odpověď `data.Invoice` s `id`, `token` (pro PDF) a `invoice_no_formatted`. |
| Jazyk dokladu | `InvoiceSetting.language` s třípísmennými kódy (`slo`, `cze`, `eng` …). Mapování `sk` → `slo`, `cs` → `cze`. |
| Text o přenesení daně | Jen příznak `vat_transfer = 1`; vlastní text dokumentace neuvádí. Větu pro odběratele proto doplníme do poznámky dokladu z našich textů (`BillingTexts`). |
| Ochrana proti duplicitě | Pole `checksum` (vlastní řetězec, nejvýš 32 znaků) při založení a `GET /api_logs/getResponseByChecksum/{checksum}` vrátí původní odpověď až asi 3 měsíce zpět. Jiný klíč pro opakování dokumentace nemá. Náš klíč: `source_key` (id faktury nebo refundace ze Stripe, do 32 znaků). Navíc se klíč zapíše do `order_no` a v nouzi jde doklad dohledat seznamem `GET /invoices/index.json/order_no:{klíč}`. |
| Platba | `POST /invoice_payments/add/ajax:1/api:1`, objekt `InvoicePayment` s `invoice_id`, `amount`, `currency`, `payment_type = "card"` (malými písmeny) a datem. Dokumentace uvádí pole data jednou jako `date` a v příkladu jako `created`; posíláme obě se stejnou hodnotou, ověří se v sandboxu. Již uhrazený doklad vrací `error = 1`, bereme ho jako hotovo. |
| Odeslání e-mailem | `POST /invoices/send`, objekt `Email` s `invoice_id`, `to`, `pdf_language`. Limit 100 e-mailů za hodinu. |
| PDF | `GET /{jazyk}/invoices/pdf/{id}/token:{token}`; chyba je HTTP 404. |
| Dobropis | Typ dokladu `cancel` a `Invoice.parent_id` = id opravovaného dokladu. Jen odvozeno z popisu polí, příklad v dokumentaci chybí; ověří se v sandboxu. |
| Detail dokladu | `GET /invoices/view/{id}.json` (bez obálky `data`). |
| Limity | Výchozí 1 000 požadavků za den a 30 000 za měsíc, hlavičky `X-RateLimit-*`. Stav HTTP při překročení dokumentace neuvádí (jen „žádná odpověď“). Doporučený timeout ani opakování neuvádí; řídíme se vlastní politikou úlohy (12 pokusů, odstup až 1 h). |
| Zpětná volání | Jen `InvoiceSetting.callback_payment` (GET po zápisu platby). Nepoužíváme. |
| E-faktúra (Peppol) | V dokumentaci API ani v knihovně není: žádný endpoint, pole ani stav doručení. Aplikace ji podle rozhodnutí 3. 10. 2026 neřeší (viz výše). |
| Tarify | Ceník SuperFaktúry pro náš účet: Základný 4,99 €, Štandardný 9,99 €, Prémiový 16,99 € měsíčně. Přístup k API má jen Prémiový (uživatel 3. 10. 2026), náš účet proto potřebuje Prémiový. |

Neověřené body jsou v proposal, K rozhodnutí 16.

### Změny od dalšího období (`SubscriptionChangePlanner`, `SubscriptionScheduleComposer`)

**Plánování.** Každá změna je řádek `subscription_changes`:
- `kind`: `price_list`, `tier` nebo `discount`;
- `from` a `to` (jsonb: `price_list_id`, `tier_code`, `stripe_price_id`, `coupon_id`, `unit_price`, `discount_percent`);
- `effective_at` (začátek období, od kterého platí);
- `status`: `scheduled`, `notified`, `applied` nebo `canceled`.

**Skládání.** `SubscriptionScheduleComposer.ComposeAsync(subscriptionId)`:
1. vezme všechny `scheduled` a `notified` změny předplatného;
2. seřadí je podle `effective_at`;
3. sestaví fáze plánu: aktuální fáze do prvního `effective_at`, pak fáze s výslednou Price a kupónem;
4. přes `IStripeGateway` založí nebo aktualizuje Subscription Schedule (`from_subscription`, `end_behavior = release`, `proration_behavior = none`) s klíčem idempotence `schedule:{subscriptionId}:{hash fází}`.

Nikde jinde se plán předplatného nemění.

**Pravidla `effective_at`:**

| Změna | Od kdy |
|---|---|
| Zdražení novým ceníkem | první začátek období ≥ max(`valid_from`, `published_at + notice_days`), u `tenants.founder_until` v budoucnu ≥ `founder_until` |
| Zlevnění novým ceníkem | další začátek období po `valid_from`, bez lhůty |
| Vyšší pásmo | první začátek období ≥ upozornění + `TierChangeNoticeDays` |
| Nižší pásmo | další začátek období |
| Ztráta slevy (zrušený e-shop) | další začátek období |
| Získání slevy (přidaný e-shop) | další začátek období |

**Upozornění.** E-mail jde přes `ops.outbox` (`kind = email`, šablona `billing.price_change`) se starou a novou cenou, datem a odkazem na zrušení. Pak `notified_at` a stav `notified`. Zlevnění se oznámí také, bez lhůty.

## Architecture Decisions

1. **Ceny drží naše databáze, Stripe je zrcadlo** (platby, Ceny v databázi). Stripe Price nejde změnit. Nový ceník tak znamená nové objekty Price a převod `lookup_key` s `transfer_lookup_key = true`. Staré Price zůstanou aktivní, dokud na ně odkazuje předplatné nebo otevřená objednávka. Pak je úloha `billing.archive_unused_prices` archivuje (`active = false`). Zda archivovaná Price dál funguje u běžícího předplatného, se ověří v dokumentaci Stripe. Do té doby se Price s běžícím předplatným nearchivují.
2. **Checkout dostává ID objektu Price ze snímku objednávky, `lookup_key` nepoužívá.** `lookup_key` slouží ke konzistenci ve Stripe a v Dashboardu. Cenu, kterou zákazník zaplatí, určuje jen objednávka, kterou viděl. Tím je splněná zásada „zákazník cenu vidí vždy před platbou“.
3. **Nabídka je samostatná tabulka `billing.price_quotes`.** Nabídka existuje dřív než objednávka (obrazovka 3c) a mění se při změně zemí. Garantovaná cena se musí dát dohledat. Proto historie nabídek, ne jen sloupec u objednávky.
4. **Jedno předplatné Stripe na e-shop** (platby, změna 1. 10. 2026):
   - fakturační den = den analýzy;
   - faktura je po e-shopech a agentura ji může přeúčtovat;
   - zrušení jednoho e-shopu neovlivní ostatní.

   Jedinečnost: `subscriptions (shop_id) WHERE status IN ('trialing','active','past_due','incomplete')`.
5. **Sleva od 3. e-shopu je kupón** (`percent_off`, `duration = forever`, `applies_to.products = [produkt sledování]`), aby se nevztahovala na analýzu. Zda Checkout v režimu předplatného respektuje `applies_to` u jednorázové položky, ověřit v dokumentaci a testem.
6. **Webhook se uloží a odpoví hned, zpracování běží ve workeru a vždy načte aktuální objekt.** Stripe nezaručuje pořadí ani jedno doručení. Deduplikace na dvou úrovních:
   - `stripe_events.id` (stejná událost);
   - `invoices.source_key`, `payments.stripe_invoice_id` a `orders.status` (různé události o stejné věci).
7. **Daňový doklad jen z `invoice.paid` s `amount_paid > 0` a z `charge.refunded`.** Checkout v režimu předplatného platí analýzu fakturou Stripe, takže jeden zdroj pokryje analýzu i obnovení. Během zkušební doby doklad nevzniká (faktura Stripe na 0).
8. **Žádné duplicitní doklady v SuperFaktúře.** Pravděpodobně nemá klíč idempotence (ověřit). Proto se doklad před založením vyhledá podle našeho klíče a teprve pak se zakládá. Výpadek mezi založením a uložením ID tak nezpůsobí dvojí doklad.
9. **Daňový režim se rozhoduje před platbou a kontroluje po ní** (platby, Jak to zapojit, DIČ zákazníka: Stripe použije přenesení daňové povinnosti už podle tvaru DIČ). IČ DPH se zákazníkovi Stripe zapisuje z našich údajů tenanta, ne přes `tax_id_collection` v Checkoutu, aby nevznikly dva zdroje. Checkout pro jiný stát EU se založí až po `verified`.
10. **Jedna karta na účet:**
    - karta je `invoice_settings.default_payment_method` zákazníka Stripe;
    - po každé změně karty `AccountCardService.ApplyDefaultAsync` nastaví stejnou kartu všem běžícím předplatným tenanta a předchozí kartu odpojí (`detach`);
    - v `payment_methods` je vždy nejvýš jeden řádek s `is_default` a bez `detached_at`.
11. **Změna karty:**
    - primárně přes zákaznický portál Stripe s `flow_data.type = payment_method_update`;
    - konfigurace portálu: zrušení předplatného vypnuté (ruší se v aplikaci), změna tarifu vypnutá a seznam faktur vypnutý, pokud to jde (ověřit);
    - náhradní cesta: SetupIntent s Payment Element v aplikaci (`POST …/billing/card/setup-intent`).
12. **Všechny změny ceny jdou od dalšího období přes Subscription Schedule z jednoho skladače.** Bez poměrného přepočtu a bez dobropisů (proposal, K rozhodnutí 6). Více souběžných změn (nové pásmo i nový ceník) se tak skládá deterministicky.
13. **Režim test/live se kontroluje při startu (fail-closed).** `Billing:Stripe:Mode` musí odpovídat předponě klíče (`sk_test_` / `sk_live_`, případně `rk_…`). Mimo prostředí `Production` se se živým klíčem aplikace nespustí. Totéž platí pro `Billing:SuperFaktura:Sandbox`. Ceník nese `stripe_mode` a ID objektů Price z jiného režimu se odmítnou.
14. **Klíče nikdy v logu.**
    - `BillingOptions.ToString()` vrací masku.
    - `HttpClient` SuperFaktúry má `RedactLoggedHeaders` pro hlavičku `Authorization`.
    - Logování těl požadavků Stripe.net je vypnuté.
    - Test `SecretsNotLoggedTests` zachytí výstup logu celého toku a hledá vzory `sk_(test|live)_`, `rk_(test|live)_`, `whsec_` a hodnotu klíče SuperFaktúry.

## Data Flow

### Flow 1: ukázka → nabídka → objednávka → Checkout → běh

```
Ukázka hotová (změna 8) ─► základ rozsahu v runs.estimate
Obrazovka 3c (otevření, zaškrtnutí země) ─► POST /shops/{s}/quote (změna 10)
   └─ ShopScopeCalculator (10) ─► IPriceQuoteService.QuoteAsync (12) ─► billing.price_quotes (jen záznam) ─► {scope, ceny, quoteId, scopeHash}
"Zaplatiť" ─► POST /shops/{s}/orders {quoteId, scopeHash, termsVersion}
              ├─ IShopOrderReadiness (10): blokující kódy → 409
              ├─ rozsah znovu ≠ scopeHash, nebo ceník už neplatí → 409 quote.stale + nová nabídka
              ├─ TaxTreatmentResolver: undetermined → 422 billing.tax_treatment_undetermined
              ├─ orders (created, snímek částek a Price ID, tax_treatment, run_id = běh awaiting_payment ze změny 8)
          ─► POST /orders/{o}/checkout ─► Stripe Checkout Session ─► 200 {url}
Zákazník platí na stránce Stripe (karta / Apple Pay / Google Pay, 3-D Secure)
Stripe ─► POST /api/webhooks/stripe (checkout.session.completed)
          ├─ podpis, livemode, INSERT stripe_events ON CONFLICT DO NOTHING
          └─ job billing.process_stripe_event (dedupe stripe:{evt}) ─► 200
Worker: načte session + subscription ze Stripe
          ├─ orders.status paid (jen z created/checkout_open; paid → nic)
          ├─ subscriptions (trialing, trial_end, period), payment_methods, výchozí karta
          ├─ RunService.MarkOrderPaidAsync(order) (změna 8) → běh pokračuje stahováním; IRunPaymentGate čte orders
          └─ audit order.paid
Stripe ─► invoice.paid (analýza, amount_paid > 0) ─► job billing.issue_invoice ─► SuperFaktúra
Návrat do aplikace: GET /orders/{o} (stav paid / čeká na potvrzení)
```

### Flow 2: obnovení předplatného

```
Plánovač: trial_end − 7 dní ─► job billing.trial_reminder (dedupe trial-reminder:{subscription}) ─► e-mail
Stripe strhne kartu na začátku období ─► invoice.paid ─► payments ─► billing.issue_invoice
                                     └► invoice.payment_failed ─► subscriptions past_due, upozornění, bez dokladu
Poslední pokus neúspěšný ─► customer.subscription.deleted ─► sledování e-shopu pozastaveno (data zůstanou)
```

### Flow 3: nový ceník

```
Admin: POST /api/admin/price-lists (koncept) ─► PUT …/tiers, …/volume-discounts
Admin: GET …/{id}/impact ─► počet předplatných, která zdraží / zlevní / zůstanou, nejbližší effective_at
Admin: POST …/{id}/publish {valid_from} ─► job billing.sync_price_list
   ├─ Stripe: Price (analýza, měsíc, rok) s lookup_key bez převodu, kupóny ─► ID do price_tiers / volume_discounts
   ├─ vše uloženo → price_lists.status = published, published_at; jinak sync_status failed a nic se nemění
   └─ job billing.activate_price_list (not_before = valid_from)
         ├─ převod lookup_key (transfer_lookup_key = true) na nové Price
         ├─ ref.markets.price_list_id = nový ceník; předchozí retired
         ├─ nabídky se starým ceníkem → při objednávce 409 quote.stale (price_list_changed)
         └─ job billing.schedule_price_list_transfer
               └─ pro každé běžící předplatné trhu a měny: subscription_changes (price_list) + skladač + e-mail
customer.subscription.updated s novou Price ─► subscription_changes applied, subscriptions.unit_price, audit
```

### Flow 4: přechod pásma a slevy

```
Změna product_count (konektor, sledování) nebo denně 3:30 ─► job billing.evaluate_tiers (po tenantech)
   ├─ pásmo z aktuálních counted_products ≠ subscriptions.tier_code → subscription_changes (tier) + e-mail
   ├─ počet produktů se vrátí do původního pásma před effective_at → změna canceled + e-mail „zmena sa neuplatní“
Zrušení / přidání e-shopu ─► VolumeDiscountResolver přepočte pořadí ─► subscription_changes (discount) pro dotčené
Plná analýza najde víc produktů ─► cena analýzy beze změny (garantovaná), sledování přes evaluate_tiers
```

### Flow 5: zrušení a obnovení sledování

```
"Zrušiť sledovanie" ─► POST /shops/{s}/subscription/cancel ─► Stripe cancel_at_period_end = true ─► audit
   ├─ během zkušební doby: na konci zkušební doby se nic nestrhne
"Obnoviť" před koncem období ─► POST …/subscription/resume ─► cancel_at_period_end = false
Konec období ─► customer.subscription.deleted ─► sledování se zastaví, nálezy, opravy a doklady zůstanou
"Zapnúť sledovanie" po skončení ─► POST …/subscription ─► nové předplatné bez zkušební doby, uložená karta
```

### Flow 6: změna karty

```
"Zmeniť kartu" ─► POST /billing/card/portal-session ─► URL portálu (flow payment_method_update) ─► návrat
Stripe ─► customer.updated / payment_method.attached ─► AccountCardService.ApplyDefaultAsync
   ├─ všechna běžící předplatná tenanta: default_payment_method = nová karta
   ├─ stará karta detach, payment_methods.detached_at
   └─ předplatná ve stavu past_due: Stripe zkusí platbu znovu (smart retries); aplikace jen ukáže stav
```

## File Changes

### `src/EshopGuard.Billing/` (nový projekt)

- `EshopGuard.Billing.csproj`: reference na `EshopGuard.Data` a `EshopGuard.Jobs`, balíček `Stripe.net`.
- `BillingOptions.cs`:
  - `Stripe` (`Mode`, `SecretKey`, `WebhookSecret`, `ApiVersion`);
  - `SuperFaktura` (`Email`, `ApiKey`, `CompanyId`, `Sandbox`, `BaseUrl`);
  - `TrialMode`, `TierChangeNoticeDays`, `CheckoutExpiresMinutes`, `PublicAppUrl`;
  - `ToString()` s maskou.
- `BillingOptionsValidator.cs`: `IValidateOptions` pro režim a předponu klíče a prostředí.
- `ServiceCollectionExtensions.cs`: `AddEshopGuardBilling(IConfiguration)`.
- `Pricing/`:
  - `PriceListService.cs`: koncept, úpravy, `PreviewImpactAsync`, `PublishAsync`, `ActivateAsync`;
  - `PriceQuoteService.cs`: implementace `IPriceQuoteService` ze změny 10 nad `ShopScope`;
  - `TierResolver.cs`, `VolumeDiscountResolver.cs`, `FairUsePolicy.cs`;
  - `PriceQuote.cs`: model ceny k rozsahu (pásmo, částky, sleva, `vat_preview`, férové užití).
- `Orders/OrderService.cs`, `Orders/OrderSnapshot.cs`.
- `Orders/BillingRunPaymentGate.cs`: implementace `IRunPaymentGate` (změna 8) nad `billing.orders.status = 'paid'`. Nahradí `OrderTablePaymentGate`.
- `Stripe/`:
  - `IStripeGateway.cs`, `StripeGateway.cs`;
  - `StripeCatalogSync.cs`: Product, Price, kupóny, `lookup_key`, archivace;
  - `CheckoutService.cs`, `SavedCardPaymentService.cs`, `AccountCardService.cs`;
  - `StripeWebhookVerifier.cs`, `StripeEventProcessor.cs` a `Handlers/` (jeden soubor na skupinu událostí);
  - `SubscriptionService.cs`: zrušení, obnovení, nové zapnutí;
  - `SubscriptionChangePlanner.cs`, `SubscriptionScheduleComposer.cs`;
  - `StripeReconciler.cs`.
- `Tax/TaxTreatmentResolver.cs`, `Tax/TaxTreatment.cs`.
- `Invoicing/`:
  - `ISuperFakturaClient.cs`, `SuperFakturaClient.cs`, `SuperFakturaModels.cs`;
  - `InvoiceBuilder.cs`, `InvoiceIssuer.cs`, `CreditNoteIssuer.cs`;
  - `InvoiceZipWriter.cs`;
  - `Texts/sk.json`, `Texts/cs.json`.
- `Notifications/BillingEmails.cs`: šablony `billing.trial_reminder`, `billing.price_change`, `billing.tier_change`, `billing.payment_failed`, `billing.subscription_ended` (texty sk, cs). Zápis do `ops.outbox` (`kind = email`), skládá a odesílá `EmailComposer` a `OutboxEmailDispatcher` ze změny 9. Šablony neobsahují token, takže smějí přes outbox.

### `src/EshopGuard.Data/`

**Migrace `Migrations/2026xxxx_AddBilling`:**

| Tabulka | Doplněné sloupce a omezení |
|---|---|
| `billing.price_lists` | `stripe_mode`, `sync_status` (pending/synced/failed), `sync_error`, `fair_use_other_pages_factor numeric(5,2) default 2`, `activated_at`, U (`market_code`, `currency`, `valid_from`) WHERE `status = 'published'` |
| `billing.price_tiers` | `lookup_key` se mění na tři sloupce `lookup_key_analysis`, `lookup_key_monthly`, `lookup_key_yearly` (tvar `{market}_{currency}_{tier}_{analysis\|monthly\|yearly}`). Nesrovnalost: databáze, část 3.6 má jeden `lookup_key` na pásmo, ale pásmo má tři Price |
| `billing.volume_discounts` | `stripe_mode` |
| `billing.price_quotes` (nová, RLS) | `shop_id`, `basis_run_id` (ukázka), `scope_hash`, `price_list_id`, `tier_code`, `counted_products`, `other_pages`, `versions` jsonb (ze `ShopScope`), `markets` text[], `analysis_price`, `monitoring_monthly`, `discount_percent`, `currency`, `vat_preview` jsonb, `fair_use` jsonb, `status` (offer/individual_offer/unavailable), `reason_code`, U (`shop_id`, `scope_hash`, `price_list_id`) |
| `billing.orders` | `price_quote_id`, `scope_hash`, `stripe_price_analysis`, `stripe_price_monitoring`, `stripe_coupon_id`, `terms_version`, `tax_treatment`, `trial_end_planned`, `stripe_subscription_id`, `checkout_expires_at`, U (`shop_id`) WHERE `status IN ('created','checkout_open')` |
| `billing.subscriptions` | `stripe_price_id`, `stripe_coupon_id`, `stripe_schedule_id`, `shop_ordinal`, `trial_reminder_sent_at`, `pause_reason` |
| `billing.subscription_changes` | `reason_code`, `stripe_schedule_phase_hash` |
| `billing.invoices` | `source_key` (U), `source_kind` (stripe_invoice/stripe_refund), `period_from`, `period_to`, `needs_review_reason`, `email_status` |
| `billing.stripe_events` | `livemode`, `attempts`, `object_id` |
| `iam.tenants` | `tax_id_status` (none/pending/verified/unverified), `tax_id_verified_at` |

**Další soubory:**
- `Seed/BillingSeed.sql`: ceník SK/EUR a CZ/CZK jako koncept, pásma podle tabulky výše, `volume_discounts` prázdné.
- `Configurations/Billing/*.cs`: konfigurace EF pro nové sloupce a `PriceQuote`.

### `src/EshopGuard.Api/`

**`Endpoints/BillingEndpoints.cs`** (prefix `/api/t/{tenantId}`, `.RequireTenantRole(TenantRole.Admin)` ze změny 9, tedy admin a owner; jinak `403 auth.forbidden_role`). Nabídku ceny vrací endpoint `POST /shops/{shopId}/quote` změny 10.

| Metoda | Cesta | Účel |
|---|---|---|
| POST | `/shops/{shopId}/orders` | objednávka z nabídky (`quoteId`, `scopeHash`, `termsVersion`) |
| GET | `/orders/{orderId}` | stav objednávky po návratu ze Stripe |
| POST | `/orders/{orderId}/checkout` | URL Checkoutu |
| POST | `/orders/{orderId}/pay-with-saved-card` | platba kartou účtu, případně `client_secret` |
| GET | `/billing/overview` | e-shopy, stav, další platba, „Nová cena od …“, karta, pásma, součet |
| POST | `/shops/{shopId}/subscription/cancel` | zrušení ke konci období |
| POST | `/shops/{shopId}/subscription/resume` | obnovení před koncem období |
| POST | `/shops/{shopId}/subscription` | nové zapnutí po skončení |
| POST | `/billing/card/portal-session` | změna karty přes portál |
| POST | `/billing/card/setup-intent` | náhradní cesta změny karty |
| GET | `/invoices?shopId=&year=` | faktury a naplánované platby |
| GET | `/invoices/{invoiceId}/pdf` | krátkodobý podepsaný odkaz |
| GET | `/invoices/zip?shopId=&year=` | ZIP |

**Další soubory:**
- `Endpoints/PublicPricesEndpoints.cs`: `GET /api/public/prices?market=sk` (aktuální zveřejněný ceník bez ID Stripe, pro změny 13 a 14).
- `Endpoints/AdminPriceListEndpoints.cs`: `/api/admin/price-lists`, `…/{id}/tiers`, `…/{id}/volume-discounts`, `…/{id}/impact`, `…/{id}/publish`. Jen administrátorská role, každé volání do `ops.audit_log`.
- `Webhooks/StripeWebhookEndpoint.cs`: `POST /api/webhooks/stripe`, bez přihlášení, vlastní limit těla 512 kB.

### `src/EshopGuard.Worker/`

**`Handlers/Billing/*.cs`, obsluhy úloh:**

| Úloha | Druh | Klíč (`dedupe_key`) | Kdy |
|---|---|---|---|
| `billing.sync_price_list` | io | `price-sync:{priceListId}:{publishRequestId}` | zveřejnění (nový pokus po chybě = nový požadavek) |
| `billing.activate_price_list` | system | `price-activate:{priceListId}` | `valid_from` |
| `billing.schedule_price_list_transfer` | system | `price-transfer:{priceListId}` | po aktivaci |
| `billing.compose_schedule` | io | `schedule:{subscriptionId}:v{verze}:{otisk fází}`, po obnovení `schedule:{subscriptionId}:resume:{čas}` | po každé změně `subscription_changes` a po obnovení sledování |
| `billing.process_stripe_event` | io | `stripe:{eventId}` | webhook |
| `billing.issue_invoice` | io | `invoice:{stripeInvoiceId}` | `invoice.paid` |
| `billing.issue_credit_note` | io | `credit:{stripeRefundId}` | `charge.refunded` |
| `billing.evaluate_tiers` | system | `tiers:{tenantId}:{místní den}` a `tiers:{shopId}:{runId}` | denně po 3:30 místního času (úloha na tenanta) a po plné analýze, která spočítala produkty |
| `billing.trial_reminder` | system | `trial-reminder:{tenantId}:{datum}` | každou hodinu, na tenanta jednou denně; posílá pro `trial_end − 7 dní` |
| `billing.expire_order` | io | `expire-order:{orderId}:{pokus}:{settle\|expire}` | 1 min po platbě kartou účtu a v `checkout_expires_at` |
| `billing.reconcile_stripe` | io | `stripe-reconcile:{datum}` | denně 4:00 |
| `billing.archive_unused_prices` | io | `price-archive:{datum}` | týdně |

### `tests/`

- `tests/EshopGuard.Billing.Tests/`:
  - `PriceQuoteServiceTests`, `TierResolverTests`, `FairUsePolicyTests`, `VolumeDiscountResolverTests`;
  - `StripeCatalogSyncTests`, `CheckoutServiceTests`, `StripeEventProcessorTests`;
  - `SubscriptionJobTests` (platba kartou účtu, vypršení, připomínka zkušební doby), společné scénáře `StripeScenario`;
  - `SubscriptionScheduleComposerTests`, `TaxTreatmentResolverTests`;
  - `InvoiceIssuerTests`, `InvoiceBuilderTests`;
  - `BillingOptionsValidatorTests`, `SecretsNotLoggedTests`;
  - `Fakes/`.
- `tests/EshopGuard.Api.Tests/Billing/`:
  - `StripeWebhookEndpointTests` (podpis, duplicita, livemode);
  - `BillingEndpointsAuthorizationTests` (role, izolace tenantů);
  - `OrderFlowTests`;
  - `SubscriptionFlowTests` (karta účtu, změna karty, zrušení, obnovení, nové zapnutí, data po konci, přehled).
- `tests/EshopGuard.Billing.IntegrationTests/`: běží jen s proměnnou `ESHOPGUARD_STRIPE_TEST=1` proti testovacímu režimu Stripe a sandboxu SuperFaktúry, ne v CI.

## Odchylky při implementaci (2. 10. 2026)

Implementace se řídí tímto návrhem s odchylkami níže. Většina vychází z povoleného směru závislostí, z práv rolí databáze
(`eshopguard_app` jen čte globální ceník) a z chování Stripe.

### Skupiny 1–4 (projekt, datový model, ceník, ocenění)
- `EshopGuard.Billing` odkazuje i na `EshopGuard.Application` (`ProjectReferenceTests`): `IPriceQuoteService`, `ShopScope` a
  `IShopOrderReadiness` změny 10 jsou tam (projekt `Application` vznikl až změnou 9). `Application` na `Billing` neodkazuje.
- Obsluhy úloh `billing.*` jsou v knihovně `Billing` (`AddBillingJobs()`), ne ve `Worker/Handlers/Billing`: worker zůstává tenký
  jako u změny 11. API registruje jen `AddEshopGuardBilling()`.
- Brána `IStripeGateway` nevrací typy Stripe.net, ale vlastní záznamy (`Stripe/StripeModels.cs`); logika a testy Stripe.net
  nevidí. Falešná brána je `tests/Shared/FakeStripeGateway.cs` (sdílí `Billing.Tests` a `Api.Tests`). Stripe.net 53.0.0,
  verze API `2026-09-30.endive`; `Billing:Stripe:ApiVersion`, když je vyplněná, musí verzi balíčku odpovídat.
- `Billing:Stripe:Mode` má navíc hodnotu `disabled` (vývoj bez klíčů, nikdy `Production`): ceny a nabídky fungují z databáze,
  každé volání Stripe vrátí `503 billing.unavailable`, webhook nic nepřijme. Výchozí hodnota chybí (fail-closed), vývojové
  `appsettings.Development.json` má `disabled`.
- Daňová data jsou nastavení `Billing:Tax` (stát dodavatele, státy oblasti DPH EU, domácí sazba jen pro náhled), ne kód.
- Admin API ceníku nemá roli v tenantovi: administrátoři EshopGuard jsou `Admin:UserIds` (výchozí prázdné = nikdo,
  `RequirePlatformAdmin`, `EndpointPolicyValidator` to pro `/api/admin/` vyžaduje). API ceník jen čte; koncept, pásma, slevy,
  náhled dopadu a žádost o zveřejnění provádí worker úlohou `billing.price_list_admin` (role worker smí do ceníku zapisovat a
  vidí předplatná všech tenantů po tenantech). API čeká nejvýš `Api:InteractiveWaitSeconds`, pak `202` s úlohou. Náhled dopadu
  je uložený v `price_lists.impact`.
- Nové objekty Price vznikají bez `lookup_key`: Stripe odmítne klíč, který má jiná Price, bez `transfer_lookup_key`. Klíče
  přejdou na nové Price až při aktivaci (`TransferLookupKeyAsync`). Uložené ID se zapisuje hned po každém volání, takže
  přerušená synchronizace pokračuje a klíče idempotence chrání i mezikrok.
- Aktivní ceník trhu a měny = zveřejněný s `valid_from` ≤ nyní, nejnovější (požadavek). Aktivace (`valid_from`) navíc převede
  klíče, nastaví `ref.markets.price_list_id`, předchozí ceník převede na `retired` a založí převod předplatných.
- Migrace se podle zvyklosti repozitáře jmenuje `F8Billing`; výchozí ceník je v `Migrations/Sql/F8/01_billing.sql`, ne
  v `Seed/BillingSeed.sql`. Pásmo `t500` začíná 0 produkty (e-shop jen se stránkami), pásmo `custom` má ceny prázdné (sloupce
  cen jsou nově nullable). Navíc: `price_tiers.archived_at` (archivace v Stripe), `payments.stripe_charge_id` (vrácení peněz),
  `price_lists.impact`/`impact_at`, `orders.monitoring_monthly`, `monitoring_discount_percent`, `checkout_attempt`,
  `subscriptions.order_id`, `schedule_hash`; stavy jako výčty s CHECK. Práva workeru: DELETE na `price_tiers` a
  `volume_discounts` (náhrada pásem konceptu), INSERT do `stripe_events` (dorovnání), UPDATE jen sloupců `iam.tenants`
  ze Stripe; audit ceníku bez tenanta jen pro akce `price_list.*` (politika `audit_log_insert_price_list`). Produkty Stripe
  jsou v `ops.system_settings` (`billing:stripe_products:{mode}`).
- Odpověď `POST …/quote` (`PriceQuoteDto`) má navíc `status`, `reasonCode`, `priceUnit` a `countedProducts`. Opakovaná nabídka
  vrátí uložený řádek (stejné `quoteId` i částky).
- Změna 8 žádný `AnalysisPriceEstimator` nemá, ukládá jen základ rozsahu: úkol 4.5 je splněný testem, že pásmo nabídky je
  pásmo počtu ze základu ukázky.
- Testy: pravidla bez databáze a služby workeru nad databází (role worker) v `EshopGuard.Billing.Tests`, endpointy
  v `EshopGuard.Api.Tests/Billing`. Ceníky jsou globální, proto má každý test vlastní skrytý trh (`x…`).

### Skupiny 5–6 (objednávka, Checkout, webhooky)
- `POST /shops/{s}/orders` nejdřív ověří, že e-shop v tenantovi existuje (`404 shop.not_found`, stejně jako cizí e-shop), pak
  verzi obchodních podmínek (`409 billing.terms_outdated` s `current`). Otevřená objednávka e-shopu se vrátí (`200`) dřív,
  než se kontroluje nabídka; nová vznikne s `201`.
- `GET /orders/{o}` přijímá `sessionId` z `success_url`: `awaitingConfirmation` je `true`, jen když objednávka čeká
  v `checkout_open` a session je její. Návrat sám nic nepotvrzuje.
- Věta Checkoutu o první měsíční platbě je v `Billing/Texts/{sk,cs}.json` (jazyk tenanta, datum v `Localization:TimeZone`),
  ne v kódu. Částky se píší s nedělitelnou mezerou (`59 €`).
- Obsluhy událostí jsou metody `StripeEventProcessor` (`Webhooks/`), ne samostatné třídy ve `Stripe/Handlers/`. Každá načte
  aktuální objekt přes `IStripeGateway`. Tenant objektu je tenant jeho zákazníka; neznámý zákazník nebo metadata jiného
  tenanta znamenají `ignored` a záznam v logu (fail-closed). Podle metadat se přiřadí jen objekt bez zákazníka.
- Uvolnění běhu: `OrderTablePaymentGate` změny 8 už čte `billing.orders` se stavem `paid`, proto nevzniká
  `BillingRunPaymentGate`. `RunService.MarkOrderPaidAsync` je idempotentní, druhá událost běh znovu nespustí.
- `billing.stripe_events.payload` je celé tělo události (jako v návrhu databáze). Ze stejné tabulky čte procesor zákazníka
  u `customer.tax_id.*`, jehož objektem je IČ DPH.
- Webhook nad 512 kB vrací `413` a nic neuloží. Neplatný podpis, starší podpis než 300 s a chybějící hlavička vrací
  `400 billing.webhook_signature_invalid`. Do logu jde jen kód.
- Falešná brána dává ID unikátní napříč testy (sdílená testovací databáze má unikátní indexy na ID Stripe).
- Úkol 5.5 (testovací hodiny Stripe, Apple Pay a Google Pay) potřebuje testovací účet Stripe, zůstává otevřený se skupinou 0.

### Skupina 7 (předplatné, karta, zrušení)
- Druhé běžící předplatné e-shopu procesor neuloží: jedinečný index ho odmítne a vznikne upozornění provozu
  `billing.alert.second_running_subscription` (log úrovně error a audit tenanta). Nové předplatné po konci je nový řádek,
  sledování e-shopu se znovu zapne a starší řádky zůstávají jako historie.
- Platba kartou účtu (`SavedCardPaymentService`): předplatné nese `metadata.order_id`, otevřená session Checkoutu se nejdřív
  zavře a každý pokus zvýší `checkout_attempt`. Objednávku zaplatí webhook, nebo úloha `billing.expire_order` (1 min po
  platbě, když webhook nedorazil). Nepotvrzené 3-D Secure úloha v `checkout_expires_at` zruší ve Stripe a objednávka vyprší.
  Předplatné, které nikdy nezačalo, dostane `canceled` s `pause_reason = not_started` a přehled ho nezobrazuje. Chování
  `payment_behavior = default_incomplete` spolu se zkušební dobou a `add_invoice_items` je ověřené jen proti falešné bráně;
  skutečný Stripe ověří úkol 5.5.
- Změna karty: konfiguraci portálu (jen změna karty) je třeba založit v účtu Stripe a zapsat do
  `Billing:Stripe:PortalConfigurationId`, proto čeká se skupinou 0. SetupIntent nese `metadata.purpose = account_card`;
  událost `setup_intent.succeeded` s tímto účelem nastaví kartu stejně jako Checkout (`AccountCardService`), cizí SetupIntent
  se ignoruje. Klíč idempotence portálu a SetupIntentu je po minutách, opakované kliknutí dostane stejnou session.
- Zrušení a obnovení: cizí nebo neexistující e-shop vrací `404 shop.not_found` (izolace tenantů), e-shop bez předplatného
  `404 billing.subscription_not_found`, bez běžícího předplatného `409 billing.subscription_not_cancelable` nebo
  `billing.subscription_not_resumable`. Opakované volání Stripe nevolá.
- Nové zapnutí (`POST /shops/{s}/subscription`): pásmo je `shops.tier_code`, jinak pásmo posledního předplatného. Cena je
  z aktivního ceníku trhu tenanta, množstevní sleva podle pořadí mezi ostatními běžícími e-shopy. `confirm_required` vrací
  i nesouhlasná částka. Klíč `start-again:{shop}:{počet předplatných}:{price}:{kupón}`: dvojklik dostane stejné předplatné,
  po uloženém neúspěchu vznikne nové. Čeká-li uložené předplatné bez objednávky na 3-D Secure, vrátí se stejný `clientSecret`.
- Připomínka před první platbou: šablony e-mailů jsou druhy `EmailTemplateKind` změny 9 (`trial_ending`, `payment_failed`,
  `subscription_ended`, `price_change`, `tier_change`, `price_change_canceled`; sk a cs v `Application/Email/Templates`, úplnost
  hlídá `EmailTemplateCompletenessTests`), ne `Notifications/BillingEmails.cs`. Posílá je `NotificationDispatcher` změny 11
  (upozornění v aplikaci a e-mail vlastníkům a správcům přes `ops.outbox`). RLS nedovolí procházet předplatná napříč tenanty,
  proto plánovač každou hodinu založí úlohu pro každého tenanta se zákazníkem Stripe, s klíčem na den. Jedno odeslání zajistí
  `trial_reminder_sent_at`. Den se počítá v `Localization:TimeZone`, zrušená zkušební doba připomínku nedostane.
- Napojení na sledování (7.7): změna 16 zatím není, test ověřuje jen to, že nálezy, doklady a soubor dokladu zůstanou po konci
  předplatného čitelné.
- Přehled (`GET /billing/overview`): počet produktů je `countedProducts` nabídky zaplacené objednávky, jinak
  `shops.product_count`. Součet za měsíc zahrnuje stavy trial, active a past_due. „Nová cena od …“ je nejbližší změna
  `subscription_changes` ve stavu `scheduled` nebo `notified` druhu `tier`, `price_list` nebo `discount`.
- Známé souběhy: webhook a úloha `billing.expire_order` mohou objednávku vyřizovat současně; zaplacení je idempotentní a běh se
  spustí jednou. Kliknutí na přelomu minuty založí dvě session portálu, platí ta poslední.

### Skupina 8 (změny pásma, slevy a ceníku)
- Počet produktů pro pásmo (`CountedProductsReader`): po plné analýze analyzované produkty × počet trhů verze (když mají všechny
  kontrolované verze stejný počet trhů), jinak `ShopScope.PriceCount`. Částečná analýza dává dolní mez a smí naplánovat jen
  vyšší pásmo. Bez počtu se nic neplánuje. Pásmo `custom` (nad 20 000) nic nemění a při každé kontrole vyvolá upozornění provozu
  `billing.alert.tier_custom` (individuální nabídka).
- `billing.evaluate_tiers` běží po tenantech (RLS): plánovač každou hodinu založí úlohu tenanta po 3:30 místního času s klíčem
  dne. Po plné analýze založí úlohu e-shopu `IProductCountObserver` v `Jobs` (`Jobs` na `Billing` neodkazuje). Úloha kromě pásma
  přeplánuje i ceník a slevu, takže dorovná ztracené změny. Předplatná zrušená ke konci období přeskočí.
- Plánování: cíl rovný aktuální ceně zruší čekající změnu stejného druhu (`not_needed`, oznámenou s e-mailem
  `price_change_canceled`), jiný cíl ji nahradí, stejný cíl ji ponechá i s datem. Nahrazení, které nezmění částku, pošle e-mail
  o zrušení původní změny.
- Zdražení platí od prvního začátku období ≥ max(`valid_from`, nyní + `notice_days`, `published_at` + `notice_days`,
  `founder_until` v budoucnu). Člen „nyní + lhůta“ chrání zákazníka při pozdním převodu. Upozornění má vždy `lockedUntil`
  (bez zakladatelské ceny prázdné). Zlevnění platí od prvního začátku období ≥ `valid_from`. Známé omezení: změna ceníku se
  stejnou cenou (`price_unchanged`), kterou pozdější vyšší pásmo zdraží, lhůtu znovu nepočítá.
- Změna `price_list` odvodí kupón z pořadí e-shopu podle slev nového ceníku. Konec předplatného
  (`customer.subscription.deleted`) zruší jeho čekající změny (`subscription_ended`, bez e-mailu, konec má vlastní), běžící
  předplatná tenanta se přečíslují podle `created_at` a každé si naplánuje slevu.
- Upozornění a e-mail uvádějí měsíční cenu (roční interval zatím není).
- Převod ceníku (`billing.schedule_price_list_transfer`) prochází tenanty po dávkách 200, každý tenant ve vlastní transakci
  (RLS), ne po 200 předplatných. Opakovaný převod změny nezdvojí.
- Skládání plánu: klíč úlohy `schedule:{sub}:v{verze}:{otisk}`; verze roste s každou naplánovanou, zrušenou i použitou změnou,
  takže nový stav plánu dostane vlastní úlohu i se stejným otiskem. Otisk nezahrnuje začátek aktuální fáze, `start_date` má jen
  první fáze. Změna, jejíž datum nastalo, čeká na událost Stripe (`billing.schedule_change_pending`, nový pokus za 15 min),
  stejně jako předplatné s jinou cenou ve Stripe než v databázi (`billing.subscription_stale`). Změna, kterou Stripe do 1 dne
  po datu nepoužil, se zruší (`missed`) s upozorněním `billing.alert.change_missed`; bez dalších změn se plán uvolní.
- Promítnutí (8.6): `customer.subscription.updated` s jinou Price nebo kupónem použije nejnovější čekající změnu splatnou do
  1 dne, jejíž cíl odpovídá, spolu se všemi před ní. Zbytek plánu se znovu složí. Přepnutí, které žádná změna nevysvětluje,
  jen zapíše synchronizace.
- Zrušení sledování uvolní plán ve Stripe (`stripe_schedule_id` a otisk se vymažou), obnovení založí složení plánu a kontrolu
  pásma.
- Test 8.6 s testovacími hodinami Stripe je úkol 8.7 a čeká se skupinou 0. Testy skupiny 8 jsou v `SubscriptionChangeTests`
  (`EshopGuard.Billing.Tests`) nad falešnou bránou Stripe.

### Skupina 9 (daňový režim)
- Opravené uváznutí: IČ DPH jiného státu EU ve stavu `none` dřív vedlo na `undetermined` (422 a záznam pro účetní). IČ DPH
  přitom do Stripe odesílá jen `StripeCustomers.EnsureAsync`, a ten běžel až po kontrole daně, takže se česká firma nemohla
  nikdy ověřit. Teď je `none` s IČ DPH `pending_verification`: objednávka vznikne, platba (`PaymentTaxGate`) nejdřív odešle
  IČ DPH zákazníkovi Stripe a vrátí 409 `billing.tax_id_pending`, dokud `customer.tax_id.updated` nepřinese `verified`.
- `PaymentTaxGate` je společná pro Checkout, platbu uloženou kartou a obnovení sledování a běží mimo transakci, aby
  odeslání IČ DPH a `stripe_customer_id` zůstaly uložené i při odmítnutí platby.
- Při placení se `orders.tax_treatment`, `vat_rate`, `vat_amount` a `amount_gross` přepočítají podle aktuálního stavu tenanta
  (`TaxTreatmentResolver.Charge`), protože objednávka mohla vzniknout ještě před ověřením.
- Kontrola dokladu (`InvoiceTaxCheck`): domácí DPH sedí, když Stripe neúčtoval přenesení ani osvobození a daň z částky bez
  daně odpovídá domácí sazbě `Billing:Tax:DomesticVatRate` s tolerancí 1 cent na řádek (Stripe Tax zaokrouhluje po
  řádcích). Přenesení sedí bez daně a s příznakem na faktuře nebo u zákazníka (`customer_tax_exempt = reverse`).
  `pending_verification` a `undetermined` jsou vždy nesoulad. Změna domácí sazby zastaví doklady, dokud ji nastavení
  nepřevezme (fail-closed).
- Očekávaný režim je `orders.tax_treatment` zaplacené objednávky (první faktura `subscription_create`). Obnovy předplatného
  se kontrolují proti aktuálnímu režimu tenanta.
- `InvoiceIssuer` (úloha `billing.issue_invoice`) založí jeden řádek `billing.invoices` na fakturu Stripe (`source_key`):
  - odběratel je snímek údajů tenanta bez e-mailu;
  - řádky, částky i `reverse_charge` jsou tak, jak je Stripe účtoval;
  - nesoulad dá `needs_review` s `tax_mismatch`, audit `invoice.needs_review` s očekávaným i účtovaným režimem a upozornění
    `billing.alert.tax_mismatch`;
  - shodný doklad zůstane `creating` pro SuperFaktúru (skupina 10), období dokladu se doplní tam;
  - nezaplacená faktura (0) se přeskočí;
  - faktura bez známého e-shopu selže s upozorněním `billing.alert.invoice_shop_unknown`.
- Úprava fakturačních údajů tenanta (IČO, IČ DPH, adresa; změna 9 ji odložila do změny 12) je úkol 9.4. Obrazovka
  `design/ui/BillingDetails.dc.html` (8b) je schválená 3. 10. 2026. Rozhodnutí uživatele 3. 10. 2026:
  - samostatná stránka v Nastavenia;
  - zemi sídla po první platbě nejde změnit (měna účtu je pevná), jen přes podporu;
  - předvyplnění z registrů podle IČO později, samostatnou změnou;
  - formát IČO se nekontroluje (v každé zemi je jiný), jen to, že je vyplněné.

  Endpoint `GET`/`PUT /api/t/{t}/billing/details` (`BillingDetailsService`):
  - Stripe Tax (`automatic_tax`) počítá daň i podle tax id zákazníka. Při novém nebo odebraném IČ DPH se proto stará tax id
    smažou ještě před uložením, pod zámkem řádku tenanta. Když Stripe není dostupný, vrátí se 503 a nic se neuloží, takže
    u zákazníka nezůstane staré IČ DPH.
  - Po uložení se zákazník Stripe aktualizuje a nové IČ DPH se odešle k ověření jen nepovinně. Když to selže, údaje zůstanou
    uložené ve stavu `none` a odešle je až `PaymentTaxGate` při platbě.
  - Klíč idempotence tax id obsahuje verzi řádku tenanta, aby se IČ DPH vrácené na dřívější hodnotu odeslalo znovu.
  - Objednávka dál vyžaduje jen IČO (`billing.company_id_required`). Úplnost ostatních údajů ukazuje `complete`.

### Skupina 11 (seznam faktur a ZIP)
- `InvoiceListService` (`EshopGuard.Billing/Invoicing`) vrací doklady i naplánované platby jedním seznamem `InvoiceListDto`
  (`Items`, `Years` pro výběr roku), seřazeným od nejnovějšího. Rok je místní (Europe/Bratislava), takže doklad
  z 31. 12. 23:30 UTC patří do následujícího roku.
- Stav řádku: dobropis `refunded`, proforma `issued`, jinak `paid`; naplánovaná platba `scheduled` bez čísla a bez PDF.
  Vypisují se doklady všech stavů (`creating`, `needs_review`, `failed` také, platba proběhla a nic se neskrývá), PDF mají
  jen vystavené (`issued` s klíčem `tenants/{tenantId}/invoices/{invoiceId}.pdf`).
- Položka `analysis`, když platba dokladu patří k objednávce nebo řádek nese cenu analýzy (`orders.stripe_price_analysis`),
  jinak `monitoring`. Dobropis přebírá položku původního dokladu.
- Období dokladu se bere ze sloupců `period_from`/`period_to`. Dokud je nevyplní skupina 10, odvozuje se z řádků Stripe
  (nejmenší začátek až největší konec minus den, místní datum).
- Naplánovaná platba: další platba každého e-shopu z přehledu `GET /billing/overview` (končící nebo skončené sledování ji
  nemá) s cenou bez DPH. Částka s DPH jen u režimů `domestic_vat` a `reverse_charge`, jinak zůstane prázdná (fail-closed,
  nic se nedopočítává).
- PDF: `GET /invoices/{invoiceId}/pdf` přesměruje na podepsaný odkaz `BlobLinks` na 5 minut, soubor
  `EG_2026_0042.pdf` (číslo dokladu bez lomítek). Doklad bez PDF vrátí 409 `billing.invoice_pdf_missing`, cizí 404.
- ZIP: `GET /invoices/zip` (ne `POST` s asynchronní úlohou). `InvoiceZipWriter` čte PDF jedno po druhém z `IBlobStore`
  a posílá je rovnou do odpovědi. `ZipArchive` zapisuje synchronně i při zavření položky, což server na odpovědi nedovolí,
  proto zapisuje do mezipaměti, která se po každé položce odešle asynchronně; v paměti je nejvýš jedno komprimované PDF.
  Strop 500 počítá doklady s PDF; nad ním 422 `billing.zip_too_large`. `X-EshopGuard-Skipped` = doklady podle filtru, které
  v ZIPu nejsou (bez PDF nebo s chybějícím souborem). Opakované číslo dostane příponu `-2`, `-3`… Soubor
  `invoices-{rok}.zip`, s filtrem e-shopu `invoices-{doména}-{rok}.zip`.
- Odchylka od scénáře „Cizí tenant a nedostatečná role“: editor dostane 403 i na PDF cizího dokladu, protože filtr role běží
  před obsluhou endpointu. 404 dostane owner a admin tenantu A. Odkaz nevznikne ani v jednom případě. Scénář je upravený.
- `BillingEndpointsAuthorizationTests` drží přesný výčet 15 endpointů změny 12 pod `/api/t/{tenantId}` (nový endpoint bez
  záznamu test shodí) a ověřuje roli i 404 pro objekty tenanta B bez změny jeho řádků a bez volání Stripe.
- Změna 13 (`add-web-app-frontend/design.md`, obrazovky 8 a 9b) počítá s jinými cestami a musí se sladit:
  - stránkování `cursor` neexistuje, objem omezuje filtr roku;
  - ZIP je `GET /invoices/zip` se streamem, ne `POST` s průběhem;
  - zrušení je `POST /shops/{shopId}/subscription/cancel`, změna karty `POST /billing/card/portal-session`.
