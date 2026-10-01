# Proposal: Konektor Shoptet (doplněk, webhooky, dorovnání, publikování oprav)

## Intent

**Problém.** E-shop bez konektoru EshopGuard jen prochází jako zákazník. To má tři důsledky:
- změnu produktu pozná nejdřív v noční kontrole;
- texty vidí až po vykreslení webu, ne tak, jak jsou v administraci;
- opravu musí obchodník zkopírovat ručně.

Návrh UI přitom slibuje „Zmenu produktu nám Shoptet ohlási hneď po uložení a text skontrolujeme do niekoľkých minút“ (Monitoring), „Prijaté zmeny zapíšeme do Shoptetu, pôvodný text si uložíme a zmenu môžete vrátiť“ (Review) a „Pripojiť cez Shoptet · Odporúčané“ (Onboarding).

**Proč Shoptet první.**
- Shoptet má asi polovinu e-shopů v CZ a SK (strategie, část 4, kanál 1).
- Produktové webhooky má od 29. 7. 2026 v betě.
- Zápis produktu, kategorie, stránky a článku jde přes PATCH (rešerše konektorů, Srovnání).
- Zápis do e-shopu nemá žádný konkurent (strategie, část 1).

**Přínos.**
- Změna produktu se zkontroluje do pár minut po uložení. Cíl této změny: do 5 minut od uložení do nálezu v aplikaci, měřeno v ověření, zatím neměřeno.
- Skrytý produkt (`visibility=hidden`) se zkontroluje ještě před zveřejněním.
- Schválená oprava se zapíše do Shoptetu jedním klikem a jde vrátit.
- Počet zveřejněných produktů pro pásmo ceny je přesný (změna 12).
- Instalace doplňku zároveň ověří vlastnictví e-shopu (architektura, část 11, bod 4).

**Fáze:** F8 (databaze-a-plan-implementace-2026-10-01.md, část 8). Podle README až po pilotu.

**Podklad:**
- `podklady/reserse/konektory-api-2026-10-01.md`: Hlavní zjištění 1–6, Srovnání (sloupec Shoptet), Podrobnosti Shoptet, Co z toho plyne 1–7;
- `architektura-multitenant-worker-2026-10-01.md`: část 4 (priority P0 a P1), část 5 Konektory (tři cesty ke změnám, zpracování události, slučovač, limity, hlídač odběrů, zápis oprav, kontrola „při uložení“, přístup je obchodní krok), část 3 (šifrování klíčů konektorů);
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.2 (`shop.connectors`, `connector_webhooks`, `connector_events`, `shop_languages`, `shop_verifications`), 3.3 (`pages.source`, `external_id`, `is_hidden_in_shop`), 3.4 (`runs.kind = connector_check`, `page_changes`), 3.5 (`publications`, `fix_proposals`), 3.8 (`rate_limit_buckets`, `schedules` `reconcile`);
- návrh UI: `Onboarding.dc.html` (Pripojiť Shoptet), `OnboardingOther.dc.html` (Shoptet „Povolenie v Shoptete“), `OnboardingScope.dc.html` („bylinkovo.sk je pripojený cez Shoptet … Publikovanie opráv je povolené“), `Review.dc.html` (Text zo Shoptetu, produkt č. 2429, Publikovať do e-shopu, Kopírovať text), `Monitoring.dc.html` (Kontrola pri uložení, aj skrytých produktov; Posledné zmeny).

## Scope

In scope:
- **Obchodní předpoklady:** smlouva a schválení doplňku u Shoptetu (odpověď do 4 týdnů), požadovaná práva, testovací e-shop. Jsou to úkoly mimo kód a podmínka ověření.
- **Instalace doplňku a propojení:**
  - instalační volání Shoptetu, výměna jednorázového kódu za token do 5 s, kontrola zdrojové sítě 185.184.254.0/24;
  - bezpečné propojení instalace s e-shopem v EshopGuardu: přihlášení správce e-shopu přes stránku nastavení doplňku, jednorázový odkaz a potvrzení vlastníkem nebo správcem účtu;
  - kontrola domény;
  - zápis ověření vlastnictví (`shop_verifications.method = connector`).
- **Tokeny:** OAuth token šifrovaně (ASP.NET Core Data Protection), krátkodobý API token (30 min) jen v paměti, tajemství podpisu webhooků šifrovaně. Nic z toho v logu.
- **Příjem webhooků** `POST /api/webhooks/shoptet`:
  - ověření podpisu HMAC-SHA1 z hlavičky `Shoptet-Webhook-Signature`;
  - uložení do `connector_events` a odpověď do 1 s (Shoptet vyžaduje 4 s);
  - zpracování ve workeru;
  - deduplikace podle otisku, protože Shoptet ID události neposílá;
  - slučování událostí e-shopu za 2 minuty do jedné úlohy.
