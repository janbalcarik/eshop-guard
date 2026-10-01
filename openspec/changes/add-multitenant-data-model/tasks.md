# Tasks

Cesty jsou relativní ke kořeni `D:\_github\Overko\eshop-guard`.

> **Stav 1. 10. 2026:** kód je pod `src/` (`src/EshopGuard.Data`, `src/tests/EshopGuard.Data.Tests`). Ověřeno v cloudu proti PostgreSQL 18.6. Index GIN nad `segment_hashes` je podle rozhodnutí z 1. 10. 2026 zrušený (úkol 9.2). Testy kategorie `Db` běží proti `eshopguard_test` (změna 2). Žádný úkol nevolá Jev ani OpenAI.

## 1. Základní typy a konvence

- [x] 1.1 `src/EshopGuard.Data/Entities/Common/`: `ITenantOwned` (`Guid TenantId`), `ISoftDeletable` (`DateTimeOffset? DeletedAt`), `IHasTimestamps`, abstraktní `TenantEntity` (`Id = Guid.CreateVersion7()`, `TenantId`, časy) a `GlobalEntity` (`Id`, časy).
  - Poznámka: `ITenantOwned` je jen značka (bez vlastnosti): `ops.audit_log` má `tenant_id` nepovinné (systémové akce); interceptory a filtr čtou vlastnost `TenantId` přes metadata EF. Rozhraní časů jsou dvě: `IHasCreatedAt` a `IHasTimestamps`.
- [x] 1.2 `Configurations/Conventions/SnakeCaseEnumConverter<TEnum>` a `EnumCheckExtensions.HasEnumCheck(...)` (omezení `ck_<tabulka>_<sloupec>` z hodnot enumu); jednotkový test převodu `KeptWithEvidence` ↔ `kept_with_evidence` a vygenerovaného textu `CHECK`.
- [x] 1.3 `Configurations/Conventions/TenantKeyExtensions.cs`: `HasTenantAlternateKey()`, `HasTenantForeignKey<TPrincipal>(x => x.ShopId)` (→ (`tenant_id`, `id`), `OnDelete(Restrict)`), `HasPartitionedTenantForeignKey<TPrincipal>(…)` (→ (`tenant_id`, `shop_id`, `id`)).
- [x] 1.4 `Configurations/Conventions/TableNames.cs`: konstanty schémat a seznamy 41 tabulek tenanta a 20 globálních tabulek (jediný zdroj pro testy).
  - Poznámka: `TableNames.cs` obsahuje i `Schemas` a `PartitionedTables`. Entity a konfigurace vygeneroval jednorázový skript ze specifikace tabulek (mimo repozitář); dál se upravují ručně.
- [x] 1.5 Zapnout `snake_case` názvy (balíček `EFCore.NamingConventions` pro EF Core 10, pokud je k dispozici; jinak vlastní konvence v `OnModelCreating`) a ověřit na jedné entitě, že vznikne `shop.shops.base_path`.
  - Poznámka: Balíček `EFCore.NamingConventions` 10.0.1. Konvence by přejmenovala i sloupce historie migrací; `EshopGuardHistoryRepository` je drží jako `MigrationId`, `ProductVersion`, aby šly číst databáze po migraci `Initial`.

## 2. Entity a konfigurace po schématech

