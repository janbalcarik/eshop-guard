# Otevřená rozhodnutí z implementačního plánu (1. 10. 2026)

Každá změna má v `proposal.md` oddíl „K rozhodnutí“, celkem asi 320 bodů. Většina je technická. Zde jsou roztříděné:
- **A** rozhoduje uživatel;
- **B** jsou technické výchozí volby, které platí, pokud uživatel nenamítne;
- **C** jsou mezery v plánu;
- **D** se ověří v dokumentaci během práce.

## Rozhodnuto

- **Měna českého trhu (A2, 1. 10. 2026, uživatel):** Kč. `ref.markets` má od změny 3 řádky `sk` (EUR) a `cz` (CZK).
- **Index GIN nad `content.page_versions.segment_hashes` zrušen (1. 10. 2026, uživatel, změna 3):** pod RLS ho PostgreSQL nepoužije (`@>` není `LEAKPROOF`), hledání věty v e-shopu s 20 000 stránkami i bez něj 9–10 ms; ušetří zápisy. Dopad: změna 8 (`PgPageStore.FindByFingerprintAsync` hledá v části e-shopu bez GIN) a změna 16 (příznak rámce webu se počítá stejně, bez GIN); při jejich implementaci upravit design.
- **Změna 3, body K rozhodnutí (1. 10. 2026, uživatel):** přijaty návrhy; `iam.tenants` a `iam.users` zatím bez RLS (politika podle uživatele ve změně 9), audit a interní náklady přežijí smazání tenanta (bez cizího klíče na tenanta).
- **Změna 4, body K rozhodnutí (1. 10. 2026, uživatel):** přijaty návrhy:
  - sloty a stropy tenantů podle designu;
  - úklid hotových a zrušených úloh po 7 dnech, neúspěšných po 30;
  - pozastavený druh úloh se obnovuje jen ručně (`ResumeResourceClassAsync`);
  - doména jako `concurrency_key`;
  - testy fronty v samostatné databázi `eshopguard_test_jobs`;
  - zrušit jde jen čekající úlohu (běžící doběhne nebo skončí přes zrušení běhu).

  Limity OpenAI podle gpt-6.1-sol, Tier 4 (10 000 požadavků a 4 000 000 tokenů za minutu, ověřeno na stránce modelu) jsou od migrace F2 v bucketech `openai` a `openai:tokens`, takže chybějící bucket OpenAI ve změně 5 nenastane. Odchylky od designu s měřením jsou v `changes/add-job-queue-and-worker/design.md`, oddíl „Odchylky při implementaci“. Dopad na změnu 17: zátěžový test ověří převzetí, když jsou na začátku fronty úlohy tenantů nad stropem (dnes asi 30 ms na 20 000 přeskočených úloh).
- **Změna 6, body K rozhodnutí (1. 10. 2026, uživatel):** přijaty návrhy z oddílu K rozhodnutí. Otevřené zůstává K13 (znění otázek pro uživatele schválí uživatel).
- **Změna 7, body K rozhodnutí (2. 10. 2026, uživatel):** „pokračuj důsledně k cíli tak, jak je navrženo“: přijaty návrhy z oddílu K rozhodnutí (K1–K13). Zadání a schémata modelu pro místa prodeje a jazyk textu napíše Claude podle designu, protože výzkumné skripty (`research/langprobe-2026-10-01/`) jsou jen lokálně; výsledky se ověří placenými kroky 7.3 a 7.4. Údaje trhů jsou podle CLAUDE.md řádky `config/jurisdictions.yaml`, ne samostatný `config/markets.yaml`.
- **Změna 8, body K rozhodnutí (2. 10. 2026, uživatel „pokračuj“):** implementace se řídí návrhy z oddílu K rozhodnutí:
  - K1: běh se kvůli nákladům nezastavuje (architektura část 12), jen varování provozu;
  - K2: strop ukázky zdarma `Runs:FreeSample:MaxInternalUsd` = 1,00 USD, nad ním `failed` `sample_budget_exceeded` (hodnotu potvrdí uživatel);
  - K3: `findings.segment_hash` = otisk věty bez kontextu, stejná věta = jeden nález s výskyty (A14);
  - K4: tabulky `checks.run_urls` a `checks.run_scopes` (migrace F4);
  - K5: jedinečné indexy a hodnoty `market_analysis`, `version_language`; shoda ceny se ověřuje přes součet tokenů;
  - K6: nárok jen na hlavní doménu, po `failed` naší chybou zůstává (uvolní ho provoz ručně);
  - K7: hranice `partial` podle návrhu (i text vykreslený JavaScriptem);
  - K8: `Runs:SiteOutageMaxHours` = 24;
  - K10: úvodní analýza čeká na platbu po zjištění rozsahu;
  - K14: `run_events.message` se nevyplňuje.

  Odchylky od designu jsou v `changes/add-analysis-runs-in-worker/design.md`, oddíl „Odchylky při implementaci“ (mimo jiné verze se stejnými adresami přes cookie se zatím nestahují a běh je vyjmenuje). Otevřené zůstává: K9 (hromadné opravy), K11 (placené živé ověření na vegis.sk, odhad a souhlas), K15 (kroky `connector_check`).
