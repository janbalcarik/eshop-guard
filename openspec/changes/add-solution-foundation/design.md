# Design: Základ řešení

## Technical Approach

Cesty jsou relativní ke kořeni `D:\_github\Overko\eshop-guard` (po změně 1).

### Projekty

| Projekt | SDK | Obsah ve F0 | Balíčky (verze centrálně v `Directory.Packages.props`) |
|---|---|---|---|
| `src/EshopGuard.Data` | `Microsoft.NET.Sdk` | `EshopGuardDb`, registrace do DI, pojistka rolí, návrhová továrna, migrace `Initial` | `Microsoft.EntityFrameworkCore` 10.0.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.x, `Microsoft.EntityFrameworkCore.Design` 10.0.x (`PrivateAssets=all`), `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Configuration.UserSecrets`, `Microsoft.Extensions.Configuration.EnvironmentVariables` |
| `src/EshopGuard.Storage` | `Microsoft.NET.Sdk` | `IBlobStore`, `BlobKey`, `FileSystemBlobStore`, `S3BlobStore`, `StorageOptions` | `AWSSDK.S3` 4.x, `Microsoft.Extensions.Options.ConfigurationExtensions` |
| `src/EshopGuard.Jobs` | `Microsoft.NET.Sdk` | prázdný (změna 4) | – |
| `src/EshopGuard.Billing` | `Microsoft.NET.Sdk` | prázdný (změna 12) | – |
| `src/EshopGuard.Connectors` | `Microsoft.NET.Sdk` | prázdný (změna 15) | – |
| `src/EshopGuard.Api` | `Microsoft.NET.Sdk.Web` | `Program.cs`, `/health`, ProblemDetails s kódy | – (sdílený framework ASP.NET Core) |
| `src/EshopGuard.Worker` | `Microsoft.NET.Sdk.Worker` | `Program.cs`, `WorkerSkeletonService`, `WorkerOptions` | `Microsoft.Extensions.Hosting` |
| `tests/EshopGuard.Data.Tests` | test (xUnit v3, exe) | role, databáze, migrace | `Npgsql` |
| `tests/EshopGuard.Storage.Tests` | test | smlouva `IBlobStore` pro FS i S3 | – |
| `tests/EshopGuard.Api.Tests` | test | `/health`, pojistka rolí, hygiena tajemství, směr závislostí | `Microsoft.AspNetCore.Mvc.Testing` 10.0.x |
| `tests/EshopGuard.Worker.Tests` | test | start a ukončení kostry workeru | – |

`Microsoft.Extensions.*` zůstávají na verzi 10.0.12, kterou dnes používá `EshopGuard.Core`; EF Core a Npgsql se zvolí v nejnovější opravné verzi 10.0, která je s ní kompatibilní (ověřit při implementaci, síť teď nepoužívám).

### Směr závislostí

| Projekt | Smí odkazovat na (`ProjectReference`) |
|---|---|
| `EshopGuard.Core` | žádný projekt `EshopGuard.*` |
| `EshopGuard.Cli` | `Core` |
| `EshopGuard.Storage` | žádný projekt `EshopGuard.*` |
| `EshopGuard.Data` | `Core` |
| `EshopGuard.Jobs` | `Core`, `Data`, `Storage` |
| `EshopGuard.Billing` | `Data`, `Jobs` |
| `EshopGuard.Connectors` | `Core`, `Data`, `Storage`, `Jobs` |
| `EshopGuard.Api` | `Data`, `Storage`, `Jobs`, `Billing`, `Connectors` |
| `EshopGuard.Worker` | `Core`, `Data`, `Storage`, `Jobs`, `Billing`, `Connectors` |
| `tests/*` | testovaný projekt a projekty, na které smí odkazovat on |

Na `Api`, `Worker` a `Cli` neodkazuje žádný projekt kromě jejich testů. `Jobs` neodkazuje na `Billing` a `Connectors`: obsluhy úloh z těchto knihoven registruje až `Worker`, takže nevznikne cyklus. `Cli` nesmí na `Data` (CLI běží v paměti, podklad část 7).

