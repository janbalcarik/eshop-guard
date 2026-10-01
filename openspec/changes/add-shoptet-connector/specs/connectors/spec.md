# Delta for Connectors

## ADDED Requirements

### Requirement: Instalace doplňku Shoptet a propojení s e-shopem

Systém MUST přijmout instalační volání Shoptetu jen ze sítě 185.184.254.0/24. Do 5 s od volání MUST vyměnit jednorázový kód za OAuth token, jinak instalaci nezapíše. Instalaci MUST propojit s e-shopem tenanta až po splnění tří podmínek:
- správce e-shopu se ověřil přes stránku nastavení doplňku v administraci Shoptetu;
- přihlášený uživatel EshopGuardu s rolí owner nebo admin propojení potvrdil;
- doména instalace odpovídá doméně e-shopu nebo jeho jazykové verze.

Systém MUST NOT propojit instalaci jen na základě shody domény. Propojení MUST zapsat ověření vlastnictví e-shopu metodou `connector`.

#### Scenario: Úspěšné propojení

- GIVEN tenant „Bylinkovo s. r. o.“ s e-shopem bylinkovo.sk a uživatelkou s rolí owner
- WHEN Shoptet z adresy 185.184.254.12 zavolá instalaci s kódem, správce otevře nastavení doplňku a uživatelka potvrdí propojení v aplikaci do 15 minut
- THEN vznikne `connectors` se stavem `connected`, `external_shop_id` z instalace a šifrovaným tokenem a `shops.source_mode = connector`
- AND `shop_verifications` má záznam `method = connector`, `status = verified` a založí se úlohy `connector.register_webhooks` a `connector.full_sync`

#### Scenario: Instalační volání z cizí adresy nebo pomalá výměna kódu

- GIVEN instalační volání z adresy mimo 185.184.254.0/24, nebo výměna kódu, která trvá déle než 4 s
- WHEN API volání zpracuje
- THEN odpoví 403, případně 503, a do `connector_installations` nic nezapíše
- AND kód instalace se nikde neuloží ani nezaloguje

#### Scenario: Doména instalace nesedí

- GIVEN nepropojená instalace e-shopu `moj-obchod.sk` a uživatel, který ji potvrzuje k e-shopu bylinkovo.sk
- WHEN zavolá `POST /api/t/{t}/connectors/shoptet/claim`
- THEN API vrátí 409 s kódem `connector.domain_mismatch` a nic nepropojí
- AND instalace zůstane nepropojená do vypršení po 7 dnech, kdy se smaže i s tokenem

#### Scenario: Instalace už propojená s jiným účtem

- GIVEN instalace propojená s tenantem A
- WHEN uživatel tenanta B se stejnou doménou zkusí propojení potvrdit
- THEN API vrátí 409 s kódem `connector.installation_already_linked`
- AND odpověď neobsahuje žádný údaj o tenantovi A

### Requirement: Šifrované uložení přístupových údajů konektoru

Systém MUST ukládat OAuth token Shoptetu a tajemství podpisu webhooků jen šifrovaně (ASP.NET Core Data Protection s účelem `EshopGuard.Connectors.Credentials.v1`) a hlavní klíč MUST mít mimo databázi a repozitář. Krátkodobý API token MUST držet jen v paměti procesu a obnovit ho před vypršením. Tokeny, kódy a tajemství MUST NOT být v logu, v odpovědi API, v `connector_events` ani v chybové zprávě.

#### Scenario: Obnova krátkodobého tokenu

- GIVEN konektor s API tokenem platným ještě 4 minuty
- WHEN worker potřebuje zavolat Shoptet API
- THEN `ShoptetTokenProvider` získá nový API token z OAuth tokenu a starý nepoužije
- AND nový token se neuloží do databáze

#### Scenario: Únik do logu

- GIVEN testovací tok propojení, příjmu webhooku a publikace s logováním na úrovni Debug a chybou API 401
- WHEN test `ConnectorSecretsNotLoggedTests` prohledá zachycený log a odpovědi API
- THEN nenajde hodnotu OAuth tokenu, API tokenu, instalačního kódu ani tajemství podpisu
- AND `credentials_enc` v databázi neobsahuje token jako prostý text

#### Scenario: Ztracený klíč Data Protection

- GIVEN konektor, jehož `credentials_enc` nejde dešifrovat, protože klíč chybí
- WHEN úloha konektoru potřebuje token
- THEN úloha skončí bez volání Shoptetu a konektor přejde do stavu `error` s kódem `connector.credentials_unreadable`
- AND zákazník v aplikaci uvidí výzvu k novému propojení

