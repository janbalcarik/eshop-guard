DROP INDEX IF EXISTS checks.ix_questions_tenant_code_status;
DROP INDEX IF EXISTS checks.ix_findings_text_trgm;
DROP INDEX IF EXISTS content.ix_pages_title_trgm;
DROP INDEX IF EXISTS checks.ix_findings_shop_status_rank;
DROP FUNCTION IF EXISTS checks.strictness_rank(jsonb);
-- pg_trgm stays: another object may use it.
