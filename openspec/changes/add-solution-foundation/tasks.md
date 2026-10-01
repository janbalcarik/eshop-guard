# Tasks

Cesty jsou relativní ke kořeni `D:\_github\Overko\eshop-guard`. Žádný úkol nevolá Jev ani OpenAI.

> **Stav 1. 10. 2026:** řešení, projekty a testy jsou ve skutečnosti pod `src/` (`src/EshopGuard.sln`, `src/tests/…`, `src/Directory.Packages.props`); `deploy/`, `.config/` a `global.json` jsou v kořeni repozitáře. Ověřeno v cloudu proti PostgreSQL 18.6; MinIO (skupina 8) čeká na rozhodnutí.

## 1. Řešení a společné nastavení sestavení

- [x] 1.1 Založit `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`) se všemi dnešními balíčky a verzemi z `src/EshopGuard.Core/EshopGuard.Core.csproj`, `src/EshopGuard.Cli/EshopGuard.Cli.csproj`, `tests/EshopGuard.Core.Tests/EshopGuard.Core.Tests.csproj` a s novými: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL` (10.0.x), `AWSSDK.S3` (4.x), `Microsoft.AspNetCore.Mvc.Testing` (10.0.x), `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Configuration.UserSecrets` (10.0.12).
- [x] 1.2 Odstranit atribut `Version` z `PackageReference` v `EshopGuard.Core.csproj`, `EshopGuard.Cli.csproj` a `EshopGuard.Core.Tests.csproj`; `dotnet build EshopGuard.sln` projde a `dotnet list package` ukáže stejné verze jako před změnou.
  - Poznámka: `dotnet list package --include-transitive` před a po: stejné verze. Sestavení 0 chyb, 0 varování.
- [x] 1.3 Založit `.config/dotnet-tools.json` s `dotnet-ef` 10.0.x (`dotnet new tool-manifest`, `dotnet tool install dotnet-ef`); `dotnet tool restore` a `dotnet ef --version` fungují.
  - Poznámka: Manifest je v kořeni repozitáře (`.config/dotnet-tools.json`, `dotnet-ef` 10.0.12), aby `dotnet tool restore` fungoval odkudkoli.
- [x] 1.4 Doplnit `.gitignore`: `.data/`, `deploy/.env`, `*.efbundle`, `efbundle.exe`.

## 2. Nové projekty a směr závislostí

- [x] 2.1 `src/EshopGuard.Data/EshopGuard.Data.csproj` (`UserSecretsId` `eshopguard-data`, `ProjectReference` `EshopGuard.Core`, balíčky EF Core, Npgsql, Design s `PrivateAssets=all`, Hosting.Abstractions, Configuration.UserSecrets).
- [x] 2.2 `src/EshopGuard.Storage/EshopGuard.Storage.csproj` (bez odkazů na projekty, `AWSSDK.S3`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions`).
- [x] 2.3 `src/EshopGuard.Jobs/EshopGuard.Jobs.csproj` (odkazy `Core`, `Data`, `Storage`), `src/EshopGuard.Billing/EshopGuard.Billing.csproj` (`Data`, `Jobs`), `src/EshopGuard.Connectors/EshopGuard.Connectors.csproj` (`Core`, `Data`, `Storage`, `Jobs`); bez zdrojových souborů.
- [x] 2.4 `src/EshopGuard.Api/EshopGuard.Api.csproj` (`Microsoft.NET.Sdk.Web`, `UserSecretsId` `eshopguard-api`, odkazy `Data`, `Storage`, `Jobs`, `Billing`, `Connectors`), `Properties/launchSettings.json` (`http://localhost:5080`, `Development`).
- [x] 2.5 `src/EshopGuard.Worker/EshopGuard.Worker.csproj` (`Microsoft.NET.Sdk.Worker`, `UserSecretsId` `eshopguard-worker`, odkazy `Core`, `Data`, `Storage`, `Jobs`, `Billing`, `Connectors`).
- [x] 2.6 Přidat všechny projekty do `EshopGuard.sln` (`dotnet sln add` do složek `src` a `tests`); `dotnet build EshopGuard.sln` projde.

