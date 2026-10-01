# Informační povinnosti slovenského e-shopu vůči spotřebiteli (znění od 27. 9. 2026)

Rešerše pro EshopGuard (screening textů webu, ne právní posouzení). Stav zdrojů k 25. 9. 2026, posuzované znění účinné od 27. 9. 2026. Text je česky, citace předpisů slovensky a doslovně: sloučil jsem jen zalomení řádků do mezer, vynechávky značí „…“ a odkazy na poznámky pod čarou (např. „22a )“) nechávám tak, jak jsou ve zdrojovém textu. Vše je návrh ke kontrole právníkem (v YAML status „ověřit“).

Příloha č. 1 zákona č. 108/2024 Z. z. (zoznam obchodných praktík, ktoré sa vždy považujú za nekalé, odkaz v § 9 ods. 5) sem nepatří, dělá ji samostatná rešerše. Stejně tak eko tvrzení (modul `eco`).

## Zdroje

Cesty jsou relativně k `D:\_github\Overko\podklady\`. Tučně jsou soubory stažené v rámci této rešerše (oficiální znění ze static.slov-lex.sk a z Cellaru EU).

| Zkratka | Soubor | Znění | Poznámka |
| --- | --- | --- | --- |
| Z108 | `predpisy-sk/sk-108-2024-ochrana-spotrebitela-zneni-od-2026-09-27.txt` | od 27. 9. 2026 | čísla řádků „ř.“ odkazují na tento soubor |
| P2 | **`predpisy-sk/sk-108-2024-priloha-2-vzorovy-formular-zneni-od-2026-09-27.txt`** (+ `.pdf`) | od 27. 9. 2026 | příloha č. 2, text vytažený z oficiálního PDF |
| P3 | **`predpisy-sk/sk-108-2024-priloha-3-poucenie-o-odstupeni-zneni-od-2026-09-27.txt`** (+ `.pdf`) | od 27. 9. 2026 | příloha č. 3 (vzorové poučenie) |
| N310 | `predpisy-sk/sk-310-2025-novela-empco.txt` | novela | kdy co nabývá účinnosti (čl. V) |
| OZ | `predpisy-sk/sk-40-1964-obciansky-zakonnik.txt` | od 31. 7. 2026 (novější verze na slov-lex není) | soubor je jeden řádek, proto bez čísel řádků |
| Z22 | **`predpisy-sk/sk-22-2004-elektronicky-obchod-zneni-od-2025-06-28.txt`** | od 28. 6. 2025 (poslední verze) | zákon o elektronickom obchode |
| ZC | **`predpisy-sk/sk-18-1996-o-cenach-zneni-od-2026-01-01.txt`** | od 1. 1. 2026 (poslední verze) | zákon o cenách, § 15 (odkaz 39 z § 6 ods. 1 Z108) |
| ObZ | **`predpisy-sk/sk-513-1991-obchodny-zakonnik-zneni-od-2026-08-17.txt`** | od 17. 8. 2026 (poslední verze) | § 3a (IČO a údaje na webe) |
| Z391 | **`predpisy-sk/sk-391-2015-alternativne-riesenie-sporov-zneni-od-2026-01-01.txt`** | od 1. 1. 2026 (poslední verze) | odkaz 27 a 28 z § 5 ods. 1 písm. q) Z108 |
| Z351 | **`predpisy-sk/sk-351-2022-pristupnost-zneni-od-2026-05-30.txt`** | od 30. 5. 2026 (poslední verze) | prístupnosť služieb |
| Z136 | **`predpisy-sk/sk-136-2010-sluzby-na-vnutornom-trhu-zneni-od-2025-06-28.txt`** | od 28. 6. 2025 (poslední verze) | odkaz 56a z § 13a Z108; použitelnost na e-shop nejistá |
| EU1960 | **`predpisy-eu/eu-2025-1960-harmonizovane-oznamenie-sk.txt`** (+ `.xhtml` s obrázky) | uplatňuje sa od 27. 9. 2026 | vykonávacie nariadenie (EÚ) 2025/1960, odkaz 22a Z108 |
| GPSR | **`predpisy-eu/eu-2023-988-gpsr-bezpecnost-vyrobkov-sk.txt`** (+ `.xhtml`) | uplatňuje sa od 13. 12. 2024 | nariadenie (EÚ) 2023/988, čl. 19; odkaz 56c Z108 |
| SOI-VS25 | `soi/soi-vyrocna-sprava-2025.txt` | – | výklad a praxe SOI, ne předpis (stáhl jiný rešeršista) |
| SOI-Z26 | `soi/soi-dohlad-zlavy-2026.txt` | – | výklad SOI k § 7 (zľavy), ne předpis |
| YAML | `..\eshop-guard\rules\legal_sk.yaml` | draft1 | existující slovenské kontroly (jen mapuji) |

U všech stažených zákonů ukazovala historie verzí na slov-lex k 25. 9. 2026 poslední verzi bez konce účinnosti, takže platí i 27. 9. 2026.

## Legenda

- **Kontrola:** `jev` = rozhodnutí ano/ne nad větou nebo odstavcem; `kód` = regulární výraz, seznam nebo struktura HTML (čísla, lhůty, ceny, IČO, telefon, e-mail, text tlačítka vždy kód); `jev + kód` = obojí je potřeba; `nelze z textu` = vyžaduje košík, pokladnu, účet zákazníka, historická data nebo fakta mimo web.
- **Kde na webu:** právní stránky (obchodné podmienky = VOP, reklamačný poriadok, doprava a platba, kontakt, odstúpenie), rám (patička a hlavička), produktová stránka, výpis kategorie, košík a pokladna (nástroj je neprochází), zákaznícky účet (neprochází), e-mail po objednávke (mimo web).
- **Skládání:** `site_presence` = informace aspoň v jednom odstavci právních stránek nebo v rámu (maximum přes odstavce ≥ `presence_threshold` 0,7); `page_presence` = musí platit na každé stránce daného typu (typicky produktové), nález po stránkách; `segment` = nález nad konkrétní větou nebo odstavcem (logika `all`/`any`/`none`). „Brána“ = pravidlo se vyhodnotí, jen když detektor potvrdí, že povinnost na web dopadá (marketplace, předplatné, recenze, sleva…).
- **Závažnost (návrh):** `high` / `medium` / `low`. Vysoká tam, kde zákon spojuje s chybějící informací přímý následek (§ 15 ods. 7, § 17 ods. 6, § 20 ods. 3, § 21 ods. 3 Z108) nebo kde jde o jádro kontrol SOI.
- **Typ „správnost obsahu“:** nejde o samostatnou informační povinnost, ale o kontrolu, že web o právech neinformuje v rozporu se zákonem (§ 4 ods. 2 písm. c) až e) Z108). Hodí se do stejného modulu, protože se pozná z textu.

## Hlavní zjištění předem

1. **Reklamačný poriadok jako samostatná povinnost e-shopu v platném znění neexistuje.** OZ nepoužívá slovo „reklamácia“ ani „reklamačný“ (jen „vytknutie vady“; v celém souboru OZ se vyskytuje jen „reklama“ ve smyslu propagace). V Z108 se „reklamačný poriadok“ objevuje jen v novelizačních článcích sektorových zákonů: „Banka a pobočka zahraničnej banky sú povinné vypracovať reklamačný poriadok a zverejniť ho na svojom webovom sídle …“ (čl. V, Z108 ř. 5436–5437), obdobně věřitel spotřebitelského úvěru (čl. XIII, ř. 6606) a dodavatelé elektřiny a plynu (čl. XVIII, ř. 6910–6911); devízová místa mají pravidla vybavovania reklamácií (čl. III, ř. 5140–5201). Pro obecný online prodej zboží takové ustanovení není. Z108 zrušil zákon č. 250/2007 Z. z. („Predpis ruší 250/2007 Z. z. Zákon o ochrane spotrebiteľa…“, Z108 ř. 1105–1107), kde povinnost reklamačného poriadku bývala; znění starého § 18 jsem lokálně neověřoval (NEOVĚŘENO). Obsahově ji dnes nesou § 5 ods. 1 písm. f) Z108 (hlavní informace o zodpovednosti za vady + harmonizované oznámenie), § 15 ods. 1 písm. c) Z108 (adresa pro reklamace, pokud je jiná) a § 622 ods. 1 OZ (kde lze vadu vytknout). Název stránky „Reklamačný poriadok“ je tedy jen zvyklost; kontrola má hledat obsah, ne dokument.
2. **Nové od 27. 9. 2026** (N310 čl. V; pro informační povinnosti jsou podstatné body čl. I 4–10, 20, 24 a 26): harmonizované oznámenie o zákonnej záruke (§ 5 ods. 1 písm. f) + EU1960), harmonizované označenie GARAN (§ 5 ods. 1 písm. g)), zodpovednosť za vady digitálneho obsahu (písm. h)), záruky nad rámec zákona (písm. j)), opraviteľnosť a náhradné diely (písm. k), l)), doba aktualizácií (písm. p)), ARS se přečísluje na písm. q), metóda porovnávania (§ 11 ods. 6 písm. c)), ekologické možnosti dodania (§ 15 ods. 1 písm. l)) a rozšíření souhrnu před objednávkou o písm. g) (§ 17 ods. 3). Od 31. 7. 2026 platí změny OZ (predĺženie zodpovednosti o 12 mesiacov po oprave, § 623 ods. 2 poučenie o výbere opravy/výmeny) a § 13a, 13b Z108. Funkcia „odstúpiť od zmluvy tu“ (§ 20a) je ve znění od 31. 7. 2026 už obsažena (`predpisy-sk/starsi-zneni/`); přesné datum účinnosti (pravděpodobně 19. 6. 2026, zákon 311/2025) NEOVĚŘENO.
3. **Platforma RSO (nariadenie 524/2013):** N310 čl. I bod 1 a bod 46 vypustil odkazy na toto nariadenie z poznámky 4 a z § 43 Z108. Povinnost odkazu na platformu RSO proto v Z108 není; zrušení samotného nariadenia (nariadením (EÚ) 2024/3228) jsem lokálně neověřoval (NEOVĚŘENO). Starý odkaz na `ec.europa.eu/consumers/odr` na webu lze hlásit jen jako zastaralou informaci (low).
4. **Počty:** 73 položek, z toho 5 existujících kontrol k namapování (4 pravidla v YAML a kandidát § 20a). Podle hlavní metody: `jev` 19, `kód` 19, `jev + kód` 22, `nelze z textu` 13. Přehled je v souhrnné tabulce na konci.

### Mapování existujících kontrol (YAML `legal_sk.yaml`, jen mapuji, nepřepisuji)

| Existující pravidlo (otázka) | Položka zde | Co z povinnosti zůstává mimo a řeší nová položka |
| --- | --- | --- |
| `legal_adr_missing` (`legal_adr`) | `sk_adr` | poučenie o žiadosti o nápravu → `sk_adr_redress_request` |
| `legal_complaints_missing` (`legal_complaints`) | `sk_legal_guarantee_info` | dĺžka trvania → `sk_legal_guarantee_duration`; harmonizované oznámenie → `sk_harmonized_legal_guarantee_notice`; správnost obsahu → `sk_complaint_handling_deadline`, `sk_defect_notice_period`, `sk_remedy_choice` |
| `legal_withdrawal_missing` (`legal_withdrawal` + regex 14 dní) | `sk_withdrawal_info` | umístění funkce § 20a → `sk_withdrawal_function_location_info`; náklady na vrátenie → `sk_withdrawal_return_costs`; výnimky → `sk_withdrawal_exceptions`; služby → `sk_withdrawal_service_payment`; nevyplnená šablona → `sk_withdrawal_template_unfilled` |
| `legal_withdrawal_form_missing` (`legal_withdrawal_form`) | `sk_withdrawal_form` | návrh doplňkového kódu (charakteristické věty přílohy č. 2) |
| kandidát v hlavičce YAML (§ 20a, tlačítko) | `sk_withdrawal_function` | upřesnění, že funkce může být za přihlášením → „k ověření“ |

---

## A. Identita a kontakt obchodníka

### sk_trader_identity
- **Ustanovení:** § 5 ods. 1 písm. b) Z108; § 11 ods. 4 písm. b) Z108 (výzva na kúpu); § 4 ods. 1 písm. a) Z22; § 3a ods. 1 a 3 ObZ.
- **Citace:**
  - „obchodné meno a sídlo alebo miesto podnikania obchodníka alebo osoby, v ktorej mene obchodník koná,“ (Z108 ř. 1389–1390)
  - „Poskytovateľ služieb je povinný príjemcovi služby na elektronickom zariadení poskytnúť najmä tieto informácie: a) názov, obchodné meno a sídlo poskytovateľa služieb, ak ide o právnickú osobu, alebo meno, priezvisko, miesto podnikania a adresu bydliska poskytovateľa služieb, ak ide o fyzickú osobu,“ (Z22 ř. 310–316)
  - „Každý podnikateľ je povinný na svojich obchodných listoch a objednávkach … uvádzať obchodné meno, sídlo alebo miesto podnikania, právnu formu právnickej osoby a identifikačné číslo, ak je pridelené.“ (ObZ ř. 4267–4270) + „Údaje podľa odseku 1 je podnikateľ povinný uvádzať aj na svojom webovom sídle, ak ho má zriadené.“ (ObZ ř. 4277–4278)
- **Požadavek lidsky:** Web musí říct, kdo prodává: obchodní jméno (u s. r. o. včetně právní formy), sídlo nebo místo podnikání. U podnikatele fyzické osoby Z22 chce i adresu bydliště.
- **Kde na webu:** kontakt, VOP, patička (rám).
- **Kontrola:** jev + kód. Kód najde právní formu a PSČ, ale nepozná, čí je adresa (prodejce, výdejní místo, dopravce); to rozhodne Jev. Zda je adresa bydliště shodná s místem podnikání, z textu nelze.
- **Otázky pro Jev:**
  - `legal_seller_name` — text_en: "Does this text state the business name of the seller who operates this online shop (a company name or the name of a sole trader)?" — text_cs: "Uvádza tento text obchodné meno predávajúceho, ktorý prevádzkuje tento e-shop (názov spoločnosti alebo meno podnikateľa – fyzickej osoby)?"
  - `legal_seller_address` — text_en: "Does this text state the postal address of the seller's registered office or place of business?" — text_cs: "Uvádza tento text poštovú adresu sídla alebo miesta podnikania predávajúceho?"
- **Kontrola kódem:**
  - právní forma: `(?i)\b(s\.\s?r\.\s?o\.|spol\.\s?s\s?r\.\s?o\.|a\.\s?s\.|k\.\s?s\.|v\.\s?o\.\s?s\.|j\.\s?s\.\s?a\.|družstvo)`
  - adresa se slovenským PSČ: `\p{Lu}[\p{L}\.\- ]+\s\d+[a-zA-Z]?(/\d+[a-zA-Z]?)?,?\s+\d{3}\s?\d{2}\s+\p{Lu}\p{L}+`
- **Skládání:** `site_presence` pro obě otázky (≥ 0,7 v aspoň jednom odstavci právních stránek nebo rámu) a `regex_required` PSČ na právních stránkách nebo v rámu. Právní forma se u fyzické osoby neuvádí, proto je jen pomocný signál; u fyzické osoby jako signál poslouží IČO (viz `sk_trader_ico`).
- **Závažnost (návrh):** high. SOI-VS25 (ř. 490–494) uvádí chybějící obchodní jméno a adresu mezi typickými znaky rizikových e-shopů.
- **Poznámky a nejistoty:** Rám (patička) se dnes zpracovává jen v eko modulu po větách; pro právní modul je potřeba vyhodnocovat i rám jako zdroj pro `site_presence`. U marketplace jde o identitu každého prodejce (viz `sk_marketplace_info`).

### sk_trader_ico
- **Ustanovení:** § 3a ods. 1 a 3 ObZ.
- **Citace:** „… uvádzať obchodné meno, sídlo alebo miesto podnikania, právnu formu právnickej osoby a identifikačné číslo, ak je pridelené.“ (ObZ ř. 4268–4270); „Údaje podľa odseku 1 je podnikateľ povinný uvádzať aj na svojom webovom sídle, ak ho má zriadené.“ (ObZ ř. 4277–4278)
- **Požadavek lidsky:** Na webu musí být IČO (identifikačné číslo organizácie).
- **Kde na webu:** kontakt, VOP, patička.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `(?i)\bI[ČC]O\s*:?\s*(\d{2}\s?\d{3}\s?\d{3}|\d{6,8})\b` na právních stránkách a v rámu.
- **Skládání:** `site_presence` přes `regex_required`.
- **Závažnost (návrh):** medium (SOI-VS25 ř. 492 jmenuje chybějící IČO u rizikových e-shopů).
- **Poznámky a nejistoty:** Kontrolní součet slovenského IČO (obdoba modulo 11) NEOVĚŘENO, proto jen formát. Starší IČO mohou mít 6 číslic. Povinnost plyne z ObZ, ne ze spotřebitelských předpisů; v nálezu citovat ObZ.

### sk_trader_register
- **Ustanovení:** § 4 ods. 1 písm. d) Z22; § 3a ods. 1 druhá veta a ods. 3 ObZ.
- **Citace:** „označenie registra, ktorý ho zapísal, a číslo zápisu,“ (Z22 ř. 321–322); „Podnikatelia zapísaní v obchodnom registri alebo v inej evidencii podnikateľov uvádzajú aj označenie registra, ktorý podnikateľa zapísal, a číslo zápisu.“ (ObZ ř. 4270–4271)
- **Požadavek lidsky:** Uvést, ve kterém registru je prodejce zapsán (obchodný register okresného súdu, živnostenský register okresného úradu) a pod jakým číslem (oddiel, vložka / číslo živnostenského registra).
- **Kde na webu:** kontakt, VOP, patička.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** registr `(?i)(obchodn\w+\s+regist\w+|živnostensk\w+\s+regist\w+)`; číslo zápisu `(?i)oddiel\s*:?\s*\w+.{0,40}?vložk\w*\s*(č\.)?\s*:?\s*\d+/?\p{Lu}?` nebo `(?i)č(íslo)?\.?\s*živnostensk\w+\s+regist\w+\s*:?\s*\d{3}-\d{3,6}`.
- **Skládání:** `site_presence`; nález „k ověření“, když chybí registr nebo číslo zápisu.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Formáty čísel živnostenského registra se liší, regex je jen návrh.

### sk_trader_vat_id
- **Ustanovení:** § 4 ods. 1 písm. b) Z22 (doplňkově § 6 ods. 1 písm. d) Z136, použitelnost nejistá).
- **Citace:** „daňové identifikačné číslo, ak je platiteľom dane z pridanej hodnoty,12)“ (Z22 ř. 317–318); „identifikačnom čísle pre daň z pridanej hodnoty, ak mu bolo pridelené, inak o daňovom identifikačnom čísle,“ (Z136 ř. 804–805)
- **Požadavek lidsky:** Plátce DPH uvede daňové číslo (v praxi IČ DPH, případně DIČ).
- **Kde na webu:** kontakt, VOP, patička.
- **Kontrola:** kód; zda je prodejce plátce DPH, z textu nelze (údaj v registru mimo web).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `(?i)\bI[ČC]\s*DPH\s*:?\s*SK\s?\d{10}\b`, `(?i)\bDI[ČC]\s*:?\s*\d{10}\b`.
- **Skládání:** `site_presence`; bez nálezu „chybí“ nejvýš „k ověření“ (nevíme, zda je plátce).
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Z22 píše „daňové identifikačné číslo“ s odkazem na starý zákon o DPH, zatímco Z136 rozlišuje IČ DPH a DIČ; co přesně stačí, NEOVĚŘENO. Ověření plátcovství přes registr Finančnej správy je mimo nástroj.

### sk_trader_phone
- **Ustanovení:** § 5 ods. 1 písm. c) Z108; § 15 ods. 3 Z108; § 4 ods. 1 písm. c) Z22.
- **Citace:** „telefónne číslo obchodníka,“ (Z108 ř. 1392); „… je povinný poskytnúť spotrebiteľovi kontaktné údaje podľa odseku 1 písm. a) a b) a podľa § 5 ods. 1 písm. b) a c) na komunikačné prostriedky, ktoré umožňujú spotrebiteľovi rýchlo kontaktovať obchodníka a účinne s ním komunikovať.“ (Z108 ř. 2078–2081); „adresu elektronickej pošty a telefónne číslo,“ (Z22 ř. 319–320)
- **Požadavek lidsky:** Na webu musí být telefon prodejce.
- **Kde na webu:** kontakt, patička, VOP.
- **Kontrola:** kód. Zda linka skutečně funguje a odpovídá („rýchlo… účinne“), z textu nelze.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** odkazy `href="tel:…"`; v textu kandidáti `(?:\+|00)421[\s\-/]?(\(0\)[\s\-/]?)?\d[\d\s\-/]{7,13}\d|\b0\d[\d\s\-/]{7,12}\d\b` blízko slov `(?i)(tel|telef|mobil|infolink|hotline|volajte)`; kandidáta potvrdit normalizací (odstranit mezery, pomlčky, lomítka, závorky) na `^(?:\+?421|00421|0)\d{9}$`.
- **Skládání:** `site_presence` přes `regex_required` (právní stránky + rám).
- **Závažnost (návrh):** high (SOI-VS25 ř. 492: chybějící telefon u rizikových e-shopů).
- **Poznámky a nejistoty:** Pozor na záměnu s IČO, IBAN a čísly objednávek; proto normalizace na 9 číslic národního čísla. Prémiová čísla viz `sk_contact_premium_rate`.

### sk_trader_email
- **Ustanovení:** § 15 ods. 1 písm. a) a ods. 3 Z108; § 4 ods. 1 písm. c) Z22.
- **Citace:** „adresu elektronickej pošty obchodníka,“ (Z108 ř. 2033); „adresu elektronickej pošty a telefónne číslo,“ (Z22 ř. 319–320)
- **Požadavek lidsky:** Web musí uvést e-mailovou adresu. Samotný kontaktní formulář nestačí.
- **Kde na webu:** kontakt, patička, VOP.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `href="mailto:…"`; text `[\w.+\-]+@[\w\-]+(\.[\w\-]+)*\.\p{L}{2,}`; maskování `(?i)[\w.+\-]+\s*(\(at\)|\[at\]|\(zavináč\)|\[zavináč\])\s*[\w\-]+\.\p{L}{2,}`. Zvláštní nález „jen kontaktní formulář“: stránka kontaktu má `<form>` s `<textarea>`, ale na webu chybí e-mail i telefon.
- **Skládání:** `site_presence` přes `regex_required`.
- **Závažnost (návrh):** high. SOI-VS25 (ř. 493–494) výslovně zmiňuje e-shopy, které ke komunikaci nabízejí jen kontaktní formulář.
- **Poznámky a nejistoty:** E-mail dopravce nebo platební brány není e-mail prodejce; při více adresách stačí přítomnost, rozlišení role by vyžadovalo Jev (zatím nenavrhuji).

### sk_trader_other_online_channel
- **Ustanovení:** § 15 ods. 1 písm. b) Z108.
- **Citace:** „iný prostriedok online komunikácie, ktorý umožňuje spotrebiteľovi uchovávať na trvanlivom médiu obsah písomnej komunikácie s obchodníkom vrátane dátumu a času komunikácie, ak ho obchodník využíva na komunikáciu so spotrebiteľom,“ (Z108 ř. 2035–2037)
- **Požadavek lidsky:** Pokud prodejce komunikuje se zákazníky jiným online kanálem (chat, messenger), musí ho uvést.
- **Kde na webu:** kontakt, VOP.
- **Kontrola:** nelze z textu (zda kanál používá, je fakt mimo web). Kód může najít chatovací widget jako signál.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** skripty `(?i)(tawk\.to|smartsupp|livechat|intercom|crisp\.chat|messenger|whatsapp|tidio)`; když widget existuje a v kontaktech ani VOP není zmínka `(?i)(chat|messenger|whatsapp)`, poznámka k ověření.
- **Skládání:** `site_presence` s bránou „widget nalezen“.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Zda chat „umožňuje uchovávať… obsah … vrátane dátumu a času“, z textu nelze.

### sk_return_complaint_address
- **Ustanovení:** § 15 ods. 1 písm. c) Z108; § 622 ods. 1 OZ.
- **Citace:** „adresu obchodníka alebo osoby, v ktorej mene obchodník koná, na ktorej môže spotrebiteľ uplatniť práva zo zodpovednosti za vady produktu, odstúpenie od zmluvy, žiadosť o nápravu alebo podať iný podnet, ak ide o adresu odlišnú od adresy podľa § 5 ods. 1 písm. b) ,“ (Z108 ř. 2039–2041); „Vadu možno vytknúť v ktorejkoľvek prevádzkarni predávajúceho, u inej osoby, o ktorej predávajúci oboznámil kupujúceho pred uzavretím zmluvy alebo pred odoslaním objednávky, alebo prostriedkami diaľkovej komunikácie na adrese sídla…“ (OZ § 622 ods. 1)
- **Požadavek lidsky:** Když se reklamace, vrácení nebo odstoupení posílají jinam než do sídla, web musí tu adresu uvést.
- **Kde na webu:** reklamačné podmienky, odstúpenie, VOP.
- **Kontrola:** jev + kód. Zda adresa je „odlišná“, a tedy povinná, z textu nelze; kód porovná nalezené adresy.
- **Otázky pro Jev:** `legal_return_address` — text_en: "Does this text give a postal address to which customers should send complaints about defective goods, returned goods or notices of withdrawal?" — text_cs: "Uvádza tento text poštovú adresu, na ktorú majú zákazníci posielať reklamovaný tovar, vrátený tovar alebo oznámenie o odstúpení od zmluvy?"
- **Kontrola kódem:** adresa (regex ze `sk_trader_identity`) v odstavci s `legal_return_address` ≥ 0,7; porovnat s adresou sídla.
- **Skládání:** `segment` jen informativně (bez nálezu při absenci, protože nejspíš platí adresa sídla). Nález jen když VOP odkazují na „adresu uvedenú nižšie/v kontaktoch“ a ta chybí (`regex` bez adresy v odstavci s kladnou odpovědí).
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Pokrývá i „inú osobu“ z § 622 ods. 1 OZ (servisní místo); existující otázka `legal_complaints` se ptá i na „kde“.

### sk_contact_premium_rate
- **Ustanovení:** § 4 ods. 2 písm. g) Z108 (zákaz); § 15 ods. 1 písm. e) Z108; § 4 ods. 1 písm. b) Z108.
- **Citace:** „používať telefónne číslo služby so zvýšenou tarifou ako telefónne číslo, na ktorom môže spotrebiteľ kontaktovať obchodníka v súvislosti s uzavretou zmluvou,“ (Z108 ř. 1349–1350); „cenu za použitie prostriedkov diaľkovej komunikácie, ktoré je možné použiť pri uzavretí zmluvy, ak sa cena počíta na základe zvýšenej sadzby,“ (Z108 ř. 2046–2047)
- **Požadavek lidsky:** Kontaktní linka k uzavřené smlouvě nesmí být prémiová. Pokud se objednává přes číslo se zvýšenou sazbou, musí být uvedena jeho cena.
- **Kde na webu:** kontakt, patička, VOP.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** po normalizaci čísla prefix prémiových služeb, např. `^(\+421|0)900`; u nalezeného prémiového čísla hledat cenu `(?i)\d+(?:[,.]\d+)?\s?(€|eur)\s?/\s?(min|minút|SMS)` v témže odstavci.
- **Skládání:** `segment` nad kontaktním odstavcem.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Úplný seznam prefixů se zvýšenou tarifou podle číslovacího plánu SR NEOVĚŘENO (0900 je příklad).

### sk_supervisory_authority
- **Ustanovení:** § 4 ods. 1 písm. e) Z22 (dohled nad Z22 vykonává SOI, § 7 Z22).
- **Citace:** „názov a adresu orgánu dozoru alebo dohľadu, ktorému činnosť poskytovateľa služieb podlieha.“ (Z22 ř. 323–325); „Dohľad nad dodržiavaním tohto zákona vykonáva Slovenská obchodná inšpekcia…“ (Z22 ř. 399)
- **Požadavek lidsky:** Uvést název a adresu dozorového orgánu (u běžného e-shopu Slovenská obchodná inšpekcia, typicky inspektorát podle sídla).
- **Kde na webu:** VOP, kontakt.
- **Kontrola:** jev + kód (SOI bývá ve VOP jen jako subjekt ARS, to se nepočítá).
- **Otázky pro Jev:** `legal_supervisory_authority` — text_en: "Does this text name a public authority as the body that supervises the seller's business (for example the Slovak Trade Inspection, SOI), not only as a body for resolving disputes?" — text_cs: "Uvádza tento text orgán verejnej správy ako orgán, ktorý vykonáva dozor alebo dohľad nad činnosťou predávajúceho (napr. Slovenskú obchodnú inšpekciu), a nie len ako subjekt na riešenie sporov?"
- **Kontrola kódem:** `(?i)(Slovensk\w+\s+obchodn\w+\s+inšpekci\w+|\bSOI\b|Inšpektorát\s+SOI)` a v témže odstavci adresa (PSČ).
- **Skládání:** `site_presence(legal_supervisory_authority)` a `regex_required` PSČ v odstavci s kladnou odpovědí.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Který inspektorát je místně příslušný, z textu neověřujeme.

### sk_info_permanently_accessible
- **Ustanovení:** § 4 ods. 3 Z22.
- **Citace:** „Informácie podľa odseku 1 a 2 musia byť príjemcovi služby ľahko a trvalo prístupné a rozlíšiteľné od komerčnej komunikácie.“ (Z22 ř. 333–335)
- **Požadavek lidsky:** Identifikační a kontaktní údaje mají být snadno dostupné stále, v praxi odkazem z patičky nebo hlavičky na každé stránce.
- **Kde na webu:** rám (patička) všech stránek.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** v rámu každé stránky odkaz, jehož URL nebo text odpovídá `(?i)(kontakt|obchodn\w*-?podmienk|vop|o-nas|impressum)`.
- **Skládání:** `page_presence` přes všechny stažené stránky; nález „k ověření“ se seznamem stránek bez odkazu.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Pokladna a košík se neprocházejí, takže tam dostupnost nezjistíme.

## B. Jazyk

### sk_language_slovak
- **Ustanovení:** § 4 ods. 1 písm. g) Z108; § 5 ods. 4 Z22.
- **Citace:** „poskytnúť spotrebiteľovi všetky informácie a dokumenty v slovenskom jazyku alebo so súhlasom spotrebiteľa v inom jazyku, ktorý je pre spotrebiteľa zrozumiteľný, …“ (Z108 ř. 1301–1302); „Informácie uvedené v odseku 3 písm. b) musia byť v štátnom jazyku.15)“ (Z22 ř. 374–375)
- **Požadavek lidsky:** Informace a dokumenty (VOP, poučení) musí být slovensky, jiný jazyk jen se souhlasem spotřebitele.
- **Kde na webu:** všechny právní stránky.
- **Kontrola:** kód (detekce jazyka). Souhlas spotřebitele s jiným jazykem z textu nezjistíme.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** detekce jazyka na každé právní stránce: slovenské znaky `[äôľĺŕ]` a slova `(?i)\b(ktor[ýáé]|sú|alebo|tovar\w*|zmluv\w*|spotrebiteľ\w*|predávajúc\w*|kupujúc\w*)\b` proti českým `[ěřů]` a `(?i)\b(kter[ýáé]|jsou|nebo|zboží|smlouv\w*|spotřebitel\w*|prodávající\w*|kupující\w*)\b`. Nález, když převažuje jiný jazyk; slabší nález „české výrazy ve slovenských podmínkách“ (např. „zboží“, „prodávající“).
- **Skládání:** `page_presence` přes právní stránky.
- **Závažnost (návrh):** high (typická chyba českých e-shopů na slovenském trhu), pásmo „k ověření“ kvůli výjimce se souhlasem.
- **Poznámky a nejistoty:** Doména .sk ani slovenština neznamenají slovenského prodejce (SOI-VS25 ř. 874).

## C. Produkt a cena

### sk_product_main_characteristics
- **Ustanovení:** § 5 ods. 1 písm. a) Z108; § 11 ods. 4 písm. a) Z108.
- **Citace:** „hlavné vlastnosti produktu v rozsahu primeranom druhu a povahe produktu a forme poskytnutia informácií,“ (Z108 ř. 1386–1387)
- **Požadavek lidsky:** Produktová stránka musí popsat hlavní vlastnosti výrobku přiměřeně jeho povaze.
- **Kde na webu:** produktová stránka.
- **Kontrola:** kód jako heuristika; přiměřenost popisu z textu posoudit nelze (záleží na druhu výrobku).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** hlavní text produktové stránky bez názvu a ceny má méně než N slov (návrh 15) a chybí tabulka parametrů (`<table>`, `<dl>`) → „k ověření“.
- **Skládání:** `page_presence` (produktové stránky).
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Klamavé údaje o vlastnostech jsou předmětem § 10 Z108 a jiné rešerše.

### sk_price_total
- **Ustanovení:** § 5 ods. 1 písm. d) Z108; § 2 písm. g) bod 1 Z108; § 6 ods. 1 Z108; § 15 ods. 1 ZC.
- **Citace:** „predajnú cenu produktu, spôsob, akým sa vypočíta, ak vzhľadom na povahu produktu nemožno predajnú cenu určiť vopred, …“ (Z108 ř. 1394–1395); „predajnou cenou 1. konečná cena vrátane dane z pridanej hodnoty a všetkých ostatných daní za jednotku produktu alebo za určené množstvo produktu,“ (Z108 ř. 1168–1171); „Každý tovar musí byť pri predaji označený cenou platnou v čase ponuky, a to cenovkou, informáciou o cene formou cenníka, vývesky alebo iným primeraným spôsobom.“ (ZC ř. 885–886)
- **Požadavek lidsky:** Každý nabízený produkt musí mít konečnou cenu včetně DPH.
- **Kde na webu:** produktová stránka (a výpis kategorie).
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** cena `\d{1,3}(?:[ \u00a0.]\d{3})*(?:,\d{1,2})?\s?(€|EUR)\b|€\s?\d+(?:,\d{2})?` nebo JSON-LD `Offer.price` s `priceCurrency`; nález, když cena chybí, nebo když je u ceny `(?i)\bbez\s+DPH\b` a na stránce není cena s DPH (`(?i)(s\s+DPH|vrátane\s+DPH|vr\.\s?DPH)`).
- **Skládání:** `page_presence` (produktové stránky).
- **Závažnost (návrh):** high.
- **Poznámky a nejistoty:** B2B e-shopy s cenami bez DPH jsou falešný poplach, pokud spotřebitelům neprodávají. „Cena na dotaz“ u individuálních produktů může spadat pod „spôsob, akým sa vypočíta“, pásmo „k ověření“.

### sk_unit_price
- **Ustanovení:** § 6 ods. 1, 3 a 4 Z108; § 2 písm. h) Z108.
- **Citace:** „Obchodník je povinný označiť tovar predajnou cenou a jednotkovou cenou jednoznačným a ľahko čitateľným spôsobom podľa osobitného predpisu. 39 ) Jednotková cena nemusí byť vyznačená, ak je zhodná s predajnou cenou.“ (Z108 ř. 1506–1507); „jednotkovou cenou konečná cena vrátane dane z pridanej hodnoty a ostatných daní za kilogram, liter, meter, meter štvorcový, meter kubický tovaru alebo inú jednotku množstva, ktorá sa často a bežne používa pri predaji tovaru,“ (Z108 ř. 1179–1181); výjimka „a) tovar s menovitou hmotnosťou alebo menovitým objemom najviac 50 g alebo 50 ml,“ (Z108 ř. 1520)
- **Požadavek lidsky:** U zboží prodávaného podle hmotnosti, objemu, délky nebo plochy musí být i cena za kg, l, m, m² nebo m³ (výjimky v § 6 ods. 3).
- **Kde na webu:** produktová stránka.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** množství v názvu nebo parametrech `(?i)(?<!\d)(\d+(?:[.,]\d+)?)\s?(kg|g|l|ml|cl|dl|m|cm|m2|m²|m3|m³)\b` (a násobky `\d+\s?[x×]\s?\d+`); když je množství větší než 50 g nebo 50 ml, hledat jednotkovou cenu `(?i)\d+(?:[.,]\d+)?\s?(€|eur)\s?/\s?(1\s?)?(kg|l|m|m2|m²|m3|m³)\b|(?i)\bza\s+(1\s?)?(kg|kilogram|l|liter|m|meter|m2|m²|m3|m³)\b`; cenu za 100 g nebo 100 ml hlásit „k ověření“.
- **Skládání:** `page_presence` s bránou „produkt má uvedené měřitelné množství nad 50 g/ml“.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Výjimky § 6 ods. 3 písm. b)–g) (balíčky různých druhů zboží, nedělitelné zboží, koncentráty…) kód spolehlivě nepozná, proto „k ověření“. Zda zákon připouští jednotku 100 g jako „inú jednotku množstva, ktorá sa často a bežne používa“, NEOVĚŘENO. § 6 ods. 4 (cena za „čistú hmotnosť po vysušení“) jen poznámkou.

### sk_price_reduction_previous_price
- **Ustanovení:** § 7 ods. 1 až 4 Z108.
- **Citace:** „Obchodník je povinný v každom oznámení o znížení ceny tovaru uviesť predchádzajúcu cenu tovaru. Obchodník je povinný určiť zníženie ceny tovaru na základe predchádzajúcej ceny tovaru.“ (Z108 ř. 1544–1546); „Predchádzajúca cena tovaru je najnižšia cena, za ktorú obchodník predával alebo poskytoval tovar a) v období 30 dní pred znížením ceny tovaru, …“ (Z108 ř. 1548–1551)
- **Požadavek lidsky:** Každé oznámení slevy musí ukázat předchozí cenu, tj. nejnižší cenu za 30 dní před slevou, a sleva (i v procentech) se musí počítat z ní. Při postupném snižování lze uvádět cenu před prvním snížením (ods. 3); neplatí pro rychle se kazící zboží (ods. 4).
- **Kde na webu:** produktová stránka, výpis kategorie, bannery a akční stránky.
- **Kontrola:** jev + kód. Jev pozná oznámení slevy, kód ceny a procenta. Zda uvedená předchozí cena je skutečně nejnižší za 30 dní, z textu nelze (historická data; šlo by jen opakovaným stahováním).
- **Otázky pro Jev:**
  - `price_reduction_announced` — text_en: "Does the sentence (field sentence) announce that the price of a product has been reduced, for example a discount, a sale price or a 'was – now' price?" — text_cs: "Oznamuje veta (pole sentence), že cena produktu bola znížená, napríklad zľavu, výpredajovú cenu alebo porovnanie „predtým – teraz“?"
  - `price_reference_rrp` — text_en: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) describe the higher compared price as the manufacturer's recommended retail price?" — text_cs: "Označuje veta (pole sentence) alebo text hneď vedľa nej (context_before, context_after) vyššiu porovnávanú cenu ako odporúčanú predajnú cenu výrobcu?"
- **Kontrola kódem:** značky slevy `(?i)(-\s?\d{1,2}\s?%|\bzľav\w*|\bzlacnen\w*|\bvýpredaj\w*|\bakci\w*\b|black\s*friday|pôvodn\w+\s+cen\w*|bežn\w+\s+cen\w*|predtým|ušetrít\w*)`; štítek předchozí ceny `(?i)(najnižš\w+\s+cen\w*[^.]{0,40}30\s*dn\w*|predchádzajúc\w+\s+cen\w*)`; ceny v cenovém bloku produktu. Nálezy: (a) sleva bez druhé ceny; (b) procento neodpovídá rozdílu vůči uvedené „najnižšej cene za 30 dní“, ale odpovídá jiné vyšší ceně (SOI-Z26, ř. 29–33: procentní sleva se má počítat z předchozí ceny); (c) přeškrtnutá cena v HTML (`<del>`, `<s>`, `line-through`) bez štítku předchozí ceny.
- **Skládání:** `segment`: `all: [price_reduction_announced ≥ 0,5]`, `none: [price_reference_rrp ≥ 0,5]` a kódový nález (a), (b) nebo (c); na produktové stránce navíc `page_presence` pro kódové nálezy.
- **Závažnost (návrh):** high (SOI dělá cílené kontroly slev, SOI-Z26).
- **Poznámky a nejistoty:** Zda jasně označené srovnání s odporúčanou cenou výrobce není „oznámením o znížení ceny“ podle § 7, v lokálních zdrojích nevykládáno (NEOVĚŘENO); proto `none: price_reference_rrp` jen snižuje pásmo, rozhodne právník. Pojem v zákoně o cenách: „… cenu s označením „odporúčaná spotrebiteľská cena““ (ZC ř. 892–894, § 15 ods. 3). Výjimku pro rychle se kazící zboží (§ 7 ods. 4) z textu nepoznáme.

### sk_delivery_costs
- **Ustanovení:** § 5 ods. 1 písm. d) Z108; § 11 ods. 4 písm. c) Z108; následek § 15 ods. 7 Z108.
- **Citace:** „… náklady na dopravu, dodanie, poštovné a iné náklady a poplatky …“ (Z108 ř. 1395–1396); „Ak obchodník nesplnil informačnú povinnosť o úhrade nákladov na dopravu, dodanie, poštovné alebo iných nákladov alebo poplatkov podľa § 5 ods. 1 písm. d) …, spotrebiteľ nie je povinný tieto náklady alebo poplatky uhradiť …“ (Z108 ř. 2103–2105)
- **Požadavek lidsky:** Web musí předem říct, kolik stojí doprava a další poplatky (dobírka, balné).
- **Kde na webu:** doprava a platba, VOP, případně produktová stránka.
- **Kontrola:** jev + kód (Jev pozná, že odstavec mluví o ceně dopravy pro zákazníka; částky najde kód).
- **Otázky pro Jev:** `legal_delivery_cost` — text_en: "Does this text state how much the customer pays for delivery or shipping, or that delivery is free?" — text_cs: "Uvádza tento text, koľko zákazník zaplatí za dopravu alebo doručenie, prípadne že doprava je zadarmo?"
- **Kontrola kódem:** v odstavci s kladnou odpovědí částka `\d+(?:,\d{2})?\s?(€|EUR)` nebo `(?i)(zdarma|zadarmo|bezplatn\w+)`; dopravci `(?i)(kuriér|packeta|zásielkovň\w*|slovensk\w+\s+pošt\w*|dpd|gls|sps|balíkobox|osobn\w+\s+odber)`.
- **Skládání:** `site_presence(legal_delivery_cost)` a `regex_required` částky nebo „zadarmo“ v témže odstavci.
- **Závažnost (návrh):** high (následek § 15 ods. 7).
- **Poznámky a nejistoty:** Ceník dopravy bývá jen v košíku; když na právních stránkách chybí, nález „k ověření“ s poznámkou, že košík nekontrolujeme.

### sk_additional_costs_notice
- **Ustanovení:** § 5 ods. 1 písm. d) Z108 (poslední část).
- **Citace:** „… a skutočnosť, že do celkovej ceny môžu byť zarátané ďalšie náklady a poplatky, ak náklady a poplatky nemožno určiť vopred,“ (Z108 ř. 1396–1397)
- **Požadavek lidsky:** Když některé náklady nejde předem vyčíslit (clo, poplatky při dodání mimo EU, montáž podle místa), musí web upozornit, že se k ceně mohou připočíst.
- **Kde na webu:** doprava a platba, VOP.
- **Kontrola:** jev; zda takové náklady vznikají, z textu nelze.
- **Otázky pro Jev:** `legal_extra_costs` — text_en: "Does this text say that further costs or fees (for example customs duties) may be added to the total price because they cannot be calculated in advance?" — text_cs: "Uvádza tento text, že k celkovej cene môžu pribudnúť ďalšie náklady alebo poplatky (napr. clo), ktoré nemožno vopred vyčísliť?"
- **Kontrola kódem:** brána: dodání mimo EU `(?i)(mimo\s+E[ÚU]|tretích\s+krajín|clo|colné)`.
- **Skládání:** `site_presence` jen s bránou; jinak bez nálezu.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Obvykle se týká jen dodání mimo EU nebo služeb s proměnnou cenou.

### sk_payment_surcharge
- **Ustanovení:** § 4 ods. 2 písm. f) Z108 (související zákaz, typ „správnost obsahu“).
- **Citace:** „účtovať spotrebiteľovi poplatky za použitie 1. platobného prostriedku 14 ) alebo 2. iného spôsobu platby, ako je platobný prostriedok, ktoré prevyšujú skutočné náklady, ktoré obchodníkovi pri platbe vzniknú,“ (Z108 ř. 1342–1347)
- **Požadavek lidsky:** Za platbu platebním prostředkem (např. kartou) se nesmí účtovat poplatek; za jiný způsob (typicky dobírka) nejvýš skutečné náklady.
- **Kde na webu:** doprava a platba, VOP.
- **Kontrola:** jev + kód; výši skutečných nákladů u dobírky z textu nelze posoudit.
- **Otázky pro Jev:** `legal_payment_surcharge` — text_en: "Does this text say that the customer pays an extra fee for using a particular payment method (for example card payment or cash on delivery)?" — text_cs: "Uvádza tento text, že zákazník zaplatí príplatok za použitie určitého spôsobu platby (napr. platby kartou alebo dobierky)?"
- **Kontrola kódem:** v odstavci s kladnou odpovědí `(?i)(kart\w+|platobn\w+\s+kart\w+|Apple\s?Pay|Google\s?Pay|online\s+platb\w+)` s částkou `\+?\s?\d+(?:,\d{2})?\s?(€|eur)` → nález; u `(?i)dobierk\w+` jen „k ověření“.
- **Skládání:** `segment` (`all: [legal_payment_surcharge ≥ 0,5]` + kód).
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Výklad, že za „platobný prostriedok“ je poplatek zakázán úplně a omezení „skutočné náklady“ patří jen k bodu 2, plyne z členění textu; potvrdit právníkem. Definice platobného prostriedku je v zákoně č. 492/2009 Z. z. (odkaz 14), nestahoval jsem.

### sk_prechecked_paid_options
- **Ustanovení:** § 5 ods. 2 Z108.
- **Citace:** „Obchodník nesmie v návrhu zmluvy alebo pri ktoromkoľvek úkone, ktorý predchádza uzavretiu zmluvy, ponúkať spotrebiteľovi predvolené možnosti, pri ktorých sa vyžaduje úkon spotrebiteľa smerujúci k ich odmietnutiu s cieľom vyhnúť sa úhrade dodatočných nákladov.“ (Z108 ř. 1450–1452)
- **Požadavek lidsky:** Placené doplňky (pojištění, prodloužená záruka, dárkové balení) nesmí být předem zaškrtnuté.
- **Kde na webu:** košík a pokladna, případně produktová stránka.
- **Kontrola:** nelze z textu (stav zaškrtávacích polí je v košíku, který nástroj neprochází).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** jen na produktové stránce: `<input type="checkbox" checked>` s popiskem obsahujícím cenu `\d+(?:,\d{2})?\s?(€|eur)` → „k ověření“.
- **Skládání:** `page_presence` (produktové stránky), jinak mimo rozsah.
- **Závažnost (návrh):** medium (právní následek: bez výslovného souhlasu spotřebitel neplatí), ale kontrolovatelnost nízká.
- **Poznámky a nejistoty:** Pro plnou kontrolu by byl potřeba průchod košíkem v headless prohlížeči.

## D. Plnění, platba, dodání

### sk_delivery_terms_deadline
- **Ustanovení:** § 5 ods. 1 písm. e) Z108; § 613 ods. 1 OZ (výchozí lhůta).
- **Citace:** „podmienky plnenia, platobné podmienky, dodacie podmienky a lehotu, do ktorej sa obchodník zaväzuje dodať alebo poskytnúť produkt,“ (Z108 ř. 1399–1400); „Predávajúci dodá kupujúcemu predanú vec bez zbytočného odkladu, najneskôr do 30 dní odo dňa uzavretia zmluvy, ak sa strany nedohodli inak.“ (OZ § 613 ods. 1)
- **Požadavek lidsky:** Web musí uvést dodací podmínky a lhůtu, do kdy prodejce zboží dodá.
- **Kde na webu:** doprava a platba, VOP, produktová stránka (dostupnost).
- **Kontrola:** jev + kód (Jev potvrdí, že jde o dobu dodání, ne o lhůtu na odstoupení; číslo najde kód).
- **Otázky pro Jev:** `legal_delivery_time` — text_en: "Does this text state how long delivery takes or by what date the goods will be delivered?" — text_cs: "Uvádza tento text, ako dlho trvá dodanie tovaru alebo do kedy bude tovar dodaný?"
- **Kontrola kódem:** `(?i)(dodac\w+\s+lehot\w*|doručen\w*|dodan\w*|expedí\w+|expedujeme|odosielame|odošleme)[^.]{0,80}?(\d{1,2})(\s?[-–]\s?(\d{1,2}))?\s*(pracovn\w+\s+)?(dn\w*|dní|hod\w*|týžd\w+)`; hodnotu nad 30 dní hlásit „k ověření“ (OZ § 613 ods. 1 umožňuje jinou dohodu).
- **Skládání:** `site_presence(legal_delivery_time)` a `regex_required` čísla v témže odstavci; produktová dostupnost jako doplňkový signál.
- **Závažnost (návrh):** medium (SOI-VS25 ř. 483–484 uvádí stížnosti na datum dodání).
- **Poznámky a nejistoty:** „Skladom“ bez lhůty nestačí; formulace „čo najskôr“ bez čísla → „k ověření“.

### sk_payment_methods
- **Ustanovení:** § 5 ods. 1 písm. e) Z108 (platobné podmienky); § 17 ods. 2 Z108.
- **Citace:** „Obchodník je povinný zabezpečiť najneskôr na začiatku postupu vytvárania objednávky spotrebiteľom označenie online rozhrania jasnými a čitateľnými informáciami o prípadných obmedzeniach dodávky alebo poskytnutia produktu a informáciami o spôsoboch platby, ktoré spotrebiteľ môže použiť na úhradu ceny.“ (Z108 ř. 2149–2152)
- **Požadavek lidsky:** Nejpozději na začátku objednávky musí být vidět, jak lze platit.
- **Kde na webu:** doprava a platba, VOP, rám (loga platebních metod); začátek objednávky je v košíku.
- **Kontrola:** jev + kód pro přítomnost na webu; umístění „na začiatku postupu vytvárania objednávky“ nelze z textu (košík).
- **Otázky pro Jev:** `legal_payment_methods` — text_en: "Does this text list the payment methods that the customer can use?" — text_cs: "Uvádza tento text spôsoby platby, ktoré môže zákazník použiť?"
- **Kontrola kódem:** `(?i)(platb\w+\s+kart\w+|platobn\w+\s+kart\w+|dobierk\w+|bankov\w+\s+prevod\w*|prevodom\s+na\s+účet|platba\s+vopred|v\s+hotovosti|Google\s?Pay|Apple\s?Pay|PayPal|GoPay|Comgate|TrustPay|Besteron|Tatra\s?Pay|CardPay|Twisto|Skip\s?Pay|na\s+splátky)`; také alt texty log v rámu.
- **Skládání:** `site_presence(legal_payment_methods)` nebo aspoň dva výrazy ze seznamu na právních stránkách či v rámu.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Nález vždy s poznámkou, že košík nekontrolujeme.

### sk_delivery_restrictions
- **Ustanovení:** § 17 ods. 2 Z108 (obmedzenia dodávky).
- **Citace:** „… jasnými a čitateľnými informáciami o prípadných obmedzeniach dodávky alebo poskytnutia produktu …“ (Z108 ř. 2150–2151)
- **Požadavek lidsky:** Pokud prodejce nedoručuje všude (jen SR, ne do zahraničí, ne na ostrovy…), musí to říct nejpozději na začátku objednávky.
- **Kde na webu:** doprava a platba, VOP.
- **Kontrola:** jev pro přítomnost; zda omezení existují, z textu nelze, proto bez nálezu při absenci.
- **Otázky pro Jev:** `legal_delivery_restriction` — text_en: "Does this text state a restriction on where or to whom the goods can be delivered (for example delivery only within Slovakia)?" — text_cs: "Uvádza tento text obmedzenie, kam alebo komu možno tovar doručiť (napr. doručenie len v rámci Slovenska)?"
- **Kontrola kódem:** žádná.
- **Skládání:** jen evidence (`segment` bez nálezu), vstup pro ruční kontrolu.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** —

### sk_eco_delivery_options
- **Ustanovení:** § 15 ods. 1 písm. l) Z108 (nové od 27. 9. 2026, N310 čl. I bod 24).
- **Citace:** „možnosti dodania tovaru šetrné k životnému prostrediu, ak sú dostupné.“ (Z108 ř. 2073)
- **Požadavek lidsky:** Pokud prodejce nabízí ekologičtější způsob doručení, musí o něm předem informovat.
- **Kde na webu:** doprava a platba, košík.
- **Kontrola:** nelze z textu (zda taková možnost existuje, je fakt mimo web; často je jen v košíku). Jev jen eviduje zmínky.
- **Otázky pro Jev:** `legal_eco_delivery` — text_en: "Does this text describe a delivery option that is presented as environmentally friendly (for example delivery by cargo bike or electric vehicle, or combining shipments)?" — text_cs: "Opisuje tento text možnosť doručenia, ktorá je prezentovaná ako šetrná k životnému prostrediu (napr. doručenie nákladným bicyklom alebo elektromobilom, alebo zlúčenie zásielok)?"
- **Kontrola kódem:** žádná.
- **Skládání:** bez nálezu; kladné odpovědi předat eko modulu (tvrzení o „ekologickej doprave“ musí být konkrétní).
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Vazba na přílohu č. 1 (obecná environmentální tvrzení) řeší jiná rešerše.

## E. Zodpovednosť za vady (reklamace) a záruky

### sk_legal_guarantee_info
- **Ustanovení:** § 5 ods. 1 písm. f) Z108; § 622 a nasl. OZ. **Existující kontrola:** `legal_complaints_missing` (otázka `legal_complaints`), jen mapuji.
- **Citace:** „existenciu a hlavné informácie o zákonnej zodpovednosti obchodníka za vady tovaru vrátane dĺžky jej trvania, a to zreteľným spôsobom aspoň v podobe a v rozsahu podľa osobitného predpisu 22a ) upravujúceho harmonizované oznámenie,“ (Z108 ř. 1402–1404)
- **Požadavek lidsky:** Web musí informovat, že prodejce odpovídá za vady, jak dlouho a jak se reklamuje.
- **Kde na webu:** reklamačné podmienky, VOP.
- **Kontrola:** jev (existující).
- **Otázky pro Jev:** existující `legal_complaints` (beze změny).
- **Kontrola kódem:** existující pravidlo žádnou nemá; doplnění viz `sk_legal_guarantee_duration`.
- **Skládání:** existující `site_presence`.
- **Závažnost (návrh):** high (beze změny).
- **Poznámky a nejistoty:** Od 27. 9. 2026 nestačí text; povinná je i forma harmonizovaného oznámení (`sk_harmonized_legal_guarantee_notice`). Samostatný „reklamačný poriadok“ zákon nepožaduje (viz úvod).

### sk_legal_guarantee_duration
- **Ustanovení:** § 5 ods. 1 písm. f) Z108 („vrátane dĺžky jej trvania“); § 619 ods. 1, 3 a 4 OZ.
- **Citace:** „Predávajúci zodpovedá za akúkoľvek vadu, ktorú má predaná vec v čase jej dodania a ktorá sa prejaví do dvoch rokov od dodania veci.“ (OZ § 619 ods. 1); „Pri použitej veci sa strany môžu dohodnúť na kratšej dobe zodpovednosti predávajúceho za vady …, nie však kratšej ako jeden rok od dodania veci.“ (OZ § 619 ods. 3); „Po prvom odstránení vady opravou veci sa doba zodpovednosti za vady veci podľa odsekov 1 až 3 predlžuje o 12 mesiacov.“ (OZ § 619 ods. 4)
- **Požadavek lidsky:** Informace o reklamaci musí obsahovat délku odpovědnosti za vady: 24 měsíců, u použitého zboží nejméně 12 měsíců, a od 31. 7. 2026 prodloužení o 12 měsíců po první opravě.
- **Kde na webu:** reklamačné podmienky, VOP.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné (vstupem je odstavec s kladnou existující otázkou `legal_complaints`).
- **Kontrola kódem:** přítomnost `(?i)(24\s*mesiac\w*|dva\s+roky|dvoch\s+rokov|2\s*rok\w*|dvojročn\w*)`; nález „kratší doba“: `(?i)(záruk\w*|zodpovednos\w+\s+za\s+vady)[^.]{0,60}?\b(6|12)\s*mesiac\w*` bez zmínky o použitém zboží `(?i)(použit\w+|bazár\w*|second\s*hand)`.
- **Skládání:** `site_presence` přes `regex_required` v odstavcích o reklamaci; `segment` nález pro kratší dobu.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Prodloužení o 12 měsíců (§ 619 ods. 4 OZ) platí pro smlouvy od 31. 7. 2026 (přechodné ustanovení § 879aa OZ podle N310 čl. II bod 13); zda musí být ve VOP, zákon výslovně neříká (povinnost informovat je v § 623 ods. 2 OZ až při odstraňování vady).

### sk_harmonized_legal_guarantee_notice
- **Ustanovení:** § 5 ods. 1 písm. f) Z108 (nové znění od 27. 9. 2026, N310 čl. I bod 4); čl. 1 a príloha I EU1960.
- **Citace:** „… a to zreteľným spôsobom aspoň v podobe a v rozsahu podľa osobitného predpisu 22a ) upravujúceho harmonizované oznámenie,“ (Z108 ř. 1403–1404); „Harmonizované oznámenie uvedené v článku 22a ods. 1 smernice 2011/83/EÚ musí byť v súlade s dizajnom a obsahom stanovenými v prílohe I k tomuto nariadeniu.“ (EU1960 ř. 52); „Žiadny z prvkov harmonizovaného oznámenia nemožno upravovať.“ (EU1960 ř. 75); „V prípade zmlúv uzavretých na diaľku prostredníctvom online rozhrania musí byť harmonizované oznámenie o zákonnej záruke súladu farebné (RGB).“ (EU1960 ř. 91)
- **Požadavek lidsky:** E-shop prodávající zboží musí před objednávkou zobrazit barevné harmonizované oznámení EU „Zákonná záruka“ v neupravené podobě (obrázek s logem, textem „Minimálne dvojročná zákonná záruka…“ a QR kódem).
- **Kde na webu:** předpis umístění neurčuje; pravděpodobně produktová stránka, pokladna nebo VOP (NEOVĚŘENO).
- **Kontrola:** nelze z textu (jde o obrázek, nástroj obrázky nevidí). Kód umí slabé signály z alt textů, názvů souborů a odkazů.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** alt, title nebo src obrázku `(?i)(harmoniz\w*|zakonna[-_ ]?zaruka|zákonn\w+\s+záruk\w+|legal[-_ ]?guarantee)`; odkaz `(?i)europa\.eu/youreurope`; text vloženého oznámení `(?i)minimálne\s+dvojročná\s+zákonná\s+záruka`.
- **Skládání:** `site_presence` přes kódové signály; bez signálu nález „k ověření“ (ne „chybí“), s poznámkou, že obrázky nekontrolujeme.
- **Závažnost (návrh):** high (nová povinnost pro každý e-shop se zbožím; zatím bez výkladu SOI).
- **Poznámky a nejistoty:** Kde přesně na webu má oznámení být a zda stačí jednou na webu, NEOVĚŘENO (sledovat výklad Komise a SOI). Obsah oznámení je jen v obrázcích přílohy I (vložené JPG v `.xhtml`); text oznámení výše jsem přečetl z obrázku, ne z textové části nařízení.

### sk_durability_guarantee_label
- **Ustanovení:** § 5 ods. 1 písm. g) Z108 (nové od 27. 9. 2026); čl. 2 a príloha II EU1960; § 626 ods. 2 OZ.
- **Citace:** „existenciu a dĺžku trvania spotrebiteľskej záruky na životnosť tovaru, 23 ) ak ju výrobca alebo dovozca bezplatne poskytuje spotrebiteľovi na celý tovar s dĺžkou trvania viac ako dva roky a tieto informácie sprístupnil obchodníkovi, … a to zreteľným spôsobom aspoň v podobe a v rozsahu podľa osobitného predpisu 22a ) upravujúceho harmonizované označenie,“ (Z108 ř. 1406–1409); „V prípade zmlúv uzavretých na diaľku prostredníctvom online rozhrania musí byť harmonizované označenie obchodnej záruky životnosti farebné.“ (EU1960 ř. 130)
- **Požadavek lidsky:** Když výrobce dává bezplatnou záruku životnosti na celý výrobek na víc než 2 roky a prodejci to sdělil, musí e-shop u produktu zobrazit štítek GARAN s počtem let.
- **Kde na webu:** produktová stránka.
- **Kontrola:** jev + kód; zda výrobce takovou záruku dává a sdělil ji prodejci, z textu nelze.
- **Otázky pro Jev:** `product_durability_guarantee` — text_en: "Does the sentence (field sentence) say that the manufacturer guarantees the product's durability or gives a manufacturer's guarantee for a stated number of years?" — text_cs: "Uvádza veta (pole sentence), že výrobca garantuje životnosť produktu alebo poskytuje záruku výrobcu na uvedený počet rokov?"
- **Kontrola kódem:** počet let v téže větě `(?i)(\d{1,2})\s*(rok\w*|rokov)` > 2; štítek v alt, title nebo src obrázku `(?i)\bGARAN\b`.
- **Skládání:** `segment`: `all: [product_durability_guarantee ≥ 0,5]` + kód (roky > 2) a na stránce chybí signál štítku → „k ověření“.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** Štítek smí být „vnorené zobrazenie“ (EU1960 ř. 135–140), plný štítek se zobrazí po kliknutí; kód ho uvidí jen přes alt nebo název souboru. Obchodní záruka prodejce (ne výrobce) sem nepatří, viz `sk_additional_guarantee_terms`.

### sk_complaint_handling_deadline
- **Ustanovení:** § 622 ods. 3 a § 623 ods. 4 OZ; § 852h ods. 6 a § 852k ods. 1 OZ (digitální obsah); § 4 ods. 1 písm. j) Z108 (služby); § 4 ods. 2 písm. e) Z108. Typ „správnost obsahu“.
- **Citace:** „Lehota oznámená podľa predchádzajúcej vety nesmie byť dlhšia ako 30 dní odo dňa vytknutia vady, ak dlhšia lehota nie je odôvodnená objektívnym dôvodom, ktorý predávajúci nemôže ovplyvniť.“ (OZ § 622 ods. 3); „porušiť alebo obchádzať povinnosti obchodníka pri uplatnení práv spotrebiteľa zo zodpovednosti za vady podľa § 622 ods. 3 a 4 , § 623 ods. 2 a § 852h ods. 6 a 7 Občianskeho zákonníka,“ (Z108 ř. 1339–1340)
- **Požadavek lidsky:** Vadu je třeba odstranit nejpozději do 30 dní od reklamace (delší lhůta jen z objektivního důvodu). Text webu nesmí slibovat delší lhůtu.
- **Kde na webu:** reklamačné podmienky, VOP.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `(?i)(reklamáci\w*|vybaven\w*|odstráneni\w+\s+vad\w*)[^.]{0,120}?(\d{1,3})\s*(kalendárn\w+\s+|pracovn\w+\s+)?(dn\w*|dní)`; nález, když je číslo > 30, nebo když jde o 30 „pracovných“ dní.
- **Skládání:** `segment` nad odstavci právních stránek.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Starší VOP mohou uvádět lhůty převzaté z dřívější úpravy nebo z českých podmínek; „odôvodnená objektívnym dôvodom“ z textu neposoudíme, proto pásmo „k ověření“. Pro digitální obsah platí stejných 30 dní (§ 852k ods. 1 OZ).

### sk_defect_notice_period
- **Ustanovení:** § 621 ods. 3 OZ; § 4 ods. 2 písm. d) Z108. Typ „správnost obsahu“.
- **Citace:** „Kupujúci môže uplatňovať práva zo zodpovednosti za vady …, len ak vytkol vadu do dvoch mesiacov od zistenia vady, najneskôr do uplynutia doby podľa § 619 ods. 1 až 4.“ (OZ § 621 ods. 3); „upierať spotrebiteľovi práva, ktoré mu vyplývajú zo zodpovednosti za vady podľa § 622 ods. 1 Občianskeho zákonníka,“ (Z108 ř. 1336–1337)
- **Požadavek lidsky:** Zákazník má na vytknutí vady dva měsíce od zjištění. Kratší lhůta ve VOP (např. „do 24 hodín od doručenia“) zákazníka mate.
- **Kde na webu:** reklamačné podmienky, VOP, doprava.
- **Kontrola:** jev + kód (Jev pozná, že text stanoví lhůtu pro zákazníka; hodnotu porovná kód).
- **Otázky pro Jev:** `legal_defect_notice_limit` — text_en: "Does this text set a time limit within which the customer must report a defect or file a complaint (for example within 24 hours of delivery)?" — text_cs: "Určuje tento text lehotu, v ktorej musí zákazník oznámiť vadu alebo uplatniť reklamáciu (napr. do 24 hodín od doručenia)?"
- **Kontrola kódem:** v odstavci s kladnou odpovědí `(?i)(\d{1,3})\s*(hod\w*|dn\w*|dní|pracovn\w+\s+dn\w*)`; nález, když je lhůta kratší než 2 měsíce.
- **Skládání:** `segment`: `all: [legal_defect_notice_limit ≥ 0,5]` + kód.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Doložky o reklamaci poškození při přepravě u dopravce mohou být legitimní; pásmo „k ověření“.

### sk_remedy_choice
- **Ustanovení:** § 623 ods. 1 a 2 OZ; § 4 ods. 2 písm. d) a e) Z108. Typ „správnost obsahu“.
- **Citace:** „Kupujúci má právo zvoliť si odstránenie vady výmenou veci alebo opravou veci.“ (OZ § 623 ods. 1); „Predávajúci pred odstránením vady informuje kupujúceho o práve vybrať si medzi opravou veci alebo výmenou veci podľa odseku 1 a o predĺžení doby zodpovednosti za vady veci podľa § 619 ods. 4.“ (OZ § 623 ods. 2)
- **Požadavek lidsky:** O opravě nebo výměně rozhoduje zákazník (s výjimkami). VOP, podle kterých rozhoduje prodávající, jsou v rozporu se zákonem.
- **Kde na webu:** reklamačné podmienky, VOP.
- **Kontrola:** jev. Samotné poučení podle § 623 ods. 2 OZ se dává až při vyřizování reklamace, z webu nelze.
- **Otázky pro Jev:** `legal_remedy_seller_decides` — text_en: "Does this text say that the seller decides whether a defective product will be repaired or replaced?" — text_cs: "Uvádza tento text, že o tom, či sa vadný tovar opraví alebo vymení, rozhoduje predávajúci?"
- **Kontrola kódem:** žádná.
- **Skládání:** `segment`: `all: [legal_remedy_seller_decides ≥ 0,5]`.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Prodávající smí odmítnout nemožný nebo nepřiměřeně nákladný způsob (§ 623 ods. 1 a 3 OZ); věta o této výjimce je v pořádku, otázka se ptá jen na obecné rozhodování prodávajícího.

### sk_additional_guarantee_terms
- **Ustanovení:** § 5 ods. 1 písm. j) Z108 (znění od 27. 9. 2026); § 626 ods. 1, 3 a 5 OZ.
- **Citace:** „podmienky popredajného servisu a iných typov spotrebiteľských záruk, 23a ) ako je spotrebiteľská záruka na životnosť tovaru podľa písmena g), ak ich obchodník, výrobca alebo dovozca poskytuje,“ (Z108 ř. 1417–1418); „Ak sú podmienky spotrebiteľskej záruky v súvisiacej reklame pre kupujúceho priaznivejšie ako podmienky podľa záručného listu, platia podmienky uvedené v reklame.“ (OZ § 626 ods. 5)
- **Požadavek lidsky:** Když web nabízí záruku nad zákon (prodloužená záruka, záruka výrobce, servis), musí uvést její podmínky.
- **Kde na webu:** produktová stránka, VOP, reklamačné podmienky.
- **Kontrola:** jev.
- **Otázky pro Jev:**
  - `legal_extra_guarantee` — text_en: "Does this text mention a guarantee offered in addition to the statutory liability for defects, for example an extended warranty or a manufacturer's guarantee?" — text_cs: "Spomína tento text záruku poskytovanú nad rámec zákonnej zodpovednosti za vady, napr. predĺženú záruku alebo záruku výrobcu?"
  - `legal_extra_guarantee_terms` — text_en: "Does this text describe the conditions under which a guarantee offered in addition to the statutory liability for defects can be claimed? If the text mentions no such guarantee, answer no." — text_cs: "Opisuje tento text podmienky, za ktorých možno uplatniť záruku poskytovanú nad rámec zákonnej zodpovednosti za vady? Ak text takú záruku nespomína, odpoveď je nie."
- **Kontrola kódem:** brána `(?i)(predĺžen\w+\s+záruk\w*|záruk\w+\s+výrobc\w*|\d+\s*(rok\w*|mesiac\w*)\s+záruk\w*)`.
- **Skládání:** brána `legal_extra_guarantee ≥ 0,5` kdekoli na webu; pak `site_presence(legal_extra_guarantee_terms)`.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** Slovo „záruka“ se na slovenských webech používá i pro zákonnou odpovědnost za vady („záruka 2 roky“); otázka proto míří jen na záruku nad rámec zákona.

### sk_digital_content_liability
- **Ustanovení:** § 5 ods. 1 písm. h) Z108 (nové od 27. 9. 2026); § 852h ods. 1 a 2 OZ.
- **Citace:** „existenciu a dĺžku trvania zodpovednosti za vady digitálneho obsahu alebo digitálnej služby,“ (Z108 ř. 1411–1412); „Obchodník zodpovedá za akúkoľvek vadu, ktorú má digitálne plnenie v čase jeho dodania a ktorá sa prejaví do dvoch rokov od jeho dodania, ak ide o digitálne plnenie, ktoré sa dodáva jednorazovo …“ (OZ § 852h ods. 1)
- **Požadavek lidsky:** E-shop s e-knihami, softwarem, licencemi nebo online službami musí uvést, že za vady odpovídá a jak dlouho.
- **Kde na webu:** VOP, reklamačné podmienky.
- **Kontrola:** kód (brána + délka trvání).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** brána `(?i)(e-?kniha|e-?book|licenci\w+|stiahnuti\w+|softvér\w*|digitáln\w+\s+(obsah|služb|produkt))`; pak v právních textech `(?i)(digitáln\w+)[^.]{0,120}?(dva\s+roky|2\s*rok\w*|24\s*mesiac\w*|počas\s+(celej\s+)?dohodnutej\s+doby)`.
- **Skládání:** `site_presence` s bránou.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** —

### sk_service_liability
- **Ustanovení:** § 5 ods. 1 písm. i) Z108; § 4 ods. 1 písm. j) Z108.
- **Citace:** „existenciu a dĺžku trvania zodpovednosti za vady služby a postup pri uplatnení práv zo zodpovednosti za vady služby,“ (Z108 ř. 1414–1415)
- **Požadavek lidsky:** Prodává-li e-shop služby (montáž, instalace, servis), musí uvést odpovědnost za vady služby, její délku a postup reklamace.
- **Kde na webu:** VOP.
- **Kontrola:** jev s bránou; zda web služby prodává, určí kód.
- **Otázky pro Jev:** `legal_service_liability` — text_en: "Does this text explain how the customer can claim rights when a service provided by the seller is defective?" — text_cs: "Vysvetľuje tento text, ako môže zákazník uplatniť práva, ak je služba poskytnutá predávajúcim vadná?"
- **Kontrola kódem:** brána `(?i)(montáž\w*|inštaláci\w+|servis\w*|služb\w+)` v nabídce (produktové stránky, košíkové doplňky nelze).
- **Skládání:** `site_presence` s bránou.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Délku odpovědnosti za vady služeb jsem v OZ nedohledával; číslo proto nekontrolovat (NEOVĚŘENO).

## F. Opravitelnost a digitální prvky (produktové stránky)

### sk_repairability_score
- **Ustanovení:** § 5 ods. 1 písm. k) Z108 (nové od 27. 9. 2026); odkaz 23b (Z108 ř. 8076–8080).
- **Citace:** „informáciu o bodovom hodnotení opraviteľnosti tovaru, 23b ) ktorým sa vyjadruje možnosť opravy 23c ) tovaru,“ (Z108 ř. 1420); odkaz 23b: „Napríklad bod 5 prílohy IV, príloha IVa, V a VI delegovaného nariadenia Komisie (EÚ) 2023/1669 …, pokiaľ ide o energetické označovanie smartfónov a tabletov typu Slate“ (Z108 ř. 8077–8079)
- **Požadavek lidsky:** U výrobků, které mají úředně stanovený index nebo třídu opravitelnosti (dnes hlavně chytré telefony a tablety), ji musí e-shop uvést.
- **Kde na webu:** produktová stránka.
- **Kontrola:** jev + kód; třída bývá jen na obrázku energetického štítku (nelze z textu).
- **Otázky pro Jev:** `product_repairability` — text_en: "Does the sentence (field sentence) state a repairability score or repairability class of the product?" — text_cs: "Uvádza veta (pole sentence) index alebo triedu opraviteľnosti produktu?"
- **Kontrola kódem:** brána kategorie `(?i)(smartfón\w*|mobiln\w+\s+telef\w+|tablet\w*)`; signál `(?i)(opraviteľnos\w+|trieda\s+opraviteľnosti|energetick\w+\s+štítok)` v textu nebo alt textu.
- **Skládání:** `page_presence` s bránou; bez signálu „k ověření“.
- **Závažnost (návrh):** medium pro telefony a tablety, jinak nepoužít.
- **Poznámky a nejistoty:** Na které další výrobky se bodové hodnocení vztahuje, se bude měnit s dalšími předpisy EU (odkaz 23b říká „napríklad“).

### sk_spare_parts_repair_info
- **Ustanovení:** § 5 ods. 1 písm. l) Z108 (nové od 27. 9. 2026).
- **Citace:** „informáciu o dostupnosti, predpokladaných nákladoch a postupe objednania náhradných dielov, … ak nebola poskytnutá informácia podľa písmena k), a ak tieto informácie výrobca alebo dovozca sprístupnil obchodníkovi,“ (Z108 ř. 1422–1425)
- **Požadavek lidsky:** Pokud výrobce dal prodejci informace o náhradních dílech a opravách (a výrobek nemá index opravitelnosti), musí je e-shop uvést.
- **Kde na webu:** produktová stránka.
- **Kontrola:** nelze z textu (zda výrobce informace poskytl, je fakt mimo web). Jev jen eviduje.
- **Otázky pro Jev:** `product_spare_parts` — text_en: "Does the sentence (field sentence) give information about the availability, price or ordering of spare parts, or about repair instructions for the product?" — text_cs: "Uvádza veta (pole sentence) informáciu o dostupnosti, cene alebo objednaní náhradných dielov alebo o návode na opravu produktu?"
- **Kontrola kódem:** žádná.
- **Skládání:** bez nálezu, jen evidence pro ruční ověření.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** —

### sk_digital_functionality_compatibility
- **Ustanovení:** § 5 ods. 1 písm. n) a o) Z108.
- **Citace:** „údaje o funkčnosti 24 ) veci s digitálnymi prvkami, 25 ) digitálneho obsahu a digitálnej služby vrátane dostupných technických ochranných opatrení,“ (Z108 ř. 1431–1432); „údaje o kompatibilite a interoperabilite 26 ) veci s digitálnymi prvkami, digitálneho obsahu a digitálnej služby, ktoré sú obchodníkovi známe …“ (Z108 ř. 1434–1435)
- **Požadavek lidsky:** U chytrých zařízení, softwaru a digitálního obsahu uvést funkčnost (včetně technických ochran, např. DRM) a s čím výrobek funguje.
- **Kde na webu:** produktová stránka.
- **Kontrola:** jev pro přítomnost; přiměřenost a úplnost nelze z textu.
- **Otázky pro Jev:** `product_compatibility` — text_en: "Does the sentence (field sentence) state which devices, operating systems or software the product works with?" — text_cs: "Uvádza veta (pole sentence), s akými zariadeniami, operačnými systémami alebo softvérom produkt funguje?"
- **Kontrola kódem:** brána `(?i)(aplikáci\w+|bluetooth|wi-?fi|smart|softvér\w*|android|iOS|windows|licenci\w+|e-?kniha)`.
- **Skládání:** `page_presence` s bránou, nález nejvýš „k ověření“.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** —

### sk_software_update_period
- **Ustanovení:** § 5 ods. 1 písm. p) Z108 (nové od 27. 9. 2026).
- **Citace:** „minimálnu dobu, počas ktorej výrobca, dovozca alebo poskytovateľ digitálneho obsahu alebo digitálnej služby poskytuje bezplatné aktualizácie vrátane bezpečnostných aktualizácií, … ak výrobca, dovozca alebo poskytovateľ digitálneho obsahu alebo digitálnej služby túto informáciu sprístupnil obchodníkovi,“ (Z108 ř. 1437–1441)
- **Požadavek lidsky:** Pokud výrobce sdělil, jak dlouho bude dodávat bezplatné (bezpečnostní) aktualizace, musí to e-shop uvést.
- **Kde na webu:** produktová stránka.
- **Kontrola:** nelze z textu (zda výrobce údaj poskytl); Jev a kód jen evidují a vytáhnou dobu.
- **Otázky pro Jev:** `product_update_period` — text_en: "Does the sentence (field sentence) state for how long software or security updates will be provided for the product?" — text_cs: "Uvádza veta (pole sentence), ako dlho sa budú pre produkt poskytovať aktualizácie softvéru alebo bezpečnostné aktualizácie?"
- **Kontrola kódem:** doba `(?i)(\d{1,2})\s*(rok\w*|mesiac\w*)|do\s+\d{1,2}\.\s?\d{1,2}\.\s?\d{4}|do\s+roku\s+\d{4}`.
- **Skládání:** bez nálezu, evidence; u telefonů a tabletů (brána jako `sk_repairability_score`) bez údaje „k ověření“.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** Příloha č. 1 obsahuje nové body o aktualizacích (33, 34 podle N310 bod 53); řeší jiná rešerše.

### sk_manufacturer_repair_info
- **Ustanovení:** § 13b ods. 7 Z108 (účinné od 31. 7. 2026, N310 čl. I bod 21).
- **Citace:** „Výrobca a osoba podľa odseku 3 počas trvania povinnosti vykonať opravu … a) bezplatne, jasným a zrozumiteľným spôsobom sprístupní informácie o opravárenských službách, b) zverejní na svojom webovom sídle informatívnu cenu za bežnú opravu tovaru, na ktorý sa vzťahuje požiadavka na opraviteľnosť.“ (Z108 ř. 1896–1903)
- **Požadavek lidsky:** Jen pro výrobce (nebo jeho zástupce, dovozce, distributora podle § 13b ods. 3) vybraných výrobků s požadavky na opravitelnost: na webu informace o opravách a orientační cena běžné opravy.
- **Kde na webu:** stránka servisu nebo oprav.
- **Kontrola:** nelze z textu (zda je e-shop výrobcem a výrobek spadá pod přílohu II směrnice 2024/1799). Jev jen eviduje.
- **Otázky pro Jev:** `legal_repair_services_info` — text_en: "Does this text give an indicative price for a typical repair of a product carried out by or on behalf of the manufacturer?" — text_cs: "Uvádza tento text orientačnú cenu bežnej opravy produktu, ktorú vykonáva výrobca alebo osoba v jeho mene?"
- **Kontrola kódem:** žádná.
- **Skládání:** bez nálezu; ruční ověření u vlastních značek.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Evropský formulář o opravě (§ 13a, príloha č. 3a) je pro opravovny dobrovolný („môže“), proto ho neuvádím jako povinnost.

## G. Smlouvy na dobu, předplatné, záloha, personalizace

### sk_contract_duration_termination
- **Ustanovení:** § 5 ods. 1 písm. m) Z108; § 11 ods. 4 písm. e) Z108.
- **Citace:** „dĺžku trvania zmluvy, ak ide o zmluvu uzavretú na určitý čas, alebo podmienky vypovedania zmluvy, ak ide o zmluvu uzavretú na neurčitý čas alebo o zmluvu, ktorej platnosť sa predlžuje automaticky,“ (Z108 ř. 1427–1429)
- **Požadavek lidsky:** U předplatného a automaticky obnovovaných smluv uvést dobu trvání nebo jak smlouvu vypovědět.
- **Kde na webu:** VOP, stránka předplatného.
- **Kontrola:** jev s bránou.
- **Otázky pro Jev:**
  - `legal_subscription` — text_en: "Does this text describe a subscription, a contract with repeated deliveries or a contract that is renewed automatically?" — text_cs: "Opisuje tento text predplatné, zmluvu s opakovanými dodávkami alebo zmluvu, ktorá sa automaticky predlžuje?"
  - `legal_termination_how` — text_en: "Does this text explain how the customer can terminate a subscription or a long-term contract (not only how to cancel a single order)?" — text_cs: "Vysvetľuje tento text, ako môže zákazník vypovedať predplatné alebo dlhodobú zmluvu (nie len ako zrušiť jednu objednávku)?"
- **Kontrola kódem:** brána `(?i)(predplat\w+|pravidel\w+\s+(dodávk|zásielk)\w*|automatick\w+\s+(obnov|predĺž)\w*|abonent\w*|členstv\w+)`.
- **Skládání:** brána `legal_subscription ≥ 0,5` kdekoli; pak `site_presence(legal_termination_how)`; délku trvání smlouvy hledá kód `(?i)na\s+(dobu\s+)?(\d{1,2})\s*(mesiac\w*|rok\w*)`.
- **Závažnost (návrh):** low až medium (u předplatného medium).
- **Poznámky a nejistoty:** —

### sk_min_commitment_duration
- **Ustanovení:** § 15 ods. 1 písm. j) Z108; souhrn před objednávkou § 17 ods. 3 Z108.
- **Citace:** „minimálnu dĺžku trvania záväzku spotrebiteľa, ak zo zmluvy vyplýva pre spotrebiteľa taký záväzok,“ (Z108 ř. 2066–2067)
- **Požadavek lidsky:** Pokud se zákazník zavazuje na minimální dobu (např. předplatné na 12 měsíců), musí to web uvést.
- **Kde na webu:** VOP, stránka předplatného; souhrn před odesláním objednávky je v košíku.
- **Kontrola:** jev + kód (brána `legal_subscription`, dobu najde kód).
- **Otázky pro Jev:** používá `legal_subscription` (výše).
- **Kontrola kódem:** `(?i)(minimáln\w+|najmenej|viazanos\w+)[^.]{0,40}?(\d{1,2})\s*(mesiac\w*|rok\w*|týžd\w+)`.
- **Skládání:** brána `legal_subscription ≥ 0,5` a slova o viazanosti `(?i)(viazanos\w+|minimáln\w+\s+dob\w+)` bez čísla → „k ověření“.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Zda závazek na minimální dobu vůbec existuje, bez textu nepoznáme.

### sk_deposit_conditions
- **Ustanovení:** § 15 ods. 1 písm. k) Z108.
- **Citace:** „poučenie o povinnosti spotrebiteľa zaplatiť preddavok alebo poskytnúť inú finančnú zábezpeku na žiadosť obchodníka a o podmienkach ich poskytnutia, ak zo zmluvy vyplýva pre spotrebiteľa taká povinnosť,“ (Z108 ř. 2069–2071)
- **Požadavek lidsky:** Když prodejce může chtít zálohu nebo jistotu, musí to i podmínky uvést předem.
- **Kde na webu:** VOP.
- **Kontrola:** jev (přítomnost); zda smlouva zálohu vyžaduje, nelze bez textu.
- **Otázky pro Jev:** `legal_deposit` — text_en: "Does this text say that the customer must pay a deposit or an advance payment, or provide another financial guarantee, at the seller's request?" — text_cs: "Uvádza tento text, že zákazník musí na žiadosť predávajúceho zaplatiť preddavok (zálohu) alebo poskytnúť inú finančnú zábezpeku?"
- **Kontrola kódem:** výše zálohy `(?i)(preddav\w*|záloh\w*|kaucia|kaucie)[^.]{0,60}?(\d+\s?%|\d+(?:,\d{2})?\s?(€|eur))`.
- **Skládání:** evidence (`segment` bez nálezu); ruční kontrola, zda jsou uvedeny podmínky.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Obvyklé u zakázkové výroby a nábytku.

### sk_personalised_price_notice
- **Ustanovení:** § 15 ods. 1 písm. d) Z108.
- **Citace:** „informáciu, že predajná cena je pre konkrétneho spotrebiteľa alebo pre skupinu spotrebiteľov určená na základe automatizovaného rozhodovania vrátane profilovania, 63 )“ (Z108 ř. 2043–2044)
- **Požadavek lidsky:** Pokud prodejce personalizuje ceny algoritmem, musí to zákazníkovi říct.
- **Kde na webu:** VOP, u ceny.
- **Kontrola:** nelze z textu (zda se ceny personalizují, z jednoho průchodu nezjistíme; šlo by jen porovnáním cen z více profilů).
- **Otázky pro Jev:** `legal_personalised_price` — text_en: "Does this text say that prices may be personalised for a customer or a group of customers on the basis of automated decision-making or profiling?" — text_cs: "Uvádza tento text, že ceny môžu byť pre zákazníka alebo skupinu zákazníkov určené na základe automatizovaného rozhodovania alebo profilovania?"
- **Kontrola kódem:** žádná.
- **Skládání:** bez nálezu, evidence.
- **Závažnost (návrh):** low (vysoká, pokud personalizace probíhá, ale to nástroj nezjistí).
- **Poznámky a nejistoty:** —

## H. Odstoupení od smlouvy

### sk_withdrawal_info
- **Ustanovení:** § 15 ods. 1 písm. f) až i) a § 19 Z108. **Existující kontrola:** `legal_withdrawal_missing` (otázka `legal_withdrawal` + regex 14 dní), jen mapuji.
- **Citace:** „poučenie o práve spotrebiteľa odstúpiť od zmluvy podľa § 19 ods. 1 , podmienkach, lehote a postupe pri uplatňovaní práva na odstúpenie od zmluvy; …“ (Z108 ř. 2049); „Spotrebiteľ môže odstúpiť od zmluvy uzavretej na diaľku … do a) 14 dní odo dňa 1. prevzatia tovaru spotrebiteľom podľa odseku 4,“ (Z108 ř. 2399–2404); následek: „… spotrebiteľ môže odstúpiť od zmluvy … do 12 mesiacov od uplynutia lehoty podľa odseku 1.“ (Z108 ř. 2428–2430)
- **Požadavek lidsky:** Poučení o právu odstoupit do 14 dní bez udání důvodu, o podmínkách a postupu.
- **Kde na webu:** VOP, stránka odstoupení nebo vrácení.
- **Kontrola:** jev + kód (existující).
- **Otázky pro Jev:** existující `legal_withdrawal`.
- **Kontrola kódem:** existující `(?i)(14|štrnásť\w*)\s*-?\s*(dn|dň|den)`.
- **Skládání:** existující `site_presence`.
- **Závažnost (návrh):** high (beze změny; následek prodloužení lhůty až o 12 měsíců).
- **Poznámky a nejistoty:** Regex 14 dní najde i „14 dní“ z vrácení peněz (§ 22 ods. 1), proto je dobré ho vázat na odstavec s kladnou `legal_withdrawal`. Dílčí části písm. f)–i) jsou rozepsané v dalších položkách. Zákonné vzorové poučenie je v P3; jeho řádné vyplnění splňuje písm. f)–h) (§ 15 ods. 6).

### sk_withdrawal_form
- **Ustanovení:** § 15 ods. 1 písm. f) Z108; § 20 ods. 6 Z108; príloha č. 2. **Existující kontrola:** `legal_withdrawal_form_missing` (otázka `legal_withdrawal_form`), jen mapuji.
- **Citace:** „… obchodník zároveň poskytne spotrebiteľovi vzorový formulár na odstúpenie od zmluvy podľa prílohy č. 2 …“ (Z108 ř. 2050); „– Týmto oznamujem/oznamujeme*, že odstupujem/odstupujeme* od zmluvy o dodaní alebo poskytnutí tohto produktu: ..............“ (P2 ř. 11–12)
- **Požadavek lidsky:** Spolu s poučením musí být vzorový formulář podle přílohy č. 2.
- **Kde na webu:** stránka odstoupení, VOP, odkaz na PDF nebo DOC.
- **Kontrola:** jev (existující); návrh doplňkového kódu.
- **Otázky pro Jev:** existující `legal_withdrawal_form`.
- **Kontrola kódem (návrh doplňku):** charakteristické věty přílohy č. 2 `(?i)(týmto\s+oznamujem\s*/\s*oznamujeme|dátum\s+objednania\s*/\s*dátum\s+prijatia|nehodiace\s+sa\s+prečiarknite)`; odkaz na soubor `(?i)(formul[áa]r|odstupeni|odstupenie).*\.(pdf|docx?)`. Přítomnost odkazu na PDF snižuje pásmo na „k ověření“ (PDF se nečte).
- **Skládání:** existující `site_presence`, doplněk jako `any` s kódem.
- **Závažnost (návrh):** medium (beze změny).
- **Poznámky a nejistoty:** Formulář má obsahovat obchodní jméno, sídlo a e-mail prodejce v kolonce „Komu“ (P2 ř. 9–10); prázdná kolonka je vada, kterou kód najde regexem `(?i)komu\s*\[obchodník\s+doplní`.

### sk_withdrawal_function
- **Ustanovení:** § 20a ods. 1, 2 a 4 Z108. **Existující kandidát** v hlavičce `legal_sk.yaml`, jen upřesňuji.
- **Citace:** „Funkcia na odstúpenie od zmluvy musí byť označená ľahko čitateľným spôsobom slovným spojením „odstúpiť od zmluvy tu“ alebo obdobnou formuláciou, … Funkcia na odstúpenie od zmluvy musí byť v online rozhraní zreteľne zobrazená a musí byť pre spotrebiteľa ľahko a nepretržite dostupná počas plynutia lehoty na odstúpenie od zmluvy …“ (Z108 ř. 2499–2503)
- **Požadavek lidsky:** Na webu musí být viditelná a stále dostupná funkce (tlačítko nebo odkaz) „odstúpiť od zmluvy tu“, která vede k online odstoupení; potvrzovací tlačítko se jmenuje „potvrdiť odstúpenie od zmluvy“ (§ 20a ods. 4).
- **Kde na webu:** rám (patička), stránka odstoupení, zákaznický účet (neprochází se).
- **Kontrola:** kód; potvrzovací tlačítko a celý tok (§ 20a ods. 3–5) nelze z textu.
- **Otázky pro Jev:** žádné (text tlačítka je kód).
- **Kontrola kódem:** text prvků `a`, `button`, `input[value]` (včetně rámu) `(?i)\bodstúpiť\s+od\s+zmluvy\s+tu\b`; obdobné formulace `(?i)(odstúpiť|odstúpenie)\s+od\s+zmluvy\s+(online|teraz|kliknutím)|online\s+odstúpeni\w*\s+od\s+zmluvy`.
- **Skládání:** `site_presence` přes kód (celý web včetně rámu); bez nálezu „k ověření“ (funkce může být za přihlášením).
- **Závažnost (návrh):** high.
- **Poznámky a nejistoty:** Co je „obdobná formulácia“, rozhodne právník; seznam variant je návrh. Zda je funkce dostupná „nepretržite“ po celou lhůtu, nelze z textu.

### sk_withdrawal_function_location_info
- **Ustanovení:** § 15 ods. 1 písm. f) Z108 (část o funkci § 20a); P3 bod 3a.
- **Citace:** „… poučenie o práve spotrebiteľa odstúpiť od zmluvy podľa § 19 ods. 1 zahŕňa aj informáciu o existencii a umiestnení funkcie na odstúpenie od zmluvy podľa § 20a ,“ (Z108 ř. 2052–2053); text bodu 3a pokynov (ve zdroji v uvozovkách): „Právo na odstúpenie od zmluvy môžete uplatniť aj online na adrese ...................... [doplňte adresu webovej stránky alebo iné vhodné vysvetlenie toho, kde sa funkcia na odstúpenie od zmluvy nachádza].“ (P3 ř. 69–71)
- **Požadavek lidsky:** Poučení o odstoupení musí říct, že online funkce existuje a kde ji zákazník najde.
- **Kde na webu:** VOP, stránka odstoupení.
- **Kontrola:** jev + kód.
- **Otázky pro Jev:** `legal_withdrawal_online` — text_en: "Does this text tell the customer where on the website they can withdraw from the contract online, for example with a 'withdraw from contract here' button or an online form?" — text_cs: "Uvádza tento text, kde na webe môže zákazník odstúpiť od zmluvy online, napr. tlačidlom „odstúpiť od zmluvy tu“ alebo online formulárom?"
- **Kontrola kódem:** věta z P3 `(?i)odstúpeni\w*\s+od\s+zmluvy\s+môžete\s+uplatniť\s+aj\s+online`; URL nebo cesta v témže odstavci `https?://\S+|(?i)(v\s+sekcii|v\s+pätičke|v\s+zákazníckom\s+účte)`.
- **Skládání:** `site_presence(legal_withdrawal_online)` nebo kódová věta z P3.
- **Závažnost (návrh):** medium až high (část poučení podle písm. f); chybějící informace podle písm. f) prodlužuje lhůtu, § 20 ods. 2 a 3).
- **Poznámky a nejistoty:** Zda neúplné poučení jen v této části vede k prodloužení lhůty podle § 20 ods. 3, je právní otázka (NEOVĚŘENO).

