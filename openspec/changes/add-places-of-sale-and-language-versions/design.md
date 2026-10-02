# Design: Místa prodeje a jazykové verze e-shopu

Cesty jsou po přejmenování ze změny 1 a po rozdělení na kroky ze změny 5 (kořen `eshop-guard/`).

## Technical Approach

### 1. Číselník trhů

`config/markets.yaml` (pro CLI; ve webu stejné hodnoty z `ref.markets`):

```yaml
markets:
  - code: sk                 # = jurisdikce v pravidlech
    country: SK              # ISO 3166-1 alpha-2, jak ho vrací model
    language: sk             # jazyk verze pro tento trh
    readable_languages: [sk, cs]   # verze v těchto jazycích čtou zákazníci tohoto trhu
    tlds: [sk]               # domovská země z domény, když ji nejde doložit
  - code: cz
    country: CZ
    language: cs
    readable_languages: [cs, sk]
    tlds: [cz]
versions:
  counted_min_own_share: 0.20      # architektura část 12, „návrh, neměřeno“
  min_sample_products: 10          # K rozhodnutí 3
  sample_pages: 100
  paired_products: 20
  language_fragments_per_version: 30
```

`MarketCatalog` načte soubor; `MarketCatalog.Supported` = trhy, pro které `RuleCatalog` má zapnuté sady pravidel (změna 6) a které hostitel povolí (`ref.markets.checks_status != none`). Ostatní země jsou nepodporované.

### 2. Technické znaky (bez modelu)

`ExtractStep` (změna 5) při jednom čtení dokumentu doplní do `PageExtract`:
- `HtmlLang` (`/html/@lang`);
- `Alternates`: `link[rel=alternate][hreflang]` → (jazyk, URL);
- `Currencies`: `priceCurrency` z JSON-LD (`JsonLdReader`), `itemprop=priceCurrency`, `meta[property=product:price:currency]`;
- `ProductIds`: `gtin`, `gtin8`, `gtin12`, `gtin13`, `gtin14`, `sku`, `mpn`, `productID` z JSON-LD produktu;
- `PhoneNumbers`: odkazy `tel:` a čísla s mezinárodní předvolbou v textu (vzor čísla, ne slova);
- `FooterLinks`: odkazy uvnitř rámu `footer`/`[role=contentinfo]`.

`SitemapParser` (změna 5) doplní `xhtml:link rel=alternate hreflang` k položkám sitemap.

`MarketSignalReader.Read(PageExtract home, IReadOnlyList<SitemapEntry> sitemap, Uri site)` → `MarketSignals`:
- `HtmlLang`, `Hreflang` (sloučené ze stránky a sitemap), `Currencies`, `PhonePrefixes`, `Tld`;
- `SwitcherCandidates`: odkazy úvodní stránky, jejichž cíl se od webu liší jen dvoupísmenným (`^/[a-z]{2}(-[a-z]{2})?/?$`) segmentem cesty, parametrem `lang`/`language`/`locale`, poddoménou, nebo jinou doménou se stejným prvním návěstím (`goodie.cz` → `goodie.sk`). Rozhoduje stavba adresy, ne text odkazu;
- `ScriptOnlySwitchElements`: prvky s `onclick`, `data-lang`, `data-language` nebo `data-locale`, které nejsou odkazem s adresou;
- `BrowserTranslationWidgets`: podpisy skriptů (`src` hostitele) překladových služeb (K rozhodnutí 6);
- `HomeLinks` (nejvýš 300) a `FooterLinks`.

### 3. Rozbor míst prodeje (model)

Obě volání jdou přes `IMarketModel` (výchozí `OpenAiMarketModel` nad `IRewriteClient`, model `RewriteOptions.Model` = `gpt-6.1-sol`; testovací `MockMarketModel`). `RewriteRequest` dostane volitelné `ReasoningEffort` (`low` pro výběr, `medium` pro rozbor jako v `sales.py`).

1. `SalesPageSelector.SelectAsync(MarketSignals)`: zadání `MarketPrompts.PickInstructions` a schéma `PickSchema` (z `sales.py`, `PICK_INSTR`, `PICK_SCHEMA`). Vstup = seznam odkazů „text | URL“ (úvodní stránka + patička). Výstup se ověří: jen URL ze seznamu, nejvýš 4, jen stejný web nebo kandidát verze. **Pojistka:** k vybraným stránkám se přidají stránky z `IShopPagesSource` (konektor, změna 15) a právní stránky, které ukázka už stáhla (`PageType.Legal`), dokud nejsou 4; když nic, výsledek nese kód `delivery_terms_not_found`.
2. Stažení vybraných stránek přes `FetchStep` se seznamem URL (robots.txt, SSRF, tempo, User-Agent `EshopGuard/0.1`); texty z `PageExtract` (hlavní text + rám + ostatní text), ořez 8 000 znaků na stránku a 6 000 u úvodní (z `sales.py`).
3. `PlacesOfSaleModel.AnalyzeAsync(MarketSignals, pages)`: zadání `MarketPrompts.SalesInstructions` podle `sales.py` rozšířené o tři síly důkazu; schéma `SalesSchema`:
   - `home_country` (ISO nebo prázdné) a `home_evidence[]`;
   - `countries[]`: `country`, `evidence_level` (`strong` | `delivery` | `generic`), `evidence[]` (`quote`, `source` = URL stránky nebo `signal:<název>`), `reason`;
   - `eu_wide_delivery` (`stated`, `quote`);
   - `language_versions[]`: `language`, `url`, `switch` (`path` | `subdomain` | `domain` | `query` | `cookie_or_script` | `unknown`), `evidence`;
   - `uncertain` (text pro interní audit).