## 3. Role a databáze

- [x] 3.1 `deploy/sql/00_roles.sql` podle designu: kontrola superuživatele a parametru `db_name`, `\getenv` pěti hesel s výjimkou při chybějícím, role přes `\gexec … WHERE NOT EXISTS` + `ALTER ROLE` s atributy a heslem, `CREATE DATABASE :db_name OWNER eshopguard_owner TEMPLATE template0 ENCODING 'UTF8'` s locale podle K rozhodnutí 2, `REVOKE ALL ON DATABASE … FROM PUBLIC`, `GRANT CONNECT` čtyřem rolím, `\connect`, `REVOKE ALL ON SCHEMA public FROM PUBLIC`, `CREATE SCHEMA IF NOT EXISTS cms AUTHORIZATION eshopguard_cms`.
- [x] 3.2 Ověřit, že skript neobsahuje žádné heslo, nesahá na jinou databázi než `:db_name` (žádný výskyt `eia_registry`) a při chybějící proměnné skončí nenulovým kódem (spustit s neúplnými proměnnými, zkontrolovat `$LASTEXITCODE` a že v `pg_roles` nic nepřibylo).
- [x] 3.3 `deploy/dev/setup-local.ps1`: parametry `-PgBin` (výchozí `C:\Program Files\PostgreSQL\18\bin`), kontrola `$env:PGPASSWORD` (jinak výzva nastavit ho v relaci, nic neukládá); hesla `[System.Security.Cryptography.RandomNumberGenerator]::GetHexString(40)`; nastavení proměnných `ESHOPGUARD_*_PASSWORD` jen pro proces `psql`; spuštění `00_roles.sql` pro `eshopguard` a `eshopguard_test`.
- [x] 3.4 Ve `setup-local.ps1` zapsat připojení do user-secrets přes standardní vstup (`$json | dotnet user-secrets set --project <projekt>`): `eshopguard-data` (`ConnectionStrings:Migrations`), `eshopguard-api` (`ConnectionStrings:App`, `Storage:S3:AccessKey`, `Storage:S3:SecretKey`), `eshopguard-worker` (`ConnectionStrings:Worker`, klíče S3), `eshopguard-tests` (`ConnectionStrings:Owner`, `:App`, `:Worker`, `:Admin`, `:Cms` pro `eshopguard_test`, klíče S3, `Storage:S3:Bucket=eshopguard-test`); vytvořit `deploy/.env` (MinIO root a uživatel aplikace), pokud neexistuje.
- [x] 3.5 Spustit `setup-local.ps1` lokálně; přepis výstupu neobsahuje žádné vygenerované heslo (porovnat s `secrets.json`); `psql -U eshopguard_app -d eshopguard -c "select current_user"` s heslem z user-secrets projde.
  - Poznámka: Ověřeno v cloudu (Linux, PowerShell 7.6, `-PgBin /usr/lib/postgresql/18/bin`): výstup neobsahuje žádné ze 7 hesel, `select current_user` jako `eshopguard_app` projde. Na Windows spustí uživatel.

## 4. Projekt Data: kontext, migrace, pojistka

- [x] 4.1 `src/EshopGuard.Data/EshopGuardDb.cs`: `public sealed class EshopGuardDb(DbContextOptions<EshopGuardDb> options) : DbContext(options)` bez `DbSet`.
- [x] 4.2 `Connections/DatabaseRole.cs` (`Owner`, `App`, `Worker`, `Admin` → názvy rolí `eshopguard_*` a klíče `ConnectionStrings:Migrations|App|Worker|Admin`), `EshopGuardConfigurationException.cs` (vlastnost `Code`, zpráva jen s názvem klíče).
- [x] 4.3 `Connections/DatabaseRoleGuard.cs`: `record RoleInfo(string CurrentUser, bool IsSuperuser, bool BypassesRls)`, `static string? Evaluate(RoleInfo info, DatabaseRole expected)` → `db.role_bypasses_rls`, `db.unexpected_role` nebo `null`; `Connections/MigrationStatus.cs`: `Evaluate(IReadOnlyCollection<string> applied, IReadOnlyCollection<string> known)` → `Pending`, `Ahead`.
- [x] 4.4 `Connections/DatabaseStartupGuard.cs` (`IHostedService`): chybějící připojení → `config.connection_string_missing`; dotaz na `pg_roles`; `DatabaseRoleGuard.Evaluate`; `GetAppliedMigrationsAsync` + `MigrationStatus`; `NpgsqlException` / `SocketException` → `db.unreachable`; log jen s kódem.
  - Poznámka: Navíc kódy `db.authentication_failed` (špatné heslo) a `db.query_failed` (jiná chyba dotazu); `db.unreachable` jen pro nedostupný server.
