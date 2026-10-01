# Proposal: Noční sledování změn e-shopů

## Intent

**Problém.** Po úvodní analýze se e-shop dál mění: přibývají produkty, mění se popisy, šablona, odznaky i obchodní podmínky. Předplatné sledování (9–59 € měsíčně za e-shop, strategie) slibuje denní kontrolu změněných stránek a nových produktů a upozornění. Dnes nic takového není:
- každá kontrola by znamenala stáhnout a vyhodnotit celý e-shop (5 000 stránek ≈ 33 min stahování a ~175 000 volání Jevu, architektura část 8), což se pro stovky e-shopů každou noc nevejde do limitu Jevu;
- obchodník by u nového produktu se stejnou větou dostal znovu stejnou otázku a nový návrh od modelu, i když ji už jednou vyřešil;
- nové místo prodeje nebo nová jazyková verze by zůstaly bez kontroly a bez upozornění.

**Proč.** Sledování je opakovaný příjem a hlavní důvod zůstat po úvodní analýze („jednou opraví a odejde“ je podle strategie hlavní riziko). Musí být levné na provoz a spolehlivě říct, co se kontrolovalo a co ne.

**Přínos.**
- Noc stahuje jen to, co se mohlo změnit: rozdíl sitemap, úvodní, právní a šablonové stránky, rotace 1/7 zbytku a podmíněné stažení (304 = nic se nepočítá).
- Jev dostane jen věty, jejichž stav (věta s kontextem) ještě není v cache tenanta. Podíl změn za den je neměřený; při 2 % změněných stránek je to podle části 8 asi 3 500 volání na e-shop s 5 000 stránkami místo ~175 000.
- Paměť rozhodnutí: stejný text dostane stejnou opravu bez modelu, „ponechat“ se znovu neptá, doklad platí dál.
- Obchodník dostane upozornění v aplikaci a e-mailem a týdenní souhrn včetně toho, co se nezkontrolovalo.
- Nové místo prodeje nebo jazyková verze vyvolá upozornění „Vyzerá to, že predávate aj …“; trh se sám nepřidá.

