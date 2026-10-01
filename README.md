# EshopGuard

Testovací prototyp kontroly textů e-shopů: projde web, vytáhne texty a pomocí modelu Jev (TypeSafe) v nich hledá rizikové environmentální tvrzení, nekalé praktiky z černé listiny a chybějící povinné informace. Výstup je screening, ne právní posouzení. Zadání je v `../Zadání CLI prototyp kontroly textů e-shopu s Jevem.md`, stažené předpisy a podklady v `../podklady/`.

Stav:

- **M1 hotový:** stahování, extrakce, segmentace, deduplikace.
- **M2 hotový:** pravidla v YAML, falešný klient Jevu, vyhodnocení a všechny výstupy.
- **M3 hotový:** skutečný klient Jevu (limit požadavků, opakování při 429/529/5xx, počítání tokenů a ceny), SQLite cache.
- **Katalog kontrol, první sady (26. 9. 2026):** `eco` draft7 a nový modul `ucp` draft3 (viz `rules/CHANGELOG.md`). Primárním trhem je Slovensko, kde od 27. 9. 2026 platí zákazy podle směrnice EmpCo. Živý test na testovacím e-shopu prochází.
- **M4:** evaluace přesnosti.

Po úpravě `rules/*.yaml` sestavte projekt znovu (`dotnet build`), protože testy i CLI si soubory pravidel kopírují při sestavení.

## Moduly

Rozsah podle země: **Slovensko** = nové povinnosti (od 27. 9. 2026 a od 19. 6. 2026), co trestá SOI, a zákonné informační povinnosti; **Česko** = co trestá ČOI a zákonné informační povinnosti. Bez volby `--modules` běží všechny moduly, které mají pravidla pro zvolenou zemi.

| Modul | Soubor | Co kontroluje | Země | Čte |
| --- | --- | --- | --- | --- |
| `eco` | `rules/eco.yaml` | Environmentální tvrzení podle směrnice (EU) 2024/825 (EmpCo), nové od 27. 9. 2026: obecná tvrzení, „udržitelný“, klimatická neutralita a kompenzace, značky udržitelnosti, tvrzení o celku, budoucí závazky. | SK | věty |
| `dur` | `rules/dur.yaml` | Nové od 27. 9. 2026: tvrzení o životnosti, opravitelnosti, spotřebním materiálu, neoriginálních dílech a aktualizacích softwaru (vše k ověření). | SK | věty |
| `ucp` | `rules/ucp.yaml` | Spotřebitelské recenze z černé listiny (body 23b a 23c), za které ČOI i SOI trestají. Ostatní body černé listiny jsou vypnuté v `rules/ucp_parked.yaml`. | SK, CZ | věty |
| `lr` | `rules/lr.yaml`, `config/legal_requirements.yaml` | Rozpracováno, vypnuto (`enabled: false`): zákonné požadavky na všechny výrobky kategorie vydávané za přednost („dojčenská fľaša bez BPA“) a bezvýznamné výhody, nové od 27. 9. 2026. Kategorie výrobku se čte z titulku, nadpisu, drobečkové navigace a kategorie e-shopu. | SK | věty |
| `legal` | `rules/legal_sk.yaml`, `rules/legal_cz.yaml` | Povinné informace na právních stránkách: mimosoudní řešení sporů, reklamace, odstoupení, vzorový formulář. Na Slovensku navíc za celý web (`site_signal`): harmonizované oznámení o zákonné záruce (od 27. 9. 2026), tlačítko „odstúpiť od zmluvy tu“ (od 19. 6. 2026) a odkaz na zrušenou platformu ODR. | SK, CZ | odstavce právních stránek, celý web |

**Síto po odstavcích.** Než věty dostanou podrobné otázky, rozdělí se hlavní text každé stránky (kromě právních) na úseky po sobě jdoucích bloků do 600 znaků a Jev u každého úseku jednou otázkou na modul řekne, zda v něm je téma modulu (příroda a značky, životnost a opravy, recenze). Věty úseku jdou na podrobné otázky modulu jen při pravděpodobnosti aspoň 0,2. Patička, hlavička, titulek, meta popis a právní stránky jdou vždy celé; úsek, který Jev nevyhodnotí (chyba, text delší než 20 000 znaků), pustí své věty do všech modulů, takže chyba síta nikdy nález neschová. Na vegis.sk (103 stránek) a naturfyt.sk (40 stránek) síto nevynechalo žádný nález a snížilo cenu na 45 % a 57 %; vypíná se volbou `--no-sieve`. Jev přijme asi 32 000 tokenů na požadavek (80 000 znaků slovenského textu prošlo, 90 000 ne), úseky jsou tedy hluboko pod limitem.

