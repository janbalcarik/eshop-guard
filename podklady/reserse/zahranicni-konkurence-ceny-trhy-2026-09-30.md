# Zahraniční konkurence, měsíční ceníky a pořadí trhů (stav 30. 9. 2026)

Doplňuje `trh-konkurence-ceny-2026-09-30.md`. Podklad připravily tři webové rešerše 30. 9. 2026. Čerpaly jen z veřejných stránek, nic neregistrovaly a nespouštěly cizí skeny. Stránky četly přes WebFetch, který obsah shrnuje, takže znění může být místy parafrázované. Rešerším došel limit vyhledávání (200/200): Shopware Store a severské trhy nejsou prohledané do hloubky.

## 1. Skenery ekologických tvrzení (EmpCo): ceníky

GreenClaims Scanner a Greenwashing Checker jsou jeden produkt (METIS SASU, Marseille). EmpCo Scanner (empco-scanner.de) a EmpCo Scan (empco-scan.com) jsou dva různé nástroje.

| Nástroj | Země | Ceny (bez DPH, není-li uvedeno) | Limity | ~100 str./měs. | ~1 000 str./měs. | Jazyky | Metoda | Přepis | Zápis do e-shopu | Zdroj |
|---|---|---|---|---|---|---|---|---|---|---|
| Appsentials EU Green Claims (Shopify) | DE, od 20. 7. 2026 | Free; $19.99; $49.99/měs. ($199/$499 ročně) | 25 / 500 / neomezeně produktů; auto-rescan „coming soon“ | $19.99 | $49.99 | DE EN FR ES IT NL SV DA PL | pravidla + volitelně AI | ano (ke zkopírování) | ne („read-only“) | https://apps.shopify.com/eu-green-claims-greenwashing |
| EcoClaim | „EU“, subjekt neuveden | jednorázově 39,99 / 89,99 / 129,99 €; Site Sweep od 149,99 € | sken 5–20 str.; Sweep do 1 000 str. | ~0,47–1,60 €/str. | Sweep od 149,99 € | tvrdí i CS, PL | AI + 82 výrazů | ano | ne | https://www.ecoclaim.eu/pricing |
| Greenwashing Checker / GreenClaims Scanner | FR | Free; Pro 29 €; Business 79 €/měs.; roční −20 % | Pro 100 str. týdně; Business 500 str. denně, 5 webů, API | 29 € | Enterprise (2×500 = 79 €) | EN FR DE ES IT NL PL PT SV | 234 deterministických pravidel, „bez AI“ | jen doporučení | ne | https://greenwashing-checker.com/en/pricing/ |
| ContentBeak | DK | Free 50 URL; od 5 / 40 / 150 €/měs. (0,03–0,05 €/URL) | do 50 000 URL/sken | 5 € za průchod | 40 € za průchod | původní jazyk, vysvětlení EN | AI | ne | ne | https://www.contentbeak.com/ |
| VERDAI | DE | 190 €/rok (≈19 €/měs.), 490 €/rok (≈49 €) | 50 / 150 kreditů/měs. (definice kreditu NEOVĚŘENO) | ~49 € | Enterprise | DE EN ES FR | AI, skóre 0–100 | ano | ne | https://www.verdai-claims.com/produkt/preise |
| GreenClaimChecker | ES firma, trh DE | Free 5; 49 / 99 / 149 €/měs. | 100 / 500 / neomezeně kontrol; celý web neprochází | 49 € | 149 € | jen DE | AI + pravidla, OCR | ano | ne (výslovně) | https://greenclaimchecker.eu/ |
| EmpCo Scanner | DE | jednorázově 149 € (50 str.), 399 € (200 str.) + DPH | 1 doména; monitoring jen Enterprise | 399 € jednoráz. (2 €/str.) | Enterprise | DE | AI | doporučení | ne | https://empco-scanner.de/ |
| EmpCo Scan | DE | jednorázově 139 € (50 str.), 359 € (200 str.) + DPH | monitoring/API jen Enterprise | 359 € jednoráz. (1,80 €/str.) | Enterprise | DE EN | pravidla + AI | ano | ne | https://www.empco-scan.com/en/pricing |
| EmpCo-Test | DE | audit 29 € (≤150 str.), monitoring 9 €/měs. (vč. DPH) | 150 str./doména | 9 € | nelze | NEOVĚŘENO | NEOVĚŘENO | NEOVĚŘENO | ne | https://empco-test.eu/en/preise |
| Greenwashing Checkup | IT | 19 €/tvrzení; web 149 € (20 str.) | nad 20 str. konzultace | 7,45 €/str. | na nabídku | IT | LLM + právní engine | matice odstranit/přeformulovat/doložit | ne | https://greenwashingcheckup.com/ |
| rechtsklar24 RechtsRadar (širší právo) | DE | report 19 €; monitoring 49 €/12 měs.; oprava 300–1 500 € | ~10 str./shop | ≈4 €/měs. (jen ~10 str.) | – | DE | pravidla, 36 povinností vč. EmpCo | seznam úkolů | ne | https://rechtsklar24.de/ |
| Jawnie (WooCommerce), Lodestone (WooCommerce) | PL / ? | zdarma (Lodestone Pro NEOVĚŘENO) | – | 0 | 0 | EN PL DE / 7 jazyků | klíčová slova / pravidla | ne | ne | wordpress.org |

