# Delta for Site-analysis

## ADDED Requirements

### Requirement: Kroky analýzy s ukládatelným vstupem a výstupem
Systém MUST rozdělit analýzu e-shopu na kroky `DiscoveryStep`, `FetchStep`, `ExtractStep`, `ProfileStep`, `SegmentStep`, `EstimateStep`, `SieveStep`, `EvaluateStep`, `RulesStep` a `RewriteStep`. Každý krok MUST dostat celý svůj vstup jako serializovatelný záznam s polem `schema_version`, MUST vrátit výstup jako serializovatelný záznam a MUST NOT si držet stav mezi voláními mimo úložiště za rozhraním.

#### Scenario: Výstup kroku projde uložením a obnovou beze změny
- GIVEN výstup `ExtractStep` pro 10 stránek testovacího e-shopu `Fixtures/site-sk`
- WHEN se výstup serializuje přes `PipelineJson.Options`, deserializuje a předá do `SegmentStep`
- THEN `SegmentStep` vrátí stejné unikátní segmenty (otisk, text, kontext, URL) jako při předání objektu v paměti

#### Scenario: Neznámá verze schématu
- GIVEN uložený vstup `EvaluateBatchInput` s `schema_version` vyšší, než knihovna zná
- WHEN ho worker předá `EvaluateStep`
- THEN krok vyhodí `PipelineSchemaException` s názvem záznamu a verzí
- AND nezavolá Jev ani nezapíše nic do cache

#### Scenario: CLI spouští stejné kroky v paměti
- GIVEN příkaz `eshopguard scan https://vegis.sk --mock --replay snapshots/vegis.sk`
- WHEN běží
- THEN `IEshopGuard.ScanSiteAsync` zavolá kroky v pořadí Discovery, Fetch+Extract po stránkách, Profile (plán), Segment, Estimate, potvrzení, Profile (vytvoření), Sieve, Evaluate, Rules
- AND uživatel CLI nevidí žádnou změnu voleb ani výstupních souborů

### Requirement: Stahování po dávkách s uloženou frontou URL
Systém MUST stahovat stránky po dávkách z fronty URL (`UrlFrontier`), jejíž stav (`UrlFrontierState`) jde uložit přes `IUrlFrontierStore` a obnovit. Dávka MUST skončit po `crawl.fetch_batch_max_pages` stránkách (výchozí 100) nebo po `crawl.fetch_batch_max_seconds` (výchozí 60 s). Fronta MUST zachovat dnešní pravidla: robots.txt podle RFC 9309, Crawl-delay, User-Agent `EshopGuard/0.1`, právní stránky přednostně, střídání produktů a ostatních stránek, limity stránek a produktů, PDF právních stránek mezi nečtenými dokumenty.

#### Scenario: Obnova po pádu nezdvojí stránky
- GIVEN e-shop se 250 URL v sitemap a dávka po 100 stránkách
- WHEN proces spadne po uložení stavu fronty po druhé dávce a nový proces pokračuje z uloženého `UrlFrontierState`
- THEN celkem se stáhne každá URL nejvýš jednou
- AND seznam stránek a nálezů je stejný jako v běhu bez přerušení

#### Scenario: Limit produktů platí přes hranici dávek
- GIVEN `sample_products` = 100 a 300 produktových URL v sitemap
- WHEN stahování proběhne ve čtyřech dávkách
- THEN do analýzy se zařadí právě 100 produktových stránek
- AND `CrawlCounters.ProductOverLimit` uvádí zbytek stejně jako dnes `ScanStats.ProductPagesOverLimit`

#### Scenario: Tempo a Crawl-delay pokračují v další dávce
- GIVEN robots.txt s `Crawl-delay: 2` a první dávka, která skončí s tempem 0,5 požadavku za sekundu
- WHEN začne druhá dávka z uloženého `PaceState`
- THEN první požadavek druhé dávky nepřijde dřív než 2 s po posledním požadavku první dávky

