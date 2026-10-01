# Značky udržitelnosti a certifikace na slovenských a českých e-shopech

26. 9. 2026 · rešerše pro `eshop-guard` (`config/labels.yaml`, pravidla `eco_label_unrecognized` a `eco_generic_claim`) · návrh k právní kontrole, ne právní výklad

## Shrnutí

- **Prověřeno 61 značek a jejich variant** ze sedmi oborů: veřejné ekoznačky, bio, kosmetika a drogerie, zvířata a klima, textil, dřevo a potraviny, další značky z e-shopů. Stav k 26. 9. 2026, jen podle veřejných dokumentů vlastníků, zákonů a akreditačních orgánů. Konkrétní výrobek ani licenci se neověřovalo.
- **Staženo 486 dokumentů** do `podklady/znacky/` (vždy originál a `.txt`) a 1 552 citací v dílčích rešerších. Všechny citace v tomto souboru jsou strojově ověřené proti číslům řádků.
- **Výsledek podle slovenského § 2 písm. o)–q) a prílohy č. 1 bodu 3 zákona 108/2024:**

| Výsledek | Počet | Značky |
| --- | --- | --- |
| vyhovuje: značka orgánu veřejné moci | 12 | EU Ecolabel, EVP, EŠV/EŠS, Nordic Swan, Blauer Engel, Österreichisches Umweltzeichen, EMAS (jen organizace), logo EU pro ekologickou produkci, SK znak ekologickej poľnohospodárskej výroby, CZ „BIO – produkt ekologického zemědělství“, Bio-Siegel, AB |
| vyhovuje: certifikační systém | 13 | COSMOS (i s logem Ecocert, BDIH, ICEA, Cosmébio, Soil Association), NATRUE, GOTS, Textile Exchange (GRS, RCS, OCS, RWS, RDS…), FSC, PEFC, Fairtrade, Rainforest Alliance, MSC, ASC, RSPO, Seedling, UEBT |
| nevyhovuje | 12 | vlastní standardy Ecocert (Ecodetergents, Natural/Organic Cosmetic), vlastní standardy ICEA, CPK a CPK bio, Fair for Life, OK compost a OK biobased, bluesign/bluepass, Cradle to Cradle Certified, The Vegan Society (Veganblume), PETA, EVE VEGAN, Fair Wear, OEKO-TEX STeP na výrobku; k tomu vlastní odznaky bez systému |
| NEOVĚŘENO | 22 | OEKO-TEX STANDARD 100, OEKO-TEX MADE IN GREEN / ORGANIC COTTON / LEATHER STANDARD, V-Label, Leaping Bunny / Cruelty Free International, BDIH Standard („Kontrollierte Natur-Kosmetik“), Vegan BDIH, Ecogarantie, Cosmébio bez COSMOS, „Soil Association organic“ mimo kosmetiku, NCS, NCP, Demeter, Naturland, Bioland, Bio Austria, USDA Organic, ClimatePartner certified, Certified B Corporation, 1 % for the Planet (CERTIFIED), BCI Cotton Label, UTZ, Milieukeur (jen bod 3) |
| netýká se (povinná značka) | 1 | energetický štítek EU |

- **Uznané vynikajúce environmentálne vlastnosti (§ 2 písm. q), bod 6)** má jen EU Ecolabel, šest národních ekoznaček typu I (EVP, EŠV/EŠS, Nordic Swan, Blauer Engel, Österreichisches Umweltzeichen, Milieukeur) a nejvyšší energetická třída, ta ale jen pro tvrzení o energetické účinnosti. Pozor: u skupin výrobků se starou stupnicí je nejvyšší třída A+++, ne A. Žádná bio značka ani soukromý certifikační systém nestačí na obecné „ekologický“ nebo „šetrný k přírodě“. Výjimkou jsou jen „bio“ a „eko“ u certifikovaných biopotravin.
- **Nejčastější důvod nevyhovění je, že vlastník systému je zároveň kontrolorem.** Komise vyžaduje dvě právně oddělené osoby [QA ř. 581–583]. Týká se to vlastních standardů Ecocertu a ICEA, Fair for Life, CPK, OK compost, bluesign, Cradle to Cradle, EVE VEGAN a The Vegan Society. PETA sama uvádí, že její loga směrnici 2024/825 nesplňují.
- **Na textilních e-shopech je nejčastější problém OEKO-TEX (NEOVĚŘENO).** Dva certifikační instituty jsou akcionáři vlastníka systému a jako povinná je doložena jen akreditace podle ISO/IEC 17025. Nástroj tu bude hlásit „k ověření“.
- **Loga certifikátorů Ecocert, ICEA, BDIH, Cosmébio a Soil Association jsou dvojznačná.** Vyhovují jen se signaturou „COSMOS ORGANIC“ nebo „COSMOS NATURAL“; samotné logo může patřit nevyhovujícímu vlastnímu standardu.
- **Rozdíl SK proti směrnici:** slovenský § 2 písm. q) nemá podmínku „úředně uznané v členských státech“, směrnice i český návrh ji mají. Závěry vycházejí ze slovenského textu. Pro navržené seznamy to nic nemění, protože všech šest národních ekoznaček typu I je i na seznamu úředně uznaných, který odkazuje Komise. Riziko pro právníka se týká jen jiných značek podle ISO 14024 (např. Cradle to Cradle tvrdí typ I).
- **Česko:** směrnice není převzatá (sněmovní tisk 53 po 2. čtení). Vládní návrh přebírá definice. Pozměňovací návrh A chce 24 měsíců přechodného období pro zboží vyrobené do 27. 9. 2026, Komise žádné přechodné období nepřipouští. Úřady sítě CPC mohou u starých zásob postupovat postupně, ale online tvrzení mohou prověřovat přednostně [CPC ř. 30]; e-shopy tedy na výjimku pro „staré zásoby“ spoléhat nemohou.
- **Pro nástroj:** do `sustainability_labels` dávat jen značky „vyhovuje“, bez obecných slov („bio“, „vegan“, „organic“, „ekologicky šetrný“). Značky NEOVĚŘENO a nevyhovuje do seznamu nedávat, ať nález „k ověření“ zůstane. Návrh YAML je v oddílu „Návrh pro labels.yaml“.

## Jak číst

- **Výsledky:**
  - „vyhovuje – orgán veřejné moci“: značku zavedl orgán veřejné moci EU nebo členského státu, podmínky certifikačního systému se nezkoumají.
  - „vyhovuje – certifikační systém“: všechny čtyři podmínky § 2 písm. p) jsou doložené veřejným dokumentem.
  - „nevyhovuje“: veřejný dokument ukazuje, že některá podmínka splněná není (nejčastěji vlastník = kontrolor), nebo to vlastník sám prohlašuje.
  - „NEOVĚŘENO“: aspoň jednu podmínku nejde doložit veřejným dokumentem, nebo si dokumenty odporují.
- **P1–P4** jsou podmínky certifikačného systému podle § 2 písm. p) bodů 1–4, viz oddíl „Právní měřítko“. V tabulce: „ano“ = doloženo, „ne“ = doloženo nesplnění, „?“ = nedoloženo, „—“ = nevyžaduje se (značka orgánu veřejné moci).
- **Citace** mají tvar „…“ [zdroj ř. N]. Zdrojem je zkratka z tabulky níže, nebo název `.txt` ve složce `podklady/znacky/`. Tři tečky značí vynechání. Citace jsou v jazyce originálu.
- Tabulka je souhrn, podrobnosti a citace ke každé značce jsou v oddílu „Značky podrobně“.