### sk_withdrawal_return_costs
- **Ustanovení:** § 15 ods. 1 písm. g) Z108; § 15 ods. 7 Z108; § 21 ods. 3 Z108.
- **Citace:** „poučenie o povinnosti spotrebiteľa znášať náklady na vrátenie tovaru po odstúpení od zmluvy podľa § 19 ods. 1 , a ak spotrebiteľ odstúpi od zmluvy uzavretej na diaľku, aj náklady na vrátenie tovaru, ktorý vzhľadom na jeho povahu nie je možné vrátiť prostredníctvom pošty,“ (Z108 ř. 2055–2057); „… znáša spotrebiteľ len náklady na vrátenie tovaru …; to neplatí, ak obchodník súhlasil, že náklady bude znášať sám, alebo ak obchodník nesplnil informačnú povinnosť podľa § 15 ods. 1 písm. g) .“ (Z108 ř. 2545–2547)
- **Požadavek lidsky:** Web musí říct, kdo platí vrácení zboží; u zboží, které nejde poslat poštou, i odhad ceny. Bez této informace platí vrácení prodejce.
- **Kde na webu:** VOP, stránka odstoupení nebo vrácení.
- **Kontrola:** jev + kód.
- **Otázky pro Jev:** `legal_withdrawal_return_cost` — text_en: "Does this text state who bears the cost of returning the goods when the customer withdraws from the contract?" — text_cs: "Uvádza tento text, kto znáša náklady na vrátenie tovaru, keď zákazník odstúpi od zmluvy?"
- **Kontrola kódem:** věty z P3 `(?i)(náklady\s+na\s+vrátenie\s+tovaru\s+znášame\s+my|priame\s+náklady\s+na\s+vrátenie\s+tovaru\s+znášate\s+vy)`; obecně `(?i)(náklad\w+\s+(na|spojen\w+\s+s)\s+vráten\w+\s+tovar\w*|vrátenie\s+tovaru\s+(je\s+)?(zdarma|bezplatn\w+))`; u nadrozměrného zboží (brána `(?i)(nábyt\w+|chladničk\w+|práčk\w+|paleta|nadrozmern\w+)`) částka `\d+(?:,\d{2})?\s?(€|eur)`.
- **Skládání:** `site_presence(legal_withdrawal_return_cost)` nebo kód; nadrozměrné zboží jako doplňkový nález „k ověření“.
- **Závažnost (návrh):** high (následek § 15 ods. 7 a § 21 ods. 3).
- **Poznámky a nejistoty:** —