- [x] 4.5 `DataServiceCollectionExtensions.AddEshopGuardData(IServiceCollection, IConfiguration, DatabaseRole)`: `NpgsqlDataSourceBuilder` s `ApplicationName` (`eshopguard-api` / `-worker` / `-migrations`), `AddDbContext<EshopGuardDb>` s `MigrationsHistoryTable("__ef_migrations_history", "ops")`, `DatabaseStartupGuard` jako první hostovaná služba; `EnableSensitiveDataLogging` nikdy.
  - Poznámka: Odchylka: `AddEshopGuardData(IServiceCollection, DatabaseRole)` bez parametru `IConfiguration`. Připojení se čte líně z `IConfiguration` v DI, takže chybějící klíč nahlásí pojistka kódem `config.connection_string_missing` a přepisy konfigurace z `WebApplicationFactory` platí. Stejně `AddEshopGuardStorage()`.
- [x] 4.6 `Design/EshopGuardDbDesignTimeFactory.cs` (`IDesignTimeDbContextFactory<EshopGuardDb>`): konfigurace z proměnných prostředí + user-secrets `eshopguard-data`, jen `ConnectionStrings:Migrations`, chybějící → výjimka s kódem.
- [x] 4.7 Vygenerovat migraci `dotnet ef migrations add Initial --project src/EshopGuard.Data --output-dir Migrations`; do `Up` doplnit `migrationBuilder.Sql("GRANT USAGE ON SCHEMA ops TO eshopguard_app, eshopguard_worker, eshopguard_admin;")` a `GRANT SELECT ON ops.__ef_migrations_history TO …`, do `Down` `REVOKE`.
- [x] 4.8 Aplikovat `dotnet ef database update --project src/EshopGuard.Data` na `eshopguard`; v `psql` ověřit `\dt ops.*` (jen historie) a vlastníka `eshopguard_owner`.

## 5. Projekt Storage

- [x] 5.1 `BlobKey.cs`: `ForShop`, `ForTenant`, `Prefix`, validace částí (`^[A-Za-z0-9][A-Za-z0-9._-]{0,199}$`, ne `.`/`..`, celkem ≤ 1 024 znaků), `ToString()` vrací klíč.
- [x] 5.2 `IBlobStore.cs` podle designu (dokumentační komentáře: `PutAsync` přepisuje, `OpenReadAsync` vrací `null`, `DeleteAsync` bez chyby u neexistujícího).
- [x] 5.3 `FileSystemBlobStore.cs`: atomický zápis (dočasný soubor + `File.Move(overwrite: true)`), kontrola, že cesta je pod kořenem, `DeletePrefixAsync` maže složku pod kořenem, `GetReadUrlAsync` → `null`.
- [x] 5.4 `S3BlobStore.cs`: `AmazonS3Config` (`ServiceURL`, `ForcePathStyle`, `AuthenticationRegion`, `RequestChecksumCalculation = WHEN_REQUIRED`, `ResponseChecksumValidation = WHEN_REQUIRED`), `PutObject`, `GetObject` (404 → `null`), `DeleteObject`, `DeletePrefixAsync` přes `ListObjectsV2` + `DeleteObjects` po 1 000, `GetPreSignedURL` s omezením `MaxSignedUrlMinutes`.
- [x] 5.5 `StorageOptions.cs`, `StorageServiceCollectionExtensions.AddEshopGuardStorage(IConfiguration)` (neznámý `Provider` nebo chybějící klíč S3 → `config.storage_provider_invalid` / `config.storage_key_missing`), `Health/StorageHealthCheck.cs` (`storage.unreachable`, `storage.bucket_missing`).
  - Poznámka: `StorageConfigurationException` je v projektu Storage (nesmí odkazovat na Data). Konfigurace se ověřuje při startu (`ValidateOnStart`). Navíc kód `storage.access_denied`.

