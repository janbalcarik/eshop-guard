# Kontrola textů e-shopu fixture.test

> Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.

> **Běh s falešným klientem (mock):** pravděpodobnosti nepocházejí z Jevu, nálezy slouží jen k ověření průchodu aplikací.

| | |
| --- | --- |
| Web | http://fixture.test/ |
| Datum | - |
| Moduly |  |
| Země | SK |
| Sady otázek | eco-2026-09-30-draft14 (eco.yaml), dur-2026-09-30-draft3 (dur.yaml), ucp-2026-09-26-draft4 (ucp.yaml), legal-sk-2026-09-26-draft4 (legal_sk.yaml) |
| Model | mock |
| Jazyk otázek | angličtina |

## Statistika

- Stránky: 15 (úvodní 1, produktová 11, právní 2, ostatní 1)
- Viditelný text: zkontrolováno 63 % (hlavní text, hlavička a patička, ostatní text stránky), navigace a filtry 37 % záměrně vynechané, výpisy jiných produktů 0 % (kontrolují se na stránce produktu), jinak nezkontrolováno 0 %
- Profily šablon stránek: použito 0 (z toho nových 0, plánováno nových 1, nevytvořeny), stránky podle profilu 0, bez profilu 13 (kontrolované celé)
- Stahování: 17 požadavků
- Segmenty: 190 výskytů, 142 unikátních (133 vět, 9 právních odstavců, 3 šablonových)
- Volání Jevu: 131, odpovědi z cache: 0, chyby: 0
- Síto po odstavcích (úseky do 600 znaků, práh 0,2): 13 úseků, volání 13, z cache 0, nevyhodnoceno 0; z podrobné kontroly vynechalo 124 z 399 dvojic věta × modul (31 %)
- Vstupní tokeny: 311 887 (odhad falešného klienta)
- Odhad ceny: 0,013099 USD (nic se neplatilo)
- Doba běhu: -

## Souhrn

Porušení podle textu zákona: 11, K posouzení: 2, K ověření: 10.

| Skupina | Pravidlo | Závažnost | Vysoká jistota | Nižší jistota |
| --- | --- | --- | ---: | ---: |
| Porušení podle textu zákona | Chýba poučenie o práve podať žiadosť o nápravu | vysoká | 1 | 0 |
| Porušení podle textu zákona | Chýba odkaz na informácie o subjekte alternatívneho riešenia sporov | vysoká | 1 | 0 |
| Porušení podle textu zákona | Obecné environmentální tvrzení bez upřesnění | vysoká | 3 | 0 |
| Porušení podle textu zákona | Klimatické tvrzení o produktu založené na kompenzacích | vysoká | 1 | 0 |
| Porušení podle textu zákona | „Udržitelný“ nebo „odpovědný“ bez upřesnění | vysoká | 1 | 0 |
| Porušení podle textu zákona | Odměna za kladnou recenzi | vysoká | 1 | 0 |
| Porušení podle textu zákona | Odznak nebo značka s obecným environmentálním výrazem | vysoká | 1 | 0 |
| Porušení podle textu zákona | Tvrzení o celém produktu, ačkoli se týká jen části | střední | 1 | 0 |
| Porušení podle textu zákona | Odkaz na zrušenú platformu ODR | nízká | 0 | 1 |
| K posouzení | Odznak nebo symbol, který může působit jako značka udržitelnosti | střední | 1 | 0 |
| K posouzení | Možné obecné environmentální tvrzení (posuzuje se případ od případu) | střední | 1 | 0 |
| K ověření | Chýba harmonizované oznámenie o zákonnej zodpovednosti za vady (nové od 27. 9. 2026) | vysoká | 0 | 1 |
| K ověření | Chýba funkcia „odstúpiť od zmluvy tu“ (povinná od 19. 6. 2026) | vysoká | 0 | 1 |
| K ověření | Budoucí environmentální závazek k ověření | střední | 1 | 0 |
| K ověření | Tvrzení o klimatické neutralitě firmy založené na kompenzacích | střední | 1 | 0 |
| K ověření | Navádzanie na skoršiu výmenu spotrebného materiálu k ověření (nové od 27. 9. 2026) | střední | 1 | 0 |
| K ověření | Tvrzení, že neoriginální díly poškodí výrobek, k ověření (nové od 27. 9. 2026) | střední | 1 | 0 |
| K ověření | Jmenovaná značka udržitelnosti mimo seznam ověřených | střední | 2 | 0 |
| K ověření | Tvrzení o ověřených recenzích k ověření | střední | 1 | 0 |
| K ověření | Tvrzení o životnosti výrobku k ověření (nové od 27. 9. 2026) | střední | 1 | 0 |