- **Změna 9, body K rozhodnutí (2. 10. 2026, uživatel „Ano souhlasím a pokračuj na 9“):** implementace se řídí návrhy z oddílu K rozhodnutí v `changes/add-identity-and-tenants-api/proposal.md`:
  - K2 matice rolí podle `design.md`; K3 heslo aspoň 10 znaků, zablokování 5 chyb na 15 minut, relace 30 dní, nové ověření 15 minut, obnova hesla 60 minut, pozvánka 7 dní;
  - K4 nový účet bez čekající pozvánky dostane tenanta pojmenovaného e-mailem; K7 pozvánka zároveň přihlásí; K8 IP v auditu jen jako HMAC; K10 pozastavený tenant `403 tenant.suspended` kromě čtení účtu;
  - K11 s odchylkou: místo funkcí `SECURITY DEFINER` politiky platné jen v transakci uživatele bez tenanta (design, oddíl „Odchylky při implementaci“).

  Otevřené zůstává: K1 (služba odesílání e-mailů, doména a SPF, DKIM, DMARC; kód posílá přes SMTP), K5 (návrh obrazovky přepínače účtů), K6 (změna e-mailu účtu), K9 (kde se vedou verze podmínek; `Legal:TermsVersion` a `Legal:PrivacyVersion` jsou zatím `0`), K12 (změna 11), K13 (změna 17). Texty e-mailů kromě odkazu na přihlášení (2b) nemají schválený návrh a čekají na schválení.
- **Změna 10, body K rozhodnutí (2. 10. 2026, uživatel „Pokračuj tedy z bodem 10“, výchozí volby se zkontrolují hromadně na konci):** implementace se řídí návrhy z oddílu K rozhodnutí v `changes/add-shops-and-onboarding-api/proposal.md`:
  - K2 změna 10 počítá rozsah a `scopeHash`, cenu dá `IPriceQuoteService` změny 12 (bez ní `503 billing.unavailable`); K4 moduly cenu nemění; K5 bez ukázky není cena (`quote.basis_missing`); K6 rozpoznání platformy jen podle technických podpisů (`config/platforms.yaml`, nejistota = `unknown`);
  - K8 s odchylkou: čitelnost jazyků mezi zeměmi se bere z `readable_languages` v `config/jurisdictions.yaml` (jeden zdroj s workerem), ne z `Markets:ReadableLanguages`; K9 lze odškrtnout kteroukoli zemi, aspoň jedna zůstane; K10 sloupce `shops.detection` a `shop_languages.decided_by`/`decided_at`, `source_mode` výchozí `web`, token ověření čitelný; K11 stropy 20 e-shopů za hodinu a 5 ukázek za den na tenanta (neměřeno);
  - K1 `Shops:Ownership:RequiredBefore` nemá výchozí hodnotu, API a worker bez něj nenastartují; vývoj a testy mají `full_analysis` podle návrhu A13. Produkce čeká na rozhodnutí A13.

  Otevřené zůstává: A13 / K1 (kdy je ověření povinné, ověření verze na jiné doméně, opakování ověření), K3 (zda změna 11 ukáže všechny nálezy ukázky), K12 (změna 13). Podpisy platforem kromě Shoptetu jsou „k ověření“ na skutečných stránkách.