- **Dorovnání:**
  - `/products/changes` (okno 30 dní) s kurzorem u konektoru;
  - každou hodinu plánuje tato změna, noční dorovnání v minutě e-shopu plánuje změna 16 (`ops.schedules` `kind = reconcile`) a volá stejnou úlohu;
  - úplná synchronizace, když kurzor zestárne;
  - noční projití kategorií, stránek a článků s porovnáním otisků textů, protože pro ně Shoptet webhook nemá.
- **Hlídač odběrů:** každou hodinu porovná registrované webhooky s očekávanými, obnoví chybějící, přečte log notifikací (7 dní) a stav doplňku. Výpadek ukáže v aplikaci.
- **Čtení textů po jazycích** (parametr `language`):
  - produkt: název, doplňkový název, krátký a dlouhý popis, meta, příznaky, parametry;
  - kategorie, stránky (čtení z `content`), články;
  - zápis do `content.pages` (`source = connector`, `external_id`) a do `shop_languages` (`source = connector`, `product_count`).
- **Kontrola změněných textů:**
  - běh `connector_check` (P1) přes kroky pipeline ze změny 8 s parametry běhu `change_source` (`webhook`, `reconcile`, `save_hidden`) a `change_kind` po stránkách;
  - zpracování výsledku do `page_changes`, paměti rozhodnutí a upozornění doplní změna 16 (`ConnectorChangeProcessor`). Tato změna jí předá zdroj a druh změny.
- **Kontrola před zveřejněním:** u produktu uloženého jako skrytý (`visibility=hidden`), když má e-shop zapnuté `check_hidden_on_save`:
  - tato změna skrytý produkt rozpozná, načte jeho text a založí přednostní kontrolu (P0);
  - hranice s úlohou `monitor.check_saved` změny 16 je v K rozhodnutí 13.
- **Publikace opravy:**
  - znovu načíst text a porovnat ho s textem, ze kterého vznikl nález, jinak konflikt a nová kontrola;
  - uložit původní znění;
  - vložit opravu jen do dotčeného bloku pole;
  - PATCH jen dotčeného pole a jazyka, `idempotency_key`, kontrola po zápisu;
  - stav v `publications` a `fix_proposals`.
- **Vrácení publikované opravy** s kontrolou, že text v e-shopu se mezitím nezměnil.
- **Limity konektoru:** nejvýš 3 souběžná spojení na token, odtok 10 požadavků/s na e-shop (leaky bucket 200), reakce na 429 a 423 (zámek 5 s).
- **Odpojení:** v aplikaci, odinstalace a pozastavení doplňku na straně Shoptetu, smazání tokenů, návrat e-shopu na procházení webu, pozastavené a neprovedené publikace.

Out of scope:
- Upgates, BiznisWeb, WooCommerce a Shopify (další změny se stejným rozhraním `EshopGuard.Connectors`);
- zápis textů variant: Shoptet při zápisu varianty vyžaduje všechny parametry, jinak je smaže. Varianty se jen čtou, oprava nabídne „Kopírovať text“;
- texty šablony (hlavička, patička, bannery): API Shoptetu je nevrací, dál se procházejí na webu (změna 16) a oprava je „Kopírovať text“;
- noční plánovač sledování e-shopů bez konektoru, rozdíl sitemap a týdenní procházení webu (změna 16). Tato změna dodá jen spouštěč z webhooku `eshop:design`;
- přihlášení do EshopGuardu přes Shoptet (architektura, část 11, bod 6, „později“);
- kontrolní obrazovka uvnitř administrace Shoptetu: pravidla Shoptetu povolují iframe jen pro nastavení doplňku;
- prodej doplňku na tržišti Shoptet a jeho cena a provize (neověřeno, obchodní otázka);
- hromadné zápisy přes dávkový JSONL;
- obrazovky aplikace (změna 13). Tato změna dodá API a stavové kódy.

## Approach

