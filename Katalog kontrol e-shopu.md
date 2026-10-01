# Katalog kontrol textů e-shopu

25. 9. 2026 · návrh k právní kontrole · podklad pro sady otázek v `eshop-guard/rules/`

## K čemu katalog je

Katalog je soupis toho, co zákony a výsledky kontrol ČOI a SOI po e-shopu chtějí a co z toho jde ověřit z textu webu. Z něj se vybírá, co bude nástroj kontrolovat. U každé kontroly je uvedeno, čím se ověřuje: otázkou pro Jev, kontrolou kódem, nebo že to z textu webu nejde.

Katalog tvoří tři části:

- **tento přehled**: priority, co se kontroluje a proč, co je potřeba doplnit v nástroji,
- **`Katalog kontrol – seznam otázek.md`**: všech 190 navržených otázek česky i anglicky,
- **rešerše v `podklady/reserse/`**: doslovné citace předpisů, regulární výrazy, příklady vět a návrhy pravidel v YAML; tam se právník podívá, když chce vidět znění.

Čísla ustanovení jsou doložená citací ze staženého znění předpisu (citace strojově porovnané se soubory). Výklad a znění otázek musí potvrdit právník, proto mají všechny odkazy status „ověřit“.

## Jak katalog číst

**Tři druhy textu.** Nástroj posílá různé otázky na různé texty:

- **A. Věty z popisků zboží a dalších textů** (produktové stránky, úvodní stránka, patička). Hledá tvrzení, za která hrozí pokuta, třeba „Tato mast vyléčí lupénku.“ Každá věta jde Jevu i se sousedními větami.
- **B. Odstavce obchodních podmínek, reklamačního řádu, kontaktu a dopravy.** Hledá, jestli e-shop neopomněl povinný údaj. Stačí, když je údaj aspoň v jednom odstavci na webu.
- **C. Povinné údaje přímo u produktu** (výrobce, jednotková cena, informace o ověřování recenzí). Mimo zvolený rozsah, uvedeno jako kandidáti, protože je ČOI často pokutuje.

**Čím se kontroluje.**

- **Jev:** otázka ano/ne k větě nebo odstavci. Otázka popisuje jen to, co je v textu vidět; paragrafy v ní nejsou, patří k pravidlu a do zprávy.
- **Kód:** regulární výraz nebo seznam. Čísla, IČO, telefony, e-maily, lhůty a ceny zjišťuje vždy kód, nikdy Jev.
- **Z textu nelze:** vyžaduje košík nebo pokladnu (nástroj je neprochází), e-mail po objednávce, historii cen, obrázky nebo fakta mimo web.

**K ověření.** U mnoha tvrzení je z textu vidět jen to, že zaznělo, ne jestli je pravdivé („jen dnes“: platí cena i zítra?). Takový nález je vždy „k ověření“ a zpráva to říká.

**Priorita.** A = časté na e-shopech a Jev to pozná; B = časté jen u části e-shopů nebo slabší signál; C = okrajové. U povinných údajů podle počtu případů u ČOI za roky 2024 a 2025.

## Souhrn

| Oblast | Položek | Jev | Jev + kód | Kód | Z textu nelze | Už v nástroji |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| A. Tvrzení ve větách (černá listina, eko) | 47 bodů | 31 (9 rozhodne text, 22 k ověření) | – | 1 | 15 | 8 eko a 10 ucp pravidel |
| B1. Povinné údaje, Česko | 66 | 22 | 17 | 13 | 13 | 4 (reklamace, odstoupení, formulář, ADR) |
| B2. Povinné údaje, Slovensko | 73 | 19 | 22 | 19 | 13 | 4 (reklamace, odstoupení, formulář, ADR) |
| C. Údaje na produktové stránce | 13 | 2 | 9 | 1 | 1 | – |

Navržených otázek pro Jev: A 45, B1 61, B2 53, C 31, celkem 190. V nástroji je dnes 30 otázek (12 eko, 14 ucp a 4 pro právní stránky), z navržených tedy 19.

Cena podle rešerše: sada nejdůležitějších 14 otázek části A vyjde asi na 0,84 USD za 10 000 vět, všech 39 otázek černé listiny asi na 2,1 USD. Otázky části B se posílají jen s odstavci právních stránek (desítky na e-shop), jejich cena je zanedbatelná.

## Priority podle skutečných kontrol

Počty jsou z kontrolní akce ČOI „Internetový prodej“ za roky 2024 a 2025 (rešerše `coi-priority-a-produktova-stranka.md`, část A). Jeden případ je jedno zjištěné porušení. ČOI vybírá e-shopy cíleně podle screeningu, čísla tedy ukazují, co se najde u rizikových e-shopů.