4. `QuoteVerifier.Verify(result, pages, signals)`:
   - normalizace: sjednocení mezer, malá písmena, odstranění uvozovek „“"' na okrajích (jako `sales.py`);
   - citace stránky se hledá v textu citované stránky; když tam není, ale je doslova v jiné dodané stránce, přijme se s opraveným zdrojem a příznakem `source_corrected`; jinak se zahodí;
   - citace `signal:<název>=<hodnota>` platí, jen když `MarketSignals` takový znak s takovou hodnotou má;
   - země bez ověřeného důkazu se nenabídne a uloží se do `Rejected` s kódem `no_verified_evidence`;
   - počty `QuotesVerified`, `QuotesDropped`.
5. `PlacesOfSaleClassifier.Classify(...)` → `PlacesOfSaleResult`:
   - síla důkazu z modelu, se dvěma pojistkami bez práce s textem: země, jejíž všechny ověřené citace jsou totožné s citací `eu_wide_delivery`, má nejvýš `generic`; vlastní jazyková verze v jazyce trhu nebo verze na doméně s TLD trhu (`LanguageVersionFinder`, ne `needs_confirmation`) zvedne sílu na `strong` (architektura: „silný: vlastní jazyková verze nebo doména“);
   - `Supported` podle `MarketCatalog`; `Preselected` = podporovaná a (`strong` nebo `delivery`);
   - domovská země: ověřená z modelu; jinak z `tlds` domény s `HomeBasis = DomainTld` a `NeedsConfirmation`; `.com` a jiné domény bez trhu = bez domovské země a kód `home_country_unknown`;
   - kódy pro klienta: `delivery_terms_not_found`, `home_country_unknown`, `home_country_from_domain`, `quotes_dropped`;
   - kódy selhání (fail-closed, výsledek pak nese jen technické znaky a domovskou zemi z domény k potvrzení): `market_analysis_failed` (chyba modelu), `market_analysis_not_confirmed` (odhad v CLI nepotvrzen), `model_missing_key`, `model_mock` (ze změny 6).

`PlacesOfSaleDiff.Compare(previous, current)` (pro změnu 16): nové země se silným důkazem.

### 4. Jazykové verze

`LanguageVersionFinder.Find(MarketSignals, PlacesOfSaleResult?, IShopLanguageSource?)` → `LanguageVersionCandidate[]` (`Language` BCP 47, `BaseUrl`, `SwitchMethod`, `Source`, `Status`, `Evidence`):
1. `hreflang` (stránky a sitemap): `Source = hreflang`, metoda podle rozdílu adresy proti webu (cesta, poddoména, doména, parametr);
2. kandidáti přepínače: `Source = switcher`;
3. jazyky z konektoru: `Source = connector`;
4. když z 1–3 nic není a model verze uvedl: `Source = llm`;
5. hlavní verze = web zadaný klientem s jazykem z `html lang` (nebo z jazyka textu, viz 5);
6. jiná doména (hostitel, který není webem e-shopu ani jeho poddoménou) → `Status = needs_confirmation`; verze v jazyce nepodporovaného trhu → `Status = unsupported` (uloží se, nekontroluje, neukazuje).

`VersionAccessProbe.ProbeAsync(candidate)` → `VersionAccess`:
- **vlastní adresa** (cesta, poddoména, doména): stáhne vstupní stránku verze a porovná `html lang` s jazykem verze;
- **parametr nebo cookie** (`?lang=sk`): stáhne přepínací adresu, z `FetchResponse.SetCookies` vezme cookie a dál prochází s ní (`SwitchMethod = cookie`);
- **jazyk prohlížeče**: stáhne vstupní stránku s `Accept-Language` verze (`SwitchMethod = accept_language`);
- **přepínač jen v JavaScriptu** (`ScriptOnlySwitchElements` a žádná adresa ani cookie): `Status = needs_browser`, kód `version_needs_browser`; verze se nekontroluje, dokud nepřijde vykreslení v Chromiu (návrh rozvoje 21);
- **překlad v prohlížeči** (`BrowserTranslationWidgets` a verze bez vlastní adresy): `SwitchMethod = browser_translation`, kód `version_browser_translation`; kontroluje se původní text, verze se neprochází zvlášť; Weglot v režimu adres (`/sk/`) se prochází jako cesta;
- **přesměrování podle IP nebo jazyka**: vstupní stránka verze vrátí `html lang` jiné verze → `Status = mismatch`, kód `version_language_mismatch` a nabídka konektoru;
- **aplikace v JavaScriptu**: `RenderCheck` (`TextNotLoaded`) platí pro každou verzi zvlášť; verze, jejíž vstupní stránka se nenačte, má kód `version_text_not_loaded`.

`VersionCrawlScope` (rozšíření `SiteScope` ze změny 5): `Host`, `BasePath`, `ExcludedBasePaths` (cesty ostatních verzí na stejném hostiteli), `Cookies`, `AcceptLanguage`, `ExpectedLanguage`, `VersionLanguage`. `UrlFrontier.Consider` přijme jen URL v rozsahu. Každá stránka dostane `PageInfo.Language` = jazyk verze a `HreflangGroup` = klíč skupiny alternativ (seřazené URL ze `Alternates`, otisk SHA-256, prvních 16 znaků).

`FetchRequest` (změna 5) dostane `Cookies` a `AcceptLanguage`; `HttpPageFetcher` je pošle místo výchozích hlaviček. Klient `EshopGuard.Crawl` dostane `UseCookies = false` (K rozhodnutí 7); cookie drží `VersionCrawlScope` a `UrlFrontierState`.

