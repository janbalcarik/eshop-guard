-- F1RowLevelSecurity: RLS with FORCE (applies to the table owner too) and the policy tenant_isolation on all
-- 41 tenant tables, listed one by one for review (the list is TableNames.TenantTables; a catalogue test compares them).
-- (SELECT ops.current_tenant_id()) is evaluated once per statement and keeps indexes on tenant_id usable.

ALTER TABLE iam.memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.memberships FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON iam.memberships
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE iam.invitations ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.invitations FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON iam.invitations
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE iam.notification_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.notification_settings FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON iam.notification_settings
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE iam.notifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.notifications FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON iam.notifications
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.shops ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shops FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shops
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.shop_markets ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shop_markets FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shop_markets
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.shop_languages ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shop_languages FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shop_languages
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.shop_verifications ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shop_verifications FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shop_verifications
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.connectors ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.connectors FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.connectors
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.connector_webhooks ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.connector_webhooks FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.connector_webhooks
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.connector_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.connector_events FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.connector_events
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.feeds ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.feeds FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.feeds
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.page_profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.page_profiles FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.page_profiles
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE shop.shop_facts ENABLE ROW LEVEL SECURITY;
ALTER TABLE shop.shop_facts FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON shop.shop_facts
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE content.pages ENABLE ROW LEVEL SECURITY;
ALTER TABLE content.pages FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON content.pages
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE content.page_versions ENABLE ROW LEVEL SECURITY;
ALTER TABLE content.page_versions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON content.page_versions
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.runs ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.runs FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.runs
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.run_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.run_events FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.run_events
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.jev_answers ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.jev_answers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.jev_answers
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.sieve_answers ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.sieve_answers FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.sieve_answers
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.findings ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.findings FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.findings
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.finding_occurrences ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.finding_occurrences FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.finding_occurrences
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.questions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.questions
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.page_changes ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.page_changes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.page_changes
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.fix_groups ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.fix_groups FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.fix_groups
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.fix_proposals ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.fix_proposals FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.fix_proposals
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.publications ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.publications FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.publications
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.decision_memory ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.decision_memory FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.decision_memory
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.evidence_items ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.evidence_items FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.evidence_items
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.evidence_links ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.evidence_links FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.evidence_links
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.protocols ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.protocols FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.protocols
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE fixes.rewrite_cache ENABLE ROW LEVEL SECURITY;
ALTER TABLE fixes.rewrite_cache FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON fixes.rewrite_cache
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.payment_methods ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.payment_methods FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.payment_methods
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.orders ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.orders FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.orders
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.subscriptions ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.subscriptions FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.subscriptions
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.subscription_changes ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.subscription_changes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.subscription_changes
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.payments FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.payments
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE billing.invoices ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.invoices FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.invoices
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE ops.schedules ENABLE ROW LEVEL SECURITY;
ALTER TABLE ops.schedules FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON ops.schedules
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE ops.outbox ENABLE ROW LEVEL SECURITY;
ALTER TABLE ops.outbox FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON ops.outbox
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE ops.audit_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE ops.audit_log FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON ops.audit_log
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));