## 6. API

- [x] 6.1 `src/EshopGuard.Api/Program.cs`: `WebApplication.CreateBuilder`, `AddEshopGuardData(builder.Services, builder.Configuration, DatabaseRole.App)`, `AddEshopGuardStorage`, `AddProblemDetails` (rozšíření `code`), `AddHealthChecks()` s kontrolami `database`, `database_role`, `migrations`, `storage`, `MapHealthChecks("/health", new() { ResponseWriter = HealthResponseWriter.WriteAsync })`, `builder.Logging.AddJsonConsole()`; `public partial class Program;` pro testy.
  - Poznámka: Odmítnutý start API výjimku nechytá (proces skončí nenulovým kódem 134, log obsahuje jen kód): `WebApplicationFactory` výjimku vstupního bodu potřebuje vidět. Worker vrací kód 1.
- [x] 6.2 `Health/DatabaseHealthCheck.cs` (`SELECT 1`, role, migrace; mapování výjimek na kódy) a `Health/HealthResponseWriter.cs` (JSON podle designu, 200/503, bez `exception` a `description`).
- [x] 6.3 `appsettings.json` bez `ConnectionStrings` a klíčů; `appsettings.Development.json` se `Storage:Provider=S3`, `ServiceUrl=http://localhost:9000`, `Region=us-east-1`, `Bucket=eshopguard-dev`, `ForcePathStyle=true`.
  - Poznámka: Podle K rozhodnutí 3 (MinIO nedostupné, viz skupina 8) má `appsettings.Development.json` `Storage:Provider=FileSystem`, `Root=.data/blobs`.

## 7. Worker

- [x] 7.1 `src/EshopGuard.Worker/WorkerOptions.cs` (`Id` výchozí `$"{Environment.MachineName}:{Environment.ProcessId}"`, `ShutdownSeconds` = 90) a `appsettings.json`.
- [x] 7.2 `WorkerSkeletonService.cs` (`BackgroundService`): log `worker.started` s `Id`, čekání na zrušení, ve `StopAsync` log `worker.stopped`.
- [x] 7.3 `Program.cs`: `Host.CreateApplicationBuilder`, `AddEshopGuardData(…, DatabaseRole.Worker)`, `AddEshopGuardStorage`, `Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(options.ShutdownSeconds))`, `AddHostedService<WorkerSkeletonService>()`, JSON log; `Properties/launchSettings.json` s `DOTNET_ENVIRONMENT=Development` (jen v něm se načtou user-secrets `eshopguard-worker`).
  - Poznámka: Hostitel se skládá ve `WorkerHost.CreateBuilder`, který používají `Program.cs` i testy.

## 8. Lokální MinIO

- [ ] 8.1 `deploy/docker-compose.dev.yml` podle designu (`name: eshopguard-dev`, MinIO s pevnou značkou obrazu ověřenou podle K rozhodnutí 3, porty `127.0.0.1:9000` a `127.0.0.1:9001`, `${MINIO_ROOT_USER:?chybí v deploy/.env}`, svazek `minio-data`, healthcheck; `minio-init` s `depends_on: condition: service_healthy`).
  - Poznámka: Blokováno: obrazy `minio/minio` a `minio/mc` na Docker Hubu už nejsou (404 k 1. 10. 2026), quay.io vyžaduje přihlášení (K rozhodnutí 3). Čeká na rozhodnutí uživatele o náhradě (SeaweedFS, Garage, nebo jen souborové úložiště ve vývoji).
