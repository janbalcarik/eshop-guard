# Delta for Operations

## ADDED Requirements

### Requirement: Provoz: Produkční sestava Docker Compose na jednom serveru
Systém MUST běžet v produkci na jednom serveru s Ubuntu 24.04 ze souboru `deploy/docker-compose.prod.yml` se službami `caddy`, `web`, `api`, `worker`, `postgres`, `walg` a `monitor` a s jednorázovými `migrate` a `cms-migrate`. Port do internetu MUST publikovat jen `caddy` (80/tcp, 443/tcp, 443/udp). Databáze MUST být jen ve vnitřní síti `db` bez přístupu z internetu. Každý kontejner MUST mít kontrolu zdraví, limit paměti, `no-new-privileges`, `cap_drop: [ALL]` (kromě nutných výjimek) a neprivilegovaného uživatele.

#### Scenario: Start celé sestavy
- GIVEN čerstvě zřízený server se soubory v `/opt/eshopguard/env`
- WHEN se spustí `docker compose -f docker-compose.prod.yml up -d`
- THEN do 120 s jsou všechny služby `healthy`
- AND `docker compose ps --format json` ukazuje publikované porty jen u `caddy`

#### Scenario: Databáze není dostupná zvenku
- GIVEN běžící sestava
- WHEN se někdo z internetu nebo ze samotného hostitele pokusí připojit na port 5432
- THEN spojení se nenaváže (port nepublikovaný, síť `db` je `internal: true`)
- AND `api`, `worker`, `web`, `walg` a `monitor` se k databázi připojí po síti `db`

#### Scenario: Ukončení workeru při nasazení
- GIVEN worker zpracovává dávku s leasem 2 min
- WHEN `docker compose up -d` nahrazuje kontejner `worker`
- THEN worker dostane SIGTERM, přestane brát úlohy a dokončí dávku do `stop_grace_period` 150 s
- AND žádná úloha nezůstane ve stavu `running` s propadlým leasem bez vrácení do fronty

### Requirement: Provoz: Vstup přes Caddy s HTTPS a omezením cest
Systém MUST přijímat provoz z internetu jen přes Caddy:
- s automatickým certifikátem HTTPS, přesměrováním HTTP → HTTPS a hlavičkou HSTS;
- `/api/*` MUST směrovat na `api` a události SSE bez bufferu;
- `/admin` a `/cms-api` MUST pustit jen z adres `CMS_ADMIN_ALLOWED_CIDRS` a jen jim nastavit hlavičku `X-EG-Admin-Allowed: 1`, kterou od klienta MUST vždy odstranit;
- `/eg-internal/*` MUST zvenku vracet 404;
- `/status/api` MUST vracet jen 200 nebo 503 bez podrobností.

#### Scenario: Administrace z nepovolené adresy
- GIVEN `CMS_ADMIN_ALLOWED_CIDRS` neobsahuje adresu návštěvníka
- WHEN návštěvník otevře `https://<host>/admin` s vlastní hlavičkou `X-EG-Admin-Allowed: 1`
- THEN dostane 404
- AND hlavička od klienta se k `web` nedostane

#### Scenario: Živý průběh bez zpoždění
- GIVEN běh posílá události přes `/api/t/{t}/runs/{r}/events`
- WHEN API odešle událost
- THEN prohlížeč ji dostane do 1 s
- AND Caddy spojení neuzavře dřív než API

#### Scenario: Vnitřní obnova cache zvenku
- GIVEN útočník zná adresu `/eg-internal/revalidate`
- WHEN pošle `POST https://<host>/eg-internal/revalidate`
- THEN dostane 404 od Caddy
- AND požadavek se k `web` nedostane

#### Scenario: Veřejný stav API
- GIVEN databáze je nedostupná a `GET /health` vrací 503 s kódem `db.unreachable`
- WHEN externí služba zavolá `https://<host>/status/api`
- THEN dostane 503 bez těla odpovědi API
- AND kódy a názvy kontrol nejsou zvenku vidět

### Requirement: Provoz: Nasazení z GitHub Actions přes GHCR
Systém MUST nasazovat novou verzi jedním krokem:
- GitHub Actions sestaví a otestují obrazy, nahrají je do GHCR se značkou commitu;
- po ručním schválení v prostředí `production` spustí na serveru `deploy/scripts/deploy.sh <sha>`.

