# Co úřady na e-shopech skutečně trestají a co musí být na produktové stránce

Rešerše pro EshopGuard, stav k 25. 9. 2026. Jde o podklad pro screening, ne o právní posouzení. Citace jsou doslovné ze stažených kopií. Cesty jsou relativní k `D:\_github\Overko\podklady\`. Závazné je vždy oficiální znění. Co jsem citací nedoložil, je označeno **NEOVĚŘENO**.

## Hlavní závěry

1. V akci „Internetový prodej" ČOI nejčastěji nachází chybějící informace o reklamacích (§ 13 zákona č. 634/1992 Sb.: 410 případů v roce 2024, 363 v roce 2025), o odstoupení včetně vzorového formuláře (§ 1820 odst. 1 písm. i) OZ: 255 a 240) a o ADR (§ 14: 174 a 135). Tyto tři povinnosti už pokrývá `rules/legal_cz.yaml`.
2. Další dvě velké skupiny nástroj z textu webu nezjistí. Jde o znění smlouvy a obchodních podmínek v textové podobě (§ 1827 odst. 2 OZ: 269 a 195), které přichází e-mailem, a o tlačítko „objednávka zavazující k platbě" (§ 1826a odst. 2 OZ: 221 a 107), které je až v pokladně.
3. Na produktové stránce data ukazují na tři priority:
   - Recenze bez informace, zda a jak se ověřují (§ 5a odst. 5: 58 a 91 případů, k tomu samostatné akce ČOI).
   - Slevy bez nejnižší ceny za 30 dní (§ 12a: 57 a 32 v e-shopové akci, 370 a 324 v celotržní akci Slevy). Právě tady jsou nejvyšší pokuty, v akci Slevy za rok 2025 celkem 38,7 mil. Kč.
   - Informace o ceně podle cenových předpisů (§ 12: 42 případů v roce 2025).

   Nařízení o obecné bezpečnosti výrobků (GPSR) uvádí ČOI od 1. čtvrtletí 2026 jako nejčastěji porušovaný „ostatní" předpis, ale bez čísla.
4. SOI (Slovensko) četnosti podle ustanovení u e-shopů nezveřejňuje. Má jen celotržní akce ke slevám a cenám (§ 6 a § 7 zákona č. 108/2024 Z. z.) a kontroly ověřování věku u e-shopů s tabákem.
5. Z textu produktové stránky se dá dobře zkontrolovat:
   - údaje o výrobci podle GPSR,
   - informace o ověřování recenzí,
   - zda je u slevy uvedena nejnižší cena za 30 dní a zda z ní sedí procento slevy,
   - měrná (jednotková) cena,
   - materiálové složení textilu,
   - u alkoholu a tabáku (CZ) upozornění na zákaz prodeje mladším 18 let a IČO.

   Z textu nejde zjistit, zda je uvedená nejnižší 30denní cena pravdivá, co je na obrázcích (vyobrazení výrobku, energetický a harmonizovaný štítek) ani nic z košíku.

---

## Část A: Priority podle skutečných kontrol

### A.1 Jak číst čísla

- ČOI vybírá e-shopy cíleně: „Česká obchodní inspekce se na základě prvotního screeningu zaměřuje na internetové obchody, které vykazují pravděpodobnost, že u nich dochází k nerespektování zákonných povinností jejich provozovatelů." (`coi/coi-kontroly-internet-2Q-2026.txt`). Čísla tedy ukazují, co se najde u rizikových e-shopů. Nejsou to podíly na trhu.
- Jeden případ je jedno zjištěné porušení. Jedna kontrola jich může mít víc.
- Za 4. čtvrtletí ČOI samostatnou zprávu nevydává. Mediální knihovna coi.gov.cz žádnou neobsahuje (hledal jsem přes WordPress API `wp-json/wp/v2/media`). Čtvrté čtvrtletí je jen v ročním souhrnu. Rozdíl „rok − (1Q + 2Q + 3Q)" nepočítám, protože roční čísla zahrnují i později uzavřené kontroly.
- Pomlčka „–" znamená, že zpráva za dané období číslo neuvádí. Neznamená nulu.
- Poslední zveřejněná zpráva je za 2. čtvrtletí 2026 (12. 8. 2026).
- Zkratky: ZOS je zákon č. 634/1992 Sb., o ochraně spotřebitele. OZ je zákon č. 89/2012 Sb., občanský zákoník.

### A.2 ČOI, kontrolní akce „Internetový prodej" (CZ)

| Ustanovení | Co to je | 1Q24 | 2Q24 | 3Q24 | Rok 2024 | 1Q25 | 2Q25 | 3Q25 | Rok 2025 | 1Q26 | 2Q26 | Z textu webu? |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Kontroly (z toho se zjištěním) | počet kontrol | 223 (175) | 194 (169) | 148 (125) | 1 000 (822) | 174 (151) | 125 (106) | 133 (119) | 751 (639) | 123 (97) | 103 (94) | – |
| Případy porušení celkem | všechny předpisy | – | – | – | 2 897 ¹ | – | 386 | 465 | 2 399 | 396 | 414 | – |
| ZOS celkem | zákon o ochraně spotřebitele | 252 | 264 | 171 | 1 211 | 217 | 173 | 213 | 1 002 | 158 | 159 | – |
| OZ celkem | občanský zákoník | 189 | 298 | 181 | 1 203 | 208 | 151 | 176 | 937 | 176 | 186 | – |
| Ostatní předpisy | GPSR, přístupnost, ceny, textil… | – | 74 | 74 | 483 | 76 | 62 | 76 | 460 | 62 ² | 69 | částečně |
| § 13 ZOS | informace o reklamacích (rozsah, podmínky, způsob, kde) | 93 | 84 | 52 | 410 | 80 | 63 | 73 | 363 | 67 | 61 | ano, právní stránky |
| § 4 odst. 4 + § 5 ZOS | klamavé konání (nepravdivé nebo zavádějící informace o výrobku, ceně…) | 50 | 62 | 37 | 222 | 36 | 24 | 28 | 148 | 19 | 31 | částečně |
| § 4 odst. 4 + § 5a ZOS | klamavé opomenutí, celkem včetně recenzí | 16 + 5 ³ | 11 + 9 ³ | 12 + 16 ³ | 132 | 28 | 26 | 38 | 153 | 24 | 19 | částečně |
| z toho § 5a odst. 5 | recenze bez informace, zda a jak se ověřují | 5 | 9 | 16 | 58 | 14 | 17 | 23 | 91 | 16 | 16 | ano, produktová stránka |
| § 4 odst. 4 + § 4 odst. 1 ZOS | jiné nekalé praktiky | – | – | – | – | 3 | 2 | 2 | 16 | – | – | ne |
| § 14 ZOS | ADR, subjekt mimosoudního řešení sporů | 34 | 53 | 25 | 174 | 31 | 27 | 26 | 135 | 20 | 18 | ano, právní stránky |
| § 12 ZOS | cena v souladu s cenovými předpisy | – | – | – | – | – | – | – | 42 | – | – | ano, produktová stránka (kód) |
| § 12a ZOS | sleva bez nejnižší ceny za 30 dní | 11 | – | – | 57 | – | – | – | 32 | – | – | částečně, produktová stránka |
| § 19 ZOS | vyřízení reklamace (potvrzení, lhůta) | 11 | 10 | – | – | – | – | – | – | – | – | ne |
| § 6 ZOS | diskriminace | – | – | – | 4 | – | – | – | 3 | – | – | ne |
| § 8 ZOS | výrobky porušující práva duševního vlastnictví | – | – | – | 19 | – | – | – | 19 | – | – | ne |
| § 1820 OZ celkem | informace před uzavřením smlouvy na dálku | – | – | – | 543 | 114 | 92 | 95 | 488 | 87 | 101 | částečně |
| § 1820 odst. 1 písm. i) | podmínky, lhůta a postup odstoupení + vzorový formulář | 45 | 65 | 41 | 255 | 58 | 43 | 55 | 240 | 49 | 36 | ano, právní stránky |
| § 1820 odst. 1 písm. l) | kdy spotřebitel nemá právo odstoupit nebo kdy mu zaniká | – | – | – | – | – | 13 | 7 | 47 | – | – | ano, právní stránky |
| § 1820 odst. 1 písm. h) | platba, způsob a čas dodání, vyřizování stížností | – | – | – | 63 | – | – | – | 46 | – | – | ano, právní stránky |
| § 1820 odst. 1 písm. c) | adresa sídla, telefon, e-mail | – | – | – | – | – | – | – | 46 | – | – | ano, kontakt (kód) |
| § 1820 odst. 1 písm. e) | celková cena včetně daní a náklady na dodání | – | – | – | – | – | – | – | – | – | 10 | částečně, produkt i košík |
| § 1827 odst. 2 OZ | znění smlouvy a obchodních podmínek v textové podobě | 43 | 65 | 35 | 269 ⁴ | 35 | 29 | 39 | 195 ⁴ | 33 | 32 | ne (e-mail po objednávce) |
| § 1827 odst. 1 OZ | potvrzení objednávky | – | – | 1 | 4 | – | – | 1 | 4 | – | – | ne |
| § 1826a odst. 2 OZ | tlačítko „objednávka zavazující k platbě" | 39 ⁵ | 51 | 36 | 221 | 33 | 15 | 17 | 107 | 21 | 21 | ne (pokladna) |
| zákon č. 424/2023 Sb. | přístupnost e-shopu | – | – | – | – | – | – | – | – | 6 | 10 kontrol ze 47 | ne |
| Pravomocné pokuty | počet / Kč | 247 / 4 954 500 ⁶ | 263 / 3 428 500 | 180 / 3 899 000 | 907 / 15 554 000 | 184 / 3 042 000 | 167 / 3 448 500 | 107 / 2 719 500 | 646 / 12 978 500 | 161 / 4 534 000 | 159 / 4 878 500 | – |

Poznámky k tabulce:

1. Výroční zpráva ČOI 2024 (`coi/coi-vyrocni-zprava-2024.txt`): „Počet jednotlivých případů porušení dosáhl 2 897".
2. „Nejčastěji šlo o porušení nařízení EP a Rady (EU) 2023/988 o obecné bezpečnosti výrobků, zákona o požadavcích na přístupnost některých výrobků a služeb, zákona o výrobcích s ukončenou životností či zákona o technických požadavcích na výrobky." (`coi/coi-kontroly-internet-1Q-2026.txt`). Počty zpráva neuvádí.
3. Čtvrtletní zprávy za rok 2024 uvádějí § 5a odst. 1, 2 (ve 3Q i odst. 3) a recenze (§ 5a odst. 5) zvlášť. Od ročního souhrnu 2024 je číslo celkové, recenze jsou „z toho".
4. § 1827 celkem: 273 v roce 2024 a 199 v roce 2025, z toho odst. 1 v obou letech 4.
5. Zpráva za 1Q 2024 uvádí „§ 1826 odst. 2". Popis („objednávka zavazující k platbě") odpovídá § 1826a odst. 2.
6. Z toho 196 pokut za kontroly z roku 2023.

§ 19 ZOS: 1Q 2024 se týká odst. 1, 2, 3, 2Q 2024 odst. 2, 5.

Zdroje sloupců. Vedle každého `.doc`/`.docx` leží textový výtah `.txt`.

| Sloupec | Období kontrol | Soubor | URL |
|---|---|---|---|
| 1Q24 | 2. 1.–31. 3. 2024 | `coi/coi-kontroly-internet-1Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/06/2024-06-12-internet-1Q.doc |
| 2Q24 | 1. 4.–30. 6. 2024 | `coi/coi-kontroly-internet-2Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/08/2024-08-12-kontroly-internet-2Q-2024.doc |
| 3Q24 | 1. 7.–30. 9. 2024 | `coi/coi-kontroly-internet-3Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/12/2024-12-16-kontroly-internet-3Q-2024.doc |
| Rok 2024 | 1. 1.–31. 12. 2024 | `coi/coi-kontroly-internet-rok-2024.docx`, `coi/coi-vyrocni-zprava-2024.pdf` | https://coi.gov.cz/wp-content/uploads/2025/04/2025-04-02-internetove-obchody_2024.docx |
| 1Q25 | 2. 1.–31. 3. 2025 | `coi/coi-kontroly-internet-1Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/06/2025-06-19-kontroly-internet-1Q-2025.docx |
| 2Q25 | 1. 4.–30. 6. 2025 | `coi/coi-kontroly-internet-2Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/08/2025-08-19-kontroly-internet-2Q-2025.docx |
| 3Q25 | 1. 7.–30. 9. 2025 | `coi/coi-kontroly-internet-3Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/12/2025-12-16-kontroly-internet-3Q-2025.docx |
| Rok 2025 | 2. 1.–31. 12. 2025 | `coi/coi-kontroly-internet-rok-2025.docx`, `coi/coi-vyrocni-zprava-2025.pdf` | https://coi.gov.cz/wp-content/uploads/2026/02/2026-02-25-internetovy-prodej_2025.docx |
| 1Q26 | 1. 1.–31. 3. 2026 | `coi/coi-kontroly-internet-1Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/07/2026-07-15-internetovy-prodej-1Q-2026.docx |
| 2Q26 | 1. 4.–30. 6. 2026 | `coi/coi-kontroly-internet-2Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/08/2026-08-12-internetovy-prodej_2Q.docx |

Výroční zpráva ČOI 2025 potvrzuje roční čísla: „Internetový prodej 751 639 85,1". Rozdělení porušení podle zákonů: „zákona č. 634/1992 Sb., o ochraně spotřebitele (42 % všech případů porušení)", dále „Ve 39 % všech případů porušení tvořila zjištěná porušení ustanovení občanského zákoníku" (`coi/coi-vyrocni-zprava-2025.txt`).

### A.3 Doplňkové akce ČOI

#### A.3.1 Akce „Slevy" (celá tržní síť včetně e-shopů, podíl e-shopů zprávy neuvádějí)

| Ukazatel | 1Q24 | 2Q24 | Rok 2024 | 1Q25 | 2Q25 | 3Q25 | Rok 2025 | 1Q26 | 2Q26 |
|---|---|---|---|---|---|---|---|---|---|
| Kontroly (z toho se zjištěním) | 477 (178) | 259 (106) | 1 755 (796) | 551 (324) | 221 (99) | 181 (83) | 1 432 (734) | 652 (257) | 239 (122) |
| § 12a ZOS: chybí nejnižší 30denní cena nebo je sleva špatně spočtená | 74 | 35 | 370 | 178 | 36 | 45 | 324 | 83 | 45 |
| Nekalé praktiky (§ 4, § 5, § 5a) | 37 | 17 | 207 | 132 | 63 | 32 | 368 | 112 | 60 |
| z toho § 4 odst. 4 + § 5 odst. 2 písm. d) (cena, výpočet ceny, cenová výhoda) | – | – | – | – | 29 | 20 | 140 | 85 | – |
| § 12 ZOS: cena podle cenových předpisů | 26 | 24 | 140 | – | 13 | 17 | 113 | 36 | 18 |
| § 3 ZOS: poctivost prodeje (v závorce odst. 1 písm. c), správné účtování) | 54 (41) | 29 (27) | 170 (154) | 63 (61) | – (14) | – (14) | 116 (112) | 40 | 30 (26) |
| Pokuty (počet / Kč) | 104 / 4 329 500 | 126 ⁷ / 5 784 000 | 385 / 17 561 500 ⁸ | 105 / 18 534 500 | 112 / 5 674 000 | 87 / 7 490 000 | 380 / 38 661 500 | 97 ⁷ / 9 495 500 | 124 / 23 819 500 |

Poznámky: ⁷ zpráva uvádí „sankčních opatření". ⁸ „Pokuty za porušení zjištěná v rámci mimořádných kontrolních akcí přesahující 20 milionů korun nabyly právní moci až v roce 2025" (`coi/coi-kontroly-slevy-rok-2024.txt`).

Mimořádné akce zaměřené na e-shopy:

- 14.–31. 10. 2024, obchodní řetězce a e-shopy: 250 kontrol, § 12a 91×, nekalé praktiky v oceňování 37×.
- 11. 11.–6. 12. 2024, Black Friday a Cyber Monday: 172 kontrol, 71 se zjištěním, § 12a 29×.
- 29. 10.–6. 12. 2025, Black Friday a Cyber Monday (předběžně): 108 kontrol, 38 se zjištěním, § 12a 12×, nekalé praktiky 23× (z toho v oceňování 9×).
- Předvánoční akce 2025 (předběžně): kontrolováno 39 e-shopů, z toho 19 evropským nástrojem na monitoring cen (asi 380 výrobků), a 77 prodejen. Zjištěno § 5 odst. 2 písm. d) 10×, § 5 odst. 1 6×, § 12 7×, § 12a 3× (`coi/coi-kontroly-slevove-akce-vanoce-2025.txt`).
- Povánoční monitoring 2026, výsledky ve zprávě za 2Q 2026: 26 e-shopů, nekalé praktiky 7×, § 12a 6×.

Zdroje: `coi/coi-kontroly-slevy-1Q-2024.doc`, `-2Q-2024.doc`, `-rok-2024.docx`, `-1Q-2025.docx`, `-2Q-2025.docx`, `-3Q-2025.docx`, `-rok-2025.docx`, `-1Q-2026.docx`, `-2Q-2026.docx` a `coi/coi-kontroly-slevove-akce-vanoce-2025.docx`. URL jsou v seznamu stažených souborů.

#### A.3.2 Akce „Spotřebitelské recenze" (jen e-shopy)

| Akce | Kontroly (se zjištěním) | § 5a odst. 5 | Další zjištění | Pokuty |
|---|---|---|---|---|
| 1. 7.–31. 12. 2024 | 77 (65) | 36 | 4× informace jen v obchodních podmínkách, ne u recenzí; 7× zkreslování recenzí (příloha č. 1 písm. z)); § 5 18×; § 5a odst. 1–3 9×; § 4 odst. 1 12× | 13 / 68 000 Kč, z toho za § 5a odst. 5 6 / 41 000 Kč |
| 28. 4.–30. 9. 2025 | 49 (46) | 32 (29 bez informace, 3 bez „jak") | 1× zkreslení; 1× recenze u výrobku, který recenzent nekoupil | 16 / 100 500 Kč, z toho za § 5a odst. 5 9 / 88 000 Kč |

Zdroje: `coi/coi-kontroly-recenze-rok-2024.docx`, `coi/coi-kontroly-recenze-2025.docx`.

#### A.3.3 Rok 2023 pro trend a pro „ostatní předpisy"

Zdroj: `coi/coi-kontroly-internet-rok-2023.doc`. 994 kontrol, z toho 866 se zjištěním. Zjištění:

- ZOS: § 13 384×, § 4 odst. 4 + § 5 243×, § 14 153×, § 5a 105×, § 12 56×, § 12a 40×, § 19 34×.
- OZ: § 1826a odst. 2 248×, § 1827 odst. 2 215×, § 1820 odst. 1 písm. i) 204×.
- Zákon č. 65/2017 Sb. (alkohol a tabák) 212×.
- Nařízení (EU) 2016/425 (osobní ochranné prostředky) 69×, nařízení (EU) č. 1007/2011 (textil) 62×, nařízení (EU) č. 524/2013 (ODR) 19×.
- Zákon č. 90/2016 Sb. 64×, zákon č. 22/1997 Sb. 60×, zákon č. 255/2012 Sb. § 10 odst. 2 68×.
- Pokuty: 726 za 11 636 000 Kč.

### A.4 Slovensko (SOI)

SOI nezveřejňuje četnosti porušení podle ustanovení u e-shopů. Stránka „Výsledky kontrol SOI" (5 stran, prošel jsem všechny) nemá žádnou e-shopovou akci. K dispozici je tohle:

| Zdroj | Co | Čísla |
|---|---|---|
| `soi/soi-vyrocna-sprava-2025.pdf` | všechny kontroly SOI v roce 2025 | 14 731 kontrol (z toho 2 489 na diaľku), nedostatky ve 2 174 (14,76 %) |
| tamtéž | podněty | 1 675 podnětů k nekalým praktikám, podle zprávy hlavně k e-shopům (hlavní vlastnosti, cena a její výpočet, reklamace vad) |
| `soi/soi-dohlad-zlavy-2025.pdf` (sken) a VS 2025, kap. 5.2.8 | zľavy, leden 2025, celá síť včetně některých e-shopů | 114 PJ, 41 s porušením (35,96 %); § 7: 19 ze 112 PJ (137 druhů zboží); procento z jiné než předchozí ceny: 4 PJ (26 druhů); § 6 predajná cena: 12 PJ (131 druhů); jednotková cena: 10 ze 42 PJ (98 z 853 druhů) |
| `soi/soi-dohlad-zlavy-2026.pdf` | zľavy 24. 11. 2025–23. 1. 2026 | 180 PJ, 32 s porušením (17,78 %); § 7: 23 PJ (81 druhů); procento z jiné než předchozí ceny: 8 PJ (31 druhů); § 6 predajná cena: 8 PJ (78 druhů); jednotková cena: 3 z 94 PJ (31 z 3 279 druhů) |
| VS 2024 a VS 2025 | e-shopy s tabákem (funkční ověřování věku) | 2023: 54 kontrol, 11 porušení; 2024: 41 kontrol, 13 porušení (31,71 %); 2025: 35 kontrol, 10 porušení (28,57 %) |

PJ je prevádzková jednotka (provozovna). Všechny paragrafy v tabulce jsou ze zákona č. 108/2024 Z. z.

### A.5 Top 10 nejčastějších porušení

Součet let 2024 a 2025 v akci Internetový prodej, jen ustanovení s uvedeným počtem:

1. § 13 ZOS, informace o reklamacích: **773** (410 + 363).
2. § 1820 odst. 1 písm. i) OZ, odstoupení a vzorový formulář: **495** (255 + 240).
3. § 1827 odst. 2 OZ, smlouva a obchodní podmínky v textové podobě: **464** (269 + 195).
4. § 4 odst. 4 + § 5 ZOS, klamavé konání: **370** (222 + 148).
5. § 1826a odst. 2 OZ, tlačítko „objednávka zavazující k platbě": **328** (221 + 107).
6. § 14 ZOS, ADR: **309** (174 + 135).
7. § 4 odst. 4 + § 5a ZOS, klamavé opomenutí: **285** (132 + 153). Z toho recenze (§ 5a odst. 5) **149** (58 + 91).
8. § 1820 odst. 1 písm. h) OZ, platba, dodání a stížnosti: **109** (63 + 46).
9. § 12a ZOS, sleva bez nejnižší 30denní ceny: **89** (57 + 32). V celotržní akci Slevy navíc **694** (370 + 324).
10. § 1820 odst. 1 písm. l) OZ, kdy nelze odstoupit: **47** (údaj jen za rok 2025). Těsně za ním jsou § 1820 odst. 1 písm. c) OZ (kontakty) se **46** a § 12 ZOS (cena) se **42**, obojí jen za rok 2025.

§ 1820 OZ jako celek (součet všech písmen) má 543 + 488 = 1 031 případů.

### A.6 Seřazené priority pro nástroj

Pořadí zohledňuje četnost u ČOI, kontrolovatelnost z textu a výši sankcí. Stránky jsou „právní" (`site_presence`) nebo „produktové" (`page_presence`, část B).

| # | Oblast | Ustanovení CZ (SK) | Četnost (2024 + 2025, pokud není uvedeno jinak) | Kde | Kontrola | Stav v nástroji |
|---|---|---|---|---|---|---|
| 1 | Reklamace | § 13 ZOS | 773 | právní stránky | jev | `legal_cz: legal_complaints` |
| 2 | Odstoupení a vzorový formulář | § 1820 odst. 1 písm. i) OZ (SK § 15 ods. 1 písm. f)) | 495 | právní stránky | jev + kód (14 dní) | `legal_withdrawal`, `legal_withdrawal_form` |
| 3 | ADR | § 14 ZOS, § 1820 odst. 1 písm. s) OZ | 309 | právní stránky | jev | `legal_adr` |
| 4 | Recenze: informace o ověřování přímo u recenzí | § 5a odst. 5 ZOS (SK § 11 ods. 6 písm. a)) | 149, k tomu 68 v samostatných akcích | produktová | jev + kód | **nové**, B.8 |
| 5 | Sleva: nejnižší 30denní cena a výpočet procenta | § 12a ZOS (SK § 7) | 89, v akci Slevy 694; pokuty 38,7 mil. Kč (2025) | produktová | kód + jev | **nové**, B.5 |
| 6 | GPSR čl. 19: výrobce, odpovědná osoba, typ, varování | nařízení (EU) 2023/988 | bez čísla; 1Q26 „nejčastěji" z ostatních předpisů; CZ pokuta až 5 mil. Kč | produktová | jev + kód | **nové**, B.1–B.4 |
| 7 | Cena: prodejní včetně daní, měrná (jednotková) cena | § 12 ZOS a § 13 zákona o cenách (SK § 6) | 42 (2025), v akci Slevy 253; SOI jednotková cena 10 ze 42 PJ | produktová | kód | **nové**, B.6 a B.7 |
| 8 | Další předsmluvní údaje | § 1820 odst. 1 písm. c), h), l), e) OZ | 46 / 109 / 47 / 10 (2Q26) | kontakt, doprava, právní stránky; písm. e) produkt | jev + kód | návrh rozšířit `legal_cz`; písm. e) v B.7 |
| 9 | Náhradní cenová tvrzení („TOP CENA", „Ušetřete…") | § 4 odst. 4 + § 5 odst. 2 písm. d) ZOS | 140 (Slevy 2025), 85 (1Q26) | produktová | jev, jen „k ověření" | **nové**, doplněk B.5 |
| 10 | Textil: materiálové složení | čl. 16 nařízení (EU) č. 1007/2011 | 62 (2023) | produktová (oděvy) | jev + kód | **nové**, B.10 |
| 11 | Alkohol, tabák: zákaz prodeje mladším 18 let a IČO v místě nabídky | § 6 a § 15 zákona č. 65/2017 Sb. | 212 (2023, všechny povinnosti zákona) | produktová | kód + jev | **nové**, B.11 |
| 12 | Klamavé konání obecně | § 4 odst. 4 + § 5 ZOS | 370 | kdekoli | jev, nízká přesnost | jen cílené vzory (eko modul) |
| – | Mimo dosah nástroje | § 1827 odst. 2 (464), § 1826a odst. 2 (328), § 1827 odst. 1 (8), § 19, § 8 (38), § 6 (7), přístupnost | – | e-mail, pokladna, proces | nelze z textu | – |

Pro Slovensko podporují data SOI hlavně body 5 a 7 (§ 7 a § 6 zákona č. 108/2024 Z. z.). Ostatní pořadí přebírám z Česka, protože povinnosti vycházejí ze stejných směrnic EU.

### A.7 Doslovné doklady k části A

Citace jsou z `coi/coi-kontroly-internet-rok-2025.txt`, pokud není uvedeno jinak.

- § 13: „Nejčastějším zjištěním bylo ve 363 případech porušení § 13, který stanoví prodávajícímu povinnost informovat spotřebitele o rozsahu, podmínkách a způsobu uplatnění práva z vadného plnění, společně s údaji o tom, kde lze reklamaci uplatnit."
- § 1820 písm. i): „Nejčastěji bylo zjištěno porušení ustanovení § 1820 (488 případů), […] z toho ve 240 případech bylo zjištěno, že spotřebiteli nebyly sděleny podmínky, lhůty a postup pro uplatnění práva na odstoupení od smlouvy, jakož i vzorový formulář pro odstoupení od smlouvy dle odst. 1 písm. i)."
- § 1827 odst. 2: „Z toho konkrétně ve 195 případech bylo zjištěno, že prodávající neposkytli spotřebiteli znění smlouvy a všeobecných podmínek v textové podobě při použití elektronických prostředků při uzavírání smlouvy dle ustanovení § 1827 odst. 2."
- § 5: „Jednalo se především o porušení § 4 odst. 4 v návaznosti na § 5 odst. 1, 2, 3 (148 případů)".
- § 1826a: „Třetím nejvíce porušovaným ustanovením občanského zákoníku bylo ustanovení § 1826a odst. 2, […] Toto porušení bylo zjištěno celkem ve 107 případech."
- § 14: „Třetím nejvíce porušovaným ustanovením zákona o ochraně spotřebitele byl § 14 odst. 1, 2 (135 případů)".
- Recenze: „Z toho 91 případů se týkalo spotřebitelských recenzí podle ust. § 4 odst. 4 v návaznosti na § 5a odst. 5".
- § 1820 písm. c), h), l): „Další časté porušení (47 případů) se týkalo odst. 1 písm. l) […] Shodně dále bylo zjištěno porušení odst. 1 písm. c) a h), a to ve 46 případech."
- § 12 a § 12a: „Porušení týkající se informace o ceně při prodeji spotřebního zboží konečnému spotřebiteli bylo zjištěno ve 42 případech (§ 12). Dále ve 32 případech bylo zjištěno porušení týkající se informace o slevě z ceny výrobku (§ 12a)."
- § 1820 písm. e), `coi/coi-kontroly-internet-2Q-2026.txt`: „Další časté porušení (10 případů) se týkalo § 1820 odst. 1 písm. e), který se vztahuje k povinnosti informovat předem spotřebitele o celkové ceně zboží nebo služby včetně všech souvisejících daní a poplatků a nákladech na dodání".
- Slevy 2025, `coi/coi-kontroly-slevy-rok-2025.txt`: „Tato povinnost vztahující se k oznámení o slevě byla porušena celkem ve 324 případech." a „nabylo v období od 2. 1. 2025 do 31. 12. 2025 právní moci celkem 380 pokut v celkové hodnotě 38 661 500 Kč."
- Náhradní výrazy, tamtéž: „jako například: „Ušetřete 50 %", s nabídkami typu „SUPER cena", „cena s aplikací", „doporučená cena od výrobce." Tímto způsobem se snaží obcházet povinnost informovat spotřebitele o nejnižší ceně za posledních 30 dnů".
- Recenze jen v obchodních podmínkách, `coi/coi-kontroly-recenze-rok-2024.txt`: „Informace nebyla poskytnuta přímo u zveřejňovaných recenzí, ale pouze ve všeobecných smluvních podmínkách."
- Rok 2023, `coi/coi-kontroly-internet-rok-2023.txt`: „zákona č. 65/2017 Sb., o ochraně zdraví před škodlivými účinky návykových látek (celkem 212 případů)" a „nařízení EP a Rady (EU) č. 1007/2011 o názvech textilních vláken a souvisejícím označování materiálového složení textilních výrobků (62 případů)".
- SOI, jednotková cena, `soi/soi-vyrocna-sprava-2025.txt`: „Nedostatky v označovaní tovaru jednotkovou cenou boli zistené v 10 PJ (23,81 % z počtu 42 prekontrolovaných PJ) u 98 druhov tovaru (11,49 % z počtu 853 prekontrolovaných druhov tovaru)."

---

## Část B: Povinné údaje na produktové stránce

### B.0 Společná pravidla pro všechny povinnosti

**Rozsah.** Pravidla platí pro produktové stránky podle zadání, tedy stránky s JSON-LD `Product` nebo `og:type=product`.

**Skládání `page_presence`.** Jde o návrh, v kódu zatím není.
- Pro každou produktovou stránku se vezme maximum pravděpodobnosti otázky přes segmenty té stránky.
- Údaj je přítomen, když maximum dosáhne `presence_threshold` (0,7, stejně jako u `site_presence`).
- Jinak vznikne nález pro tuto stránku se skóre 1 − maximum, nejméně v pásmu „k ověření".
- Zpráva má souhrn „údaj chybí na N z M produktových stránek, na kterých povinnost platí" a seznam URL.

**`applies_if`.** Podmínka, kdy povinnost na stránce platí, například kategorie výrobku, zobrazené recenze nebo oznámená sleva.
- Vyhodnocuje se maximem přes segmenty stránky s prahem 0,5, nebo kódem.
- Když podmínka nevyjde, pravidlo se na stránce nevyhodnocuje.
- Když vyjde nejistě (0,5–0,7), nález je nejvýš „k ověření".

**Záhlaví výrobku.** Kód z něj sestaví jeden virtuální segment: název (h1), drobečková navigace, kategorie z JSON-LD a prvních asi 300 znaků popisu. Na tento segment se kladou otázky na kategorii, tedy jedno volání Jevu na otázku a stránku.

**Serializace textu pro Jev.**
- Přeškrtnutý text (`<del>`, `<s>`, `<strike>`, CSS `line-through`) převést na „[přeškrtnuto: 1 299 Kč]". Jinak Jev přeškrtnutou cenu nepozná.
- Tabulky parametrů převést na segmenty „Štítek: hodnota".
- Obsah záložek (tabů) zahrnout.

**JSON-LD.** Spotřebitel ho nevidí. Je to jen pomocný signál, nikdy důkaz, že údaj je uveden „jasně a viditelně".

**Čísla.** Ceny, procenta, roky, EAN, IČO a množství zjišťuje vždy kód. Jev rozhoduje jen, zda údaj v textu je a k čemu se vztahuje.

**Co nástroj nevidí:**
- obrázky (Jev je nevidí),
- košík a pokladnu,
- obsah načítaný až JavaScriptem, pokud crawler nerenderuje,
- odkazovaná PDF.

**Otázky na kategorii výrobku.** Ptají se na záhlaví výrobku. Všechny jsou ano/ne a když text výrobek nezmiňuje, musí vyjít „ne".

| id | EN (pro Jev) | CS | Použití |
|---|---|---|---|
| `pp_gpsr_excluded_category` | Is the product offered in this text a food, a drink, a dietary supplement, a medicine, animal feed, a living plant or animal, a plant protection product or an antique? | Je výrobek nabízený v tomto textu potravina, nápoj, doplněk stravy, léčivo, krmivo, živá rostlina nebo zvíře, přípravek na ochranu rostlin nebo starožitnost? | vyloučení z GPSR (B.1–B.4) |
| `pp_is_packaged_food` | Is the product offered in this text a packaged food or drink, such as groceries, bottled drinks, coffee, sweets or a dietary supplement? | Je výrobek nabízený v tomto textu balená potravina nebo nápoj, například potraviny, nápoje v lahvích, káva, sladkosti nebo doplněk stravy? | měrná cena (B.6) |
| `pp_is_unit_price_nonfood_cz` | Is the product offered in this text one of these packaged non-food products: paint, varnish, glue or solvent; dry building mix, tiles, wallpaper, insulation or floor covering; a cleaning, disinfecting, washing or laundry product; a skin-care, hair-care, oral-care, bath or shaving product, deodorant or perfume; toilet paper, tissues, sanitary pads, tampons or nappies; pet food, pet treats or pet litter; or a non-food oil or car fluid? | Je výrobek nabízený v tomto textu jeden z těchto balených nepotravinářských výrobků: barva, lak, lepidlo nebo rozpouštědlo; suchá stavební směs, dlaždice, tapeta, izolace nebo podlahová krytina; čisticí, dezinfekční, mycí nebo prací prostředek; přípravek k péči o pleť, vlasy nebo ústa, do koupele či na holení, deodorant nebo parfém; toaletní papír, kapesníky, vložky, tampony nebo pleny; krmivo, pamlsky nebo stelivo pro zvířata; nepotravinářský olej nebo kapalina do auta? | měrná cena CZ (B.6); seznam podle přílohy vyhlášky č. 291/2024 Sb. Pro přesnost lze rozdělit na víc otázek |
| `pp_is_multi_product_set` | Is the product offered in this text a set of different products sold together in one package for a single price? | Je výrobek nabízený v tomto textu sada různých výrobků prodávaných v jednom balení za jednu cenu? | výjimka z měrné ceny (B.6) |
| `pp_is_textile` | Is the product offered in this text a textile product, such as clothing, underwear, bed linen, towels, curtains or fabric sold by the metre? | Je výrobek nabízený v tomto textu textilní výrobek, například oblečení, spodní prádlo, ložní prádlo, ručníky, záclony nebo metrový textil? | B.10 |
| `pp_is_alcohol` | Is the product offered in this text an alcoholic drink? | Je výrobek nabízený v tomto textu alkoholický nápoj? | B.11 |
| `pp_is_tobacco_nicotine` | Is the product offered in this text a tobacco product, a smoking accessory, a herbal product for smoking, an electronic cigarette, a nicotine pouch or another product containing nicotine? | Je výrobek nabízený v tomto textu tabákový výrobek, kuřácká pomůcka, bylinný výrobek určený ke kouření, elektronická cigareta, nikotinový sáček nebo jiný výrobek obsahující nikotin? | B.11 |
| `pp_is_toy` | Is the product offered in this text a toy or a product intended for children to play with? | Je výrobek nabízený v tomto textu hračka nebo výrobek určený ke hře dětí? | B.4 (k ověření) |
| `pp_is_household_chemical` | Is the product offered in this text a cleaning, washing, disinfecting or other household chemical product? | Je výrobek nabízený v tomto textu čisticí, prací, dezinfekční nebo jiný chemický výrobek pro domácnost? | B.4 (k ověření) |

---

### B.1 GPSR: údaje o výrobci

- **id:** `pp_gpsr_manufacturer`
- **Ustanovení:** čl. 19 písm. a) nařízení (EU) 2023/988 (GPSR), použitelné od 13. 12. 2024 (čl. 52). Sankce: v CZ § 9 odst. 2 písm. a) a odst. 3 písm. b) zákona č. 387/2024 Sb., v SK § 9 ods. 7 písm. b) zákona č. 83/2025 Z. z.
- **Citace:**
  - „Pokud hospodářské subjekty dodávají výrobky na trh online nebo prostřednictvím jiných prostředků prodeje na dálku, musí nabídka těchto výrobků jasně a viditelně uvádět alespoň tyto údaje: a) jméno, zapsaný obchodní název nebo zapsanou ochrannou známku výrobce a poštovní a elektronickou adresu, na kterých lze výrobce kontaktovat;" (`predpisy-eu/eu-2023-988-gpsr-cs.txt`, čl. 19)
  - SK znění: „Ak hospodárske subjekty sprístupňujú výrobky na trhu online alebo prostredníctvom iných prostriedkov predaja na diaľku, v príslušnej ponuke týchto výrobkov sa jasne a viditeľne uvádzajú aspoň tieto informácie:" (`predpisy-eu/eu-2023-988-gpsr-bezpecnost-vyrobkov-sk.txt`, čl. 19)
  - „Elektronickou adresou může být e-mailová adresa nebo vyhrazená část vašich internetových stránek, která spotřebitelům umožní vás přímo a snadno kontaktovat. Internetové stránky samy o sobě nestačí, pokud neumožňují přímou komunikaci s vámi." (`eu-komise/komise-gpsr-pokyny-2025-6233-cs.txt`, sdělení Komise C/2025/6233)
  - Tamtéž: „musí nabídka těchto výrobků (např. nabídka výrobků ve vašem e-shopu) jasně a viditelně uvádět alespoň tyto údaje".
  - Rozsah: „2. Toto nařízení se nepoužije na: a) humánní nebo veterinární léčivé přípravky; b) potraviny; c) krmiva; d) živé rostliny a živočichy, […] f) přípravky na ochranu rostlin; […] i) starožitnosti." (GPSR čl. 2 odst. 2)
  - Harmonizované výrobky: pokyny uvádějí „Pokud jde o výrobky, které již podléhají zvláštním požadavkům uloženým harmonizačními právními předpisy EU:", a mezi použitelnými kapitolami „kapitola III oddíl 2 – povinnosti hospodářských subjektů v případě prodeje na dálku, oznamování nehod souvisejících s výrobky a ustanovení o informacích v elektronické podobě". Článek 19 je v kapitole III oddílu 2; výjimka v čl. 2 odst. 1 se týká „kapitola III oddíl 1".
  - CZ: „(2) Hospodářský subjekt dodávající výrobky na trh prostřednictvím prostředků komunikace na dálku se dopustí přestupku tím, že v nabídce výrobku neuvede jasně a viditelně a) údaje podle čl. 19 písm. a) nebo b) nařízení o obecné bezpečnosti výrobků," a pokuta „b) 5000000 Kč, jde-li o přestupek podle odstavce 2." (`predpisy-cz/cz-387-2024-zakon-o-obecne-bezpecnosti-vyrobku.txt`, § 9)
  - SK: „b) čl. 15 a čl. 19 písm. a) až c) nariadenia (EÚ) 2023/988 pokutu od 50 eur do 10 000 eur," (`predpisy-sk/sk-83-2025-vseobecna-bezpecnost-vyrobkov-zneni-od-2025-05-01.txt`, § 9 ods. 7)
- **Požadavek lidsky:** Na každé produktové stránce musí být vidět, kdo výrobek vyrobil (jméno, obchodní firma nebo ochranná známka) a jak ho kontaktovat: poštovní adresa a e-mail nebo webový kontakt, přes který lze výrobce přímo oslovit. Neplatí pro potraviny, léčiva, krmiva, živé rostliny a zvířata, přípravky na ochranu rostlin a starožitnosti. Platí i pro hračky, elektro, kosmetiku a další výrobky s vlastní harmonizovanou legislativou.
- **Kontrola:**
  - Jev rozhoduje, zda text uvádí výrobce, jeho adresu a elektronický kontakt, a hlavně zda se údaj vztahuje k výrobci, a ne k e-shopu.
  - Kód najde e-mail nebo URL v segmentu, kde Jev odpověděl ano. JSON-LD `manufacturer` a `brand` slouží jen pomocně.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_manufacturer_named` | Does this text identify the manufacturer of the product by a company name, trade name or trademark (for example "Manufacturer: …")? | Uvádí tento text výrobce výrobku jeho názvem, obchodní firmou nebo ochrannou známkou (například „Výrobce: …")? |
| `pp_manufacturer_postal_address` | Does this text give a postal address (for example street, town and postcode) that is presented as the address of the product's manufacturer? | Uvádí tento text poštovní adresu (například ulici, město a PSČ), která je uvedena jako adresa výrobce výrobku? |
| `pp_manufacturer_electronic_contact` | Does this text give an e-mail address or a web contact page that is presented as a way to contact the product's manufacturer? | Uvádí tento text e-mailovou adresu nebo webovou kontaktní stránku, která je uvedena jako kontakt na výrobce výrobku? |

- **Kód:**

```text
e-mail:          (?i)[\p{L}\p{N}._%+-]+@[\p{L}\p{N}-]+(?:\.[\p{L}\p{N}-]+)+
web:             (?i)\b(?:https?://|www\.)[^\s<>"']+
blok výrobce:    (?i)\b(výrobce|výrobca|vyrobeno|vyrobil|manufacturer|producent|dovozce|dovozca|importér|odpovědná osoba|zodpovedná osoba|responsible person)\b
PSČ (jen podpůrně, koliduje s cenami): \b\d{3}[\s\u00A0]?\d{2}\b
```

  Pravidlo: elektronický kontakt je přítomen, když Jev u segmentu odpoví ano a kód v tomtéž nebo sousedním segmentu najde e-mail nebo URL. Pokud kód najde odkaz s textem jako „Informace o výrobci" nebo „bezpečnost výrobku", dostane nález poznámku „údaje mohou být za odkazem", viz poznámky.
- **Skládání:** `page_presence` pro každou ze tří otázek zvlášť, s podmínkou `applies_if: pp_gpsr_excluded_category < 0,5`. Nález uvede, která složka chybí. Zpráva ukáže souhrn za web.
- **Závažnost:** high.
- **Poznámky:**
  - U vlastních značek je výrobcem prodejce. Adresa v patičce e-shopu otázku nesplní, protože otázka se ptá na adresu „uvedenou jako adresa výrobce". Takový nález má být „k ověření".
  - Pokud jsou údaje na jiné stránce za odkazem, nevím, zda to splňuje „jasně a viditelně v nabídce". Pokyny Komise to v části k čl. 19 neřeší, takže **NEOVĚŘENO**. Doporučuji pásmo nejvýš „k ověření".
  - Záložky načítané JavaScriptem crawler bez renderování nevidí, a vznikne falešný nález. Kód by měl poznat prázdnou záložku a přidat poznámku.

### B.2 GPSR: odpovědná osoba v EU

- **id:** `pp_gpsr_eu_responsible_person`
- **Ustanovení:** čl. 19 písm. b) a čl. 16 odst. 1 GPSR. Sankce stejné jako v B.1.
- **Citace:**
  - „b) pokud výrobce není usazen v Unii, jméno a poštovní a elektronickou adresu odpovědné osoby ve smyslu čl. 16 odst. 1 tohoto nařízení nebo čl. 4 odst. 1 nařízení (EU) 2019/1020;" (GPSR čl. 19)
  - „1. Výrobek, na nějž se vztahuje toto nařízení, nelze uvádět na trh, pokud neexistuje hospodářský subjekt usazený v Unii, který odpovídá za plnění úkolů stanovených v čl. 4 odst. 3 nařízení (EU) 2019/1020 ve vztahu k tomuto výrobku." (GPSR čl. 16 odst. 1)
  - „Osobou odpovědnou za výrobek může být dovozce, vámi pověřený zplnomocněný zástupce nebo poskytovatel služeb kompletního vyřízení objednávek." (`eu-komise/komise-gpsr-pokyny-2025-6233-cs.txt`)