### sk_withdrawal_service_payment
- **Ustanovení:** § 15 ods. 1 písm. h) Z108; § 17 ods. 10 Z108.
- **Citace:** „poučenie o povinnosti spotrebiteľa uhradiť obchodníkovi cenu za skutočne poskytnuté plnenie podľa § 21 ods. 5 , ak spotrebiteľ odstúpi od zmluvy podľa § 19 ods. 1 , ktorej predmetom je poskytnutie služby, po udelení výslovného súhlasu obchodníkovi podľa § 17 ods. 10 písm. c) ,“ (Z108 ř. 2059–2061)
- **Požadavek lidsky:** Prodává-li e-shop služby, musí poučit, že při odstoupení po zahájení služby na žádost zákazníka se platí poměrná část.
- **Kde na webu:** VOP.
- **Kontrola:** jev s bránou (služby).
- **Otázky pro Jev:** `legal_withdrawal_service_payment` — text_en: "Does this text say that a customer who asked for a service to start during the withdrawal period must pay for the part of the service already provided if they withdraw?" — text_cs: "Uvádza tento text, že zákazník, ktorý požiadal o začatie poskytovania služby počas lehoty na odstúpenie, musí pri odstúpení zaplatiť za už poskytnuté plnenie?"
- **Kontrola kódem:** brána jako `sk_service_liability`.
- **Skládání:** `site_presence` s bránou.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Samotný souhlas a poučení podle § 17 ods. 10 probíhají v objednávce (viz `sk_service_digital_start_consent`).

