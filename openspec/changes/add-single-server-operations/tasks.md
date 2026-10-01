# Tasks

Cesty jsou relativně ke kořeni repozitáře `eshop-guard/`. Vše se nejdřív ověří na dočasném testovacím serveru (Ubuntu 24.04), teprve potom na produkci.

## 1. Server a jeho zabezpečení

- [ ] 1.1 Napsat `deploy/server/provision.sh` (idempotentní, Ubuntu 24.04):
  - uživatelé `deploy` a `ops` s klíči z parametru;
  - `deploy/server/sshd-eshopguard.conf` do `/etc/ssh/sshd_config.d/`;
  - `ufw` (`default deny incoming`, 80/tcp, 443/tcp, 443/udp, 22/tcp jen z `ADMIN_SSH_CIDRS` nebo jen na `tailscale0`);
  - časové pásmo UTC, `systemd-timesyncd`, swap 4 GB;
  - adresáře `/opt/eshopguard/{compose,env,state,bin}` s právy podle `design.md`.
- [ ] 1.2 Doplnit do `provision.sh`:
  - Docker Engine z oficiálního repozitáře s připnutou verzí a `deploy/server/daemon.json` (`log-driver: local`, `max-size 20m`, `max-file 10`, `live-restore`, `userland-proxy: false`, `no-new-privileges`);
  - pravidla `DOCKER-USER` z `deploy/server/ufw-after.rules`;
  - `deploy/server/50unattended-upgrades` (jen bezpečnostní, `Automatic-Reboot "false"`);
  - `deploy/server/cloud-init.yaml`, který skript spustí při zřízení.
- [ ] 1.3 Napsat `deploy/server/verify-server.sh`:
  - `sshd -T` (`passwordauthentication no`, `permitrootlogin no`, `allowusers`);
  - `ufw status verbose`;
  - `ss -tlnp` (jen 22, 80, 443 a `docker-proxy` jen pro `caddy`);
  - `docker compose ps` (porty jen u `caddy`);
  - `unattended-upgrades` aktivní;
  - výstup OK/CHYBA po bodech a nenulový kód při chybě.

  Napsat `deploy/server/maintenance-reboot.sh` (ohlášený restart: zastavit `worker` po dokončení dávky, restart, kontrola zdraví).
- [ ] 1.4 Test: na dočasném serveru spustit `provision.sh` dvakrát za sebou (druhý běh beze změn), `verify-server.sh` vrátí 0. Z adresy mimo povolené:
  - `ssh -o PreferredAuthentications=password deploy@<host>` → odmítnuto;
  - `nmap -Pn -p- <host>` → otevřené jen 80 a 443.

## 2. Obrazy kontejnerů

- [ ] 2.1 Napsat Dockerfile:
  - `src/EshopGuard.Api/Dockerfile` a `src/EshopGuard.Worker/Dockerfile`: vícestupňové, sestavení `mcr.microsoft.com/dotnet/sdk:10.0`, běh na chiseled `aspnet`/`runtime` 10.0, uživatel bez rootu;
  - `HeartbeatFileWriter` ve workeru (`/tmp/heartbeat` každých 15 s);
  - `deploy/docker/migrate.Dockerfile` (`dotnet ef migrations bundle --self-contained -r linux-x64`, kontrola `current_user = eshopguard_owner` před spuštěním, jinak `db.migrations_wrong_role`).
- [ ] 2.2 Napsat další Dockerfile:
  - `web/Dockerfile`: Next.js `standalone`, `node:24-slim` přes digest, uživatel `node`, `payload migrate` pro `cms-migrate`;
  - `deploy/postgres/Dockerfile`: `postgres:18` přes digest + binárka WAL-G s připnutou verzí a ověřeným SHA-256, `supercronic`. Před sestavením ověřit podporu PostgreSQL 18 ve WAL-G (proposal, K rozhodnutí bod 5);
  - `deploy/monitor/Dockerfile`: `alpine` přes digest s `curl`, `jq`, `openssl`, `postgresql-client`, `supercronic`.
- [ ] 2.3 Test: v CI sestavit všechny obrazy:
  - `trivy image --severity CRITICAL --exit-code 1` projde;
  - `docker run --rm <obraz> id -u` není `0` (kromě `postgres`, který uživatele mění sám);
  - `.github/dependabot.yml` hlídá ekosystémy `docker`, `github-actions`, `nuget`, `npm`.

## 3. Docker Compose pro produkci