### Requirement: Příjem webhooků Shoptetu

Systém MUST na `POST /api/webhooks/shoptet`:
1. ověřit podpis HMAC-SHA1 syrového těla proti hlavičce `Shoptet-Webhook-Signature` porovnáním v konstantním čase;
2. uložit událost do `shop.connector_events` a ve stejné transakci založit úlohu slučování;
3. odpovědět 200 do 1 s, bez načítání dat ze Shoptetu a bez volání Jevu.

Webhook neznámé instalace nebo s neplatným podpisem MUST odmítnout a nic neuložit. Při nedostupné databázi MUST odpovědět chybou, aby Shoptet doručení zopakoval.

#### Scenario: Platná událost

- GIVEN propojený konektor bylinkovo.sk a událost `product:update` s produktem 2429 podepsaná jeho tajemstvím
- WHEN Shoptet událost doručí
- THEN API odpoví 200 do 1 s a v `connector_events` je řádek se stavem `received`
- AND existuje úloha `connector.coalesce` pro aktuální dvouminutové okno konektoru

#### Scenario: Neplatný podpis

- GIVEN událost s hlavičkou `Shoptet-Webhook-Signature`, která neodpovídá tělu
- WHEN Shoptet nebo útočník událost doručí
- THEN API odpoví 401, nic neuloží a nezaloží úlohu
- AND log obsahuje jen kód `connector.webhook_signature_invalid` a ID konektoru, ne tělo

#### Scenario: Výpadek databáze

- GIVEN nedostupná databáze
- WHEN přijde událost
- THEN API odpoví 503
- AND změnu zachytí opakované doručení Shoptetu nebo nejpozději hodinové dorovnání

### Requirement: Odstranění duplicit a slučování událostí konektoru

Systém MUST odstraňovat duplicitní události podle otisku (`eshopId`, `event`, `eventInstance`, `eventCreated`), protože Shoptet ID události neposílá. Události jednoho konektoru za dvouminutové okno MUST sloučit do jedné úlohy a ID produktů rozdělit do dávek po nejvýš 100. Před kontrolou MUST vždy načíst aktuální stav produktu přes API. Když se text produktu nezměnil, MUST NOT spustit kontrolu ani volat Jev.

#### Scenario: Stejná událost doručená třikrát

- GIVEN událost `product:update` produktu 2429, kterou Shoptet doručí třikrát po 15 minutách
- WHEN API zpracuje všechna tři doručení
- THEN v `connector_events` je jeden řádek se stejným `dedupe_key`
- AND produkt 2429 se načte a zkontroluje nejvýš jednou

#### Scenario: Hromadný import 300 produktů

- GIVEN import, který během jedné minuty pošle události pro 300 produktů
- WHEN skončí slučovací okno
- THEN vzniknou 3 úlohy `connector.fetch_products` po 100 ID, ne 300 úloh
- AND všechny události jsou ve stavu `coalesced` s odkazem na úlohu

#### Scenario: Změna jen ceny nebo skladu

- GIVEN událost `product:update`, po které jsou název, popisy a meta produktu stejné jako v aktuální `page_versions`
- WHEN úloha produkt načte a porovná otisk
- THEN událost skončí s výsledkem `no_text_change`, nevznikne nová verze stránky ani běh
- AND neproběhne žádné volání Jevu

### Requirement: Dorovnání změn dotazem „změněno od“

Systém MUST každou hodinu a v noci načíst změněné produkty přes `/products/changes` od uloženého kurzoru s překryvem 5 minut a zpracovat je stejnou cestou jako webhooky. Kurzor MUST posunout až po zpracování. Když je kurzor starší než 29 dní nebo chybí, MUST provést úplnou synchronizaci katalogu po dávkách. Kategorie, stránky a články MUST každou noc porovnat podle otisků textů, protože pro ně Shoptet webhook ani „změněno od“ nemá.

#### Scenario: Ztracený webhook

- GIVEN změna popisu produktu 3100 v 9:10, jejíž webhook se nedoručil
- WHEN v 10:00 proběhne `connector.reconcile_changes`
- THEN produkt 3100 se načte a zkontroluje během `connector_check` s parametrem `change_source = reconcile`
- AND `sync_cursor` se posune na čas poslední zpracované změny

#### Scenario: Doplněk pozastavený déle než 30 dní

- GIVEN konektor s `sync_cursor` starším 35 dní po obnovení doplňku
- WHEN proběhne dorovnání
- THEN místo `/products/changes` se spustí `connector.full_sync` po dávkách 100 produktů s pokračováním na konci fronty
- AND aplikace ukáže, že se e-shop synchronizuje znovu celý, a změny za výpadek nejsou tiše vynechané