### sk_withdrawal_exceptions
- **Ustanovení:** § 15 ods. 1 písm. i) Z108; § 19 ods. 1 písm. a) až m) Z108.
- **Citace:** „poučenie o tom, že spotrebiteľ nie je oprávnený odstúpiť od zmluvy podľa § 19 ods. 1 , alebo poučenie o okolnostiach, za ktorých spotrebiteľ stráca právo na odstúpenie od zmluvy,“ (Z108 ř. 2063–2064); příklad výjimky: „e) dodanie tovaru uzavretého v ochrannom obale, ktorý nie je vhodné vrátiť z dôvodu ochrany zdravia alebo z hygienických dôvodov, ak ochranný obal bol po dodaní porušený,“ (Z108 ř. 2348–2350)
- **Požadavek lidsky:** Pokud se na nabízené zboží vztahuje výjimka (hygiena, zboží na míru, rychle se kazící, zapečetěný software…), musí o ní web poučit.
- **Kde na webu:** VOP, stránka odstoupení, případně produktová stránka.
- **Kontrola:** jev pro přítomnost; zda se výjimky na sortiment vztahují, nelze z textu určit spolehlivě (kód jen odhadne podle kategorií).
- **Otázky pro Jev:** `legal_withdrawal_exceptions` — text_en: "Does this text list cases in which the customer cannot withdraw from the contract or loses the right to withdraw (for example unsealed hygiene products or goods made to the customer's specifications)?" — text_cs: "Uvádza tento text prípady, v ktorých zákazník nemôže odstúpiť od zmluvy alebo stráca právo na odstúpenie (napr. rozbalený hygienický tovar alebo tovar vyrobený podľa požiadaviek zákazníka)?"
- **Kontrola kódem:** brána sortimentu `(?i)(kozmetik\w*|spodn\w+\s+bielizeň|plavk\w*|potravin\w*|na\s+mieru|personaliz\w+|gravír\w*|softvér\w*|hry)`.
- **Skládání:** brána kódu, pak `site_presence(legal_withdrawal_exceptions)`; nález nejvýš „k ověření“.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Naopak výluky, které zákon nezná, řeší `sk_withdrawal_restrictive_terms`.

