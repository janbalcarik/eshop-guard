# Delta for Tenancy

## ADDED Requirements

### Requirement: Datový model všech schémat
Databáze `eshopguard` MUST obsahovat tabulky z části 3 podkladu `databaze-a-plan-implementace-2026-10-01.md` ve schématech `iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops` a `ref` (61 tabulek), vytvořené migracemi EF Core jako `eshopguard_owner`. Model EF Core MUST odpovídat databázi tak, že po migraci nehlásí žádné čekající změny. Schéma `cms` MUST zůstat bez tabulek aplikace.

#### Scenario: Migrace na prázdnou databázi
- GIVEN databáze `eshopguard_test` po migraci `Initial` ze změny 2
- WHEN se jako `eshopguard_owner` aplikují migrace `F1DataModel`, `F1RowLevelSecurity` a `F1PartitionMaintenance`
- THEN v devíti schématech existuje 61 tabulek (dělené tabulky počítané jednou jako rodič)
- AND `dotnet ef migrations has-pending-model-changes` nehlásí žádnou změnu

#### Scenario: Migrace zpět
- GIVEN databáze po migracích F1
- WHEN se jako `eshopguard_owner` spustí `dotnet ef database update Initial`
- THEN tabulky, politiky, funkce `ops.current_tenant_id` a `ops.ensure_monthly_partitions` zmizí
- AND tabulka `ops.__ef_migrations_history` obsahuje jen `Initial`

#### Scenario: Základní číselníky
- GIVEN databáze po migracích F1
- WHEN se přečte `ref.locales` a `ref.markets`
- THEN `ref.locales` obsahuje `sk` a `cs` a `ref.markets` obsahuje `sk` s měnou `EUR`
- AND `ref.markets` obsahuje `cz` s měnou `CZK` (rozhodnuto 1. 10. 2026)

### Requirement: Kontext tenanta v každé transakci
Každý přístup k datům tenanta MUST běžet v transakci, ve které je nastavené `app.tenant_id` přes `set_config('app.tenant_id', <tenant>, true)` (ekvivalent `SET LOCAL`). Hodnota MUST být předaná jako parametr, ne složená do textu SQL, a MUST zaniknout s koncem transakce. `EshopGuardDb` MUST zakládat transakci i pro `SaveChanges` s jediným příkazem.

#### Scenario: Uložení jedné entity
- GIVEN `ITenantContext` nastavený na tenanta A
- WHEN se přes `EshopGuardDb` uloží jeden nový `Shop`
- THEN příkaz běží v transakci, ve které `current_setting('app.tenant_id')` vrací A
- AND po potvrzení vrací `current_setting('app.tenant_id', true)` na stejném spojení prázdnou hodnotu

#### Scenario: Pokus o změnu tenanta v jednom scope
- GIVEN `ITenantContext` nastavený na tenanta A
- WHEN kód zavolá `Set` s tenantem B
- THEN vznikne `InvalidOperationException` a kontext zůstane A

#### Scenario: Neplatná hodnota tenanta
- GIVEN v transakci je `app.tenant_id` nastavené na text, který není `uuid` (např. `x' OR '1'='1`)
- WHEN se spustí `SELECT * FROM shop.shops`
- THEN dotaz skončí chybou převodu na `uuid` (22P02) a nevrátí žádný řádek
- AND `TenantSql.BeginAsync` a interceptor přijímají tenanta jen jako `Guid`, takže text do `set_config` z aplikace nikdy nepošlou

### Requirement: Bez kontextu tenanta žádná data
Dotaz nebo zápis do tabulky tenanta bez nastaveného `app.tenant_id` MUST skončit chybou, MUST NOT vrátit prázdný výsledek. V databázi MUST politika volat `ops.current_tenant_id()`, která bez kontextu vyhodí chybu se SQLSTATE 42501; v EF Core MUST dotaz bez kontextu vyhodit `TenantNotSetException` s kódem `tenant.not_set`.

#### Scenario: Čisté SQL bez kontextu
- GIVEN spojení jako `eshopguard_app` bez `set_config('app.tenant_id', …)`
- WHEN se spustí `SELECT count(*) FROM shop.shops`
- THEN PostgreSQL vrátí chybu 42501 „app.tenant_id is not set“

#### Scenario: Čtení po skončení transakce na stejném spojení
- GIVEN na spojení proběhla transakce s nastaveným tenantem A a skončila
- WHEN se na stejném spojení mimo transakci spustí `SELECT * FROM checks.findings`
- THEN dotaz skončí chybou 42501, ne prázdným výsledkem ani řádky tenanta A

