# EshopGuard: všechny návrhy rozvoje (30. 9. 2026)

Uživatel 30. 9. 2026: „udělal bych to všechno … jako poslední návrh dej to renderování pomocí Chromia na straně serveru a jako předposlední ten doplněk pro Shoptet“. Pořadí níže je podle hodnoty. Poslední dvě položky určil uživatel.

UI se navrhuje v plátně https://claude.ai/artifact/34wYLsJzieFtmWdueAwYay a kód se píše až po schválení návrhu.

Hotovo 30. 9. 2026 a v seznamu už není: rozpoznání stránek, jejichž text vykresluje JavaScript (`TextNotLoaded`).

## Rozbor: hromadné opravy (seskupení stejných textů)

**Měřeno na posledních skenech** (vzorky, ne celé e-shopy):

| E-shop | Nálezy | Položky „nález × stránka“ | Opakované texty |
|---|---|---|---|
| vegis.sk | 40 | 147 | 1 text ze šablony na všech 108 stránkách (73 % položek) |
| naturfyt.sk | 32 | 118 | 8 opakovaných textů dává 94 položek (80 %): šablona 39×, odznak „Vegan“ 19×, „Certifikovaná prírodná kozmetika“ 11×, „Len overené a certifikované značky…“ 11×, „Eco“ 5× a další |

Seskupení tedy u obou vzorků ubere asi 73 % rozhodnutí: 147 → 40 a 118 → 32. Na celém e-shopu s 5 000 stránkami roste počet opakování lineárně, takže bez seskupení se nedá pracovat.

**Zjištěná mezera v dnešním přepisu:** `PageRewriter.BuildWork` přepíše opakovaný nález jen na PRVNÍ stránce. Ostatní stránky jen vypíše v poli `AlsoOn`. Opakovaný text se tedy dnes opraví na jedné stránce z 38 a na zbytku ho obchodník musí přepsat ručně.

**Druhy opakování a jak s nimi naložit:**
1. **Šablona webu** (lišta výhod, patička, hlavička, banner). Opraví se jednou v nastavení nebo šabloně e-shopu. Konektor šablonu často zapsat nemůže, pak nabídnout „Kopírovať text“ a návod, kde ho změnit. V seznamu oprav je to položka „Celý e-shop: šablóna“, ne změna na každé stránce.
2. **Stejná věta nebo odznak v textech produktů** (standardní věta obchodu, text dodavatele, odznaky „Vegan“, „BIO“). Řeší ji hromadná oprava.
3. **Podobné, ale různé věty** se stejným výrazem („ekologická alternatíva“ v různých větách). NESESKUPOVAT do jedné opravy: slovenština skloňuje a okolní věta je pokaždé jiná. Opraví se po stránkách. Pro přehled je lze seskupit v zobrazení „Podľa nálezov“.

**Jak generovat text u seskupené věty.** Zvážené varianty:
- (a) přepis po stránkách jako dnes;
- (b) jeden vložitelný náhradní text pro celou skupinu;
- (c) bez generování, text napíše obchodník.

Doporučení je (b) s kontrolou na každé stránce:
1. Skupina = stejný normalizovaný text a stejné nálezy na aspoň 2 stránkách.
2. Jedno volání modelu dostane větu, nálezy a až 5 různých okolí (předchozí a následující věta z různých stránek). Pokyn zní:
   - náhradní věta musí stát sama o sobě;
   - nesmí se opírat o sousední věty;
   - okolí se nemění;
   - místo náhrady je možné větu odstranit;
   - fakta se nevymýšlejí, chybějící údaj = „[doplňte: …]“.

   Model u každého ukázkového okolí řekne, jestli tam náhrada sedí.
