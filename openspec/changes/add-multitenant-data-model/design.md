# Design: Datový model pro více zákazníků

## Technical Approach

Cesty jsou relativní ke kořeni `D:\_github\Overko\eshop-guard`. Sloupce tabulek jsou přesně podle části 3 podkladu `databaze-a-plan-implementace-2026-10-01.md`; tento design k nim doplňuje typy, klíče, vazby, izolaci a dělení.

### Konvence typů

| Vzor | Typ PostgreSQL | .NET |
|---|---|---|
| `id` tabulek s `uuid` | `uuid NOT NULL DEFAULT uuidv7()` | `Guid`, v konstruktoru `Guid.CreateVersion7()`, EF `ValueGeneratedNever()` + `HasDefaultValueSql("uuidv7()")` |
| `id` tabulek jen s přidáváním (`connector_events`, `run_events`, `page_changes`, `usage_records`, `jobs`, `outbox`, `audit_log`) | `bigint GENERATED ALWAYS AS IDENTITY` | `long`, `UseIdentityAlwaysColumn()` |
| `*_id` (odkaz) | `uuid` (v `usage_records.job_id` `bigint`) | `Guid` / `Guid?` / `long?` |
| `created_at`, `updated_at` | `timestamptz NOT NULL DEFAULT now()` | `DateTimeOffset` (UTC) |
| ostatní `*_at`, `valid_from`, `valid_until`, `trial_end`, `current_period_*`, `effective_at`, `founder_until` | `timestamptz` (nepovinné, pokud podklad neříká jinak) | `DateTimeOffset?` |
| `day` (`usage_daily`), `period_from`, `period_to`, `taxable_supply_date`, `due_date` | `date` | `DateOnly` |
| výčty (`status`, `kind`, `role`, `platform`, …) | `text NOT NULL` + `CHECK (sloupec IN (…))` | .NET `enum`, převod `SnakeCaseEnumConverter<T>` |
| ceny pro zákazníka (`amount_*`, `analysis_price`, `monitoring_*`, `unit_price`, `refunded_amount`, `discount_amount`, `vat_amount`) | `numeric(12,2)` | `decimal` |
| interní náklady (`cost_usd`) | `numeric(14,6)` | `decimal` |
| `percent`, `discount_percent`, `vat_rate` | `numeric(5,2)` | `decimal` |
| `currency` | `char(3)` | `string` |
| počty (`product_count`, `page_count`, `occurrences`, `attempts`, `number`, `*_chars`, `calls`, `cache_hits`) | `integer`; tokeny a bajty (`*_tokens`, `bytes_in`) `bigint` | `int` / `long` |
| `segment_hash`, `url_hash` | `bigint` | `long` |
| `segment_hashes` | `bigint[]` | `long[]` |
| `probabilities` | `real[]` | `float[]` |
| ostatní otisky (`state_hash`, `chunk_hash`, `question_set_hash`, `source_hash`, `text_hash`, `token_hash`, `old_value_hash`, `webhook_secret_hash`, `requested_ip_hash`) | `bytea` (SHA-256, 32 B) | `byte[]` |
| `jsonb` sloupce podle podkladu | `jsonb` | `JsonDocument` (jen čtení a zápis celku) |
| `text[]`, `uuid[]` (`modules`, `jurisdictions`, `scopes`, `sitemaps`, `ui_locales`, `rule_set_ids`, `finding_ids`, `fix_proposal_ids`, `excluded_page_ids`) | `text[]`, `uuid[]` | `string[]`, `Guid[]` |
| `credentials_enc` | `bytea` | `byte[]` |
| `audit_log.ip` | `inet` | `IPAddress?` |
| příznaky (`is_home`, `counted`, `is_current`, `draining`, …) | `boolean NOT NULL DEFAULT false` | `bool` |
| `xmin` (token souběžnosti) | systémový sloupec | `uint Version` s `IsRowVersion()` (Npgsql ho mapuje na `xmin`) |
| `ops.rate_limit_buckets.capacity`, `tokens`, `refill_per_sec`; `ops.domains.rate` | `double precision` | `double` |
| `ops.domains.crawl_delay_ms`, `consecutive_errors`; `ops.jobs.attempts`, `max_attempts` | `integer` | `int` |
| `ops.domains.lease_job_id` | `bigint` (= `ops.jobs.id`, bez cizího klíče, úlohy se mažou) | `long?` |

`created_at` má každá tabulka (včetně `ops.jobs`, kde slouží pro stáří nejstarší úlohy). `updated_at` mají tabulky, jejichž řádky se mění (včetně `ops.jobs`, `ops.domains`, `ops.rate_limit_buckets`), ne tabulky jen s přidáváním (`run_events`, `connector_events`, `usage_records`, `audit_log`, `page_changes`) a cache odpovědí (`jev_answers`, `sieve_answers`).