**Cenová hladina EmpCo skenerů:**
- **Předplatné za 100 str. měsíčně:** medián ≈ 29 €, rozptyl 5–49 €.
- **Za 1 000 str. měsíčně:** ≈ 50–150 €, jinak Enterprise.
- **Typické tarify:** Free (1–25 str.) → 19–29 € → 49–79 € → 99–150 € → Enterprise. Roční sleva 17–20 %.
- **Jednorázové audity v DE:** 139–149 € za 50 str., 359–399 € za 200 str.

**Doložené slabiny:**
- **Jazyky:** slovenštinu ani maďarštinu nemá žádný předplatný skener. Češtinu tvrdí jen EcoClaim, a to bez českého práva.
- **Právo:** SK zákon 108/2024 necituje nikdo. Většina cituje jen směrnici.
- **Zápis a oprava:** zápis zpět do e-shopu nemá nikdo. Opakovanou kontrolu navrženého textu nepopisuje nikdo.
- **Rozsah:** tvrzení o životnosti, falešnou naléhavost ani recenze nemá nikdo.
- **Falešné poplachy:** skenery s klíčovými slovy hlásí i „natural“ a „biodegradable“ jako „banned terms“, přitom v příloze I zakázané per se nejsou (úsudek rešerše). Monitoring má doložený jen Greenwashing Checker a ContentBeak. Trh je starý týdny: Appsentials má 1 recenzi, pluginy méně než 10 instalací.

## 2. Měsíční checkery z příbuzných oblastí (kotvy)

| Kategorie | Příklady | Cena |
|---|---|---|
| Právní ochrana e-shopu DE (paušál za shop) | IT-Recht Kanzlei 9,90 / 24,90 / 54,90 €; Händlerbund 64,90 / 149,90 €; eRecht24 30–180 €; Protected Shops 9,90–48,90 €; Trusted Shops od 119 € | měsíčně, netto |
| Skener spotřebitelského práva | ComplianceGuard 79 / 149 / 399 € za doménu; dsgvo.pro 9–89 € | měsíčně |
| Přístupnost (EAA) | Pope Tech 30 $ (50 str.) až 270 $ (500+); UserWay 990 $/rok za 100 str.; AccessibilityChecker 149 $ / 100 URL | 0,45–1,50 $/str./měs. |
| Hlídání změn stránek | Little Warden 34,99 £ / 100 URL; Visualping 140 $ / 200 str.; Distill 35 $ / 150 | 0,35–1,40 $/str. |
| Cookie skenery | Cookiebot 15 € (350 str.), 30 € (3 500); CookieYes 23 € (4 000) | 0,02–0,15 €/str. |