- **Požadavek lidsky:** Když výrobce sídlí mimo EU, musí nabídka uvést i jméno, poštovní a elektronickou adresu odpovědné osoby v EU. Tou může být dovozce, zplnomocněný zástupce nebo poskytovatel fulfillmentu.
- **Kontrola:**
  - Podmínku určí kód podle země v adrese výrobce. Záložní variantou je otázka Jevu `pp_manufacturer_outside_eu`.
  - Přítomnost údajů: Jev, e-mail potvrdí kód jako v B.1.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_manufacturer_outside_eu` (podmínka) | Does this text state that the product's manufacturer is based in a country outside the European Union (for example China, the United States, the United Kingdom, Switzerland or Turkey)? | Uvádí tento text, že výrobce výrobku sídlí v zemi mimo Evropskou unii (například v Číně, USA, Spojeném království, Švýcarsku nebo Turecku)? |
| `pp_eu_rp_named` | Does this text name a company or person presented as responsible for the product in the EU, such as an "EU responsible person", an importer or an authorised representative? | Uvádí tento text firmu nebo osobu označenou jako odpovědná za výrobek v EU, například „odpovědnou osobu v EU", dovozce nebo zplnomocněného zástupce? |
| `pp_eu_rp_postal_address` | Does this text give a postal address presented as the address of the product's EU responsible person, importer or authorised representative? | Uvádí tento text poštovní adresu uvedenou jako adresa odpovědné osoby v EU, dovozce nebo zplnomocněného zástupce pro tento výrobek? |
| `pp_eu_rp_electronic_contact` | Does this text give an e-mail address or a web contact page presented as the contact of the product's EU responsible person, importer or authorised representative? | Uvádí tento text e-mailovou adresu nebo webovou kontaktní stránku uvedenou jako kontakt na odpovědnou osobu v EU, dovozce nebo zplnomocněného zástupce pro tento výrobek? |

- **Kód:** země mimo EU v adrese výrobce.

```text
(?i)\b(Čín[aěuy]|China|PRC|Hong[\s-]?Kong|Tchaj-?wan|Taiwan|USA|U\.S\.A\.|Spojené státy|United States|Spojené království|Velk[áé] Británi[ei]|United Kingdom|Great Britain|Turecko|Turkey|Türkiye|Švýcarsko|Švajčiarsko|Switzerland|Indie|India|Japonsko|Japan|Korea|Vietnam|Ukrajina|Ukraine|Srbsko|Serbia)\b
```

- **Skládání:** `page_presence` pro tři otázky o odpovědné osobě, s podmínkou `applies_if: výrobce mimo EU` (kód, nebo Jev ≥ 0,5). Když adresa výrobce chybí, podmínku nelze určit. Nález B.1 pak dostane poznámku „nelze ověřit, zda je potřeba odpovědná osoba".
- **Závažnost:** high, ale jen když podmínka platí.
- **Poznámky:** Zda se Norsko, Island a Lichtenštejnsko (EHP) považují pro GPSR za „usazené v Unii", je **NEOVĚŘENO**. Proto nejsou v seznamu zemí mimo EU.

### B.3 GPSR: identifikace výrobku

- **id:** `pp_gpsr_product_identification`
- **Ustanovení:** čl. 19 písm. c) GPSR a bod 42 odůvodnění. V CZ § 9 odst. 2 písm. b) zákona č. 387/2024 Sb., v SK § 9 ods. 7 písm. b) zákona č. 83/2025 Z. z.
- **Citace:**
  - „c) údaje umožňující identifikaci výrobku, včetně jeho vyobrazení a typu a případných dalších identifikátorů výrobku, a" (GPSR čl. 19)
  - „Za vyobrazení by měla být považována fotografie, ilustrace nebo jiný obrazový prvek, který umožňuje snadnou identifikaci výrobku nebo potenciálního výrobku." (GPSR, bod 42 odůvodnění)
- **Požadavek lidsky:** Nabídka musí obsahovat obrázek výrobku a jeho typ (model, typové označení). Pokud existují další identifikátory, jako EAN nebo číslo šarže, patří tam také.
- **Kontrola:**
  - Vyobrazení kontroluje jen kód, a to přítomnost produktového obrázku (`<img>` v galerii, `og:image`, JSON-LD `image`). Obsah obrázku z textu zjistit nejde, protože Jev obrázky nevidí.
  - Typ a identifikátor: kód (EAN/GTIN s kontrolním součtem, štítky, JSON-LD `gtin*`, `mpn`, `model`), Jev jen pro volné formulace.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_product_type_identifier` | Does this text state a type, a model name or number, an EAN code or another code that identifies this particular product? | Uvádí tento text typ, název nebo číslo modelu, kód EAN nebo jiný kód, který identifikuje tento konkrétní výrobek? |