#### Scenario: Nedostupný robots.txt zastaví stahování
- GIVEN server vrací na `/robots.txt` chybu 503 i po opakování
- WHEN proběhne `DiscoveryStep`
- THEN se nestáhne žádná další stránka webu
- AND výsledek nese upozornění, že web se podle RFC 9309 neprocházel

### Requirement: Podmíněné stažení stránky
Systém MUST poslat `If-None-Match` a `If-Modified-Since`, když `IPageStore` zná `ETag` nebo `Last-Modified` stránky. Odpověď 304 MUST znamenat „stránka se nezměnila“: stránka se znovu nevytěží, nezapočítá do volání Jevu a zůstane její poslední verze. Odpověď 304 MUST NOT skončit jako chyba ani jako přesměrování.

#### Scenario: Nezměněná stránka vrátí 304
- GIVEN stránka s uloženým `ETag` `"abc"` a server, který na `If-None-Match: "abc"` vrátí 304
- WHEN ji `FetchStep` stáhne
- THEN výsledek je `FetchOutcome.NotModified`
- AND `ExtractStep` ji nedostane a `IPageStore` nepřidá novou verzi

#### Scenario: Server validátory ignoruje
- GIVEN stránka s uloženým `ETag` a server, který vždy vrací 200
- WHEN ji `FetchStep` stáhne a `ExtractStep` vytěží
- THEN nová verze v `IPageStore` vznikne jen tehdy, když se změnil `TextHash` vytěženého textu

#### Scenario: Bez validátorů se stahuje jako dnes
- GIVEN běh CLI bez uložených stránek
- WHEN se stahuje
- THEN požadavky neobsahují `If-None-Match` ani `If-Modified-Since`
- AND výsledek je stejný jako před změnou

### Requirement: Ochrana proti SSRF při stahování
Systém MUST před každým spojením, včetně robots.txt, sitemap a každého kroku přesměrování, ověřit všechny adresy, na které vede název domény po DNS, a MUST odmítnout spojení, když je kterákoli z nich vnitřní, místní, linková nebo metadatová (minimálně 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8, 169.254.0.0/16 a obdoby v IPv6). Systém MUST povolit jen porty 80 a 443 a MUST se připojit přesně na ověřenou adresu. Odmítnutá adresa MUST být ve výsledku uvedená jako nezkontrolovaná.

#### Scenario: Doména vede na vnitřní adresu
- GIVEN uživatel zadá `https://intranet.example` a DNS vrátí 10.0.0.5
- WHEN `HttpPageFetcher` stahuje
- THEN žádné spojení se nenaváže
- AND odpověď má chybu `ssrf_blocked` a URL je v `ScanResult.BlockedUrls`

#### Scenario: Přesměrování na metadatovou adresu
- GIVEN veřejná stránka vrátí 302 na `http://169.254.169.254/latest/meta-data/`
- WHEN crawler následuje přesměrování
- THEN druhý krok se odmítne bez spojení a stránka se počítá jako nezkontrolovaná s důvodem `ssrf_blocked`

#### Scenario: Smíšená odpověď DNS a nestandardní port
- GIVEN doména, pro kterou DNS vrátí 93.184.216.34 a ::1, a jiná adresa `https://shop.example:8443/`
- WHEN se má stahovat
- THEN obě se odmítnou (první kvůli adrese ::1, druhá kvůli portu)

#### Scenario: Výjimka jen pro lokální testovací e-shop
- GIVEN příkaz `eshopguard scan http://localhost:8000 --allow-private-network`
- WHEN běží proti `serve-fixture`
- THEN stahování proběhne
- AND bez volby `--allow-private-network` se stejný příkaz odmítne s chybou `ssrf_blocked`

### Requirement: Jedno čtení HTML stránky
Systém MUST rozparsovat HTML stránky v `ExtractStep` jednou a z jednoho dokumentu spočítat hlavní text, rám, ostatní text, navigaci, výpisy jiných produktů, kontrolu vykreslení, odkazy, obrázky, meta, JSON-LD, kategorii, tokeny stavby a přiřazení uloženého profilu. Systém MUST NOT mazat uzly ze sdíleného dokumentu. Výsledné bloky textu MUST být stejné jako před změnou.

