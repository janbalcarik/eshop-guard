# Delta for Monitoring

## ADDED Requirements

### Requirement: Plánovač nočního sledování rozložený do okna
Systém MUST pro každý sledovaný e-shop založit jednou za místní datum běh `monitoring` s prioritou 3 v minutě okna `shops.monitor_slot_minute`, která se počítá ze stabilního hashe ID e-shopu jako hash % (`window_minutes` − `slot_reserve_minutes`) v časovém pásmu `Europe/Bratislava`. Úloha MUST mít `dedupe_key = monitor:{shop}:{místní datum}`, takže dvojí založení nevznikne. Plánovač MUST běžet jen v instanci se zámkem plánovače a MUST jen zakládat úlohy. Sledovat se MUST jen e-shop ve stavu `active` s dokončenou úvodní analýzou (`last_full_run_id`) a předplatným ve stavu `trialing`, `active` nebo `past_due`.

#### Scenario: Minuta e-shopu je stabilní
- GIVEN e-shop s ID X a okno 0:00–6:00 s rezervou 120 minut
- WHEN se minuta spočítá v procesu A a po restartu v procesu B
- THEN obě hodnoty jsou stejné a leží v intervalu 0–239
- AND výpočet nepoužívá `GetHashCode`

#### Scenario: Dvojí založení nehrozí
- GIVEN dvě instance workeru a minuta e-shopu právě nastala
- WHEN obě zkusí plánovat ve stejné minutě a jedna z nich ještě jednou po restartu
- THEN existuje právě jeden běh `monitoring` a jedna úloha `monitor.plan` s `dedupe_key monitor:{shop}:{datum}`

#### Scenario: Noc změny času
- GIVEN noc 25. 10. 2026, kdy se hodina 2:00–3:00 místního času opakuje, a e-shop s minutou 2:30
- WHEN plánovač běží celou noc
- THEN pro datum 25. 10. 2026 vznikne jeden běh
- AND v noci posunu času vpřed se minuta v neexistující hodině posune na první platnou minutu

#### Scenario: E-shop, který se nesleduje
- GIVEN e-shop se zrušeným předplatným (`canceled`) a jiný e-shop bez dokončené úvodní analýzy
- WHEN nastane jejich minuta
- THEN běh se nezaloží a `ops.schedules.next_run_at` se posune na další noc

### Requirement: Dotažení zpožděných nocí a hlídání okna
Systém MUST po výpadku plánovače založit běh jen pro poslední zmeškanou noc a stránky, které se nekontrolovaly 7 dní, MUST zařadit do plánu (důvod `overdue`). Běh, který nestihne konec okna, se MUST NOT zrušit; MUST se dokončit, MUST vyvolat provozní metriku `eshopguard.monitoring.window_missed` a MUST mít `stats.finished_after_window = true`. Dva běhy sledování téhož e-shopu MUST NOT běžet současně.

#### Scenario: Plánovač dvě noci neběžel
- GIVEN plánovač nebyl dvě noci v provozu
- WHEN se znovu spustí
- THEN pro každý e-shop vznikne jeden běh pro poslední zmeškanou noc, ne dva
- AND plán obsahuje i stránky rotačních skupin zmeškaných nocí s důvodem `overdue`, pokud se nekontrolovaly 7 dní

#### Scenario: Běh nestihne okno
- GIVEN běh `monitoring` je v 6:00 ve stavu `evaluating`
- WHEN `MonitorWindowWatchdog` zkontroluje konec okna
- THEN běh pokračuje s prioritou 3 a skončí normálně
- AND provoz dostane metriku `window_missed` a běh má `stats.finished_after_window = true`

#### Scenario: Minulá noc ještě běží
- GIVEN běh sledování e-shopu z minulé noci ještě není v konečném stavu
- WHEN nastane minuta e-shopu další noci
- THEN nový běh se založí, ale jeho úloha `monitor.plan` se odkládá (`not_before`), dokud předchozí běh neskončí
- AND plán nové noci se sestaví až nad výsledkem předchozí noci