Na serveru MUST NOT probíhat ruční úpravy mimo skripty. Najednou MUST běžet nejvýš jedno nasazení. Nasazení MUST skončit chybou a ponechat běžet předchozí verzi, když chybí klíč v konfiguraci, když zálohy nejsou čerstvé nebo když selže migrace.

#### Scenario: Úspěšné nasazení
- GIVEN commit `abc1234` v `main` prošel CI
- WHEN správce schválí nasazení v prostředí `production`
- THEN server stáhne obrazy `…:abc1234`, provede migrace, spustí služby a do 120 s jsou zdravé
- AND `state/current_tag` je `abc1234` a `state/previous_tag` je předchozí značka

#### Scenario: Neúplná konfigurace
- GIVEN `worker.env` neobsahuje `OPENAI_API_KEY` ze šablony
- WHEN se spustí `deploy.sh`
- THEN skript skončí chybou `env.missing_key` s názvem souboru a klíče (bez hodnot) dřív, než stáhne obrazy
- AND běžící verze se nezmění

#### Scenario: Zálohy nejsou čerstvé
- GIVEN poslední archivovaný WAL je starší než 5 min, nebo poslední base backup starší než 26 h
- WHEN se spustí `deploy.sh`
- THEN skript skončí kódem `deploy.backup_stale` před migrací
- AND odejde upozornění a migrace se nespustí

#### Scenario: Souběžné nasazení
- GIVEN jedno nasazení právě běží
- WHEN se spustí druhé
- THEN druhé skončí chybou `deploy.locked` a nic nezmění

### Requirement: Provoz: Migrace před startem a vrácení verze
Systém MUST spustit migrace EF Core (`efbundle`) jako `eshopguard_owner` a migrace Payload jako `eshopguard_cms` před startem nové verze aplikace. Migrace MUST být rozšiřující. Odebrání nebo přejmenování sloupce či tabulky MUST mít značku `contract` a CI ji MUST bez značky odmítnout. Když nová verze nenaběhne do 120 s, MUST se aplikační kontejnery samy vrátit na předchozí značku. Databáze se nevrací.

#### Scenario: Nová verze nenaběhne
- GIVEN nová verze API po startu vrací na `/health` 503
- WHEN uplyne 120 s čekání na zdraví
- THEN `deploy.sh` spustí `web`, `api` a `worker` s předchozí značkou a ta je do 3 min zdravá
- AND odejde upozornění `deploy.rolled_back` a skript skončí nenulovým kódem

#### Scenario: Selhaná migrace
- GIVEN migrace nové verze selže na chybě SQL
- WHEN ji spustí `deploy.sh`
- THEN skript skončí chybou před spuštěním nové verze
- AND předchozí verze běží dál a transakce migrace je vrácená

#### Scenario: Migrace se špatnou rolí
- GIVEN `ConnectionStrings__Migrations` omylem obsahuje roli `eshopguard_app`
- WHEN se spustí kontejner `migrate`
- THEN migrace odmítne běžet s kódem `db.migrations_wrong_role` (kontrola `current_user`)
- AND nic se nezmění

#### Scenario: Odebírající migrace bez značky
- GIVEN PR přidává migraci s `migrationBuilder.DropColumn(...)` bez komentáře `// contract`
- WHEN běží `deploy/scripts/check-migrations.sh` v CI
- THEN kontrola selže s názvem souboru migrace
- AND PR nejde sloučit

### Requirement: Provoz: Tajné klíče mimo repozitář s nejmenšími právy
Systém MUST držet tajné klíče jen v `/opt/eshopguard/env/<služba>.env` na serveru (adresář `0700`, soubory `0600`, vlastník `deploy`) a v tajemstvích GitHubu. V repozitáři MUST být jen šablony `deploy/env/*.env.example` bez hodnot. Každý kontejner MUST dostat jen klíče, které potřebuje. Šifrovací klíč záloh a klíč Data Protection MUST mít šifrovanou zálohu mimo server.