- [ ] 3.1 Napsat `deploy/docker-compose.prod.yml` se službami, sítěmi (`edge`, `db` s `internal: true`, `egress`), svazky (`pgdata`, `caddy_data`, `caddy_config`, `dpkeys`), `env_file` podle tabulky v `design.md` a profilem `tools` pro `migrate` a `cms-migrate`.
- [ ] 3.2 Doplnit ke každé službě:
  - `healthcheck`, `restart: unless-stopped`, `mem_limit` podle `design.md`;
  - `read_only` s `tmpfs` (`web`, `api`, `worker`, `monitor`);
  - `cap_drop: [ALL]` (u `caddy` jen `NET_BIND_SERVICE`), `security_opt: [no-new-privileges:true]`;
  - `stop_grace_period: 150s` u `worker`;
  - `depends_on` s `condition: service_healthy`.
- [ ] 3.3 Napsat konfiguraci PostgreSQL a role:
  - `deploy/postgres/postgresql.conf`: `wal_level=replica`, `archive_mode=on`, `archive_command='wal-g wal-push %p'`, `archive_timeout=60`, `shared_buffers=4GB`, `effective_cache_size=10GB`, `max_connections=100`, `log_line_prefix` bez textu dotazů, `log_statement=none`;
  - `deploy/postgres/initdb/10-eshopguard.sh`: spustí `deploy/sql/00_roles.sql` (změna 2) s `-v db_name=eshopguard`, `20_cms_schema.sql` (změna 14), `30_backup_role.sql`, `31_monitor_role.sql`.
- [ ] 3.4 Test: `deploy/tests/compose-smoke.sh` na dočasném serveru:
  - `up -d`, všechny služby `healthy` do 120 s;
  - publikované porty jen u `caddy`;
  - `nc -z 127.0.0.1 5432` z hostitele selže;
  - `docker compose exec api` se k `postgres` připojí;
  - zastavení `worker` při běžící dávce nezanechá úlohu `running` bez leasu.

## 4. Caddy

- [ ] 4.1 Napsat `deploy/caddy/Caddyfile` podle tabulky v `design.md`:
  - weby z `{$SITE_HOSTS}`, `email {$ACME_EMAIL}`;
  - `/api/*` s `flush_interval -1` pro SSE;
  - `/status/api` s `handle_response`;
  - `/admin*` a `/cms-api*` s `remote_ip {$CMS_ADMIN_ALLOWED_CIDRS}` a `header_up X-EG-Admin-Allowed 1`, jinak 404;
  - `/eg-internal/*` → 404;
  - `header_up -X-EG-Admin-Allowed` všude, limity těla, bezpečnostní hlavičky, `encode zstd gzip`;
  - log JSON s filtry (`ip_mask 24 48`, mazání `token`, `session_id`, `code`, `state`, `Cookie`, `Authorization`, `Set-Cookie`).
- [ ] 4.2 Test: `deploy/tests/caddy-routes.sh` (`caddy validate` a `curl` proti testovacímu serveru, lokálně s `tls internal`):
  - HTTP → HTTPS a HSTS;
  - `/admin` z nepovolené adresy s podvrženou hlavičkou → 404;
  - `/eg-internal/revalidate` → 404;
  - `/status/api` při zastavené databázi → 503 bez těla;
  - první událost SSE do 1 s;
  - log požadavku `/app/login/confirm?token=AbC123` bez `AbC123` a s maskovanou IP.

## 5. Tajné klíče

- [ ] 5.1 Napsat šablony `deploy/env/{postgres,migrate,api,worker,web,walg,monitor,caddy}.env.example` jen s názvy klíčů podle `design.md` a prázdnými hodnotami. Rozšířit test hygieny tajemství ze změny 2 (`SecretsHygieneTests`) o `deploy/env/` a `deploy/**`.
- [ ] 5.2 Napsat `deploy/scripts/check-env.sh`:
  - každý `/opt/eshopguard/env/<služba>.env` má všechny klíče ze šablony a žádný navíc (`env.missing_key`, `env.unexpected_key`);
  - práva `0600` a vlastník `deploy` (`env.bad_permissions`);
  - zakázané dvojice (např. `TYPESAFE_API_KEY` ve `web.env`);
  - výpis bez hodnot.