Nepovinné jsou sloupce, u kterých podklad uvádí „null = …“, odkazy vznikající později v životním cyklu (`*_run_id` kromě `runs.id`, `stripe_*`, `superfaktura_*`, `order_id`, `evidence_id`, `group_id`), časy událostí kromě `created_at` / `updated_at` a texty chyb (`error`, `last_error`). Vlastnické sloupce (`tenant_id`, `shop_id`) jsou povinné kromě `shop_facts.shop_id`, `notification_settings.shop_id`, `notifications.shop_id`, `notifications.user_id`, `decision_memory.shop_id` (null = všechny) a `ops.audit_log.tenant_id` (systémová akce).

### Rozdělení tabulek: tenant (RLS) a globální

**41 tabulek tenanta** (`ITenantOwned`, RLS s `FORCE`, politika `tenant_isolation`):
- `iam`: `memberships`, `invitations`, `notification_settings`, `notifications`;
- `shop`: `shops`, `shop_markets`, `shop_languages`, `shop_verifications`, `connectors`, `connector_webhooks`, `connector_events`, `feeds`, `page_profiles`, `shop_facts`;
- `content`: `pages`, `page_versions`;
- `checks`: `runs`, `run_events`, `jev_answers`, `sieve_answers`, `findings`, `finding_occurrences`, `questions`, `page_changes`;
- `fixes`: `fix_groups`, `fix_proposals`, `publications`, `decision_memory`, `evidence_items`, `evidence_links`, `protocols`, `rewrite_cache`;
- `billing`: `payment_methods`, `orders`, `subscriptions`, `subscription_changes`, `payments`, `invoices`;
- `ops`: `schedules`, `outbox`, `audit_log` (proposal, K rozhodnutí 1).

**20 globálních tabulek** (bez RLS; žádné texty zákazníků):
- `iam.tenants`, `iam.users`, `iam.user_logins`, `iam.user_tokens` (podklad: bez RLS, přístup přes členství; proposal K rozhodnutí 3);
- `shop.free_sample_claims`, `checks.rule_sets`;
- `billing.price_lists`, `billing.price_tiers`, `billing.volume_discounts`, `billing.promo_codes`, `billing.stripe_events`;
- `usage.usage_records`, `usage.usage_daily`;
- `ops.jobs`, `ops.workers`, `ops.domains`, `ops.rate_limit_buckets`, `ops.system_settings`;
- `ref.markets`, `ref.locales`.

Globální tabulky, které nesou `tenant_id` (`free_sample_claims`, `promo_codes`, `stripe_events`, `usage_*`, `jobs`), mají obyčejnou vlastnost `Guid? TenantId` a **neimplementují** `ITenantOwned`, takže na ně nepůsobí filtr EF ani interceptor.

### Klíče, vazby a indexy

Zkratky: AK = alternativní klíč (`tenant_id`, `id`) pro složené cizí klíče; FKt(x) = složený cizí klíč (`tenant_id`, `x_id`) → (`tenant_id`, `id`) cílové tabulky; FKp(x) = trojice (`tenant_id`, `shop_id`, `x_id`) → (`tenant_id`, `shop_id`, `id`) u dělených tabulek; FK = obyčejný cizí klíč na globální tabulku. Všechny cizí klíče `ON DELETE RESTRICT` (mazání tenanta řeší úloha), kromě `user_logins.user_id` (`CASCADE`).

