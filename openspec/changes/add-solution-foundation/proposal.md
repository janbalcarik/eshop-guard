# Proposal: Základ řešení – projekty, databáze, role, úložiště a konfigurace

## Intent

**Problém.** Po přejmenování (změna 1) má řešení jen knihovnu `EshopGuard.Core`, CLI a jejich testy. Webová verze potřebuje databázi s oddělenými rolemi, úložiště souborů, API a workery. Bez společného základu by každá další změna (3–17) zakládala vlastní projekty, připojení a tajemství po svém. Navíc hrozí dvě tiché chyby, které by se později těžko hledaly:
- aplikace připojená jako superuživatel `postgres` obchází Row-Level Security, takže testy izolace tenantů by procházely, i když izolace nefunguje;
- hesla a klíče skončí v `appsettings.json` nebo v logu.

**Proč teď.** Je to druhý krok fáze F0. Datový model (změna 3), fronta (změna 4) i API (změny 9–11) stojí na projektech, rolích a konfiguraci z této změny.

**Přínos.**
- Struktura řešení podle podkladu (část 7) s vynuceným směrem závislostí: knihovna `EshopGuard.Core` zůstane bez databáze a CLI dál běží v paměti.
- Lokální PostgreSQL 18.6 s databází `eshopguard` a pěti rolemi; aplikace se odmítne spustit pod superuživatelem nebo rolí, která obchází RLS (fail-closed).
- Migrace běží jen jako `eshopguard_owner`; první prázdná migrace ověří celý řetězec.
- Tajemství jen v proměnných prostředí a `dotnet user-secrets`, nikdy v repozitáři ani v logu; ověřeno testem.
- Úložiště souborů za rozhraním `IBlobStore` (S3/MinIO a souborový systém) s klíči `tenants/{tenant}/shops/{shop}/…`.
- `dotnet test` konečně spouští testy (dnes hlásí 0).

