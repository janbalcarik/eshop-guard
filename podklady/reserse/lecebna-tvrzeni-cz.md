# Léčebná tvrzení podle druhu výrobku (SK a CZ)

26. 9. 2026 · rešerše pro `eshop-guard` (otázky pro Jev) · screening, ne právní posouzení · všechny právní odkazy mají status „ověřit“

## Shrnutí

- **Potraviny, nápoje, čaje, doplňky stravy, minerální vody a potraviny pro zvláštní lékařské účely:** tvrzení, že výrobek nemoci **předchází, mírní ji nebo ji vyléčí**, je zakázané **bez ohledu na pravdivost**. Základ je v EU předpisech, které platí v SK i CZ: nařízení (EU) č. 1169/2011, čl. 7 odst. 3, a podle odst. 4 i pro reklamu a obchodní úpravu. U doplňků stravy navíc směrnice 2002/46/ES, čl. 6 odst. 2. Národně: SK zákon 147/2001 Z. z., § 3 ods. 1 písm. f), u doplňků výnos č. 16826/2007-OL, § 17 ods. 2; CZ zákon 40/1995 Sb., § 5d, u doplňků vyhláška 58/2018 Sb., § 3 odst. 4 písm. a). Nález tu může být „rozhodne text“ (checkability `text`).
- **Výjimky u potravin jsou jen dvě:** schválené tvrzení o snížení rizika onemocnění podle čl. 14 nařízení (ES) č. 1924/2006 (jmenuje rizikový faktor, např. cholesterol) a údaje z přílohy III směrnice 2009/54/ES u přírodních minerálních vod (např. „může působit mírně projímavě“). Zdravotní tvrzení o normálních funkcích těla nejsou léčebná. Povolená jsou jen ze seznamu EU, např. „Vitamin C přispívá k normální funkci imunitního systému“ (nařízení (EU) č. 432/2012).
- **Neregistrovaný „bylinný přípravek“ prodávaný jako potravina:** tvrzení je zakázané, protože jde o potravinu. Navíc je výrobek prezentovaný s léčebnými vlastnostmi léčivým přípravkem podle prezentace a bez registrace nesmí na trh ani do reklamy. CZ: zákon 378/2007 Sb., § 2 odst. 1 písm. a) a § 25 odst. 1, zákon 40/1995 Sb., § 5 odst. 3. SK: zákon 362/2011 Z. z., § 46 ods. 1, zákon 147/2001 Z. z., § 8 ods. 4 písm. a).
- **Kosmetika:** nařízení 1223/2009 ani 655/2013 léčebná tvrzení výslovně nezakazují. Kosmetika ale smí mít jen kosmetický účel (čl. 2 odst. 1 písm. a)). Výrobek prezentovaný k léčbě nebo prevenci nemoci podle manuálu pracovní skupiny Komise z června 2025 kosmetikou není; manuál tak řadí „anti-acne“ přípravky, léčbu nebo prevenci atopie a hojení kůže. Podle směrnice 2001/83/ES pak může jít o léčivý přípravek podle prezentace, který bez registrace nesmí na trh. Navrhuji `text`, ale jen s potvrzením právníka.
- **Registrovaný lék:** léčebné tvrzení je povolené v rozsahu souhrnu údajů o přípravku (CZ 40/1995, § 5 odst. 4; SK 147/2001, § 8 ods. 7 písm. a)), nález je jen „k ověření“. Výjimka jsou homeopatika: v CZ homeopatikum registrované zjednodušeně nemá léčebnou indikaci, takže léčebné tvrzení nesmí mít vůbec (378/2007, § 28); v SK smí reklama homeopatika obsahovat jen údaje schválené při registraci (147/2001, § 8 ods. 24).
- **Zdravotnický prostředek:** tvrzení je povolené v rozsahu určeného účelu (nařízení (EU) 2017/745, čl. 7 písm. d)), nález „k ověření“.
- **Ostatní zboží** (náramky, lampy, přístroje, textil): platí jen černá listina, tedy tvrzení je zakázané, jen když je nepravdivé (EU bod 17, CZ písm. q), SK bod 23). Nález je „k ověření“. Určený účel se podle MDR čte i z propagace (čl. 2 bod 12), takže předmět s léčebným účelem by musel splňovat MDR jako zdravotnický prostředek (čl. 2 bod 1, čl. 5 odst. 1); to z webu poznat nejde. **Jen v CZ** platí navíc § 5n zákona 40/1995 Sb.: reklama na výrobek, který není lék, zdravotnický prostředek ani potravina pro zvláštní lékařské účely, nesmí naznačovat, že se jeho používáním zlepší nebo zachová zdravotní stav. Slovenský zákon o reklame 147/2001 Z. z. obdobné ustanovení nemá.
- **Oprava citace v nástroji:** CZ příloha č. 1 písm. q) zákona 634/1992 Sb. zní „nepravdivě prohlašuje, že výrobek nebo služba může vyléčit nemoc, zdravotní poruchu nebo postižení“ [CZ-ZOS ř. 873], ne „…je schopen léčit nemoci, poruchy nebo vady“.
- **Návrh pro Jev:** modul `hc` s 8 otázkami (3 druhy tvrzení, rizikový faktor, 4 druhy výrobku) a 6 pravidly (oddíl 3). Nahrazuje otázky `ucp_cure` a `ucp_is_medicine` a pravidla `ucp_cure_claim` a `ucp_cure_claim_medicine` v `rules/ucp.yaml` (draft3). Nález `text` vznikne jen tehdy, když druh výrobku uvádí text u věty; jinak spadne do obecného pravidla „k ověření“.

## Zdroje