#### Scenario: Změna stránky obchodních podmínek

- GIVEN stránka „Obchodné podmienky“ změněná v administraci Shoptetu
- WHEN v noci proběhne `connector.sync_content`
- THEN rozdílný otisk textu vytvoří novou verzi stránky a běh `connector_check`
- AND nezměněné kategorie, stránky a články se nekontrolují

### Requirement: Hlídač odběrů webhooků a stavu doplňku

Systém MUST každou hodinu:
- porovnat registrované webhooky instalace s očekávanými a chybějící nebo neaktivní znovu zaregistrovat;
- přečíst log notifikací za posledních 7 dní a při neúspěšných doručeních spustit dorovnání hned;
- zapsat stav do `connectors.health`.

Když API odmítá token nebo je doplněk pozastavený, MUST konektor přepnout do stavu `error` nebo `paused` a ukázat to v aplikaci. Aplikace pak MUST NOT tvrdit, že změny kontroluje do pár minut.

#### Scenario: Shoptet odregistroval webhook

- GIVEN instalace, které Shoptet po změně práv odregistroval webhook `product:update`
- WHEN proběhne `connector.verify_webhooks`
- THEN webhook se zaregistruje znovu a `connector_webhooks` má nový `external_id`
- AND `health.missing` obsahuje záznam o obnovení s časem

#### Scenario: Neúspěšná doručení v logu notifikací

- GIVEN log notifikací se 4 neúspěšnými doručeními od poslední kontroly
- WHEN hlídač log přečte
- THEN hned se založí `connector.reconcile_changes`
- AND `health.failed_deliveries` je 4

#### Scenario: Pozastavený doplněk

- GIVEN Shoptet odmítá API token, protože doplněk je pozastavený
- WHEN hlídač zavolá API
- THEN konektor má stav `paused` a publikace ve stavu `queued` čekají
- AND obrazovka sledování dostane stav `connector_paused` místo věty „do niekoľkých minút“

### Requirement: Čtení textů ze Shoptetu po jazycích

Systém MUST číst z Shoptetu texty:
- produktů: název, doplňkový název, krátký a dlouhý popis, meta popis, příznaky, parametry;
- kategorií;
- stránek (z pole `content`);
- článků.

Číst MUST pro každý jazyk, který se kontroluje podle aktivních míst prodeje, s parametrem `language`. Texty MUST uložit jako stránky e-shopu se zdrojem `connector`, s číslem v Shoptetu a příznakem skrytí. Počet zveřejněných produktů po jazycích MUST zapsat do `shop_languages.product_count`. Varianty se do něj nepočítají. Text, který se nepodařilo načíst, MUST být uvedený jako nezkontrolovaný.

#### Scenario: Dvě jazykové verze

- GIVEN e-shop s místy prodeje SK a CZ a jazyky `sk` a `cs` v Shoptetu
- WHEN úplná synchronizace načte produkt 2429
- THEN vzniknou dvě stránky (`language = sk` a `cs`) s `source = connector` a `external_id = 2429`
- AND na obrazovce Review je u nálezu „Text zo Shoptetu, produkt č. 2429“

#### Scenario: Počet produktů pro pásmo ceny

- GIVEN katalog s 5 834 viditelnými produkty, 120 skrytými a 900 variantami
- WHEN úplná synchronizace skončí
- THEN `shop_languages.product_count` je 5 834 pro každý jazyk se zveřejněnými produkty a `source = connector`
- AND další výpočet nabídky a denní kontrola pásma předplatného (změna 12) použijí tento počet

#### Scenario: Produkt nejde načíst

- GIVEN produkt, u kterého API po 5 pokusech vrací 500
- WHEN úloha dávky skončí
- THEN ostatní produkty dávky se zkontrolují a tento se v běhu uvede jako „nenačítané zo Shoptetu“ s kódem chyby
- AND jeho předchozí nálezy se neoznačí jako vyřešené

### Requirement: Limity volání Shoptet API

Systém MUST na jednu instalaci držet nejvýš 3 souběžná spojení a odtok nejvýš 10 požadavků za sekundu přes globální kbelík `connector:shoptet:{eshopId}` (kapacita 200) v `ops.rate_limit_buckets`, společný pro všechny workery a oddělený od limitu Jevu. Na odpověď 429 MUST reagovat čekáním podle `Retry-After` a na 423 opakováním po 6 s. Dlouhé synchronizace MUST běžet po dávkách tak, aby publikace a kontroly s vyšší prioritou nečekaly na jejich konec.

#### Scenario: Dva workery a jeden e-shop