### Role a databáze (`deploy/sql/00_roles.sql`)

Spuštění (dělá `deploy/dev/setup-local.ps1`):

```powershell
$env:PGPASSWORD = 'postgres'                 # jen v této relaci, jen pro tento skript
$env:ESHOPGUARD_OWNER_PASSWORD = '<vygenerováno>'   # ... APP, WORKER, ADMIN, CMS
& "$PgBin\psql.exe" -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -v db_name=eshopguard -f deploy/sql/00_roles.sql
```

Obsah skriptu po krocích:
1. `\set ON_ERROR_STOP on`; ověření `current_setting('is_superuser') = 'on'` a parametru `db_name` (`\if :{?db_name}`); jinak `DO $$ BEGIN RAISE EXCEPTION … $$` a konec s chybou.
2. Načtení hesel: `\getenv owner_password ESHOPGUARD_OWNER_PASSWORD` (a další čtyři); chybějící heslo = výjimka. Hesla se do souboru ani do výstupu nedostanou.
3. Role (idempotentně přes `SELECT format(...) WHERE NOT EXISTS (SELECT 1 FROM pg_roles …) \gexec`, potom vždy `ALTER ROLE`, které vynutí atributy a nastaví heslo):

   | Role | Atributy | Účel |
   |---|---|---|
   | `eshopguard_owner` | `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS` | vlastník databáze a objektů, migrace |
   | `eshopguard_app` | totéž | API, podléhá RLS |
   | `eshopguard_worker` | totéž | worker, podléhá RLS |
   | `eshopguard_admin` | `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION BYPASSRLS` | podpora, obchází RLS |
   | `eshopguard_cms` | `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS` | Payload CMS, jen schéma `cms` |

4. Databáze `:db_name` (jen pokud neexistuje): `CREATE DATABASE … OWNER eshopguard_owner TEMPLATE template0 ENCODING 'UTF8' LOCALE_PROVIDER builtin BUILTIN_LOCALE 'C.UTF-8'` (locale podle K rozhodnutí 2 v proposal).
5. `REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC`; `GRANT CONNECT ON DATABASE :"db_name" TO eshopguard_app, eshopguard_worker, eshopguard_admin, eshopguard_cms`.
6. `\connect :"db_name"`; `REVOKE ALL ON SCHEMA public FROM PUBLIC`; `CREATE SCHEMA IF NOT EXISTS cms AUTHORIZATION eshopguard_cms`; `REVOKE ALL ON SCHEMA cms FROM PUBLIC`.
7. Schémata aplikace (`iam`, `shop`, …, `ops`) skript nezakládá: vytváří je migrace jako `eshopguard_owner`, který jako vlastník databáze má právo `CREATE`.

Skript nikdy nepracuje s jinou databází než `:db_name` a `postgres` (připojení); databáze `eia_registry` se ho netýká.

### Připojení a konfigurace

| Klíč | Kdo čte | Role | Lokálně |
|---|---|---|---|
| `ConnectionStrings:App` | `EshopGuard.Api` | `eshopguard_app` | user-secrets `eshopguard-api` |
| `ConnectionStrings:Worker` | `EshopGuard.Worker` | `eshopguard_worker` | user-secrets `eshopguard-worker` |
| `ConnectionStrings:Migrations` | `EshopGuardDbDesignTimeFactory`, `efbundle` | `eshopguard_owner` | user-secrets `eshopguard-data` |
| `ConnectionStrings:Owner`, `:App`, `:Worker`, `:Admin`, `:Cms` | testovací projekty (databáze `eshopguard_test`) | podle názvu | user-secrets `eshopguard-tests`, nebo proměnné `ESHOPGUARD_TEST_ConnectionStrings__…` |
| `Storage:Provider` | Api, Worker, testy | – | `S3` (Development), `FileSystem` (testy) |
| `Storage:S3:ServiceUrl`, `:Region`, `:Bucket`, `:ForcePathStyle` | Api, Worker | – | `appsettings.Development.json` (`http://localhost:9000`, `us-east-1`, `eshopguard-dev`, `true`) |
| `Storage:S3:AccessKey`, `:SecretKey` | Api, Worker | – | user-secrets |
| `Storage:FileSystem:Root` | Api, Worker, testy | – | `.data/blobs` (v `.gitignore`) |
| `Worker:Id`, `Worker:ShutdownSeconds` | Worker | – | výchozí `{MachineName}:{ProcessId}`, 90 |