- **Kód:**

```text
GTIN/EAN:  \b(\d{8}|\d{12,14})\b  a kontrolní součet GS1 (mod 10)
štítek:    (?i)\b(EAN|GTIN|kód výrobku|kód produktu|katalogové číslo|objednací (?:kód|číslo)|typ(?:ové označení)?|model|označen[íi]e? modelu|MPN|part\s?number|číslo šarže|šarže|sériové číslo)\b\s*[:#]?\s*\S+
obrázek:   JSON-LD Product.image, og:image nebo <img> v hlavní oblasti produktu (bez loga a ikon)
```

- **Skládání:** `page_presence` s podmínkou `applies_if: pp_gpsr_excluded_category < 0,5`.
- **Závažnost:** medium.
- **Poznámky:** Interní kód e-shopu (SKU) nemusí být typem výrobce. Pokud je na stránce jen SKU, nález má být „k ověření". Samotný název výrobku bez typu vede k nálezu.

### B.4 GPSR: varování a bezpečnostní informace

- **id:** `pp_gpsr_safety_information`
- **Ustanovení:** čl. 19 písm. d) GPSR. V CZ § 8 odst. 2 a § 9 odst. 2 písm. c) zákona č. 387/2024 Sb. V SK § 2 ods. 6 a § 9 ods. 7 písm. c) zákona č. 83/2025 Z. z.
- **Citace:**
  - „d) případné varovné nebo bezpečnostní informace, které mají být k výrobku či jeho obalu připojeny nebo uvedeny v průvodním dokumentu k němu v souladu s tímto nařízením nebo příslušnými harmonizačními právními předpisy Unie, v jazyce, který je spotřebitelům snadno srozumitelný […]" (GPSR čl. 19)
  - CZ: „(2) Hospodářský subjekt dodávající výrobky na trh prostřednictvím prostředků komunikace na dálku je povinen uvést varování a bezpečnostní informace podle čl. 19 písm. d) nařízení o obecné bezpečnosti výrobků v českém jazyce." (`predpisy-cz/cz-387-2024-zakon-o-obecne-bezpecnosti-vyrobku.txt`, § 8)
  - SK: „Hospodársky subjekt, ktorý sprístupňuje výrobok na trhu prostredníctvom prostriedkov predaja na diaľku, uvedie v ponuke výrobku upozornenia a bezpečnostné pokyny podľa osobitného predpisu v slovenskom jazyku." (`predpisy-sk/sk-83-2025-...txt`, § 2 ods. 6; vynechán odkaz na poznámku)