### 5. Která verze pro které místo prodeje

`VersionMarketPlanner.Plan(versions, activeMarkets, MarketCatalog, mainVersion)` → `VersionPlan` (čistá funkce, bez sítě):
- pro každý aktivní trh `m`: verze s `Language` = `m.language` a stavem `active`; když není, hlavní verze;
- kontrolované verze = sjednocení; u každé `Jurisdictions` = všechny aktivní trhy, jejichž `readable_languages` obsahují jazyk verze;
- verze s `needs_confirmation`, `needs_browser`, `mismatch`, `excluded` nebo `unsupported` se nekontrolují a plán je vyjmenuje s kódem;
- `CountedProducts` = součet `product_count` kontrolovaných verzí s `Counted = true` (pro pásmo, změna 12);
- `Recalculate(activeMarkets)` = nový plán bez stahování a modelu (odškrtnutí země na 3c).

Příklad (tabulkový test): verze `cs` (hlavní) a `sk`, trhy SK + CZ → kontroluje se `cs` [cz, sk] a `sk` [sk, cz]; jen SK → `sk` [sk]; jen CZ → `cs` [cz]; verze jen `cs`, trhy SK + CZ → `cs` [cz, sk]; verze `cs`, `sk`, `pl`, trhy SK + CZ → `pl` se nekontroluje (`unsupported`).

### 6. Rozbor verzí v ukázce

Běží jen tehdy, když plán má víc než jednu kontrolovanou nebo potvrzovanou verzi pro podporované trhy.

`VersionSamplePlanner.Plan(versions, sitemaps, budget = 100)` → `VersionSamplePlan`:
- ~20 párů stejného produktu ve dvou verzích: párování přes `hreflang` (stránka nebo sitemap), ID z konektoru, `gtin*` (EAN), `sku`/`mpn`/`productID`; kandidáti z produktových URL sitemap ve stejném pořadí náhodně s pevným semínkem běhu;
- povinné stránky každé verze: stránky vybrané v rozboru míst prodeje a jejich alternativy v ostatních verzích, jinak právní stránky ukázky (`PageType.Legal`);
- zbytek rovnoměrně náhodné produkty po verzích;
- když pár nejde vytvořit, `PairingMode = sentence_overlap`.

`VersionComparer.CompareAsync(plan, extracts)` → `VersionComparisonResult` po verzích:
- **jazyk textu**: `TextLanguageModel.LabelAsync(fragments)` jedno volání na verzi nad ~30 větami 30–300 znaků z `MainBlocks` produktových stránek (bez `RestBlocks`, kde bývají recenze a bloky dopravy); zadání a schéma z `validate.py` (`INSTR`, `SCHEMA`); výsledek `LanguageShare` (podíl vět po jazycích);
- **vlastní texty**: otisky vět (`SentenceFingerprint`) hlavního textu produktových stránek verze proti všem ostatním kontrolovaným verzím; `OwnTextShare` = podíl unikátních vět bez shody (K rozhodnutí 4);
- **druh rozdílu u páru** (bez modelu, prahy z `validate.py`, ověřeno 44 z 46): `identical` (stejný normalizovaný text), `untranslated` (převažující jazyk vět verze je jazyk druhé verze), `translation` (poměr délek 0,8–1,25 a rozdíl počtu vět nejvýš max(2; 15 %)), jinak `shortened_or_different` (K rozhodnutí 5); příklady s URL;
- bez párů: `SentenceOverlapShare` = podíl vět jedné verze doslova obsažených v druhé;
- **povinné stránky**: `MandatoryPagesDiffer` podle otisků vět, s URL;
- **počet produktů**: produktové URL sitemap v rozsahu verze nebo konektor; neznámý = `null`;
- **započítání**: `Counted = OwnTextShare ≥ counted_min_own_share` a aspoň `min_sample_products` produktových stránek s hlavním textem a známý počet produktů a úspěšné určení jazyka; jinak `Counted = false` s kódem `version_sample_insufficient`, `version_product_count_unknown` nebo `version_language_unknown`;
- **upozornění**: `untranslated_text` (český text na slovenské verzi) je upozornění, ne porušení; pravidla se na text použijí stejně.

### 7. Výstupy

`MarketsAnalysisResult` (serializovatelný, `schema_version`):
- `Markets[]` → `ShopMarketRow` (`CountryCode`, `IsHome`, `Status` `suggested`/`unsupported`, `EvidenceLevel`, `Source = detected`, `Evidence` (ověřené citace se zdrojem, znaky, `home_basis`, interní `reason` a `uncertain` modelu), `Preselected`);
- `Versions[]` → `ShopLanguageRow` (`Language`, `BaseUrl`, `SwitchMethod`, `Source`, `Status`, `TranslatedShare`, `LanguageShare`, `DescriptionLanguages` (odkud věty jsou, označené, přeložené a cizojazyčné produkty), `ProductCount`, `ProductCountAtLeast`); *(do 2. 10. 2026 `OwnTextShare`, `Comparison` a `Counted`, odchylka 22)*;
- `Summary` pro 3c: kód a parametry jedné věty (`versions_single`, `versions_by_market` {`languages`, `urls`, `markets`, `translated_share`}, `versions_untranslated_texts` {`language`, `share`, `products`, `of`, `text_language`}, `version_other_domain_needs_confirmation` {`domain`}, `version_needs_browser`, `version_browser_translation`, `version_language_mismatch`, `version_sample_insufficient`);
- `Details` pro 3d: po verzích podíl přeložených produktů, jazyk popisů, příklady produktů v jiném jazyce a věty s jazykem od modelu;
- `Plan` (`VersionPlan`) a `Usage` (volání modelu, tokeny, cena).

