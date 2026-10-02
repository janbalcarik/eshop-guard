-- Vrácení F4: funkce nároku zmizí a aplikace s workerem dostanou zpět přímý přístup podle F1. Tabulky run_urls a run_scopes
-- (s politikami a právy) odstraní migrace EF.
DROP FUNCTION IF EXISTS shop.claim_free_sample(text, uuid, uuid);
GRANT SELECT, INSERT ON shop.free_sample_claims TO eshopguard_app;
GRANT SELECT, INSERT ON shop.free_sample_claims TO eshopguard_worker;