### sk_withdrawal_template_unfilled
- **Ustanovení:** § 15 ods. 6 Z108; príloha č. 3.
- **Citace:** „Informačná povinnosť podľa odseku 1 písm. f) až h) sa považuje za splnenú, ak obchodník poskytne spotrebiteľovi riadne vyplnené poučenie o uplatnení práva na odstúpenie od zmluvy podľa prílohy č. 3 .“ (Z108 ř. 2099–2101); „Máte právo odstúpiť od tejto zmluvy bez uvedenia dôvodu v lehote ..... dní (doplňte podľa bodu 1 Pokynov na vyplnenie)“ (P3 ř. 8–9)
- **Požadavek lidsky:** Kdo použije vzorové poučení, musí ho vyplnit. Ponechané tečky, „(doplňte…)“ nebo „Pokyny na vyplnenie“ znamenají, že poučení není řádné.
- **Kde na webu:** VOP, stránka odstoupení.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `\.{4,}\s*(dní|dňoch|eur)|(?i)\(doplňte|\[doplňte|pokyny\s+na\s+vyplnenie|podľa\s+bodu\s+\d+[a-z]?\s+pokynov`.
- **Skládání:** `segment` nad odstavci právních stránek (nález „vysoká jistota“).
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Tečky jako výplň ve formuláři podle přílohy č. 2 jsou v pořádku; regex proto hledá tečky jen před „dní“, „dňoch“ nebo „eur“.