### 8. Odhad ceny

`MarketAnalysisEstimate` se spočítá před každým voláním modelu podle `sales.py`: tokeny = znaky / 3,2; vstup × `rewrite.input_usd_per_million` (2,00), výstup × `rewrite.output_usd_per_million` (10,00); výběr: odkazy + zadání a 800 výstupních tokenů; rozbor: zadání + 2 000 + úvodní stránka + 4 × stránka a 4 000 výstupních tokenů; jazyk: 30 vět na verzi a 500 výstupních tokenů. Ověřené náklady (1. 10. 2026): 0,03–0,08 USD na e-shop za místa prodeje, 0,043 USD za jazyk 46 párů z 5 e-shopů. CLI odhad vypíše a nad `rewrite.max_usd_without_confirm` se zeptá; worker ho porovná s rozpočtem ukázky (změna 8).

## Architecture Decisions

1. **Model jen tam, kde struktura nestačí, a jen s ověřitelnými výroky.** Znaky (hreflang, html lang, měna, adresy verzí) jsou spolehlivé a zdarma; model rozhoduje o záměru prodeje a domovské zemi, ale každá jeho věta musí stát na citaci, kterou kód najde. Proto 62 z 62 citací a žádné vymyšlené země.
2. **Tři síly důkazu místo dvou stavů.** Chyba freshlabels (26 zemí kvůli tabulce dopravy) ukázala, že „doručujeme“ není „cílíme“. Síla důkazu je vidět i klientovi.
3. **Verze jako samostatný rozsah procházení.** Jedno pravidlo pro všechny varianty (každý text verze se kontroluje), žádné větvení podle toho, jestli se překládá jen menu. Stejný text v obou verzích zaplatí Jev jen jednou díky cache podle otisku věty.
4. **Plán verzí jako čistá funkce.** Přepočet ceny po odškrtnutí země na 3c je okamžitý a stejný v API i v testech.
5. **Bez slovníků slov.** Jazyk určuje model nad větami, stránky o prodeji model nad odkazy, přepínač stavba adresy. Výzkumná počítání písmen a hledání „popis“ ve třídách (`pairs.py`) do knihovny nejdou.
6. **Cookie po rozsazích, ne sdílené.** Sdílený kontejner cookie by přenášel stav mezi weby a tenanty.

## Data Flow

```
ukázka zdarma (změna 8) nebo CLI `markets`:
  DiscoveryStep ─► sitemap (+ xhtml:link) ─┐
  FetchStep(home) ─► ExtractStep ─► PageExtract (html lang, alternates, měny, ID, telefony, patička)
                                          │
                     MarketSignalReader ──► MarketSignals
                                          │  odhad ─► potvrzení / rozpočet
                     SalesPageSelector (model, ≤4 URL) + pojistka (patička, právní stránky, konektor)
                     FetchStep(vybrané) ─► ExtractStep ─► texty stránek
                     PlacesOfSaleModel (model) ─► QuoteVerifier ─► PlacesOfSaleClassifier ─► PlacesOfSaleResult
                                          │
                     LanguageVersionFinder ─► kandidáti ─► VersionAccessProbe ─► VersionAccess
                     VersionMarketPlanner(aktivní/navržené trhy) ─► VersionPlan
                     [víc verzí] VersionSamplePlanner ─► 100 stránek ─► FetchStep/ExtractStep po rozsazích verzí
                                 VersionComparer + TextLanguageModel (model, 1 volání na verzi) ─► VersionComparisonResult
                                          │
                     MarketsAnalysisResult ─► (změna 8) shop_markets, shop_languages, pages.language
                                          ─► (změna 10) 3c: věta, země s důvodem, cena; 3d: podrobnosti
3c odškrtnutí země ─► VersionMarketPlanner.Recalculate ─► nové CountedProducts (bez sítě a modelu)
analýza ─► pro každou kontrolovanou verzi VersionCrawlScope + Jurisdictions ─► kroky změny 5 a 6
```

## File Changes

### Nové soubory (`src/EshopGuard.Core/`)

- `Markets/MarketCatalog.cs` (`MarketDefinition`, načtení `config/markets.yaml`, `Supported`).
- `Markets/MarketSignals.cs`, `Markets/MarketSignalReader.cs`.
- `Markets/IMarketModel.cs`, `Markets/OpenAiMarketModel.cs`, `Markets/MockMarketModel.cs`, `Markets/MarketPrompts.cs` (zadání a schémata výběru a rozboru, `Version`).
- `Markets/SalesPageSelector.cs`, `Markets/PlacesOfSaleModel.cs`, `Markets/QuoteVerifier.cs`, `Markets/PlacesOfSaleClassifier.cs`, `Markets/PlacesOfSaleResult.cs` (`CountryEvidence`, `EvidenceLevel`, `HomeBasis`, kódy), `Markets/PlacesOfSaleDiff.cs`.
- `Markets/MarketAnalysisEstimate.cs`.
- `Markets/IShopPagesSource.cs`, `Languages/IShopLanguageSource.cs` (implementuje změna 15).
- `Languages/LanguageVersionCandidate.cs`, `Languages/LanguageVersionFinder.cs`.
- `Languages/VersionAccess.cs`, `Languages/VersionAccessProbe.cs`.
- `Languages/VersionCrawlScope.cs`.
- `Languages/VersionMarketPlanner.cs`, `Languages/VersionPlan.cs`.
- `Languages/VersionSamplePlanner.cs`, `Languages/VersionSamplePlan.cs`.
- `Languages/ITextLanguageModel.cs`, `Languages/OpenAiTextLanguageModel.cs`, `Languages/MockTextLanguageModel.cs`.
- `Languages/VersionComparer.cs`, `Languages/VersionComparisonResult.cs`.
- `Markets/MarketsAnalysisResult.cs` (`ShopMarketRow`, `ShopLanguageRow`, `VersionSummary`, `VersionDetails`), `Markets/MarketsAnalyzer.cs` (orchestrace pro CLI a pro změnu 8).
- `config/markets.yaml`.
- `src/EshopGuard.Cli/Commands/MarketsCommand.cs` (`eshopguard markets <url> [--mock] [--yes] [--out]`, zápis `markets.json`).

