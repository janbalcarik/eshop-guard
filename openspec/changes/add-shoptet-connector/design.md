## Technical Approach

### Projekty a vrstvy

- **`src/EshopGuard.Connectors`** (projekt ze struktury F0, tady první obsah) obsahuje:
  - `Abstractions/`: společné pro všechny platformy;
  - `Shoptet/`: implementace pro Shoptet;
  - `Publishing/`: publikace a vrácení, nezávislé na platformě;
  - `Security/`: šifrování tokenů.

  Knihovna nemá závislost na ASP.NET.
- **`src/EshopGuard.Api`** přidá:
  - instalační endpoint;
  - endpointy propojení a stavu konektoru;
  - příjem webhooků;
  - endpointy publikace a vrácení.

  Dlouhou práci nedělá. Jediná výjimka je výměna instalačního kódu za token, kterou Shoptet vyžaduje do 5 s.
- **`src/EshopGuard.Worker`** přidá obsluhy úloh `connector.*`. Všechny úlohy jednoho konektoru mají `concurrency_key = connector:{connectorId}`, takže na jeden e-shop běží vždy jedna úloha. Uvnitř úlohy jsou nejvýš 3 souběžné požadavky.
- **`src/EshopGuard.Data`** dostane migraci `AddShoptetConnector`.

**Názvy endpointů Shoptetu** (OpenAPI bundle cdd90bf, 1. 10. 2026), tvar zpráv webhooků a pole produktů se před implementací ověří proti dokumentaci. V textu jsou uvedené tak, jak je popisuje rešerše. Co rešerše neříká, je označené „ověřit“.

### Společné abstrakce (`EshopGuard.Connectors/Abstractions`)

```csharp
public sealed record ConnectorText(
    string ExternalType,      // product | category | page | article
    string ExternalId,        // GUID nebo ID v Shoptetu
    string Language,          // sk, cs, …
    string Field,             // name | additional_name | short_description | description | meta_description | content
    string Value,             // HTML nebo text, jak ho vrátilo API
    bool IsHidden,            // visibility = hidden
    string? Url, string? Category);

public interface IConnectorTextSource      // čtení
public interface IConnectorChangeFeed      // „změněno od“
public interface IConnectorWebhookVerifier // podpis
public interface IConnectorPublisher       // GET aktuální pole + PATCH jednoho pole v jednom jazyce
public interface IConnectorWebhookRegistrar
public interface IConnectorChangeSink       // předání změněných stránek ke kontrole (výchozí: běh connector_check; změna 16 doplní zpracování)
```

### Instalace a propojení (`Shoptet/ShoptetInstallHandler`, `Shoptet/ShoptetClaimService`)

**Tři kroky, propojení jen po všech třech:**

1. **Instalace v Shoptetu.** Zákazník v Onboardingu klikne na „Pripojiť Shoptet“. API vrátí adresu doplňku na tržišti Shoptetu a uloží `connector_link_intents` (tenant, e-shop, uživatel, platnost 60 min). Ten slouží jen k předvyplnění a k ověření domény, ne k propojení.
2. **Instalační volání.** Shoptet zavolá `GET|POST /api/connectors/shoptet/install?code=…` (metodu ověřit) z 185.184.254.0/24. API:
   - zkontroluje zdrojovou adresu (za Caddy z hlavičky `X-Forwarded-For` jen od důvěryhodné proxy);
   - do 4 s vymění kód za OAuth token;
   - zjistí `eshopId` a adresu e-shopu;
   - uloží globální řádek `shop.connector_installations` (bez tenanta, token šifrovaně, platnost nepropojené instalace 7 dní);
   - odpoví 200.

   Když výměna selže nebo překročí 4 s, odpoví 5xx a instalace se nezapíše.
3. **Potvrzení.**
   - Správce e-shopu otevře v administraci Shoptetu stránku nastavení doplňku (`/connect/shoptet/settings`). Shoptet ho na ní přihlásí a předá ověřitelnou identitu e-shopu (mechanismus ověřit v dokumentaci „addon-settings-in-shoptet-administration“).
   - Stránka vydá jednorázový odkaz s tokenem: platnost 15 min, v DB jen otisk SHA-256 (stejná pravidla jako přihlášení odkazem, databáze, část 9, bod 1).
   - Odkaz otevře aplikaci EshopGuard. Přihlášený uživatel s rolí owner nebo admin vybere e-shop (předvyplněný z `connector_link_intents`) a potvrdí.
   - `ShoptetClaimService.ClaimAsync` ověří, že doména instalace odpovídá `shops.domain`, případně některé `shop_languages.base_url`. Pak přesune token do `shop.connectors` (RLS tenanta), smaže ho z `connector_installations`, zapíše `shop_verifications` (`method = connector`, `status = verified`), `shops.source_mode = connector` a `shops.platform = shoptet`. Nakonec založí úlohy `connector.register_webhooks` a `connector.full_sync`.

