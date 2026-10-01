# Proposal: Nálezy, opravy, doklady a protokol v API

## Intent

**Problém.** Po ukázce a úvodní analýze (změna 8) leží v databázi:
- nálezy s verdikty po zemích, výskyty na stránkách, otázky;
- návrhy oprav s variantami a hromadné opravy.

Obchodník s nimi ale nemůže nic dělat. Obrazovky, které jsou jádrem produktu, nemají data ani akce:
- Přehled (`Dashboard.dc.html`, `Mobile.dc.html`): souhrn, „Stránky, ktoré riešiť najskôr“, „Rýchle odpovede“;
- Opravy (`Fixes.dc.html`): záložky, filtr jazykové verze, „Podľa stránok / Podľa nálezov“, hromadné opravy;
- Nálezy (`Findings.dc.html`);
- Oprava stránky (`Review.dc.html`, `ReviewQuestion.dc.html`): změny v kontextu, verdikt SK/CZ, varianty, Prijať/Upraviť/Zamietnuť, otázka Áno/Nie, „Publikovať do e-shopu“, „Kopírovať text“;
- Hromadná oprava (`GroupFix.dc.html`);
- Doklady (`Evidence.dc.html`);
- Protokol (`Protocol.dc.html`);
- upozornění v sekci „Upozornenia“ obrazovky Sledovanie (`Monitoring.dc.html`) a zvoneček v mobilu.

**Proč teď.** F5 (README plánu: změna 11 závisí na 8 a 9). Je to poslední API před frontendem (změna 13) a konektorem (změna 15), který bude publikovat schválené opravy.

**Přínos.**
- Obchodník vidí, co řešit nejdřív: stránky seřazené podle nejpřísnějšího verdiktu, u každého nálezu verdikt po zemích („SK · Porušenie, CZ · Na posúdenie“).
- Opravy schvaluje rychle:
  - jedno rozhodnutí místo 36 u opakovaného textu;
  - varianty „S upresnením / Bez environmentálneho slova“;
  - vlastní úprava, kterou Jev znovu zkontroluje.

  Návrh, který neprojde kontrolou ve všech zemích, nejde přijmout.
- Odpověď „Áno / Nie“ zadaná jednou platí pro všechny stránky se stejným textem a uloží se mezi doklady. Doklad platí pro všechny e-shopy účtu a před koncem platnosti přijde připomenutí.
- Protokol PDF s číslem EG-RRRR-NNNN zaznamená péči obchodníka:
  - verze pravidel a země;
  - rozhodnutí a opravy;
  - doklady;
  - co zbývá rozhodnout.
- Rozhodnutí se zapisují do paměti rozhodnutí (`fixes.decision_memory`). Sledování (změna 16) pak stejný text vyřeší bez modelu.
- Průběh běhů živě přes SSE, upozornění v aplikaci a e-mailem podle nastavení.

**Fáze:** F5 API (`databaze-a-plan-implementace-2026-10-01.md`, část 8: „nálezy a opravy (stránky, změny, varianty, otázky, hromadné opravy, šablona), doklady, protokol PDF, sledování, upozornění“).

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 3.4 `checks`: `runs`, `run_events`, `findings` (stavy, `verdicts`), `finding_occurrences`, `questions`;
  - část 3.5 `fixes`: `fix_groups`, `fix_proposals`, `publications`, `decision_memory`, `evidence_items`, `evidence_links`, `protocols`;
  - část 3.1: `notifications`, `notification_settings`;
  - část 3.3: `pages`, `page_versions` (otisky vět, soubor extrakce).
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 3: izolace, soubory přes podepsané odkazy;
  - část 4: P0 přegenerování a kontrola;
  - část 5: publikování přes konektor, konflikt, klíč idempotence;
  - část 6: průběh přes SSE s `LISTEN/NOTIFY`;
  - část 12: verdikt po zemích, řazení podle nejpřísnějšího, návrh opravy musí projít ve všech zemích, protokol uvádí země, API vrací kódy, backend skládá jen e-maily a PDF, texty nálezů z pravidel.
- `navrhy-rozvoje-2026-09-30.md`:
  - bod 1: paměť rozhodnutí, hromadné opravy;
  - 1b: dvě varianty;
  - 2: text v kontextu;
  - 3: předgenerovaná varianta pro „Nie“;
  - 4: doklad zadaný jednou;
  - 5: „Čo pomôže“;
  - 6: protokol;
  - 7: znění otázky na doklad.
- Návrh UI: `Dashboard`, `Fixes`, `Findings`, `Review`, `ReviewQuestion`, `GroupFix`, `Evidence`, `Protocol`, `Monitoring` (Upozornenia), `Mobile`, `Sidebar` (počty v menu), `Verify` (skupiny otázek).