### Měněné soubory

- `src/EshopGuard.Core/Extract/ContentExtractor.cs`, `Extract/ExtractedPage.cs` (`PageExtract`): `HtmlLang`, `Alternates`, `Currencies`, `ProductIds`, `PhoneNumbers`, `FooterLinks`.
- `src/EshopGuard.Core/Extract/JsonLdReader.cs`: `priceCurrency`, `gtin*`, `sku`, `mpn`, `productID`.
- `src/EshopGuard.Core/Crawl/SitemapParser.cs`: `xhtml:link` alternativy.
- `src/EshopGuard.Core/Crawl/FetchRequest.cs`, `Crawl/IPageFetcher.cs` (`FetchResponse.SetCookies`), `Crawl/HttpPageFetcher.cs`: `Cookies`, `AcceptLanguage`, čtení `Set-Cookie`.
- `src/EshopGuard.Core/Crawl/UrlTools.cs`: `IsInScope(Uri, VersionCrawlScope)`, `IsSubdomainOf`.
- `src/EshopGuard.Core/Pipeline/UrlFrontier.cs`, `Pipeline/Contracts/SiteScope.cs`, `Pipeline/FetchStep.cs`, `Pipeline/ExtractStep.cs` (ze změny 5): rozsah verze, cookie a jazyk, `PageInfo.Language`, `HreflangGroup`, kontrola `html lang` proti `ExpectedLanguage`.
- `src/EshopGuard.Core/Models/PageInfo.cs`: `Language`, `HreflangGroup`, `ProductIds`.
- `src/EshopGuard.Core/Fix/IRewriteClient.cs` (`RewriteRequest.ReasoningEffort`), `Fix/OpenAiRewriteClient.cs` (použije ho, když je zadané).
- `src/EshopGuard.Core/Options/EshopGuardOptions.cs`: `MarketsOptions` (`CatalogFile` = `config/markets.yaml`, `MaxSelectedPages` = 4, `PageTextChars` = 8 000, `HomeTextChars` = 6 000).
- `src/EshopGuard.Core/ServiceCollectionExtensions.cs`: registrace služeb `Markets/` a `Languages/`; `MockMarketModel` a `MockTextLanguageModel` při `Rewrite.UseMock`; u klienta `EshopGuard.Crawl` `UseCookies = false`.
- `src/EshopGuard.Cli/CliHost.cs`: registrace `MarketsCommand`.
- `README.md`: příkaz `markets`, výstup `markets.json`, cookie po rozsazích.

### Testy (`tests/EshopGuard.Core.Tests/`)

- `Fixtures/versions/` (nové testovací e-shopy, generátor `generate_versions.py`): `path-shop` (`/` cs, `/sk/` sk, `hreflang`), `domain-shop-cz` a `domain-shop-sk` (dvě domény, přepínač odkazem), `cookie-shop` (`?lang=sk` nastaví cookie), `accept-language-shop` (verze podle hlavičky), `script-switch-shop` (přepínač tlačítkem s `onclick`), `widget-shop` (skript překladu v prohlížeči), `redirect-shop` (vrací vždy `html lang="cs"`), `spa-shop` (prázdný kořen aplikace u verze `sk`).
- `MultiHostFileSystemPageFetcher.cs`: hostitel → složka, simulace `Set-Cookie`, cookie a `Accept-Language`.
- `Markets/MarketSignalReaderTests.cs`, `Markets/QuoteVerifierTests.cs`, `Markets/PlacesOfSaleClassifierTests.cs` (případ freshlabels: tabulka cen dopravy 26 zemí → `delivery`, ne `strong`; obecné doručení do EU → `generic`, nepředvyplněné; nepodporované `PL` → `unsupported`), `Markets/SalesPageSelectorTests.cs` (URL mimo seznam se zahodí, pojistka z patičky a právních stránek), `Markets/HomeCountryTests.cs`.
- `Languages/LanguageVersionFinderTests.cs`, `Languages/VersionAccessProbeTests.cs`, `Languages/VersionCrawlScopeTests.cs` (verze se nesmíchají), `Languages/VersionMarketPlannerTests.cs` (tabulka z designu, oddíl 5), `Languages/VersionComparerTests.cs` (syntetické páry, prahy `validate.py`, 20 %, nejistota), `Languages/VersionSamplePlannerTests.cs` (100 stránek, ~20 párů, povinné stránky, pevné semínko).
- `Markets/MarketsCommandTests.cs` (CLI s `--mock` nad testovacími e-shopy).

## Odchylky při implementaci (2. 10. 2026)

