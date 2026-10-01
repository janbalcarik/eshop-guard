# Proposal: Datový model pro více zákazníků s izolací tenantů

## Intent

**Problém.** Webová verze bude držet data stovek až tisíců zákazníků v jedné databázi: e-shopy, stránky, odpovědi Jevu, nálezy, opravy, doklady i faktury. Jediná chyba v dotazu (zapomenutý filtr, ruční SQL, záměna identifikátoru) by ukázala data jednoho zákazníka jinému. Jde o texty obchodů, nezveřejněné koncepty z konektorů a fakturační údaje. Zároveň porostou některé tabulky do stovek milionů řádků (`jev_answers` ~300 mil. při 1 000 e-shopech).

**Proč teď.** Fáze F1 navazuje na základ (změna 2). Fronta (změna 4), běhy ve workeru (8), API (9–11) i platby (12) potřebují hotové tabulky a hlavně hotovou izolaci, kterou ostatní změny jen používají. Dodatečně se izolace do hotových tabulek zavádí špatně.

**Přínos.**
- Všech 61 tabulek z části 3 podkladu v devíti schématech (`iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops`, `ref`) jako entity EF Core 10 a migrace.
- Izolace tenantů na třech úrovních v databázi a aplikaci (čtvrtá, API, přijde ve změně 9):
  - globální filtr EF Core a kontrola `tenant_id` při ukládání;
  - Row-Level Security s `FORCE` na 41 tabulkách tenanta;
  - složené cizí klíče (`tenant_id`, `id`), takže odkaz na řádek jiného tenanta nejde založit ani rolí, která RLS obchází.
- Bez nastaveného tenanta dotaz na data tenanta **selže**, nevrátí tiše prázdný výsledek (fail-closed: „žádné nálezy“ nesmí vzniknout z chybějícího kontextu).
- Velké tabulky rozdělené od začátku (HASH po e-shopu nebo tenantovi, RANGE po měsících), takže pozdější přechod nebude potřeba.
- Testy izolace podle podkladu: dva tenanti se stejnou doménou, každá tabulka tenanta přes EF i čisté SQL jako `eshopguard_app`, zápis s cizím `tenant_id` selže.

