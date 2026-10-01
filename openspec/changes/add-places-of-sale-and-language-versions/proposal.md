# Proposal: Místa prodeje a jazykové verze e-shopu

## Intent

**Problém.**
- Knihovna dnes zná jen jednu zemi zadanou ručně (`--country`) a jednu „verzi“ webu: crawler projde všechno na stejném hostiteli (`UrlTools.IsSameSite`), takže u e-shopu s verzí `neco.cz/sk/` smíchá českou a slovenskou verzi, a verzi na jiné doméně (`goodie.sk`) nepozná vůbec.
- Obchodník, který zaměřuje činnost do jiného členského státu, musí vůči tamním spotřebitelům splnit tamní pravidla (nařízení Řím I, čl. 6; zákon 108/2024 Z. z., § 1 ods. 2; zákon 22/2004 Z. z., § 3 ods. 4; architektura část 12, odvození z textu, potvrdí právník). Aplikace proto musí zjistit, kde e-shop prodává, a nabídnout to klientovi k potvrzení (obrazovka 3c „Kde predávate“).
- Průzkum 46 produktů v 5 e-shopech (1. 10. 2026) ukázal, že kontrola jedné jazykové verze o druhé nic neříká: úplně jiné texty (panakeia 9 z 9), zkrácené texty (havlikovaapoteka 1 z 7), český popis na slovenském webu s tvrzeními „čistě přírodní“ a „netoxické složení“ (goodie 2 z 10), jiná doprava, ceny a vrácení zboží (freshlabels).
- Cena podle pásma má počítat produkty ve verzích s vlastními texty. Bez rozboru verzí ji nejde spočítat ani garantovat.

**Co změna přinese.**
- V ukázce zdarma rozbor míst prodeje: technické znaky bez modelu, pak dvě volání modelu s doslovnými citacemi, které se ověří proti textu stránek. Ověřeno 1. 10. 2026 na 9 e-shopech (vegis, naturfyt, bonami, freshlabels, goodie, havlikovaapoteka, panakeia, nutriadapt, footshop; `gpt-6.1-sol`, skript `research/langprobe-2026-10-01/sales.py`):
  - cena 0,42 USD celkem, 0,03–0,08 USD na e-shop (průměr 0,047);
  - citace 62 z 62 doslova nalezeny v textu stránek;
  - domovská země 8 z 9 správně (panakeia.cz správně SK), 1× poctivě „nejisté“ (goodie, chyběla adresa);
  - SK a CZ u všech e-shopů souhlasí s jazykovými verzemi a doménami;
  - výběr stránek 7 z 9 našel dopravu nebo obchodní podmínky (vegis a goodie ne) → pojistka z patičky, z právních stránek ukázky a z konektoru;
  - chyba: freshlabels dostal 26 zemí jako „cílí“ jen kvůli tabulce cen dopravy → tři síly důkazu (silný, doručení, obecné).
- Nalezení a přepnutí jazykových verzí s pojistkami. Ověřeno 1. 10. 2026 (`switchcheck.py`): všech 7 e-shopů s víc verzemi má pro každou verzi vlastní adresu (cesta: havlikovaapoteka; doména: goodie, bonami, footshop, freshlabels, panakeia, nutriadapt), každá verze se načte obyčejným stažením se správným `html lang`, žádný překladový widget ani přepínač bez odkazu.
- Rozbor verzí v ukázce: jazyk textu, vlastní texty, druh rozdílu, povinné stránky, počet produktů, započítání do ceny. Ověřeno 1. 10. 2026 na 46 uložených párech z 5 e-shopů (`validate.py`, 0,043 USD): shoda s ručním čtením 44 z 46 (bonami 10× překlad; havlikovaapoteka 6× překlad, 1× zkráceno; panakeia 9× jiný text; goodie 2× nepřeložený popis). Obě odchylky vznikly tím, že výzkumný skript bral popis i s recenzemi a bloky dopravy; extraktor knihovny hlavní text odděluje.
- Pravidlo „která verze pro které místo prodeje“ a přepočet ceny při odškrtnutí země bez dalšího stahování.
- Výstupy pro tabulky `shop.shop_markets`, `shop.shop_languages`, `content.pages.language` a pro obrazovky 3c a 3d.