- **Požadavek lidsky:** Pokud výrobek musí mít varování nebo bezpečnostní informace na sobě, na obalu nebo v průvodní dokumentaci, musí být uvedeny i v nabídce. V CZ česky, v SK slovensky.
- **Kontrola:**
  - Zda je varování u konkrétního výrobku povinné, z textu zjistit nejde. Záleží na etiketě a na konkrétních předpisech.
  - Z textu jde zjistit, zda nějaké varování stránka obsahuje (Jev) a v jakém je jazyce (kód).
  - Proto je to jen pravidlo „k ověření" pro kategorie, kde varování typicky bývají.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_safety_information` | Does this text contain a warning or safety information about the product, such as an age limit, a hazard warning, a precaution or instructions for safe use? | Obsahuje tento text varování nebo bezpečnostní informaci o výrobku, například věkové omezení, upozornění na nebezpečí, bezpečnostní opatření nebo pokyny k bezpečnému použití? |

- **Kód:** rozpoznání jazyka segmentu s varováním (cs pro CZ, sk pro SK) a klíčová slova.

```text
(?i)\b(varování|upozornění|pozor|nebezpečí|nevhodné pro děti|pro děti do \d+|nepoužívejte|uchovávejte mimo dosah|bezpečnostní (?:pokyny|informace)|výstraha|upozornenie|nebezpečenstvo|nevhodné pre deti|uchovávajte mimo dosahu|bezpečnostné pokyny)\b
```

- **Skládání:** `page_presence` s podmínkou `applies_if: pp_is_toy ≥ 0,5 OR pp_is_household_chemical ≥ 0,5`, pásmo nejvýš „k ověření". Samostatný nález „varování není v jazyce trhu" vznikne, když kód najde varování jen v cizím jazyce.
- **Závažnost:** medium.
- **Poznámky:** Konkrétní povinná varování u hraček (směrnice 2009/48/ES) a u nebezpečných směsí (nařízení CLP, reklama při prodeji na dálku) jsou **NEOVĚŘENO**. Předpisy jsem nestahoval. Jsou to kandidáti na samostatné moduly.

### B.5 Sleva: předchozí (nejnižší 30denní) cena a výpočet slevy

- **id:** `pp_price_reduction_prior_price`, doplněk `pp_price_advantage_wording`
- **Ustanovení:** CZ § 12a ZOS. SK § 7 zákona č. 108/2024 Z. z. EU čl. 6a směrnice 98/6/ES ve znění směrnice (EU) 2019/2161.
- **Citace:**
  - CZ: „(1) Informace o slevě z ceny výrobku obsahuje informaci o nejnižší ceně výrobku, za kterou jej prodávající nabízel a prodával a) v době 30 dnů před poskytnutím slevy, b) od okamžiku, kdy začal výrobek nabízet a prodávat, do okamžiku poskytnutí slevy, pokud je výrobek v prodeji dobu kratší než 30 dnů, nebo c) v době 30 dnů před prvním poskytnutím slevy, zvyšuje-li prodávající slevu z ceny postupně." (`predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt`, § 12a)
  - CZ: „(2) Odstavec 1 se nepoužije pro výrobky, které podléhají rychlé zkáze, nebo pro výrobky s krátkou dobou spotřeby." (tamtéž)
  - SK: „(1) Obchodník je povinný v každom oznámení o znížení ceny tovaru uviesť predchádzajúcu cenu tovaru. Obchodník je povinný určiť zníženie ceny tovaru na základe predchádzajúcej ceny tovaru." (`predpisy-sk/sk-108-2024-ochrana-spotrebitela-zneni-od-2026-09-27.txt`, § 7)
  - SK: „(2) Predchádzajúca cena tovaru je najnižšia cena, za ktorú obchodník predával alebo poskytoval tovar a) v období 30 dní pred znížením ceny tovaru, alebo b) od začiatku predaja alebo poskytovania tovaru, ak obchodník predával alebo poskytoval tovar v období kratšom ako 30 dní pred znížením ceny." (tamtéž; ods. 4 vylučuje tovar podliehajúci rýchlemu zníženiu kvality alebo skaze)
  - EU: „1. Veškerá oznámení o slevě z ceny musí uvádět předchozí cenu, kterou obchodník uplatňoval po určité období před uplatněním slevy z ceny. 2. Předchozí cenou se rozumí nejnižší cena, kterou obchodník uplatňoval během období ne kratšího než 30 dnů před uplatněním slevy z ceny." (`predpisy-eu/eu-1998-6-oznacovani-cen-konsolid-2022-cs.txt`, čl. 6a)
  - Pokyny Komise 2021/C 526/02 (`eu-komise/komise-pokyny-cl6a-smernice-98-6-2021-cs.txt`):
    - „Článek 6a směrnice o označování cen se vztahuje na oznámení o slevě z ceny ve všech distribučních kanálech (např. kamenné obchody, online)."
    - „Článek 6a se vztahuje rovněž například na oznámení jako „sleva", „speciální nabídka" nebo „nabídka Black Friday", která vytvářejí dojem snížení ceny, a u zboží dotčeného oznámením musí být uvedena „předchozí" cena"
    - „jakákoliv uvedená procentuální sleva z ceny musí být založena na „předchozí" ceně stanovené v souladu s článkem 6a"
    - „„Předchozí" cena jednotlivého zboží, na které se oznámení vztahuje, musí být uvedena v místě prodeje, tj. na cenovkách v obchodech nebo cenových sekcích v rozhraních online obchodů."
    - „Naproti tomu článek 6a se nevztahuje na obecná marketingová tvrzení, která propagují nabídku prodávajícího tím, že ji porovnávají s nabídkami jiných prodejců, aniž vyvolávají nebo vytvářejí dojem slevy z ceny, například „nejlepší/nejnižší ceny"."
  - ČOI (`coi/coi-kontroly-slevy-rok-2024.txt`): „sleva musí být odvozena z nejnižší ceny, za kterou bylo zboží prodáváno za posledních 30 dní před jeho zlevněním".
  - SOI, příklad zjištění (`soi/soi-dohlad-zlavy-2026.txt`): „Pri tovaroch obchodník deklaroval osobitnú cenovú výhodu, napriek tomu, že aktuálna akciová cena nebola nižšia ako najnižšia cena uplatňovaná počas 30 dní pred znížením ceny."
