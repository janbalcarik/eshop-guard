# Design: Provoz na jednom serveru

Cesty jsou relativně ke kořeni repozitáře `eshop-guard/` (po změně 1). Na serveru je kořen `/opt/eshopguard`.

## Technical Approach

### Server

| Oblast | Nastavení |
|---|---|
| Systém | Ubuntu 24.04 LTS, časové pásmo UTC, `systemd-timesyncd`, swap 4 GB (`vm.swappiness=10`) |
| Uživatelé | `deploy` (nasazení, skupina `docker`), `ops` (správa, `sudo`). Root bez přihlášení |
| SSH (`/etc/ssh/sshd_config.d/10-eshopguard.conf`) | `PermitRootLogin no`, `PasswordAuthentication no`, `KbdInteractiveAuthentication no`, `PubkeyAuthentication yes`, `AllowUsers deploy ops`, `MaxAuthTries 3` |
| Firewall `ufw` | `default deny incoming`, `allow 80/tcp`, `allow 443/tcp`, `allow 443/udp`, `22/tcp` jen z `ADMIN_SSH_CIDRS` (nebo jen na `tailscale0`, K rozhodnutí bod 4) |
| Docker a firewall | Docker publikuje porty mimo `ufw`. Pravidla v řetězci `DOCKER-USER` (`/etc/ufw/after.rules`) propustí ke kontejnerům z rozhraní internetu jen nová spojení na 80 a 443 ke `caddy` a odpovědi na existující spojení |
| Docker (`/etc/docker/daemon.json`) | `log-driver: local` (`max-size: 20m`, `max-file: 10`), `live-restore: true`, `userland-proxy: false`, `no-new-privileges: true`. Docker Engine z oficiálního repozitáře s připnutou verzí |
| Aktualizace | `unattended-upgrades` jen bezpečnostní, `Automatic-Reboot "false"`. Potřebu restartu hlásí `monitor` |
| Adresáře | `/opt/eshopguard/{compose,env,state,bin}` (`deploy`, 0750; `env` 0700, soubory 0600) |

### Kontejnery (`deploy/docker-compose.prod.yml`)

| Služba | Obraz | Sítě | Porty ven | Zdraví | Paměť | Pozn. |
|---|---|---|---|---|---|---|
| `caddy` | `caddy:2` (digest) | `edge`, `egress` | 80, 443/tcp, 443/udp | `caddy validate` + `wget -q localhost:2019/config/` | 256 MB | svazky `caddy_data`, `caddy_config` |
| `web` | `ghcr.io/${GHCR_OWNER}/eshopguard-web:${IMAGE_TAG}` | `edge`, `db`, `egress` | – | `GET /healthz` | 1,5 GB | `read_only`, `tmpfs /tmp` a `.next/cache` |
| `api` | `…/eshopguard-api:${IMAGE_TAG}` | `edge`, `db`, `egress` | – | `GET /health` | 1,5 GB | svazek `dpkeys` |
| `worker` | `…/eshopguard-worker:${IMAGE_TAG}` | `db`, `egress` | – | soubor srdečního tepu `/tmp/heartbeat` mladší 60 s | 4 GB | `stop_grace_period: 150s` (lease 2 min), svazek `dpkeys` |
| `postgres` | `…/eshopguard-postgres:18-walg-<verze>` | `db`, `egress` | – | `pg_isready` | 6 GB | svazek `pgdata`. `egress` jen kvůli `archive_command` do S3 |
| `walg` | stejný obraz jako `postgres`, příkaz `supercronic` | `db`, `egress` | – | stáří posledního úspěšného běhu | 512 MB | `pgdata` jen pro čtení, denní base backup, manifest, záloha konfigurace |
| `monitor` | `…/eshopguard-monitor:${IMAGE_TAG}` | `db`, `edge`, `egress` | – | stáří vlastního tepu | 128 MB | `/:/host:ro` jen pro `df` a `/var/run/reboot-required` |
| `migrate` (profil `tools`) | `…/eshopguard-migrate:${IMAGE_TAG}` | `db` | – | – | 512 MB | jednorázově `efbundle` jako `eshopguard_owner` |
| `cms-migrate` (profil `tools`) | obraz `web`, příkaz `payload migrate` | `db` | – | – | 512 MB | jednorázově jako `eshopguard_cms` |

