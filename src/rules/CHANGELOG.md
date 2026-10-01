# Změny sad otázek

Každá změna znění otázky nebo pravidla zvyšuje verzi sady (`version`), protože verze je součástí klíče cache i hlavičky zprávy.

## Česko: tlačítko „Odstoupit od smlouvy“, legal_cz draft4 (připraveno 1. 10. 2026, změna 6)

Nová verze sady `legal_cz` (`legal-cz-2026-10-01-draft4`) přidá dvě pravidla s účinností od 1. 1. 2027. Znění je ověřené proti textu zákona v `podklady/predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt` a proti rešerši `podklady/reserse/cz-informacni-povinnosti.md` (oddíl `cz_withdrawal_button`). Všechny odkazy mají stav „ověřit“ a ustanovení posoudí právník.

Doslovné citace ze zákona č. 159/2026 Sb. (čl. V, změna občanského zákoníku):
- **§ 1830a odst. 1 OZ:** „Má-li spotřebitel právo odstoupit od smlouvy uzavřené distančním způsobem prostřednictvím on-line rozhraní, umožní podnikatel spotřebiteli odstoupit od smlouvy také prohlášením učiněným v on-line rozhraní použitím tlačítka nebo obdobného ovládacího prvku pro odstoupení od smlouvy.“
- **§ 1830a odst. 2 OZ:** „Tlačítko nebo obdobný ovládací prvek pro odstoupení od smlouvy musí být v on-line rozhraní zobrazeny výrazným způsobem, snadno přístupné, dostupné nepřetržitě po celou lhůtu pro odstoupení od smlouvy a musí být označeny snadno čitelným nápisem „Odstoupit od smlouvy“ nebo jinou odpovídající jednoznačnou formulací.“
- **§ 1820 odst. 1 písm. i) OZ (nové znění):** „i) má-li spotřebitel právo odstoupit od smlouvy, podmínky, lhůtu a postup pro uplatnění tohoto práva, jakož i vzorový formulář pro odstoupení od smlouvy, a v případě smlouvy uzavírané distančním způsobem prostřednictvím on-line rozhraní i údaje o možnosti odstoupit od smlouvy také použitím tlačítka nebo obdobného ovládacího prvku pro odstoupení od smlouvy a o jejich umístění; náležitosti vzorového formuláře stanoví prováděcí právní předpis,“
- **Účinnost (čl. XII):** „Tento zákon nabývá účinnosti dnem 1. ledna 2027.“

Pravidla:
- `legal_withdrawal_function_missing`: stejné logické id jako slovenské pravidlo (návrh změny 6, K rozhodnutí 1).
  - Rozsah `site_signal`: odkaz nebo tlačítko s nápisem „Odstoupit od smlouvy“ na stažených stránkách.
  - Hodnocení `verify`, protože tlačítko může být jen v účtu zákazníka, kam nástroj nevidí. Závažnost `high`, účinnost `cz: 2027-01-01`.
  - Vzor je předepsaný nápis ze zákona, ne slovník pro klasifikaci. Jinou „odpovídající jednoznačnou formulaci“ vzor nepozná, proto zůstává `verify`.
- `legal_withdrawal_button_info_missing`: rozsah `site_presence`, účinnost `cz: 2027-01-01`.
  - Otázky `legal_withdrawal_online_option` a `legal_withdrawal_button_location` v doslovném znění z rešerše.
  - Nové otázky změní sadu, proto nová verze. České právní odstavce se v Jevu vyhodnotí znovu.

## Profily šablon stránek (1. 10. 2026)

Nápad uživatele: strukturu stránky má určit jazykový model z několika stránek místo ručních pravidel. Ověřeno nejdřív sondou (scratchpad/structure_probe). Profil z kostry 3 stránek (úvodní, produkt, kategorie) od gpt-6.1-sol za 0,18 USD:
- typ stránky poznal 16 z 16;
- na 10 neviděných stránkách nezůstal žádný selektor bez shody;
- žádná věta obchodu se neztratila;
- odstranil balast, který dříve chodil Jevu: naturfyt 199 z 920 vět, vegis 31 z 785.

Měřítko „text mimo známé oblasti profilu“ vyšlo 0 % pro stránku s vlastním profilem a 94–97 % s cizím. Podobnost stavby je u stejné šablony 0,83–0,99, u jiného typu stránky téhož obchodu 0,32–0,67.

Zapracováno do knihovny `Profiles/`:
- profil se ukládá a použije se, dokud stránky sedí;
- nesedící stránky dostanou profil své šablony;
- profil smí vynechat jen ovládací prvky mimo hlavní text;
- dvě pojistky na stránce: přeskakovací oblast s oblastí ke kontrole nebo s hlavním textem se nepoužije.

Zkušební běh bez placených volání s profily ze sondy (1. 10. 2026):
- vegis: profil sedí na 103 ze 103 stránek, vynechal hlavně „Prihlásiť sa“, záložky a drobečkovou navigaci;
- naturfyt: 40 ze 40 stránek, 5 % viditelného textu (cookie lišta, přihlášení, košík, formuláře, menu).

Jedna produktová stránka naturfytu nejdřív nesedla (13,8 %) kvůli řadě „Súvisiaci tovar“, kterou vzorové stránky neměly. Proto se navigace a dlaždice jiných produktů rozpoznané podle struktury počítají jako známé, potom nejvýš 4 %.

## Ostatní text stránky a pokrytí (1. 10. 2026)

Sady pravidel se nemění, mění se, co se kontroluje. SmartReader vybírá hlavní text a 30. 9. na naturfyt.sk vynechal celý blok s odznaky, cenou a krátkým popisem produktu. Nově se kontroluje i ostatní viditelný text: vše mimo hlavní text, hlavičku, patičku a navigaci. Ve výstupech je jako zdroj `rest` a v `pages.jsonl` jako `rest_text`. Zpráva uvádí pokrytí (zkontrolováno / navigace / jinak nezkontrolováno) a vypíše stránky s nezkontrolovaným podílem aspoň 20 %.

- **Pokrytí:** vegis.sk zkontrolováno 17 % viditelného textu, navigace 83 %. naturfyt.sk 35 % / 65 %. Jinak nezkontrolováno 0 %.
- **Sken vegis.sk** (108 stránek, 1 462 nových volání, 0,188 USD): 40 → 63 nálezů (porušení 13 → 17, k posouzení 21 → 37, k ověření 6 → 9).
  - Asi 7 skutečných dřív minutých tvrzení, například „Jemná starostlivosť, sviežosť a ekologická voľba v jednom balení 💚“.
  - Asi 16 šumu z bloků „podobné produkty“: dlaždice cizích produktů slité do jednoho textu s názvy a cenami.