**Skupiny nálezů.** Zpráva dělí nálezy podle toho, nakolik je rozhoduje text zákona (pole `checkability` pravidla):

- **porušení podle textu zákona** (`text`): text splňuje znaky zákazu tak, jak je popisuje zákon nebo jeho odůvodnění, například „ekologický“ nebo „šetrný k životnímu prostředí“ bez upřesnění (příklady z odůvodnění 9 směrnice (EU) 2024/825);
- **k posouzení** (`assess`): záleží na tom, jak text chápe průměrný spotřebitel, a Komise to posuzuje případ od případu, například „prírodný“, „BIO“ u kosmetiky nebo odznak „Vegan“;
- **k ověření** (`verify`): záleží na faktech mimo web, například na certifikaci jmenované značky nebo na košíku, který nástroj nestahuje.

Jistota (vysoká, nižší) říká jen, jak si je Jev jistý, že text odpovídá popisu pravidla; zda jde o porušení, určuje skupina.

Každá věta jde do větných modulů (na Slovensku `eco`, `dur` a `ucp`, v Česku `ucp`) jedním voláním Jevu se všemi otázkami všech modulů naráz; cache ukládá odpovědi zvlášť po modulech, takže nová verze jedné sady se ptá znovu jen na své otázky. Na vegis.sk (108 stránek) to snížilo počet volání z 21 182 na 7 084, cenu z 0,98 na 0,75 USD a dobu vyhodnocení zhruba třikrát; odpovědi se od volání po modulech liší stejně málo jako dva běhy téhož způsobu (72 % odpovědí úplně stejných, průměrný rozdíl 0,003). Menu, drobečková navigace, seznamy odkazů a popisky filtrů se nevyhodnocují; odznaky u produktu („Eco“, „Vegan“) se čtou každý zvlášť. Stejný text v titulku nebo meta popisu jako v obsahu stránky dává jeden nález a stejný text se stejným pravidlem na více stránkách také jeden nález se seznamem stránek. Právní odkazy jsou u pravidel zvlášť pro EU, Slovensko a Česko; zpráva vypíše ty pro zemi z `--country`. Pravidla, u kterých se Česko liší (EmpCo zatím nepřevzalo, sněmovní tisk 53), mají v `explanation_by_jurisdiction` vlastní vysvětlení: v Česku jde zatím jen o posouzení klamavého konání případ od případu. Zprávy a české znění otázek jsou česky, Jev dostává otázky anglicky (`--question-lang`).

## Požadavky

- .NET SDK 10.0.4xx (verze je zafixovaná v `global.json`).

## Sestavení a testy

```bash
dotnet build
dotnet test
```

Testy nepotřebují síť. Testovací e-shop se čte ze souborů v `tests/EshopGuard.Core.Tests/Fixtures/site/` a testy používají skutečné soubory pravidel z `rules/` a `config/labels.yaml`.

Testy s kategorií `Jev` projdou proti skutečnému API český testovací e-shop (`Fixtures/site`, země cz, očekávání v `Fixtures/expected_findings.json`) a slovenský (`Fixtures/site-sk`, země sk, `Fixtures/expected_findings_sk.json`). Běží, jen když je k dispozici klíč (`JEV_API_KEY` nebo `TYPESAFE_API_KEY`), a stojí dohromady asi 0,02 USD. Bez něj:

```bash
dotnet test -- --filter-not-trait "Category=Jev"
```

Jen živý test:

```bash
dotnet test -- --filter-trait "Category=Jev"
```

## Nastavení

