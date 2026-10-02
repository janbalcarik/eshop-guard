# Delta for Billing

## ADDED Requirements

### Requirement: Ceník v databázi po trzích a měnách

Systém MUST vést ceník jako verze v `billing.price_lists` pro dvojici trh a měna, s pásmy podle počtu zveřejněných produktů v `billing.price_tiers` (do 500, do 2 000, do 5 000, do 20 000, nad 20 000 dohodou) a se slevami za počet e-shopů v `billing.volume_discounts`. Pro trh a měnu MUST platit vždy nejvýš jedna aktivní verze: zveřejněná, s `valid_from` ≤ nyní, nejnovější. Ceník MUST jít změnit bez nasazení, přes admin API. Každá změna ceníku MUST jít do `ops.audit_log`.

#### Scenario: Výběr pásma podle hranic včetně

- GIVEN aktivní ceník SK/EUR s pásmy `t500` (1–500), `t2000` (501–2 000), `t5000` (2 001–5 000), `t20000` (5 001–20 000) a `custom` (nad 20 000)
- WHEN se určuje pásmo pro 500, 501, 20 000 a 20 001 započtených produktů
- THEN výsledek je `t500`, `t2000`, `t20000` a `custom`
- AND pásmo `custom` nemá cenu ani objekt Price ve Stripe a nabídka v něm má stav `individual_offer`

#### Scenario: Chybějící ceník pro trh a měnu

- GIVEN tenant s trhem `cz` a měnou CZK a ceník CZ/CZK jen jako koncept
- WHEN si tenant vyžádá nabídku ceny e-shopu
- THEN nabídka má stav `unavailable` a kód `billing.price_list_missing`
- AND objednávku z ní nejde vytvořit a nevznikne žádné volání Stripe

#### Scenario: Náhled dopadu před zveřejněním

- GIVEN koncept nového ceníku SK/EUR se sledováním `t2000` za 21 € místo 19 € a 40 běžících předplatných v pásmu `t2000`
- WHEN administrátor zavolá `GET /api/admin/price-lists/{id}/impact`
- THEN odpověď uvede 40 předplatných se zdražením, 0 se zlevněním a nejbližší datum účinnosti podle `notice_days`
- AND nic se nezmění ve Stripe ani v předplatných

### Requirement: Synchronizace ceníku do Stripe

Systém MUST při zveřejnění ceníku založit ve Stripe pro každé pásmo s cenou tři objekty Price (analýza jednorázově, sledování měsíčně a ročně, `tax_behavior = exclusive`) a pro každou slevu kupón omezený na produkt sledování. ID objektů MUST uložit do `price_tiers` a `volume_discounts`. Každé volání Stripe MUST nést klíč idempotence odvozený z ID ceníku, pásma a druhu ceny. Ceník MUST přejít do stavu `published` až po úspěšném založení všech objektů. `lookup_key` MUST přejít na nové objekty Price s `transfer_lookup_key` až k `valid_from` ceníku.

#### Scenario: Úspěšné zveřejnění a převod lookup_key k datu platnosti

- GIVEN koncept ceníku SK/EUR se 4 pásmy s cenou, sleva od 3. e-shopu 10 % a `valid_from` za 5 dní
- WHEN administrátor ceník zveřejní
- THEN ve Stripe vznikne 12 objektů Price a 1 kupón s `applies_to` na produkt sledování a ceník má `status = published` a `sync_status = synced`
- AND `lookup_key` (například `sk_eur_t2000_monthly`) zůstane na starých objektech Price až do `valid_from`, kdy je úloha `billing.activate_price_list` přesune a nastaví `ref.markets.price_list_id`

#### Scenario: Výpadek Stripe uprostřed synchronizace

- GIVEN zveřejňovaný ceník a Stripe, který po 5 založených Price vrátí chybu 500
- WHEN úloha `billing.sync_price_list` skončí chybou
- THEN ceník zůstane `draft` se `sync_status = failed` a nové objednávky dál používají předchozí ceník
- AND opakovaná úloha díky klíčům idempotence nezaloží znovu už existujících 5 objektů Price a dokončí zbylých 7

#### Scenario: Ceník z jiného režimu Stripe