| Tabulka | PK | Jedinečnost a indexy | Cizí klíče |
|---|---|---|---|
| `iam.tenants` | `id` | I `market_code` | FK `market_code` → `ref.markets(code)`, `locale` → `ref.locales(code)` |
| `iam.users` | `id` | U `lower(email)` WHERE `deleted_at IS NULL` (SQL) | FK `locale` → `ref.locales` |
| `iam.user_logins` | `id` | U (`provider`, `provider_key`) | FK `user_id` → `users` |
| `iam.user_tokens` | `id` | U `token_hash`; I (`email`, `purpose`) WHERE `used_at IS NULL` | FK `user_id` → `users` (nepovinný) |
| `iam.memberships` | (`tenant_id`, `user_id`) | I `user_id` | FK `tenant_id` → `tenants`, `user_id`, `invited_by` → `users` |
| `iam.invitations` | `id` | AK; U `token_hash`; I (`tenant_id`, `email`) | FK `tenant_id`, `invited_by` |
| `iam.notification_settings` | `id` | U (`tenant_id`, `user_id`, `shop_id`) `NULLS NOT DISTINCT` | FK `user_id`; FKt(`shop`) |
| `iam.notifications` | `id` | I (`tenant_id`, `user_id`, `read_at`) | FK `user_id`; FKt(`shop`) |
| `shop.shops` | `id` | AK; U (`tenant_id`, `domain`, `base_path`) WHERE `deleted_at IS NULL` | FK `tenant_id`; FKt(`last_full_run` → `checks.runs`) |
| `shop.shop_markets` | (`shop_id`, `country_code`) | – | FKt(`shop`), FKt(`detection_run`), FK `confirmed_by` |
| `shop.shop_languages` | (`shop_id`, `language`) | – | FKt(`shop`), FKt(`sample_run`) |
| `shop.shop_verifications` | `id` | AK | FKt(`shop`) |
| `shop.connectors` | `id` | AK; U (`tenant_id`, `shop_id`); I (`platform`, `external_shop_id`) | FKt(`shop`) |
| `shop.connector_webhooks` | `id` | AK | FKt(`connector`) |
| `shop.connector_events` | (`id`, `received_at`) | I (`connector_id`, `dedupe_key`) | FKt(`connector`), FKt(`shop`) |
| `shop.feeds` | `id` | AK | FKt(`shop`) |
| `shop.page_profiles` | `id` | AK; U (`shop_id`, `number`) | FKt(`shop`), FKt(`created_run`) |
| `shop.shop_facts` | `id` | AK | FKt(`shop`) nepovinný, FK `created_by` |
| `shop.free_sample_claims` | `domain` | – | FK `tenant_id`; FKt(`shop`) |
| `content.pages` | (`shop_id`, `id`) | U (`tenant_id`, `shop_id`, `id`); U (`shop_id`, `url_hash`); I (`shop_id`, `next_check_at`); `CHECK rotation_bucket BETWEEN 0 AND 6` | FKt(`shop`), FKt(`profile` → `page_profiles`), FKp(`current_version` → `page_versions`) přidaný až po `page_versions` |
| `content.page_versions` | (`shop_id`, `id`) | U (`tenant_id`, `shop_id`, `id`); U (`shop_id`, `page_id`) WHERE `is_current`; I (`shop_id`, `page_id`, `fetched_at`); bez indexu GIN (rozhodnutí 11) | FKp(`page`), FKt(`run`) |
| `checks.rule_sets` | `id` | U (`module`, `version`) | – |
| `checks.runs` | `id` | AK; I (`tenant_id`, `shop_id`, `created_at` DESC) | FKt(`shop`), FKt(`order` → `billing.orders`), FK `requested_by` |
| `checks.run_events` | (`id`, `at`) | I (`run_id`, `id`) | FKt(`run`) |
| `checks.jev_answers` | (`tenant_id`, `cache_key`) (podklad upravený 1. 10. 2026 pro změnu 5b; `question_set_hash` nepovinný sloupec) | – | FK `tenant_id` |
| `checks.sieve_answers` | (`tenant_id`, `question_set_hash`, `chunk_hash`) | – | FK `tenant_id` |
| `checks.findings` | `id` | AK; U (`shop_id`, `rule_id`, `segment_hash`) WHERE `scope = 'segment'`; I (`tenant_id`, `shop_id`, `status`) | FKt(`shop`), FK `rule_set_id`, FKp(`page`), FKt(`first_run`, `last_seen_run`, `resolved_run`) |
| `checks.finding_occurrences` | (`finding_id`, `page_id`) | I (`shop_id`, `page_id`) | FKt(`finding`), FKp(`page`) |
| `checks.questions` | `id` | AK; I (`tenant_id`, `shop_id`, `status`) | FKt(`shop`), FKt(`finding`), FKt(`evidence` → `fixes.evidence_items`), FK `answered_by` |
| `checks.page_changes` | `id` | I (`tenant_id`, `shop_id`, `detected_at` DESC) | FKt(`shop`), FKp(`page`), FKt(`run`) |
| `fixes.fix_groups` | `id` | AK; I (`tenant_id`, `shop_id`, `status`) | FKt(`shop`), FK `approved_by` |
| `fixes.fix_proposals` | `id` | AK; I (`tenant_id`, `shop_id`, `status`) | FKt(`shop`), FKp(`page`), FKp(`page_version`), FKt(`group` → `fix_groups`), FKt(`created_run`), FK `decided_by` |
| `fixes.publications` | `id` | AK; U `idempotency_key` | FKt(`shop`), FKt(`connector`), FKp(`page`), FK `requested_by` |
| `fixes.decision_memory` | `id` | AK; I (`tenant_id`, `shop_id`, `segment_hash`) WHERE `superseded_at IS NULL` | FKt(`shop`) nepovinný, FKt(`evidence`), FKt(`source_proposal` → `fix_proposals`), FK `created_by` |
| `fixes.evidence_items` | `id` | AK; I (`tenant_id`, `status`) | FK `created_by` |
| `fixes.evidence_links` | `id` | AK | FKt(`evidence`), FKt(`shop`), FKp(`page`) nepovinný, FKt(`finding`) nepovinný |
| `fixes.protocols` | `id` | AK; U (`tenant_id`, `number`) | FKt(`shop`), FK `generated_by` |
| `fixes.rewrite_cache` | (`tenant_id`, `key`) | – | FK `tenant_id` |
| `billing.price_lists` | `id` | I (`market_code`, `valid_from`) | FK `market_code` → `ref.markets` |
| `billing.price_tiers` | `id` | U (`price_list_id`, `code`) | FK `price_list_id` |
| `billing.volume_discounts` | `id` | U (`price_list_id`, `from_shop_number`) | FK `price_list_id` |
| `billing.promo_codes` | `id` | U `code` | FK `tenant_id` nepovinný |
| `billing.payment_methods` | `id` | AK; U `stripe_payment_method_id`; U (`tenant_id`) WHERE `is_default AND detached_at IS NULL` | FK `tenant_id` |
| `billing.orders` | `id` | AK; U `stripe_checkout_session_id` | FKt(`shop`), FK `price_list_id`, FKt(`run`), FK `created_by` |
| `billing.subscriptions` | `id` | AK; U (`shop_id`) WHERE `status <> 'canceled'` (K rozhodnutí 14); U `stripe_subscription_id` | FKt(`shop`), FK `price_list_id` |
| `billing.subscription_changes` | `id` | AK | FKt(`subscription`) |
| `billing.payments` | `id` | AK | FKt(`order`), FKt(`subscription`) |
| `billing.invoices` | `id` | AK; I (`tenant_id`, `issued_at` DESC) | FKt(`shop`), FKt(`payment`), FKt(`credit_note_for` → `invoices`) |
| `billing.stripe_events` | `id` (text `evt_…`) | I `tenant_id` | – |
| `usage.usage_records` | (`id`, `occurred_at`) | I (`tenant_id`, `occurred_at`); I `run_id` | žádné (K rozhodnutí 12) |
| `usage.usage_daily` | (`day`, `tenant_id`, `shop_id`, `provider`, `operation`) | – | – |
| `ops.jobs` | `id` | U `dedupe_key`; I (`resource_class`, `priority`, `not_before`, `id`) WHERE `state = 'queued'`; I (`lease_until`) WHERE `state = 'running'`; I `run_id` | – |
| `ops.workers` | `id` (text `host:pid`) | – | – |
| `ops.domains` | `domain` | – | – |
| `ops.rate_limit_buckets` | `key` | – | – |
| `ops.schedules` | (`shop_id`, `kind`) (K rozhodnutí 5) | I `next_run_at` | FKt(`shop`) |
| `ops.outbox` | `id` | I (`created_at`) WHERE `sent_at IS NULL` | FK `tenant_id` |
| `ops.audit_log` | (`id`, `at`) | I (`tenant_id`, `at` DESC) | žádné (K rozhodnutí 12) |
| `ops.system_settings` | `key` | – | – |
| `ref.markets` | `code` | – | FK `price_list_id` → `billing.price_lists` (nepovinný) |
| `ref.locales` | `code` | – | FK `fallback_code` → `ref.locales` |