1. **Nová složka `src/EshopGuard.Connectors/Shoptet`** se společnými abstrakcemi v `EshopGuard.Connectors/Abstractions`: `IConnectorTextSource`, `IConnectorPublisher`, `IConnectorWebhookVerifier`, `ConnectorText`. Další platformy přidají jen svou implementaci.
2. **Webhook je rychlá cesta, zdrojem pravdy je dotaz „změněno od“.** Shoptet zkusí doručení 3× po 15 minutách a pak zprávu zahodí, ID pro duplicity neposílá. Worker proto vždy načte aktuální stav přes API a obsahu zprávy nevěří.
3. **API jen ověří, uloží a odpoví.** Výjimka: výměna instalačního kódu za token musí proběhnout synchronně do 5 s. Tu dělá API přímo, s časovým limitem 4 s.
4. **Publikace je fail-closed.** Zapíše se jen tehdy, když:
   - text v e-shopu je stejný jako text, ze kterého vznikl nález;
   - opravovaný blok se v poli najde právě jednou;
   - konektor má právo zápisu.

   Jinak vznikne konflikt nebo „Kopírovať text“. Zápis je idempotentní: opakovaný pokus po úspěšném PATCH pozná, že nový text už v e-shopu je.
5. **Propojení instalace s účtem nikdy jen podle domény.** Kdokoli si může v EshopGuardu založit e-shop s cizí doménou. Propojení proto vyžaduje přihlášení správce e-shopu v administraci Shoptetu i potvrzení přihlášeným uživatelem EshopGuardu.

## Dependencies

- **8 `add-analysis-runs-in-worker`:** běh `connector_check` a `recheck`, kroky Evaluate, Rules a Rewrite nad texty bez procházení (`IEshopGuard.AnalyzeTextsAsync` s `TextInput`), zápis nálezů.
- **11 `add-findings-and-fixes-api`:** `fix_proposals` (přijaté, upravené), skupiny oprav a tlačítko „Kopírovať text“. Hranice: tato změna přidá endpoint publikace a vrácení a obsluhu ve workeru. Pokud je změna 11 už založila, napojí se na ně (proposal, K rozhodnutí 6).
- **10 `add-shops-and-onboarding-api`:** volba způsobu připojení na obrazovce 2, `shops.source_mode`, `shop_languages`, `shop_verifications`.
- **9 `add-identity-and-tenants-api`:** role (propojit doplněk smí owner a admin, publikovat editor a výš), upozornění.
- **4 `add-job-queue-and-worker`:** fronta, `dedupe_key`, `concurrency_key`, `not_before`, `rate_limit_buckets`, plánovač.
- **3 `add-multitenant-data-model`:** tabulky `connectors`, `connector_webhooks`, `connector_events` (měsíční části, uchování 30 dní) a `publications`. Tato změna doplní chybějící sloupce a tabulky propojení.
- **Externí:**
  - partnerská smlouva a schválený doplněk Shoptetu s právy ke čtení a zápisu produktů, kategorií, stránek a článků a k webhookům;
  - testovací e-shop Shoptet;
  - veřejná adresa API s HTTPS pro instalační volání a webhooky (lokálně tunel).
- **Navazují:**
  - 16 `add-change-monitoring`: noční dorovnání v minutě e-shopu, týdenní procházení webu, `ConnectorChangeProcessor` (zápis `page_changes`, paměť rozhodnutí, upozornění „Nový nález“), obrazovka Sledovanie;
  - 12: `shop_languages.product_count` z konektoru → pásmo.

## Done when

- **Kontrola po uložení:** v testovacím e-shopu Shoptet se změní popis produktu a nález se v aplikaci objeví do 5 minut od uložení. Měří se v ověření na 10 změnách, medián a maximum se zapíší.
- **Skrytý produkt:** uložení skrytého produktu s porušením dá nález z běhu s `change_source = save_hidden` dřív, než se produkt zveřejní. Popisek „Kontrola pred zverejnením“ na obrazovce Sledovanie doplní změna 16.
- **Publikace a vrácení:**
  - publikace přijaté opravy zapíše do Shoptetu jen dotčený blok a jazyk;
  - vrácení obnoví původní znění;
  - změna textu v administraci mezi nálezem a publikací dá konflikt bez zápisu.
- **Ztracený webhook:** výpadek přijímače (webhook se neuloží) dorovná hodinová úloha `/products/changes` a změna se zkontroluje.
- **Hlídač:** smazaný odběr webhooku hlídač do hodiny obnoví a aplikace výpadek ukáže.
- **Odinstalace:** odinstalace doplňku smaže tokeny a e-shop přepne na procházení webu s upozorněním.
- **Testy:** `dotnet test` projde včetně `tests/EshopGuard.Connectors.Tests`. Test logů nenajde žádný token Shoptetu. `openspec validate add-shoptet-connector` projde.

## K rozhodnutí