Řetězec připojení: `Host=localhost;Port=5432;Database=eshopguard;Username=eshopguard_app;Password=…;Application Name=eshopguard-api`. `Include Error Detail` zůstává vypnuté (jinak by chybové zprávy PostgreSQL nesly hodnoty řádků, tedy i texty zákazníků) a `EnableSensitiveDataLogging` se v Api ani Worker nikdy nezapíná.

Očekávaná role je v kódu hostitele (`DatabaseRole.App` v Api, `DatabaseRole.Worker` ve Workeru), ne v konfiguraci, aby ji nešlo konfigurací povolit.

### Pojistka při startu (`DatabaseStartupGuard`)

`IHostedService` v `EshopGuard.Data`, registrovaný jako první hostovaná služba (`AddEshopGuardData` ji přidá před ostatní). Ve `StartAsync`:
1. chybí `ConnectionStrings:{name}` → `EshopGuardConfigurationException` s kódem `config.connection_string_missing` a jménem klíče (bez hodnoty);
2. `SELECT current_user, r.rolsuper, r.rolbypassrls FROM pg_roles r WHERE r.rolname = current_user`;
3. `DatabaseRoleGuard.Evaluate(info, expected)` (čistá funkce, testovaná bez databáze):
   - `rolsuper` nebo `rolbypassrls` (u jiné než `DatabaseRole.Admin`) → `db.role_bypasses_rls`;
   - `current_user` ≠ očekávaná role → `db.unexpected_role`;
4. `MigrationStatus.Evaluate(applied, known)`: chybí-li v databázi migrace, kterou kód zná → `db.migrations_pending`; migrace navíc v databázi (starší kód po vrácení verze) se jen zapíše do logu jako `db.migrations_ahead`;
5. nedostupná databáze → `db.unreachable`.

Výjimka ve `StartAsync` zastaví hostitele dřív, než Kestrel začne poslouchat; proces skončí nenulovým kódem a log obsahuje jen kód, typ výjimky a název klíče.

### `GET /health`

```json
{ "status": "ok", "checks": [
  { "name": "database", "status": "ok" },
  { "name": "database_role", "status": "ok" },
  { "name": "migrations", "status": "ok" },
  { "name": "storage", "status": "ok" } ] }
```

- `status`: `ok` / `failed`; HTTP 200 jen když jsou všechny kontroly `ok`, jinak 503.
- Neúspěšná kontrola má `code` (`db.unreachable`, `db.role_bypasses_rls`, `db.unexpected_role`, `db.migrations_pending`, `storage.unreachable`, `storage.bucket_missing`), nikdy text výjimky, řetězec připojení ani název hostitele.
- Implementace: ASP.NET Core Health Checks (`AddHealthChecks().AddCheck<DatabaseHealthCheck>("database")` …) a vlastní `ResponseWriter` v `Health/HealthResponseWriter.cs`. Ukazatele fronty a disku doplní změna 17.

### Úložiště (`EshopGuard.Storage`)