- **Změna 11, body K rozhodnutí (2. 10. 2026, uživatel „Ano pokračuj“, výchozí volby se zkontrolují hromadně na konci):** implementace se řídí návrhy z oddílu K rozhodnutí v `changes/add-findings-and-fixes-api/proposal.md`:
  - K1 skupiny hromadných oprav staví jiná změna, tato je jen čte a mění; K2 pořadí přísnosti `text` > `assess` > `verify`, pak závažnost a pásmo; K3 `kept` = rozhodnutí obchodníka, `dismissed` = chyba nástroje (`false_positive`), obojí v protokolu; K4 u e-shopu jen s ukázkou 5 nálezů souhrnu, ostatní v počtech, rozhodnutí `409 shop.sample_only`;
  - K5 jazyk protokolu z požadavku, jinak jazyk domovského trhu; K7 upozornění výchozí zapnutá (`Notifications:Defaults`); K8 řádek upozornění pro každého člena, sloupce `protocols.status`/`error_code`, `fix_proposals.recheck_result`, jedinečné číslo protokolu; K9 odpověď platí pro stejný kód a otisk textu ve všech e-shopech tenanta (otázky za celý web jen v e-shopu); K10 po „Nie“ u otázek za celý web text z pravidla, ne z LLM; K11 `Fixes:DailyGenerationsPerTenant` 50 (neměřeno); K12 doklady PDF, JPG, PNG do 20 MB, `expiring` 30 dní před koncem, bez antiviru;
  - K14 zrušit jde jen ukázku zdarma a opakovanou kontrolu; K15 upozornění jsou v této změně; K16 e-maily o konci běhu posílá změna 8 přes `NotificationDispatcher` (platí `email_run_finished`); K17 po „Kopírovať text“ zůstane nález `approved`, na `resolved` ho přepne až další kontrola.

  Otevřené zůstává: K6 (knihovna PDF; do rozhodnutí protokol skončí `failed` s `pdf_renderer_unavailable`), K13 („Nová kontrola“ na Přehledu, koncový bod nezaložen). Texty e-mailů nových upozornění (sk, cs) a texty protokolu jsou návrh k hromadné kontrole. Živá kontrola 12.6 (placená) čeká na odhad ceny a souhlas.
- **Změna 6, K8 překlad textů (2. 10. 2026, uživatel):** návrh překladu smí napsat model. Na žádost uživatele ho napsal přímo Claude v pracovní session, ne OpenAI za běhu produktu. Pravidlo „LLM jen OpenAI“ platí pro produkt. Soubory mají `machine_draft: true` a nepoužijí se, dokud je nezkontroluje člověk. Kontrola proběhne později lokálně: sken několika skutečných e-shopů a porovnání vlastního rozboru stránek s výsledky Jevu.
- **Cena za každou zemi (2. 10. 2026, uživatel):** do pásma jde součet produktů za každou zaškrtnutou zemi (počet produktů verze kontrolované pro tu zemi; e-shop s jednou verzí a dvěma zeměmi = dvojnásobek). Texty jazykových verzí se kvůli ceně neporovnávají, páry produktů a práh vlastních textů 20 % jsou zrušené; jazyk popisů produktů se dál určuje kvůli upozornění na nepřeložené texty (D, asi 0,01 USD na verzi). Hranice pásem jsou v databázi (`billing.price_tiers`). Neznámý počet produktů (bez produktové sitemap) se nedopočítává, dodá ho konektor; produkty do vzorku se najdou i bez produktové sitemap podle struktury stránky. Dopad: architektura (část 12), ceník, změny 7, 8, 10, 12, 14 a 16, obrazovky 3c a 3d (nejdřív návrh UI ke schválení).
- **Další trhy (1. 10. 2026, uživatel):** nástroj bude později i pro Německo, Polsko, Maďarsko a další země. Kód nesmí znát seznam zemí ani jazyků. Nová země znamená jen data: řádek v `config/jurisdictions.yaml`, sady pravidel, složku `rules/texts/<jazyk>/` a podklady (změna 6, design oddíl 9).
- **Změna 5b bez převodu dat (1. 10. 2026, uživatel):** odpovědi Jevu, přepisy a profily ze `src/cache/jev-cache.sqlite` se do PostgreSQL nepřevádějí (hodnota řádově 10 USD, odhad); příkaz `cache import` nevzniká. Smysl úložiště odpovědí je hlavně limit Jevu (1 200 požadavků za minutu na klíč) při opakovaných bězích. Smazání souboru rozhodne uživatel.
- **Úložiště souborů (1. 10. 2026, uživatel, změna 2):** soubory (snímky HTML, extrakce, PDF, doklady) se zatím ukládají do lokálního souborového systému za rozhraním `IBlobStore` (`FileSystemBlobStore`; na serveru připojený svazek Dockeru, složka `Storage:FileSystem:Root` povinná). Úložiště v cloudu (Azure, AWS…) se přidá později jako další implementace `IBlobStore` vybraná v `Storage:Provider`. MinIO se nepoužívá (obrazy `minio/minio` a `minio/mc` na Docker Hubu nejsou). Provoz poběží na dedikovaném serveru s Dockerem. Dopad na další změny:
  - změna 8: `S3PageContentStore` stojí nad `IBlobStore`, jen ho při implementaci pojmenovat podle úložiště (např. `BlobPageContentStore`); testy proti souborovému systému, ne MinIO;
  - změna 14: média CMS (`@payloadcms/storage-s3`, veřejné čtení ze S3) řešit při implementaci (lokální úložiště Payloadu, nebo cloud přes nový poskytovatel);
  - změna 17: zálohy databáze (WAL-G) dál do S3 u jiného poskytovatele; nově je potřeba zálohovat i svazek se soubory (bod 9 „Zálohy souborů v S3 aplikace“ přepracovat) a nastavit `Storage__FileSystem__Root`.