- GIVEN aplikace v testovacím režimu Stripe a ceník s `stripe_mode = live`
- WHEN se má z ceníku vytvořit nabídka nebo Checkout
- THEN systém ceník odmítne s kódem `billing.stripe_mode_mismatch`
- AND do Stripe nepošle žádné ID objektu Price z jiného režimu

### Requirement: Ocenění rozsahu e-shopu

Systém MUST k rozsahu e-shopu, který spočítá `ShopScopeCalculator` (změna 10), přes `IPriceQuoteService` určit:
- pásmo podle počtu započtených produktů;
- cenu analýzy a měsíčního sledování z aktivního ceníku trhu a měny tenanta;
- slevu podle pořadí e-shopu v účtu;
- odhad DPH;
- stav férového užití.

Započtené produkty MUST být součet produktů za každou zaškrtnutou zemi (rozhodnutí 2. 10. 2026) a hranice pásem MUST pocházet z `billing.price_tiers`, ne z kódu. Důvody, proč se verze nekontroluje, MUST nabídka převzít z rozsahu beze změny. Každou cenu, kterou zákazník viděl, MUST zapsat do `billing.price_quotes` jednou na kombinaci e-shopu, `scopeHash` a ceníku, stav e-shopu přitom nemění. Když počet ostatních stránek překročí `fair_use_other_pages_factor` × počet produktů, nabídka MUST mít stav `individual_offer` a nesmí jít zaplatit.

#### Scenario: Dvě země po 5 834 produktech

- GIVEN rozsah bylinkovo.sk ze změny 10 s místy prodeje SK a CZ, slovenskou verzí pro SK a českou pro CZ po 5 834 produktech, celkem 11 668
- WHEN endpoint `POST …/quote` zavolá `IPriceQuoteService`
- THEN nabídka má pásmo `t20000`, analýzu 199 € a sledování 59 € měsíčně bez DPH a počty produktů po zemích
- AND v `billing.price_quotes` je jeden záznam s `scope_hash` tohoto rozsahu

#### Scenario: Odškrtnutí země dá novou cenu hned

- GIVEN nabídka bylinkovo.sk v pásmu `t20000` a česká verze potřebná jen kvůli místu prodeje CZ
- WHEN zákazník na obrazovce 3c odškrtne Česko a změna 10 pošle rozsah s 5 834 produkty a českou verzí s důvodem `not_needed_by_markets`
- THEN nabídka má pásmo `t20000` (5 834 > 5 000) a nový záznam s jiným `scope_hash`
- AND důvod `not_needed_by_markets` je v nabídce beze změny

#### Scenario: Opakovaný dotaz se stejným rozsahem

- GIVEN záznam nabídky pro rozsah s `scope_hash` H a ceník A
- WHEN zákazník obrazovku 3c otevře znovu a rozsah se nezměnil
- THEN `IPriceQuoteService` vrátí stejné částky a stejné `quoteId`
- AND druhý záznam v `billing.price_quotes` nevznikne

#### Scenario: Překročené férové užití

- GIVEN e-shop s 300 produkty, 4 000 ostatními stránkami a `fair_use_other_pages_factor = 2`
- WHEN se spočítá nabídka
- THEN nabídka má stav `individual_offer` a kód `billing.fair_use_exceeded`
- AND API pro ni odmítne vytvořit objednávku s kódem `billing.quote_not_payable`

### Requirement: Garantovaná cena objednávky

Systém MUST před vytvořením objednávky:
- znovu spočítat rozsah e-shopu z uloženého stavu;
- ověřit, že `scopeHash` odpovídá nabídce, kterou zákazník potvrdil, a že ceník nabídky je stále aktivní;
- ověřit připravenost e-shopu přes `IShopOrderReadiness` (změna 10).

Do objednávky MUST uložit snímek částek, měny, daňového režimu, verze obchodních podmínek, `scope_hash` a ID objektů Price a kupónu. Zákazník MUST zaplatit přesně částky ze snímku. Když plná analýza najde víc produktů, cena zaplacené analýzy se MUST NOT změnit a běh se MUST NOT zastavit kvůli doplatku.

#### Scenario: Nabídka zastarala kvůli novému ceníku