**Pravidla, která z kotev plynou:**
- **Sleva podle objemu:** 10× víc stránek stojí 2–4× víc.
- **Hranice pásem:** 100 / 250 / 500 / 2 000 / 5 000 / 10 000.
- **Free tier** je standard.
- **Roční platba:** obvykle „2 měsíce zdarma“.
- **Odlišení tarifů:** vyšší tarify mají častější sken a API.
- **Model „stránky v celém účtu, webů neomezeně“** má Pope Tech, stejně jako náš návrh.

Zdroje: complianceguardhq.com/pricing, it-recht-kanzlei.de/schutzpakete.html, haendlerbund.de, e-recht24.de/premium, pope.tech/pricing, userway.org/pricing, accessibilitychecker.org/pricing, littlewarden.com/pricing, visualping.io/pricing, distill.io/pricing, cookiebot.com/en/pricing, cookieyes.com/pricing.

## 3. Trhy (výběr)

| Země | E-shopy (proxy) | EmpCo v národním právu | Vymáhání | Konkurence v jazyce | Klíčové platformy |
|---|---|---|---|---|---|
| DE | Shopify + Woo ~168 tis.; Shopware 19 tis. | 3. UWGÄndG, BGBl. 2026 I Nr. 43, od 27. 9. 2026, bez doprodeje zásob | vysoké, soukromé výzvy konkurentů (Abmahnungen): 9 % obchodníků dostalo výzvu, 41 % zaplatilo 1 001–2 000 € (Händlerbund 2026) | vysoká | Woo, Shopify, Shopware, JTL |
| AT | ~24 tis. | UWG-Novelle BGBl. I 58/2026; přechod § 44 odst. 16 | NEOVĚŘENO | vysoká (DE nástroje) | Woo, Shopify, Shopware |
| HU | ~24 tis. (Shopify + Woo) | 2025. évi XCIV. (částečně), od 27. 9. 2026, bez přechodu | střední (GVH) | žádný skener nenalezen | Woo, UNAS (~10 tis.), Shoprenter (>6 tis.) |
| PL | ~75 tis. | Sejm 4. 9. 2026, v Senátu, použití až 27. 9. 2027 | vysoké (UOKiK 7 řízení, až 10 % obratu) | nízká–střední | Woo, Shoper, PrestaShop, IdoSell |
| RO | 25–40 tis. | OUG 18/2026, od 27. 9. 2026 | NEOVĚŘENO | skoro žádná | Woo, Gomag, MerchantPro |
| IT | ~131 tis. | D.Lgs. 30/2026, od 27. 9. 2026 | vysoké (AGCM) | střední | Woo, Shopify, PrestaShop |
| FR / ES | 192 / 153 tis. | nepřijato | FR nejvyšší (DGCCRF) | FR nejvyšší | Woo, Shopify, PrestaShop |
| CZ | Shoptet ~25 tis. | nepřijato (tisk 53), EK zahájila řízení 28. 5. 2026 | – | – | Shoptet |

**Doporučené pořadí podle rešerše:** DE + AT (jeden jazyk), HU, PL (rok na přípravu), RO, volitelně IT. FR a ES odložit (bez zákona), NL a BE jsou nasycené.

**Rizika:**
- **Citace práva:** ve státech bez transpozice (CZ, PL do 2027, FR, ES, SI) nelze černou listinu prezentovat jako platný zákon.
- **Rozdíly mezi zeměmi:** pravidla je nutné vést po zemích s okny platnosti.
- **Právní služba v DE:** automatické přepisy mohou narazit na zákon o právních službách (RDG). Smartlaw BGH 2021 NEOVĚŘENO, nutná právní analýza.
- **Lokální platformy:** API Shoper, IdoSell, UNAS, Shoprenter, Gomag, JTL a Lightspeed NEOVĚŘENO.