## A. Rozhoduje uživatel (produkt, obchod, právo)

| # | Otázka | Doporučení | Kde |
|---|---|---|---|
| A1 | Přejmenovat kód na EshopGuard hned, nebo se začátkem F0? | hned, změna 1 je připravená | 1 |
| A2 | Měna pro české zákazníky | **Rozhodnuto 1. 10. 2026: Kč** (účetní potvrdí fakturaci ze SK firmy) | 3, 12 |
| A3 | Domény EshopGuard (.sk, .cz, .com) | vlastní doména na vydání, jinak `/sk`, `/cz` | 14, 17 |
| A4 | Co prodává český web | i kontrolu podle SK zákona pro české e-shopy prodávající na SK; české vydání do rozhodnutí jako koncept | 14 |
| A5 | E-shop zaškrtne jen Slovensko, ale hlavní verze je česká a slovenská existuje: kontrolovat i českou? | ne, jen slovenskou (podle upřesněného pravidla); při pochybnosti se zeptat | 7, 10 |
| A6 | Smí ukázka zdarma stáhnout verzi na jiné doméně (např. goodie.sk) před potvrzením klientem? | ano, jen pro zjištění (pár stránek), kontrola až po potvrzení | 7 |
| A7 | Co klient uvidí z ukázky zdarma | podle strategie: počty podle závažnosti, 5 nejzávažnějších s vysvětlením, 1 ukázka opravy | 10, 11 |
| A8 | Zkušební doba sledování | kalendářní měsíc (jako v návrhu UI: platba 1. 10. → první měsíční 1. 11.) | 12 |
| A9 | Vyšší pásmo | od dalšího období bez doplatku, upozornění 7 dní předem | 12 |
| A10 | Sleva od 3. e-shopu: kolik % | do rozhodnutí vypnutá | 12 |
| A11 | Roční platba sledování | zatím jen měsíční (návrh UI roční nenabízí) | 12 |
| A12 | Výpovědní lhůta při zdražení | 30 dní, ověří právník | 12 |
| A13 | Kdy je povinné ověřit vlastnictví e-shopu (meta, DNS, konektor) | před úvodní analýzou a vždy před publikováním; ukázka zdarma bez ověření | 10 |
| A14 | Identita nálezu: stejná věta na více místech = jeden nález (otisk textu bez kontextu) | ano, ale výskyty se ukazují všechny (nic se neskrývá) | 8 |
| A15 | Překlad textů pravidel do slovenštiny | první návrh smí napsat model, každý text zkontroluje člověk | 6 |
| A16 | Poskytovatel serveru a S3 pro zálohy | Hetzner Cloud CCX23 + S3 s ochranou proti smazání (object lock), zálohy 30 dní | 17 |
| A17 | Kalkulačka ceny na webu | zatím ne, později posuvník nad pásmy z API | 14 |
| A18 | Shoptet: smlouva a schválení doplňku (až 4 týdny) | zahájit souběžně s F5–F7 | 15 |
| A19 | Souhlas obchodníka se zpracováním textů přes OpenAI | ano, do obchodních podmínek (právník) | 15, 12 |
| A20 | Chybějící návrhy UI (pravidlo: nejdřív návrh): průběh ukázky; běžící, částečná a selhaná analýza; pokrytí kontroly (nezkontrolované stránky); Nastavenia; Zabudnuté heslo; panel upozornění; hledání; dialogy Pridať doklad a Zrušiť sledovanie; přepnutí účtu; mobilní varianty; souhlas s obchodními podmínkami a řádek DPH na 3c; pohled „Podľa nálezov“ | navrhnout je před F7 | 13 |
| A21 | Cena e-shopu bez známého počtu produktů: jedna sitemap bez produktové (živá ukázka naturfyt.sk na Shoptetu, 2. 10. 2026), bez konektoru | **rozhodnuto 2. 10. 2026:** když produkty známe, cena podle produktů (beze změny); když ne, z počtu stránek ke kontrole ze sitemap (`priceBasis.unit = pages`, stejná pásma), v UI „stránok na kontrolu“; sledování od 2. měsíce podle skutečného počtu produktů z úvodní analýzy. Bez stránek i produktů dál bez ceny (`scope.product_count_unknown`). Kód změny 10 hotový (oprava J), částky změna 12 | 10, 12, 15 |