Indexy pro cizí klíče vytváří EF sám (u složených klíčů index (`tenant_id`, `x_id`)). Jedinečnost s `WHERE` přes `HasFilter`, `NULLS NOT DISTINCT` přes `AreNullsDistinct(false)`, výrazový index na `lower(email)` ručním SQL.

### Dělené tabulky

| Tabulka | Dělení | Části | Názvy částí |
|---|---|---|---|
| `content.pages` | `PARTITION BY HASH (shop_id)` | 16 | `content.pages_p00` … `_p15` |
| `content.page_versions` | `PARTITION BY HASH (shop_id)` | 32 | `content.page_versions_p00` … `_p31` |
| `checks.jev_answers` | `PARTITION BY HASH (tenant_id)` | 32 | `checks.jev_answers_p00` … `_p31` |
| `checks.sieve_answers` | `PARTITION BY HASH (tenant_id)` | 32 (K rozhodnutí 9) | `checks.sieve_answers_p00` … `_p31` |
| `shop.connector_events` | `PARTITION BY RANGE (received_at)` | po měsících | `shop.connector_events_y2026m10` … |
| `checks.run_events` | `PARTITION BY RANGE (at)` | po měsících | `checks.run_events_y2026m10` … |
| `usage.usage_records` | `PARTITION BY RANGE (occurred_at)` | po měsících | `usage.usage_records_y2026m10` … |
| `ops.audit_log` | `PARTITION BY RANGE (at)` | po měsících | `ops.audit_log_y2026m10` … |

- Migrace vygenerovaná EF má pro tyto tabulky `CreateTable`; `EshopGuardMigrationsSqlGenerator` k jejich `CREATE TABLE` připojí `PARTITION BY …` podle `TableNames.PartitionedTables` (změna oproti původnímu návrhu s ručním `CREATE TABLE`: sloupce, klíče a cizí klíče tak zůstávají z EF se stejnými názvy jako ve snímku modelu). HASH části zakládá výslovné SQL `F1/02_partitions.sql` na konci migrace. Indexy (`CreateIndex`) zůstávají z EF; na rodiči dělené tabulky se vytvoří i na částech. `CREATE INDEX CONCURRENTLY` na rodiči nejde, další migrace proto indexy dělených tabulek zakládají bez `CONCURRENTLY` nebo po částech.
- Cizí klíč `pages` → `page_versions` (`current_version_id`) se přidá `ALTER TABLE … ADD CONSTRAINT` až po vytvoření `page_versions`.
- Hranice měsíců jsou v UTC: `FOR VALUES FROM ('2026-10-01 00:00:00+00') TO ('2026-11-01 00:00:00+00')`.
- Výchozí část (`DEFAULT`) nevzniká: zápis do měsíce bez části skončí chybou 23514 „no partition of relation … found for row“ (fail-closed). Proto se části zakládají dopředu.
- Na částech nejsou žádná práva pro `eshopguard_app`, `eshopguard_worker` ani `eshopguard_admin`; přístup jde jen přes rodiče, kde platí RLS.