## Scope

In scope:
- **Přehled e-shopu:**
  - poslední kontrola, počty nálezů podle skupiny, počty stránek po záložkách;
  - stránky k řešení nejdřív, rychlé odpovědi, počty pro boční menu.

  Data sledování doplní změna 16.
- **Pohled po stránkách:**
  - záložky Na riešenie / Na schválenie / Potrebujeme vašu odpoveď / Publikované;
  - položky „Celý e-shop: šablóna“ a „Celý e-shop: košík a objednávka“;
  - filtr jazykové verze;
  - řazení podle nejpřísnějšího verdiktu.
- **Pohled po nálezech:**
  - záložky Porušenia / Na posúdenie / Na overenie / Opravené;
  - filtry témat (modul), stavu, jazykové verze a země;
  - seskupení podle stránky;
  - export CSV v jazyce uživatele.
- **Hledání** (Ctrl K) ve stránkách a textech nálezů e-shopu.
- **Verdikty po zemích a texty pravidel:**
  - `verdicts` z nálezu, nejpřísnější verdikt;
  - katalog textů pravidel `GET /api/catalog/rule-texts` (názvy, vysvětlení po jurisdikcích, „Čo pomôže“, otázky) v jazyce uživatele;
  - odkazy na zákon v jazyce zákona.
- **Stavy nálezu** `open`, `needs_answer`, `proposed`, `approved`, `published`, `kept`, `kept_with_evidence`, `dismissed`, `resolved`:
  - stavový automat s povolenými přechody;
  - rozhodnutí „Ponechať“, „Nejde o problém“, „Znovu otvoriť“;
  - zápis do `fixes.decision_memory`.
- **Oprava stránky:**
  - změny v kontextu (okolní text ze souboru extrakce), varianty, údaje k doplnění („Obal: [materiál obalu]“);
  - odkaz na hromadnou změnu, otázky;
  - „Zmena 3 z 5“, další a předchozí stránka ve frontě, počet odstavců beze změny;
  - kód „co pomůže“ z pravidla.
- **Návrhy oprav:**
  - výběr varianty, vlastní úprava, doplnění údaje;
  - přijetí jen po kontrole Jevem ve všech aktivních zemích;
  - zamítnutí, vrácení přijetí;
  - kontrola jako úloha P0 se stavem `pending` / `ok` / `still_finding`;
  - souběžnost přes `If-Match`.
- **Otázky Áno/Nie** (u nálezu i za celý web):
  - odpověď platí pro všechny otázky se stejným kódem a stejným textem v tenantovi;
  - „Áno“ → doklad a `kept_with_evidence`;
  - „Nie“ → připravená varianta, jinak úloha na návrh v rozpočtu tenanta;
  - změna odpovědi do publikace.
- **Doklady:**
  - seznam se stavy a počty, přidání se souborem (PDF, JPG, PNG) do úložiště tenanta;
  - úprava, smazání, vazby na nálezy;
  - stažení přes krátkodobý podepsaný odkaz;
  - stav `valid` / `expiring` / `expired` / `awaiting_answer` / `claim_removed`;
  - denní úloha připomenutí a vypršení (vypršený nebo smazaný doklad znovu otevře nálezy).
- **Hromadné opravy:**
  - seznam a detail (vzorky v kontextu, kontrola sedí / řešit jednotlivě, stránky);
  - údaj, režim (nahradit / odstranit / vlastní znění), vyřazení stránek;
  - schválení na všech vybraných stránkách, „Len na tejto stránke“, vrácení schválení.
- **Publikace:**
  - požadavek na publikování schválených oprav (záznam `publications` s klíčem idempotence a úloha `publish.fix`) přes rozhraní `IFixPublisher`, implementace ve změně 15;
  - stav a vrácení;
  - sestavený text pole pro „Kopírovať text“.
- **Protokol PDF:**
  - číslo EG-RRRR-NNNN v řadě po tenantovi a roce, jazyk protokolu;
  - obsah podle návrhu (souhrn, rozhodnutí a opravy, doklady, verze pravidel, země, co nebylo zkontrolováno, prohlášení);
  - vykreslení úlohou ve workeru, stažení podepsaným odkazem.
- **Upozornění:**
  - seznam, přečtení, přečtení všech, nastavení e-mailů po e-shopech;
  - `NotificationDispatcher` pro ostatní změny (řádek pro každého člena, e-mail přes `ops.outbox` podle nastavení);
  - šablony e-mailů pro doklady, protokol a publikování.