#### Scenario: Web nemá klíče workeru
- GIVEN běžící sestava
- WHEN test spustí `docker inspect` kontejneru `web`
- THEN proměnné prostředí neobsahují `TYPESAFE_API_KEY`, `OPENAI_API_KEY`, `Stripe__SecretKey` ani `ConnectionStrings__App`
- AND `check-env.sh` by klíč navíc v `web.env` odmítl kódem `env.unexpected_key`

#### Scenario: Soubor čitelný pro ostatní
- GIVEN `api.env` má omylem práva `0644`
- WHEN se spustí `check-env.sh`
- THEN kontrola selže kódem `env.bad_permissions`
- AND nasazení nepokračuje

#### Scenario: Šablona s hodnotou
- GIVEN někdo do `deploy/env/api.env.example` vloží hodnotu `Stripe__SecretKey=sk_live_…`
- WHEN běží test hygieny tajemství (změna 2, rozšířený o `deploy/env/`)
- THEN test selže s cestou k souboru, bez vypsání hodnoty
- AND CI neprojde

### Requirement: Provoz: Zálohy WAL-G mimo poskytovatele
Systém MUST zálohovat PostgreSQL přes WAL-G:
- průběžně WAL (`archive_timeout` 60 s) a denně base backup ve 20:30 UTC do S3 u jiného poskytovatele než server;
- šifrovaně (libsodium) s uchováním 30 dní;
- pověření záloh MUST NOT umět mazat objekty a objekty MUST chránit object lock;
- po každém base backupu MUST vzniknout manifest počtů řádků a šifrovaná záloha konfigurace.

Automatické zálohy virtuálu od poskytovatele MUST být zapnuté jako druhá vrstva.

#### Scenario: Ztráta dat nejvýš minuty
- GIVEN aplikace zapíše do `ops.audit_log` řádek v čase T
- WHEN uplynou 2 minuty
- THEN WAL s tímto zápisem je v S3 (`wal-g wal-verify integrity` bez chyby)
- AND obnova k času T + 2 min řádek obsahuje

#### Scenario: Pokus o smazání zálohy ze serveru
- GIVEN útočník získal pověření z `walg.env`
- WHEN se pokusí smazat base backup v S3
- THEN poskytovatel S3 vrátí `AccessDenied`
- AND objekt zůstane chráněný object lock až do konce 30 dní

#### Scenario: Selhání archivace WAL
- GIVEN pověření S3 přestanou platit
- WHEN PostgreSQL nedokáže archivovat WAL
- THEN `pg_stat_archiver.failed_count` roste a `monitor` do 10 min pošle upozornění `backup.wal_stale`
- AND nasazení nové verze se do nápravy odmítne (`deploy.backup_stale`)

### Requirement: Provoz: Zkouška obnovy na čistý server do 2 hodin
Systém MUST mít skript `deploy/scripts/restore-drill.sh`, který obnoví databázi a konfiguraci z S3 na čistý server s Ubuntu 24.04 a změří čas každého kroku. Zkouška MUST běžet jednou měsíčně (`restore-drill.yml`) a MUST skončit do 2 h. Obnovená kopie MUST běžet v režimu zkoušky: worker zastavený, `DRILL_MODE=1`, bez klíčů Jevu, OpenAI, Stripe a SuperFaktúry, bez odesílání e-mailů a bez změny DNS.

#### Scenario: Úspěšná měsíční zkouška
- GIVEN čistý server a poslední base backup s manifestem
- WHEN se spustí `restore-drill.sh --target <host>`
- THEN do 2 h je `/health` zdravé, počty řádků odpovídají manifestu a poslední `ops.audit_log.at` je nejvýš 5 min před koncem zálohy
- AND výsledek JSON s časy kroků je jako artefakt workflow a v `s3://…/drills/`

#### Scenario: Obnovená kopie nic neodešle
- GIVEN obnovená databáze obsahuje neodeslané záznamy v `ops.outbox` a úlohy ve `ops.jobs`
- WHEN zkouška spustí API a web v režimu zkoušky
- THEN žádný e-mail neodejde, žádná úloha se nezpracuje a žádné volání Jevu, OpenAI, Stripe ani SuperFaktúry neproběhne
- AND `DrillModeGuard` zapíše do logu `drill.external_call_blocked` u každého pokusu