- [x] 2.1 `iam`: entity `Tenant`, `User`, `UserLogin`, `UserToken`, `Membership`, `Invitation`, `NotificationSetting`, `Notification` + enumy (`TenantStatus`, `PartnerKind`, `MembershipRole`, `UserTokenPurpose`) a konfigurace podle tabulky „Klíče, vazby a indexy“ v designu.
- [x] 2.2 `shop`: `Shop`, `ShopMarket`, `ShopLanguage`, `ShopVerification`, `Connector`, `ConnectorWebhook`, `ConnectorEvent`, `Feed`, `PageProfile`, `ShopFact`, `FreeSampleClaim` + enumy (`ShopPlatform`, `ShopSourceMode`, `ShopStatus`, `ShopMarketStatus`, `EvidenceLevel`, `MarketSource`, `LanguageSwitchMethod`, `LanguageSource`, `ShopLanguageStatus`, `VerificationMethod`, `ConnectorStatus`, `ConnectorAccess`, `FeedFormat`).
- [x] 2.3 `content`: `Page`, `PageVersion` + enumy (`PageType`, `PageSource`, `PageStatus`); `CHECK rotation_bucket BETWEEN 0 AND 6`.
- [x] 2.4 `checks`: `RuleSet`, `Run`, `RunEvent`, `JevAnswer`, `SieveAnswer`, `Finding` (`verdicts` jsonb), `FindingOccurrence`, `Question`, `PageChange` + enumy (`RunKind`, `RunTrigger`, `RunStatus`, `Checkability`, `FindingBand`, `FindingScope`, `FindingStatus`, `QuestionScope`, `QuestionStatus`, `PageChangeSource`, `PageChangeKind`, `PageChangeResult`).
  - Poznámka: `checks.jev_answers` rovnou s klíčem (`tenant_id`, `cache_key`) a `question_set_hash bytea` nepovinným podle novějšího podkladu databáze (úprava 1. 10. 2026 pro změnu 5b), aby změna 5b nemusela tabulku přestavovat.
- [x] 2.5 `fixes`: `FixGroup`, `FixProposal`, `Publication`, `DecisionMemory`, `EvidenceItem`, `EvidenceLink`, `Protocol`, `RewriteCacheEntry` + enumy (`FixGroupKind`, `FixGroupStatus`, `FixField`, `RecheckStatus`, `FixProposalStatus`, `PublicationStatus`, `Decision`, `EvidenceSubjectKind`, `EvidenceKind`, `EvidenceSource`, `EvidenceStatus`); `Version` (`xmin`) u `FixGroup`, `FixProposal`, `EvidenceItem`.
- [x] 2.6 `billing`: `PriceList`, `PriceTier`, `VolumeDiscount`, `PromoCode`, `PaymentMethod`, `Order`, `Subscription`, `SubscriptionChange`, `Payment`, `Invoice`, `StripeEvent` + enumy (`PriceListStatus`, `OrderKind`, `OrderStatus`, `SubscriptionStatus`, `BillingInterval`, `SubscriptionChangeKind`, `PaymentStatus`, `InvoiceKind`, `EinvoiceStatus`); `Version` (`xmin`) u `Subscription`.
- [x] 2.7 `usage`: `UsageRecord`, `UsageDaily` + enumy (`UsageProvider`, `UsageOperation`).
- [x] 2.8 `ops`: `Job` (`priority` `smallint` 0–4), `Worker`, `Domain`, `RateLimitBucket`, `Schedule` (PK (`shop_id`, `kind`) podle K rozhodnutí 5), `OutboxMessage`, `AuditLogEntry`, `SystemSetting` + enumy (`JobResourceClass`, `JobState`, `ScheduleKind`, `OutboxKind`, `AuditActorKind`); indexy `ops.jobs` podle podkladu (`resource_class`, `priority`, `not_before`, `id`) WHERE `state = 'queued'` a (`lease_until`) WHERE `state = 'running'`.
  - Poznámka: Entity `ops.workers` a `ops.domains` se jmenují `WorkerNode` a `CrawlDomain`: `Worker` by se v projektu `EshopGuard.Worker` krylo se jmenným prostorem a třída `Domain` nemůže mít vlastnost `Domain`.
- [x] 2.9 `ref`: `Market`, `Locale` + enumy (`MarketWebStatus`, `MarketChecksStatus`); `HasData` pro `ref.locales` (`sk`, `cs`, `enabled = false`) a `ref.markets` (`sk` podle designu).
  - Poznámka: Rozhodnuto 1. 10. 2026 (uživatel): `ref.markets` `sk` (EUR, kontroly `full`) i `cz` (CZK, kontroly `limited`, v Česku zatím jen modul `ucp`).
- [x] 2.10 `EshopGuardDb`: `DbSet` pro všech 61 entit, `ApplyConfigurationsFromAssembly`, konstruktor s `ITenantContext`, `Database.AutoTransactionBehavior = AutoTransactionBehavior.Always`.
  - Poznámka: `DbSet` má všech 61 entit; společné výchozí hodnoty sloupců (`uuidv7()`, `now()`, `false`, `{}`) nastavuje `EshopGuardDb.ApplyColumnDefaults`.

