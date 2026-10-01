# Delta for Markets-and-languages

## ADDED Requirements

### Requirement: Technické znaky trhu bez modelu
Systém MUST z úvodní stránky a sitemap přečíst bez volání modelu `html lang`, alternativy `hreflang` (ze stránek i ze sitemap), měny ve strukturovaných datech, telefonní předvolby, doménu nejvyššího řádu, odkazy úvodní stránky a patičky a kandidáty přepínače jazyka podle stavby adresy (segment cesty, parametr jazyka, poddoména, jiná doména se stejným prvním návěstím). Systém MUST NOT určovat jazyk ani zemi podle seznamu slov nebo písmen.

#### Scenario: Verze v cestě podle hreflang
- GIVEN úvodní stránka `path-shop` s `<link rel="alternate" hreflang="sk" href="/sk/">` a `html lang="cs"`
- WHEN `MarketSignalReader` přečte znaky
- THEN `MarketSignals.Hreflang` obsahuje `sk` → `/sk/` a `HtmlLang` je `cs`

#### Scenario: Přepínač odkazem na jinou doménu
- GIVEN úvodní stránka `domain-shop-cz` s odkazem na `https://domain-shop-sk.test/` a bez `hreflang`
- WHEN se přečtou znaky
- THEN odkaz je mezi `SwitcherCandidates` s druhem `domain`

#### Scenario: Měna a předvolba
- GIVEN JSON-LD s `"priceCurrency": "EUR"` a v patičce `tel:+421911705846`
- WHEN se přečtou znaky
- THEN `Currencies` obsahuje `EUR` a `PhonePrefixes` obsahuje `421`

### Requirement: Výběr stránek o prodeji a doručení
Systém MUST nechat model vybrat ze seznamu odkazů úvodní stránky a patičky nejvýš 4 stránky, na kterých e-shop píše o prodeji a doručení, a MUST přijmout jen URL, které v seznamu byly. Když výběr nedá žádnou nebo méně než 4 stránky, systém MUST doplnit stránky z konektoru a právní stránky, které ukázka už stáhla. Když ani potom stránka s podmínkami doručení není, výsledek MUST nést kód `delivery_terms_not_found`.

#### Scenario: Model vrátí adresu mimo seznam
- GIVEN model vrátí 5 URL, z nichž jedna v seznamu odkazů není
- WHEN `SalesPageSelector` výsledek ověří
- THEN použije nejvýš 4 URL a žádnou mimo seznam

#### Scenario: Pojistka u e-shopu bez odkazu na dopravu na úvodní stránce
- GIVEN e-shop jako vegis.sk, kde model z úvodní stránky nevybral stránku dopravy ani obchodních podmínek, a ukázka stáhla právní stránku `/obchodne-podmienky`
- WHEN se sestaví stránky pro rozbor
- THEN obsahují `/obchodne-podmienky`

#### Scenario: Chyba modelu
- GIVEN `IMarketModel` vyhodí `RewriteApiException` (ne fatální)
- WHEN probíhá výběr
- THEN rozbor míst prodeje skončí s kódem `market_analysis_failed`, domovská země se doplní z domény k potvrzení a nic se nevydává za zjištěné

### Requirement: Určení zemí s ověřenými citacemi
Systém MUST nechat model určit země, kde e-shop prodává, a domovskou zemi, každou s doslovnými citacemi z dodaných stránek nebo s technickým znakem. Každá citace MUST být ověřená proti textu stránky (po sjednocení mezer, velikosti písmen a krajních uvozovek); neověřená citace MUST být zahozená a započítaná v `QuotesDropped`. Země bez ověřeného důkazu MUST NOT být nabídnutá. Volný text důvodu od modelu MUST NOT se ukazovat klientovi.

#### Scenario: Citace je doslova na stránce
- GIVEN model cituje „Tovar zasielame aj do zahraničia“ ze stránky dopravy, kde ta věta je
- WHEN `QuoteVerifier` ověří citaci
- THEN citace platí a je u země s URL zdroje