- `config/settings.yaml`: limity stahování, pravidla segmentace a ceny. Před skenováním cizího webu doplňte do `user_agent` skutečný kontakt.
- `config/labels.yaml`: seznamy značek podle rešerše `podklady/reserse/znacky-udrzatelnosti.md` (nález ruší jen značky, které podmínky splňují), poznámky ke značkám, které je nesplňují nebo jsou neověřené (`label_notes`), a slova pro kontrolu obrázků.
- `config/legal_requirements.yaml`: seznam zákonných požadavků pro modul `lr` (61 položek z rešerše `podklady/reserse/zakonne-poziadavky-ako-prednost.md`).
- `config/sieve.yaml`: síto po odstavcích (otázka na téma každého větného modulu, práh, velikost úseků); vlastní `version`, takže změna síta nezneplatní uložené podrobné odpovědi.
- `rules/*.yaml`: otázky pro Jev a pravidla. Každou změnu znění zapište do `rules/CHANGELOG.md` a zvyšte `version`.
- `.env` (zkopírujte z `.env.example`): klíč a adresa API Jevu. Když `JEV_API_KEY` chybí, použije se proměnná prostředí `TYPESAFE_API_KEY` (i z uživatelského prostředí Windows). Klíč se nikam nezapisuje ani neloguje.
- Tempo stahování se přizpůsobuje serveru: začíná na `crawl.requests_per_second` (1 za sekundu); dokud server odpovídá do 0,5 s bez chyb, zrychluje po 0,25 až na `max_requests_per_second` (3), při odpovědi pomalejší než 1,5 s nebo chybě zpomalí na 70 %, při 429 nebo 503 na polovinu a počká podle Retry-After (nejvýš 60 s) a stránku zkusí znovu (nejvýš dvakrát). Crawl-delay z robots.txt strop sníží. Stahuje se jedním spojením. `--rate` nastaví pevné tempo. Na vegis.sk (107 stránek, server odpovídá za 0,1 s) 111 požadavků za 42 s místo 3,6 min při pevných 0,5 za sekundu.
- Souběžnost: `jev.requests_per_minute: 1200` a `concurrency: 8`, podle dokumentovaného limitu jev-1.13.0 (1 200 požadavků za minutu a 250 000 tokenů za sekundu, https://docs.typesafe.ai/models; limity se mohou měnit, vyšší nabízí firemní tarif). Při odezvě kolem 0,33 s stačí 8 souběžných požadavků na 20 za sekundu. Při odpovědi 429 nebo 529 klient počká a zkusí to znovu. Krátký test 300 požadavků s 32 souběžnými spojeními (77 za sekundu) chybu nevrátil jen proto, že se vešel pod minutový limit.
- Cache odpovědí Jevu je v `cache/jev-cache.sqlite`. Opakovaný běh se stejnými texty a otázkami nic nestojí; změna znění otázky (nová `version`) cache pro danou sadu obejde. `--no-cache` ji vypne úplně.

## Spuštění na testovacím e-shopu

V jednom terminálu spusťte testovací e-shop:

```bash
dotnet run --project src/EshopGuard.Cli -- serve-fixture --port 8000
```

Ve druhém terminálu ho projděte. Během běhu serveru použijte `--no-build`, protože server drží sestavené soubory:

```bash
dotnet run --no-build --project src/EshopGuard.Cli -- scan http://localhost:8000
```

Bez klíče nebo pro zkoušku bez placených volání přidejte `--mock`. Před voláním Jevu se vypíše odhad počtu volání, tokenů a ceny; nad limitem `max_calls_without_confirm` (5 000) se čeká na potvrzení, `--yes` ho přeskočí.

Výstupy jsou ve složce `out/<doména>-<YYYYMMDD-HHMM>/`:

| Soubor | Obsah |
| --- | --- |
| `report.md` | Zpráva pro člověka v češtině |
| `findings.json` | Všechny nálezy se všemi poli a pravděpodobnostmi otázek |
| `findings.csv` | Řádek na nález a prázdné sloupce `human_label` a `note` pro ruční označení |
| `segments.csv` | Všechny unikátní segmenty s kontextem a pravděpodobností každé otázky |
| `pages.jsonl` | Řádek na staženou stránku: adresa, typ, titulek, vytažený hlavní text, počet znaků čitelného textu v HTML a zda se text načetl |
| `sieve.csv` | Každý úsek hlavního textu, který prošel sítem: stránka, pořadí, stav a pravděpodobnost tématu každého modulu |
| `profiles.json` | Profily šablon stránek použité ve skenu: oblasti se selektorem, akcí a důvodem, vzorové stránky, počet stránek a vynechané znaky podle role |
| `run.log` | Průběh běhu |

Hlavní text stránky vybírá SmartReader, který může vynechat například blok s odznaky, cenou a dopravou. Proto se kontroluje i **ostatní viditelný text stránky**: vše, co není hlavní text, hlavička, patička ani navigace (menu, seznamy kategorií, drobečková navigace, filtry). V `pages.jsonl` je jako `rest_text` a segmenty mají zdroj `rest`. **Výpisy jiných produktů** (podobné produkty, doporučené produkty na úvodní stránce) se na cizí stránce nekontrolují. Jejich texty se kontrolují na stránce produktu, odkud pocházejí. Rozpoznávají se podle struktury, ne podle nadpisu: aspoň 3 stejné krátké dlaždice s cenou a odkazem na jinou stránku webu. Při kontrole jen vzorku stránek se proto produkty mimo vzorek nekontrolují.

Zpráva uvádí **pokrytí**: kolik viditelného textu se zkontrolovalo, kolik připadlo na záměrně vynechanou navigaci a výpisy jiných produktů a kolik zůstalo jinak nezkontrolované. Stránky, kde nezkontrolovaný podíl mimo navigaci dosáhne 20 %, zpráva vypíše.

**Profily šablon stránek** (`profiles` v `settings.yaml`) odstraňují ovládací prvky, které rozpoznávání podle struktury nezachytí: cookie lištu, přihlášení, košík, pole a souhlasy formulářů, filtry, záložky. Postup:

1. Po stažení se každá stránka porovná s uloženými profily obchodu. Použije ten, který nechá nejméně jejího textu mimo známé oblasti. Navigace a dlaždice jiných produktů se počítají jako známé vždy. Když i nejlepší profil nechá mimo víc než `max_unknown_share` (10 %), stránka nesedí žádnému.
2. Nesedící stránky se seskupí podle stavby (podobnost názvů prvků a tříd aspoň `template_similarity`, 0,75). Skupina aspoň `min_template_pages` (3) stránek dostane nový profil: model přepisu (`rewrite.model`) dostane zjednodušenou kostru `sample_pages` (3) vzorových stránek a vrátí oblasti šablony se selektory a akcí „kontrolovat“ nebo „vynechat“. První profil obchodu vidí i úvodní stránku a stránku jiné skupiny, aby se naučil společný rámec. Za sken vznikne nejvýš `max_new_profiles_per_scan` (5) profilů. Jejich cena je v odhadu před spuštěním, nad `max_usd_without_confirm` se potvrzuje.
3. Profil se ověří na vzorových stránkách a uloží do souboru cache (tabulka `page_profiles`). Další skeny ho použijí bez modelu, takže rozdíly mezi skeny pocházejí z obchodu, ne z modelu. Nový profil vznikne jen tehdy, když stránky přestanou sedět: obchod změní šablonu nebo přibude sekce s jinou stavbou.

Profil jen ubírá, a to bloky hlavičky, patičky a ostatního textu. **Hlavní text nikdy.** Vynechat smí jen role navigace, výpisy produktů, cookie lišta, přihlášení, vyhledávání, košík, sdílení a pole formulářů. Jinou roli model vynechat nemůže, oblast se kontroluje. Přeskakovací oblast se na stránce nepoužije, když obsahuje oblast ke kontrole nebo delší blok hlavního textu: selektor tam zjevně zachytil něco jiného. Při ověřování na vzorových stránkách se taková oblast změní na kontrolovanou. Blok, který je i mimo vynechané oblasti (odznak u produktu i u podobného produktu), zůstává. Právní stránky a nenačtené stránky profil nepoužívají. Pravidla, která hledají povinné údaje kdekoli na stránce, vidí i vynechané části. Co se vynechalo, je v `pages.jsonl` (`profile_id`, `profile_unknown_share`, `profile_skipped_text`) a v `profiles.json`. Bez klíče OpenAI nebo s `--mock` se nové profily nevytvářejí a stránky bez profilu se kontrolují celé.

Nástroj stahuje HTML a JavaScript nespouští. Stránka, která má v HTML méně než `crawl.min_page_text_chars` (200) znaků čitelného textu včetně menu a patičky, se hlásí jako nenačtená: web ji nejspíš vykresluje až JavaScriptem. Zpráva ji vypíše v části „Co nebylo zkontrolováno“ i se znakem aplikace (Next.js, Nuxt, Angular, prázdný kontejner, hláška „zapněte JavaScript“), v souhrnu a v upozorněních řekne, že u ní chybějící nálezy neznamenají „v pořádku“, a nálezy o chybějících informacích na celém webu doplní poznámkou. Zkontrolovaný zůstává jen titulek, meta popis a popis z JSON-LD. Když se nenačte aspoň polovina stránek, zpráva doporučí připojení přes konektor nebo feed. Části stránek, které se dotahují dodatečně (widgety recenzí, odpočty), takto rozpoznat nejde.

## Jeden text

```bash
dotnet run --project src/EshopGuard.Cli -- check-text "Ekologický šampon. Obal je ze 100 % recyklovaného papíru."
```

Vypíše pro každou větu pravděpodobnost každé otázky a výsledek každého pravidla. `--kind legal` vyhodnotí text jako právní stránku.

## Přepis problematických pasáží

```bash
dotnet run --project src/EshopGuard.Cli -- rewrite out/shop.sk-20260930-0919 --limit 5
```

Vezme výsledky hotového skenu (`findings.json`, `pages.jsonl`) a stránky s nálezem ve skupině porušení nebo k posouzení pošle modelu OpenAI (`rewrite.model`, výchozí `gpt-6.1-sol`). Model dostane celý text stránky v číslovaných blocích a vrátí jen změněné bloky („původně → nově“), místa „[doplňte: …]“ pro fakta, která zná jen obchod, a zdůvodnění; nálezy, jejichž znění jen popisuje složení nebo původ, může ponechat. Nástroj každý změněný blok znovu zkontroluje svými pravidly (se sousedními bloky jako okolím) a označí ho jako vyřešeno, čeká na doplnění, stále nález nebo ponecháno. Výstupem je `rewrite.md` (u každé stránky změny „původně → nově“, zdůvodnění a celý opravený text se zvýrazněnými změnami) a `rewrite.json` ve složce skenu; v něm má každá stránka pole `blocks` s každým blokem textu před přepisem a po něm (`original`, `rewritten`, `changed`), podklad pro zobrazení rozdílů ve webu.

- Zadání je v `config/rewrite.yaml`: pokyny, příklady špatných a dobrých znění a doslovné výňatky ze zákona, směrnice a výkladu Komise. Tato společná část jde v každém požadavku první a je pro všechny stránky stejná, takže ji OpenAI po první stránce účtuje z mezipaměti (0,10 místo 2,00 USD za milion tokenů); stránka a její nálezy jdou až za ni. Každou změnu zadání zapište do `rules/CHANGELOG.md` a zvyšte `version`.
- Klíč je v `OPENAI_API_KEY` (`.env`, proměnná prostředí nebo uživatelské prostředí Windows); nikam se nezapisuje. Na kontrolu přepisů je potřeba i klíč Jevu.
- Před voláním vypíše odhad ceny; nad `rewrite.max_usd_without_confirm` (1 USD) se zeptá, `--yes` dotaz přeskočí. Hotové přepisy se ukládají do `cache/jev-cache.sqlite` (tabulka `rewrite_cache`) podle stránky, nálezů, modelu a verze zadání, takže opakovaný běh nic nestojí.
- Měřeno 30. 9. 2026: vegis.sk 24 stránek, 36 nálezů za 0,25 USD (91 s), naturfyt.sk 12 stránek, 28 nálezů za 0,15 USD (81 s); z mezipaměti OpenAI 78–81 % vstupu. Kontrola přepisů Jevem stojí setiny centu.
- Návrhy píše jazykový model: před zveřejněním je musí zkontrolovat člověk a konečné znění posoudit právník. Kontrola pravidly není úplná pojistka.

## Příkazy

| Příkaz | Popis |
| --- | --- |
| `scan <url>` | Projde web podle robots.txt a sitemap, vyhodnotí texty a vytvoří výstupy. Volby: `--max-pages`, `--sample-products`, `--modules` (výchozí: všechny moduly s pravidly pro zemi), `--country` (`sk` nebo `cz`, výchozí `sk`), `--question-lang`, `--rate` (pevné tempo stahování), `--concurrency`, `--include`, `--exclude`, `--out`, `--mock`, `--no-cache`, `--no-sieve`, `--yes`. |
| `check-text "<text>"` | Vyhodnotí jeden text. Volby: `--kind`, `--modules`, `--country`, `--category` (kategorie výrobku pro modul `lr`), `--question-lang`, `--mock`. |
| `rewrite <složka skenu>` | Navrhne přepis problematických pasáží modelem OpenAI a znovu je zkontroluje. Volby: `--country`, `--limit`, `--mock`, `--no-cache`, `--yes`. |
| `serve-fixture` | Lokální testovací e-shop (`--port`, `--root`). |
| `evaluate` | Měření přesnosti na označeném vzorku (M4). |

## Použití knihovny bez CLI

```csharp
services.AddEshopGuard(options =>
{
    options.Jev.ApiKey = configuration["TYPESAFE_API_KEY"];   // nebo options.Jev.UseMock = true
    options.Cache.Path = "cache/jev-cache.sqlite";
    options.Rules.Directory = "rules";
    options.Rules.LabelsFile = "config/labels.yaml";
});

var guard = provider.GetRequiredService<IEshopGuard>();
var result = await guard.AnalyzeTextsAsync(
    [new TextInput { Text = "Tento šampon je ekologický a šetrný k přírodě." }],
    new AnalyzeOptions { Country = "cz" });   // výchozí je "sk"
```
