# Tasks

## 0. Obchodní předpoklady a ověření dokumentace

- [ ] 0.1 Podat u Shoptetu žádost o partnerství a doplněk:
  - popis doplňku, práva ke čtení a zápisu produktů, kategorií, stránek a článků a k webhookům, adresa instalace a stránky nastavení;
  - zjistit poplatky a provize.

  Odpověď do 4 týdnů. Hotovo: schválený doplněk v testovacím režimu, `ClientId` a `ClientSecret` v user-secrets, nikdy v repozitáři.
- [ ] 0.2 Získat testovací e-shop Shoptet:
  - zapnout jazyky `sk` a `cs`;
  - založit 20 testových produktů včetně skrytých a variant, 3 stránky a 2 články.

  Bez reálných dat zákazníků.
- [ ] 0.3 Ověřit v dokumentaci Shoptetu (OpenAPI bundle a stránky z rešerše) body označené „ověřit“ v `design.md`:
  - instalační volání a výměna kódu, identita na stránce nastavení doplňku;
  - získání API tokenu a název hlavičky;
  - tvar zprávy webhooku a kódování podpisu, vydání tajemství podpisu;
  - názvy událostí produktů, hromadných událostí a událostí doplňku;
  - `/products/changes`, filtry kategorií, stránek a článků;
  - hodnoty `visibility`, zápis stránky do `description`.

  Výsledky zapsat do `design.md`, nejasnosti do proposal, K rozhodnutí.
- [ ] 0.4 S uživatelem rozhodnout:
  - propojení instalace (K rozhodnutí 2);
  - jeden nebo víc konektorů na e-shop (K rozhodnutí 11);
  - limit P0 pro skryté produkty (K rozhodnutí 9);
  - cíl 5 minut (K rozhodnutí 4).

## 1. Projekt, konfigurace a abstrakce

- [ ] 1.1 Doplnit `src/EshopGuard.Connectors/EshopGuard.Connectors.csproj` o reference na `EshopGuard.Core`, `EshopGuard.Data` a `EshopGuard.Jobs` a o balíček `Microsoft.AspNetCore.DataProtection`. Test: `dotnet build`.
- [ ] 1.2 `ConnectorsOptions` (sekce `Connectors:Shoptet`, okna a limity podle `design.md`) s maskou v `ToString()` a validací povinných hodnot při startu. Test `ConnectorsOptionsTests`.
- [ ] 1.3 Abstrakce v `Abstractions/`:
  - `ConnectorText`, `IConnectorTextSource`, `IConnectorChangeFeed`, `IConnectorWebhookVerifier`, `IConnectorPublisher`, `IConnectorWebhookRegistrar`;
  - `ConnectorErrorCodes` s kódy použitými ve specifikaci.

  Test: kódy jsou jedinečné a mají texty sk a cs ve zprávách frontendu (test úplnosti ze změny 13, do té doby seznam v testu).
- [ ] 1.4 `ServiceCollectionExtensions.AddEshopGuardConnectors`:
  - typovaný `ShoptetApiClient` s `RedactLoggedHeaders` a timeoutem 30 s;
  - napojení v `EshopGuard.Api/Program.cs` a `EshopGuard.Worker/Program.cs`.

  Test startu přes `WebApplicationFactory`.
- [ ] 1.5 `EshopGuard.Core/Extract/HtmlText.cs`: zpřístupnit rozdělení fragmentu HTML na bloky s mapou na uzly DOM pro `EshopGuard.Connectors` (`InternalsVisibleTo` nebo veřejná metoda). Test: dnešních 192 testů Core projde beze změny.

## 2. Datový model

- [ ] 2.1 Porovnat tabulky `shop.connectors`, `connector_webhooks`, `connector_events` a `fixes.publications` ze změny 3 se seznamem v `design.md`, File Changes. Rozdíly zapsat do komentáře migrace.
- [ ] 2.2 Migrace `AddShoptetConnector`:
  - sloupce `connectors` (`webhook_secret_enc`, `shop_language`, `paused_reason`, `last_full_sync_at`) a jejich jedinečnosti;
  - `connector_events` (`coalesce_window_start`, `job_id`, `result`, U v měsíčních částech);
  - `connector_webhooks`, `publications`.

  Test: migrace jako `eshopguard_owner` na prázdné i naplněné DB.