- **Požadavek lidsky:** Když stránka oznamuje slevu, musí u ceny uvést nejnižší cenu za 30 dní před slevou. Slevou se myslí procenta, částka, přeškrtnutá cena, „akce", „speciální nabídka" nebo „Black Friday".
  - U zboží v prodeji kratší dobu se uvede nejnižší cena od začátku prodeje.
  - Při postupném zvyšování slevy se uvede cena před prvním snížením.
  - Procento slevy se počítá z této předchozí ceny.
  - Neplatí pro zboží, které rychle podléhá zkáze nebo má krátkou dobu spotřeby.
- **Kontrola:**
  - Kód: signály slevy, text s 30denní cenou, kontrola procenta.
  - Jev: zda je sleva oznámena a zda je 30denní cena uvedena ve volné formulaci.
  - Nelze z textu: zda je uvedená 30denní cena pravdivá (chybí cenová historie) a zda jde o rychle se kazící zboží.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_price_reduction_announced` (podmínka) | Does this text announce a price reduction for the product, for example a discount in percent or money, a sale, special-offer or Black Friday price, or an earlier higher price shown next to the current price? | Oznamuje tento text snížení ceny výrobku, například slevu v procentech nebo v penězích, výprodejovou, akční nebo black friday cenu, nebo dřívější vyšší cenu uvedenou vedle současné ceny? |
| `pp_prior_lowest_price_stated` | Does this text state the lowest price of the product in the 30 days before the price reduction (sometimes called the previous price)? | Uvádí tento text nejnižší cenu výrobku za 30 dnů před snížením ceny (někdy označovanou jako předchozí cena)? |
| `pp_price_advantage_wording` (doplněk) | Does this text present the product's price as especially advantageous with a phrase such as "top price", "super price", "our price", "you save", or by comparing it with a recommended price? | Prezentuje tento text cenu výrobku jako mimořádně výhodnou pomocí výrazu jako „TOP cena", „super cena", „naše cena", „ušetříte" nebo srovnáním s doporučenou cenou? |

- **Kód:**

```text
signál slevy (text):  (?i)\b(slev\w*|zlevněn\w*|zľav\w*|zlacnen\w*|akčn\w*\s+cen\w*|akce|akcia|výprodej\w*|výpredaj\w*|black[\s-]?friday|cyber[\s-]?monday|původní\s+cen\w*|pôvodn\w*\s+cen\w*|dříve|predtým)\b
procento:             [-–−][\s\u00A0]?\d{1,2}(?:[.,]\d)?[\s\u00A0]?%
signál slevy (HTML):  <(del|s|strike)\b  nebo  text-decoration:\s*line-through  nebo  class="[^"]*(old|original|regular|before|previous|crossed|strike)[-_]?price
30denní cena:         (?i)(nejnižš\w*|najnižš\w*)\s+cen\w*[^.]{0,80}?(30|třiceti|tridsiatich)\s*(dn\w*|dní)|(předchozí|predchádzajúc\w*)\s+cen\w*
náhradní výrazy:      (?i)\b(top\s+cena|super\s+(cena|nabídka|ponuka)|naše\s+cena|ušetř\w*|ušetr\w*|cena\s+s\s+aplikací|doporučená\s+(?:prodejní\s+|spotřebitelská\s+)?cena|odporúčaná\s+(?:predajná\s+)?cena|MOC)\b
```

  Výpočet dělá kód. Značení: P je aktuální cena, R nejnižší 30denní cena z textu, X přeškrtnutá cena a D uvedené procento.
  - Očekávané procento je (R − P) / R × 100.
  - Když se D od očekávaného liší o víc než 1 procentní bod, vznikne nález „procento slevy není počítané z nejnižší 30denní ceny" (k ověření). Zvlášť to platí, když D odpovídá (X − P) / X a zároveň X ≠ R.
  - Když R ≤ P, vznikne nález „oznámená sleva není snížením oproti nejnižší 30denní ceně" (k ověření).
- **Skládání:**
  - `pp_prior_lowest_price_stated`: `page_presence` s podmínkou `applies_if: pp_price_reduction_announced ≥ 0,5 OR kód našel přeškrtnutou cenu nebo procento`.
  - Výpočet: kódová kontrola na úrovni stránky.
  - Náhradní výrazy: `segment`, pásmo nejvýš „k ověření", a jen když na stránce není 30denní cena.
- **Závažnost:** high. Náhradní výrazy medium.
- **Poznámky:**
  - Český § 12a výslovně neříká, že se sleva odvozuje z předchozí ceny. Opírá se to o pokyny Komise, o výklad ČOI a o rozsudek SDEU C‑330/23, na který odkazuje SOI (`soi/soi-dohlad-zlavy-2025.txt`, text z OCR). Samotný rozsudek jsem nestahoval, **NEOVĚŘENO** přímo. Slovenský § 7 ods. 1 to říká výslovně.
  - U obecného banneru (například „-20 % na vše") musí být předchozí cena u každého zboží.
  - Věrnostní a personalizované slevy do čl. 6a nespadají (pokyny Komise, oddíl 2.3).
  - Nápad: nástroj si může cenovou historii budovat sám z opakovaných skenů. Pak by šlo porovnat uváděnou 30denní cenu s vlastními daty.

### B.6 Jednotková (měrná) cena

- **id:** `pp_unit_price`
- **Ustanovení:**
  - CZ: § 13 odst. 4–7 a 10 zákona č. 526/1990 Sb., o cenách (znění od 13. 5. 2026), vyhláška č. 291/2024 Sb. a § 12 ZOS.
  - SK: § 6 ods. 1 a 3 a § 2 písm. h) zákona č. 108/2024 Z. z. a § 15 zákona č. 18/1996 Z. z.
  - EU: čl. 2 písm. b) a čl. 3 odst. 1 a 4 směrnice 98/6/ES.
- **Citace:**
  - CZ: „(4) Stanoví-li tak tento zákon, je prodávající dále povinen při nabídce a prodeji balených výrobků poskytnout spotřebiteli také informaci o ceně za měrnou jednotku množství výrobku (dále jen „měrná cena"). […] Měrná cena nemusí být uvedena, je-li totožná s prodejní cenou." (`predpisy-cz/cz-526-1990-zakon-o-cenach.txt`, § 13)
  - CZ: „(5) U měrné ceny se jako měrná jednotka množství uvede s ohledem na povahu výrobku 1 kilogram, 1 litr, 1 metr, 1 metr čtvereční nebo 1 metr krychlový výrobku. Lze uvádět i jiné jednotky množství v případech, kdy to odpovídá všeobecným zvyklostem nebo povaze výrobku." (tamtéž)
  - CZ: „(6) Povinnost podle odstavce 4 se vztahuje na balené potravinářské výrobky, které jsou v souladu s přímo použitelným předpisem Evropské unie označeny údajem o množství, objemu nebo hmotnosti výrobku, s výjimkou výrobků, u kterých není jejich označení měrnou cenou vzhledem k jejich povaze nebo účelu výrobků vhodné nebo u kterých by to bylo zavádějící." (tamtéž; vynechán odkaz na poznámku)
  - CZ: „(7) Povinnost podle odstavce 4 se dále vztahuje na balené nepotravinářské výrobky uvedené v seznamu, který stanoví Ministerstvo financí vyhláškou." (tamtéž)
  - CZ: „(10) Pro ceny uváděné v reklamě na výrobky nabízené spotřebiteli se použijí odstavce 2 až 9 obdobně." (tamtéž)
  - CZ vyhláška: „Povinnost označení měrnou cenou se vztahuje na druhy balených nepotravinářských výrobků, které jsou uvedeny v seznamu v příloze k této vyhlášce, nejsou-li tyto výrobky prodávány spotřebiteli v kombinaci s jiným výrobkem v jednom obalu a za jednu cenu." (`predpisy-cz/cz-291-2024-vyhlaska-merna-cena.txt`, § 2; účinnost 1. 1. 2025)
  - Stanovisko MF: „V případě tekutých produktů je nutné jako měrnou jednotku množství u měrné ceny použít „1 litr"." (`predpisy-cz/mf-stanovisko-merne-ceny-2024-11.txt`)
  - SK: „(1) Obchodník je povinný označiť tovar predajnou cenou a jednotkovou cenou jednoznačným a ľahko čitateľným spôsobom podľa osobitného predpisu. Jednotková cena nemusí byť vyznačená, ak je zhodná s predajnou cenou. Tovar predávaný na množstvo sa označuje len jednotkovou cenou." (`predpisy-sk/sk-108-2024-...txt`, § 6; poznámka 39 odkazuje na „§ 15 zákona Národnej rady Slovenskej republiky č. 18/1996 Z. z.")
  - SK: „(3) Označenie jednotkovou cenou sa nevzťahuje na a) tovar s menovitou hmotnosťou alebo menovitým objemom najviac 50 g alebo 50 ml, b) rôzne druhy tovarov, ak sa predávajú v jednom balení za jednu cenu," (tamtéž, § 6; další výjimky jsou v písm. c) až g))
  - SK: „h) jednotkovou cenou konečná cena vrátane dane z pridanej hodnoty a ostatných daní za kilogram, liter, meter, meter štvorcový, meter kubický tovaru alebo inú jednotku množstva, ktorá sa často a bežne používa pri predaji tovaru," (tamtéž, § 2)
  - EU: „4. V kterékoli reklamě, jež uvádí prodejní cenu výrobku podle článku 1, je třeba označit rovněž jednotkovou cenu podle článku 5." (`predpisy-eu/eu-1998-6-oznacovani-cen-konsolid-2022-cs.txt`, čl. 3)
- **Požadavek lidsky:**
  - **CZ:** Vedle prodejní ceny musí být i cena za 1 kg, 1 l, 1 m, 1 m² nebo 1 m³ (u kusových potravin za 1 ks). Platí pro dvě skupiny: balené potraviny označené množstvím a nepotravinářské výrobky ze seznamu vyhlášky č. 291/2024 Sb. (barvy, lepidla, stavební směsi, čisticí a prací prostředky, kosmetika a hygiena, krmiva a steliva, oleje…). Neplatí, když je měrná cena stejná jako prodejní (balení 1 kg), a u sad různých výrobků za jednu cenu.
  - **SK:** Jednotkovou cenu musí mít zásadně všechno zboží, u kterého se množství vyjadřuje hmotností, objemem, délkou nebo plochou. Výjimky jsou například zboží do 50 g/ml, různé druhy v jednom balení za jednu cenu nebo zboží, které se běžně takovými údaji neoznačuje.
- **Kontrola:**
  - Kód: množství z názvu a parametrů, regulární výraz na jednotkovou cenu, přepočet a porovnání výše.
  - Jev jen pro kategorii: `pp_is_packaged_food`, `pp_is_unit_price_nonfood_cz`, `pp_is_multi_product_set`.
- **Kód:**

```text
množství:         (?i)\b(\d+(?:[.,]\d+)?)[\s\u00A0]*(mg|g|kg|ml|cl|dl|l|m|cm|mm|m2|m²|m3|m³)\b
kusy a dávky:     (?i)\b(\d+)[\s\u00A0]*(ks|kusů|kusov|tbl|tablet|kaps\w*|dáv\w*|PD)\b
jednotková cena:  (?i)(Kč|CZK|€|EUR)[\s\u00A0]*/[\s\u00A0]*(?:1|100)?[\s\u00A0]*(kg|g|l|ml|m|m2|m²|m3|m³|ks|kus|dávk\w*|pran\w*)\b|(?:cena|měrná\s+cena|jednotková\s+cena)[\s\u00A0]+za[\s\u00A0]+(?:1|100)?[\s\u00A0]*(kg|kilogram|g|l|litr|liter|ml|m|metr|meter|m2|m²|ks|kus)\b
```

  Výpočet: z prodejní ceny P a množství Q přepočítat cenu na jednotku (1 kg, 1 l…) a porovnat s uvedenou jednotkovou cenou. Odchylka nad 1 % dá nález „jednotková cena nesedí" (k ověření). Když Q je přesně 1 jednotka, jednotková cena není potřeba. V SK při Q ≤ 50 g nebo 50 ml také ne.
- **Skládání:** `page_presence` (kód) s podmínkami:
  - CZ: `applies_if: (pp_is_packaged_food ≥ 0,5 OR pp_is_unit_price_nonfood_cz ≥ 0,5) AND množství nalezeno AND NOT pp_is_multi_product_set`.
  - SK: `applies_if: množství v jednotkách hmotnosti, objemu, délky nebo plochy nalezeno AND Q > 50 g/ml AND NOT pp_is_multi_product_set`.
- **Závažnost:** medium.
- **Poznámky:**
  - Že se česká měrná cena vztahuje i na nabídku v e-shopu, plyne ze slov „při nabídce a prodeji" a z odst. 10 o reklamě. Výslovné potvrzení pro e-shopy v podkladech nemám, **NEOVĚŘENO**.
  - Podle MF je u tekutin jednotka 1 l, u ostatních výrobků 1 kg a u potravin prodávaných po kusech 1 ks.

### B.7 Prodejní cena včetně daní a informace o dopravě

- **id:** `pp_total_price`, `pp_delivery_cost_info`
- **Ustanovení:**
  - CZ: § 13 odst. 2 a 3 zákona o cenách; § 5a odst. 3 písm. c) ZOS (nabídka ke koupi podle § 2 odst. 1 písm. p)); § 1811 odst. 2 písm. c) a e) a § 1820 odst. 1 písm. e) OZ.
  - SK: § 2 písm. g), § 5 ods. 1 písm. d) a § 11 ods. 4 písm. c) a ods. 5 zákona č. 108/2024 Z. z.; § 15 ods. 1 zákona č. 18/1996 Z. z.
  - EU: čl. 2 písm. a) směrnice 98/6/ES.
- **Citace:**
  - CZ: „(2) Prodávající je povinen při nabídce a prodeji zboží poskytnout informaci spotřebiteli tak, aby měl spotřebitel možnost seznámit se s cenou v české měně před jednáním o koupi zboží, a to a) označením zboží cenou, kterou uplatňuje v okamžiku nabídky […]" (`predpisy-cz/cz-526-1990-zakon-o-cenach.txt`, § 13)
  - CZ: „(3) Cenou podle odstavce 2 se rozumí konečná nabídková cena, která zahrnuje všechny daně, poplatky a jiná obdobná peněžitá plnění (dále jen „prodejní cena"). Údaj o prodejní ceně musí být jednoznačný, snadno rozpoznatelný a dobře čitelný." (tamtéž)
  - CZ: „p) nabídkou ke koupi obchodní sdělení, které způsobem vhodným pro použitý typ obchodního sdělení uvádí informace o výrobku nebo službě a cenu a umožňuje tak spotřebiteli uskutečnit koupi," (ZOS § 2 odst. 1)
  - CZ: „c) cena včetně daní, poplatků a jiných obdobných peněžitých plnění, […] a případně i veškeré další platby za dopravu nebo dodání, nebo pokud tyto platby nelze rozumně stanovit předem, skutečnost, že k ceně mohou být účtovány takové další platby," (ZOS § 5a odst. 3; úvod odstavce: „Nejsou-li patrné ze souvislostí, považují se v případě nabídky ke koupi za podstatné tyto informace:")
  - CZ: „e) celkovou cenu a náklady na dodání podle § 1811 odst. 2 písm. c) a e);" (`predpisy-cz/cz-89-2012-obcansky-zakonik.txt`, § 1820 odst. 1)
  - SK: „c) predajnej cene produktu, o spôsobe, akým sa vypočíta, ak vzhľadom na povahu produktu nemožno predajnú cenu primerane určiť vopred, o nákladoch na dopravu, dodanie alebo poštovné, alebo o skutočnosti, že do ceny môžu byť zarátané ďalšie náklady, ak ich nemožno určiť vopred," (zákon 108/2024, § 11 ods. 4)
  - SK: „(5) Výzvou na kúpu podľa odseku 4 sa rozumie každá obchodná komunikácia, ktorá obsahuje opis základných znakov produktu a jeho predajnú cenu spôsobom, ktorý zodpovedá povahe použitej obchodnej komunikácie, a tým umožňuje spotrebiteľovi uskutočniť kúpu." (tamtéž)
  - SK: „Každý tovar musí byť pri predaji označený cenou platnou v čase ponuky, a to cenovkou, informáciou o cene formou cenníka, vývesky alebo iným primeraným spôsobom." (`predpisy-sk/sk-18-1996-o-cenach-zneni-od-2026-01-01.txt`, § 15 ods. 1)
  - EU: „a) „prodejní cenou" rozumí konečná cena za jednotku nebo za dané množství výrobku, která zahrnuje DPH a všechny ostatní daně;" (směrnice 98/6/ES, čl. 2)
- **Požadavek lidsky:**
  - Cena na produktové stránce musí být konečná, tedy včetně DPH a všech poplatků. V CZ musí být v české měně. Musí být jednoznačná a čitelná.
  - Náklady na dopravu, nebo aspoň informaci, že se připočtou, musí spotřebitel znát před objednávkou.
  - U produktové stránky jako nabídky ke koupi je doprava podstatnou informací, pokud není patrná ze souvislostí.
- **Kontrola:**
  - Cenu kontroluje kód: zda je cena na stránce, v jaké měně je a zda je uvedeno „bez DPH" bez varianty „s DPH".
  - Dopravu kontroluje Jev, jen jako „k ověření", protože nástroj neprochází košík.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_delivery_cost_info` | Does this text state the delivery cost for the product, that delivery is free, or that delivery costs may be added to the price? | Uvádí tento text cenu dopravy výrobku, že je doprava zdarma, nebo že k ceně mohou být připočteny náklady na dopravu? |