**Fáze:** F3 Knihovna po krocích.

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`, část 12: „Čtyři různé věci, které se nesmí slít“, „Místa prodeje e-shopu“ (Proč, Pojem, Zásady, Jak to funguje 1–5, Ověření), „Jazykové verze e-shopu“ (Proč, Jedno pravidlo, Která verze se kontroluje, Najít a přepnout, Co klient uvidí, Cena, Rozbor verzí v ukázce zdarma, Ověření, Přepínání bez JavaScriptu, Pojistky, Cena rozhodnuto); část 11, body 9 a 10;
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.2 (`shops.home_country`, `shops.language`, `shop_markets`, `shop_languages`), 3.3 (`pages.language`, `hreflang_group`, `external_id`), 3.9 (`ref.markets`, `ref.locales`), část 8 fáze F3;
- `navrhy-rozvoje-2026-09-30.md`: návrh 13 (konektory), 19 (další země a jazyky), 21 (Chromium);
- ověřovací skripty a výsledky (jen pro odkaz): `research/langprobe-2026-10-01/sales.py`, `validate.py`, `switchcheck.py`, `pairs.py`, `sales_out/*.json`, `validate_report.json`;
- kód: `src/EshopGuard.Core/Crawl/Crawler.cs`, `UrlTools.cs`, `SitemapParser.cs`, `HttpPageFetcher.cs`, `Extract/ContentExtractor.cs`, `JsonLdReader.cs`, `Fix/IRewriteClient.cs`, `Fix/OpenAiRewriteClient.cs`, `Profiles/ProfileModel.cs` (vzor volání modelu se schématem).

Cesty jsou po přejmenování ze změny 1 a po rozdělení na kroky ze změny 5.

## Scope

In scope:
- **Technické znaky bez modelu** (`MarketSignalReader`): `html lang`, `hreflang` na stránkách a v sitemap (`xhtml:link`), kandidáti přepínače podle stavby odkazu (dvoupísmenný segment cesty, parametr `lang`/`language`/`locale`, jiná doména se stejným prvním návěstím), měny ve strukturovaných datech, telefonní předvolby, doména nejvyššího řádu, seznam odkazů úvodní stránky a patičky.
- **Výběr stránek** (`SalesPageSelector`, model, nejvýš 4 stránky ze seznamu odkazů) s pojistkou: odkazy z patičky, právní stránky ukázky, stránky z konektoru (`IShopPagesSource`).
- **Určení zemí a domovské země** (`PlacesOfSaleModel`, model) s doslovnými citacemi; `QuoteVerifier` ověří každou citaci proti textu citované stránky; neověřená citace se nepoužije.
- **Síla důkazu** pro každou zemi (`strong`, `delivery`, `generic`) a předvyplnění: podporované země se silným důkazem nebo doručením předvyplněné, s obecným ne; nepodporované země uložené skrytě (`unsupported`).
- **Domovská země** z doložené adresy, jinak z domény s potvrzením klientem.
- **Nalezení jazykových verzí** (`LanguageVersionFinder`): `hreflang`, přepínač, cesta, poddoména, doména, `html lang`, jazyky z konektoru, model jako záloha; verze na jiné doméně ve stavu `needs_confirmation`.
- **Přepínání a procházení verzí**: rozsah procházení verze (`VersionCrawlScope`: hostitel, kořenová cesta, vyloučené cesty ostatních verzí), vlastní cookie a hlavička `Accept-Language` po verzích (`FetchRequest.Cookies`, `AcceptLanguage`), oddělené cookie po rozsazích místo sdíleného kontejneru.
- **Pojistky**: přepínač jen v JavaScriptu (`needs_browser`, verze se nekontroluje a klient to ví), překlad až v prohlížeči (kontrolujeme původní text a řekneme to), přesměrování podle IP nebo jazyka (`html lang` neodpovídá → upozornění a nabídka konektoru), aplikace v JavaScriptu (`TextNotLoaded` po verzích).
- **Pravidlo „která verze pro které místo prodeje“** (`VersionMarketPlanner`): verze v jazyce každého zaškrtnutého trhu, jinak hlavní; posouzení podle všech zaškrtnutých zemí, jejichž zákazníci verzi čtou (čeština a slovenština navzájem, `config/markets.yaml`); odškrtnutí země vyřadí verzi, kterou potřebovala jen ta země, a přepočítá počet produktů pro pásmo.
- **Rozbor verzí v ukázce** (`VersionSamplePlanner`, `VersionComparer`, `TextLanguageModel`): jen při víc verzích pro podporované trhy; rozdělení 100 stránek; párování (`hreflang`, ID z konektoru, EAN/GTIN, kód produktu); otisky vět (podíl vlastních textů), druh rozdílu podle délky a počtu vět, jazyk textu jedním voláním modelu nad ~30 úseky hlavního textu na verzi; povinné stránky zvlášť; počet produktů; započítání při ≥ 20 % vlastních textů, při nejistotě nepočítat.
- **Výstupy**: záznamy pro `shop_markets` a `shop_languages`, jazyk stránky pro `pages.language` a `hreflang_group`, kódy a parametry pro větu na 3c a podrobnosti na 3d.
- **Odhad ceny** před každým voláním modelu (`MarketAnalysisEstimate`), v CLI potvrzení, ve workeru rozpočet běhu.
- **CLI** příkaz `eshopguard markets <url>` (`--mock`, `--yes`, `--out`) s výstupem `markets.json`.

Out of scope:
- Obrazovky 3c a 3d, texty vět a nastavení výjimek verzí: změny 10 a 13 (tady jen kódy a parametry).
- Zápis do databáze a orchestrace ukázky po dávkách: změna 8.
- Upozornění „Vyzerá to, že predávate aj …“ při sledování a „Odteraz kontrolujeme aj …“ po přidání podpory země: změny 10 a 16 (tady jen porovnání dvou výsledků rozboru `PlacesOfSaleDiff`).
- Konektory a jejich jazyky a stránky: změna 15 (tady jen rozhraní `IShopLanguageSource`, `IShopPagesSource`).
- Vykreslení v Chromiu pro přepínač jen v JavaScriptu (návrh rozvoje 21).
- Pravidla a verdikty po zemích (změna 6), ceník a pásma (změna 12).
- Ověření vlastnictví e-shopu (architektura část 11, bod 4).

## Approach

1. **Nejdřív struktura, model až potom.** Technické znaky se čtou z dokumentu, který `ExtractStep` (změna 5) už jednou rozparsoval. Model dostane hotové znaky a texty stránek, nehledá v HTML sám.
2. **Model jen s ověřitelnými výroky.** Každá země a domovská země stojí na doslovné citaci z dodaných stránek nebo na technickém znaku. `QuoteVerifier` citaci najde v textu citované stránky (po sjednocení mezer, velikosti písmen a uvozovek); neověřená citace se zahodí a země bez ověřeného důkazu se nenabídne. Důvod v jazyce modelu se klientovi neukazuje; klient vidí sílu důkazu (kód) a ověřené citace v jazyce e-shopu.
3. **Bez slovníků klíčových slov.** Jazyk textu určuje model, ne seznam slov nebo písmen (výzkumný `switchcheck.py` počítal písmena „ě ř ů“ a „ä ľ ĺ ŕ ô“ jen pro rozbor, do knihovny to nejde). Stránky o prodeji vybírá model ze seznamu odkazů, ne podle slov v adrese.
4. **Verze = rozsah procházení.** Každá verze má vlastní `VersionCrawlScope`; hlavní verze vyloučí cesty ostatních verzí na stejném hostiteli. Tím se verze nesmíchají a každá stránka nese jazyk verze.
5. **Fail-closed.** Co nešlo rozhodnout (chybí podmínky doručení, nejistá domovská země, verze dostupná jen v JavaScriptu, nedostatečný vzorek), je ve výsledku jako kód a klient to vidí. Nejistá verze se do ceny nepočítá.
6. **Ověření na stejných e-shopech.** Po implementaci se rozbor zopakuje knihovnou na 9 e-shopech a 46 párech (placené, odhad a souhlas) a porovná s výsledky výzkumu.

## Dependencies

- **Změna 5 `refactor-library-into-pipeline-steps`:** `ExtractStep` (jedno čtení HTML), `FetchStep` a `FetchRequest`, `UrlFrontier`, `DiscoveryStep` se sitemap, `SentenceFingerprint`, `IPageStore`, `IRateLimiter`, ochrana proti SSRF (platí i pro stránky verzí a výběr stránek).
- **Změna 6 `add-multi-jurisdiction-rules-and-rule-texts`:** vyhodnocení pro víc jurisdikcí (verze se posuzuje podle všech zemí, jejichž zákazníci ji čtou); podporované jurisdikce = jurisdikce se zapnutými sadami pravidel.
- Navazují: změna 8 (ukázka zdarma: rozbor, plán 100 stránek, zápis `shop_markets`, `shop_languages`), změna 10 (obrazovky 3c a 3d přes API, potvrzení domény verze), změna 12 (pásmo podle počtu produktů), změna 15 (jazyky a stránky z konektoru), změna 16 (nový silný znak při sledování).

## Done when

- Testy nad novými testovacími e-shopy bez sítě projdou: verze v cestě, verze na jiné doméně, přepnutí cookie, přepnutí jazykem prohlížeče, přepínač jen v JavaScriptu, překlad v prohlížeči, přesměrování na jinou verzi, verze vykreslovaná JavaScriptem.
- `QuoteVerifier` zahodí každou citaci, která není doslova v citované stránce; země bez ověřeného důkazu se nenabídne (test s falešným modelem).
- `VersionMarketPlanner` dává pro kombinace trhů a verzí výsledek podle architektury, část 12 (tabulkový test), a přepočet po odškrtnutí země proběhne bez sítě a modelu.
- `VersionComparer` zařadí páry stejně jako `validate.py` na syntetických párech (shodný text, překlad, zkrácený nebo jiný text, nepřeložený text) a podíl vlastních textů ≥ 20 % rozhodne o započítání.
- Ostré ověření (placené, se souhlasem) na 9 e-shopech: citace ověřené ve všech případech, kde je model uvede; domovská země nejméně 8 z 9; freshlabels nemá 26 zemí se silným důkazem; SK a CZ souhlasí s verzemi a doménami. Na 46 párech shoda s ručním čtením nejméně 44 z 46.
- `eshopguard markets <url> --mock` zapíše `markets.json` s místy prodeje, verzemi a kódy pro 3c a 3d.
- `dotnet test` projde.

## K rozhodnutí

1. **Pravidlo výběru verzí vs. „jedno pravidlo pro všechny varianty“.** Architektura nejdřív říká „kontroluje se každý text, který zákazník může vidět na kterékoli verzi, podle všech zaškrtnutých míst prodeje“, upřesnění z 1. 10. 2026 pak „verze v jazyce každého zaškrtnutého místa prodeje, jinak hlavní“. U českého e-shopu se slovenskou verzí a zaškrtnutým jen Slovenskem by se česká hlavní verze nekontrolovala, i když ji slovenský zákazník může číst. Změna se řídí upřesněním. Potvrdit.
2. **Smí rozbor v ukázce stahovat verzi na jiné doméně před potvrzením klientem** („Patrí goodie.sk k tomuto e-shopu?“)? 5 z 9 ověřených e-shopů má verze na jiné doméně a 46 párů pochází právě z nich. Návrh: v ukázce ano (veřejné stránky, robots.txt, stejné tempo), do ceny a do kontroly až po potvrzení.
3. **Minimální vzorek pro započítání verze.** Architektura: „když vzorek nestačí, verze se do ceny nepočítá“, ale hranici neurčuje. Návrh: aspoň 10 produktových stránek verze s hlavním textem. Neměřeno.
4. **Práh 20 % vlastních textů** je v architektuře „návrh, neměřeno“. Změna ho bere jako nastavení `versions.counted_min_own_share` (0,20). Definice podílu: podíl unikátních vět hlavního textu produktových stránek verze ve vzorku, jejichž otisk se nevyskytuje v žádné jiné kontrolované verzi. Potvrdit definici.
5. **Druh rozdílu „zkrácený“ a „jiný“.** Výzkumný skript je slučuje do jedné skupiny („jiný nebo zkrácený text“); architektura uvádí havlikovaapoteka „1× zkráceno“ a panakeia „9× jiný text“. Bez modelu rozdělit nejde spolehlivě. Návrh: jedna skupina `shortened_or_different` s poměrem délek jako parametrem; rozdělení až po změření.
6. **Rozpoznání překladu v prohlížeči podle podpisu skriptu** (`weglot`, `gtranslate`, `translate.google`, `conveythis`, `localizejs`, `transifex` z `switchcheck.py`). Je to technický znak (jako dnešní `RenderCheck` podle `__NEXT_DATA__`), ne slovník pro klasifikaci textu. Potvrdit, že to zásada „žádné slovníky klíčových slov“ dovoluje.
7. **Sdílené cookie.** Klient pro stahování dnes používá `SocketsHttpHandler` s výchozím `UseCookies = true`, tedy kontejner cookie sdílený všemi požadavky, weby a v budoucnu i tenanty. Změna přepne na `UseCookies = false` a cookie po rozsazích. U webů, které nastavují cookie, to může změnit stažený obsah proti dnešku; nahrávky ze změny 5 to neodhalí (přehrávají odpovědi). Návrh: přijmout (izolace tenantů) a ověřit ostrým stažením vegis.sk a naturfyt.sk (zdarma, se souhlasem).
8. **`shop_markets.source` má jen `detected` a `user`.** Domovská země doplněná z domény potřebuje odlišení. Návrh: `evidence.home_basis = "domain_tld"` v `jsonb`, nebo nová hodnota `domain` (změna 3).
9. **`shop_languages.switch_method` má jen `path`, `subdomain`, `domain`, `query`, `cookie`.** Chybí přepnutí jazykem prohlížeče, přepínač jen v JavaScriptu a překlad v prohlížeči. Návrh: hodnoty `accept_language`, `script`, `browser_translation` (změna 3). Primární klíč (`shop_id`, `language`) neunese dvě verze ve stejném jazyce (nutriadapt má dvě „cs“, footshop `en-GB`, `en-US`, `en-IE`). Návrh: `language` = celý tag BCP 47 a u shody jazyka nepodporovaného trhu uložit jen první; potvrdit.
10. **Předvyplnění podle síly důkazu** nemá v `shop_markets` vlastní sloupec. Návrh: odvodit z `evidence_level` (`strong` a `delivery` předvyplněné) v API (změna 10).
11. **Párování stejnou cestou na jiné doméně** (`pairs.py`, režim `swap:<host>`) architektura neuvádí. Změna ho nepoužívá; když verze nejdou spárovat přes `hreflang`, ID z konektoru, EAN/GTIN ani kód produktu, porovná se podíl vět jedné verze doslova obsažených v druhé (podle architektury).
12. **Tři síly důkazu nejsou ověřené.** `sales.py` měl jen dva stavy (`targets`, `delivers_only`). Změna schéma rozšíří na `strong`, `delivery`, `generic`; ostré ověření na 9 e-shopech je úkol s odhadem ceny a souhlasem.
13. **Výsledek ukázky se mění jen přes 3c.** Pokud klient zaškrtne zemi, kterou rozbor nenašel (např. Česko u e-shopu, který prodává jen tam), uloží se `source = user` bez důkazu. Potvrdit, že klient smí zaškrtnout jakoukoli podporovanou zemi (architektura: „Zaškrtnout jde i Česko u e-shopu, který prodává jen tam“).