Hlavní zdroje:
- https://cms.law/en/int/expert-guides/cms-implementation-tracker-for-the-empco-directive
- https://bootleads.com/stores/countries/
- https://storeleads.app/reports/
- https://www.buzer.de/gesetz/17410/index.htm
- https://www.e-commerce-magazin.de/recht-abmahnung-als-teures-risiko-diese-fehler-kosten-bares-geld-a-77461e5c346822d574d969615effefe9/
- https://ts.at/wissen/wettbewerbsrecht/uwg-novelle-empco-beschlossen/
- https://njt.jog.gov.hu/jogszabaly/2025-94-00-00.0
- https://www.cfconsulting.pl/blog/dyrektywa-empco-greenwashing
- https://legislatie.just.ro/Public/DetaliiDocument/308474
- https://www.altalex.com/documents/2026/03/24/vigore-d-lgs-30-2026-nuove-regole-greenwashing-durabilita-pratiche-commerciali-scorrette

### Doplněk: pozdější zpráva o transpozici (30. 9. 2026)

- **PL:**
  - Senát přijal zákon 24. 9. 2026 bez pozměňovacích návrhů a týž den ho předal prezidentovi. Podpis a vyhlášení v Dz.U. jsou NEOVĚŘENÉ.
  - Čl. 3: účinnost od 27. 9. 2027, přechodné ustanovení není.
  - Zdroje: https://orka.sejm.gov.pl/proc10.nsf/ustawy/2799_u.htm ; https://www.wnp.pl/rynki/senat-przyjal-bez-poprawek-nowelizacje-ustawy-o-zakazie-tzw-greenwashingu,1102320.html
- **AT:**
  - Spotřebitelská část je ve VerbRÄG 2026, BGBl. I Nr. 59/2026.
  - Tlačítko odstoupení (§ 13a FAGG) platí až od 1. 10. 2026 pro smlouvy uzavřené po 30. 9. 2026.
  - Zdroje: https://www.ris.bka.gv.at/Dokumente/BgblAuth/BGBLA_2026_I_58/BGBLA_2026_I_58.pdf ; https://ris.bka.gv.at/eli/bgbl/I/2026/59/20260728
- **DE:** tlačítko je v § 356a BGB od 19. 6. 2026 (BGBl. 2026 I Nr. 28). Informace o trvanlivosti podle EGBGB platí od 27. 9. 2026.
- **IT:**
  - D.Lgs. 30/2026 je v platnosti od 24. 3. 2026 a použije se od 27. 9. 2026.
  - Tlačítko upravuje čl. 54-bis Codice del consumo (D.Lgs. 209/2025) od 19. 6. 2026.
- **HU:** spotřebitelskou část doplňuje nařízení 116/2026 (VII. 30.) Korm. rendelet od 27. 9. 2026.
- **NL:** Stb. 2026, 152 je v platnosti od 16. 7. 2026 (Stb. 2026, 204). Výslovné datum použití 27. 9. 2026 v textu nalezeno nebylo.
- **BE:** zákon z 22. 7. 2026. Šestiměsíční přechod pro zboží uvedené na trh před 27. 9. 2026 zmiňuje jen blog, NEOVĚŘENO.
- **Přechod pro staré zásoby** je ověřený jen v AT (§ 44 odst. 16 UWG: 3 roky pro civilní nároky u zboží uvedeného na trh do 27. 9. 2026).

### Doplněk: velikost trhů (pozdější zpráva, 30. 9. 2026)