Jistota říká, jak si je Jev jistý, že text odpovídá popisu pravidla; zda jde o porušení, určuje skupina.

## Porušení podle textu zákona (11)

Text webu splňuje znaky zákazu tak, jak je popisuje zákon nebo jeho odůvodnění; výjimky (například ekoznačka EU) uvádí vysvětlení u pravidla.

### Chýba poučenie o práve podať žiadosť o nápravu

Pravidlo `legal_redress_request_missing`, závažnost vysoká, porušení podle textu zákona.

Súčasťou povinných informácií je poučenie, že spotrebiteľ môže obchodníkovi podať žiadosť o nápravu, ak nie je spokojný s vybavením reklamácie alebo sa domnieva, že obchodník porušil jeho práva.

**Doporučení:** Doplňte do obchodných podmienok poučenie o práve podať žiadosť o nápravu a kam ju poslať.

**Předpisy:**

- SK: § 5 ods. 1 písm. q) zákona č. 108/2024 Z. z. v znení od 27. 9. 2026; § 11 zákona č. 391/2015 Z. z. (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,91, vysoká jistota
   - Nejbližší odstavec: „Príloha: Vzorový formulár na odstúpenie od zmluvy – Oznamujem, že odstupujem od zmluvy na tento tovar: … Dátum objednania: … Meno a priezvisko spotrebiteľa: … Adresa spotrebiteľa: … Dátum: …“
   - Stránky (1): http://fixture.test/obchodne-podmienky.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,09, práh přítomnosti je 0,70.

### Chýba odkaz na informácie o subjekte alternatívneho riešenia sporov

Pravidlo `legal_adr_missing`, závažnost vysoká, porušení podle textu zákona.

Predávajúci musí spotrebiteľa poučiť o práve podať žiadosť o nápravu s uvedením odkazu na webové sídlo, na ktorom sú zverejnené informácie o príslušnom subjekte alternatívneho riešenia sporov. Uvedenie SOI len ako orgánu dozoru alebo odkaz na zrušenú platformu ODR túto povinnosť nesplní.

**Doporučení:** Doplňte do obchodných podmienok odkaz na webové sídlo s informáciami o príslušnom subjekte alternatívneho riešenia sporov (pri bežnom tovare SOI).

**Předpisy:**

- SK: § 5 ods. 1 písm. q) a ods. 3 zákona č. 108/2024 Z. z. v znení od 27. 9. 2026 (do 26. 9. 2026 písm. l) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,90, vysoká jistota
   - Nejbližší odstavec: „Vybavenie reklamácie – O vybavení reklamácie vás budeme informovať e-mailom a vydáme vám doklad o dátume a spôsobe vybavenia reklamácie.“
   - Stránky (1): http://fixture.test/reklamacny-poriadok.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,10, práh přítomnosti je 0,70.

### Obecné environmentální tvrzení bez upřesnění

Pravidlo `eco_generic_claim`, závažnost vysoká, porušení podle textu zákona.

Věta používá výraz, který odůvodnění 9 směrnice (EU) 2024/825 uvádí jako příklad obecného environmentálního tvrzení (například „ekologický“, „šetrný k životnímu prostředí“, „zelený“, „přátelský k přírodě“), nebo přímo tvrdí přínos pro životní prostředí, přírodu či klima. Bez upřesnění jasně a výrazně přímo u tvrzení je zakázané, pokud obchodník neprokáže uznaný vynikající environmentální profil relevantní pro tvrzení (ekoznačka EU nebo úředně uznaná ekoznačka typu I). Certifikované biopotraviny smí „bio“ a „eko“ používat.

**Doporučení:** Tvrzení odstraňte, nebo ho nahraďte konkrétním a doložitelným údajem přímo u něj.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 4a, ve znění směrnice (EU) 2024/825; odůvodnění 9 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 6, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Tento šampón je ekologický a šetrný k prírode.“ – skóre 0,87, vysoká jistota
   - Kontext: …Ekologický šampón s levanduľou **[věta]** Balenie obsahuje 250 ml a vydrží približne 2 mesiace. Cena: 8,90 €…
   - Pravděpodobnosti: eco_claim=0.872; eco_generic=0.873; eco_explicit_term=0.917; eco_organic_food=0.027; eco_sustainable_term=0.061; eco_neutral=0.084; eco_other_subject=0.023; eco_diy=0.083; eco_audience=0.031
   - Stránky (1): http://fixture.test/produkt-1.html

