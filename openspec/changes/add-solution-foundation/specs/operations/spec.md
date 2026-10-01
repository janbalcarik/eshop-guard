# Delta for Operations

## ADDED Requirements

### Requirement: Základ: Databáze eshopguard a oddělené role
Systém MUST mít databázi `eshopguard` (a testovací `eshopguard_test`) s vlastníkem `eshopguard_owner` a role `eshopguard_owner`, `eshopguard_app`, `eshopguard_worker`, `eshopguard_admin` a `eshopguard_cms`. Žádná z rolí nesmí být superuživatel; `BYPASSRLS` smí mít jen `eshopguard_admin`. Role `eshopguard_cms` MUST mít práva jen ke schématu `cms`. Skript `deploy/sql/00_roles.sql` MUST být idempotentní a MUST číst hesla jen z proměnných prostředí.

#### Scenario: První spuštění skriptu založí role a databázi
- GIVEN lokální PostgreSQL 18.6 bez rolí `eshopguard_*` a bez databáze `eshopguard`
- AND proměnné `ESHOPGUARD_OWNER_PASSWORD`, `…_APP_…`, `…_WORKER_…`, `…_ADMIN_…`, `…_CMS_PASSWORD` jsou nastavené
- WHEN se `deploy/sql/00_roles.sql` spustí přes `psql` jako `postgres` s `-v db_name=eshopguard`
- THEN existuje databáze `eshopguard` s vlastníkem `eshopguard_owner`
- AND v `pg_roles` mají `eshopguard_owner`, `eshopguard_app`, `eshopguard_worker` a `eshopguard_cms` `rolsuper = false` a `rolbypassrls = false`
- AND `eshopguard_admin` má `rolsuper = false` a `rolbypassrls = true`
- AND schéma `cms` patří `eshopguard_cms` a `PUBLIC` nemá na databázi ani na schématu `public` žádné právo

#### Scenario: Opakované spuštění nic nezdvojí a vrátí atributy
- GIVEN role a databáze už existují a někdo roli `eshopguard_app` ručně přidal `BYPASSRLS`
- WHEN se skript spustí znovu
- THEN skončí bez chyby, databáze se znovu nezaloží
- AND `eshopguard_app` má znovu `rolbypassrls = false` a nové heslo z proměnné prostředí

#### Scenario: Chybějící heslo nebo spuštění bez superuživatele
- GIVEN proměnná `ESHOPGUARD_APP_PASSWORD` není nastavená, nebo se skript spouští jako jiná role než superuživatel
- WHEN se skript spustí s `ON_ERROR_STOP`
- THEN skončí nenulovým kódem dřív, než založí nebo změní jakoukoli roli
- AND výstup obsahuje název chybějící proměnné, ale žádné heslo

#### Scenario: Role CMS se nedostane k datům aplikace
- GIVEN databáze `eshopguard` po migraci a role `eshopguard_cms`
- WHEN se `eshopguard_cms` pokusí vytvořit tabulku ve schématu `ops` nebo číst `ops.__ef_migrations_history`
- THEN PostgreSQL vrátí chybu oprávnění (SQLSTATE 42501)
- AND vytvoření tabulky ve schématu `cms` projde

### Requirement: Základ: Aplikace bez superuživatele a bez obejití RLS
API se MUST připojovat výhradně jako `eshopguard_app` a worker výhradně jako `eshopguard_worker`, i lokálně. Při startu MUST ověřit roli spojení a odmítnout start, když je role superuživatel, má `BYPASSRLS` nebo není očekávaná. Očekávaná role MUST být daná kódem hostitele, ne konfigurací.

#### Scenario: Start s rolí postgres skončí chybou
- GIVEN `ConnectionStrings:App` ukazuje na roli `postgres`
- WHEN se spustí `EshopGuard.Api`
- THEN proces skončí nenulovým kódem dřív, než začne poslouchat na HTTP portu
- AND log obsahuje kód `db.role_bypasses_rls` a neobsahuje heslo

#### Scenario: Start s rolí obcházející RLS
- GIVEN `ConnectionStrings:Worker` ukazuje na roli `eshopguard_admin` (`BYPASSRLS`)
- WHEN se spustí `EshopGuard.Worker`
- THEN worker skončí s kódem `db.role_bypasses_rls` a nevezme žádnou práci