**Fáze:** F9 Noční sledování.

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`: část 4 (třídy P0–P4, vyhrazený podíl limitu Jevu), část 5 (Plánovač nočního sledování, Konektory: tři cesty ke změnám, slučovač, dorovnávání), část 7 (upozornění, když noční běhy nestihnou okno), část 8 (Noční sledování: výpočet kapacity), část 11 bod 3 (frekvence podle tarifu), část 12 (Při sledování: nový silný znak → upozornění, trh se sám nepřidá);
- `databaze-a-plan-implementace-2026-10-01.md`: `shop.shops.monitor_slot_minute`, `content.pages` (`rotation_bucket`, `next_check_at`, `http_etag`, `http_last_modified`), `content.page_versions.segment_hashes`, `checks.page_changes`, `fixes.decision_memory`, `fixes.evidence_items`, `iam.notifications`, `iam.notification_settings`, `ops.schedules`, `ops.outbox`; část 5 (kolik práce je za noc); část 8 fáze F9;
- `navrhy-rozvoje-2026-09-30.md`: návrh 1a′ (paměť rozhodnutí pro sledování), návrh 14 (kontrola po uložení produktu nebo konceptu);
- `platby-a-fakturace-2026-10-01.md`: sledování se platí po e-shopech, 30 dní zkušební doby, po posledním neúspěšném pokusu se sledování pozastaví;
- `strategie-a-cenik-2026-09-30.md`: řádek „Sledování“ (denní kontrola změněných stránek a nových produktů, upozornění, férové užití N změněných stránek měsíčně).

## Scope

In scope:
- Plánovač nočního sledování v jedné instanci workeru (zámek ze změny 4): minuta v okně podle hashe ID e-shopu (`shops.monitor_slot_minute`), jedinečná úloha (e-shop, datum), dotažení zpožděných nocí, hlídání konce okna.
- Výběr stránek na noc u e-shopu bez konektoru: rozdíl sitemap (nové, zmizelé, změněné podle `lastmod`), úvodní, právní a šablonové stránky každou noc, rotace celého webu 1/7 (`pages.rotation_bucket`), opakování stránek, které minulou noc selhaly (`pages.next_check_at`).
- Podmíněné stažení (`If-None-Match`, `If-Modified-Since`): 304 nic nepočítá, stejný `text_hash` nevytvoří verzi.
- Kontrola jen změněných vět: Jev dostane jen stavy (věta s kontextem), které nejsou v cache tenanta; nálezy nezměněných vět zůstanou beze změny; zmizelé výskyty se odeberou.
- Paměť rozhodnutí (`fixes.decision_memory`): stejný text → stejná oprava bez LLM s kontrolou Jevem v novém okolí; „ponechat“ → bez otázky; „ponechat s dokladem“ → platný doklad nález uzavře, propadlý ho vrátí k odpovědi; změna verze pravidel paměť zneplatní.
- Záznam změn (`checks.page_changes`) a výsledku (`new_violation`, `new_assess`, `fix_confirmed`, `ok`).
- Signály nového místa prodeje nebo jazykové verze (nová `hreflang`, nový přepínač, nová měna ve strukturovaných datech, nová doména verze) → `shop_markets.status = suggested` nebo `shop_languages.status = needs_confirmation` a upozornění; trh ani verze se sami nepřidají do kontroly.
- E-shopy s konektorem (napojení na změnu 15): běhy `connector_check` změny 15 (webhooky, hodinové a noční dorovnání `connector.reconcile_changes`, noční `connector.sync_content`, kontrola skrytého produktu s P0) projdou stejným zpracováním jako noc (změněné věty, paměť rozhodnutí, upozornění); plánovač k tomu jednou týdně zakládá běh `weekly_web`, který podmíněně projde celý web kvůli šabloně, odznakům a stránkám, které API nevrací.
- Priority úloh sledování: P0 kontrola produktu uloženého jako skrytý nebo koncept (běh změny 15), P1 ostatní změny z konektoru, P3 noční sledování a týdenní web, P4 souhrn a úklid.
- Upozornění v aplikaci (`iam.notifications`) a e-mailem (`ops.outbox`) na nové porušení podle `notification_settings`.
- Týdenní souhrn e-mailem v jazyce příjemce (`users.locale`), včetně toho, co se nezkontrolovalo.
- Odhad nákladů noci před placeným voláním a provozní upozornění nad prahem.
- Zátěžový test: 100 testovacích e-shopů v nočním okně.

Out of scope:
- Konektor Shoptet, příjem webhooků, slučovač událostí, dotaz `/products/changes`, plánování dorovnání (`ops.schedules kind = reconcile`), kontrola skrytého produktu, hlídač odběrů a zápis oprav: změna 15. Tady se jen napojí výstup jejích běhů `connector_check`.
- Překontrola po nové verzi pravidel (`runs.kind = rule_update`, P4) a obnova profilů (`profile_refresh`): K rozhodnutí 6.
- Automatické zveřejnění opravy z paměti (`decision_memory.auto_publish`): standardně vypnuté (návrh 1a′), K rozhodnutí 11.
- Doklad podle značky bez otázky u nových produktů (návrh 1a′): potřebuje značku produktu, K rozhodnutí 10.
- Fakta obchodu pro návrhy (návrh 1a, `shop.shop_facts`).
- Upozornění „Odteraz kontrolujeme aj …“ po přidání podpory nového trhu: patří k přidání trhu (architektura část 12, Přidání dalšího trhu).
- Obrazovka Sledování a texty upozornění ve frontendu: změna 13; API pro sledování: změna 11 nebo 10.
- E-mailová infrastruktura (odesílání z `ops.outbox`, šablony obecně): změna 9; tady jen šablony `weekly_summary` a `new_violation`.
- Zátěžový test tisíců e-shopů: změna 17.

## Approach

1. **Plánovač jen zakládá, nic nepočítá** (architektura část 2). `MonitorScheduler` v `EshopGuard.Jobs/Monitoring` běží v jedné instanci podle zámku v databázi, každou minutu najde e-shopy, jejichž minuta v okně nastala, a založí běh `monitoring` s úlohou `monitor.plan` (`dedupe_key = monitor:{shop}:{místní datum}`).
2. **Běh sledování používá kroky úvodní analýzy ze změny 8** (`run.fetch`, `run.extract`, `run.segment`, `run.sieve`, `run.evaluate`, `run.rules`, `run.rewrite`, `run.finalize`) v rozdílovém režimu: plán stránek místo celé fronty, podmíněné stažení, segmentace jen změněných verzí, pravidla jen nad dotčenými větami a stránkami, přepis až po paměti rozhodnutí.
3. **Čistá logika v knihovně.** Výběr stránek (rozdíl sitemap, rotace), rozdíl otisků vět, porovnání s pamětí rozhodnutí a rozpoznání nového trhu jsou funkce bez databáze v `EshopGuard.Core/Monitoring`. Worker zůstává tenký.
4. **Fail-closed.** Stránka, která v noci nešla stáhnout, není „beze změny“: zůstanou jí nálezy, dostane `next_check_at` na další noc a objeví se ve výčtu nezkontrolovaného běhu i v týdenním souhrnu. Stránka, kterou se nepodařilo zkontrolovat déle než 7 dní, se hlásí zvlášť.
5. **Náklady:** před první dávkou Jevu nebo LLM se uloží odhad; noc se kvůli ceně nezastavuje, nad prahem jde upozornění provozu (stejný princip jako garantovaná cena ve změně 8).

## Dependencies

- **Změna 8 `add-analysis-runs-in-worker`:** obsluhy kroků, stavový automat, `run_urls`, zápis `pages`, `page_versions`, `findings`, `fix_proposals`, `usage_records`, `run_events`, `UncheckedReport`; e-shop musí mít dokončenou úvodní analýzu (`shops.last_full_run_id`).
- **Změna 15 `add-shoptet-connector`:** `connector_events`, slučovač (`connector.coalesce`), načtení přes API (`connector.fetch_products`), dorovnání `connector.reconcile_changes` (každou hodinu a ve 2:00), `connector.sync_content` (3:00), běhy `connector_check` přes `IRunService` (P1, skrytý produkt P0) a `page_changes` se `source = webhook | save_hidden`.
- **Změna 4 `add-job-queue-and-worker`:** fronta, priority 0–4, vyhrazený podíl limitu Jevu pro P0–P1, zámek plánovače, `ops.schedules`.
- **Změna 5 `refactor-library-into-pipeline-steps`:** podmíněné stažení (ETag, Last-Modified), otisky vět, cache Jevu v PostgreSQL.
- **Změna 7 `add-places-of-sale-and-language-versions`:** technické znaky míst prodeje a verzí, rozbor LLM s ověřenými citacemi, síla důkazu.
- **Změna 9 `add-identity-and-tenants-api`:** `users.locale`, odesílání e-mailů z `ops.outbox`.
- **Změna 11 `add-findings-and-fixes-api`:** rozhodnutí o nálezech a opravách (přijmout, upravit, ponechat, doklad), ze kterých se plní paměť.
- **Změna 12 `add-billing-and-invoicing`:** stav předplatného e-shopu (`billing.subscriptions.status`).

## Done when

- Zátěžový test: noční běh 100 testovacích e-shopů (syntetické e-shopy s 200–5 000 stránkami, 2 % změněných stránek za noc, Jev nahrazený testovacím klientem s latencí 0,33 s a globálním limitem 1 200 požadavků/min) stihne okno se všemi 100 běhy ve stavu `finished`.
- Test „mění se jen změněné věty“: po změně k vět na m stránkách dostane Jev jen stavy těchto vět a jejich sousedů v kontextu, nové verze vzniknou jen u m stránek a nálezy ostatních stránek se nezmění.
- Test paměti rozhodnutí: stejná věta na novém produktu dostane schválenou opravu bez volání OpenAI a s jednou kontrolou Jevem; „ponechat“ nevytvoří otázku ani návrh.
- Test nového trhu: nová `hreflang` na `pl` a nová měna PLN vytvoří `shop_markets.status = unsupported` bez upozornění; nová česká verze u slovenského e-shopu vytvoří upozornění a do `runs.jurisdictions` dalšího běhu se nepřidá.
- Týdenní souhrn odejde jednou za týden každému příjemci v jeho jazyce a obsahuje počet nezkontrolovaných stránek s důvody.
- `dotnet test` projde.

## K rozhodnutí

1. **Frekvence podle tarifu** (architektura část 11, bod 3): návrh „denně změněné stránky a týdně celý web“ jako výchozí. Do rozhodnutí mají všechny tarify stejnou noční frekvenci a nastavení je v `system_settings.monitoring.frequency_by_tier`.
2. **Okno, rezerva a časové pásmo.** Architektura dává okno 0:00–6:00. Když minuta v okně vychází z hashe přes celých 360 minut, e-shop s minutou 5:59 nemá šanci okno stihnout. Návrh: minuta = hash % (360 − rezerva), rezerva `monitoring.slot_reserve_minutes` (návrh 120), časové pásmo `Europe/Bratislava` pro SK i CZ. Potvrdit.
3. **`ops.schedules` má PK `shop_id`, ale sloupec `kind` má tři hodnoty** (nightly, weekly_web, reconcile). Jeden e-shop s konektorem potřebuje aspoň dvě. Návrh: PK (`shop_id`, `kind`), doplní migrace této změny.
4. **`fixes.decision_memory` nemá vazbu na verzi pravidel**, ale „ponechat“ se má znovu ptát po změně zákona nebo pravidel (návrh 1a′). Návrh: sloupce `rule_id` a `rule_set_id` (verze modulu v době rozhodnutí); paměť platí jen pro stejnou verzi sady pravidel daného modulu.
5. **`content.pages` nemá `lastmod` ze sitemap**, bez kterého nejde poznat změněnou URL. Návrh: sloupec `sitemap_lastmod timestamptz` a snímek sitemap v úložišti.
6. **Překontrola po nové verzi pravidel** (`rule_update`, P4) a obnova profilů (`profile_refresh`) nemají v plánu 17 změn vlastníka, ačkoli strategie prodává „překontrolu stránek při změně zákona“. Návrh: samostatná změna po 16.
7. **Kdo zapisuje do paměti rozhodnutí:** změna 11 (při schválení, úpravě, zamítnutí, ponechání) nebo tato změna? Návrh: `DecisionMemoryWriter` dodá tato změna a změna 11 ho zavolá; pokud ho změna 11 už má, tato změna jen čte.
8. **Návrhy oprav k novým nálezům v noci:** generovat hned (~1 cent za stránku, architektura a návrh 1a) nebo až při otevření v aplikaci (levnější, návrh 1a)? Do rozhodnutí: v noci jen pro nové nálezy skupiny porušení, ostatní při otevření.
9. **Hranice změn 15 a 16.** Změna 15 zakládá běhy `connector_check` (i P0 pro skrytý produkt) a zapisuje `page_changes`. Tato změna k nim přidává paměť rozhodnutí, rozdíl otisků, výsledek změny (`new_violation` …) a upozornění. Kdo zpracuje kroky běhu `connector_check` nad texty z API, je otevřené i ve změně 8 (její K rozhodnutí 15). Potvrdit, aby se kód neduplikoval.
10. **Doklad podle značky bez otázky u nových produktů** (návrh 1a′) potřebuje značku produktu (JSON-LD `brand` nebo konektor). Mimo rozsah; kdy a kde?
11. **Automatické zveřejnění z paměti** (`auto_publish`, standardně vypnuté): mimo rozsah, dokud nebude zápis oprav přes konektor (změna 15) ověřený na pilotu.
12. **Týdenní souhrn:** který den a hodina? Návrh: pondělí po skončení nočního okna v časovém pásmu e-shopu.
13. **Odmítnuté místo prodeje:** upozornit znovu, když přijde silnější důkaz (např. dřív jen doručení, teď vlastní verze)? Do rozhodnutí: se stejnou nebo slabší silou důkazu se znovu neupozorňuje.
14. **Férové užití** (strategie: v ceně N změněných stránek měsíčně, N po pilotu): zatím se nic neomezuje, jen se počítá a nad prahem jde upozornění provozu.
15. **Co jsou „šablonové stránky“** pro každonoční kontrolu. Návrh: první vzorová stránka každého profilu šablony (`page_profiles.sample_urls`), aby se změna rámce, odznaků a patičky chytila do jedné noci.
16. **Sledování při `past_due`:** platby říkají, že se sledování pozastaví až po posledním neúspěšném pokusu. Návrh: `trialing`, `active` a `past_due` se sledují, `canceled`, `paused` a `incomplete` ne.
17. **Neměřeno:** podíl měněných stránek za den, podíl odpovědí 304 u skutečných e-shopů (Shoptet a další často posílají dynamické stránky bez ETag), jak často nové produkty opakují staré texty. Zátěžový test běží na syntetických datech; skutečné podíly změří pilot.
18. **Přísná priorita P2 před P3 v noci.** Výběr úloh řadí podle priority, takže úvodní analýza velkého e-shopu spuštěná večer (podle části 8 až 2,4 h celého limitu Jevu) odsune všechno noční sledování a okno se nestihne. Návrh: v nočním okně vyhradit P3 podíl limitu Jevu (`rate_limit_buckets.reserved`), např. 50 % z podílu P2–P4. Rozhodnutí patří i do změny 4.
19. **Chybějící sloupce pro idempotenci:** `iam.notifications` a `ops.outbox` nemají klíč proti dvojímu založení (upozornění za běh, souhrn za týden), `checks.page_changes` nemá jedinečnost (`run_id`, `page_id`, `change_kind`). Návrh: sloupec `dedupe_key text` s jedinečným indexem u obou tabulek a jedinečný index u `page_changes`.
20. **Nová jazyková verze:** návrh je verzi nekontrolovat, dokud ji klient nepotvrdí (`shop_languages.status = needs_confirmation`), a do potvrzení ji uvádět v souhrnu jako nezkontrolovanou. Alternativa: kontrolovat hned podle pravidla „kontroluje se každý text, který zákazník může vidět“ (architektura část 12) a cenu upravit od dalšího období. Rozhodnout.
21. **Pevné časy nočních úloh konektoru.** Změna 15 plánuje noční dorovnání ve 2:00 a `connector.sync_content` ve 3:00 pro všechny e-shopy. Architektura (část 4 a 5) chce noční práci rozloženou do okna podle e-shopu. Návrh: obě úlohy v `shops.monitor_slot_minute` e-shopu (ze stejného `MonitorSlot`). Sladit se změnou 15.