- GIVEN nabídka z 1. 11. podle ceníku A a 2. 11. aktivovaný ceník B
- WHEN zákazník 3. 11. zavolá `POST /shops/{s}/orders` s ID staré nabídky
- THEN API vrátí 409 s kódem `quote.stale`, parametrem `reason = price_list_changed` a v těle novou nabídku podle ceníku B
- AND objednávka nevznikne, dokud zákazník nepotvrdí novou nabídku

#### Scenario: Rozsah se změnil v jiné záložce

- GIVEN nabídka s `scope_hash` H1 a zákazník, který mezitím v jiné záložce odškrtl Česko (uložený rozsah má H2)
- WHEN první záložka odešle objednávku s H1
- THEN API vrátí 409 s kódem `quote.stale`, parametrem `reason = scope_changed` a nabídkou pro H2
- AND žádná Checkout Session nevznikne

#### Scenario: Analýza najde víc produktů než ukázka

- GIVEN zaplacená objednávka v pásmu `t2000` (1 460 produktů) a plná analýza, která zjistí, že e-shop má 2 920 započtených produktů
- WHEN běh analýzy skončí
- THEN zkontroluje se celý e-shop bez doplatku a `orders.amount_net` zůstane 69 €
- AND úloha `billing.evaluate_tiers` naplánuje sledování v pásmu `t5000` od dalšího období s upozorněním e-mailem

#### Scenario: Dvě otevřené objednávky pro stejný e-shop

- GIVEN e-shop s objednávkou ve stavu `checkout_open`
- WHEN druhá záložka prohlížeče zavolá `POST /shops/{s}/orders`
- THEN API vrátí existující otevřenou objednávku a druhou nezaloží
- AND `POST /orders/{o}/checkout` vrátí stejnou nevypršelou Checkout Session

### Requirement: Platba úvodní analýzy a sledování přes Stripe Checkout

Systém MUST pro první platbu tenanta založit Stripe Checkout Session v režimu předplatného:
- s jednorázovou položkou analýzy a předplatným sledování se zkušební dobou;
- s metadaty `tenant_id`, `shop_id` a `order_id`;
- se zákazníkem Stripe tenanta;
- s automatickým výpočtem daně;
- s jazykem tenanta;
- s textem o měsíční platbě po zkušební době a o možnosti kdykoli zrušit.

Platební metody MUST zahrnovat kartu, Apple Pay a Google Pay. Běh analýzy MUST zůstat ve stavu `awaiting_payment`, dokud zpracování webhooku nepotvrdí zaplacení objednávky. Návrat ze Stripe (`success_url`) sám o sobě platbu MUST NOT potvrzovat.

#### Scenario: Úspěšná platba spustí analýzu

- GIVEN objednávka bylinkovo.sk za 199 € bez DPH s během ve stavu `awaiting_payment`
- WHEN zákazník zaplatí testovací kartou a přijde `checkout.session.completed` s `payment_status = paid`
- THEN objednávka má stav `paid`, vznikne `subscriptions` ve stavu `trialing` s `trial_end` podle `Billing:TrialMode` a běh přejde do stahování
- AND karta se uloží jako výchozí karta účtu v `payment_methods`

#### Scenario: Návrat do aplikace dřív než webhook

- GIVEN zákazník zaplatil a prohlížeč se vrátil na `success_url` dřív, než přišel webhook
- WHEN aplikace zavolá `GET /orders/{o}`
- THEN odpověď má stav `checkout_open` s příznakem `awaiting_confirmation` a běh zůstane `awaiting_payment`
- AND po zpracování webhooku vrátí stejný dotaz stav `paid`

#### Scenario: Zákazník platbu nedokončí

- GIVEN otevřená Checkout Session s `expires_at` za 60 minut
- WHEN session vyprší a přijde `checkout.session.expired`
- THEN objednávka má stav `expired`, běh zůstane `awaiting_payment` a nic se nestrhne
- AND nová objednávka z téže nabídky jde vytvořit, pokud nabídka není zastaralá

#### Scenario: Zkušební doba do stejného dne příštího měsíce

- GIVEN `Billing:TrialMode = calendar_month` a platba 31. 1. 2027
- WHEN se založí Checkout Session
- THEN `subscription_data.trial_end` je 28. 2. 2027 ve stejnou hodinu a souhrn objednávky uvede „Prvá mesačná platba … bude 28. 2. 2027“
- AND při `TrialMode = days_30` by `trial_end` byl 2. 3. 2027