#### Scenario: Start s jinou běžnou rolí
- GIVEN `ConnectionStrings:App` ukazuje na `eshopguard_owner`
- WHEN se spustí `EshopGuard.Api`
- THEN start selže s kódem `db.unexpected_role`

#### Scenario: Start se správnou rolí
- GIVEN `ConnectionStrings:App` ukazuje na `eshopguard_app` a všechny migrace jsou aplikované
- WHEN se spustí `EshopGuard.Api`
- THEN API nastartuje a `GET /health` vrátí kontrolu `database_role` se stavem `ok`

### Requirement: Základ: Migrace jen jako vlastník schématu
Migrace databáze MUST běžet jako `eshopguard_owner` přes `ConnectionStrings:Migrations`. Historie migrací MUST být v `ops.__ef_migrations_history`. Aplikační role MUST smět historii jen číst a MUST NOT smět měnit schéma. Start API nebo workeru MUST selhat, když databáze nemá všechny migrace, které kód zná.

#### Scenario: Prázdná migrace jako vlastník
- GIVEN databáze `eshopguard` bez tabulek a `ConnectionStrings:Migrations` s rolí `eshopguard_owner`
- WHEN se spustí `dotnet ef database update --project src/EshopGuard.Data`
- THEN existuje schéma `ops` a tabulka `ops.__ef_migrations_history` s jedním řádkem migrace `Initial`
- AND `eshopguard_app` smí z `ops.__ef_migrations_history` číst

#### Scenario: Migrace pod aplikační rolí selže
- GIVEN připojení jako `eshopguard_app`
- WHEN se pokusí aplikovat migraci nebo spustit `CREATE TABLE ops.x (id int)`
- THEN PostgreSQL vrátí chybu oprávnění (SQLSTATE 42501) a schéma se nezmění

#### Scenario: Chybí připojení pro migrace
- GIVEN `ConnectionStrings:Migrations` není v proměnných prostředí ani v user-secrets
- WHEN se spustí `dotnet ef database update`
- THEN příkaz skončí chybou s kódem `config.connection_string_missing` a názvem klíče
- AND nepoužije žádné výchozí připojení

#### Scenario: Neaplikovaná migrace zastaví start
- GIVEN kód zná migraci, která v `ops.__ef_migrations_history` chybí
- WHEN se spustí API nebo worker
- THEN start selže s kódem `db.migrations_pending`

### Requirement: Základ: Tajemství mimo repozitář a logy
Hesla k databázi, přístupové klíče úložiště a klíče služeb (Jev, OpenAI, Stripe, SuperFaktúra) MUST být jen v proměnných prostředí nebo v `dotnet user-secrets`, nikdy v souborech projektu. Systém MUST NOT zapsat heslo ani klíč do logu, odpovědi API nebo výstupu skriptu. Chybějící tajemství MUST vést k chybě s názvem klíče, ne k výchozí hodnotě.

#### Scenario: Konfigurační soubory bez hesel
- GIVEN všechny soubory `appsettings*.json` a `launchSettings.json` pod `src/` a konfigurační soubory (`*.json`, `*.yml`, `*.yaml`, `*.sql`, `*.ps1`, `*.sh`, `.env.example`) pod `deploy/` a `tests/` (bez `deploy/.env` mimo repozitář)
- WHEN test `SecretsHygieneTests` projde jejich obsah
- THEN žádný z nich neobsahuje `Password=` ani `PASSWORD:` s doslovnou hodnotou (povolený je jen odkaz na proměnnou)
- AND `deploy/.env` je v `.gitignore`

#### Scenario: Heslo se nedostane do logu ani při chybě
- GIVEN `ConnectionStrings:App` se zkušebním heslem `SENTINEL-…` a nedostupným hostitelem
- WHEN API selže při startu
- THEN žádný záznam logu zachycený testem neobsahuje `SENTINEL-`
- AND log obsahuje kód `db.unreachable`

#### Scenario: Skript nastavení hesla nevypíše
- GIVEN `deploy/dev/setup-local.ps1` vygeneruje nová hesla
- WHEN doběhne
- THEN jeho výstup ani přepis relace neobsahují žádné z vygenerovaných hesel
- AND hesla jsou v user-secrets `eshopguard-data`, `eshopguard-api`, `eshopguard-worker` a `eshopguard-tests`