#### Scenario: Vymyšlená citace
- GIVEN model u země `HU` cituje větu, která v žádné dodané stránce není, a jiný důkaz nemá
- WHEN proběhne ověření
- THEN citace se zahodí, `HU` je v `Rejected` s kódem `no_verified_evidence` a nenabídne se

#### Scenario: Citace ze špatně uvedené stránky
- GIVEN citace, která není na uvedené stránce, ale doslova je na jiné dodané stránce
- WHEN proběhne ověření
- THEN citace platí se zdrojem opraveným na stránku, kde je, a s příznakem `source_corrected`

#### Scenario: Neexistující technický znak
- GIVEN model cituje `signal: hreflang=pl`, ale `MarketSignals` žádnou alternativu `pl` nemá
- WHEN proběhne ověření
- THEN znak se jako důkaz nepoužije

### Requirement: Síla důkazu a předvyplnění podporovaných zemí
Systém MUST u každé ověřené země určit sílu důkazu `strong` (vlastní jazyková verze nebo doména, měna, sídlo, místní úřad v podmínkách), `delivery` (konkrétní podmínky doručení do té země: cena, dopravce) nebo `generic` (obecné doručení, např. do celé EU). Podporované země se silou `strong` nebo `delivery` MUST být předvyplněné, se silou `generic` MUST NOT. Země, jejíž všechny ověřené citace jsou totožné s citací obecného doručení do EU, MUST mít nejvýš `generic`.

#### Scenario: Tabulka cen dopravy
- GIVEN e-shop jako freshlabels s tabulkou cen dopravy do 26 zemí a vlastními doménami pro SK a CZ
- WHEN se určí síla důkazu
- THEN SK a CZ mají `strong` a jsou předvyplněné
- AND země jen z tabulky mají `delivery`, nikdy `strong`

#### Scenario: Obecné doručení do EU
- GIVEN jediný důkaz pro CZ je věta „Doručujeme do celej EÚ“
- WHEN se určí síla důkazu
- THEN CZ má `generic` a není předvyplněné

#### Scenario: Vlastní verze zvedne sílu důkazu
- GIVEN model u SK uvedl jen `delivery` a e-shop má slovenskou verzi na vlastní cestě `/sk/`
- WHEN se určí síla důkazu
- THEN SK má `strong`

### Requirement: Podporované a nepodporované země
Systém MUST klientovi nabízet jen země, které umí kontrolovat (trhy z `MarketCatalog.Supported`: zapnuté sady pravidel pro jurisdikci a povolení hostitele). Zjištěné nepodporované země MUST se uložit se stavem `unsupported` a MUST NOT se ukazovat. Seznam podporovaných zemí MUST jít rozšířit jen daty (řádek v číselníku trhů a pravidla s jurisdikcí), bez změny kódu.

#### Scenario: Polsko zatím nepodporujeme
- GIVEN rozbor zjistí `PL` se silou `strong`
- WHEN se sestaví výsledek
- THEN `PL` má v `ShopMarketRow` stav `unsupported` a v `Summary` pro 3c není

#### Scenario: Přibude podpora země
- GIVEN uložený výsledek s `PL` ve stavu `unsupported` a nový řádek trhu `pl` se zapnutými pravidly
- WHEN se výsledek znovu vyhodnotí proti `MarketCatalog`
- THEN `PL` je podporovaná a nabídnutá (oznámení „Odteraz kontrolujeme aj …“ posílá změna 16)

### Requirement: Domovská země
Systém MUST určit domovskou zemi z ověřené citace (sídlo, adresa provozovatele). Když ji nejde doložit, MUST ji doplnit z domény nejvyššího řádu podle číselníku trhů s příznakem `home_country_from_domain` a s potřebou potvrzení klientem. Když doména zemi neurčuje, výsledek MUST nést kód `home_country_unknown` a domovská země MUST zůstat prázdná.