1. **Trhy v `config/jurisdictions.yaml`, ne v `config/markets.yaml`.** Podle CLAUDE.md je další trh jeden řádek jednoho souboru. Jurisdikce dostala pole `country`, `language`, `readable_languages` a `tlds`; jurisdikce bez `country` není trh. `MarketCatalog.From(RuleCatalog, hostAllows)` je postaví z načtených pravidel. Limity rozboru (oddíl `versions` návrhu) jsou v `settings.yaml`, oddíl `markets` (`MarketsOptions`), spolu s podpisy překladových služeb (`browser_translation_scripts`, K rozhodnutí 6).
2. **Zadání modelu napsal Claude.** Výzkumné skripty `sales.py` a `validate.py` nejsou v repozitáři (jen lokálně). Zadání a schémata (`MarketPrompts`) jsou napsaná podle tohoto designu; čísla z výzkumu (62 z 62 citací, 44 z 46 párů) pro ně platí až po ověření 7.3 a 7.4.
3. **Předvolby podle stavby E.164.** `PhonePrefixes.CallingCode` pozná délku předvolby podle číslovacího plánu (1 a 7 jednomístné, výčet dvoumístných, ostatní trojmístné). Kód tak nezná žádnou zemi; komu předvolba patří, posoudí model.
4. **Kandidát přepínače v cestě a v poddoméně** musí mít dvoupísmenný segment (`^[a-z]{2}(-[a-z]{2})?$`), jak říká oddíl 2. Třípísmenné segmenty (`/bio/`, `/eko/`) by byly falešní kandidáti.
5. **Testovací e-shopy na doménách `.cz` a `.sk`** (`path-shop.cz`, `domain-shop.cz` a `domain-shop.sk`…), ne `*.test`. Verze na jiné doméně má podle oddílu 2 stejné první návěstí, takže `domain-shop-sk.test` ze scénáře specifikace by kandidátem nebyl. Domény testovacích e-shopů se nikdy nestahují ze sítě (`MultiHostFileSystemPageFetcher`).
6. **Cookie po rozsazích i u běžného skenu.** `UrlFrontierState.Cookies` drží cookie jednoho procházení (webu nebo verze) a posílá je dál, takže web, který nastavuje cookie, dostane během skenu stejné chování jako dřív se sdíleným kontejnerem. Mezi weby, verzemi a tenanty se nesdílí nic.
7. **Oprava znaku produktové sitemap.** `DiscoveryStep` bral za produktovou každou sitemap, protože slovo „sitemap“ obsahuje nápovědu „item“ (`crawl.product_sitemap_hints`). Za produkty se tak počítaly i právní a obsahové stránky ze `/sitemap.xml`; počet produktů verze by byl špatně. Teď se nápovědy hledají v cestě bez slova „sitemap“. Referenční výstupy testovacích e-shopů se nezměnily (418 testů). U skutečných webů se jedinou `/sitemap.xml` mohou stránky navíc projít v pořadí ostatních stránek a nepočítat se do limitu produktů; ověří to lokální test nahrávek (`Category=Snapshot`).
8. **`VersionAccess` je výsledek zkoušky**, zapsaný do `LanguageVersionCandidate` (stav, způsob přepnutí, rozsah, deklarovaný jazyk, kódy). Navíc kódy `version_unreachable` (adresa verze nevrátí stránku) a `version_robots_disallowed` (robots.txt verzi zakazuje); stav `active` bez rozsahu = nekontroluje se a důvod je v kódu.
9. **Přepínací parametr cookie** (`?lang=sk`) má v rozsahu všech verzí stejného hostitele hodnotu `*` (`ExcludedQuery`): odkaz „Slovensky“ uvnitř české verze by jinak přepnul cookie procházení na slovenštinu.
10. **`VersionCrawler`** spouští kroky změny 5 (`DiscoveryStep`, `FetchStep`) v rozsahu jedné verze, buď celou verzi, nebo jen zadané adresy vzorku.
11. **Jazyk textu přes `IMarketModel`.** `TextLanguageModel` (rozhraní `ITextLanguageModel`) posílá jedno volání na verzi přes stejný model jako místa prodeje. Samostatné `OpenAiTextLanguageModel` a `MockTextLanguageModel` nevznikly: falešný model (`MockMarketModel`) dostane deklarovaný jazyk verze a vrátí ho u každé věty. Skutečný model deklarovaný jazyk nedostává, aby ho neovlivnil.
12. **`IMarketsAnalyzer`** je veřejné rozhraní knihovny (CLI, později worker změny 8). Implementace `MarketsAnalyzer` je interní, protože používá interní kroky změny 5.
13. **Věta pro 3c a upozornění.** `Summary` je jedna věta podle priority: verze k potvrzení, potřeba prohlížeče, překlad v prohlížeči, jiný jazyk, malý vzorek, nepřeložené texty, jen menu, vlastní texty, jedna verze. Ostatní platné kódy jsou v `Notices`, nic se tedy nevynechá (fail-closed). Nepodporované verze se neuvádějí.
14. **Verze čekající na potvrzení je ve vzorku** (K rozhodnutí 2): stáhne se a porovná, ale v plánu kontroly ani v ceně není, dokud ji klient nepotvrdí.
15. **Povinné stránky verze** jsou stránky vybrané pro rozbor míst prodeje a jejich alternativy v jazyce verze (v jejím rozsahu). Právní stránky ukázky doplní změna 8, až ukázka poběží ve workeru.
16. **Počet produktů verze** je počet adres produktových sitemap v rozsahu verze. Bez produktové sitemap je neznámý (`version_product_count_unknown`) a verze se nezapočítá. Počet z konektoru doplní změna 15.
17. **Scénáře specifikace s doménami** používají `domain-shop.cz` a `domain-shop.sk` (odchylka 5).
18. **Ověření 7.2–7.4 proběhne lokálně** se souhlasem uživatele. Cloudové prostředí nemá klíč OpenAI a skenování cizích webů uživatel naplánoval lokálně. Do té doby platí pro zadání modelu jen testy s falešným modelem.