| # | Co | Část | Případy 2024 + 2025 | Kontrola | Stav |
| ---: | --- | :---: | ---: | --- | --- |
| 1 | Informace o reklamacích (§ 13 ZOS) | B | 773 | Jev | hotovo |
| 2 | Odstoupení od smlouvy a vzorový formulář (§ 1820 odst. 1 písm. i) OZ) | B | 495 | Jev + kód | hotovo |
| 3 | Znění smlouvy a podmínek v textové podobě po objednávce (§ 1827 odst. 2 OZ) | – | 464 | z textu nelze (e-mail) | – |
| 4 | Klamavé konání obecně (§ 5 ZOS) | A | 370 | jen cílené kontroly z černé listiny | priorita A hotovo (modul `ucp`) |
| 5 | Tlačítko „objednávka zavazující k platbě“ (§ 1826a odst. 2 OZ) | – | 328 | z textu nelze (pokladna) | – |
| 6 | Mimosoudní řešení sporů (§ 14 ZOS) | B | 309 | Jev | hotovo |
| 7 | Recenze: informace, zda a jak se ověřují (§ 5a odst. 5 ZOS) | C | 149, k tomu 68 v samostatných akcích | Jev + kód | navrženo |
| 8 | Platba, dodání, vyřizování stížností (§ 1820 odst. 1 písm. h) OZ) | B | 109 | Jev | navrženo |
| 9 | Sleva bez nejnižší ceny za 30 dní (§ 12a ZOS) | C | 89; v celotržní akci Slevy 694, pokuty 38,7 mil. Kč za rok 2025 | kód + Jev | navrženo |
| 10 | Kdy nelze odstoupit (§ 1820 odst. 1 písm. l) OZ) | B | 47 (jen 2025) | Jev | navrženo |
| 11 | Adresa, telefon, e-mail (§ 1820 odst. 1 písm. c) OZ) | B | 46 (jen 2025) | kód + Jev | navrženo |
| 12 | Cena podle cenových předpisů (§ 12 ZOS) | C | 42 (jen 2025) | kód | navrženo |
| 13 | Údaje o výrobci podle nařízení o bezpečnosti výrobků (GPSR) | C | bez čísla; od 1. čtvrtletí 2026 „nejčastěji“ porušovaný „ostatní“ předpis | Jev + kód | navrženo |

Klamavé praktiky ČOI podle druhu nerozlišuje. Pořadí v části A proto stojí na tom, co je na e-shopech běžné a co Jev z věty pozná (rešerše `cerne-listiny-a-eko.md`). Slovenská SOI četnosti podle ustanovení u e-shopů nezveřejňuje; její data podporují hlavně slevy a jednotkovou cenu.

## Část A: tvrzení ve větách popisků a dalších textů

Černá listina obsahuje praktiky zakázané za všech okolností: 47 bodů v příloze I směrnice 2005/29/ES po změně směrnicí EmpCo. Slovensko má od 27. 9. 2026 všech 47, Česko 34, protože 12 bodů z EmpCo (eko tvrzení, bod 10a a body 23d–23j) zatím nepřevzalo (sněmovní tisk 53 neschválen). V Česku se na tyto body dá jít jen přes obecný zákaz klamání (§ 5 ZOS), případ od případu; zpráva to u českých nálezů musí říct.

### A1. Eko tvrzení (hotovo, sada `eco`, verze draft7)

| Pravidlo | Co hledá | Stav |
| --- | --- | --- |
| `eco_generic_claim` | Obecné tvrzení („ekologický“, „šetrný k přírodě“) bez upřesnění hned u něj; výjimka pro uznanou ekoznačku na stránce a pro certifikované biopotraviny | hotovo, ověřeno proti Jevu |
| `eco_sustainable_claim` | „Udržitelný“, „odpovědný“, „uvědomělý“ bez upřesnění; bez výjimky pro ekoznačku (odůvodnění 10) | hotovo, ověřeno proti Jevu |
| `eco_neutrality` | Tvrzení o klimatu produktu (neutrální, snížený či pozitivní dopad) založené na kompenzacích (bod 4c) | hotovo, ověřeno proti Jevu |
| `eco_climate_claim_unsupported` | Tvrzení o klimatu bez uvedeného základu (bod 4a, k ověření) | hotovo |
| `eco_company_climate_claim` | Klimatická neutralita celé firmy založená na kompenzacích (bod 4a, ne 4c; k ověření) | hotovo, ověřeno proti Jevu |
| `eco_label_unrecognized` | Značka udržitelnosti mimo seznam certifikovaných, včetně sociálních značek | hotovo, ověřeno proti Jevu |
| `eco_part_as_whole` | Výhoda části (obal, doprava) vydávaná za výhodu celého produktu | hotovo, rozděleno na dvě otázky |
| `eco_future_claim` | Budoucí environmentální závazek (čl. 6 odst. 2 písm. d), k ověření) | hotovo, ověřeno proti Jevu |

Opravy navržené rešerší jsou zapracované (draft6) a odladěné proti Jevu (draft7): neutralita rozdělená podle produktu, firmy a kompenzací, snížený a pozitivní dopad na klima, „udržitelný“ bez výjimky pro ekoznačku, sociální značky a budoucí závazky. Pro Slovensko pravidla cituji prílohu č. 1 zákona č. 108/2024 Z. z. ve znění od 27. 9. 2026. Pro Česko cituji § 4 a § 5 ZOS a zpráva říká, že jde o posouzení klamavého konání případ od případu, dokud Česko směrnici nepřevezme. Podrobnosti, včetně naměřených pravděpodobností před a po úpravách, jsou v `eshop-guard/rules/CHANGELOG.md`.

<details><summary>Původní návrh oprav z rešerše</summary>


Rešerše navrhuje sadu opravit a doplnit:

1. **Neutralita:** zákaz v bodě 4c platí jen pro tvrzení o **produktu** založená na **kompenzacích**. Tvrzení o firmě jako celku posuzuje Komise jinak (Q&A, otázka 10), takže vysvětlení „zakázané vždy“ u ní neplatí. Návrh: dvě nové otázky `eco_offset_basis` (tvrzení stojí na kompenzacích) a `eco_company_level` (týká se firmy, ne produktu) a dvě pravidla: produkt s kompenzacemi (vysoká závažnost) a firma (k ověření).
2. **Snížený a pozitivní dopad:** `eco_neutral` dnes pokrývá jen „neutrální“, bod 4c zakazuje i „snížený“ a „pozitivní“ dopad na klima založený na kompenzacích. Doplnit znění otázky.
3. **„Udržitelný“, „odpovědný“:** tato slova nejde opřít jen o ekoznačku (odůvodnění EmpCo, bod 10), výjimka podle seznamu značek na stránce u nich nemá platit. Nová otázka `eco_sustainable_term`.
4. **Sociální značky:** bod 2a platí i pro značky férového obchodu nebo dobrých životních podmínek zvířat. Nová otázka `eco_social_label`.
5. **Budoucí závazky** („uhlíkově neutrální do roku 2030“): posuzují se případ od případu a potřebují veřejný prováděcí plán. Nová otázka `eco_future_claim`, nález k ověření.