**Jedinečnost.** Jeden aktivní konektor na instalaci: U (`platform`, `external_shop_id`) WHERE `status <> 'revoked'`. Druhý tenant, který chce stejnou instalaci, dostane `connector.installation_already_linked` bez údajů o prvním tenantovi.

### Tokeny (`Security/ConnectorCredentialsProtector`, `Shoptet/ShoptetTokenProvider`)

- **OAuth token (neomezený)** je šifrovaný přes `IDataProtector` s účelem `EshopGuard.Connectors.Credentials.v1`. Uložený je v `connectors.credentials_enc` s `credentials_key_id`. Hlavní klíč je mimo databázi (`.env` na serveru, lokálně user-secrets, adresář klíčů Data Protection mimo repozitář).
- **API token (30 min)** získává `ShoptetTokenProvider.GetAsync(connectorId)`. Je jen v paměti procesu (`IMemoryCache`, vyprší 5 min před koncem platnosti) a nikdy se neukládá do databáze.
- **Tajemství podpisu webhooků** je v `connectors.webhook_secret_enc`, šifrované stejně. Odkud ho Shoptet vydává, ověřit (proposal, K rozhodnutí 3).
- **Logy.** Typovaný `HttpClient` pro Shoptet má `RedactLoggedHeaders` pro hlavičku s tokenem (název hlavičky ověřit, podle rešerše přístupový token). Výjimky z HTTP se logují bez těla požadavku. Test `ConnectorSecretsNotLoggedTests` hledá v zachyceném logu hodnoty testovacích tokenů.

### Příjem webhooku (`Api/Webhooks/ShoptetWebhookEndpoint`)

1. Přečte syrové tělo (strop 1 MB, protože `sendPayload: full` posílá celý detail produktu).
2. Z těla vezme `eshopId` (název pole ověřit) a najde konektor. Tajemství je v cache paměti po dobu 10 min.
3. Ověří HMAC-SHA1 nad syrovým tělem proti hlavičce `Shoptet-Webhook-Signature` porovnáním v konstantním čase (`CryptographicOperations.FixedTimeEquals`). Kódování podpisu (hex nebo base64) ověřit.
4. Spočítá `dedupe_key` = SHA-256(`eshopId` | `event` | `eventInstance` | `eventCreated`). Názvy polí ověřit, Shoptet ID události neposílá.
5. V jedné transakci zapíše `INSERT INTO shop.connector_events … ON CONFLICT (connector_id, dedupe_key) DO NOTHING` a, když jde o nový řádek, úlohu `connector.coalesce` s `dedupe_key = coalesce:{connectorId}:{začátek 2min okna}` a `not_before = konec okna`.
6. Odpoví 200 do 1 s (cíl p99 < 300 ms).

**Chyby:**

| Situace | Odpověď | Uloží se |
|---|---|---|
| Neznámý `eshopId` | 401 | nic, jen metrika |
| Neplatný podpis | 401 | nic, jen metrika |
| Databáze nedostupná | 503 | nic; Shoptet zkusí znovu 3× po 15 min a zbytek dorovná hodinové dorovnání |
| Odinstalovaný nebo odpojený konektor | 200 | nic, aby Shoptet webhook nevypnul kvůli chybám; mezitím ho rušíme |

### Slučování a načtení (`Shoptet/ShoptetEventCoalescer`, `Shoptet/ShoptetChangeProcessor`)

**Slučování.** `connector.coalesce` vezme všechny události konektoru ve stavu `received` s `received_at < konec okna` (`FOR UPDATE SKIP LOCKED`):
- rozdělí je na ID produktů, `eshop:design` a události doplňku;
- produkty dá do dávek po 100 jako úlohy `connector.fetch_products` (P1);
- `eshop:design` předá změně 16 jako požadavek na procházení webu;
- události doplňku (odinstalace, pozastavení, obnovení; názvy ověřit) zpracuje hned;
- události označí `coalesced` s ID úlohy.

Hromadný import 300 produktů tak dá 3 dávky, ne 300 úloh.