2. „Ekologický produkt – obal je z recyklovaného papiera.“ – skóre 0,87, vysoká jistota
   - Kontext: …Tuhý šampón s pŕhľavou Vegan **[věta]** Šampón vystačí zhruba na 60 umytí.…
   - Pravděpodobnosti: eco_claim=0.869; eco_generic=0.903; eco_explicit_term=0.892; eco_organic_food=0.032; eco_sustainable_term=0.043; eco_neutral=0.043; eco_other_subject=0.055; eco_diy=0.070; eco_audience=0.051
   - Stránky (1): http://fixture.test/produkt-3.html

3. „Ekologický šampón s levanduľou“ – skóre 0,86, vysoká jistota
   - Kontext: … **[věta]** Tento šampón je ekologický a šetrný k prírode. Balenie obsahuje 250 ml a vydrží približne 2 mesiace.…
   - Pravděpodobnosti: eco_claim=0.901; eco_generic=0.864; eco_explicit_term=0.883; eco_organic_food=0.025; eco_sustainable_term=0.043; eco_neutral=0.034; eco_other_subject=0.050; eco_diy=0.031; eco_audience=0.035
   - Stránky (1): http://fixture.test/produkt-1.html

### Klimatické tvrzení o produktu založené na kompenzacích

Pravidlo `eco_neutrality`, závažnost vysoká, porušení podle textu zákona.

Tvrzení, že produkt (zboží nebo služba, například doprava) má neutrální, snížený nebo pozitivní dopad na klima, je zakázané vždy, pokud stojí na kompenzaci emisí mimo hodnotový řetězec produktu. Povolené je jen tvrzení založené na skutečném snížení emisí během životního cyklu produktu.

**Doporučení:** Tvrzení odstraňte. O podpoře klimatických projektů můžete informovat, ale ne jako o vlastnosti produktu.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 4c, ve znění směrnice (EU) 2024/825; odůvodnění 12 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 8, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Doprava je klimaticky neutrálna vďaka kompenzácii emisií.“ – skóre 0,87, vysoká jistota
   - Kontext: …Prírodná kozmetika zo Záhoria Vyrábame mydlá a šampóny z byliniek, ktoré pestujeme vo vlastnej záhrade pri Senici. **[věta]** Objednávky odosielame do 2 pracovných dní cez Packetu alebo Slovenskú poštu.…
   - Pravděpodobnosti: eco_neutral=0.884; eco_offset_basis=0.870; eco_company_level=0.032
   - Stránky (1): http://fixture.test/

### „Udržitelný“ nebo „odpovědný“ bez upřesnění

Pravidlo `eco_sustainable_claim`, závažnost vysoká, porušení podle textu zákona.

Tvrzení „udržitelný“, „odpovědný“ nebo „uvědomělý“ se kromě životního prostředí týkají i sociálních znaků, proto je podle odůvodnění směrnice EmpCo nelze opřít jen o ekoznačku. Bez jasného upřesnění přímo u tvrzení jde o zakázané obecné tvrzení.

**Doporučení:** Tvrzení odstraňte, nebo přímo u něj uveďte, v čem konkrétně a doložitelně je produkt udržitelný.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 4a, ve znění směrnice (EU) 2024/825; odůvodnění 10 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 6, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Sme zodpovedná a udržateľná firma.“ – skóre 0,87, vysoká jistota
   - Kontext: …O nás **[věta]** Ako firma sme klimaticky neutrálni vďaka výsadbe stromov. Do roku 2030 budeme vyrábať úplne bez emisií.…
   - Pravděpodobnosti: eco_sustainable_term=0.867; eco_generic=0.919; eco_other_subject=0.083; eco_diy=0.093; eco_audience=0.082
   - Stránky (1): http://fixture.test/o-nas.html

### Odměna za kladnou recenzi

Pravidlo `ucp_review_reward_positive`, závažnost vysoká, porušení podle textu zákona.

Odměna za kladné hodnocení vede k falešným recenzím; zadávání falešných recenzí je zakázané za všech okolností.