- **Běhy a průběh:**
  - seznam a detail běhů, zrušení (jen ukázka zdarma a opakovaná kontrola);
  - SSE pro běh (`GET …/runs/{runId}/events`, obnovení přes `Last-Event-ID`) a pro e-shop (stav návrhů a publikací);
  - `LISTEN/NOTIFY` přes jedno spojení na instanci API.
- **OpenAPI, ProblemDetails s kódy, testy oprávnění rolí a izolace tenantů přes API** (konvence změny 9).

Out of scope:
- vznik nálezů, otázek, návrhů oprav a jejich variant (změna 8);
- sestavení `fix_groups` a kontrola „oprava sedí na stránce“ (K rozhodnutí 1);
- zápis do e-shopu, kontrola konfliktu a vrácení v platformě (změna 15; zde jen rozhraní a fronta);
- tabulka „Posledné zmeny“, týdenní souhrn, noční sledování, automatické zveřejnění z paměti rozhodnutí (změna 16);
- „Nová kontrola“ (opakovaný běh) na Přehledu (K rozhodnutí 13);
- ověření dokladu ve veřejném registru (katalog EU Ecolabel; dostupnost neověřena, návrh rozvoje 15);
- fakta obchodu pro návrhy (`shop.shop_facts`, návrh rozvoje 1a);
- protokol s logem partnera;
- obrazovky (změna 13).

## Approach

1. **API tenké, logika v `EshopGuard.Application`:**
   - čtení: `FindingQueryService`, `PageWorkQueryService`, `PageReviewService`, `OverviewService`, `SearchService`;
   - rozhodnutí: `FindingDecisionService` + `FindingStatusMachine` (čistá funkce);
   - opravy: `FixProposalService`, `FixGroupService`, `QuestionService`, `EvidenceService`;
   - publikace a protokoly: `PublicationService`, `FixedTextComposer`, `ProtocolService`, `ProtocolNumberAllocator`, `ProtocolDocumentBuilder`;
   - upozornění: `NotificationService`, `NotificationDispatcher`;
   - běhy: `RunQueryService`, `RunEventStream`, `RuleTextCatalog`.

   Úlohy (kontrola textu, návrh po „Nie“, vykreslení PDF, připomenutí dokladů) obsluhuje `EshopGuard.Jobs`.
2. **API vrací kódy a parametry, ne věty.** Vysvětlení, „Čo pomôže“ a znění otázek skládá frontend z katalogu textů pravidel podle `ruleSetId`, `ruleId`, jurisdikce a jazyka uživatele. Odkazy na zákon zůstávají v jazyce zákona. Backend skládá jen PDF protokolu, export CSV a e-maily.
3. **Fail-closed:**
   - přijmout jde jen návrh s `recheck_status = ok` ve všech aktivních zemích e-shopu a bez nevyplněného údaje;
   - publikovat jde jen přes připojený konektor s implementací `IFixPublisher`, jinak `409` a nic se nezapíše;
   - vypršený nebo smazaný doklad vrátí nálezy do `open`;
   - nepovolený přechod stavu → `409 finding.transition_not_allowed`;
   - protokol uvádí i nezkontrolované stránky a nálezy čekající na rozhodnutí.
4. **Nic se tiše neslučuje.** Odpověď a hromadná oprava ukážou, na kolik nálezů a stránek dopadnou (`appliesTo`), ještě než se potvrdí. Stránky, kde hromadná oprava nesedí, zůstávají k jednotlivému řešení.
5. **Souběžnost a idempotence:**
   - úpravy návrhů, skupin a dokladů přes `If-Match` (token `xmin`);
   - publikace s jedinečným `idempotency_key`;
   - čísla protokolů pod zámkem `pg_advisory_xact_lock` po tenantovi a roce.
6. **Živý průběh:**
   - spouštěč na `checks.run_events` a změnách stavu volá `pg_notify` s `tenant_id:run_id:event_id`, bez textů;
   - API drží jedno naslouchající spojení na instanci a řádky čte až pod RLS konkrétního požadavku.
7. **Placená volání jen v rozpočtu.** Kontrola textu (Jev) je úloha P0 s vyhrazeným podílem limitu. Návrh po odpovědi „Nie“ (OpenAI) se generuje jen tehdy, když připravená varianta chybí, a jen do denního rozpočtu tenanta (`Fixes:DailyGenerationsPerTenant`). Nad ním `429 budget.daily_limit_reached` (fail-closed).

## Dependencies