**Načtení.** `connector.fetch_products`:
1. Pro každý produkt a každý jazyk, který se kontroluje (`shop_languages` aktivní a potřebné pro aktivní místa prodeje), načte aktuální detail přes API s parametrem `language`.
2. Spočítá otisk textových polí a porovná ho s `page_versions.text_hash` aktuální verze.
3. Beze změny textu (například jen sklad nebo cena) událost skončí `processed` a výsledkem `no_text_change`. Nic se neplatí.
4. Změněné texty zapíše do `pages` a `page_versions` (`source = connector`, `external_id`, `is_hidden_in_shop`). Pak přes `IConnectorChangeSink` založí běh `connector_check` (`trigger = webhook`, změna 8) s texty jako `TextInput`. Parametry běhu jsou `change_source` (`webhook`, `reconcile` nebo `save_hidden`) a `change_kind` po stránce (`new`, `text_changed`, `removed`, `hidden_saved`). Z nich změna 16 zapíše `page_changes` a upozornění (`ConnectorChangeProcessor`).
5. Skryté produkty dostanou běh s prioritou P0, ale nejvýš 20 v jedné dávce (proposal, K rozhodnutí 9), jen když `shops.check_hidden_on_save` je zapnuté, a `change_source = save_hidden`. Pokud změna 16 ponechá vlastní úlohu `monitor.check_saved`, `IConnectorChangeSink` jí skrytý produkt předá místo založení běhu (proposal, K rozhodnutí 13).
6. Smazaný produkt (404 nebo událost smazání) dostane `pages.status = gone` a předá se s `change_kind = removed`. Nálezy se neoznačí jako vyřešené, to rozhoduje zpracování výsledku.

**Zámek dávky.** Úloha drží dávku v leasu. Při pádu se dávka zopakuje: otisky zajistí, že se nic nezdvojí.

### Dorovnání (`Shoptet/ShoptetChangeReconciler`)

- **Úloha `connector.reconcile_changes`:**
  - zavolá `/products/changes` s `changeTimeFrom = sync_cursor − 5 min` (překryv proti ztrátě na hraně), po stránkách;
  - ID předá stejnou cestou jako webhook (`connector.fetch_products`, `change_source = reconcile`);
  - po zpracování nastaví `sync_cursor` na nejvyšší čas změny a `last_reconcile_at`.
- **Kdy se spouští:**
  - každou hodinu ji plánuje tato změna (`dedupe_key = reconcile:{connectorId}:{datum}T{hodina}`);
  - noční běh v minutě e-shopu plánuje změna 16 (`ops.schedules` `kind = reconcile`, `dedupe_key = reconcile:{shop}:{datum}`) s parametrem `nightly = true`, který navíc spustí `connector.sync_content`;
  - dokud změna 16 není hotová, má parametr `nightly = true` hodinový běh v hodině `shops.monitor_slot_minute`.
- **Kurzor starší než 29 dní** (okno Shoptetu je 30 dní) nebo chybějící: úloha `connector.full_sync`:
  - projde všechny produkty po stránkách, pro velké katalogy asynchronní snapshot JSONL (ověřit);
  - spočítá `product_count` zveřejněných produktů po jazycích do `shop_languages`, pro pásmo ve změně 12; počítá se jen `visibility = visible`, varianty ne (proposal, K rozhodnutí 5);
  - nastaví `sync_cursor`.
- **Noční `connector.sync_content`** (z nočního dorovnání): kategorie, stránky (čtení z `content`) a články. Porovná otisky s aktuálními verzemi a změněné pošle do běhu `connector_check`. Webhook ani „změněno od“ pro ně Shoptet nemá. Jestli existuje filtr podle data změny, ověřit.
- **Pojistka:** události ve stavu `received` starší než 10 min bez úlohy (například výpadek plánovače) pošle dorovnání do slučování.

### Hlídač odběrů (`Shoptet/ShoptetWebhookRegistrar`, `Shoptet/ShoptetHealthChecker`)

**Úloha `connector.verify_webhooks`** (každou hodinu):
1. Načte registrované webhooky instalace a porovná je s očekávanými v `connector_webhooks`:
   - `product:create`, `product:update`, `product:delete`;
   - hromadné události produktů;
   - `eshop:design`;
   - události doplňku.

   Přesné názvy ověřit (proposal, K rozhodnutí 7). Jedna URL na událost a instalaci.
2. Chybějící nebo neaktivní odběry zaregistruje znovu. Od 18. 9. 2026 se webhooky bez práva samy odregistrují.
3. Přečte log notifikací za posledních 7 dní (`/api/webhooks/notifications`). Když najde neúspěšná doručení od poslední kontroly, spustí `connector.reconcile_changes` hned.
4. Zapíše `connectors.health`: `webhooks_ok`, `missing`, `failed_deliveries`, `last_checked_at` a `api_status`.

**Stav konektoru podle odpovědi API:**

| Odpověď | Stav konektoru |
|---|---|
| 401 nebo 403 při získání API tokenu | `error` (práva odebrána) |
| Odpověď o pozastaveném doplňku (tvar ověřit) | `paused` |