- **Kód:**

```text
cena CZ:   (?i)\b\d{1,3}(?:[\s\u00A0.]\d{3})*(?:,\d{1,2}|,-)?[\s\u00A0]*(Kč|CZK)\b
cena SK:   (?i)\b\d{1,3}(?:[\s\u00A0.]\d{3})*(?:,\d{1,2})?[\s\u00A0]*(€|EUR)\b|€[\s\u00A0]*\d
bez DPH:   (?i)\bbez[\s\u00A0]*DPH\b
s DPH:     (?i)\b(s|vč\.?|včetně|vrátane)[\s\u00A0]*DPH\b
doprava:   (?i)\b(doprav\w*|doručen\w*|doručenie|poštovn\w*|dopravn\w*)\b
```

  Pravidla kódu:
  1. Na stránce není viditelná cena → nález. U „cena na dotaz" jen „k ověření".
  2. Česká stránka s cenou, která není v Kč → nález podle § 13 odst. 2 zákona o cenách.
  3. Cena „bez DPH" bez ceny „s DPH" nebo „vč. DPH" → „k ověření". E-shop může být určen jen podnikům.
- **Skládání:** `page_presence`. Cena kódem, doprava Jevem s pásmem nejvýš „k ověření".
- **Závažnost:** cena medium, doprava low.
- **Poznámky:** ČOI ve 2Q 2026 našla 10 případů podle § 1820 odst. 1 písm. e) OZ. Informace o dopravě bývá na stránce „Doprava a platba". Pokud na ni produktová stránka odkazuje, stačí „k ověření".

### B.8 Recenze: informace o ověřování

- **id:** `pp_reviews_verification`, doplněk `pp_reviews_verified_claim`
- **Ustanovení:**
  - CZ: § 4 odst. 4 a § 5a odst. 5 ZOS; příloha č. 1 písm. y) a z) ZOS.
  - SK: § 11 ods. 6 písm. a) a príloha č. 1 body 31 a 32 zákona č. 108/2024 Z. z.
  - EU: pokyny Komise k směrnici 2005/29/ES (2021/C 526/01), oddíl 4.2.4.
- **Citace:**
  - CZ: „(5) Poskytuje-li prodávající přístup k hodnocení výrobků nebo služeb provedenému jiným spotřebitelem (dále jen „spotřebitelská recenze"), za podstatnou informaci se považuje také informace o tom, zda a jak prodávající zajišťuje, aby zveřejněná spotřebitelská recenze pocházela od spotřebitele, který výrobek nebo službu skutečně použil nebo si je zakoupil." (`predpisy-cz/cz-634-1992-...txt`, § 5a)
  - CZ: „y) uvádí, že recenze výrobku nebo služby podává spotřebitel, který produkt skutečně použil nebo jej zakoupil, aniž by přijal přiměřená opatření k ověření toho, zda pocházejí od takového spotřebitele," (tamtéž, příloha č. 1)
  - SK: „a) tom, či a akým spôsobom obchodník zabezpečuje, že hodnotenia produktov pochádzajú od spotrebiteľov, ktorí produkt skutočne kúpili alebo použili, ak obchodník poskytuje spotrebiteľom prístup k hodnoteniu produktov," (zákon 108/2024, § 11 ods. 6)
  - SK: „31. Vyhlásenie, že hodnotenia produktu poskytujú spotrebitelia, ktorí tento produkt skutočne použili alebo kúpili, bez prijatia náležitých a primeraných krokov na kontrolu toho, že hodnotenia pochádzajú od takýchto spotrebiteľov." (tamtéž, príloha č. 1)
  - Pokyny Komise: „Tyto informace musí být jasné, srozumitelné a musí být zpřístupněny „v okamžiku, kdy je poskytován přístup ke spotřebitelským recenzím", tj. informace by měly být přístupné ve stejném rozhraní, kde spotřebitelé mohou číst zveřejněné recenze, a to i prostřednictvím jasně identifikovaných a zřetelně zobrazených hypertextových odkazů." (`eu-komise/komise-pokyny-ucpd-2021-cs.txt`)
  - ČOI: „Informace nebyla poskytnuta přímo u zveřejňovaných recenzí, ale pouze ve všeobecných smluvních podmínkách." (`coi/coi-kontroly-recenze-rok-2024.txt`, hodnoceno jako porušení)
- **Požadavek lidsky:**
  - Když stránka ukazuje recenze nebo hodnocení (hvězdičky), musí na stejném místě uvést, zda a jak e-shop zajišťuje, že pocházejí od zákazníků, kteří výrobek koupili nebo použili.
  - Stačí to uvést přímo u recenzí nebo jasně označeným odkazem. Jen v obchodních podmínkách to nestačí.
  - Tvrdit „ověřené recenze" bez přiměřených opatření je zakázané vždy.
- **Kontrola:** Jev a kód.
  - Jev: zda stránka ukazuje recenze, zda uvádí informaci o ověřování, zda tvrdí, že jsou recenze ověřené, a zda popisuje způsob ověření.
  - Kód: JSON-LD `aggregateRating` a `review`, skripty widgetů, odkaz na zásady recenzí.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_reviews_shown` (podmínka) | Does this text show reviews, ratings or star scores of the product from customers or other users? | Zobrazuje tento text recenze, hodnocení nebo hvězdičky výrobku od zákazníků či jiných uživatelů? |
| `pp_review_verification_info` | Does this text say whether or how the shop checks that product reviews come from customers who actually bought or used the product? A statement that reviews are not checked also counts. | Uvádí tento text, zda nebo jak obchod ověřuje, že recenze výrobků pocházejí od zákazníků, kteří výrobek skutečně koupili nebo použili? Počítá se i sdělení, že recenze ověřovány nejsou. |
| `pp_reviews_verified_claim` | Does this text claim that the reviews come from verified customers or from people who actually bought or used the product? | Tvrdí tento text, že recenze pocházejí od ověřených zákazníků nebo od lidí, kteří výrobek skutečně koupili nebo použili? |
| `pp_review_verification_method` | Does this text describe how reviews are checked, for example that only customers with a completed order can write a review? | Popisuje tento text, jakým způsobem se recenze ověřují, například že recenzi může napsat jen zákazník s dokončenou objednávkou? |

- **Kód:**

```text
JSON-LD:         Product.aggregateRating nebo Product.review
widgety:         skript/iframe obsahující heureka|trustedshops|trusted-shops|yotpo|trustpilot|judge\.me
odkaz na zásady: (?i)(jak|zda)\s+(ověřujeme|kontrolujeme|overujeme)\s+recenz\w*|ověřování\s+recenzí|overovan\w*\s+recenzi\w*|pravost\s+recenzí
```

- **Skládání:**
  - `pp_review_verification_info`: `page_presence` s podmínkou `applies_if: pp_reviews_shown ≥ 0,5 OR JSON-LD aggregateRating/review`. Odkaz na zásady nalezený kódem splňuje povinnost s poznámkou „ověřit, že je zřetelný".
  - Druhé pravidlo: `pp_reviews_verified_claim` platí a `pp_review_verification_method` na stránce ani v odkazu není → „k ověření" (CZ příloha č. 1 písm. y), SK bod 31).
- **Závažnost:** high.
- **Poznámky:**
  - Widgety třetích stran se načítají JavaScriptem nebo v iframe a nástroj jejich obsah nevidí. Když kód najde skript widgetu, nález má být „k ověření".
  - Pod povinnost spadá i hodnocení obchodu, ne jen výrobku, pokud slouží k propagaci výrobků (pokyny Komise, oddíl 4.2.4).

### B.9 Hlavní vlastnosti výrobku

- **id:** `pp_main_characteristics`
- **Ustanovení:** CZ § 1811 odst. 2 písm. b) a § 1820 odst. 1 písm. a) OZ; § 5a odst. 3 písm. a) ZOS. SK § 5 ods. 1 písm. a) a § 11 ods. 4 písm. a) zákona č. 108/2024 Z. z.
- **Citace:**
  - CZ: „a) údaje o hlavních vlastnostech zboží nebo služby v rozsahu odpovídajícím použitému prostředku komunikace na dálku a povaze zboží nebo služby," (`predpisy-cz/cz-89-2012-obcansky-zakonik.txt`, § 1820 odst. 1)
  - CZ: „a) hlavní znaky výrobku nebo služby v rozsahu odpovídajícím danému sdělovacímu prostředku, jakož i výrobku nebo službě," (ZOS § 5a odst. 3)
  - SK: „a) hlavné vlastnosti produktu v rozsahu primeranom druhu a povahe produktu a forme poskytnutia informácií," (zákon 108/2024, § 5 ods. 1)
- **Požadavek lidsky:** Před objednávkou musí spotřebitel znát hlavní vlastnosti výrobku v rozsahu, který odpovídá výrobku a médiu.
- **Kontrola:** Jev zjistí, zda text uvádí aspoň jednu konkrétní vlastnost. Úplnost z textu posoudit nejde, protože závisí na druhu výrobku.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_main_characteristics` | Does this text describe at least one specific property of the product, such as its material, dimensions, weight, volume, composition, performance or function? | Popisuje tento text alespoň jednu konkrétní vlastnost výrobku, například materiál, rozměry, hmotnost, objem, složení, výkon nebo funkci? |

