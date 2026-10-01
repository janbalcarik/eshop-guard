# Delta for Analysis-runs

## ADDED Requirements

### Requirement: Stavový automat běhu
Systém MUST vést každý běh (`checks.runs`) stavovým automatem `queued → discovering → awaiting_payment → crawling → profiling → segmenting → evaluating → ruling → rewriting → finished | partial | failed | canceled`. Přechod MUST proběhnout jen z očekávaného stavu (`UPDATE … WHERE status = @from`), MUST zapsat událost `run.status` a konečné stavy `finished`, `partial`, `failed` a `canceled` se MUST NOT dál měnit. Ukázka zdarma (`kind = free_sample`) MUST přejít z `discovering` rovnou do `crawling`. Zrušení (`cancel_requested = true`) MUST být zjištěno nejpozději před začátkem další dávky.

#### Scenario: Ukázka zdarma projde stavy bez čekání na platbu
- GIVEN e-shop s 80 stránkami a běh `free_sample` ve stavu `queued`
- WHEN workery zpracují všechny úlohy běhu
- THEN běh projde stavy `discovering`, `crawling`, `profiling`, `segmenting`, `evaluating`, `ruling`, `rewriting` a skončí `finished` nebo `partial`
- AND stav `awaiting_payment` v `run_events` není
- AND pro každý přechod existuje právě jedna událost `run.status` s `from` a `to`

#### Scenario: Pozdní dávka nepovolený přechod neprovede
- GIVEN běh ve stavu `evaluating`
- WHEN opožděná dávka extrakce zkusí přechod `crawling → profiling`
- THEN `UPDATE` nezmění žádný řádek a stav zůstane `evaluating`
- AND nevznikne událost `run.status` ani úloha `run.profile`

#### Scenario: Zrušení mezi dávkami
- GIVEN úvodní analýza ve stavu `crawling` se 40 hotovými a 20 čekajícími dávkami stahování
- WHEN uživatel zavolá `RequestCancelAsync`
- THEN žádná další dávka stahování nepošle požadavek na e-shop
- AND čekající úlohy běhu přejdou do `canceled` a běh skončí `canceled` s událostí `run.canceled`
- AND stránky a verze stažené do té doby zůstanou v databázi

#### Scenario: Výsledek dávky po zrušení se nezapíše
- GIVEN běh ve stavu `canceled` a dávka Jevu, která běžela už před zrušením
- WHEN dávka dokončí volání a zkusí zapsat výsledek
- THEN transakce zjistí konečný stav běhu a nálezy ani průběh nezapíše
- AND odpovědi Jevu uložené do cache tenanta zůstanou (byly zaplacené)

### Requirement: Založení ukázky zdarma jednou na doménu
Systém MUST založit ukázku zdarma (`IRunService.CreateFreeSampleAsync`) jen tehdy, když doména e-shopu (bez `www`, malými písmeny) ještě nemá řádek v globální tabulce `shop.free_sample_claims`. Nárok (funkce `shop.claim_free_sample` se `SECURITY DEFINER`), běh ve stavu `queued` a úloha `run.discover` s prioritou P0 MUST vzniknout v jedné transakci. Plán ukázky MUST obsahovat nejvýš 100 stránek rozdělených mezi jazykové verze podporovaných míst prodeje podle plánu ze změny 7 (spárované produkty, povinné stránky každé verze, zbytek náhodné produkty po verzích); verze pro nepodporované trhy se MUST NOT stahovat.

#### Scenario: První nárok založí běh
- GIVEN doména `vegis.sk` nemá nárok na ukázku
- WHEN `CreateFreeSampleAsync` založí ukázku pro e-shop tenanta A
- THEN v jedné transakci vznikne řádek `free_sample_claims (domain='vegis.sk')`, běh `free_sample` ve stavu `queued` a úloha `run.discover` s prioritou 0
- AND když transakce selže, nevznikne ani jeden z těchto řádků

#### Scenario: Druhý nárok na stejnou doménu je odmítnut
- GIVEN doména `vegis.sk` už má nárok tenanta A
- WHEN tenant B založí e-shop `https://www.vegis.sk/` a požádá o ukázku
- THEN služba vrátí chybu s kódem `sample.already_used_for_domain`
- AND nevznikne běh ani úloha a tenant B se nedozví nic o tenantovi A