Aplikace pak ukáže „Zmeny zo Shoptetu neprichádzajú, kontrolujeme raz denne z webu“. Monitoring už nesmí tvrdit „do niekoľkých minút“.

### Limity (`Shoptet/ShoptetRateLimiter`)

- **Kbelík** `ops.rate_limit_buckets` s klíčem `connector:shoptet:{eshopId}`: kapacita 200, doplňování 10/s. Je společný pro všechny workery a odděleně od limitu Jevu. Každý požadavek si rezervuje 1 token (`UPDATE … RETURNING`).
- **Souběh:** `concurrency_key = connector:{connectorId}` a uvnitř úlohy `SemaphoreSlim(3)`. Tím nejvýš 3 spojení na token.
- **Odpovědi:**
  - 429: počkat podle `Retry-After`, jinak 2 s, nejvýš 5 pokusů;
  - 423 (zámek 5 s po stejném zápisu): počkat 6 s a zkusit znovu;
  - 5xx: opakování úlohy s rostoucím odstupem.
- **Dlouhé úlohy** (úplná synchronizace) běží po dávkách 100 produktů a pokračování zařadí na konec fronty. Publikace (P1) tak nečeká na celou synchronizaci (P2).

### Publikace (`Publishing/PublicationService`, `Publishing/FieldPatcher`, `Shoptet/ShoptetPublisher`)

**Založení.** `POST /api/t/{t}/shops/{s}/publications` (role editor a výš):
- vezme přijaté nebo upravené `fix_proposals`;
- seskupí je podle (`external_type`, `external_id`, `language`, `field`);
- pro každou skupinu založí `publications` s `idempotency_key = pub:{sha256(seřazená ID návrhů a jejich revize)}` (U), `status = queued`;
- založí úlohu `connector.publish` (P1, `dedupe_key = publish:{publicationId}`).

Druhé kliknutí vrátí existující publikace.

**Předpoklady (fail-closed):**

| Podmínka | Jinak |
|---|---|
| `connectors.status = connected` | `connector.not_connected` |
| `connectors.access = read_write` | `connector.read_only` → „Kopírovať text“ |
| `shop_verifications` ověřené | `connector.ownership_not_verified` |
| Pole lze zapisovat (ne varianta, ne šablona) | `connector.field_not_writable` → „Kopírovať text“ |

**Postup úlohy `connector.publish`:**
1. Načte aktuální hodnotu pole v jazyce (`GET`, parametr `language`).
2. **Idempotence:** když se normalizovaný aktuální text rovná očekávanému novému textu (`expected_new_hash`), nastaví `published` bez zápisu. Platí pro opakování po úspěšném PATCH a pádu před uložením stavu.
3. **Kontrola konfliktu:** porovná normalizovaný text aktuální hodnoty (bloky přes `HtmlText` z `EshopGuard.Core.Extract`) s textem pole v `page_versions` verze, ze které vznikly návrhy (`source_text_hash`). Při rozdílu:
   - `publications.status = conflict` s `conflict_details` (který blok se liší, bez celého textu v logu);
   - `fix_proposals.status = conflict`;
   - nový běh `connector_check` produktu.

   Nic se nezapíše.
4. Uloží `old_value` (celé pole, jak ho vrátilo API) a `old_value_hash`. To je původní znění pro vrácení.
5. `FieldPatcher.Apply(oldValue, proposals)`:
   - najde blok podle `block_index` a jeho původního textu;
   - spočítá nejmenší změněný úsek (společný začátek a konec původního a nového textu);
   - když úsek leží v jednom textovém uzlu, nahradí jen ten podřetězec a všechny značky kolem zůstanou;
   - když úsek přechází přes formátovací prvky (`b`, `strong`, `i`, `em`), nahradí je prostým textem;
   - když přechází přes odkaz `a` nebo jiný prvek, nebo když se původní text v bloku nenajde právě jednou: `patch_unsafe` → `failed` s kódem `connector.patch_unsafe` a nabídkou „Kopírovať text“.
6. PATCH jen tohoto pole s parametrem `language`, bez ostatních polí. U stránky zápis do `description`, protože Shoptet stránku čte z `content` a zapisuje do `description` (ověřit, K rozhodnutí 8).
7. Znovu načte pole a ověří, že normalizovaný text odpovídá `expected_new_hash`. Když ne, `failed` s `connector.verify_after_write_failed` a upozornění provozu.
8. Nastaví:
   - `publications.status = published` a `published_at`;
   - `fix_proposals.status = published`;
   - nová verze stránky z přečteného pole;
   - běh `recheck` produktu (změna 8) s `change_kind = fix_published`. Nález bez dalšího výskytu se uzavře. „Oprava potvrdená“ v `page_changes` zapíše změna 16 podle `publications.status = published`;
   - audit `publication.published` (kdo, kdy, pole, jazyk, bez textu).