### sk_withdrawal_refund_deadline
- **Ustanovení:** § 22 ods. 1 Z108; P3 bod 2. Typ „správnost obsahu“.
- **Citace:** „Obchodník je povinný do 14 dní odo dňa doručenia oznámenia o odstúpení od zmluvy vrátiť spotrebiteľovi všetky platby, ktoré od neho prijal … vrátane nákladov na dopravu, dodanie, poštovné a iných nákladov a poplatkov.“ (Z108 ř. 2567–2571)
- **Požadavek lidsky:** Peníze včetně původního poštovného se vracejí do 14 dní od doručení odstoupení (prodejce smí počkat na vrácení zboží, § 22 ods. 5).
- **Kde na webu:** VOP, stránka odstoupení.
- **Kontrola:** kód.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `(?i)(vrát\w*)\s+(\S+\s+){0,6}?(platb\w*|peniaz\w*|peňaz\w*|kúpn\w+\s+cen\w*|sum\w*)[^.]{0,80}?do\s+(\d{1,3})\s*(pracovn\w+\s+)?(dn\w*|dní)`; nález při > 14 dnech nebo pracovních dnech; nález při větě, že se nevrací poštovné `(?i)(poštovné|doprav\w+)[^.]{0,40}(nevracia|nevraciame|sa\s+nevráti)`.
- **Skládání:** `segment`.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Nevrácení dodatečných nákladů za dražší zvolenou dopravu je dovolené (§ 22 ods. 3); věta o poštovném proto jen „k ověření“.

### sk_withdrawal_restrictive_terms
- **Ustanovení:** § 19 ods. 1 Z108 („aj bez uvedenia dôvodu“); § 20 ods. 6 Z108; § 21 ods. 4 a 6 Z108; § 4 ods. 2 písm. c) Z108. Typ „správnost obsahu“.
- **Citace:** „Spotrebiteľ má právo odstúpiť od zmluvy uzavretej na diaľku … aj bez uvedenia dôvodu v lehote podľa § 20 ods. 1 až 3 …“ (Z108 ř. 2328–2329); „Spotrebiteľovi z uplatnenia práva na odstúpenie od zmluvy … nevznikajú okrem povinností podľa odsekov 1, 3 až 5 a povinnosti uhradiť dodatočné náklady podľa § 22 ods. 3 žiadne ďalšie povinnosti alebo náklady.“ (Z108 ř. 2561–2563)
- **Požadavek lidsky:** VOP nesmí odstoupení podmiňovat důvodem, originálním obalem, vyloučit slevové zboží (zákon takovou výjimku nezná) ani účtovat storno poplatek. Za snížení hodnoty zboží zákazník odpovídá jen při nadměrném zacházení (§ 21 ods. 4).
- **Kde na webu:** VOP, stránka odstoupení nebo vrácení.
- **Kontrola:** jev.
- **Otázky pro Jev:**
  - `legal_withdrawal_sale_excluded` — text_en: "Does this text say that discounted, sale or clearance goods cannot be returned or are excluded from withdrawal from the contract?" — text_cs: "Uvádza tento text, že zľavnený alebo výpredajový tovar nemožno vrátiť alebo je vylúčený z odstúpenia od zmluvy?"
  - `legal_withdrawal_reason_required` — text_en: "Does this text say that the customer must give a reason when withdrawing from the contract? An optional field for a reason does not count." — text_cs: "Uvádza tento text, že zákazník musí pri odstúpení od zmluvy uviesť dôvod? Nepovinné pole na dôvod sa nepočíta."
  - `legal_withdrawal_packaging_condition` — text_en: "Does this text make withdrawal or a refund conditional on returning the goods in their original or undamaged packaging? A mere recommendation to use the original packaging does not count." — text_cs: "Podmieňuje tento text odstúpenie od zmluvy alebo vrátenie peňazí vrátením tovaru v originálnom alebo nepoškodenom obale? Samotné odporúčanie použiť originálny obal sa nepočíta."
  - `legal_withdrawal_fee` — text_en: "Does this text say that the seller charges a fee when the customer withdraws from the contract, other than the cost of sending the goods back (for example a cancellation or handling fee)?" — text_cs: "Uvádza tento text, že predávajúci si pri odstúpení od zmluvy účtuje poplatok iný než náklady na zaslanie tovaru späť (napr. stornovací alebo manipulačný poplatok)?"
- **Kontrola kódem:** výše poplatku `(?i)(storn\w*|manipulačn\w+)\s+poplat\w*[^.]{0,40}?(\d+\s?%|\d+(?:,\d{2})?\s?(€|eur))`; věta „nepreberáme zásielky na dobierku“ `(?i)(dobierk\w+)[^.]{0,40}(nepreber\w+|neprijím\w+)` jako „k ověření“.
- **Skládání:** `segment`: `any` přes čtyři otázky (každá vlastní nález a vysvětlení).
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Výluka slevového zboží je častá chyba; výluky podle § 19 ods. 1 (hygiena, na míru…) jsou v pořádku a otázka `legal_withdrawal_sale_excluded` se na ně neptá.

## I. Alternativní řešení sporů

### sk_adr
- **Ustanovení:** § 5 ods. 1 písm. q) a ods. 3 Z108; § 3 ods. 2 písm. c) Z391. **Existující kontrola:** `legal_adr_missing` (otázka `legal_adr`), jen mapuji.
- **Citace:** „poučenie o práve spotrebiteľa podať obchodníkovi žiadosť o nápravu podľa osobitného predpisu 27 ) s uvedením odkazu na webové sídlo, na ktorom sú zverejnené informácie o príslušnom subjekte alternatívneho riešenia sporov. 28 )“ (Z108 ř. 1443–1445); „Informácie podľa odseku 1 písm. q) obchodník zverejní najmä vo všeobecných obchodných podmienkach a na svojom webovom sídle, ak ho má zriadené.“ (Z108 ř. 1454–1455)
- **Požadavek lidsky:** Poučení o žádosti o nápravu a odkaz na web příslušného subjektu ARS (u běžného e-shopu SOI podle § 3 ods. 2 písm. c) Z391).
- **Kde na webu:** VOP, reklamačné podmienky.
- **Kontrola:** jev (existující).
- **Otázky pro Jev:** existující `legal_adr`.
- **Kontrola kódem (návrh doplňku):** odkaz na web subjektu ARS `(?i)(soi\.sk|alternatívn\w+\s+riešen\w+\s+(spotrebiteľsk\w+\s+)?spor\w*)`.
- **Skládání:** existující `site_presence`.
- **Závažnost (návrh):** high (beze změny).
- **Poznámky a nejistoty:** Existující otázka pokrývá subjekt ARS a odkaz, ne poučení o žádosti o nápravu (viz další položka). Doložka, která zákazníka váže na jeden subjekt ARS, se nebere v úvahu (§ 12 ods. 2 Z391: „Na ustanovenia zmluvy, ktoré zaväzujú spotrebiteľa podať návrh na vopred určený subjekt alternatívneho riešenia sporov, sa neprihliada.“, Z391 ř. 862–864). Odkaz na platformu RSO už Z108 nevyžaduje (viz úvod).

### sk_adr_redress_request
- **Ustanovení:** § 5 ods. 1 písm. q) Z108 (první část); § 11 ods. 1 Z391.
- **Citace:** „poučenie o práve spotrebiteľa podať obchodníkovi žiadosť o nápravu …“ (Z108 ř. 1443); „Spotrebiteľ má právo podať obchodníkovi žiadosť o nápravu, ak medzi spotrebiteľom a obchodníkom vznikne spor z uplatnenia práv zo zodpovednosti za vady alebo ak sa spotrebiteľ domnieva, že obchodník porušil iné práva spotrebiteľa.“ (Z391 ř. 845–847)
- **Požadavek lidsky:** VOP musí zákazníka poučit, že se může nejdřív obrátit na prodejce se žádostí o nápravu (a když prodejce odmítne nebo do 30 dní neodpoví, může jít k subjektu ARS, § 11 ods. 3 Z391).
- **Kde na webu:** VOP, reklamačné podmienky.
- **Kontrola:** jev + kód.
- **Otázky pro Jev:** `legal_redress_request` — text_en: "Does this text tell the customer that they can send the seller a request for redress if they are not satisfied with how a complaint was handled or believe their rights were violated?" — text_cs: "Informuje tento text zákazníka, že môže predávajúcemu podať žiadosť o nápravu, ak nie je spokojný so spôsobom vybavenia reklamácie alebo sa domnieva, že predávajúci porušil jeho práva?"
- **Kontrola kódem:** `(?i)žiados\w+\s+o\s+nápravu`.
- **Skládání:** `site_presence`: `any: [legal_redress_request ≥ 0,7, regex]`.
- **Závažnost (návrh):** medium až high (součást téže povinnosti jako ARS).
- **Poznámky a nejistoty:** Informace o subjektech ARS po zamítnutí žádosti se dává na trvalém nosiči (§ 11 ods. 2 Z391), mimo web.

## J. Elektronický obchod (zákon č. 22/2004 Z. z., § 5)

### sk_ecommerce_order_steps
- **Ustanovení:** § 5 ods. 3 písm. b) bod 1 Z22.
- **Citace:** „príjemcu služieb pred odoslaním jeho objednávky jednoznačne a zrozumiteľne informovať o 1. úkonoch potrebných na uzatvorenie zmluvy,“ (Z22 ř. 364–367)
- **Požadavek lidsky:** VOP mají popsat, jak se objednává a kdy vzniká smlouva.
- **Kde na webu:** VOP, stránka „Ako nakupovať“.
- **Kontrola:** jev.
- **Otázky pro Jev:** `legal_order_steps` — text_en: "Does this text describe the steps by which the customer places an order and concludes the contract?" — text_cs: "Opisuje tento text kroky, ktorými zákazník vytvorí objednávku a uzavrie zmluvu?"
- **Kontrola kódem:** žádná.
- **Skládání:** `site_presence`.
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** —

### sk_ecommerce_error_correction
- **Ustanovení:** § 5 ods. 3 písm. a) a písm. b) bod 2 Z22.
- **Citace:** „príjemcovi služieb vytvoriť také podmienky, ktoré umožnia zistiť a opraviť chyby jeho úkonov na elektronickom zariadení pred odoslaním objednávky,“ (Z22 ř. 360–362); „2. technických prostriedkoch na zistenie a opravu chýb,“ (Z22 ř. 368–369)
- **Požadavek lidsky:** Zákazník musí mít možnost před odesláním opravit chyby v objednávce a VOP mají říct jak.
- **Kde na webu:** VOP; samotná možnost opravy je v košíku.
- **Kontrola:** jev pro informaci; funkční možnost opravy nelze z textu.
- **Otázky pro Jev:** `legal_order_error_correction` — text_en: "Does this text explain how the customer can check and correct mistakes in the order before sending it?" — text_cs: "Vysvetľuje tento text, ako môže zákazník pred odoslaním objednávky skontrolovať a opraviť chyby v objednávke?"
- **Kontrola kódem:** žádná.
- **Skládání:** `site_presence`.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** —

### sk_ecommerce_contract_storage
- **Ustanovení:** § 5 ods. 3 písm. b) bod 3 Z22.
- **Citace:** „3. tom, či zmluva bude uložená u poskytovateľa služieb a či je príjemcovi služieb dostupná,“ (Z22 ř. 370–371)
- **Požadavek lidsky:** VOP mají říct, zda prodejce smlouvu (objednávku) archivuje a zda k ní má zákazník přístup.
- **Kde na webu:** VOP.
- **Kontrola:** jev.
- **Otázky pro Jev:** `legal_contract_storage` — text_en: "Does this text say whether the seller stores the concluded contract and whether the customer can access it?" — text_cs: "Uvádza tento text, či predávajúci uzavretú zmluvu uchováva a či je zákazníkovi prístupná?"
- **Kontrola kódem:** žádná.
- **Skládání:** `site_presence`.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** —

