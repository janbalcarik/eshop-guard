-- F7FindingsApi (change 11): order of findings by their strictest verdict, search in pages and findings, questions by code.

-- How strict the strictest verdict of a finding is, smaller is stricter: the order of VerdictOrder of the library (change 6):
-- valid before upcoming, then the group (text, assess, verify, anything else), the severity (high, medium, low) and the band
-- (high before review). A list of findings sorts by it in SQL, so paging by a cursor is stable (AD 2 of change 11).
CREATE FUNCTION checks.strictness_rank(verdicts jsonb) RETURNS smallint
LANGUAGE sql IMMUTABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
  SELECT coalesce(min(
        (CASE v->>'status' WHEN 'upcoming' THEN 1 ELSE 0 END) * 64
      + (CASE v->>'checkability' WHEN 'text' THEN 0 WHEN 'assess' THEN 1 WHEN 'verify' THEN 2 ELSE 3 END) * 16
      + (CASE v->>'severity' WHEN 'high' THEN 0 WHEN 'medium' THEN 1 WHEN 'low' THEN 2 ELSE 3 END) * 4
      + (CASE v->>'band' WHEN 'high' THEN 0 ELSE 1 END)), 127)::smallint
  FROM jsonb_array_elements(CASE WHEN jsonb_typeof(verdicts) = 'array' THEN verdicts ELSE '[]'::jsonb END) AS v
$$;

REVOKE ALL ON FUNCTION checks.strictness_rank(jsonb) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION checks.strictness_rank(jsonb) TO eshopguard_app, eshopguard_worker, eshopguard_admin;

CREATE INDEX ix_findings_shop_status_rank ON checks.findings (shop_id, status, checks.strictness_rank(verdicts));

-- Search (Ctrl K): pages by title, findings by text, in one e-shop (the shop filter comes from the other indexes).
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX ix_pages_title_trgm ON content.pages USING gin (title gin_trgm_ops);
CREATE INDEX ix_findings_text_trgm ON checks.findings USING gin (text gin_trgm_ops);

-- An answer goes to every open question with the same code in the tenant (AD 6).
CREATE INDEX ix_questions_tenant_code_status ON checks.questions (tenant_id, code, status);