19. **Oprava po prvním skutečném rozboru (goodie.sk, 2. 10. 2026).** Rozbor ukázal „vlastní texty 0 %“ u všech verzí a verzi goodie.cz dvakrát. Příčiny a opravy:
    - **Produkt jen v mikrodatech.** goodie.sk označuje produkt `itemtype="http://schema.org/Product"`, nemá JSON-LD a `og:type` je `article`, takže všechny produktové stránky vyšly jako obsahové a do porovnání šlo 0 produktů. `ContentExtractor` nově čte `HasProductMicrodata` (právě jedna položka Product, která není uvnitř jiné; výpis s více produkty ne) a `PageClassifier` ji bere jako produkt. Do porovnání verzí navíc jdou stránky naplánované jako produkty (sitemap je tak označuje a cena je tak počítá), i když je třídič nepozná.
    - **Jedna verze dvakrát.** hreflang `cs-CZ` a přepínač na stejnou doménu (jazyk `cs` podle TLD) dávaly dvě verze; `LanguageVersionFinder` je nově slučuje, když mají stejnou adresu a stejný hlavní jazyk.
    - **Páry z hreflang stránek.** Sitemap goodie protějšky neuvádí, takže se v obou verzích vybraly různé náhodné produkty a páry nevznikly. Když plán ze sitemap nemá žádný pár, rozbor nejdřív stáhne produkty hlavní verze (nejvýš `markets.paired_products`) a jejich protějšky podle hreflang na stránce nahradí náhodné produkty ostatních verzí (`VersionSamplePlanner.WithPagePairs`); počet požadavků zůstává. goodie.sk: 0 → 20 párů.
    - **Vlastní texty z párů.** S aspoň `markets.min_sample_products` páry se podíl vlastních vět počítá jen nad spárovanými produkty (věty, které na protějšku nejsou). Nad různými produkty by vlastní vypadal každý text, i zkopírovaný katalog. Nové pole páru `shared_sentence_share` (podíl vět protějšku nalezených doslova na stránce hlavní verze). goodie.sk: sk 49 %, cs-cz 52 % vlastních vět; u 21 párů `shared_sentence_share` 0–0,84 (4 páry ≥ 0,7, zřejmě nepřeložené).

20. **Porovnání verzí na popisu produktu z profilu (2. 10. 2026, schváleno uživatelem).** Rozbor goodie.sk se skutečným modelem ukázal, že vlastní texty obou verzí (49 a 52 %) snižují recenze zákazníků: obě domény ukazují pod produktem stejné recenze (většinou česky). Páry s `shared_sentence_share` ≥ 0,7 z odchylky 19 tedy nebyly nepřeložené popisy, ale sdílené recenze. Úpravy:
    - **Popis z profilu.** `MarketsAnalysisRequest.Profiles` (`None`, `Stored`, `Create`). S profilem se u produktové stránky porovnávají jen oblasti s rolí `main_description` a `short_description` bez oblastí jiných rolí uvnitř (`ProfileDescription`; recenze, související produkty). Profily se zkoušejí po verzích od hlavní: uložené profily verze a profily verzí před ní (e-shop na dvou doménách mívá jednu šablonu). V režimu `Create` chybějící profil produktové šablony napíše model (`ProfileStep`); cena profilu se přičte k odhadu rozboru (`MarketAnalysisEstimate.ProfilesUsd`) a potvrzuje se stejně jako rozbor, nepotvrzený odhad dá `version_profiles_not_confirmed` a další verze se už neptá. Nový profil se uloží, takže ho kontrola webu použije znovu. Stránka bez profilu nebo bez oblasti popisu se porovná celým hlavním textem; `comparison.basis` je `description`, `main_text` nebo `mixed`. Klasifikaci dělá model profilu, žádný slovník.
    - **Po produktech.** `own_product_share` = podíl spárovaných produktů s textem přeloženým nebo jiným a v jazyce verze; `foreign_text_products` z `labeled_products` = produkty, jejichž první věty model víc než z poloviny označil jiným jazykem než jazyk verze (remíza nestačí), i u hlavní verze, s převažujícím `foreign_text_language`. Takový produkt je upozornění `untranslated_text` a 3c dostane `versions_untranslated_texts` s parametry `language`, `share`, `products`, `of` a `text_language` (goodie.sk: české popisy na slovenské verzi). Pro jazyk se berou nejdřív věty spárovaných produktů po řadě, `markets.language_fragments_per_version` 30 → 40 (dvě věty z každého z 20 párů).
    - **Kde se to použije.** Worker (`run.markets` v ukázce) vždy `Create`; `market_usd` interního odhadu zahrnuje i tyto profily, spotřeba je v `usage_records` jako `profile`. Profily, které by odhad ukázky dostaly nad strop, se jen vynechají (porovná se celý text, odhad se bez nich neukládá); ukázku zastaví jen odhad samotného rozboru. CLI `markets --profiles` (profily v PostgreSQL jako u `scan`, databáze se ověří před stažením); bez volby se porovnává celý hlavní text a databáze není potřeba.
    - **Ověření.** Testy s falešným modelem profilu (`PagePairsTests`, `ProfileDescriptionTests`, `VersionProfilesTests`, CLI `DatabaseCheckTests`): sdílené recenze bez profilu snižují vlastní texty přeložené verze pod 60 %, s profilem jsou nad 95 % a páry jsou překlad; zkopírované popisy zůstanou bez vlastních textů; české popisy slovenské verze dají `untranslated_text` hlavní verze. Na goodie.sk (2. 10. 2026, `markets --profiles`, skutečný model): vlastní texty sk 49 → 87 %, cs-cz 52 → 88 %; 21 párů (19 překlad, 2 zkrácený nebo jiný), `own_product_share` 1,0; `basis` `mixed` (44 produktových stránek z 46 na verzi s popisem z profilu); `shared_sentence_share` v příkladech nejvýš 0,43 (dřív až 0,84). Jeden profil (`goodie.sk#1`) za 0,149 USD (55 075 vstupních a 3 880 výstupních tokenů, odhad 0,158 USD), rozbor 0,057 USD, celkem 0,206 USD (odhad nejvýš 0,270 USD). Jeden produkt vyšel jako „popis česky“ jen z jedné české věty ze dvou; od té doby se produkt počítá do cizího jazyka až s víc než polovinou vět (remíza nestačí). Věty s jazykem od modelu jsou v `details[].language_fragments` (`LabeledFragment`), aby šlo výsledek ověřit ručně.