#### Scenario: Sto stránek napříč jazykovými verzemi
- GIVEN e-shop se slovenskou a českou verzí, obě pro podporovaná místa prodeje, a polskou verzí pro nepodporovaný trh
- WHEN skončí `run.discover` a `run.markets`
- THEN `checks.run_urls` obsahuje nejvýš 100 URL ve frontách `sample_pair`, `sample_mandatory` a `sample_random` jen ze slovenské a české verze
- AND každá z obou verzí má v plánu své povinné stránky (doprava, obchodní podmínky, reklamace), pokud je e-shop má
- AND polská verze není v plánu a klientovi se neukazuje

#### Scenario: Malý e-shop pod sto stránek
- GIVEN e-shop, jehož sitemapa a odkazy dají jen 37 stránek
- WHEN se sestaví plán ukázky
- THEN plán obsahuje všech 37 stránek a souhrn ukázky uvede 37 zkontrolovaných stránek
- AND nic se nedoplňuje ani neopakuje

### Requirement: Výstup ukázky zdarma
Systém MUST na konci ukázky uložit do `runs.stats.sample` počty nálezů podle závažnosti a podle `checkability` nejpřísnějšího verdiktu, identifikátory nejvýš 5 nejzávažnějších nálezů a nejvýš 1 ukázku opravy. Pořadí nálezů MUST jít podle `VerdictOrder.Compare` nad nejpřísnějším verdiktem (`Finding.Strictest`, změna 6), při shodě podle skóre, počtu výskytů a `rule_id`. Vysvětlení se MUST NOT ukládat jako hotový text; nález nese `rule_id` a parametry. Když ukázka opravy nevznikne, MUST být uveden důvod.

#### Scenario: Souhrn s pěti nejzávažnějšími a ukázkou opravy
- GIVEN ukázka našla 15 nálezů: 4 vysoké, 9 středních a 2 nízké závažnosti
- WHEN skončí `run.rules` a `run.rewrite`
- THEN `stats.sample.findings_by_severity` je `{high: 4, medium: 9, low: 2}`
- AND `top_finding_ids` obsahuje 5 nálezů seřazených podle `VerdictOrder`
- AND `example_fix_proposal_id` odkazuje na `fix_proposals` s původním textem, navrženým textem a `recheck_status = ok`

#### Scenario: Méně než pět nálezů
- GIVEN ukázka našla 3 nálezy
- WHEN se sestaví souhrn
- THEN `top_finding_ids` obsahuje právě 3 nálezy a nic se nedoplňuje

#### Scenario: Žádný nález nejde přepsat
- GIVEN všechny nálezy ukázky mají `scope = site` (chybějící povinná informace na webu)
- WHEN běh dojde ke kroku přepisu
- THEN OpenAI se nevolá
- AND `example_fix_proposal_id` je `null` a `example_fix_missing_reason` je `no_rewritable_finding`

#### Scenario: Přepsaný text nález nevyřeší
- GIVEN první kandidát na ukázku opravy po přepisu stále dává nález
- WHEN kontrola nového textu skončí
- THEN se zkusí další kandidát z pěti nejzávažnějších, nejvýš `free_sample.max_example_attempts` pokusů
- AND když žádný nevyjde, `example_fix_missing_reason` je `still_finding` a souhrn netvrdí, že oprava existuje

### Requirement: Základ rozsahu z ukázky a garantovaná cena
Systém MUST po ukázce uložit do `runs.estimate.basis` základ rozsahu: po jazykových verzích počet produktů, počet ostatních stránek, `counted`, důvod nezapočtení a podíl vlastních textů, a počet URL v sitemap. Pásmo ani částku tato změna MUST NOT počítat; počítá je `ShopScopeCalculator` (změna 10) a `IPriceQuoteService` (změna 12). Úvodní analýza MUST zkontrolovat vše, co najde, bez zastavení a bez doplatku, i když najde víc stránek nebo vlastních textů, a MUST jen zaznamenat interní náklad. Interní část odhadu (`estimate.internal`) a skutečný náklad se MUST NOT dostat do odpovědí pro zákazníka ani do `run_events`.

