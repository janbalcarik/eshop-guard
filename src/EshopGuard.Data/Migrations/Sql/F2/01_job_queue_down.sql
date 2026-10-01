-- Vrácení F2: výchozí řádky (indexy a omezení vrací migrace EF).
DELETE FROM ops.rate_limit_buckets WHERE key IN ('jev', 'openai', 'openai:tokens');
DELETE FROM ops.system_settings WHERE key = 'jobs.paused_classes';