### Requirement: Jedna platební karta na účet

Systém MUST mít pro tenanta nejvýš jednu platnou výchozí kartu (`payment_methods` s `is_default` a bez `detached_at`) a MUST ji používat pro všechna předplatná tenanta. Další e-shop tenanta s uloženou kartou MUST jít zaplatit tlačítkem „Zaplatiť kartou •••• {last4}“ bez nového zadávání karty, s ověřením 3-D Secure, když ho banka vyžádá. Změna karty MUST proběhnout přes zákaznický portál Stripe (tok `payment_method_update`) nebo přes SetupIntent a MUST se promítnout do všech běžících předplatných tenanta.

#### Scenario: Druhý e-shop uloženou kartou

- GIVEN tenant s kartou VISA •••• 4242 a nabídka pro e-shop bylinkovo-darceky.sk v pásmu `t500`
- WHEN zákazník zavolá `POST /orders/{o}/pay-with-saved-card` a banka 3-D Secure nevyžaduje
- THEN vznikne předplatné s položkou sledování, jednorázovou položkou analýzy 39 € a zkušební dobou a objednávka je po webhooku `paid`
- AND Checkout se neotevře

#### Scenario: Banka vyžaduje 3-D Secure u uložené karty

- GIVEN platba uloženou kartou, jejíž první faktura skončí stavem `requires_action`
- WHEN API zpracuje `pay-with-saved-card`
- THEN vrátí `client_secret` a stav `requires_action` a běh zůstane `awaiting_payment`
- AND když zákazník ověření nedokončí, objednávka po vypršení přejde do `expired` a předplatné ve stavu `incomplete` se zruší

#### Scenario: Změna karty platí pro všechna předplatná

- GIVEN tenant se třemi běžícími předplatnými a kartou •••• 4242
- WHEN zákazník v portálu Stripe zadá kartu •••• 1881 a přijde `customer.updated`
- THEN všechna tři předplatná mají `default_payment_method` = •••• 1881 a stará karta má `detached_at`
- AND přehled `GET /billing/overview` ukáže „•••• 1881 · pre všetky e-shopy v účte“

### Requirement: Příjem webhooků Stripe s odstraněním duplicit

Systém MUST přijímat webhooky Stripe na `POST /api/webhooks/stripe`, ověřit podpis z hlavičky `Stripe-Signature` (tolerance 300 s) a shodu `livemode` s nastaveným režimem. Událost MUST uložit do `billing.stripe_events` a ve stejné transakci založit úlohu zpracování. Pak MUST odpovědět 200 bez čekání na zpracování. Tatáž událost doručená vícekrát MUST být zpracována nejvýš jednou. Zpracování MUST načíst aktuální stav objektu ze Stripe a MUST NOT spoléhat na pořadí událostí. Noční dorovnání MUST doplnit zaplacené faktury a změny předplatných, jejichž webhook chybí.

#### Scenario: Neplatný podpis

- GIVEN požadavek na `/api/webhooks/stripe` s hlavičkou `Stripe-Signature` podepsanou jiným tajemstvím
- WHEN ho API přijme
- THEN odpoví 400, do `stripe_events` nic neuloží a nezaloží žádnou úlohu
- AND do logu zapíše jen kód `billing.webhook_signature_invalid` bez těla požadavku

#### Scenario: Dvakrát doručená událost

- GIVEN událost `evt_1` typu `invoice.paid` už uložená a zpracovaná
- WHEN Stripe doručí `evt_1` znovu
- THEN API odpoví 200, druhý řádek ani druhá úloha nevznikne
- AND v SuperFaktúře existuje k faktuře Stripe jediný doklad

#### Scenario: Události v obráceném pořadí

- GIVEN předplatné ve stavu `trialing`
- WHEN dorazí `customer.subscription.updated` se starším stavem až po události s novějším stavem `active`
- THEN zpracování načte předplatné ze Stripe a v `billing.subscriptions` zůstane `active`
- AND starší obsah zprávy se nikdy nezapíše přes novější stav