</details>

### A2. Klamavé praktiky z černé listiny (nový modul `ucp`)

**Priorita A: časté na e-shopech a Jev je pozná.** Hotovo jako modul `ucp` (`rules/ucp.yaml`, draft3, 26. 9. 2026). Deset pravidel, všechna nastražené věty z tabulky na testovacím e-shopu najdou a věty „bez nálezu“ nechají být (ověřeno proti Jevu). Léčebná tvrzení zatím rozlišují jen lék nebo zdravotnický prostředek; rozlišení potravin, doplňků stravy a kosmetiky podle odvětvových předpisů čeká na rešerši.

| Kontrola | Body (EU / CZ / SK) | Nález | Bez nálezu | Otázky | Výsledek |
| --- | --- | --- | --- | --- | --- |
| Zákonná práva jako výhoda obchodu | 10 / př. 1 i) / 14 | „14 dní na vrácení zboží bez udání důvodu.“ pod nadpisem „Proč nakoupit u nás?“ | „U nás máte na vrácení zboží 30 dní.“ (nad rámec zákona) | `ucp_legal_right`, `ucp_shop_advantage`, `ucp_beyond_legal_minimum` | rozhodne text, vysoká závažnost |
| „Zdarma“ s poplatkem | 20 / př. 1 t) / 26 | „Vzorek zdarma, stačí uhradit balné 29 Kč.“ | „Doprava zdarma při nákupu nad 1 500 Kč.“ | `ucp_free`, `ucp_free_extra_fee`, `ucp_free_with_purchase` | rozhodne text; dárek podmíněný nákupem k ověření |
| Falešná naléhavost | 7 / př. 1 f) / 11 | „Jen dnes o 30 % levněji!“, „Poslední 2 kusy.“ | „Skladem.“ | `ucp_urgency_time`, `ucp_urgency_stock` | k ověření; jistotu dá až opakovaný sken (úprava nástroje) |
| Léčebná tvrzení | 17 / př. 1 q) / 23 | „Bylinná mast vyléčí lupénku během 14 dnů.“ | „Krém hydratuje a zklidňuje suchou pokožku.“ | `ucp_cure`, `ucp_is_medicine` | vysoká závažnost, k ověření; u léků nízká |
| Recenze: odměna za kladné hodnocení, jen kladné recenze | 23c / př. 1 z) / 32 | „Za hodnocení 5 hvězdičkami vám vrátíme 100 Kč.“, „Zveřejňujeme pouze pozitivní recenze.“ | „Napište recenzi, moc nám pomůže.“ | `ucp_review_reward`, `ucp_review_reward_positive`, `ucp_reviews_only_positive` | rozhodne text |
| Recenze: „ověření zákazníci“ | 23b / př. 1 y) / 31 | „Všechny recenze jsou od ověřených zákazníků.“ | „Napište nám recenzi.“ | `ucp_reviews_verified_claim` | k ověření (opatření nejsou na webu vidět) |

**Priorita B: časté jen u části e-shopů nebo slabší signál** (všechny k ověření, podrobnosti v rešerši):

- značky důvěry, certifikáty a ocenění obchodu (`ucp_trust_mark`) a tvrzení o schválení úřadem nebo doporučení odborníky (`ucp_approval_claim`),
- tvrzení o legalitě produktu, typicky CBD nebo kratom (`ucp_legality_claim`),
- zákonný požadavek vydávaný za přednost, např. „kojenecká láhev bez BPA“ (`ucp_legal_requirement_as_feature`, bod 10a, jen SK; potřebuje seznam, který sestaví právník),
- „totální výprodej, končíme“ (`ucp_closing_down`) a „nejnižší cena na trhu, jinde nekoupíte“ (`ucp_market_conditions`),
- trvanlivost, opravitelnost a strašení neoriginálními díly (`ucp_durability_claim`, `ucp_repairability_claim`, `ucp_non_original_parts_claim`, body 23g, 23h, 23j, jen SK),
- přímá výzva dětem ke koupi (`ucp_children_direct_exhortation`, rozhodne text) a výhra, za kterou se platí (`ucp_false_prize_win`).

**Priorita C** (okrajové): kodexy chování, strašení bezpečností, záměna s výrobcem, pyramidové hry, hazard, soutěže, obchodník vydávající se za spotřebitele, servis v jiném státě.

**Z textu nelze (15 bodů):** vábivá reklama a „bait and switch“ (potřebují sklad a historii), skrytá reklama a placené pořadí ve vyhledávání, faktury v zásilkách, přeprodej vstupenek, body o aktualizacích softwaru a spotřebním materiálu a agresivní praktiky v osobním nebo telefonickém kontaktu.

**Zapojení do nástroje.** Rešerše navrhuje tři sady podle priority, protože Jev dostává v jednom volání všechny otázky sady: `ucp_a` (14 otázek, body 7, 10, 17, 20, 23b, 23c), `ucp_b` (17 otázek) a `ucp_c` (8 otázek). Funguje to s dnešním nástrojem; vliv počtu otázek v jednom volání na přesnost je potřeba změřit (M4).

## Část B: povinné údaje na právních stránkách

Hotové čtyři kontroly (reklamace, odstoupení, formulář, ADR) pokrývají tři nejčastější nálezy ČOI. Návrhy níže doplňují zbytek předsmluvních informací. Údaje „jev + kód“ kontroluje Jev (o čem odstavec je, komu adresa patří) a kód (číslo, IČO, PSČ). U kontroly „nelze z textu“ je v rešerši důvod.

Nejvíc práce ušetří:

- **Identita a kontakt** (jméno, adresa, IČO, telefon, e-mail): hlavně kód nad celým webem včetně patičky; Jev jen rozliší adresu prodávajícího od adresy ČOI nebo dopravce.
- **§ 1820 odst. 1 písm. h) a l) OZ** (platba a dodání, výjimky z odstoupení): čistě Jev, 109 a 47 případů u ČOI.
- **§ 1826 odst. 1 OZ** (elektronické uzavírání smlouvy: uložení smlouvy, jazyky, kroky objednávky, oprava chyb): čistě Jev, v obchodních podmínkách časté a snadno kontrolovatelné.
- **Tlačítko pro odstoupení:** na Slovensku podle § 20a od 19. 6. 2026, v Česku podle rešerše § 1830a OZ ve znění zákona č. 159/2026 Sb. od 1. 1. 2027 (ověřit). Tlačítko může být za přihlášením, nález proto nejvýš k ověření.

### B1. Česko

Sloupec ČOI uvádí počet případů u daného ustanovení (u písmene § 1820 celkem, ne u jednotlivé kontroly).

**A. Identita a kontakt podnikatele**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_trader_name` | § 435 odst. 1 OZ; § 1820 odst. 1 písm. b) OZ; § 5a odst. 3 písm. b) ZOS | jev + kód | Kontakt, obchodní podmínky, patička |  |
| `cz_trader_address` | § 435 odst. 1 OZ; § 1820 odst. 1 písm. c) OZ | jev + kód | Kontakt, obchodní podmínky, patička | 46 (jen 2025) |
| `cz_trader_ico` | § 435 odst. 1 OZ; § 7 odst. 2 a 3 ZOK | kód | Kontakt, obchodní podmínky, patička |  |
| `cz_trader_register` | § 435 odst. 1 OZ; § 7 odst. 2 a 3 ZOK | kód | Kontakt, obchodní podmínky, patička |  |
| `cz_trader_phone` | § 1820 odst. 1 písm. c) OZ | kód | Kontakt, obchodní podmínky, patička | 46 (jen 2025) |
| `cz_trader_email` | § 1820 odst. 1 písm. c) OZ | kód | Kontakt, obchodní podmínky, patička | 46 (jen 2025) |
| `cz_trader_other_channel` | § 1820 odst. 1 písm. c) OZ | nelze z textu | Kontakt | 46 (jen 2025) |
| `cz_trader_principal` | § 1820 odst. 1 písm. c) a d) OZ | jev + kód | obchodní podmínky, Kontakt | 46 (jen 2025) |
| `cz_establishment_address` | § 1820 odst. 1 písm. d) OZ | nelze z textu | Kontakt, Doprava |  |
| `cz_premium_rate_contact` | § 1820 odst. 1 písm. g) OZ; § 3a ZOS | kód | Kontakt, reklamační řád, patička |  |

**B. Výrobek a cena**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_product_characteristics` | § 1820 odst. 1 písm. a) OZ; § 5a odst. 3 písm. a) ZOS | jev + kód | produktové stránky |  |
| `cz_price_total` | § 1820 odst. 1 písm. e) OZ; § 12 ZOS; § 13 odst. 2 a 3 zák. 526/1990 | kód | produktové stránky | 42 (jen 2025) |
| `cz_unit_price` | § 13 odst. 4 až 7 zák. 526/1990; vyhl. 291/2024 | kód | produktové stránky |  |
| `cz_delivery_costs` | § 1820 odst. 1 písm. e) OZ; § 1821 OZ | jev + kód | Doprava a platba, obchodní podmínky |  |
| `cz_subscription_price` | § 1820 odst. 1 písm. e) OZ | jev + kód | předplatné, obchodní podmínky |  |
| `cz_price_personalization` | § 1820 odst. 1 písm. f) OZ | nelze z textu | u ceny, obchodní podmínky |  |
| `cz_discount_prior_price` | § 12a ZOS | kód | produktové stránky, akce | 89 (+ 694 v akci Slevy) |

**C. Platba, dodání a stížnosti**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_payment_methods` | § 1820 odst. 1 písm. h) OZ; § 11a ZOS | jev | Doprava a platba, obchodní podmínky | 109 |
| `cz_delivery_methods` | § 1820 odst. 1 písm. h) OZ | jev | Doprava a platba, obchodní podmínky | 109 |
| `cz_delivery_time` | § 1820 odst. 1 písm. h) OZ; § 2159 odst. 1 OZ | jev + kód | Doprava a platba, produktové stránky | 109 |
| `cz_delivery_restrictions` | § 11a ZOS | jev | Doprava a platba, obchodní podmínky |  |
| `cz_cross_border_delivery` | čl. 7 nař. 2018/644; § 24 odst. 6 písm. b) ZOS | jev + kód | Doprava a platba |  |
| `cz_complaint_handling` | § 1820 odst. 1 písm. h) OZ; § 1811 odst. 2 písm. d) OZ | jev | obchodní podmínky, Kontakt | 109 |
| `cz_advance_payment` | § 1820 odst. 1 písm. q) OZ | jev | obchodní podmínky, Doprava a platba |  |

**D. Odstoupení od smlouvy**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_withdrawal` (hotovo) | § 1820 odst. 1 písm. i) OZ; § 1829 odst. 1 OZ | jev + kód | obchodní podmínky, Odstoupení | 495 |
| `cz_withdrawal_form` (hotovo) | § 1820 odst. 1 písm. i) OZ; NV 29/2023 | jev | obchodní podmínky, Odstoupení, PDF | 495 |
| `cz_withdrawal_period_start` | § 1829 odst. 1 a 2 OZ | jev + kód | obchodní podmínky, Odstoupení |  |
| `cz_withdrawal_return_costs` | § 1820 odst. 1 písm. j) OZ; § 1832 odst. 3 OZ | jev + kód | obchodní podmínky, Odstoupení |  |
| `cz_withdrawal_service_payment` | § 1820 odst. 1 písm. k) OZ | jev | obchodní podmínky |  |
| `cz_withdrawal_exceptions` | § 1820 odst. 1 písm. l) OZ; § 1837 OZ | jev | obchodní podmínky, Odstoupení | 47 (jen 2025) |
| `cz_withdrawal_button` | § 1820 odst. 1 písm. i) a § 1830a OZ ve znění 159/2026 (od 1. 1. 2027) | jev + kód | celý web, účet, obchodní podmínky | 495 |