| Zkratka | Soubor v `D:\_github\Overko\podklady\` | Co to je |
| --- | --- | --- |
| SK108 | `predpisy-sk/sk-108-2024-ochrana-spotrebitela-zneni-od-2026-09-27.txt` | Zákon č. 108/2024 Z. z. ve znění od 27. 9. 2026 |
| SK310 | `predpisy-sk/sk-310-2025-novela-empco.txt` | Novela č. 310/2025 Z. z. (poznámky pod čarou 7a–7f, účinnost) |
| SK310-DS | `predpisy-sk/sk-310-2025-dovodova-sprava-vladny-navrh.txt` | Důvodová zpráva k vládnímu návrhu novely (MH SR), nově staženo |
| EmpCo | `predpisy-eu/eu-2024-825-empco-cs.txt` | Směrnice (EU) 2024/825, česky |
| QA | `eu-komise/komise-qa-empco-2026-09.txt` | Otázky a odpovědi Komise k EmpCo, září 2026 |
| CPC | `eu-komise/cpc-common-understanding-old-stock-2026-06.txt` | Společný postoj sítě CPC ke starým zásobám, červen 2026, nově staženo |
| CZ53, CZ53/3, CZ53/4, CZ53-hist | `snemovna/snemovni-tisk-53-0-navrh-zakona.txt`, `…-53-3-pozmenovaci-navrhy.txt`, `…-53-4-usneseni-hv-stanovisko.txt`, `…-53-historie.txt` | Sněmovní tisk 53: vládní návrh s důvodovou zprávou (text z PDF), pozměňovací návrhy, stanovisko výboru, historie |
| 1223, 655 | `predpisy-eu/eu-2009-1223-kosmeticke-pripravky-konsolid-2026-05-18-cs.txt`, `predpisy-eu/eu-2013-655-kriteria-tvrzeni-kosmetika-cs.txt` | Nařízení o kosmetice a nařízení o kritériích tvrzení |
| UCPD-pokyny | `eu-komise/komise-pokyny-ucpd-2021-cs.txt` | Pokyny Komise ke směrnici 2005/29/ES |

## Právní měřítko

### Slovensko od 27. 9. 2026 (rozhoduje)

- **Zákaz (príloha č. 1 bod 3):** „Zobrazenie značky udržateľnosti, ktorá nie je založená na certifikačnom systéme alebo ktorú nezaviedli orgány verejnej moci.“ [SK108 ř. 7665–7666].
  - Přechodné období pro značky novela nemá. Jediné přechodné ustanovení se týká úprav účinných od 1. 1. 2026 [SK310 ř. 529–531], body 50–53 (příloha č. 1) nabývají účinnosti 27. 9. 2026 [SK310 ř. 807–810].
  - Komise: „The ECGT Directive does not provide for a transition period beyond this date.“ [QA ř. 587–588].
- **Značka udržateľnosti (§ 2 písm. o):** „akákoľvek dobrovoľná verejná alebo súkromná známka dôveryhodnosti, známka kvality alebo ich ekvivalent, ktorej cieľom je odlíšiť a propagovať produkt, postup alebo obchodníka s odkazom na určité environmentálne vlastnosti, sociálne vlastnosti alebo oboje, okrem povinnej značky podľa osobitných predpisov“ [SK108 ř. 1217–1220].
  - Jako příklad povinné značky uvádí poznámka 7a energetické štítkování podle nařízení (EU) 2017/1369 [SK310 ř. 179–180].
  - Definice zahrnuje i sociální značky. Komise k sociálním znakům řadí i „animal welfare“ [QA ř. 612–613].
- **Certifikačný systém (§ 2 písm. p):** nezávislý systém overovania se čtyřmi podmínkami [SK108 ř. 1222–1238]:
  - P1: „systém je za transparentných, spravodlivých a nediskriminačných podmienok prístupný všetkým obchodníkom“ [SK108 ř. 1226–1227];
  - P2: „požiadavky systému vypracúva vlastník systému po konzultácii s príslušnými odborníkmi a zainteresovanými stranami a sú verejne dostupné“ [SK108 ř. 1229–1230];
  - P3: „systém určuje postupy riešenia nesúladu s požiadavkami systému a predpokladá odňatie alebo pozastavenie používania značky udržateľnosti“ [SK108 ř. 1232–1233];
  - P4: „monitorovanie dodržiavania požiadaviek systému vykonáva tretia osoba, ktorá je nezávislá od vlastníka systému a obchodníka“ [SK108 ř. 1236–1237]. Způsobilost se prokazuje podle technických norem, příkladem je „STN EN ISO/IEC 17065“ [SK310 ř. 183], nebo podle osobitných predpisov, příkladem je nařízení (ES) č. 765/2008 o akreditaci [SK310 ř. 185].
- **Výklad Komise k P4:**
  - Vlastník systému a obchodník smějí být tatáž osoba, pokud je systém otevřený všem [QA ř. 570–574].
  - Kontrolující třetí strana musí být nezávislá na obou: „compliance with the provisions of the ECGT Directive can only be achieved if the scheme owner and the third party are legally separated, i.e. there are two different legal entities“ [QA ř. 581–583].
  - Obchodník musí před zobrazením značky ověřit veřejně dostupné podmínky systému [QA ř. 565–569].
  - Slovenská důvodová zpráva zdůrazňuje nezávislost třetí osoby na obchodníkovi „bez akéhokoľvek konfliktu záujmov“ [SK310-DS ř. 21]. Zákon sám vyžaduje nezávislost i na vlastníkovi.
- **Značky orgánů veřejné moci:**
  - Příklady podle odůvodnění 7 EmpCo jsou loga EMAS (nařízení 1221/2009) a EU Ecolabel (nařízení 66/2010) [EmpCo ř. 33].
  - Značky orgánů států mimo EU jsou podle Komise zakázané, „unless these labels are based on a certification scheme“ [QA ř. 843–845].
- **Certifikační ochranné známky:**
  - Mohou být značkou udržitelnosti: „traders may display these certification marks only if the marks are established by public authorities or are based on a certification scheme“ [QA ř. 337–338].
  - Individuální ochranná známka výrobce značkou udržitelnosti obvykle není [QA ř. 331–333].
- **Vlastní odznaky** e-shopu nebo výrobce (zelený lístek, kapka, pečeť „eco friendly“) mohou podle kontextu spadat pod značky udržitelnosti [QA ř. 407–412]. Bez certifikačního systému jsou pak zakázané.
- **Uznané vynikajúce environmentálne vlastnosti (§ 2 písm. q):**
  - Jsou to vlastnosti podle nařízení 66/2010 (EU Ecolabel) [SK310 ř. 189], „podľa technických noriem“ s příkladem „STN EN ISO 14024 … Environmentálne označovanie typu I“ [SK310 ř. 191–192], nebo nejvyšší úroveň podle jiných předpisů, příkladem je nařízení 2017/1369 [SK310 ř. 193].
  - Jen ony omlouvají obecné tvrzení typu „ekologický“ (bod 6) [SK108 ř. 7676–7680].
  - Důvodová zpráva jako příklad nejvyšší úrovně uvádí „trieda A“ podle nařízení 2017/1369 [SK310-DS ř. 25].
- **Rozdíl SK proti směrnici a českému návrhu** (riziko pro právníka):
  - Směrnice mluví o systémech EN ISO 14024 typu I „úředně uznanými v členských státech“ [EmpCo ř. 135] a český návrh také [CZ53 ř. 22]. Slovenský § 2 písm. q) tuto podmínku v textu nemá [SK108 ř. 1240–1242] a důvodová zpráva mluví jen o „technickej normy“ [SK310-DS ř. 25].
  - Závěry v této rešerši vycházejí ze slovenského textu. Jako ekoznačku typu I ale počítám jen značky, u kterých shodu s ISO 14024 doložil vlastník, příslušný stát, Komise nebo členství v GEN.
  - Všech šest takových národních značek je zároveň na seznamu úředně uznaných ekoznaček podle zprávy k čl. 11 nařízení 66/2010, na kterou odkazuje Komise: „1. Österreichisches Umweltzeichen (AUSTRIA) 2. Ekologicky Setrny Vyrobek (CZECH REPUBLIC) 3. Nordic Ecolabel“, „4. Blue Angel (GERMANY)“, „7. NL Milieukeur (NETHERLANDS)“ [eu-ecolabel-komise-clanek-11-zaverecna-zprava-2018.txt ř. 65] a „NPEHOW (SLOVAKIA)“ [eu-ecolabel-komise-clanek-11-zaverecna-zprava-2018.txt ř. 66]. Zpráva vznikla v roce 2017 u konzultantů a úředním seznamem není. Pro navržené seznamy ale rozdíl nic nemění.
  - Rozdíl je důležitý jen u jiných značek, které deklarují shodu s ISO 14024, ale úředně uznané nejsou. Příkladem je Cradle to Cradle Certified. Good Environmental Choice a Green Product Mark zpráva výslovně řadí mezi „Ecolabels Type I not officially recognised at national/ regional level by the Member States“ [eu-ecolabel-komise-clanek-11-zaverecna-zprava-2018.txt ř. 67–68]. Podle doslovného SK textu by takové značky mohly zakládat uznané vynikající vlastnosti, podle směrnice ne.
- **Relevance tvrzení:**
  - Vynikající profil musí být relevantní pro tvrzení. Například „biologicky rozložitelný“ neomluví EU Ecolabel bez kritéria rozložitelnosti [QA ř. 490–496].
  - „Udržitelný“ ani „odpovědný“ nejde opřít jen o environmentální profil [EmpCo ř. 39].

### Česko

- Směrnice není k 25. 9. 2026 převzatá: sněmovní tisk 53 je po druhém čtení, stanovisko garančního výboru bylo doručeno 4. 9. 2026 [CZ53-hist ř. 5].
- Vládní návrh přebírá definice téměř doslova, včetně „úředně uznanými v členských státech Evropské unie“ [CZ53 ř. 22]. Nový zákaz zní: „uvádí označení udržitelnosti, které se nezakládá na systému certifikace nebo není zavedeno orgánem veřejné správy“ [CZ53 ř. 52–53].
- Důvodová zpráva vlády:
  - „proces úředního uznávání“ „není žádným předpisem definován a upraven“ a „V současné době neexistuje oficiální seznam“ [CZ53 ř. 555–556];
  - typ I dokládá příklady „Ekologicky šetrný výrobek/služba“ [CZ53 ř. 519];
  - u české ekoznačky je MŽP „vlastníkem programu i subjektem udílejícím certifikát“ [CZ53 ř. 510];
  - EMAS „nelze použít jako uznávaný vynikající environmentální profil“ [CZ53 ř. 647];
  - „Logo pro ekologickou produkci není označením udržitelnosti ve smyslu směrnice 2024/825“ [CZ53 ř. 648].
- Pozměňovací návrh garančního výboru (tisk 53/2, v tisku 53/3 jako návrh A) chce 24měsíční přechodné období pro výrobky vyrobené nebo uvedené na trh do 27. 9. 2026: „do 24 měsíců ode dne nabytí účinnosti tohoto zákona pohlíží tak, jako by byly v souladu s tímto zákonem“ [CZ53/3 ř. 16]. Výbor ho doporučil [CZ53/4 ř. 21].
- Q&A Komise přechodné období po 27. 9. 2026 nepřipouští [QA ř. 587–588]. Síť CPC umožňuje u starých zásob jen postupné vymáhání, přičemž úřady mohou přednostně řešit „online claims as they do not face the same challenges as offline claims“ [CPC ř. 30].

## Přehledová tabulka

OVM = orgán veřejné moci, CS = certifikační systém, CB = certifikační orgán. Sloupec „Podmínky“ platí pro § 2 písm. p) body 1–4 (P1 otevřenost, P2 požadavky po konzultaci a veřejné, P3 nesoulad a odnětí, P4 nezávislá způsobilá třetí strana). Sloupec „Vynikající“ platí pro § 2 písm. q). Zdroje jsou soubory v `podklady/znacky/` (prefix a hlavní dokument), citace jsou v oddílu „Značky podrobně“.

| Značka | Vlastník | Typ | Podmínky § 2 písm. p) | Vynikající § 2 písm. q) | Závěr | Zdroje |
| --- | --- | --- | --- | --- | --- | --- |
| **A. Veřejné ekoznačky a štítky** | | | | | | |
| EU Ecolabel | EU, nařízení 66/2010; uděluje MŽP SR (SAŽP), MŽP ČR | OVM (EU) | — | ano (pozn. 7d, Q&A ot. 7), jen pro tvrzení krytá kritérii skupiny | vyhovuje – OVM | `eu-ecolabel-nariadenie-66-2010-konsolid-2017-11-14-sk`, `sk-evp-minzp-ekoprodukty-stranka` |
| Environmentálne vhodný produkt (EVP) | MŽP SR, odborně SAŽP; zákon 469/2002 Z. z. | OVM (SR) | — | ano (typ I podle MŽP SR; na seznamu k čl. 11) | vyhovuje – OVM | `sk-evp-zakon-469-2002-zneni-od-2012-12-01`, `sk-evp-sazp-narodna-znacka-stranka` |
| Ekologicky šetrný výrobek / Ekologicky šetrná služba (EŠV/EŠS) | MŽP ČR (vlastník i certifikační orgán) | OVM (ČR) | — | ano (typ I; CZ53, seznam k čl. 11) | vyhovuje – OVM | `cz-esv-pravidla-narodniho-programu-2024`, `cz-esv-ekoznacka-esv-a-ess-stranka` |
| Nordic Swan Ecolabel | Severská rada ministrů; licence státní subjekty v DK, SE, FI | OVM (mezivládní) | — | ano (Q&A ot. 7) | vyhovuje – OVM | `nordic-swan-goals-and-principles-nmr`, `nordic-swan-official-ecolabel-stranka` |
| Der Blaue Engel | BMUKN (Německo), uděluje RAL gGmbH | OVM (DE) | — | ano (Q&A ot. 7) | vyhovuje – OVM | `blauer-engel-akteure-stranka`, `blauer-engel-empco-stranka` |
| Österreichisches Umweltzeichen | Rakouská republika (BMLUK) | OVM (AT) | — | ano (Q&A ot. 7) | vyhovuje – OVM | `umweltzeichen-at-satzung-marke-stranka` |
| Milieukeur | Stichting Milieukeur (SMK), NL | CS (soukromá nadace) | P1 ? · P2 ano · P3 ? · P4 ano | ano (Q&A ot. 7) | NEOVĚŘENO (bod 3); vynikající ano | `milieukeur-betrouwbaar-transparant-geborgd-stranka` |
| EMAS | EU, nařízení 1221/2009 | OVM (EU), jen organizace | — | ne | vyhovuje – OVM; nesmí být na výrobku ani obalu | `emas-nariadenie-1221-2009-konsolid-2023-07-12-sk` |
| Energetický štítek EU | EU, nařízení 2017/1369 | povinná značka, není značkou udržitelnosti | — | jen nejvyšší třída a jen pro energetická tvrzení | netýká se | `energy-label-nariadenie-2017-1369-konsolid-2021-05-01-sk` |
| **B. Ekologické zemědělství** | | | | | | |
| Logo EU pro ekologickou produkci („Euro-leaf“) | EU, nařízení 2018/848 | OVM (EU); u balených biopotravin povinné | — | ne (Q&A ot. 14) | vyhovuje – OVM (u balených mimo definici) | `eu-bio-2018-848-konsolid-2025-03-25-sk` |
| SK grafický znak ekologickej poľnohospodárskej výroby | SR, zákon 282/2020 Z. z. (MPRV SR, ÚKSÚP) | OVM (SR) | — | ne | vyhovuje – OVM | `sk-bio-zakon-282-2020-zneni-od-2023-04-01` |
| CZ „BIO – produkt ekologického zemědělství“ (biozebra) | MZe ČR, zákon 242/2000 Sb. | OVM (ČR); u balených certifikovaných v ČR povinné | — | ne | vyhovuje – OVM | `cz-bio-zakon-242-2000`, `cz-bio-mze-loga-a-znaceni` |
| Bio-Siegel (DE) | spolkové ministerstvo zemědělství, BLE | OVM (DE) | — | ne | vyhovuje – OVM | `de-bio-siegel-oekolandbau-stranka` |
| AB Agriculture Biologique (FR) | ministerstvo zemědělství FR | OVM (FR) | — | ne | vyhovuje – OVM | `fr-ab-reglement-usage-revision-2025` |
| USDA Organic | USDA AMS (USA) | OVM mimo EU, v EU jen jako CS (Q&A ot. 17) | P1 ano · P2 ano · P3 ano · P4 ? (akredituje sám USDA podle US předpisu) | ne | NEOVĚŘENO | `usda-organic-ecfr-7cfr205` |
| Demeter | IBDA a Biodynamic Federation Demeter International e.V.; v DE Demeter e.V. | CS | P1 ? · P2 ? · P3 ano · P4 ? (certifikuje správce nebo licenční organizace) | ne | NEOVĚŘENO (spíše nevyhovuje) | `demeter-international-standard-2026-en`, `demeter-de-sanktionskatalog` |
| Naturland | Naturland e.V. | CS | P1 ano · P2 ? · P3 ano · P4 ? (rozhoduje komise vlastníka) | ne | NEOVĚŘENO | `naturland-kontrolle-zertifizierung-stranka` |
| Bioland | Bioland e.V. | CS | ? | ne | NEOVĚŘENO | `bioland-herstellung-stranka` |
| Bio Austria | NEOVĚŘENO (PDF bez textu) | ? | ? | ne | NEOVĚŘENO | `bio-austria-zeichennutzungsbedingungen-2025-08` |
| **C. Přírodní kosmetika a drogerie** | | | | | | |
| COSMOS ORGANIC / COSMOS NATURAL (suroviny COSMOS CERTIFIED / APPROVED) | COSMOS-standard AISBL, Brusel | CS | P1 ano · P2 ano · P3 ano · P4 ano (12 CB s ISO/IEC 17065) | ne | vyhovuje – CS | `cosmos-control-manual-v4-2`, `cosmos-empco-brands-ext-2026-061` |
| Logo Ecocert, BDIH, ICEA, Cosmébio nebo Soil Association **se signaturou COSMOS** | viz COSMOS | CS | viz COSMOS | ne | vyhovuje – CS | `cosmos-labelling-guide-v4-0` |
| Ecocert, vlastní standardy (Ecodetergents; Natural / Organic Cosmetic) | ECOCERT Greenlife SAS | CS | P1 ? · P2 ano · P3 ano · P4 ne (vlastník = certifikátor) | ne | nevyhovuje | `ecocert-ecodetergents-standard-v6-4`, `ecocert-ts004-detergents-certification-process` |
| BDIH Standard / „Kontrollierte Natur-Kosmetik“ (bez COSMOS) | BDIH e.V.; kontroluje IONC GmbH (100% dcera) | CS | P1 ? · P2 ? · P3 ? · P4 ? | ne | NEOVĚŘENO | `bdih-cosmos-vztah-2016`, `bdih-richtlinie-stranka` |
| Vegan BDIH | BDIH e.V., IONC GmbH | CS | P1 ano · P2 ? · P3 ? · P4 ? | ne | NEOVĚŘENO | `bdih-vegan-fuer-unternehmen-stranka` |
| NATRUE | NATRUE AISBL, Brusel | CS | P1 ano · P2 ano · P3 ano · P4 ano | ne | vyhovuje – CS | `natrue-control-manual-cb-v3-1`, `natrue-label-requirements-v3-9` |
| ICEA, vlastní standardy (Eco Bio Cosmesi, Eco Bio Detergenza, Vegan) | ICEA, Bologna | CS | P1 ano · P2 ano · P3 ano · P4 ne (vlastník = certifikátor) | ne | nevyhovuje | `icea-rc6-04-02-regulation-ecobio-cosmetics`, `icea-ecobiodetergenza-stranka` |
| Ecogarantie | Probila-Unitrab, Belgie | CS | P1 ano · P2 ano · P3 ano · P4 ? (akreditace kontrolora jen pro bio, konečné odvolání u vlastníka) | ne | NEOVĚŘENO | `ecogarantie-standards-2024`, `ecogarantie-empco-stranka` |
| Cosmébio bez COSMOS (staré logo podle Charty) | Cosmébio (FR) | ? | ? | ne | NEOVĚŘENO | `cosmebio-le-label-stranka` |
| „Soil Association organic“ na nekosmetice | Soil Association; certifikuje dcera | CS | P4 ? | ne | NEOVĚŘENO | `soil-association-labelling-stranka` |
| CPK / CPK bio | KEZ o.p.s., Chrudim | CS | P1 ? · P2 ? · P3 ? · P4 ne (vlastník = certifikátor) | ne | nevyhovuje | `cpk-standardy-2022` |
| NCS – Natural Cosmetics Standard | GfaW mbH, Göttingen | CS | P1 ano · P2 ? · P3 ano · P4 ? | ne | NEOVĚŘENO | `ncs-gfaw-standard-stranka` |
| NCP – Nature Care Product | GfaW mbH | CS | P1 ano · P2 ? · P3 ano · P4 ? | ne | NEOVĚŘENO | `ncp-gfaw-standard-stranka` |
| **D. Zvířata, veganství, klima** | | | | | | |
| The Vegan Society – Vegan Trademark („Veganblume“) | The Vegan Society, UK | licenční systém | P1 ano · P2 ? · P3 ano · P4 ne (kontroluje vlastník) | ne | nevyhovuje (je-li v kontextu značkou udržitelnosti, Q&A ot. 15) | `vegan-society-misuse-policy-2026`, `vegan-society-trademark-standards-stranka` |
| V-Label | V-Label GmbH (CH); licence v SK Slovenská vegánska spoločnosť, v CZ ProVeg | licenční systém | P1 ano · P2 ano · P3 ? · P4 ? | ne | NEOVĚŘENO | `v-label-faq`, `v-label-audity-de` |
| PETA (Beauty Without Bunnies, PETA-Approved Vegan) | PETA US, PETA Foundation | prohlášení firmy | P1 ne · P2 ? · P3 ? · P4 ne | ne | nevyhovuje (vlastník sám uvádí nesoulad) | `peta-us-ultimate-cruelty-free-list` |
| Leaping Bunny / Cruelty Free International | Cruelty Free International, UK | schvalovací program | P1 ? · P2 ? · P3 ? · P4 ? | ne | NEOVĚŘENO | `leaping-bunny-approval-programme-2026-03` |
| EVE VEGAN | EVE VEGAN EUROPE SAS, Paříž | CS | P4 ne (certifikuje vlastník) | ne | nevyhovuje | `eve-vegan-certification-process` |
| ClimatePartner certified | ClimatePartner GmbH, Mnichov | CS | P1 ano · P2 ? · P3 ano · P4 ? | ne | NEOVĚŘENO; „ClimatePartner verified“ v EU nevyhovuje | `climatepartner-certification-programme-en` |
| **E. Textil a výrobky** | | | | | | |
| GOTS | Global Standard gGmbH, Stuttgart | CS | P1 ano (nepřímo, ISO/IEC 17065 čl. 4.4) · P2 ano · P3 ano · P4 ano | ne | vyhovuje – CS | `gots-cb-approval-procedure-v4.0`, `gots-integrity` |
| OEKO-TEX STANDARD 100 | OEKO-TEX AG, Curych | CS | P1 ano · P2 ano · P3 ano · P4 ? (instituty jsou akcionáři vlastníka, povinná jen ISO/IEC 17025) | ne | NEOVĚŘENO | `oeko-tex-statute-of-independence`, `oeko-tex-trademark-regulation` |
| OEKO-TEX MADE IN GREEN, ORGANIC COTTON, LEATHER STANDARD | OEKO-TEX AG | CS | P4 ? | ne | NEOVĚŘENO | `oeko-tex-made-in-green-standard` |
| OEKO-TEX STeP | OEKO-TEX AG | certifikace závodu | na výrobku zakázaná vlastníkem | ne | nevyhovuje (na výrobku) | `oeko-tex-trademark-regulation` |
| bluesign PRODUCT / APPROVED, bluepass | bluesign technologies ag (SGS) | CS | P4 ne (posuzuje a rozhoduje vlastník) | ne | nevyhovuje; bluepass Consumer Product NEOVĚŘENO | `bluesign-system-v3.0`, `bluesign-bluepass-empco-compliance` |
| Textile Exchange (GRS, RCS, OCS, RWS, RDS, RMS, RAS) | Textile Exchange (USA) | CS | P1 ano (nepřímo) · P2 ano · P3 ano · P4 ano | ne | vyhovuje – CS | `textile-exchange-claims-policy-v1.4`, `textile-exchange-general-criteria-cb-v1.0` |
| BCI Cotton Label (dříve Better Cotton) | Better Cotton Initiative, Švýcarsko | CS | P1 ? (členství) · P2 ano · P3 ano · P4 ? (CB až 12 měsíců bez akreditace) | ne | NEOVĚŘENO; staré logo z hmotnostní bilance a členské logo u výrobku nevyhovuje | `better-cotton-claims-framework-v4.1` |
| Cradle to Cradle Certified | Cradle to Cradle Products Innovation Institute | CS | P4 ne (vlastník = CB) | ne (tvrdí typ I, úředně uznaný není) | nevyhovuje | `c2c-certification-scheme-2026-01` |
| Fair Wear | Fair Wear | členská iniciativa | P4 ne (ověřuje sám) | ne | nevyhovuje (jako značka na výrobku) | `fair-wear-member-guide-2019` |
| **F. Dřevo, papír, potraviny, ryby** | | | | | | |
| FSC | Forest Stewardship Council, A.C., Mexiko | CS | P1 ano · P2 ano · P3 ano · P4 ano (CB s ISO/IEC 17065, akreditace ASI) | ne | vyhovuje – CS | `fsc-std-20-001-v5-0-certifikacni-organy`, `fsc-empco-stranka` |
| PEFC | PEFC Council, Ženeva | CS | P1 ano · P2 ano · P3 ano · P4 ano (akreditace signatářem IAF MLA / EA) | ne | vyhovuje – CS | `pefc-st-2003-2020-certifikacni-organy`, `pefc-st-2001-2020-trademarks` |
| Fairtrade | Fairtrade Labelling Organizations International e.V., Bonn | CS (sociální) | P1 ano · P2 ano · P3 ano · P4 ano (FLOCERT GmbH, DAkkS ISO/IEC 17065) | ne | vyhovuje – CS | `fairtrade-empco-stranka`, `flocert-dakks-akreditace` |
| Rainforest Alliance | Rainforest Alliance, Inc., New York | CS | P1 ano · P2 ano · P3 ano · P4 ano (do 30. 10. 2026 i ISO/IEC 17021) | ne | vyhovuje – CS | `rainforest-rules-certification-bodies-v1-3`, `rainforest-empco-stranka` |
| UTZ (stará značka) | Rainforest Alliance | dobíhající | ? | ne | NEOVĚŘENO | `rainforest-license-agreement-terms` |
| MSC | Marine Stewardship Council, Londýn | CS | P1 ano · P2 ano · P3 ano · P4 ano (CAB akreditované ASI) | ne | vyhovuje – CS | `msc-general-certification-requirements-v2-7`, `msc-empco-stranka` |
| ASC | Stichting ASC, Utrecht; ASC Ltd, Londýn | CS | P1 ano · P2 ano · P3 ano · P4 ano | ne | vyhovuje – CS | `asc-empco-stranka`, `asc-car-farm-feed-v1-0` |
| **G. Další značky z e-shopů** | | | | | | |
| Fair for Life | Ecocert Environnement SAS | CS | P1 ano · P2 ano · P3 ano · P4 ne (vlastník = certifikátor) | ne | nevyhovuje (k 26. 9. 2026) | `fair-for-life-empco-roadmap-2026-07` |
| RSPO | Roundtable on Sustainable Palm Oil, Curych | CS | P1 ano · P2 ano · P3 ano · P4 ano (CB akreditované ASI) | ne | vyhovuje – CS | `rspo-scc-certification-systems-2020`, `rspo-ecd-stranka` |
| OK compost (HOME, INDUSTRIAL), OK biobased | TÜV AUSTRIA Belgium NV | CS | P1 ano · P2 ? · P3 ano · P4 ne (vlastník = certifikátor) | ne | nevyhovuje | `tuv-ok-compost-certification-rules-2026-01` |
| Seedling (kompostovatelné) | European Bioplastics e.V., Berlín | CS | P1 ano · P2 ano · P3 ano · P4 ano (DIN CERTCO, DAkkS) | ne | vyhovuje – CS (u certifikátů TÜV AUSTRIA NEOVĚŘENO) | `seedling-certification-scheme-2025`, `seedling-dincertco-dakks-urkunde-teil-07` |
| Certified B Corporation | B Lab | CS (firma) | P1 ano · P2 ano · P3 ano · P4 ? (jen u V2 přes auditory ISO 17021) | ne | NEOVĚŘENO; „Pending B Corp“ nevyhovuje | `b-corp-third-party-certification-stranka` |
| 1 % for the Planet | 1% for the Planet, Inc. | členství / CS | P4 ? (EU ověření BSI, rozsah akreditace neověřen) | ne | CERTIFIED: NEOVĚŘENO; MEMBER: nevyhovuje | `one-percent-planet-compliance-stranka` |
| UEBT | Union for Ethical BioTrade | CS | P1 ano · P2 ano · P3 ano · P4 ano (CB s ISO 17065/17021, akreditace pro jiná schémata) | ne | vyhovuje – CS; „UEBT member“ nevyhovuje | `uebt-rules-for-certification-bodies-2026-04` |
| **H. Bez systému** | | | | | | |
| Vlastní odznak e-shopu nebo výrobce (lístek, „eco friendly“, „vegan“, „cruelty free“, „100% natural“) | obchodník | žádný systém | — | ne | nevyhovuje (bod 3), je-li podle kontextu značkou udržitelnosti | QA ř. 407–412 |

## Značky podrobně

U každé značky: vlastník, typ, doklady k P1–P4 (u značek orgánů veřejné moci jen pro úplnost), uznané vynikající vlastnosti, prohlášení vlastníka k EmpCo, závěr a varianty názvu. Varianty jsou psané tak, jak je uvádějí vlastníci nebo e-shopy. V závorce je normalizovaný tvar, pokud se liší podstatně (LabelMatcher převádí text na malá písmena bez diakritiky a interpunkce).

### A. Veřejné ekoznačky a štítky

#### EU Ecolabel
- **Vlastník a typ:** EU. „Týmto nariadením sa určujú pravidlá ustanovenia a uplatňovania dobrovoľnej schémy environmentálnej značky EÚ.“ [eu-ecolabel-nariadenie-66-2010-konsolid-2017-11-14-sk.txt ř. 61]. Jde o značku orgánu veřejné moci EU; odůvodnění 7 EmpCo ji uvádí jako příklad [EmpCo ř. 33].
- **Kdo uděluje:**
  - SK: „Ministerstvo životného prostredia SR pôsobí ako ústredný orgán štátnej správy na úseku environmentálneho označovania produktov a zároveň je príslušným orgánom pri procesoch udeľovania európskej environmentálnej značky.“ [sk-evp-minzp-ekoprodukty-stranka.txt ř. 85].
  - CZ: „V České republice roli zprostředkovatele pro udělení Ekoznačky EU zastává Ministerstvo životního prostředí (MŽP).“ [cz-esv-ekoznacka-co-je-ekoznaceni-stranka.txt ř. 97].
- **Pro úplnost:** žádat může „Akýkoľvek subjekt, ktorý chce používať environmentálnu značku EÚ“ [eu-ecolabel-nariadenie-66-2010-konsolid-2017-11-14-sk.txt ř. 209]. Příslušný orgán při zneužití „zakáže používanie environmentálnej značky EÚ“ [ř. 265].
- **Vynikající:** ano, přímo podle § 2 písm. q) a poznámky 7d [SK310 ř. 189]. Jen pro tvrzení, která kryjí kritéria dané skupiny výrobků [QA ř. 490–496].
- **Závěr:** vyhovuje – orgán veřejné moci.
- **Varianty:** „EU Ecolabel“ (text loga); SK „environmentálna značka EÚ“ a starší „Európsky kvet“ [sk-evp-minzp-ekoprodukty-stranka.txt ř. 83]; CZ „Ekoznačka EU“; DE „Europäischen Umweltzeichen“ [umweltzeichen-at-wer-sind-wir-stranka.txt ř. 67] (1. pád „Europäisches Umweltzeichen“); registrační číslo ve tvaru xxxx/yyy/zzzzz. Riskantní je samotné „Ecolabel“ (část názvu Nordic Swan Ecolabel i Austrian Ecolabel) a samotné „ekoznačka“ (obecné slovo).

#### Environmentálne vhodný produkt (EVP, Slovensko)
- **Vlastník a typ:** „Národná značka EVP je vlastníctvom MŽP SR a má verejnoprávnu povahu.“ [sk-evp-sazp-narodna-znacka-stranka.txt ř. 143]. Právním základem je zákon č. 469/2002 Z. z. o environmentálnom označovaní výrobkov (poslední znění od 1. 12. 2012) [sk-evp-zakon-469-2002-zneni-od-2012-12-01.txt ř. 15]. Jde o značku orgánu veřejné moci SR.
- **Pro úplnost:**
  - P3: vzor smlouvy dovoluje „dočasne pozastaviť alebo zrušiť oprávnenia nadobúdateľa používať túto značku“ [sk-evp-zakon-469-2002-zneni-od-2012-12-01.txt ř. 1505–1510].
  - P4: kontrolu zajišťuje MŽP se SAŽP, tedy ne třetí strana nezávislá na vlastníkovi. U značky orgánu veřejné moci se to nevyžaduje.
- **Vynikající:** ano. Podle MŽP SR jde o typ I podle ISO 14024 [sk-evp-minzp-ekoprodukty-stranka.txt ř. 85] a zpráva k čl. 11 nařízení 66/2010 ji uvádí jako úředně uznanou („NPEHOW (SLOVAKIA)“) [eu-ecolabel-komise-clanek-11-zaverecna-zprava-2018.txt ř. 65–66]. Členem GEN není.
- **Závěr:** vyhovuje – orgán veřejné moci. Na e-shopech bude vzácná: platná národní kritéria existují jen pro tuhá paliva z biomasy [sk-evp-sazp-kriteria-stranka.txt ř. 141–153].
- **Varianty:** „Environmentálne vhodný produkt“ (text loga) [sk-evp-zakon-469-2002-zneni-od-2012-12-01.txt ř. 817–818]; starší „Environmentálne vhodný výrobok“ [ř. 1247–1248]; zkratka „EVP“ (příliš víceznačná, nepoužívat). Pozor: „environmentálne vhodný“ je i běžné obecné spojení.

#### Ekologicky šetrný výrobek / Ekologicky šetrná služba (EŠV/EŠS, Česko)
- **Vlastník a typ:** Ministerstvo životního prostředí ČR je vlastníkem schématu i certifikačním orgánem: „Tuto funkci vykonává Odbor finančních a dobrovolných nástrojů Ministerstva životního prostředí.“ [cz-esv-pravidla-narodniho-programu-2024.txt ř. 106]. Jde o značku orgánu veřejné moci ČR; typ I podle ČSN ISO 14024 [cz-esv-ekoznacka-esv-a-ess-stranka.txt ř. 43].
- **Pro úplnost:** P1 platí „zásada otevřenosti“ [cz-esv-pravidla-narodniho-programu-2024.txt ř. 189–190]. P3: pravidla upravují odnětí a pozastavení licence [ř. 287, 291]. P4: inspekci provádí MŽP „prostřednictvím vlastních pověřených pracovníků“ [ř. 328], jako certifikační systém by tedy neprošla. U značky orgánu veřejné moci to nevadí, stejně to vidí i česká důvodová zpráva [CZ53 ř. 510].
- **Vynikající:** ano (národní typ I, na seznamu k čl. 11; příklad v CZ53 [CZ53 ř. 519]).
- **Závěr:** vyhovuje – orgán veřejné moci.
- **Varianty:** „Ekologicky šetrný výrobek“, „Ekologicky šetrná služba“, zkratky „EŠV“, „EŠS“ [cz-esv-ekoznacka-esv-a-ess-stranka.txt ř. 41]; v cizojazyčných materiálech „ENVIRONMENTALLY FRIENDLY PRODUCT“ [cz-esv-graficky-manual-2018.txt ř. 94–96]. Riziko: „ekologicky šetrný výrobek“ je zároveň typické obecné tvrzení. V seznamu pro celou stránku by omlouvalo i obyčejné tvrzení, proto navrhuji jen tvary s vazbou na značku (viz návrh YAML).

#### Nordic Swan Ecolabel
- **Vlastník a typ:** zřídila ji Severská rada ministrů; licence udělují národní organizace určené vládami, v EU Miljømærkning Danmark, Miljömärkning Sverige AB (100 % stát) a Ympäristömerkintä Suomi Oy (státní společnost) [nordic-swan-dk-miljomaerkning-om-stranka.txt ř. 19–21]. Jde o značku zavedenou orgány veřejné moci; Komise ji výslovně uvádí jako úředně uznanou ekoznačku typu I [QA ř. 485–486].
- **Prohlášení k EmpCo:** „The Nordic Swan Ecolabel is an officially recognized ISO 14024 ecolabel. According to the Empowering Consumers directive this means that licensees can continue to use the label across Europe.“ [nordic-swan-official-ecolabel-stranka.txt ř. 339].
- **Pro úplnost:** kontrolu provádějí národní organizace, které jsou zároveň nositeli schématu, jako certifikační systém by tedy P4 nesplnila. U značky orgánu veřejné moci se to nevyžaduje.
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ano. Riziko pro právníka: Severská rada zahrnuje i Norsko a Island (Q&A ot. 17 míří na orgány EU), značku ale v EU spravují státní subjekty DK, SE a FI a Komise ji jmenuje výslovně.
- **Varianty:** „Nordic Swan Ecolabel“ (text loga) [nordic-swan-guidelines-using-label-2025-04.txt ř. 260], „Nordic Swan“, „Nordic Ecolabel“, SE „Svanen“, DK „Svanemærket“, NO „Svanemerket“, FI „Joutsenmerkki“, CZ „ekoznačka severské labutě“ [UCPD-pokyny ř. 2942]; povinné osmimístné číslo licence. Riskantní: samotné „Swan“, „labuť“.

#### Der Blaue Engel
- **Vlastník a typ:** „Das Bundesministerium für Umwelt, Klimaschutz, Naturschutz und nukleare Sicherheit (BMUKN) ist der Zeicheninhaber.“ [blauer-engel-akteure-stranka.txt ř. 207–211]. Uděluje RAL gGmbH, samostatná právnická osoba [ř. 229]. Jde o značku orgánu veřejné moci („ist das Umweltzeichen der Bundesregierung“ [blauer-engel-logo-leitfaden-2021-11.txt ř. 33]).
- **Prohlášení k EmpCo:** „Der Blaue Engel ist als nationales, in den Mitgliedstaaten offiziell anerkanntes Umweltzeichen nach ISO 14024 als „anerkannt hervorragende Umweltleistung“ hervorgehoben.“ [blauer-engel-empco-stranka.txt ř. 253].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ano (Q&A ot. 7).
- **Varianty:** „Der Blaue Engel“, „Blauer Engel“, skloňované „Blauen Engel“, „Blue Angel“ [blauer-engel-actors-en-stranka.txt ř. 207]; text loga „BLAUER ENGEL“ a „DAS UMWELTZEICHEN“; CZ „modrý anděl“ [UCPD-pokyny ř. 2942]. Po normalizaci se „Der Blaue Engel“ (blaue engel) neshoduje s „Blauer Engel“ (blauer engel), do seznamu patří oba tvary. Riskantní: samotné „Umweltzeichen“, „UZ“ s číslem.

#### Österreichisches Umweltzeichen
- **Vlastník a typ:** „Republik Österreich, vertreten durch den Bundesminister für Land- und Forstwirtschaft, Klima- und Umweltschutz, Regionen und Wasserwirtschaft (BMLUK) mit Sitz in Stubenring 1, 1010 Wien.“ [umweltzeichen-at-satzung-marke-stranka.txt ř. 102]. Jde o značku orgánu veřejné moci AT.
- **Pro úplnost:** P3: „… die Berechtigung zur Zeichennutzung ‑ dauernd oder bis zur Wiederherstellung des vertragsgemäßen Zustandes ‑ entzogen.“ [umweltzeichen-at-satzung-marke-stranka.txt ř. 255].
- **Prohlášení k EmpCo:** „Das Österreichische Umweltzeichen gilt als rechtssicherer Nachweis für erbrachte Umweltleistungen.“ [umweltzeichen-at-empco-lizenznehmer-news-2026-stranka.txt ř. 109].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ano (Q&A ot. 7). Na aktuálním seznamu GEN není.
- **Varianty:** „Österreichisches Umweltzeichen“, text loga „Umweltzeichen“, anglicky „Austrian Ecolabel“. Samotné „Umweltzeichen“ je obecné německé slovo a patří i k názvům jiných značek, do seznamu nepatří.

#### Milieukeur (Nizozemsko)
- **Vlastník a typ:** „SMK is the developer and manager of Milieukeur.“ [milieukeur-contact-en-stranka.txt ř. 51]. Stichting Milieukeur je soukromá nadace, posuzuje se tedy jako certifikační systém.
- **P1:** NEOVĚŘENO. **P2:** College van Deskundigen se zástupci státu a veřejná slyšení [milieukeur-betrouwbaar-transparant-geborgd-stranka.txt ř. 65]. **P3:** jen „Sanctions will be imposed for the improper use of the word-brand or logo.“ [milieukeur-communication-en-stranka.txt ř. 75–77]; pravidla pozastavení a odnětí nestažena, NEOVĚŘENO. **P4:** certifikují instituce akreditované Raad voor Accreditatie podle ISO/IEC 17065 [milieukeur-betrouwbaar-transparant-geborgd-stranka.txt ř. 71].
- **Vynikající:** ano, Komise ji jmenuje jako „Dutch Ecolabel (Milieukeur)“ [QA ř. 486].
- **Závěr:** pro bod 3 NEOVĚŘENO (P1, P3), pro bod 6 vynikající ano. Na SK/CZ e-shopech vzácná.

#### EMAS
- **Typ:** značka orgánu veřejné moci EU (nařízení 1221/2009, odůvodnění 7 EmpCo [EmpCo ř. 33]), určená organizacím, ne výrobkům.
- **Pravidla:** logo se nesmí používat „na výrobkoch ani na ich obaloch“ [emas-nariadenie-1221-2009-konsolid-2023-07-12-sk.txt ř. 533–541]. EMAS nezakládá uznané vynikající vlastnosti [CZ53 ř. 647].
- **Závěr:** vyhovuje – orgán veřejné moci, ale jen jako značka organizace. Logo u konkrétního výrobku v e-shopu je riziko podle čl. 10 odst. 4 nařízení.
- **Varianty:** „EMAS“; povinný text loga SK „Overené environmentálne manažérstvo“ [emas-nariadenie-1221-2009-konsolid-2023-07-12-sk.txt ř. 3379–3381].

#### Energetický štítek EU
- **Typ:** povinná značka, a proto podle § 2 písm. o) není značkou udržitelnosti; poznámka 7a ho uvádí jako příklad [SK310 ř. 179–180].
- **Vynikající:** jen nejvyšší třída a jen pro tvrzení o energetické účinnosti [QA ř. 491–494]. Stupnice A–G: „obsahuje uzavretú stupnicu výlučne s použitím písmen A až G“ [energy-label-nariadenie-2017-1369-konsolid-2021-05-01-sk.txt ř. 155]. Kotle a ohřívače se přeškálují až „najneskôr do 2. augusta 2030“ [ř. 465–471] a podle metadat Cellaru stále platí akty se starými stupnicemi do A+++ (klimatizace 626/2011, kotle 811/2013, ohřívače 812/2013, větrací jednotky 1254/2014, lokální topidla 2015/1186, kotle na tuhá paliva 2015/1187) [energy-label-cellar-platnost-delegovanych-aktov-2026-09-26.txt ř. 4–10]. U nich třída A nejvyšší není.
- **Závěr:** netýká se bodu 3. Pro bod 6 ne jako seznam značek, ale jako zvláštní pravidlo (viz návrh).

### B. Ekologické zemědělství

#### Logo EU pro ekologickou produkci („Euro-leaf“)
- **Typ:** zavedeno nařízením 2018/848; u balených biopotravin je povinné: „v prípade balených potravín sa na balení uvádza logo ekologickej poľnohospodárskej výroby Európskej únie“ [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 2013]. Tam je mimo definici značky udržitelnosti [SK108 ř. 1217–1220]. Jinde je dobrovolné, např. u dovozu [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 2081], a jde o značku orgánu veřejné moci EU.
- **Pro úplnost:** kontrolní subjekty jsou akreditované podle normy „Posudzovanie zhody – Požiadavky na organizácie certifikujúce výrobky, procesy a služby“ [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 2817].
- **Vynikající:** ne: „The EU Organic Food Label may not be classified as an EN ISO 14024 Type 1 ecolabel“ [QA ř. 789–790]. Pojmy jako bio a eko ale u biopotravin „can be used throughout the European Union to demonstrate compliance with the EU organic farming rules“ [QA ř. 794–795].
- **Závěr:** vyhovuje – orgán veřejné moci. Logo na kosmetice je zneužití, kosmetika je mimo působnost nařízení: „akými sú napr. kozmetické výrobky“ [eu-bio-komise-organic-logo-sk.txt ř. 187].
- **Varianty:** SK „Logo ekologickej poľnohospodárskej výroby Európskej únie“ [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 2063–2065], „Ekologické logo EÚ“ [eu-bio-komise-organic-logo-sk.txt ř. 161]; CZ „Logo Evropské unie pro ekologickou produkci“ [eu-bio-2018-848-konsolid-2025-03-25-cs.txt ř. 2051–2053]; EN „EU organic logo“, „Euro-leaf“ [eu-bio-komise-tlacova-sprava-ip-10-142-en.txt ř. 9]; DE „EU-Bio-Logo“ [de-bio-siegel-oekolandbau-stranka.txt ř. 81]. Kód kontrolního subjektu ve tvaru „AB-CDE-999“ [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 6551–6553], např. SK-BIO-002, CZ-BIO-001, je dobrý signál certifikované biopotraviny. Riskantní: samotné „bio“, „eko“, „organic“.

#### SK grafický znak ekologickej poľnohospodárskej výroby
- **Typ:** zákon č. 282/2020 Z. z.: produkty „možno ich označiť aj grafickým znakom ekologickej poľnohospodárskej výroby, ktorý je spolu s jeho opisom uvedený v prílohe“ [sk-bio-zakon-282-2020-zneni-od-2023-04-01.txt ř. 1421–1423]. Jde o dobrovolnou značku orgánu veřejné moci SR. Inšpekčné organizácie musí mít akreditaci SNAS [ř. 1166] podle STN EN ISO/IEC 17065 [ř. 2008–2011].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ne.
- **Varianty:** úředně „grafický znak ekologickej poľnohospodárskej výroby“ [sk-bio-zakon-282-2020-priloha-graficky-znak.txt ř. 1]; v grafice (jen obrázek) text „EKO POĽNOHOSPODÁRSTVO“. Riskantní: samotné „EKO“.

#### CZ „BIO – produkt ekologického zemědělství“ (biozebra)
- **Typ:** „Grafický znak BIO, tzv. biozebra, s nápisem „Produkt ekologického zemědělství“ se v ČR používá jako celostátní ochranná známka pro biopotraviny.“ [cz-bio-mze-loga-a-znaceni.txt ř. 632]. Balená biopotravina certifikovaná v ČR „se na obalu označí také grafickým znakem“ [cz-bio-zakon-242-2000.txt ř. 317], tam je tedy povinný. Jde o značku orgánu veřejné moci ČR.
- **Pro úplnost:** kontrolují 4 soukromé organizace s veřejnoprávní smlouvou s MZe [cz-bio-mze-kontrolni-system.txt ř. 638]; u KEZ doložena akreditace ČIA podle ČSN EN ISO/IEC 17065 [cz-bio-kez-kdo-jsme.txt ř. 71].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ne.
- **Varianty:** „BIO - PRODUKT ekologického zemědělství“ [cz-bio-mze-logomanual-bio-produkt.txt ř. 1–2], „biozebra“, „české biologo“, kód CZ-BIO-xxx [cz-bio-mze-loga-a-znaceni.txt ř. 634]. Riziko: „produkt ekologického zemědělství“ může být i popis bez loga.

#### Bio-Siegel (Německo)
- **Typ:** „Markeninhaber des Bio-Siegels ist das … Bundesministerium für Landwirtschaft, Ernährung und Heimat“ [de-bio-siegel-oekolandbau-stranka.txt ř. 104–105]; zákon Öko-Kennzeichengesetz. Značka orgánu veřejné moci členského státu; kontrolují Öko-Kontrollstellen [ř. 79].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ne. Na kosmetiku se nesmí použít [de-bio-siegel-oekolandbau-faq.txt ř. 125].
- **Varianty:** „Bio-Siegel“ (bio siegel), „Biosiegel“, „Öko-Kennzeichen“; nápis „Bio“ a „nach EG-Öko-Verordnung“ [de-bio-siegel-oekokennzv.txt ř. 34].

#### AB Agriculture Biologique (Francie)
- **Typ:** „La marque« AB» est la propriété exclusive du ministère en charge de l'agriculture“ [fr-ab-reglement-usage-revision-2025.txt ř. 23]. Značka orgánu veřejné moci FR; sankce „suspension du droit d'usage“ [ř. 69].
- **Závěr:** vyhovuje – orgán veřejné moci; vynikající ne.
- **Varianty:** „AB-Agriculture Biologique“ [fr-ab-reglement-usage-revision-2025.txt ř. 14], „marque AB“, logo s nápisem „AGRICULTURE BIOLOGIQUE“. Riskantní: samotné „AB“.

#### USDA Organic
- **Typ:** značka orgánu veřejné moci mimo EU (USDA, National Organic Program). Podle Komise je v EU zakázaná, „unless these labels are based on a certification scheme“ [QA ř. 843–845].
- **P1:** certifikátor musí přijímat žádosti „without regard to size or membership in any association or group“ [usda-organic-ecfr-7cfr205.txt ř. 1793]. **P2:** předpis 7 CFR 205 a veřejná zasedání rady NOSB [usda-organic-ams-nosb-stranka.txt ř. 347]. **P3:** pozastavení nebo zrušení certifikace [usda-organic-ecfr-7cfr205.txt ř. 2785].
- **P4:** certifikují soukromé, zahraniční nebo státní subjekty akreditované ministerstvem („authorizes a private, foreign, or State entity“ [usda-organic-ecfr-7cfr205.txt ř. 19]), tedy jiné osoby než USDA. Způsobilost ale potvrzuje akreditace samotného vlastníka podle amerického předpisu, ne technická norma ani předpis podle poznámky 7c.
- **Závěr:** NEOVĚŘENO. Právní otázka: stačí akreditace podle předpisu třetí země? Pro „vyhovuje“ mluví, že EU uznává americký kontrolní systém jako rovnocenný (nařízení 2021/2325, `usda-organic-eu-2021-2325-uznane-tretie-krajiny-sk.txt`).
- **Varianty:** „USDA Organic“, „USDA certified organic“, „Certified organic by …“. Riskantní: samotné „organic“.

#### Demeter
- **Vlastník:** ochranné známky drží IBDA (Švýcarsko), správu má Biodynamic Federation Demeter International e.V. (Darmstadt): „The IBDA holds the property rights to the Demeter and biodynamic trademarks.“ [demeter-international-ibda-stranka.txt ř. 111]. Standard přitom uvádí, že vlastnictví je u „individual national owners“ [demeter-international-standard-2026-en.txt ř. 839].
- **P1:** značku lze použít jen s licencí, v Německu je podmínkou i členství (NEOVĚŘENO). **P2:** o standardu rozhoduje Members' Assembly; konzultace s externími stranami NEOVĚŘENO. **P3:** v Německu až „Aberkennung oder zur Vertragskündigung“ [demeter-de-sanktionskatalog.txt ř. 103].
- **P4:**
  - Certifikátory uznává sama federace: „state accreditation or state approved accreditation is not a requirement“ [demeter-international-standard-2026-en.txt ř. 223].
  - Kde není certifikující člen, a to včetně SK a CZ, „the Federation has its own certification organisation (ICO)“ [demeter-international-certification-stranka.txt ř. 123].
  - V Německu certifikuje „Abteilung Qualität“ spolku Demeter e.V. [demeter-de-sanktionskatalog.txt ř. 47].
- **Závěr:** NEOVĚŘENO, spíše nevyhovuje (certifikační rozhodnutí vydává správce nebo licenční organizace, akreditace se nevyžaduje).
- **Varianty:** „Demeter“, „Biodynamic“. Riskantní: „Demeter“ je i jméno jiných značek (např. parfémy), „biodynamický“ je obecné slovo.

#### Naturland, Bioland, Bio Austria
- **Naturland:** inspekce dělají „unabhängige externe, kompetente und staatlich zugelassene Kontrollstellen“ [naturland-kontrolle-zertifizierung-stranka.txt ř. 471], o certifikaci ale rozhoduje „unsere Anerkennungskommission“ [ř. 550], tedy orgán vlastníka. Závěr: NEOVĚŘENO.
- **Bioland:** kdo vydává certifikát, není doloženo [bioland-herstellung-stranka.txt ř. 435]. Závěr: NEOVĚŘENO.
- **Bio Austria:** podmínky užívání značky (08/2025) jsou jen skenem bez textové vrstvy (`bio-austria-zeichennutzungsbedingungen-2025-08.pdf`, k ruční kontrole). Závěr: NEOVĚŘENO.

### C. Přírodní kosmetika a drogerie

#### COSMOS (COSMOS ORGANIC, COSMOS NATURAL; suroviny COSMOS CERTIFIED, COSMOS APPROVED)
- **Vlastník:** „The COSMOS-standard is owned and managed by the COSMOS-standard AISBL, a nonprofit, international association registered in Belgium“ [cosmos-control-manual-v4-2.txt ř. 17]. Zakladateli jsou BDIH, Cosmebio, Ecocert, ICEA a Soil Association.
- **P1:** „Open access on equal terms — any cosmetics company in the world can apply, under identical conditions.“ [cosmos-empco-brands-ext-2026-061.txt ř. 15].
- **P2:** revize probíhají „after full and open consultation with stakeholders“ [cosmos-standard-v4-2.txt ř. 64]; dokumenty jsou veřejné.
- **P3:** certifikační orgán „shall implement appropriate measures such as reduction, suspension or withdrawal of certification“ [cosmos-control-manual-v4-2.txt ř. 251].
- **P4:** „COSMOS-standard AISBL does not issue certificates. All audits are conducted by separate, ISO 17065-accredited Certification Bodies.“ [cosmos-empco-brands-ext-2026-061.txt ř. 18].
  - Doložené akreditace: Ecocert Greenlife COFRAC 5-0520 s rozsahem COSMOS [ecocert-cofrac-5-0520.txt ř. 9–13]; Soil Association Certification IOAS [soil-association-ioas-cosmos-certifikat.txt ř. 2–7].
  - Výhrada: certifikační orgány musí být členy vlastníka a dva z nich (Ecocert, ICEA) jsou zakladatelé. Právní oddělení podle Q&A je splněné, materiální nezávislost zajišťuje akreditace. Akreditaci ICEA a IONC pro COSMOS jsem veřejně nedohledal.
- **Vynikající:** ne. COSMOS sám: „The COSMOS mark alone does not justify these terms.“ [cosmos-empco-brands-ext-2026-061.txt ř. 33].
- **Prohlášení k EmpCo:** „The COSMOS certification scheme qualifies as a recognised certification scheme under the Directive“ [cosmos-empco-dos-donts-2026-09.txt ř. 6]. Pro e-shopy COSMOS upozorňuje, že názvy kategorií jako „Green Beauty“ nebo „Eco Cosmetics“ jsou obecná environmentální tvrzení [cosmos-empco-retailers-ext-2026-062.txt ř. 23].
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** signatura pod logem certifikátora nebo asociace, anglicky, velkými písmeny [cosmos-labelling-guide-v4-0.txt ř. 80]: „COSMOS ORGANIC“, „COSMOS NATURAL“, „certified COSMOS ORGANIC“, „COSMOS ORGANIC certified“ [ř. 64–66], „COSMOS-standard“; na e-shopech „Ecocert COSMOS ORGANIC“, „BDIH COSMOS NATURAL“, „ICEA COSMOS ORGANIC“ (naturshop.sk uvádí „COSMOS ORGANIC“ [eshop-vzorek-naturshop-sk-certifikacie.txt ř. 549] a „COSMOS NATURAL“ [eshop-vzorek-naturshop-sk-certifikacie.txt ř. 565]). Riskantní: samotné „COSMOS“ (květina krasulka, názvy výrobků), „organic“, „natural“. „COSMOS APPROVED“ patří jen surovinám a u hotového výrobku je podezřelé.

#### Ecocert: vlastní standardy (Ecodetergents; Natural / Organic Cosmetic)
- **Vlastník = certifikátor:** „These rights are the exclusive property of ECOCERT Greenlife.“ [ecocert-ecodetergents-standard-v6-4.txt ř. 1–4] a „The scheme Natural Detergents … is managed by Ecocert Greenlife. It is a private scheme. Ecocert Greenlife is accredited for the certification according to this scheme“ [ecocert-ts004-detergents-certification-process.txt ř. 35]. Kosmetický standard v2 má stejného vlastníka [ecocert-natural-organic-cosmetics-standard-v2.txt ř. 1–5].
- **P4:** ne. Akreditace COFRAC podle ISO/IEC 17065 pro vlastní standardy existuje [ecocert-cofrac-5-0520.txt ř. 98–100], Q&A ale vyžaduje dvě různé právnické osoby [QA ř. 581–583].
- **Změna v roce 2026, neověřená:**
  - Ecocert odkazuje na „Updated certification statements“ z července 2026, uložené ve složce „Externalisation des labels maison“ [ecocert-cosmetics-statement-presmerovani.txt ř. 4].
  - Nový text u kosmetiky zní „Cosmetic certified by Ecocert Greenlife SAS according to "Natural Cosmetic" Standard available at cosmetics.ecocert.com“ [ecocert-cosmetics-updated-statements-2026-07-08-prepis.txt ř. 19]. Je to ruční přepis z náhledu.
  - Kdo je novým vlastníkem standardu, dokument neuvádí. Dokument k detergentům je nepřístupný.
- **Závěr:** nevyhovuje (podle posledních veřejných znění standardů je vlastník totožný s certifikátorem). Doporučuji sledovat, zda Ecocert vlastnictví standardů oddělí. Logo Ecocert se signaturou COSMOS posuzovat jako COSMOS.
- **Varianty:**
  - povinný text Ecodetergents: „Detergent certified by ECOCERT Greenlife according to the ECOCERT "Ecodetergent" standard available on http://detergents.ecocert.com“ [ecocert-ecodetergents-standard-v6-4.txt ř. 1226–1227];
  - „Ecodetergent“, „ECOCERT ECODETERGENTS“ (naturshop.sk);
  - „NATURAL COSMETIC“, „ORGANIC COSMETIC“ pod logem ECOCERT;
  - „Cosmétique écologique et biologique“.
  - Samotné „Ecocert“ je nejednoznačné (COSMOS, bio potraviny, Fair for Life, vlastní standardy).

#### BDIH
- **Vlastník a kontrolor:** BDIH e.V., Mannheim; kontroluje IONC GmbH: „Die IONC GmbH wird ausschließlich vom BDIH gehalten“ [bdih-vegan-fuer-unternehmen-stranka.txt ř. 31], se stejným jednatelem.
- **BDIH se signaturou COSMOS:** IONC je autorizovaný certifikační orgán COSMOS [bdih-ionc-cosmos-stranka.txt ř. 19], posuzovat jako COSMOS: vyhovuje.
- **Starý BDIH Standard („Kontrollierte Natur-Kosmetik“):** nové výrobky se od 1. 1. 2017 certifikují jen podle COSMOS, dříve přihlášené smějí zůstat bez časového omezení [bdih-cosmos-vztah-2016.txt ř. 23]. P2 a P3 nejsou veřejně doloženy, akreditace IONC je deklarována jen pro COSMOS [bdih-vegan-stranka.txt ř. 41]. Závěr: NEOVĚŘENO.
- **Vegan BDIH:** „Eine Mitgliedschaft des Unternehmens ist nicht erforderlich“ [bdih-vegan-fuer-unternehmen-stranka.txt ř. 24]; P2–P4 nedoloženy. Závěr: NEOVĚŘENO. Zda jde vůbec o značku udržitelnosti, závisí na kontextu (Q&A ot. 15).
- **Varianty:** „BDIH“, „Kontrollierte Natur-Kosmetik“ [bdih-richtlinie-stranka.txt ř. 3], „Kontrollierte Naturkosmetik“, „BDIH Standard“, „Vegan BDIH“. Samotné „BDIH“ nerozliší COSMOS od starého standardu, rozhoduje signatura COSMOS.

#### NATRUE
- **Vlastník:** NATRUE AISBL, Brusel [natrue-agreement-finished-products-v7.txt ř. 21].
- **P1:** certifikace nezávisí na členství [natrue-label-requirements-v3-9.txt ř. 214]; výhrada: značku smí nést jen značka, u níž kritéria splňuje aspoň 75 % výrobků [ř. 215].
- **P2:** kritéria vyvíjí Scientific Committee s interními i externími experty [natrue-organisation-stranka.txt ř. 91–95]; informace jsou veřejné.
- **P3:** certifikát lze „withheld or suspended … denied or withdrawn“ [natrue-control-manual-cb-v3-1.txt ř. 423].
- **P4:** schváleným certifikátorem se orgán stane až po akreditaci [natrue-control-manual-cb-v3-1.txt ř. 71], hlavním akreditačním orgánem je IOAS [ř. 87]; seznam akreditovaných orgánů (Certisys, EcoControl, Bio.inspecta, CCPB aj.) je v `natrue-ioas-akreditovane-cb-stranka.txt`. Výhrada: „grace period“ dovoluje certifikovat před dokončením akreditace [ř. 128].
- **Prohlášení k EmpCo:** společné prohlášení s COSMOS [cosmos-natrue-empco-joint-statement.txt ř. 8].
- **Závěr:** vyhovuje – certifikační systém. P2 stojí na odborném výboru; širší konzultaci se zainteresovanými stranami jsem nenašel, to je riziko pro právníka.
- **Varianty:** „NATRUE“ (povinně velkými písmeny [natrue-label-usage-guidelines-v2.txt ř. 180]), „NATRUE Label“, úrovně „CERTIFIED NATURAL COSMETIC“, „CERTIFIED ORGANIC COSMETIC“. Hledat jen „NATRUE“; úrovně používá i Ecocert.

#### ICEA
- **Vlastník = certifikátor u vlastních standardů:** značky „Eco Bio Cosmetic“ a „Natural Cosmetic“ jsou majetkem ICEA [icea-rc6-04-02-regulation-ecobio-cosmetics.txt ř. 250], certifikuje sám ICEA [icea-ecobiodetergenza-stranka.txt ř. 179].
- **Závěr:**
  - ICEA se signaturou COSMOS: vyhovuje, jako COSMOS.
  - Vlastní standardy (Eco Bio Cosmesi, Natural Cosmetic, Eco Bio Detergenza, Eco Detergenza, Vegetarian/Vegan): nevyhovuje, vlastník je totožný s certifikátorem (Q&A ot. 8).
- **Varianty:** povinný text „Certified by ICEA according to Standard DTR07. Criteria available at www.icea.bio“ [icea-ecobiodetergenza-stranka.txt ř. 179]; „ICEA ECO BIO COSMETICS“ (naturshop.sk [eshop-vzorek-naturshop-sk-certifikacie.txt ř. 597]); „ECOBIOCOSMESI“, „ECO BIO DETERGENZA“. Samotné „ICEA“ nerozliší COSMOS od vlastního standardu.

#### Ecogarantie
- **Vlastník:** Probila-Unitrab, Belgie; vlastník shodu sám nekontroluje [ecogarantie-empco-stranka.txt ř. 75].
- **P1–P3:** doloženy (členství otevřené každé firmě, jejíž výrobky splňují standard; sankce ukládá certifikační orgán [ecogarantie-standards-2024.txt ř. 443]).
- **P4:**
  - Kontrolují Certisys a TÜV NORD. Certisys ale doložil akreditaci BELAC podle ISO/IEC 17065 jen „for its certification activities in organic agriculture“ [certisys-accreditations-stranka.txt ř. 241].
  - O odvolání s konečnou platností rozhoduje orgán vlastníka: „Appeals Board of Probila-Unitrab, whose decision is final“ [ecogarantie-standards-2024.txt ř. 447].
- **Typ I:** „Ecogarantie has not been formally assessed against ISO 14024.“ [ecogarantie-faq-empco-stranka.txt ř. 43].
- **Závěr:** NEOVĚŘENO. Kontrolor je jiná osoba, ale akreditace pro rozsah Ecogarantie doložená není a konečné slovo při odvolání má vlastník.
- **Varianty:** „Ecogarantie“, „ECOGARANTIE“, „Écogarantie“ (stejný normalizovaný tvar); „Eco Garantie“ (eco garantie). Nezaměnit s „Biogarantie“.

#### Cosmébio, Soil Association
- **Cosmébio:** od 1. 1. 2017 se nové výrobky certifikují podle COSMOS a signatura „COSMOS ORGANIC“ nebo „COSMOS NATURAL“ je pod logem Cosmébio [cosmebio-le-label-stranka.txt ř. 101–103]. Se signaturou vyhovuje jako COSMOS. Staré logo podle Charty asociace: NEOVĚŘENO.
- **Soil Association:** logo SA se signaturou COSMOS vyhovuje jako COSMOS. „Soil Association organic“ na nekosmetických výrobcích podle vlastního standardu certifikuje 100% dcera vlastníka [soil-association-hb-standards-stranka.txt ř. 474] bez doložené akreditace: NEOVĚŘENO.

#### CPK / CPK bio (Certifikovaná přírodní kosmetika)
- **Vlastník = certifikátor:** KEZ o.p.s., Chrudim [cpk-standardy-2022.txt ř. 1]; standardy i ochranné známky jsou majetkem KEZ [ř. 97] a certifikát vydává také KEZ [ř. 11].
- **P2:** KEZ „není povinna připomínky … akceptovat“ [cpk-standardy-2022.txt ř. 60]. **P3:** sankce ve standardech nejsou (NEOVĚŘENO). **P4:** ne.
- **Závěr:** nevyhovuje. Naturshop.sk ji už uvádí jako starší označení [eshop-vzorek-naturshop-sk-certifikacie.txt ř. 721–727].
- **Varianty:** „CPK“, „CPK bio“, „CPK - CERTIFIKOVANÁ PŘÍRODNÍ KOSMETIKA“ [cpk-standardy-2022.txt ř. 93]. Samotné „CPK“ má i jiné významy (kreatinkináza u doplňků stravy).

#### NCS – Natural Cosmetics Standard, NCP – Nature Care Product
- **Vlastník:** GfaW Gesellschaft für angewandte Wirtschaftsethik mbH, Göttingen [ncs-gfaw-imprint-stranka.txt ř. 81–83].
- **P1, P3:** doloženy [ncs-gfaw-standard-stranka.txt ř. 447].
- **P4:** certifikační orgány schvaluje GfaW. Jediný viditelný orgán, EcoControl GmbH, má akreditaci IOAS jen pro NATRUE [ncs-ecocontrol-ioas-17065-certifikat.txt ř. 2–8] a je zároveň spoluautorem standardu.
- **Závěr:** NEOVĚŘENO (NCS i NCP). NCP navíc dovoluje označit výrobek jako „ecological“ se synonymem „eco“ [ncp-gfaw-standard-stranka.txt ř. 604], což je podle EmpCo obecné environmentální tvrzení.
- **Varianty:** „Natural Cosmetics Standard“, „NCS - vegan“, „NCS - organic quality“, „Nature Care Product“. Samotné „NCS“ má i jiné významy (Natural Colour System u barev).

### D. Zvířata, veganství, klima

#### The Vegan Society – Vegan Trademark („Veganblume“)
- **Vlastník:** „The Trademark is a registered mark owned by the trademark owner (The Vegan Society) and licensed to approved parties under a formal Trademark Licence Agreement.“ [vegan-society-misuse-policy-2026.txt ř. 6].
- **P1:** „The scheme is open to participation by any business“ [vegan-society-misuse-policy-2026.txt ř. 7]. **P3:** „Suspension or withdrawal of certification“ [ř. 40]. **P2:** dokumenty jsou veřejné, konzultace NEOVĚŘENO.
- **P4:** ne. Kontroluje vlastník: „Our dedicated and experienced vegan team check each product application“ [vegan-society-trademark-standards-stranka.txt ř. 260].
- **Je to značka udržitelnosti?** Vlastník: „The Vegan Trademark is not an environmental or eco certification“ [vegan-society-trademark-standards.txt ř. 7]. Q&A: záleží na kontextu [QA ř. 801–803]. Kritérium „bez testování na zvířatech“ se ale týká welfare zvířat, tedy sociální vlastnosti.
- **Prohlášení k EmpCo:** kvůli EmpCo vlastník nahradil logo se slunečnicí novým designem [vegan-society-blog-rebrand-2026.txt ř. 264]; spojení značky s environmentálními nebo sociálními tvrzeními zakazuje [vegan-society-misuse-policy-2026.txt ř. 34].
- **Závěr:** nevyhovuje (P4), pokud je v daném kontextu značkou udržitelnosti.
- **Varianty:** „Vegan Trademark“, „The Vegan Society“, „Registered with The Vegan Society“, „Veganblume“ [vegan-blume-vegan-society-de.txt ř. 15], „Vegan-Blume“. Riskantní: samotné „vegan“, „vegánsky“, „veganský“. „Slovenská vegánska spoločnosť“ je partner V-Label, ne Vegan Society.

#### V-Label
- **Vlastník:** „As the holder of the trademark rights, the Switzerland-based V-Label GmbH“ [v-label-o-nas.txt ř. 172]. Licence v SK uděluje Slovenská vegánska spoločnosť, v CZ ProVeg, z.s.
- **P1:** „Licensing is open to any company whose products meet the Criteria, and the requirements apply equally to all applicants.“ [v-label-faq.txt ř. 190]. **P2:** kritéria jsou veřejná a vznikla po konzultaci s veganskými a vegetariánskými organizacemi [ř. 186].
- **P3:** jen částečně. Licenci lze odebrat, ale „Das V-Label verliert nur, wer nachweisbar und wissentlich betrügt“ [v-label-audity-de.txt ř. 196].
- **P4:** externí audity (např. SGS) proběhnou až do 12 měsíců po udělení licence [v-label-audity-de.txt ř. 226]; akreditace kontrolorů nedoložena. V-Label sám píše, že jde o licenci, ne o certifikaci [ř. 196].
- **Závěr:** NEOVĚŘENO. Na kosmetice od verze kritérií 02.03 (9/2026) už neznamená „netestováno na zvířatech“.
- **Varianty:** „V-Label“ (v label), „VLabel“ (vlabel), s podtitulem „vegan“ nebo „vegetarian“; česky „vegan certifikát V-Label“ [v-label-proveg-cz.txt ř. 387].

#### PETA (Beauty Without Bunnies, PETA-Approved Vegan)
- **Prohlášení vlastníka:** „The PETA cruelty-free marks are intended to be used in the United States of America, Canada, and India, and no longer in the European Union. It does not comply with the requirements of Directive 2024/825“ [peta-us-ultimate-cruelty-free-list.txt ř. 481].
- **P4:** ne. Podkladem je prohlášení firmy podepsané generálním ředitelem [peta-us-vegan-companies-faq.txt ř. 9], laboratorní testování podmínkou není [peta-us-approved-vegan-faq.txt ř. 147].
- **Závěr:** nevyhovuje.
- **Varianty:** „Beauty Without Bunnies“, „Global Beauty Without Bunnies“, „PETA Approved Global Animal Test Policy“, „Animal Test–Free“, „PETA-Approved Vegan“. Riskantní: samotné „cruelty free“, „bunny“, „zajíček“; samotné „PETA“ hledat jen s „approved“, „cruelty“, „bunny“ nebo „vegan“.

#### Leaping Bunny / Cruelty Free International
- **Vlastník:** Cruelty Free International, Londýn [leaping-bunny-approval-programme-2026-03.txt ř. 75–76]. Od března 2026 má program nový název a logo („Cruelty Free International Approval Programme“).
- **P2:** úplná kritéria jsou jen v „Application Pack“ na vyžádání: „A brand may request an Application Pack which includes full details of the programme criteria.“ [leaping-bunny-approval-programme-2026-03.txt ř. 25].
- **P3, P4:** sankční řád veřejně není; „annual independent audits“ jsou zmíněny [ř. 25], kdo audituje a s jakou akreditací, zveřejněno není.
- **Tvrzení CFI:** „The European Commission has confirmed in writing that cruelty-free claims (including logos) are permitted when a programme’s criteria are stricter than EU law and clearly explained to the public.“ [leaping-bunny-approval-programme-2026-03.txt ř. 36]. Dopis Komise jsem nenašel (NEOVĚŘENO); týká se bodu 15, ne bodu 3.
- **Závěr:** NEOVĚŘENO s vysokým rizikem. Kvůli welfare zvířat jde velmi pravděpodobně o značku udržitelnosti.
- **Varianty:** „Leaping Bunny“, „Cruelty Free International“, „Cruelty Free International Approved“. Riskantní: samotné „cruelty free“.

#### EVE VEGAN
- Kontrolu dokumentů i audit dělá a certifikát vydává sám vlastník EVE VEGAN EUROPE SAS [eve-vegan-certification-process.txt ř. 103]. Standard obsahuje i environmentální prvky („Sustainable production methods that consider the ecosystem must be adopted.“ [eve-vegan-certification-standards.txt ř. 67–69]).
- **Závěr:** nevyhovuje (P4).
- **Varianty:** „EVE VEGAN“, „Expertise Vegane Europe“.

#### Kosmetika „netestováno na zvířatech“ (souvislost s bodem 15)
- Nařízení 1223/2009 zakazuje uvádět na trh kosmetiku testovanou na zvířatech kvůli tomuto nařízení (čl. 18) [1223 ř. 873]. Tvrzení o netestování je dovolené jen za podmínek čl. 20 odst. 3 [1223 ř. 1037].
- Vydávat plošný zákaz EU za přednost výrobku je praktika podle prílohy č. 1 bodu 15: „Prezentovanie požiadaviek, ktoré sa podľa právnych predpisov vzťahujú na všetky produkty v príslušnej kategórii produktov na trhu Európskej únie, ako charakteristickej črty ponuky obchodníka.“ [SK108 ř. 7722–7724].
- Logo „cruelty free“ je značkou udržitelnosti se sociální vlastností (welfare zvířat [QA ř. 612–613]). Vlastní ikona bez systému proto spadá pod bod 3.

#### ClimatePartner
- **Vlastník:** ClimatePartner GmbH, Mnichov [climatepartner-certification-programme-en.txt ř. 4182].
- **P1:** „open to all companies … transparent, fair, and non-discriminatory conditions.“ [climatepartner-certification-programme-en.txt ř. 961]. **P3:** pozastavení a odnětí [ř. 1537, 1558].
- **P4:** certifikační orgány musí být nezávislé „from ClimatePartner as system owner and from the audited companies“ [ř. 1705–1706]. Které to jsou a kdo je akredituje, doloženo není. O certifikaci rozhoduje sám ClimatePartner [ř. 2371].
- **Bod 8 (kompenzace):** program sám zakazuje tvrzení o neutralitě založená na kompenzaci [ř. 1033–1036]. „ClimatePartner verified“ je značka „for companies and products outside the EU“ bez auditu třetí stranou [climatepartner-blog-verified.txt ř. 151].
- **Závěr:** „ClimatePartner certified“ NEOVĚŘENO; „ClimatePartner verified“ v EU nevyhovuje; staré štítky „klimaneutral“ a „carbon neutral“ spadají pod bod 8.
- **Varianty:** „ClimatePartner certified“, „Climate Partner“, „Climate-ID“, „Financial climate contribution“.

### E. Textil a výrobky

#### GOTS (Global Organic Textile Standard)
- **Vlastník:** „Global Standard gemeinnützige GmbH Rotebühlstr. 102 · 70178 Stuttgart · Germany“ [gots-conditions-for-use-of-signs-v4.0.txt ř. 4].
- **P1:** nepřímo. Certifikační orgány musí dodržovat nediskriminační podmínky podle ISO/IEC 17065 čl. 4.4 [gots-cb-approval-procedure-v4.0.txt ř. 281–283].
- **P2:** revize každé 3 roky podle kodexu ISEAL [gots-standard-v8.1.txt ř. 9] se dvěma koly veřejné konzultace [gots-revisions.txt ř. 233].
- **P3:** certifikaci lze pozastavit a odejmout [gots-cb-approval-procedure-v4.0.txt ř. 976–978].
- **P4:** „All certification decisions are within the responsibility and purview of the CBs. Global Standards does not interfere“ [gots-integrity.txt ř. 219]. Certifikační orgán musí mít akreditaci ISO/IEC 17065 [gots-cb-approval-procedure-v4.0.txt ř. 1096].
- **Prohlášení k EmpCo:** praktický návod k tvrzením ze září 2026, např. „This product is GOTS-certified.“ [gots-claims-guidance.txt ř. 45].
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** „GOTS“, „Global Organic Textile Standard“ (jen anglicky), „GOTS-certified“, „certifikát GOTS“; na etiketě stupeň („organic“ smí být v jazyce země prodeje, tedy i „bio“), certifikační orgán a číslo licence. Riskantní: „organic cotton“, „biobavlna“ samy o sobě.

#### OEKO-TEX (STANDARD 100, MADE IN GREEN, ORGANIC COTTON, LEATHER STANDARD, STeP)
- **Vlastník:** OEKO-TEX AG, Curych [oeko-tex-trademark-regulation.txt ř. 3–4].
- **P1–P3:** doloženy. Odnětí certifikátu vlastníkem upravují podmínky [oeko-tex-trademark-regulation.txt ř. 409].
- **P4 sporné:**
  - Akcionáři vlastníka jsou dva certifikační instituty: „vis-à-vis its shareholders, TESTEX AG, Zurich, and Hohenstein Laboratories GmbH & Co. KG“ [oeko-tex-statute-of-independence.txt ř. 8]. Tentýž statut uvádí: „The conformity assessment bodies also include the shareholders of OEKO-TEX AG“ [ř. 15].
  - Instituty jsou akreditovány „in particular ISO/IEC 17025“ [oeko-tex-statute-of-independence.txt ř. 15]. Akreditace pro certifikaci podle ISO/IEC 17065 je v dokumentech jen „where applicable“, prohlášení o nezávislosti přitom mluví o 17065. Dokumenty si odporují.
  - Slovenský VUTCH má akreditaci SNAS podle ISO/IEC 17065, rozsah pro OEKO-TEX neověřen.
- **Je STANDARD 100 značkou udržitelnosti?** Pro: OEKO-TEX se popisuje jako „certification scheme for textile and leather sustainability“ [oeko-tex-organisation.txt ř. 122]. Proti: certifikát potvrzuje test na škodliviny pro zdraví a hlavní sdělení je „The original safety standard“ [oeko-tex-standard-100-key-claim.txt ř. 3].
- **Prohlášení k EmpCo:** známkový řád tvrdí, že „meets the requirements of Directive (EU) 2024/825“ [oeko-tex-trademark-regulation.txt ř. 20].
- **STeP:** certifikace závodu, na výrobku se nesmí použít [oeko-tex-trademark-regulation.txt ř. 438].
- **Závěr:** STANDARD 100, MADE IN GREEN, ORGANIC COTTON, LEATHER STANDARD: NEOVĚŘENO (P4). STeP na výrobku: nevyhovuje.
- **Varianty:**
  - registrované slovní známky „the word marks OEKO-TEX®, OEKO-TEX, OEKOTEX and ÖKO-TEX“ [oeko-tex-standard-100.txt ř. 94] (oeko tex, oekotex, oko tex);
  - „OEKO-TEX STANDARD 100“ a starší „STANDARD 100 by OEKO-TEX“;
  - „MADE IN GREEN by OEKO-TEX“, „OEKO-TEX ORGANIC COTTON“, „LEATHER STANDARD by OEKO-TEX“, „STeP by OEKO-TEX“.
  - Riskantní: samotné „Standard 100“, „made in green“.

#### bluesign / bluepass
- **P4:** ne. Posuzuje i rozhoduje sám vlastník: „reviewed by an assessment/on-site inspection carried out by BLUESIGN“ a zpráva obsahuje „a decision of BLUESIGN on the compliance“ [bluesign-system-v3.0.txt ř. 250].
- **Změna 2026:** bluesign v září 2026 tvrdí, že spotřebitelské výrobky bluepass certifikuje samostatný akreditovaný subjekt [bluesign-bluepass-empco-compliance.txt ř. 117]. Jméno ani akreditační orgán neuvádí a podle konzultační stránky je tento krok teprve v návrhu [bluesign-criteria-public-consultation.txt ř. 157].
- **Závěr:** bluesign PRODUCT, bluesign APPROVED, bluepass Article a Chemical Product: nevyhovuje. bluepass Consumer Product: NEOVĚŘENO.
- **Varianty:** „bluesign“, „bluesign PRODUCT“, „bluesign APPROVED“, „bluepass“; formulace „bluesign certified product“ je podle vlastníka zakázaná.

#### Textile Exchange (GRS, RCS, OCS, RWS, RDS, RMS, RAS)
- **P2:** veřejná konzultace k revizím trvá nejméně 60 dní [textile-exchange-standard-setting-procedures-v1.0.txt ř. 224].
- **P3:** certifikát lze pozastavit nebo odejmout [textile-exchange-general-criteria-cb-v1.0.txt ř. 1904].
- **P4:** certifikační orgány akredituje autorizovaný akreditační orgán [textile-exchange-accreditation.txt ř. 193]. Od 1. 10. 2026 se vyžaduje akreditace ISO/IEC 17065 od člena IAF [textile-exchange-general-criteria-cb-v1.0.txt ř. 22–28].
- **P1:** nepřímo přes ISO/IEC 17065 čl. 4.4.
- **Prohlášení k EmpCo:** pravidla tvrzení V1.4 jsou povinná pro posouzení po 27. 9. 2026 [textile-exchange-claims-policy-v1.4.txt ř. 14].
- **Závěr:** vyhovuje – certifikační systém. Týká se tvrzení o výrobku s logem, licenčním číslem, certifikačním orgánem a procentem.
- **Varianty:** „Global Recycled Standard“ a „GRS“, „Recycled Claim Standard“ a „RCS“, „Organic Content Standard“ a „OCS“ („OCS 100“, „OCS Blended“), „Responsible Wool Standard“ a „RWS“, „Responsible Down Standard“ a „RDS“; typická věta „Made with 100% GRS certified recycled materials“ [textile-exchange-claims-policy-v1.4.txt ř. 535]. Trojpísmenné zkratky jsou víceznačné, hledat je jen s „certified“ nebo s plným názvem.

#### BCI Cotton Label (Better Cotton)
- **P4:** všechna certifikační rozhodnutí dělá třetí strana [better-cotton-certification-stranka.txt ř. 1125]. Certifikační orgán ale smí pracovat až 12 měsíců jen s podanou žádostí o akreditaci [better-cotton-general-certification-requirements-v1.2.txt ř. 244–247].
- **P1:** podmínkou je členství, jehož podmínky neověřeny.
- **Hmotnostní bilance:** starý štítek na výrobku se nově neschvaluje [better-cotton-claims-framework-v4.1.txt ř. 1010]. Členské logo se nikdy nesmí použít u výrobku [better-cotton-bci-label-guidelines-v1.1.txt ř. 161–163].
- **Závěr:** BCI Cotton Label: NEOVĚŘENO. Staré logo „Better Cotton“ z hmotnostní bilance a členské logo u výrobku: nevyhovuje.

#### Cradle to Cradle Certified
- **P4:** ne. „Certification Body (CB): The Cradle to Cradle Products Innovation Institute (C2CPII).“ a „Certification Scheme Owner (Scheme Owner): The Cradle to Cradle Products Innovation Institute (C2CPII).“ [c2c-certification-scheme-2026-01.txt ř. 127–128].
- **Typ I:** vlastník tvrdí shodu s ISO 14024 [c2c-standard-stranka.txt ř. 49], úředně uznaný ale není. Sám uvádí, že to k obecným tvrzením neopravňuje [ř. 91]. Podle doslovného SK textu § 2 písm. q) je to otevřená otázka pro právníka, značka ale neprojde bodem 3.
- **Závěr:** nevyhovuje.
- **Varianty:** „Cradle to Cradle Certified“, „C2C Certified“ s úrovní (Bronze, Silver, Gold, Platinum). Riskantní: samotné „C2C“ a „Cradle to Cradle“ (filozofie).

#### Fair Wear
- Sama uvádí „we do not certify brands, factories or garments“ [fair-wear-member-guide-2019.txt ř. 457]; plnění ověřuje Fair Wear sám. Logo smějí členové dát i na výrobek.
- **Závěr:** nevyhovuje jako značka na výrobku (není certifikační systém).

### F. Dřevo, papír, potraviny, ryby

#### FSC
- **Vlastník:** Forest Stewardship Council, A.C., „registered office in Oaxaca, Mexico“ [fsc-financni-vykazy-2020.txt ř. 682].
- **P1:** „The FSC certification system is open to all traders“ [fsc-empco-stranka.txt ř. 1997].
- **P2:** veřejné konzultace nejméně 60 dní [fsc-pro-01-001-v4-0-tvorba-standardu.txt ř. 435].
- **P3:** certifikační orgán smí certifikát pozastavit nebo odejmout „with immediate effect“ [fsc-std-20-001-v5-0-certifikacni-organy.txt ř. 498]; držitel pak musí „immediately cease to make any use of FSC trademarks“ [ř. 501].
- **P4:** certifikační orgány „operate independently from FSC and the certified companies“ [fsc-empco-stranka.txt ř. 2040]; povinná shoda s ISO/IEC 17065 [fsc-std-20-001-v5-0-certifikacni-organy.txt ř. 81–82], akreditace přes ASI.
  - Výhrada: ASI je dceřinou společností FSC A.C.; konsolidovaná závěrka mezi dceřinými společnostmi uvádí „ASI Assurance Services International GmbH“ [fsc-financni-vykazy-2020.txt ř. 682].
  - V EHP proto běží dvoustupňový program s národními akreditačními orgány podle nařízení 765/2008 [asi-pro-20-126-ttap-v2-1.txt ř. 43].
- **Prohlášení k EmpCo:** „FSC meets EmpCo requirements for certification scheme and our labels are aligned with the Directive.“ [fsc-empco-stranka.txt ř. 1987].
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** „FSC“ (s ® nebo ™), „Forest Stewardship Council“, „FSC 100%“, „FSC MIX“ / „FSC Mix“, „FSC RECYCLED“ / „FSC Recycled“ [fsc-std-50-001-v3-0-trademark.txt ř. 313–319], „FSC-certified“, licenční kód „FSC C######“ (fsc c012345). „FSC Controlled Wood“ není certifikovaný výrobek a logo FSC se k jeho propagaci nesmí použít, v textu e-shopu je to signál chyby.

#### PEFC
- **Vlastník:** PEFC Council, Ženeva [pefc-st-2001-2020-trademarks.txt ř. 7].
- **P1:** „Open and non-discriminatory access“ [pefc-empco-stranka.txt ř. 261].
- **P2:** národní standardy s konzultací nejméně 60 dní [pefc-st-1001-2017-tvorba-standardu.txt ř. 292].
- **P3:** „If the certification is suspended, withdrawn or terminated, the PEFC trademarks licence will be automatically suspended“ [pefc-st-2001-2020-trademarks.txt ř. 247].
- **P4:** certifikační orgány musí splňovat ISO/IEC 17065 [pefc-st-2003-2020-certifikacni-organy.txt ř. 70] a mít akreditaci od signatáře IAF MLA nebo EA [ř. 503].
- **Prohlášení k EmpCo:** PEFC systém přezkoumal podle směrnice [pefc-empco-stranka.txt ř. 201]. Korigendum 2026 vypustilo obecná tvrzení ze štítků [pefc-st-2001-2020-trademarks.txt ř. 608–610].
- **Závěr:** vyhovuje – certifikační systém (P4 je z celé skupiny nejčistší).
- **Varianty:** „PEFC“, „PEFC Certified“, „PEFC Recycled“ [pefc-st-2001-2020-trademarks.txt ř. 308], licenční číslo „PEFC/XX-XX-XX“.

#### Fairtrade
- **Vlastník:** Fairtrade Labelling Organizations International e.V., Bonn; značka FAIRTRADE je od roku 2021 certifikační ochranná známka EU [fairtrade-empco-stranka.txt ř. 520].
- **P1:** otevřené producentům a obchodníkům ochotným a schopným splnit standardy [fairtrade-empco-stranka.txt ř. 522]. **P2:** konzultace „at least one round of 60 days“ [fairtrade-sop-tvorba-standardu.txt ř. 114].
- **P3:** po decertifikaci musí obchodování za podmínek Fairtrade okamžitě skončit [fairtrade-assurance-rules.txt ř. 1166].
- **P4:** certifikuje FLOCERT GmbH s akreditací DAkkS podle DIN EN ISO/IEC 17065 (D-ZE-14408-01-00) [flocert-dakks-akreditace.txt ř. 6–9].
  - Výhrada: FLOCERT je „an independently governed subsidiary of Fairtrade International, FLOCERT's sole shareholder“ [flocert-historie-stranka.txt ř. 227]. Požadavek Q&A na dvě právnické osoby je splněný, nezávislost ale stojí na akreditaci.
  - Akreditace nepokrývá drahé kovy, klima ani textil [flocert-akreditace-stranka.txt ř. 195]; tam je P4 NEOVĚŘENO.
- **Prohlášení k EmpCo:** značka „is based on a certification scheme, which meets the requirements defined by EmpCo“ [fairtrade-empco-stranka.txt ř. 520].
- **Závěr:** vyhovuje – certifikační systém (u zlata, textilu a klimatu NEOVĚŘENO).
- **Varianty:** „Fairtrade“, „FAIRTRADE“, „Fairtrade Mark“, „Fairtrade certified“; tvary „fairtradový“, „fairtradová“, „fairtradové“ se po normalizaci neshodují se slovem „fairtrade“ a musí být v seznamu zvlášť. Riskantní: „fair trade“ (dvě slova, obecný pojem i jiné systémy), „férový obchod“, „spravodlivý obchod“.

#### Rainforest Alliance (včetně UTZ)
- **Vlastník:** Rainforest Alliance, Inc., New York; „RA is the exclusive owner of the RA Marks“ [rainforest-license-agreement-terms.txt ř. 267].
- **P1:** otevřené farmám a firmám, které splní požadavky [rainforest-empco-stranka.txt ř. 237]. **P2:** první konzultace „minimum of sixty days“ [rainforest-standards-development-procedure-v2-0.txt ř. 249].
- **P3:** po pozastavení musí držitel „immediately cease to sell or ship product with a Rainforest Alliance certified claim“ [rainforest-certification-auditing-rules-supply-chain-v1-1.txt ř. 1511].
- **P4:** autorizované certifikační orgány s akreditací ISO 17065 [rainforest-empco-stranka.txt ř. 255]. Do 30. 10. 2026 stačí u dodavatelského řetězce i ISO/IEC 17021 a akreditace přes člena ISEAL [rainforest-rules-certification-bodies-2020.txt ř. 522–527]. Od 31. 10. 2026 jen ISO/IEC 17065 [rainforest-rules-certification-bodies-v1-3.txt ř. 381–382].
- **Prohlášení k EmpCo:** „Rainforest Alliance certification qualifies as a certification scheme under EmpCo“ [rainforest-empco-stranka.txt ř. 181]. Pravidla tvrzení jsou kvůli EmpCo v revizi; v EU se zatím nemají používat dvě tvrzení o „responsible sourcing“ [rainforest-labeling-claims-policy-stranka.txt ř. 1037].
- **UTZ:** „legacy mark“ [rainforest-license-agreement-terms.txt ř. 12]; nové firmy se nezapisují. NEOVĚŘENO.
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** „Rainforest Alliance“, „Rainforest Alliance Certified“; starší „UTZ“, „UTZ Certified“. Riskantní: samotné „Rainforest“ (názvy vůní), „RA“, „UTZ“ (i značka snacků).

#### MSC
- **Vlastník:** Marine Stewardship Council, Londýn (charita). Licence uděluje dceřiná společnost MSCI; licenci potřebuje i e-shop [msc-ecolabel-user-guide.txt ř. 1023].
- **P1:** „open to any fishery or supply chain company that meets the requirements“ [msc-empco-stranka.txt ř. 303]. **P2:** první konzultace nejméně 60 dní [msc-standard-setting-procedure.txt ř. 537].
- **P3:** „If a certificate is suspended or withdrawn, the MSC label can no longer be used“ [msc-empco-stranka.txt ř. 331].
- **P4:** certifikační orgány akredituje ASI [msc-empco-stranka.txt ř. 344], musí splňovat ISO 17065 [msc-general-certification-requirements-v2-7.txt ř. 527]. ASI MSC nevlastní.
- **Prohlášení k EmpCo:** texty tvrzení změněny kvůli směrnici [msc-empco-tvrzeni-stranka.txt ř. 275], samotná značka se nemění.
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** „MSC“, „Marine Stewardship Council“, „MSC certified“, kód „MSC-C-XXXXX“. Riskantní: „MSc.“ (titul), MSC Cruises.

#### ASC
- **P1:** „open under transparent and non-discriminatory terms to all traders that respect the EU law“ [asc-empco-stranka.txt ř. 303]. **P2:** aspoň dvě veřejné konzultace u nového standardu [asc-tvorba-standardu-procedure-v3-0.txt ř. 977–978].
- **P3:** po pozastavení nebo odnětí nesmí klient prodávat výrobky jako ASC [asc-car-farm-feed-v1-0.txt ř. 1573–1574].
- **P4:** certifikační orgány jsou „independent from ASC“ pod dohledem ASI a splňují ISO 17065 [asc-empco-stranka.txt ř. 331].
- **Závěr:** vyhovuje – certifikační systém.
- **Varianty:** „ASC“, „Aquaculture Stewardship Council“, CZ „Mořské plody z chovu s certifikátem ASC“ [asc-label-stranka.txt ř. 567–569], SK „Morské plody z chovu s certifikáciou ASC“ [ř. 919–921], kód „ASC-C-xxxxx“. Riskantní: „ASC/DESC“ (řazení).

### G. Další značky z e-shopů

#### Fair for Life
- **Vlastník = certifikátor:** „These rights are the exclusive property of Ecocert Environnement SAS (Ecocert).“ [fair-for-life-standard-2026-04-01.txt ř. 46]; „Should you need to be certified, you must be directly contracted with ECOCERT“ [fair-for-life-certification-process-2023.txt ř. 100].
- **Vlastník sám** uvádí, že oddělení teprve připravuje: „establishing a distinct organizational structure that formally separates scheme ownership from certification activities done by Ecocert“ [fair-for-life-empco-roadmap-2026-07.txt ř. 34–39]. Fair for Life navíc není v rozsahu akreditace COFRAC 5-0635.
- **Závěr:** nevyhovuje (k 26. 9. 2026). Sledovat, zda Ecocert oddělení oznámí.
- **Varianty:** „Fair for Life“, „FAIR FOR LIFE“, povinný text „Fair Trade certified according to the Fair for Life standard“ [fair-for-life-standard-2026-04-01.txt ř. 9228–9229]. Riskantní: „For Life“, „fair trade“.

#### RSPO
- **P1:** „accessible to all who are committed to meeting its standards“ [rspo-ecd-stranka.txt ř. 137]. **P2:** standardy vznikají konsenzuálně [ř. 149].
- **P3:** certifikace se pozastavuje a místo „shall cease from making any certified product claim“ [rspo-scc-certification-systems-2020.txt ř. 428].
- **P4:** „All RSPO certification bodies (CBs) are accredited by the Assurance Services International (ASI).“ [rspo-certification-bodies-stranka.txt ř. 203], a to proti ISO/IEC 17065 [rspo-scc-certification-systems-2020.txt ř. 68].
- **Závěr:** vyhovuje – certifikační systém. Pozor: štítek „MIXED“ nezaručuje certifikovaný olej ve výrobku a „CREDITS“ ho neobsahuje; oficiální texty obsahují slovo „sustainable“, které sám RSPO považuje za rizikové [rspo-empco-scrutiny-stranka.txt ř. 117].
- **Varianty:** „RSPO“, „RSPO certified“, „RSPO MIXED“, „RSPO CREDITS“, „Roundtable on Sustainable Palm Oil“.

#### OK compost, OK biobased (TÜV AUSTRIA)
- **Vlastník = certifikátor:** „The OK compost ®, OK biodegradable ®, OK biobased ® and OK renewable ® certification schemes and conformity marks are the property of TABE. Only TABE can issue certificates“ [tuv-ok-compost-certification-rules-2026-01.txt ř. 165].
- **Závěr:** nevyhovuje (P4; P2 nedoloženo).
- **Varianty:** „OK compost HOME“, „OK compost INDUSTRIAL“, „OK biobased“ [tuv-ok-compost-graphical-chart-logos.txt ř. 83–84]. Riskantní: „kompostovateľné“, „compostable“ (tvrzení, ne značka).

#### Seedling
- **Vlastník:** European Bioplastics e.V., Berlín [seedling-certification-scheme-2025.txt ř. 6–7]; certifikují DIN CERTCO a TÜV AUSTRIA Belgium.
- **P4:** „DIN CERTCO owns an accreditation for product certifications according to DIN EN ISO/IEC 17065“ [seedling-certification-scheme-2025.txt ř. 19], doloženo přílohou akreditace DAkkS [seedling-dincertco-dakks-urkunde-teil-07.txt ř. 11–14]. U TÜV AUSTRIA Belgium akreditace neověřena (databáze BELAC je za CAPTCHA).
- **Závěr:** vyhovuje – certifikační systém (doloženo pro DIN CERTCO).
- **Varianty:** „Seedling“ s textem „industrially compostable“ a číslem registrace [seedling-guideline-logo-2023.txt ř. 15]; německy „Keimling“. Riskantní: „seedling“ v anglických textech zahradnictví.

#### Certified B Corporation
- **P4:** u nového modelu (Standards V2) „an independent third-party assurance provider is responsible for auditing and certifying“ [b-corp-third-party-certification-stranka.txt ř. 41], ve starém modelu rozhodoval B Lab sám [b-corp-faq-new-standards-stranka.txt ř. 204]. Z textu e-shopu to nejde rozlišit a akreditace auditorů je neověřená.
- **Závěr:** NEOVĚŘENO; „Pending B Corp“ nevyhovuje (jen sebehodnocení).
- **Varianty:** „Certified B Corporation“, „B Corp“, „B Corporation“.

#### 1 % for the Planet
- **Členství:** logo „1% FOR THE PLANET MEMBER“ nemá být v EU používáno; „is the only logo that should be used on packaging, marketing materials and at point of sale within the EU“ platí pro logo CERTIFIED [one-percent-planet-compliance-stranka.txt ř. 109].
- **P4:** pro EU nově ověřuje BSI [one-percent-planet-compliance-stranka.txt ř. 66]; rozsah akreditace neověřen.
- **Závěr:** CERTIFIED: NEOVĚŘENO; MEMBER: nevyhovuje (biooo.cz ho uvádí jako „1% FOR THE PLANET MEMBER“).

#### UEBT
- **P4:** „The CB shall be accredited to ISO/IEC 17065 and/or ISO/IEC 17021 at all times.“ [uebt-rules-for-certification-bodies-2026-04.txt ř. 248]. Jde ale o akreditaci pro jiná schémata („proxy-accreditation“ [ř. 254]), to je riziko pro právníka.
- **Závěr:** vyhovuje – certifikační systém. Členské logo nevyhovuje: „‘UEBT member’ is not ‘UEBT certified’“ [uebt-claims-labelling-policy-2026-09.txt ř. 353].
- **Varianty:** „UEBT certified“, „Union for Ethical BioTrade“. Riskantní: „UEBT member“, „ethically sourced“.

## Návrh pro labels.yaml

Pravidla návrhu:

- **`sustainability_labels` obsahuje jen značky „vyhovuje“.** Seznam jen ruší nález `eco_label_unrecognized` ve větě, a proto nesmí obsahovat značky NEOVĚŘENO ani nevyhovuje. U nich má nález „k ověření“ zůstat; odmítnuté značky jsou v komentáři na konci.
- **`excellent_performance_labels` obsahuje jen EU Ecolabel a šest národních ekoznaček typu I.** Seznam ruší nález `eco_generic_claim` na celé stránce, proto v něm nesmí být nic, co je zároveň běžným obecným tvrzením. Holé „Ekologicky šetrný výrobek“ ze současného souboru ven, místo něj tvary s vazbou na značku.
- **Normalizace (`LabelMatcher.Normalize`):**
  - Text se převádí na malá písmena bez diakritiky a interpunkce: „OEKO-TEX“ = „oeko tex“, ale „OEKOTEX“ = „oekotex“.
  - „Der Blaue Engel“ obsahuje „blaue engel“, ne „blauer engel“.
  - Tvary se stejným normalizovaným zápisem stačí uvést jednou („EU-Ecolabel“ = „EU Ecolabel“, „Ekoznačka EÚ“ = „Ekoznačka EU“).
  - Skloňované tvary se musí uvést zvlášť. Delší název je pokrytý kratším, pokud ho obsahuje („Nordic Swan Ecolabel“ obsahuje „Nordic Swan“, „FSC Mix“ obsahuje „FSC“).
- Tvary označené „(neověřeno)“ jsem v podkladech nenašel. Jsou to bezpečné varianty názvu, ne obecná slova.

```yaml
# Návrh 26. 9. 2026, rešerše podklady/reserse/znacky-udrzatelnosti.md. Posoudí právník.
# Názvy se hledají jako celá slova bez ohledu na velikost písmen, diakritiku a interpunkci.