#### Scenario: Slovenská firma na české doméně
- GIVEN panakeia.cz s ověřenou citací slovenské adresy provozovatele
- WHEN se určí domovská země
- THEN je `SK` se zdrojem citace, i když doména je `.cz`

#### Scenario: Adresa na vybraných stránkách chybí
- GIVEN goodie.cz bez adresy provozovatele na vybraných stránkách
- WHEN se určí domovská země
- THEN je `CZ` s `home_country_from_domain` a označením k potvrzení na 3c

#### Scenario: Doména bez země
- GIVEN e-shop na doméně `.com` bez doložené adresy
- WHEN se určí domovská země
- THEN zůstane prázdná s kódem `home_country_unknown`

### Requirement: Nalezení jazykových verzí
Systém MUST najít jazykové verze e-shopu z `hreflang` na stránkách a v sitemap, z přepínače, z `html lang`, z adres verzí (cesta, poddoména, doména, parametr), z jazyků konektoru a teprve když to ze stavby nejde poznat, z výsledku modelu. Verze na jiné doméně, než je doména e-shopu nebo její poddoména, MUST mít stav `needs_confirmation` a MUST NOT se kontrolovat ani počítat do ceny, dokud ji klient nepotvrdí. Verze v jazyce nepodporovaného trhu MUST mít stav `unsupported`.

#### Scenario: Verze v cestě
- GIVEN `path-shop` s alternativou `sk` na `/sk/`
- WHEN `LanguageVersionFinder` hledá verze
- THEN najde `cs` (hlavní, `/`) a `sk` (`/sk/`, `SwitchMethod = path`, `Source = hreflang`, stav `active`)

#### Scenario: Verze na jiné doméně
- GIVEN `domain-shop-cz` s přepínačem na `domain-shop-sk.test`
- WHEN se hledají verze
- THEN verze `sk` má `SwitchMethod = domain` a stav `needs_confirmation`

#### Scenario: Model jako záloha
- GIVEN web bez `hreflang` a bez kandidátů přepínače, kde model v rozboru uvedl verzi `sk` na `https://shop.test/?lang=sk`
- WHEN se hledají verze
- THEN verze `sk` má `Source = llm` a ověří se stažením (`VersionAccessProbe`) dřív, než se použije

#### Scenario: Polská verze
- GIVEN verze `pl` a trhy SK a CZ podporované
- WHEN se hledají verze
- THEN verze `pl` má stav `unsupported` a v plánu kontroly ani v 3c není

### Requirement: Přepínání jazykových verzí a pojistky
Systém MUST procházet verzi s vlastní adresou jako samostatný rozsah (hostitel, kořenová cesta, vyloučené cesty ostatních verzí), verzi přepínanou cookie s vlastní cookie a verzi přepínanou jazykem prohlížeče s vlastní hlavičkou `Accept-Language`. Cookie MUST být oddělené po rozsazích procházení, nikdy sdílené mezi weby. Verze dostupná jen přes JavaScript MUST mít stav `needs_browser` a MUST se nekontrolovat; překlad až v prohlížeči MUST vést ke kontrole původního textu s kódem `version_browser_translation`; vstupní stránka verze s jiným `html lang` MUST vést ke kódu `version_language_mismatch` a nabídce konektoru; stránky verze bez načteného textu MUST se hlásit po verzích.

#### Scenario: Verze se nesmíchají
- GIVEN `path-shop` se sitemap, která obsahuje stránky `/` i `/sk/`
- WHEN se projde hlavní verze `cs`
- THEN žádná stránka pod `/sk/` v ní není a každá stránka verze `sk` má `PageInfo.Language = sk`

#### Scenario: Přepnutí cookie
- GIVEN `cookie-shop`, kde `?lang=sk` nastaví cookie `lang=sk` a stránky pak vrací slovensky
- WHEN `VersionAccessProbe` verzi `sk` vyzkouší a crawler ji projde
- THEN všechny požadavky verze `sk` nesou cookie `lang=sk` a požadavky verze `cs` ne