**E. Vady, reklamace a záruky**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_complaints` (hotovo) | § 13 odst. 1 ZOS; § 1820 odst. 1 písm. m) OZ | jev | reklamační řád, obchodní podmínky | 773 |
| `cz_complaint_place` | § 13 odst. 1 ZOS; § 2172 OZ | jev + kód | reklamační řád, obchodní podmínky | 773 |
| `cz_defect_period` | § 2165 odst. 1 OZ; § 2168 OZ | kód | reklamační řád, obchodní podmínky |  |
| `cz_guarantee_after_sales` | § 1820 odst. 1 písm. m) OZ; § 2174a OZ | jev | reklamační řád, produktové stránky |  |

**F. Další údaje podle § 1820 OZ a mimosoudní řešení sporů**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_code_of_conduct` | § 1820 odst. 1 písm. n) OZ | jev | obchodní podmínky, patička |  |
| `cz_contract_duration` | § 1820 odst. 1 písm. o) OZ | jev + kód | obchodní podmínky, předplatné |  |
| `cz_minimum_duration` | § 1820 odst. 1 písm. p) OZ | kód | obchodní podmínky, předplatné |  |
| `cz_digital_compatibility` | § 1820 odst. 1 písm. r) OZ; § 1811 odst. 2 písm. h) a i) OZ | jev | produktové stránky |  |
| `cz_adr` (hotovo) | § 14 odst. 1 ZOS; § 1820 odst. 1 písm. s) OZ | jev | obchodní podmínky, reklamační řád | 309 |
| `cz_supervisory_authority` | § 1820 odst. 1 písm. s) OZ | jev | obchodní podmínky |  |

**G. Elektronické uzavírání smlouvy a objednávka**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_contract_storage` | § 1826 odst. 1 písm. a) OZ | jev | obchodní podmínky |  |
| `cz_contract_languages` | § 1826 odst. 1 písm. b) OZ | jev | obchodní podmínky |  |
| `cz_order_steps` | § 1826 odst. 1 písm. c) OZ | jev | obchodní podmínky, Jak nakupovat |  |
| `cz_input_error_correction` | § 1826 odst. 1 písm. d) OZ | jev | obchodní podmínky, Jak nakupovat |  |
| `cz_order_review` | § 1826 odst. 2 OZ | nelze z textu | pokladna |  |
| `cz_pre_order_summary` | § 1826a odst. 1 OZ | nelze z textu | pokladna |  |
| `cz_order_button` | § 1826a odst. 2 OZ | nelze z textu | pokladna | 328 |
| `cz_order_confirmation_terms` | § 1824a odst. 1, § 1827 OZ | nelze z textu | e-mail po objednávce | 464 |
| `cz_extra_payment_consent` | § 1817 OZ | nelze z textu | košík, pokladna |  |

**H. Jazyk a forma**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_language` | § 11 odst. 1 ZOS; § 1811 odst. 1 OZ | kód | produktové stránky, právní stránky |  |
| `cz_units` | § 11 odst. 3 ZOS | kód | produktové stránky |  |
| `cz_legibility` | § 1824 odst. 1 OZ | nelze z textu | všude |  |

**I. Recenze a on-line tržiště**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_reviews_verification` | § 5a odst. 5 ZOS | jev + kód | produktové stránky, recenze, obchodní podmínky | 149 |
| `cz_marketplace_ranking` | § 11b písm. a) ZOS; § 5a odst. 4 ZOS | jev | tržiště: řazení, obchodní podmínky |  |
| `cz_marketplace_seller_status` | § 11b písm. b) až d) ZOS; § 5a odst. 3 písm. f) ZOS | jev | tržiště: produktové stránky, obchodní podmínky |  |

**J. Podle druhu zboží a sortimentu**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_used_goods` | § 10 ZOS | nelze z textu | produktové stránky |  |
| `cz_footwear_materials` | § 10a odst. 1 ZOS | jev | produktové stránky obuvi |  |
| `cz_use_instructions` | § 9 ZOS | nelze z textu | s výrobkem |  |
| `cz_deposit_packaging` | § 18 odst. 1 ZOS | jev + kód | produktové stránky nápojů |  |
| `cz_best_before` | § 2163 OZ | nelze z textu | na výrobku |  |
| `cz_gpsr_manufacturer` | čl. 19 písm. a) a b) nař. 2023/988 | jev + kód | produktové stránky |  |
| `cz_gpsr_product_id` | čl. 19 písm. c) nař. 2023/988 | kód | produktové stránky |  |
| `cz_gpsr_warnings` | čl. 19 písm. d) nař. 2023/988 | nelze z textu | produktové stránky |  |
| `cz_take_back` | § 18 odst. 3 zák. 542/2020 | jev | Zpětný odběr, obchodní podmínky |  |

**K. Připravované povinnosti**

| Kontrola | Ustanovení | Jak | Kde | ČOI 2024+25 |
| --- | --- | --- | --- | ---: |
| `cz_empco_pending` | čl. 2 směrnice 2024/825 (v ČR netransponováno) | zatím neaktivovat | produktové stránky, Doprava a platba |  |

### B2. Slovensko

