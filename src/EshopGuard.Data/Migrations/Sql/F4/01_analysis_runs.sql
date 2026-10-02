-- F4AnalysisRuns (změna 8): tabulky běhu ve workeru a nárok na ukázku zdarma.

-- checks.run_urls (výsledek každé adresy běhu) a checks.run_scopes (rozsah procházení: robots.txt, fronta, tempo) jsou data
-- tenanta: RLS s FORCE a politika tenant_isolation jako u ostatních 41 tabulek (F1/04_rls.sql), práva podle matice změny 3.
ALTER TABLE checks.run_urls ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.run_urls FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.run_urls
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

ALTER TABLE checks.run_scopes ENABLE ROW LEVEL SECURITY;
ALTER TABLE checks.run_scopes FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON checks.run_scopes
  USING (tenant_id = (SELECT ops.current_tenant_id()))
  WITH CHECK (tenant_id = (SELECT ops.current_tenant_id()));

GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_urls TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_urls TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_urls TO eshopguard_admin;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_scopes TO eshopguard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_scopes TO eshopguard_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON checks.run_scopes TO eshopguard_admin;

-- Nárok na ukázku zdarma: jednou na doménu napříč tenanty. Tabulka shop.free_sample_claims je globální (bez RLS), proto ji
-- aplikace ani worker nečtou ani nezapisují přímo: jediná cesta je tato funkce, která vrací jen true/false a nikdy neprozradí,
-- který tenant doménu použil. Volající musí mít nastavený svůj tenant a e-shop musí patřit jemu (shop.shops má RLS i pro
-- vlastníka funkce). Doména se normalizuje stejně jako v RunService (malá písmena, bez „www.“ a koncové tečky).
CREATE FUNCTION shop.claim_free_sample(p_domain text, p_tenant_id uuid, p_shop_id uuid) RETURNS boolean
LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $$
DECLARE
  v_domain text := rtrim(lower(btrim(coalesce(p_domain, ''))), '.');
BEGIN
  IF v_domain LIKE 'www.%' THEN
    v_domain := substr(v_domain, 5);
  END IF;

  IF v_domain = '' THEN
    RAISE EXCEPTION 'sample.domain_missing' USING ERRCODE = '22023';
  END IF;

  IF p_tenant_id IS DISTINCT FROM ops.current_tenant_id() THEN
    RAISE EXCEPTION 'sample.tenant_mismatch' USING ERRCODE = '42501';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM shop.shops s WHERE s.id = p_shop_id AND s.tenant_id = p_tenant_id AND s.deleted_at IS NULL) THEN
    RAISE EXCEPTION 'sample.shop_not_found' USING ERRCODE = '42501';
  END IF;

  INSERT INTO shop.free_sample_claims (domain, tenant_id, shop_id, claimed_at, created_at)
  VALUES (v_domain, p_tenant_id, p_shop_id, now(), now())
  ON CONFLICT (domain) DO NOTHING;
  RETURN FOUND;
END $$;

REVOKE ALL ON FUNCTION shop.claim_free_sample(text, uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION shop.claim_free_sample(text, uuid, uuid) TO eshopguard_app, eshopguard_worker;

REVOKE SELECT, INSERT ON shop.free_sample_claims FROM eshopguard_app, eshopguard_worker;