## B. Technické výchozí volby (platí, pokud uživatel nenamítne)

- **Hranice mezi změnami:**
  - nabídka ceny: změna 10 počítá rozsah a koncový bod `quote`, změna 12 částky, slevy, DPH a uložení nabídky, změna 8 jen základ (počty produktů po verzích);
  - objednávka odmítne zastaralou nabídku (`409 quote.stale`).
- **Úložiště:**
  - cache (`PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`) dělá změna 5b, stejně pro CLI i aplikaci, bez SQLite (požadavek uživatele 1. 10. 2026);
  - ostatní úložiště (`PgPageStore`, `PgUrlFrontierStore`, `S3PageContentStore`) dělá změna 8;
  - změna 5 dodá rozhraní a společné testy chování.
- **Projekt `EshopGuard.Application`** pro služby, API zůstane tenké. Rozhraní pro rozsah a vlastnictví jsou v `EshopGuard.Jobs`, aby nevznikl cyklus změn 8 a 10. Politiku vlastnictví dává od změny 10 `Shops:Ownership:RequiredBefore` (bez nastavení API ani worker nenastartují, do rozhodnutí A13).
- **Přihlášení:** token v odkazu za `#` (nedostane se do logů serveru); e-maily s tokenem jdou přímo, ne přes `ops.outbox`; cizí tenant vrací 404.
- **Databáze:**
  - funkce `SECURITY DEFINER` pro práci napříč tenanty (plánovač, outbox, přepínač účtů, pozvánky);
  - `ops.schedules` s klíčem (`shop_id`, `kind`);
  - jedinečnost jen nesmazaných řádků;
  - cizí klíče dělených tabulek přes (`tenant_id`, `shop_id`, `id`);
  - `sieve_answers` 32 částí;
  - upozornění = řádek na každého člena;
  - jasná chyba při chybějícím tenantovi.
- **Fronta:**
  - neúspěšné úlohy se drží 30 dní;
  - kromě zámku domény i klíč souběhu `domain:{doména}`;
  - v nočním okně vyhrazený podíl limitu Jevu pro sledování (P3);
  - noční úlohy konektorů rozložené podle minuty e-shopu, ne pevně ve 2:00.