### Vrácení (`Publishing/RollbackService`)

`POST /api/t/{t}/publications/{id}/rollback` (editor a výš) založí úlohu `connector.rollback`:
1. Načte aktuální pole.
2. Když jeho otisk ≠ otisk publikovaného nového znění (někdo text mezitím upravil), nastaví `conflict` a nic nezapíše. Uživatel dostane původní znění ke zkopírování.
3. Když se shoduje, PATCH `old_value`, ověření po zápisu, `rolled_back` a `rolled_back_at`, `fix_proposals` zpět na `accepted`, audit `publication.rolled_back`.
4. Opakované vrácení již vrácené publikace je bez účinku a vrátí 200 se stavem `rolled_back`.

### Odpojení (`Shoptet/ShoptetDisconnectService`)

- **V aplikaci** (`POST …/connector/disconnect`, owner nebo admin), úloha `connector.disconnect`:
  1. zruší registrované webhooky přes API (když to jde);
  2. nastaví `connectors.status = revoked`, `credentials_enc` a `webhook_secret_enc` na NULL a vyhodí API token z paměti;
  3. publikace ve stavu `queued` přejdou do `failed` s kódem `connector.disconnected`;
  4. `shops.source_mode = web` a `shops.check_hidden_on_save = false`;
  5. upozornění a audit.

  Zákazník dostane pokyn odinstalovat doplněk v administraci Shoptetu. Odinstalaci z naší strany přes API rešerše nepotvrzuje (ověřit).
- **Odinstalace v Shoptetu** (událost doplňku nebo 401 trvale): stejný postup bez volání API.
- **Pozastavení doplňku:**
  - `paused`, tokeny zůstanou a publikace čekají;
  - po obnovení se spustí `connector.reconcile_changes` od `sync_cursor`, při kurzoru nad 29 dní `connector.full_sync`.

## Architecture Decisions

1. **Webhook = rychlá cesta, „změněno od“ = zdroj pravdy** (rešerše, Hlavní zjištění 4). Shoptet zkusí 3× po 15 min a zprávu zahodí. Hodinové dorovnání přes `/products/changes` s překryvem 5 min proto chytí, co webhook ztratí.
2. **Obsahu zprávy se nevěří.** Shoptet nezaručuje pořadí a ID události neposílá. Worker vždy načte aktuální stav přes API a porovná otisk textu. Většina `product:update` (sklad, cena) tak skončí bez volání Jevu.
3. **Slučovací okno 2 minuty s pevným klíčem okna.** Klíč úlohy `coalesce:{connectorId}:{začátek okna}` s `not_before = konec okna` dává jednu úlohu na okno bez závislosti na částečné jedinečnosti fronty. Událost, která přijde po spuštění úlohy, patří do dalšího okna. Zpoždění je 0–2 min plus fronta.
4. **Propojení vyžaduje dvě nezávislé identity.** Správce e-shopu se ověří v administraci Shoptetu a uživatel EshopGuardu (owner nebo admin) se přihlásí u nás. Propojení jen podle domény by útočníkovi, který si založí e-shop s cizí doménou, dalo zápis do cizího Shoptetu.
5. **Jedna úloha na konektor najednou** (`concurrency_key`) a uvnitř 3 spojení. Je to nejjednodušší způsob, jak dodržet „3 souběžná spojení na token“ přes víc workerů. Dlouhé úlohy jdou po dávkách, aby P1 nečekalo.
6. **Kontrola konfliktu porovnává celé pole, ne jen blok** (architektura, část 5, Zápis oprav: „Když se mezitím změnil, je to konflikt a nová kontrola“). Je to přísnější a bezpečné: kdo text mezitím upravil, mohl změnit i kontext opravy.
7. **Oprava se vkládá do nejmenšího změněného úseku v původním HTML.** Zůstanou odkazy, formátování i okolní text („Text okolo nemeníme“, Review). Kde by se značky ztratily, nic se nezapíše a nabídne se „Kopírovať text“.
8. **Idempotence publikace na dvou úrovních:**
   - `publications.idempotency_key` brání dvojímu založení;
   - porovnání aktuální hodnoty s očekávaným novým textem brání dvojímu zápisu po pádu.

   Shoptet klíč idempotence pro PATCH nemá (rešerše ho neuvádí). Druhý stejný zápis do 5 s dostane 423, což řeší pravidlo opakování.