- **Sken naturfyt.sk** (43 stránek, 749 volání, 0,091 USD): 33 → 49 nálezů (porušení 10 → 23).
  - Vrátil se odznak „Eco“ (5 stránek).
  - Přibyly dlaždice produktů na úvodní stránce s krátkými popisy („Ekologický univerzálny čistič…“, „100% biologicky rozložiteľné tablety…“).
- **Horní odhad ceny** pro nový e-shop vzrostl (vegis.sk 1,28 → 1,79 USD), protože ostatní text jde mimo síto.
- **Výpisy jiných produktů** (schváleno uživatelem 1. 10.) se na cizí stránce nekontrolují, kontrolují se na stránce produktu. Rozpoznávají se podle struktury, ne podle nadpisu („Mohlo by sa vám hodiť“, „Podobné produkty“…):
  - rodič s aspoň 3 stejnými prvky;
  - většina z nich je krátká (do 600 znaků) a má cenu (číslo s měnou) a odkaz na jinou stránku téhož webu;
  - dlaždicí může být i samotný odkaz, jako na vegis.sk `<a class="product">`.

  Pásek výhod bez cen zůstává. Delší texty dlaždic se odeberou i z hlavního textu, krátké (odznak „Eco“) zůstanou. V pokrytí jsou jako „výpisy jiných produktů“.
  - vegis.sk: výpisy na 97 ze 108 stránek, 2 % viditelného textu; 63 → 50 nálezů (zmizelo všech 14 slepených dlaždic).
  - naturfyt.sk: výpisy na 8 ze 43 stránek, 4 %; 49 → 34 nálezů. Zmizely dlaždice na úvodní stránce, včetně dvou nálezů z původního běhu („Ľanová taška … ekologickú alternatívu“, „Šetrne čistí … bezpečný pre ľudí aj prírodu“). Ve vzorku chybí stránky těch produktů; při kontrole celého e-shopu se najdou tam.
  - Proti původnímu běhu bez ostatního textu: vegis.sk +10 skutečných nálezů (například „Jemná starostlivosť, sviežosť a ekologická voľba v jednom balení 💚“), naturfyt.sk +3 (odznak „Eco“, „Koncentrovaný gél na šetrné a ekologické pranie…“, „Značka: Khadi“).
  - Přepočet 0,019 + 0,012 USD.
- **Otevřené:** ostatní text zařadit do síta.
- Testy 184 ze 184.

## Slovo jen o zákaznících: eco draft14 (30. 9. 2026)

Věta „pre ekologicky zmýšľajúcich zákazníkov“ byla na vegis.sk porušením (`eco_generic_claim`). Nesplňovala by podmínky podle znění zákona: environmentální tvrzení je vyjádření, které „uvádza alebo naznačuje“ přínos produktu (§ 2 písm. n) zákona č. 108/2024 Z. z.), a tahle věta výrobek výslovně neoznačuje. Zda přínos naznačuje, záleží na vnímání spotřebitele, a patří proto mezi nálezy k posouzení.

- **Nová otázka `eco_audience`:** popisuje environmentální slovo jen lidi, pro které je produkt určený, jejich hodnoty nebo životní styl, a ne tento produkt, obal, dopravu nebo firmu?
  - Je v `none` pravidel `eco_generic_claim` a `eco_sustainable_claim`.
  - Nové pravidlo k posouzení `eco_generic_claim_implied` platí, když je věta tvrzením (`eco_claim`), je obecná (`eco_generic`), slovo popisuje jen zákazníky a výraz je výslovný nebo „udržateľný“.
  - Stávající otázky se nemění.
- **Sonda** (158 vět s `eco_claim` i `eco_generic` aspoň 0,3 z posledních běhů a 9 kontrolních vět, 0,0033 USD):
  - Kontrolní věty 9 z 9 správně. Ano: „pre ekologicky zmýšľajúcich zákazníkov“ 0,92, „pre milovníkov prírody a udržateľného životného štýlu“ 0,93. Ne: „Ekologická zubná kefka pre náročných zákazníkov“ 0,04, „Pre ekologicky zmýšľajúcich: kefka je šetrná k životnému prostrediu“ 0,06.
  - Na skutečných větách se výsledek pravidel změnil jen u cílové věty (porušení → k posouzení, 0,91).
- **Sken vegis.sk** (108 stránek, 0,421 USD): 43 → 40 nálezů (porušení 14 → 13, k posouzení 23 → 21, k ověření 6 → 6).
  - Cílová věta je teď k posouzení (`eco_generic_claim_open` 0,51, protože `eco_explicit_term` při novém položení klesl těsně pod práh).
  - Kolísáním Jevu u znovu položených eko otázek zmizely čtyři nálezy k posouzení 0,50–0,53 („100 % prírodný produkt“, „100 % prírodný francúzsky zelený íl“, „prírodný šampón…“ a nesmyslný „✔️ vodotesný horoskop EN“). Přibyl jeden 0,50 („Veríme, že prírodné znamená jednoduché…“).
- **Sken naturfyt.sk** (43 stránek, 0,133 USD): 36 → 33.
  - Tři změny těsně u prahu (0,51–0,61).
  - **Odznak „Eco“ (0,76) zmizel kvůli extrakci, ne kvůli pravidlům.** Na stránce pořád je (`<span class="flag flag-eco">`), ale SmartReader dnes vynechal celý blok s odznaky, cenou a dopravou. Zapsáno jako samostatný návrh (odznaky číst mimo hlavní text).
- Testy 177 ze 177.

## Přepis problematických pasáží: config/rewrite.yaml rewrite-2026-09-30-draft1 (30. 9. 2026)

Nový příkaz `checker rewrite <složka skenu>` (knihovna: `ITextRewriter`). Zadání pro model je v `config/rewrite.yaml` a má vlastní `version` (je součástí klíče mezipaměti přepisů).

- **Model gpt-6.1-sol** (OpenAI, `reasoning_effort: medium`), vybraný pilotem na 24 stránkách vegis.sk: ve srovnání s gpt-6-luna (desetina ceny) upřesňuje tvrzení fakty ze stránky („BIO olej … (Ecocert)“, „extrakcia bez rozpúšťadiel“) a nic nevymýšlí (všechny použité údaje ověřené na stránkách), luna víc maže naslepo.
- **Pokyny** z 2. kola pilotu: nevymýšlet fakta, chybějící údaj jako „[doplňte: …]“; upřesňovat faktem z téže stránky; měnit jen nutné; certifikační slova (BIO, spravodlivý obchod, cruelty-free…) nemazat, ale doplnit certifikaci ze stránky nebo „[doplňte]“ s poznámkou, že bez ní se musí odstranit; u nálezů k posouzení nejmenší zásah a ponechat popis složení či původu.
- **Deset příkladů špatných a dobrých znění** (obecné slovo za jiné obecné, vymyšlený fakt, smazané pravdivé BIO, „vedomá voľba pre planétu“, celek místo části, zbytečně odstraněný popis složení, vyprázdněný blok, příklad Komise z otázky 4, změna dávkování).
- **Doslovné výňatky:** § 2 písm. n) až q), § 9 ods. 12, § 10 ods. 1 písm. a) a b), príloha č. 1 body 3, 6, 7, 8 zákona č. 108/2024 Z. z. (znění od 27. 9. 2026), odůvodnění 9 a 10 směrnice (EU) 2024/825, otázky Komise 3, 4, 5 a 7.
- **Měření** (nástroj, ne pilot): vegis.sk 24 stránek, 36 nálezů: vyřešeno 14, čeká na doplnění 12, stále nález 2, ponecháno 8; 0,245 USD + kontrola Jevem 0,008 USD. naturfyt.sk (příklady z něj nepocházejí) 12 stránek, 28 nálezů: 11 / 10 / 1 / 6; 0,148 + 0,007 USD. Opakovaný běh z mezipaměti 0 USD.