```csharp
public interface IBlobStore
{
    Task PutAsync(BlobKey key, Stream content, string contentType, CancellationToken ct = default); // přepíše existující
    Task<Stream?> OpenReadAsync(BlobKey key, CancellationToken ct = default);                    // null = neexistuje
    Task<bool> ExistsAsync(BlobKey key, CancellationToken ct = default);
    Task DeleteAsync(BlobKey key, CancellationToken ct = default);                               // neexistující = bez chyby
    Task<int> DeletePrefixAsync(BlobKey prefix, CancellationToken ct = default);                 // smazání tenanta
    Task<Uri?> GetReadUrlAsync(BlobKey key, TimeSpan validFor, CancellationToken ct = default);  // null = nepodporováno (FS)
}
```

- `BlobKey.ForShop(Guid tenantId, Guid shopId, params string[] segments)` → `tenants/{tenantId:D}/shops/{shopId:D}/{segments…}`; `BlobKey.ForTenant(Guid tenantId, …)`; `BlobKey.Prefix(...)` pro mazání. Část klíče musí odpovídat `^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$` a nesmí být `.` ani `..`; jinak `ArgumentException`. Celý klíč nejvýš 1 024 znaků.
- `FileSystemBlobStore`: kořen `Storage:FileSystem:Root`; zápis do dočasného souboru ve stejné složce a `File.Move(overwrite: true)` (žádný napůl zapsaný soubor); kontrola, že výsledná cesta je pod kořenem; `GetReadUrlAsync` vrací `null` (API pak soubor streamuje).
- `S3BlobStore`: `AmazonS3Client` s `ServiceURL`, `ForcePathStyle`, `RequestChecksumCalculation = WHEN_REQUIRED` a `ResponseChecksumValidation = WHEN_REQUIRED` (novější AWS SDK posílají kontrolní součty, které některá úložiště kompatibilní s S3 nepřijímají; ověřit proti zvolené verzi MinIO); `GetReadUrlAsync` přes `GetPreSignedURL` s platností `validFor`, nejvýš `Storage:MaxSignedUrlMinutes` (návrh 15, nastavitelné). Bucket je soukromý, žádná veřejná politika.

### Lokální MinIO (`deploy/docker-compose.dev.yml`)

- Služba `minio` (obraz s pevnou značkou, ne `latest`), porty jen na `127.0.0.1:9000` (API) a `127.0.0.1:9001` (konzole), svazek `minio-data`, `MINIO_ROOT_USER` a `MINIO_ROOT_PASSWORD` jen jako `${…:?chybí v deploy/.env}`.
- Služba `minio-init` (obraz `minio/mc`) po zdravém startu spustí `deploy/minio/minio-init.sh`: buckety `eshopguard-dev` a `eshopguard-test`, politika `eshopguard-rw` (`deploy/minio/eshopguard-rw-policy.json`: `s3:GetObject`, `s3:PutObject`, `s3:DeleteObject`, `s3:ListBucket` jen na tyto dva buckety), uživatel aplikace `ESHOPGUARD_S3_ACCESS_KEY` / `ESHOPGUARD_S3_SECRET_KEY` s touto politikou. Skript je idempotentní.
- `deploy/.env` (v `.gitignore`) vytvoří `setup-local.ps1`; v repozitáři je jen `deploy/.env.example` s prázdnými hodnotami.

### `dotnet test`

`global.json` má `"test": { "runner": "Microsoft.Testing.Platform" }`, takže `dotnet test` (SDK 10) běží v režimu MTP a spouští jen projekty, které jsou aplikacemi MTP. Projekty xUnit v3 se jimi stanou vlastností `UseMicrosoftTestingPlatformRunner=true` (hypotéza, ověří úkol 9.1). Vlastnost se nastaví v `tests/Directory.Build.props` pro všechny testovací projekty včetně `EshopGuard.Core.Tests`. Kategorie testů (`[Trait("Category", …)]`):
- `Db`: potřebují PostgreSQL a databázi `eshopguard_test`;
- `S3`: potřebují MinIO;
- `Jev`: placené volání Jevu (už dnes).

Bez konfigurace připojení test kategorie `Db` **selže** se zprávou, který klíč chybí (ne přeskočí): nic se tiše nevynechá. Vynechat se dají jen výslovně filtrem.

