# Tasks

## 1. Výchozí stav (před změnou)
- [ ] 1.1 Zapsat počty řádků `cache` a `rewrite_cache` ze `src/cache/jev-cache.sqlite` (otevřít jen pro čtení, `immutable=1`) a otisk SHA-256 souboru do `openspec/changes/replace-local-cache-with-postgres/baseline.md`.
- [ ] 1.2 Zachytit referenční klíče: pro testovací e-shopy `Fixtures/site` a `Fixtures/site-sk` uložit seznam `JevCacheKey.LegacyKey` všech segmentů (test `JevCacheKeyCompatibilityTests` ze změny 5, pokud už existuje, jinak ho vytvořit zde).
- [ ] 1.3 Zapsat počet odpovědí z cache u `eshopguard scan http://localhost:8000 --mock` a u `scan https://vegis.sk --replay snapshots/vegis.sk` (z odhadu ceny, na dotaz odpovědět „ne“, nic se neplatí).

## 2. Databáze
- [ ] 2.1 Migrace `*_CacheTablesLegacyKey`:
  - `checks.jev_answers` s PK (`tenant_id`, `cache_key text`), sloupci `response jsonb`, `model text`, `question_set_hash bytea null`, `created_at timestamptz` a dělením HASH(`tenant_id`) 32;
  - totéž pro `checks.sieve_answers`.
- [ ] 2.2 `fixes.rewrite_cache`: PK (`tenant_id`, `key text`), `answer jsonb`, `model text`, `created_at`.
- [ ] 2.3 `shop.page_profiles`: sloupce podle dnešního `PageProfile` (`site`, `regions jsonb`, `sample_urls jsonb`, `model`, `prompt_version`, `created_at`, `retired_at`).
- [ ] 2.4 RLS FORCE a politiky jako u ostatních tabulek tenanta; granty `SELECT, INSERT` roli `eshopguard_worker` (a `eshopguard_app` jen `SELECT`).
- [ ] 2.5 Do `databaze-a-plan-implementace-2026-10-01.md` zapsat upravené sloupce (K rozhodnutí 2) a sladit se změnou 3.
- [ ] 2.6 Test: migrace proběhne jako `eshopguard_owner` na prázdné i na existující testovací databázi.

## 3. Úložiště nad PostgreSQL
- [ ] 3.1 `src/EshopGuard.Data/Stores/PgJevCache.cs`:
  - `GetAsync`;
  - `GetManyAsync` (jeden dotaz `cache_key = ANY(@keys)`);
  - `SetAsync` (`ON CONFLICT DO NOTHING`);
  - serializace `JevResult` stejným `JsonSerializerOptions` jako dnešní `SqliteJevCache`.
- [ ] 3.2 `src/EshopGuard.Data/Stores/PgRewriteCache.cs` (`IRewriteCache`).
- [ ] 3.3 `src/EshopGuard.Data/Stores/PgPageProfileStore.cs` (`IPageProfileStore`: `GetAsync(site)`, `AddAsync`).
- [ ] 3.4 `src/EshopGuard.Data/Stores/PostgresStoresExtensions.cs`: `AddEshopGuardPostgresStores(connectionString, ITenantResolver)` registruje všechny tři.
- [ ] 3.5 Test: `PgJevCacheContractTests`, `PgRewriteCacheContractTests`, `PgPageProfileStoreContractTests` dědí `StoreContractTests` a běží proti `eshopguard_test` jako `eshopguard_worker`.
- [ ] 3.6 Test izolace: odpověď tenanta A není vidět tenantovi B (EF i čisté SQL).

## 4. CLI napojené na PostgreSQL
- [ ] 4.1 `CliConfiguration.cs`:
  - načíst `ConnectionStrings__Cli` (proměnná prostředí nebo `dotnet user-secrets` projektu `EshopGuard.Cli`, nikdy `settings.yaml`);
  - slug tenanta `cli`.
