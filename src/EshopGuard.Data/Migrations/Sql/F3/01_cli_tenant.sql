-- F3CacheInPostgres: the reserved tenant of the CLI, whose cache (Jev answers, rewrites, template profiles) lives in the
-- same tables as the web application's. The worker role may not write iam.tenants, so `eshopguard cache init` creates it
-- through this function; a scan never creates it, so nothing is written into a foreign database without that command.
-- The id is fixed (EshopGuard.Data.Stores.CliTenant.Id); the function only ever inserts this one row.
CREATE FUNCTION iam.ensure_cli_tenant() RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $$
BEGIN
  INSERT INTO iam.tenants (id, name, country_code, locale, market_code, status)
  VALUES ('00000000-0000-0000-0000-0000000000c1', 'EshopGuard CLI', 'SK', 'sk', 'sk', 'active')
  ON CONFLICT (id) DO NOTHING;
  RETURN '00000000-0000-0000-0000-0000000000c1'::uuid;
END $$;

REVOKE ALL ON FUNCTION iam.ensure_cli_tenant() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION iam.ensure_cli_tenant() TO eshopguard_worker;
