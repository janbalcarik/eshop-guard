# Delta for Web-app

## ADDED Requirements

### Requirement: Přihlášení odkazem v e-mailu
Systém MUST na obrazovce 2a nabídnout jako výchozí přihlášení odkazem v e-mailu. Na obrazovce 2b MUST ukázat odeslání s odpočtem „Poslať znova“ podle `retryAfterSeconds` z API. Na obrazovce 2c MUST přihlásit až tlačítkem „Prihlásiť sa“ (POST). Otevření odkazu (GET) MUST NOT odkaz spotřebovat. Odpověď po odeslání MUST být stejná, ať účet existuje, nebo ne.

#### Scenario: Odeslání odkazu a odpočet
- GIVEN uživatel je na `/app/login` s rozhraním ve slovenštině
- WHEN zadá `jana@bylinkovo.sk` a zvolí „Poslať odkaz na prihlásenie“
- THEN aplikace zobrazí 2b „Skontrolujte e-mail“ s touto adresou a neaktivní tlačítko „Poslať znova o 0:59“ s odpočtem podle `retryAfterSeconds`
- AND po doběhnutí odpočtu se tlačítko aktivuje a „Použiť iný e-mail“ vrátí na 2a s prázdným polem

#### Scenario: Náhled odkazu poštou odkaz nespotřebuje
- GIVEN poštovní klient nebo antivir otevře `/app/login/confirm?token=…`
- WHEN se stránka načte metodou GET
- THEN aplikace zavolá jen dotaz na stav odkazu, který odkaz nespotřebuje, a zobrazí „Pokračujete ako jana@bylinkovo.sk“ s tlačítkem „Prihlásiť sa“
- AND token zmizí z adresního řádku (`history.replaceState`) a stránka má `Referrer-Policy: no-referrer`
- AND relace vznikne až po stisku tlačítka

#### Scenario: Vypršelý nebo použitý odkaz
- GIVEN odkaz je starší než 15 minut nebo už byl jednou použitý
- WHEN uživatel otevře obrazovku 2c
- THEN aplikace místo tlačítka „Prihlásiť sa“ zobrazí, že odkaz už neplatí, a tlačítko „Poslať nový odkaz“
- AND nikoho nepřihlásí

#### Scenario: Překročený limit odeslání
- GIVEN na e-mail už odešlo 5 odkazů za poslední hodinu
- WHEN uživatel požádá o další odkaz
- THEN API vrátí kód `auth.magic_link.rate_limited` s dobou čekání a aplikace zůstane na 2a s textem v jazyce rozhraní, kdy půjde odkaz poslat znovu
- AND obrazovku 2b jako úspěch neukáže

### Requirement: Přihlášení heslem, přes Google a volba jazyka před přihlášením
Systém MUST na obrazovce 2a nabídnout přepnutí na přihlášení heslem („Prihlásiť sa heslom“) a zpět („Prihlásiť sa bez hesla, odkazom v e-maile“), tlačítko „Pokračovať cez Google“ a přepínač jazyka rozhraní (Slovenčina, Čeština). Výchozí jazyk před přihlášením MUST odpovídat vydání webu, ze kterého uživatel přišel (`?from=sk|cz`).

#### Scenario: Výchozí jazyk podle vydání webu
- GIVEN uživatel přišel z českého vydání webu na `/app/login?from=cz`
- WHEN se obrazovka 2a načte
- THEN texty jsou česky a přepínač ukazuje „Čeština“ jako zvolenou (`aria-checked="true"`)
- AND odkaz v e-mailu se vyžádá s `locale=cs`

#### Scenario: Špatné heslo
- GIVEN uživatel je v režimu hesla
- WHEN zadá špatné heslo a zvolí „Prihlásiť sa“
- THEN API vrátí kód `auth.invalid_credentials` a aplikace ukáže text u formuláře bez prozrazení, zda e-mail existuje
- AND fokus se přesune na pole „Heslo“

#### Scenario: Přihlášení přes Google zrušené uživatelem
- GIVEN uživatel zvolil „Pokračovať cez Google“ a prohlížeč přešel na `/api/auth/external/google?returnUrl=/app`
- WHEN uživatel souhlas u Googlu zruší a API vrátí uživatele na `/app/login?error=auth.external_canceled`
- THEN obrazovka 2a ukáže text v jazyce rozhraní, že přihlášení přes Google bylo zrušeno
- AND formulář odkazu v e-mailu zůstane výchozí volbou