**Doporučení:** Odměnu za recenzi nepodmiňujte jejím obsahem ani hodnocením.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23c; Pokyny Komise 2021/C 526/01, oddíl 4.2.4 (odměna za zveřejnění pozitivních recenzí) (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 32 (do 26. 9. 2026 bod 27) (status: ověřit)

1. „Za hodnotenie 5 hviezdičkami vám vrátime 5 €.“ – skóre 0,87, vysoká jistota
   - Kontext: …Cena: 8,90 € Recenzie **[věta]** …
   - Pravděpodobnosti: ucp_review_reward=0.865; ucp_review_reward_positive=0.874
   - Stránky (1): http://fixture.test/produkt-1.html

### Odznak nebo značka s obecným environmentálním výrazem

Pravidlo `eco_label_generic_term`, závažnost vysoká, porušení podle textu zákona.

Odznak nebo značka bez vlastního názvu certifikačního systému (například „Eco“ nebo „Green“ u výrobku) je značkou udržitelnosti, která nevychází z certifikačního systému (bod 3), a zároveň výrazem, který odůvodnění 9 směrnice (EU) 2024/825 uvádí jako příklad obecného environmentálního tvrzení (bod 6). Je zakázaná, pokud výrobek nemá ekoznačku EU nebo úředně uznanou ekoznačku typu I.

**Doporučení:** Odznak odstraňte, nebo ho nahraďte skutečnou certifikovanou značkou, kterou výrobek má.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 2a, ve znění směrnice (EU) 2024/825 (označení udržitelnosti zahrnuje environmentální i sociální znaky) (status: ověřit)
- EU: Směrnice 2005/29/ES, příloha I bod 4a, ve znění směrnice (EU) 2024/825; odůvodnění 9 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 3, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 6, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Eco“ – skóre 0,86, vysoká jistota
   - Kontext: …Tuhý dezodorant s levanduľou **[věta]** Vegan Doprava zadarmo nad 50 €…
   - Pravděpodobnosti: eco_label=0.937; eco_explicit_term=0.864; eco_named_label=0.058; eco_organic_food=0.094; eco_generic=0.046; eco_other_subject=0.048; eco_diy=0.056
   - Stránky (1): http://fixture.test/produkt-9.html

### Tvrzení o celém produktu, ačkoli se týká jen části

Pravidlo `eco_part_as_whole`, závažnost střední, porušení podle textu zákona.

Environmentální tvrzení o celém produktu nebo celé firmě je zakázané, pokud se týká jen určitého aspektu, například obalu.

**Doporučení:** Upřesněte, které části se výhoda týká, například jen obalu.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 4b, ve znění směrnice (EU) 2024/825 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 7, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Ekologický produkt – obal je z recyklovaného papiera.“ – skóre 0,87, vysoká jistota
   - Kontext: …Tuhý šampón s pŕhľavou Vegan **[věta]** Šampón vystačí zhruba na 60 umytí.…
   - Pravděpodobnosti: eco_claim=0.869; eco_whole_claim=0.891; eco_part_benefit=0.898; eco_explicit_term=0.892; eco_organic_food=0.032; eco_other_subject=0.055; eco_diy=0.070
   - Stránky (1): http://fixture.test/produkt-3.html

### Odkaz na zrušenú platformu ODR

Pravidlo `legal_odr_link_outdated`, závažnost nízká, porušení podle textu zákona.

Platforma EÚ na riešenie sporov online (ODR) bola 20. 7. 2025 zrušená. Odkaz na ňu už spotrebiteľa k informáciám o subjekte alternatívneho riešenia sporov nedovedie; ak je jediným odkazom, povinnosť podľa § 5 ods. 1 písm. q) nemusí byť splnená.

**Doporučení:** Odkaz na platformu ODR odstráňte a nahraďte ho odkazom na informácie o príslušnom subjekte alternatívneho riešenia sporov (pri bežnom tovare SOI).

**Předpisy:**

- EU: Nařízení (EU) 2024/3228, kterým se zrušuje nařízení (EU) č. 524/2013 s účinkem od 20. 7. 2025 (status: ověřit)
- SK: Zákon č. 310/2025 Z. z., čl. I body 1 a 46 (vypustenie odkazov na nariadenie (EÚ) č. 524/2013); § 5 ods. 1 písm. q) zákona č. 108/2024 Z. z. (status: ověřit)

1. **Informace na webu nenalezena** – skóre 1,00, nižší jistota
   - Stránky (15): http://fixture.test/, http://fixture.test/o-nas.html, http://fixture.test/obchodne-podmienky.html, http://fixture.test/produkt-1.html, http://fixture.test/produkt-10.html a dalších 10
   - Poznámka: Nalezeno na 15 z 15 stažených stránek.

## K posouzení (2)

Zda jde o zakázanou praktiku, záleží na tom, jak text chápe průměrný spotřebitel; zákon tyto výrazy nejmenuje a Komise je posuzuje případ od případu. Rozhodne člověk.

### Odznak nebo symbol, který může působit jako značka udržitelnosti

Pravidlo `eco_label_open`, závažnost střední, k posouzení, záleží na tom, jak text chápe průměrný spotřebitel.

Odznak bez vlastního názvu certifikačního systému a bez environmentálního výrazu (například „Vegan“, „Vegetarian“, „GMO free“, „BIO“ u výrobku, který není potravinou, nebo zelený lístek) je značkou udržitelnosti, jen pokud jím obchodník naznačuje přínos pro životní prostředí nebo sociální přínos, například pro dobré životní podmínky zvířat. Komise to posuzuje případ od případu podle kontextu a vnímání průměrného spotřebitele (otázky a odpovědi č. 5, 14 a 15). Pokud je značkou udržitelnosti, musí vycházet z certifikačního systému.