# SK príloha č. 1 bod 6 (EU bod 4a): uznané vynikajúce environmentálne vlastnosti, § 2 písm. q).
# EU Ecolabel a národní ekoznačky typu I podle EN ISO 14024; všech šest je i na seznamu úředně uznaných
# (zpráva k čl. 11 nařízení 66/2010). Omlouvá jen tvrzení krytá kritérii výrobku (Q&A ot. 7).
# Energetická třída sem nepatří: je jen pro energetická tvrzení a „A“ není nejvyšší u starých stupnic.
excellent_performance_labels:
  # EU Ecolabel (nařízení 66/2010)
  - "EU Ecolabel"
  - "Ecolabel EU"
  - "Ekoznačka EU"                      # CZ; stejný tvar jako SK „Ekoznačka EÚ“
  - "Ekoznačku EU"
  - "Ekoznačkou EU"
  - "Environmentálna značka EÚ"
  # 2. pád („kritériá ekoznačky EU“) záměrně chybí: bývá i u výrobků bez udělené značky
  - "Environmentálnu značku EÚ"
  - "Environmentálnou značkou EÚ"
  - "Európsky kvet"                     # starší SK název
  - "Europäisches Umweltzeichen"
  - "Europäischen Umweltzeichen"
  # Environmentálne vhodný produkt (SK, zákon 469/2002)
  - "Environmentálne vhodný produkt"
  - "Environmentálne vhodný výrobok"    # starší název
  # Ekologicky šetrný výrobek / služba (CZ, MŽP); holý název je i obecné tvrzení, proto jen s vazbou na značku
  - "ekoznačka Ekologicky šetrný výrobek"
  - "ekoznačku Ekologicky šetrný výrobek"
  - "ekoznačkou Ekologicky šetrný výrobek"
  - "ekoznačky Ekologicky šetrný výrobek"
  - "ekoznačka Ekologicky šetrná služba"
  - "ekoznačku Ekologicky šetrná služba"
  - "ekoznačkou Ekologicky šetrná služba"
  - "certifikát Ekologicky šetrný výrobek"
  - "certifikátem Ekologicky šetrný výrobek"
  - "ekoznačka EŠV"
  - "ekoznačku EŠV"
  - "ekoznačkou EŠV"
  # Nordic Swan Ecolabel
  - "Nordic Swan"                       # pokrývá „Nordic Swan Ecolabel“
  - "Nordic Ecolabel"
  - "Svanen"
  - "Svanemærket"
  - "Svanemerket"
  - "Joutsenmerkki"
  - "Severská labuť"                    # CZ i SK
  - "severské labutě"                   # CZ 2. pád, pokyny Komise k UCPD
  - "severskej labute"                  # SK 2. pád (neověřeno)
  # Der Blaue Engel
  - "Blauer Engel"
  - "Blaue Engel"                       # „Der Blaue Engel“
  - "Blauen Engel"
  - "Blue Angel"
  - "Modrý anděl"
  - "Modrého anděla"
  - "Modrý anjel"                       # SK (neověřeno)
  # Österreichisches Umweltzeichen
  - "Österreichisches Umweltzeichen"
  - "Österreichischen Umweltzeichen"
  - "Austrian Ecolabel"
  - "Rakouská ekoznačka"                # (neověřeno)
  # Milieukeur (NL); pro bod 3 NEOVĚŘENO, proto jen v tomto seznamu
  - "Milieukeur"