#### Scenario: Základ z ukázky
- GIVEN ukázka zjistila slovenskou verzi s 3 120 produkty a vlastními texty a českou verzi s 3 090 produkty, kde je přeložené jen menu
- WHEN skončí `run.finalize` ukázky
- THEN `estimate.basis.versions` obsahuje `sk` s `counted = true` a `cs` s `counted = false` a `not_counted_reason = menu_only_translation`
- AND `estimate.basis` neobsahuje pásmo ani částku

#### Scenario: Analýza najde víc, než ukázal vzorek
- GIVEN zaplacená úvodní analýza s interním odhadem 30 USD
- WHEN zjištění rozsahu najde o 40 % víc stránek a skutečný náklad je 45 USD
- THEN běh se nezastaví a zkontroluje všechny stránky
- AND skutečný náklad je v `usage_records` a nad prahem `runs.cost_alert_ratio` vznikne provozní upozornění `eshopguard.run.cost_over_estimate`
- AND běh nevytvoří žádnou platbu ani změnu objednávky

#### Scenario: Interní náklad zákazník nevidí
- GIVEN dokončená úvodní analýza s vyplněným `estimate.internal`
- WHEN API čte běh přes `RunReadModel`
- THEN výsledek neobsahuje `estimate.internal` ani žádnou částku v USD
- AND žádná událost `run_events` běhu neobsahuje interní cenu

### Requirement: Odhad interních nákladů před placeným voláním
Systém MUST před prvním placeným voláním Jevu nebo OpenAI v běhu spočítat a uložit odhad interních nákladů (`estimate.internal`): po zjištění rozsahu hrubě, po segmentaci jako horní mez z `EstimateStep`, u profilů z `ProfilePlan`, u přepisů z odhadu `RewriteStep`. U ukázky zdarma, kde zákazník nic neplatí, MUST souhlas s cenou představovat strop `system_settings.free_sample.max_internal_usd`; nad ním se placené kroky MUST NOT spustit.

#### Scenario: Odhad Jevu před prvním voláním
- GIVEN běh dokončil `run.segment`
- WHEN `run.estimate` zakládá dávky `run.sieve`
- THEN `estimate.internal.basis` je `segmented` a obsahuje `jev_calls_upper`, `jev_input_tokens` a `jev_usd`
- AND první úloha `run.sieve` vznikne až ve stejné transakci po zápisu odhadu

#### Scenario: Strop ukázky zdarma je překročen
- GIVEN strop `free_sample.max_internal_usd` a ukázka, jejíž odhad po segmentaci strop překročí
- WHEN `run.estimate` porovná odhad se stropem
- THEN žádné volání Jevu ani OpenAI nevznikne
- AND ukázka skončí `failed` s kódem `sample_budget_exceeded` a provoz dostane upozornění
- AND vzorek se tiše nezmenší

#### Scenario: Odhad profilů před voláním OpenAI
- GIVEN e-shop se 3 šablonami bez uloženého profilu
- WHEN začne `run.profile`
- THEN `estimate.internal.openai.profiles_usd` je uložený dřív, než odejde první požadavek na OpenAI
- AND když je profilování vypnuté nebo chybí klíč, stránky se kontrolují celé a statistika uvede počet stránek bez profilu

### Requirement: Úvodní analýza čeká na zaplacení
Systém MUST při založení úvodní analýzy (`IRunService.CreateFullAnalysisAsync`) ověřit vlastnictví přes `IShopOwnershipPolicy` (implementace změna 10); bez implementace se úvodní analýza MUST NOT založit. Úvodní analýza MUST po zjištění rozsahu přejít do `awaiting_payment` a MUST NOT stáhnout žádnou stránku nad rámec zjištění rozsahu, dokud `IRunPaymentGate` nepotvrdí zaplacenou objednávku nebo schválení administrátorem. Pokračování MUST založit první dávky stahování ve stejné transakci jako přechod do `crawling`. Zaplacení před koncem zjišťování rozsahu se MUST NOT ztratit. Schválení bez platby MUST být zapsané do `ops.audit_log`.