- [ ] 2.3 Nové tabulky:
  - `shop.connector_link_intents` s RLS;
  - globální `shop.connector_installations` a `shop.connector_claim_tokens`: práva jen pro systémovou cestu API, `eshopguard_app` k nim nemá přímý přístup, přístup přes funkce `SECURITY DEFINER` s auditem.

  Test v `EshopGuard.Data.Tests/RlsIsolationTests`: tenant nevidí cizí `connector_link_intents`, `eshopguard_app` nečte `connector_installations` přímo.
- [ ] 2.4 Úloha údržby smaže nepropojené instalace po `expires_at` (7 dní) včetně tokenu a použité nebo vypršelé odkazy po 24 h. Test s pevným časem.

## 3. Tokeny a šifrování

- [ ] 3.1 `ConnectorCredentialsProtector` (`IDataProtector`, účel `EshopGuard.Connectors.Credentials.v1`, `credentials_key_id`):
  - adresář klíčů Data Protection z konfigurace mimo repozitář;
  - nečitelná data → `connector.credentials_unreadable`.

  Testy: šifrování a dešifrování, cizí klíč.
- [ ] 3.2 `ShoptetTokenProvider`:
  - API token z OAuth tokenu, `IMemoryCache` do 5 min před koncem platnosti;
  - 401 nebo 403 → stav konektoru `error`.

  Testy s `FakeShoptetHandler` (obnova, chyba).
- [ ] 3.3 Test `ConnectorSecretsNotLoggedTests`: tok propojení, webhooku a publikace s logem na úrovni Debug a chybou 401. V logu, odpovědích ani `connector_events` nesmí být OAuth token, API token, instalační kód ani tajemství podpisu.

## 4. Instalace a propojení

- [ ] 4.1 Endpoint `POST /api/t/{t}/shops/{s}/connectors/shoptet/start` (owner, admin): `connector_link_intents` na 60 min, odpověď `{addonUrl}`. Test oprávnění.
- [ ] 4.2 `ShoptetInstallEndpoints` a `ShoptetInstallHandler`:
  - kontrola zdrojové sítě 185.184.254.0/24 přes důvěryhodnou proxy (`ForwardedHeadersOptions.KnownProxies` = Caddy);
  - výměna kódu s limitem 4 s;
  - zjištění `eshopId` a adresy;
  - zápis do `connector_installations`.

  Testy `ShoptetInstallEndpointTests`: cizí adresa 403, pomalá výměna 503, úspěch.
- [ ] 4.3 `ShoptetSettingsPageHandler` (`/api/connectors/shoptet/settings`):
  - ověření identity e-shopu a správce mechanismem z 0.3;
  - vydání jednorázového odkazu (15 min, otisk SHA-256);
  - přesměrování do aplikace.

  Test: odkaz se použije jen jednou a vypršelý odkaz vrátí `connector.claim_expired`.
- [ ] 4.4 `ShoptetClaimService.ClaimAsync` a endpoint `POST /api/t/{t}/connectors/shoptet/claim` (owner, admin):
  - kontrola domény (`shops.domain` nebo `shop_languages.base_url`, bez `www`);
  - kontrola jedinečnosti instalace;
  - přesun tokenu do `connectors`;
  - `shop_verifications` (`connector`, `verified`), `shops.source_mode` a `platform`;
  - úlohy `register_webhooks` a `full_sync`.

  Testy `ShoptetClaimServiceTests` podle 4 scénářů požadavku „Instalace doplňku Shoptet a propojení s e-shopem“.
- [ ] 4.5 Endpoint `GET /api/t/{t}/shops/{s}/connector`: stav, `health`, poslední webhook a dorovnání, přístup (`read`/`read_write`), kódy místo vět. Test kontraktu.

## 5. Příjem webhooků