**Fáze:** F0 Základ, druhý krok (po změně 1).

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`: část 1 (lokální PostgreSQL 18.6, `postgres`/`postgres`, sousední databáze `eia_registry`), část 2 (role, `uuid` v7, soubory mimo databázi), část 7 (struktura aplikace, konfigurace vývoje), část 8 (F0);
- `architektura-multitenant-worker-2026-10-01.md`: část 2 (aplikace), část 3 (čtyři úrovně izolace, soubory `tenants/{tenantId}/shops/{shopId}/…`), část 7 (spolehlivost), část 7.1 (Compose s MinIO pro vývoj, `/health`, tajné klíče, migrace jako samostatný krok).

## Scope

In scope:
- Nové projekty v `src/`: `EshopGuard.Data` (EF Core 10 + Npgsql), `EshopGuard.Storage`, `EshopGuard.Jobs`, `EshopGuard.Billing`, `EshopGuard.Connectors` (zatím prázdné knihovny se správnými odkazy), `EshopGuard.Api` (ASP.NET Core), `EshopGuard.Worker` (Generic Host).
- Nové testovací projekty: `tests/EshopGuard.Data.Tests`, `tests/EshopGuard.Storage.Tests`, `tests/EshopGuard.Api.Tests`, `tests/EshopGuard.Worker.Tests` (`EshopGuard.Jobs.Tests` založí změna 4, aby neexistoval testovací projekt bez testů).
- Centrální verze balíčků `Directory.Packages.props`, společné nastavení testů `tests/Directory.Build.props`, lokální nástroj `dotnet-ef` v `.config/dotnet-tools.json`.
- Zprovoznění `dotnet test` nad `EshopGuard.sln` (dnes 0 testů).
- `deploy/sql/00_roles.sql`: role `eshopguard_owner`, `eshopguard_app`, `eshopguard_worker`, `eshopguard_admin`, `eshopguard_cms`, databáze podle parametru (`eshopguard`, testovací `eshopguard_test`), oprávnění k databázi, schéma `cms` pro Payload. Spouští se jednou jako `postgres`; hesla jen z proměnných prostředí.
- `deploy/dev/setup-local.ps1`: vygeneruje hesla, spustí `00_roles.sql` pro obě databáze, uloží připojení do `dotnet user-secrets`, připraví `deploy/.env` pro MinIO. Hesla nikdy nevypisuje.
- `deploy/docker-compose.dev.yml` s MinIO a jednorázovou službou `minio-init` (bucket `eshopguard-dev` a `eshopguard-test`, uživatel aplikace s právy jen na bucket).
- Konfigurace připojení `ConnectionStrings:App`, `:Worker`, `:Migrations` (proměnné `ConnectionStrings__App` atd.), v testech navíc `:Owner`, `:Admin` a `:Cms` pro `eshopguard_test`.
- `EshopGuardDb` bez tabulek, tabulka historie migrací `ops.__ef_migrations_history` a první migrace `Initial` jako `eshopguard_owner`.
- Pojistka rolí při startu API a workeru (`DatabaseStartupGuard`): odmítne superuživatele, `BYPASSRLS`, jinou než očekávanou roli a neaplikované migrace.
- `GET /health` v API (databáze, role, migrace, úložiště; jen kódy, žádné texty chyb).
- Kostra workeru: Generic Host, kontrola role, záznam startu a korektní ukončení v časovém limitu (obsluhu úloh doplní změna 4).
- Testy: role a jejich atributy, migrace jako vlastník, odmítnutí superuživatele, tajemství mimo repozitář a log, směr závislostí, `/health`, ukončení workeru, `IBlobStore`.

Out of scope:
- Entity, tabulky, RLS a dělení tabulek: změna 3.
- Fronta, sloty, plánovač a skutečná obsluha úloh ve workeru: změna 4.
- Přihlášení, tenanti v API, OpenAPI: změna 9.
- Ukazatele fronty a disku v `/health`, OpenTelemetry, produkční Compose, Caddy, WAL-G, GitHub Actions: změna 17.
- Šifrování klíčů konektorů (Data Protection): změna 15.
- PgBouncer (až od desítek workerů, poznámka ve změně 4).

## Approach

1. **Projekty a závislosti.** Graf odkazů je pevný (design, tabulka „Směr závislostí“) a hlídá ho test, který čte `ProjectReference` ze všech `*.csproj`. Reflexe nestačí, protože kompilátor nepoužitý odkaz do sestavení nezapíše.
2. **Role a databáze jednorázovým skriptem** `deploy/sql/00_roles.sql` spuštěným přes `psql` jako `postgres`. Skript je idempotentní (`\gexec` s `WHERE NOT EXISTS`, `ALTER ROLE` vynutí atributy i při opakování), hesla čte přes `\getenv` z proměnných prostředí, takže nejsou v souboru ani v parametrech procesu. Aplikace heslo `postgres` nikdy nevidí.
3. **Fail-closed při startu.** `DatabaseStartupGuard` se spustí před ostatními službami hostitele. Superuživatel, `BYPASSRLS`, jiná role než očekávaná, chybějící připojení nebo neaplikovaná migrace ukončí proces s kódem chyby. Žádné výchozí připojení se nedoplňuje.
4. **Migrace jen jako vlastník.** Návrhová továrna `EshopGuardDbDesignTimeFactory` čte jen `ConnectionStrings:Migrations` (user-secrets projektu `EshopGuard.Data` nebo proměnná prostředí). Do produkce se migrace dodá jako balíček `efbundle` (architektura 7.1); balíček se staví ve změně 17.
5. **Tajemství.** `appsettings*.json` připojení ani klíče neobsahují. Lokálně user-secrets (zvlášť pro `EshopGuard.Data`, `EshopGuard.Api`, `EshopGuard.Worker` a testy), na serveru proměnné prostředí ze souboru `.env` (změna 17). Test prohledá konfigurační soubory a zachytí log startu se zkušebním heslem.
6. **Úložiště.** `IBlobStore` s implementacemi `S3BlobStore` (AWS SDK pro .NET proti MinIO i S3 u poskytovatele) a `FileSystemBlobStore` (testy, vývoj bez Dockeru). Klíče skládá jen `BlobKey`, který odmítne `..`, prázdné části a absolutní cesty.
7. **`dotnet test`.** `global.json` přepíná `dotnet test` do režimu Microsoft.Testing.Platform a testovací projekty xUnit v3 v něm dnes nejsou spustitelné jako aplikace MTP. Nejpravděpodobnější oprava je `UseMicrosoftTestingPlatformRunner=true` v `tests/Directory.Build.props` (neověřeno; úkol 9.1 to nejdřív ověří).

## Dependencies

- Změna 1 `rename-to-eshopguard` (cesty `eshop-guard/`, `EshopGuard.sln`, `EshopGuard.Core`).
- Lokální PostgreSQL 18.6 jako služba `postgresql-x64-18` na `localhost:5432`, účet `postgres`/`postgres` jen pro `00_roles.sql`. Databázi `eia_registry` jiného projektu skript nesmí měnit.
- Docker Desktop (nebo jiný Docker) pro MinIO; bez něj jde vývoj se `Storage:Provider = FileSystem`.
- Navazují: změna 3 (entity a RLS do `EshopGuardDb`), 4 (`EshopGuard.Jobs`, kostra workeru), 9–12 (API), 17 (provozní požadavky do `operations`, `/health` doplní frontu a disk).

## Done when

- `dotnet build EshopGuard.sln` projde bez chyb.
- `dotnet test EshopGuard.sln` spustí testy všech testovacích projektů a hlásí nenulové počty; `EshopGuard.Core.Tests` dává stejné počty jako po změně 1 (192 podle podkladů).
- Po `deploy/dev/setup-local.ps1` existují databáze `eshopguard` a `eshopguard_test` a role `eshopguard_owner`, `_app`, `_worker`, `_cms` bez `SUPERUSER` a `BYPASSRLS`, `eshopguard_admin` s `BYPASSRLS` a bez `SUPERUSER`; `dotnet ef database update` jako `eshopguard_owner` vytvoří `ops.__ef_migrations_history` s migrací `Initial`.
- API a worker s rolí `postgres` skončí chybou `db.role_bypasses_rls`; s `eshopguard_app` / `eshopguard_worker` nastartují a `GET /health` vrátí 200 se stavem `ok` u kontrol `database`, `database_role`, `migrations` a `storage`.
- Test hygieny tajemství projde: žádné heslo v souborech pod `src/`, `deploy/` a `tests/`, zkušební heslo se neobjeví v logu.
- `docker compose -f deploy/docker-compose.dev.yml up -d` vytvoří buckety a testy `EshopGuard.Storage.Tests` kategorie `S3` projdou proti MinIO.

## K rozhodnutí

1. **Testovací databáze.** Podklad říká jen „proti lokálnímu PostgreSQL“. Návrh: samostatná databáze `eshopguard_test` (stejné role, testy ji mohou mazat a plnit), aby testy nezasahovaly do vývojových dat v `eshopguard`. Potvrdit.
2. **Řazení textu v databázi (locale).** Podklady locale databáze neurčují. Návrh: `LOCALE_PROVIDER builtin`, `BUILTIN_LOCALE 'C.UTF-8'` (PostgreSQL 17+). Chová se stejně na Windows i na Linuxu serveru a nezávisí na verzi knihovny ICU; slovenské a české řazení pro zobrazení se dá přes kolace ICU (`sk-x-icu`, `cs-x-icu`) jen tam, kde je potřeba. Alternativa: locale systému (na Windows a Linuxu se liší názvy i chování).
3. **MinIO jako lokální S3.** MinIO je pod AGPLv3 a v roce 2025 omezilo komunitní distribuci (neověřeno, ověřit dostupnost obrazu a podmínky před implementací). Pokud obraz nebude k dispozici, alternativy: jiný server kompatibilní s S3 (Garage, SeaweedFS) nebo jen `FileSystemBlobStore` pro vývoj a S3 až na serveru. Nic se tím nemění v kódu aplikace (rozhraní `IBlobStore`).
4. **Je na vývojovém počítači Docker?** Podklady s ním počítají (`docker-compose.dev.yml`), ale ověřené to není.
5. **Audit použití `eshopguard_admin`.** Podklad: „obchází RLS, každé použití jde do auditu“. PostgreSQL sám čtení neaudituje; potřeba je buď rozšíření `pgaudit`, nebo správcovský nástroj, který každý přístup zapíše do `ops.audit_log`. V této změně role vznikne bez mechanismu auditu. Rozhodnout způsob (návrh: zápis přes správcovský nástroj + `log_connections` pro tuto roli).
6. **Projekty `EshopGuard.Billing` a `EshopGuard.Connectors` už ve F0.** Podklad v části 7 je uvádí, ale řádek F0 v části 8 zmiňuje jen Data, Storage, Jobs, Api a Worker. Zadání této změny je zakládá prázdné; potvrdit.
7. **`EshopGuard.Storage.Tests` a `EshopGuard.Worker.Tests`** nejsou v seznamu testovacích projektů části 7 (tam jsou Core, Data, Jobs, Api). Změna 8 už `EshopGuard.Worker.Tests` předpokládá. Návrh: oba přidat.
8. **Rozdíl `README.md` a skutečnosti.** `README.md` uvádí `dotnet test`, ale ten dnes hlásí 0 testů (testy jdou přes `EshopGuard.Core.Tests.exe`). Tato změna to opravuje a `README.md` srovná; pokud oprava v úkolu 9.1 nevyjde, zůstane spouštění přes exe a podmínka F0 „`dotnet test` projde“ se musí upravit.