### Funkce pro RLS a dělení

```sql
CREATE FUNCTION ops.current_tenant_id() RETURNS uuid
LANGUAGE plpgsql STABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
DECLARE v text := current_setting('app.tenant_id', true);
BEGIN
  IF v IS NULL OR v = '' THEN
    RAISE EXCEPTION 'app.tenant_id is not set' USING ERRCODE = '42501';
  END IF;
  RETURN v::uuid;
END $$;

-- pro každou ze 41 tabulek tenanta (výčet v F1/04_rls.sql, ne smyčka, kvůli kontrole při revizi):
ALTER TABLE shop.shops ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shops FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shops
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));
```

`ops.ensure_monthly_partitions(p_months_ahead integer) RETURNS SETOF text`:
- `LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp`, vlastník `eshopguard_owner`;
- `p_months_ahead` mimo 0–24 → výjimka;
- `pg_advisory_xact_lock(hashtext('ops.ensure_monthly_partitions'))` proti souběhu dvou workerů;
- pevný seznam čtyř tabulek (žádný vstup s názvem tabulky), pro aktuální měsíc v UTC a `p_months_ahead` dalších vytvoří chybějící část `CREATE TABLE %I.%I PARTITION OF %I.%I FOR VALUES FROM (%L) TO (%L)` a vrátí její název;
- `REVOKE ALL … FROM PUBLIC`, `GRANT EXECUTE … TO eshopguard_worker, eshopguard_admin`.

Migrace na konci zavolá `SELECT ops.ensure_monthly_partitions(3)`.

### Oprávnění rolí

S = SELECT, I = INSERT, U = UPDATE, D = DELETE. `TRUNCATE`, `REFERENCES` a `TRIGGER` nedostane `eshopguard_app` ani `eshopguard_worker` nikde (`TRUNCATE` RLS neuplatňuje). `eshopguard_cms` nemá žádné právo v devíti schématech. Výchozí práva (`ALTER DEFAULT PRIVILEGES`) se nepoužívají: každá migrace práva uděluje výslovně a test je kontroluje.

| Skupina | `eshopguard_app` | `eshopguard_worker` | `eshopguard_admin` |
|---|---|---|---|
| `USAGE` na schématech `iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops`, `ref` | ano (kromě `usage`) | ano | ano |
| 41 tabulek tenanta | S I U D | S I U D | S I U D |
| `iam.tenants`, `iam.users`, `iam.user_logins`, `iam.user_tokens` | S I U | S | S I U D |
| `checks.rule_sets`, `billing.price_lists`, `price_tiers`, `volume_discounts`, `promo_codes`, `ref.markets`, `ref.locales` | S | S I U | S I U D |
| `shop.free_sample_claims` | S I | S I | S I U D |
| `billing.stripe_events` | S I U | S U | S I U D |
| `usage.usage_records`, `usage.usage_daily` | – | S I U | S |
| `ops.jobs` | S I U | S I U D | S I U D |
| `ops.workers`, `ops.domains` | S | S I U D | S I U D |
| `ops.rate_limit_buckets` | S I U | S I U | S I U D |
| `ops.system_settings` | S | S U | S I U D |
| sekvence (`USAGE` na sekvence identity sloupců) | ano | ano | ano |
| `ops.current_tenant_id()` | `EXECUTE` | `EXECUTE` | `EXECUTE` |
| `ops.ensure_monthly_partitions(integer)` | – | `EXECUTE` | `EXECUTE` |

Matici můžou rozšířit navazující změny (9–16) vlastní migrací; test oprávnění se rozšíří s nimi.

### Kontext tenanta a interceptory (`EshopGuard.Data/Tenancy/`)

```csharp
public interface ITenantContext            // scoped: jeden požadavek API, jedna úloha workeru
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    void Set(Guid tenantId, Guid? userId = null);   // podruhé s jiným tenantem → InvalidOperationException
    Guid RequireTenantId();                         // bez tenanta → TenantNotSetException (Code = "tenant.not_set")
}
```

- `TenantTransactionInterceptor : DbTransactionInterceptor`: v `TransactionStarted(Async)` a `TransactionUsed(Async)` zavolá na spojení transakce `SELECT set_config('app.tenant_id', @tenant, true), set_config('app.user_id', @user, true)`, pokud je tenant nastavený. Hodnota je parametr (`SET LOCAL` parametry nepřijímá a skládání SQL by otevřelo injekci).
- `EshopGuardDb` nastaví v konstruktoru `Database.AutoTransactionBehavior = AutoTransactionBehavior.Always`: EF Core jinak u jediného příkazu `SaveChanges` transakci nezakládá a `app.tenant_id` by nebyl nastavený.
- `TenantSaveChangesInterceptor : SaveChangesInterceptor` v `SavingChanges(Async)`:
  - `Added` + `ITenantOwned` s `TenantId == Guid.Empty` → doplní `RequireTenantId()`;
  - `Added` s jiným `TenantId` než kontext → `CrossTenantWriteException` (Code `tenant.cross_write`, jen název typu entity, žádné hodnoty);
  - `Modified` se změněným `TenantId` → `CrossTenantWriteException`.