Slovenská sada je od 26. 9. 2026 zapnutá (Slovensko je primární trh a výchozí země nástroje); ustanovení má dál status „ověřit“ pro právníka. Rešerše k ní přidává i kontroly **správnosti** údajů (web uvádí o právech zákazníka něco v rozporu se zákonem, třeba 30 dní místo 14 nebo vyloučení zlevněného zboží z odstoupení); nejsou to informační povinnosti, ale časté chyby.

**A. Identita a kontakt obchodníka**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_trader_identity` | § 5/1 b) Z108; § 4/1 a) Z22; § 3a ObZ | jev + kód | kontakt, VOP, patička |
| `sk_trader_ico` | § 3a/1, 3 ObZ | kód | kontakt, VOP, patička |
| `sk_trader_register` | § 4/1 d) Z22; § 3a/1 ObZ | kód | kontakt, VOP, patička |
| `sk_trader_vat_id` | § 4/1 b) Z22 | kód (+ plátcovství nelze) | kontakt, VOP |
| `sk_trader_phone` | § 5/1 c), § 15/3 Z108; § 4/1 c) Z22 | kód | kontakt, patička |
| `sk_trader_email` | § 15/1 a), § 15/3 Z108; § 4/1 c) Z22 | kód | kontakt, patička |
| `sk_trader_other_online_channel` | § 15/1 b) Z108 | nelze z textu | kontakt |
| `sk_return_complaint_address` | § 15/1 c) Z108; § 622/1 OZ | jev + kód | reklamace, odstoupení |
| `sk_contact_premium_rate` | § 4/2 g), § 15/1 e) Z108 | kód | kontakt |
| `sk_supervisory_authority` | § 4/1 e) Z22 | jev + kód | VOP, kontakt |
| `sk_info_permanently_accessible` | § 4/3 Z22 | kód | rám všech stránek |

**B. Jazyk**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_language_slovak` | § 4/1 g) Z108; § 5/4 Z22 | kód | právní stránky |

**C. Produkt a cena**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_product_main_characteristics` | § 5/1 a) Z108 | kód (heuristika) | produkt |
| `sk_price_total` | § 5/1 d), § 2 g), § 6/1 Z108; § 15 ZC | kód | produkt |
| `sk_unit_price` | § 6/1, 3, 4, § 2 h) Z108 | kód | produkt |
| `sk_price_reduction_previous_price` | § 7 Z108 | jev + kód (správnost ceny nelze) | produkt, výpis, bannery |
| `sk_delivery_costs` | § 5/1 d), § 15/7 Z108 | jev + kód | doprava a platba |
| `sk_additional_costs_notice` | § 5/1 d) Z108 | jev | doprava, VOP |
| `sk_payment_surcharge` | § 4/2 f) Z108 | jev + kód | doprava a platba |
| `sk_prechecked_paid_options` | § 5/2 Z108 | nelze z textu | košík |

**D. Plnění, platba, dodání**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_delivery_terms_deadline` | § 5/1 e) Z108; § 613/1 OZ | jev + kód | doprava, VOP, produkt |
| `sk_payment_methods` | § 5/1 e), § 17/2 Z108 | jev + kód (košík nelze) | doprava a platba, rám |
| `sk_delivery_restrictions` | § 17/2 Z108 | jev | doprava |
| `sk_eco_delivery_options` | § 15/1 l) Z108 | nelze z textu | doprava, košík |

**E. Zodpovednosť za vady (reklamace) a záruky**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_legal_guarantee_info` | § 5/1 f) Z108; § 622 OZ | jev | reklamace, VOP |
| `sk_legal_guarantee_duration` | § 5/1 f) Z108; § 619 OZ | kód | reklamace, VOP |
| `sk_harmonized_legal_guarantee_notice` | § 5/1 f) Z108; EU1960 | nelze z textu (obrázek), slabý kód | produkt, pokladna, VOP |
| `sk_durability_guarantee_label` | § 5/1 g) Z108; EU1960 | jev + kód | produkt |
| `sk_complaint_handling_deadline` | § 622/3, § 623/4 OZ; § 4/2 e) Z108 | kód | reklamace |
| `sk_defect_notice_period` | § 621/3 OZ; § 4/2 d) Z108 | jev + kód | reklamace, doprava |
| `sk_remedy_choice` | § 623/1, 2 OZ; § 4/2 d), e) Z108 | jev | reklamace |
| `sk_additional_guarantee_terms` | § 5/1 j) Z108; § 626 OZ | jev | produkt, VOP |
| `sk_digital_content_liability` | § 5/1 h) Z108; § 852h OZ | kód | VOP |
| `sk_service_liability` | § 5/1 i) Z108 | jev | VOP |

**F. Opravitelnost a digitální prvky (produktové stránky)**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_repairability_score` | § 5/1 k) Z108 | jev + kód | produkt |
| `sk_spare_parts_repair_info` | § 5/1 l) Z108 | nelze z textu | produkt |
| `sk_digital_functionality_compatibility` | § 5/1 n), o) Z108 | jev | produkt |
| `sk_software_update_period` | § 5/1 p) Z108 | nelze z textu | produkt |
| `sk_manufacturer_repair_info` | § 13b/7 Z108 | nelze z textu | servis |