- GIVEN dva workery a úlohy úplné synchronizace a publikace pro stejný konektor
- WHEN běží současně
- THEN nikdy neběží dvě úlohy stejného konektoru najednou a souběžných požadavků je nejvýš 3
- AND publikace (P1) proběhne před dalším pokračováním synchronizace (P2)

#### Scenario: Odpověď 429

- GIVEN Shoptet vrátí 429 s `Retry-After: 3`
- WHEN klient požadavek opakuje
- THEN počká aspoň 3 s a zkusí to nejvýš 5×
- AND po vyčerpání pokusů se úloha vrátí do fronty s rostoucím odstupem a nic se neztratí

#### Scenario: Zámek po stejném zápisu

- GIVEN dva stejné PATCH požadavky po sobě a Shoptet vrátí na druhý 423
- WHEN publikace zpracuje odpověď
- THEN po 6 s ověří aktuální hodnotu pole
- AND když už obsahuje nový text, publikace skončí `published` bez dalšího zápisu

### Requirement: Publikace opravy do Shoptetu

Systém MUST zapsat schválenou opravu do Shoptetu jen tehdy, když jsou splněné všechny tyto podmínky:
- konektor má stav `connected` a právo zápisu;
- vlastnictví e-shopu je ověřené;
- uživatel má roli editor nebo vyšší;
- pole lze zapsat.

Před zápisem MUST:
1. znovu načíst pole a porovnat jeho text s textem verze, ze které vznikl návrh; při rozdílu zapsat konflikt, nic nezapsat a spustit novou kontrolu;
2. uložit původní znění pole pro vrácení.

Systém MUST změnit jen dotčený úsek dotčeného pole v jednom jazyce a zachovat okolní text a značky. Kde to bezpečně nejde, MUST nabídnout „Kopírovať text“. Publikace MUST být idempotentní (`idempotency_key`, kontrola aktuální hodnoty) a po zápisu MUST ověřit výsledek a spustit novou kontrolu.

#### Scenario: Úspěšná publikace

- GIVEN přijatá změna 1 produktu 2429 („v ekologickom sete“ → „v sete so zubnou kefkou s bambusovou rukoväťou“) a text v Shoptetu stejný jako ve verzi nálezu
- WHEN editor klikne na „Publikovať do e-shopu (1 zmena)“
- THEN publikace uloží `old_value`, zapíše PATCH jen pole `description` v jazyce `sk` a po zápisu ověří nový text
- AND `publications.status = published`, `fix_proposals.status = published` a běh `recheck` produktu nález uzavře, protože nový text pravidlo nespouští

#### Scenario: Text se mezitím změnil v administraci

- GIVEN přijatá oprava produktu 2429 a obchodník, který mezitím v Shoptetu upravil jiný odstavec téhož popisu
- WHEN proběhne `connector.publish`
- THEN publikace má stav `conflict`, návrh `conflict` a do Shoptetu se nic nezapíše
- AND spustí se nová kontrola produktu a uživatel uvidí „Text sa v e-shope medzitým zmenil“

#### Scenario: Opakování po pádu workeru po zápisu

- GIVEN worker, který zapsal PATCH a spadl před uložením stavu
- WHEN se úloha `connector.publish` spustí znovu
- THEN zjistí, že pole už obsahuje očekávaný nový text, a nastaví `published` bez druhého zápisu
- AND druhé kliknutí na „Publikovať“ vrátí existující publikaci se stejným `idempotency_key`

#### Scenario: Oprava by porušila odkaz v textu

- GIVEN návrh, jehož změněný úsek zasahuje do odkazu `<a href>` v popisu produktu
- WHEN `FieldPatcher` hledá místo pro opravu
- THEN publikace skončí `failed` s kódem `connector.patch_unsafe` a do Shoptetu se nic nezapíše
- AND uživatel dostane nabídku „Kopírovať text“ s celým navrženým odstavcem

### Requirement: Vrácení publikované opravy

Systém MUST umožnit vrátit publikovanou opravu do uloženého původního znění. Původní znění MUST zapsat jen tehdy, když aktuální text pole je stále ten, který publikace zapsala. Jinak MUST zapsat konflikt a nic nezapsat. Vrácení MUST jít do auditu a opakované vrácení MUST být bez účinku.

#### Scenario: Úspěšné vrácení

- GIVEN publikovaná oprava produktu 2429 a text v Shoptetu beze změny od publikace
- WHEN editor klikne na „Vrátiť zmenu“
- THEN do pole `description` v jazyce `sk` se zapíše `old_value` a po ověření má publikace stav `rolled_back`
- AND návrh opravy se vrátí do stavu `accepted` a audit obsahuje `publication.rolled_back` s uživatelem a časem