9. **Texty z konektoru jsou data tenanta**, i koncepty skrytých produktů. Ukládají se jen v `pages`, `page_versions` a souborech tenanta. `connector_events.payload` se ukládá jen u událostí bez citlivého obsahu. Při `sendPayload: full` se ukládá jen seznam ID a typ, celý detail ne, protože se stejně načítá znovu. Události se mažou po 30 dnech (měsíční části).
10. **Webhook neznámé instalace se odmítne 401, odpojené instalace se potvrdí 200.** U odpojené instalace by opakované chyby vedly Shoptet k vypnutí webhooku, což nevadí, ale zbytečně by zahltily log. Tajemství podpisu odpojeného konektoru už nemáme, proto se nic neukládá.
11. **Konektor může patřit k jazykové verzi.** Oddělené CZ a SK e-shopy jsou v Shoptetu oddělené instalace (rešerše, Jazyky), zatímco v EshopGuardu může být `bylinkovo.cz` jazykovou verzí e-shopu `bylinkovo.sk` (architektura, část 12). Proto `connectors.shop_language` (null = hlavní verze) a jedinečnost (`shop_id`, `shop_language`). Nesrovnalost s ER diagramem (`shops ||--o| connectors`), proposal K rozhodnutí. Do rozhodnutí se zakládá jen hlavní verze.

## Data Flow

### Flow 1: připojení

```
Onboarding "Pripojiť Shoptet" ─► POST /api/t/{t}/shops/{s}/connectors/shoptet/start ─► connector_link_intents ─► {addonUrl}
Zákazník nainstaluje doplněk v Shoptetu
Shoptet (185.184.254.0/24) ─► /api/connectors/shoptet/install?code ─► výměna kódu ≤ 4 s ─► connector_installations ─► 200
Správce otevře nastavení doplňku v administraci ─► /connect/shoptet/settings (identita e-shopu od Shoptetu)
   └─► jednorázový odkaz (15 min, otisk SHA-256) ─► aplikace EshopGuard
Přihlášený owner/admin potvrdí e-shop ─► POST /api/t/{t}/connectors/shoptet/claim
   ├─ doména sedí? ne → 409 connector.domain_mismatch
   ├─ instalace už propojená jinde? → 409 connector.installation_already_linked
   ├─ connectors (connected, token šifrovaně), shop_verifications (connector, verified), shops.source_mode = connector
   └─ úlohy connector.register_webhooks, connector.full_sync (počty produktů po jazycích → pásmo při dalším výpočtu nabídky, změny 10 a 12)
```

### Flow 2: změna produktu → nález

```
Obchodník uloží produkt ─► Shoptet product:update ─► POST /api/webhooks/shoptet
   ├─ podpis HMAC-SHA1 ✔, dedupe_key ─► connector_events (received) + job connector.coalesce (okno 2 min) ─► 200 (< 1 s)
Konec okna ─► connector.coalesce ─► connector.fetch_products (≤ 100 ID, P1)
   ├─ GET detail po jazycích (kbelík 10/s, 3 spojení)
   ├─ otisk textu beze změny → processed (no_text_change)
   └─ změna → pages / page_versions ─► běh connector_check (P1; skrytý produkt P0) ─► Jev ─► pravidla ─► návrh opravy
         └─ změna 16 (ConnectorChangeProcessor): page_changes (webhook | reconcile | save_hidden), upozornění
```

### Flow 3: ztracený webhook

```
Každou hodinu ─► connector.reconcile_changes ─► /products/changes?changeTimeFrom = cursor − 5 min
   └─ ID ─► connector.fetch_products (stejná cesta, otisky zabrání dvojí kontrole) ─► sync_cursor
Kurzor > 29 dní ─► connector.full_sync (po dávkách, pokračování na konec fronty)
```

### Flow 4: publikace a vrácení

```
Review "Publikovať do e-shopu (1 zmena)" ─► POST /api/t/{t}/shops/{s}/publications
   └─ publications (queued, idempotency_key) ─► connector.publish (P1)
connector.publish:
   GET pole ─► už nový text? → published (bez zápisu)
            ─► text ≠ text verze nálezu? → conflict + nová kontrola
            ─► old_value ─► FieldPatcher ─► patch_unsafe? → failed + „Kopírovať text“
            ─► PATCH pole + language (423 → 6 s, 429 → Retry-After) ─► GET ověření ─► published
            ─► fix_proposals published, běh recheck (změna 16 zapíše „Oprava potvrdená“)
"Vrátiť zmenu" ─► POST …/publications/{id}/rollback ─► connector.rollback
   GET pole ─► ≠ publikovaný text → conflict (nic se nezapíše) | = → PATCH old_value ─► rolled_back
```

### Flow 5: hlídač a odpojení