## Architecture Decisions

1. **Jedna databáze, schémata, pět rolí** podle podkladu (část 2). Vlastník databáze je `eshopguard_owner`, takže migrace nepotřebují superuživatele a `postgres` zná jen jednorázový skript.
2. **Hesla přes `\getenv`, ne `-v`.** Parametry `psql -v` jsou vidět v seznamu procesů; proměnné prostředí procesu `psql` ne. Do user-secrets se připojení zapisují přes standardní vstup (`… | dotnet user-secrets set --project …`), ne jako argument.
3. **Pojistka rolí v kódu hostitele, ne v konfiguraci.** Superuživatel obchází RLS i s `FORCE ROW LEVEL SECURITY` (podklad, část 2). Kdyby se aplikace spustila pod `postgres`, testy izolace by procházely naprázdno. Proto odmítnutí při startu a test, který to dokazuje.
4. **Historie migrací v `ops.__ef_migrations_history`**, ne ve `public`: schéma `public` zůstane bez práv pro `PUBLIC`, aplikační role dostanou jen `SELECT` na tabulku historie (kontrola neaplikovaných migrací při startu). EF Core 9+ při migraci zamyká tabulku historie, takže souběžné migrace z více testovacích projektů jsou bezpečné.
5. **Centrální verze balíčků** (`Directory.Packages.props`, `ManagePackageVersionsCentrally`): deset projektů musí mít stejné verze `Microsoft.Extensions.*`, EF Core a Npgsql, jinak NuGet hlásí snížení verze (NU1605). `EshopGuard.Core` a `EshopGuard.Cli` přijdou o atribut `Version` u `PackageReference`, verze zůstanou stejné.
6. **AWS SDK pro .NET místo klienta MinIO:** produkční úložiště je S3 u poskytovatele (architektura 7.1), MinIO je jen lokální náhrada. Jedno rozhraní, jedna implementace pro oba.
7. **Testovací databáze `eshopguard_test`** se stejnými rolemi (proposal, K rozhodnutí 1). Testy nikdy nesahají do `eshopguard`.
8. **Selhání místo přeskočení** u testů bez prostředí (kategorie `Db`, `S3`): zásada fail-closed. Výslovné vynechání: `--filter-not-trait "Category=Db"` (syntaxe podle úkolu 9.1).
9. **Kostra workeru je opravdu kostra:** jen start, kontrola role, `ops.workers` ještě neexistuje (změna 3) a obsluhu úloh doplní změna 4. Korektní ukončení se ale testuje už teď, protože na něm stojí nasazení bez ztráty dávek (`HostOptions.ShutdownTimeout` = `Worker:ShutdownSeconds`, Compose `stop_grace_period` ve změně 17 musí být delší).

## Data Flow

```
setup-local.ps1
  ├─ vygeneruje 5 hesel (RandomNumberGenerator) a heslo MinIO
  ├─ psql 00_roles.sql (db_name=eshopguard)       → role, databáze eshopguard, schéma cms
  ├─ psql 00_roles.sql (db_name=eshopguard_test)  → databáze eshopguard_test
  ├─ dotnet user-secrets (stdin)                   → eshopguard-data / -api / -worker / -tests
  └─ deploy/.env                                   → MinIO root + uživatel aplikace

dotnet ef database update (ConnectionStrings:Migrations, eshopguard_owner)
  → CREATE SCHEMA ops, ops.__ef_migrations_history, migrace Initial (GRANT USAGE ON SCHEMA ops, GRANT SELECT na historii)

EshopGuard.Api start
  → konfigurace (appsettings + user-secrets v Development + proměnné prostředí)
  → NpgsqlDataSource (ConnectionStrings:App)
  → DatabaseStartupGuard: role = eshopguard_app, ne super, ne BYPASSRLS, žádná čekající migrace
  → Kestrel → GET /health → database, database_role, migrations, storage → 200 / 503 s kódy

EshopGuard.Worker start
  → DatabaseStartupGuard (eshopguard_worker) → WorkerSkeletonService (log worker.started {id})
  → SIGTERM / Ctrl+C → WorkerSkeletonService.StopAsync (log worker.stopped) do Worker:ShutdownSeconds

Zápis souboru
  → BlobKey.ForShop(tenant, shop, "pages", "…") → IBlobStore.PutAsync → MinIO (vývoj) / S3 (server) / souborový systém (testy)
```