#### Scenario: Dotaz EF bez kontextu
- GIVEN `ITenantContext` bez tenanta
- WHEN se spustí `db.Findings.CountAsync()`
- THEN vznikne `TenantNotSetException` a do databáze nic neodejde

### Requirement: Row-Level Security na všech tabulkách tenanta
Všech 41 tabulek tenanta (design, „Rozdělení tabulek“) MUST mít `ENABLE` i `FORCE ROW LEVEL SECURITY` a politiku `tenant_isolation` s `USING` i `WITH CHECK` `tenant_id = (SELECT ops.current_tenant_id())`. Každá tabulka v devíti schématech MUST být buď mezi tabulkami tenanta, nebo mezi 20 globálními tabulkami z podkladu; jiná MUST NOT existovat.

#### Scenario: Katalog souhlasí
- GIVEN databáze po migracích F1
- WHEN test projde `pg_class` a `pg_policy` pro schémata `iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops`, `ref` (bez částí dělených tabulek)
- THEN každá tabulka tenanta má `relrowsecurity = true`, `relforcerowsecurity = true` a politiku `tenant_isolation`
- AND každá další tabulka je v seznamu globálních tabulek

#### Scenario: Nová tabulka bez RLS
- GIVEN migrace přidá tabulku `shop.shop_notes` se sloupcem `tenant_id`, ale bez RLS a bez zařazení do seznamu globálních
- WHEN se spustí katalogový test
- THEN test selže s názvem `shop.shop_notes`

#### Scenario: Vlastník tabulek RLS neobejde
- GIVEN spojení jako `eshopguard_owner` s nastaveným tenantem A
- WHEN se přečte `fixes.fix_proposals`
- THEN vrátí jen řádky tenanta A (`FORCE ROW LEVEL SECURITY` platí i pro vlastníka)

### Requirement: Izolace tenantů se stejnou doménou
Dva tenanti se stejnou doménou e-shopu MUST mít oddělená data. Pro každou tabulku tenanta MUST čtení přes EF Core i čisté SQL jako `eshopguard_app` a `eshopguard_worker` s nastaveným tenantem vrátit jen řádky tohoto tenanta, a to i s vypnutým filtrem EF `Tenant`.

#### Scenario: Dva e-shopy vegis.sk
- GIVEN tenanti A a B, oba s e-shopem `vegis.sk` (`base_path` `/`) a s jedním řádkem v každé ze 41 tabulek tenanta
- WHEN se jako `eshopguard_app` s tenantem A přečte každá tabulka čistým SQL
- THEN počet řádků je větší než 0 a žádný řádek nemá `tenant_id` B

#### Scenario: EF bez filtru tenanta
- GIVEN stejná data a `ITenantContext` nastavený na A
- WHEN se pro každý typ entity tenanta spustí dotaz s `IgnoreQueryFilters(["Tenant"])`
- THEN vrátí jen řádky tenanta A (zastaví je RLS)

#### Scenario: Seeder pokrývá všechny tabulky
- GIVEN seznam tabulek s RLS z katalogu databáze
- WHEN se porovná se seznamem tabulek, do kterých `TenantDataSeeder` zapsal
- THEN seznamy jsou stejné; chybějící tabulka test shodí

### Requirement: Zápis s cizím tenant_id selže
Zápis řádku s `tenant_id` jiného tenanta, než je v kontextu, MUST selhat: v EF Core výjimkou `CrossTenantWriteException` ještě před odesláním do databáze, v čistém SQL chybou RLS (42501). Změna `tenant_id` existujícího řádku MUST selhat stejně. Chybějící `tenant_id` u nové entity MUST interceptor doplnit z kontextu.

#### Scenario: EF s cizím tenantem
- GIVEN `ITenantContext` = A
- WHEN se přidá `Shop` s `TenantId` = B a zavolá `SaveChangesAsync`
- THEN vznikne `CrossTenantWriteException` s kódem `tenant.cross_write`
- AND databáze nedostala žádný `INSERT`

#### Scenario: Čisté SQL s cizím tenantem
- GIVEN spojení `eshopguard_app` s nastaveným tenantem A
- WHEN se spustí `INSERT INTO shop.shops (…, tenant_id) VALUES (…, B)` nebo pro kteroukoli tabulku tenanta `UPDATE … SET tenant_id = B`
- THEN PostgreSQL vrátí 42501 („new row violates row-level security policy“) a nic se nezmění