- **9 `add-identity-and-tenants-api`:** `/api/t/{tenantId}`, role (čtení viewer, rozhodnutí a publikace editor), CSRF, ProblemDetails, OpenAPI, audit, e-maily přes `ops.outbox`, `EshopGuard.Application`.
- **8 `add-analysis-runs-in-worker`:**
  - zapsané `findings` (s `verdicts`), `finding_occurrences`, `questions`, `fix_proposals` (s `alternatives`, `placeholders`, `recheck_status`);
  - `run_events`, `runs.progress`;
  - soubory extrakce stránek v úložišti.
- **6 `add-multi-jurisdiction-rules-and-rule-texts`:** `rule_sets.texts` po jazycích, `verdicts` po zemích, pořadí přísnosti verdiktů (`VerdictStrictness`), kontrola textu pro víc jurisdikcí nad společnými odpověďmi Jevu.
- **5 `refactor-library-into-pipeline-steps`:** `AnalyzeTextsAsync` pro kontrolu jednoho textu, cache Jevu po tenantovi.
- **4 `add-job-queue-and-worker`:** úlohy P0/P1, `concurrency_key`, globální limity Jevu a OpenAI.
- **3 `add-multitenant-data-model`:** schémata `checks`, `fixes`, `iam` (upozornění), RLS.
- **2 `add-solution-foundation`:** `IBlobStore` (S3/MinIO) s podepsanými odkazy.
- **Navazují:**
  - 13 (obrazovky);
  - 15 (implementace `IFixPublisher`, `publish.fix`, `publish.rollback`, nastaví `published`);
  - 16 (paměť rozhodnutí při sledování, `resolved`, „Posledné zmeny“, `new_violation`, týdenní souhrn).

## Done when

- **Testy služeb a stavů** (`tests/EshopGuard.Application.Tests/Findings/*`, `Fixes/*`):
  - `FindingStatusMachineTests` pokryje všechny dvojice stavů;
  - `ProtocolNumberAllocatorTests`: 20 souběžných požadavků dá 20 různých po sobě jdoucích čísel;
  - `PageTabsTests` sedí s počty z návrhu UI nad testovacími daty „bylinkovo.sk“: 28 stránek s nálezem, 24 / 12 / 12 / 4.
- **Testy API** (`tests/EshopGuard.Api.Tests/Findings/*`):
  - celý tok stránky „Zubná pasta + bambusová kefka“: změny, varianta, úprava a kontrola (`MockJevClient`), přijetí, hromadná oprava na 36 stránkách, publikace s `FakeFixPublisher`;
  - otázka „Sviečka Vodnár“ s odpovědí platnou pro 4 stránky a dokladem;
  - protokol PDF vygenerovaný a stažený.
- **SSE:** test přijme události běhu ve správném pořadí, po odpojení pokračuje od `Last-Event-ID` a události cizího tenanta nedostane.
- **Matice rolí a izolace přes API:** všechny koncové body této změny pro 4 role, dva tenanti se stejnou doménou a se stejným textem nálezu. Odpověď v jednom tenantovi nezmění otázky druhého.
- **Bez placených volání v testech:** `MockJevClient`, `MockRewriteClient`. Živá kontrola jen jako samostatný úkol s odhadem ceny a souhlasem.
- **Validace:** `openspec validate add-findings-and-fixes-api` projde.

## K rozhodnutí

1. **Kdo staví hromadné opravy** (`fixes.fix_groups`, kontrola „oprava sedí na 36 stránkách, 2 řešit jednotlivě“). Změna 8 to nechává otevřené (její K rozhodnutí 9, návrh samostatné změny mezi 8 a 11). Tato změna je jen čte a mění jejich stav. Bez nich zůstane obrazovka GroupFix prázdná a opakovaný text bude mít návrh jen na první stránce.
2. **Pořadí přísnosti verdiktů.** Architektura říká „řadí se podle nejpřísnějšího“, pořadí ale nedefinuje. Návrh:
   - skupina: porušení (`text`) > na posúdenie (`assess`) > na overenie (`verify`);
   - pak závažnost (`high` > `medium` > `low`);
   - pak pásmo (`high` > `review`).

   Pokud pořadí definuje změna 6, převezme se odtud. Potvrdit hlavně `assess` × `verify`.
3. **`dismissed` × `kept`.** Datový model má oba stavy bez popisu. Návrh:
   - `kept` = „Ponechať, veta nič nesľubuje“ (rozhodnutí obchodníka, jde do protokolu);
   - `dismissed` = „Nejde o problém“ (chyba nástroje, kód důvodu `false_positive`; jde do protokolu a slouží k měření přesnosti).

   Má mít obchodník možnost označit nález za chybný?
