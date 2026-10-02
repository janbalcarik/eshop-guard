-- F8Billing (change 12): RLS of the new tenant table billing.price_quotes, the privileges billing needs, the audit of the
-- global price lists and the default price lists SK/EUR and CZ/CZK as drafts (published through the admin API).

-- 1. billing.price_quotes: a table of a tenant (RLS with FORCE, the same policy as the other tables of tenants).
ALTER TABLE billing.price_quotes ENABLE ROW LEVEL SECURITY;
ALTER TABLE billing.price_quotes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON billing.price_quotes
    USING (tenant_id = (SELECT ops.current_tenant_id()))
    WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.price_quotes TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.price_quotes TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON billing.price_quotes TO eshopguard_admin;

-- 2. The worker writes what Stripe reports about a tenant (customer.tax_id.*, the currency of the first payment, the client of
--    SuperFaktúra); only these columns, never the billing details of the tenant.
GRANT UPDATE (tax_id_status, tax_id_verified_at, currency, stripe_customer_id, superfaktura_client_id, updated_at) ON iam.tenants TO eshopguard_worker;

-- 3a. The tiers and discounts of a draft are replaced by the worker (the admin API of price lists, change 12).
GRANT DELETE ON billing.price_tiers, billing.volume_discounts TO eshopguard_worker;

-- 3. The nightly reconciliation (billing.reconcile_stripe) stores events whose webhook was lost.
GRANT INSERT ON billing.stripe_events TO eshopguard_worker;

-- 4. The products of Stripe (analysis, monitoring) per mode, written by the worker when the first price list is synchronized.
INSERT INTO ops.system_settings (key, value) VALUES ('billing:stripe_products:test', '{}'::jsonb), ('billing:stripe_products:live', '{}'::jsonb)
ON CONFLICT (key) DO NOTHING;

-- 5. Changes of the global price lists are audited without a tenant (actions price_list.*), only by the worker.
CREATE POLICY audit_log_insert_price_list ON ops.audit_log FOR INSERT TO eshopguard_worker
    WITH CHECK (tenant_id IS NULL AND action LIKE 'price_list.%');

-- 6. Default price lists (design of change 12, „Ceny a pásma“; strategy, part 3): drafts with the tiers, 30 days of notice,
--    fair use 2× the products, no volume discount (K rozhodnutí 4). The tier custom has no price (by agreement).
DO $$
DECLARE
    sk uuid := uuidv7();
    cz uuid := uuidv7();
BEGIN
    INSERT INTO billing.price_lists (id, name, market_code, currency, valid_from, status, notice_days, sync_status, fair_use_other_pages_factor)
    VALUES (sk, 'SK 2026', 'sk', 'EUR', now(), 'draft', 30, 'pending', 2),
           (cz, 'CZ 2026', 'cz', 'CZK', now(), 'draft', 30, 'pending', 2);

    INSERT INTO billing.price_tiers (price_list_id, code, min_products, max_products, analysis_price, monitoring_monthly, monitoring_yearly,
                                     lookup_key_analysis, lookup_key_monthly, lookup_key_yearly)
    VALUES (sk, 't500', 0, 500, 39, 9, 90, 'sk_eur_t500_analysis', 'sk_eur_t500_monthly', 'sk_eur_t500_yearly'),
           (sk, 't2000', 501, 2000, 69, 19, 190, 'sk_eur_t2000_analysis', 'sk_eur_t2000_monthly', 'sk_eur_t2000_yearly'),
           (sk, 't5000', 2001, 5000, 99, 29, 290, 'sk_eur_t5000_analysis', 'sk_eur_t5000_monthly', 'sk_eur_t5000_yearly'),
           (sk, 't20000', 5001, 20000, 199, 59, 590, 'sk_eur_t20000_analysis', 'sk_eur_t20000_monthly', 'sk_eur_t20000_yearly'),
           (sk, 'custom', 20001, NULL, NULL, NULL, NULL, NULL, NULL, NULL),
           (cz, 't500', 0, 500, 990, 249, 2490, 'cz_czk_t500_analysis', 'cz_czk_t500_monthly', 'cz_czk_t500_yearly'),
           (cz, 't2000', 501, 2000, 1790, 490, 4900, 'cz_czk_t2000_analysis', 'cz_czk_t2000_monthly', 'cz_czk_t2000_yearly'),
           (cz, 't5000', 2001, 5000, 2490, 750, 7500, 'cz_czk_t5000_analysis', 'cz_czk_t5000_monthly', 'cz_czk_t5000_yearly'),
           (cz, 't20000', 5001, 20000, 4990, 1490, 14900, 'cz_czk_t20000_analysis', 'cz_czk_t20000_monthly', 'cz_czk_t20000_yearly'),
           (cz, 'custom', 20001, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
END $$;