3. Náhrada se zkontroluje Jevem v okolí každé stránky. Kontrola je levná a stejná okolí vyřeší cache.
4. Stránky, kde nález trvá nebo náhrada nesedí, vypadnou ze skupiny do „riešiť jednotlivo“. Například stránky, kde další věta na opravovanou navazuje („Preto sú aj…“).
5. Přepis stránky se dělá AŽ po skupinových náhradách. Skupinový text je zamčený, model ho nesmí měnit. Po přepisu se zámek ověří a porušení = výjimka.
6. Údaj „[doplňte: …]“ ve skupině se vyplní jednou a platí pro všechny stránky skupiny.
7. Otázka na doklad („Máte certifikát Vegan?“) se ve skupině pokládá jednou. Odpověď se uloží jako doklad (viz návrh 4) a „Nie“ spustí skupinovou náhradu podle bodu 2.

**Proč ne (a) ani (c):**
- (a) dá N různých znění, N rozhodnutí a N volání modelu.
- (c) je pomalé pro obchodníka.
- (b) = jedno rozhodnutí, stejné znění všude, jedno volání modelu. Bezpečnost drží kontrola na každé stránce a výjimky.
- Úprava okolí se u skupiny záměrně nedělá. Kde je potřeba, stránka jde do jednotlivé opravy.

**UI:**
- **Na stránce opravy** má změna ze skupiny štítek „Rovnaký text na 38 stránkach“ a dvě tlačítka: „Prijať na 36 stránkach“ (hlavní) a „Len na tejto stránke“.
- **Samostatná obrazovka „Hromadná oprava“** obsahuje:
  - náhradu a jednu otázku;
  - ukázky, jak text vypadá na stránce;
  - kontrolu „36 v poriadku · 2 jednotlivo“;
  - seznam stránek s možností vyřadit.
- **V seznamu oprav** je pruh „Hromadné opravy“ a položka „Celý e-shop: šablóna“.
- Schválení skupiny jde vrátit.

## Seznam návrhů

| # | Návrh | Co přinese | Závislosti | Stav |
|---|---|---|---|---|
| 1 | **Hromadné opravy** (rozbor výše) | −73 % rozhodnutí na vzorcích; oprava na všech stránkách, ne jen na první | přepis (`Fix/`), UI | návrh UI 30. 9. |
| 1a′ | **Paměť rozhodnutí pro sledování změn** (zpřesnění uživatele 1. 10.). Opakovaný text při úvodní analýze řeší hromadná oprava, fakta slouží hlavně BUDOUCÍM změnám. Paměť se plní sama:
- schválená oprava se znovu použije na stejný text u nového produktu (bez modelu, jen kontrola Jevem);
- odpověď nebo doklad podle značky = bez otázky u nových produktů;
- rozhodnutí „ponechat“ = neptat se znovu (kromě změny zákona nebo pravidel);
- fakta podle tématu dostane model u jinak formulovaných vět.

Volitelně (standardně vypnuté) automatické zveřejnění stejné opravy s kontrolou a zápisem do protokolu. Fakta jdou v zadání hned za společnou část kvůli mezipaměti OpenAI pro účet. U každého návrhu „Použitý fakt“, připomínka k potvrzení faktů, rozsah faktu, při změně zákona překontrola. Jak často nové produkty opakují staré texty: NEMĚŘENO, ověří pilot se sledováním. | lepší návrhy u nových produktů, méně otázek, stejné formulace, důvod zůstat u předplatného | 1a, hromadné opravy, konektor s webhookem | promyšleno, UI čeká |
| 1a | **Fakta obchodu pro návrhy** (nápad uživatele 30. 9.): v nastavení fakta podle tématu, ne přesné texty (například „Obal: papierové krabice z recyklovaného papiera“). Nástroj sám najde nejčastější témata nálezů (ekologický obal, doprava, přírodní kosmetika) a vyzve k doplnění. Návrhový model dostane jen fakta k tématům dané stránky a zapracuje je do textu místo „[doplňte]“. Fakt je sám tvrzení, proto musí být pravdivý a doložitelný a ukládá se mezi doklady. **Náklady** (uživatel se bál prodělku při cenách podle stránek):
- přegenerovávat skupinově (1 volání na skupinu);
- vkládat doslova bez modelu do rámce „Obal: [údaj]“;
- jen neschválené změny a až při otevření nebo schvalování;
- zpracovat souhrnně, noční dávka Batch API −50 %;
- cache a denní strop na účet.

