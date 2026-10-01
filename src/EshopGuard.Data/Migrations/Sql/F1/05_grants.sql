-- F1RowLevelSecurity: privileges of the application roles (design, "Oprávnění rolí"). Explicit per table; no
-- ALTER DEFAULT PRIVILEGES. TRUNCATE, REFERENCES and TRIGGER are never granted (TRUNCATE ignores RLS); partitions
-- get nothing, so the parent with its RLS is the only way in. eshopguard_cms gets nothing in these schemas.

GRANT USAGE ON SCHEMA iam, shop, content, checks, fixes, billing, ops, ref TO eshopguard_app;
GRANT USAGE ON SCHEMA iam, shop, content, checks, fixes, billing, usage, ops, ref TO eshopguard_worker, eshopguard_admin;

-- 41 tenant tables
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.memberships TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.memberships TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.memberships TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.invitations TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.invitations TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.invitations TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notification_settings TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notification_settings TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notification_settings TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notifications TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notifications TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.notifications TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shops TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shops TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shops TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_markets TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_markets TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_markets TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_languages TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_languages TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_languages TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_verifications TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_verifications TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_verifications TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connectors TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connectors TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connectors TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_webhooks TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_webhooks TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_webhooks TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_events TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_events TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.connector_events TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.feeds TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.feeds TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.feeds TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.page_profiles TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.page_profiles TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.page_profiles TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_facts TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_facts TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.shop_facts TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.pages TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.pages TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.pages TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.page_versions TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.page_versions TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON content.page_versions TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.runs TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.runs TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.runs TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_events TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_events TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_events TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.jev_answers TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.jev_answers TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.jev_answers TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.sieve_answers TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.sieve_answers TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.sieve_answers TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.findings TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.findings TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.findings TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.finding_occurrences TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.finding_occurrences TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.finding_occurrences TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.questions TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.questions TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.questions TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.page_changes TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.page_changes TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.page_changes TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_groups TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_groups TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_groups TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_proposals TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_proposals TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.fix_proposals TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.publications TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.publications TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.publications TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.decision_memory TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.decision_memory TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.decision_memory TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_items TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_items TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_items TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_links TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_links TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.evidence_links TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.protocols TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.protocols TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.protocols TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.rewrite_cache TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.rewrite_cache TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON fixes.rewrite_cache TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payment_methods TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payment_methods TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payment_methods TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.orders TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.orders TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.orders TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscriptions TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscriptions TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscriptions TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscription_changes TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscription_changes TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.subscription_changes TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payments TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payments TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.payments TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.invoices TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.invoices TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.invoices TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.schedules TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.schedules TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.schedules TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.outbox TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.outbox TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.outbox TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.audit_log TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.audit_log TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.audit_log TO eshopguard_admin;

-- identities
GRANT SELECT, INSERT, UPDATE ON iam.tenants TO eshopguard_app;
GRANT SELECT ON iam.tenants TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.tenants TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE ON iam.users TO eshopguard_app;
GRANT SELECT ON iam.users TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.users TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE ON iam.user_logins TO eshopguard_app;
GRANT SELECT ON iam.user_logins TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.user_logins TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE ON iam.user_tokens TO eshopguard_app;
GRANT SELECT ON iam.user_tokens TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON iam.user_tokens TO eshopguard_admin;

-- catalogues
GRANT SELECT ON checks.rule_sets TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON checks.rule_sets TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.rule_sets TO eshopguard_admin;
GRANT SELECT ON billing.price_lists TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON billing.price_lists TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.price_lists TO eshopguard_admin;
GRANT SELECT ON billing.price_tiers TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON billing.price_tiers TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.price_tiers TO eshopguard_admin;
GRANT SELECT ON billing.volume_discounts TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON billing.volume_discounts TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.volume_discounts TO eshopguard_admin;
GRANT SELECT ON billing.promo_codes TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON billing.promo_codes TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.promo_codes TO eshopguard_admin;
GRANT SELECT ON ref.markets TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON ref.markets TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ref.markets TO eshopguard_admin;
GRANT SELECT ON ref.locales TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON ref.locales TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ref.locales TO eshopguard_admin;

-- free samples
GRANT SELECT, INSERT ON shop.free_sample_claims TO eshopguard_app;
GRANT SELECT, INSERT ON shop.free_sample_claims TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON shop.free_sample_claims TO eshopguard_admin;

-- Stripe events
GRANT SELECT, INSERT, UPDATE ON billing.stripe_events TO eshopguard_app;
GRANT SELECT, UPDATE ON billing.stripe_events TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.stripe_events TO eshopguard_admin;

-- usage (internal)
GRANT SELECT, INSERT, UPDATE ON usage.usage_records TO eshopguard_worker;
GRANT SELECT ON usage.usage_records TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE ON usage.usage_daily TO eshopguard_worker;
GRANT SELECT ON usage.usage_daily TO eshopguard_admin;

-- job queue
GRANT SELECT, INSERT, UPDATE ON ops.jobs TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.jobs TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.jobs TO eshopguard_admin;

-- workers and domains
GRANT SELECT ON ops.workers TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.workers TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.workers TO eshopguard_admin;
GRANT SELECT ON ops.domains TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.domains TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.domains TO eshopguard_admin;

-- rate limits
GRANT SELECT, INSERT, UPDATE ON ops.rate_limit_buckets TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE ON ops.rate_limit_buckets TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.rate_limit_buckets TO eshopguard_admin;

-- system settings
GRANT SELECT ON ops.system_settings TO eshopguard_app;
GRANT SELECT, UPDATE ON ops.system_settings TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ops.system_settings TO eshopguard_admin;

-- identity sequences (bigint ids of append-only tables)
GRANT USAGE ON ALL SEQUENCES IN SCHEMA shop, checks, ops TO eshopguard_app, eshopguard_worker, eshopguard_admin;
GRANT USAGE ON ALL SEQUENCES IN SCHEMA usage TO eshopguard_worker, eshopguard_admin;