**Doporučení:** Posuďte, zda odznak v kontextu obchodu působí jako slib přínosu pro přírodu nebo zvířata. Pokud ano, používejte ho jen s certifikací; jde-li jen o údaj o složení nebo o vhodnosti pro určitou stravu, přínos nenaznačujte.

**Předpisy:**

- EU: Směrnice 2005/29/ES, čl. 2 písm. r) a příloha I bod 2a, ve znění směrnice (EU) 2024/825; otázky a odpovědi Komise (září 2026), otázky 5, 14 a 15 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 3, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Vegan“ – skóre 0,89, vysoká jistota
   - Kontext: …Tuhý šampón s pŕhľavou **[věta]** Ekologický produkt – obal je z recyklovaného papiera. Šampón vystačí zhruba na 60 umytí.…
   - Pravděpodobnosti: eco_label=0.049; eco_social_label=0.889; eco_named_label=0.085; eco_explicit_term=0.088; eco_organic_food=0.036; eco_generic=0.028; eco_other_subject=0.078; eco_diy=0.049
   - Stránky (2): http://fixture.test/produkt-3.html, http://fixture.test/produkt-9.html

### Možné obecné environmentální tvrzení (posuzuje se případ od případu)

Pravidlo `eco_generic_claim_open`, závažnost střední, k posouzení, záleží na tom, jak text chápe průměrný spotřebitel.

Věta používá slovo, které zákon ani odůvodnění 9 směrnice (EU) 2024/825 mezi příklady obecných environmentálních tvrzení neuvádí, například „prírodný“, „bio“ u výrobku, který není potravinou, nebo „šetrný“ bez zmínky o přírodě. Environmentálním tvrzením je, jen pokud u průměrného spotřebitele vyvolá dojem přínosu pro životní prostředí; Komise to posuzuje případ od případu (otázky a odpovědi č. 3 u názvů výrobků a značek, č. 14 u „bio“ a „eco“). Pokud takový dojem vyvolá a přímo u něj není upřesnění, jde o zakázané obecné tvrzení.

**Doporučení:** Posuďte, zda výraz v kontextu stránky působí jako slib přínosu pro přírodu. Pokud ano, doplňte přímo k němu konkrétní doložitelný údaj, nebo ho odstraňte.

**Předpisy:**

- EU: Směrnice 2005/29/ES, čl. 2 písm. o) a p) a příloha I bod 4a, ve znění směrnice (EU) 2024/825; otázky a odpovědi Komise (září 2026), otázky 3 a 14 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 6, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Ocenené certifikátom GreenStar Planet.“ – skóre 0,86, vysoká jistota
   - Kontext: …Pleťový krém s rakytníkom **[věta]** Krém je vhodný pre suchú pleť, obsah balenia je 50 ml. ✓ Netestované na zvieratách.…
   - Pravděpodobnosti: eco_claim=0.914; eco_generic=0.862; eco_organic_food=0.061; eco_sustainable_term=0.028; eco_neutral=0.051; eco_explicit_term=0.064; eco_other_subject=0.076; eco_diy=0.073
   - Stránky (1): http://fixture.test/produkt-4.html

## K ověření (10)

Tvrzení nebo chybějící informace je na webu vidět, ale zda jde o porušení, záleží na faktech mimo web (certifikace značky, pravdivost údaje, košík a pokladna, které nástroj nestahuje).

### Chýba harmonizované oznámenie o zákonnej zodpovednosti za vady (nové od 27. 9. 2026)

Pravidlo `legal_harmonized_notice_missing`, závažnost vysoká, k ověření, záleží na faktech mimo web.

Od 27. 9. 2026 musí e-shop pred objednávkou zreteľne informovať o zákonnej zodpovednosti za vady vrátane jej dĺžky, a to aspoň v podobe a rozsahu harmonizovaného oznámenia EÚ: farebného obrázka s QR kódom na portál Vaša Európa, ktorý sa nesmie upravovať. Text v obchodných podmienkach (napríklad „Zákonná záruka je 24 mesiacov“) túto podobu nenahrádza.

**Doporučení:** Zobrazte farebné harmonizované oznámenie podľa prílohy I nariadenia (EÚ) 2025/1960 bez úprav na stránke produktu alebo v pokladni pred odoslaním objednávky; ak ho už máte, doplňte k obrázku popis (alt), aby bolo dohľadateľné.

**Předpisy:**

