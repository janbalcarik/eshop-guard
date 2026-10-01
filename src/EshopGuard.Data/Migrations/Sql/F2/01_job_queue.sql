-- F2 Fronta úloh (změna 4): výchozí řádky pozastavení druhů a globálních limitů volání.
-- Indexy a omezení ops.jobs jsou v modelu EF (JobConfiguration) a vytváří je migrace sama.

-- Pozastavené druhy zdrojů: {"jev": {"reason": "jev.credit_exhausted", "at": "…"}}. Worker smí jen měnit (S U), řádek musí existovat.
INSERT INTO ops.system_settings (key, value) VALUES ('jobs.paused_classes', '{}'::jsonb)
ON CONFLICT (key) DO NOTHING;

-- Buckety limitů (token bucket, doplňování podle času). p0_p1_share = podíl kapacity vyhrazený prioritám P0 a P1.
--   jev:           1 200 dotazů za minutu (20/s), zásoba na minutu.
--   openai:        gpt-6.1-sol, Tier 4: 10 000 požadavků za minutu (166,67/s); zásoba na 10 s, protože OpenAI
--                  vynucuje limity i v kratších oknech než minuta.
--   openai:tokens: gpt-6.1-sol, Tier 4: 4 000 000 tokenů za minutu (66 666,67/s); zásoba na 10 s.
INSERT INTO ops.rate_limit_buckets (key, capacity, tokens, refill_per_sec, reserved) VALUES
    ('jev', 1200, 1200, 20, '{"p0_p1_share": 0.2}'::jsonb),
    ('openai', 1666.6666666666667, 1666.6666666666667, 166.66666666666666, '{"p0_p1_share": 0.2}'::jsonb),
    ('openai:tokens', 666666.6666666666, 666666.6666666666, 66666.66666666667, '{"p0_p1_share": 0.2}'::jsonb)
ON CONFLICT (key) DO NOTHING;