- [ ] 5.1 `ShoptetWebhookVerifier`: HMAC-SHA1 nad syrovým tělem, `CryptographicOperations.FixedTimeEquals`, kódování podle 0.3. Testy se vzorovým podpisem z dokumentace.
- [ ] 5.2 `ShoptetWebhookEndpoint`:
  - strop těla 1 MB;
  - vyhledání konektoru, cache tajemství 10 min;
  - ověření podpisu, `dedupe_key`;
  - `INSERT … ON CONFLICT DO NOTHING` a úloha `connector.coalesce` v jedné transakci;
  - 200;
  - pravidla 401, 503 a 200 podle tabulky v `design.md`.

  Testy `ShoptetWebhookEndpointTests`.
- [ ] 5.3 Ukládání obsahu zprávy: při `sendPayload: full` uložit jen typ události a ID, ne detail produktu. Test: `connector_events.payload` neobsahuje popis produktu.
- [ ] 5.4 Měření doby odpovědi: test 200 událostí po sobě proti lokální DB, p99 < 300 ms a maximum < 1 s. Výsledek zapsat do `design.md`.

## 6. Slučování a načtení změn

- [ ] 6.1 `ShoptetEventCoalescer` (úloha `connector.coalesce`):
  - výběr událostí okna `FOR UPDATE SKIP LOCKED`;
  - dávky po 100 ID;
  - `eshop:design` předat změně 16 (rozhraní `IWebCrawlRequest`, do té doby prázdná implementace);
  - události doplňku hned;
  - stav `coalesced`.

  Testy: 3 doručení téže události → 1 řádek, import 300 produktů → 3 úlohy.
- [ ] 6.2 `ShoptetTextMapper`:
  - pole produktu, kategorie, stránky (`content`) a článku na `ConnectorText` a `TextInput` (`Kind`, `Id`, `Url`, `Category`);
  - HTML se převede na bloky stejně jako při procházení webu.

  Testy s `Fixtures/shoptet/*.json`.
- [ ] 6.3 `ShoptetChangeProcessor` (úloha `connector.fetch_products`):
  - načtení po jazycích, otisk textu, `no_text_change`;
  - zápis `pages` a `page_versions` (`source = connector`, `external_id`, `is_hidden_in_shop`);
  - smazaný produkt `gone`;
  - běh `connector_check` přes `IConnectorChangeSink` a `RunService` (změna 8).

  Testy: změna jen ceny bez běhu, změna popisu s během, chyba načtení s „nenačítané zo Shoptetu“.
- [ ] 6.4 `IConnectorChangeSink` a výchozí `RunConnectorChangeSink`:
  - běh `connector_check` s parametry `change_source` (webhook, reconcile, save_hidden) a `change_kind` po stránce (new, text_changed, removed, hidden_saved);
  - výsledek běhu do `connector_events.result`;
  - zápis `page_changes` a upozornění nechat změně 16 (`ConnectorChangeProcessor`).

  Test: parametry běhu pokrývají všechny řádky tabulky „Posledné zmeny“ z návrhu Monitoring, které pocházejí z konektoru.

## 7. Dorovnání a úplná synchronizace

- [ ] 7.1 `ShoptetChangeReconciler` (úloha `connector.reconcile_changes`):
  - `/products/changes` od `sync_cursor − 5 min` po stránkách;
  - stejná cesta jako webhook;
  - posun kurzoru až po zpracování;
  - hodinový plán s `dedupe_key = reconcile:{connectorId}:{datum}T{hodina}`;
  - parametr `nightly = true` (spustí i `connector.sync_content`) dostane noční běh změny 16, do její implementace hodinový běh v hodině `shops.monitor_slot_minute`.

  Test „Ztracený webhook“.
- [ ] 7.2 `ShoptetFullSync` (úloha `connector.full_sync`):
  - po dávkách 100 s pokračováním na konci fronty;
  - počty zveřejněných produktů po jazycích do `shop_languages.product_count` (jen `visibility = visible`, bez variant);
  - počty se projeví při dalším výpočtu nabídky (`POST …/quote`, změny 10 a 12) a v denní úloze `billing.evaluate_tiers` (změna 12).

  Testy: kurzor 35 dní → úplná synchronizace, počty 5 834 / 120 / 900.