### Requirement: Připojení e-shopu podle rozpoznané platformy
Systém MUST na obrazovce 3a po zadání adresy ukázat výsledek rozpoznání platformy z API („Rozpoznané: Shoptet“, nebo „Platformu sme nerozpoznali“ s variantou 3b). Systém MUST nabídnout jen způsoby připojení, které API vrátí jako dostupné. Nedostupný způsob MUST zůstat viditelný jako neaktivní s vysvětlením. Klíče konektoru zadané na 3b MUST odejít jen v těle požadavku na API a MUST NOT se uložit v prohlížeči.

#### Scenario: Rozpoznaná platforma
- GIVEN uživatel zadá `https://bylinkovo.sk`
- WHEN API do 600 ms po posledním úhozu vrátí `recognized: true, platform: shoptet`
- THEN pole adresy má zelený štítek „Rozpoznané: Shoptet“ a karta „Pripojiť cez Shoptet“ je označená „Odporúčané“
- AND vedle formuláře je tabulka „Čo ktorý spôsob umožní“ s hodnotami áno / nie / čiastočne podle návrhu

#### Scenario: Nerozpoznaná platforma a klíče WooCommerce
- GIVEN API vrátí pro `https://moj-obchod.sk` výsledek `recognized: false`
- WHEN uživatel zvolí „WooCommerce“, vloží Consumer key a Consumer secret a zvolí „Overiť a pripojiť“
- THEN aplikace pošle klíče jedním `POST` na API a pole po odpovědi vymaže
- AND klíče nejsou v `localStorage`, `sessionStorage`, adrese, logu prohlížeče ani v datech chyb

#### Scenario: Ukázka zdarma už na doméně proběhla
- GIVEN doména už má záznam ve `free_sample_claims`
- WHEN uživatel zvolí „Pokračovať“
- THEN API vrátí kód `shops.free_sample_already_claimed` a aplikace ukáže vysvětlení v jazyce rozhraní (podle schváleného návrhu stavu)
- AND na obrazovku 3c nepřejde a nic se netváří jako spuštěná ukázka

### Requirement: Kde predávate a cena přepočítaná serverem
Systém MUST na obrazovce 3c ukázat jen podporované země z API, u každé země důvod (ověřené citace nebo technické znaky) a štítek pravidel. Systém MUST při každé změně zaškrtnutí míst prodeje nebo modulů okamžitě vyžádat novou nabídku ceny ze serveru a zobrazit jen její částky. Během přepočtu a po chybě nabídky MUST být tlačítko platby neaktivní. Klient MUST NOT s cenami počítat.

#### Scenario: Odškrtnutí země přepočítá cenu
- GIVEN 3c ukazuje zaškrtnuté Slovensko a Česko a nabídku „11 668 produktov v 2 verziách s vlastnými textami, pásmo do 20 000“
- WHEN uživatel odškrtne „Česko“
- THEN aplikace hned pošle `POST …/quote` s `markets: [sk]` a po dobu požadavku ukáže „Prepočítavame cenu…“ s neaktivním tlačítkem platby
- AND po odpovědi zobrazí počet produktů, pásmo, řádky objednávky a „Dnes zaplatíte“ přesně podle nabídky ze serveru

#### Scenario: Rychlé změny za sebou
- GIVEN uživatel během 300 ms odškrtne a znovu zaškrtne „Česko“
- WHEN odpovědi na oba požadavky přijdou v opačném pořadí
- THEN aplikace zobrazí jen nabídku k poslednímu výběru (`markets: [sk, cz]`) a starší požadavek zruší přes `AbortController`
- AND `quoteId` u tlačítka platby patří k zobrazené nabídce

#### Scenario: Chyba nabídky
- GIVEN API při přepočtu vrátí 503 nebo kód `billing.price_list_missing`
- WHEN odpověď dorazí
- THEN souhrn objednávky ukáže text chyby v jazyce rozhraní a tlačítko „Skúsiť znova“
- AND tlačítko „Zaplatiť … a spustiť kontrolu“ zůstane neaktivní a žádná dřívější cena se neukáže jako platná