#### Scenario: Stejné bloky jako dřív
- GIVEN všechny stránky nahrávek vegis.sk a naturfyt.sk a obou testovacích e-shopů
- WHEN se vytěží starou a novou cestou
- THEN `MainBlocks`, `ChromeRegions`, `RestBlocks`, `NavigationChars`, `ListingChars`, `Render` a po přiřazení profilu `ProfileSkippedBlocks` jsou shodné

#### Scenario: Plán profilů nečte HTML znovu
- GIVEN 50 stránek bez profilu
- WHEN proběhne `ExtractStep` a `ProfileStep.PlanAsync`
- THEN počítadlo čtení `HtmlParserProbe` ukáže jedno naše čtení na stránku a žádné v plánu profilů

#### Scenario: Stránka, jejíž extrakce trvá příliš dlouho
- GIVEN stránka, jejíž extrakce překročí `crawl.extract_timeout_seconds`
- WHEN `ExtractStep` dávku zpracuje
- THEN stránka má `ExtractionStatus.NotProcessed` s důvodem `extract_timeout` a je v `ScanResult.NotProcessedPages`
- AND ostatní stránky dávky se zpracují normálně

### Requirement: Úložiště za rozhraním nezávislým na databázi
Systém MUST přistupovat k cache odpovědí Jevu, cache přepisů, profilům šablon, obsahu stránek (HTML a extrakce), záznamům stránek a jejich verzí s otisky vět, frontě URL a limitům volání jen přes rozhraní `IJevCache`, `IRewriteCache`, `IPageProfileStore`, `IPageContentStore`, `IPageStore`, `IUrlFrontierStore` a `IRateLimiter` v `EshopGuard.Core`. Knihovna `EshopGuard.Core` MUST NOT záviset na `EshopGuard.Data`, Npgsql ani EF Core. Implementace registrovaná hostitelem MUST nahradit výchozí.

#### Scenario: Core bez závislosti na databázi
- GIVEN sestavené `EshopGuard.Core.dll`
- WHEN test `CoreDependencyTests` projde jeho odkazované sestavy a `EshopGuard.Core.csproj`
- THEN mezi nimi není `EshopGuard.Data`, `Npgsql` ani `Microsoft.EntityFrameworkCore`

#### Scenario: Hostitel nahradí výchozí úložiště
- GIVEN hostitel zaregistruje vlastní `IPageStore` před `AddEshopGuard`
- WHEN se sestaví kontejner a proběhne `ExtractStep`
- THEN verze stránek zapisuje hostitelova implementace a `InMemoryPageStore` se nevytvoří

#### Scenario: CLI bez databáze
- GIVEN CLI bez připojení k PostgreSQL
- WHEN běží `scan`
- THEN použije `InMemoryPageContentStore`, `InMemoryPageStore`, `InMemoryUrlFrontierStore`, `LocalRateLimiter` a SQLite cache v `cache/jev-cache.sqlite`

#### Scenario: Každá implementace projde společné testy chování
- GIVEN abstraktní `StoreContractTests` pro `IPageStore`
- WHEN je zdědí `InMemoryPageStore` (a později implementace v `EshopGuard.Data`)
- THEN opakovaný zápis stejné verze nevytvoří druhý řádek a nová verze vznikne jen při změně `TextHash`

### Requirement: Klíč cache Jevu po částech a kompatibilní s dneškem
Systém MUST skládat klíč cache Jevu z druhu (`Detail`, `Sieve`), otisku sady otázek (`QuestionSetHash`: model, verze sady, jazyk otázek, kanonický JSON otázek) a otisku stavu (`StateHash`: kanonický JSON věty s kontextem nebo textu úseku). Systém MUST ke klíči spočítat i `LegacyKey` přesně podle dnešního `JevCacheKey.Create`, aby uložené odpovědi zůstaly platné. Změna textů pravidel bez změny otázek MUST NOT změnit `QuestionSetHash`.

