DELETE FROM billing.price_tiers t USING billing.price_lists l
 WHERE t.price_list_id = l.id AND l.name IN ('SK 2026', 'CZ 2026') AND l.status = 'draft';
DELETE FROM billing.price_lists WHERE name IN ('SK 2026', 'CZ 2026') AND status = 'draft';
DROP POLICY IF EXISTS audit_log_insert_price_list ON ops.audit_log;
DELETE FROM ops.system_settings WHERE key IN ('billing:stripe_products:test', 'billing:stripe_products:live');
REVOKE INSERT ON billing.stripe_events FROM eshopguard_worker;
REVOKE DELETE ON billing.price_tiers, billing.volume_discounts FROM eshopguard_worker;
REVOKE UPDATE (tax_id_status, tax_id_verified_at, currency, stripe_customer_id, superfaktura_client_id, updated_at) ON iam.tenants FROM eshopguard_worker;
REVOKE ALL ON billing.price_quotes FROM eshopguard_app, eshopguard_worker, eshopguard_admin;
DROP POLICY IF EXISTS tenant_isolation ON billing.price_quotes;