#### Scenario: Ztracený webhook doplní noční dorovnání

- GIVEN zaplacená obnova předplatného, jejíž webhook `invoice.paid` nedorazil
- WHEN proběhne úloha `billing.reconcile_stripe`
- THEN chybějící platba se zapíše do `payments` a založí se úloha `billing.issue_invoice` se stejným klíčem jako z webhooku
- AND když webhook dorazí později, druhý doklad nevznikne

### Requirement: Předplatné sledování po e-shopech

Systém MUST mít pro každý e-shop nejvýš jedno běžící předplatné Stripe (`trialing`, `active`, `past_due`, `incomplete`) s fakturačním dnem odvozeným ode dne analýzy. Systém MUST:
- 7 dní před koncem zkušební doby poslat připomenutí s částkou a datem první platby;
- při neúspěšné platbě ukázat upozornění v aplikaci a poslat e-mail, doklad přitom nevzniká;
- po posledním neúspěšném pokusu pozastavit sledování e-shopu, nálezy, opravy, doklady a protokoly přitom zachovat;
- při zrušení nechat sledování běžet do konce zaplaceného období, během zkušební doby nic nestrhnout a do konce období umožnit zrušení vrátit.

#### Scenario: Připomenutí před první platbou

- GIVEN předplatné ve stavu `trialing` s `trial_end` 1. 11. 2026
- WHEN plánovač 25. 10. 2026 spustí `billing.trial_reminder`
- THEN do `ops.outbox` jde e-mail v jazyce tenanta s částkou 59 € bez DPH, datem 1. 11. 2026 a odkazem na zrušení
- AND opakované spuštění úlohy druhý e-mail nepošle (`trial_reminder_sent_at`)

#### Scenario: Neúspěšná platba a konec po posledním pokusu

- GIVEN aktivní předplatné, u kterého selže obnova
- WHEN přijde `invoice.payment_failed`
- THEN předplatné má stav `past_due`, aplikace ukáže upozornění s odkazem na změnu karty a doklad nevznikne
- AND po posledním neúspěšném pokusu a `customer.subscription.deleted` má e-shop pozastavené sledování a nálezy, opravy a doklady zůstanou dostupné

#### Scenario: Zrušení během zkušební doby

- GIVEN e-shop ve zkušební době do 19. 5. 2027
- WHEN zákazník 10. 5. zavolá `POST /shops/{s}/subscription/cancel`
- THEN předplatné má `cancel_at_period_end = true`, přehled ukáže „Sledovanie skončí 19. 5. 2027“ a 19. 5. se nic nestrhne
- AND do 19. 5. jde zrušení vrátit přes `POST …/subscription/resume`

#### Scenario: Nové zapnutí po skončení sledování

- GIVEN e-shop se zrušeným předplatným a tenant s uloženou kartou
- WHEN zákazník zavolá `POST /shops/{s}/subscription`
- THEN vznikne nové předplatné bez zkušební doby podle aktuálního ceníku a pásma a první platba se strhne hned
- AND před potvrzením API vrátí částku a datum k zobrazení (`confirm_required`) a předplatné založí až po potvrzení

### Requirement: Změna pásma a slevy od dalšího období

Systém MUST denně a po každé změně počtu produktů porovnat pásmo každého běžícího předplatného s aktuálním počtem započtených produktů. Rozdíl MUST zapsat jako změnu `subscription_changes` s účinností od dalšího období:
- u vyššího pásma nejdřív `TierChangeNoticeDays` po odeslání upozornění;
- u nižšího pásma od nejbližšího období.

Při přidání nebo zrušení e-shopu MUST stejně přepočítat slevu od dalšího období. Systém MUST NOT účtovat poměrný doplatek ani vystavovat dobropis kvůli změně pásma nebo slevy.

#### Scenario: E-shop přeroste pásmo

- GIVEN předplatné v pásmu `t2000` s obnovou 1. 12. a `TierChangeNoticeDays = 7`
- WHEN 20. 11. konektor nahlásí 2 150 zveřejněných produktů
- THEN vznikne změna `tier` na `t5000` s účinností 1. 12., e-mail s cenou 19 € → 29 € a přehled ukáže „Nová cena od 1. 12.“
- AND Subscription Schedule má od 1. 12. fázi s objektem Price `t5000` bez poměrného přepočtu