## 3. Kontext tenanta a interceptory

- [x] 3.1 `Tenancy/ITenantContext.cs`, `TenantContext.cs` (scoped, druhé `Set` s jiným tenantem → `InvalidOperationException`), `TenantNotSetException` (`Code = "tenant.not_set"`), `CrossTenantWriteException` (`Code = "tenant.cross_write"`, jen název typu entity).
- [x] 3.2 `Tenancy/TenantTransactionInterceptor.cs`: `TransactionStarted(Async)` a `TransactionUsed(Async)` → `SELECT set_config('app.tenant_id', @t, true), set_config('app.user_id', @u, true)` s parametry `NpgsqlParameter` (text z `Guid.ToString("D")`).
- [x] 3.3 `Tenancy/TenantSaveChangesInterceptor.cs` (doplnění, odmítnutí cizího a změny `TenantId`), `TimestampSaveChangesInterceptor.cs` (`TimeProvider`), `SoftDeleteSaveChangesInterceptor.cs` (`Deleted` → `DeletedAt`, výjimka přes `db.HardDelete(entity)`).
  - Poznámka: Všechny tři interceptory ukládání jsou v `Tenancy/SaveChangesInterceptors.cs`; pořadí: měkké mazání → kontrola tenanta → časy. `EshopGuardDbOptions.UseEshopGuardInterceptors` je přidává do aplikace i testů.
- [x] 3.4 Pojmenované filtry v `EshopGuardDb.OnModelCreating`: `"Tenant"` pro všechny `ITenantOwned` (výraz přes vlastnost `CurrentTenantId => _tenantContext.RequireTenantId()`), `"SoftDelete"` pro `ISoftDeletable`.
- [x] 3.5 `Tenancy/TenantDbContextExtensions.ExecuteInTenantTransactionAsync<T>(…)` (strategie provádění EF, kontrola tenanta) a `Tenancy/TenantSql.BeginAsync(NpgsqlConnection, Guid, Guid?, CancellationToken)`.
- [x] 3.6 `DataServiceCollectionExtensions`: registrace `ITenantContext` (scoped), čtyř interceptorů a `TimeProvider.System`; `AddDbContext` je přidá přes `AddInterceptors`.

## 4. Migrace

- [x] 4.1 `dotnet ef migrations add F1DataModel --project src/EshopGuard.Data` jako vlastník (návrhová továrna ze změny 2); zkontrolovat, že vznikne `EnsureSchema` pro devět schémat a `CreateTable` pro 61 tabulek.
- [x] 4.2 `Migrations/Sql/F1/01_functions.sql` (`ops.current_tenant_id()` podle designu, `GRANT EXECUTE` třem rolím) a jeho volání v `Up` před prvním `CreateTable`.
- [x] 4.3 `Migrations/Sql/F1/02_partitioned_tables.sql`: v `F1DataModel.Up` nahradit `CreateTable` osmi dělených tabulek ručním `CREATE TABLE … PARTITION BY …` se stejnými sloupci a klíči, HASH části `_p00`…; ponechat pořadí kvůli cizím klíčům; cizí klíč `pages.current_version_id` přidat po `page_versions`.
  - Poznámka: Odchylka od návrhu: `CREATE TABLE` dělených tabulek generuje EF a `EshopGuardMigrationsSqlGenerator` připojí `PARTITION BY` podle `TableNames.PartitionedTables`; sloupce, klíče i cizí klíče tak zůstávají z EF (stejné názvy jako ve snímku modelu). Části HASH zakládá výslovné SQL `02_partitions.sql`.