- `TimestampSaveChangesInterceptor` (`TimeProvider`): `CreatedAt` při `Added`, `UpdatedAt` při `Added` i `Modified`.
- `SoftDeleteSaveChangesInterceptor`: `Deleted` + `ISoftDeletable` → `Modified` s `DeletedAt = now`; skutečné smazání jen přes `db.HardDelete(entity)` (příznak v `ChangeTracker`), které používá jen úloha mazání tenanta.
- Pojmenované globální filtry EF Core 10 v `EshopGuardDb.OnModelCreating` (smyčkou přes typy implementující rozhraní):
  - `"Tenant"`: `e => e.TenantId == CurrentTenantId`, kde `CurrentTenantId => _tenantContext.RequireTenantId()` (bez tenanta dotaz vyhodí `TenantNotSetException`);
  - `"SoftDelete"`: `e => e.DeletedAt == null` u `ISoftDeletable` (`tenants`, `users`, `shops`, `evidence_items`).
  - `IgnoreQueryFilters(["SoftDelete"])` ukáže smazané; `IgnoreQueryFilters(["Tenant"])` RLS v databázi stejně neobejde.
- `TenantDbContextExtensions.ExecuteInTenantTransactionAsync<T>(this EshopGuardDb db, Func<Task<T>> work, CancellationToken ct)`: ověří tenanta, otevře transakci přes strategii provádění EF, spustí práci, potvrdí. Čtení mimo transakci skončí chybou z databáze (42501), ne prázdným výsledkem.
- `TenantSql.BeginAsync(NpgsqlConnection connection, Guid tenantId, Guid? userId, CancellationToken ct)` → `NpgsqlTransaction` s nastaveným `app.tenant_id` pro čisté SQL a COPY mimo EF.

### Výčty

- `Configurations/Conventions/SnakeCaseEnumConverter<TEnum>`: hodnota enumu ↔ `snake_case` text (`KeptWithEvidence` ↔ `kept_with_evidence`, `AwaitingPayment` ↔ `awaiting_payment`).
- `Configurations/Conventions/EnumCheckExtensions.HasEnumCheck(...)`: z hodnot enumu vytvoří `CHECK (status IN ('open', …))` s názvem `ck_<tabulka>_<sloupec>`; text omezení tak nejde rozejít s enumem.
- Hodnoty enumů přesně podle části 3 podkladu (např. `RunStatus`: `queued`, `discovering`, `awaiting_payment`, `crawling`, `profiling`, `segmenting`, `evaluating`, `ruling`, `rewriting`, `finished`, `partial`, `failed`, `canceled`; `JobResourceClass`: `fetch`, `cpu`, `jev`, `llm`, `io`, `system`).
- `ops.jobs.priority` je `smallint` s `CHECK (priority BETWEEN 0 AND 4)`.

### Základní řádky (`HasData`)

- `ref.locales`: `sk` (Slovenčina), `cs` (Čeština), `fallback_code` null, `enabled = false` (K rozhodnutí 17).
- `ref.markets`: `sk` (`country_code` `SK`, `default_locale` `sk-SK`, `ui_locales` `{sk}`, `jurisdiction` `sk`, `currency` `EUR`, `price_list_id` null, `web_status` `hidden`, `checks_status` `full`) a `cz` (`CZ`, `cs-CZ`, `{cs}`, `cz`, `CZK`, `hidden`, `limited`: v Česku zatím jen modul `ucp`). Měnu rozhodl uživatel 1. 10. 2026 (K rozhodnutí 16).

### Testovací data izolace

Testy nic nemažou: každý běh založí nové tenanty (nová `uuid`), takže nepřekáží jiným testovacím projektům ve stejné databázi `eshopguard_test` a starší řádky skryje RLS. `tests/EshopGuard.Data.Tests/Isolation/TenantDataSeeder.cs` založí pro tenanta A i B (oba s e-shopem `vegis.sk`, `base_path` `/`) jeden platný řádek v každé ze 41 tabulek tenanta, s odkazy jen uvnitř tenanta, přes EF jako `eshopguard_app` s nastaveným tenantem. Globální řádky (uživatel pro každého tenanta, jeden `rule_sets`, jeden `price_lists`) založí předem. Test pokrytí porovná seznam tabulek, do kterých seeder zapsal, se seznamem tabulek s RLS z katalogu; chybějící tabulka = selhání.

## Architecture Decisions