### Requirement: Výběr stránek na noc
Systém MUST u e-shopu bez konektoru sestavit plán noci z rozdílu sitemap (nové URL, novější `lastmod`, zmizelé URL k ověření), z úvodní, právních a šablonových stránek, z rotační skupiny noci (`pages.rotation_bucket = DayNumber % 7`), ze stránek s `next_check_at` v minulosti a ze stránek nekontrolovaných 7 dní. Každá URL MUST mít v `run_urls.reason` důvod. Každá aktivní stránka MUST být stažena aspoň jednou za 7 nocí. Nedostupná sitemapa MUST NOT způsobit, že se stránky považují za zmizelé.

#### Scenario: Rozdíl sitemap
- GIVEN sitemapa má oproti minulé noci 12 nových URL, 30 URL s novějším `lastmod` a 3 URL chybí
- WHEN skončí `monitor.plan`
- THEN plán obsahuje 12 URL s důvodem `sitemap_new`, 30 s `sitemap_lastmod` a 3 s `sitemap_removed`
- AND `pages.sitemap_lastmod` se aktualizuje až po úspěšném běhu

#### Scenario: Každá stránka aspoň jednou týdně
- GIVEN e-shop s 5 000 aktivními stránkami bez změn v sitemap
- WHEN proběhne 7 po sobě jdoucích nocí
- THEN každá aktivní stránka byla aspoň jednou v plánu
- AND úvodní stránka, právní stránky a první vzorová stránka každého profilu šablony byly v plánu každou noc

#### Scenario: Sitemapa je nedostupná
- GIVEN sitemapa vrátí 503
- WHEN se sestavuje plán
- THEN žádná stránka nedostane důvod `sitemap_removed` ani `status = gone`
- AND běh pokračuje s ostatními zdroji a výčet nezkontrolovaného uvede `sitemap_unavailable`

#### Scenario: Ověření zmizelé stránky
- GIVEN URL zmizela ze sitemap
- WHEN ji dávka stáhne a dostane 410
- THEN stránka má `status = gone`, její výskyty nálezů se odeberou a vznikne `page_changes.change_kind = removed`
- AND když místo toho dostane 500, stránka zůstane `active` s nálezy a dostane `next_check_at` na další noc

### Requirement: Podmíněné stažení při sledování
Systém MUST při sledování posílat `If-None-Match` s `pages.http_etag` a `If-Modified-Since` s `pages.http_last_modified`. Odpověď 304 MUST NOT spustit extrakci ani Jev. Odpověď 200 se stejným `text_hash` MUST NOT vytvořit novou verzi stránky. Robots.txt, Crawl-delay a User-Agent `EshopGuard/0.1` MUST platit stejně jako u úvodní analýzy.

#### Scenario: Stránka se nezměnila (304)
- GIVEN stránka s uloženým `ETag` a server, který na `If-None-Match` vrací 304
- WHEN ji noční dávka stáhne
- THEN `run_urls.state = not_modified`, posune se `pages.last_fetched_at`
- AND nevznikne úloha extrakce, verze ani volání Jevu pro tuto stránku

#### Scenario: Server bez ETag vrátí stejný text
- GIVEN server neposílá `ETag` ani `Last-Modified` a text stránky se nezměnil
- WHEN ji noční dávka stáhne a extrahuje
- THEN `text_hash` je stejný jako u aktuální verze a nová verze nevznikne
- AND Jev se pro stránku nevolá

#### Scenario: Text se změnil
- GIVEN server vrátí 200 s jiným textem
- WHEN extrakce dá nový `text_hash`
- THEN vznikne nová verze s `is_current = true`, starší ztratí `is_current`
- AND vznikne `page_changes.change_kind = text_changed`

### Requirement: Kontrola jen změněných vět
Systém MUST u změněné stránky poslat Jevu jen stavy (věta s kontextem), které nejsou v `checks.jev_answers` tenanta; nezměněné odpovědi MUST vzít z cache. Nálezy nezměněných stránek se MUST NOT měnit. Výskyty změněné stránky se MUST přepočítat a nález bez výskytu MUST dostat `status = resolved`. Rozdílové vyhodnocení pravidel MUST dát stejné nálezy jako plné vyhodnocení nad stejnými daty.

#### Scenario: Změna jedné věty
- GIVEN stránka s 40 větami a všechny stavy jsou v cache
- WHEN obchodník změní jednu větu uprostřed odstavce
- THEN Jev dostane nejvýš 5 stavů: změněnou větu a sousední věty, jejichž kontext se změnil (až 2 před a 2 za)
- AND ostatních 35 vět se nevyhodnocuje znovu