- [ ] 5.3 Napsat `deploy/walg/backup-config.sh`: denní `tar` z `/opt/eshopguard/env` a svazku `dpkeys` šifrovaný `age` veřejným klíčem `CONFIG_BACKUP_AGE_RECIPIENT` → `s3://…/config/<datum>.tar.age`. Zapsat do `README.md` (oddíl Provoz), kde mimo server leží soukromý klíč `age` a `WALG_LIBSODIUM_KEY` (správce hesel majitele, dvě kopie).
- [ ] 5.4 Test: `deploy/tests/check-env.bats`:
  - chybějící klíč, klíč navíc, práva `0644` a zakázaná dvojice → nenulový kód s příslušným kódem;
  - v `docker inspect web` nejsou `TYPESAFE_API_KEY`, `OPENAI_API_KEY`, `Stripe__SecretKey`, `ConnectionStrings__App`;
  - záloha konfigurace jde dešifrovat jen soukromým klíčem `age`.

## 6. Nasazení z GitHub Actions

- [ ] 6.1 Vytvořit (nebo rozšířit, pokud existuje ze změny 2) `.github/workflows/ci.yml`:
  - `dotnet build` a `dotnet test` se službou PostgreSQL 18;
  - joby `web` ze změn 13 a 14;
  - `check-migrations.sh`;
  - sestavení obrazů bez nahrání a Trivy;
  - `log-scan.sh` nad logy integračních testů.
- [ ] 6.2 Napsat `.github/workflows/deploy.yml`:
  - spouští se po úspěšném CI na `main` (`workflow_run`) a ručně;
  - sestaví a nahraje `ghcr.io/<owner>/eshopguard-{api,worker,web,migrate,monitor}:${{ github.sha }}` s popisky OCI;
  - `environment: production` s ručním schválením;
  - připojení podle `DEPLOY_NETWORK` (`tailscale` s dočasným klíčem `TS_AUTHKEY`, nebo `direct`);
  - `ssh deploy@$DEPLOY_HOST /opt/eshopguard/bin/deploy.sh <sha>` s `known_hosts` z tajemství;
  - žádné tajemství aplikace v GitHubu kromě klíče SSH a VPN.
- [ ] 6.3 Napsat `deploy/scripts/deploy.sh` podle kroků 0–8 v `design.md`:
  - `flock` (`deploy.locked`), `check-env.sh`;
  - `docker login` tokenem jen pro čtení, `pull`;
  - pojistka záloh (`deploy.backup_stale`);
  - `migrate`, `cms-migrate`, `up -d`, čekání na zdraví ≤ 120 s;
  - automatický návrat na `previous_tag` s upozorněním `deploy.rolled_back`;
  - zápis `current_tag`, `previous_tag` a řádku do `state/deploy.log`.

  Napsat `deploy/scripts/rollback.sh` (`deploy.sh <previous_tag> --skip-migrations`).
- [ ] 6.4 Test na dočasném serveru:
  1. nasadit značku A;
  2. nasadit B s rozšiřující migrací (nový sloupec);
  3. `rollback.sh` na A → A zdravá nad schématem B;
  4. značka C s API, které vrací 503 → automatický návrat na B do 3 min a upozornění;
  5. migrace s chybou SQL → B běží dál;
  6. rozbité archivování WAL → `deploy.backup_stale`;
  7. dvě nasazení současně → druhé `deploy.locked`.

## 7. Migrace

- [ ] 7.1 Napsat `deploy/scripts/check-migrations.sh`: v nových souborech `src/EshopGuard.Data/Migrations/*.cs` hledá `DropColumn`, `DropTable`, `RenameColumn`, `RenameTable`, `DropIndex` na sloupcích, které ještě existují v modelu, a bez komentáře `// contract` skončí chybou s názvem souboru. Do `README.md` (oddíl Provoz) zapsat pravidlo rozšiřujících migrací.
- [ ] 7.2 Ověřit, že kontejner `cms-migrate` běží jako `eshopguard_cms` a migrace Payload nesahají mimo schéma `cms` (navazuje na test změny 14, `tests/cms/db-isolation.test.ts`).
- [ ] 7.3 Test:
  - `check-migrations.sh` nad ukázkovou migrací s `DropColumn` bez značky selže, se značkou projde;
  - `migrate` s `ConnectionStrings__Migrations` role `eshopguard_app` skončí `db.migrations_wrong_role` a nic nezmění.

## 8. Zálohy WAL-G

