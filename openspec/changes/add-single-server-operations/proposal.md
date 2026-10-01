# Proposal: Provoz na jednom serveru (Docker Compose, Caddy, WAL-G, nasazení a hlídání)

## Intent

**Problém.**
- Po změnách 2–16 existuje API, worker, web s CMS a databáze, ale jen na vývojovém počítači.
- Chybí produkční server, nasazení nové verze, zálohy mimo server, ověřená obnova, hlídání a zátěžový test.
- Bez toho nejde pustit první platící zákazníky. Výpadek nebo ztráta databáze by znamenaly ztrátu nálezů, rozhodnutí, dokladů a faktur všech tenantů.

**Proč teď.**
- Uživatel rozhodl 1. 10. 2026, že start bude na jednom serveru s Linuxem a Docker Compose (architektura, část 7.1).
- F10 v plánu (databáze, část 8) končí, když „obnova na čistý server do 2 h“ projde a „zátěžový test potvrdí kapacitu z části 5“.
- Fáze 0 architektury (část 10): „Nová verze se nasadí jedním krokem a obnova na čistý server projde do 2 h“.

**Přínos.**
- **Nasazení jedním krokem.** Commit v `main` projde testy, obrazy jdou do GHCR a po schválení se nasadí na server. Migrace proběhnou předem jako `eshopguard_owner`. Když nová verze nenaběhne, server se sám vrátí na předchozí značku. Ručně se na server nesahá.
- **Ztráta dat nejvýš ~1 minuta.**
  - WAL-G posílá změny databáze průběžně (`archive_timeout = 60 s`) a denně plnou zálohu do S3 u jiného poskytovatele;
  - zálohy jsou šifrované a chráněné proti smazání (object lock);
  - přežijí i výpadek celého poskytovatele serveru.
- **Ověřená obnova.** Měsíční zkouška obnovy na čistý server měří čas, cíl je do 2 h. Neověřená záloha se nepočítá jako záloha.
- **Hlídání:**
  - dostupnost zvenku každou minutu;
  - zdraví API a workerů;
  - fronta (P0 čeká déle než 30 s, noční běhy nestihnou okno);
  - chyby 5xx, disk, zálohy, certifikáty, restartující se kontejnery.

  Upozornění chodí e-mailem a SMS.
- **Logy bez osobních údajů a klíčů:** maskované IP, bez tokenů v adresách, bez cookie, kontrola vzorů v CI i na serveru.
- **Bezpečný server:** SSH jen klíčem, firewall, databáze bez portu ven, kontejnery bez rootu a bez zbytečných práv.
- **Zátěžový test** změří skutečnou kapacitu (souběh, tisíce e-shopů simulovaně) proti výpočtu v architektuře (části 5 a 8). Kapacitu dnes známe jen výpočtem (neměřeno).

**Fáze:** F10 (databaze-a-plan-implementace-2026-10-01.md, část 8). Odpovídá Fázi 0 v architektuře (část 10).

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 2 (aplikace, struktura `deploy/`);
  - část 7 (SSRF, sledování provozu, testy spolehlivosti, zátěžový test 50 souběžných analýz);
  - část 7.1 (server, kontejnery, nasazení, tajné klíče, zabezpečení, tři vrstvy záloh, hlídání, údržba, kdy přejít dál);
  - část 8 (kapacita, 0,25–0,28 s CPU na stránku);
  - část 11, bod 1 (poskytovatel);
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 2 (role `eshopguard_owner`, `eshopguard_app`, `eshopguard_worker`, `eshopguard_admin`, `eshopguard_cms`);
  - část 5 (souběh workerů při tisících e-shopů);
  - část 7 (`deploy/docker-compose.prod.yml`, `deploy/sql/00_roles.sql`, konfigurace);
  - F10, část 9, bod 4;
- `hostovani-srovnani-2026-10-01.md`, oddíl „Jeden server na start“ (Hetzner CCX23 ~104 €, OVH VPS-4 ~23 €, Webglobe Praha ~2 500–2 800 Kč; záloha WAL mimo poskytovatele; DNS u nezávislého poskytovatele) a „Přenositelnost“;
- změna 2 `add-solution-foundation`: `GET /health`, `efbundle`, `deploy/sql/00_roles.sql`, JSON logy, „ukazatele fronty a disku doplní změna 17“.

## Scope

In scope:
- **Server Ubuntu 24.04 LTS:**
  - zřizovací skript `deploy/server/provision.sh` a `cloud-init.yaml`;
  - uživatelé `deploy` a `ops`, SSH jen klíčem, `ufw`, pravidla `DOCKER-USER` (Docker jinak firewall obchází);
  - automatické bezpečnostní aktualizace bez automatického restartu;
  - časová synchronizace, swap;
  - kontrolní skript `verify-server.sh`.