#### Scenario: Nezměněné stránky zůstávají
- GIVEN noc, ve které se změnily 3 stránky z 500
- WHEN běh skončí
- THEN nálezy a výskyty ostatních 497 stránek mají stejné hodnoty jako před během (kromě `last_seen_run_id` u stránek, které se stáhly)

#### Scenario: Věta s nálezem zmizí
- GIVEN nález na jediné stránce a k němu zveřejněná oprava (`publications.status = published`)
- WHEN noc zjistí, že věta na stránce už není
- THEN nález má `status = resolved`, `resolved_run_id` a `resolved_at`
- AND `page_changes.result = fix_confirmed`

#### Scenario: Rozdílový režim odpovídá plnému vyhodnocení
- GIVEN testovací e-shop po sérii 10 nocí s náhodnými změnami
- WHEN se nálezy po rozdílových během porovnají s plným vyhodnocením (`RuleScope.Full`) nad stejnými verzemi a odpověďmi
- THEN množiny nálezů a výskytů jsou shodné

### Requirement: Paměť rozhodnutí při sledování
Systém MUST před návrhem opravy porovnat nový nebo znovu nalezený nález s `fixes.decision_memory` podle otisku normalizovaného textu, pravidla a verze sady pravidel modulu. Rozhodnutí `replace` MUST vytvořit návrh se stejným náhradním textem bez volání OpenAI a MUST ho zkontrolovat Jevem v okolí nové stránky. Rozhodnutí `keep` MUST nález uzavřít jako `kept` bez otázky a bez návrhu. Rozhodnutí `keep_with_evidence` MUST platit jen s platným dokladem. Záznam z jiné verze sady pravidel se MUST NOT použít.

#### Scenario: Stejný text na novém produktu
- GIVEN obchodník schválil náhradu věty „100 % ekologický obal“ a v noci přibyl produkt se stejnou větou
- WHEN běh dojde ke kroku přepisu
- THEN vznikne `fix_proposals` s `proposed_text` z paměti a `model = decision_memory`
- AND OpenAI se nevolá a Jev dostane jednu kontrolu nového textu v okolí nové stránky
- AND když kontrola projde, `recheck_status = ok`; když ne, `still_finding` a nález zůstane `open`

#### Scenario: Ponechat bez otázky
- GIVEN obchodník u věty rozhodl „ponechat“ při stejné verzi sady pravidel
- WHEN se věta objeví na další stránce
- THEN nález má `status = kept`
- AND nevznikne otázka ani návrh opravy

#### Scenario: Doklad propadl
- GIVEN rozhodnutí „ponechat s dokladem“ a doklad má `status = expired`
- WHEN se věta objeví na nové stránce
- THEN nález má `status = needs_answer` a otázka se položí znovu

#### Scenario: Nová verze pravidel
- GIVEN záznam paměti vznikl při verzi sady pravidel modulu `eco` 1.4 a aktuální je 1.5
- WHEN se věta objeví znovu
- THEN paměť se nepoužije a nález je `open`

### Requirement: Upozornění na nové místo prodeje nebo jazykovou verzi
Systém MUST při sledování porovnat technické znaky úvodní a právních stránek (`html lang`, `hreflang`, odkazy přepínače, měny ve strukturovaných datech) se známými místy prodeje a verzemi. Nová verze nebo silný znak země podporovaného trhu MUST vytvořit upozornění „Vyzerá to, že predávate aj …“ (kód `market_suggested` nebo `language_suggested`). Trh ani verze se MUST NOT přidat do kontroly bez potvrzení klienta. Země nepodporovaného trhu se MUST uložit skrytě jako `unsupported` bez upozornění. Placený rozbor LLM MUST mít před voláním odhad ceny.

#### Scenario: Nová česká verze slovenského e-shopu
- GIVEN slovenský e-shop s jedinou verzí a aktivním místem prodeje `sk`
- WHEN úvodní stránka nově obsahuje `hreflang="cs"` na `/cz/`
- THEN vznikne `shop_languages (language = cs, status = needs_confirmation, source = hreflang)` a upozornění `language_suggested`
- AND verze se do potvrzení nekontroluje a týdenní souhrn ji uvede jako nezkontrolovanou