Sítě:
- `edge`: Caddy ↔ web/api;
- `db`: `internal: true`, bez internetu;
- `egress`: odchozí spojení (crawl, Jev, OpenAI, Stripe, SuperFaktúra, SMTP, S3, ACME).

Všechny služby mají `restart: unless-stopped`, `security_opt: [no-new-privileges:true]`, `cap_drop: [ALL]` (Caddy dostane jen `NET_BIND_SERVICE`) a běží pod neprivilegovaným uživatelem.

### Tajné klíče (`/opt/eshopguard/env/*.env`, šablony `deploy/env/*.env.example`)

| Soubor | Klíče (názvy) | Kdo čte |
|---|---|---|
| `postgres.env` | `POSTGRES_PASSWORD` a hesla rolí podle `deploy/sql/00_roles.sql` (změna 2) včetně `eshopguard_cms`, heslo `eshopguard_backup` a `eshopguard_monitor` | `postgres` (jen první inicializace) |
| `migrate.env` | `ConnectionStrings__Migrations` (role `eshopguard_owner`) | `migrate` |
| `api.env` | `ConnectionStrings__App`, `Storage__S3__*`, `DataProtection__KeyRingPath`, `Stripe__SecretKey`, `Stripe__WebhookSecret`, `SuperFaktura__*`, `Authentication__Google__ClientSecret`, `Smtp__*` | `api` |
| `worker.env` | `ConnectionStrings__Worker`, `Storage__S3__*`, `DataProtection__KeyRingPath`, `TYPESAFE_API_KEY`, `OPENAI_API_KEY`, `Stripe__SecretKey`, `SuperFaktura__*`, `Smtp__*` | `worker` |
| `web.env` | `CMS_DATABASE_URI` (role `eshopguard_cms`), `PAYLOAD_SECRET`, `REVALIDATE_SECRET`, `CMS_MEDIA_S3_*`, `CMS_MEDIA_PUBLIC_URL`, `SMTP_*`, `API_INTERNAL_URL`, `SITE_ROUTING`, `SITE_DOMAIN_*` | `web`, `cms-migrate` |
| `walg.env` | `WALG_S3_PREFIX`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_ENDPOINT`, `AWS_REGION`, `WALG_LIBSODIUM_KEY`, `WALG_COMPRESSION_METHOD=zstd`, `PGUSER=eshopguard_backup`, `PGPASSWORD`, `CONFIG_BACKUP_AGE_RECIPIENT`, `BACKUP_HEARTBEAT_URL` | `postgres` (archivace WAL), `walg` |
| `monitor.env` | `MONITOR_PG_DSN` (role `eshopguard_monitor`), `ALERT_SMTP_*`, `ALERT_TO`, `MONITOR_HEARTBEAT_URL` | `monitor` |
| `caddy.env` | `SITE_HOSTS`, `ACME_EMAIL`, `CMS_ADMIN_ALLOWED_CIDRS` | `caddy` |
| `ghcr.token` | token GitHub jen pro čtení balíčků | `deploy.sh` |

Pravidla:
- `deploy/scripts/check-env.sh` ověří, že každý soubor má všechny klíče ze své šablony a žádný klíč navíc, a že práva jsou `0600` a vlastník `deploy`;
- zakázané dvojice jsou explicitně v testu, například `TYPESAFE_API_KEY` ve `web.env` nebo `Stripe__SecretKey` ve `web.env`;
- klíč šifrování záloh `WALG_LIBSODIUM_KEY` a soukromý klíč `age` k záloze konfigurace jsou navíc mimo server (správce hesel majitele). Bez nich zálohy nejdou obnovit.

### Caddy (`deploy/caddy/Caddyfile`)

| Cesta | Cíl | Poznámka |
|---|---|---|
| `/api/t/*/runs/*/events`, `/api/*/events` | `api:8080` | `flush_interval -1` (SSE bez bufferu) |
| `/api/*` | `api:8080` | `request_body max_size 25MB` (doklady do 20 MB + rezerva) |
| `/status/api` | `api:8080/health` | `handle_response`: 2xx → `200 ok`, jinak `503`, bez těla API |
| `/admin*`, `/cms-api*` | `web:3000` | jen `remote_ip {$CMS_ADMIN_ALLOWED_CIDRS}` + `header_up X-EG-Admin-Allowed 1`. Ostatní → `404` |
| `/eg-internal/*` | – | `404` vždy zvenku (volá se jen po síti `edge`/`db`) |
| ostatní | `web:3000` | `request_body max_size 2MB` |

Pro všechny cesty platí:
- `header_up -X-EG-Admin-Allowed` (klient ji nesmí podvrhnout);
- hlavičky `Strict-Transport-Security: max-age=31536000; includeSubDomains`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin` (CSP nastavuje Next.js);
- přesměrování HTTP → HTTPS a `encode zstd gzip`;
- log JSON s filtry: `request>remote_ip` a `request>client_ip` `ip_mask 24 48`, `request>uri` `query { delete token; delete session_id; delete code; delete state }`, `request>headers>Cookie delete`, `request>headers>Authorization delete`, `resp_headers>Set-Cookie delete`.

### Nasazení (`deploy/scripts/deploy.sh <sha>`)

```
0  flock /opt/eshopguard/state/deploy.lock (jedno nasazení najednou)
1  check-env.sh (všechny služby)                                   → chyba = konec, nic se nemění
2  docker login ghcr.io (ghcr.token, jen čtení); IMAGE_TAG=<sha>; compose pull
3  pojistka záloh: poslední WAL archivovaný < 5 min (pg_stat_archiver),
   poslední base backup < 26 h (wal-g backup-list)                   → jinak konec s kódem deploy.backup_stale
4  compose run --rm migrate      (efbundle, eshopguard_owner)        → chyba = konec, běží stará verze
5  compose run --rm cms-migrate  (payload migrate, eshopguard_cms)    → chyba = konec
6  compose up -d --remove-orphans web api worker monitor
7  čekání na zdraví ≤ 120 s: api /health 200, web /healthz 200, https://<host>/status/api 200
   → selže: IMAGE_TAG=<previous> compose up -d web api worker; upozornění deploy.rolled_back; konec ≠ 0
8  state/previous_tag ← state/current_tag; state/current_tag ← <sha>; řádek do state/deploy.log
```

`rollback.sh` = `deploy.sh <previous_tag> --skip-migrations`. Migrace jsou rozšiřující, takže předchozí značka s novějším schématem běží. Změna 2 už počítá s tím, že „migrace navíc v databázi (starší kód po vrácení verze) se jen zapíše do logu“.

### Zálohy

| Vrstva | Co | Kdy | Kde | Uchování |
|---|---|---|---|---|
| 1 WAL-G | WAL (`archive_command = 'wal-g wal-push %p'`, `archive_timeout = 60`) | průběžně | S3 jiného poskytovatele, šifrováno libsodium, zstd | 30 dní (object lock + životní cyklus) |
| 1 WAL-G | base backup (`wal-g backup-push $PGDATA`) | denně 20:30 UTC (mimo noční okno 0:00–6:00 SEČ/SELČ) | totéž | 30 dní |
| 1 manifest | `manifests/<backup>.json`: počty řádků `iam.tenants`, `shop.shops`, `checks.findings`, `fixes.fix_proposals`, `billing.invoices`, poslední `ops.audit_log.at`, LSN | po base backupu | totéž | 30 dní |
| 1 konfigurace | `tar` z `/opt/eshopguard/env` a svazku `dpkeys` šifrovaný `age` (veřejný klíč na serveru) | denně | totéž, `config/` | 30 dní |
| 2 poskytovatel | automatická záloha virtuálu | denně | u poskytovatele | podle poskytovatele |
| 3 zkouška | `restore-drill.sh` na čistý server | měsíčně | – | výsledek JSON 12 měsíců |

Role `eshopguard_backup`:
- `LOGIN REPLICATION`;
- `EXECUTE` na `pg_backup_start`, `pg_backup_stop`, `pg_switch_wal`;
- `pg_read_all_settings`.

Pověření S3 pro `walg` má jen `PutObject`, `GetObject` a `ListBucket` a nemá `DeleteObject`.

### Zkouška obnovy (`deploy/scripts/restore-drill.sh --target <host> [--time <ISO>]`)

```
T0  čistý Ubuntu 24.04 (poskytovatel CLI nebo ručně)            měří se každý krok
T1  provision.sh                                                   ~10 min (odhad)
T2  stažení config/<latest>.tar.age, dešifrování (age klíč dodá obsluha), check-env.sh
T3  compose up postgres (prázdný PGDATA) → stop; wal-g backup-fetch $PGDATA LATEST;
    restore_command = 'wal-g wal-fetch %f %p'; recovery.signal; volitelně recovery_target_time
T4  start postgres, čekání na pg_is_in_recovery() = false
T5  REŽIM ZKOUŠKY: worker --scale 0; api/web s DRILL_MODE=1 (odesílání outbox vypnuto,
    Stripe/SuperFaktúra/Jev/OpenAI klíče prázdné → služby odmítnou start operací); DNS se nemění
T6  kontroly: /health 200; počty řádků = manifest; poslední ops.audit_log.at ≥ konec zálohy − 5 min;
    přihlášení testovacím účtem provozu do aplikace přes /etc/hosts
T7  výsledek JSON (časy kroků, celkem, RPO, shoda manifestu) → artefakt workflow + s3://…/drills/
    celkem > 2 h nebo neshoda → upozornění drill.failed
```

### Hlídání

| Kontrola | Zdroj | Interval | Práh | Kód upozornění |
|---|---|---|---|---|
| Dostupnost zvenku | externí služba: `https://<host>/healthz`, `/status/api` | 1 min | 2 selhání po sobě | (externí, SMS + e-mail) |
| Zdraví API | `GET api:8080/health` | 1 min | ≠ 200 | `health.api_failed` |
| Fronta P0 | `/health` kontrola `queue` (nejstarší `queued` s `priority=0`) | 1 min | > 30 s | `queue.p0_stale` |
| Workery | `/health` kontrola `workers` (`ops.workers.heartbeat_at`) | 1 min | žádný živý < 120 s | `workers.none_alive` |
| Noční okno | `/health/metrics` (`priority=3`, `state=queued`) | 06:00 Europe/Bratislava | > 0 | `night.window_missed` |
| Selhané úlohy | `/health/metrics` (`failed` za hodinu) | 15 min | > 0 (souhrn) | `jobs.failed` |
| Chyby 5xx | logy `caddy` za 5 min | 5 min | > 1 % požadavků a zároveň ≥ 20 | `http.5xx_rate` |
| Restarty | `docker inspect` `RestartCount` | 5 min | nárůst ≥ 3 za 15 min | `container.restart_loop` |
| Disk | `df` `/host` a datový kořen Dockeru | 5 min | volno < 20 % / < 10 % | `disk.low` / `disk.critical` |
| Archivace WAL | `pg_stat_archiver` (role `eshopguard_monitor`) | 5 min | `last_archived_time` > 10 min při zápisech, nárůst `failed_count` | `backup.wal_stale` |
| Base backup | `wal-g backup-list` (v `walg`) | 1 h | poslední > 26 h | `backup.base_stale` |
| Certifikát | `openssl s_client` na `SITE_HOSTS` | denně | < 14 dní | `tls.expiring` |
| Restart systému | `/host/var/run/reboot-required` | denně | existuje > 3 dny | `os.reboot_required` |
| Logy | `log-scan.sh` za 24 h | denně | nález | `logs.sensitive_pattern` |
| Monitor sám | ping `MONITOR_HEARTBEAT_URL` | 5 min | chybí 15 min | (externí „mrtvý muž“) |

`alert.sh`:
- posílá e-mail přes `ALERT_SMTP_*`;
- odstraňuje duplicity (jeden kód nejvýš jednou za hodinu, `state/alerts/<kód>`);
- při návratu do normálu pošle „vyřešeno“;
- text upozornění neobsahuje data zákazníků, jen kód, službu, hodnotu a čas.

### Zátěžový test (`tests/EshopGuard.LoadTests`)

| Součást | Popis |
|---|---|
| `FakeShops` | Kestrel. E-shopy podle hlavičky `Host` (`shop-0001.loadtest.internal` …) se sitemap, produkty a právními stránkami. HTML ~300 kB z fixture vegis (anonymizováno), `ETag`/`Last-Modified`/304, podíl změněných stránek nastavitelný (výchozí 2 %) |
| `FakeJev`, `FakeOpenAi` | Kestrel. Latence podle naměřeného rozdělení (výchozí 300 ms), limit 1 200 požadavků/min na klíč → 429 s `Retry-After`, deterministické odpovědi |
| Pojistky | Test se ukončí s chybou, když je nastavený `TYPESAFE_API_KEY` nebo `OPENAI_API_KEY`, nebo když adresy Jevu či OpenAI nemíří na falešné služby. Výjimka SSRF pro `*.loadtest.internal` platí jen v `ASPNETCORE_ENVIRONMENT=LoadTest` a v `Production` se ignoruje |
| Scénáře | **S1** 50 souběžných úvodních analýz × 500 stránek. **S2** noční sledování 3 000 e-shopů (sitemap 5 000 URL, rotace 1/7 + 2 % změn, počet stránek zmenšený faktorem `--scale` (výchozí 0,1), výsledky přepočtené). **S3** během S2 každých 10 s úloha P0 (odhad ceny) a měření čekání. **S4** 200 souběžných uživatelů API (Přehled, Opravy, revize) nad daty 3 000 e-shopů |
| Měření | Stránky/s, CPU s/stránku, převzetí úloh/s, čekání P0 (p50/p95/p99), podíl výkonu na tenanta, vrácené leasy, zdvojené výsledky (kontrola jedinečných klíčů), latence API p95, velikost databáze a WAL za hodinu |
| Výstup | `load-test-<datum>.json` + souhrn v `$GITHUB_STEP_SUMMARY`, porovnání s architekturou (0,25–0,28 s CPU/stránka, 2,5 stránky/s na doménu, kapacita z části 5) |

## Architecture Decisions

1. **AD-1 Compose a skripty místo Kamal, Ansible nebo Kubernetes.** Odpovídá rozhodnutí z 1. 10. 2026 (architektura, část 7.1). Přechod na víc serverů je jiné nasazení (Helm), ne jiná aplikace. Obrazy, proměnné prostředí a kontroly zdraví jsou stejné.
2. **AD-2 Jen Caddy má porty ven a Docker je spoutaný v `DOCKER-USER`.** `ufw` sám nestačí, protože Docker zapisuje vlastní pravidla `iptables`. Databáze nemá žádné `ports:` a je v síti `internal: true`.
3. **AD-3 WAL-G uvnitř obrazu PostgreSQL.** `archive_command` běží v kontejneru databáze, takže WAL-G musí být v jejím obrazu. Base backup potřebuje přístup k `PGDATA`. Proto `walg` používá stejný obraz se svazkem jen pro čtení. Binárka WAL-G má připnutou verzi a ověřený SHA-256.
4. **AD-4 Ochrana záloh proti smazání ze serveru.** Kdo ovládne server, nesmí smazat zálohy. Pověření bez `DeleteObject` a object lock v režimu compliance na 30 dní. Mazání starých záloh dělá jen životní cyklus bucketu.
5. **AD-5 Pojistka záloh před migrací.** Bez čerstvé zálohy se nemigruje. Rozbitá migrace na databázi bez zálohy je jediný nevratný krok nasazení.
6. **AD-6 Automatický návrat jen u aplikačních kontejnerů.** Databáze se nevrací. Rozšiřující migrace zaručí, že předchozí kód běží nad novým schématem. Odebírající migrace (`contract`) vyžadují značku v souboru migrace a schválení v PR.
7. **AD-7 Režim zkoušky obnovy.**
   - Obnovená kopie produkce je stejně nebezpečná jako produkce: worker by začal stahovat e-shopy a volat Jev, outbox by poslal e-maily a fakturační úlohy by volaly Stripe.
   - Proto se startuje bez workeru, s `DRILL_MODE=1` (API a web odmítnou odesílání outbox a externí volání) a s prázdnými klíči externích služeb.
   - DNS se nemění.
8. **AD-8 Hlídání jako kontejner se skripty.** Pro jeden server stačí `supercronic` a shellové skripty nad `/health`, `/health/metrics`, `pg_stat_archiver`, `df` a logy Dockeru.
   - Externí služba hlídá dostupnost a samotný `monitor` (mrtvý muž).
   - Grafy a OpenTelemetry jsou K rozhodnutí.
9. **AD-9 `/health` s frontou a workery, podrobná čísla jen uvnitř.**
   - Veřejná `/status/api` vrací jen 200/503.
   - Kontroly `queue` a `workers` doplňují `GET /health` ze změny 2 (jen názvy, stavy a kódy).
   - `/health/metrics` s čísly je dostupný jen v síti `edge`/`db`, protože Caddy ho nesměruje.
10. **AD-10 Zátěžový test proti falešným službám se skutečnými limity.** Placený Jev ani OpenAI se nikdy nevolá. Falešný Jev má stejný limit 1 200 požadavků/min, takže test ukáže i chování globálního čítače limitů (změna 4).

## Data Flow

### Nasazení

```
git push main → .github/workflows/ci.yml (build, test, web, Trivy, log-scan nad logy testů)
  → .github/workflows/deploy.yml
      build & push ghcr.io/<owner>/eshopguard-{api,worker,web,migrate,monitor}:<sha>
      environment production (ruční schválení)
      síť: Tailscale (dočasný klíč) | direct
      ssh deploy@server /opt/eshopguard/bin/deploy.sh <sha>
          → check-env → pull → pojistka záloh → migrate → cms-migrate → up -d → zdraví ≤ 120 s
          → ok: state/current_tag   |   chyba: návrat na previous_tag + upozornění
```

### Požadavek uživatele

```
internet :443 → caddy (TLS, hlavičky, maskovaný log)
  /api/*            → api:8080 → postgres (eshopguard_app, RLS)    SSE: flush_interval -1
  /admin, /cms-api  → (povolená IP) → web:3000 (Payload, eshopguard_cms)
  ostatní           → web:3000 (Next.js) → api:8080 po síti edge (API_INTERNAL_URL)
worker → postgres (eshopguard_worker) → egress: e-shopy, Jev, OpenAI, S3
```

### Záloha a obnova

```
postgres: každý WAL segment / 60 s → wal-g wal-push → S3 (šifrováno, object lock)
walg 20:30 UTC: wal-g backup-push → manifest → záloha konfigurace (age) → ping BACKUP_HEARTBEAT_URL
restore-drill: čistý server → provision → config (age) → backup-fetch + wal-fetch → REŽIM ZKOUŠKY → kontroly → JSON
```

## File Changes

```
deploy/
  docker-compose.prod.yml
  caddy/Caddyfile
  postgres/Dockerfile                    FROM postgres:18 (digest) + wal-g (verze, sha256)
  postgres/postgresql.conf               archive_mode, archive_command, archive_timeout, wal_level=replica,
                                         shared_buffers, effective_cache_size, max_connections, log_line_prefix bez dotazů
  postgres/initdb/10-eshopguard.sh       spustí deploy/sql/00_roles.sql (změna 2), 20_cms_schema.sql (změna 14),
                                         30_backup_role.sql, 31_monitor_role.sql
  sql/30_backup_role.sql                 role eshopguard_backup
  sql/31_monitor_role.sql                role eshopguard_monitor (pg_monitor, SELECT na ops.jobs a ops.workers bez obsahu)
  walg/crontab                           20:30 base backup + manifest + config backup
  walg/backup-base.sh, walg/write-manifest.sh, walg/backup-config.sh
  monitor/Dockerfile, monitor/crontab
  monitor/check-health.sh, check-queue.sh, check-night-window.sh, check-errors.sh, check-restarts.sh,
          check-disk.sh, check-backups.sh, check-certs.sh, check-reboot.sh, alert.sh
  docker/migrate.Dockerfile              efbundle --self-contained -r linux-x64
  env/postgres.env.example, migrate.env.example, api.env.example, worker.env.example, web.env.example,
      walg.env.example, monitor.env.example, caddy.env.example
  server/provision.sh, server/cloud-init.yaml, server/sshd-eshopguard.conf, server/ufw-after.rules,
         server/daemon.json, server/50unattended-upgrades, server/verify-server.sh, server/maintenance-reboot.sh
  scripts/deploy.sh, rollback.sh, check-env.sh, check-migrations.sh, log-scan.sh, log-scan.allowlist,
          restore-drill.sh
  tests/compose-smoke.sh, tests/caddy-routes.sh, tests/monitor-alerts.sh, tests/check-env.bats
src/EshopGuard.Api/Dockerfile
src/EshopGuard.Api/Health/QueueHealthCheck.cs, WorkersHealthCheck.cs, MetricsEndpoint.cs  (/health doplněk, /health/metrics)
src/EshopGuard.Api/DrillMode/DrillModeGuard.cs     (DRILL_MODE=1: odmítne outbox a externí volání)
src/EshopGuard.Worker/Dockerfile
src/EshopGuard.Worker/Health/HeartbeatFileWriter.cs   (/tmp/heartbeat)
web/Dockerfile                          Next.js standalone, node:24-slim (digest), uživatel node, payload migrate
tests/EshopGuard.LoadTests/
  EshopGuard.LoadTests.csproj, Program.cs (scénáře S1–S4, --scale, --scenario)
  FakeShops/FakeShopServer.cs, FakeShops/ShopGenerator.cs
  FakeServices/FakeJevServer.cs, FakeServices/FakeOpenAiServer.cs
  Guards/RealKeyGuard.cs, Measurements/*.cs, Report/LoadTestReport.cs
.github/
  workflows/ci.yml                      (vytvořit, nebo rozšířit): dotnet + PostgreSQL 18, web, Trivy, check-migrations, log-scan
  workflows/deploy.yml                  obrazy → GHCR, environment production, deploy.sh
  workflows/restore-drill.yml           měsíčně + ručně
  workflows/load-test.yml               ručně
  dependabot.yml                        docker, github-actions, nuget, npm
README.md                               oddíl „Provoz“ (zřízení serveru, nasazení, návrat, obnova, upozornění)
```
