# Design: Cache jen v PostgreSQL

## Technical Approach

- Implementace rozhraní ze změny 5 jsou v `src/EshopGuard.Data/Stores/`:

  | Třída | Rozhraní | Tabulka | Poznámka |
  |---|---|---|---|
  | `PgJevCache` | `IJevCache` | `checks.jev_answers` (druh `Detail`), `checks.sieve_answers` (druh `Sieve`) | `GetAsync`, `GetManyAsync` jedním dotazem `cache_key = ANY($1)` na tabulku, `SetAsync` přes `INSERT … ON CONFLICT (tenant_id, cache_key) DO NOTHING` |
  | `PgRewriteCache` | `IRewriteCache` | `fixes.rewrite_cache` | klíč jako dnes (`key`), odpověď `jsonb` |
  | `PgPageProfileStore` | `IPageProfileStore` | `shop.page_profiles` | e-shop podle domény (klíč webu) v tenantu; CLI ho založí, když chybí |

- Každá operace otevře spojení z poolu role `eshopguard_worker` a transakci s kontextem tenanta (`TenantSql.BeginAsync`). Platí RLS jako v aplikaci; čisté SQL místo EF, protože dávkové čtení tisíců klíčů je jeden dotaz.
- Registrace jedním rozšířením `services.AddEshopGuardPostgresStores(Func<IServiceProvider, IStoreTenant>)`. CLI předá pevného tenanta `cli`, worker (změna 8) tenanta úlohy.
- Tenant `cli` má pevné ID `00000000-0000-0000-0000-0000000000c1`. Založí ho příkaz `eshopguard cache init` funkcí `iam.ensure_cli_tenant()` (`SECURITY DEFINER`, spustit smí jen `eshopguard_worker`), protože role workeru do `iam.tenants` zapisovat nesmí. Běh CLI tenanta nezakládá: proti cizí databázi se tak nic nevytvoří bez výslovného příkazu.
- Připojení CLI: `ConnectionStrings:Cli` z proměnné prostředí `ConnectionStrings__Cli`, ze souboru `.env` nebo z user-secrets projektu `EshopGuard.Cli`, nikdy ze `settings.yaml`. Před během CLI ověří přes `DatabaseInspector` spojení, roli (ne superuživatel, ne `BYPASSRLS`), použité migrace a existenci tenanta `cli`.

## Architecture Decisions

1. **Klíč cache se nemění.** `cache_key` = dnešní `sha256:…` (`JevCacheKey.LegacyKey`), CLI i worker ho počítají stejnou knihovnou. Pro úklid se ukládá i `question_set_hash` a `model`.
2. **Odpověď se ukládá jako `jsonb`** (celý `JevResult`), ne jako `real[]`. Unese i odpovědi typu choice a score. Totéž platí pro `checks.sieve_answers`: tabulka dostane stejný tvar jako `jev_answers` (dosud `question_set_hash`, `chunk_hash`, `probabilities real[]`); je prázdná, takže úprava nic nepřevádí.
3. **Bez souborů.** Knihovna ani CLI nesmí vytvořit soubor cache. Nastavení `cache.path` a `CacheOptions.Path` zaniká a test kontroluje pracovní složku po běhu.
4. **Fail-closed:** nedostupná databáze, chybějící migrace nebo chybějící tenant `cli` znamená chybu ještě před stažením, odhadem ceny a voláním Jevu. Nikdy se nepokračuje bez cache. `--no-cache` vypne jen odpovědi Jevu a přepisy; profily šablon jsou šablona obchodu, ne cache, a čtou se z databáze dál.
5. **`--mock` bez databáze.** Falešný klient Jevu i přepisu má vymyšlené odpovědi, které se nikdy nesmí dostat do cache (jako dnes `NullJevCache`). Běh s `--mock` se proto k databázi nepřipojuje vůbec: cache je vypnutá a profily šablon jsou jen v paměti běhu. Testy výstupů CLI (`Snapshot`, `PipelineEquivalence`) tak dál běží bez databáze.
6. **Výchozí úložiště knihovny** bez hostitele: `NullJevCache`, `NullRewriteCache`, `InMemoryPageProfileStore`. SQLite v knihovně není; host, který chce cache, zaregistruje úložiště PostgreSQL před `AddEshopGuard`.
7. **Data se nepřevádějí** (rozhodnutí uživatele 1. 10. 2026). Soubor `src/cache/jev-cache.sqlite` po změně kód nečte; smazání rozhodne uživatel.

## Data Flow