### Requirement: Základ: Směr závislostí mezi projekty
Řešení MUST obsahovat projekty `EshopGuard.Core`, `.Cli`, `.Data`, `.Storage`, `.Jobs`, `.Billing`, `.Connectors`, `.Api`, `.Worker` a jejich testy. `EshopGuard.Core` MUST NOT odkazovat na žádný jiný projekt `EshopGuard.*`, `EshopGuard.Cli` MUST odkazovat jen na `EshopGuard.Core` a na `EshopGuard.Api`, `EshopGuard.Worker` a `EshopGuard.Cli` MUST NOT odkazovat žádný projekt kromě jejich testů. Povolené odkazy MUST hlídat test.

#### Scenario: Povolený graf projde
- GIVEN odkazy `ProjectReference` podle tabulky „Směr závislostí“ v designu
- WHEN se spustí `ProjectReferenceTests`
- THEN test projde

#### Scenario: Knihovna Core s odkazem na Data neprojde
- GIVEN někdo přidá do `src/EshopGuard.Core/EshopGuard.Core.csproj` `ProjectReference` na `EshopGuard.Data`
- WHEN se spustí `ProjectReferenceTests`
- THEN test selže a vypíše zakázanou hranu `EshopGuard.Core → EshopGuard.Data`

#### Scenario: Nový projekt bez pravidla
- GIVEN v `src/` přibude projekt, který tabulka pravidel nezná
- WHEN se spustí `ProjectReferenceTests`
- THEN test selže s názvem projektu (nový projekt se musí do pravidel výslovně přidat)

### Requirement: Základ: Sestavení a testy jedním příkazem
`dotnet build EshopGuard.sln` MUST projít bez chyb a `dotnet test EshopGuard.sln` MUST spustit testy všech testovacích projektů. Testy, které potřebují PostgreSQL (`Category=Db`), MUST bez konfigurace prostředí selhat se jménem chybějícího klíče, ne se tiše přeskočit; vynechat je smí jen výslovný filtr. Placené testy (`Category=Jev`) MUST NOT běžet bez výslovného souhlasu uživatele.

#### Scenario: dotnet test spustí všechny projekty
- GIVEN lokální databáze `eshopguard_test` po migraci a vyplněné user-secrets `eshopguard-tests`
- WHEN se spustí `dotnet test EshopGuard.sln` s filtrem bez kategorie `Jev`
- THEN výsledek uvádí testy z `EshopGuard.Core.Tests`, `EshopGuard.Data.Tests`, `EshopGuard.Storage.Tests`, `EshopGuard.Api.Tests` a `EshopGuard.Worker.Tests`
- AND `EshopGuard.Core.Tests` má stejný počet testů jako po změně 1 (192 podle podkladů) a žádný neselže

#### Scenario: Chybí připojení k testovací databázi
- GIVEN user-secrets `eshopguard-tests` neobsahují `ConnectionStrings:App`
- WHEN se spustí testy kategorie `Db`
- THEN selžou se zprávou, že chybí klíč `ConnectionStrings:App`
- AND nevypíší se jako přeskočené ani úspěšné

#### Scenario: Výslovné vynechání testů s databází
- GIVEN vývojář bez lokálního PostgreSQL
- WHEN spustí `dotnet test` s filtrem, který vynechá kategorii `Db`
- THEN projdou ostatní testy a žádný test kategorie `Db` se nespustí

### Requirement: Základ: Kontrola stavu API
API MUST mít `GET /health`, který ověří spojení s databází, roli spojení, aplikované migrace a dostupnost úložiště souborů. Odpověď MUST obsahovat jen názvy kontrol, stavy a kódy chyb, nikdy text výjimky, řetězec připojení, heslo nebo název hostitele. Když kterákoli kontrola selže, odpověď MUST mít stav HTTP 503.

#### Scenario: Vše v pořádku
- GIVEN API běží jako `eshopguard_app`, migrace jsou aplikované a složka `Storage:FileSystem:Root` je zapisovatelná
- WHEN klient zavolá `GET /health`
- THEN odpověď má stav 200, `status = ok` a kontroly `database`, `database_role`, `migrations`, `storage` ve stavu `ok`

#### Scenario: Databáze za běhu spadne
- GIVEN API běží a služba PostgreSQL se zastaví
- WHEN klient zavolá `GET /health`
- THEN odpověď má stav 503 a kontrola `database` má `status = failed` a `code = db.unreachable`
- AND tělo odpovědi neobsahuje text výjimky Npgsql

