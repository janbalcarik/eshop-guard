# Proposal: Cache jen v PostgreSQL, stejně pro CLI i aplikaci

## Intent

**Dnešní stav (zjištěno 1. 10. 2026):** CLI ukládá odpovědi Jevu, přepisy textů a profily šablon do souboru SQLite `src/cache/jev-cache.sqlite`. Soubor má 56 MB a obsahuje:
- tabulku `cache`: 66 159 odpovědí Jevu, klíč `sha256:…` přes celý dotaz, odpověď jako JSON;
- tabulku `rewrite_cache`: 36 přepisů.

Plán dosud počítal s tím, že CLI zůstane u SQLite a PostgreSQL použije jen aplikace (změny 5 a 8).

**Požadavek uživatele (1. 10. 2026):** cache MUSÍ být součástí PostgreSQL. Lokálně se nesmí nic vytvářet a v CLI i v aplikaci musí fungovat naprosto stejně.

**Přínos:**
- jedna implementace pro CLI i worker, takže chování při testu a v provozu se neliší;
- žádný lokální soubor, který by se dal ztratit, musel zálohovat nebo hlídat v gitu.

**Rozhodnutí uživatele (1. 10. 2026):** data ze SQLite se **nepřevádějí**. Uložené odpovědi mají hodnotu řádově 10 USD (odhad z počtu odpovědí a odhadovače tokenů, neměřeno); hlavní smysl úložiště odpovědí je limit Jevu (1 200 požadavků za minutu na klíč) při opakovaných bězích, ne cena. Profily šablon ze SQLite se vytvoří znovu při dalším ostrém skenu (0,07–0,13 USD za profil, vždy po odhadu a souhlasu).

Fáze F3, po F1. Podklady:
- `databaze-a-plan-implementace-2026-10-01.md`, tabulky `checks.jev_answers`, `fixes.rewrite_cache`, `shop.page_profiles`;
- změna 5, rozhraní `IJevCache`, `IRewriteCache`, `IPageProfileStore`.

## Scope

In scope:
- `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore` v `EshopGuard.Data` (dosud ve změně 8, přesouvá se sem).
- Úprava tabulek:
  - `checks.jev_answers`: klíč je dnešní `cache_key` (`sha256:…`) a odpověď se ukládá jako `jsonb` (unese i odpovědi typu choice/score); totéž `checks.sieve_answers`;
  - `fixes.rewrite_cache` a `shop.page_profiles` podle dnešních záznamů.
- CLI napojené na PostgreSQL:
  - připojení přes `ConnectionStrings__Cli` (user-secrets nebo proměnná prostředí), role `eshopguard_worker`;
  - vyhrazený tenant `cli` v lokální nebo testovací databázi.
- Odstranění SQLite z kódu:
  - třídy `SqliteJevCache`, `SqliteRewriteCache`, `SqlitePageProfileStore`;
  - balíček `Microsoft.Data.Sqlite`;
  - `CacheOptions.Path` a `cache.path` v `config/settings.yaml`.
- CLI smí odkazovat `EshopGuard.Data` (úprava povolených závislostí v `ProjectReferenceTests`).
- Test, že CLI nevytvoří žádný soubor ani složku `cache/`.
- Test, že CLI i worker používají tytéž implementace.

Out of scope:
- Ostatní úložiště (`IPageStore`, `IPageContentStore`, `IUrlFrontierStore`, `IRateLimiter`) zůstávají ve změně 8.
- Převod dat ze SQLite (rozhodnutí uživatele 1. 10. 2026).
- Smazání souboru `src/cache/jev-cache.sqlite`. Po této změně ho kód nečte; smazání rozhodne uživatel (v gitu je ignorovaný).

## Approach

1. Změna 5 dodá rozhraní a do této změny nechá SQLite jako přechodnou implementaci, aby se dalo porovnat s dneškem.
2. Tato změna:
   - doplní implementace nad PostgreSQL;
   - přepne na ně CLI;
   - odstraní SQLite.
3. Klíč cache se počítá stejně jako dnes (`JevCacheKey.LegacyKey`), CLI i worker ho počítají stejnou knihovnou.
4. Když PostgreSQL není dostupná, CLI skončí **před** prvním placeným voláním Jevu (fail-closed). Bez cache by se platily už zaplacené odpovědi znovu.
5. Běh s `--mock` se k databázi nepřipojuje vůbec: vymyšlené odpovědi falešného klienta se nikdy nesmí dostat do cache (jako dnes) a testy bez databáze zůstanou bez databáze.

## Dependencies

- Změna 3 `add-multitenant-data-model`: tabulky, role, RLS a tenant.
- Změna 5 `refactor-library-into-pipeline-steps`: rozhraní `IJevCache` s `JevCacheKey` a `GetManyAsync`, `IRewriteCache`, `IPageProfileStore`.
- Změna 8 implementace z této změny převezme a nebude je psát znovu.

## Done when

- `eshopguard scan` bez `--mock` uloží odpovědi do `checks.jev_answers` a `checks.sieve_answers` u tenanta `cli` a druhý stejný běh pošle 0 požadavků na Jev (test s falešným klientem Jevu proti testovací databázi). Nic se neplatí.
- Po běhu CLI neexistuje složka `cache/` ani žádný soubor `*.sqlite`, `*.sqlite-shm` nebo `*.sqlite-wal`.
- V kódu není `Microsoft.Data.Sqlite` ani třída `Sqlite*`.
- Všechny testy projdou a přibudou testy chování úložišť nad PostgreSQL (dědí společné testy ze změny 5) a test izolace tenantů.
- Test registrace služeb potvrdí, že CLI dostane třídy `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore` z jednoho rozšíření, které použije i worker (změna 8).

## K rozhodnutí

1. **Tenant pro CLI:** navrženo vyhrazený tenant `cli` (slug) v lokální nebo testovací databázi. Proti produkční databázi se CLI nespouští bez výslovného souhlasu.
2. **Klíč `jev_answers`:** navrženo dnešní `cache_key` (`sha256:…`) jako primární klíč s tenantem, `question_set_hash` jako doplňkový sloupec pro úklid. Plán databáze uváděl klíč (`tenant_id`, `question_set_hash`, `state_hash`) a `probabilities real[]`, tento návrh ho mění. Změnu 3 a dokument databáze je potřeba sladit.
3. **Kdy smazat `src/cache/jev-cache.sqlite`:** data se nepřevádějí (rozhodnutí uživatele 1. 10. 2026), soubor kód po změně nečte; smazání rozhodne uživatel.
4. **Odpovědi Jevu se mezi tenanty nesdílejí** (cache po tenantovi), i když by sdílení šetřilo. Potvrdit.