#### Scenario: Doplnění tenanta
- GIVEN `ITenantContext` = A a nový `Finding` bez nastaveného `TenantId`
- WHEN se uloží
- THEN řádek má `tenant_id` = A

### Requirement: Odkaz jen v rámci tenanta
Odkazy mezi tabulkami tenanta MUST být složené cizí klíče přes (`tenant_id`, `id`), u dělených tabulek `pages` a `page_versions` přes (`tenant_id`, `shop_id`, `id`). Odkaz na řádek jiného tenanta MUST selhat i pro roli `eshopguard_admin`, která RLS obchází.

#### Scenario: Stránka odkazuje na e-shop jiného tenanta
- GIVEN e-shop tenanta B
- WHEN `eshopguard_admin` vloží do `content.pages` řádek s `tenant_id` A a `shop_id` e-shopu tenanta B
- THEN PostgreSQL vrátí 23503 (porušení cizího klíče)

#### Scenario: Nález odkazuje na stránku jiného tenanta
- GIVEN stránka tenanta B
- WHEN `eshopguard_admin` vloží `checks.finding_occurrences` tenanta A s `page_id` této stránky
- THEN PostgreSQL vrátí 23503

### Requirement: Role aplikace nemůže RLS obejít
Role `eshopguard_app` a `eshopguard_worker` MUST NOT mít `TRUNCATE`, `REFERENCES` ani `TRIGGER` na žádné tabulce v devíti schématech a MUST NOT mít žádné právo k jednotlivým částem dělených tabulek. Role `eshopguard_cms` MUST NOT mít žádné právo v devíti schématech. Oprávnění MUST odpovídat matici v designu.

#### Scenario: TRUNCATE jako aplikace
- GIVEN spojení `eshopguard_app`
- WHEN spustí `TRUNCATE shop.shops`
- THEN PostgreSQL vrátí 42501

#### Scenario: Přímý dotaz na část dělené tabulky
- GIVEN spojení `eshopguard_app` s nastaveným tenantem A
- WHEN spustí `SELECT * FROM content.pages_p00`
- THEN PostgreSQL vrátí 42501 (přímý přístup by RLS rodiče obešel)

#### Scenario: CMS a data zákazníků
- GIVEN spojení `eshopguard_cms`
- WHEN spustí `SELECT * FROM iam.users` nebo `SELECT * FROM shop.shops`
- THEN PostgreSQL vrátí 42501

### Requirement: Časově řazené identifikátory a společné sloupce
Tabulky s identifikátorem `uuid` MUST používat `uuid` verze 7: v .NET `Guid.CreateVersion7()`, v databázi výchozí `uuidv7()`. Tabulky jen s přidáváním (`connector_events`, `run_events`, `page_changes`, `usage_records`, `jobs`, `outbox`, `audit_log`) MUST používat `bigint GENERATED ALWAYS AS IDENTITY`. `created_at` a `updated_at` MUST být `timestamptz` v UTC a MUST je vyplňovat aplikace i výchozí hodnota v databázi.

#### Scenario: Identifikátor z EF
- GIVEN dvě entity `Run` vytvořené po sobě
- WHEN se uloží
- THEN oba identifikátory mají verzi 7 a druhý je při řazení `uuid` v PostgreSQL větší

#### Scenario: Identifikátor z čistého SQL
- GIVEN `INSERT` do `checks.runs` bez sloupce `id`
- WHEN se řádek uloží
- THEN `id` má verzi 7 (z výchozí hodnoty `uuidv7()`)

#### Scenario: Úprava aktualizuje čas
- GIVEN uložený `Shop`
- WHEN se změní `Name` a uloží
- THEN `updated_at` je novější než `created_at`

### Requirement: Měkké mazání a souběžné úpravy
`iam.tenants`, `iam.users`, `shop.shops` a `fixes.evidence_items` MUST se mazat měkce přes `deleted_at`; výchozí dotazy EF je MUST NOT vracet. `fixes.fix_proposals`, `fixes.fix_groups`, `fixes.evidence_items` a `billing.subscriptions` MUST mít token souběžnosti `xmin`, takže souběžná úprava stejného řádku MUST jednu ze změn odmítnout.

#### Scenario: Smazání e-shopu
- GIVEN e-shop tenanta A
- WHEN se v EF zavolá `Remove(shop)` a `SaveChangesAsync`
- THEN řádek v databázi zůstane s vyplněným `deleted_at`
- AND `db.Shops` ho nevrátí a `db.Shops.IgnoreQueryFilters(["SoftDelete"])` ano