- [x] 4.4 `Migrations/Sql/F1/03_expression_indexes.sql`: `CREATE UNIQUE INDEX ux_users_email_lower ON iam.users (lower(email)) WHERE deleted_at IS NULL`; ověřit, že částečné jedinečnosti (`shops`, `findings`, `page_versions`, `subscriptions`, `payment_methods`) a `NULLS NOT DISTINCT` (`notification_settings`) vygeneroval EF z konfigurace.
- [x] 4.5 `Down` migrace `F1DataModel`: smazání v opačném pořadí včetně dělených tabulek a funkce `ops.current_tenant_id()`.
- [x] 4.6 `dotnet ef migrations add F1RowLevelSecurity`: `Up` = `Migrations/Sql/F1/04_rls.sql` (41 tabulek výčtem: `ENABLE`, `FORCE`, `CREATE POLICY tenant_isolation`) a `05_grants.sql` (matice z designu, `USAGE` na schématech a sekvencích); `Down` = `DROP POLICY`, `NO FORCE`, `DISABLE`, `REVOKE`.
- [x] 4.7 `dotnet ef migrations add F1PartitionMaintenance`: `Up` = `06_partition_maintenance.sql` (funkce `ops.ensure_monthly_partitions`, `REVOKE` z `PUBLIC`, `GRANT EXECUTE` worker a admin, `SELECT ops.ensure_monthly_partitions(3)`); `Down` = `DROP FUNCTION` (části zůstávají, smaže je `Down` F1DataModel s rodiči).
- [x] 4.8 `Migrations/SqlResource.cs` a `EmbeddedResource Include="Migrations/Sql/**/*.sql"` v `EshopGuard.Data.csproj`; chybějící prostředek = výjimka s názvem souboru.
- [x] 4.9 `dotnet ef database update` na `eshopguard` i `eshopguard_test`; `dotnet ef migrations has-pending-model-changes --project src/EshopGuard.Data` nic nehlásí; `dotnet ef database update Initial` a znovu nahoru na `eshopguard_test` projde.
  - Poznámka: Cloud: `eshopguard` i `eshopguard_test` nahoru, `eshopguard_test` zpět na `Initial` (tabulky i funkce zmizely, v historii jen `Initial`) a znovu nahoru; `has-pending-model-changes` bez změn.

## 5. Údržba částí

- [x] 5.1 `Maintenance/PartitionMaintainer.cs`: `EnsureMonthlyPartitionsAsync(int monthsAhead, CancellationToken)` (volá funkci přes `NpgsqlDataSource` workeru, vrací názvy, zapíše je do logu) a `GetMonthlyHorizonAsync(CancellationToken)` (z `pg_inherits` a `pg_class.relpartbound` poslední pokrytý měsíc každé ze čtyř tabulek).
- [x] 5.2 Registrace `PartitionMaintainer` v `AddEshopGuardData` jen pro `DatabaseRole.Worker` a `DatabaseRole.Admin` (API ji nemá).

## 6. Testovací infrastruktura

- [x] 6.1 `tests/EshopGuard.Data.Tests/Isolation/TwoTenantsFixture.cs`: založí nové tenanty A a B (nová `uuid` při každém běhu, takže netřeba nic mazat a testy nepřekáží souběžně běžícím `EshopGuard.Jobs.Tests` ve stejné databázi), uživatele s jedinečným e-mailem `test-<guid>@example.invalid`, jeden `rule_sets` a jeden `price_lists`. Starší data z předchozích běhů RLS skryje; testy počítají jen s řádky A a B. `TRUNCATE` se v `EshopGuard.Data.Tests` nepoužívá.
  - Poznámka: Kolekce `Db` má dvě fixture: `PostgresTestDatabase` a `TwoTenantsFixture`.
- [x] 6.2 `Isolation/TenantDataSeeder.cs`: pro každý ze dvou tenantů jeden platný řádek ve všech 41 tabulkách tenanta (e-shop `vegis.sk`, `base_path` `/`), zápis přes EF jako `eshopguard_app` v `ExecuteInTenantTransactionAsync`; vrací seznam tabulek, do kterých zapsal.
- [x] 6.3 `Isolation/SeederCoverageTests.cs`: seznam ze seederu = seznam tabulek s RLS z katalogu = `TableNames.TenantTables`.

## 7. Testy izolace