- EU: Směrnice 2011/83/EU, čl. 5 odst. 1 písm. e), čl. 6 odst. 1 písm. l) a čl. 22a odst. 1 a 3, ve znění směrnice (EU) 2024/825; prováděcí nařízení Komise (EU) 2025/1960, čl. 1 a příloha I (status: ověřit)
- SK: § 5 ods. 1 písm. f) zákona č. 108/2024 Z. z. v znení zákona č. 310/2025 Z. z. (účinnosť od 27. 9. 2026) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 1,00, nižší jistota
   - Poznámka: Na žádné z 15 stažených stránek se nenašel obrázek, odkaz ani text, který by to ukazoval.
   - Poznámka: Košík, pokladnu a zákaznický účet nástroj nestahuje; tam to ověřte ručně.

### Chýba funkcia „odstúpiť od zmluvy tu“ (povinná od 19. 6. 2026)

Pravidlo `legal_withdrawal_function_missing`, závažnost vysoká, k ověření, záleží na faktech mimo web.

E-shop musí spotrebiteľovi umožniť odstúpiť od zmluvy aj funkciou na odstúpenie, označenou „odstúpiť od zmluvy tu“ alebo obdobnou jednoznačnou formuláciou, zreteľne zobrazenou a nepretržite dostupnou počas celej lehoty na odstúpenie. Na prehľadaných stránkach sa nenašiel odkaz ani tlačidlo s takýmto označením.

**Doporučení:** Doplňte zreteľne viditeľný odkaz alebo tlačidlo „odstúpiť od zmluvy tu“ (napríklad v pätičke a v zákazníckom účte) s online formulárom podľa § 20a ods. 3.

**Předpisy:**

- EU: Směrnice 2011/83/EU, čl. 11a, vložený směrnicí (EU) 2023/2673 (použije se od 19. 6. 2026) (status: ověřit)
- SK: § 20a zákona č. 108/2024 Z. z. v znení zákona č. 311/2025 Z. z. (účinnosť od 19. 6. 2026) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 1,00, nižší jistota
   - Poznámka: Na žádné z 15 stažených stránek se nenašel obrázek, odkaz ani text, který by to ukazoval.
   - Poznámka: Košík, pokladnu a zákaznický účet nástroj nestahuje; tam to ověřte ručně.

### Budoucí environmentální závazek k ověření

Pravidlo `eco_future_claim`, závažnost střední, k ověření, záleží na faktech mimo web.

Tvrzení o budoucím environmentálním výkonu (například „klimaticky neutrální do roku 2030“) je klamavé, pokud za ním nestojí jasné, veřejně dostupné a ověřitelné závazky v podrobném a realistickém prováděcím plánu s měřitelnými cíli, který pravidelně ověřuje nezávislý odborník. Posuzuje se případ od případu.

**Doporučení:** Ověřte, že k závazku existuje veřejný prováděcí plán s měřitelnými cíli a nezávislým ověřováním, a odkažte na něj přímo u tvrzení; jinak tvrzení odstraňte.

**Předpisy:**

- EU: Směrnice 2005/29/ES, čl. 6 odst. 2 písm. d), ve znění směrnice (EU) 2024/825; otázky a odpovědi Komise (září 2026), otázky 6 a 12 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., § 10 ods. 2 písm. d), ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Do roku 2030 budeme vyrábať úplne bez emisií.“ – skóre 0,91, vysoká jistota
   - Kontext: …Sme zodpovedná a udržateľná firma. Ako firma sme klimaticky neutrálni vďaka výsadbe stromov. **[věta]** Naše mydlá nesú pečať Fair Soap Alliance za férové pracovné podmienky. Mydlá varíme ručne od roku 2010.…
   - Pravděpodobnosti: eco_future_claim=0.907
   - Stránky (1): http://fixture.test/o-nas.html

### Tvrzení o klimatické neutralitě firmy založené na kompenzacích

Pravidlo `eco_company_climate_claim`, závažnost střední, k ověření, záleží na faktech mimo web.

Zákaz tvrzení založených na kompenzacích (bod 4c) se týká produktů. Tvrzení, že je klimaticky neutrální celá firma, je ale obecné environmentální tvrzení a bez uznaného vynikajícího environmentálního profilu je zakázané. Investice do klimatických projektů smí firma propagovat, pokud to nepůsobí klamavě.