21. **Opravy po rozboru pěti e-shopů (2. 10. 2026, `markets --profiles`, 0,561 USD).**
    - **Verze v jazyce nepodporovaného trhu zjištěném ze stránky.** bonami.bg, .ee a .it se našly jen přes přepínač bez jazyka (TLD v katalogu trhů není), takže čekaly na potvrzení; jazyk se zjistil až stažením a stav se nepřepočítal. Vzorek se dělil mezi pět verzí (5 párů místo 20 pro češtinu, 300 požadavků) a 3c nabízelo potvrdit italskou doménu. `LanguageVersionFinder.AfterProbe` po zkoušce přístupu použije pravidlo nepodporovaného jazyka znovu.
    - **Počet produktů z neúplné sitemap.** `product_0.xml` bonami měla přes 5 MB (limit stránky), takže se nenačetla a 2 632 adres ze zbytku vypadalo jako počet produktů; adresy z obrázkových sitemap se navíc počítaly dvakrát. Nově: limit souboru sitemap `crawl.max_sitemap_bytes` 50 MB (protokol sitemap), produktové sitemap se čtou před ostatními (limit `crawl.max_sitemap_urls` zastaví ostatní stránky, ne produkty), počítají se jedinečné adresy a nenačtená nebo useknutá produktová sitemap dá `DiscoveryResult.ProductSitemapsIncomplete`: počet verze je neznámý s kódem `version_product_count_incomplete` a dolní mezí `product_count_at_least` (i v základu rozsahu ukázky). Fail-closed: neúplný počet se do ceny nepočítá.
    - **E-shopy bez produktové sitemap** (freshlabels: jedna sitemap na jazyk, 25 413 adres, produkty pod `/produkty/`; havlikovaapoteka: jedna `sitemap0.xml.gz`, 2 601 adres s plochými adresami) nemají ve vzorku produkty a verze se neporovnají; návrh řešení čeká na schválení.

22. **Cena za každou zemi, texty verzí se neporovnávají (rozhodnutí uživatele 2. 10. 2026, implementováno 2. 10. 2026).** Do pásma jde součet produktů za každou zaškrtnutou zemi: pro každou zemi počet produktů verze, kterou pro ni plán kontroluje (`VersionMarketPlanner`); e-shop s jednou verzí a dvěma zeměmi má dvojnásobek. Tím odpadá důvod porovnávat texty verzí (oddíl 6): párování produktů, otisky vět, podíl vlastních textů, druh rozdílu u párů, porovnání povinných stránek, práh `counted_min_own_share` a souhrnné kódy `versions_both_own_texts` a `versions_same_texts_menu_only`. Zůstává: nalezení verzí a plán, počet produktů verze (s neúplnou sitemap neznámý), jazyk popisů produktů a upozornění na nepřeložené texty (odchylka 20, po produktech), profily šablon pro výběr vět popisu. Vzorek každé verze tvoří povinné stránky a náhodné produkty; produkty se najdou i bez produktové sitemap podle struktury stránky a počet produktů pak zůstane neznámý, dokud ho nedodá konektor. Hranice pásem jsou v `billing.price_tiers` (změna 12). Obrazovky 3c a 3d se nejdřív upraví v návrhu UI. Návrh UI schválil uživatel 2. 10. 2026 (plátno verze 31). Implementace: `VersionMarketPlanner` vrací `ByMarket` (země, verze, produkty) a `CountedProducts` je jejich součet, `VersionPlan.ProductCounts` drží počty po verzích pro přepočet; `VersionSamplePlanner` plánuje jen povinné stránky a náhodné produkty (druhy `mandatory`, `product`); `VersionComparer` nahradil `VersionLanguages` (jazyk popisů, `TranslatedShare`, upozornění), věty pro model z 20 náhodných produktů verze (`markets.language_products`); věta pro 3c `versions_by_market` místo `versions_both_own_texts` a `versions_same_texts_menu_only`; `version_sample_insufficient` a `version_language_unknown` platí i pro hlavní verzi a jsou jen upozornění. Odstraněno: `markets.counted_min_own_share`, `markets.paired_products`, `PairingMode`, `PairKinds`, porovnání povinných stránek. Databáze: migrace `F4LanguagesByCountry`. Verze bez produktové sitemap (úkol 8.4): `MarketsAnalyzer` stáhne po dávkách nejvýš `markets.product_probe_pages` (40, neměřeno) náhodných stránek její sitemap, dokud struktura stránky neukáže `markets.language_products` produktů (JSON-LD, mikrodata, `og:type`); nalezené produkty jdou do vzorku jako `product` a znovu se nestahují, počet produktů verze zůstane neznámý. Test `VersionLanguageAnalysisTests.WithoutAProductSitemap_…` (60 produktů a 30 kategorií v jedné sitemap).
