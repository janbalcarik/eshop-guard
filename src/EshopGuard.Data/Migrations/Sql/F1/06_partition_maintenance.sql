-- F1PartitionMaintenance: monthly partitions are created ahead of time (there is no DEFAULT partition).
-- SECURITY DEFINER owned by eshopguard_owner; the worker may only execute it. The table list is fixed in the function,
-- nothing from the caller becomes an identifier. Two workers at once are serialized by an advisory lock.
CREATE FUNCTION ops.ensure_monthly_partitions(p_months_ahead integer) RETURNS SETOF text
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, pg_temp AS $$
DECLARE
  t record;
  m integer;
  month_start timestamptz;
  month_end timestamptz;
  part_name text;
BEGIN
  IF p_months_ahead IS NULL OR p_months_ahead < 0 OR p_months_ahead > 24 THEN
    RAISE EXCEPTION 'p_months_ahead must be between 0 and 24' USING ERRCODE = '22023';
  END IF;

  PERFORM pg_advisory_xact_lock(hashtext('ops.ensure_monthly_partitions'));

  FOR t IN SELECT * FROM (VALUES
      ('shop', 'connector_events'),
      ('checks', 'run_events'),
      ('usage', 'usage_records'),
      ('ops', 'audit_log')) AS v(schema_name, table_name)
  LOOP
    FOR m IN 0..p_months_ahead LOOP
      month_start := (date_trunc('month', now() AT TIME ZONE 'UTC') + make_interval(months => m)) AT TIME ZONE 'UTC';
      month_end := (date_trunc('month', now() AT TIME ZONE 'UTC') + make_interval(months => m + 1)) AT TIME ZONE 'UTC';
      part_name := t.table_name || to_char(month_start AT TIME ZONE 'UTC', '"_y"YYYY"m"MM');
      IF to_regclass(format('%I.%I', t.schema_name, part_name)) IS NULL THEN
        EXECUTE format('CREATE TABLE %I.%I PARTITION OF %I.%I FOR VALUES FROM (%L) TO (%L)',
                       t.schema_name, part_name, t.schema_name, t.table_name, month_start, month_end);
        RETURN NEXT t.schema_name || '.' || part_name;
      END IF;
    END LOOP;
  END LOOP;
END $$;

REVOKE ALL ON FUNCTION ops.ensure_monthly_partitions(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION ops.ensure_monthly_partitions(integer) TO eshopguard_worker, eshopguard_admin;

SELECT ops.ensure_monthly_partitions(3);