#### Scenario: Žádné místo prodeje
- GIVEN uživatel odškrtne obě země
- WHEN API vrátí kód `shops.markets_none_selected`
- THEN u fieldsetu „Kde predávate“ se zobrazí, že je potřeba vybrat aspoň jednu zemi, a fokus zůstane v seznamu zemí
- AND platba není možná

### Requirement: Jazykové verze souhrnem a podrobnostmi
Systém MUST na obrazovce 3c ukázat jazykové verze e-shopu jednou až dvěma větami z výsledku ukázky (například „Našli sme slovenskú … a českú verziu …“) bez zaškrtávání verzí. Na obrazovce 3d MUST ukázat tabulku verzí (Verzia, Adresa, Produkty, Jazyk textov, Vlastné texty, Do ceny), porovnání stejných produktů a odkaz na vyloučení verze v nastaveních. Po návratu z 3d MUST zůstat zachovaný výběr na 3c.

#### Scenario: Podrobnosti verzí
- GIVEN ukázka našla verze `bylinkovo.sk` a `bylinkovo.sk/cz/`
- WHEN uživatel na 3c zvolí „Podrobnosti“
- THEN 3d ukáže dva řádky tabulky s hodnotami z API (např. „98 % po česky“, „Pri 1 produkte slovenský text“, „96 % produktov“, „Áno“) a štítky porovnání „17 preložených“, „2 skrátené alebo iný text“, „1 nepreložený“
- AND „Späť na objednávku“ vrátí na 3c se stejným výběrem zemí a stejnou nabídkou

#### Scenario: E-shop bez dalších verzí
- GIVEN e-shop má jen jednu verzi pro podporovaná místa prodeje
- WHEN se načte 3c
- THEN fieldset „Jazykové verzie“ ukáže jednu větu o jediné verzi a odkaz „Podrobnosti“ se nezobrazí
- AND nic se netváří jako porovnání dvou verzí

### Requirement: Objednávka a platba přes Stripe Checkout
Systém MUST založit objednávku s ID zobrazené nabídky a u prvního e-shopu přesměrovat na adresu Stripe Checkout z API. Po návratu MUST čekat na potvrzení platby z API (webhook). Návratová adresa sama MUST NOT být důkaz platby. U dalšího e-shopu MUST nabídnout platbu uloženou kartou účtu, když ji API nabídne.

#### Scenario: Úspěšná platba
- GIVEN uživatel na 3c zvolí „Zaplatiť … a spustiť kontrolu“
- WHEN API vrátí `checkoutUrl` a uživatel na stránce Stripe zaplatí
- THEN aplikace na `/app/t/{t}/checkout/return?order={o}` ukáže „Overujeme platbu…“ a každé 2 s se ptá na stav objednávky
- AND po stavu `paid` přejde na Přehled e-shopu s průběhem úvodní analýzy

#### Scenario: Platba se nepotvrdí do 60 sekund
- GIVEN objednávka je po návratu ze Stripe stále `checkout_open`
- WHEN uplyne 60 s dotazování
- THEN aplikace ukáže, že platbu ještě ověřujeme a výsledek pošleme e-mailem, s odkazem na Přehled
- AND analýzu jako spuštěnou neoznačí

#### Scenario: Zrušená platba
- GIVEN uživatel na stránce Stripe zvolí návrat zpět bez platby
- WHEN aplikace dostane stav objednávky `expired` nebo `canceled`
- THEN vrátí uživatele na 3c s upozorněním v jazyce rozhraní a s novou nabídkou ceny
- AND běh analýzy zůstane ve stavu `awaiting_payment`

#### Scenario: Zastaralá nabídka při objednávce
- GIVEN mezi zobrazením nabídky a stiskem tlačítka se zveřejnil nový ceník
- WHEN API odmítne objednávku kódem `billing.quote_expired`
- THEN aplikace hned vyžádá novou nabídku, ukáže novou cenu a upozornění, že se cena změnila
- AND na Stripe nepřesměruje, dokud uživatel tlačítko nestiskne znovu