- [x] 7.1 `Isolation/TenantIsolationSqlTests.cs` (`[Theory]` přes tabulky tenanta z katalogu, role `App` i `Worker`): `BEGIN; set_config(A)`; `SELECT count(*), count(*) FILTER (WHERE tenant_id <> A)` → (> 0, 0).
- [x] 7.2 `Isolation/TenantIsolationEfTests.cs` (`[Theory]` přes typy `ITenantOwned` z modelu EF): s filtrem i s `IgnoreQueryFilters(["Tenant"])` jen řádky A a aspoň jeden.
- [x] 7.3 `Isolation/CrossTenantWriteTests.cs`: EF `Add` s `TenantId` B → `CrossTenantWriteException` bez odeslaného příkazu; EF změna `TenantId` → výjimka; čisté SQL `INSERT INTO shop.shops … tenant_id = B` → 42501; pro všech 41 tabulek `UPDATE … SET tenant_id = B` → 42501 (zapsat, pokud by některá tabulka vrátila 23503 dřív než 42501, a ukázat uživateli).
  - Poznámka: Všech 41 tabulek vrátilo při `UPDATE … SET tenant_id = B` 42501, žádná dřív 23503. Změnu `TenantId`, který je součástí alternativního klíče (`tenant_id`, `id`), odmítne už EF (`InvalidOperationException`); u tabulek bez takového klíče `CrossTenantWriteException`. Testy interceptoru ukládání jsou v `CrossTenantWriteTests` (soubor `TenantSaveChangesInterceptorTests.cs` proto nevznikl).
- [x] 7.4 `Isolation/CompositeForeignKeyTests.cs` (role `Admin`): stránka tenanta A s e-shopem B → 23503; `finding_occurrences` A se stránkou B → 23503; `fix_proposals` A se skupinou B → 23503.
- [x] 7.5 `Isolation/MissingTenantContextTests.cs`: SQL bez kontextu → 42501 „app.tenant_id is not set“; na stejném spojení po skončené transakci → 42501; neplatný text v `app.tenant_id` → 22P02; EF bez kontextu → `TenantNotSetException` a žádný příkaz.
- [x] 7.6 `Tenancy/TenantTransactionTests.cs`: `SaveChanges` s jedním `Shop` běží v transakci s `app.tenant_id` = A (ověřit přes `DbCommandInterceptor` v testu, který přečte `current_setting`); po potvrzení na stejném spojení prázdná hodnota; druhé `Set` s B → `InvalidOperationException`.

## 8. Katalogové a modelové testy

- [x] 8.1 `Catalog/RlsCatalogTests.cs`: každá tabulka v devíti schématech (bez částí, `relispartition = false`) je buď v `TableNames.TenantTables` s `relrowsecurity`, `relforcerowsecurity` a politikou `tenant_isolation` (`USING` i `WITH CHECK` volá `ops.current_tenant_id`), nebo v `TableNames.GlobalTables`; jinak selhání s názvem tabulky.
- [x] 8.2 `Catalog/GrantsTests.cs`: podle matice z designu přes `has_table_privilege`; `app` a `worker` bez `TRUNCATE`, `REFERENCES`, `TRIGGER` nikde; žádné právo `app`/`worker`/`admin` na částech (`pages_p00`, `run_events_y…`); `eshopguard_cms` bez práv v devíti schématech; `PUBLIC` bez práv k tabulkám.
- [x] 8.3 `Catalog/EnumCheckConstraintTests.cs`: pro každou vlastnost typu enum v modelu EF existuje `CHECK` se stejnou množinou hodnot; `UPDATE checks.runs SET status = 'done'` → 23514.
- [x] 8.4 `Catalog/ModelCatalogConsistencyTests.cs`: typy `ITenantOwned` v modelu EF = `TableNames.TenantTables`; globální entity s vlastností `TenantId` neimplementují `ITenantOwned`.
- [x] 8.5 `Model/UuidV7Tests.cs`: `Id` z EF má verzi 7 a roste; `INSERT` čistým SQL bez `id` dostane verzi 7.
- [x] 8.6 `Model/SoftDeleteTests.cs`: `Remove(shop)` nastaví `deleted_at`, výchozí dotaz ho nevrátí, `IgnoreQueryFilters(["SoftDelete"])` ano; nový e-shop se stejnou doménou a `base_path` po měkkém smazání projde; dva nesmazané se stejnou doménou → 23505.
- [x] 8.7 `Model/ConcurrencyTokenTests.cs`: souběžná úprava `FixProposal`, `FixGroup`, `EvidenceItem`, `Subscription` → druhá `DbUpdateConcurrencyException`.
- [x] 8.8 `Model/TimestampTests.cs` (`created_at` při vložení, `updated_at` po úpravě, obojí s posunem 0) a `Model/RefSeedTests.cs` (`ref.locales` `sk`, `cs`; `ref.markets` `sk` s `EUR`).