# SK príloha č. 1 bod 3 (EU bod 2a): značka založená na certifikačnom systéme alebo zavedená orgánom verejnej moci.
# Jen značky s výsledkem „vyhovuje“. Obsahuje první seznam kromě Milieukeur a navíc tvary ve 2. pádě.
sustainability_labels:
  # --- orgány veřejné moci: ekoznačky (stejné jako výše, bez Milieukeur) ---
  - "EU Ecolabel"
  - "Ecolabel EU"
  - "Ekoznačka EU"
  - "Ekoznačku EU"
  - "Ekoznačkou EU"
  - "Ekoznačky EU"
  - "Environmentálna značka EÚ"
  - "Environmentálnu značku EÚ"
  - "Environmentálnou značkou EÚ"
  - "Environmentálnej značky EÚ"
  - "Európsky kvet"
  - "Europäisches Umweltzeichen"
  - "Europäischen Umweltzeichen"
  - "Environmentálne vhodný produkt"
  - "Environmentálne vhodný výrobok"
  - "ekoznačka Ekologicky šetrný výrobek"
  - "ekoznačku Ekologicky šetrný výrobek"
  - "ekoznačkou Ekologicky šetrný výrobek"
  - "ekoznačky Ekologicky šetrný výrobek"
  - "ekoznačka Ekologicky šetrná služba"
  - "ekoznačku Ekologicky šetrná služba"
  - "ekoznačkou Ekologicky šetrná služba"
  - "certifikát Ekologicky šetrný výrobek"
  - "certifikátem Ekologicky šetrný výrobek"
  - "ekoznačka EŠV"
  - "ekoznačku EŠV"
  - "ekoznačkou EŠV"
  - "Nordic Swan"
  - "Nordic Ecolabel"
  - "Svanen"
  - "Svanemærket"
  - "Svanemerket"
  - "Joutsenmerkki"
  - "Severská labuť"
  - "severské labutě"
  - "severskej labute"
  - "Blauer Engel"
  - "Blaue Engel"
  - "Blauen Engel"
  - "Blue Angel"
  - "Modrý anděl"
  - "Modrého anděla"
  - "Modrý anjel"
  - "Österreichisches Umweltzeichen"
  - "Österreichischen Umweltzeichen"
  - "Austrian Ecolabel"
  - "Rakouská ekoznačka"
  # --- orgány veřejné moci: EMAS (jen organizace, na výrobku zakázané) ---
  - "EMAS"
  # --- orgány veřejné moci: ekologické zemědělství ---
  - "EU Organic"                        # pokrývá „EU organic logo“
  - "Euro-leaf"                         # euro leaf
  - "Euroleaf"                          # (neověřeno)
  - "Eurolist"                          # SK název z roku 2010
  - "logo EU pro ekologickou produkci"
  - "Logo Evropské unie pro ekologickou produkci"
  - "logo ekologickej poľnohospodárskej výroby"   # pokrývá „… Európskej únie“ i „… EÚ“
  - "ekologické logo EÚ"
  - "EU-Bio-Logo"
  - "grafický znak ekologickej poľnohospodárskej výroby"
  - "grafickým znakom ekologickej poľnohospodárskej výroby"
  - "Produkt ekologického zemědělství"  # text českého znaku; riziko popisu bez loga je malé, biopotraviny řeší i eco_organic_food
  - "biozebra"
  - "Bio-Siegel"
  - "Biosiegel"
  - "Öko-Kennzeichen"
  - "AB Agriculture Biologique"
  # --- certifikační systémy: kosmetika (loga Ecocert, BDIH, ICEA, Cosmébio a Soil Association jen se signaturou COSMOS) ---
  - "COSMOS ORGANIC"                    # pokrývá „certified COSMOS ORGANIC“, „Ecocert COSMOS ORGANIC“
  - "COSMOS NATURAL"
  - "COSMOS CERTIFIED"                  # suroviny
  - "COSMOS APPROVED"                   # suroviny; u hotového výrobku podezřelé
  - "COSMOS-standard"
  - "NATRUE"
  # --- certifikační systémy: textil ---
  - "GOTS"
  - "Global Organic Textile Standard"
  - "Textile Exchange"
  - "Global Recycled Standard"
  - "GRS certified"
  - "Recycled Claim Standard"
  - "RCS certified"
  - "Organic Content Standard"
  - "OCS certified"
  - "OCS 100"
  - "OCS Blended"
  - "Responsible Wool Standard"
  - "RWS certified"
  - "Responsible Down Standard"
  - "RDS certified"
  - "Responsible Mohair Standard"
  - "Responsible Alpaca Standard"
  # --- certifikační systémy: dřevo a papír ---
  - "FSC"                               # pokrývá FSC 100%, FSC Mix, FSC Recycled, FSC-C012345
  - "Forest Stewardship Council"
  - "PEFC"
  # --- certifikační systémy: potraviny, ryby, suroviny ---
  - "Fairtrade"                         # pokrývá „Fairtrade Mark“, „FAIRTRADE certified“
  - "fairtradový"
  - "fairtradová"
  - "fairtradové"
  - "fairtradovou"
  - "fairtradového"
  - "fairtradových"
  - "fairtradovej"                      # SK
  - "Rainforest Alliance"               # pokrývá „Rainforest Alliance Certified“
  - "MSC"
  - "Marine Stewardship Council"
  - "ASC"
  - "Aquaculture Stewardship Council"
  - "RSPO"
  - "Roundtable on Sustainable Palm Oil"
  - "UEBT certified"
  # --- certifikační systémy: obaly ---
  - "Seedling"                          # doloženo pro certifikáty DIN CERTCO