4. **Co z ukázky zdarma uvidí obchodník v Opravách** (stejné jako K rozhodnutí 3 změny 10). Do rozhodnutí vrací koncové body této změny u e-shopu ve stavu `sample` jen nálezy ze souhrnu ukázky (5 nejzávažnějších) a ostatní nálezy jako počty.
5. **Výchozí jazyk protokolu.** Architektura: „návrh: výchozí je jazyk trhu e-shopu“. Změna 13 počítá s jazykem domovské země e-shopu. Tato změna bere `locale` z požadavku, jinak výchozí jazyk trhu `shops.home_country`.
6. **Knihovna PDF.** QuestPDF (licence Community jen do 1 mil. USD obratu ročně, nad ní placená) nebo PdfSharp/MigraDoc (MIT). Licenci ověřit před volbou. Návrh počítá s rozhraním `IPdfRenderer`.
7. **Výchozí nastavení upozornění.** Obrazovka Sledovanie ukazuje přepínače bez výchozích hodnot. Návrh: e-mail při novém porušení zapnutý, týdenní souhrn zapnutý, konec běhu zapnutý (`Notifications:Defaults`).
8. **Nesrovnalosti s datovým modelem (změna 3):**
   - `notifications.user_id = NULL` („všem v účtu“) má jediné `read_at`, takže přečtení jedním uživatelem by skrylo upozornění ostatním. Návrh: `NotificationDispatcher` zakládá řádek pro každého člena a `user_id` je vždy vyplněné.
   - `protocols` nemá stav vykreslení ani chybu. Návrh: sloupce `status` (`rendering` / `ready` / `failed`) a `error_code` (migrace této změny).
   - `fix_proposals` má jen `recheck_status`, ne v čem kontrola neprošla. Návrh: sloupec `recheck_result jsonb` (země, pravidla, čas).
   - Jedinečnost `protocols` (`tenant_id`, `number`) v modelu chybí. Doplní ji migrace této změny.
9. **Rozsah platnosti odpovědi.** Návrh UI: „Odpoveď platí pre všetky 4 sviečky Vodnár s rovnakým údajom“. Obrazovka Doklady: „platí pre všetky produkty s rovnakým tvrdením“. Datový model: doklady platí pro všechny e-shopy tenanta. Návrh: odpověď se přenese na všechny otevřené otázky se stejným kódem a stejným otiskem textu ve všech e-shopech tenanta. Otázky za celý web (košík, tlačítko odstoupení) jen v rámci e-shopu. Odpověď ukáže `appliesTo` (počet nálezů a stránek) před potvrzením.
10. **Odkud je text po odpovědi „Nie“ u otázek za celý web** (harmonizované oznámení, tlačítko „odstúpiť od zmluvy tu“). Návrh: pevný text z pravidel (změna 6) v jazyce e-shopu, ne z LLM. Kde text pravidlo nemá, vznikne úloha na návrh v rozpočtu tenanta.
11. **Denní rozpočet návrhů na tenanta** (`Fixes:DailyGenerationsPerTenant`, návrh 50). Neměřeno. Přepis stojí ~1 cent za stránku (návrh rozvoje 1a).
12. **Soubory dokladů:**
    - povolené typy (návrh PDF, JPG, PNG) a velikost (návrh 20 MB);
    - zda soubory kontrolovat antivirem. V podkladech není.
    - Stav „Končí o N dní“: návrh `expiring` 30 dní před `valid_until` (`Evidence:ExpiringDays`).
13. **„Nová kontrola“ na Přehledu** (stejné jako K rozhodnutí 6 změny 13): spouští opakovaný běh? Je v předplatném zdarma? Tato změna koncový bod nezakládá.
14. **Zrušení běhu.** Tato změna dovolí zrušit jen ukázku zdarma a opakovanou kontrolu. Zrušení zaplacené úvodní analýzy souvisí s vrácením peněz (změna 12, její K rozhodnutí 11).
15. **Upozornění ve změně 9, nebo 11.** Změna 13 čeká `notifications` a `notification_settings` ve změně 9. Zadání plánu je dává do změny 11 a tato změna se jím řídí.
16. **E-maily o bězích a nastavení `email_run_finished`.** Změna 8 zapisuje e-maily o konci běhu přímo do `ops.outbox`, takže nastavení `email_run_finished` neuplatní. Návrh: po této změně ať změna 8 volá `NotificationDispatcher`.
17. **Publikováno u e-shopu bez konektoru.** Po „Kopírovať text“ se nález neoznačí jako publikovaný. Návrh: zůstane `approved` a na `resolved` ho přepne až další kontrola (změna 16), aby se nehlásilo něco, co v e-shopu není.