1. **RLS je rozhodující, filtr EF je druhá vrstva.** Filtr chrání před chybou v LINQ, RLS před chybou kdekoli (ruční SQL, COPY, nový kód). Oba bez nastaveného tenanta selžou nahlas.
2. **Transakční `set_config(…, true)` místo nastavení relace.** Hodnota zanikne s transakcí, takže se nepřenese na jiný požadavek přes pool spojení a funguje za PgBouncerem v transakčním režimu (změna 4, poznámka). Cena: veškerý přístup k datům tenanta je v transakci (`AutoTransactionBehavior.Always`, `ExecuteInTenantTransactionAsync`).
3. **Funkce s chybou místo prázdného výsledku** (proposal, K rozhodnutí 10).
4. **Složené cizí klíče přes (`tenant_id`, `id`)**, u dělených tabulek (`tenant_id`, `shop_id`, `id`). Platí i pro roli `eshopguard_admin`, která RLS obchází.
5. **`ON DELETE RESTRICT`** všude: data tenanta se mažou jen úlohou mazání tenanta ve správném pořadí, nikdy kaskádou z omylu. Výjimka `user_logins` (patří uživateli).
6. **Dělení ručním SQL v migraci**, model EF beze změny. Vlastní generátor SQL pro EF by byl elegantnější, ale skrytý; ruční SQL je vidět při revizi migrace.
7. **Bez výchozí měsíční části** (fail-closed) a bez `ALTER DEFAULT PRIVILEGES` (práva výslovně, kontrolovaná testem).
8. **Žádné navigační kolekce** v entitách ve F1 (jen cizí klíče jako vlastnosti), aby se náhodou nenačítaly celé grafy; navigace přidají změny, které je potřebují.
9. **Kolize jmen s knihovnou:** entity `Finding` a `PageProfile` mají stejné jméno jako typy v `EshopGuard.Core`; jmenné prostory `EshopGuard.Data.Entities.Checks` a `EshopGuard.Data.Entities.Shops` je oddělují, mapovací kód používá aliasy (`using CoreFinding = EshopGuard.Core.Models.Finding;`). Jmenné prostory jsou v množném čísle (`Shops`), aby se nekryly s třídou `Shop`.
10. **Historie migrací beze změny názvů sloupců:** konvence `snake_case` (`EFCore.NamingConventions`) by přejmenovala `MigrationId` a `ProductVersion`; `EshopGuardHistoryRepository` je drží, aby šly číst databáze po migraci `Initial`.
11. **Index GIN pod RLS (zjištění 1. 10. 2026):** operátor `@>` nad poli není `LEAKPROOF`, takže pod RLS ho PostgreSQL nesmí použít jako podmínku indexu; pro `eshopguard_app` a `eshopguard_worker` se index GIN nad `segment_hashes` nepoužije. Hledání věty v e-shopu s 20 000 stránkami trvá i tak 9–10 ms (měřeno). Rozhodnuto 1. 10. 2026 (uživatel): index zrušen, šetří zápisy verzí stránek.

## Data Flow

```
Požadavek API / úloha workeru
  → nový DI scope → ITenantContext.Set(tenantId, userId)        (API: změna 9, worker: změna 4 z ops.jobs.tenant_id)
  → db.ExecuteInTenantTransactionAsync(...)
      → BEGIN
      → TenantTransactionInterceptor: SELECT set_config('app.tenant_id', $1, true), set_config('app.user_id', $2, true)
      → dotazy EF: filtr "Tenant" (WHERE tenant_id = @p) + RLS (tenant_id = (SELECT ops.current_tenant_id()))
      → SaveChanges: TenantSaveChangesInterceptor (doplní / odmítne tenant_id), časy, měkké mazání
      → INSERT/UPDATE: RLS WITH CHECK + složené cizí klíče
      → COMMIT (app.tenant_id zanikne)

Údržba (změna 4, denně)
  → worker (eshopguard_worker) → PartitionMaintainer.EnsureMonthlyPartitionsAsync(3)
  → SELECT * FROM ops.ensure_monthly_partitions(3)   (SECURITY DEFINER, vlastník eshopguard_owner)
  → nové části na další měsíce, vrácené názvy do logu
```

## File Changes