#### Scenario: Dnešní cache zůstane platná
- GIVEN referenční seznam klíčů `Baselines/jev-legacy-keys.txt` zachycený dnešním kódem pro testovací e-shopy
- WHEN nový kód spočítá klíče stejných segmentů a úseků síta
- THEN každý `LegacyKey` se shoduje se seznamem
- AND běh nad `cache/jev-cache.sqlite` najde stejný počet odpovědí v cache jako před změnou

#### Scenario: Síto a podrobné otázky se nepletou
- GIVEN stejný text jako úsek síta a jako věta
- WHEN se spočítají klíče
- THEN mají různý `Kind` a implementace úložiště je může uložit do různých tabulek (`checks.sieve_answers`, `checks.jev_answers`)

#### Scenario: Dávkové čtení z cache
- GIVEN dávka 200 vět, z nichž 150 je v cache
- WHEN `EvaluateStep` čte cache
- THEN zavolá `IJevCache.GetManyAsync` jednou pro celou dávku a Jevu pošle jen požadavky pro 50 vět

### Requirement: Otisk věty pro verze stránek
Systém MUST ke každému segmentu spočítat 64bitový otisk normalizovaného textu bez kontextu (`SentenceFingerprint`) a ke každé zkontrolované stránce MUST vrátit seřazený seznam unikátních otisků jejích segmentů pro `page_versions.segment_hashes`.

#### Scenario: Stejný text, jiná velikost písmen a mezery
- GIVEN texty „Ekologický  šampon.“ a „ekologický šampon.“
- WHEN se spočítají otisky
- THEN jsou shodné

#### Scenario: Stejná věta v různém kontextu
- GIVEN stejná věta na dvou stránkách s různými sousedními větami
- WHEN se segmentuje
- THEN věta má dva segmenty s různým `Segment.Hash`, ale stejný `Segment.Fingerprint`
- AND obě stránky mají otisk ve svém seznamu

#### Scenario: Výstupy CLI se nemění
- GIVEN běh CLI nad testovacím e-shopem
- WHEN se zapíše `segments.csv`
- THEN soubor nemá nový sloupec a je shodný s referenčním

### Requirement: Odhad ceny jako samostatný krok
Systém MUST spočítat odhad volání Jevu, tokenů, ceny a nových profilů v kroku `EstimateStep` bez jakéhokoli placeného volání. Kroky MUST NOT volat callback pro potvrzení; callback `ScanOptions.ConfirmJevCalls` MUST volat jen běh pro CLI (`InMemoryPipelineRunner`) na stejném místě a za stejných podmínek jako dnes.

#### Scenario: Odhad nic nestojí
- GIVEN 3 000 segmentů a falešný klient Jevu, který počítá volání
- WHEN proběhne `EstimateStep`
- THEN klient nedostal žádný požadavek a `IRewriteClient` také ne
- AND `RunEstimate` obsahuje počet volání, volání z cache, tokeny, cenu a plánované profily s cenou

#### Scenario: Uživatel CLI odhad odmítne
- GIVEN `scan` bez `--yes` a odpověď „ne“ na dotaz po odhadu
- WHEN běh pokračuje
- THEN Jev ani OpenAI se nezavolají
- AND výstupy obsahují stažené stránky a segmenty a upozornění, že vyhodnocení nebylo potvrzeno, stejně jako dnes

#### Scenario: Worker použije odhad bez callbacku
- GIVEN `EstimateStep` volaný z workeru
- WHEN skončí
- THEN vrátí `RunEstimate` s `RequiresConfirmation` a nic nečeká

### Requirement: Jev po dávkách idempotentně
Systém MUST posílat Jevu věty a úseky síta po dávkách nejvýš 200 položek a každou odpověď MUST uložit do `IJevCache` hned po přijetí. Opakované spuštění stejné dávky MUST NOT poslat Jevu požadavek pro položku, jejíž odpověď už je v cache. Fatální chyba Jevu (odmítnutý klíč, došlý kredit) MUST zastavit dávku; jiná chyba MUST nechat položku nevyhodnocenou a vypsanou.