### Requirement: Živý průběh běhu
Systém MUST ukazovat průběh ukázky zdarma, úvodní analýzy a publikování živě přes SSE (`text/event-stream`) z událostí běhu. Texty MUST skládat z kódů a parametrů v jazyce uživatele. Při výpadku spojení MUST se připojit znovu s `Last-Event-ID` a po třech neúspěších přejít na dotazování. Částečný nebo selhaný běh MUST ukázat s důvodem a s tím, co nebylo zkontrolováno.

#### Scenario: Průběh ukázky zdarma
- GIVEN běh ukázky zdarma je ve stavu `crawling`
- WHEN API pošle události `run.progress` s `pagesDone: 63, pagesTotal: 100`
- THEN karta ukázky ukáže průběh 63 ze 100 stránek a odečítač obrazovky oznámí jen změnu kroku, ne každé číslo
- AND po události `run.finished` se načtou místa prodeje, verze a první nabídka ceny

#### Scenario: Výpadek spojení
- GIVEN spojení SSE spadne třikrát za sebou
- WHEN se aplikace nepřipojí ani napotřetí
- THEN přejde na `GET …/runs/{r}` každých 5 s a průběh dál aktualizuje
- AND průběh nezamrzne bez upozornění: po 60 s bez nové informace ukáže „Spojenie sa obnovuje“

#### Scenario: Částečný výsledek
- GIVEN běh skončí ve stavu `partial`, protože 12 stránek vrátilo „text sa nenačítal“
- WHEN uživatel otevře Přehled
- THEN aplikace ukáže, že kontrola je částečná, počet nezkontrolovaných stránek a důvody (podle schváleného návrhu stavu)
- AND tento počet se nezapočítá do „skontrolované“

### Requirement: Přehled e-shopu
Systém MUST na obrazovce 4 (desktop) a 9 (mobil) ukázat:
- souhrn poslední kontroly s počtem nálezů ve třech skupinách (porušenie, na posúdenie, na overenie) s množným číslem podle jazyka;
- karty „Opravy na schválenie“, „Potrebujeme vašu odpoveď“ a „Publikované opravy“ s průběhem;
- „Stránky, ktoré riešiť najskôr“ v pořadí z API, „Rýchle odpovede“ a souhrn sledování;
- odkaz „Stiahnuť protokol (PDF)“.

#### Scenario: Souhrn v jazyce uživatele
- GIVEN e-shop bylinkovo.sk má 43 nálezů (14, 23, 6) z kontroly 30. 9. 2026 v 8:19
- WHEN uživatel s rozhraním ve slovenštině otevře Přehled
- THEN podtitul zní „bylinkovo.sk · kontrola 30. 9. 2026 o 8:19 · 43 nálezov: 14 porušení, 23 na posúdenie, 6 na overenie“
- AND s rozhraním v češtině jsou stejná data česky a s českými tvary množného čísla

#### Scenario: Rychlá odpověď
- GIVEN „Rýchle odpovede“ obsahují otázku „Viete doložiť dobu horenia 22 hodín?“
- WHEN uživatel zvolí „Nie“
- THEN aplikace odešle odpověď, tlačítka jsou během požadavku neaktivní a otázka zmizí až po úspěšné odpovědi API
- AND počet „Potrebujeme vašu odpoveď“ se obnoví z API

#### Scenario: Čtenář bez práva odpovídat
- GIVEN uživatel má v tenantovi roli `viewer`
- WHEN otevře Přehled
- THEN tlačítka „Áno“ a „Nie“ jsou neaktivní s popiskem, že odpovídat může editor a vyšší role
- AND nejsou skrytá

### Requirement: Seznam oprav
Systém MUST na obrazovce 5 ukázat:
- záložky „Na riešenie“, „Na schválenie“, „Potrebujeme vašu odpoveď“ a „Publikované“ s počty z API;
- přepínač „Podľa stránok“ / „Podľa nálezov“ a filtr „Verzia“ (Všetky verzie a jazykové verze e-shopu);
- pruh hromadných oprav s nejčastějšími texty a počtem stránek;
- řádky stránek s ukázkou změny nebo otázky, štítky počtu nálezů po skupinách, rozsahem práce a stavem.

Stav filtrů MUST být v adrese stránky.