**Doporučení:** Místo neutrality uveďte, co firma skutečně dělá (například kolik emisí snížila a jak), a podporu projektů popište odděleně od vlastností produktů.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 4a, ve znění směrnice (EU) 2024/825; otázky a odpovědi Komise (září 2026), otázky 6 a 10 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 6, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Ako firma sme klimaticky neutrálni vďaka výsadbe stromov.“ – skóre 0,90, vysoká jistota
   - Kontext: …O nás Sme zodpovedná a udržateľná firma. **[věta]** Do roku 2030 budeme vyrábať úplne bez emisií. Naše mydlá nesú pečať Fair Soap Alliance za férové pracovné podmienky.…
   - Pravděpodobnosti: eco_neutral=0.938; eco_offset_basis=0.918; eco_company_level=0.903
   - Stránky (1): http://fixture.test/o-nas.html

### Navádzanie na skoršiu výmenu spotrebného materiálu k ověření (nové od 27. 9. 2026)

Pravidlo `dur_consumable_early`, závažnost střední, k ověření, záleží na faktech mimo web.

Navádět spotřebitele, aby spotřební materiál vyměnil nebo doplnil dřív, než je z technických důvodů nutné, je na Slovensku od 27. 9. 2026 zakázané za všech okolností. Zda je interval technicky odůvodněný, z textu poznat nelze.

**Doporučení:** Ověřte, že doporučený interval výměny vychází z technických důvodů (údaje výrobce); pokud ne, doporučte výměnu po spotřebování nebo opotřebení.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23i, ve znění směrnice (EU) 2024/825 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 38, ve znění zákona č. 310/2025 Z. z. (účinnosť od 27. 9. 2026) (status: ověřit)

1. „Filter vymieňajte každý mesiac, aj keď ešte funguje.“ – skóre 0,90, vysoká jistota
   - Kontext: …Vysávač má výkon 700 W a nádobu na 1,5 l prachu. Motor vydrží 10 rokov každodenného používania. **[věta]** Neoriginálne vrecká poškodia motor vysávača.…
   - Pravděpodobnosti: dur_consumable_early=0.903
   - Stránky (1): http://fixture.test/produkt-7.html

### Tvrzení, že neoriginální díly poškodí výrobek, k ověření (nové od 27. 9. 2026)

Pravidlo `dur_non_original_damage`, závažnost střední, k ověření, záleží na faktech mimo web.

Nepravdivé tvrzení, že neoriginální spotřební materiál, náhradní díly nebo příslušenství poškodí funkčnost zboží, je na Slovensku od 27. 9. 2026 zakázané za všech okolností. Pravdivost z textu poznat nelze.

**Doporučení:** Ověřte, že tvrzení o poškození má oporu v dokladech výrobce; pokud ne, tvrzení odstraňte.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23j, ve znění směrnice (EU) 2024/825 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 39, ve znění zákona č. 310/2025 Z. z. (účinnosť od 27. 9. 2026) (status: ověřit)

1. „Neoriginálne vrecká poškodia motor vysávača.“ – skóre 0,90, vysoká jistota
   - Kontext: …Motor vydrží 10 rokov každodenného používania. Filter vymieňajte každý mesiac, aj keď ešte funguje. **[věta]** …
   - Pravděpodobnosti: dur_non_original_damage=0.903
   - Stránky (1): http://fixture.test/produkt-7.html

### Jmenovaná značka udržitelnosti mimo seznam ověřených

Pravidlo `eco_label_unrecognized`, závažnost střední, k ověření, záleží na faktech mimo web.

Značka udržitelnosti (environmentální i sociální, například férový obchod) je zakázaná, pokud nevychází z certifikačního systému s nezávislým ověřováním nebo ji nezavedl orgán veřejné moci. Věta jmenuje značku, která není na seznamu ověřených; zda podmínky splňuje, se z webu zjistit nedá.