`src/EshopGuard.Data/`:
- `EshopGuardDb.cs`: `DbSet` pro 61 entit, konstruktor s `ITenantContext`, `AutoTransactionBehavior.Always`, `OnModelCreating` (`ApplyConfigurationsFromAssembly`, filtry `Tenant` a `SoftDelete`), `OnConfiguring` nic.
- `Entities/Common/`: `ITenantOwned.cs`, `ISoftDeletable.cs`, `IHasTimestamps.cs`, `TenantEntity.cs`, `GlobalEntity.cs`.
- `Entities/Iam/`: `Tenant.cs`, `User.cs`, `UserLogin.cs`, `UserToken.cs`, `Membership.cs`, `Invitation.cs`, `NotificationSetting.cs`, `Notification.cs`, `IamEnums.cs`.
- `Entities/Shops/`: `Shop.cs`, `ShopMarket.cs`, `ShopLanguage.cs`, `ShopVerification.cs`, `Connector.cs`, `ConnectorWebhook.cs`, `ConnectorEvent.cs`, `Feed.cs`, `PageProfile.cs`, `ShopFact.cs`, `FreeSampleClaim.cs`, `ShopEnums.cs`.
- `Entities/Content/`: `Page.cs`, `PageVersion.cs`, `ContentEnums.cs`.
- `Entities/Checks/`: `RuleSet.cs`, `Run.cs`, `RunEvent.cs`, `JevAnswer.cs`, `SieveAnswer.cs`, `Finding.cs`, `FindingOccurrence.cs`, `Question.cs`, `PageChange.cs`, `ChecksEnums.cs`.
- `Entities/Fixes/`: `FixGroup.cs`, `FixProposal.cs`, `Publication.cs`, `DecisionMemory.cs`, `EvidenceItem.cs`, `EvidenceLink.cs`, `Protocol.cs`, `RewriteCacheEntry.cs`, `FixesEnums.cs`.
- `Entities/Billing/`: `PriceList.cs`, `PriceTier.cs`, `VolumeDiscount.cs`, `PromoCode.cs`, `PaymentMethod.cs`, `Order.cs`, `Subscription.cs`, `SubscriptionChange.cs`, `Payment.cs`, `Invoice.cs`, `StripeEvent.cs`, `BillingEnums.cs`.
- `Entities/Usage/`: `UsageRecord.cs`, `UsageDaily.cs`, `UsageEnums.cs`.
- `Entities/Ops/`: `Job.cs`, `WorkerNode.cs`, `CrawlDomain.cs` (jména kvůli kolizi se jmenným prostorem `EshopGuard.Worker` a s vlastností `Domain`), `RateLimitBucket.cs`, `Schedule.cs`, `OutboxMessage.cs`, `AuditLogEntry.cs`, `SystemSetting.cs`, `OpsEnums.cs`.
- `Entities/Ref/`: `Market.cs`, `Locale.cs`, `RefEnums.cs`.
- `Configurations/<Schéma>/<Entita>Configuration.cs` pro každou entitu (61 souborů) a `Configurations/Conventions/`: `SnakeCaseEnumConverter.cs`, `EnumCheckExtensions.cs`, `TenantKeyExtensions.cs` (`HasTenantAlternateKey`, `HasTenantForeignKey<TPrincipal>(…)`, `HasPartitionedTenantForeignKey<TPrincipal>(…)`), `TableNames.cs` (konstanty schémat a seznamy 41 a 20 tabulek pro testy).
- `Tenancy/`: `ITenantContext.cs`, `TenantContext.cs`, `TenantNotSetException.cs`, `CrossTenantWriteException.cs`, `TenantTransactionInterceptor.cs`, `TenantSaveChangesInterceptor.cs`, `TimestampSaveChangesInterceptor.cs`, `SoftDeleteSaveChangesInterceptor.cs`, `TenantDbContextExtensions.cs`, `TenantSql.cs`.
- `Maintenance/PartitionMaintainer.cs`: `EnsureMonthlyPartitionsAsync(int monthsAhead, CancellationToken)` a `GetMonthlyHorizonAsync(CancellationToken)` (poslední pokrytý měsíc pro každou ze čtyř tabulek, pro hlídání ve změně 17).
- `Migrations/<ts>_F1DataModel.cs`, `<ts>_F1RowLevelSecurity.cs`, `<ts>_F1PartitionMaintenance.cs` (+ `.Designer.cs`, aktualizovaný `EshopGuardDbModelSnapshot.cs`).
- `Migrations/Sql/F1/01_functions.sql`, `02_partitioned_tables.sql`, `03_expression_indexes.sql`, `04_rls.sql`, `05_grants.sql`, `06_partition_maintenance.sql` (vložené prostředky, `Migrations/SqlResource.cs` je čte).
- `DataServiceCollectionExtensions.cs`: registrace `ITenantContext` (scoped), interceptorů a `TimeProvider.System`.
- `EshopGuard.Data.csproj`: `EmbeddedResource Include="Migrations/Sql/**/*.sql"`.

`tests/EshopGuard.Data.Tests/`:
- `Isolation/TenantDataSeeder.cs`, `Isolation/TwoTenantsFixture.cs`, `Isolation/SeederCoverageTests.cs`, `Isolation/TenantIsolationEfTests.cs`, `Isolation/TenantIsolationSqlTests.cs`, `Isolation/CrossTenantWriteTests.cs`, `Isolation/CompositeForeignKeyTests.cs`, `Isolation/MissingTenantContextTests.cs`.
- `Catalog/RlsCatalogTests.cs`, `Catalog/GrantsTests.cs`, `Catalog/EnumCheckConstraintTests.cs`, `Catalog/ModelCatalogConsistencyTests.cs`.
- `Partitioning/PartitioningTests.cs`, `Partitioning/PartitionMaintenanceTests.cs`.
- `Model/SoftDeleteTests.cs`, `Model/UuidV7Tests.cs`, `Model/ConcurrencyTokenTests.cs`, `Model/TimestampTests.cs`, `Model/RefSeedTests.cs`.
- `Tenancy/TenantSaveChangesInterceptorTests.cs` (`Category=Db`): doplnění `tenant_id`, odmítnutí cizího `tenant_id` a změny `tenant_id` ještě před odesláním do databáze (zachycené příkazy EF přes `DbCommandInterceptor` v testu neobsahují žádný `INSERT` ani `UPDATE`).
- `Tenancy/TenantTransactionTests.cs` (`Category=Db`): `SaveChanges` s jediným příkazem běží v transakci s nastaveným `app.tenant_id`; po potvrzení transakce `current_setting('app.tenant_id', true)` na stejném spojení vrací prázdnou hodnotu.
