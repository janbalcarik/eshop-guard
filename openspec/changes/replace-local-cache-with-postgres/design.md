# Design: Cache jen v PostgreSQL

## Technical Approach

- Implementace rozhraní ze změny 5 jsou v `src/EshopGuard.Data/Stores/`:

  | Třída | Rozhraní | Tabulka | Poznámka |
  |---|---|---|---|
  | `PgJevCache` | `IJevCache` | `checks.jev_answers` | `GetAsync`, `GetManyAsync` jedním dotazem `cache_key = ANY(@keys)`, `SetAsync` přes `INSERT … ON CONFLICT (tenant_id, cache_key) DO NOTHING` |
  | `PgRewriteCache` | `IRewriteCache` | `fixes.rewrite_cache` | klíč jako dnes (`key`), odpověď `jsonb` |
  | `PgPageProfileStore` | `IPageProfileStore` | `shop.page_profiles` | pro CLI e-shop podle domény v tenantu `cli`, pro worker podle úlohy |

- Registrace přes jedno rozšíření `services.AddEshopGuardPostgresStores(connectionString, tenantResolver)`. Volá ho CLI (`CliHost`) i worker. CLI zjistí tenanta podle slugu `cli`, worker podle úlohy.
- Každé spojení nastaví `SET LOCAL app.tenant_id` (interceptor ze změny 3). Platí RLS jako v aplikaci.

## Architecture Decisions

1. **Klíč cache se nemění.** `jev_answers.cache_key` = dnešní `sha256:…` (`JevCacheKey.LegacyKey`). Převedené odpovědi se tak najdou a CLI i worker počítají klíč stejně. Pro úklid se ukládá i `question_set_hash` a `model`.
2. **Odpověď se ukládá jako `jsonb`** (celý `JevResult`, jako dnes `response_json`), ne jako `real[]`. Unese i odpovědi typu choice a score.
3. **Bez souborů.** Knihovna ani CLI nesmí vytvořit soubor cache. Nastavení `cache.path` a `CacheOptions.Path` zaniká a test kontroluje pracovní složku po běhu.
4. **Fail-closed:** nedostupná databáze nebo chybějící tenant `cli` znamená chybu ještě před odhadem ceny a před voláním Jevu. Nikdy se nepokračuje bez cache.
5. **Převod jen pro čtení:** SQLite se otevře s `Mode=ReadOnly` a `immutable=1`, aby nevznikly `-shm` ani `-wal`. Řádky se zapisují po dávkách 1 000 přes `COPY` do dočasné tabulky a `INSERT … ON CONFLICT DO NOTHING`. Převod je idempotentní.
6. **Převodní příkaz** je jediné místo, kde po této změně zůstane čtení SQLite: samostatná třída `SqliteCacheImporter` v `EshopGuard.Cli` s balíčkem jen tam. Po rozhodnutí uživatele (K rozhodnutí 3) se odstraní i ten.

## Data Flow

```
CLI scan → AddEshopGuardPostgresStores → PgJevCache (tenant cli) → checks.jev_answers
Worker   → AddEshopGuardPostgresStores → PgJevCache (tenant z úlohy) → checks.jev_answers
eshopguard cache import --from src/cache/jev-cache.sqlite → SqliteCacheImporter (jen čtení) → COPY → jev_answers / rewrite_cache (tenant cli)
```

## File Changes

- Nové:
  - `src/EshopGuard.Data/Stores/PgJevCache.cs`, `PgRewriteCache.cs`, `PgPageProfileStore.cs`, `PostgresStoresExtensions.cs`;
  - migrace `src/EshopGuard.Data/Migrations/*_CacheTablesLegacyKey.cs` (úprava `jev_answers`, `rewrite_cache`, `page_profiles`) a SQL pro granty roli `eshopguard_worker`;
  - `src/EshopGuard.Cli/Commands/CacheImportCommand.cs`, `CacheInitCommand.cs` (založí tenant `cli`, pokud chybí), `src/EshopGuard.Cli/Cache/SqliteCacheImporter.cs`;
  - testy `src/tests/EshopGuard.Data.Tests/Stores/PgJevCacheContractTests.cs`, `PgRewriteCacheContractTests.cs`, `PgPageProfileStoreContractTests.cs` (dědí `StoreContractTests` ze změny 5), `CacheImportTests.cs`;
  - testy `src/tests/EshopGuard.Cli.Tests/NoLocalFilesTests.cs`, `SameStoresRegistrationTests.cs`.
- Měněné:
  - `src/EshopGuard.Cli/CliHost.cs` a `CliConfiguration.cs`: připojení `ConnectionStrings__Cli`, tenant `cli`, bez `cache.path`;
  - `src/EshopGuard.Core/Options/*` (bez `CacheOptions.Path`);
  - `src/config/settings.yaml` (bez `cache:`);
  - `README.md`.
- Odstraněné:
  - `src/EshopGuard.Core/Cache/SqliteJevCache.cs`;
  - SQLite části `Fix/RewriteCache.cs` a `Profiles/PageProfileStore.cs`;
  - `PackageReference Microsoft.Data.Sqlite` v `EshopGuard.Core.csproj`.