1. **Smlouva a schválení doplňku u Shoptetu** jsou obchodní krok s odpovědí do 4 týdnů. Bez nich nejde ověření naostro. Kdo a kdy podá žádost? Které poplatky a provize platí? Obojí neověřeno.
2. **Mechanismus propojení instalace s účtem.** Návrh: stránka nastavení doplňku v administraci Shoptetu přihlásí správce e-shopu a vydá jednorázový odkaz. Uživatel EshopGuardu pak propojení potvrdí. Ověřit v dokumentaci:
   - zda Shoptet stránce nastavení předává ověřitelnou identitu e-shopu a správce (installing-the-addon, addon-settings-in-shoptet-administration);
   - zda jde předat vlastní parametr přes instalaci.

   Když ano, jde postup zjednodušit.
3. **Nesrovnalost v datovém modelu.** `shop.connectors.webhook_secret_hash` (databáze, část 3.2) nestačí, protože HMAC podpis jde ověřit jen se skutečným tajemstvím. Tato změna přidává `webhook_secret_enc` (šifrovaně stejně jako tokeny) a `webhook_secret_hash` nechá jen pro rychlé porovnání. Jak Shoptet tajemství podpisu vydává a obnovuje, ověřit v dokumentaci webhooků.
4. **Cíl „do pár minut“.** Návrh: do 5 minut od uložení do nálezu (slučovací okno 2 min + načtení + Jev). Při plné frontě Jevu může být víc, protože P1 sdílí limit s P2–P4 nad vyhrazený podíl.
5. **Zveřejněné produkty pro pásmo.** Které hodnoty `visibility` se počítají jako zveřejněné (`visible`, případně `detailOnly`), ověřit v OpenAPI Shoptetu. Do ověření se počítá jen `visible`. Varianty se nepočítají.
6. **Hranice se změnou 11** (kdo zakládá endpoint `POST …/publications`). Tato změna ho zakládá. Pokud ho změna 11 už zavedla, tato změna jen doplní obsluhu ve workeru.
7. **Hromadné události Shoptetu** (import, zhruba po 300 položkách) a události doplňku (odinstalace, pozastavení, obnovení): přesné názvy, tvar zprávy a zda nesou seznam ID ověřit v dokumentaci (webhooks, product-webhooks-pilot, pausing-and-resuming-addon).
8. **Zápis stránky.** Shoptet stránku čte z `content`, ale zapisuje do `description`. Ověřit na testovacím e-shopu, že zápis do `description` změní text, který čte `content`.
9. **Kontrola skrytých produktů je P0** a čerpá vyhrazený podíl limitu Jevu. Při hromadném importu skrytých produktů (stovky) by mohla vyhrazený podíl vyčerpat. Návrh: P0 jen do 20 produktů v jedné sloučené dávce, zbytek P1.
10. **Data z konektoru jsou texty tenanta**, včetně nezveřejněných konceptů skrytých produktů. Ukládají se jen v tabulkách a souborech tenanta, nikdy v provozních logech. Souhlas obchodníka se zpracováním přes OpenAI (návrhy oprav) patří do podmínek doplňku. U Upgates je povinný, u Shoptetu ověřit.
11. **Jeden, nebo víc konektorů na e-shop (nesrovnalost).**
    - ER diagram (databáze, část 4) má `shops ||--o| connectors`, tedy nejvýš jeden konektor na e-shop.
    - Oddělené CZ a SK e-shopy jsou ale v Shoptetu oddělené instalace (rešerše, Jazyky).
    - V EshopGuardu přitom může být `bylinkovo.cz` jazykovou verzí e-shopu `bylinkovo.sk` (architektura, část 12, verze na jiné doméně).

    Návrh: konektor nese `shop_language`, jedinečnost je (`shop_id`, `shop_language`). Do rozhodnutí se propojuje jen hlavní verze a druhá instalace dostane `connector.second_installation_not_supported`.
12. **Co dělat při trvalém výpadku konektoru.** Návrh: po 24 h ve stavu `error` přepnout e-shop na procházení webu (změna 16) a upozornit zákazníka. Do té doby se opírat o noční dorovnání.
13. **Hranice se změnou 16** (její K rozhodnutí 9). Návrh:
    - **15:** událost, načtení aktuálního textu přes API, rozpoznání skrytého produktu, nové verze stránek a založení běhu `connector_check` s `change_source`, `change_kind` a prioritou (P0 pro skryté, nejvýš 20 na dávku);
    - **16:** zpracování výsledku (rozdíl vět, paměť rozhodnutí, `page_changes`, upozornění).

    Pokud změna 16 ponechá vlastní úlohu `monitor.check_saved` pro skryté produkty, tato změna jí jen předá událost (rozhraní `IConnectorChangeSink`) a vlastní P0 běh nezakládá. Hodnotu `page_changes.source = reconcile` (změna nalezená dorovnáním, ne webhookem) navrhujeme doplnit ve změně 16.
