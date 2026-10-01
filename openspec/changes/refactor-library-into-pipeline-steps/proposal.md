# Proposal: Knihovna rozdělená na kroky analýzy pro worker po dávkách

## Intent

**Problém.** Celý sken je dnes jedno dlouhé volání `EshopGuardService.ScanSiteAsync` v paměti:
- `Crawler.CrawlAsync` stáhne a vytěží všechny stránky najednou a drží je v paměti (`CrawlResult.Pages`, HTML v `CrawledPage.CompressedHtml`). Fronta URL (`Crawler.Run._legalQueue`, `_productHintQueue`, `_otherQueue`, `_visited`) žije jen v objektu `Run`, takže pád procesu zahodí celé stahování.
- Profily, segmenty, odhad, síto, Jev a pravidla následují v jedné metodě. Potvrzení ceny je callback uprostřed skenu (`ScanOptions.ConfirmJevCalls`).
- `HttpPageFetcher` stáhne libovolnou adresu, kterou uživatel zadá, včetně vnitřních adres (10.x, 127.x, 169.254.169.254). Architektura část 7: „Bez toho nejde web spustit.“
- Stránka se čte jako HTML opakovaně: `ContentExtractor.Extract` (vlastní `HtmlParser`), SmartReader (parsuje znovu), `ContentExtractor.ReadRemainder` (třetí `HtmlParser` nad čerstvou kopií), `PageProfiler.PrepareAsync` (čtvrté čtení) a `PageProfiler.Fit` → `ProfileMatcher.Apply(Parse(...))` (páté). Změřeno 1. 10. 2026: 0,25–0,28 s procesoru na stránku; při 3 000 e-shopech ~180 jádrohodin za noc (databázový návrh část 5).
- Cache Jevu, přepisů a profilů jsou za rozhraním, ale klíč cache Jevu je jeden neprůhledný řetězec (`JevCacheKey.Create`), takže ho nejde uložit do `checks.jev_answers` s primárním klíčem (`tenant_id`, `question_set_hash`, `state_hash`). Stránky, jejich verze a otisky vět žádné úložiště nemají.

**Co změna přinese.**
- Worker (změna 8) spustí každý krok jako úlohu nad dávkou: pád stojí nejvýš jednu dávku, velký e-shop neblokuje malé (architektura část 5, „Úlohy po dávkách“).
- Web je chráněný proti SSRF už v knihovně, takže na to nemůže zapomenout žádná vrstva.
- Noční sledování (změna 16) dostane podmíněné stažení: nezměněná stránka vrátí 304 a nic se nepočítá.
- Jedno čtení HTML místo pěti. Úspora procesoru je **neměřená**; změří se před změnou a po ní na stejných stránkách (úkoly 1.8 a 9.4).
- CLI se pro uživatele nemění: kroky běží v paměti za sebou a výsledek je stejný jako dnes.