```
Každou hodinu ─► connector.verify_webhooks ─► chybí odběr → registrace; neúspěšná doručení → reconcile hned
Token 401/403 ─► connectors.status error ─► upozornění, Monitoring ukáže výpadek
Odinstalace / "Odpojiť" ─► connector.disconnect ─► tokeny NULL, revoked, publikace queued → failed, source_mode = web
```

## File Changes

### `src/EshopGuard.Connectors/`

- **`EshopGuard.Connectors.csproj`:** reference na `EshopGuard.Core`, `EshopGuard.Data` a `EshopGuard.Jobs` a balíček `Microsoft.AspNetCore.DataProtection`.
- **`ServiceCollectionExtensions.cs`:** `AddEshopGuardConnectors(IConfiguration)` registruje typovaný `HttpClient` `ShoptetApiClient` s `RedactLoggedHeaders`.
- **`ConnectorsOptions.cs`:**
  - `Shoptet:ClientId`, `Shoptet:ClientSecret`, `Shoptet:AddonUrl`, `Shoptet:InstallSourceCidr` (výchozí 185.184.254.0/24), `Shoptet:WebhookBaseUrl`;
  - `CoalesceWindowSeconds = 120`, `ReconcileOverlapMinutes = 5`, `HiddenP0BatchLimit = 20`;
  - `ToString()` s maskou.
- **`Abstractions/`:** `ConnectorText.cs`, `IConnectorTextSource.cs`, `IConnectorChangeFeed.cs`, `IConnectorWebhookVerifier.cs`, `IConnectorPublisher.cs`, `IConnectorWebhookRegistrar.cs`, `IConnectorChangeSink.cs` a výchozí `RunConnectorChangeSink.cs`, `ConnectorErrorCodes.cs`.
- **`Security/ConnectorCredentialsProtector.cs`.**
- **`Shoptet/`:**

  | Soubor | Účel |
  |---|---|
  | `ShoptetApiClient.cs` | GET produktů, kategorií, stránek a článků s parametrem `language`; `/products/changes`; PATCH; webhooky; log notifikací |
  | `ShoptetTokenProvider.cs` | API token z OAuth tokenu, cache v paměti |
  | `ShoptetInstallHandler.cs` | výměna instalačního kódu |
  | `ShoptetClaimService.cs` | potvrzení propojení |
  | `ShoptetSettingsPageHandler.cs` | identita správce z nastavení doplňku, vydání odkazu |
  | `ShoptetWebhookVerifier.cs` | HMAC-SHA1 |
  | `ShoptetWebhookRegistrar.cs`, `ShoptetHealthChecker.cs` | hlídač odběrů a stav doplňku |
  | `ShoptetEventCoalescer.cs`, `ShoptetChangeProcessor.cs` | slučování a načtení změn |
  | `ShoptetChangeReconciler.cs`, `ShoptetFullSync.cs`, `ShoptetContentSync.cs` | dorovnání |
  | `ShoptetTextMapper.cs` | pole Shoptetu na `ConnectorText` a `TextInput` |
  | `ShoptetRateLimiter.cs` | kbelík a souběh |
  | `ShoptetPublisher.cs` | čtení a zápis pole |
  | `ShoptetDisconnectService.cs` | odpojení |
  | `ShoptetModels.cs` | modely odpovědí a zpráv |

- **`Publishing/`:** `PublicationService.cs`, `FieldPatcher.cs`, `RollbackService.cs`, `TextNormalizer.cs` (normalizace a otisk přes `HtmlText` z Core).

### `src/EshopGuard.Core/`

- **`Extract/HtmlText.cs`:** zveřejnit (nebo `InternalsVisibleTo EshopGuard.Connectors`) rozdělení fragmentu HTML na bloky s mapováním bloků na uzly DOM pro `FieldPatcher`. Beze změny chování dnešních 192 testů.

### `src/EshopGuard.Data/`

**Migrace `Migrations/2026xxxx_AddShoptetConnector`:**

| Tabulka | Změna |
|---|---|
| `shop.connectors` | `webhook_secret_enc bytea`, `shop_language text null`, `paused_reason`, `last_full_sync_at`, U (`platform`, `external_shop_id`) WHERE `status <> 'revoked'`, U (`shop_id`, `shop_language`) |
| `shop.connector_installations` (nová, **globální**, bez textů zákazníka, jen systémová role) | `platform`, `external_shop_id`, `shop_url`, `credentials_enc`, `credentials_key_id`, `installed_at`, `expires_at`, `claimed_at`, `claimed_connector_id` |
| `shop.connector_link_intents` (nová, RLS) | `shop_id`, `platform`, `created_by`, `expires_at`, `used_at` |
| `shop.connector_claim_tokens` (nová, globální) | `installation_id`, `token_hash` (U), `expires_at`, `used_at`, `requested_ip_hash` |
| `shop.connector_events` | `coalesce_window_start`, `job_id`, `result` (no_text_change/checked/ignored/error), U (`connector_id`, `dedupe_key`) v každé měsíční části |
| `shop.connector_webhooks` | `expected bool`, `last_error` |
| `fixes.publications` | `external_type`, `source_text_hash`, `expected_new_hash`, `conflict_details jsonb`, `error_code`, `job_id` |
| `fixes.fix_proposals` | stav `conflict` už existuje, beze změny |