### sk_ecommerce_contract_language
- **Ustanovení:** § 5 ods. 3 písm. b) bod 4 a ods. 4 Z22.
- **Citace:** „4. jazyku ponúkanom na uzatvorenie zmluvy.“ (Z22 ř. 372–373); „Informácie uvedené v odseku 3 písm. b) musia byť v štátnom jazyku.15)“ (Z22 ř. 374–375)
- **Požadavek lidsky:** VOP mají uvést jazyk, ve kterém se smlouva uzavírá; informace podle § 5 ods. 3 písm. b) musí být slovensky.
- **Kde na webu:** VOP.
- **Kontrola:** jev + kód (jazyk textu viz `sk_language_slovak`).
- **Otázky pro Jev:** `legal_contract_language` — text_en: "Does this text state the language or languages in which the contract can be concluded?" — text_cs: "Uvádza tento text jazyk alebo jazyky, v ktorých možno zmluvu uzavrieť?"
- **Kontrola kódem:** `(?i)(slovensk\w+\s+jazyk\w*|v\s+jazyku\s+\w+)` v témže odstavci.
- **Skládání:** `site_presence`.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** —

### sk_terms_reproducible
- **Ustanovení:** § 5 ods. 5 písm. a) Z22.
- **Citace:** „Poskytovateľ služieb je povinný príjemcu služieb informovať o a) zmluvných lehotách a zmluvných podmienkach tak, aby si príjemca služieb mohol podstatné náležitosti zmluvy v elektronickej podobe reprodukovať,“ (Z22 ř. 377–380)
- **Požadavek lidsky:** VOP musí být dostupné tak, aby si je zákazník mohl uložit nebo vytisknout.
- **Kde na webu:** VOP.
- **Kontrola:** kód (existence stránky nebo souboru VOP).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** stránka typu VOP podle URL a titulku (`obchodne-podmienky`, `vop`) nebo odkaz na PDF; jinak nález. Doplňuje vestavěné pravidlo „Nenalezeny právní stránky“.
- **Skládání:** `site_presence`.
- **Závažnost (návrh):** low (jako samostatný nález), fakticky pokryto vestavěným pravidlem.
- **Poznámky a nejistoty:** VOP vložené jen jako obrázek nebo v iframe by kód nenašel.

### sk_order_confirmation
- **Ustanovení:** § 5 ods. 6 Z22; § 17 ods. 12 Z108.
- **Citace:** „Poskytovateľ služieb je povinný elektronicky potvrdiť objednávku bezodkladne po jej doručení.“ (Z22 ř. 384–385); „Obchodník je povinný najneskôr pri dodaní produktu alebo pri začatí poskytovania služby doručiť spotrebiteľovi potvrdenie o uzavretí zmluvy na diaľku na trvanlivom médiu.“ (Z108 ř. 2213–2215)
- **Požadavek lidsky:** Potvrzení objednávky e-mailem hned po jejím přijetí a potvrzení smlouvy na trvalém nosiči nejpozději s dodáním.
- **Kde na webu:** e-mail po objednávce (mimo web).
- **Kontrola:** nelze z textu (děje se po objednávce).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** žádná.
- **Skládání:** mimo rozsah; jen zmínka ve zprávě jako ruční kontrola.
- **Závažnost (návrh):** low (z hlediska nástroje).
- **Poznámky a nejistoty:** —

## K. Objednávkový proces (košík a pokladna, nástroj je neprochází)

### sk_order_button_label
- **Ustanovení:** § 17 ods. 4 a 6 Z108.
- **Citace:** „… tlačidlo alebo funkcia musia byť označené ľahko čitateľným spôsobom slovným spojením „objednávka s povinnosťou platby“ alebo obdobnou formuláciou, ktorá jednoznačne vyjadruje, že odoslanie objednávky zahŕňa povinnosť spotrebiteľa zaplatiť cenu.“ (Z108 ř. 2161–2164); „Spotrebiteľovi nevzniknú zo zmluvy alebo v súvislosti s odoslaním objednávky žiadne záväzky, ak obchodník alebo prevádzkovateľ online trhu porušili povinnosť podľa odseku 4.“ (Z108 ř. 2170–2172)
- **Požadavek lidsky:** Tlačítko odeslání objednávky musí říkat, že objednávka zavazuje k platbě („objednávka s povinnosťou platby“ nebo obdobně).
- **Kde na webu:** pokladna (poslední krok).
- **Kontrola:** nelze z textu (pokladnu nástroj neprochází). Slabý signál: VOP někdy tlačítko jmenují.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** ve VOP citovaný popisek tlačítka `(?i)(tlačidl\w+|kliknut\w+\s+na)\s*[„"']([^"“”']{3,40})["“”']`; když popisek neobsahuje `(?i)(povinnosť\w*\s+platby|zaplatiť|s\s+povinnosťou\s+platby|objednať\s+a\s+zaplatiť)` → „k ověření“.
- **Skládání:** `segment` jen se slabým signálem; jinak ruční kontrola pokladny.
- **Závažnost (návrh):** high z hlediska práva (následek § 17 ods. 6), ale nástrojem nekontrolovatelné.
- **Poznámky a nejistoty:** Pro skutečnou kontrolu je potřeba headless průchod pokladnou (mimo rozsah zadání).

### sk_pre_order_summary
- **Ustanovení:** § 17 ods. 3 Z108 (od 27. 9. 2026 včetně písm. g), N310 bod 26).
- **Citace:** „Obchodník je povinný pri zmluve uzavretej na diaľku prostredníctvom elektronických prostriedkov bezprostredne pred odoslaním objednávky spotrebiteľom výslovne, jednoznačne a zrozumiteľne uviesť informácie podľa § 5 ods. 1 písm. a) , d) , g) a i) a § 15 ods. 1 písm. j) , ak sa spotrebiteľ podľa zmluvy zaväzuje zaplatiť cenu.“ (Z108 ř. 2154–2156)
- **Požadavek lidsky:** Těsně před odesláním objednávky shrnout hlavní vlastnosti, celkovou cenu s dopravou, případnou záruku životnosti (GARAN), odpovědnost za vady služby a minimální dobu závazku.
- **Kde na webu:** pokladna.
- **Kontrola:** nelze z textu (pokladna).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** žádná.
- **Skládání:** mimo rozsah.
- **Závažnost (návrh):** medium (nekontrolovatelné).
- **Poznámky a nejistoty:** —

### sk_service_digital_start_consent
- **Ustanovení:** § 17 ods. 10 a 12 Z108; § 19 ods. 1 písm. a) a m) Z108.
- **Citace:** „… je obchodník povinný a) osobitne poučiť spotrebiteľa o tom, že udelením súhlasu so začatím … dodávania digitálneho obsahu, ktorý obchodník dodáva inak ako na hmotnom nosiči, pred uplynutím lehoty na odstúpenie od zmluvy stráca právo na odstúpenie od zmluvy, b) vyžiadať vyhlásenie spotrebiteľa, že bol poučený podľa písmena a), a c) vyžiadať od spotrebiteľa výslovný súhlas …“ (Z108 ř. 2193–2206)
- **Požadavek lidsky:** U služeb a digitálního obsahu (stahování) je před zahájením plnění potřeba zvláštní poučení, prohlášení a výslovný souhlas zákazníka.
- **Kde na webu:** pokladna.
- **Kontrola:** nelze z textu (probíhá v objednávce). VOP poučení obsahují jen obecně (pokrývá `sk_withdrawal_exceptions`).
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** žádná.
- **Skládání:** mimo rozsah.
- **Závažnost (návrh):** medium (nekontrolovatelné).
- **Poznámky a nejistoty:** —

## L. Akce, recenze, pořadí, online tržiště

### sk_promo_conditions
- **Ustanovení:** § 4 ods. 5 Z22.
- **Citace:** „Ak je pri komerčnej komunikácii súčasťou ponuky tovaru a služieb osobitná ponuka, napríklad zľava, odmena, dar, spotrebiteľská hra alebo súťaž, musí byť od základnej ponuky pre príjemcu služieb rozlíšiteľná, a podmienky, ktoré musia byť splnené na jej získanie alebo na účasť v nej, musia byť ľahko prístupné, zrozumiteľné a jednoznačné.“ (Z22 ř. 340–343)
- **Požadavek lidsky:** U akcí typu dárek, bonus, soutěž, kupón nebo podmíněná sleva musí být podmínky snadno dostupné a jasné.
- **Kde na webu:** bannery, akční stránky, produktové stránky, pravidla soutěží.
- **Kontrola:** jev.
- **Otázky pro Jev:**
  - `promo_conditional_offer` — text_en: "Does the sentence (field sentence) announce a special offer such as a gift, bonus, voucher, contest or discount that the customer gets only if a condition is met?" — text_cs: "Oznamuje veta (pole sentence) osobitnú ponuku, napr. darček, bonus, poukaz, súťaž alebo zľavu, ktorú zákazník získa len pri splnení určitej podmienky?"
  - `promo_conditions_stated` — text_en: "Do the sentence (field sentence) or the text right next to it (context_before, context_after) state the conditions for getting the special offer, or link to them? If no special offer is announced, answer no." — text_cs: "Uvádza veta (pole sentence) alebo text hneď vedľa nej (context_before, context_after) podmienky na získanie osobitnej ponuky alebo odkaz na ne? Ak veta neoznamuje žiadnu osobitnú ponuku, odpoveď je nie."
- **Kontrola kódem:** odkaz na pravidla `(?i)(podmienk\w+|pravidl\w+)\s+(akcie|súťaže|kampane)` nebo odkaz s tímto textem poblíž.
- **Skládání:** `segment`: `all: [promo_conditional_offer ≥ 0,5]`, `none: [promo_conditions_stated ≥ 0,5]`; kódový odkaz na pravidla na stránce nález ruší.
- **Závažnost (návrh):** low až medium.
- **Poznámky a nejistoty:** Samotná sleva ceny je v `sk_price_reduction_previous_price`.

### sk_sponsor_identification
- **Ustanovení:** § 4 ods. 4 Z22; § 11 ods. 2 Z108 (neoznámený obchodní účel).
- **Citace:** „Ak poskytovateľ služieb uskutočňuje komerčnú komunikáciu v mene alebo na účet inej osoby, musí byť táto osoba identifikovaná.“ (Z22 ř. 337–338); „… alebo ak obchodník neoznámi obchodný účel obchodnej praktiky, ibaže je zrejmý z kontextu, …“ (Z108 ř. 1718–1719)
- **Požadavek lidsky:** Obsah zveřejněný za úplatu nebo v zájmu jiné firmy (sponzorované články, placené recenze) musí být označen a zadavatel identifikován.
- **Kde na webu:** blog, magazín, recenze.
- **Kontrola:** nelze z textu (zda byl obsah placený, je fakt mimo web). Jev jen eviduje označení.
- **Otázky pro Jev:** `content_sponsored` — text_en: "Does the sentence (field sentence) say that the content is advertising, is sponsored, or is published on behalf of another company?" — text_cs: "Uvádza veta (pole sentence), že obsah je reklama, je sponzorovaný alebo je zverejnený v mene inej spoločnosti?"
- **Kontrola kódem:** `(?i)(sponzorovan\w+|reklamn\w+\s+článok|platená\s+spolupráca|\bPR\s+článok|v\s+spolupráci\s+s)`.
- **Skládání:** bez nálezu, evidence.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Skrytá reklama je i na černé listině (příloha č. 1), řeší jiná rešerše.

### sk_reviews_verification_info
- **Ustanovení:** § 11 ods. 6 písm. a) Z108 (znění od 1. 1. 2026, N310 bod 19).
- **Citace:** „a) tom, či a akým spôsobom obchodník zabezpečuje, že hodnotenia produktov pochádzajú od spotrebiteľov, ktorí produkt skutočne kúpili alebo použili, ak obchodník poskytuje spotrebiteľom prístup k hodnoteniu produktov,“ (Z108 ř. 1754–1757)
- **Požadavek lidsky:** Pokud e-shop zobrazuje hodnocení produktů, musí říct, zda a jak ověřuje, že jsou od skutečných kupujících. I odpověď „neověřujeme“ je informace, která stačí.
- **Kde na webu:** stránka o recenzích, VOP, u recenzí na produktové stránce.
- **Kontrola:** jev + kód (kód pozná, že web recenze zobrazuje).
- **Otázky pro Jev:** `legal_reviews_verification` — text_en: "Does this text explain whether and how the shop checks that product reviews come from customers who actually bought or used the product?" — text_cs: "Vysvetľuje tento text, či a ako e-shop overuje, že hodnotenia produktov pochádzajú od zákazníkov, ktorí produkt skutočne kúpili alebo použili?"
- **Kontrola kódem:** brána JSON-LD `"@type"\s*:\s*"(AggregateRating|Review)"`, microdata `itemprop="(ratingValue|reviewCount)"`, text `(?i)(hodnoteni\w+\s+zákazník\w*|recenzi\w+|\d[,.]\d\s*/\s*5)`; signál `(?i)(overen\w+\s+(nákup|zákazník)\w*|overené\s+hodnoteni\w+)`.
- **Skládání:** brána (recenze nalezeny); pak `site_presence(legal_reviews_verification)` přes právní stránky, rám a produktové stránky.
- **Závažnost (návrh):** medium až high (u webů s recenzemi).
- **Poznámky a nejistoty:** Od 1. 1. 2026 platí i pro recenze výrobků, které obchodník sám neprodává (N310 bod 19 vypustil „ktoré predáva alebo poskytuje“). Nepravdivé tvrzení o ověřených recenzích je na černé listině (jiná rešerše).

### sk_search_ranking_parameters
- **Ustanovení:** § 11 ods. 6 písm. b), ods. 7 a 8 Z108; § 16 ods. 1 písm. a) a ods. 2 Z108 (online trh).
- **Citace:** „b) hlavných parametroch, ktoré určujú poradie produktov vo výsledku vyhľadávania v online rozhraní, 49 ) … ak spotrebitelia majú možnosť v online rozhraní vyhľadávať … produkty v ponuke rôznych obchodníkov alebo iných osôb …; informácie sa poskytujú v osobitnej časti online rozhrania, ktorá je priamo a ľahko dostupná …“ (Z108 ř. 1759–1764)
- **Požadavek lidsky:** Jen tam, kde vyhledávání ukazuje nabídky různých prodejců (tržiště, srovnávače): samostatná, snadno dostupná stránka s hlavními parametry řazení výsledků.
- **Kde na webu:** samostatná stránka odkazovaná z výsledků vyhledávání, VOP.
- **Kontrola:** jev + kód (kód pozná tržiště, Jev informaci). Pro e-shop s vlastním zbožím se nepoužije.
- **Otázky pro Jev:** `legal_ranking_parameters` — text_en: "Does this text explain the main parameters that determine the order in which products or offers are shown in search results?" — text_cs: "Vysvetľuje tento text hlavné parametre, ktoré určujú poradie produktov alebo ponúk vo výsledkoch vyhľadávania?"
- **Kontrola kódem:** brána tržiště `(?i)(predáva\s+a\s+(doručuje|odosiela)|predajca\s*:|ďalší\s+predajcovia|ponuky\s+(od\s+)?predajcov|marketplace|partnersk\w+\s+predajc\w+)`; odkaz ze stránky vyhledávání (URL s `search`, `vyhladavanie`, `?q=`) na stránku o řazení `(?i)(radenie|poradie|ako\s+radíme)`.
- **Skládání:** brána tržiště; pak `site_presence(legal_ranking_parameters)`.
- **Závažnost (návrh):** low pro běžný e-shop, medium až high pro tržiště.
- **Poznámky a nejistoty:** Stránky vyhledávání nástroj podle zadání vyřazuje; odkaz se proto musí hledat v rámu nebo na stránkách kategorií.

### sk_comparison_service_info
- **Ustanovení:** § 11 ods. 6 písm. c) Z108 (nové od 27. 9. 2026).
- **Citace:** „c) metóde porovnávania produktov, produktoch, ktoré sú predmetom porovnávania, ich dodávateľoch a o opatreniach na zabezpečenie aktualizácie informácií, ak obchodník poskytuje službu, v rámci ktorej sa porovnávajú produkty, a spotrebiteľovi poskytuje informácie o environmentálnych alebo sociálnych vlastnostiach produktov, …“ (Z108 ř. 1767–1770)
- **Požadavek lidsky:** Jen pro srovnávací služby, které ukazují environmentální nebo sociální vlastnosti: popsat metodu srovnání, srovnávané produkty a dodavatele a aktualizaci.
- **Kde na webu:** stránka metodiky.
- **Kontrola:** jev s bránou (u běžného e-shopu se nepoužije).
- **Otázky pro Jev:** `legal_comparison_method` — text_en: "Does this text describe the method by which products are compared?" — text_cs: "Opisuje tento text metódu, ktorou sa produkty porovnávajú?"
- **Kontrola kódem:** brána `(?i)(porovna\w+|porovnávač\w*)` spolu s eko výrazy z modulu `eco`.
- **Skládání:** `site_presence` s bránou.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Hranice „služby porovnávania“ u e-shopu s funkcí „porovnať produkty“ je právní otázka.

### sk_marketplace_info
- **Ustanovení:** § 11 ods. 4 písm. f) Z108; § 16 ods. 1 písm. b) až d) Z108.
- **Citace:** „b) skutočnosť, či osoba, ktorá ponúka produkt na online trhu, je obchodníkom podľa vyhlásenia, ktoré poskytla prevádzkovateľovi online trhu, c) poučenie, že sa na zmluvu nebudú vzťahovať právne predpisy na ochranu spotrebiteľa, ak osoba podľa písmena b) nie je obchodníkom, d) deľbu povinností, …“ (Z108 ř. 2124–2130)
- **Požadavek lidsky:** Na tržišti u každé nabídky říct, zda prodává podnikatel, upozornit, že u soukromníka neplatí spotřebitelské právo, a vysvětlit dělbu povinností mezi provozovatelem a prodejcem.
- **Kde na webu:** stránka nabídky, VOP tržiště.
- **Kontrola:** jev + kód (brána tržiště).
- **Otázky pro Jev:**
  - `offer_seller_status` — text_en: "Does the text state whether the seller of the offered product is a business (trader) or a private individual?" — text_cs: "Uvádza text, či predávajúci ponúkaného produktu je podnikateľ (obchodník) alebo súkromná osoba?"
  - `legal_marketplace_no_consumer_law` — text_en: "Does this text warn that consumer protection law does not apply to contracts with sellers who are not traders?" — text_cs: "Upozorňuje tento text, že na zmluvy s predávajúcimi, ktorí nie sú obchodníkmi, sa nevzťahujú predpisy na ochranu spotrebiteľa?"
  - `legal_marketplace_duties_split` — text_en: "Does this text explain how the obligations related to the contract are divided between the marketplace operator and the seller?" — text_cs: "Vysvetľuje tento text, ako sú povinnosti súvisiace so zmluvou rozdelené medzi prevádzkovateľa online trhu a predávajúceho?"
- **Kontrola kódem:** brána tržiště jako u `sk_search_ranking_parameters`.
- **Skládání:** brána; `offer_seller_status` jako `page_presence` na stránkách nabídek; zbylé dvě `site_presence`.
- **Závažnost (návrh):** medium (jen tržiště).
- **Poznámky a nejistoty:** Povinnosti provozovatelů platforem podle nařízení (EÚ) 2022/2065 (DSA) jsem neřešil (NEOVĚŘENO).