#### Scenario: Znak nepodporované země
- GIVEN e-shop nově uvádí ceny v PLN a `hreflang="pl"`
- WHEN běží `monitor.signals`
- THEN vznikne `shop_markets (country_code = pl, status = unsupported)`
- AND nevznikne upozornění ani volání LLM

#### Scenario: Silný znak podporované země
- GIVEN slovenský e-shop nově uvádí ceny v Kč a má vlastní doménu `.cz` v `hreflang`
- WHEN běží `monitor.market_check` po uložení odhadu ceny
- THEN vznikne `shop_markets (country_code = cz, status = suggested, source = detected)` s ověřenými citacemi v `evidence` a upozornění `market_suggested`
- AND `runs.jurisdictions` dalšího běhu obsahuje jen `sk`

#### Scenario: Odmítnutý trh se znovu nenabízí
- GIVEN klient odmítl místo prodeje `cz` s důkazem úrovně doručení
- WHEN se při sledování objeví stejný nebo slabší důkaz
- THEN nevznikne nové upozornění a stav zůstane `declined`

### Requirement: Změny z konektoru ve sledování
Systém MUST zpracovat běhy `connector_check` ze změny 15 stejnou cestou jako noční změny: rozdíl otisků vět, paměť rozhodnutí, výsledek změny v `page_changes` a upozornění; u produktu uloženého jako skrytý nebo koncept MUST upozornění vzniknout hned po běhu. E-shop s konektorem MUST NOT mít noční procházení webu, kromě stránek k opakování (`retry`), a plánovač mu MUST jednou týdně založit běh `weekly_web`, který podmíněně projde celý web. Když konektor hlásí, že změny nepřicházejí, MUST se e-shopu zapnout noční procházení webu, dokud se konektor neobnoví.

#### Scenario: Změna produktu z webhooku
- GIVEN e-shop s konektorem a změna 15 vytvoří běh `connector_check` pro produkt č. 2429
- WHEN běh skončí
- THEN Jev dostal jen stavy změněných vět popisu
- AND `page_changes` se `source = webhook` má výsledek podle nálezů a paměť rozhodnutí se použila stejně jako v noci

#### Scenario: Týden e-shopu s konektorem
- GIVEN e-shop s připojeným a zdravým konektorem
- WHEN uplyne týden bez selhaných stránek
- THEN plánovač založil 1 běh `weekly_web` a žádný noční běh webu
- AND běh `weekly_web` měl v plánu všechny aktivní stránky webu s podmíněným stažením

#### Scenario: Uložený skrytý produkt
- GIVEN e-shop s `check_hidden_on_save = true` a změna 15 založí běh `connector_check` s prioritou 0 pro uložený skrytý produkt
- WHEN běh skončí
- THEN `page_changes` se `source = save_hidden` a `change_kind = hidden_saved` má výsledek podle nálezů
- AND upozornění `new_violation` vznikne hned, ne až v nočním běhu nebo v souhrnu

#### Scenario: Konektor přestal doručovat změny
- GIVEN změna 15 označí konektor e-shopu jako nezdravý (webhooky nechodí, dorovnání selhává)
- WHEN nastane minuta e-shopu
- THEN plánovač založí noční běh webu jako u e-shopu bez konektoru
- AND po obnovení konektoru se noční běh webu znovu vypne

### Requirement: Priority úloh sledování
Systém MUST zakládat úlohy sledování s prioritami: 0 kontrola uloženého skrytého produktu (běh změny 15 a jeho zpracování), 1 ostatní změny z konektoru, 3 noční běh, týdenní procházení webu a rozbor nového trhu, 4 týdenní souhrn a úklid. Úlohy s prioritou 3 a 4 MUST NOT čerpat podíl limitu Jevu vyhrazený prioritám 0 a 1.

#### Scenario: Kontrola uloženého produktu předběhne noc
- GIVEN fronta Jevu plná dávek nočního sledování s prioritou 3
- WHEN přijde běh `connector_check` s prioritou 0 a jeho dávka Jevu a `monitor.memory`
- THEN worker je vezme jako další úlohy třídy `jev` a dokončí je do 30 s

#### Scenario: Noc nečerpá vyhrazený podíl
- GIVEN globální limit Jevu 1 200 požadavků/min a podíl P2–P4 80 %
- WHEN běží jen noční sledování
- THEN noční dávky za minutu nerezervují víc než 960 požadavků