## File Changes

Kořen řešení:
- `EshopGuard.sln`: přidat projekty do složek `src` a `tests` (`dotnet sln add`).
- `Directory.Packages.props` (nový): `ManagePackageVersionsCentrally=true`, verze všech balíčků včetně dnešních z `EshopGuard.Core`, `EshopGuard.Cli` a `EshopGuard.Core.Tests`.
- `src/EshopGuard.Core/EshopGuard.Core.csproj`, `src/EshopGuard.Cli/EshopGuard.Cli.csproj`, `tests/EshopGuard.Core.Tests/EshopGuard.Core.Tests.csproj`: odstranit `Version` z `PackageReference`.
- `.config/dotnet-tools.json` (nový): `dotnet-ef` 10.0.x.
- `.gitignore`: doplnit `.data/`, `deploy/.env` (`.env` už platí na všech úrovních, doplnit výslovně kvůli čitelnosti), `*.efbundle`, `efbundle.exe`.
- `README.md`: oddíly „Lokální databáze a úložiště“ (setup-local, Compose, migrace), „Testy“ (`dotnet test`, kategorie `Db`, `S3`, `Jev` a filtry).

`src/EshopGuard.Data/`:
- `EshopGuard.Data.csproj` (`UserSecretsId` = `eshopguard-data`, `ProjectReference` na `EshopGuard.Core`).
- `EshopGuardDb.cs`: `public sealed class EshopGuardDb(DbContextOptions<EshopGuardDb> options) : DbContext(options)`; ve F0 bez `DbSet`.
- `DataServiceCollectionExtensions.cs`: `AddEshopGuardData(this IServiceCollection services, IConfiguration configuration, DatabaseRole role)` → `NpgsqlDataSource` (název klíče podle role, `Application Name`), `AddDbContext<EshopGuardDb>(… UseNpgsql(dataSource, o => o.MigrationsHistoryTable("__ef_migrations_history", "ops")))`, `DatabaseStartupGuard` jako první `IHostedService`.
- `Connections/DatabaseRole.cs` (`Owner`, `App`, `Worker`, `Admin`, s mapováním na název role a klíč připojení).
- `Connections/DatabaseRoleGuard.cs` (`RoleInfo`, `Evaluate`), `Connections/MigrationStatus.cs`, `Connections/DatabaseStartupGuard.cs`.
- `EshopGuardConfigurationException.cs` (vlastnost `Code`).
- `Design/EshopGuardDbDesignTimeFactory.cs`: `IDesignTimeDbContextFactory<EshopGuardDb>`, čte `ConnectionStrings:Migrations` z proměnných prostředí a user-secrets `eshopguard-data`; když chybí, výjimka `config.connection_string_missing`.
- `Migrations/<timestamp>_Initial.cs`, `.Designer.cs`, `EshopGuardDbModelSnapshot.cs`: `Up` = `GRANT USAGE ON SCHEMA ops TO eshopguard_app, eshopguard_worker, eshopguard_admin; GRANT SELECT ON ops.__ef_migrations_history TO …`; `Down` = `REVOKE`.