- [ ] 8.2 `deploy/minio/minio-init.sh` (idempotentní: `mc alias set`, `mc mb --ignore-existing` pro `eshopguard-dev` a `eshopguard-test`, `mc admin policy create` z `eshopguard-rw-policy.json`, `mc admin user add`, `mc admin policy attach`) a `deploy/minio/eshopguard-rw-policy.json`.
- [ ] 8.3 `deploy/.env.example` s prázdnými `MINIO_ROOT_USER`, `MINIO_ROOT_PASSWORD`, `ESHOPGUARD_S3_ACCESS_KEY`, `ESHOPGUARD_S3_SECRET_KEY`.
- [ ] 8.4 `docker compose -f deploy/docker-compose.dev.yml up -d` a ověřit buckety (`mc ls`) a že uživatel aplikace nesmí `mc mb` (odmítnuto).

## 9. Testy

- [x] 9.1 Ověřit příčinu „`dotnet test` hlásí 0 testů“: `dotnet test EshopGuard.sln -v d` a dokumentace xUnit v3 k Microsoft.Testing.Platform; zapsat zjištění. Pokud potvrdí hypotézu, `tests/Directory.Build.props` s `UseMicrosoftTestingPlatformRunner=true`; jinak navrhnout opravu a před dalším krokem ji ukázat uživateli.
  - Poznámka: Zjištění: hypotéza neplatila. Příčinou byl `global.json` ve `src/`: z kořene repozitáře se nenašel, `dotnet test` přešel do režimu VSTest a SDK 10 skončil chybou (starší hlásilo 0 testů). Uživatel schválil přesun `global.json` do kořene repozitáře; `UseMicrosoftTestingPlatformRunner` není potřeba (xUnit v3 4.x je aplikace MTP).
- [x] 9.2 `tests/Directory.Build.props` (import nadřazeného `Directory.Build.props`, `OutputType=Exe`, `IsTestProject`, `IsPackable=false`, `xunit.v3`, `Using Xunit`, `UserSecretsId` `eshopguard-tests` mimo `EshopGuard.Core.Tests`) a zjednodušit `EshopGuard.Core.Tests.csproj`; počet testů v `EshopGuard.Core.Tests` se nezmění.
  - Poznámka: `EshopGuard.Core.Tests`: 190 testů bez kategorie `Jev` jako před změnou.
- [x] 9.3 `tests/Shared/TestConfiguration.cs` (user-secrets `eshopguard-tests` + proměnné `ESHOPGUARD_TEST_`, `Require(key)` selže se jménem klíče), připojený do testovacích projektů přes `Compile Include`.
- [x] 9.4 `tests/EshopGuard.Data.Tests/PostgresTestDatabase.cs`: fixture kolekce `Db`, `Database.MigrateAsync()` jako `Owner`, `NpgsqlDataSource` pro `App`, `Worker`, `Admin`.
- [x] 9.5 `RolesTests.cs` (`Category=Db`): atributy pěti rolí v `pg_roles`; `eshopguard_app` a `eshopguard_worker` dostanou 42501 na `CREATE TABLE ops.x` i `CREATE TABLE public.x`; `eshopguard_cms` 42501 na `SELECT` z `ops.__ef_migrations_history` a projde `CREATE TABLE cms.t` (v transakci s `ROLLBACK`).
- [x] 9.6 `DatabaseTests.cs` (`Category=Db`): vlastník `eshopguard_test` je `eshopguard_owner`; `datacl` nemá položku pro `PUBLIC`; schéma `cms` patří `eshopguard_cms`.
- [x] 9.7 `MigrationTests.cs` (`Category=Db`): `ops.__ef_migrations_history` obsahuje `Initial`; `eshopguard_app` smí číst historii; `Database.MigrateAsync()` jako `App` selže s 42501.
- [x] 9.8 `DatabaseRoleGuardTests.cs` a `MigrationStatusTests.cs` (bez databáze): superuživatel, `BYPASSRLS` u `App`/`Worker`, `BYPASSRLS` u `Admin` povolen, jiná role, čekající a přebývající migrace.
- [x] 9.9 `tests/EshopGuard.Storage.Tests/BlobKeyTests.cs`: platné klíče, `..`, `.`, prázdná část, lomítko v části, absolutní cesta, příliš dlouhý klíč.
- [x] 9.10 `BlobStoreContractTests.cs` (abstraktní: zápis/čtení bajt po bajtu, přepis, `null` pro neexistující, `DeleteAsync` bez chyby, `DeletePrefixAsync` smaže jen prefix tenanta) a `FileSystemBlobStoreTests.cs` (+ zrušený zápis nenechá soubor).
- [ ] 9.11 `S3BlobStoreTests.cs` (`Category=S3`, bucket `eshopguard-test`): smlouva z 9.10, anonymní GET → 403, podepsaný odkaz platí a po vypršení vrátí 403.
  - Poznámka: Ověřeno jen proti emulátoru moto v cloudu (Docker ani MinIO tam nejsou): smlouva `IBlobStore`, mazání prefixu, podepsaný odkaz a chybějící bucket projdou (12 z 13). Vypršení podepsaného odkazu moto bez autentizace nevynucuje a s autentizací má chyby v ověřování podpisů (stejné i s boto3). Ověřit proti skutečnému serveru S3 podle rozhodnutí ve skupině 8.