#### Scenario: Upozornění by přišlo pozdě

- GIVEN předplatné s obnovou 1. 12. a `TierChangeNoticeDays = 7`
- WHEN 28. 11. počet produktů přeroste pásmo
- THEN změna má účinnost až od období začínajícího 1. 1.
- AND obnova 1. 12. proběhne v původním pásmu

#### Scenario: Počet produktů se vrátí před účinností

- GIVEN naplánovaná změna `tier` na `t5000` od 1. 12.
- WHEN 25. 11. klesne počet produktů na 1 900
- THEN změna přejde do stavu `canceled`, Subscription Schedule se složí znovu bez ní a zákazník dostane e-mail, že se změna neuplatní
- AND obnova 1. 12. proběhne v pásmu `t2000`

#### Scenario: Zrušení e-shopu odebere slevu třetímu

- GIVEN tenant se třemi předplatnými, sleva 10 % od 3. e-shopu na třetím
- WHEN zákazník zruší sledování prvního e-shopu a jeho období skončí
- THEN třetí e-shop je nově druhý a od svého dalšího období platí bez slevy, s e-mailem o nové ceně
- AND změna je zapsaná v `subscription_changes` s `kind = discount` a v auditu

### Requirement: Změna ceníku u běžících předplatných

Systém MUST po aktivaci nového ceníku naplánovat u každého běžícího předplatného jeho trhu a měny cenu stejného pásma podle nového ceníku:
- zdražení od prvního období začínajícího nejdřív `notice_days` po zveřejnění;
- zlevnění od nejbližšího období bez lhůty.

Systém MUST zákazníkovi poslat e-mail se starou a novou cenou, datem účinnosti a možností zrušit. Na obrazovce Predplatné a platby MUST ukázat „Nová cena od …“. Nové objednávky MUST platit nový ceník od jeho aktivace. Každá použitá změna MUST jít do auditu a doklad v SuperFaktúře MUST nést skutečně účtovanou cenu.

#### Scenario: Zdražení po výpovědní lhůtě

- GIVEN ceník B zveřejněný 1. 3. 2027 s `valid_from` 1. 3. 2027, `notice_days = 30` a sledováním `t5000` 32 € místo 29 €, a předplatné s obnovou vždy 15. dne v měsíci
- WHEN proběhne `billing.schedule_price_list_transfer`
- THEN vznikne změna `price_list` s účinností 15. 4. 2027 a e-mail „29 € → 32 € od 15. 4. 2027“
- AND obnova 15. 3. 2027 se zaúčtuje za 29 € a doklad za 15. 4. 2027 nese 32 €

#### Scenario: Zlevnění bez lhůty

- GIVEN ceník B se sledováním `t500` 8 € místo 9 €, aktivovaný 1. 3. 2027, a předplatné s obnovou 5. dne v měsíci
- WHEN proběhne převod
- THEN změna má účinnost 5. 3. 2027 a zákazník dostane e-mail o zlevnění
- AND výpovědní lhůta se nepoužije

#### Scenario: Zamčená cena zakládajícího zákazníka

- GIVEN tenant s `founder_until` 30. 9. 2028 a zdražení ceníkem od 15. 4. 2027
- WHEN proběhne převod
- THEN účinnost zdražení je první začátek období po 30. 9. 2028
- AND e-mail uvede, do kdy cena zůstává zamčená

#### Scenario: Souběh nového ceníku a vyššího pásma

- GIVEN naplánovaná změna `tier` od 1. 12. a změna `price_list` od 1. 1.
- WHEN skladač sestaví Subscription Schedule
- THEN plán má fázi do 1. 12. se starou cenou, fázi od 1. 12. s novým pásmem podle starého ceníku a fázi od 1. 1. s novým pásmem podle nového ceníku
- AND opakované složení se stejnými změnami nevolá Stripe znovu (stejný otisk fází)

### Requirement: Daňový režim odběratele

Systém MUST před založením Checkoutu nebo platbou uloženou kartou určit daňový režim odběratele:
- `domestic_vat` pro sídlo na Slovensku;
- `reverse_charge` pro jiný stát EU s IČ DPH ověřeným ve VIES (stav ze Stripe `customer.tax_id.updated`);
- `pending_verification`, dokud ověření běží;
- `undetermined` pro ostatní případy, dokud je nepotvrdí účetní.