- [ ] 8.1 Nastavit `walg.env` (S3 u jiného poskytovatele, `WALG_LIBSODIUM_KEY`, `WALG_COMPRESSION_METHOD=zstd`, `PGUSER=eshopguard_backup`) a `deploy/sql/30_backup_role.sql` (`LOGIN REPLICATION`, `EXECUTE` na `pg_backup_start`, `pg_backup_stop`, `pg_switch_wal`). Bucket u poskytovatele: object lock compliance 30 dní, životní cyklus mazání po 31 dnech, pověření `walg` bez `DeleteObject`. Postup zapsat do `README.md` (oddíl Provoz).
- [ ] 8.2 Napsat `deploy/walg/crontab` (20:30 UTC), `backup-base.sh` (`wal-g backup-push $PGDATA`), `write-manifest.sh` (počty řádků podle `design.md`, poslední `ops.audit_log.at`, LSN → `manifests/<backup>.json`) a ping `BACKUP_HEARTBEAT_URL` po úspěchu.
- [ ] 8.3 Zapnout automatické denní zálohy virtuálu u poskytovatele (druhá vrstva) a zapsat do `README.md` (oddíl Provoz), kde jsou a jak se obnovují.
- [ ] 8.4 Test na dočasném serveru:
  - zápis do `ops.audit_log`, po 2 min `wal-g wal-verify integrity` bez chyby;
  - `wal-g backup-list` obsahuje dnešní zálohu a manifest existuje;
  - smazání objektu pověřením z `walg.env` → `AccessDenied`;
  - po změně pověření na neplatné roste `pg_stat_archiver.failed_count`.

## 9. Zkouška obnovy

- [ ] 9.1 Napsat `deploy/scripts/restore-drill.sh` podle kroků T0–T7 v `design.md`:
  - měření každého kroku;
  - režim zkoušky (`worker` škálovaný na 0, `DRILL_MODE=1`, prázdné klíče Jevu, OpenAI, Stripe a SuperFaktúry, bez změny DNS);
  - kontroly manifestu a RPO;
  - výsledek JSON.

  Napsat do API `src/EshopGuard.Api/DrillMode/DrillModeGuard.cs` (odmítne odeslání outbox a externí volání, zapíše `drill.external_call_blocked`).
- [ ] 9.2 Odhad ceny + souhlas uživatele: dočasný server na 2–3 h (hodinová cena podle poskytovatele, neověřeno). Pak napsat `.github/workflows/restore-drill.yml` (první pondělí v měsíci 07:00 UTC a ručně). Zřízení serveru přes CLI poskytovatele po rozhodnutí (K rozhodnutí bod 1), do té doby parametr `--target` s ručně zřízeným serverem. Artefakt a kopie do `s3://…/drills/`. Při překročení 2 h nebo neshodě upozornění `drill.failed`.
- [ ] 9.3 Test: první zkouška ručně na čistém serveru:
  - celkem ≤ 2 h;
  - počty řádků = manifest;
  - RPO ≤ 5 min;
  - v logu zkoušky jsou `drill.external_call_blocked` a žádné odchozí spojení na Jev, OpenAI, Stripe, SuperFaktúru ani SMTP (`ss` a log `egress`).

## 10. Hlídání

- [ ] 10.1 Doplnit do API:
  - `src/EshopGuard.Api/Health/QueueHealthCheck.cs` (nejstarší `queued` úloha P0 > 30 s → `queue.p0_stale`);
  - `WorkersHealthCheck.cs` (žádný worker s `heartbeat_at` < 120 s → `workers.none_alive`);
  - `MetricsEndpoint.cs`: `/health/metrics` s počty podle `resource_class` a `priority`, stářím nejstarší úlohy, `failed` za hodinu, jen pro vnitřní síť (Caddy ho nesměruje).
- [ ] 10.2 Napsat skripty `deploy/monitor/check-*.sh` a `alert.sh` podle tabulky v `design.md`:
  - intervaly v `deploy/monitor/crontab`;
  - odstranění duplicit (`state/alerts/<kód>`, 1 h) a oznámení o vyřešení;
  - ping `MONITOR_HEARTBEAT_URL` každých 5 min;
  - `deploy/sql/31_monitor_role.sql` (`pg_monitor`, `SELECT` na `ops.jobs` a `ops.workers` bez sloupce `payload`).
- [ ] 10.3 Nastavit externí kontroly (služba podle K rozhodnutí bod 3): `https://<host>/healthz` a `/status/api` každou minutu, upozornění po 2 selháních e-mailem a SMS; „mrtvý muž“ pro `MONITOR_HEARTBEAT_URL` (15 min) a `BACKUP_HEARTBEAT_URL` (26 h).
- [ ] 10.4 Test: `deploy/tests/monitor-alerts.sh` na dočasném serveru:
  - zastavené `api` → `health.api_failed` do 2 min a „vyřešeno“ po startu;
  - `fallocate` na 91 % disku → `disk.critical` jednou za hodinu;
  - neplatná pověření S3 → `backup.wal_stale` do 10 min;
  - vložená testovací úloha P0 stará 31 s → `queue.p0_stale`;
  - zastavený `monitor` → upozornění externí služby do 15 min.