**Fáze:** F3 Knihovna po krocích.

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`: část 9 „Co se musí změnit v knihovně“ (body 1–6), část 5 „Úlohy po dávkách“ a „Globální limity“, část 6 „Průběh analýzy a schválení ceny“, část 7 (SSRF, nepřátelské HTML), část 8 (CPU 0,25–0,28 s na stránku, „tři čtení HTML“);
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.2 (`page_profiles`), 3.3 (`pages`, `page_versions`, 64bitové otisky vět, tabulka vět záměrně neexistuje), 3.4 (`jev_answers`, `sieve_answers`, `rewrite_cache` ve 3.5), část 5 bod 2 (stahování odděleně od zpracování), část 7 (`EshopGuard.Core/Pipeline`), část 8 fáze F3;
- kód: `src/EshopGuard.Core/EshopGuardService.cs`, `Crawl/Crawler.cs`, `Crawl/HttpPageFetcher.cs`, `Crawl/IPageFetcher.cs`, `Extract/ContentExtractor.cs`, `Extract/RenderCheck.cs`, `Profiles/PageProfiler.cs`, `Profiles/ProfileMatcher.cs`, `Profiles/PageProfileStore.cs`, `Cache/IJevCache.cs`, `Cache/JevCacheKey.cs`, `Cache/SqliteJevCache.cs`, `Fix/RewriteCache.cs`, `Rules/PageSieve.cs`, `Rules/SegmentEvaluator.cs`, `Jev/JevClient.cs`.

Cesty v této změně jsou uvedené po přejmenování ze změny 1 (`EshopGuard.*` → `EshopGuard.*`, `IEshopGuard` → `IEshopGuard`, `AddEshopGuard` → `AddEshopGuard`).

## Scope

In scope:
- Jmenný prostor `EshopGuard.Core.Pipeline` s kroky `DiscoveryStep`, `FetchStep`, `ExtractStep`, `ProfileStep` (plán, vytvoření, nové přiřazení), `SegmentStep`, `EstimateStep`, `SieveStep`, `EvaluateStep` (Jev po dávkách), `RulesStep`, `RewriteStep`. Každý krok má vstup a výstup jako serializovatelné záznamy (System.Text.Json, pole `schema_version`).
- `UrlFrontier`: fronta URL se stavem, který jde uložit a obnovit (`UrlFrontierState`), za rozhraním `IUrlFrontierStore`. Stahování vydává stránky průběžně po dávkách (do 100 stránek nebo 60 s).
- Podmíněné stažení: `FetchRequest` s `If-None-Match` a `If-Modified-Since`, `FetchResponse` s `ETag`, `Last-Modified` a výsledkem 304 `NotModified`.
- Ochrana proti SSRF v `HttpPageFetcher`: povolené jen porty 80 a 443, kontrola všech adres po DNS a připojení přímo na ověřenou adresu (`SocketsHttpHandler.ConnectCallback`), u každého kroku přesměrování, i pro robots.txt a sitemap. Výjimka jen pro lokální testovací e-shop volbou `--allow-private-network`.
- Jedno čtení HTML stránky (`ParsedPage`): extrakce, kontrola vykreslení, ostatní text, tokeny stavby a přiřazení uloženého profilu bez mazání uzlů z dokumentu. SmartReader zůstane, dokud se nezměří jinak (K rozhodnutí 6).
- Časový limit extrakce jedné stránky; stránka nad limit má stav `not_processed` a objeví se ve zprávě.
- Rozhraní úložišť v `EshopGuard.Core.Storage`: `IJevCache` se strukturovaným klíčem `JevCacheKey` (`QuestionSetHash`, `StateHash`, druh `Detail`/`Sieve`, `LegacyKey`), `IRewriteCache`, `IPageProfileStore`, nové `IPageContentStore` (HTML a extrakce), `IPageStore` (stránky, verze, validátory, otisky vět), `IUrlFrontierStore` a `IRateLimiter`. Výchozí implementace pro CLI: v paměti a dnešní SQLite jen **přechodně**. Změna 5b `replace-local-cache-with-postgres` přepne CLI na PostgreSQL a SQLite odstraní (požadavek uživatele 1. 10. 2026: cache jen v PostgreSQL, stejně pro CLI i aplikaci).
- 64bitový otisk věty `SentenceFingerprint` (pro `page_versions.segment_hashes` a pro porovnání jazykových verzí ve změně 7).
- Odhad jako samostatný krok bez callbacku; callback `ConfirmJevCalls` zůstává jen v běhu pro CLI (`InMemoryPipelineRunner`).
- `IEshopGuard.ScanSiteAsync` a `AnalyzeTextsAsync` jako tenká fasáda nad kroky.
- `RecordingPageFetcher` a `ReplayPageFetcher` a volby CLI `--record <složka>` a `--replay <složka>` pro offline porovnání.
- Referenční výstupy dnešního kódu, test shody a změření procesoru před změnou a po ní.

Out of scope:
- PostgreSQL implementace úložišť v `EshopGuard.Data` (potřebují tabulky ze změny 3) a orchestrace kroků jako úloh (změna 8). Tato změna dodá rozhraní a společné testy chování (`StoreContractTests`), které každá implementace musí projít. Viz K rozhodnutí 1.
- Globální limit Jevu a OpenAI v `ops.rate_limit_buckets` a zámky domén v `ops.domains` (změny 4 a 8); tady jen rozhraní `IRateLimiter` a lokální implementace.
- Vyhodnocení pro víc jurisdikcí, texty pravidel a kódy místo vět (změna 6).
- Jazykové verze, cookie a `Accept-Language` po verzích, rozsah procházení podle cesty verze (změna 7).
- Rozdíl sitemap, rotace a paměť rozhodnutí (změna 16).
- Odstranění dnešních heuristik podle seznamů slov (K rozhodnutí 5): změna musí dát stejný výsledek jako dnes.
- Vykreslení v Chromiu (návrh rozvoje 21).
- Změny pravidel, otázek Jevu a zadání přepisu.

## Approach

1. **Nejdřív zachytit dnešek.** Před první úpravou kódu se přidá jen nahrávání a přehrávání odpovědí webu (`RecordingPageFetcher`, `ReplayPageFetcher`). Dnešní kód pak nad nahrávkou vegis.sk a naturfyt.sk a nad oběma testovacími e-shopy vytvoří referenční výstupy (`findings.json`, `pages.jsonl`, `segments.csv`, `sieve.csv`, `profiles.json`, `report.md`), seznam klíčů cache Jevu a změřený čas procesoru na stránku. Složka `D:\_github\Overko` není repozitář git, takže návrat k dnešnímu kódu není jinak možný (K rozhodnutí 8).
2. **Kroky jako čisté služby.** Každý krok dostane vstup, potřebná úložiště a vrátí výstup. Nic si nedrží mezi voláními. Stav, který dnes žije v `Crawler.Run` a `ProfilingRun`, se přesune do serializovatelných záznamů.
3. **Běh pro CLI = kroky za sebou v paměti** (`InMemoryPipelineRunner`). Zachová dnešní pořadí stránek, pořadí upozornění a místo potvrzení ceny. Worker (změna 8) volá tytéž kroky po dávkách.
4. **Jedno čtení HTML.** `ParsedPage` parsuje dokument jednou. Místo `element.Remove()` se pracuje s množinou vynechaných uzlů, takže dokument zůstane celý pro další čtení. Výsledné bloky musí být stejné jako dnes (test nad všemi stránkami nahrávek).
5. **Klíč cache beze změny ceny.** `JevCacheKey.LegacyKey` se počítá přesně jako dnešní `JevCacheKey.Create`, takže SQLite cache CLI (66 159 uložených odpovědí v `cache/jev-cache.sqlite`, zjištěno 1. 10. 2026) zůstane platná a nic se neplatí znovu. PostgreSQL implementace použije dvojici `QuestionSetHash` a `StateHash`.
6. **SSRF na úrovni připojení.** Kontrola v `ConnectCallback` vidí skutečnou cílovou adresu po DNS, takže ji neobejde přesměrování ani DNS rebinding. Klient pro stahování nepoužívá proxy.
7. **Ověření shody** offline: přehrání nahrávek s `--mock` dá stejné výstupy jako referenční, klíče cache Jevu jsou stejné. Ostrý běh s Jevem je samostatný úkol s odhadem ceny a souhlasem uživatele.

## Dependencies

- **Změna 1 `rename-to-eshopguard`:** názvy projektů, jmenných prostorů a User-Agent `EshopGuard/0.1`.
- Běží souběžně se změnami 3 a 4 (nesahá na databázi).
- Navazují: změna 6 (pravidla nad krokem `RulesStep` a `EvaluateStep`), změna 7 (rozsah procházení verzí, `FetchRequest` s cookie a jazykem, otisky vět), změna 8 (orchestrace kroků jako úloh, PostgreSQL implementace úložišť), změna 16 (podmíněné stažení, `lastmod` ze sitemap).

## Done when

- Výsledek CLI na vegis.sk a naturfyt.sk je stejný jako dnes: přehrání nahrávek s `--mock` dá stejné `findings.json`, `pages.jsonl`, `segments.csv`, `sieve.csv` a `profiles.json` jako referenční výstupy dnešního kódu (po vynechání časů a dob běhu) a `report.md` se liší jen v řádcích s časem a dobou.
- Totéž platí pro testovací e-shopy `Fixtures/site` a `Fixtures/site-sk`.
- Klíče cache Jevu (`LegacyKey`) všech segmentů a úseků síta jsou stejné jako referenční seznam, takže ostrý běh nezaplatí žádné volání navíc.
- Všech 192 dnešních testů a nové testy projdou (`dotnet test`, bez kategorie `Jev`).
- Čas procesoru na extrakci a přiřazení profilu na stránku je na stejných nahrávkách nižší než referenční (čísla z úkolů 1.8 a 9.4 se zapíšou do `README.md`; architekturu, část 8, opraví autor podkladů).
- Testy SSRF: vnitřní adresa po DNS, po přesměrování, v IPv6 a na jiném portu se nestáhne a objeví se ve zprávě jako nezkontrolovaná.
- Test obnovy: stahování přerušené po libovolné dávce a obnovené z uloženého `UrlFrontierState` dá stejné stránky a nálezy jako běh bez přerušení.

## K rozhodnutí

1. **Kdo napíše PostgreSQL implementace úložišť.** Databázový návrh (část 8, F3) je řadí do F3, zadání této změny jen „tak, aby PostgreSQL implementace mohla být v `EshopGuard.Data`“, a návrh změny 8 je uvádí jako dodávku změny 5. Implementace potřebují tabulky ze změny 3, která běží souběžně. Návrh: změna 5 dodá rozhraní a `StoreContractTests`, implementace v `EshopGuard.Data` (`PgJevCache`, `PgRewriteCache`, `PgPageProfileStore`, `PgPageStore`, `PgUrlFrontierStore`, `S3PageContentStore`) napíše změna 8. Potvrdit a sjednotit text změny 8.
2. **Kdo napíše `IRateLimiter` nad `ops.rate_limit_buckets`:** změna 4, nebo 8? Tato změna dodá jen rozhraní a lokální implementaci.
3. **Rozsahy adres pro SSRF.** Architektura vyjmenovává 10.x, 172.16–31.x, 192.168.x, 127.x, 169.254.x a obdoby v IPv6. Design navrhuje navíc 0.0.0.0/8, 100.64.0.0/10, 192.0.0.0/24, 192.0.2.0/24, 198.18.0.0/15, 198.51.100.0/24, 203.0.113.0/24, 224.0.0.0/4, 240.0.0.0/4, 255.255.255.255, v IPv6 `::`, `::1`, `fc00::/7`, `fe80::/10`, `ff00::/8`, `2001:db8::/32`, `64:ff9b::/96` a mapované IPv4 `::ffff:0:0/96`. Potvrdit.
4. **Otisk věty.** Návrh: prvních 8 bajtů SHA-256 z `TextTools.NormalizeForHash(text)` (bez kontextu, bez nového balíčku). Alternativa XxHash64 potřebuje balíček `System.IO.Hashing`. Který otisk půjde do `findings.segment_hash`, rozhodne změna 8 (její K rozhodnutí 3 navrhuje 64bitový otisk textu).
5. **Dnešní heuristiky podle seznamů slov** odporují zásadě „žádné slovníky klíčových slov pro klasifikaci“: `CrawlOptions.LegalPageSlugs` (`PageClassifier.IsLegalUrl`, `IsLegalText`), `CrawlOptions.ProductSitemapHints`, `CrawlOptions.ExcludeUrlPatterns`, `LabelConfiguration.EcoImageKeywords` (obrázky k ručnímu posouzení). Tato změna je kvůli shodě výsledků nemění. Rozhodnout, zda a čím je nahradit (struktura stránky, Jev, LLM) v samostatné změně.
6. **SmartReader a jedno čtení.** `SmartReader.Reader` dostává text HTML a parsuje ho sám. Jestli jde předat už rozparsovaný dokument, se ověří v kódu balíčku 0.11.1 (úkol 4.5). Když ne, zůstanou dvě čtení (naše a SmartReader) místo pěti. Architektura i databázový návrh píšou o „třech čteních“, v kódu je jich ve skutečnosti pět (viz Intent).
7. **Nahrávky cizích e-shopů.** HTML vegis.sk a naturfyt.sk je cizí obsah. Návrh: nahrávky jen lokálně ve složce `snapshots/` (v `.gitignore`), test shody nad nimi má kategorii `Snapshot` a bez nahrávky se přeskočí. Do repozitáře jdou jen referenční výstupy testovacích e-shopů.
8. **Repozitář.** `D:\_github\Overko` není repozitář git. Návrh: založit repozitář (změna 1 nebo 2) před začátkem této změny, aby šel porovnat starý a nový kód.
9. **Počet dnešních testů.** Podklady uvádějí 192 testů; v kódu je 123 metod `[Fact]`/`[Theory]` (zbytek jsou případy `InlineData`). Číslo potvrdí `dotnet test` před začátkem (úkol 1.1).
10. **Názvy po přejmenování.** Změna předpokládá `EshopGuardService` (dnes `EshopGuardService`) a `EshopGuardOptions` (dnes `EshopGuardOptions`). Podklad F0 jmenuje jen `IEshopGuard` a `AddEshopGuard`. Sjednotit se změnou 1.
11. **Časový limit extrakce.** Návrh 30 s na stránku (`crawl.extract_timeout_seconds`). SmartReader nejde přerušit, takže úloha po limitu doběhne na pozadí. Potvrdit hodnotu.
12. **Tvar odpovědi v `checks.jev_answers`.** Databázový návrh ukládá `probabilities real[]`, `IJevCache` vrací `JevResult` (odpovědi po id otázek, typ, model, spotřeba). Pole čísel unese jen odpovědi „ano/ne“ v pevném pořadí id otázek; typy `choice` a `score` (`QuestionDefinition.Type`) by se ztratily. Zapnutá pravidla je dnes nepoužívají. Návrh: zatím `real[]` a `RuleValidator` odmítne zapnutou sadu s jiným typem otázky, dokud se tabulka nerozšíří; nebo uložit odpověď jako `jsonb`. Rozhodnout se změnou 3.