## Zbytkový šum: eco draft13 (30. 9. 2026)

Na vegis.sk zůstávaly mezi nálezy k posouzení věty, ve kterých „prírodný“ nebo „BIO“ nepopisuje prodávaný výrobek: k čemu se surovina přidává („Vhodný do prírodnej kozmetiky“, „ideálny do detskej, jemnej a BIO kozmetiky“), výsledek receptu („Recept na prírodný dezodorant na topánky“, „Vzorový recept – Luxusné BIO anti-age sérum“) a název výrobku čtený jako jmenovaná značka („Náramok Tri Hita Karana“, balijská filozofie). Environmentálním tvrzením je podle čl. 2 písm. o) směrnice 2005/29/ES jen tvrzení o produktu, kategorii produktu, značce nebo obchodníkovi.

- **Nové otázky `eco_other_subject`** (popisuje slovo jako prírodný, bio, eko jen druh výrobků, do kterých se tento produkt přidává, nebo to, co si zákazník vyrobí podle receptu?) **a `eco_diy`** (je věta součástí receptu nebo návodu „urob si sám“?). Obě jsou v `none` pravidel, která předpokládají tvrzení nebo odznak o tomto produktu: `eco_generic_claim`, `eco_generic_claim_open`, `eco_sustainable_claim`, `eco_label_generic_term`, `eco_label_open`, `eco_part_as_whole`. Stávající otázky se nemění, takže se neposunou ani jejich odpovědi.
- **`eco_named_label`** odpovídá ne, když je název jen názvem tohoto produktu nebo řady (po místě, osobě, filozofii) a věta ho nepředstavuje jako značku; **`eco_label`** odpovídá ne, když je věta jen názvem produktu bez slova jako bio, eko, zelený, přírodní a nejde o název značky.
- **Zkoušené a vrácené:** výjimka přímo v `eco_claim` s výčtem slov (natural, organic, bio, eco, clean) zvedla „ano“ u citlivých vět ze 172 na 232, protože Jev slova z výčtu začal číst jako eko tvrzení i u samotného výrobku; tatáž výjimka bez výčtu stáhla pod práh i skutečné položky k posouzení („Prírodné sérum proti vypadávaniu vlasov…“ 0,55 → 0,42). Výjimka „k čemu se přidává“ v `eco_label` odebrala i „BIO Kaméliový olej“, „Bioherba“ a „🌱 Bio & zdravie“. Proto samostatné otázky, jak radí dokumentace Jevu (jedno rozhodnutí na otázku, kombinace v kódu).
- **Měření:** čtyři kola sond na 478 citlivých větách z vegis.sk a naturfyt.sk (0,086 USD). Nová otázka `eco_other_subject` vyšla ano u 11 vět, všech správně (0,54–0,90); `eco_diy` u receptů 0,83–0,96. `eco_named_label` změnila 9 odpovědí, všechny správně (Tri Hita Karana, název obchodu Naturfyt, „Značka: Hanus“, „DAB 10“, „E 425“); `eco_label` kromě náramku jen posuny těsně u prahu.
- **Sken vegis.sk** (108 stránek, 0,398 USD): 50 → 43 nálezů (porušení 14 → 14, k posouzení 29 → 23, k ověření 7 → 6). Zmizelo 9 zamýšlených nálezů a jeden hraniční: „🏡 prírodná dezinfekcia domácnosti“ (`eco_other_subject` 0,51; slovo popisuje i použití samotného oleje). Kolísáním Jevu u znovu položených eko otázek zmizely dva odznaky těsně u prahu („Prirodzené zmeny farby sú bežné pri BIO olejoch“ a „Vŕbovka malokvetá tinktúra, Bioherba“, `eco_label` 0,49) a přibylo pět nálezů 0,50–0,55; čtyři jsou skutečné položky k posouzení („100 % prírodný francúzsky zelený íl“), pátý nesmyslný („✔️ vodotesný horoskop EN“, `eco_claim` 0,58 už v minulém běhu). **Sken naturfyt.sk** (43 stránek, 0,154 USD): 36 → 36, nové otázky nepotlačily nic, dvě výměny těsně u prahu (0,51). Testy 161 ze 161 včetně živých testů na testovacích e-shopech.

## Životnost podle definice: dur draft3 (30. 9. 2026)

Na vegis.sk (100 produktů) dalo `dur_lifetime_claim` 7 nálezů a 6 z nich byla trvanlivost při skladování („Sérum vydrží 2–4 týždne“, „vydrží tri mesiace v chladničke“, „si zachováva kvalitu … až 1 rok“), sedmý byla doba, na kterou vystačí náplň difuzéru. Životnost je ale podle § 617 písm. d) (text zákona č. 108/2024 Z. z. v podkladech) „schopnosť zachovať si pri bežnom používaní svoju funkčnosť a výkonnosť“.

- **dur draft3:** otázka se ptá, jak dlouho produkt při běžném používání funguje nebo jaké používání vydrží, a odpovídá ne u trvanlivosti při skladování („v lednici vydrží tři měsíce“, „spotřebujte do šesti měsíců“) a u doby, na kterou vystačí balení, zásoba nebo jedna náplň.
- **Měření** (skutečné věty z vegis.sk s kontextem): trvanlivost při skladování 0,64–0,82 → 0,06–0,24, náplň difuzéru 0,78 → 0,14; kontrolní věty „Motor vydrží 10 rokov každodenného používania“, „Batéria vydrží až 500 cyklov“, „Životnosť LED žiarovky je 25 000 hodín“, „Bunda vydrží aj 100 praní“ 0,95–0,98 beze změny. „Doba horenia: 22 hodín“ u svíčky zůstává (0,74): jde o dobu používání výrobku a doslovné znění definice ji nevylučuje, proto zůstává nálezem k ověření.

## Zrychlení: jedno volání na větu, síto po odstavcích sieve draft1, souběžnost (30. 9. 2026)