#### Scenario: Filtr jazykové verze
- GIVEN e-shop má verze slovenskou a českou
- WHEN uživatel zvolí ve filtru „Čeština“
- THEN adresa obsahuje `lang=cs`, seznam a počty v záložkách jsou jen z české verze podle API
- AND tlačítko Zpět prohlížeče vrátí filtr „Všetky verzie“

#### Scenario: Štítky s množným číslem
- GIVEN stránka má 6 porušení, jiná 1 porušení a jiná 2 porušení
- WHEN se seznam vykreslí ve slovenštině
- THEN štítky zní „6 porušení“, „1 porušenie“ a „2 porušenia“
- AND barvy štítků odpovídají návrhu (porušenie `#FDECEC`/`#9A1C1C`, na posúdenie `#FEF1DC`/`#8A4B00`, na overenie `#E6EEFB`/`#1E4E9C`)

#### Scenario: Prázdná záložka
- GIVEN záložka „Publikované“ má 0 stránek
- WHEN ji uživatel otevře
- THEN aplikace ukáže prázdný stav v jazyce rozhraní, ne prázdnou plochu
- AND ostatní záložky ukazují své počty dál

### Requirement: Oprava stránky
Systém MUST na obrazovkách 6a a 6b ukázat stránku se všemi změnami:
- tři zobrazení: „Vedľa seba“, „Zmeny v texte“, „Celý nový text“;
- u každé změny verdikt po zemích (např. „SK · Porušenie“, „CZ · Na posúdenie“) seřazený od nejpřísnějšího podle API;
- alternativy návrhu, doplnění údaje, otázky s odpověďmi;
- panel „Prečo meniť“, „Čo pomôže“, odkazy na zákon po zemích a výsledek nové kontroly („Nový text prešiel kontrolou“);
- spodní lištu s počtem přijatých změn, „Kopírovať text“ a „Publikovať do e-shopu (N zmien)“.

Klávesové zkratky A (prijať), U (upraviť), J a K (ďalšia / predchádzajúca) MUST fungovat jen při fokusu v seznamu změn mimo textová pole a MUST jít vypnout.

#### Scenario: Přijetí změny zkratkou
- GIVEN fokus je na kartě „Zmena 3“ v seznamu změn
- WHEN uživatel stiskne `A`
- THEN aplikace odešle rozhodnutí `accept` s hlavičkou `If-Match` a po úspěchu ukáže u změny „Prijaté“
- AND spodní lišta ukáže „Prijaté 2 z 5 zmien“ a fokus přejde na další nerozhodnutou změnu

#### Scenario: Zkratka při psaní do pole
- GIVEN kurzor je v poli „Z čoho je obal?“
- WHEN uživatel napíše písmeno `a`
- THEN písmeno se zapíše do pole a žádná změna se nepřijme
- AND po vypnutí zkratek v Nastaveniach nereaguje na `A`, `U`, `J`, `K` ani seznam změn

#### Scenario: Souběžná úprava jiným uživatelem
- GIVEN kolega mezitím upravil stejný návrh
- WHEN uživatel zvolí „Prijať“ a API vrátí 412
- THEN aplikace ukáže „Návrh medzitým zmenil iný používateľ“ s tlačítkem „Načítať znova“
- AND změnu jako přijatou neoznačí

#### Scenario: Publikování s konfliktem
- GIVEN uživatel přijal 2 změny a zvolí „Publikovať do e-shopu (2 zmeny)“
- WHEN publikování vrátí u jedné změny stav `conflict`, protože text v e-shopu se mezitím změnil
- THEN aplikace ukáže u té změny, že text v e-shopu se změnil a stránku kontrolujeme znovu, a u druhé „Publikované“
- AND spodní lišta neukáže úspěch pro obě změny

### Requirement: Hromadná oprava
Systém MUST na obrazovce 6c ukázat:
- jednu opravu pro opakovaný text s verdiktem a odkazem na zákon;
- pole pro doplnění údaje a tři způsoby opravy (nahradit navrhovaným textem, větu odstranit ze všech stránek, vlastní znění);
- ukázky „Ako to bude vyzerať na stránkach“;
- souhrn „Kontrola na každej stránke“ (oprava sedí / riešiť jednotlivo) s důvodem a odkazy;
- seznam stránek se zaškrtnutím.