#### Scenario: Opakovaná dávka nic neplatí znovu
- GIVEN dávka 200 vět, která skončila po 120 odpovědích pádem procesu
- WHEN se dávka spustí znovu
- THEN Jev dostane požadavky jen pro 80 vět
- AND součet volání obou pokusů je 200

#### Scenario: Došlý kredit
- GIVEN Jev vrátí chybu, kterou `JevApiException.IsFatal` označí za fatální
- WHEN `EvaluateStep` zpracovává dávku
- THEN další požadavky se neodešlou a krok vyhodí výjimku s kódem služby
- AND odpovědi uložené před chybou v cache zůstanou

#### Scenario: Jednotlivá chyba věty
- GIVEN Jev vrátí 500 pro jednu větu i po opakováních
- WHEN dávka skončí
- THEN `EvaluateBatchResult.NotEvaluated` obsahuje její otisk
- AND pravidla u ní hlásí `RuleOutcome.NotEvaluated` a souhrn běhu uvádí počet chyb

### Requirement: Limit volání služeb za rozhraním
Systém MUST získat povolení od `IRateLimiter` před každým voláním Jevu a OpenAI. Výchozí `LocalRateLimiter` MUST omezit Jev stejně jako dnes (`jev.requests_per_minute` rozložené po sekundách) a OpenAI MUST NOT omezovat jinak než dnes. Hostitel MUST moct zaregistrovat vlastní limiter (globální čítač v databázi) bez změny kódu kroků.

#### Scenario: Výchozí limit jako dnes
- GIVEN `jev.requests_per_minute` = 1200 a 100 požadavků najednou
- WHEN je `EvaluateStep` odešle přes `LocalRateLimiter`
- THEN za žádnou sekundu neodejde víc než 20 požadavků

#### Scenario: Limiter hostitele
- GIVEN hostitel zaregistruje limiter, který pro `RequestPriority.P3` povolí jen 1 požadavek za sekundu
- WHEN dávka nočního sledování posílá věty
- THEN `JevClient` čeká na povolení hostitelova limiteru a vlastní token bucket nemá

#### Scenario: Zrušení během čekání na povolení
- GIVEN dávka čeká na povolení a běh se zruší
- WHEN se zruší `CancellationToken`
- THEN čekání skončí `OperationCanceledException` a žádný požadavek se neodešle

### Requirement: Výsledek CLI shodný s dneškem
Systém MUST po rozdělení na kroky dát na stejném vstupu stejný výsledek CLI jako před změnou. Porovnání MUST běžet offline nad nahrávkami odpovědí webu (`--replay`) a s falešným klientem Jevu (`--mock`) a MUST porovnat `findings.json`, `pages.jsonl`, `segments.csv`, `sieve.csv`, `profiles.json` a `report.md` po vynechání časů, dob a tempa.

#### Scenario: Testovací e-shopy beze změny
- GIVEN referenční výstupy dnešního kódu v `tests/EshopGuard.Core.Tests/Baselines/site` a `Baselines/site-sk`
- WHEN `PipelineEquivalenceTests` spustí nový kód nad `Fixtures/site` a `Fixtures/site-sk`
- THEN všechny porovnávané soubory jsou po normalizaci shodné

#### Scenario: Nahrávky vegis.sk a naturfyt.sk
- GIVEN nahrávky ve `snapshots/vegis.sk` a `snapshots/naturfyt.sk` a referenční výstupy v `baselines/`
- WHEN test kategorie `Snapshot` spustí nový kód s `--replay` a `--mock`
- THEN nálezy (pravidlo, text, URL, pásmo, skóre), stránky a segmenty jsou shodné

#### Scenario: Rozdíl se ukáže, ne schová
- GIVEN nový kód, který na jedné stránce vytěží o blok méně
- WHEN běží test shody
- THEN test selže a vypíše soubor, stránku a první odlišný řádek

#### Scenario: Chybějící nahrávka
- GIVEN složka `snapshots/vegis.sk` neexistuje
- WHEN běží testy
- THEN test kategorie `Snapshot` je označený jako přeskočený s důvodem „chybí nahrávka“, ne jako úspěšný