#### Scenario: Text po publikaci někdo upravil

- GIVEN publikovaná oprava a obchodník, který potom text v Shoptetu znovu upravil
- WHEN editor zkusí změnu vrátit
- THEN vrácení skončí `conflict` a do Shoptetu se nic nezapíše
- AND uživatel dostane původní znění ke zkopírování

#### Scenario: Dvojí kliknutí na vrácení

- GIVEN publikace už ve stavu `rolled_back`
- WHEN editor zavolá vrácení znovu
- THEN API vrátí 200 se stavem `rolled_back`
- AND do Shoptetu se nic nezapíše

### Requirement: Kontrola skrytého produktu před zveřejněním

Systém MUST produkt, který přišel ze Shoptetu jako skrytý (`visibility=hidden`), zkontrolovat přednostně (P0). Běh MUST mít `change_source = save_hidden`, aby šel výsledek ukázat jako „Kontrola pred zverejnením, pri uložení“ (obrazovka Sledovanie, změna 16). Platí to jen u e-shopu, který má zapnuté „Kontrola pri uložení, aj skrytých produktov“ (`shops.check_hidden_on_save`). Přednostně MUST jít nejvýš 20 skrytých produktů z jedné sloučené dávky, zbytek s prioritou P1. Skryté produkty se MUST NOT počítat do pásma ceny.

#### Scenario: Skrytý produkt s porušením

- GIVEN e-shop se zapnutou kontrolou skrytých produktů a obchodník, který uloží skrytý produkt „Sviečka Levanduľa“ s textem „100 % ekologická“
- WHEN přijde webhook a skončí slučovací okno
- THEN běh `connector_check` má prioritu P0 a `change_source = save_hidden` a nález se objeví v aplikaci dřív, než obchodník produkt zveřejní
- AND stránka produktu má `is_hidden_in_shop = true` a změna 16 z parametrů běhu zapíše `page_changes` (`hidden_saved`) a upozornění

#### Scenario: Kontrola skrytých vypnutá

- GIVEN e-shop s `check_hidden_on_save = false`
- WHEN obchodník uloží skrytý produkt
- THEN skrytý produkt se jen uloží jako nová verze stránky a zkontroluje se až po zveřejnění jako běžná změna (P1)
- AND pro skrytý produkt nevznikne běh P0

#### Scenario: Import 200 skrytých produktů

- GIVEN hromadný import 200 skrytých produktů v jednom okně
- WHEN slučování založí kontroly
- THEN prvních 20 produktů má prioritu P0 a zbylých 180 P1
- AND vyhrazený podíl limitu Jevu pro P0 zůstane dostupný ostatním tenantům

### Requirement: Odpojení konektoru Shoptet

Systém MUST umožnit konektor odpojit v aplikaci (role owner nebo admin) a MUST zpracovat odinstalaci i pozastavení doplňku na straně Shoptetu. Odpojení a odinstalace MUST:
- smazat OAuth token a tajemství podpisu;
- zrušit webhooky, když to jde;
- označit čekající publikace jako neprovedené;
- přepnout e-shop na procházení webu;
- upozornit zákazníka.

Nálezy, opravy, publikace a jejich původní znění MUST zůstat. Pozastavení MUST tokeny ponechat a po obnovení dorovnat změny za dobu výpadku.

#### Scenario: Odpojení v aplikaci

- GIVEN propojený konektor a 2 publikace ve stavu `queued`
- WHEN owner zavolá `POST …/connector/disconnect`
- THEN `connectors.status = revoked`, `credentials_enc` a `webhook_secret_enc` jsou NULL a obě publikace mají `failed` s kódem `connector.disconnected`
- AND `shops.source_mode = web` a zákazník dostane pokyn odinstalovat doplněk v administraci Shoptetu

#### Scenario: Odinstalace v Shoptetu

- GIVEN propojený konektor
- WHEN přijde událost odinstalace doplňku, nebo API trvale vrací 401
- THEN konektor se odpojí stejně jako v aplikaci, bez volání Shoptet API
- AND další webhooky této instalace se potvrdí 200 a nic se z nich neuloží

#### Scenario: Obnovení pozastaveného doplňku

- GIVEN konektor ve stavu `paused` 3 dny
- WHEN Shoptet doplněk obnoví a hlídač zjistí, že API odpovídá
- THEN konektor má stav `connected` a hned se spustí `connector.reconcile_changes` od `sync_cursor`
- AND čekající publikace pokračují s kontrolou konfliktu proti aktuálnímu textu