## M. Přístupnost, bezpečnost výrobků, služby

### sk_accessibility_info
- **Ustanovení:** § 6 ods. 1 písm. b) a ods. 2 písm. a) Z351; působnost § 2 ods. 1 písm. d) a ods. 3 Z351.
- **Citace:** „Tento zákon sa vzťahuje na služby poskytované spotrebiteľom,2) ktorými sú … d) služby informačnej spoločnosti.3)“ (Z351 ř. 230, 254–255); „Tento zákon sa nevzťahuje na služby poskytované mikropodnikom.4)“ (Z351 ř. 270); „Poskytovateľ služby je povinný informácie podľa odseku 1 písm. b) a) uviesť vo všeobecných podmienkach na poskytovanie služby alebo v obdobnom dokumente v rozsahu: … 3. opis spôsobu, akým služba spĺňa ustanovené požiadavky na prístupnosť služby pre osoby so zdravotným postihnutím,“ (Z351 ř. 418–428)
- **Požadavek lidsky:** E-shop, který není mikropodnik, musí ve VOP nebo podobném dokumentu popsat službu a to, jak splňuje požadavky přístupnosti pro osoby se zdravotním postižením.
- **Kde na webu:** VOP, vyhlásenie o prístupnosti.
- **Kontrola:** jev + kód; zda je provozovatel mikropodnik (méně než 10 zaměstnanců a obrat nebo bilance do 2 mil. EUR podle doporučení 2003/361/ES, odkaz 4), z textu nelze.
- **Otázky pro Jev:** `legal_accessibility` — text_en: "Does this text describe how the online shop meets accessibility requirements for people with disabilities (for example in an accessibility statement)?" — text_cs: "Opisuje tento text, ako e-shop spĺňa požiadavky na prístupnosť pre osoby so zdravotným postihnutím (napr. vo vyhlásení o prístupnosti)?"
- **Kontrola kódem:** `(?i)(prístupnos\w+|vyhlásen\w+\s+o\s+prístupnosti|zdravotn\w+\s+postihnut\w+|WCAG|EN\s?301\s?549)`; odkaz v rámu na stránku přístupnosti.
- **Skládání:** `site_presence`; nález vždy nejvýš „k ověření“ (výjimka mikropodniku).
- **Závažnost (návrh):** medium.
- **Poznámky a nejistoty:** Hranice mikropodniku podle doporučení Komise uvádím zpaměti, text doporučení jsem nestahoval (NEOVĚŘENO). Samotné technické splnění přístupnosti z textu nelze.

### sk_gpsr_product_listing
- **Ustanovení:** čl. 19 GPSR (nariadenie (EÚ) 2023/988, priamo uplatniteľné; odkaz 56c Z108).
- **Citace:** „Ak hospodárske subjekty sprístupňujú výrobky na trhu online …, v príslušnej ponuke týchto výrobkov sa jasne a viditeľne uvádzajú aspoň tieto informácie: a) meno, registrované obchodné meno alebo registrovaná ochranná známka výrobcu, ako aj poštová a elektronická adresa, na ktorej ho možno kontaktovať;“ (GPSR ř. 540–542); „d) všetky upozornenia alebo bezpečnostné pokyny, ktoré sa umiestňujú na výrobok alebo obal … v jazyku, ktorý je pre spotrebiteľov ľahko zrozumiteľný …“ (GPSR ř. 548)
- **Požadavek lidsky:** Každá produktová nabídka musí uvést výrobce (jméno nebo ochrannou známku) s poštovní a elektronickou adresou, u výrobce mimo EU odpovědnou osobu, identifikaci výrobku (obrázek, typ, jiný identifikátor) a varování a bezpečnostní pokyny.
- **Kde na webu:** produktová stránka.
- **Kontrola:** jev + kód; zda výrobek nějaká varování má, z textu nelze.
- **Otázky pro Jev:**
  - `product_manufacturer_name` — text_en: "Does the sentence (field sentence) name the manufacturer of the product (a company name or a registered trade mark), not only the seller?" — text_cs: "Uvádza veta (pole sentence) výrobcu produktu (obchodné meno alebo registrovanú ochrannú známku), a nie len predávajúceho?"
  - `product_safety_warning` — text_en: "Does the sentence (field sentence) give a warning or a safety instruction for using the product?" — text_cs: "Uvádza veta (pole sentence) upozornenie alebo bezpečnostný pokyn na používanie produktu?"
- **Kontrola kódem:** štítek `(?i)\b(výrobca|výrobcu|manufacturer)\s*:`; v jeho okolí adresa (PSČ nebo stát) a e-mail nebo web; odpovědná osoba `(?i)zodpovedn\w+\s+osob\w+`; identifikátor `(?i)(EAN|GTIN|kód\s+produktu|model|typ)\s*:?\s*[\w\-]+` nebo JSON-LD `gtin`, `mpn`, `sku`.
- **Skládání:** `page_presence` (produktové stránky): nález, když chybí výrobce nebo jeho adresa; varování jen evidence.
- **Závažnost (návrh):** medium až high (týká se každé produktové stránky).
- **Poznámky a nejistoty:** Uplatňuje se od 13. 12. 2024 (GPSR ř. 938). Není to informační povinnost ze spotřebitelského zákona, ale dopadá na tytéž stránky. Dozor v SR je mimo tuto rešerši. Pokyny Komise ke GPSR (česky) stáhl jiný rešeršista: `eu-komise/komise-gpsr-pokyny-2025-6233-cs.txt`, nečetl jsem je.

### sk_services_act_info
- **Ustanovení:** § 6 ods. 1, 2 a 5 Z136 (použitelnost na online prodej zboží NEOVĚŘENO).
- **Citace:** „Usadený poskytovateľ služby a cezhraničný poskytovateľ služby … je pred poskytnutím služby alebo pred podpísaním zmluvy o poskytnutí služby povinný príjemcu služby zrozumiteľne a jednoznačne informovať o …“ (Z136 ř. 790–792); „c) príslušnom jednotnom kontaktnom mieste alebo o príslušnom orgáne, ktorý rozhodol o udelení oprávnenia, na základe ktorého poskytovateľ poskytuje službu,“ (Z136 ř. 801–802); „g) zmluvných ustanoveniach, ktoré sa týkajú voľby práva alebo voľby súdu, ktoré poskytovateľ služby uplatňuje,“ (Z136 ř. 814–815)
- **Požadavek lidsky:** Pokud se zákon o službách vztahuje i na e-shop (maloobchod je „činnosť … obchodnej … povahy“ podle § 2 písm. a) Z136), přidává nad rámec Z108 a Z22 hlavně orgán, který vydal oprávnění (živnostenský úrad), IČ DPH nebo DIČ a doložky o volbě práva a soudu. Podle § 6 ods. 5 jen v rozsahu, který přesahuje jiné předpisy.
- **Kde na webu:** VOP, kontakt.
- **Kontrola:** kód (přítomnost); použitelnost zákona z textu neurčíme.
- **Otázky pro Jev:** žádné.
- **Kontrola kódem:** `(?i)(okresn\w+\s+úrad\w*[^.]{0,40}živnostensk\w+|živnostensk\w+\s+odbor)`; volba práva `(?i)(rozhodn\w+\s+práv\w+|právom\s+Slovenskej\s+republiky|príslušn\w+\s+súd)`.
- **Skládání:** `site_presence`, nález nejvýš „k ověření“ a až po potvrzení použitelnosti právníkem.
- **Závažnost (návrh):** low.
- **Poznámky a nejistoty:** Doporučuji zapnout až po právním posouzení; do té doby jen evidence.

---

## Souhrnná tabulka

| # | id | Ustanovení | Kontrola | Kde |
| --- | --- | --- | --- | --- |
| 1 | sk_trader_identity | § 5/1 b) Z108; § 4/1 a) Z22; § 3a ObZ | jev + kód | kontakt, VOP, patička |
| 2 | sk_trader_ico | § 3a/1, 3 ObZ | kód | kontakt, VOP, patička |
| 3 | sk_trader_register | § 4/1 d) Z22; § 3a/1 ObZ | kód | kontakt, VOP, patička |
| 4 | sk_trader_vat_id | § 4/1 b) Z22 | kód (+ plátcovství nelze) | kontakt, VOP |
| 5 | sk_trader_phone | § 5/1 c), § 15/3 Z108; § 4/1 c) Z22 | kód | kontakt, patička |
| 6 | sk_trader_email | § 15/1 a), § 15/3 Z108; § 4/1 c) Z22 | kód | kontakt, patička |
| 7 | sk_trader_other_online_channel | § 15/1 b) Z108 | nelze z textu | kontakt |
| 8 | sk_return_complaint_address | § 15/1 c) Z108; § 622/1 OZ | jev + kód | reklamace, odstoupení |
| 9 | sk_contact_premium_rate | § 4/2 g), § 15/1 e) Z108 | kód | kontakt |
| 10 | sk_supervisory_authority | § 4/1 e) Z22 | jev + kód | VOP, kontakt |
| 11 | sk_info_permanently_accessible | § 4/3 Z22 | kód | rám všech stránek |
| 12 | sk_language_slovak | § 4/1 g) Z108; § 5/4 Z22 | kód | právní stránky |
| 13 | sk_product_main_characteristics | § 5/1 a) Z108 | kód (heuristika) | produkt |
| 14 | sk_price_total | § 5/1 d), § 2 g), § 6/1 Z108; § 15 ZC | kód | produkt |
| 15 | sk_unit_price | § 6/1, 3, 4, § 2 h) Z108 | kód | produkt |
| 16 | sk_price_reduction_previous_price | § 7 Z108 | jev + kód (správnost ceny nelze) | produkt, výpis, bannery |
| 17 | sk_delivery_costs | § 5/1 d), § 15/7 Z108 | jev + kód | doprava a platba |
| 18 | sk_additional_costs_notice | § 5/1 d) Z108 | jev | doprava, VOP |
| 19 | sk_payment_surcharge | § 4/2 f) Z108 | jev + kód | doprava a platba |
| 20 | sk_prechecked_paid_options | § 5/2 Z108 | nelze z textu | košík |
| 21 | sk_delivery_terms_deadline | § 5/1 e) Z108; § 613/1 OZ | jev + kód | doprava, VOP, produkt |
| 22 | sk_payment_methods | § 5/1 e), § 17/2 Z108 | jev + kód (košík nelze) | doprava a platba, rám |
| 23 | sk_delivery_restrictions | § 17/2 Z108 | jev | doprava |
| 24 | sk_eco_delivery_options | § 15/1 l) Z108 | nelze z textu | doprava, košík |
| 25 | sk_legal_guarantee_info (existující) | § 5/1 f) Z108; § 622 OZ | jev | reklamace, VOP |
| 26 | sk_legal_guarantee_duration | § 5/1 f) Z108; § 619 OZ | kód | reklamace, VOP |
| 27 | sk_harmonized_legal_guarantee_notice | § 5/1 f) Z108; EU1960 | nelze z textu (obrázek), slabý kód | produkt, pokladna, VOP |
| 28 | sk_durability_guarantee_label | § 5/1 g) Z108; EU1960 | jev + kód | produkt |
| 29 | sk_complaint_handling_deadline | § 622/3, § 623/4 OZ; § 4/2 e) Z108 | kód | reklamace |
| 30 | sk_defect_notice_period | § 621/3 OZ; § 4/2 d) Z108 | jev + kód | reklamace, doprava |
| 31 | sk_remedy_choice | § 623/1, 2 OZ; § 4/2 d), e) Z108 | jev | reklamace |
| 32 | sk_additional_guarantee_terms | § 5/1 j) Z108; § 626 OZ | jev | produkt, VOP |
| 33 | sk_digital_content_liability | § 5/1 h) Z108; § 852h OZ | kód | VOP |
| 34 | sk_service_liability | § 5/1 i) Z108 | jev | VOP |
| 35 | sk_repairability_score | § 5/1 k) Z108 | jev + kód | produkt |
| 36 | sk_spare_parts_repair_info | § 5/1 l) Z108 | nelze z textu | produkt |
| 37 | sk_digital_functionality_compatibility | § 5/1 n), o) Z108 | jev | produkt |
| 38 | sk_software_update_period | § 5/1 p) Z108 | nelze z textu | produkt |
| 39 | sk_manufacturer_repair_info | § 13b/7 Z108 | nelze z textu | servis |
| 40 | sk_contract_duration_termination | § 5/1 m) Z108 | jev | VOP, předplatné |
| 41 | sk_min_commitment_duration | § 15/1 j) Z108 | jev + kód | VOP, předplatné |
| 42 | sk_deposit_conditions | § 15/1 k) Z108 | jev | VOP |
| 43 | sk_personalised_price_notice | § 15/1 d) Z108 | nelze z textu | VOP, cena |
| 44 | sk_withdrawal_info (existující) | § 15/1 f)–i), § 19 Z108 | jev + kód | VOP, odstoupení |
| 45 | sk_withdrawal_form (existující) | § 15/1 f), § 20/6 Z108; příl. 2 | jev | odstoupení |
| 46 | sk_withdrawal_function (existující kandidát) | § 20a Z108 | kód | rám, odstoupení, účet |
| 47 | sk_withdrawal_function_location_info | § 15/1 f) Z108; příl. 3 | jev + kód | VOP, odstoupení |
| 48 | sk_withdrawal_return_costs | § 15/1 g), § 15/7, § 21/3 Z108 | jev + kód | VOP, odstoupení |
| 49 | sk_withdrawal_service_payment | § 15/1 h) Z108 | jev | VOP |
| 50 | sk_withdrawal_exceptions | § 15/1 i), § 19/1 Z108 | jev | VOP, odstoupení |
| 51 | sk_withdrawal_template_unfilled | § 15/6 Z108; příl. 3 | kód | VOP, odstoupení |
| 52 | sk_withdrawal_refund_deadline | § 22/1 Z108 | kód | VOP, odstoupení |
| 53 | sk_withdrawal_restrictive_terms | § 19/1, § 21/6 Z108 | jev | VOP, odstoupení |
| 54 | sk_adr (existující) | § 5/1 q), § 5/3 Z108 | jev | VOP |
| 55 | sk_adr_redress_request | § 5/1 q) Z108; § 11 Z391 | jev + kód | VOP, reklamace |
| 56 | sk_ecommerce_order_steps | § 5/3 b) 1 Z22 | jev | VOP |
| 57 | sk_ecommerce_error_correction | § 5/3 a), b) 2 Z22 | jev | VOP |
| 58 | sk_ecommerce_contract_storage | § 5/3 b) 3 Z22 | jev | VOP |
| 59 | sk_ecommerce_contract_language | § 5/3 b) 4, § 5/4 Z22 | jev + kód | VOP |
| 60 | sk_terms_reproducible | § 5/5 a) Z22 | kód | VOP |
| 61 | sk_order_confirmation | § 5/6 Z22; § 17/12 Z108 | nelze z textu | e-mail |
| 62 | sk_order_button_label | § 17/4, 6 Z108 | nelze z textu | pokladna |
| 63 | sk_pre_order_summary | § 17/3 Z108 | nelze z textu | pokladna |
| 64 | sk_service_digital_start_consent | § 17/10, 12 Z108 | nelze z textu | pokladna |
| 65 | sk_promo_conditions | § 4/5 Z22 | jev | bannery, akce |
| 66 | sk_sponsor_identification | § 4/4 Z22; § 11/2 Z108 | nelze z textu | blog |
| 67 | sk_reviews_verification_info | § 11/6 a) Z108 | jev + kód | recenze, VOP |
| 68 | sk_search_ranking_parameters | § 11/6 b), § 16/1 a) Z108 | jev + kód | tržiště |
| 69 | sk_comparison_service_info | § 11/6 c) Z108 | jev | srovnávač |
| 70 | sk_marketplace_info | § 11/4 f), § 16/1 b)–d) Z108 | jev + kód | tržiště |
| 71 | sk_accessibility_info | § 6 Z351 | jev + kód | VOP, přístupnost |
| 72 | sk_gpsr_product_listing | čl. 19 GPSR | jev + kód | produkt |
| 73 | sk_services_act_info | § 6 Z136 | kód | VOP, kontakt |

Součty podle hlavní metody: jev 19, kód 19, jev + kód 22, nelze z textu 13 (celkem 73, z toho 5 existujících kontrol).

## Co to vyžaduje od kódu (nové typy kontrol)

Návrh, ne zadání; bez toho část pravidel nejde zapsat do YAML.

1. `regex_required` s parametrem `where` (právní stránky, rám, produktové stránky, celý web); dnes jen právní stránky.
2. Porovnání čísel z regexu (`value_gt`, `value_lt`) pro lhůty a ceny (30 dní, 14 dní, 2 měsíce, 50 g/ml, procento slevy).
3. Vyhledání textu v prvcích `a`, `button`, `input[value]` včetně rámu (tlačítka, odkazy).
4. Regex nad alt, title a src obrázků (harmonizované oznámení, GARAN, energetický štítek).
5. Test JSON-LD a microdata (`AggregateRating`, `Review`, `Offer.price`, `gtin`).
6. Detekce jazyka právních stránek.
7. Rozsah `page_presence` nad vzorkem produktových stránek a nad rámem všech stránek.
8. Brány (podmíněné pravidlo): tržiště, předplatné, recenze, sleva, digitální obsah, služby, kategorie telefonů a tabletů, widget chatu.
9. Detekce formuláře (`form` + `textarea`) pro nález „jen kontaktní formulář“.
10. Vyhodnocení rámu (patička) i v právním modulu pro `site_presence` (kontakty bývají jen v patičce).

## Mimo rozsah a NEOVĚŘENO

- Příloha č. 1 Z108 (černá listina) a obecné klamavé praktiky § 10 až 12 Z108: jiná rešerše.
- Sektorové informace k výrobkům (potraviny podle nařízení (EÚ) č. 1169/2011, energetické štítky podle nařízení (EÚ) 2017/1369, textil podle nařízení (EÚ) č. 1007/2011, pneumatiky, kosmetika, chemické látky, alkohol a tabák): nestahoval jsem, NEOVĚŘENO.
- Informace o zpětném odběru elektroodpadu a baterií (zákon č. 79/2015 Z. z., nařízení (EÚ) 2023/1542): NEOVĚŘENO.
- Ochrana osobních údajů a cookies (GDPR): mimo spotřebitelské informační povinnosti.
- Platforma RSO: Z108 ji po novele 310/2025 nezmiňuje; zrušení nařízení 524/2013 NEOVĚŘENO.
- Předajné akce (§ 18 Z108), telefonické nabídky (§ 17 ods. 1) a smlouvy mimo provozovnu: netýkají se e-shopu.
- Evropský formulář o opravě (§ 13a Z108, príloha č. 3a): dobrovolný pro opravovny.
- Přesné umístění harmonizovaného oznámení na webu, seznam prefixů prémiových čísel SR, kontrolní součet IČO, přijatelnost jednotkové ceny za 100 g, hranice mikropodniku a použitelnost Z136 na e-shop se zbožím: NEOVĚŘENO, k ověření právníkem.