- [ ] 7.3 `ShoptetContentSync` (úloha `connector.sync_content` z nočního dorovnání): kategorie, stránky a články s porovnáním otisků. Test: změněná stránka „Obchodné podmienky“ → běh, nezměněné ne.
- [ ] 7.4 Pojistka: události `received` starší 10 min bez úlohy předá hodinové dorovnání slučování. Test s pevným časem.

## 8. Hlídač odběrů a stav doplňku

- [ ] 8.1 `ShoptetWebhookRegistrar`:
  - registrace očekávaných webhooků po propojení (úloha `connector.register_webhooks`);
  - jedna URL na událost a instalaci;
  - `connector_webhooks` s `external_id`.

  Test s `FakeShoptetHandler`.
- [ ] 8.2 `ShoptetHealthChecker` (úloha `connector.verify_webhooks`, každou hodinu):
  - porovnání a obnova odběrů;
  - log notifikací za 7 dní → dorovnání hned;
  - `connectors.health`;
  - 401 a 403 → `error`, pozastavení → `paused`.

  Testy podle 3 scénářů požadavku „Hlídač odběrů webhooků a stavu doplňku“.
- [ ] 8.3 Stav pro obrazovku sledování: `GET …/connector` vrátí `connector_paused` nebo `connector_error` a změna 16 podle něj nesmí ukázat „do niekoľkých minút“. Test kontraktu.

## 9. Limity

- [ ] 9.1 `ShoptetRateLimiter`:
  - kbelík `connector:shoptet:{eshopId}` (200, 10/s) v `ops.rate_limit_buckets` s rezervací `UPDATE … RETURNING`;
  - `SemaphoreSlim(3)` v úloze;
  - `concurrency_key = connector:{connectorId}` u všech úloh konektoru.

  Test v `EshopGuard.Jobs.Tests`: 2 workery a 1 konektor nikdy nemají 2 úlohy najednou ani víc než 3 požadavky.
- [ ] 9.2 Opakování:
  - 429 podle `Retry-After`, nejvýš 5×;
  - 423 po 6 s;
  - 5xx přes opakování úlohy.

  Testy `ShoptetRateLimiterTests`.
- [ ] 9.3 Test priority: publikace P1 předběhne pokračování úplné synchronizace P2 stejného konektoru.

## 10. Publikace a vrácení

- [ ] 10.1 `TextNormalizer`: normalizace mezer a entit a otisk SHA-256 textu bloků přes `HtmlText`. Testy: stejný text v jiném HTML formátování dá stejný otisk, změna slova jiný.
- [ ] 10.2 `FieldPatcher.Apply`:
  - nejmenší změněný úsek;
  - nahrazení v jednom textovém uzlu;
  - přechod přes formátovací prvky;
  - `patch_unsafe` u odkazů, jiných prvků a nejednoznačného výskytu.

  Testy `FieldPatcherTests`: věty ze scénářů Review (změny 1, 2, 3 a 5), odkaz uvnitř úseku, dvojí výskyt věty, zachování okolních značek.
- [ ] 10.3 `PublicationService` a endpointy `POST/GET …/publications`:
  - seskupení návrhů podle pole a jazyka;
  - `idempotency_key`;
  - předpoklady (`not_connected`, `read_only`, `ownership_not_verified`, `field_not_writable`);
  - role editor a výš.

  Testy `PublicationEndpointsTests` včetně dvojího kliknutí.
- [ ] 10.4 `ShoptetPublisher` a úloha `connector.publish`:
  - GET pole;
  - kontrola, zda tam už je nový text;
  - kontrola konfliktu proti `source_text_hash`;
  - `old_value`, `FieldPatcher`, PATCH jednoho pole s `language` (stránka do `description`);
  - ověření po zápisu;
  - `fix_proposals`, nová verze stránky, běh `recheck` s `change_kind = fix_published`, audit.

  Testy `PublicationServiceTests` podle 4 scénářů požadavku „Publikace opravy do Shoptetu“.