**Doporučení:** Ověřte, zda značka vychází z certifikačního systému s nezávislým ověřováním nebo ji zavedl úřad. Pokud ne, odstraňte ji.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 2a, ve znění směrnice (EU) 2024/825 (označení udržitelnosti zahrnuje environmentální i sociální znaky) (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 3, ve znění zákona č. 310/2025 Z. z. (účinnost od 27. 9. 2026) (status: ověřit)

1. „Naše mydlá nesú pečať Fair Soap Alliance za férové pracovné podmienky.“ – skóre 0,87, vysoká jistota
   - Kontext: …Ako firma sme klimaticky neutrálni vďaka výsadbe stromov. Do roku 2030 budeme vyrábať úplne bez emisií. **[věta]** Mydlá varíme ručne od roku 2010.…
   - Pravděpodobnosti: eco_named_label=0.871; eco_label=0.938; eco_social_label=0.895; eco_organic_food=0.081
   - Stránky (1): http://fixture.test/o-nas.html

2. „Ocenené certifikátom GreenStar Planet.“ – skóre 0,86, vysoká jistota
   - Kontext: …Pleťový krém s rakytníkom **[věta]** Krém je vhodný pre suchú pleť, obsah balenia je 50 ml. ✓ Netestované na zvieratách.…
   - Pravděpodobnosti: eco_named_label=0.864; eco_label=0.919; eco_social_label=0.075; eco_organic_food=0.061
   - Stránky (1): http://fixture.test/produkt-4.html

### Tvrzení o ověřených recenzích k ověření

Pravidlo `ucp_reviews_verified_claim`, závažnost střední, k ověření, záleží na faktech mimo web.

Tvrdit, že recenze píší zákazníci, kteří produkt koupili nebo použili, je zakázané, pokud obchod nepřijal přiměřená opatření k ověření (například hodnotit smí jen zákazník s dokončenou objednávkou). Opatření na webu vidět nejsou, proto jde o nález k ověření.

**Doporučení:** Ověřte, že obchod skutečně kontroluje původ recenzí (například podle čísla objednávky), a popište to přímo u recenzí.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23b; Pokyny Komise 2021/C 526/01, oddíl 4.2.4 (opodstatněné a přiměřené kroky k ověření) (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 31 (do 26. 9. 2026 bod 26) (status: ověřit)

1. „Všetky recenzie sú od overených zákazníkov.“ – skóre 0,87, vysoká jistota
   - Kontext: …✓ Netestované na zvieratách. Recenzie **[věta]** …
   - Pravděpodobnosti: ucp_reviews_verified_claim=0.869
   - Stránky (1): http://fixture.test/produkt-4.html

### Tvrzení o životnosti výrobku k ověření (nové od 27. 9. 2026)

Pravidlo `dur_lifetime_claim`, závažnost střední, k ověření, záleží na faktech mimo web.

Nepravdivé tvrzení, že zboží má za běžných podmínek používání určitou životnost (dobu nebo intenzitu používání), je na Slovensku od 27. 9. 2026 zakázané za všech okolností. Z textu nelze poznat, zda je tvrzení pravdivé, proto jde o nález k ověření.

**Doporučení:** Ověřte, že údaj o životnosti má oporu v dokladech výrobce (zkoušky, norma, podmínky používání); pokud ne, tvrzení odstraňte.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23g, ve znění směrnice (EU) 2024/825 (status: ověřit)
- SK: Zákon č. 108/2024 Z. z., príloha č. 1 bod 36, ve znění zákona č. 310/2025 Z. z. (účinnosť od 27. 9. 2026) (status: ověřit)

1. „Motor vydrží 10 rokov každodenného používania.“ – skóre 0,87, vysoká jistota
   - Kontext: …Tyčový vysávač Tornádo Vysávač má výkon 700 W a nádobu na 1,5 l prachu. **[věta]** Filter vymieňajte každý mesiac, aj keď ešte funguje. Neoriginálne vrecká poškodia motor vysávača.…
   - Pravděpodobnosti: dur_lifetime_claim=0.866
   - Stránky (1): http://fixture.test/produkt-7.html

## Obrázky k ruční kontrole

Jev obrázky nevidí. Tyto obrázky mají v alt textu nebo názvu souboru environmentální slovo, zkontrolujte je ručně.

| Stránka | Soubor | Alt text | Slovo |
| --- | --- | --- | --- |
| http://fixture.test/produkt-5.html | eu-ecolabel.png | EU Ecolabel | eco |

## Co nebylo zkontrolováno

- Text v obrázcích (Jev obrázky nevidí; podezřelé obrázky jsou vypsané výše).
- Části stránek, které web dotahuje až JavaScriptem (widgety recenzí, odpočty, záložky načítané po kliknutí). Stránky, které by byly bez JavaScriptu celé prázdné, nástroj hlásí zvlášť; v tomto běhu žádné nebyly.
- Navigace a filtry (menu, seznamy kategorií, drobečková navigace, volby filtrů): jde o odkazy a volby, ne o tvrzení.
- Výpisy jiných produktů na stránce (podobné produkty, dlaždice s cenou a odkazem na jiný produkt): jejich texty se kontrolují na stránce toho produktu; při kontrole jen vzorku stránek se produkty mimo vzorek nekontrolují.
- Procesní povinnosti, které z textu webu nevyplývají (např. zda se reklamace skutečně vyřizují včas).
- Stránky nad limit: 0 nestažených, 0 produktových nad limit vzorku; 1 zakázaných v robots.txt.
- Věty v úsecích, kde síto nenašlo téma modulu: 124 dvojic věta × modul bez podrobných otázek; úseky a pravděpodobnosti síta jsou v sieve.csv, bez síta běží sken s volbou --no-sieve.
- Právní dokumenty v PDF: žádné odkazy nenalezeny.

---

Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.