- **Skládání:** `page_presence`, pásmo nejvýš „k ověření".
- **Závažnost:** low.
- **Poznámky:** Povinnost je obvykle splněná. Smysl má hlavně u stránek, kde je jen název a cena. SOI 2025 zmiňuje stížnosti na klamání o hlavních vlastnostech (dostupnost, datum dodání). To se z textu rozhodnout nedá.

### B.10 Textil: materiálové složení

- **id:** `pp_textile_fibre_composition`
- **Ustanovení:** čl. 16 odst. 1 a 3 nařízení (EU) č. 1007/2011. Nařízení je přímo použitelné v CZ i SK.
- **Citace:**
  - „1. Při dodávání textilního výrobku na trh se údaje o materiálovém složení textilií zmíněné v článcích 5, 7, 8 a 9 uvádějí čitelně, viditelně, zřetelně […] Tyto informace musí být pro zákazníka jasně viditelné před samotným nákupem, a to i pokud se jedná o nákup elektronickými prostředky." (`predpisy-eu/eu-2011-1007-textil-cs.txt`, čl. 16)
  - „3. Označení musí být poskytnuta v úředním jazyce či jazycích členského státu, na jehož území jsou textilní výrobky dodávány spotřebitelům, nerozhodne-li dotyčný členský stát jinak." (tamtéž)
  - ČOI, celotržní akce textil 2025: „Jednalo se převážně o porušení povinnosti označení textilního výrobku materiálovým složením v českém jazyce (výrobky měly značení pouze v anglickém jazyce)" (`coi/coi-vyrocni-zprava-2025.txt`)
- **Požadavek lidsky:** U textilu musí být materiálové složení, tedy názvy vláken s procenty, vidět před nákupem, i online. Musí být v úředním jazyce: v CZ česky, v SK slovensky.
- **Kontrola:** Jev rozhodne o kategorii a o přítomnosti složení. Kód hledá procenta s názvy vláken a hlídá jazyk názvů.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_fibre_composition` | Does this text state the fibre composition of the product, that is the textile fibres with their percentages (for example "95 % cotton, 5 % elastane")? | Uvádí tento text materiálové složení výrobku, tedy textilní vlákna s jejich procentním podílem (například „95 % bavlna, 5 % elastan")? |

- **Kód:**

```text
CS/SK:          (?i)\b\d{1,3}(?:[.,]\d)?[\s\u00A0]?%[\s\u00A0]*(bavln\w*|polyester\w*|elastan\w*|visk[oó]z\w*|vln\w*|vlna|len|lnu|ľan\w*|hedváb\w*|hodváb\w*|polyamid\w*|akryl\w*|lyocell|modal|kašmír\w*|polypropylen\w*|polyuretan\w*)
jen anglicky:   (?i)\b\d{1,3}\s?%\s?(cotton|elastane|spandex|viscose|wool|linen|silk|nylon|acrylic)\b
```

  Když se složení najde jen s anglickými názvy vláken, vznikne nález „složení není v úředním jazyce". Slovo „polyester" je stejné v obou jazycích, proto v anglickém seznamu chybí.
- **Skládání:** `page_presence` s podmínkou `applies_if: pp_is_textile ≥ 0,5`.
- **Závažnost:** medium.
- **Poznámky:**
  - Výjimky podle čl. 17 a přílohy V jsem nestudoval, **NEOVĚŘENO**.
  - Oficiální české a slovenské názvy vláken jsou v příloze I nařízení. Seznam v regulárním výrazu je zatím pracovní.
  - Pravidla pro obuv jsou v jiném předpisu, **NEOVĚŘENO**.

### B.11 Alkohol a tabák (CZ): zákaz prodeje mladším 18 let a identifikace prodejce v místě nabídky

- **id:** `pp_cz_alcohol_tobacco_distance`
- **Ustanovení:** § 6 odst. 2 a 3 (tabák a nikotinové výrobky) a § 15 odst. 2 a 3 (alkohol) zákona č. 65/2017 Sb.
- **Citace:**
  - „(2) Prodejce alkoholických nápojů prostřednictvím prostředku komunikace na dálku je povinen před prodejem alkoholických nápojů spotřebitele informovat o zákazu prodeje osobám mladším 18 let zjevně viditelným textem způsobem přiměřeným možnostem prostředku komunikace na dálku." (`predpisy-cz/cz-65-2017-zakon-o-ochrane-zdravi-pred-navykovymi-latkami.txt`, § 15)
  - „(3) Prodejce alkoholických nápojů prostřednictvím prostředku komunikace na dálku je povinen uvést v místě nabídky prodeje alkoholických nápojů své jméno, adresu sídla a identifikační číslo osoby." (tamtéž, § 15)
  - § 6 odst. 2 a 3 zní shodně pro „tabákových výrobků, kuřáckých pomůcek, bylinných výrobků určených ke kouření, elektronických cigaret, nikotinových sáčků bez obsahu tabáku a výrobků obsahujících nikotin", například „je povinen uvést v místě nabídky prodeje těchto výrobků své jméno, adresu sídla a identifikační číslo osoby." (tamtéž, § 6)
- **Požadavek lidsky:** E-shop s alkoholem, tabákem, e-cigaretami nebo nikotinovými sáčky musí:
  - před prodejem zjevně viditelným textem informovat, že se tyto výrobky nesmí prodat osobám mladším 18 let,
  - v místě nabídky uvést své jméno, adresu sídla a IČO.
- **Kontrola:** Kód hledá formulaci 18+, IČO a adresu. Jev určuje kategorii a zachytí volné formulace zákazu prodeje.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_under18_notice` | Does this text state that the product must not be sold to persons under 18? | Uvádí tento text, že se výrobek nesmí prodávat osobám mladším 18 let? |
| `pp_seller_identity` | Does this text give the seller's business name together with its registered office address? | Uvádí tento text obchodní jméno prodejce spolu s adresou jeho sídla? |

- **Kód:**

```text
18+:  (?i)(osobám|osobam)[\s\u00A0]+mlad[šs]ím[\s\u00A0]+(?:než[\s\u00A0]+)?18|18[\s\u00A0]*let|\b18\+
IČO:  (?i)\bI[ČC]O?\b[\s\u00A0:]*(\d{8})\b   (kontrolní součet mod 11: algoritmus není v podkladech, NEOVĚŘENO)
```

- **Skládání:** `page_presence` s podmínkou `applies_if: pp_is_alcohol ≥ 0,5 OR pp_is_tobacco_nicotine ≥ 0,5`. Patička je součástí stránky, takže IČO v patičce povinnost splní. Věkovou bránu v modálním okně crawler nemusí vidět, proto chybějící upozornění na 18 let je jen „k ověření".
- **Závažnost:** medium.
- **Poznámky:**
  - Zda je text „zjevně viditelný", z textu nerozhodneme.
  - Systém ověřování věku podle § 6 odst. 1 z textu zkontrolovat nejde. SOI ho u e-shopů kontroluje a v roce 2025 našla porušení v 28,57 % kontrol.
  - Slovenská obdoba (zákon č. 377/2004 Z. z.) je **NEOVĚŘENO**, nestahoval jsem ji.

### B.12 EmpCo: harmonizovaný štítek záruky výrobce na trvanlivost (SK od 27. 9. 2026)

- **id:** `pp_sk_durability_guarantee_label`
- **Ustanovení:**
  - SK: § 5 ods. 1 písm. g) zákona č. 108/2024 Z. z. ve znění od 27. 9. 2026 a vykonávací nařízení (EU) 2025/1960, příloha II.
  - EU: čl. 6 odst. 1 písm. la) směrnice 2011/83/EU ve znění směrnice (EU) 2024/825.
  - CZ: transpozice zatím neschválena.
- **Citace:**
  - SK: „g) existenciu a dĺžku trvania spotrebiteľskej záruky na životnosť tovaru, ak ju výrobca alebo dovozca bezplatne poskytuje spotrebiteľovi na celý tovar s dĺžkou trvania viac ako dva roky a tieto informácie sprístupnil obchodníkovi, a existenciu zákonnej zodpovednosti obchodníka za vady tovaru, a to zreteľným spôsobom aspoň v podobe a v rozsahu podľa osobitného predpisu […] upravujúceho harmonizované označenie," (`predpisy-sk/sk-108-2024-...txt`, § 5 ods. 1; poznámka 22a odkazuje na nařízení 2025/1960)
  - EU: „Použije se ode dne 27. září 2026." (`predpisy-eu/eu-2025-1960-harmonizovane-oznameni-a-stitek-cs.txt`, čl. 3)
  - Tamtéž: „V případě smluv uzavřených na dálku prostřednictvím on-line rozhraní musí být harmonizovaný štítek pro obchodní záruku na trvanlivost barevný."
  - Tamtéž: „Pokud se používá vnořené zobrazení, harmonizovaný štítek se musí zobrazit v plném rozsahu při prvním kliknutí myší, najetí myší nebo rozbalení dotykové obrazovky."
  - EmpCo, bod 28 odůvodnění: „nebo pokud je zboží nabízeno k prodeji on-line, umístěním štítku přímo vedle obrázku zboží" (`predpisy-eu/eu-2024-825-empco-cs.txt`)
  - Stav v CZ: „Sněmovní tisk 53 (CZ transpozice EmpCo): po 2. čtení, 3. čtení k 25. 9. 2026 neproběhlo" (`README.md`)
- **Požadavek lidsky:** Pokud výrobce nabízí bezplatnou záruku trvanlivosti na celé zboží delší než dva roky a sdělil to prodejci, musí slovenský e-shop od 27. 9. 2026 u výrobku zobrazit harmonizovaný štítek. Online musí být barevný. Může být vnořený, tedy zobrazený po kliknutí nebo najetí myší. Vhodné místo je vedle obrázku výrobku.
- **Kontrola:**
  - Podmínku lze z textu zjistit jen částečně. Jev pozná, že text zmiňuje záruku výrobce, a kód pozná, že jde o víc než 2 roky. Zda výrobce informaci prodejci skutečně předal, z textu nezjistíme.
  - Samotný štítek je obrázek, takže ho z textu ověřit nejde. Kód může hledat odkaz na cíl QR kódu `europa.eu/youreurope/commercial-guarantee-durability` nebo alt text obrázku.
- **Otázky:**

| id | EN | CS |
|---|---|---|
| `pp_manufacturer_durability_guarantee` (podmínka) | Does this text state that the manufacturer gives a guarantee of durability or a manufacturer's guarantee for the product? | Uvádí tento text, že výrobce poskytuje na výrobek záruku trvanlivosti nebo záruku výrobce? |

- **Kód:**

```text
roky záruky:  (?i)(záruk\w*|garanc\w*)[^.]{0,40}?(\d{1,2})[\s\u00A0]*(let|roky|roků|rokov|rok|years)   → počet let > 2
štítek:       href, src nebo alt obsahující "commercial-guarantee-durability" nebo název štítku (heuristika)
```

- **Skládání:** `page_presence` (kód) s podmínkou `applies_if: pp_manufacturer_durability_guarantee ≥ 0,5 AND roky > 2`, jen pro SK, pásmo nejvýš „k ověření".
- **Závažnost:** low.
- **Poznámky:**
  - Harmonizované oznámení o zákonné záruce (§ 5 ods. 1 písm. f)) patří na web obecně, ne k výrobku. Podle bodu 28 odůvodnění EmpCo se u internetového prodeje zobrazí „jako obecné upozornění na internetových stránkách obchodníka". Je to tedy `site_presence` a do této rešerše nepatří.
  - Podmíněné jsou i údaje o opravitelnosti (§ 5 ods. 1 písm. k) a l)) a o aktualizacích softwaru (písm. p)). Platí jen tehdy, pokud je výrobce zpřístupnil, a to z textu ověřit nelze.

### B.13 Energetický štítek (jen zmínka)

- Rámec je nařízení (EU) 2017/1369. Jeho název je doložen v `soi/soi-vyrocna-sprava-2025.txt`: „Nariadenie EP a R (ES) č. 2017/1369 zo 4. júla 2017, ktorým sa stanovuje rámec pre energetické označovanie a zrušuje smernica 2010/30/EÚ". Tamtéž je uvedeno delegované nařízení (EU) č. 518/2014 „pokiaľ ide o označovanie energeticky významných výrobkov na internete štítkami".
- Konkrétní pravidla zobrazení na internetu (vnořené zobrazení, informační list, databáze EPREL) jsou **NEOVĚŘENO**. Předpisy jsem nestahoval.
- Kontrola: z textu nelze, protože štítek je obrázek. Kód může u spotřebičů najít obrázek štítku, odkaz na EPREL nebo text „energetická třída" a upozornit na něj „k ověření".

### B.14 Souhrn