**Fáze:** F1 Datový model.

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`: část 2 (zásady: `uuid` v7, `tenant_id`, složené cizí klíče, RLS s `FORCE`, role, `timestamptz`, peníze, výčty jako `text` + `CHECK`, `jsonb`, `xmin`, dělení tabulek), část 3 (tabulky 3.1–3.9), část 4 (vazby), část 6 (objem a dělení), část 7 (`EshopGuardDb.cs`, `Configurations/`, `Migrations/`, `Tenancy/`), část 8 (F1);
- `architektura-multitenant-worker-2026-10-01.md`: část 3 (model, co je po tenantech, čtyři úrovně izolace, testy izolace), část 12 (`ref.markets`, `shop_markets`, `shop_languages`, `findings.verdicts`).

## Scope

In scope:
- Entity a `IEntityTypeConfiguration` pro všechny tabulky části 3:
  - `iam` (8): `tenants`, `users`, `user_logins`, `user_tokens`, `memberships`, `invitations`, `notification_settings`, `notifications`;
  - `shop` (11): `shops`, `shop_markets`, `shop_languages`, `shop_verifications`, `connectors`, `connector_webhooks`, `connector_events`, `feeds`, `page_profiles`, `shop_facts`, `free_sample_claims`;
  - `content` (2): `pages`, `page_versions`;
  - `checks` (9): `rule_sets`, `runs`, `run_events`, `jev_answers`, `sieve_answers`, `findings` (včetně `verdicts` jsonb po jurisdikcích), `finding_occurrences`, `questions`, `page_changes`;
  - `fixes` (8): `fix_groups`, `fix_proposals`, `publications`, `decision_memory`, `evidence_items`, `evidence_links`, `protocols`, `rewrite_cache`;
  - `billing` (11): `price_lists`, `price_tiers`, `volume_discounts`, `promo_codes`, `payment_methods`, `orders`, `subscriptions`, `subscription_changes`, `payments`, `invoices`, `stripe_events`;
  - `usage` (2): `usage_records`, `usage_daily`;
  - `ops` (8): `jobs`, `workers`, `domains`, `rate_limit_buckets`, `schedules`, `outbox`, `audit_log`, `system_settings`;
  - `ref` (2): `markets`, `locales` (se základními řádky).
- Schéma `cms` jen jako role a prázdné schéma (už ze změny 2); jeho obsah spravuje Payload CMS (změna 14).
- Identifikátory `uuid` v7 (`Guid.CreateVersion7()` v .NET, výchozí `uuidv7()` v databázi), `bigint identity` u tabulek jen s přidáváním.
- `tenant_id` na každé tabulce tenanta, alternativní klíč (`tenant_id`, `id`) a složené cizí klíče; u dělených tabulek (`tenant_id`, `shop_id`, `id`).
- Výčty jako `text` s `CHECK` generovaným z .NET enumů, `xmin` jako token souběžnosti, `created_at` / `updated_at`, měkké mazání `deleted_at`.
- RLS: funkce `ops.current_tenant_id()`, `ENABLE` + `FORCE ROW LEVEL SECURITY` a politika `tenant_isolation` na 41 tabulkách tenanta; oprávnění rolí podle matice v designu.
- Dělení tabulek: `content.pages` HASH(`shop_id`) 16, `content.page_versions` HASH(`shop_id`) 32, `checks.jev_answers` HASH(`tenant_id`) 32, `checks.sieve_answers` HASH(`tenant_id`) 32; RANGE po měsících `shop.connector_events`, `checks.run_events`, `usage.usage_records`, `ops.audit_log`.
- Funkce `ops.ensure_monthly_partitions(int)` a služba `PartitionMaintainer`, které měsíční části zakládají dopředu (naplánování úlohy přidá změna 4).
- `ITenantContext`, interceptor transakce (`set_config('app.tenant_id', …, true)` = `SET LOCAL`), interceptory ukládání (tenant, časy, měkké mazání), pojmenované globální filtry EF `Tenant` a `SoftDelete`.
- Testy izolace, katalogu RLS, oprávnění, dělení, měkkého mazání, `uuid` v7, výčtů a souběžnosti.

Out of scope:
- PostgreSQL implementace `IJevCache`, `IRewriteCache`, `IPageProfileStore` (`Stores/`) a hromadný zápis přes COPY (`Bulk/`): změna 5 a 8.
- Logika fronty nad `ops.jobs`, `ops.domains`, `ops.rate_limit_buckets` a naplánování údržby: změna 4.
- Úložiště ASP.NET Core Identity nad `iam.users` a přihlášení, přepínání účtů, pozvánky: změna 9.
- Tabulka `checks.run_urls` a další jedinečné indexy pro idempotentní zápis běhů: změna 8 (K rozhodnutí 4 a 5 tam).
- Mazání starých měsíčních částí podle doby uchování (30 / 90 dní, 24 měsíců, 10 let) a úklid stránek, které zmizely: změny 16 a 17.
- Úloha natvrdo mazající tenanta: samostatně (viz K rozhodnutí 12).
- Šifrování `connectors.credentials_enc`: změna 15.

## Approach

1. **Entity po schématech** (`Entities/<Schéma>/`), konfigurace po schématech (`Configurations/<Schéma>/`), jména tabulek a sloupců `snake_case`. Společné základy `TenantEntity`, `GlobalEntity`, rozhraní `ITenantOwned`, `ISoftDeletable`, `IHasTimestamps`.
2. **Izolace v databázi je hlavní pojistka, EF filtr je pohodlí.** RLS s `FORCE` platí i pro vlastníka tabulek; politika volá `ops.current_tenant_id()`, která bez nastaveného `app.tenant_id` vyhodí chybu (SQLSTATE 42501). Funkce se v politice volá jako `(SELECT ops.current_tenant_id())`, takže se vyhodnotí jednou za dotaz a nebrání použití indexu.
3. **`SET LOCAL` jen v transakci.** Přístup k datům tenanta běží vždy v transakci; interceptor `TenantTransactionInterceptor` po začátku každé transakce zavolá `SELECT set_config('app.tenant_id', @tenant, true)` (parametr, ne skládání SQL). Hodnota platí do konce transakce, takže se nepřenese na další použití spojení z poolu a funguje i za PgBouncerem v transakčním režimu.
4. **Složené cizí klíče** brání odkazu přes hranici tenanta i tam, kde RLS neplatí (role `eshopguard_admin`, údržba).
5. **Dělené tabulky** vytvoří migrace ručním SQL (EF Core dělení neumí); model EF je mapuje jako běžné tabulky. Primární a jedinečné klíče obsahují klíč dělení. Aplikační role mají práva jen k rodičovské tabulce, ne k jednotlivým částem (přímý dotaz na část by RLS obešel).
6. **Měsíční části** zakládá `SECURITY DEFINER` funkce vlastníka; worker ji smí jen spustit. Výchozí část (`DEFAULT`) záměrně není: chybějící část = chyba zápisu, ne tiché odložení dat.
7. **Testy izolace jsou generické** přes všechny tabulky tenanta z katalogu databáze a z modelu EF. Nová tabulka bez RLS, bez řádku v testovacím seederu nebo bez zařazení do seznamu globálních tabulek test shodí.

## Dependencies

- Změna 2 `add-solution-foundation`: projekt `EshopGuard.Data`, role, databáze `eshopguard` a `eshopguard_test`, migrace `Initial`, `PostgresTestDatabase`.
- PostgreSQL 18 (funkce `uuidv7()`, dělení tabulek, cizí klíče na dělené tabulky).
- EF Core 10 (pojmenované globální filtry, zámek migrací).
- Navazují: změna 4 (fronta nad `ops.*`, naplánování `PartitionMaintainer`), 5 (úložiště cache v `checks.jev_answers`, `fixes.rewrite_cache`, `shop.page_profiles`), 7, 8, 9–16.

## Done when

- `dotnet ef database update` jako `eshopguard_owner` vytvoří všech 61 tabulek v devíti schématech; `dotnet ef migrations has-pending-model-changes` nic nehlásí.
- Testy izolace (`Category=Db`) projdou: pro dva tenanty se stejnou doménou `vegis.sk` vrací každá z 41 tabulek tenanta přes EF i čisté SQL jako `eshopguard_app` a `eshopguard_worker` jen vlastní řádky; zápis s cizím `tenant_id` skončí chybou (EF: výjimka před odesláním, SQL: 42501); odkaz na e-shop jiného tenanta skončí 23503 i jako `eshopguard_admin`.
- Dotaz na tabulku tenanta bez nastaveného tenanta skončí chybou (EF: `TenantNotSetException`, SQL: 42501), ne prázdným výsledkem.
- Katalogový test: každá tabulka v devíti schématech je buď v seznamu 41 tabulek tenanta s `relrowsecurity` a `relforcerowsecurity` a politikou `tenant_isolation`, nebo v seznamu 20 globálních tabulek.
- `content.pages` má 16 částí, `content.page_versions`, `checks.jev_answers` a `checks.sieve_answers` po 32; čtyři měsíční tabulky mají části na aktuální měsíc a nejméně 3 další; `PartitionMaintainer` je idempotentní.
- `dotnet test EshopGuard.sln` projde včetně nových testů a `EshopGuard.Core.Tests` beze změny počtu.

## K rozhodnutí

Uživatel 1. 10. 2026 přijal návrhy všech bodů (body 3 a 12 výslovně) a k bodu 16 určil měnu Kč.

1. **Systémové procesy a tabulky s RLS.** Podle podkladu mají RLS všechny tabulky kromě seznamu globálních, tedy i `ops.schedules`, `ops.outbox` a `ops.audit_log`. Plánovač (změna 16) a odesílání e-mailů a faktur (změny 9, 12) ale potřebují najít práci napříč tenanty. Tato změna RLS zavádí podle podkladu. Návrh pro navazující změny: `SECURITY DEFINER` funkce, které vrátí jen dvojice (`tenant_id`, `id`) čekající práce bez obsahu, a zpracování pak běží s kontextem tenanta. Alternativa: zařadit tyto tři tabulky mezi globální.
2. **Seznam účtů uživatele a pozvánky.** `iam.memberships` a `iam.invitations` mají RLS podle tenanta, ale přepínač účtů („ve kterých tenantech jsem“) a přijetí pozvánky podle tokenu se ptají napříč tenanty. Návrh pro změnu 9: druhá politika na `memberships` podle `app.user_id` a funkce `iam.find_invitation(token_hash)`. Rozhodnout ve změně 9.
3. **`iam.tenants` a `iam.users` bez RLS** (podklad: „přístup jen přes členství“). Role `eshopguard_app` tak technicky přečte fakturační údaje všech tenantů a e-maily všech uživatelů; chrání je jen API. Ponechat podle podkladu, nebo přidat politiku podle `app.user_id` (změna 9)?
4. **Příjem webhooků konektorů:** API musí najít konektor podle (`platform`, `external_shop_id`) dřív, než zná tenanta. Návrh pro změnu 15: funkce `shop.resolve_connector(...)` vracející jen (`tenant_id`, `connector_id`).
5. **`ops.schedules` má PK `shop_id`, ale sloupec `kind` má tři hodnoty** (`nightly`, `weekly_web`, `reconcile`); e-shop s konektorem potřebuje víc plánů. Návrh: PK (`shop_id`, `kind`). Do rozhodnutí implementuji návrh, protože s PK `shop_id` nejde uložit noční běh a týdenní procházení zároveň.
6. **`billing.promo_codes` s `tenant_id`** (null = veřejný) je v seznamu globálních tabulek jako „promo_codes (veřejné)“. Kódy vázané na tenanta tak RLS nechrání. Ponechat globální (API filtruje), nebo RLS s politikou `tenant_id IS NULL OR tenant_id = …`?
7. **`id`, `created_at`, `updated_at` „jsou všude“** podle podkladu, ale tabulky se složeným PK (`jev_answers`, `sieve_answers`, `rewrite_cache`, `memberships`, `finding_occurrences`, `shop_markets`, `shop_languages`) ho mají bez `id`. Návrh: bez náhradního `id` (u `jev_answers` by 16 B na řádek a index stály při 300 mil. řádků ~10 GB, neměřeno); `created_at` všude, `updated_at` jen kde se řádek mění (ne `jev_answers`, `sieve_answers`).
8. **Cizí klíče na dělené tabulky** nemohou být přes dvojici (`tenant_id`, `id`), protože jedinečný klíč dělené tabulky musí obsahovat klíč dělení. U `pages` a `page_versions` jsou proto přes trojici (`tenant_id`, `shop_id`, `id`); všechny odkazující tabulky mají `shop_id`. Potvrdit.
9. **Počet částí `sieve_answers`** podklad neuvádí (část 6 jen `jev_answers` 32). Návrh 32 jako `jev_answers`.
10. **Chování bez nastaveného tenanta.** Podklad uvádí politiku `tenant_id = current_setting('app.tenant_id')::uuid`; ta podle stavu spojení buď vyhodí chybu, nebo (po předchozím `SET LOCAL` na stejném spojení) selže na převodu prázdného řetězce. Návrh: funkce `ops.current_tenant_id()` s jednoznačnou chybou 42501 „app.tenant_id is not set“. Prázdný výsledek nechci: bez kontextu by worker mohl uzavřít „žádné nálezy“.
11. **Měkké mazání a jedinečnost.** `shops` U (`tenant_id`, `domain`, `base_path`) a `users.email` U by po měkkém smazání nedovolily e-shop nebo účet založit znovu. Návrh: jedinečnost jen pro nesmazané (`WHERE deleted_at IS NULL`), `users` přes index na `lower(email)`.
12. **Smazání tenanta proti uchování auditu.** Podklad: „smazání tenanta je úloha, která maže natvrdo“ a zároveň uchování `audit_log` 10 let. Návrh: `ops.audit_log.tenant_id` a `usage.usage_records.tenant_id` bez cizího klíče na `iam.tenants`, aby smazání tenanta audit a interní náklady nesmazalo. Potvrdit, co se má při smazání tenanta s auditem stát.
13. **Typy otisků** podklad neurčuje. Návrh: `segment_hash`, `segment_hashes` a `url_hash` `bigint` (64bitový otisk podle části 3.3); otisky pro identitu a cache (`state_hash`, `chunk_hash`, `question_set_hash`, `text_hash`, `token_hash`, `old_value_hash`, `webhook_secret_hash`, `requested_ip_hash`) `bytea` SHA-256, protože kolize by tiše vrátila cizí odpověď.
14. **`subscriptions.shop_id` „U pro aktivní“:** které stavy jsou aktivní? Návrh: jedinečné pro všechny stavy kromě `canceled`.
15. **Odstranění duplicit `connector_events`:** index (`connector_id`, `dedupe_key`) nemůže být jedinečný přes měsíční části (jedinečný klíč musí obsahovat `received_at`). Duplicitní webhook z jiného měsíce tak projde; zpracování je idempotentní („aspoň jednou“). Potvrdit, nebo přidat malou nedělenou tabulku klíčů (změna 15).
16. **Rozhodnuto 1. 10. 2026 (uživatel): `cz` v Kč**, řádek `cz` (CZK) i `sk` (EUR) zakládá migrace. Původní text: **`ref.markets` pro `cz`:** měna českého trhu není rozhodnutá (architektura část 11, bod 7: Kč, nebo euro). Řádek `sk` (EUR) založí migrace; řádek `cz` až po rozhodnutí. Dále: `ref.markets.price_list_id` → `billing.price_lists` a `price_lists.market_code` → `ref.markets` tvoří kruhovou vazbu; obě strany budou nepovinné.
17. **`ref.locales.enabled`:** podklad říká, že zapnout jde jen jazyk s úplnými texty. Slovenské texty pravidel zatím nejsou (změna 6). Návrh: `sk` i `cs` založit s `enabled = false` a zapnout je ve změnách 6 a 13 po testu úplnosti.
18. **Token souběžnosti `xmin`** podklad uvádí u „návrhů oprav, dokladů, předplatného“. Návrh: `fix_proposals`, `fix_groups` (také je upravuje člověk: vyplněné hodnoty, vyřazené stránky), `evidence_items` a `subscriptions`.