| Zkratka | Soubor v `D:\_github\Overko\podklady\` | Co to je |
| --- | --- | --- |
| FIC | `predpisy-eu/eu-2011-1169-informace-o-potravinach-konsolid-2025-04-01-cs.txt` | Nařízení (EU) č. 1169/2011, konsolidace k 1. 4. 2025 (poslední v Cellaru) |
| NHC | `predpisy-eu/eu-2006-1924-vyzivova-a-zdravotni-tvrzeni-konsolid-2014-12-13-cs.txt` | Nařízení (ES) č. 1924/2006, konsolidace k 13. 12. 2014 (poslední v Cellaru) |
| DS | `predpisy-eu/eu-2002-46-doplnky-stravy-konsolid-2025-11-26-cs.txt` | Směrnice 2002/46/ES o doplňcích stravy, konsolidace k 26. 11. 2025 |
| R432 | `predpisy-eu/eu-2012-432-povolena-zdravotni-tvrzeni-konsolid-2025-08-20-cs.txt` | Nařízení (EU) č. 432/2012, seznam povolených zdravotních tvrzení (asi 225 řádků), konsolidace k 20. 8. 2025 |
| R983 | `predpisy-eu/eu-2009-983-tvrzeni-o-snizeni-rizika-onemocneni-konsolid-2014-07-11-cs.txt` | Nařízení (ES) č. 983/2009, příklad schválených tvrzení o snížení rizika onemocnění |
| FSG | `predpisy-eu/eu-2013-609-potraviny-pro-zvlastni-skupiny-konsolid-2025-09-01-cs.txt` | Nařízení (EU) č. 609/2013 (kojenecká výživa, potraviny pro zvláštní lékařské účely), konsolidace k 1. 9. 2025 |
| MV | `predpisy-eu/eu-2009-54-prirodni-mineralni-vody-cs.txt` | Směrnice 2009/54/ES o přírodních minerálních vodách (jediné znění v Cellaru) |
| KOS | `predpisy-eu/eu-2009-1223-kosmeticke-pripravky-konsolid-2026-05-18-cs.txt` | Nařízení (ES) č. 1223/2009 o kosmetických přípravcích, konsolidace k 18. 5. 2026 |
| K655 | `predpisy-eu/eu-2013-655-kriteria-tvrzeni-kosmetika-cs.txt` | Nařízení Komise (EU) č. 655/2013, společná kritéria pro tvrzení u kosmetiky (nebylo měněno) |
| LP | `predpisy-eu/eu-2001-83-humanni-lecive-pripravky-konsolid-2025-01-01-cs.txt` | Směrnice 2001/83/ES o humánních léčivých přípravcích, konsolidace k 1. 1. 2025 |
| MDR | `predpisy-eu/eu-2017-745-zdravotnicke-prostredky-konsolid-2026-07-19-cs.txt` | Nařízení (EU) 2017/745 o zdravotnických prostředcích, konsolidace k 19. 7. 2026 |
| UCPD | `predpisy-eu/eu-2005-29-ucpd-konsolidace-2026-09-27-cs.txt` | Směrnice 2005/29/ES k 27. 9. 2026 (staženo dříve) |
| Pokyny | `eu-komise/komise-pokyny-ucpd-2021-cs.txt` | Pokyny Komise ke směrnici 2005/29/ES (2021), oddíl 3.3 k bodu 17 (staženo dříve) |
| BM | `eu-komise/komise-borderline-manual-kosmetika-v5-5-2025-06-en.txt` | Manuál pracovní skupiny pro kosmetické přípravky k hranici kosmetika × lék × zdravotnický prostředek, verze 5.5 (červen 2025), anglicky; podle úvodu „NOT LEGALLY BINDING“ [BM ř. 23] |
| CZ-ZOS | `predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt` | Zákon č. 634/1992 Sb. (staženo dříve) |
| CZ-LEK | `predpisy-cz/cz-378-2007-zakon-o-lecivech-zneni-od-2026-01-01.txt` | Zákon č. 378/2007 Sb., o léčivech, znění od 1. 1. 2026 (verze 44) |
| CZ-REK | `predpisy-cz/cz-40-1995-zakon-o-regulaci-reklamy-zneni-od-2025-10-10.txt` | Zákon č. 40/1995 Sb., o regulaci reklamy, znění od 10. 10. 2025 (verze 39) |
| CZ-POT | `predpisy-cz/cz-110-1997-zakon-o-potravinach-zneni-od-2025-07-01.txt` | Zákon č. 110/1997 Sb., o potravinách, znění od 1. 7. 2025 (verze 43) |
| CZ-DS | `predpisy-cz/cz-58-2018-vyhlaska-o-doplncich-stravy-zneni-od-2025-07-01.txt` | Vyhláška č. 58/2018 Sb., o doplňcích stravy, znění od 1. 7. 2025 |
| SK-ZOS | `predpisy-sk/sk-108-2024-ochrana-spotrebitela-zneni-od-2026-09-27.txt` | Zákon č. 108/2024 Z. z. ve znění od 27. 9. 2026 (staženo dříve) |
| SK-LIEK | `predpisy-sk/sk-362-2011-lieky-a-zdravotnicke-pomocky-zneni-od-2026-05-30.txt` | Zákon č. 362/2011 Z. z. o liekoch a zdravotníckych pomôckach, znění od 30. 5. 2026 (poslední na static.slov-lex.sk) |
| SK-REK | `predpisy-sk/sk-147-2001-reklama-zneni-od-2024-07-01.txt` | Zákon č. 147/2001 Z. z. o reklame, znění od 1. 7. 2024 (poslední) |
| SK-POT | `predpisy-sk/sk-152-1995-potraviny-zneni-od-2026-01-01.txt` | Zákon č. 152/1995 Z. z. o potravinách, znění od 1. 1. 2026 (poslední) |
| SK-VD | `predpisy-sk/sk-vynos-16826-2007-OL-vyzivove-doplnky-povodne-znenie.txt` | Výnos MP SR a MZ SR č. 16826/2007-OL (Potravinový kódex, výživové doplnky), původní znění z webu ÚVZ SR; text výnosu ve Sbírce není (jen oznámení 370/2007 Z. z.) |
| SK-UVZ | `predpisy-sk/sk-uvz-vyzivove-doplnky-stranka.txt` | Stránka ÚVZ SR „Výživové (potravinové) doplnky“ s výčtem právního základu |

Citace mají tvar „…“ [zkratka ř. N], čísla řádků platí pro uvedené `.txt`. U výtahů z PDF (BM, SK-VD) jsou zalomení řádků převzatá z PDF, citace proto zabírá víc řádků. Tři tečky značí zkrácení. Nařízení EU platí v SK i CZ přímo; směrnice 2002/46/ES je převedena v CZ vyhláškou 58/2018 Sb. a v SK výnosem 16826/2007-OL.

## 1. Druh výrobku × zákaz bez ohledu na pravdivost

Kontrola: `jev (text)` = nález rozhodne věta s kontextem, pravdivost se nezkoumá. `jev + ověřit` = tvrzení je vidět, zakázané je ale jen mimo schválený rozsah nebo když je nepravdivé.

| Druh výrobku | Zakázané bez ohledu na pravdivost? | EU (platí v SK i CZ) | SK | CZ | Klíčová citace | Kontrola |
| --- | --- | --- | --- | --- | --- | --- |
| Potravina a nápoj (i čaj, minerální voda, potravina pro zvláštní lékařské účely) | **Ano.** Výjimka: schválené tvrzení o snížení rizika onemocnění a údaje z přílohy III u minerálních vod | 1169/2011 čl. 7 odst. 3 a 4; 609/2013 čl. 9 odst. 5; 2009/54/ES čl. 9 odst. 2 | 147/2001 § 3 ods. 1 písm. f); 152/1995 § 12 ods. 2 | 40/1995 § 5d odst. 1 a 2; 110/1997 § 17 odst. 2 písm. b) | „nesmějí informace o potravině připisovat jakékoli potravině vlastnosti umožňující zabránit určité lidské nemoci, zmírnit ji nebo ji vyléčit“ [FIC ř. 325] | jev (text) |
| Doplněk stravy (SK výživový doplnok) | **Ano**, je to potravina a platí i výslovný zákaz | jako výše + 2002/46/ES čl. 6 odst. 2 | výnos 16826/2007-OL § 17 ods. 2 (platnost k 2026 NEOVĚŘENA); 147/2001 § 3 ods. 1 písm. f) | vyhl. 58/2018 § 3 odst. 4 písm. a) (jen označování); 40/1995 § 5d | „Označovanie, prezentácia a reklama nesmie prisudzovať výživovým doplnkom schopnosť prevencie, liečby alebo vyliečenia ľudských chorôb“ [SK-VD ř. 1474–1476] | jev (text) |
| Kosmetika | **Ano, odvozeně.** Výslovný zákaz chybí; výrobek prezentovaný k léčbě nebo prevenci nemoci není kosmetika a může být lékem podle prezentace | 1223/2009 čl. 2 odst. 1 písm. a), čl. 20 odst. 1; 2001/83/ES čl. 1 bod 2 písm. a), čl. 2 odst. 2, čl. 6 odst. 1; BM body 154, 183, 206, 209 (nezávazné) | 362/2011 § 46 ods. 1; v 147/2001 pravidlo pro kozmetiku není, jiné SK předpisy ke kozmetike neprohledány | 378/2007 § 2 odst. 1 písm. a), § 24a odst. 4, § 25 odst. 1; 40/1995 § 5n (rozsah NEOVĚŘEN) | „products presented as having properties to treat or prevent atopy/atopic skin cannot be qualified as cosmetic products“ [BM ř. 1090–1091] | jev (text), potvrdí právník |
| Lék registrovaný (i tradiční rostlinný) | **Ne**, povoleno v rozsahu souhrnu údajů o přípravku. Lék na předpis nesmí do reklamy pro veřejnost. Homeopatikum: CZ bez léčebné indikace, SK jen údaje schválené při registraci | 2001/83/ES čl. 87 odst. 2 | 147/2001 § 8 ods. 4 písm. c), ods. 7 písm. a), ods. 9 písm. k), ods. 24 | 40/1995 § 5 odst. 4, § 5a odst. 2 písm. a), odst. 6, odst. 7 písm. j); 378/2007 § 28 odst. 1 písm. b), odst. 4 | „Jakékoliv informace obsažené v reklamě na humánní léčivý přípravek musí odpovídat údajům uvedeným v souhrnu údajů“ [CZ-REK ř. 189] | jev + ověřit |
| Lék neregistrovaný, „bylinný přípravek“ prodávaný jako potravina | **Ano.** Jako potravina (řádek 1); jako lék podle prezentace bez registrace nesmí na trh ani do reklamy | 1169/2011 čl. 7 odst. 3; 2001/83/ES čl. 1 bod 2 písm. a), čl. 6 odst. 1, čl. 87 odst. 1 | 362/2011 § 2 ods. 7, § 46 ods. 1; 147/2001 § 3 ods. 1 písm. f), § 8 ods. 4 písm. a) | 378/2007 § 2 odst. 1 písm. a), § 24a odst. 4, § 25 odst. 1; 40/1995 § 5 odst. 3, § 5n odst. 1 | „látka nebo kombinace látek prezentovaná s tím, že má léčebné nebo preventivní vlastnosti v případě onemocnění lidí“ [CZ-LEK ř. 45] | jev (text) |
| Zdravotnický prostředek (SK zdravotnícka pomôcka) | **Ne**, povoleno v rozsahu určeného účelu; mimo něj zakázané | MDR čl. 7, čl. 2 bod 12, čl. 5 odst. 1 | jen MDR; národní pravidlo reklamy na pomôcky v 147/2001 ani 362/2011 není | 40/1995 § 5k odst. 3, § 5l odst. 3 písm. a) a c), odst. 4 písm. i) | „navrhují odlišné způsoby použití prostředku, než ty, o nichž je uvedeno, že tvoří součást určeného účelu“ [MDR ř. 733] | jev + ověřit |
| Ostatní zboží (přístroje, šperky, textil, lampy, služby) | **SK: ne**, jen když je nepravdivé (černá listina). **CZ: v reklamě ano** podle § 5n odst. 2 písm. a) (výklad rozsahu NEOVĚŘEN). Předmět s léčebným účelem by musel splňovat MDR | 2005/29/ES příl. I bod 17; MDR čl. 2 body 1 a 12, čl. 5 odst. 1 | 108/2024 príl. č. 1 bod 23 (do 26. 9. 2026 bod 18) | 634/1992 příl. č. 1 písm. q); 40/1995 § 5n | „Nepravdivé tvrdenie, že produkt je schopný liečiť chorobu, dysfunkciu alebo postihnutie.“ [SK-ZOS ř. 7750] | jev + ověřit |

### 1.1 Potravina a nápoj

- EU, zákaz: „S výhradou odchylek stanovených v právních předpisech Unie, které se vztahují na přírodní minerální vody a na potraviny určené pro zvláštní výživu, nesmějí informace o potravině připisovat jakékoli potravině vlastnosti umožňující zabránit určité lidské nemoci, zmírnit ji nebo ji vyléčit, ani na tyto vlastnosti odkazovat.“ [FIC ř. 325] (čl. 7 odst. 3)
- Platí i pro reklamu a obchodní úpravu: „Odstavce 1, 2 a 3 se rovněž použijí na:“ „a) související reklamu;“ „b) obchodní úpravu potravin, jejich tvar, vzhled nebo balení…“ [FIC ř. 329–339] (čl. 7 odst. 4)
- Text e-shopu je informace o potravině: „„informacemi o potravinách“ informace týkající se potravin zpřístupněné konečnému spotřebiteli prostřednictvím etikety, jiného průvodního materiálu nebo jinými prostředky, včetně nástrojů moderních technologií…“ [FIC ř. 121] (čl. 2 odst. 2 písm. a))
- Komise: „Toto obecné ustanovení se vztahuje na provozovatele potravinářských podniků ve všech fázích potravinového řetězce…“ [Pokyny ř. 2388]
- Minerální vody: „Uvedení jakýchkoliv údajů připisujících přírodní minerální vodě vlastnosti týkající se prevence, ošetřování nebo léčby lidských nemocí je zakázáno.“ [MV ř. 193] (čl. 9 odst. 2); povolené údaje z přílohy III, např. „Může působit mírně projímavě“ [MV ř. 467].
- Potraviny pro zvláštní lékařské účely a kojenecká výživa: „…nesmí být zavádějící ani těmto výrobkům nesmí přisuzovat vlastnosti prevence, léčby nebo vyléčení lidských onemocnění, ani podobné vlastnosti naznačovat.“ [FSG ř. 251] (čl. 9 odst. 5). Odchylka „pro zvláštní výživu“ v čl. 7 odst. 3 FIC tedy léčebná tvrzení nepovoluje.
- Výjimka, tvrzení o snížení rizika onemocnění: „Odchylně od čl. 2 odst. 1 písm. b) směrnice 2000/13/ES smějí být uvedena následující tvrzení, pokud bylo … schváleno jejich zahrnutí do seznamu…“ [NHC ř. 449] (čl. 14 odst. 1). Směrnice 2000/13/ES je zrušena a „Odkazy na zrušené právní předpisy se považují za odkazy na toto nařízení.“ [FIC ř. 1577] (čl. 53 odst. 2). Že jde o dnešní čl. 7 odst. 3 FIC, dovozuji ze shodného obsahu; srovnávací tabulku jsem neověřil (NEOVĚŘENO).
- SK: „Reklama nesmie“ … „prezentovať potraviny a výživové doplnky tak, akoby mali účinky liekov,“ [SK-REK ř. 679, 703] (§ 3 ods. 1 písm. f)); „Podmienky používania výživových a zdravotných tvrdení o potravinách ustanovuje osobitný predpis.“ [SK-POT ř. 2175] (§ 12 ods. 2, odkaz na 1924/2006 [SK-POT ř. 3381]).
- CZ: „V reklamě na potraviny mohou být uvedena výživová nebo zdravotní tvrzení za podmínek přímo použitelného předpisu Evropské unie“ [CZ-REK ř. 297] (§ 5d odst. 1); reklama na potraviny musí splňovat požadavky „přímo použitelným předpisem Evropské unie o poskytování informací o potravinách spotřebitelům“ [CZ-REK ř. 299] (§ 5d odst. 2). Přestupek: „…uvádí na trh potravinu klamavě označenou nebo opatřenou zavádějícími informacemi anebo ji nabízí klamavým způsobem,“ [CZ-POT ř. 1351] (§ 17 odst. 2 písm. b)). Zákon 110/1997 Sb. jiné ustanovení k léčebným tvrzením nemá (hledáno „léč“, „nemoc“, „onemocn“).
- Černá listina tu podle Komise mnoho nepřidá: „Bod 17 proto platí pouze doplňkově k stávajícím pravidlům EU týkajícím se zdravotních tvrzení.“ [Pokyny ř. 2383]

### 1.2 Doplněk stravy

- Doplněk stravy je potravina: „„doplňky stravy“ potraviny, jejichž účelem je doplňovat běžnou stravu…“ [DS ř. 65]; CZ „doplňkem stravy potravina…“ [CZ-POT ř. 41] (§ 2 odst. 1 písm. g)); SK „výživovým doplnkom potravina na doplnenie prirodzenej stravy…“ [SK-POT ř. 1333] (§ 2 písm. d)). Platí proto celý oddíl 1.1.
- EU: „Označování, obchodní úprava a reklama nesmějí doplňkům stravy připisovat vlastnosti týkající se prevence, ošetřování nebo léčby lidských nemocí, ani na tyto vlastnosti poukazovat.“ [DS ř. 171] (čl. 6 odst. 2)
- CZ: „(4) Označování nesmí“ „a) doplňkům stravy přisuzovat vlastnosti týkající se prevence, léčby nebo vyléčení lidských onemocnění nebo na tyto vlastnosti odkazovat“ [CZ-DS ř. 61–63] (§ 3 odst. 4 písm. a); jen označování, reklamu pokrývá FIC čl. 7 odst. 4 a DS čl. 6 odst. 2).
- SK: „Označovanie, prezentácia a reklama nesmie prisudzovať výživovým doplnkom schopnosť prevencie, liečby alebo vyliečenia ľudských chorôb alebo odvolávať sa na také schopnosti.“ [SK-VD ř. 1474–1477] (§ 17 ods. 2). ÚVZ SR uvádí výnos jako právní základ: „výnos MP SR a MZ SR 16826/2007-OL … (§ 15 - 17) zmenený a doplnený v rokoch 2009 …, 2010 … a 2014 …“ [SK-UVZ ř. 84–91]. Změny z let 2009, 2010 a 2014 jsem prošel, § 17 nemění (soubory `sk-vynos-*-zmena-*`). Zda výnos platí k 26. 9. 2026 a zda ho později něco nezměnilo: NEOVĚŘENO.
- ÚVZ SR k doplňkům: „nie sú určené na liečbu alebo prevenciu ľudských ochorení“ [SK-UVZ ř. 82].
- Zdravotní tvrzení u doplňků se řídí nařízením 1924/2006: to se použije, „aniž jsou dotčeny“ mimo jiné „d) směrnice 2002/46/ES“ [NHC ř. 167], a přebírá definici „doplněk stravy“ [NHC ř. 177]; CZ „Výživová a zdravotní tvrzení u doplňků stravy se mohou uvést za podmínek přímo použitelného předpisu Evropské unie…“ [CZ-DS ř. 67].

### 1.3 Kosmetika

- Účel kosmetiky: „…výhradně nebo převážně za účelem jejich čištění, parfemace, změny jejich vzhledu, jejich ochrany, jejich udržování v dobrém stavu nebo úpravy tělesných pachů;“ [KOS ř. 189] (čl. 2 odst. 1 písm. a)). Léčba ani prevence nemoci mezi účely není.
- Pravdivost tvrzení: „…nesmějí být používány texty, názvy, ochranné známky, vyobrazení … které by přisuzovaly těmto přípravkům vlastnosti nebo funkce, jež nemají.“ [KOS ř. 1025] (čl. 20 odst. 1). Tvrzení musí být doložená důkazy [K655 ř. 95]. Obojí závisí na pravdivosti.
- O rámci rozhoduje úřad: „Společná kritéria se uplatní, jen bylo-li o dotčeném přípravku rozhodnuto, že je skutečně kosmetickým přípravkem.“ [K655 ř. 25] (odůvodnění 4)
- Lék podle prezentace: „jakákoliv látka nebo kombinace látek představená s tím, že má léčebné nebo preventivní vlastnosti v případě onemocnění lidí“ [LP ř. 73]; v pochybnostech „se použije tato směrnice“ [LP ř. 217] (čl. 2 odst. 2); bez registrace „Žádný léčivý přípravek nesmí být uveden na trh“ [LP ř. 337] (čl. 6 odst. 1). CZ: „Nelze-li po posouzení všech vlastností výrobku jednoznačně určit, zda je léčivým přípravkem nebo jiným výrobkem, platí, že se jedná o léčivý přípravek.“ [CZ-LEK ř. 1137] (§ 24a odst. 4); o pochybnostech rozhoduje SÚKL [CZ-LEK ř. 631] (§ 13 odst. 2 písm. h)).
- Manuál (nezávazný, červen 2025):
  - atopie: vhodné „…“appropriate for/suitable to skins with atopic tendency/atopic skin” can be qualified as cosmetic products…“ [BM ř. 1085–1086], ale „…products presented as having properties to treat or prevent atopy/atopic skin cannot be qualified as cosmetic products.“ [BM ř. 1090–1091] (bod 154),
  - hojení: „…intended to promote the healing of the skin or to treat wounds does not fulfill the definition of cosmetic…“ [BM ř. 1219–1221] (bod 183),
  - akné: přípravky „…presented, either explicitly or implicitly, for use in the prevention or treatment of acne … do not fulfil the definition of a cosmetic product…“ a „…a product that is presented as an ‘anti-acne’ product should not be marketed as a cosmetic product.“ [BM ř. 1340–1345] (bod 209); naopak „suitable for acne-prone skin“ smí, „provided undue prominence is not given to the claim“ [BM ř. 1329–1330] (bod 206).
- Černá listina kosmetiku výslovně zahrnuje: „Zakázaná obchodní praktika podle bodu 17 se vztahuje rovněž na produkty nebo služby, jako jsou kosmetické přípravky, estetická ošetření, wellness produkty…“ [Pokyny ř. 2452]
- Hodnocení: výslovný zákaz „kosmetika nesmí tvrdit léčbu nemoci“ v předpisech není. Zakázanost bez ohledu na pravdivost dovozuji takto: pravdivé tvrzení ukazuje na lék podle prezentace, který bez registrace nesmí na trh; nepravdivé spadá pod černou listinu a čl. 20. Právník musí potvrdit, že to stačí pro `text`.

### 1.4 Registrovaný lék

- EU: „Všechny prvky reklamy na léčivý přípravek musí být v souladu s údaji uvedenými v souhrnu údajů o přípravku.“ [LP ř. 3579] (čl. 87 odst. 2)
- CZ: „Jakékoliv informace obsažené v reklamě na humánní léčivý přípravek musí odpovídat údajům uvedeným v souhrnu údajů tohoto přípravku.“ [CZ-REK ř. 189] (§ 5 odst. 4); veřejnosti nelze propagovat „a) humánní léčivé přípravky, jejichž výdej je vázán pouze na lékařský předpis“ [CZ-REK ř. 201] (§ 5a odst. 2); reklama nesmí „j) poukazovat nevhodným, přehnaným nebo zavádějícím způsobem na možnost uzdravení“ [CZ-REK ř. 241] (§ 5a odst. 7).
- SK: reklama liekov „sa musí v každej časti zhodovať s údajmi uvedenými v súhrne charakteristických vlastností lieku“ [SK-REK ř. 1051] (§ 8 ods. 7 písm. a)); zakázaná je reklama liekov, „ktorých výdaj je viazaný na lekársky predpis“ [SK-REK ř. 1007] (§ 8 ods. 4 písm. c)); reklama pro veřejnost nesmí obsahovat prvek, který „odkazuje nadmerným, hrozivým alebo klamlivým spôsobom na potvrdenie o vyliečení ochorenia“ [SK-REK ř. 1135] (§ 8 ods. 9 písm. k)).
- Homeopatika: zjednodušená registrace jen pro přípravky, kde „ani v jakékoli informaci, která se ho týká, není uvedena léčebná indikace“ [CZ-LEK ř. 1321] (§ 28 odst. 1 písm. b)); na obalu „Homeopatický přípravek bez schválených léčebných indikací“ [CZ-LEK ř. 1329] (§ 28 odst. 4); CZ reklama může obsahovat „pouze údaje uváděné na obalu či v příbalové informaci“ [CZ-REK ř. 219] (§ 5a odst. 6). SK: „V reklame homeopatických liekov sa môžu používať len informácie a údaje schválené pri registrácii homeopatického lieku.“ [SK-REK ř. 1235] (§ 8 ods. 24)

### 1.5 Neregistrovaný lék, „bylinný přípravek“ prodávaný jako potravina

- Jako potravina: oddíl 1.1, zakázané vždy.
- Jako lék podle prezentace: CZ „látka nebo kombinace látek prezentovaná s tím, že má léčebné nebo preventivní vlastnosti v případě onemocnění lidí nebo zvířat“ [CZ-LEK ř. 45] (§ 2 odst. 1 písm. a)); „Léčivý přípravek nesmí být uveden na trh v České republice, pokud mu nebyla udělena“ registrace [CZ-LEK ř. 1147] (§ 25 odst. 1); do reklamy smí jen „humánní léčivý přípravek registrovaný podle zvláštního právního předpisu“ [CZ-REK ř. 187] (§ 5 odst. 3). EU: „Členské státy zakážou jakoukoliv reklamu na léčivý přípravek, pro který nebyla udělena registrace…“ [LP ř. 3575] (čl. 87 odst. 1).
- SK: „Liek je liečivo alebo zmes liečiv a pomocných látok, ktoré sú … určené na ochranu pred chorobami, na diagnostiku chorôb, liečenie chorôb…“ [SK-LIEK ř. 9483] (§ 2 ods. 7). Výslovné „podľa prezentácie“ ani pravidlo pro pochybnosti jsem v 362/2011 nenašel (hledáno „prezentovan“, „pochybnost“, „definíci“); pojem prezentace plyne ze směrnice 2001/83/ES [LP ř. 73]. Registrace: humánne lieky „možno uviesť na trh len na základe povolenia na uvedenie humánneho lieku na trh“ [SK-LIEK ř. 14233] (§ 46 ods. 1); zakázaná je reklama liekov, „ktoré nie sú v Slovenskej republike registrované“ [SK-REK ř. 995–999] (§ 8 ods. 4 písm. a)).
- CZ navíc: „Zakazuje se reklama na výrobek cílící na zdraví, který není léčivým přípravkem, ani zdravotnickým prostředkem, … ani potravinou pro zvláštní lékařské účely, která naznačuje, že výrobek je léčivým přípravkem…“ [CZ-REK ř. 463] (§ 5n odst. 1).
- Pozor na registrované tradiční rostlinné léky; reklama na ně musí nést text CZ „Použití tohoto tradičního rostlinného léčivého přípravku je založeno výlučně na zkušenosti z dlouhodobého použití“ [CZ-LEK ř. 1417] (§ 30 odst. 8 písm. a)), SK „Tradičný rastlinný liek určený na indikácie overené výhradne dlhodobým používaním“ [SK-REK ř. 1087] (§ 8 ods. 8 písm. b) bod 4). To je signál pro řádek 4, ne 5.

### 1.6 Zdravotnický prostředek

- Léčebný účel je součástí definice: prostředek určený „k jednomu nebo několika z těchto konkrétních léčebných účelů:“ „diagnostika, prevence, monitorování, predikce, prognóza, léčba nebo mírnění nemoci,“ [MDR ř. 175–179] (čl. 2 bod 1).
- Určený účel se čte i z propagace: „…podle údajů uvedených výrobcem na označení, v návodu k použití nebo v propagačních nebo prodejních materiálech či prohlášeních…“ [MDR ř. 263] (čl. 2 bod 12).
- Zákaz klamavých tvrzení v reklamě: „…je zakázáno používat text, názvy … které by mohly uživatele nebo pacienta uvést v omyl, pokud jde o určený účel, bezpečnost a účinnost prostředku, tím, že:“ [MDR ř. 717], mimo jiné „připisují prostředku funkce a vlastnosti, které daný prostředek nemá;“ [MDR ř. 721] a „navrhují odlišné způsoby použití prostředku, než ty, o nichž je uvedeno, že tvoří součást určeného účelu…“ [MDR ř. 733] (čl. 7 písm. a) a d)).
- CZ: do reklamy jen prostředek, „který lze uvádět na trh v souladu s nařízením … (EU) 2017/745“ [CZ-REK ř. 393] (§ 5k odst. 3); reklama pro veřejnost musí „a) být formulována tak, aby bylo zřejmé, že výrobek je zdravotnickým prostředkem“ a „c) obsahovat podstatu určeného účelu“ [CZ-REK ř. 413, 417] (§ 5l odst. 3); nesmí „i) poukazovat nevhodným, přehnaným nebo zavádějícím způsobem na možnost uzdravení“ [CZ-REK ř. 439] (§ 5l odst. 4).
- SK: národní pravidla pro reklamu na zdravotnícke pomôcky jsem v 147/2001 ani 362/2011 nenašel (hledáno „zdravotníck“, „reklam“), platí MDR čl. 7.

### 1.7 Ostatní zboží

- Černá listina: EU „Nepravdivé tvrzení, že produkt může vyléčit nemoci, poruchu nebo tělesné postižení.“ [UCPD ř. 1020]; CZ „nepravdivě prohlašuje, že výrobek nebo služba může vyléčit nemoc, zdravotní poruchu nebo postižení,“ [CZ-ZOS ř. 873]; SK „Nepravdivé tvrdenie, že produkt je schopný liečiť chorobu, dysfunkciu alebo postihnutie.“ [SK-ZOS ř. 7750].
- Výklad Komise: „Tento zákaz se týká případů, kdy obchodník tvrdí, že jeho produkt nebo služba může zlepšit nebo vyléčit určité fyzické nebo psychické potíže.“ [Pokyny ř. 2365]; „…tělesným stavům, které jsou z lékařského hlediska považovány za patologie, poruchy nebo tělesná postižení.“ [Pokyny ř. 2385]; příklad masážního křesla „…(včetně vyléčení nemocí páteře a krevního oběhu)“ [Pokyny ř. 2369]; důkazní břemeno nese obchodník: „Pokud obchodník nepředloží náležité a relevantní důkazy … bude se jednat o zakázanou obchodní praktiku podle bodu 17…“ [Pokyny ř. 2456].
- Předmět (ne látka) s léčebným účelem odpovídá definici zdravotnického prostředku (MDR čl. 2 body 1 a 12, oddíl 1.6); u látky jde o lék podle prezentace (oddíl 1.5). „Prostředek může být uveden na trh nebo do provozu pouze tehdy, pokud splňuje požadavky tohoto nařízení…“ [MDR ř. 623] (čl. 5 odst. 1). Zda výrobek shodu má, z webu poznat nejde.
- CZ § 5n odst. 2: „Reklama na výrobek, který není léčivým přípravkem, zdravotnickým prostředkem, diagnostickým zdravotnickým prostředkem in vitro, potravinou pro zvláštní lékařské účely nebo jiným výrobkem, u něhož tento zákon stanoví jinak, nesmí“ [CZ-REK ř. 465] „a) naznačovat, že používáním výrobku se zlepší nebo zachová zdravotní stav toho, kdo jej užívá,“ [CZ-REK ř. 467]. Nepravdivost tu podmínkou není. Otevřené (NEOVĚŘENO): (1) zda je text produktové stránky „reklamou“; definice je široká a zahrnuje „počítačové sítě“ [CZ-REK ř. 19, 21], (2) zda se § 5n vztahuje i na kosmetiku (zákon pro ni „jinak“ nestanoví) a na potraviny (pro ně je § 5d).

## 2. Co je léčebné tvrzení a co ne

### 2.1 Znaky podle předpisů

| Předpis | Co je zakázané připisovat | Citace |
| --- | --- | --- |
| FIC čl. 7 odst. 3 (potraviny) | zabránit nemoci, zmírnit ji, vyléčit ji, i jen odkazovat | „…vlastnosti umožňující zabránit určité lidské nemoci, zmírnit ji nebo ji vyléčit, ani na tyto vlastnosti odkazovat.“ [FIC ř. 325] |
| DS čl. 6 odst. 2 (doplňky) | prevence, ošetřování, léčba | „…vlastnosti týkající se prevence, ošetřování nebo léčby lidských nemocí, ani na tyto vlastnosti poukazovat.“ [DS ř. 171] |
| FSG čl. 9 odst. 5 (zvláštní lékařské účely) | prevence, léčba, vyléčení, i naznačení | „…vlastnosti prevence, léčby nebo vyléčení lidských onemocnění, ani podobné vlastnosti naznačovat.“ [FSG ř. 251] |
| SK výnos § 17 ods. 2 | prevence, liečba, vyliečenie | „…schopnosť prevencie, liečby alebo vyliečenia ľudských chorôb alebo odvolávať sa na také schopnosti.“ [SK-VD ř. 1475–1477] |
| SK 147/2001 § 3 ods. 1 písm. f) | účinky liekov | „…prezentovať potraviny a výživové doplnky tak, akoby mali účinky liekov,“ [SK-REK ř. 703] |
| Lék podle prezentace | léčebné nebo preventivní vlastnosti | „…prezentovaná s tím, že má léčebné nebo preventivní vlastnosti v případě onemocnění…“ [CZ-LEK ř. 45] |
| MDR čl. 2 bod 1 | prevence, léčba, mírnění nemoci | „diagnostika, prevence, monitorování, predikce, prognóza, léčba nebo mírnění nemoci,“ [MDR ř. 179] |
| Černá listina | vyléčit (Komise: „zlepšit nebo vyléčit“) | „…může vyléčit nemoc, zdravotní poruchu nebo postižení,“ [CZ-ZOS ř. 873] |

Léčebné tvrzení tedy má dva znaky: **(a)** účinek typu předejít, zmírnit, léčit, vyléčit nebo hojit a **(b)** předmětem je nemoc, zdravotní porucha, infekce nebo postižení, tedy patologie [Pokyny ř. 2385], ne normální funkce těla, pohoda nebo vzhled. Stačí i nepřímý odkaz („na ledviny při zánětu“, „při chřipce“, „lék na…“), protože FIC i DS zakazují i „odkazovat“ a „poukazovat“.

### 2.2 Zdravotní tvrzení u potravin (povolená jen ze seznamu EU)

- Definice: „„zdravotním tvrzením“ se rozumí každé tvrzení, které uvádí, naznačuje nebo ze kterého vyplývá, že existuje souvislost mezi kategorií potravin, potravinou nebo některou z jejích složek a zdravím;“ [NHC ř. 209] (čl. 2 odst. 2 bod 5)
- Povolená jen schválená: „Zdravotní tvrzení jsou zakázána, pokud … nejsou schválena v souladu s tímto nařízením a obsažena v seznamu schválených tvrzení…“ [NHC ř. 379] (čl. 10 odst. 1). Platí pro označování, obchodní úpravu i reklamu [NHC ř. 141] (čl. 1 odst. 2).
- Příklad povoleného tvrzení: „Vitamin C přispívá k normální funkci imunitního systému“, podmínka „Tvrzení smí být použito pouze u potravin, které jsou přinejmenším zdrojem vitaminu C…“ [R432 ř. 513]. Mluví o normální funkci, ne o nemoci, takže léčebné tvrzení to není.
- Nespecifické „pro zdraví“, „posiluje imunitu“: „…je přípustný pouze tehdy, pokud je doplněn zvláštním zdravotním tvrzením, které je uvedeno v seznamech…“ [NHC ř. 391] (čl. 10 odst. 3). Léčebné tvrzení to není, ale může jít o nepovolené zdravotní tvrzení (kandidát na další modul).
- Zakázaná zdravotní tvrzení bez výjimky, např. „tvrzení, která naznačují, že nekonzumováním dané potraviny by mohlo být ohroženo zdraví“ [NHC ř. 407] (čl. 12 písm. a)).
- Tvrzení o snížení rizika onemocnění: „…spotřeba určité kategorie potravin, potraviny nebo některé z jejích složek významně snižuje riziko vzniku určitého lidského onemocnění;“ [NHC ř. 211] (čl. 2 odst. 2 bod 6); jen schválená (čl. 14 odst. 1) a s povinným údajem, „že se na vzniku onemocnění … podílí více rizikových faktorů“ [NHC ř. 457] (čl. 14 odst. 2). Příklad schváleného znění: „Bylo zjištěno, že rostlinné steroly snižují hladinu cholesterolu v krvi. Vysoká hladina cholesterolu je rizikovým faktorem pro vznik ischemické choroby srdeční“ [R983 ř. 169]. Schválené znění vždy jmenuje rizikový faktor; „chrání před infarktem“ je u potraviny prevence nemoci, a tedy zakázané.
- Obchodní značka jako tvrzení: značku, „které lze považovat za výživová a zdravotní tvrzení“, lze použít jen doplněnou o odpovídající tvrzení [NHC ř. 151] (čl. 1 odst. 3). Značka typu „Artrostop“ proto může být léčebným odkazem.

### 2.3 Kosmetická tvrzení

- Povolený rámec: čištění, parfemace, změna vzhledu, ochrana, udržování v dobrém stavu, úprava pachů [KOS ř. 189]. Tvrzení jako „zklidňuje pokožku“, „hydratuje“, „chrání před UV zářením“ nebo „pro pleť se sklonem k akné“ tam patří, pokud jsou doložená [K655 ř. 95] a pravdivá [KOS ř. 1025].
- Za hranou (oddíl 1.3): léčba nebo prevence atopie [BM ř. 1090–1091], „anti-acne“ [BM ř. 1344–1345], hojení kůže a ran [BM ř. 1219–1221].

### 2.4 Hranice v příkladech

Příklady jsou ilustrativní (moje), zdroj pravidla je ve sloupci „Proč“.

| Věta | Druh výrobku | Léčebné tvrzení? | Proč |
| --- | --- | --- | --- |
| „Vitamin C přispívá k normální funkci imunitního systému.“ | doplněk stravy | ne | schválené zdravotní tvrzení [R432 ř. 513] |
| „Posiluje imunitu.“ | doplněk stravy | ne | nespecifické zdravotní tvrzení, jen se schváleným tvrzením [NHC ř. 391] |
| „Chrání před chřipkou a nachlazením.“ | doplněk stravy | **ano** (prevence) | [FIC ř. 325], [DS ř. 171] |
| „Pomáhá při zánětu močových cest.“ | bylinný čaj | **ano** (zmírnění, léčba) | [FIC ř. 325] |
| „Rostlinné steroly snižují hladinu cholesterolu v krvi; vysoká hladina cholesterolu je rizikovým faktorem…“ | margarín | výjimka, jen ve schváleném znění | [NHC ř. 449], [R983 ř. 169] |
| „Snižuje riziko infarktu.“ | potravina | **ano**, není ve schváleném znění a nejmenuje rizikový faktor | [NHC ř. 379, 449] |
| „Bez lepku, vhodné pro osoby s celiakií.“ | potravina | ne | údaj o vhodnosti, ne účinek na nemoc (můj výklad) |
| „Zklidňuje podrážděnou pokožku.“ | kosmetika | ne | udržování v dobrém stavu [KOS ř. 189] |
| „Vhodné pro pleť se sklonem k akné.“ | kosmetika | ne, pokud není zdůrazněné | [BM ř. 1329–1332] |
| „Anti-akné sérum odstraní zánětlivé pupínky.“ | kosmetika | **ano** | [BM ř. 1340–1345] |
| „Krém léčí atopický ekzém.“ | kosmetika | **ano** | [BM ř. 1090–1091] |
| „Urychluje hojení ran.“ | kosmetika | **ano** | [BM ř. 1219–1221] |
| „Tlumí bolest hlavy.“ | registrovaný lék | ano, povolené podle SPC | [CZ-REK ř. 189], [SK-REK ř. 1051] |
| „Uvolní ucpaný nos při rýmě.“ | zdravotnický prostředek | ano, povolené v rozsahu určeného účelu | [MDR ř. 733] |
| „Masážní křeslo léčí nemoci páteře.“ | ostatní | **ano**, zakázané, když je nepravdivé | [Pokyny ř. 2369] |

## 3. Návrh pravidla pro Jev

### 3.1 Principy

- Druh tvrzení a druh výrobku jsou samostatné znaky: jedna otázka = jeden znak. Otázky se neptají na legalitu, jen na to, co text říká.
- Engine umí v pravidle jednu skupinu `any` vedle `all` a `none` (ověřeno v `src/EshopGuard.Core/Rules/RuleEngine.cs`, ř. 99–114). Proto jsou tři druhy tvrzení v `any` a druh výrobku v `all`.
- `text` jen tam, kde zákaz platí bez ohledu na pravdivost (potravina, doplněk, bylinný přípravek; kosmetika po potvrzení právníkem). Lék a zdravotnický prostředek jsou `verify` s nízkou závažností. Když text druh výrobku neuvádí, vznikne obecný nález `verify`; nic se neumlčí.
- `allowlist_absent` s výrazy „léčivý přípravek“ nebo „zdravotnický prostředek“ k vyloučení nálezů **nepoužívat**. Stránky doplňků často obsahují zápor („není léčivý přípravek“) a nález by zmizel neprávem.

### 3.2 Druh výrobku: povinné údaje, které by na stránce měly být

Legální výrobky musí svůj druh v reklamě nebo označení uvádět. Proto je výskyt údajů dobrý signál pro otázky o druhu výrobku a pro budoucí kontrolu kódem `regex_page` (navržená v rešerši černé listiny, v kódu zatím není).

| Druh | Údaj | Ustanovení |
| --- | --- | --- |
| Doplněk stravy | „doplněk stravy“ v názvu | DS čl. 6 odst. 1 [DS ř. 167]; CZ-DS § 3 odst. 1 písm. a) [CZ-DS ř. 35] |
| Doplněk stravy (CZ reklama) | text „doplněk stravy“ | CZ-REK § 5d odst. 3 [CZ-REK ř. 301] |
| Výživový doplnok (SK) | slová „výživový doplnok“ | SK-VD § 17 ods. 1 písm. a) [SK-VD ř. 1458] |
| Lék (CZ reklama) | „aby bylo zřejmé, že výrobek je humánním léčivým přípravkem“ a výzva k pročtení příbalové informace | CZ-REK § 5a odst. 5 písm. a) a d) [CZ-REK ř. 211, 217] |
| Liek (SK reklama) | „dal jednoznačne identifikovať ako liek“ a výzva na prečítanie písomnej informácie | SK-REK § 8 ods. 8 [SK-REK ř. 1067, 1083] |
| Tradiční rostlinný lék | povinná věta (oddíl 1.5) | [CZ-LEK ř. 1417], [SK-REK ř. 1087] |
| Homeopatikum (CZ) | „Homeopatický přípravek bez schválených léčebných indikací“ | [CZ-LEK ř. 1329] |
| Zdravotnický prostředek (CZ reklama) | „aby bylo zřejmé, že výrobek je zdravotnickým prostředkem“ a podstata určeného účelu | CZ-REK § 5l odst. 3 [CZ-REK ř. 413, 417] |

Chybí-li u léčebného tvrzení údaj o léku nebo zdravotnickém prostředku, je to podezřelé i u skutečného léku: ten porušuje povinnost uvést druh výrobku v reklamě.

### 3.3 Otázky

Věta jde Jevu jako `{sentence, context_before, context_after}`. „Answer no if…“ míří na skrytý předpoklad, který by jinak vedl k „ano“.

- `hc_treat` (léčí, vyléčí, hojí)
  - EN: "Does the sentence (field sentence) claim or suggest that the product cures, heals or treats a disease or a health disorder in people (for example 'cures acne', 'heals eczema', 'treats high blood pressure', 'a remedy for cystitis', 'helps with diabetes')? Answer no if the sentence only mentions a disease without saying that the product acts on it (for example a warning 'not suitable for diabetics', 'suitable for people with coeliac disease' or 'for the dietary management of…'), or if it only describes effects on normal body functions, well-being or appearance (for example 'contributes to the normal function of the immune system', 'soothes dry skin')."
  - CS: „Tvrdí nebo naznačuje věta (pole sentence), že produkt u lidí léčí, vyléčí nebo hojí nemoc či zdravotní poruchu (například „léčí akné“, „hojí ekzém“, „léčí vysoký krevní tlak“, „lék na zánět močového měchýře“, „pomáhá při cukrovce“)? Pokud věta nemoc jen zmiňuje, aniž by tvrdila, že na ni produkt působí (například upozornění „nevhodné pro diabetiky“, „vhodné pro osoby s celiakií“ nebo „k dietnímu postupu při…“), nebo pokud popisuje jen účinky na normální funkce těla, pohodu či vzhled (například „přispívá k normální funkci imunitního systému“, „zklidňuje suchou pokožku“), odpověz ne.“
- `hc_prevent` (předchází, chrání před nemocí)
  - EN: "Does the sentence (field sentence) claim or suggest that the product prevents a disease or a health disorder, or protects people against a disease or an infection (for example 'prevents colds', 'protects against flu', 'prevents osteoporosis', 'stops cancer from developing')? Answer no if the sentence only says that the product supports or maintains a normal body function (for example 'contributes to the normal function of the immune system') without naming a disease or an infection, or if it only protects against external factors such as sun, cold, dirt or odour (for example 'protects the skin from UV rays')."
  - CS: „Tvrdí nebo naznačuje věta (pole sentence), že produkt předchází nemoci či zdravotní poruše nebo chrání před nemocí či infekcí (například „předchází nachlazení“, „chrání před chřipkou“, „zabraňuje osteoporóze“, „brání vzniku rakoviny“)? Pokud věta jen uvádí, že produkt podporuje nebo udržuje normální funkci těla (například „přispívá k normální funkci imunitního systému“), a nejmenuje žádnou nemoc ani infekci, nebo pokud produkt chrání jen před vnějšími vlivy, jako je slunce, chlad, nečistoty nebo pach (například „chrání pokožku před UV zářením“), odpověz ne.“
- `hc_relieve` (mírní nemoc nebo její příznaky)
  - EN: "Does the sentence (field sentence) claim or suggest that the product relieves, reduces or eases a disease, a health disorder or its symptoms (for example 'relieves arthritis pain', 'eases hay fever symptoms', 'reduces migraine attacks', 'soothes a sore throat during a cold')? Answer no if the complaint is a normal everyday state that the sentence does not present as a disease or a health disorder (for example tiredness, occasional stress, dry or tired-looking skin, dandruff, muscle fatigue after sport)."
  - CS: „Tvrdí nebo naznačuje věta (pole sentence), že produkt mírní, zmenšuje nebo ulevuje od nemoci, zdravotní poruchy či jejích příznaků (například „ulevuje od bolesti kloubů při artróze“, „zmírňuje příznaky senné rýmy“, „snižuje počet záchvatů migrény“, „zklidní bolest v krku při nachlazení“)? Pokud jde o běžný stav, který věta nepředstavuje jako nemoc ani zdravotní poruchu (například únava, občasný stres, suchá nebo unavená pleť, lupy, únava svalů po sportu), odpověz ne.“
- `hc_risk_factor` (tvar schváleného tvrzení o snížení rizika)
  - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that the product or one of its ingredients lowers a named risk factor for a disease (for example 'plant sterols have been shown to lower blood cholesterol; high cholesterol is a risk factor in the development of coronary heart disease')? Answer no if the text says that the product prevents, treats or cures the disease itself or reduces the risk of the disease without naming a risk factor that the product lowers."
  - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že produkt nebo jeho složka snižuje konkrétně pojmenovaný rizikový faktor nemoci (například „bylo zjištěno, že rostlinné steroly snižují hladinu cholesterolu v krvi; vysoká hladina cholesterolu je rizikovým faktorem pro vznik ischemické choroby srdeční“)? Pokud text tvrdí, že produkt předchází samotné nemoci, léčí ji, vyléčí ji nebo snižuje riziko nemoci, a nejmenuje rizikový faktor, který produkt snižuje, odpověz ne.“
- `hc_is_food` (potravina, nápoj, doplněk stravy)
  - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) describe the product as a food, a drink or a food supplement, i.e. something that is eaten or drunk (for example 'food supplement', 'herbal tea', 'juice', 'protein bar', capsules 'to supplement the diet')? Answer no if the text says that the product itself is a medicine or a medical device, or if nothing in the text says that the product is eaten or drunk."
  - CS: „Popisuje věta (pole sentence) nebo text hned vedle ní (context_before, context_after) produkt jako potravinu, nápoj nebo doplněk stravy, tedy něco, co se jí nebo pije (například „doplněk stravy“, „bylinný čaj“, „šťáva“, „proteinová tyčinka“, tobolky „k doplnění stravy“)? Pokud text uvádí, že produkt sám je lék nebo zdravotnický prostředek, nebo pokud z textu nevyplývá, že se produkt jí nebo pije, odpověz ne.“
- `hc_is_cosmetic` (kosmetika)
  - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) describe the product as a cosmetic or a body-care product applied to the skin, hair, nails, lips or teeth (for example cream, serum, shampoo, lotion, toothpaste, deodorant)? Answer no if the text says that the product itself is a medicine or a medical device, or if the product is swallowed, inhaled or injected."
  - CS: „Popisuje věta (pole sentence) nebo text hned vedle ní (context_before, context_after) produkt jako kosmetiku nebo přípravek péče o tělo, který se nanáší na pokožku, vlasy, nehty, rty nebo zuby (například krém, sérum, šampon, mléko, zubní pasta, deodorant)? Pokud text uvádí, že produkt sám je lék nebo zdravotnický prostředek, nebo pokud se produkt polyká, vdechuje nebo vstřikuje, odpověz ne.“
- `hc_is_medicine` (lék)
  - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) state that the product is a medicine or a registered medicinal product (for example 'medicinal product', 'léčivý přípravek', 'liek', 'traditional herbal medicinal product', a marketing authorisation number, or a request to read the package leaflet)? Answer no if the text only says that the product contains medicinal herbs, has healing effects or is natural, without stating that the product itself is a medicine, or if it says that the product is not a medicine."
  - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že produkt je lék nebo registrovaný léčivý přípravek (například „léčivý přípravek“, „liek“, „tradiční rostlinný léčivý přípravek“, registrační číslo nebo výzva k pročtení příbalové informace)? Pokud text jen uvádí, že produkt obsahuje léčivé byliny, má léčivé či hojivé účinky nebo je přírodní, a netvrdí, že produkt sám je lék, nebo pokud uvádí, že produkt lékem není, odpověz ne.“
- `hc_is_medical_device` (zdravotnický prostředek)
  - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) state that the product is a medical device (for example 'medical device', 'zdravotnický prostředek', 'zdravotnícka pomôcka', or a CE mark followed by a four-digit notified body number)? Answer no if the text only mentions a CE mark without calling the product a medical device, only says 'dermatologically tested' or 'recommended by doctors', or says that the product is not a medical device."
  - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že produkt je zdravotnický prostředek (například „zdravotnický prostředek“, „zdravotnícka pomôcka“, „medical device“ nebo označení CE se čtyřmístným číslem oznámeného subjektu)? Pokud text jen zmiňuje označení CE a produkt zdravotnickým prostředkem nenazývá, jen uvádí „dermatologicky testováno“ nebo „doporučeno lékaři“, nebo uvádí, že produkt zdravotnickým prostředkem není, odpověz ne.“

### 3.4 Skládání

Výňatek ve formátu `rules/eco.yaml`, bez výchozích hodnot `scope: segment` a `bands`; otázky viz 3.3.

```yaml
version: "hc-2026-09-26-draft1"
module: hc
applies_to: sentence
jurisdictions: [sk, cz]
rules:
  - id: hc_food_disease_claim
    title: "Tvrzení o prevenci, zmírnění nebo léčbě nemoci u potraviny nebo doplňku stravy"
    logic:
      all: [{q: hc_is_food, gte: 0.5}]
      any: [{q: hc_treat, gte: 0.5}, {q: hc_prevent, gte: 0.5}, {q: hc_relieve, gte: 0.5}]
      none: [{q: hc_is_medicine, gte: 0.5}, {q: hc_is_medical_device, gte: 0.5}, {q: hc_risk_factor, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Nařízení (EU) č. 1169/2011, čl. 7 odst. 3 a odst. 4 písm. a) a b)", status: "ověřit"}
      - {jurisdiction: eu, ref: "Směrnice 2002/46/ES, čl. 6 odst. 2 (doplňky stravy)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 147/2001 Z. z., § 3 ods. 1 písm. f)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Výnos MP SR a MZ SR č. 16826/2007-OL, § 17 ods. 2 (výživové doplnky)", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 40/1995 Sb., § 5d odst. 1 a 2", status: "ověřit"}
      - {jurisdiction: cz, ref: "Vyhláška č. 58/2018 Sb., § 3 odst. 4 písm. a) (doplňky stravy)", status: "ověřit"}
    explanation: "U potravin, nápojů a doplňků stravy je zakázané tvrdit, že výrobek nemoci předchází, mírní ji nebo ji léčí, i když by to byla pravda. Výjimkou jsou jen schválená tvrzení o snížení rizika onemocnění."
    recommendation: "Tvrzení odstraňte. Zdravotní účinek lze uvést jen schváleným zdravotním tvrzením z registru EU v povoleném znění."
  - id: hc_food_risk_reduction
    title: "Tvrzení o snížení rizika onemocnění u potraviny k ověření"
    logic:
      all: [{q: hc_is_food, gte: 0.5}, {q: hc_risk_factor, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Nařízení (ES) č. 1924/2006, čl. 10 odst. 1 a čl. 14", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 152/1995 Z. z., § 12 ods. 2", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 40/1995 Sb., § 5d odst. 1", status: "ověřit"}
    explanation: "Tvrzení, že potravina snižuje rizikový faktor nemoci, je povolené jen ve znění schváleném v registru EU a s upozorněním, že na vzniku nemoci se podílí více rizikových faktorů."
    recommendation: "Porovnejte znění s registrem EU a doplňte povinné upozornění."
  - id: hc_cosmetic_disease_claim
    title: "Tvrzení o prevenci, zmírnění nebo léčbě nemoci u kosmetiky"
    logic:
      all: [{q: hc_is_cosmetic, gte: 0.5}]
      any: [{q: hc_treat, gte: 0.5}, {q: hc_prevent, gte: 0.5}, {q: hc_relieve, gte: 0.5}]
      none: [{q: hc_is_medicine, gte: 0.5}, {q: hc_is_medical_device, gte: 0.5}]
    severity: high
    checkability: text   # návrh; zákaz je odvozený z definice kosmetiky, potvrdí právník
    legal_refs:
      - {jurisdiction: eu, ref: "Nařízení (ES) č. 1223/2009, čl. 2 odst. 1 písm. a) a čl. 20 odst. 1", status: "ověřit"}
      - {jurisdiction: eu, ref: "Směrnice 2001/83/ES, čl. 1 bod 2 písm. a), čl. 2 odst. 2 a čl. 6 odst. 1", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 362/2011 Z. z., § 46 ods. 1", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 378/2007 Sb., § 2 odst. 1 písm. a), § 24a odst. 4 a § 25 odst. 1", status: "ověřit"}
    explanation: "Kosmetika smí pokožku čistit, chránit, udržovat v dobrém stavu nebo měnit její vzhled. Výrobek nabízený k léčbě nebo prevenci nemoci, například akné nebo atopického ekzému, kosmetikou není; takto ho lze nabízet jen jako registrovaný lék nebo zdravotnický prostředek."
    recommendation: "Nahraďte tvrzení kosmetickým (například „pro pleť se sklonem k akné“, „zklidňuje suchou pokožku“), nebo doložte, že jde o registrovaný lék či zdravotnický prostředek."
  - id: hc_medicine_claim
    title: "Léčebné tvrzení u léku k ověření"
    logic:
      all: [{q: hc_is_medicine, gte: 0.5}]
      any: [{q: hc_treat, gte: 0.5}, {q: hc_prevent, gte: 0.5}, {q: hc_relieve, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2001/83/ES, čl. 87 odst. 2", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 147/2001 Z. z., § 8 ods. 4 písm. c), ods. 7 písm. a) a ods. 24", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 40/1995 Sb., § 5 odst. 4, § 5a odst. 2 písm. a) a odst. 6", status: "ověřit"}
    explanation: "Reklama na registrovaný lék smí uvádět jen účinky ze souhrnu údajů o přípravku. Lék na předpis se veřejnosti propagovat nesmí a homeopatikum registrované bez indikací nesmí léčebné účinky uvádět vůbec."
    recommendation: "Porovnejte tvrzení se souhrnem údajů o přípravku (SPC) a ověřte, že lék je volně prodejný."
  - id: hc_device_claim
    title: "Tvrzení o účinku zdravotnického prostředku k ověření"
    logic:
      all: [{q: hc_is_medical_device, gte: 0.5}]
      any: [{q: hc_treat, gte: 0.5}, {q: hc_prevent, gte: 0.5}, {q: hc_relieve, gte: 0.5}]
      none: [{q: hc_is_medicine, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Nařízení (EU) 2017/745, čl. 7 a čl. 2 bod 12", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 40/1995 Sb., § 5l odst. 3 a 4", status: "ověřit"}
    explanation: "Zdravotnický prostředek smí slibovat jen účinky v rozsahu určeného účelu, pro který prošel posouzením shody."
    recommendation: "Porovnejte tvrzení s určeným účelem v návodu k použití a v prohlášení o shodě."
  - id: hc_other_disease_claim
    title: "Tvrzení o léčbě nebo prevenci nemoci u výrobku, jehož druh text neuvádí"
    logic:
      any: [{q: hc_treat, gte: 0.5}, {q: hc_prevent, gte: 0.5}, {q: hc_relieve, gte: 0.5}]
      none: [{q: hc_is_food, gte: 0.5}, {q: hc_is_cosmetic, gte: 0.5}, {q: hc_is_medicine, gte: 0.5}, {q: hc_is_medical_device, gte: 0.5}]
    severity: high
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 17", status: "ověřit"}
      - {jurisdiction: eu, ref: "Nařízení (EU) 2017/745, čl. 2 body 1 a 12, čl. 5 odst. 1", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 23", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. q)", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 40/1995 Sb., § 5n", status: "ověřit"}
    explanation: "Nepravdivé tvrzení, že výrobek vyléčí nemoc, je zakázané vždy. Výrobek s léčebným nebo preventivním účelem musí být registrovaný lék nebo zdravotnický prostředek. U potravin a doplňků stravy je takové tvrzení zakázané, i když je pravdivé."
    explanation_by_jurisdiction:
      cz: "Nepravdivé tvrzení, že výrobek vyléčí nemoc, je zakázané vždy. Reklama na výrobek, který není lék, zdravotnický prostředek ani potravina pro zvláštní lékařské účely, navíc nesmí naznačovat, že se jeho používáním zlepší nebo zachová zdravotní stav. U potravin a doplňků stravy je takové tvrzení zakázané, i když je pravdivé."
    recommendation: "Zjistěte, o jaký výrobek jde. Pokud nejde o registrovaný lék ani zdravotnický prostředek, tvrzení odstraňte."
```

Vztah k současné sadě: `rules/ucp.yaml` (draft3) má otázky `ucp_cure` a `ucp_is_medicine` a pravidla `ucp_cure_claim` (high, verify) a `ucp_cure_claim_medicine` (low, verify); `rules/CHANGELOG.md` rozlišení potravin, doplňků a kosmetiky odkládá na tuto rešerši. Návrh: `hc_other_disease_claim` nahradí `ucp_cure_claim`, `hc_medicine_claim` a `hc_device_claim` nahradí `ucp_cure_claim_medicine` a obě otázky `ucp_*` z `ucp` vypadnou. Samostatný modul znamená jedno volání Jevu navíc na větu s 8 otázkami (anglicky asi 4 200 znaků). Druhá možnost je vložit otázky do `ucp` (14 − 2 + 8 = 20 otázek v jednom volání); vliv počtu otázek na přesnost je NEOVĚŘENÝ. Vysvětlení u `ucp_cure_claim` („U potravin, doplňků stravy a kosmetiky zakazují připisovat léčebné účinky i odvětvové předpisy.“) platí doslova pro potraviny a doplňky; u kosmetiky je zákaz jen odvozený (oddíl 1.3).

CZ § 5n (oddíl 1.7) by pro ostatní zboží v ČR dával `text`, na Slovensku jen `verify`. Engine má checkability na pravidle a otázky jsou vázané na sadu, takže samostatné CZ pravidlo by potřebovalo vlastní sadu s kopiemi otázek. Navrhuji to až po stanovisku právníka k rozsahu § 5n.

### 3.5 Testovací věty

A = ano, N = ne; očekávané odpovědi a pravidlo. Věty jsou ilustrativní, do vzorku je potřeba přidat reálné věty z e-shopů.

| # | Věta (a kontext) | treat | prevent | relieve | risk | food | cosm | med | md | Pravidlo |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | „Bylinný čaj na ledviny pomáhá při zánětu močových cest.“ | A | N | A | N | A | N | N | N | hc_food_disease_claim |
| 2 | „Doplněk stravy s vitaminem C chrání před chřipkou.“ | N | A | N | N | A | N | N | N | hc_food_disease_claim |
| 3 | „Doplněk stravy s vitaminem C. Vitamin C přispívá k normální funkci imunitního systému.“ | N | N | N | N | A | N | N | N | žádné |
| 4 | „Rostlinné steroly snižují hladinu cholesterolu v krvi. Vysoká hladina cholesterolu je rizikovým faktorem pro vznik ischemické choroby srdeční.“ (margarín) | N | N | N | A | A | N | N | N | hc_food_risk_reduction |
| 5 | „Bez lepku, vhodné pro osoby s celiakií.“ | N | N | N | N | A | N | N | N | žádné |
| 6 | „Tento krém léčí atopický ekzém.“ | A | N | N | N | N | A | N | N | hc_cosmetic_disease_claim |
| 7 | „Krém pro pokožku se sklonem k atopii zklidňuje suchou pokožku.“ | N | N | N | N | N | A | N | N | žádné |
| 8 | „Anti-akné sérum odstraní zánětlivé pupínky.“ | A | N | A | N | N | A | N | N | hc_cosmetic_disease_claim |
| 9 | „Čisticí gel pro pleť se sklonem k akné čistí póry.“ | N | N | N | N | N | A | N | N | žádné |
| 10 | „Léčivý přípravek k léčbě bolesti hlavy. Čtěte pečlivě příbalovou informaci.“ | A | N | A | N | N | N | A | N | hc_medicine_claim |
| 11 | „Zdravotnický prostředek: nosní sprej s mořskou vodou uvolní ucpaný nos při rýmě.“ | N | N | A | N | N | N | N | A | hc_device_claim |
| 12 | „Magnetický náramek vyléčí artrózu.“ | A | N | N | N | N | N | N | N | hc_other_disease_claim |
| 13 | „Solná lampa chrání před astmatem.“ | N | A | N | N | N | N | N | N | hc_other_disease_claim |
| 14 | „Nevhodné pro osoby s cukrovkou.“ | N | N | N | N | – | N | N | N | žádné |
| 15 | „Homeopatický přípravek bez schválených léčebných indikací. Pomáhá při nachlazení.“ | N | N | A | N | N | N | A | N | hc_medicine_claim (homeopatikum bez indikací tvrzení mít nemá, oddíl 1.4) |
| 16 | „Respirátor FFP2 chrání před viry.“ | N | A | N | N | N | N | N | N | hc_other_disease_claim (falešně pozitivní, viz 3.6) |

### 3.6 Rizika a mezery

- **Druh výrobku bývá daleko od věty** (název produktu, kategorie, drobečková navigace). Bez něj pravidla pro potraviny a kosmetiku nevzniknou a nález spadne do `hc_other_disease_claim` (`verify`). To je bezpečné, ale ztrácí se `text`. Návrh: přidat Jevu kontext stránky (název produktu a kategorii) nebo samostatnou otázku na úrovni stránky. V kódu nic takového není.
- **Zápory a upozornění** („není léčivý přípravek“, „nevhodné pro diabetiky“) řeší „Answer no“ v otázkách. Umlčovat přes `allowlist_absent` nedoporučuji (oddíl 3.1).
- **Osobní ochranné prostředky, opalovací krémy:** „chrání před viry“ nebo „chrání před rakovinou kůže“ skončí jako prevence nemoci. U respirátoru jde nejspíš o ochranný prostředek, u opalovacího krému o hraniční tvrzení. Nezkoumal jsem (NEOVĚŘENO); zatím jen `verify`.
- **Potraviny pro zvláštní lékařské účely** mají v označení uvádět, pro jaké pacienty jsou určené. Přesné povinné znění (nařízení v přenesené pravomoci (EU) 2016/128) jsem nestahoval (NEOVĚŘENO); „k dietnímu postupu při…“ je proto v „Answer no“ otázky `hc_treat`.
- **Obchodní značky a názvy** („Artrostop“, „Prostamax“) mohou být léčebným odkazem [NHC ř. 151]. Zda je Jev u samotného názvu pozná, je NEOVĚŘENO.
- **Zvířata:** otázky se ptají na lidi, veterinární přípravky a krmiva nejsou zahrnuty.
- **Přesnost Jevu u těchto otázek** a vliv 8 otázek v jednom požadavku: NEOVĚŘENO, potřebuje označený vzorek.

## 4. Stažené soubory

Staženo 26. 9. 2026. EU předpisy z Cellaru s hlavičkami `Accept: application/xhtml+xml` a `Accept-Language: ces`; nejnovější konsolidace podle SPARQL `https://publications.europa.eu/webapi/rdf/sparql`. Textové výtahy (`.txt`) vedle originálů.

| Soubor | Co to je | Zdroj |
| --- | --- | --- |
| `predpisy-eu/eu-2011-1169-informace-o-potravinach-konsolid-2025-04-01-cs.*` | Nařízení (EU) č. 1169/2011, čl. 2, 7, 53 | `http://publications.europa.eu/resource/celex/02011R1169-20250401` |
| `predpisy-eu/eu-2006-1924-vyzivova-a-zdravotni-tvrzeni-konsolid-2014-12-13-cs.*` | Nařízení (ES) č. 1924/2006, čl. 1–3, 10–14 | `…/celex/02006R1924-20141213` |
| `predpisy-eu/eu-2002-46-doplnky-stravy-konsolid-2025-11-26-cs.*` | Směrnice 2002/46/ES, čl. 2 a 6 | `…/celex/02002L0046-20251126` |
| `predpisy-eu/eu-2012-432-povolena-zdravotni-tvrzeni-konsolid-2025-08-20-cs.*` | Nařízení (EU) č. 432/2012, seznam povolených zdravotních tvrzení (vhodné jako budoucí seznam pro kód) | `…/celex/02012R0432-20250820` |
| `predpisy-eu/eu-2009-983-tvrzeni-o-snizeni-rizika-onemocneni-konsolid-2014-07-11-cs.*` | Nařízení (ES) č. 983/2009, schválená tvrzení o snížení rizika onemocnění | `…/celex/02009R0983-20140711` |
| `predpisy-eu/eu-2013-609-potraviny-pro-zvlastni-skupiny-konsolid-2025-09-01-cs.*` | Nařízení (EU) č. 609/2013, čl. 2 a 9 | `…/celex/02013R0609-20250901` |
| `predpisy-eu/eu-2009-54-prirodni-mineralni-vody-cs.*` | Směrnice 2009/54/ES, čl. 9 a příloha III | `…/celex/02009L0054-20090716` |
| `predpisy-eu/eu-2009-1223-kosmeticke-pripravky-konsolid-2026-05-18-cs.*` | Nařízení (ES) č. 1223/2009, čl. 2 a 20 | `…/celex/02009R1223-20260518` |
| `predpisy-eu/eu-2013-655-kriteria-tvrzeni-kosmetika-cs.*` | Nařízení Komise (EU) č. 655/2013 | `…/celex/32013R0655` |
| `predpisy-eu/eu-2001-83-humanni-lecive-pripravky-konsolid-2025-01-01-cs.*` | Směrnice 2001/83/ES, čl. 1, 2, 6, 87 | `…/celex/02001L0083-20250101` |
| `predpisy-eu/eu-2017-745-zdravotnicke-prostredky-konsolid-2026-07-19-cs.*` | Nařízení (EU) 2017/745, čl. 2, 5, 7 | `…/celex/02017R0745-20260719` |
| `eu-komise/komise-borderline-manual-kosmetika-v5-5-2025-06-en.pdf` a `.txt` | Borderline manual ke kosmetice, verze 5.5 (červen 2025) | `https://single-market-economy.ec.europa.eu/document/download/93257feb-2b4d-4b85-a851-225f1ccf3c9b_en?filename=Borderline%20Manual` |
| `predpisy-cz/cz-378-2007-zakon-o-lecivech-zneni-od-2026-01-01.*` | Zákon o léčivech, § 2, 13, 24a, 25, 28, 30 | `https://www.zakonyprolidi.cz/cs/2007-378` |
| `predpisy-cz/cz-40-1995-zakon-o-regulaci-reklamy-zneni-od-2025-10-10.*` | Zákon o regulaci reklamy, § 1, 5–5a, 5d, 5k–5n | `https://www.zakonyprolidi.cz/cs/1995-40` |
| `predpisy-cz/cz-110-1997-zakon-o-potravinach-zneni-od-2025-07-01.*` | Zákon o potravinách, § 2 a 17 | `https://www.zakonyprolidi.cz/cs/1997-110` |
| `predpisy-cz/cz-58-2018-vyhlaska-o-doplncich-stravy-zneni-od-2025-07-01.*` | Vyhláška o doplňcích stravy, § 3 | `https://www.zakonyprolidi.cz/cs/2018-58` |
| `predpisy-sk/sk-362-2011-lieky-a-zdravotnicke-pomocky-zneni-od-2026-05-30.*` | Zákon o liekoch a zdravotníckych pomôckach, § 2, 46 | `https://static.slov-lex.sk/static/SK/ZZ/2011/362/20260530.html` |
| `predpisy-sk/sk-147-2001-reklama-zneni-od-2024-07-01.*` | Zákon o reklame, § 2, 3, 8 | `https://static.slov-lex.sk/static/SK/ZZ/2001/147/20240701.html` |
| `predpisy-sk/sk-152-1995-potraviny-zneni-od-2026-01-01.*` | Zákon o potravinách, § 2 a 12 | `https://static.slov-lex.sk/static/SK/ZZ/1995/152/20260101.html` |
| `predpisy-sk/sk-vynos-16826-2007-OL-vyzivove-doplnky-povodne-znenie.*` | Výnos MP SR a MZ SR č. 16826/2007-OL, původní znění (§ 17) | `https://www.uvzsr.sk/documents/41637/43962/16826_2007.pdf/b5648b0e-8a6e-8e55-9030-5b7f318afc1f` |
| `predpisy-sk/sk-vynos-20374-2009-OL-zmena-*`, `sk-vynos-09015-2010-OL-zmena-*`, `sk-vynos-2014-06-17-zmena-*` | Změny výnosu z let 2009, 2010 a 2014 (§ 17 nemění) | odkazy ze stránky ÚVZ SR (`uvzsr.sk/documents/41637/43962/…`) |
| `predpisy-sk/sk-uvz-vyzivove-doplnky-stranka.*` | Stránka ÚVZ SR k výživovým doplnkům | `https://www.uvzsr.sk/web/uvz/vyzivove-potravinove-doplnky1` |

Verze slovenských zákonů jsou poslední v historii na static.slov-lex.sk k 26. 9. 2026. Budoucí znění tam není uvedené.

## NEOVĚŘENO a otevřené otázky

1. Zda je text produktové stránky e-shopu „reklamou“ podle CZ 40/1995 § 1 odst. 2 a SK 147/2001 § 2 ods. 1 písm. a). Pro potraviny to nevadí, FIC pokrývá informace o potravinách i reklamu.
2. Rozsah CZ § 5n zákona 40/1995: zda se vztahuje na kosmetiku a potraviny a zda by v ČR dával `text` i u ostatního zboží. Kterou novelou byl zaveden, jsem nezjišťoval.
3. Platnost SK výnosu 16826/2007-OL k 26. 9. 2026 a jeho případné změny po roce 2014.
4. Že čl. 2 odst. 1 písm. b) směrnice 2000/13/ES odpovídá čl. 7 odst. 3 FIC, dovozuji ze shodného znění; srovnávací tabulku jsem neověřil.
5. SK zákon 362/2011 nemá výslovné „liek podľa prezentácie“ ani pravidlo pro pochybnosti. Jak slovenské úřady prezentaci posuzují, jsem neověřoval.
6. Kosmetika: `text` stojí na odvození z definice a na nezávazném manuálu. Potřebné je stanovisko právníka.
7. Hraniční výrobky (respirátory, opalovací krémy, potraviny pro zvláštní lékařské účely a povinné znění podle nařízení 2016/128) jsem nezkoumal.
8. Přesnost otázek u Jevu je neověřená, chybí označený vzorek.