- [x] 9.12 `tests/EshopGuard.Api.Tests/HealthEndpointTests.cs` (`Category=Db`, `WebApplicationFactory<Program>` s prostředím `Testing`, aby se nenačetly user-secrets `eshopguard-api` s vývojovou databází; `ConnectionStrings:App` z `TestConfiguration` na `eshopguard_test`; úložiště `FileSystem` v dočasné složce): 200 a čtyři kontroly `ok`; nedostupný port databáze → 503 `db.unreachable`; tělo neobsahuje `Exception`, `Password`, `Host=`.
  - Poznámka: Test s nedostupnou databází běží bez pojistky při startu (jinak by API nenastartovalo) a nepotřebuje kategorii `Db`.
- [x] 9.13 `StartupGuardTests.cs` (`Category=Db`): připojení `Admin` → start selže `db.role_bypasses_rls`; `Owner` → `db.unexpected_role`; chybějící `ConnectionStrings:App` → `config.connection_string_missing`.
- [x] 9.14 `SecretsHygieneTests.cs`: projde konfigurační soubory `src/**/appsettings*.json`, `src/**/launchSettings.json` a `*.json`, `*.yml`, `*.yaml`, `*.sql`, `*.ps1`, `*.sh` a `.env.example` pod `deploy/` a `tests/` (bez `bin`, `obj` a bez `deploy/.env`, který je mimo repozitář a skutečná hesla obsahuje; zdrojové `*.cs` ne, protože test `LogRedactionTests` zkušební heslo záměrně obsahuje). Chyba = `Password=` nebo `PASSWORD:` s doslovnou hodnotou; povolený je jen odkaz na proměnnou (`$…`, `${…}`, `{…}`) a prázdná hodnota v `.env.example`. Dále: `.gitignore` obsahuje `deploy/.env` a soubor `deploy/.env` není mezi soubory, které by šly do repozitáře.
- [x] 9.15 `LogRedactionTests.cs` + `InMemoryLoggerProvider.cs`: API se zkušebním heslem `SENTINEL-<guid>` a nedostupným hostitelem; žádný zachycený záznam (zpráva, výjimka, stav) neobsahuje `SENTINEL-`; je tam `db.unreachable`.
- [x] 9.16 `ProjectReferenceTests.cs`: najde kořen (`EshopGuard.sln`) od `AppContext.BaseDirectory`, načte `ProjectReference` ze všech `src/**/*.csproj` a porovná s tabulkou povolených hran z designu; neznámý projekt = chyba.
- [x] 9.17 `tests/EshopGuard.Worker.Tests/WorkerStartupTests.cs` (`Category=Db`): role `Worker` → log `worker.started`; role `App` → `db.unexpected_role`; `WorkerShutdownTests.cs`: `StopAsync` po 2 s doběhne do `ShutdownSeconds` a log obsahuje `worker.stopped`.
  - Poznámka: `WorkerShutdownTests` běží bez databáze (pojistka odebraná), `ShutdownSeconds=5`.