Sady pravidel se nemění (stejné otázky, stejné verze), mění se, kolik volání se posílá:

- **Jedno volání na větu:** otázky všech větných modulů (eco 14, dur 5, ucp 4) jdou v jednom požadavku, cache dál ukládá odpovědi po modulech. Na vegis.sk (108 stránek, stejná sada stránek, bez cache) 21 182 → 7 084 volání, 0,98 → 0,75 USD, 22 → 9,7 min; 55 z 57 nálezů stejných. Odpovědi se od volání po modulech liší stejně jako dva běhy téhož způsobu (72,0 % a 72,6 % odpovědí úplně stejných, průměrný rozdíl 0,0034, přes práh 0,5 přešlo 5 a 4 z 6 900); rozdílné nálezy mají skóre 0,50–0,55.
- **Síto po odstavcích, config/sieve.yaml sieve-2026-09-30-draft1:** úseky hlavního textu do 600 znaků, jedna otázka na téma za modul, práh 0,2. Měřeno na vegis.sk a naturfyt.sk: žádný ztracený nález, nejnižší pravděpodobnost síta u úseku se skutečným nálezem 0,64 a 0,87, cena 45 % a 57 % běhu bez síta. Síto po celé stránce dopadlo hůř (61 %), protože téma „příroda, bio“ je na obchodě s přírodními produkty skoro na každé stránce. Otázka na téma eco zahrnuje i férový obchod a dobré životní podmínky zvířat („vegan“, „cruelty-free“); bez toho by při prahu 0,5 vypadly odznaky „vegánsky produkt“.
- **Limit Jevu:** asi 32 000 tokenů na požadavek (80 000 znaků prošlo, 90 000 vrátilo `max_tokens_exceeded`); vložená eko věta se v textu do 80 000 znaků našla na začátku, uprostřed i na konci (0,88–0,96). Úsek delší než 20 000 znaků se sítu neposílá a jeho věty jdou celé.
- **Souběžnost zůstává 8 a 1 200 požadavků za minutu:** to je dokumentovaný limit jev-1.13.0 (1 200 požadavků za minutu, 250 000 tokenů za sekundu, docs.typesafe.ai/models). Test 300 požadavků s 32 souběžnými spojeními (77 za sekundu, bez chyb) se jen vešel pod minutový limit; dlouhý sken by limit překračoval, proto se nastavení 32 a 4 000 za minutu vrátilo.
- **Přizpůsobivé tempo stahování** (strop 3 stránky za sekundu podle rozhodnutí uživatele): start 1 za sekundu, zrychlení při odpovědích do 0,5 s, zpomalení při pomalých odpovědích a chybách, při 429/503 čekání podle Retry-After a nejvýš dva opakované pokusy, Crawl-delay z robots.txt. vegis.sk: server odpovídá za 0,1 s, 107 stránek za 42 s (průměr 2,7 za sekundu) místo 3,6 min; nálezy beze změny (50 z 50).

## Tři skupiny nálezů a rešerše značek: eco draft11 a draft12, legal-cz draft3, lr draft1 (29. 9. 2026)

Po eco draft10 zbylo na Naturfytu 31 nálezů ve větách a většina nebyla chybou nástroje, ale otevřenou částí zákona: černá listina platí vždy, jen když jsou splněné všechny znaky, a u slov jako „prírodný“, „BIO“ u kosmetiky nebo odznaku „Vegan“ zákon předem neříká, zda jde o environmentální tvrzení. Komise je posuzuje případ od případu podle vnímání průměrného spotřebitele (otázky a odpovědi č. 3, 5, 14 a 15). Výstup je proto rozdělený do tří skupin podle toho, nakolik nález rozhoduje text zákona:

- **Nová hodnota `checkability: assess` („k posouzení“)** vedle `text` („porušení podle textu zákona“) a `verify` („k ověření“, rozhoduje fakt mimo web). Zpráva má oddíly v tomto pořadí, souhrn uvádí počty skupin a pásmo jistoty se místo „k ověření“ jmenuje „nižší jistota“, aby se nepletlo se skupinou.
- **eco draft11, nová otázka `eco_explicit_term`:** používá věta výraz, který odůvodnění 9 a 10 směrnice (EU) 2024/825 uvádí jako příklad obecného tvrzení („ekologický“, „šetrný k životnímu prostředí“, „zelený“, „přátelský k přírodě“…; odůvodnění uvádí i „podobná tvrzení“), nebo přímo tvrdí přínos pro životní prostředí, přírodu či klima? `eco_generic_claim` ji vyžaduje (porušení), nové `eco_generic_claim_open` ji vylučuje (k posouzení, s odkazem na otázky Komise č. 3 a 14).
- **eco draft11, nová otázka `eco_named_label`** (jmenuje věta značku vlastním názvem?) a tři pravidla pro značky místo jednoho: `eco_label_unrecognized` jen pro jmenované značky mimo seznam (k ověření certifikace), `eco_label_generic_term` pro odznak s výrazem z odůvodnění 9, například „Eco“ (porušení, body 3 a 6), a `eco_label_open` pro ostatní odznaky, například „Vegan“, „Vegetarian“, „GMO free“, „BIO“ u kosmetiky nebo lístek 🌱 (k posouzení, otázky Komise č. 5, 14 a 15). Odznaková pravidla se nepoužijí na větu, kterou Jev hodnotí jako obecné tvrzení (`eco_generic`: odznak není tvrzení ve větě); jinak by jedna věta dala dva nálezy.
- **eco draft11, tři chyby nástroje:** `eco_claim` odpovídá ne u pokynu, který zmiňuje jiný produkt („Vlasy umyte prírodným šampónom.“ 0,53 → 0,09); `eco_generic` odpovídá ne, když obecný výraz jen popisuje, co zaručuje jmenovaná značka („Certifikát Ecogarantie zaručuje … prísne ekologické požiadavky“ 0,53 → 0,11, zůstává jako značka k ověření); dvojí nález u „Prírodný ekologický prací gél s Bio eukalyptovou silicou“ řeší pravidlo výše. Zkoušené a vrácené: výjimka „bio popisuje jen jednu složku“ v `eco_label` stáhla odznak „GMO free“ z 0,62 na 0,43.
- **Přehodnoceno:** „ekodrogéria“ jako název kategorie je porušení, ne otevřený případ: definice environmentálního tvrzení výslovně zahrnuje tvrzení o „kategórii produktov“ a „eko“ je příkladem z odůvodnění 9.
- **eco draft12:** `eco_part_as_whole` (bod 7) vyžaduje `eco_explicit_term`; věta jen s „prírodný“ („Prírodný čistič … s včelím a karnaubským voskom“) je jen k posouzení, ne zároveň porušení. Pravidla `eco_label_unrecognized` a `eco_label_open` mají novou kontrolu `label_notes`: nález uvede poznámku ke značce ze souboru značek.
- **config/labels.yaml podle rešerše podklady/reserse/znacky-udrzatelnosti.md (26. 9. 2026, 61 značek):** `sustainability_labels` obsahuje jen značky „vyhovuje“ (orgány veřejné moci a certifikační systémy, u kosmetiky COSMOS ORGANIC/NATURAL a NATRUE), `excellent_performance_labels` jen EU Ecolabel a šest národních ekoznaček typu I (holé „Ekologicky šetrný výrobek“ je i obecné tvrzení, proto jen s vazbou na ekoznačku). Nový oddíl `label_notes` má 25 poznámek ke značkám „nevyhovuje“ a „NEOVĚŘENO“, například „Ecocert: logo vyhovuje jen se signaturou COSMOS…“, „Ecogarantie: neověřená…“, „PETA: vlastník sám uvádí, že loga směrnici nesplňují“.
- **legal-cz draft3:** `legal_complaints` se ptá jen, zda text vysvětluje, jak nebo kde reklamovat, stejně jako legal-sk draft4. Staré znění „jak, kde a za jakých podmínek“ kolísalo u reklamačního řádu rozděleného do odstavců kolem prahu 0,7 a ostrý test bez cache jednou neprošel (nález navíc `legal_complaints_missing`).
- **lr draft1 (bod 15, zákonné požadavky jako přednost):** modul a seznam 61 položek `config/legal_requirements.yaml` z rešerše podklady/reserse/zakonne-poziadavky-ako-prednost.md jsou hotové a otestované (výrazy tvrzení se znak po znaku shodují s testovacím skriptem rešerše, ostrý test na testovacím e-shopu prošel), ale na skutečném e-shopu neověřené, proto `enabled: false`. Informace o zákoně a bezvýznamné výhody mají skupinu k posouzení.