#### Scenario: Zkouška trvá déle než 2 h
- GIVEN obnova WAL trvá déle, než se čekalo
- WHEN celkový čas překročí 2 h
- THEN zkouška se dokončí, výsledek se označí `failed: duration` a odejde upozornění `drill.failed`
- AND výsledek obsahuje časy kroků, aby šlo najít pomalý krok

### Requirement: Provoz: Hlídání zdraví, fronty, chyb, disku, záloh a certifikátů
Systém MUST hlídat:
- dostupnost zvenku každou minutu (`/healthz`, `/status/api`);
- zdraví API, živé workery a čekání úloh P0 (nejvýš 30 s);
- dokončení nočních běhů do 6:00 (Europe/Bratislava);
- podíl chyb 5xx, restarty kontejnerů, volné místo na disku;
- čerstvost archivace WAL a base backupu, platnost certifikátu a potřebu restartu systému.

`GET /health` (změna 2) MUST doplnit kontroly `queue` a `workers`. Upozornění MUST odejít e-mailem bez dat zákazníků, s odstraněním duplicit a s oznámením o vyřešení. Monitor sám MUST posílat signál „mrtvého muže“ externí službě.

#### Scenario: Zastavené API
- GIVEN běžící sestava
- WHEN se kontejner `api` zastaví
- THEN externí služba do 2 min ohlásí nedostupnost `/status/api` a `monitor` pošle `health.api_failed`
- AND po opětovném spuštění odejde jedno oznámení „vyřešeno“

#### Scenario: Úloha P0 čeká příliš dlouho
- GIVEN fronta je plná úloh P2 a úloha P0 čeká 31 s
- WHEN proběhne kontrola fronty
- THEN `/health` hlásí kontrolu `queue` s kódem `queue.p0_stale` a `monitor` pošle upozornění
- AND upozornění obsahuje jen kód, třídu priority a čas čekání

#### Scenario: Zaplněný disk
- GIVEN volné místo na disku klesne pod 10 %
- WHEN proběhne kontrola disku
- THEN odejde upozornění `disk.critical`
- AND další stejné upozornění odejde nejdřív za hodinu, pokud stav trvá

#### Scenario: Monitor nežije
- GIVEN kontejner `monitor` spadl
- WHEN 15 min nepřijde jeho signál
- THEN externí služba „mrtvého muže“ pošle upozornění
- AND výpadek hlídání tak nezůstane bez povšimnutí

### Requirement: Provoz: Logy bez osobních údajů a klíčů
Systém MUST vést logy kontejnerů s rotací (`local`, 20 MB × 10 souborů na kontejner). Logy Caddy MUST maskovat IP adresy (IPv4 na /24, IPv6 na /48), mazat parametry `token`, `session_id`, `code` a `state` z adresy a nezapisovat hlavičky `Cookie`, `Authorization` a `Set-Cookie`. Kontrola `deploy/scripts/log-scan.sh` MUST v CI (nad logy testů) i denně na serveru hledat e-maily, klíče (`sk_live_`, `sk_test_`, `whsec_`, `sk-…`), tokeny `Bearer`, JWT a nemaskované IP. Při nálezu MUST selhat nebo upozornit bez vypsání nalezené hodnoty.

#### Scenario: Odkaz na přihlášení v logu
- GIVEN uživatel otevře `https://<host>/app/login/confirm?token=AbC123`
- WHEN Caddy zapíše přístupový log
- THEN záznam obsahuje cestu `/app/login/confirm` bez parametru `token` a IP s posledním oktetem `0`
- AND `log-scan.sh` nad tímto logem nic nenajde

#### Scenario: E-mail v logu aplikace
- GIVEN chyba v kódu zapíše do logu `jana@bylinkovo.sk`
- WHEN běží `log-scan.sh` v CI nad logy integračních testů
- THEN kontrola selže se jménem vzoru `email`, kontejnerem a číslem řádku
- AND samotná adresa se ve výstupu kontroly neobjeví

#### Scenario: Povolená výjimka
- GIVEN log obsahuje e-mail provozu `ops@eshopguard.sk` ze seznamu `log-scan.allowlist`
- WHEN běží `log-scan.sh`
- THEN tento výskyt se nehlásí
- AND jiné e-maily se hlásí dál