#### Scenario: Bez platby se nestahuje
- GIVEN úvodní analýza ve stavu `awaiting_payment` s objednávkou ve stavu `checkout_open`
- WHEN uplyne libovolná doba
- THEN neexistuje žádná úloha `run.fetch` běhu a e-shop nedostal žádný požadavek kromě robots.txt a sitemap

#### Scenario: Zaplacení posune běh
- GIVEN úvodní analýza ve stavu `awaiting_payment` a její objednávka přejde do `paid`
- WHEN změna 12 zavolá `MarkOrderPaidAsync(orderId)`
- THEN běh přejde do `crawling` a ve stejné transakci vzniknou první úlohy `run.fetch`
- AND druhé volání se stejnou objednávkou nic nezmění a nevrátí chybu
- AND volání s objednávkou ve stavu `created` vrátí `order_not_paid` a běh zůstane `awaiting_payment`

#### Scenario: Zaplaceno dřív, než skončí zjišťování rozsahu
- GIVEN úvodní analýza je ještě ve stavu `discovering` a její objednávka už je `paid`
- WHEN `MarkOrderPaidAsync` proběhne a potom skončí `run.discover`
- THEN běh projde `awaiting_payment` a ve stejné transakci přejde do `crawling`

#### Scenario: Schválení administrátorem bez platby
- GIVEN pilotní e-shop s úvodní analýzou ve stavu `awaiting_payment` bez objednávky
- WHEN administrátor zavolá `ApproveWithoutPaymentAsync(runId, adminId, důvod)`
- THEN běh přejde do `crawling`
- AND `ops.audit_log` obsahuje záznam `run.approved_without_payment` s administrátorem, důvodem a `run_id`

### Requirement: Kroky běhu po dávkách přes frontu
Systém MUST provádět běh jako řetěz úloh `run.discover`, `run.markets` (jen ukázka), `run.fetch`, `run.extract` (jen úvodní analýza), `run.profile`, `run.refit`, `run.segment`, `run.versions` (jen ukázka s víc verzemi), `run.estimate`, `run.sieve`, `run.plan_evaluate`, `run.evaluate`, `run.rules`, `run.rewrite` a `run.finalize` nad kroky změny 5, s dávkou do 100 stránek nebo 60 s u stahování, do 100 stránek u extrakce, 200 vět u Jevu a 25 stránek u přepisu. Pokračování dávky MUST jít na konec fronty. Celoplošné kroky MUST běžet pro jeden běh nejvýš jednou najednou (`concurrency_key`). Stahování MUST držet zámek domény v `ops.domains`, dodržet robots.txt včetně Crawl-delay, posílat User-Agent `EshopGuard/0.1` s kontaktem a MUST NOT se připojit na vnitřní, místní nebo metadatovou adresu.

#### Scenario: Velký e-shop neblokuje malý
- GIVEN tenant A má úvodní analýzu s 5 000 stránkami ve stavu `evaluating` a tenant B založí ukázku se 100 stránkami
- WHEN dva workery zpracovávají frontu
- THEN dávky Jevu obou běhů se ve frontě střídají
- AND ukázka tenanta B skončí dřív než analýza tenanta A

#### Scenario: Stejná doména u dvou tenantů
- GIVEN dva tenanti spustí úvodní analýzu e-shopu na stejné doméně
- WHEN workery zpracovávají jejich dávky stahování
- THEN v žádném okamžiku neběží dvě dávky stahování této domény současně
- AND každý běh si stránky stahuje sám a nevidí stránky ani HTML druhého tenanta

#### Scenario: Stránka zakázaná v robots.txt
- GIVEN robots.txt e-shopu zakazuje `/kosik/` a sitemapa obsahuje `/kosik/obsah`
- WHEN dávka stahování dojde k této URL
- THEN e-shop nedostane na tuto URL žádný požadavek
- AND `run_urls.state` je `robots_blocked` a URL je ve výčtu nezkontrolovaného

#### Scenario: Adresa vede do vnitřní sítě
- GIVEN odkaz v sitemap e-shopu přesměruje na adresu, jejíž DNS vrací `169.254.169.254`
- WHEN dávka stahování přesměrování následuje
- THEN spojení se nenaváže a `run_urls.state` je `ssrf_blocked`
- AND worker s `CrawlOptions.AllowPrivateNetwork = true` v konfiguraci odmítne start