**G. Smlouvy na dobu, předplatné, záloha, personalizace**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_contract_duration_termination` | § 5/1 m) Z108 | jev | VOP, předplatné |
| `sk_min_commitment_duration` | § 15/1 j) Z108 | jev + kód | VOP, předplatné |
| `sk_deposit_conditions` | § 15/1 k) Z108 | jev | VOP |
| `sk_personalised_price_notice` | § 15/1 d) Z108 | nelze z textu | VOP, cena |

**H. Odstoupení od smlouvy**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_withdrawal_info` | § 15/1 f)–i), § 19 Z108 | jev + kód | VOP, odstoupení |
| `sk_withdrawal_form` | § 15/1 f), § 20/6 Z108; příl. 2 | jev | odstoupení |
| `sk_withdrawal_function` | § 20a Z108 | kód | rám, odstoupení, účet |
| `sk_withdrawal_function_location_info` | § 15/1 f) Z108; příl. 3 | jev + kód | VOP, odstoupení |
| `sk_withdrawal_return_costs` | § 15/1 g), § 15/7, § 21/3 Z108 | jev + kód | VOP, odstoupení |
| `sk_withdrawal_service_payment` | § 15/1 h) Z108 | jev | VOP |
| `sk_withdrawal_exceptions` | § 15/1 i), § 19/1 Z108 | jev | VOP, odstoupení |
| `sk_withdrawal_template_unfilled` | § 15/6 Z108; příl. 3 | kód | VOP, odstoupení |
| `sk_withdrawal_refund_deadline` | § 22/1 Z108 | kód | VOP, odstoupení |
| `sk_withdrawal_restrictive_terms` | § 19/1, § 21/6 Z108 | jev | VOP, odstoupení |

**I. Alternativní řešení sporů**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_adr` | § 5/1 q), § 5/3 Z108 | jev | VOP |
| `sk_adr_redress_request` | § 5/1 q) Z108; § 11 Z391 | jev + kód | VOP, reklamace |

**J. Elektronický obchod (zákon č. 22/2004 Z. z., § 5)**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_ecommerce_order_steps` | § 5/3 b) 1 Z22 | jev | VOP |
| `sk_ecommerce_error_correction` | § 5/3 a), b) 2 Z22 | jev | VOP |
| `sk_ecommerce_contract_storage` | § 5/3 b) 3 Z22 | jev | VOP |
| `sk_ecommerce_contract_language` | § 5/3 b) 4, § 5/4 Z22 | jev + kód | VOP |
| `sk_terms_reproducible` | § 5/5 a) Z22 | kód | VOP |
| `sk_order_confirmation` | § 5/6 Z22; § 17/12 Z108 | nelze z textu | e-mail |

**K. Objednávkový proces (košík a pokladna, nástroj je neprochází)**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_order_button_label` | § 17/4, 6 Z108 | nelze z textu | pokladna |
| `sk_pre_order_summary` | § 17/3 Z108 | nelze z textu | pokladna |
| `sk_service_digital_start_consent` | § 17/10, 12 Z108 | nelze z textu | pokladna |

**L. Akce, recenze, pořadí, online tržiště**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_promo_conditions` | § 4/5 Z22 | jev | bannery, akce |
| `sk_sponsor_identification` | § 4/4 Z22; § 11/2 Z108 | nelze z textu | blog |
| `sk_reviews_verification_info` | § 11/6 a) Z108 | jev + kód | recenze, VOP |
| `sk_search_ranking_parameters` | § 11/6 b), § 16/1 a) Z108 | jev + kód | tržiště |
| `sk_comparison_service_info` | § 11/6 c) Z108 | jev | srovnávač |
| `sk_marketplace_info` | § 11/4 f), § 16/1 b)–d) Z108 | jev + kód | tržiště |

**M. Přístupnost, bezpečnost výrobků, služby**

| Kontrola | Ustanovení | Jak | Kde |
| --- | --- | --- | --- |
| `sk_accessibility_info` | § 6 Z351 | jev + kód | VOP, přístupnost |
| `sk_gpsr_product_listing` | čl. 19 GPSR | jev + kód | produkt |
| `sk_services_act_info` | § 6 Z136 | kód | VOP, kontakt |

## Část C: údaje na produktové stránce (kandidáti mimo zvolený rozsah)

Tyto povinnosti nejsou v popisku ani v obchodních podmínkách, ale přímo na stránce produktu. Nejsou ve zvoleném rozsahu. Podle dat ČOI jsou ale mezi nejdražšími nálezy (recenze, slevy), proto je uvádím jako další krok.

| # | Povinnost | CZ | SK | Kontrola | Závažnost |
| --- | --- | --- | --- | --- | --- |
| C1 | Výrobce: jméno, poštovní a elektronická adresa | GPSR čl. 19 a) | GPSR čl. 19 a) | Jev + kód | vysoká |
| C2 | Odpovědná osoba v EU (výrobce mimo EU) | GPSR čl. 19 b) | stejně | kód + Jev | vysoká |
| C3 | Obrázek a typ výrobku | GPSR čl. 19 c) | stejně | kód, Jev | střední |
| C4 | Varování v jazyce trhu | GPSR čl. 19 d) | stejně | Jev + kód; nutnost varování z textu nelze | střední, k ověření |
| C5 | Nejnižší cena za 30 dní a výpočet slevy | § 12a ZOS | § 7 zákona 108/2024 | kód + Jev; pravdivost ceny z textu nelze | vysoká |
| C6 | Měrná (jednotková) cena | § 13 zákona o cenách, vyhl. 291/2024 | § 6 zákona 108/2024 | kód | střední |
| C7 | Konečná cena včetně daní, informace o dopravě | § 13 zákona o cenách; § 1820 odst. 1 písm. e) OZ | § 5, § 11 zákona 108/2024 | kód, doprava Jev | střední |
| C8 | Informace, zda a jak se ověřují recenze (přímo u recenzí) | § 5a odst. 5 ZOS | § 11 ods. 6 písm. a) | Jev + kód | vysoká |
| C9 | Hlavní vlastnosti výrobku | § 1811, § 1820 a) OZ | § 5 a) | Jev | nízká, k ověření |
| C10 | Materiálové složení textilu | nařízení 1007/2011 čl. 16 | stejně | Jev + kód | střední |
| C11 | Alkohol a tabák: zákaz prodeje mladším 18 let a IČO | zákon 65/2017 § 6, § 15 | ověřit | kód + Jev | střední |
| C12 | Štítek GARAN (záruka výrobce nad 2 roky) | čeká na transpozici | § 5 ods. 1 písm. g) od 27. 9. 2026 | štítek je obrázek, z textu jen podmínka | nízká, k ověření |
| C13 | Energetický štítek | nařízení 2017/1369 | stejně | z textu nelze (obrázek) | – |