- **Stahování:** cookie zvlášť pro každou verzi (`UseCookies = false` u sdíleného klienta), ověří se ostrým porovnáním.
- **Změna 5 (návrhy z jejího oddílu K rozhodnutí, použité při implementaci 1. 10. 2026):**
  - rozsahy SSRF podle návrhu designu včetně 100.64.0.0/10, dokumentačních a testovacích sítí, multicastu, `64:ff9b::/96`, IPv4 mapovaných do IPv6 a navíc 6to4 (`2002::/16`, kontroluje se vložená IPv4);
  - otisk věty = prvních 8 bajtů SHA-256 normalizovaného textu, bez nového balíčku;
  - limit čtení stránky 30 s (`crawl.extract_timeout_seconds`);
  - SmartReader 0.11.1 přijme hotový dokument (`Reader(string, IHtmlDocument)`), ale mění ho, proto dostává hlubokou kopii jediného čtení; výsledky stejné na 430 stránkách;
  - nahrávky cizích e-shopů jen lokálně (`src/snapshots/`, `src/baselines/`).
- **Pravidla SK a CZ:** stejné id otázek s jiným zněním → oddělené dotazy na Jev, jeden nález s verdikty po zemích. Dnešní seznamy slov (`LegalPageSlugs` apod.) zůstávají ve změně 5 kvůli shodě výsledků, nahrazení je samostatný úkol.
- **Konektor:**
  - šifrované tajemství webhooku (`webhook_secret_enc`);
  - propojení instalace jen s přihlášeným správcem Shoptetu a uživatelem EshopGuardu;
  - do rozhodnutí propojit jen hlavní jazykovou verzi.
- **Provoz:**
  - nasazení přes Tailscale místo otevřeného SSH;
  - pgBackRest jako náhrada, kdyby WAL-G nepodporoval PostgreSQL 18;
  - zkouška obnovy v režimu bez workeru a klíčů (`DRILL_MODE`);
  - pravidla `DOCKER-USER` kvůli obcházení firewallu.
- **Web:** pořadí výběru vydání doména → cookie → jazyk prohlížeče (ruční volba má přednost); `hreflang` jen mezi stránkami se stejnou skupinou.

## C. Mezery v plánu (zatím bez změny)

1. **Hromadné opravy** (`fix_groups`): sestavení skupin stejného textu. Dnes `PageRewriter` přepíše opakovaný nález jen na první stránce. Návrh: nová změna 18 `add-group-fixes-and-rule-updates`.
2. **Překontrola po nové verzi pravidel** (`rule_update`): kdo ji zakládá a co přepočítá. Návrh: tamtéž (změna 18).
3. **Zpracování běhu `connector_check`:** kroky dělá změna 16 (paměť rozhodnutí, upozornění), změna 15 zakládá běh a stahuje data. Potvrdit sladěním názvů.
4. **Verzované obchodní podmínky a zásady ochrany soukromí:** v návrhu databáze chybí tabulka. Návrh: `billing.legal_documents` ve změně 12, souhlas uložený k objednávce.
5. **Chybějící sloupce a tabulky v návrhu databáze**, které změny navrhují. Doplní se do `databaze-a-plan-implementace` před F1:
   - `checks.run_urls`;
   - `pages.sitemap_lastmod`;
   - `protocols.status`;
   - `shops.detection`;
   - tři `lookup_key` u pásma;
   - `shop_languages` s víc verzemi v jednom jazyce;
   - další `switch_method`.

## D. Ověřit v dokumentaci během práce (ne rozhodnutí)

- **Stripe:** dvě slevy v Checkoutu, skrytí faktur v portálu, testovací hodiny.
- **SuperFaktúra:** sandbox, API e-faktúry, dohledání dokladu podle našeho klíče.
- **Shoptet:**
  - hodnoty `visibility`;
  - zápis stránky (`description` × `content`);
  - názvy událostí a kódování podpisu;
  - zda předává identitu do nastavení doplňku.
- **Ostatní:** podpora PostgreSQL 18 ve WAL-G; verze Next.js podporovaná Payloadem 3. (Licence obrazu MinIO už není potřeba, viz Rozhodnuto.)

Podrobné body jsou v `changes/<změna>/proposal.md`, oddíl „K rozhodnutí“.