### Requirement: Zápis stránek, verzí a otisků vět
Systém MUST pro každou staženou a zpracovanou stránku zapsat řádek `content.pages` (jedinečně podle `shop_id` a `url_hash`) a novou `content.page_versions` jen tehdy, když se `text_hash` liší od aktuální verze. Verze MUST nést `html_blob_key`, `extract_blob_key`, `text_hash`, počty znaků, `extraction_method`, `text_not_loaded` a `segment_hashes` jako seřazené jedinečné otisky `SentenceFingerprint.Of` všech segmentů stránky. Text vět se MUST NOT ukládat do samostatné tabulky; je v souboru extrakce a v nálezu.

#### Scenario: První stažení stránky
- GIVEN stránka produktu, kterou e-shop ještě nemá v `content.pages`
- WHEN dávka extrakce stránku zpracuje se známým profilem
- THEN vznikne řádek `pages` a jedna verze s `is_current = true`
- AND `segment_hashes` obsahuje otisky všech segmentů hlavního textu, rámce, ostatního textu, titulku, meta popisu a JSON-LD
- AND HTML a extrakce jsou v úložišti pod `tenants/{tenant}/shops/{shop}/pages/…`

#### Scenario: Stejný text při dalším běhu
- GIVEN stránka má aktuální verzi s `text_hash` X
- WHEN další běh stránku stáhne a extrakce dá stejný `text_hash` X
- THEN nevznikne nová verze
- AND posune se jen `pages.last_fetched_at` a `last_seen_at`

#### Scenario: Stránka čeká na nový profil
- GIVEN stránka patří šabloně, pro kterou se profil teprve vytvoří
- WHEN skončí `run.extract`
- THEN verze stránky ještě nevznikne
- AND po `run.refit` vznikne verze s `pages.profile_id` a `profile_unknown_share`

#### Scenario: Text stránky, která se nenačetla
- GIVEN stránka, jejíž text vykresluje JavaScript (`TextNotLoaded`)
- WHEN extrakce stránku zpracuje
- THEN verze má `text_not_loaded = true`, `pages.status = not_loaded` a `run_urls.state = not_loaded`
- AND stránka je ve výčtu nezkontrolovaného

### Requirement: Zápis nálezů s verdikty po zemích a návrhů oprav
Systém MUST zapsat nálezy do `checks.findings` s `rule_id`, `rule_set_id`, `module`, `checkability`, `severity`, `band`, `scope`, `segment_hash`, kopií textu, `score`, parametry a `verdicts` po všech jurisdikcích běhu (`runs.jurisdictions`). Stejná věta na více stránkách MUST dát jeden nález s výskyty v `checks.finding_occurrences`. Nález z dřívějšího běhu MUST zachovat `first_run_id` a stav a dostat `last_seen_run_id`. Návrhy oprav MUST být v `fixes.fix_proposals` s původním a navrženým textem, důvodem, zástupnými údaji, výsledkem kontroly nového textu, modelem a verzí pokynů.

#### Scenario: Nález pro dvě místa prodeje
- GIVEN e-shop s aktivními místy prodeje `sk` a `cz` a věta, kterou slovenská pravidla hodnotí jako porušení a česká jako „na posouzení“
- WHEN skončí `run.rules`
- THEN nález má v `verdicts` záznam pro `sk` i `cz` s vlastním `band`, `severity`, `checkability` a `legal_refs`
- AND sloupce `severity`, `checkability` a `band` nálezu odpovídají verdiktu `sk` (nejpřísnějšímu)

#### Scenario: Stejná věta na 38 stránkách
- GIVEN věta odznaku je na 38 stránkách a pravidlo na ní dává nález
- WHEN se nálezy zapíší
- THEN vznikne jeden nález s `occurrences = 38`
- AND `finding_occurrences` má 38 řádků s `page_id` a `block_index`

#### Scenario: Nález trvá z minulého běhu
- GIVEN nález ve stavu `approved` z ukázky zdarma
- WHEN úvodní analýza najde stejnou větu se stejným pravidlem
- THEN zůstane stejný řádek nálezu se stejným `first_run_id` a stavem `approved`
- AND `last_seen_run_id` je úvodní analýza