#### Scenario: Přepínač jen v JavaScriptu
- GIVEN `script-switch-shop` s tlačítkem jazyka bez adresy (`onclick`) a bez cookie po stažení
- WHEN se verze vyzkouší
- THEN verze má stav `needs_browser`, nekontroluje se a výsledek nese kód `version_needs_browser`

#### Scenario: Server vrátí jinou verzi
- GIVEN `redirect-shop`, který na `Accept-Language: sk` i na adresu verze vrací stránku s `html lang="cs"`
- WHEN se verze `sk` vyzkouší
- THEN má stav `mismatch` s kódem `version_language_mismatch` a nabídkou konektoru

### Requirement: Která verze se kontroluje pro které místo prodeje
Systém MUST pro každý zaškrtnutý trh kontrolovat verzi v jeho jazyce, a když není, hlavní verzi. Každou kontrolovanou verzi MUST posoudit podle všech zaškrtnutých zemí, jejichž zákazníci ji můžou číst (čeština a slovenština navzájem podle `readable_languages`). Odškrtnutí země MUST vyřadit verzi, kterou potřebovala jen ta země, a MUST přepočítat počet produktů pro pásmo bez stahování a bez volání modelu.

#### Scenario: Český e-shop se slovenskou verzí, oba trhy
- GIVEN verze `cs` (hlavní) a `sk` a zaškrtnuté trhy SK a CZ
- WHEN `VersionMarketPlanner` sestaví plán
- THEN kontroluje se `cs` podle `cz` a `sk` a verze `sk` podle `sk` a `cz`

#### Scenario: Trh bez vlastní verze
- GIVEN jen verze `cs` a zaškrtnuté trhy SK a CZ
- WHEN se sestaví plán
- THEN kontroluje se `cs` podle `cz` a `sk`

#### Scenario: Odškrtnutí Česka
- GIVEN plán s verzemi `cs` a `sk`, obě započítané, a klient na 3c odškrtne CZ
- WHEN se zavolá `VersionPlan.Recalculate`
- THEN kontroluje se jen `sk` podle `sk` a `CountedProducts` klesne o počet produktů verze `cs`
- AND nic se nestahuje a model se nevolá

#### Scenario: Verze vyřazená klientem
- GIVEN verze `sk` se stavem `excluded` (nastavení e-shopu „túto verziu nekontrolovať“)
- WHEN se sestaví plán pro trh SK
- THEN kontroluje se hlavní verze a plán uvádí `sk` jako vyřazenou

### Requirement: Rozbor jazykových verzí v ukázce
Systém MUST rozbor verzí spustit jen tehdy, když e-shop má víc verzí pro podporované trhy. Rozbor MUST rozdělit vzorek 100 stránek na ~20 spárovaných produktů (`hreflang`, ID z konektoru, EAN/GTIN, kód produktu), povinné stránky každé verze a náhodné produkty po verzích; MUST určit jazyk textu jedním voláním modelu nad ~30 větami hlavního textu produktů na verzi; MUST spočítat podíl vlastních textů podle otisků vět, druh rozdílu u párů podle délky a počtu vět, rozdíl povinných stránek a počet produktů. Verze MUST se započítat do ceny jen při podílu vlastních textů aspoň `counted_min_own_share` (0,20) a dostatečném vzorku; při nejistotě MUST se nezapočítat.

#### Scenario: Věrný překlad
- GIVEN 10 párů, kde slovenské texty mají poměr délky 0,9–1,1 a stejný počet vět jako české a jsou slovensky
- WHEN `VersionComparer` porovná páry
- THEN všechny mají druh `translation` a verze `sk` má podíl vlastních textů nad 0,20 a `Counted = true`

