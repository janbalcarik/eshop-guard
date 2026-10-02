-- F5IdentityPolicies (change 9): reads of a user before a tenant is chosen. Every table of tenants keeps its policy
-- tenant_isolation; a transaction of a user without a tenant sets app.tenant_id to the nil uuid (TenantSql.NoTenant), which
-- owns no row, so only the policies below add rows, and only in such a transaction (ops.in_user_scope): a transaction of a
-- tenant still sees nothing but its tenant. The owner role has no BYPASSRLS and the tables FORCE RLS, so functions
-- SECURITY DEFINER would not see across tenants either; permissive policies by user or by token do it instead.

CREATE FUNCTION ops.current_user_id() RETURNS uuid
LANGUAGE sql STABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
  SELECT nullif(current_setting('app.user_id', true), '')::uuid
$$;

-- Hash (hex) of the token of an invitation being opened (InvitationService): only the holder of the token knows it.
CREATE FUNCTION ops.current_invitation_hash() RETURNS bytea
LANGUAGE sql STABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
  SELECT decode(nullif(current_setting('app.invitation_hash', true), ''), 'hex')
$$;

-- A transaction of a user without a tenant (app.tenant_id = nil uuid); fails like ops.current_tenant_id without a context.
CREATE FUNCTION ops.in_user_scope() RETURNS boolean
LANGUAGE sql STABLE PARALLEL SAFE SET search_path = pg_catalog AS $$
  SELECT ops.current_tenant_id() = '00000000-0000-0000-0000-000000000000'::uuid
$$;

REVOKE ALL ON FUNCTION ops.current_user_id() FROM PUBLIC;
REVOKE ALL ON FUNCTION ops.in_user_scope() FROM PUBLIC;
REVOKE ALL ON FUNCTION ops.current_invitation_hash() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION ops.current_user_id() TO eshopguard_app, eshopguard_worker, eshopguard_admin;
GRANT EXECUTE ON FUNCTION ops.current_invitation_hash() TO eshopguard_app, eshopguard_worker, eshopguard_admin;
GRANT EXECUTE ON FUNCTION ops.in_user_scope() TO eshopguard_app, eshopguard_worker, eshopguard_admin;

-- „Moje účty“: a user reads his own memberships in every tenant.
CREATE POLICY memberships_select_own ON iam.memberships FOR SELECT
  USING ((SELECT ops.in_user_scope()) AND user_id = (SELECT ops.current_user_id()));

-- An invitation opened by its token (before sign-in, or by a user of another tenant).
CREATE POLICY invitations_select_by_token ON iam.invitations FOR SELECT
  USING ((SELECT ops.in_user_scope()) AND token_hash = (SELECT ops.current_invitation_hash()));

-- Pending invitations of the signed-in user's own (verified) e-mail address.
CREATE POLICY invitations_select_own_email ON iam.invitations FOR SELECT
  USING ((SELECT ops.in_user_scope())
         AND email = (SELECT u.email FROM iam.users u WHERE u.id = (SELECT ops.current_user_id()) AND u.deleted_at IS NULL));

-- Security events without a tenant (sign-in, a new account).
CREATE POLICY audit_log_insert_auth ON ops.audit_log FOR INSERT
  WITH CHECK (tenant_id IS NULL AND (action LIKE 'auth.%' OR action LIKE 'user.%'));

-- The pause of 60 s between sign-in links of one e-mail address.
CREATE INDEX ix_user_tokens_email_purpose_created ON iam.user_tokens (email, purpose, created_at DESC);

-- Unlinking Google (API) and the daily cleanup of tokens and full buckets of the sign-in (worker).
GRANT DELETE ON iam.user_logins TO eshopguard_app;
GRANT DELETE ON iam.user_tokens TO eshopguard_worker;
GRANT DELETE ON ops.rate_limit_buckets TO eshopguard_worker;