- **Obrazy kontejnerů** (vícestupňové, bez rootu, základ připnutý přes digest): `api`, `worker`, `web`, `migrate` (`efbundle`), `postgres` (PostgreSQL 18 + WAL-G), `monitor`. Kontrola zranitelností (Trivy) v CI.
- **`deploy/docker-compose.prod.yml`:**
  - služby `caddy`, `web`, `api`, `worker`, `postgres`, `walg`, `monitor` a jednorázové `migrate`, `cms-migrate`;
  - sítě `edge`, `db` (bez internetu) a `egress`;
  - kontroly zdraví, limity paměti, `read_only`, `cap_drop: [ALL]`, `no-new-privileges`;
  - rotace logů.
- **Caddy:**
  - automatický HTTPS (Let's Encrypt), HSTS a bezpečnostní hlavičky;
  - směrování `/api/*` → `api` (SSE bez bufferu), zbytek → `web`;
  - `/admin` a `/cms-api` jen z povolených adres (hlavička `X-EG-Admin-Allowed`), `/eg-internal/*` zvenku zakázané;
  - veřejný stav `/status/api` bez podrobností;
  - limity velikosti těla;
  - logy JSON s maskováním.
- **Nasazení:**
  - GitHub Actions → GHCR (značka = commit) → schválení v prostředí `production` → SSH na server → `deploy/scripts/deploy.sh <sha>`;
  - pojistka záloh před migrací, migrace `eshopguard_owner` a `eshopguard_cms`, start, čekání na zdraví;
  - automatický návrat aplikačních kontejnerů na předchozí značku, ruční `rollback.sh`;
  - pravidlo rozšiřujících migrací (expand/contract) s kontrolou v CI.
- **Tajné klíče:**
  - soubory `/opt/eshopguard/env/<služba>.env` (0600, vlastník `deploy`), každý kontejner jen klíče, které potřebuje;
  - šablony `deploy/env/*.env.example` bez hodnot;
  - kontrola `check-env.sh` před startem;
  - šifrovaná záloha konfigurace a klíčů Data Protection.
- **Zálohy:**
  - WAL-G (průběžné WAL, denní base backup ve 20:30 UTC mimo noční okno sledování);
  - šifrování libsodium, S3 u jiného poskytovatele s object lock (30 dní);
  - pověření bez práva mazat;
  - manifest počtů řádků pro kontrolu obnovy;
  - automatické zálohy virtuálu od poskytovatele jako druhá vrstva.
- **Zkouška obnovy:**
  - `deploy/scripts/restore-drill.sh` na čistý server v režimu zkoušky: worker zastavený, odesílání e-mailů a volání Jevu, OpenAI, Stripe a SuperFaktúry vypnuté;
  - měření kroků, cíl ≤ 2 h;
  - měsíční workflow `restore-drill.yml`.
- **Hlídání:**
  - rozšíření `GET /health` (změna 2) o kontroly `queue` a `workers`, vnitřní `/health/metrics`;
  - kontejner `monitor` (zdraví, fronta, noční okno, disk, zálohy, certifikáty, 5xx, restarty, potřeba restartu, vzory v logech);
  - upozornění e-mailem, signál „mrtvého muže“ a externí kontrola dostupnosti.
- **Logy:**
  - ovladač `local` s rotací;
  - filtry Caddy (maskování IP na /24 a /48, mazání `token` a `session_id` z adresy, bez `Cookie` a `Authorization`);
  - `log-scan.sh` v CI a denně na serveru.
- **Zátěžový test:**
  - projekt `tests/EshopGuard.LoadTests` s falešnými e-shopy a falešným Jevem a OpenAI;
  - scénáře: 50 souběžných analýz, noční sledování 3 000 e-shopů simulovaně, P0 při plné frontě, souběh uživatelů API;
  - běh na dočasném serveru stejného typu.

Out of scope:
- víc serverů, Kubernetes, spravovaná databáze, PgBouncer (architektura, část 7.1, „Kdy přejít dál“);
- výběr a objednání poskytovatele serveru, S3 a služeb hlídání (K rozhodnutí body 1–3). Skripty jsou na poskytovateli nezávislé;
- nasazení bez výpadku (Kamal). Restart při nasazení trvá vteřiny a nasazuje se mimo špičku;
- OpenTelemetry s externím cílem (Grafana Cloud). Zde jen kontroly a upozornění (K rozhodnutí bod 6);
- logika aplikace a její testy (změny 2–16). Zde jen její obrazy, konfigurace a provozní kontroly;
- stavová stránka pro zákazníky a ohlášení odstávky v aplikaci (K rozhodnutí bod 8).

## Approach

1. **Vše jako kód v `deploy/`, na serveru se nic neupravuje ručně.**
   - Server se zřídí skriptem.
   - Nasazení, návrat i zkouška obnovy jsou skripty, které spouští GitHub Actions.
   - Stejné skripty se ověří na dočasném testovacím serveru dřív, než poběží na produkci.
2. **Fail-closed v provozu:**
   - nasazení odmítne pokračovat, když chybí klíč v `.env`, když zálohy nejsou čerstvé (poslední WAL starší než 5 min nebo base backup starší než 26 h), nebo když migrace selže; stará verze běží dál;
   - zkouška obnovy startuje bez klíčů externích služeb a bez workeru, takže obnovená kopie nikdy nebude stahovat e-shopy, volat placené API, posílat e-maily ani strhávat platby;
   - zátěžový test odmítne běžet s reálným klíčem Jevu nebo OpenAI.
3. **Nejmenší práva:**
   - port ven má jen `caddy` (80, 443/tcp, 443/udp);
   - databáze je jen v síti `db` bez přístupu z internetu;
   - každý kontejner má jen své klíče;
   - kontejnery běží bez rootu, s `read_only`, `cap_drop: [ALL]` a `no-new-privileges`;
   - pověření pro zálohy nesmí mazat a staré zálohy maže až životní cyklus bucketu po uplynutí object lock.
4. **Migrace jsou jen rozšiřující** (expand/contract): přidání sloupce nebo tabulky v jednom vydání, odebrání nejdřív ve vydání, které už starý sloupec nepoužívá. Díky tomu jde po migraci vrátit předchozí značku aplikace bez vracení databáze. CI hlídá `DropColumn`, `DropTable` a `RenameColumn` bez značky `contract`.
5. **Kapacita se měří, ne odhaduje.** Zátěžový test běží proti falešným e-shopům a falešnému Jevu s limitem 1 200 požadavků/min (dokumentace jev-1.13.0) a měří CPU na stránku, propustnost fronty, férovost mezi tenanty a čekání P0. Výsledek se porovná s částmi 5 a 8 architektury. Rozdíl se zapíše jako nález, ne přizpůsobením cíle.

## Dependencies

- **2 `add-solution-foundation`:**
  - `deploy/sql/00_roles.sql` (role a hesla přes `\getenv`);
  - `GET /health` s kontrolami a kódy, `efbundle` s `ConnectionStrings:Migrations`;
  - JSON logy, `IBlobStore` (S3);
  - `deploy/docker-compose.dev.yml`.
- **3 `add-multitenant-data-model`:** schémata, RLS, `ops.audit_log`. Na počty řádků se odkazuje manifest záloh.
- **4 `add-job-queue-and-worker`:** `ops.jobs` (`resource_class`, `priority`, `state`, `not_before`), `ops.workers.heartbeat_at`, ukončení workeru po dávce v rámci leasu (2 min).
- **5 `refactor-library-into-pipeline-steps`:** ochrana proti SSRF. Výjimka pro falešné e-shopy jen v prostředí `LoadTest`.
- **8 `add-analysis-runs-in-worker`:** běhy po dávkách (scénáře zátěžového testu).
- **9–12:** klíče Google, SMTP, Stripe a SuperFaktúra a jejich proměnné; ukazatele webhooků.
- **13, 14:** obraz `web`, `/healthz`, `X-EG-Admin-Allowed`, `/eg-internal/revalidate`, `cms-migrate`, SSE přes `/api/t/{t}/runs/{r}/events`.
- **16 `add-change-monitoring`:** noční okno 0:00–6:00 a úlohy P3. Na ně se váže kontrola „noční běhy nestihnou okno“.
- **Externí:**
  - účet u poskytovatele serveru;
  - S3 u jiného poskytovatele s object lock;
  - DNS u nezávislého poskytovatele;
  - služba externí kontroly dostupnosti a SMS;
  - organizace nebo účet GitHub s GHCR.

## Done when

- **Nasazení jedním krokem:** commit v `main` → CI → schválení `production` → nová verze na serveru, `deploy.sh` skončí 0 a web, API i worker jsou zdravé.
- **Vrácení verze:** úmyslně rozbitá verze (API nenaběhne) se do 3 min sama vrátí na předchozí značku a upozornění odejde.
- **Obnova:** zkouška obnovy na čistý server (Ubuntu 24.04) projde **do 2 h**:
  - počty řádků odpovídají manifestu;
  - poslední zápis v `ops.audit_log` je nejvýš 5 min před koncem zálohy (RPO);
  - během zkoušky neodešel žádný e-mail ani volání Jevu, OpenAI, Stripe a SuperFaktúry.
- **Zátěžový test** proběhne na dočasném serveru stejného typu:
  - 50 souběžných analýz doběhne bez ztracené a zdvojené úlohy;
  - P0 čeká při plné frontě nejvýš 30 s (p99);
  - zpráva uvede naměřené CPU na stránku a propustnost a porovná je s architekturou (části 5 a 8);
  - noční sledování 100 e-shopů se vejde do okna 0:00–6:00 s rezervou aspoň 50 %.
- **Hlídání:** zastavené API, zaplněný disk (> 90 %), rozbité archivování WAL a úloha P0 čekající 31 s vyvolají upozornění do 10 min.
- **Zabezpečení:**
  - externí sken portů najde otevřené jen 80 a 443 (22 jen z povolených adres);
  - přihlášení SSH heslem je odmítnuto;
  - `docker compose ps` ukazuje publikované porty jen u `caddy`.
- **Logy:** `log-scan.sh` nenajde v logech za 24 h žádný e-mail, token, klíč ani nemaskovanou IP.
- **Validace:** `openspec validate add-single-server-operations` projde.

## K rozhodnutí

1. **Poskytovatel serveru** (architektura, část 11, bod 1; databáze, část 9, bod 4):
   - Hetzner Cloud CCX23 (~104 € se zálohami, živý přesun při poruše, vyhrazená jádra);
   - OVH VPS-4 (~23 €, vyhrazená jádra neověřena, nejdřív změřit výkon);
   - Webglobe Praha (~2 500–2 800 Kč, DC v ČR).

   Doporučení: Hetzner CCX23 kvůli vyhrazeným jádrům a přesunu při poruše; zkoušku obnovy jde na Hetzneru automatizovat přes `hcloud`. Skripty počítají s libovolným Ubuntu 24.04.
2. **S3 pro zálohy a soubory u jiného poskytovatele než server.** Podmínka: object lock (WORM), životní cyklus a pověření bez mazání. Kandidáty a ceny (Backblaze B2, Wasabi, Scaleway, OVH Object Storage, Hetzner Object Storage při serveru jinde) je potřeba ověřit. Neověřeno, zda Hetzner Storage Box z hostování podporuje S3 a object lock.
3. **Služby hlídání a upozornění:** externí kontrola dostupnosti s SMS (UptimeRobot, Better Stack…) a signál „mrtvého muže“ (Healthchecks.io nebo vlastní). Doporučení: jedna služba pro obojí v EU. Cena a sídlo neověřeny.
4. **SSH z GitHub Actions vs. „SSH jen z vybraných adres“** (nesrovnalost v architektuře, část 7.1). Hostované runnery GitHubu mají tisíce měnících se adres, takže je nejde povolit jednotlivě. Možnosti:
   - **A:** Tailscale (nebo WireGuard) s dočasným klíčem v workflow, SSH povolené jen na rozhraní `tailscale0`;
   - **B:** self-hosted runner na serveru (spouští kód repozitáře na produkci, jen pro chráněnou větev);
   - **C:** server si sám stahuje nové značky (pull) bez příchozího SSH.

   Doporučení A. Do rozhodnutí je deploy job napsaný pro A s proměnnou `DEPLOY_NETWORK=tailscale|direct`.
5. **Podpora PostgreSQL 18 ve WAL-G.** Ověřit podle poznámek k vydání WAL-G před sestavením obrazu `postgres`. Když podpora chybí, náhrada je pgBackRest se stejnými pravidly (šifrování, S3, object lock, test obnovy).
6. **Sledování provozu (OpenTelemetry, metriky, grafy)** z architektury (část 7) je zde zjednodušené na kontroly s upozorněním. Grafy (Grafana Cloud nebo vlastní Prometheus a Grafana) později.
7. **Uchování záloh:** 14, nebo 30 dní (architektura: 14–30). Návrh 30 dní kvůli smazání tenanta („zálohy se dočistí jejich rotací“): data smazaného tenanta zmizí ze záloh nejpozději za 30 dní. Ověřit se zásadami ochrany soukromí.
8. **Ohlášená odstávka** (architektura, část 7.1, Údržba): restart serveru po aktualizacích se nespouští sám. `monitor` ohlásí „restart potřeba“ a provoz ho naplánuje. Kde se odstávka ohlásí zákazníkům (pruh v aplikaci, e-mail, stavová stránka), zatím není navrženo.
9. **Zálohy souborů v S3 aplikace** (snímky HTML, protokoly PDF, doklady). Architektura je drží mimo server. Návrh: verzování bucketu a životní cyklus 30 dní pro staré verze. Replikace k druhému poskytovateli až podle ceny.
10. **Prostředí pro zátěžový test a zkoušku obnovy:** dočasný server stejného typu po dobu testu (hodinová cena podle poskytovatele, neověřeno). Spouští se jen se souhlasem uživatele (úkoly 9.2 a 12.3).
11. **Rozdělení paměti na 16 GB (CCX23)** je odhad: PostgreSQL 6 GB (`shared_buffers` 4 GB), worker 4 GB, API 1,5 GB, web 1,5 GB, ostatní 1 GB. Upřesní zátěžový test.