`src/EshopGuard.Storage/`:
- `EshopGuard.Storage.csproj`, `IBlobStore.cs`, `BlobKey.cs`, `FileSystemBlobStore.cs`, `S3BlobStore.cs`, `StorageOptions.cs` (`Provider`, `FileSystem`, `S3`, `MaxSignedUrlMinutes`), `StorageServiceCollectionExtensions.cs` (`AddEshopGuardStorage(IConfiguration)`; neznámý `Provider` → `EshopGuardConfigurationException` `config.storage_provider_invalid`), `Health/StorageHealthCheck.cs` (FS: zápis a smazání zkušebního souboru `health/probe`; S3: `HeadBucket`).

`src/EshopGuard.Jobs/EshopGuard.Jobs.csproj`, `src/EshopGuard.Billing/EshopGuard.Billing.csproj`, `src/EshopGuard.Connectors/EshopGuard.Connectors.csproj`: jen projekt s odkazy podle tabulky směru závislostí.

`src/EshopGuard.Api/`:
- `EshopGuard.Api.csproj` (`UserSecretsId` = `eshopguard-api`), `Program.cs` (`AddEshopGuardData(…, DatabaseRole.App)`, `AddEshopGuardStorage`, `AddProblemDetails` s rozšířením `code`, `AddHealthChecks`, `MapHealthChecks("/health", …)`, JSON log na konzoli), `Health/DatabaseHealthCheck.cs`, `Health/HealthResponseWriter.cs`, `appsettings.json` (bez `ConnectionStrings`), `appsettings.Development.json` (`Storage` pro MinIO bez klíčů), `Properties/launchSettings.json` (`http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development`).

`src/EshopGuard.Worker/`:
- `EshopGuard.Worker.csproj` (`UserSecretsId` = `eshopguard-worker`), `Program.cs` (`Host.CreateApplicationBuilder`, `AddEshopGuardData(…, DatabaseRole.Worker)`, `AddEshopGuardStorage`, `Configure<HostOptions>(o => o.ShutdownTimeout = …)`, `AddHostedService<WorkerSkeletonService>()`), `WorkerOptions.cs`, `WorkerSkeletonService.cs`, `appsettings.json`.

`deploy/`:
- `sql/00_roles.sql`, `dev/setup-local.ps1`, `docker-compose.dev.yml`, `minio/minio-init.sh`, `minio/eshopguard-rw-policy.json`, `.env.example`.

`tests/`:
- `Directory.Build.props` (import nadřazeného, `OutputType=Exe`, `IsTestProject=true`, `IsPackable=false`, `UseMicrosoftTestingPlatformRunner=true`, `PackageReference` `xunit.v3`, `Using Include="Xunit"`; `UserSecretsId` = `eshopguard-tests` pro všechny kromě `EshopGuard.Core.Tests`).
- `tests/EshopGuard.Core.Tests/EshopGuard.Core.Tests.csproj`: odstranit, co převzal `Directory.Build.props` (počet testů se nesmí změnit).
- `tests/Shared/TestConfiguration.cs` (sdílený soubor připojený přes `Compile Include`): načte user-secrets `eshopguard-tests` a proměnné `ESHOPGUARD_TEST_`; `Require(string key)` selže se zprávou s názvem klíče.
- `tests/EshopGuard.Data.Tests/`: `PostgresTestDatabase.cs` (fixture kolekce `Db`: migrace jako `Owner`), `RolesTests.cs`, `DatabaseTests.cs`, `MigrationTests.cs`, `DatabaseRoleGuardTests.cs`, `MigrationStatusTests.cs`.
- `tests/EshopGuard.Storage.Tests/`: `BlobKeyTests.cs`, `BlobStoreContractTests.cs` (abstraktní), `FileSystemBlobStoreTests.cs`, `S3BlobStoreTests.cs` (`Category=S3`).
- `tests/EshopGuard.Api.Tests/`: `HealthEndpointTests.cs`, `StartupGuardTests.cs`, `SecretsHygieneTests.cs`, `LogRedactionTests.cs`, `ProjectReferenceTests.cs`, `InMemoryLoggerProvider.cs`.
- `tests/EshopGuard.Worker.Tests/`: `WorkerStartupTests.cs`, `WorkerShutdownTests.cs`.