## 9. Testy dělení

- [x] 9.1 `Partitioning/PartitioningTests.cs`: strategie a počty částí z `pg_partitioned_table` a `pg_inherits` podle designu; RANGE tabulky mají část pro aktuální měsíc a 3 další; vložení do `checks.run_events` s `at` v roce 2035 → 23514.
- [x] 9.2 V `PartitioningTests` ověřit `EXPLAIN` dotazu `page_versions … WHERE shop_id = @s AND is_current AND segment_hashes @> ARRAY[@h]`: prochází jednu část a používá index GIN (při malém objemu dat vypnout `enable_seqscan` jen v testu).
  - Poznámka: Zjištění 1. 10. 2026: pod RLS PostgreSQL index GIN nad `segment_hashes` nepoužije, protože operátor `@>` (`arraycontains`) není `LEAKPROOF` a nesmí běžet před politikou. Změřeno (jeden běh): e-shop s 20 000 aktuálními verzemi, hledání věty jako `eshopguard_app` 9–10 ms (index tenanta + filtr), jako `eshopguard_admin` 7,6 ms (sekvenčně, GIN ani tam nevyhrál). Test ověřuje průchod jediné části. **Rozhodnuto 1. 10. 2026 (uživatel): index GIN zrušen** (odebrán z konfigurace i z migrace `F1DataModel`, která ještě nebyla v `main`).
- [x] 9.3 `Partitioning/PartitionMaintenanceTests.cs`: `EnsureMonthlyPartitionsAsync(6)` vrátí 12 částí, druhé volání 0; dvě souběžná volání bez chyby; `eshopguard_app` → 42501; `p_months_ahead = 100` → chyba; `GetMonthlyHorizonAsync` vrátí měsíc posledního volání.
  - Poznámka: Test před během smaže měsíční části za 3. měsícem (prázdné v `eshopguard_test`), aby počet nově založených nezávisel na předchozích bězích.

## 10. Dokumentace

- [x] 10.1 `README.md`, oddíl „Databáze“: schémata, pravidlo „data tenanta jen v transakci s `ExecuteInTenantTransactionAsync`“, seznam globálních tabulek, jak přidat tabulku tenanta (entita `TenantEntity`, konfigurace, `ENABLE` + `FORCE` + politika `tenant_isolation` a práva v SQL nové migrace, zápis do `TableNames`, řádek v `TenantDataSeeder`) a že katalogový test jinak selže.

## 11. Ověření

- [x] 11.1 `dotnet build EshopGuard.sln`: 0 chyb.
- [x] 11.2 `dotnet ef migrations has-pending-model-changes --project src/EshopGuard.Data`: žádné změny.
- [x] 11.3 `dotnet test EshopGuard.sln` (filtr bez `Jev`): všechny testy `EshopGuard.Data.Tests` projdou; `TenantIsolationSqlTests` a `TenantIsolationEfTests` mají po 41 případech (× 2 role u SQL); `EshopGuard.Core.Tests` má stejný počet jako po změně 1.
  - Poznámka: Cloud: `EshopGuard.Data.Tests` 266 testů (82 izolace SQL, 41 izolace EF, 41 přesun mezi tenanty, …), všechny prošly.
- [x] 11.4 Ručně v `psql` jako `eshopguard_app`: `SELECT count(*) FROM shop.shops;` bez kontextu → chyba 42501; `BEGIN; SELECT set_config('app.tenant_id', '<A>', true); SELECT domain FROM shop.shops; COMMIT;` → jen e-shop tenanta A.
  - Poznámka: Cloud: bez kontextu „ERROR: app.tenant_id is not set“, s kontextem jen e-shopy jednoho tenanta.
- [x] 11.5 V `psql` jako `eshopguard_owner` (jen čtení katalogu, `postgres` se používá jen pro `00_roles.sql`): `SELECT count(*) FROM pg_policy WHERE polname = 'tenant_isolation'` = 41; `\d+ content.pages` ukáže 16 částí.
  - Poznámka: Cloud (čteno jako `postgres`): 61 tabulek, 41 politik, `content.pages` 16 částí, další tři po 32, měsíční po 4.