#### Scenario: Souhrn běží jen při volné kapacitě
- GIVEN fronta třídy `system` obsahuje úlohy priority 3 i týdenní souhrn s prioritou 4
- WHEN worker vybírá úlohu
- THEN vezme nejdřív úlohy priority 3

### Requirement: Upozornění na nové porušení
Systém MUST po běhu sledování nebo kontroly z konektoru s aspoň jedním výsledkem `new_violation` založit jedno upozornění v aplikaci (`iam.notifications`, `kind = new_violation`, počty podle závažnosti) a e-mail členům se zapnutým `notification_settings.email_new_violation` v jejich jazyce (`users.locale`). Upozornění MUST mít `dedupe_key` podle běhu, takže opakované dokončení nevytvoří druhé. Stránka, která se nedá zkontrolovat `monitoring.max_failed_nights` nocí po sobě, MUST vyvolat upozornění `monitoring_unchecked`.

#### Scenario: Tři nová porušení v jedné noci
- GIVEN noční běh našel 3 nová porušení na 2 stránkách
- WHEN skončí `run.finalize`
- THEN vznikne jedno upozornění `new_violation` s `{high: …, medium: …, low: …}` a odkazem na běh
- AND členové s `email_new_violation = true` dostanou jeden e-mail v jazyce svého rozhraní

#### Scenario: Noc bez nových porušení
- GIVEN noční běh našel jen nálezy k posouzení (`new_assess`)
- WHEN běh skončí
- THEN upozornění `new_violation` nevznikne

#### Scenario: Opakované dokončení běhu
- GIVEN `run.finalize` spadl po založení upozornění a zpracuje se znovu
- WHEN se upozornění zakládá podruhé
- THEN existuje stále jedno upozornění a jeden e-mail na příjemce

#### Scenario: Stránka nejde zkontrolovat tři noci
- GIVEN obchodní podmínky vrací 500 tři noci po sobě a `monitoring.max_failed_nights = 3`
- WHEN skončí třetí noční běh
- THEN vznikne upozornění `monitoring_unchecked` s odkazem na stránku
- AND nálezy této stránky zůstanou beze změny

### Requirement: Týdenní souhrn e-mailem
Systém MUST jednou týdně poslat každému členovi se zapnutým `email_weekly_summary` souhrn sledovaných e-shopů v jeho jazyce: noci (plánované, dokončené, částečné, zmeškané, dokončené po okně), stránky (zkontrolované, beze změny, změněné, nové, zmizelé), nové nálezy podle závažnosti, vyřešené nálezy, nálezy čekající na rozhodnutí, nezkontrolované stránky podle důvodu, stránky nekontrolované déle než 7 dní a navržená místa prodeje. Souhrn MUST odejít i v týdnu beze změn a MUST mít `dedupe_key` podle tenanta, uživatele a týdne.

#### Scenario: Souhrn s nezkontrolovanými stránkami
- GIVEN týden, ve kterém 4 stránky nešly stáhnout a 1 jazyková verze čeká na potvrzení
- WHEN se sestaví souhrn
- THEN souhrn uvádí `unchecked.failed = 4` a `unconfirmed_language_versions = 1`
- AND netvrdí, že je web zkontrolovaný celý

#### Scenario: Týden beze změn
- GIVEN týden, ve kterém se na e-shopu nic nezměnilo
- WHEN nastane čas souhrnu
- THEN e-mail odejde s nulovými změnami a počtem zkontrolovaných stránek

#### Scenario: Čeština pro slovenský e-shop
- GIVEN uživatel s `locale = cs` spravuje slovenský e-shop
- WHEN se sestaví e-mail
- THEN texty a formát čísel a dat jsou česky a odkazy na zákon zůstávají v jazyce zákona

#### Scenario: Dvojí spuštění souhrnu a vypnuté nastavení
- GIVEN úloha `monitor.weekly_summary` se zpracuje dvakrát a jeden člen má `email_weekly_summary = false`
- WHEN obě zpracování skončí
- THEN každý člen se zapnutým nastavením má v `ops.outbox` právě jeden e-mail za týden
- AND člen s vypnutým nastavením nedostane nic