```
CLI scan (bez --mock) → kontrola databáze a tenanta cli → AddEshopGuardPostgresStores → PgJevCache (tenant cli) → checks.jev_answers / sieve_answers
CLI scan --mock       → bez databáze: NullJevCache, NullRewriteCache, profily v paměti
Worker (změna 8)      → AddEshopGuardPostgresStores → PgJevCache (tenant z úlohy) → checks.jev_answers / sieve_answers
eshopguard cache init → SELECT iam.ensure_cli_tenant() → iam.tenants (pevné ID)
```

## File Changes

- Nové:
  - `src/EshopGuard.Data/Stores/PgJevCache.cs`, `PgRewriteCache.cs`, `PgPageProfileStore.cs`, `PostgresStoresExtensions.cs` (`IStoreTenant`, `CliTenant`);
  - migrace `src/EshopGuard.Data/Migrations/*_F3CacheInPostgres.cs` (tvar `checks.sieve_answers`, funkce `iam.ensure_cli_tenant()` a grant);
  - `src/EshopGuard.Cli/Commands/CacheInitCommand.cs`, `src/EshopGuard.Cli/CliDatabase.cs` (připojení, kontrola před během);
  - `src/EshopGuard.Core/Profiles/PageProfileStore.cs`: `InMemoryPageProfileStore`;
  - testy `src/tests/EshopGuard.Data.Tests/Stores/PgStoreContractTests.cs` (dědí společné testy ze změny 5), `PgStoreIsolationTests.cs`, `CliTenantTests.cs`;
  - testy `src/tests/EshopGuard.Cli.Tests/NoLocalFilesTests.cs`, `SameStoresRegistrationTests.cs`, `DatabaseCheckTests.cs`.
- Měněné:
  - `src/EshopGuard.Cli/CliHost.cs` a `CliConfiguration.cs`: připojení `ConnectionStrings:Cli`, tenant `cli`, bez `cache.path`;
  - `src/EshopGuard.Cli/EshopGuard.Cli.csproj`: odkaz na `EshopGuard.Data`, `UserSecretsId`;
  - `src/tests/EshopGuard.Api.Tests/ProjectReferenceTests.cs`: CLI smí odkazovat `EshopGuard.Data`;
  - `src/EshopGuard.Core/Options/EshopGuardOptions.cs` (bez `CacheOptions.Path`), `ServiceCollectionExtensions.cs` (výchozí úložiště bez SQLite);
  - `src/config/settings.yaml` (bez `cache:`);
  - `deploy/dev/setup-local.ps1`, `scripts/cloud-setup.sh` (user-secrets `ConnectionStrings:Cli`, `cache init`);
  - `README.md`, `CLAUDE.md`.
- Odstraněné:
  - `src/EshopGuard.Core/Cache/SqliteJevCache.cs`;
  - SQLite části `Fix/RewriteCache.cs` a `Profiles/PageProfileStore.cs`;
  - `PackageReference Microsoft.Data.Sqlite` ze všech projektů a z `Directory.Packages.props`.

## Odchylky při implementaci (1. 10. 2026)

- **Převod dat vypuštěn** (rozhodnutí uživatele): bez příkazu `cache import`, bez `SqliteCacheImporter`, bez balíčku SQLite v CLI. Původní skupina úkolů 5 zmizela.
- **První odpověď klíče zůstává.** `IJevCache.SetAsync` dřív (SQLite) odpověď přepsal; nově platí první uložená odpověď (`ON CONFLICT DO NOTHING`), takže se nálezy nemění potichu. Přepis (`IRewriteCache`) se dál přepisuje jako dosud.
- **Stará data `sieve_answers`** (jen z testů izolace, tvar bez id otázek) migrace vyprázdní (`TRUNCATE`, RLS ho neomezuje); v produkci tabulka prázdná je.
- **Granty beze změny.** Role `eshopguard_app` a `eshopguard_worker` mají na tabulkách cache práva ze změny 3 (včetně `UPDATE` a `DELETE` pro úklid a smazání dat tenanta); zúžení na `SELECT, INSERT` by kolidovalo s úklidem.
- **Kontrola před během** používá `DatabaseInspector` ze změny 2 (spojení, role bez `BYPASSRLS`, migrace) a pak hledá tenanta `cli`.
- **Výstup CLI do roury:** bez konzole měl Spectre.Console nulovou šířku a každý řádek zkrátil na „…“. Při přesměrovaném výstupu je šířka 200 znaků, aby šly testovat hlášky CLI.
- **Test `Snapshot`** nečte soubor `extraction-hashes.txt`, který ve složce referenčních výstupů nechala změna 5 (sken ho nepíše).
