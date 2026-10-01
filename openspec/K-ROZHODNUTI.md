# Otevřená rozhodnutí z implementačního plánu (1. 10. 2026)

Každá změna má v `proposal.md` oddíl „K rozhodnutí“, celkem asi 320 bodů. Většina je technická. Zde jsou roztříděné:
- **A** rozhoduje uživatel;
- **B** jsou technické výchozí volby, které platí, pokud uživatel nenamítne;
- **C** jsou mezery v plánu;
- **D** se ověří v dokumentaci během práce.

## A. Rozhoduje uživatel (produkt, obchod, právo)

| # | Otázka | Doporučení | Kde |
|---|---|---|---|
| A1 | Přejmenovat kód na EshopGuard hned, nebo se začátkem F0? | hned, změna 1 je připravená | 1 |
| A2 | Měna pro české zákazníky | Kč (účetní potvrdí fakturaci ze SK firmy) | 3, 12 |
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

## B. Technické výchozí volby (platí, pokud uživatel nenamítne)

- **Hranice mezi změnami:**
  - nabídka ceny: změna 10 počítá rozsah a koncový bod `quote`, změna 12 částky, slevy, DPH a uložení nabídky, změna 8 jen základ (počty produktů po verzích);
  - objednávka odmítne zastaralou nabídku (`409 quote.stale`).
- **Úložiště:** PostgreSQL implementace úložišť knihovny (`PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`, `PgPageStore`, `PgUrlFrontierStore`, `S3PageContentStore`) dělá změna 8, změna 5 dodá rozhraní a společné testy chování.
- **Projekt `EshopGuard.Application`** pro služby, API zůstane tenké. Rozhraní pro rozsah a vlastnictví jsou v `EshopGuard.Jobs`, aby nevznikl cyklus změn 8 a 10. Do rozhodnutí A13 platí `DenyAllOwnershipPolicy`.
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
- **Ostatní:** podpora PostgreSQL 18 ve WAL-G; verze Next.js podporovaná Payloadem 3; licence obrazu MinIO.

Podrobné body jsou v `changes/<změna>/proposal.md`, oddíl „K rozhodnutí“.