#### Scenario: Návrh opravy z přepisu
- GIVEN stránka se dvěma nálezy v hlavním textu
- WHEN dávka přepisu stránku zpracuje
- THEN `fix_proposals` obsahuje změnu s `original_text`, `proposed_text`, `reason`, `placeholders`, `recheck_status`, `model`, `prompt_version`, `created_run_id` a `status = proposed`
- AND změna, po které nález trvá, má `recheck_status = still_finding`

### Requirement: Idempotence a odolnost proti pádu workeru
Systém MUST zapisovat výsledek každé dávky, přičtení průběhu, bariéru a dokončení úlohy v jedné transakci, která se potvrdí jen s vlastním leasem a číslem pokusu. Opakovaná dávka MUST NOT vytvořit duplicitní řádky v `run_urls`, `pages`, `page_versions`, `findings`, `finding_occurrences`, `fix_proposals` ani `run_events` a další krok MUST vzniknout právě jednou. Odpovědi Jevu a OpenAI MUST být uložené do cache tenanta průběžně, takže pád workeru stojí nejvýš jednu dávku a znovu se zaplatí nejvýš volání, která byla v letu.

#### Scenario: Pád workeru v dávce Jevu
- GIVEN dávka 200 vět, ze které 120 odpovědí je uložených v `jev_answers`
- WHEN worker spadne a lease vyprší
- THEN jiný worker dávku převezme a pošle Jevu jen zbývající věty
- AND počty řádků nálezů a výskytů na konci běhu jsou stejné jako v běhu bez pádu
- AND součet `usage_records.calls` se od běhu bez pádu liší nejvýš o počet volání v letu (≤ `jev.concurrency`)

#### Scenario: Zaseknutý worker po převzetí úlohy jiným
- GIVEN worker 1 se zasekl a jeho úlohu po vypršení leasu převzal worker 2
- WHEN worker 1 se probudí a zkusí dávku dokončit
- THEN `CompleteAsync` vyhodí `LeaseLostException` a transakce workeru 1 nezapíše nic

#### Scenario: Celoplošný krok vznikne jednou
- GIVEN dvě poslední dávky extrakce běhu skončí ve stejném okamžiku na dvou workerech
- WHEN obě provedou bariéru
- THEN vznikne právě jedna úloha `run.profile` (`dedupe_key run:{id}:profile`)
- AND čítač `progress.extract.done` je roven počtu dávek

#### Scenario: Zabití workeru v každém druhu dávky
- GIVEN testovací e-shop `Fixtures/site-sk` a `WorkerHarness`, který zabije worker před potvrzením transakce
- WHEN se worker zabije postupně v dávce `run.fetch`, `run.extract`, `run.evaluate`, `run.rules` a `run.rewrite`
- THEN každý běh skončí stejným stavem a stejnými počty řádků `pages`, `page_versions`, `findings`, `finding_occurrences` a `fix_proposals` jako běh bez pádu

### Requirement: Samooprava dočasných chyb v kroku
Systém MUST dočasné chyby (Jev a OpenAI 408, 429, 5xx a vypršení času; e-shop 429 a 503; výpadek databáze nebo úložiště) opakovat automaticky v rámci kroku s rostoucím odstupem a MUST NOT po uživateli chtít běh spustit znovu. Fatální chyba služby (odmítnutý klíč, došlý kredit) MUST pozastavit úlohy dané třídy přes změnu 4, MUST NOT spotřebovat pokus úlohy a běh MUST po obnovení pokračovat. Celý běh se automaticky MUST NOT opakovat.

#### Scenario: Jev dočasně vrací 503
- GIVEN Jev vrací 503 po dobu 3 minut
- WHEN dávka Jevu vyčerpá opakování klienta
- THEN úloha se vrátí do fronty s `not_before` podle rostoucího odstupu
- AND po obnovení Jevu běh pokračuje a skončí bez zásahu uživatele