U `pending_verification` a `undetermined` MUST platbu odmítnout s kódem. Při vystavení dokladu MUST porovnat daň zaplacenou ve Stripe s určeným režimem a při nesouladu MUST NOT doklad odeslat. Systém MUST přijímat jen odběratele s IČO (B2B).

#### Scenario: Česká firma s ověřeným IČ DPH

- GIVEN tenant se sídlem v CZ a IČ DPH CZ12345678, které Stripe ověřil (`verified`)
- WHEN zákazník vytvoří objednávku
- THEN daňový režim je `reverse_charge` a nabídka ukáže `vat_preview` „prenesenie daňovej povinnosti“
- AND doklad v SuperFaktúře bude bez DPH s textem o přenesení daňové povinnosti v znění potvrzeném účetní

#### Scenario: IČ DPH se teprve ověřuje

- GIVEN tenant se sídlem v CZ, jehož IČ DPH má stav `pending`
- WHEN zákazník zavolá `POST /orders/{o}/checkout`
- THEN API vrátí 409 s kódem `billing.tax_id_pending` a Checkout nevznikne
- AND po přijetí `customer.tax_id.updated` se stavem `verified` jde Checkout založit

#### Scenario: Firma z jiného státu bez IČ DPH

- GIVEN tenant se sídlem v CZ bez IČ DPH
- WHEN zákazník vytvoří objednávku
- THEN API vrátí 422 s kódem `billing.tax_treatment_undetermined` a nic se neúčtuje
- AND případ se zapíše do provozního přehledu jako čekající na rozhodnutí účetní

#### Scenario: Nesoulad daně ve Stripe

- GIVEN objednávka s režimem `domestic_vat` a faktura Stripe bez DPH s příznakem přenesení daňové povinnosti
- WHEN běží `billing.issue_invoice`
- THEN doklad má stav `needs_review` s důvodem `tax_mismatch` a do SuperFaktúry nic neodejde
- AND provoz dostane upozornění

### Requirement: Daňové doklady v SuperFaktúře

Systém MUST za každou platbu s nenulovou částkou (analýza, každé zaplacené období sledování) vystavit právě jeden daňový doklad v SuperFaktúře se zapsanou platbou kartou a datem přijetí platby. Doklad MUST obsahovat:
- snímek fakturačních údajů tenanta;
- položky v jazyce tenanta s názvem e-shopu, pásmem a obdobím;
- skutečně účtovanou cenu po slevě, DPH nebo text o přenesení daňové povinnosti;
- měnu.

Během zkušební doby a u neúspěšné platby doklad MUST NOT vzniknout. Slovenským odběratelům MUST systém od 1. 1. 2027 posílat doklad jako e-faktúru přes Peppol a sledovat její doručení. Ostatním MUST posílat PDF e-mailem. Při vrácení peněz MUST vystavit dobropis k původnímu dokladu. Výpadek SuperFaktúry MUST vést k opakování úlohy, ne ke ztrátě dokladu.

#### Scenario: Faktura za analýzu hned po platbě

- GIVEN zaplacená Checkout Session s analýzou 199 € a sledováním ve zkušební době
- WHEN přijde `invoice.paid` s `amount_paid` = 199 € + DPH
- THEN v SuperFaktúře vznikne doklad s položkou „Úvodná analýza · bylinkovo.sk · s 1. mesiacom sledovania“ a zapsanou platbou kartou
- AND PDF je v úložišti pod `tenants/{tenantId}/invoices/{invoiceId}.pdf` a doklad je v seznamu se stavem „Zaplatená“

#### Scenario: Výpadek SuperFaktúry po založení dokladu

- GIVEN úloha `billing.issue_invoice`, která doklad v SuperFaktúře založila, ale spadla před uložením jeho ID
- WHEN úloha běží znovu
- THEN najde doklad v SuperFaktúře podle klíče `source_key` a převezme ho
- AND druhý doklad nevznikne a číselná řada nemá díru ani duplicitu

#### Scenario: E-faktúra slovenskému odběrateli od roku 2027