- [ ] 4.2 `CliHost.cs`: volat `AddEshopGuardPostgresStores` místo registrace SQLite; tenant `cli` podle slugu.
- [ ] 4.3 Příkaz `eshopguard cache init`: založí tenanta `cli`, pokud chybí (jako `eshopguard_owner` nebo přes funkci `SECURITY DEFINER`), a vypíše stav.
- [ ] 4.4 Kontrola před během: nedostupná databáze nebo chybějící tenant `cli` znamená chybu s návratovým kódem ≠ 0 ještě před odhadem ceny. Test s neplatným připojením ověří, že `MockJevClient` nebyl zavolán.
- [ ] 4.5 Test `SameStoresRegistrationTests`: kontejner CLI i workeru vrací `PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`.

## 5. Převod dnešní cache
- [ ] 5.1 `src/EshopGuard.Cli/Cache/SqliteCacheImporter.cs`:
  - otevření `Mode=ReadOnly` s `immutable=1`;
  - čtení po dávkách 1 000;
  - zápis přes `COPY` do dočasné tabulky a `INSERT … ON CONFLICT DO NOTHING`.
- [ ] 5.2 Příkaz `eshopguard cache import --from <soubor>`: na konci počty převedených, přeskočených (už existujících) a nepřevedených s klíči; ověření náhodného vzorku 500 odpovědí po normalizaci JSON.
- [ ] 5.3 Test `CacheImportTests`:
  - malý soubor SQLite vytvořený testem v dočasné složce;
  - úplný převod, opakovaný převod (0 nových), řádek s neplatným JSON (přeskočen a vypsán);
  - po testu nevzniklo `-shm` ani `-wal`.
- [ ] 5.4 Převést skutečnou cache: `eshopguard cache import --from src/cache/jev-cache.sqlite`; počty se musí shodovat s 1.1. Žádné placené volání.

## 6. Odstranění SQLite
- [ ] 6.1 Smazat:
  - `src/EshopGuard.Core/Cache/SqliteJevCache.cs`;
  - SQLite implementace v `Fix/RewriteCache.cs` a `Profiles/PageProfileStore.cs`;
  - jejich registrace.
- [ ] 6.2 Odebrat `Microsoft.Data.Sqlite` z `EshopGuard.Core.csproj`; v `EshopGuard.Cli.csproj` ho ponechat jen pro převod (K rozhodnutí 3).
- [ ] 6.3 Odebrat `CacheOptions.Path` a oddíl `cache:` ze `src/config/settings.yaml`; upravit komentáře, které o souboru cache mluví (oddíl síta v `settings.yaml`).
- [ ] 6.4 `README.md`:
  - cache je v PostgreSQL;
  - nastavení `ConnectionStrings__Cli`;
  - příkazy `cache init` a `cache import`.
- [ ] 6.5 Test `NoLocalFilesTests`: běh `scan --mock` na testovacím e-shopu v dočasné pracovní složce. Po běhu ve složce není `cache/` ani žádný `*.sqlite*`.

## 7. Ověření
- [ ] 7.1 `dotnet build src/EshopGuard.sln` bez chyb a varování.
- [ ] 7.2 Testy bez placených: `EshopGuard.Core.Tests -notrait "Category=Jev"` (dnes 190) a nové testy `EshopGuard.Data.Tests` a `EshopGuard.Cli.Tests` projdou.
- [ ] 7.3 `scan --mock` na testovacím e-shopu a `scan https://vegis.sk --replay …`: počet odpovědí z cache je stejný jako v 1.3 a cena 0 USD.
- [ ] 7.4 Počty v PostgreSQL odpovídají 1.1. Po všech bězích neexistuje `cache/` ani `*.sqlite` mimo původní záložní soubor.
- [ ] 7.5 `DO_NOT_TRACK=1 openspec validate replace-local-cache-with-postgres --no-interactive` projde.
- [ ] 7.6 Uživateli předat rozhodnutí K 3 (smazat záložní `src/cache/jev-cache.sqlite` a převodní příkaz).