Tlačítko „Prijať na N stránkach“ MUST použít počet vybraných stránek z odpovědi API.

#### Scenario: Doplnění údaje a nová kontrola
- GIVEN oprava má zástupný údaj „[materiál obalu]“
- WHEN uživatel vyplní „papierová krabica bez plastovej výplne“
- THEN aplikace údaj uloží, ukáže probíhající novou kontrolu a po výsledku `ok` text „Nový text sme znova skontrolovali“
- AND při výsledku `still_finding` tlačítko „Prijať na 36 stránkach“ zůstane neaktivní s vysvětlením

#### Scenario: Vyřazení stránek
- GIVEN seznam „Stránky, na ktoré sa oprava zapíše (36)“
- WHEN uživatel odškrtne 2 stránky a zvolí „Vyradiť označené“
- THEN po odpovědi API nadpis ukáže „(34)“ a tlačítko „Prijať na 34 stránkach“
- AND vyřazené stránky se objeví v seznamu oprav jednotlivě, nezmizí

#### Scenario: Stránky k řešení jednotlivě
- GIVEN API označí 2 stránky jako „riešiť jednotlivo“, protože na větu navazuje další
- WHEN se 6c vykreslí
- THEN karta „Kontrola na každej stránke“ ukáže 36 a 2 s vysvětlením z API a odkazy na obě stránky
- AND tyto 2 stránky nejsou v seznamu stránek, na které se oprava zapíše

### Requirement: Sledování změn, doklady a protokol
Systém MUST na obrazovce 7 ukázat rozsah sledování, výsledek dnešní kontroly, přepínače upozornění (role `switch`) a tabulku „Posledné zmeny“ s výběrem období. Na obrazovce 10 MUST ukázat čtyři souhrnná čísla a tabulku dokladů (Tvrdenie, Doklad, Platí pre, Stav, Odkiaľ) se stavy z API. Odkaz „Stiahnuť protokol (PDF)“ MUST vyžádat protokol z API a stáhnout ho přes podepsaný odkaz, bez generování v prohlížeči.

#### Scenario: Přepnutí upozornění
- GIVEN přepínač „Týždenný súhrn“ je vypnutý
- WHEN uživatel ho zapne klávesou Mezerník
- THEN aplikace uloží nastavení a po úspěchu má přepínač `aria-checked="true"`
- AND při chybě API se vrátí do vypnutého stavu s textem chyby

#### Scenario: Sledování ještě neběží
- GIVEN API sledování (změna 16) pro e-shop nevrací data nebo vrací kód `monitoring.not_started`
- WHEN uživatel otevře Sledovanie zmien
- THEN obrazovka ukáže prázdný stav, že sledování ještě neběží, a nastavení upozornění
- AND žádná ukázková ani odhadnutá čísla se nezobrazí

#### Scenario: Doklad s končící platností
- GIVEN doklad „Kontrolovaná prírodná kozmetika“ platí do 20. 10. 2026 a dnes je 30. 9. 2026
- WHEN se načte obrazovka Doklady
- THEN stav ukáže „Končí o 20 dní“ se správným tvarem množného čísla a barvou `#FEF1DC`/`#8A4B00`
- AND „Platí pre“ je odkaz na seznam oprav s produkty, kterých se doklad týká

#### Scenario: Protokol se generuje déle
- GIVEN uživatel zvolí „Stiahnuť protokol (PDF)“
- WHEN API vrátí 202 s ID úlohy
- THEN odkaz ukáže průběh „Pripravujeme protokol…“ a po dokončení stáhne PDF přes podepsaný odkaz
- AND při selhání ukáže kód chyby v jazyce uživatele a odkaz se znovu aktivuje

### Requirement: Předplatné a platby
Systém MUST na obrazovce 8 ukázat:
- e-shopy v účtu s cenou analýzy, stavem sledování, další platbou a „Zrušiť sledovanie“;
- součet sledování;
- platební kartu se „Zmeniť kartu“;
- pásma s vyznačeným aktuálním pásmem;
- faktury ve vlastní posuvné oblasti s filtrem E-shop a Rok, odkazem PDF a „Stiahnuť ZIP“.