- GIVEN slovenský tenant a obnova předplatného zaplacená 5. 1. 2027
- WHEN se vystaví doklad
- THEN `einvoice_required = true`, doklad se odešle přes Peppol a `einvoice_status` postupně přejde `queued` → `sent` → `delivered`
- AND když doklad není doručený do 3 dní nebo skončí `failed`, provoz dostane upozornění

#### Scenario: Vrácení peněz a dobropis

- GIVEN doklad za analýzu 69 € a vrácení celé částky provedené provozem ve Stripe
- WHEN přijde `charge.refunded`
- THEN vznikne dobropis s `credit_note_for` = původní doklad a zápornou částkou 69 € + DPH, odeslaný stejnou cestou jako původní doklad
- AND `payments.status` je `refunded` a dvakrát doručená událost druhý dobropis nevystaví

### Requirement: Seznam faktur a export ZIP

Systém MUST na `GET /api/t/{tenantId}/invoices` vracet doklady tenanta seřazené od nejnovějšího s filtrem e-shop a rok. Seznam MUST zahrnovat naplánované platby běžících předplatných jako položky „Naplánovaná“ bez PDF. PDF MUST vydávat jen přes krátkodobý podepsaný odkaz. ZIP MUST obsahovat PDF všech vystavených dokladů podle filtru. Přístup MUST mít jen role owner a admin daného tenanta.

#### Scenario: Filtr podle e-shopu a roku

- GIVEN tenant s 18 doklady za tři e-shopy v letech 2026 a 2027
- WHEN zákazník zavolá `GET /invoices?shopId={bylinkovo.sk}&year=2026`
- THEN odpověď obsahuje jen doklady bylinkovo.sk z roku 2026 a neobsahuje naplánované platby z roku 2027
- AND každý doklad má datum, položku, částku s DPH, stav a odkaz na PDF

#### Scenario: ZIP faktur

- GIVEN filtr „Všetky e-shopy“ a rok 2026 se 6 vystavenými doklady a 1 dokladem ve stavu `needs_review`
- WHEN zákazník zavolá `GET /invoices/zip?year=2026`
- THEN ZIP obsahuje 6 PDF pojmenovaných číslem dokladu
- AND doklad bez PDF v ZIPu není a odpověď hlavičkou `X-EshopGuard-Skipped: 1` uvede, kolik dokladů chybí

#### Scenario: Cizí tenant a nedostatečná role

- GIVEN uživatel s rolí editor v tenantu A a doklad tenanta B
- WHEN zavolá `GET /api/t/{A}/invoices` a `GET /api/t/{A}/invoices/{dokladB}/pdf`
- THEN první volání vrátí 403 s kódem `auth.forbidden_role` (změna 9) a druhé 404
- AND nevznikne žádný podepsaný odkaz

### Requirement: Testovací režim a ochrana platebních klíčů

Systém MUST mít oddělený testovací režim Stripe (testovací klíče, testovací hodiny pro zkušební dobu a obnovy) a SuperFaktúry (sandbox). Při startu MUST ověřit, že klíč odpovídá nastavenému režimu. Mimo produkční prostředí MUST odmítnout spuštění se živým klíčem. Klíče Stripe, tajemství webhooku a klíč SuperFaktúry MUST NOT být v repozitáři, v logu, v odpovědi API ani v chybové zprávě.

#### Scenario: Živý klíč ve vývojovém prostředí

- GIVEN prostředí `Development`, `Billing:Stripe:Mode = test` a klíč s předponou `sk_live_`
- WHEN se spustí API nebo worker
- THEN start skončí chybou validace `BillingOptions` s textem bez hodnoty klíče
- AND žádné volání Stripe neproběhne

#### Scenario: Klíče se nedostanou do logu

- GIVEN celý tok objednávky v testech s logováním na úrovni Debug a chyba SuperFaktúry 401
- WHEN test `SecretsNotLoggedTests` prohledá zachycený log
- THEN nenajde vzory `sk_test_`, `sk_live_`, `rk_test_`, `rk_live_`, `whsec_` ani hodnotu klíče SuperFaktúry
- AND chybová zpráva v `invoices.needs_review_reason` obsahuje jen kód chyby a stavový kód HTTP