## 11. Logy

- [ ] 11.1 Napsat `deploy/scripts/log-scan.sh`:
  - vzory `email`, `stripe_key` (`sk_live_`, `sk_test_`, `rk_`), `stripe_webhook` (`whsec_`), `openai_key` (`sk-[A-Za-z0-9_-]{20,}`), `bearer`, `jwt` (`eyJ…\.…\.…`), `token_param` (`token=`), `ipv4_full` (nemaskovaná v logu Caddy);
  - výjimky z `deploy/scripts/log-scan.allowlist`;
  - výstup jen název vzoru, kontejner a číslo řádku, nikdy hodnota.
- [ ] 11.2 Zapojit `log-scan.sh` do `ci.yml` (nad logy integračních a e2e testů) a do `monitor` (denně nad `docker compose logs --since 24h` → `logs.sensitive_pattern`). Zkontrolovat, že JSON logy API a workeru ze změny 2 nezapisují e-mail, jen `user_id`, `tenant_id` a `run_id`.
- [ ] 11.3 Test:
  - log s vloženým `jana@bylinkovo.sk`, `sk_test_SENTINEL` a `Bearer SENTINEL` → `log-scan.sh` selže se třemi vzory a výstup neobsahuje `SENTINEL` ani adresu;
  - e-mail ze seznamu výjimek se nehlásí;
  - rotace: soubor logu kontejneru nepřekročí 20 MB × 10.

## 12. Zátěžový test

- [ ] 12.1 Založit `tests/EshopGuard.LoadTests`:
  - `FakeShops/FakeShopServer.cs` a `ShopGenerator.cs` (e-shopy podle `Host`, sitemap, HTML ~300 kB z anonymizované fixture, `ETag`/304, podíl změn);
  - `FakeServices/FakeJevServer.cs` a `FakeOpenAiServer.cs` (latence, 1 200 požadavků/min → 429 s `Retry-After`);
  - `Guards/RealKeyGuard.cs` (`loadtest.real_key_present`, adresy jen na falešné služby);
  - výjimka SSRF pro `*.loadtest.internal` jen v `ASPNETCORE_ENVIRONMENT=LoadTest` (změna 5) s testem, že v `Production` neplatí.
- [ ] 12.2 Napsat scénáře S1–S4 v `Program.cs` (`--scenario`, `--scale`, `--workers`), měření podle `design.md` a `Report/LoadTestReport.cs` (JSON + souhrn s porovnáním s architekturou, části 5 a 8).
- [ ] 12.3 Odhad ceny + souhlas uživatele: dočasný server stejného typu jako produkce na ~4–6 h (hodinová cena podle poskytovatele, neověřeno), žádné placené API. Pak napsat `.github/workflows/load-test.yml` (ručně, cíl `--target`, artefakt zprávy).
- [ ] 12.4 Test a přijetí na dočasném serveru:
  - S1: 50/50 běhů `finished`, 0 zdvojených výsledků, 0 ztracených úloh;
  - S3: čekání P0 v p99 ≤ 30 s;
  - S2: zpráva s CPU s/stránku, stránkami/s a přepočtem: noční sledování 100 e-shopů se vejde do 0:00–6:00 s rezervou ≥ 50 %;
  - S4: latence API p95 zapsaná. Hranice 800 ms je návrh, upřesní první měření;
  - `loadtest.real_key_present` při nastaveném `OPENAI_API_KEY`;
  - nepříznivé výsledky zapsat do proposal, K rozhodnutí, bez úpravy cílů.

## 13. Ověření

- [ ] 13.1 Zřídit produkční server (`provision.sh` nebo `cloud-init.yaml`) a `verify-server.sh` → 0. Pak jedním schválením v GitHub Actions nasadit aktuální `main`: web na `https://<doména>/sk`, aplikace na `/app/login` a API `/status/api` 200 s platným certifikátem.
- [ ] 13.2 Spustit zkoušku obnovy (úkol 9.3) z produkčních záloh na čistý server: do 2 h, shoda manifestu, RPO ≤ 5 min, žádné odchozí volání externích služeb.
- [ ] 13.3 Vyhodnotit zprávu zátěžového testu (úkol 12.4) proti kapacitě z částí 5 a 8 architektury. Rozdíly zapsat jako nález.
- [ ] 13.4 Z adresy mimo povolené spustit sken portů (jen 80 a 443) a pokus o přihlášení SSH heslem (odmítnut). `log-scan.sh` nad logy produkce za 24 h → bez nálezu. `openspec validate add-single-server-operations` projde.