**Počty e-shopů ze Store Leads** (25. 9. 2026, https://storeleads.app/reports) počítají každou doménu s košíkem. Oproti národním údajům vycházejí 2–3× výš: FR 309 855 proti 158 000 u FEVAD, NL 259 085 proti 99 795 u CBS. Platformy Upgates a plentymarkets tam nejsou, v CZ/SK nejspíš spadají pod „Custom“.

**Národní údaje:**
- **HU:** 38 000 webshopů (PwC Digitális Kereskedelmi Körkép 2025). Číslo je jen z výtahu vyhledávače, stránka vrací 403. Obrat 2 092 mld HUF za 2025 včetně nákupů v zahraničí ([hvg.hu](https://hvg.hu/kkv/20260611_pwc-e-kereskedelmi-korkep-2025-2000-milliard-forint)).
- **PL:** ~75 000 e-shopů (Dun & Bradstreet, 1/2026), obrat 47,4 mld € za 2025 (European E-commerce Report 2026 přes news.ro).
- **RO:** 12,9 mld € za 2025 (EEcR 2026). Počet e-shopů 25–40 tis. je jen odhad agentury.
- **FR:** 196,4 mld € za 2025 (FEVAD). **NL:** 99 795 webwinkels (CBS, Q3 2026). **DE:** 92,3 mld € za 2025 (HDE Online-Monitor 2026).

**Lokální platformy vázané na jednu zemi** (Store Leads):

| Země | Platforma a podíl jejích obchodů v zemi | Počet obchodů v zemi |
|---|---|---|
| PL | Shoper 95 % | 14 665 |
| HU | UNAS 96 % | 8 443 |
| HU | ShopRenter 94 % | 5 028 |
| RO | Gomag ~100 % | 8 125 |
| RO | MerchantPro 84 % | – |
| DE | Shopware 63 % | – |
| DE | JTL 72 % | – |
| CZ / SK | Shoptet 77 % / 17 % | – |

Konektor na lokální platformu tedy otevírá právě jednu zemi.

### Doplněk: vymáhání 2024–2026 (pozdější zpráva, 30. 9. 2026)

- **DE: oprava.**
  - Přechodné ustanovení **§ 15b UWG** existuje. Týká se jen zboží uvedeného na trh před 27. 9. 2026. Weby a katalogy musí vyhovovat hned (https://www.wettbewerbszentrale.de/uwg-erhaelt-begrenzte-empco-uebergangsregel/). Dřívější údaj „bez doprodeje zásob“ tím upřesňujeme.
  - Konkurent si může vymáhat náklady výzvy u klamavých tvrzení. Výluka v § 13 odst. 4 UWG se týká jen informačních a označovacích povinností online (https://www.gesetze-im-internet.de/uwg_2004/__13.html).
  - Příklady částek podle Händlerbundu:
    - Wettbewerbszentrale účtuje ~350–375 €.
    - Výzva za nedoložené „nachhaltig“ 7/2025 stála 1 377,29 € (https://www.haendlerbund.de/de/news/aktuelles/abmahnungen-juli-2025).
  - Abmahnstudie 2025: výzvu dostalo 18 % obchodníků v roce 2024, v roce 2023 to bylo 12 %.
  - DUH: 92 řízení (9/2024), z toho 48 závazků se smluvní pokutou.
  - Spotřebitel má nově nárok na náhradu škody podle § 9 odst. 2 UWG.
- **IT (AGCM):** GLS 8 mil. € (1/2025, „Climate Protect“); Shein 1 mil. € (8/2025).
- **FR (DGCCRF):**
  - Za 2023–2024: >3 000 provozoven, 430 příkazů, 70 řízení.
  - Shein 40 mil. € (7/2025).
- **PL (UOKiK):**
  - Obvinění: Allegro, DHL, DPD, InPost (7/2025); Bolt, Tchibo, Zara (1/2026).
  - Pravomocná pokuta za greenwashing zatím žádná.
  - Zdroj: https://uokik.gov.pl/en/greenwashing-the-president-of-uokik-raises-allegations-against-allegro-dhl-dpd-and-inpost
- **HU (GVH):**
  - Greenwashing zatím řeší závazky bez pokut (PET lahve, 8/2026).
  - V e‑commerce ukládá tvrdé pokuty: eMAG 235 mil. HUF (1/2026) a 225 mil. HUF (8/2026).
- **NL (ACM):** H&M a Decathlon 2022 skončily závazky a dary, bez pokuty. Soud Amsterdam 3/2024 vyhověl žalobě proti KLM.
- **RO: ROZPOR ZDROJŮ.**
  - Tato zpráva cituje CMS (17. 4. 2026): EmpCo nepřevzato.
  - Zpráva o transpozici uvádí OUG 18/2026 (MO 236 z 26. 3. 2026).
  - Nutno ověřit v primárním zdroji (legislatie.just.ro).
- **CPC (EU):** Zalando (2/2024) a aerolinky (11/2025) skončily závazky. Shein (5/2025) dostal konstatování porušení.

### Doplněk: transpozice z oficiálních sbírek (pozdější zpráva, 30. 9. 2026)

- **CZ – tlačítko odstoupení: zákon č. 159/2026 Sb.**
  - Ze dne 19. 8. 2026, vyhlášen 2. 9. 2026, **účinný od 1. 1. 2027**.
  - Nový § 1830a OZ zavádí tlačítko s textem „Odstoupit od smlouvy“. Vzorový formulář stanoví nařízení vlády 66/2026 Sb.
  - Zdroj: https://e-sbirka.gov.cz/sb/2026/159
  - **Text zatím není v `podklady/predpisy-cz`. Před vytvořením CZ pravidla ho stáhnout.**
- **CZ – EmpCo (tisk 53):** nepřijato. 3. čtení je možné od 5. 9. 2026, ale neproběhlo. Komisi zatím CZ oznámila jen starší předpisy (634/1992, 89/2012, 378/2015, 374/2022).
- **SK:** zákon 310/2025 Z. z. (21. 10. 2025, vyhlášen 19. 11. 2025), části k EmpCo platí od 27. 9. 2026. Přechod pro zboží na trhu nenalezen. Tlačítko odstoupení upravuje 311/2025 Z. z. od 19. 6. 2026 (§ 20a zákona 108/2024).
- **BE:**
  - Zákon z 22. 7. 2026 (Moniteur belge 4. 8. 2026, numac 2026005912), účinný od 27. 9. 2026.
  - Čl. 15 vylučuje jen použití čl. XV.2 §1 CDE (pravomoc inspektorů, výklad NEOVĚŘEN) pro zboží vyrobené nebo uvedené na trh před 27. 9. 2026, a to do 27. 3. 2027. Zákazy samy platí od 27. 9. 2026.
  - Zdroj: https://www.ejustice.just.fgov.be/eli/loi/2026/07/22/2026005912/moniteur
- **HR:**
  - NN 59/2026 (vyhlášen 9. 6. 2026), části k EmpCo platí od 27. 9. 2026, přechod pro zboží není.
  - Tlačítko: čl. 81.a od 19. 6. 2026 a Pravilnik NN 105/26.
  - Zdroj: https://narodne-novine.nn.hr/clanci/sluzbeni/2026_06_59_728.html
- **SI:** nepřijato, vládní návrh ZVPot-1A.
- **FR:** nepřijato. DDADUE leží v Národním shromáždění (n° 2518), EmpCo je v čl. 20.

Oznámené národní předpisy v EUR-Lex:
- https://eur-lex.europa.eu/legal-content/EN/NIM/?uri=CELEX:32024L0825
- https://eur-lex.europa.eu/legal-content/EN/NIM/?uri=CELEX:32023L2673

## 4. Naše náklady pro srovnání (změřeno v prototypu, jen SK)

- **Úvodní analýza:** kontrola ~0,4–0,9 centu za stránku, oprava ~1 cent za stránku s nálezem (~22 % stránek). Dohromady ~0,005–0,01 € za stránku.
- **Sledování:** kontrolují se jen změněné stránky. Při 10 % změn měsíčně stojí 1 000 sledovaných stránek ~1 USD. Nová verze pravidel stojí ~9 USD za 1 000 stránek jednorázově.
- **Kvalita Jevu v jiných jazycích než slovenštině NENÍ ZMĚŘENA.**