#### Scenario: Přeložené jen menu
- GIVEN verze `sk`, jejíž hlavní texty produktů mají stejné otisky vět jako verze `cs`
- WHEN se porovná
- THEN páry mají druh `identical`, podíl vlastních textů je pod 0,20, `Counted = false`
- AND `Summary` má kód `versions_same_texts_menu_only`

#### Scenario: Český text na slovenské verzi
- GIVEN 2 z 10 párů, kde převažující jazyk vět slovenské verze je `cs`
- WHEN se porovná
- THEN tyto páry mají druh `untranslated`, verze nese upozornění `untranslated_text` s příklady
- AND upozornění není porušení a pravidla se na text použijí stejně

#### Scenario: Malý vzorek
- GIVEN verze `sk`, ze které se podařilo stáhnout jen 4 produktové stránky s hlavním textem
- WHEN se porovná
- THEN `Counted = false` s kódem `version_sample_insufficient` a verze se do ceny nepočítá

### Requirement: Výstupy pro místa prodeje, jazykové verze a obrazovky onboardingu
Systém MUST vrátit `MarketsAnalysisResult` se záznamy pro `shop_markets` (země, domovská, stav, síla důkazu, zdroj, ověřené citace a znaky, předvyplnění), pro `shop_languages` (jazyk, adresa, způsob přepnutí, zdroj, stav, podíl vlastních textů, podíl jazyků, porovnání, započítání, počet produktů), jazyk a skupinu alternativ každé stránky, jednu větu pro 3c jako kód s parametry a podrobnosti pro 3d. Výsledek MUST NOT obsahovat hotové věty pro klienta.

#### Scenario: Dvě verze s vlastními texty
- GIVEN verze `cs` a `sk`, `sk` s podílem vlastních textů 0,96
- WHEN se sestaví výsledek
- THEN `Summary` má kód `versions_both_own_texts` s parametry `languages = [cs, sk]` a `share = 0.96`
- AND `ShopLanguageRow` verze `sk` má `Counted = true`

#### Scenario: Verze čeká na potvrzení domény
- GIVEN verze `sk` na `goodie.sk` ve stavu `needs_confirmation`
- WHEN se sestaví výsledek
- THEN `Summary` má kód `version_other_domain_needs_confirmation` s parametrem `domain = goodie.sk`

#### Scenario: CLI zapíše výsledek
- GIVEN `eshopguard markets http://localhost:8000 --allow-private-network --mock` nad testovacím e-shopem `path-shop`
- WHEN příkaz skončí
- THEN `markets.json` obsahuje místa prodeje, verze, plán a souhrn

### Requirement: Odhad ceny před voláním modelu při rozboru
Systém MUST před každým voláním modelu při rozboru míst prodeje a jazykových verzí spočítat odhad ceny (tokeny = znaky / 3,2; ceny z `rewrite.input_usd_per_million` a `rewrite.output_usd_per_million`). V CLI MUST odhad vypsat a nad `rewrite.max_usd_without_confirm` se zeptat; bez souhlasu MUST NOT model volat. Klíč OpenAI MUST NOT se objevit v logu ani ve výstupu.

#### Scenario: Uživatel CLI odmítne
- GIVEN `eshopguard markets https://shop.example` bez `--yes` a odpověď „ne“
- WHEN běží
- THEN model se nezavolá a `markets.json` obsahuje jen technické znaky a kód `market_analysis_not_confirmed`

#### Scenario: Chybí klíč OpenAI
- GIVEN prázdný `OPENAI_API_KEY` a bez `--mock`
- WHEN běží rozbor
- THEN skončí s kódem `model_missing_key`, technické znaky a verze ze stavby se vrátí a nic se nevydává za ověřené

#### Scenario: Skutečná spotřeba se zapíše
- GIVEN rozbor jednoho e-shopu s modelem
- WHEN skončí
- THEN `MarketsAnalysisResult.Usage` obsahuje počet volání, vstupní, z mezipaměti a výstupní tokeny a cenu v USD