Na mobilu (9b) MUST faktury ukázat bez vnořeného posuvníku: první 4 a rozbalení „Zobraziť všetky faktúry (N)“. Všechny částky MUST pocházet z API. Slevu MUST zobrazit, jen když ji API vrátí.

#### Scenario: Filtr faktur a ZIP
- GIVEN účet má 18 faktur ze tří e-shopů za roky 2026 a 2027
- WHEN uživatel zvolí E-shop „bylinkovo.sk“ a Rok „2027“ a pak „Stiahnuť ZIP“
- THEN seznam ukáže jen faktury bylinkovo.sk z roku 2027 a ZIP se vyžádá se stejným filtrem
- AND oblast faktur je klávesnicí posouvatelná (`role="region"`, `tabindex="0"`, název „Zoznam faktúr“)

#### Scenario: Mobil bez vnořeného posuvníku
- GIVEN šířka okna je 390 px
- WHEN se načte Predplatné a platby
- THEN faktury jsou součástí toku stránky, viditelné jsou první 4 a tlačítko „Zobraziť všetky faktúry (18)“ má výšku aspoň 48 px
- AND žádný prvek stránky nemá vlastní svislý posuvník kromě celé stránky

#### Scenario: Zrušení sledování
- GIVEN e-shop bylinkovo.cz má aktivní sledování
- WHEN uživatel zvolí „Zrušiť sledovanie“
- THEN aplikace otevře potvrzovací dialog s datem konce zaplaceného období z API a teprve po potvrzení odešle zrušení
- AND po úspěchu ukáže stav z API (sledování běží do konce období), při chybě se stav nezmění

#### Scenario: Sleva od 3. e-shopu není rozhodnutá
- GIVEN API nevrací žádnou slevu za počet e-shopů (`volume_discounts` prázdné)
- WHEN se načte obrazovka 8
- THEN text o slevě od 3. e-shopu ani řádek se slevou se nezobrazí
- AND zástupný text „[X] %“ se nikde neobjeví

### Requirement: Navigace, přepínač e-shopu, menu účtu a mobilní zobrazení
Systém MUST na desktopu ukázat boční menu podle `Sidebar.dc.html`:
- logo a přepínač e-shopu se stavem připojení;
- položky Prehľad, Opravy, Doklady a Sledovanie zmien s počty z API;
- kartu Sledovanie zmien s další platbou;
- tlačítko účtu s menu (název firmy, e-mail, Predplatné a platby, Nastavenia, Jazyk, Odhlásiť sa).

Pod šířkou 768 px MUST ukázat mobilní hlavičku s přepínačem e-shopu a zvonečkem a spodní navigaci (Prehľad, Opravy · N, Sledovanie, Účet). Každý cíl dotyku na mobilu MUST mít aspoň 44 × 44 px.

#### Scenario: Změna jazyka v menu účtu
- GIVEN uživatel má rozhraní ve slovenštině
- WHEN v menu účtu zvolí „Jazyk“ a „Čeština“
- THEN aplikace uloží `users.locale = cs` přes `PATCH /api/me` a po úspěchu vykreslí aktuální obrazovku česky
- AND při chybě API zůstane slovenština a zobrazí se text chyby

#### Scenario: Přepnutí e-shopu
- GIVEN účet má e-shopy bylinkovo.sk, bylinkovo-darceky.sk a bylinkovo.cz
- WHEN uživatel v přepínači zvolí bylinkovo.cz na obrazovce Opravy
- THEN aplikace přejde na Opravy e-shopu bylinkovo.cz se stejnými filtry, pokud dávají smysl, a uloží volbu do cookie `eg_ctx`
- AND počty v menu jsou z bylinkovo.cz

#### Scenario: Ovládání menu účtu klávesnicí
- GIVEN fokus je na tlačítku účtu „Jana Kováčová“
- WHEN uživatel stiskne Enter, šipkou dolů vybere „Nastavenia“ a stiskne Escape
- THEN menu se otevře s `aria-expanded="true"`, šipky procházejí položky a Escape menu zavře a vrátí fokus na tlačítko
- AND položka aktuální obrazovky (např. Predplatné a platby) je vyznačená podle návrhu