Část C potřebuje v nástroji nový způsob skládání `page_presence`: údaj musí být na každé produktové stránce a zpráva hlásí „chybí na X z N produktových stránek“.

## Co z textu webu zkontrolovat nejde

- **Pokladna a e-mail po objednávce:** znění smlouvy v textové podobě (464 případů u ČOI), tlačítko „objednávka zavazující k platbě“ (328), souhrn před objednávkou, předem zaškrtnuté placené doplňky. Nástroj košík ani pokladnu neprochází.
- **Pravdivost cenových údajů:** zda uvedená nejnižší cena za 30 dní odpovídá skutečnosti, vyžaduje historii cen.
- **Obrázky:** loga, certifikáty, energetický štítek, štítek GARAN, harmonizované oznámení o zákonné záruce. Jev obrázky nevidí; nástroj je jen vypíše k ruční kontrole podle alt textu a názvu souboru.
- **Fakta mimo web:** zda je výrobek skutečně léčivý, zda existuje schválení nebo certifikát, zda firma opravdu končí, jak dlouho reklamace trvá.
- **Agresivní praktiky mimo web:** telefonáty, návštěvy, obtěžování.

## Co je potřeba doplnit v nástroji

Rešerše se v tomto shodují. Bez těchto úprav jde do YAML zapsat jen část kontrol:

1. **`page_presence`**: údaj musí být na každé produktové stránce, s podmínkou (jen když stránka ukazuje recenze, slevu nebo patří do kategorie). Potřebné pro část C.
2. **Podmíněná přítomnost na webu (`site_presence_if`)**: například zobrazuje recenze → musí vysvětlit jejich ověřování; nabízí předplatné → musí uvést dobu trvání.
3. **Vzor kdekoli na webu (`regex_site`)**: IČO, telefon, e-mail, adresa, harmonizované oznámení, včetně patičky, alt textů a odkazů. Dnešní `regex_required` hledá jen v textu právních stránek.
4. **Rám stránky jako zdroj pro právní modul**: IČO a kontakty bývají v patičce, dnes ji právní modul nečte.
5. **Kontroly kódem ve větě** (`regex_present`, `list_present`): například číslo s jednotkou u trvanlivosti nebo seznam látek zakázaných v kategorii pro bod 10a.
6. **Čtení JSON-LD**: cena, platnost ceny, dostupnost, hodnocení a recenze.
7. **Srovnání s předchozím skenem**: „jen dnes“ nebo „končíme“ týdny po sobě změní nález „k ověření“ na jistý.
8. **Kontrolní součet IČO**: pro Česko ověřený algoritmus, pro Slovensko neověřeno.

## Otevřené otázky pro právníka

- **Seznamy značek:** úplný seznam značek s certifikačním systémem (bod 2a) a značek s uznaným vynikajícím profilem (bod 4a); seznam látek a požadavků pro bod 10a.
- **Léčebná tvrzení u potravin a doplňků stravy:** odvětvové předpisy (nařízení 1169/2011 a 1924/2006) nejsou ve stažených podkladech, rešerše je označuje „neověřeno“.
- **IČO na webu** u podnikajících fyzických osob (§ 435 odst. 1 OZ, věta třetí): výklad nejistý.
- **Výjimka pro pravidelný rozvoz potravin** (§ 1820 a násl. OZ): zda vyřazuje celý pododdíl.
- **Tlačítko pro odstoupení v Česku od 1. 1. 2027** (zákon č. 159/2026 Sb.): potvrdit.
- **Česká transpozice EmpCo** (sněmovní tisk 53): zda převezme i nové informační povinnosti (harmonizované oznámení, štítek GARAN).
- **Slovensko:** umístění harmonizovaného oznámení na webu, kontrolní součet IČO, jednotková cena za 100 g, zda se zákon 136/2010 Z. z. vztahuje na e-shop se zbožím.
- **Znění všech otázek:** jedna otázka = jeden znak, bez právních závěrů; právník potvrdí, že znaky odpovídají ustanovení.

## Doporučený postup

1. **Právník projde části A a B** (ustanovení, otázky, příklady vět) a vybere, co zapracovat.
2. **Implementace po sadách:** ~~nejdřív `ucp_a` (6 bodů, 14 otázek) a opravy sady `eco`~~ (hotovo 26. 9. 2026), pak z části B identita a kontakt, § 1820 odst. 1 písm. h) a l) a § 1826 odst. 1 OZ.
3. **Označený vzorek pro každou novou otázku** (milník M4): příklady „nález“ a „bez nálezu“ jsou u každého bodu v rešerši.
4. **Část C** až po rozhodnutí, protože potřebuje `page_presence`.

## Zdroje

- Rešerše (25. 9. 2026): `podklady/reserse/cerne-listiny-a-eko.md`, `cz-informacni-povinnosti.md`, `sk-informacne-povinnosti.md`, `coi-priority-a-produktova-stranka.md`.
- Předpisy a zprávy: `podklady/` (seznam s odkazy v `podklady/README.md`). Nově staženo: konsolidované znění směrnice 2005/29/ES, Pokyny Komise k ní (2021), nařízení 2023/988 (GPSR) a 2025/1960 (harmonizované oznámení), slovenské zákony 22/2004, 18/1996, 513/1991, 391/2015, 351/2022 a 136/2010, české předpisy (mimo jiné zákon o cenách a zákon o obchodních korporacích), čtvrtletní a roční zprávy ČOI 2023–2026 o internetovém prodeji, slevách a recenzích, výroční zprávy ČOI a SOI.