# Záměrně NEZAŘAZENO (rešerše, oddíly C–G):
#   nevyhovuje: Ecodetergents a „NATURAL/ORGANIC COSMETIC“ (Ecocert), ICEA Eco Bio Cosmesi/Detergenza/Vegan,
#     CPK a CPK bio, Fair for Life, OK compost a OK biobased, bluesign a bluepass, Cradle to Cradle Certified,
#     The Vegan Society a Veganblume, PETA, EVE VEGAN, Fair Wear, OEKO-TEX STeP, „ClimatePartner verified“,
#     „Pending B Corp“, „1% for the Planet MEMBER“, „UEBT member“;
#   NEOVĚŘENO: OEKO-TEX (STANDARD 100, MADE IN GREEN, ORGANIC COTTON, LEATHER STANDARD), V-Label,
#     Leaping Bunny / Cruelty Free International, BDIH bez COSMOS a „Kontrollierte Naturkosmetik“, Vegan BDIH,
#     Ecogarantie, NCS, NCP, Demeter, Naturland, Bioland, Bio Austria, USDA Organic, ClimatePartner certified,
#     B Corp, 1% for the Planet CERTIFIED, BCI Cotton Label, UTZ, Cosmébio bez COSMOS, Soil Association organic;
#   obecná slova (nikdy): bio, eko, eco, organic, natural, vegan, cruelty free, fair trade, ekologický,
#     samotné Ecocert, ICEA, BDIH, Cosmebio, Demeter, COSMOS, Ecolabel, ekoznačka, Umweltzeichen.
```

### Doporučené úpravy pravidel (mimo seznamy)

1. **Energetická třída (bod 6):** nepřidávat do `excellent_performance_labels`.
   - Normalizace maže znaménko plus, takže „trieda A+“ by se shodovala s „trieda A“.
   - Třída A u starých stupnic (A+++ až D) nejvyšší není [energy-label-deleg-nariadenie-811-2013-vykurovanie-sk.txt ř. 1474].
   - Lépe zvláštní otázka pro Jev („tvrzení o energetické účinnosti“) a kontrola kódem nad textem před normalizací (regex `\b(A\+{0,3})\b` u „energetická trieda/třída“), s výjimkou jen pro nejvyšší třídu dané stupnice.
2. **Loga certifikátorů bez COSMOS:** Ecocert, ICEA, BDIH, Cosmébio a Soil Association ve větě bez „COSMOS ORGANIC“ nebo „COSMOS NATURAL“ nechat jako nález „k ověření“. Vysvětlení: „Ověřte, zda jde o certifikaci COSMOS; vlastní standardy Ecocert a ICEA podmínky nesplňují.“
3. **Kódy kontrolních subjektů bio** ve tvaru AB-CDE-999 [eu-bio-2018-848-konsolid-2025-03-25-sk.txt ř. 6551–6553] (SK-BIO-002, CZ-BIO-001; „DE-ÖKO-006“ se normalizuje na „de oko 006“) jsou spolehlivý signál certifikované biopotraviny, vhodný jako výjimka pro otázku `eco_organic_food`.
4. **Známé nevyhovující značky:** nový seznam a kontrola `list_present` (navržená v `reserse/cerne-listiny-a-eko.md`) by umožnily nález „nevyhovuje“ se zdůvodněním, například „PETA sama uvádí, že značka nesplňuje směrnici 2024/825“ nebo „vlastník systému je zároveň certifikátorem“. Zatím spadnou pod obecné „k ověření“.
5. **OEKO-TEX** bude na textilních e-shopech nejčastější nález. Vysvětlení by mělo uvádět konkrétní důvod (instituty jsou akcionáři vlastníka, akreditace podle ISO/IEC 17065 není jednoznačně doložena) a že STANDARD 100 je hlavně zdravotní značka.
6. **„Netestováno na zvířatech“ u kosmetiky:** samostatně podle bodu 15 prílohy č. 1 (zákonný požadavek jako přednost) a čl. 20 odst. 3 nařízení 1223/2009. Otázka patří do modulu `ucp` z rešerše černých listin, ne do seznamu značek; podrobně je rozebraná v `reserse/zakonne-poziadavky-ako-prednost.md` (řádek tabulky „Kosmetika: zkoušky na zvířatech“).
7. **Drogerie a CLP:** u směsí označovaných podle CLP (čisticí prostředky, barvy) nesmí být „ecological“ a podobné výrazy na etiketě ani obalu, ani s EU Ecolabel [QA ř. 502–508]. Text e-shopu to přímo neřeší, v poznámce k nálezu je to užitečné upozornění.

## Nejistoty a otázky pro právníka

1. **SK § 2 písm. q) bez „úředně uznané“:** doslovný SK text je širší než směrnice a český návrh. Navržené seznamy to neovlivňuje. U Cradle to Cradle a dalších značek s vlastním prohlášením o ISO 14024 by ale podle SK textu šlo tvrdit uznané vynikající vlastnosti, podle směrnice ne.
2. **„Orgán verejnej moci“ není definován:** Nordic Swan zřídila mezivládní rada včetně Norska a Islandu; v EU ho spravují státní subjekty DK, SE a FI.
3. **Stoprocentní dcery jako kontrolor:** FLOCERT (Fairtrade), IONC (BDIH), Soil Association Certification. Q&A vyžaduje jen dvě právnické osoby, nezávislost pak stojí na akreditaci. Ta je doložená u FLOCERT (DAkkS, ISO/IEC 17065), u IONC jen pro COSMOS.
4. **Akreditace mimo národní akreditační orgány:**
   - ASI patří do skupiny FSC; v EHP proto běží dvoustupňový program s národními akreditačními orgány.
   - Rainforest Alliance a UEBT akceptují „proxy“ akreditaci pro jiná schémata.
   - USDA akredituje podle vlastního předpisu.
   - B Corp auditoři mají ISO 17021; Rainforest Alliance tuto normu akceptuje do 30. 10. 2026.
   - Slovenský zákon uvádí ISO/IEC 17065 a nařízení 765/2008 jen jako příklady („napríklad“).
5. **OEKO-TEX:** instituty jsou akcionáři vlastníka a dokumenty si odporují v tom, zda je nutná akreditace podle ISO/IEC 17065. Otevřené je i to, zda je STANDARD 100 vůbec značkou udržitelnosti.
6. **Vegan a cruelty free:** „vegan“ je značkou udržitelnosti jen podle kontextu (Q&A ot. 15). Cruelty free se týká welfare zvířat, tedy sociální vlastnosti, a logo je proto velmi pravděpodobně značkou udržitelnosti. Výslovný výklad Komise ke cruelty free jsem nenašel.
7. **Ecocert (včetně Fair for Life) v přestavbě:** dokumenty z července 2026 naznačují „Externalisation des labels maison“ a změnu textů u kosmetiky. Pokud Ecocert vlastnictví standardů převede na jinou osobu, může se výsledek u Ecodetergents, Natural/Organic Cosmetic a Fair for Life změnit. Oddělení u Fair for Life vlastník sám plánuje.
8. **Česko:** do přijetí tisku 53 platí jen obecný zákaz klamavých praktik; o 24měsíčním přechodném období rozhodne třetí čtení.
9. **Ručně ověřit** (curl ani prohlížeč nestačily):
   - databáze BELAC pro TÜV AUSTRIA Belgium a Certisys (chráněná CAPTCHA, neobcházel jsem ji);
   - dokument Ecocert k Ecodetergents ze 7. 7. 2026 (SharePoint bez oprávnění);
   - rejstřík ÚPV, ochranná známka EŠV č. 174170;
   - podmínky Bio Austria (sken, OCR bez němčiny);
   - „Certification Process Requirements for B Corps“ (imagerelay);
   - seznam kontrolních orgánů V-Label (načítá se JavaScriptem);
   - seznam certifikačních orgánů Rainforest Alliance (Power BI);
   - „Application Pack“ Cruelty Free International (jen na vyžádání).

## Stažené soubory

Soubory jsou v `D:\_github\Overko\podklady\znacky\` (486 dokumentů, vždy originál a `.txt`, 409 MB). Přehled s URL je v `podklady/README.md`, oddíl „Značky udržitelnosti (26. 9. 2026)“. Nově jsou staženy i:

- `predpisy-sk/sk-310-2025-dovodova-sprava-vladny-navrh.*`,
- `snemovna/snemovni-tisk-53-0-navrh-zakona.*` a tisky 53/2, 53/3 a 53/4,
- `eu-komise/cpc-common-understanding-old-stock-2026-06.*`.