### Requirement: Odhad nákladů noci
Systém MUST po rozdílové segmentaci a před první dávkou Jevu nebo LLM uložit do `runs.estimate.internal` odhad nákladů noci. Nad prahem `monitoring.alert_usd_per_shop_night` MUST vzniknout provozní upozornění; běh se MUST NOT zastavit ani zmenšit. Zákazník MUST NOT interní náklad vidět.

#### Scenario: Odhad před prvním voláním
- GIVEN noční běh se 40 změněnými stavy vět
- WHEN se zakládá první dávka síta nebo Jevu
- THEN `estimate.internal` s počtem volání a cenou je už uložený

#### Scenario: Neobvykle drahá noc
- GIVEN e-shop, kde obchodník v noci hromadně přepsal 3 000 popisů
- WHEN odhad noci překročí práh
- THEN provoz dostane metriku `eshopguard.monitoring.cost_over_threshold`
- AND běh zkontroluje všechny změněné stránky

#### Scenario: Vše z paměti a cache
- GIVEN všechny změněné věty noci jsou v cache Jevu a jejich nálezy mají záznam v paměti rozhodnutí
- WHEN běh skončí
- THEN `usage_records` noci má pro OpenAI `calls = 0` a pro Jev jen kontroly náhrad v novém okolí

### Requirement: Výčet nezkontrolovaného při sledování
Systém MUST každou stránku, kterou noc měla zkontrolovat a nezkontrolovala, ponechat s jejími nálezy, nastavit jí `next_check_at` na další noc, uvést ji ve výčtu nezkontrolovaného běhu a v týdenním souhrnu a ukončit běh stavem `partial`. Chyba stažení MUST NOT znamenat „beze změny“ ani „zmizelo“.

#### Scenario: Stránka v noci selže
- GIVEN dávka nemůže stáhnout 2 stránky ani po opakování
- WHEN skončí `run.finalize`
- THEN běh je `partial` se `stats.unchecked.failed = 2`
- AND obě stránky mají `next_check_at` na další noc a jejich nálezy se nezměnily

#### Scenario: Stránka nekontrolovaná déle než týden
- GIVEN stránka, kterou se nepodařilo stáhnout 8 dní
- WHEN se sestaví týdenní souhrn
- THEN souhrn ji započítá do `pages_unchecked_over_7_days`
- AND další plán noci ji obsahuje s důvodem `overdue`

#### Scenario: Robots.txt nově zakazuje část webu
- GIVEN e-shop nově zakázal v robots.txt `/blog/`
- WHEN noc dojde ke stránkám blogu
- THEN stránky se nestahují, mají `run_urls.state = robots_blocked` a jsou ve výčtu nezkontrolovaného
- AND jejich nálezy zůstanou se stavem z poslední kontroly

### Requirement: Kapacita nočního okna
Systém MUST zvládnout noční sledování 100 testovacích e-shopů v okně 0:00–6:00 na jednom serveru podle architektury (část 7.1): všechny běhy MUST skončit v konečném stavu před koncem okna. Zátěžový test MUST běžet bez placených volání a MUST zapsat změřené hodnoty (doba nejdelšího běhu, volání Jevu, procesor na stránku, podíl 304).

#### Scenario: Sto e-shopů v okně
- GIVEN 100 syntetických e-shopů (60 × 500, 30 × 2 000, 10 × 5 000 stránek), 2 % změněných stránek za noc, polovina serverů s `ETag`, Jev s latencí 0,33 s a limitem 1 200 požadavků/min
- WHEN proběhne noc s oknem 360 minut
- THEN všech 100 běhů skončí `finished` před koncem okna
- AND počet volání Jevu odpovídá počtu změněných stavů vět

#### Scenario: Zkrácená varianta pro průběžné testy
- GIVEN 10 syntetických e-shopů po 500 stránkách a okno zkrácené na 30 minut se stejnou rezervou v poměru
- WHEN proběhne `NightWindowLoadTests` v kategorii `Load`
- THEN všechny běhy skončí před koncem zkráceného okna

#### Scenario: Okno se nestihne
- GIVEN test s uměle sníženým limitem Jevu na 100 požadavků/min
- WHEN noc proběhne
- THEN test zaznamená metriku `window_missed`, běhy se dokončí po okně a žádný není zrušený