#### Scenario: Došel kredit OpenAI
- GIVEN OpenAI vrátí 429 `insufficient_quota` v dávce přepisu
- WHEN obsluha dostane `RewriteApiException` s `IsFatal = true`
- THEN všechny úlohy třídy `llm` se pozastaví a běh dostane událost `run.paused_internal` bez názvu dodavatele a bez částky
- AND `attempts` úlohy se nezvýší a po obnovení kreditu se dávka dokončí

#### Scenario: E-shop žádá o zpomalení
- GIVEN e-shop odpovídá 429 s `Retry-After: 30`
- WHEN dávka stahování dostane tuto odpověď
- THEN tempo se sníží, URL zůstane `pending` a zkusí se v pozdější dávce
- AND po `crawl.max_url_attempts` neúspěšných pokusech je URL `failed` s kódem `http_429`

#### Scenario: Dlouhý výpadek e-shopu
- GIVEN úvodní stránka e-shopu je nedostupná déle než `runs.site_outage_max_hours`
- WHEN uplyne tato doba
- THEN zbývající URL přejdou do `failed` s kódem `site_unreachable`
- AND běh dokončí kroky nad staženými stránkami a skončí `partial`

### Requirement: Částečný výsledek s výčtem nezkontrolovaného
Systém MUST ukončit běh stavem `partial`, když nebyla zkontrolována kterákoli plánovaná stránka nebo věta z jiného důvodu než robots.txt nebo filtr URL. Běh MUST uvést počty nezkontrolovaného podle důvodu v `runs.stats.unchecked` a úplný seznam v `checks.run_urls` a v `runs/{r}/unchecked.json.gz`. Stránky zakázané v robots.txt a vyřazené filtrem MUST být vyjmenované i u stavu `finished`. Běh, ve kterém nejde zkontrolovat žádnou stránku, MUST skončit `failed` s kódem.

#### Scenario: Část stránek selže
- GIVEN úvodní analýza, ve které 12 stránek po opakování vrátí 500 a 3 stránky překročí 5 MB
- WHEN skončí `run.finalize`
- THEN běh je `partial` a `stats.unchecked` obsahuje `{failed: 12, too_large: 3}`
- AND každá z 15 URL má v `run_urls` stav a kód chyby

#### Scenario: Věty bez odpovědi Jevu
- GIVEN 7 vět nedostalo odpověď Jevu ani po opakování
- WHEN se vyhodnotí pravidla
- THEN pravidla na těchto větách mají výsledek `NotEvaluated` a nevznikne z nich nález ani tvrzení, že jsou v pořádku
- AND běh je `partial` a výčet uvádí stránky, na kterých tyto věty jsou

#### Scenario: Jen stránky zakázané v robots.txt
- GIVEN všechny plánované stránky se zkontrolovaly kromě 4 zakázaných v robots.txt
- WHEN skončí `run.finalize`
- THEN běh je `finished`
- AND `stats.unchecked.robots_blocked` je 4 a URL jsou ve výčtu

#### Scenario: Nic nejde zkontrolovat
- GIVEN robots.txt e-shopu zakazuje celý web
- WHEN skončí `run.discover`
- THEN běh skončí `failed` s kódem `robots_disallow_all`
- AND žádná stránka se nestáhla a nevznikla žádná placená úloha

### Requirement: Průběh běhu pro živé zobrazení
Systém MUST každý přechod stavu, průběh kroku po dávce a konec běhu zapsat do `checks.run_events` s kódem a parametry a ve stejné transakci zavolat `pg_notify('run_events', run_id)`. Události MUST NOT obsahovat text stránek, interní ceny ani názvy dodavatelů služeb a MUST NOT obsahovat hotové věty pro uživatele (text skládá frontend z kódu). `IRunQueueEstimator` MUST vracet pozici běhu ve frontě a odhad dokončení z naměřené doby dávek; bez historie MUST vrátit `null`.

#### Scenario: Přechod stavu se oznámí po potvrzení
- GIVEN posluchač `LISTEN run_events`
- WHEN běh přejde z `crawling` do `profiling`
- THEN posluchač dostane oznámení s `run_id` až po potvrzení transakce
- AND v `run_events` je řádek `run.status` s `{from: "crawling", to: "profiling"}`
- AND vrácená transakce (ztráta leasu) nevytvoří řádek ani oznámení

