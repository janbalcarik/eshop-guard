-- Role a databáze EshopGuard. Spouští se jednou jako superuživatel (postgres), idempotentně.
--
--   psql -h localhost -U postgres -d postgres -v ON_ERROR_STOP=1 -v db_name=eshopguard -f deploy/sql/00_roles.sql
--
-- Hesla jen z proměnných prostředí procesu psql (ne -v, to je vidět v seznamu procesů):
--   ESHOPGUARD_OWNER_PASSWORD, ESHOPGUARD_APP_PASSWORD, ESHOPGUARD_WORKER_PASSWORD,
--   ESHOPGUARD_ADMIN_PASSWORD, ESHOPGUARD_CMS_PASSWORD
-- Skript sahá jen na databázi :db_name (a připojuje se k postgres). Jiné databáze nemění.

\set ON_ERROR_STOP on
-- Chybové hlášky bez citace příkazu: příkaz ALTER ROLE obsahuje heslo.
\set VERBOSITY terse
\set SHOW_CONTEXT never

-- 1. Kontroly před jakoukoli změnou
SELECT current_setting('is_superuser') = 'on' AS eg_is_superuser \gset
\if :eg_is_superuser
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: spusťte jako superuživatel (postgres)'; END $$;
\endif

\if :{?db_name}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí parametr -v db_name=...'; END $$;
\endif
SELECT :'db_name' ~ '^[a-z][a-z0-9_]{0,62}$' AS eg_db_name_ok \gset
\if :eg_db_name_ok
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: neplatný název databáze (povolené a-z, 0-9, _)'; END $$;
\endif

\getenv eg_owner_password ESHOPGUARD_OWNER_PASSWORD
\getenv eg_app_password ESHOPGUARD_APP_PASSWORD
\getenv eg_worker_password ESHOPGUARD_WORKER_PASSWORD
\getenv eg_admin_password ESHOPGUARD_ADMIN_PASSWORD
\getenv eg_cms_password ESHOPGUARD_CMS_PASSWORD
\if :{?eg_owner_password}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí proměnná prostředí ESHOPGUARD_OWNER_PASSWORD'; END $$;
\endif
\if :{?eg_app_password}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí proměnná prostředí ESHOPGUARD_APP_PASSWORD'; END $$;
\endif
\if :{?eg_worker_password}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí proměnná prostředí ESHOPGUARD_WORKER_PASSWORD'; END $$;
\endif
\if :{?eg_admin_password}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí proměnná prostředí ESHOPGUARD_ADMIN_PASSWORD'; END $$;
\endif
\if :{?eg_cms_password}
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: chybí proměnná prostředí ESHOPGUARD_CMS_PASSWORD'; END $$;
\endif
SELECT length(:'eg_owner_password') >= 16 AND length(:'eg_app_password') >= 16 AND length(:'eg_worker_password') >= 16
   AND length(:'eg_admin_password') >= 16 AND length(:'eg_cms_password') >= 16 AS eg_passwords_ok \gset
\if :eg_passwords_ok
\else
  DO $$ BEGIN RAISE EXCEPTION '00_roles.sql: hesla ESHOPGUARD_*_PASSWORD musí mít aspoň 16 znaků'; END $$;
\endif

-- Příkazy s heslem nesmí skončit v logu serveru (log_statement, chybový STATEMENT).
SET log_statement = 'none';
SET log_min_error_statement = 'panic';

-- 2. Role: založit chybějící, potom vždy vynutit atributy a heslo
SELECT format('CREATE ROLE %I', r)
  FROM unnest(ARRAY['eshopguard_owner', 'eshopguard_app', 'eshopguard_worker', 'eshopguard_admin', 'eshopguard_cms']) AS r
 WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) \gexec

ALTER ROLE eshopguard_owner  WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'eg_owner_password';
ALTER ROLE eshopguard_app    WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'eg_app_password';
ALTER ROLE eshopguard_worker WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'eg_worker_password';
ALTER ROLE eshopguard_admin  WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION BYPASSRLS   PASSWORD :'eg_admin_password';
ALTER ROLE eshopguard_cms    WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'eg_cms_password';

-- Žádná z rolí nesmí být členem jiné role (SET ROLE by obešel oddělení, např. na eshopguard_admin).
SELECT format('REVOKE %I FROM %I GRANTED BY %I', g.rolname, m.rolname, gr.rolname)
  FROM pg_auth_members am
  JOIN pg_roles g ON g.oid = am.roleid
  JOIN pg_roles m ON m.oid = am.member
  JOIN pg_roles gr ON gr.oid = am.grantor
 WHERE m.rolname IN ('eshopguard_owner', 'eshopguard_app', 'eshopguard_worker', 'eshopguard_admin', 'eshopguard_cms') \gexec

\unset eg_owner_password
\unset eg_app_password
\unset eg_worker_password
\unset eg_admin_password
\unset eg_cms_password
RESET log_statement;
RESET log_min_error_statement;

-- 3. Databáze (jen pokud neexistuje). Locale builtin C.UTF-8: stejné chování na Windows i Linuxu, nezávislé na ICU.
SELECT format('CREATE DATABASE %I OWNER eshopguard_owner TEMPLATE template0 ENCODING %L LOCALE %L LOCALE_PROVIDER builtin BUILTIN_LOCALE %L',
              :'db_name', 'UTF8', 'C', 'C.UTF-8')
 WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db_name') \gexec
ALTER DATABASE :"db_name" OWNER TO eshopguard_owner;

-- 4. Práva k databázi: nic pro PUBLIC, CONNECT jen pro role aplikace
REVOKE ALL ON DATABASE :"db_name" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db_name" TO eshopguard_app, eshopguard_worker, eshopguard_admin, eshopguard_cms;

-- 5. Uvnitř databáze: schéma public bez práv pro PUBLIC, schéma cms pro Payload
\connect :"db_name"
REVOKE ALL ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA IF NOT EXISTS cms AUTHORIZATION eshopguard_cms;
ALTER SCHEMA cms OWNER TO eshopguard_cms;
REVOKE ALL ON SCHEMA cms FROM PUBLIC;
-- Schémata aplikace (ops, …) zakládají migrace jako eshopguard_owner (vlastník databáze má CREATE).

SELECT rolname, rolsuper, rolbypassrls, rolcanlogin
  FROM pg_roles WHERE rolname LIKE 'eshopguard\_%' ORDER BY rolname;
