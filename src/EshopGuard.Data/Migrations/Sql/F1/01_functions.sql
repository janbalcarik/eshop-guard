-- F1DataModel: tenant of the current transaction for RLS policies.
-- Without app.tenant_id the function fails (42501) instead of returning NULL: a missing context must never look like
-- "no rows" (fail-closed). After a transaction that used set_config(…, true) the setting is '' on the same connection,
-- which is treated the same way.
CREATE FUNCTION ops.current_tenant_id() RETURNS uuid
LANGUAGE plpgsql STABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
DECLARE
  v text := current_setting('app.tenant_id', true);
BEGIN
  IF v IS NULL OR v = '' THEN
    RAISE EXCEPTION 'app.tenant_id is not set' USING ERRCODE = '42501';
  END IF;
  RETURN v::uuid;
END $$;

REVOKE ALL ON FUNCTION ops.current_tenant_id() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION ops.current_tenant_id() TO eshopguard_app, eshopguard_worker, eshopguard_admin;