#### Scenario: Události bez textů a cen
- GIVEN dokončená úvodní analýza testovacího e-shopu
- WHEN test projde všechny `run_events` běhu
- THEN žádné `data` ani `message` neobsahuje větu ze stránek, částku v USD ani slova „Jev“, „TypeSafe“ nebo „OpenAI“

#### Scenario: Pozice ve frontě a odhad dokončení
- GIVEN dvě ukázky zdarma jiných tenantů čekají ve stavu `queued` a byly založené dřív
- WHEN API zavolá `IRunQueueEstimator` pro novou ukázku
- THEN pozice je 2
- AND odhad dokončení se spočítá z průměrné doby dávek za poslední hodinu; bez dokončených dávek je `null`

### Requirement: Záznam spotřeby po dávkách
Systém MUST zapisovat interní spotřebu do `usage.usage_records` po dávkách s `tenant_id`, `shop_id`, `run_id`, `job_id`, `provider`, `model`, `operation`, `calls`, `cache_hits`, tokeny, `bytes_in` a `cost_usd`, a to ve stejné transakci jako uložené odpovědi. Cena MUST se počítat stejnými vzorci a ze stejné konfigurace jako CLI. Zákazník MUST NOT mít ke spotřebě přístup.

#### Scenario: Dávka Jevu zapíše spotřebu
- GIVEN dávka Jevu s 200 větami, z nichž 50 je v cache
- WHEN dávka skončí
- THEN součet záznamů dávky s `provider = jev`, `operation = sentence_eval` má `calls = 150` a `cache_hits = 50`
- AND `cost_usd` odpovídá `input_tokens × cost.usd_per_million_input_tokens / 10⁶`

#### Scenario: Vše z cache
- GIVEN druhý běh nad stejným textem, kde jsou všechny odpovědi v cache tenanta
- WHEN běh skončí
- THEN záznamy Jevu mají `calls = 0` a `cost_usd = 0`

#### Scenario: Spotřebu nevidí aplikace zákazníka
- GIVEN připojení rolí `eshopguard_app`
- WHEN dotaz čte `usage.usage_records`
- THEN databáze vrátí chybu oprávnění

### Requirement: Shoda výsledku workeru s CLI
Systém MUST dát přes workery stejné nálezy jako cesta CLI (`InMemoryPipelineRunner`) nad stejným snímkem webu a stejnými odpověďmi služeb: shodné jako množiny podle pravidla, rozsahu, otisku textu, množiny URL, verdiktů a skóre. Spotřeba MUST sedět: počty volání a tokenů po službách přesně, cena spočítaná ze součtu tokenů na 6 desetinných míst. Živé ověření na skutečném e-shopu MUST proběhnout až po odhadu ceny a souhlasu uživatele.

#### Scenario: Testovací e-shop offline
- GIVEN `Fixtures/site-sk`, `DeterministicTestJevClient` a `DeterministicTestRewriteClient`
- WHEN proběhne úvodní analýza přes `WorkerHarness` se 3 workery a zvlášť `InMemoryPipelineRunner` se stejnými moduly a jurisdikcemi
- THEN množiny nálezů jsou shodné
- AND součty volání a tokenů v `usage_records` se rovnají `ScanStats.JevCalls`, `InputTokens`, `ProfileCalls`, `ProfileInputTokens` a statistikám přepisu

#### Scenario: Živé srovnání na vegis.sk
- GIVEN uživatel schválil odhad ceny běhu vegis.sk a test běží s `ESHOPGUARD_LIVE_CONSENT=1`
- WHEN worker dokončí úvodní analýzu a cesta CLI pak čte stejný snímek HTML z úložiště přes `BlobSnapshotPageFetcher` a odpovědi z `PgJevCache` téhož tenanta
- THEN nálezy obou cest jsou shodné
- AND cesta CLI nezaplatí za Jev nic navíc (všechny odpovědi z cache)

#### Scenario: Rozdíl se nepřehlédne
- GIVEN worker dal o jeden nález víc než cesta CLI
- WHEN test porovná výsledky
- THEN test selže a vypíše rozdílný nález s pravidlem, otiskem textu a URL
- AND neexistuje tolerance na počet nálezů