- [ ] 10.5 `RollbackService`, úloha `connector.rollback` a endpoint `POST …/publications/{id}/rollback`: kontrola otisku publikovaného textu, zápis `old_value`, ověření, audit, opakované vrácení bez účinku. Testy podle 3 scénářů požadavku „Vrácení publikované opravy“.
- [ ] 10.6 „Kopírovať text“ jako výsledek pro `read_only`, `field_not_writable`, `patch_unsafe` a konflikt při vrácení: odpověď obsahuje celý navržený odstavec, případně původní znění. Test kontraktu se změnou 11.

## 11. Skryté produkty a odpojení

- [ ] 11.1 Kontrola skrytých produktů:
  - P0 jen pro `visibility = hidden` a `shops.check_hidden_on_save`, nejvýš 20 na sloučenou dávku, zbytek P1;
  - `change_source = save_hidden` pro zápis `page_changes` a upozornění ve změně 16;
  - skryté produkty mimo pásmo ceny.

  Testy podle 3 scénářů požadavku „Kontrola skrytého produktu před zveřejněním“.
- [ ] 11.2 `ShoptetDisconnectService` a úloha `connector.disconnect`:
  - zrušení webhooků, když to jde;
  - smazání tokenů a tajemství, vyhození API tokenu z paměti;
  - `revoked`, publikace `queued` → `failed`;
  - `source_mode = web`, `check_hidden_on_save = false`;
  - upozornění a audit;
  - endpoint `POST …/connector/disconnect` (owner, admin).

  Test „Odpojení v aplikaci“.
- [ ] 11.3 Odinstalace a pozastavení ze strany Shoptetu:
  - událost doplňku nebo trvalé 401 → odpojení bez volání API;
  - pozastavení → `paused`;
  - obnovení → `connected` a dorovnání od kurzoru (nad 29 dní úplná synchronizace).

  Testy podle scénářů „Odinstalace v Shoptetu“ a „Obnovení pozastaveného doplňku“.

## 12. Ověření (testovací e-shop Shoptet)

- [ ] 12.1 Automatické kontroly: `dotnet test` (všechny projekty včetně `EshopGuard.Connectors.Tests`) a `openspec validate add-shoptet-connector` projdou.
- [ ] 12.2 Připravit prostředí:
  - lokální API za veřejným HTTPS tunelem pro instalaci a webhooky;
  - doplněk v testovacím režimu, testovací e-shop z 0.2;
  - klíče jen v user-secrets.
- [ ] 12.3 Odhad ceny a souhlas uživatele před během s Jevem a OpenAI:
  - kontrola 20 testových produktů a 10 změn: odhad z `JevCallEstimate` a `ProfileCostUsd`;
  - bez souhlasu běží `MockJevClient` a `MockRewriteClient` a měří se jen čas do nálezu bez Jevu.
- [ ] 12.4 Propojení: instalace, stránka nastavení, potvrzení v aplikaci, úplná synchronizace. Kontrola:
  - `shop_languages.product_count` sedí s administrací;
  - ověření vlastnictví je zapsané;
  - webhooky jsou registrované.
- [ ] 12.5 Kontrola po uložení: 10× změnit popis produktu v administraci a změřit čas od uložení do nálezu v aplikaci. Medián a maximum zapsat do protokolu ověření. Cíl do 5 minut.
- [ ] 12.6 Skrytý produkt: uložit skrytý produkt s „100 % ekologická“ a ověřit nález „Kontrola pred zverejnením“ před zveřejněním.
- [ ] 12.7 Publikace a vrácení:
  - publikovat přijatou opravu a ověřit v administraci, že se změnil jen dotčený úsek a jazyk;
  - vrátit změnu a ověřit původní znění;
  - upravit text v administraci a publikovat jinou opravu → konflikt bez zápisu.
- [ ] 12.8 Chybové cesty:
  - zastavit API na 20 minut během změny produktu → hodinové dorovnání změnu zachytí;
  - smazat odběr webhooku v Shoptetu → hlídač ho do hodiny obnoví;
  - pozastavit a obnovit doplněk;
  - odinstalovat doplněk → tokeny smazané, `source_mode = web`, upozornění.

  Výsledky zapsat do protokolu ověření v tomto souboru (datum, bez tokenů a textů zákazníků).