#### Scenario: Mobilní šířka bez vodorovného posunu
- GIVEN okno má šířku 390 px
- WHEN se načte kterákoli obrazovka aplikace
- THEN `document.scrollingElement.scrollWidth` není větší než 390
- AND spodní navigace má 4 položky, aktivní je vyznačená barvou `#0B4A43` a tučně

### Requirement: Přístupnost aplikace
Systém MUST splnit WCAG 2.2 úrovně A a AA na všech obrazovkách aplikace:
- vše ovladatelné klávesnicí;
- viditelný fokus (obrys 2 px `#0E5A52`);
- kontrast textu aspoň 4,5 : 1 a prvků rozhraní aspoň 3 : 1;
- popisky polí, `aria-live` pro změny ceny a průběhu;
- sémantické záložky a skupiny tlačítek s `aria-pressed`;
- změny textu čitelné i bez barvy.

#### Scenario: Automatická kontrola
- GIVEN Playwright otevře každou obrazovku aplikace ve slovenštině i češtině na šířce 1440 i 390 px
- WHEN spustí axe s pravidly `wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`
- THEN počet porušení je 0
- AND test selže, když přibude porušení, a vypíše prvek a pravidlo

#### Scenario: Odstraněný a přidaný text pro odečítač
- GIVEN změna ukazuje `<del>v ekologickom sete</del>` a `<ins>v sete so zubnou kefkou…</ins>`
- WHEN ji přečte odečítač obrazovky
- THEN před odstraněným textem zazní „odstránené:“ a před přidaným „pridané:“ (skrytý text v jazyce rozhraní)
- AND sloupce mají nadpisy „Pôvodný text“ a „Navrhovaný text“

#### Scenario: Kontrast tokenů
- GIVEN tokeny barev v `web/src/styles/tokens.css`
- WHEN běží test `tokens-contrast.test.ts`
- THEN všechny dvojice text–pozadí z návrhu (např. `#5F646C` na `#F6F5F1`, `#A9B4AF` na `#0E3B36`, `#8A4B00` na `#FEF1DC`) mají kontrast aspoň 4,5 : 1
- AND nová dvojice pod hranicí test zastaví

#### Scenario: Ilustrace bez fokusu
- GIVEN obrazovka obsahuje dekorativní ikony SVG z návrhu
- WHEN uživatel prochází stránku klávesou Tab
- THEN dekorativní SVG mají `aria-hidden="true"` a nedostanou fokus
- AND tlačítka jen s ikonou (zvoneček, předchozí / další změna, Späť) mají `aria-label` v jazyce rozhraní

### Requirement: Komunikace s API, chyby a tajné údaje
Systém MUST volat API jen klientem vygenerovaným z OpenAPI a převést každou chybu ProblemDetails na kód a text v jazyce uživatele. Neznámá chyba MUST NOT vypadat jako úspěch. Do klientského kódu MUST NOT se dostat žádný serverový klíč ani tajemství. Server Next.js MUST NOT logovat cookie, hlavičky autorizace ani tokeny odkazů.

#### Scenario: Neznámý kód chyby
- GIVEN API vrátí 500 s kódem `fixes.unexpected_state`, pro který není text
- WHEN akce skončí
- THEN aplikace ukáže obecný text chyby v jazyce uživatele s kódem a `traceId`
- AND tlačítko akce se znovu aktivuje a stav na obrazovce se nezmění

#### Scenario: Vypršelá relace
- GIVEN relace uživatele vypršela
- WHEN libovolný požadavek vrátí 401
- THEN aplikace přesměruje na `/app/login?next=<aktuální adresa>`
- AND po přihlášení vrátí uživatele na stejnou adresu

#### Scenario: Klient neodpovídá OpenAPI
- GIVEN snímek `web/openapi/eshopguard-api.json` se změnil a `schema.d.ts` je zastaralé
- WHEN běží `pnpm api:check` v CI
- THEN kontrola selže s výpisem rozdílu
- AND sestavení se nenasadí

#### Scenario: Tajemství v klientském balíčku
- GIVEN klientská komponenta omylem čte `process.env.STRIPE_SECRET_KEY`
- WHEN běží lint a `pnpm secrets:scan` nad `.next/static`
- THEN pravidlo `eg/no-server-env-in-client` i kontrola selžou
- AND sestavení neprojde