Ověření: sonda na všech 31 větách z Naturfytu s jejich kontextem a 7 kontrolních větách (2× 0,003 USD); sken Naturfytu s eco draft11 (0,185 USD): 12 porušení, 18 k posouzení, 6 k ověření (4 za celý web jsou mezi nimi). Offline testy 144 ze 144, ostrý test na českém i slovenském testovacím e-shopu prošel dvakrát po sobě.

## Značky a odznaky podruhé: eco draft10 (26. 9. 2026)

Kontrolní sken naturfyt.sk po draft9 ukázal dvě chyby:

- **Odznaky se na většině stránek dál slévaly** („Vegan Eco Doprava ZDARMA nad 39,90 €“): SmartReader (Readability) z výstupu maže atributy `class`, podle kterých se odznaky poznají. Rozdělení fungovalo jen na stránkách čtených záložní heuristikou. SmartReader teď třídy odznaků a drobečkové navigace (`breadcrumb`) zachovává; na uložené produktové stránce Naturfytu vychází „Vegan“, „Raw“ a „Doprava ZDARMA nad 39,90 €“ zvlášť.
- **Oprava draft9 u odznaku „Vegan“:** otázky a odpovědi Komise (září 2026) mají k „vegan“ a „vegetarian“ vlastní otázku č. 15: zda jde o značku udržitelnosti, záleží na konkrétním případu, na kontextu a na vnímání průměrného spotřebitele; značkou nebo environmentálním tvrzením to je, pokud obchodník naznačuje přínos pro životní prostředí nebo sociální přínos (příklad Komise: „vegan = better for the planet“). Draft9 to opíral jen o obecnou definici sociálních znaků a otázka `eco_social_label` pak počítala za značku i popisné věty: „Vegánske kakaové karamelky…“, „pre vegánov“, „Produkty pre rôzne diéty: … vegan, raw…“ (0,65–0,73) a „Bez testovania na zvieratách.“ (0,51).
- **eco draft10:** `eco_social_label` se ptá jen na značku, pečeť, logo, certifikát nebo odznak; slova „vegánsky“, „vegetariánsky“ nebo „netestované na zvieratách“, která jen popisují produkt, značkou nejsou. Samostatný odznak „Vegan“ zůstává jako jeden nález k ověření se všemi stránkami; vysvětlení a doporučení u `eco_label_unrecognized` nově cituje otázku č. 15. Tvrzení „netestované na zvieratách“ se posoudí v připravovaném modulu k bodu 15 (zákonné požadavky prezentované jako zvláštnost nabídky).
- **eco draft10:** `eco_label` odpovídá ne, když věta jen obecně říká, že produkty nebo značky jsou certifikované, a žádnou značku nejmenuje („Len overené a certifikované značky – bezpečné a šetrné.“ 0,62 → 0,13; jako obecné tvrzení se dál posuzuje).
- **Názvy kosmetiky s „BIO“** („RaE Dezodorant Citrónová tráva BIO 25 ml“) zůstávají nálezem k ověření: podle otázky a odpovědi č. 3 může být název produktu environmentálním tvrzením a podle č. 14 mohou být „bio“ a „eco“ mimo biopotraviny obecným tvrzením. `eco_generic` u nich dává jen 0,15–0,32, proto je dál zachycuje otázka na značku.

Ověření na skutečných větách z Naturfytu s jejich kontextem (22 vět, obě znění, 0,0025 USD): všech 6 falešných značek zmizelo, všechny skutečné značky, odznaky a certifikáty („Vegan“, „Vegetarian“, „Eco“, „GMO free“, BDIH, Ecogarantie) i názvy kosmetiky s „BIO“ zůstaly. Testovací e-shop má na čistém produktu novou větu „je vhodný aj pre vegánov“; offline testy 130 ze 130, ostrý test proti Jevu na českém i slovenském testovacím e-shopu prošel.

## Šum ze skutečného e-shopu: eco draft9, dur draft2, legal-sk draft4 (26. 9. 2026)

Opakovaný sken naturfyt.sk (40 produktů) ukázal zbylý šum; opravy stojí na zákoně a na struktuře HTML, ne na seznamech slov:

- **Odznaky u produktu** (Shoptet `span.flag`, jinde `badge`, `label`, `sticker`, `ribbon`, `tag`) se čtou každý zvlášť; dřív z nich vznikala „věta“ „Vegan Eco Doprava ZDARMA nad 39,90 €“.
- **eco draft9:** samostatný odznak („Eco“, „Bio“, „Green“) je podle definice v § 2 písm. o) značka udržitelnosti (známka dôveryhodnosti), a proto bod 3, ne obecné tvrzení podle bodu 6 (obecné tvrzení je jen takové, které „sa neuvádza na značke udržateľnosti“). `eco_generic` u odznaku odpovídá ne, `eco_label` ho výslovně zahrnuje; jeden odznak tak dává jeden nález místo dvou.
- **Odznak „Vegan“** je také značka udržitelnosti: sociální znaky zahrnují „etické záväzky, ako sú dobré životné podmienky zvierat“ (odůvodnění 3 směrnice (EU) 2024/825, otázky a odpovědi Komise č. 9). `eco_social_label` ho proto zahrnuje; nález je k ověření (certifikace, například V-Label).
- **Seznam složení** s „BIO“ u jednotlivých složek není tvrzení o výrobku: `eco_generic` i `eco_label` u něj odpovídají ne.
- **Popisky zaškrtávacích políček a přepínačů** (filtry v postranním panelu, „Eco 1“) se nečtou, jsou to ovládací prvky, ne text.
- **Stejný text se stejným pravidlem** na více stránkách (odznak „Vegan“ u deseti produktů) je jeden nález se seznamem stránek.
- **dur draft2:** `dur_repairable_claim` odpovídá ne u popisu používání nebo nastavení výrobku („proces vytlačenia je vratný“ dal 0,66).
- **legal-sk draft4:** `legal_complaints` se ptala na tři znaky naráz („ako, kde a za akých podmienok“), a když byl reklamační řád rozdělený do odstavců, odstavec „Kde a ako reklamovať“ dostal jen 0,73 při prahu přítomnosti 0,7 a v jednom ostrém běhu testu práh nepřekročil. Otázka se nově ptá jen, zda text vysvětluje, jak nebo kde reklamovat: 0,98; odstavec o lhůtách 0,11. Věta „reklamujte podľa reklamačného poriadku“ bez postupu dává 0,74 (na webu se skutečným reklamačním řádem neškodí).

Ověření: offline testy 127 ze 127, ostrý test proti Jevu na českém i slovenském testovacím e-shopu prošel.

## Zaměření na nové povinnosti: eco draft8, dur draft1, ucp draft4, ucp-parked draft1, legal-sk draft3 (26. 9. 2026)

Rozsah podle zadání uživatele:
- **Slovensko:** nové povinnosti (zákon č. 310/2025 Z. z. od 27. 9. 2026; tlačítko pro odstoupení podle zákona č. 311/2025 Z. z. od 19. 6. 2026), k tomu to, za co trestá SOI, a zákonné informační povinnosti.
- **Česko:** jen to, za co trestá ČOI, a zákonné informační povinnosti.

Změny sad:

- **eco draft8:** běží jen pro Slovensko (`jurisdictions: [sk]`). V Česku směrnice (EU) 2024/825 zatím neplatí (sněmovní tisk 53) a ČOI za eko tvrzení doložené pokuty nemá.
  - `eco_claim` a `eco_generic` nově výslovně odpovídají „ne“, když krátký nadpis tvrzení jen přebírá z kontextu.
  - Na skutečném e-shopu (naturfyt.sk) daly nadpisy „Podrobný popis“ 0,57 a „Vhodný:“ 0,54.
- **ucp draft4:** zůstaly jen recenze, tedy body 23b a 23c přílohy I (SK príloha č. 1 body 31 a 32, CZ písm. y) a z)).
  - Doklady o pokutách: ČOI vede samostatné kontrolní akce k recenzím (zprávy za roky 2024 a 2025) a SOI ve výroční zprávě 2025 uvádí na webech „falošné profily, zľavy, recenzie“.
- **ucp-parked draft1** (`enabled: false`): naléhavost, zákonná práva jako výhoda, „zdarma“ s poplatkem a léčebná tvrzení.
  - Nejsou nové a ve zprávách ČOI a SOI v podkladech k nim doklad o pokutách není.
  - Odladěné znění (ucp draft3) zůstává pro případné zapnutí.
- **legal-sk draft3:**
  - Informace o mimosoudním řešení sporů jsou nově doslova podle § 5 ods. 1 písm. q), jako dvě otázky:
    - `legal_adr` vyžaduje odkaz na web s informacemi o subjektu alternativního řešení sporů. Za odpověď „ne“ se počítá SOI uvedená jen jako orgán dozoru nebo odkaz jen na zrušenou platformu ODR.
    - `legal_redress_request` vyžaduje poučení o žádosti o nápravu (§ 11 zákona č. 391/2015 Z. z.).
  - Na naturfyt.sk dala stará otázka 0,91 odstavci, kde je SOI uvedená jen jako orgán dozoru.
  - Nový typ pravidla `site_signal` hledá znak na celém webu (text, odkazy, obrázky) bez Jevu. Nález je vždy k ověření, protože košík, pokladnu a zákaznický účet nástroj nestahuje. Tři nová pravidla:
    - `legal_harmonized_notice_missing`: § 5 ods. 1 písm. f) a prováděcí nařízení (EU) 2025/1960, od 27. 9. 2026. Hledá se obrázek, odkaz nebo text harmonizovaného oznámení; text „Zákonná záruka je 24 mesiacov“ ho nenahrazuje.
    - `legal_withdrawal_function_missing`: § 20a, od 19. 6. 2026; směrnice 2011/83/EU, čl. 11a vložený směrnicí (EU) 2023/2673. Hledá se jen v textu odkazů, protože věta „má právo odstúpiť od zmluvy“ není funkce.
    - `legal_odr_link_outdated`: platforma ODR zrušena 20. 7. 2025 nařízením (EU) 2024/3228; zákon č. 310/2025 Z. z., čl. I body 1 a 46, vypustil odkazy na nařízení č. 524/2013.
- **dur draft1** (nový modul, jen Slovensko): príloha č. 1 body 34, 36, 37, 38 a 39, tedy tvrzení o životnosti, opravitelnosti, spotřebním materiálu, neoriginálních dílech a aktualizacích (EU body 23e a 23g–23j).
  - Všechny zákazy závisí na pravdivosti, proto jsou nálezy k ověření.
  - Otázka o životnosti vylučuje délku záruky a údaj, na jak dlouho vystačí balení.
  - Body 33 a 35 (zamlčení, skrytá vlastnost) z textu webu ověřit nejdou.

Ověření: offline testy 122 ze 122. Nový slovenský testovací e-shop (`Fixtures/site-sk`) i upravený český prošly ostrým testem proti Jevu napoprvé.

## legal-sk-2026-09-26-draft2 (26. 9. 2026)

- Sada je zapnutá (`enabled: true`), protože primárním trhem je Slovensko; výchozí země nástroje je nově `sk`. Znění otázek a pravidel se nemění, ustanovení mají dál status „ověřit“ pro kontrolu právníkem.

## eco-2026-09-26-draft7, ucp-2026-09-26-draft2 a draft3 (26. 9. 2026, ostré běhy proti Jevu)

První ostrý běh s eco draft6 a ucp draft1 (jev-1.13.0, otázky anglicky, 216 volání, 0,011 USD): 13 z 15 nastražených nálezů, 6 nálezů navíc, jeden z nich na čisté stránce. Příčiny podle pravděpodobností jednotlivých otázek a opravy:

- `eco_sustainable_term` se ptalo i na „podobné slovo“ a dalo 0,77 větě „Tento šampon je ekologický a šetrný k přírodě.“ a 0,61 biomoštu. Výjimka v `eco_generic_claim` pak přesunula obecné tvrzení k nesprávnému pravidlu a biomošt dostal nález. Otázka se nově ptá jen na tři slova z odůvodnění 10 směrnice (EU) 2024/825 („uvědomělé“, „udržitelné“, „odpovědné“) a jejich tvary. Čistě environmentální slova z odůvodnění 9 výslovně vylučuje. Slovo „etický“ vypadlo: v odůvodnění 10 není a samo o sobě není environmentálním tvrzením. Výsledek: 0,13 a 0,14.
- `eco_generic` dalo 0,46 větě „Jsme odpovědná a udržitelná firma.“, protože sousední věta o klimatické neutralitě působila jako upřesnění. Otázka nově vysvětluje, co je konkrétní údaj, a to příkladem z odůvodnění 9 („100 % energie použité k výrobě tohoto obalu pochází z obnovitelných zdrojů“). Jiné obecné tvrzení poblíž upřesněním není. Výsledek: 0,64, nález k ověření.
- `eco_generic` dávalo 0,69 větě „Oceněno certifikátem GreenStar Planet.“, takže vedle `eco_label_unrecognized` vznikl druhý nález za totéž. Tvrzení obsažené v označení udržitelnosti podle definice v čl. 2 písm. p) směrnice 2005/29/ES obecným tvrzením není a posuzuje se podle bodu 2a. Otázka proto odpovídá „ne“, když je obecný pojem jen součástí názvu značky, certifikátu nebo pečeti. Výsledek: 0,14.
- `eco_neutral` dalo 0,78 budoucímu cíli „Do roku 2030 budeme vyrábět zcela bez emisí.“ a spustilo i `eco_company_climate_claim`. Otázky a odpovědi Komise (č. 6, písm. c) řadí tvrzení o budoucím přechodu ke klimatické neutralitě samostatně pod čl. 6 odst. 2 písm. d), tedy pod pravidlo `eco_future_claim`. Otázka proto budoucí cíle vylučuje. Výsledek: 0,13.
- `eco_offset_basis` dalo stejnému budoucímu cíli 0,96 kvůli sázení stromů v sousední větě, která patří k jinému tvrzení. Otázka se nově ptá, zda na kompenzacích stojí tvrzení ve větě samotné. Výsledek: 0,36; nastražené věty s kompenzací drží 0,98.
- `ucp_free` dalo jen 0,72 větě „Vzorek zdarma, stačí uhradit balné 29 Kč.“. Jev zřejmě posuzoval, jestli je vzorek opravdu zdarma, jenže bod 20 se týká právě označení „zdarma“ při poplatku. Draft2 doplnil „odpověz ano i tehdy, když je s tím spojený poplatek nebo podmínka“ a příklad „za 0 €“ pro Slovensko: 0,96. Nadpis „Vzorek pleťového krému“ tím ale stoupl z 0,36 na 0,60 kvůli větě pod ním a vznikl duplicitní nález. Draft3 se proto ptá jen na samotnou větu a výslovně říká „ne“, když je „zdarma“ jen v kontextu: nadpis 0,08.

Výsledek eco draft7 a ucp draft3 ve dvou bězích (CLI s cache a živý test bez cache) je stejný. Všech 15 nastražených nálezů se najde, nálezy za celý web sedí a na čistých stránkách není nic. Zbývají dva nálezy k ověření (0,60 a 0,64) u „přírodní kosmetiky“ na úvodní stránce a na stránce O nás. Nechávám je: podle otázek a odpovědí Komise (č. 3) může slovo „natural“ v názvu vyvolat environmentální asociaci a posuzuje se případ od případu.

## ucp-2026-09-26-draft1 (26. 9. 2026)

Nový modul `ucp` pro nekalé praktiky z černé listiny, které se na e-shopech vyskytují nejčastěji a Jev je pozná z věty (sada A katalogu kontrol). Body směrnice 2005/29/ES, příloha I: 7, 10, 17, 20, 23b a 23c. Slovensko je primární trh: zákon č. 108/2024 Z. z., príloha č. 1 body 11, 14, 23, 26, 31 a 32 (číslování od 27. 9. 2026, dřívější čísla 7, 10, 18, 21, 26 a 27 ověřená ve starším znění). V Česku platí stejné body jako příloha č. 1 písm. f), i), q), t), y) a z) zákona č. 634/1992 Sb. ve spojení s § 4 odst. 4, tedy tak, jak je cituje ČOI.

- 14 otázek, každá s jedním znakem a větou „Pokud …, odpověz ne“: časové omezení, omezená zásoba, zákonné právo, výhoda obchodu (čte i kontext kvůli nadpisům jako „Proč nakoupit u nás?“), nad zákonné minimum, léčení nemoci, lék nebo zdravotnický prostředek, zdarma, poplatek u věci zdarma, dárek podmíněný nákupem, tvrzení o ověřených recenzích, odměna za recenzi, odměna za kladnou recenzi, zveřejňování jen kladných recenzí.
- Poštovné a příplatek za zvolený způsob platby otázka `ucp_free_extra_fee` výslovně vylučuje: bod 20 připouští „nevyhnutelné náklady“ na převzetí nebo doručení (Pokyny Komise 2021/C 526/01, oddíl 3.4).
- Neutrální údaj o dostupnosti („Skladem 12 ks“) otázka `ucp_urgency_stock` výslovně vylučuje; falešné časovače a tvrzení o omezených zásobách patří k bodu 7 podle oddílu 4.2.7 Pokynů.
- Pravidla, u kterých z textu nelze poznat pravdivost (naléhavost, léčení, ověřené recenze, dárek k nákupu, odměna bez podmínky), mají `checkability: verify`. Jistý nález z textu (`text`) je jen tam, kde zákaz nezávisí na skutečnosti mimo web: zákonné právo jako výhoda, poplatek u věci zdarma, odměna za kladnou recenzi, jen kladné recenze.
- Léčebná tvrzení zatím rozlišují jen lék nebo zdravotnický prostředek (`ucp_cure_claim_medicine`, nízká závažnost). Rozlišení potravin, doplňků stravy a kosmetiky podle odvětvových předpisů se doplní po dokončení rešerše.

## eco-2026-09-26-draft6 (26. 9. 2026)

Primárním trhem je Slovensko, kde od 27. 9. 2026 platí zákaz podle směrnice (EU) 2024/825 (zákon č. 310/2025 Z. z.). Změny ověřené v podkladech (odůvodnění 9, 10 a 12 směrnice (EU) 2024/825, čl. 2 písm. q) směrnice 2005/29/ES, otázky a odpovědi Komise ze září 2026 č. 6, 7, 10 a 14):