Odhad: 10 faktů u e-shopu s 5 000 stránkami ≈ 1,5 USD (jednotkové ceny změřené: přepis ~1 cent za stránku, kontrola ~0,03 centu). | méně otázek, konkrétnější a hezčí texty | rozpoznání témat (otázka Jevu na téma, nejdřív změřit), pokyny pro přepis, UI v nastavení / Dokladech | nápad |
| 1b | **Dvě varianty návrhu:** „S upresnením“ (zachová záměr a doplní fakt ze stránky) a „Bez environmentálneho slova“. Pokyny pro přepis upřednostní zachování záměru. | méně „drsné“ změny | pokyny pro přepis, UI | návrh UI 30. 9. |
| 1c | **Ostatní text stránky** (rozšířeno z odznaků na celou stránku). SmartReader 30. 9. na naturfyt.sk vynechal blok s odznaky a cenou. Nově se kontroluje všechno viditelné mimo hlavní text, hlavičku, patičku a navigaci a zpráva uvádí pokrytí. | nic nezmizí potichu | – | HOTOVO 30. 9. (180 testů); živé přeměření čeká na souhlas |
| 1d | **Ostatní text stránky přes síto.** Dnes jde mimo síto všemi moduly, takže horní odhad ceny vegis.sk vzrostl z 1,28 na 1,79 USD. Zařadit ho do síta jako hlavní text. | nižší cena kontroly | síto | návrh |
| 1e | **Profily šablon stránek (nápad uživatele 1. 10.).** Model z kostry vzorových stránek označí oblasti šablony a co je ovládací prvek. Profil se uloží, každá stránka se s ním porovná bez modelu (text mimo známé oblasti nad 10 % = nesedí). Nesedící stránky se seskupí podle stavby a dostanou profil své šablony. Hlavní text se nikdy nevynechává. | čistší vstup, naturfyt −5 % viditelného textu (cookie, přihlášení, košík, formuláře); kvalita na 2 obchodech beze změny | knihovna `Profiles/` | **hotovo 1. 10. 2026**, ostrý běh čeká |
| 2 | **Text v kontextu** na obrazovce opravy („… okolí … změna … okolí …“, rozkliknutí celého odstavce a stránky) | obchodník vidí, co mění | data bloků už máme | návrh UI 30. 9. |
| 3 | **Předgenerovaná varianta pro „Nie“** a nálezy k ověření v přepisu (doba hoření, certifikáty) | po „Nie“ okamžitě hotový a zkontrolovaný text | přepis, schéma odpovědi | návrh |
| 4 | **Doklad zadaný jednou platí všude** (obrazovka „Doklady“ podle značky a tvrzení, platnost, upozornění na konec platnosti) | méně otázek; doklad po ruce při kontrole SOI | úložiště, UI | návrh UI 30. 9. |
| 5 | **„Čo pomôže“ u každého nálezu:** upřesnit text / doklad / nic nepomůže, změnit text | obchodník hned ví, co dělat | texty pravidel | návrh UI 30. 9. |
| 6 | **Protokol kontroly v PDF** (data, verze pravidel, opravy, doklady) | dokumentace péče; právní účinek NEOVĚŘENO, neslibovat ochranu před pokutou | výstup, UI | návrh UI 30. 9. |
| 7 | **Otázka na doklad přeformulovat:** „Máte od výrobcu podklad (napr. protokol o skúške), alebo ho viete na požiadanie získať?“ a nápověda, že text na obalu nestačí; tlačítka „Áno, vieme doložiť“ / „Nie, nevieme doložiť“ | srozumitelnost | texty | návrh |
| 8 | **Zapnout vypnuté moduly po pilotu:** falešná naléhavost, léčebná a zdravotní tvrzení, „zdarma“ s poplatkem, zákonný požadavek jako přednost (`lr`) | větší záběr, hlavně zdravá výživa | pilot, měření | připraveno, vypnuto |
| 9 | **Sporné případy ze srovnání** (anglické „all natural“ v titulku, „clean beauty“, „…olej natural“) a poznámka k „netoxický“ podle CLP u nebezpečných směsí | méně minutí | pravidla; CLP NEOVĚŘENO v podkladech | k prověření |
| 10 | **Česko:** stáhnout zákon 159/2026 Sb. a NV 66/2026 Sb., pravidlo pro tlačítko „Odstoupit od smlouvy“ od 1. 1. 2027; EmpCo po přijetí tisku 53 | český trh | podklady | čeká |
| 11 | **Pilot, srovnávací test a advokát:** 10–20 SK e-shopů, ruční označení nálezů, test proti 3 konkurentům, posouzení pravidel | důkaz „nejlepší“ | lidé | návrh |
| 12 | **Web MVP:** úvodní stránka, bezplatná kontrola (vzorek 100 stránek), účty, platby Stripe, zpráva a opravy | prodej | architektura (ASP.NET Core, fronta na pozadí, Next.js) | návrh UI hotový |
| 13 | **Konektory v knihovně:** Shoptet API (produkty, stránky, webhooky), Upgates, WooCommerce, Shopify, produktový feed jako čtecí zdroj | zápis oprav, spolehlivé texty | přístupy k API | rešerše hotová |
| 14 | **Kontrola po uložení produktu nebo konceptu** přes webhook. Upřesněno 1. 10. 2026 rešerší konektorů: háček před uložením nemá žádná platforma, kontrola proběhne do pár minut po uložení. | chyba se chytí před zveřejněním jen u produktu uloženého jako skrytý nebo koncept (Shoptet, Upgates, WooCommerce, Shopify; BiznisWeb ne); skutečně před uložením jen náš plugin pro WooCommerce | konektory | návrh |
| 15 | **Ověření certifikátu v registru**, kde je veřejný (katalog EU Ecolabel; dostupnost NEOVĚŘENO) | méně otázek | rešerše registrů | návrh UI 30. 9. |
| 16 | **Čtení obchodních podmínek v PDF** | dnes je jen vypisujeme | knihovna pro PDF | návrh |
| 17 | **Obrázky a odznaky:** text v obrázku (OCR) nebo model s viděním | tvrzení v ikonách | cena za obrázek NEOVĚŘENO | návrh |
| 18 | **Text z dat JavaScriptových webů** (`__NEXT_DATA__`, `__NUXT__`) bez prohlížeče | levná pomoc pro nenačtené stránky | rozpoznání hotové | podle měření |
| 19 | **Další země a jazyky:** DE+AT (po posouzení RDG; Shopware, JTL), HU (UNAS, Shoprenter), PL (před 27. 9. 2027; Shoper, IdoSell), RO; pravidla po zemích s obdobím platnosti; nejdřív změřit Jev v daném jazyce | růst | rešerše hotová | návrh |
| 20 | **Doplněk na tržišti Shoptet** (instalace z tržiště, přihlášení, platby) | hlavní prodejní kanál v CZ a SK; podmínky pro partnery NEOVĚŘENO | konektor Shoptet (13) | předposlední podle uživatele |
| 21 | **Vykreslení v Chromiu na serveru** (Playwright pro .NET) jen pro stránky označené jako nenačtené a pro widgety (odpočty, recenze) | záběr u JavaScriptových webů | infrastruktura; nejdřív změřit podíl takových e-shopů | poslední podle uživatele |