| # | Povinnost | CZ | SK | Kontrola | Skládání | Závažnost |
|---|---|---|---|---|---|---|
| B.1 | Výrobce: jméno, poštovní a elektronická adresa | GPSR čl. 19 a); 387/2024 § 9 | GPSR čl. 19 a); 83/2025 § 9 | jev + kód | `page_presence`, bez vyloučených kategorií | high |
| B.2 | Odpovědná osoba v EU | GPSR čl. 19 b) | stejně | kód (země) + jev | `page_presence`, jen když je výrobce mimo EU | high |
| B.3 | Obrázek a typ výrobku | GPSR čl. 19 c) | stejně | kód (obrázek jen přítomnost), jev | `page_presence` | medium |
| B.4 | Varování v jazyce trhu | GPSR čl. 19 d); 387/2024 § 8 | 83/2025 § 2 ods. 6 | jev + kód (jazyk); nutnost varování z textu zjistit nelze | `page_presence`, rizikové kategorie, jen k ověření | medium |
| B.5 | Nejnižší 30denní cena, výpočet procenta slevy | ZOS § 12a | 108/2024 § 7 | kód + jev; pravdivost z textu zjistit nelze | `page_presence`, když je oznámena sleva | high |
| B.6 | Měrná (jednotková) cena | zákon o cenách § 13, vyhl. 291/2024 | 108/2024 § 6 | kód; kategorie Jev | `page_presence`, podle kategorie a množství | medium |
| B.7 | Konečná cena včetně daní; doprava | zákon o cenách § 13; ZOS § 5a odst. 3; OZ § 1820 odst. 1 e) | 108/2024 § 5, § 11 | kód; doprava Jev (k ověření) | `page_presence` | medium / low |
| B.8 | Informace o ověřování recenzí | ZOS § 5a odst. 5; příl. 1 y) | 108/2024 § 11 ods. 6 a); príl. 1 bod 31 | jev + kód | `page_presence`, když jsou recenze | high |
| B.9 | Hlavní vlastnosti | OZ § 1811, § 1820 a); ZOS § 5a | 108/2024 § 5 a), § 11 | jev | `page_presence`, k ověření | low |
| B.10 | Materiálové složení textilu | nař. 1007/2011 čl. 16 | stejně | jev + kód | `page_presence`, textil | medium |
| B.11 | Upozornění 18+ a IČO u alkoholu a tabáku | 65/2017 § 6, § 15 | NEOVĚŘENO | kód + jev | `page_presence`, kategorie | medium |
| B.12 | Harmonizovaný štítek záruky na trvanlivost | čeká na transpozici | 108/2024 § 5 g) od 27. 9. 2026 | z textu jen podmínka; štítek je obrázek | `page_presence`, jen k ověření | low |
| B.13 | Energetický štítek | 2017/1369 (zmínka) | stejně | z textu nelze | – | – |

Co z textu produktové stránky nejde vůbec: pravdivost 30denní ceny, obsah obrázků (vyobrazení výrobku, energetický a harmonizovaný štítek), nutnost konkrétního varování, podmínky EmpCo na straně výrobce, cokoli v košíku a pokladně (§ 1826a odst. 2, doprava v košíku), ověřování věku a to, zda je text „zjevně viditelný".

### B.15 Návrh zápisu v YAML

Jen ilustrace sémantiky. `page_presence` a `applies_if` v kódu zatím nejsou.

```yaml
# Návrh: rules/product_cz.yaml
version: "product-cz-2026-09-25-draft1"
module: product
applies_to: sentence
page_types: [product]
jurisdictions: [cz]
presence_threshold: 0.7
rules:
  - id: pp_reviews_verification_missing
    title: "U recenzí chybí informace, zda a jak se ověřují"
    scope: page_presence
    applies_if:
      any: [{q: pp_reviews_shown, gte: 0.5}, {code: jsonld_has, field: "aggregateRating|review"}]
    question: pp_review_verification_info
    code_checks:
      - {type: link_text_present, where: page, pattern: '(?i)ověřování\s+recenzí|(jak|zda)\s+(ověřujeme|kontrolujeme)\s+recenz'}
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: cz, ref: "§ 4 odst. 4 a § 5a odst. 5 zákona č. 634/1992 Sb.", status: "ověřit"}
  - id: pp_gpsr_manufacturer_address_missing
    title: "V nabídce chybí poštovní adresa výrobce"
    scope: page_presence
    applies_if:
      none: [{q: pp_gpsr_excluded_category, gte: 0.5, where: page_header}]
    question: pp_manufacturer_postal_address
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "čl. 19 písm. a) nařízení (EU) 2023/988", status: "ověřit"}
      - {jurisdiction: cz, ref: "§ 9 odst. 2 písm. a) zákona č. 387/2024 Sb.", status: "ověřit"}
```

---

## Stažené soubory

Všechny `.txt` jsou textové výtahy. Originál leží vedle nich. Dokumenty `.doc` jsem převedl nástrojem antiword, `.docx` rozbalením `word/document.xml`, PDF nástrojem pdftotext a sken SOI přes tesseract (jen strany 1–3).

**`coi/` (ČOI)**

| Soubor | URL |
|---|---|
| `coi-kontroly-internet-1Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/06/2024-06-12-internet-1Q.doc |
| `coi-kontroly-internet-2Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/08/2024-08-12-kontroly-internet-2Q-2024.doc |
| `coi-kontroly-internet-3Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/12/2024-12-16-kontroly-internet-3Q-2024.doc |
| `coi-kontroly-internet-rok-2024.docx` | https://coi.gov.cz/wp-content/uploads/2025/04/2025-04-02-internetove-obchody_2024.docx |
| `coi-kontroly-internet-3Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/12/2025-12-16-kontroly-internet-3Q-2025.docx |
| `coi-kontroly-internet-rok-2025.docx` | https://coi.gov.cz/wp-content/uploads/2026/02/2026-02-25-internetovy-prodej_2025.docx |
| `coi-kontroly-internet-1Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/07/2026-07-15-internetovy-prodej-1Q-2026.docx |
| `coi-kontroly-internet-2Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/08/2026-08-12-internetovy-prodej_2Q.docx |
| `coi-kontroly-internet-rok-2023.doc` | https://coi.gov.cz/wp-content/uploads/2024/04/2024-04-26-e-shopy-rok-2023.doc |
| `coi-kontroly-slevy-1Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/06/2024-06-07-slevy-1Q.doc |
| `coi-kontroly-slevy-2Q-2024.doc` | https://coi.gov.cz/wp-content/uploads/2024/08/2024-08-08-kontroly-slevy-2Q-2024.doc |
| `coi-kontroly-slevy-rok-2024.docx` | https://coi.gov.cz/wp-content/uploads/2025/04/2025-04-03-slevy-2024.docx |
| `coi-kontroly-slevy-1Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/05/2025-05-29-slevy-1Q-2025.docx |
| `coi-kontroly-slevy-2Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/07/2025-07-31-slevy-2Q-2025.docx |
| `coi-kontroly-slevy-3Q-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/12/2025-12-11-slevy-3Q-2025.docx |
| `coi-kontroly-slevove-akce-vanoce-2025.docx` | https://coi.gov.cz/wp-content/uploads/2026/01/2026-01-12-kontroly-slevove-akce.docx |
| `coi-kontroly-slevy-rok-2025.docx` | https://coi.gov.cz/wp-content/uploads/2026/03/2026-03-05-slevy-2025.docx |
| `coi-kontroly-slevy-1Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/06/2026-06-17-poskytovani-slev-1Q-2026.docx |
| `coi-kontroly-slevy-2Q-2026.docx` | https://coi.gov.cz/wp-content/uploads/2026/08/2026-08-31-slevy-2Q.docx |
| `coi-slevove-akce-black-friday-2024.doc` (rady spotřebitelům, ne výsledky kontrol) | https://coi.gov.cz/wp-content/uploads/2024/11/2024-11-19-slevove-akce-bf.doc |
| `coi-kontroly-recenze-rok-2024.docx` | https://coi.gov.cz/wp-content/uploads/2025/04/2025-04-29-spotrebitelske-recenze-2024.docx |
| `coi-kontroly-recenze-2025.docx` | https://coi.gov.cz/wp-content/uploads/2025/12/2025-12-03-recenze.docx |
| `coi-vyrocni-zprava-2025.pdf` | https://coi.gov.cz/wp-content/uploads/2026/04/COI_Vyrocni_zprava_2025.pdf |
| `coi-vyrocni-zprava-2024.pdf` | https://coi.gov.cz/wp-content/uploads/2025/04/COI-vyrocni-zprava-2024.pdf |
| `coi-kontroly-internet-1Q-2025.txt`, `coi-kontroly-internet-2Q-2025.txt` | jen nové textové výtahy k existujícím `.docx` |

**`soi/` (SOI)**

| Soubor | URL |
|---|---|
| `soi-vyrocna-sprava-2025.pdf` | https://www.soi.sk/files/documents/kcinnost/vyrocne-spravy/vyrocna-sprava-soi-2025.pdf |
| `soi-vyrocna-sprava-2024.pdf` | https://www.soi.sk/files/documents/kcinnost/vyrocne-spravy/vyrocna-sprava-soi-2024.pdf |
| `soi-dohlad-zlavy-2026.pdf` | https://www.soi.sk/files/documents/v%c3%bdsledky%20kontrol/2026/informacia-o-vysledkoch-dohladu-nad-dodrziavanim-povinnosti-obchodnikov-pri-ponuke-a-predaji-tovaru-zlavach.pdf |
| `soi-dohlad-zlavy-2025.pdf` (sken; `.txt` obsahuje OCR stran 1–3) | https://www.soi.sk/files/documents/v%c3%bdsledky%20kontrol/2025/informacia-o-vysledkoch-dohladu-nad-dodrziavanim-povinnosti-obchodnikov-pri-ponuke-a-predaji-tovaru-v-zlavach.pdf |

**Předpisy a výklad**

| Soubor | Co | Zdroj |
|---|---|---|
| `predpisy-eu/eu-2023-988-gpsr-cs.xhtml` + `.txt` | GPSR česky | Cellar `http://publications.europa.eu/resource/celex/32023R0988` (Accept: application/xhtml+xml, Accept-Language: ces) |
| `predpisy-eu/eu-1998-6-oznacovani-cen-konsolid-2022-cs.xhtml` + `.txt` | směrnice 98/6/ES, konsolidace 28. 5. 2022 (včetně čl. 6a) | Cellar `celex/01998L0006-20220528` |
| `predpisy-eu/eu-2011-1007-textil-cs.xhtml` + `.txt` | nařízení (EU) č. 1007/2011 | Cellar `celex/32011R1007` |
| `eu-komise/komise-gpsr-pokyny-2025-6233-cs.xhtml` + `.txt` | Sdělení Komise C/2025/6233: pokyny ke GPSR pro podniky | Cellar `celex/52025XC06233` |
| `eu-komise/komise-pokyny-cl6a-smernice-98-6-2021-cs.xhtml` + `.txt` | Pokyny Komise k čl. 6a (2021/C 526/02) | Cellar `celex/52021XC1229(06)` |
| `predpisy-cz/cz-526-1990-zakon-o-cenach.html` + `.txt` | zákon o cenách, znění od 13. 5. 2026 | https://www.zakonyprolidi.cz/cs/1990-526 |
| `predpisy-cz/cz-291-2024-vyhlaska-merna-cena.html` + `.txt` | vyhláška č. 291/2024 Sb. | https://www.zakonyprolidi.cz/cs/2024-291 |
| `predpisy-cz/mf-stanovisko-merne-ceny-2024-11.pdf` + `.txt` | stanovisko MF k měrným cenám | https://mf.gov.cz/assets/attachments/2024-11-06_Stanovisko-merne-ceny.pdf |
| `predpisy-cz/mf-stanovisko-merne-ceny-doplneni-2025-04.pdf` + `.txt` | doplnění stanoviska MF | https://mf.gov.cz/assets/attachments/2025-04-03_Stanovisko-Merne-ceny-doplneni-2025.pdf |
| `predpisy-cz/cz-387-2024-zakon-o-obecne-bezpecnosti-vyrobku.html` + `.txt` | český prováděcí zákon ke GPSR, znění od 1. 7. 2025 | https://www.zakonyprolidi.cz/cs/2024-387 |
| `predpisy-cz/cz-65-2017-zakon-o-ochrane-zdravi-pred-navykovymi-latkami.html` + `.txt` | zákon č. 65/2017 Sb., znění od 1. 7. 2025 | https://www.zakonyprolidi.cz/cs/2017-65 |
| `predpisy-sk/sk-83-2025-vseobecna-bezpecnost-vyrobkov-zneni-od-2025-05-01.html` + `.txt` | slovenský prováděcí zákon ke GPSR | https://static.slov-lex.sk/static/SK/ZZ/2025/83/20250501.html |

**Soubory, které souběžně stáhl jiný běh a které jsem použil.** Svoje identické duplikáty jsem smazal. Tytéž předpisy mohl ve stejnou dobu uložit i souběžný běh pod stejným názvem.
- `eu-komise/komise-pokyny-ucpd-2021-cs.*` (pokyny k SNOP 2021/C 526/01)
- `predpisy-eu/eu-2025-1960-harmonizovane-oznameni-a-stitek-cs.*`
- `predpisy-eu/eu-2023-988-gpsr-bezpecnost-vyrobkov-sk.*`
- `predpisy-sk/sk-18-1996-o-cenach-zneni-od-2026-01-01.*`

`podklady/README.md` jsem neupravoval.

## Nejistoty a NEOVĚŘENO

1. SOI neuvádí četnosti porušení podle ustanovení u e-shopů. Slovenské priority proto opírám o české údaje a o celotržní cenové akce SOI.
2. Za 4. čtvrtletí nejsou samostatné zprávy. Čtvrtletní zprávy navíc neuvádějí vždy stejná ustanovení („–" v tabulce).
3. Akce Slevy se týká celého trhu. Podíl e-shopů zprávy neuvádějí.
4. Pro GPSR ČOI počty neuvádí, jen že jde o nejčastěji porušovaný „ostatní" předpis v 1Q 2026 (62 případů ostatních předpisů celkem).
5. Oficiální PDF pokynů Komise ke GPSR (C(2025) 7699, webgate.ec.europa.eu) nebylo dostupné, server přesměrovával na stránku „sorry". Použil jsem verzi z Úředního věstníku C/2025/6233. Že jde o tentýž text, soudím podle shodného názvu, text jsem nesrovnával.
6. **NEOVĚŘENO** (předpisy nestaženy nebo nejasné):
   - zda stačí údaje podle GPSR za odkazem,
   - status zemí EHP pro odpovědnou osobu,
   - varování u hraček a podle CLP,
   - rozsudek SDEU C‑330/23 přímo,
   - slovenský zákon č. 377/2004 Z. z.,
   - výjimky a oficiální názvy vláken v nařízení č. 1007/2011,
   - pravidla pro obuv,
   - kontrolní součet IČO,
   - podrobnosti energetického štítku na internetu,
   - výslovné potvrzení české měrné ceny pro e-shopy.
7. Český § 12a nepožaduje výslovně, aby se procento slevy počítalo z předchozí ceny. Kontrola procenta v B.5 stojí na výkladu Komise a ČOI. Slovenský § 7 ods. 1 to požaduje výslovně.
8. EmpCo (B.12) platí jen na Slovensku od 27. 9. 2026. V Česku čeká na transpozici, sněmovní tisk 53.
9. Všechna pravidla v části B jsou návrh k odladění a právní kontrole, ne hotový výklad zákona.