#### Scenario: Úložiště nejde zapsat
- GIVEN `Storage:FileSystem:Root` ukazuje na místo, kde nejde vytvořit složku (např. existující soubor)
- WHEN klient zavolá `GET /health`
- THEN odpověď má stav 503 a kontrola `storage` má `code = storage.unreachable`

### Requirement: Základ: Kostra workeru s korektním ukončením
`EshopGuard.Worker` MUST běžet jako .NET Generic Host, MUST se při startu ohlásit identitou `{MachineName}:{ProcessId}` (nebo `Worker:Id`) a MUST se při ukončení (SIGTERM, Ctrl+C) zastavit do `Worker:ShutdownSeconds`. Ukončení MUST být zapsané v logu kódem `worker.stopped`.

#### Scenario: Worker nastartuje a zastaví se
- GIVEN `ConnectionStrings:Worker` s rolí `eshopguard_worker`
- WHEN se worker spustí a po 2 s dostane požadavek na ukončení
- THEN log obsahuje `worker.started` s identitou workeru a potom `worker.stopped`
- AND hostitel skončí do `Worker:ShutdownSeconds`

#### Scenario: Worker bez připojení nestartuje
- GIVEN `ConnectionStrings:Worker` chybí
- WHEN se worker spustí
- THEN skončí nenulovým kódem s kódem `config.connection_string_missing` a názvem klíče

### Requirement: Základ: Úložiště souborů za rozhraním IBlobStore
Systém MUST ukládat soubory (snímky HTML, extrakce, PDF) mimo databázi přes rozhraní `IBlobStore`; zatím jedinou implementací je lokální souborový systém, další poskytovatel (Azure, AWS…) se přidá jako nová implementace bez změny volajícího kódu. Klíče souborů tenanta MUST začínat `tenants/{tenantId}/` a u e-shopu `tenants/{tenantId}/shops/{shopId}/`. Klíč s `..`, prázdnou částí, lomítkem uvnitř části nebo absolutní cestou MUST být odmítnut. Soubory MUST NOT být dostupné přímo z internetu, jen přes API.

#### Scenario: Zápis a čtení souboru e-shopu
- GIVEN `BlobKey.ForShop(tenant, shop, "pages", "p1.html.gz")`
- WHEN se soubor zapíše přes `PutAsync` a přečte přes `OpenReadAsync`
- THEN obsah je bajt po bajtu stejný
- AND klíč je `tenants/{tenant}/shops/{shop}/pages/p1.html.gz`

#### Scenario: Pokus o únik z kořene
- GIVEN část klíče `..` nebo `a/b`
- WHEN se volá `BlobKey.ForShop`
- THEN vznikne `ArgumentException` a nic se nezapíše

#### Scenario: Neexistující soubor
- GIVEN klíč, pod kterým nic není
- WHEN se volá `OpenReadAsync`
- THEN vrátí `null` (ne prázdný proud)
- AND `DeleteAsync` na stejný klíč skončí bez chyby

#### Scenario: Přerušený zápis
- GIVEN soubor, který už pod klíčem existuje
- WHEN se nový zápis přeruší (zrušení, chyba proudu)
- THEN pod klíčem zůstane původní obsah a nezůstane žádný napůl zapsaný ani dočasný soubor

### Requirement: Základ: Souborové úložiště s výslovně nastavenou složkou
Úložiště MUST být vybrané nastavením `Storage:Provider`; zatím jediná platná hodnota je `FileSystem`. Složka `Storage:FileSystem:Root` MUST být nastavená výslovně (bez výchozí hodnoty), aby soubory na serveru neskončily mimo připojený svazek. Neplatné nastavení MUST zastavit start API i workeru s kódem a názvem klíče, bez hodnoty.

#### Scenario: Chybí složka úložiště
- GIVEN `Storage:Provider = FileSystem` a `Storage:FileSystem:Root` není nastavená
- WHEN se spustí API nebo worker
- THEN start selže s `config.storage_key_missing: Storage:FileSystem:Root`

#### Scenario: Neznámý poskytovatel
- GIVEN `Storage:Provider` je prázdný nebo má hodnotu bez implementace (např. `Azure`)
- WHEN se spustí API nebo worker
- THEN start selže s `config.storage_provider_invalid: Storage:Provider`