- Odkazy: slovenská príloha č. 1 body 3, 6, 7 a 8 s účinností od 27. 9. 2026. Český odkaz na přílohu č. 1 zákona č. 634/1992 Sb. nahradil § 4 odst. 3 a 4 a § 5 (klamavé konání), protože Česko směrnici zatím nepřevzalo (sněmovní tisk 53 nebyl k 25. 9. 2026 schválen). Pravidla mají nové pole `explanation_by_jurisdiction`, takže česká zpráva neříká „zakázané“, ale „může být klamavé, posuzuje se případ od případu“.
- `eco_sustainable_claim` (nové, nová otázka `eco_sustainable_term`): „udržitelný“ nebo „odpovědný“ bez upřesnění. Bez výjimky pro ekoznačku, protože se tyto pojmy týkají i sociálních znaků a o ekoznačku se opřít nedají (odůvodnění 10).
- `eco_neutrality` vyžaduje nově uvedený základ v kompenzacích (otázka `eco_offset_basis`) a vylučuje tvrzení o celé firmě (`eco_company_level`): zákaz bodu 4c se týká produktů (otázka Komise č. 10).
- `eco_climate_claim_unsupported` (nové): tvrzení o klimatu bez uvedeného základu. Jde o obecné tvrzení podle bodu 4a (otázka č. 6); na čem stojí, z textu nepoznáme, proto `verify`.
- `eco_company_climate_claim` (nové): klimatická neutralita celé firmy založená na kompenzacích. Posuzuje se podle bodu 4a, ne 4c, proto střední závažnost a `verify`.
- `eco_neutral` zahrnuje i „klimaticky pozitivní“ a snížený, kompenzovaný či omezený dopad na klima, jak je vyjmenovává odůvodnění 12.
- `eco_label_unrecognized` se spustí i u sociálních značek (nová otázka `eco_social_label`), protože označení udržitelnosti podle čl. 2 písm. q) zahrnuje environmentální i sociální znaky.
- `eco_future_claim` (nové): budoucí environmentální závazek, posouzení podle čl. 6 odst. 2 písm. d) směrnice (SK § 10 ods. 2 písm. d)), vždy `verify`.
- `eco_generic_claim` vylučuje věty, které mají vlastní pravidlo („udržitelný“, tvrzení o klimatu), aby jedna věta nedávala dva nálezy za totéž.

## eco-2026-09-25-draft5 (25. 9. 2026)

- `eco_part_benefit` se ptá, zda věta nebo její kontext uvádí výhodu omezenou na jednu část, a výslovně říká, že bez výhody je odpověď ne (draft4 formulaci předpokládala). Měření ukázalo, že vysoké hodnoty u vět jako „Objednávky odesíláme do 2 pracovních dnů“ (0,95) jsou správně: v jejich kontextu stojí „Doprava je klimaticky neutrální…“. Rozlišení mezi nálezem a čistou větou dělá hlavně `eco_whole_claim` („Nese ekoznačku EU Ecolabel.“ 0,73 × výhoda části 0,39, bez nálezu; „Ekologický produkt – obal…“ 0,72 × 0,96, nález).
- Pravidlo `eco_part_as_whole` dostalo výjimku `none: eco_organic_food` jako obě další pravidla s „bio/eko“: certifikovaná biopotravina je „bio“ celá a má přednost nařízení (EU) 2018/848. Bez ní se hlásil „BIO jablečný mošt z certifikovaného ekologického zemědělství.“ (0,72).

## eco-2026-09-25-draft4 (25. 9. 2026)

- Otázka `eco_part_as_whole` se ptala na dva znaky naráz a u vět se značkou kolísala kolem prahu 0,5. Nahradily ji dvě otázky podle postupu v zadání (jeden znak = jedna otázka): `eco_whole_claim` (věta prezentuje jako přínosný celý produkt nebo firmu) a `eco_part_benefit` (uvedená výhoda se týká jen části). Pravidlo `eco_part_as_whole` vyžaduje obě a k tomu `eco_claim`.

## eco-2026-09-25-draft3 (25. 9. 2026, první běh proti skutečnému Jevu)

Odchylky na testovacím e-shopu s draft2 (jev-1.13.0, otázky anglicky):

- `eco_label_unrecognized` se spustilo u věty o certifikovaném ekologickém zemědělství (eco_label 0,92). Jev měl pravdu, že jde o certifikaci, ale ta je založená na systému zavedeném orgánem veřejné moci (nařízení (EU) 2018/848), takže podle bodu 2a je povolená. Pravidlo dostalo stejnou výjimku jako obecné tvrzení: `none: eco_organic_food`.
- `eco_part_as_whole` dávalo 0,51–0,58 i větám, které mluví jen o jedné části (obal, doprava) a na celek ji nezobecňují. Otázka nově výslovně říká, že taková věta je „ne“. Věty z testovacího e-shopu do otázky záměrně nepatří.

Výsledek draft3 ve třech bězích: všechny nastražené nálezy nalezené, plané poplachy u biomoštu, obalu a dopravy zmizely. Nově ale `eco_part_as_whole` dává větě „Nese ekoznačku EU Ecolabel.“ 0,52–0,53 ve dvou ze tří běhů (hrana prahu 0,5) a nastražený nález u produktu 3 klesl z 0,85 na 0,78 (k ověření). Otázka se ptá na dva znaky naráz (tvrzení o celku a výhoda jen části); návrh je rozdělit ji na dvě otázky ano/ne podle postupu v zadání, čeká na schválení.

## eco-2026-09-25-draft2, legal-cz-2026-09-25-draft2 (25. 9. 2026)

- Otázky eko modulu se ptají na pole `sentence` a upřesnění hledají i v `context_before` a `context_after`. Zákon posuzuje upřesnění „na stejném médiu“, podle Q&A Komise hned vedle tvrzení.
- Nová otázka `eco_organic_food` a podmínka `none`: certifikované biopotraviny smí „bio“ a „eko“ používat (Q&A Komise, otázka 14).
- `eco_generic_claim` ověřuje seznam `excellent_performance_labels` na celé stránce; `eco_label_unrecognized` ověřuje seznam `sustainability_labels` ve větě (body 4a a 2a mají různé seznamy).
- Doplněné odkazy na slovenskou přílohu č. 1 (body 3, 6, 7, 8) a na § 1820 odst. 1 písm. s) OZ.

## legal-sk-2026-09-25-draft1 (25. 9. 2026)

- První verze slovenské sady s ověřenými ustanoveními zákona č. 108/2024 Z. z.; vypnutá (`enabled: false`) do kontroly právníkem.

## eco-2026-09-25-draft1, legal-cz-2026-09-25-draft1 (25. 9. 2026)

- Úvodní návrh ze zadání.