#### Scenario: Znovu přidaný e-shop
- GIVEN měkce smazaný e-shop `vegis.sk` tenanta A
- WHEN tenant A založí e-shop `vegis.sk` se stejným `base_path`
- THEN uložení projde (jedinečnost platí jen pro nesmazané)

#### Scenario: Dva lidé schvalují stejný návrh
- GIVEN dva kontexty načetly stejný `FixProposal`
- WHEN první uloží změnu stavu a druhý potom také
- THEN druhé uložení skončí `DbUpdateConcurrencyException`

### Requirement: Výčtové hodnoty jako text s omezením
Výčtové sloupce MUST být `text` s omezením `CHECK`, jehož seznam hodnot MUST odpovídat hodnotám .NET enumu v zápisu `snake_case`. Hodnota mimo seznam MUST být odmítnuta databází.

#### Scenario: Omezení odpovídá enumu
- GIVEN databáze po migracích F1
- WHEN test porovná definice `CHECK` v `pg_constraint` s hodnotami enumů z modelu EF
- THEN se pro každý výčtový sloupec shodují

#### Scenario: Neplatná hodnota z čistého SQL
- GIVEN spojení `eshopguard_app` s tenantem A
- WHEN spustí `UPDATE checks.runs SET status = 'done' …`
- THEN PostgreSQL vrátí 23514

### Requirement: Dělení velkých tabulek
`content.pages` MUST být dělená `HASH(shop_id)` na 16 částí, `content.page_versions` `HASH(shop_id)` na 32, `checks.jev_answers` a `checks.sieve_answers` `HASH(tenant_id)` na 32. `shop.connector_events`, `checks.run_events`, `usage.usage_records` a `ops.audit_log` MUST být dělené `RANGE` po kalendářních měsících v UTC bez výchozí části. Primární a jedinečné klíče dělených tabulek MUST obsahovat klíč dělení.

#### Scenario: Počty částí
- GIVEN databáze po migracích F1
- WHEN se přečte `pg_partitioned_table` a `pg_inherits`
- THEN `pages` má 16 částí, `page_versions`, `jev_answers` a `sieve_answers` po 32 a čtyři měsíční tabulky mají část pro aktuální měsíc a aspoň 3 další

#### Scenario: Zápis do měsíce bez části
- GIVEN `checks.run_events` nemá část pro rok 2035
- WHEN se jako `eshopguard_worker` s tenantem A vloží událost s `at` = `2035-01-15`
- THEN PostgreSQL vrátí 23514 („no partition of relation … found for row“) a nic se neuloží

#### Scenario: Hledání stránek podle otisku věty
- GIVEN aktuální verze stránek s `segment_hashes`
- WHEN se spustí `SELECT page_id FROM content.page_versions WHERE shop_id = @s AND is_current AND segment_hashes @> ARRAY[@h]`
- THEN plán dotazu použije index GIN nad `segment_hashes` a prochází jen část odpovídající `shop_id`

### Requirement: Zakládání měsíčních částí dopředu
Funkce `ops.ensure_monthly_partitions(p_months_ahead)` MUST založit chybějící měsíční části čtyř měsíčních tabulek pro aktuální měsíc a `p_months_ahead` dalších a MUST vrátit názvy založených částí. Funkce MUST být idempotentní, bezpečná při souběhu a spustitelná jen rolemi `eshopguard_worker` a `eshopguard_admin`. Služba `PartitionMaintainer` ji MUST volat z knihovny `EshopGuard.Data`.

#### Scenario: Opakované volání
- GIVEN části na aktuální měsíc a 3 další existují
- WHEN `PartitionMaintainer.EnsureMonthlyPartitionsAsync(6)` proběhne dvakrát za sebou
- THEN první volání vrátí 12 nových částí (3 měsíce × 4 tabulky) a druhé žádnou

#### Scenario: Souběžné volání ze dvou workerů
- GIVEN dva workery zavolají funkci ve stejnou chvíli
- WHEN obě volání doběhnou
- THEN žádné neskončí chybou „relation already exists“ a každá část existuje jednou

#### Scenario: Volání aplikační rolí
- GIVEN spojení `eshopguard_app`
- WHEN zavolá `SELECT ops.ensure_monthly_partitions(3)`
- THEN PostgreSQL vrátí 42501

#### Scenario: Nepřípustný rozsah
- GIVEN spojení `eshopguard_worker`
- WHEN zavolá funkci s `p_months_ahead` = 100
- THEN funkce skončí chybou a nic nezaloží
