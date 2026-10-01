# Proposal: Cache jen v PostgreSQL, stejně pro CLI i aplikaci

## Intent

**Dnešní stav (zjištěno 1. 10. 2026):** CLI ukládá odpovědi Jevu, přepisy textů a profily šablon do souboru SQLite `src/cache/jev-cache.sqlite`. Soubor má 56 MB a obsahuje:
- tabulku `cache`: 66 159 odpovědí Jevu, klíč `sha256:…` přes celý dotaz, odpověď jako JSON;
- tabulku `rewrite_cache`: 36 přepisů.

Plán dosud počítal s tím, že CLI zůstane u SQLite a PostgreSQL použije jen aplikace (změny 5 a 8).

**Požadavek uživatele (1. 10. 2026):** cache MUSÍ být součástí PostgreSQL. Lokálně se nesmí nic vytvářet a v CLI i v aplikaci musí fungovat naprosto stejně.

**Přínos:**
- jedna implementace pro CLI i worker, takže chování při testu a v provozu se neliší;
- žádný lokální soubor, který by se dal ztratit, musel zálohovat nebo hlídat v gitu;
- už zaplacené odpovědi se převedou, takže se nic neplatí znovu.

Fáze F3, po F1. Podklady:
- `databaze-a-plan-implementace-2026-10-01.md`, tabulky `checks.jev_answers`, `fixes.rewrite_cache`, `shop.page_profiles`;
- změna 5, rozhraní `IJevCache`, `IRewriteCache`, `IPageProfileStore`.

## Scope

In scope:
- `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore` v `EshopGuard.Data` (dosud ve změně 8, přesouvá se sem).
- Úprava tabulek:
  - `checks.jev_answers`: klíč je dnešní `cache_key` (`sha256:…`) a odpověď se ukládá jako `jsonb`, aby šel převod a odpovědi typu choice/score;
  - `fixes.rewrite_cache` a `shop.page_profiles` podle dnešních záznamů.
- CLI napojené na PostgreSQL:
  - připojení přes `ConnectionStrings__Cli` (user-secrets nebo proměnná prostředí), role `eshopguard_worker`;
  - vyhrazený tenant `cli` v lokální nebo testovací databázi.
- Jednorázový převod SQLite → PostgreSQL příkazem `eshopguard cache import`. Čte jen pro čtení a nevytváří `-shm` ani `-wal`.
- Odstranění SQLite z kódu:
  - třídy `SqliteJevCache`, `SqliteRewriteCache`, `SqlitePageProfileStore`;
  - balíček `Microsoft.Data.Sqlite`;
  - `CacheOptions.Path` a `cache.path` v `config/settings.yaml`.
- Test, že CLI nevytvoří žádný soubor ani složku `cache/`.
- Test, že CLI i worker používají tytéž implementace.

Out of scope:
- Ostatní úložiště (`IPageStore`, `IPageContentStore`, `IUrlFrontierStore`, `IRateLimiter`) zůstávají ve změně 8.
- Smazání souboru `src/cache/jev-cache.sqlite`. Rozhodne uživatel po ověření převodu, do té doby slouží jako záloha (v gitu je ignorovaný).

## Approach

1. Změna 5 dodá rozhraní a do této změny nechá SQLite jako přechodnou implementaci, aby se dalo porovnat s dneškem.
2. Tato změna:
   - doplní implementace nad PostgreSQL;
   - přepne na ně CLI;
   - převede data;
   - odstraní SQLite.
3. Klíč cache se počítá stejně jako dnes (`JevCacheKey.LegacyKey`), takže převedené odpovědi se v CLI i ve workeru najdou.
4. Když PostgreSQL není dostupná, CLI skončí **před** prvním placeným voláním Jevu (fail-closed). Bez cache by se platily už zaplacené odpovědi znovu.

## Dependencies

- Změna 3 `add-multitenant-data-model`: tabulky, role, RLS a tenant.
- Změna 5 `refactor-library-into-pipeline-steps`: rozhraní `IJevCache` s `JevCacheKey` a `GetManyAsync`, `IRewriteCache`, `IPageProfileStore`.
- Změna 8 implementace z této změny převezme a nebude je psát znovu.

## Done when

- `eshopguard scan` s `--mock` na testovacím e-shopu i se `--replay` na nahrávce vegis.sk najde v PostgreSQL stejný počet odpovědí v cache jako dnešní kód v SQLite. Nic se neplatí.
- Po běhu CLI neexistuje složka `cache/` ani žádný soubor `*.sqlite`, `*.sqlite-shm` nebo `*.sqlite-wal`.
- Převod:
  - v PostgreSQL je 66 159 odpovědí Jevu a 36 přepisů (podle stavu souboru při převodu);
  - náhodný vzorek 500 odpovědí se po normalizaci JSON shoduje se SQLite;
  - opakovaný převod nic nezdvojí.
- V kódu není `Microsoft.Data.Sqlite` ani třída `Sqlite*`.
- Všechny testy projdou (dnes 190 bez placených) a přibudou testy chování úložišť nad PostgreSQL.
- Test registrace služeb potvrdí, že CLI a worker dostanou tytéž třídy `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`.

## K rozhodnutí

1. **Tenant pro CLI:** navrženo vyhrazený tenant `cli` (slug) v lokální nebo testovací databázi. Proti produkční databázi se CLI nespouští bez výslovného souhlasu.
2. **Klíč `jev_answers`:** navrženo dnešní `cache_key` (`sha256:…`) jako primární klíč s tenantem, `question_set_hash` jako doplňkový sloupec pro úklid. Plán databáze uváděl klíč (`tenant_id`, `question_set_hash`, `state_hash`) a `probabilities real[]`, tento návrh ho mění. Změnu 3 a dokument databáze je potřeba sladit.
3. **Kdy smazat `src/cache/jev-cache.sqlite`:** až po ověření převodu, rozhodne uživatel.
4. **Odpovědi Jevu se mezi tenanty nesdílejí** (cache po tenantovi), i když by sdílení šetřilo. Potvrdit.