## 10. Dokumentace

- [x] 10.1 `README.md`: oddíl „Lokální databáze a úložiště“ (PostgreSQL 18.6 jako služba, `setup-local.ps1` s `$env:PGPASSWORD` jen v relaci, `docker compose -f deploy/docker-compose.dev.yml up -d`, `dotnet tool restore`, `dotnet ef database update --project src/EshopGuard.Data`), oddíl „Testy“ (`dotnet test`, kategorie `Db`, `S3`, `Jev`, filtr podle 9.1, placené testy jen se souhlasem); věta, že `postgres` se používá jen pro `00_roles.sql`.

## 11. Ověření

- [x] 11.1 `dotnet build EshopGuard.sln`: 0 chyb.
- [x] 11.2 `dotnet test EshopGuard.sln` s filtrem bez kategorie `Jev`: všech pět testovacích projektů hlásí nenulové počty, 0 selhání; `EshopGuard.Core.Tests` má stejný počet testů jako po změně 1 (192 podle podkladů).
  - Poznámka: Cloud: 316 testů (Core 190, Data 43, Storage 51, Api 25, Worker 7), 315 prošlo; jediné selhání je vypršení podepsaného odkazu proti moto (viz 9.11).
- [x] 11.3 `dotnet test` s filtrem bez `Db` a `S3` a bez spuštěného PostgreSQL a MinIO: projde a výstup uvádí vynechané testy; bez filtru testy `Db` selžou se jménem chybějícího klíče (ověřit s prázdným `ESHOPGUARD_TEST_ConnectionStrings__App`).
  - Poznámka: 271 testů prošlo s vypnutým PostgreSQL. Microsoft.Testing.Platform počet vynechaných testů nevypisuje, je vidět jen z rozdílu celkového počtu (316 → 271). S prázdným `ESHOPGUARD_TEST_ConnectionStrings__App` selže 5 testů se zprávou „Chybí konfigurace testů: ConnectionStrings:App“.
- [ ] 11.4 `psql` jako `postgres`: `select rolname, rolsuper, rolbypassrls from pg_roles where rolname like 'eshopguard%'` odpovídá tabulce rolí; `\l eshopguard` a `\l eshopguard_test` mají vlastníka `eshopguard_owner`; databáze `eia_registry` je beze změny (stejný vlastník a `datacl` jako před změnou).
  - Poznámka: Role a atributy ověřené v cloudu; kontrolu `eia_registry` (v cloudu neexistuje) udělá uživatel lokálně.
- [x] 11.5 `dotnet run --project src/EshopGuard.Api` (Development, MinIO běží) a `curl -i http://localhost:5080/health`: 200 a čtyři kontroly `ok`. Zastavit službu `postgresql-x64-18` → 503 `db.unreachable`; službu znovu spustit.
  - Poznámka: Cloud, `Storage:Provider=FileSystem`: 200 a čtyři `ok`; po `pg_ctlcluster 18 main stop` 503 a `db.unreachable`; po startu znovu 200.
- [x] 11.6 `$env:ConnectionStrings__App = "Host=localhost;Database=eshopguard;Username=postgres;Password=postgres"; dotnet run --project src/EshopGuard.Api`: proces skončí nenulovým kódem, log obsahuje `db.role_bypasses_rls` a ne `Password`. Proměnnou hned smazat.
  - Poznámka: Cloud: kód 134, `db.role_bypasses_rls`, žádné `Now listening`, žádné `Password` v logu.
- [x] 11.7 `dotnet run --project src/EshopGuard.Worker`, po startu Ctrl+C: log `worker.started` a `worker.stopped`, konec do 90 s.
  - Poznámka: Cloud: `worker.started`, po SIGINT `worker.stopped`, konec za 58 ms, kód 0.
- [x] 11.8 Prohledat konfigurační soubory: `rg -n "Password=[^;\"'$\{]" src deploy tests --glob "!**/bin/**" --glob "!**/obj/**" --glob "!*.cs"` nevrátí nic (stejné pravidlo jako `SecretsHygieneTests`).