**Další soubory:** `Configurations/Shop/*.cs` a `Configurations/Fixes/PublicationConfiguration.cs`.

### `src/EshopGuard.Api/`

Role se vynucují přes `.RequireTenantRole` ze změny 9 (`owner`/`admin` = `TenantRole.Admin`, „editor a výš“ = `TenantRole.Editor`). Nedostatečná role vrátí `403 auth.forbidden_role`, cizí tenant 404.

- **`Endpoints/ShoptetConnectorEndpoints.cs`:**

  | Metoda | Cesta | Role |
  |---|---|---|
  | POST | `/api/t/{tenantId}/shops/{shopId}/connectors/shoptet/start` | owner, admin |
  | POST | `/api/t/{tenantId}/connectors/shoptet/claim` | owner, admin |
  | GET | `/api/t/{tenantId}/shops/{shopId}/connector` | stav, health, poslední webhook a dorovnání, přístup |
  | POST | `/api/t/{tenantId}/shops/{shopId}/connector/disconnect` | owner, admin |

- **`Endpoints/ShoptetInstallEndpoints.cs`:** `/api/connectors/shoptet/install` (kontrola zdrojové sítě, limit 4 s) a `/api/connectors/shoptet/settings` (stránka nastavení doplňku → odkaz do aplikace).
- **`Endpoints/PublicationEndpoints.cs`** (editor a výš):
  - `POST /api/t/{tenantId}/shops/{shopId}/publications`;
  - `GET /api/t/{tenantId}/shops/{shopId}/publications?status=`;
  - `POST /api/t/{tenantId}/publications/{publicationId}/rollback`.
- **`Webhooks/ShoptetWebhookEndpoint.cs`:** `POST /api/webhooks/shoptet`.

### `src/EshopGuard.Worker/`

**`Handlers/Connectors/*.cs`, obsluhy úloh** (všechny s `concurrency_key = connector:{connectorId}`):

| Úloha | Druh | Priorita | Kdy |
|---|---|---|---|
| `connector.register_webhooks` | io | P1 | po propojení |
| `connector.verify_webhooks` | io | P1 | každou hodinu |
| `connector.coalesce` | system | P1 | konec okna |
| `connector.fetch_products` | io | P1 (skryté P0) | ze slučování a dorovnání |
| `connector.reconcile_changes` | io | P1 | každou hodinu (tato změna), v noci v minutě e-shopu (změna 16) |
| `connector.full_sync` | io | P2 | po propojení, kurzor > 29 dní |
| `connector.sync_content` | io | P3 | z nočního dorovnání |
| `connector.publish` | io | P1 | po kliknutí |
| `connector.rollback` | io | P1 | po kliknutí |
| `connector.disconnect` | io | P1 | odpojení, odinstalace |

### `tests/`

- **`tests/EshopGuard.Connectors.Tests/`:**
  - `ShoptetWebhookVerifierTests`, `ShoptetEventCoalescerTests`, `ShoptetChangeProcessorTests`, `ShoptetChangeReconcilerTests`;
  - `ShoptetTokenProviderTests`, `ShoptetRateLimiterTests`;
  - `FieldPatcherTests`, `PublicationServiceTests`, `RollbackServiceTests`, `ShoptetClaimServiceTests`;
  - `ConnectorSecretsNotLoggedTests`;
  - `Fixtures/shoptet/*.json`: odpovědi podle příkladů z OpenAPI, bez reálných dat zákazníků;
  - `Fakes/FakeShoptetHandler.cs`.
- **`tests/EshopGuard.Api.Tests/Connectors/`:** `ShoptetWebhookEndpointTests` (podpis, duplicita, čas odpovědi), `ShoptetInstallEndpointTests` (zdrojová síť, limit 4 s), `ConnectorEndpointsAuthorizationTests`, `PublicationEndpointsTests`.
- **`tests/EshopGuard.Connectors.IntegrationTests/`:** běží jen s `ESHOPGUARD_SHOPTET_TEST=1` proti testovacímu e-shopu Shoptet, ne v CI.