### Requirement: Provoz: Zabezpečení serveru
Systém MUST zřídit server skriptem `deploy/server/provision.sh`:
- SSH jen klíčem, bez přihlášení roota a bez hesel, jen uživatelé `deploy` a `ops`;
- firewall `ufw` s povolenými 80/tcp, 443/tcp a 443/udp a SSH jen z povolených adres (nebo jen přes rozhraní VPN podle rozhodnutí);
- pravidla `DOCKER-USER`, aby Docker neobešel firewall;
- automatické bezpečnostní aktualizace bez automatického restartu.

Skript `verify-server.sh` MUST stav ověřit.

#### Scenario: Přihlášení heslem
- GIVEN zřízený server
- WHEN se někdo pokusí přihlásit přes SSH heslem jako `root` nebo `deploy`
- THEN server přihlášení odmítne (`Permission denied (publickey)`)
- AND `sshd -T` ukazuje `passwordauthentication no` a `permitrootlogin no`

#### Scenario: Sken portů zvenku
- GIVEN zřízený server s běžící sestavou
- WHEN se spustí sken všech portů TCP z adresy mimo povolené
- THEN otevřené jsou jen 80 a 443
- AND port 22 je filtrovaný a port 5432 zavřený

#### Scenario: Kontejner s omylem publikovaným portem
- GIVEN někdo do Compose přidá `ports: ["8080:8080"]` u `api`
- WHEN se spustí `verify-server.sh` a sken zvenku
- THEN `verify-server.sh` selže s názvem služby a portem
- AND pravidla `DOCKER-USER` spojení z internetu na 8080 zahodí

### Requirement: Provoz: Zátěžový test souběhu a tisíců e-shopů
Systém MUST mít zátěžový test `tests/EshopGuard.LoadTests` proti falešným e-shopům a falešnému Jevu a OpenAI (limit 1 200 požadavků/min, odpovědi 429). Test MUST obsahovat scénáře:
- 50 souběžných úvodních analýz;
- noční sledování 3 000 simulovaných e-shopů;
- čekání úloh P0 při plné frontě;
- 200 souběžných uživatelů API.

Test MUST odmítnout běh, když je nastavený reálný klíč Jevu nebo OpenAI, nebo když adresy služeb nemíří na falešné servery. Výsledek MUST porovnat naměřené hodnoty s architekturou (části 5 a 8).

#### Scenario: Souběžné analýzy bez ztrát a zdvojení
- GIVEN dočasný server stejného typu jako produkce a 50 falešných e-shopů po 500 stránkách
- WHEN se spustí scénář S1 se 2 instancemi workeru
- THEN všech 50 běhů skončí ve stavu `finished`
- AND kontrola jedinečných klíčů výsledků (běh, stránka; tenant, věta, verze otázek) nenajde žádné zdvojení a žádná úloha nezůstane ztracená

#### Scenario: P0 při plné frontě
- GIVEN během scénáře S2 je ve frontě přes 10 000 úloh P2 a P3
- WHEN scénář S3 zakládá každých 10 s úlohu P0
- THEN čekání úloh P0 je v p99 nejvýš 30 s
- AND překročení se ve zprávě označí jako selhání testu

#### Scenario: Reálný klíč v prostředí
- GIVEN proměnná `OPENAI_API_KEY` je nastavená
- WHEN se spustí `dotnet run --project tests/EshopGuard.LoadTests`
- THEN test skončí před prvním požadavkem s kódem `loadtest.real_key_present`
- AND žádné volání mimo falešné služby neproběhne

#### Scenario: Zpráva o kapacitě
- GIVEN scénář S2 doběhl s faktorem `--scale 0.1`
- WHEN se vytvoří zpráva
- THEN obsahuje naměřené CPU s na stránku, stránky/s, převzetí úloh/s, podíl výkonu na tenanta a přepočet na plný rozsah
- AND porovnání s architekturou (0,25–0,28 s CPU na stránku, ~120 e-shopů za noc při 1 200 požadavcích/min) uvede rozdíl, i když je nepříznivý
