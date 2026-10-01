# Informační povinnosti českého e-shopu vůči spotřebiteli

Rešerše pro screening textů webu, stav k 25. 9. 2026. Návrh ke kontrole právníkem, ne právní výklad. Cesty k souborům v citacích jsou relativní ke složce `D:\_github\Overko\podklady\`. Každá citace je doslovný výřez ze zdrojového souboru, vynechaný text je nahrazen „…“.

## 0. Jak číst tento dokument

### Zdroje a znění

| Předpis | Soubor | Znění |
| --- | --- | --- |
| Občanský zákoník, zák. č. 89/2012 Sb. (OZ) | `predpisy-cz/cz-89-2012-obcansky-zakonik.txt` | verze 18, 1. 1. 2026 – 31. 12. 2026. Od 1. 1. 2027 verze 19 podle zákona č. 159/2026 Sb. (nové písm. i) v § 1820 odst. 1 a nový § 1830a). |
| Zákon o ochraně spotřebitele, zák. č. 634/1992 Sb. (ZOS) | `predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt` | verze 57, 20. 8. 2025 – 31. 12. 2026. Od 1. 1. 2027 verze 58 (159/2026 Sb., jen finanční služby a přestupky k § 1830a OZ). |
| Zákon o cenách, zák. č. 526/1990 Sb. | `predpisy-cz/cz-526-1990-zakon-o-cenach.txt` | verze 26 od 13. 5. 2026 |
| Vyhláška č. 291/2024 Sb. (měrná cena nepotravinářského zboží) | `predpisy-cz/cz-291-2024-vyhlaska-merna-cena.txt` (stáhl jiný rešeršista) | verze 1 od 1. 1. 2025 |
| Nařízení vlády č. 29/2023 Sb. (vzorové poučení a formulář) | `predpisy-cz/cz-29-2023-nv-vzorove-pouceni-a-formular-odstoupeni.txt`, příloha jen jako sken `…-priloha.pdf`, v .txt je její přepis | verze 2 od 19. 6. 2026 (NV č. 66/2026 Sb.). Nahradilo NV č. 363/2013 Sb., zrušené k 18. 2. 2023. |
| Nařízení vlády č. 66/2026 Sb. | `predpisy-cz/cz-66-2026-nv-novela-vzoroveho-pouceni.txt` | účinnost 19. 6. 2026 |
| Zákon č. 159/2026 Sb. (finanční služby na dálku, tlačítko pro odstoupení) | `predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt` | platnost 2. 9. 2026, účinnost 1. 1. 2027 |
| Zákon o obchodních korporacích, zák. č. 90/2012 Sb. (ZOK) | `predpisy-cz/cz-90-2012-zakon-o-obchodnich-korporacich.txt` | verze 7 od 19. 7. 2024 |
| Zákon o výrobcích s ukončenou životností, zák. č. 542/2020 Sb. | `predpisy-cz/cz-542-2020-zakon-o-vyrobcich-s-ukoncenou-zivotnosti.txt` | verze 5 od 1. 7. 2025 |
| Zákon o některých službách informační společnosti, zák. č. 480/2004 Sb. | `predpisy-cz/cz-480-2004-zakon-o-nekterych-sluzbach-informacni-spolecnosti.txt` | verze 12 od 23. 3. 2023. Povinnost pro text webu neobsahuje, § 7 se týká obchodních sdělení e-mailem. |
| Nařízení (EU) 2023/988 o obecné bezpečnosti výrobků (GPSR) | `predpisy-eu/eu-2023-988-gpsr-cs.txt` | Úř. věst. L 135/1, použitelné od 13. 12. 2024 |
| Nařízení (EU) 2018/644 o přeshraničních službách dodávání balíků | `predpisy-eu/eu-2018-644-preshranicni-dodani-baliku-cs.txt` | čl. 7 |
| Nařízení (EU) 2024/3228 (zrušení platformy ODR) | `predpisy-eu/eu-2024-3228-zruseni-platformy-odr-cs.txt` | nařízení 524/2013 zrušeno k 20. 7. 2025 |
| Směrnice (EU) 2024/825 (EmpCo) | `predpisy-eu/eu-2024-825-empco-cs.txt` | čl. 2 mění informační povinnosti směrnice 2011/83/EU, v ČR k 25. 9. 2026 netransponováno |

### Co nástroj z textu nevidí

Nástroj neprochází košík, pokladnu, přihlášený účet ani e-maily po objednávce. Nečte PDF, obrázky (loga, odznaky, harmonizované štítky) a obsah vykreslený až JavaScriptem. Povinnosti, které se odehrávají tam, jsou níže označené „nelze z textu“ a patří do sekce zprávy „Co nebylo zkontrolováno“.

Právní modul dnes vyhodnocuje jen odstavce právních stránek. Identifikační a kontaktní údaje ale bývají v patičce (rám stránky). Kontroly kódem u identity a kontaktu proto navrhuji pouštět i nad rámem stránky a nad stránkou Kontakt. Doporučuji doplnit `legal_page_slugs` o `doprava-a-platba`, `platba`, `zpusoby-platby`, `jak-nakupovat`, `vop`, `vseobecne-obchodni-podminky`, `reklamace`, `vraceni-zbozi`, `odstoupeni-od-smlouvy`, `zpetny-odber`, `recenze` a `o-nas`.

### Typy kontroly a skládání

Kontrola:

- **jev**: rozhodnutí ano/ne nad odstavcem právní stránky nebo nad větou produktové stránky.
- **kód**: regulární výraz, seznam nebo struktura (JSON-LD). Čísla, lhůty, ceny, IČO, telefon a e-mail zjišťuje vždy kód.
- **jev + kód**: obojí, typicky Jev pozná, o čem odstavec je, a kód v něm ověří číslo nebo adresu.
- **nelze z textu**: vyžaduje objednávkový proces, historická data nebo fakta mimo web.

Skládání (existující typy jsou `segment`, `site_presence` a `regex_required`, ostatní jsou návrh):

- `segment`: nález u konkrétního segmentu.
- `site_presence`: informace musí být aspoň v jednom odstavci právních stránek, práh `presence_threshold` 0,7.
- `site_presence_if(spouštěč)`: jako `site_presence`, ale vyhodnotí se jen tehdy, když spouštěč (otázka nebo kód) platí někde na webu.
- `site_regex`: dnešní `regex_required`; navrhuji rozšířit prohledávání o rám stránky a stránku Kontakt.
- `page_type_presence(typ)`: informace musí být přímo na stránce daného typu, například v obchodních podmínkách.
- `page_presence`, `page_presence_if(spouštěč)`: informace musí být na každé produktové stránce (maximum přes věty stránky, nebo kód nad textem stránky včetně JSON-LD). Ve zprávě jeden souhrnný nález „X z N produktových stránek“ se seznamem URL, protože se prochází jen vzorek (`--sample-products`).
- `page_regex`, `page_regex_if(spouštěč)`: kód musí najít vzor na každé produktové stránce.
- `segment_regex`: každý výskyt vzoru je nález „k ověření“.
- informativní: zapsat do zprávy bez nálezu.

### Jak jsou psané otázky pro Jev

Podle poučení z testů sady `eco` (rules/CHANGELOG.md): jedna otázka = jeden pozorovatelný znak, žádný právní závěr, žádný skrytý předpoklad. Kde by otázka mohla předpokládat, že text něco obsahuje, končí větou „Answer no if…“ / „…odpověz ne.“ Odstavec právní stránky jde Jevu jako text („Does this text…“), věta produktové stránky jako objekt s kontextem („Does the sentence (field sentence)…“). Navržené prahy vycházejí z existujících (`presence_threshold` 0,7, pásma 0,85 a 0,5) a je potřeba je změřit na označeném vzorku.

### Priorita podle kontrol ČOI

Kontroly internetových obchodů za rok 2025 (`coi/coi-kontroly-internet-rok-2025.txt`): § 13 ZOS 363 případů, § 14 ZOS 135, § 5a odst. 5 ZOS (recenze) 91, § 12 ZOS 42, § 12a ZOS 32; § 1820 OZ 488 (z toho písm. i) 240, písm. l) 47, písm. c) a h) po 46), § 1827 OZ 199 (z toho odst. 2 195), § 1826a odst. 2 OZ 107. Kontroly slev 2025 (`coi/coi-kontroly-slevy-rok-2025.txt`): § 12a ZOS 324 případů. Závažnosti níže tomu odpovídají.

### Obecná pravidla pro celou oblast

- Informace podle § 1811 a § 1820 OZ se sdělují před objednávkou: „sdělí podnikatel spotřebiteli v dostatečném předstihu před uzavřením smlouvy nebo před tím, než spotřebitel učiní závaznou nabídku,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Důkazní břemeno nese obchod: „V případě pochybností musí podnikatel prokázat, že sdělil spotřebiteli údaje, které je povinen sdělit podle tohoto pododdílu.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Výjimka pro rozvoz potravin, která může vyřadit celý pododdíl § 1820 a násl. (výklad sporný, ověřit): „g) o dodávce potravin, nápojů nebo jiného zboží běžné spotřeby, které podnikatel fyzicky dodává do spotřebitelovy domácnosti, do místa jeho bydliště nebo na jeho pracoviště formou častých a pravidelných dodávek,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Nesplnění povinností podle OZ trestá ČOI přes ZOS: „d) nesdělí spotřebiteli některý z údajů podle § 1820 odst. 1 občanského zákoníku,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Horní hranice pokuty za většinu informačních povinností: „e) 5000000 Kč, jde-li o přestupek podle odstavce 1 písm. a), odstavce 4, odstavce 5 písm. a), d) až f) nebo k), odstavce 7, 8, 11, 13 až 16, 19, 20 nebo 21.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)

## 1. Mapování existujících kontrol

| Existující pravidlo (otázka) | Ustanovení | Sekce zde | Navržené doplnění |
| --- | --- | --- | --- |
| `legal_adr_missing` (`legal_adr`) | § 14 odst. 1 ZOS, § 1820 odst. 1 písm. s) OZ (první část) | cz_adr | kód na webovou adresu subjektu, `page_type_presence` pro obchodní podmínky, nevyžadovat ODR; druhá část písm. s) je samostatná sekce cz_supervisory_authority |
| `legal_complaints_missing` (`legal_complaints`) | § 13 ZOS, § 1820 odst. 1 písm. m) OZ (první část) | cz_complaints | rozpad na znaky: cz_complaint_place (jev + kód), cz_defect_period (kód) |
| `legal_withdrawal_missing` (`legal_withdrawal` + regex „14 dnů“) | § 1820 odst. 1 písm. i) OZ, § 1829 odst. 1 OZ | cz_withdrawal | rozpoznání vzorového poučení kódem, cz_withdrawal_period_start, cz_withdrawal_return_costs, cz_withdrawal_exceptions |
| `legal_withdrawal_form_missing` (`legal_withdrawal_form`) | § 1820 odst. 1 písm. i) OZ, příloha písm. b) NV 29/2023 | cz_withdrawal_form | kód na znaky formuláře podle NV 29/2023 |

## A. Identita a kontakt podnikatele

### cz_trader_name
- Ustanovení: § 435 odst. 1 věta první OZ; § 1820 odst. 1 písm. b) OZ; § 5a odst. 3 písm. b) ZOS (nabídka ke koupi); souběžně § 1811 odst. 2 písm. a) OZ.
- Citace: „Každý podnikatel musí uvádět na obchodních listinách a v rámci informací zpřístupňovaných veřejnosti prostřednictvím dálkového přístupu své jméno a sídlo.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „b) údaje o své totožnosti,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „b) adresa a totožnost prodávajícího nebo osoby, která jedná jeho jménem nebo na jeho účet,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Web musí říct, kdo obchod provozuje: jméno podnikající fyzické osoby nebo obchodní firmu právnické osoby. Název obchodu nebo doména („SuperShop.cz“) nestačí, pokud nejsou obchodní firmou.
- Kde na webu: Kontakt, obchodní podmínky (článek „Provozovatel“), patička. U produktových stránek stačí rám stránky („nejsou-li patrné ze souvislostí“, § 5a odst. 3 ZOS).
- Kontrola: jev + kód. Jev pozná, že odstavec jmenuje provozovatele, a ne dopravce nebo ČOI; kód potvrdí právní formu nebo IČO.
- Otázky pro Jev:
  - `legal_trader_name` — EN: "Does this text state the name or business name of the company or person that operates the shop and sells the goods (for example 'Provozovatel: Example s.r.o.')? Answer no if the text names only a brand, a web domain, a carrier or an authority." — CS: „Uvádí tento text jméno nebo obchodní firmu společnosti či osoby, která obchod provozuje a zboží prodává (např. ‚Provozovatel: Example s.r.o.‘)? Pokud text jmenuje jen značku, doménu, dopravce nebo úřad, odpověz ne.“
- Kontrola kódem: v odstavci s `legal_trader_name` ≥ 0,7 hledat právní formu `(?i)(?:spol\.\s?s\s?r\.\s?o\.|s\.\s?r\.\s?o\.|a\.\s?s\.|v\.\s?o\.\s?s\.|k\.\s?s\.|z\.\s?s\.|z\.\s?ú\.|\bSE\b)` nebo IČO (cz_trader_ico). Doplňkově JSON-LD `Organization.name` a `legalName`.
- Skládání: `site_presence` nad právními stránkami a rámem stránky.
- Závažnost (návrh): high
- Poznámky / nejistoty: § 435 platí pro web vždy, nezávisle na § 1820. Riziko falešné přítomnosti: odstavec o ADR jmenuje „Českou obchodní inspekci“, otázka to vylučuje.

### cz_trader_address
- Ustanovení: § 435 odst. 1 věta první OZ (sídlo); § 1820 odst. 1 písm. c) OZ (adresa sídla); § 5a odst. 3 písm. b) ZOS.
- Citace: „c) adresu sídla, telefonní číslo a adresu pro doručování elektronické pošty,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - Citace § 435 odst. 1 viz cz_trader_name.
- Požadavek lidsky: Uvést adresu sídla prodávajícího (ulice nebo obec, číslo, PSČ, město).
- Kde na webu: Kontakt, obchodní podmínky, patička.
- Kontrola: jev + kód. Kód najde adresu, Jev ověří, že patří prodávajícímu. Odstavce o ADR a reklamaci totiž často obsahují adresu ČOI nebo servisu.
- Otázky pro Jev:
  - `legal_trader_own_address` — EN: "Does this text give the postal address of the seller itself (the shop operator), for example its registered office? Answer no if the only address in the text belongs to an authority, a carrier, a pick-up point or another organisation." — CS: „Uvádí tento text poštovní adresu samotného prodávajícího (provozovatele obchodu), například jeho sídlo? Pokud jediná adresa v textu patří úřadu, dopravci, výdejnímu místu nebo jiné organizaci, odpověz ne.“
- Kontrola kódem: PSČ `\b[1-7]\d{2}[\s\u00a0]?\d{2}\b`, které nenásleduje „Kč“, a zároveň ulice s číslem `\p{Lu}[\p{L}.\- ]{2,40}\s\d{1,5}(?:/\d{1,5})?[a-zA-Z]?\b` nebo název obce v témže odstavci. Adresy známých úřadů vyřadit seznamem (adresu ČOI ověřit).
- Skládání: `site_presence`: odstavec právních stránek nebo rámu, kde kód najde adresu a zároveň `legal_trader_own_address` ≥ 0,7.
- Závažnost (návrh): high
- Poznámky / nejistoty: Slovo „sídlo“ v textu být nemusí. Když web uvádí jen adresu skladu nebo výdejny, pozná to jen člověk. ČOI 2025: § 1820 odst. 1 písm. c) 46 případů.

### cz_trader_ico
- Ustanovení: § 435 odst. 1 věta poslední OZ; § 7 odst. 2 a 3 ZOK (akciová společnost a s. r. o. s webem).
- Citace: „Byl-li podnikateli přidělen identifikující údaj, uvede i ten.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Akciová společnost bez zbytečného odkladu po svém vzniku a dále průběžně uveřejňuje způsobem umožňujícím dálkový přístup … údaje, které je povinna uvádět na obchodních listinách, a další údaje stanovené tímto zákonem.“ (predpisy-cz/cz-90-2012-zakon-o-obchodnich-korporacich.txt)
  - „Zřídí-li společnost s ručením omezeným internetové stránky, vztahuje se na ni ustanovení odstavce 2 obdobně.“ (predpisy-cz/cz-90-2012-zakon-o-obchodnich-korporacich.txt)
- Požadavek lidsky: Uvést IČO. Pro s. r. o. a a. s. jednoznačně (web musí obsahovat údaje z obchodních listin, tedy i IČO), u ostatních podnikatelů sporné.
- Kde na webu: Kontakt, obchodní podmínky, patička.
- Kontrola: kód
- Kontrola kódem: `(?i)\bI[ČC]O?\b\s*[:.]?\s*((?:\d[\s\u00a0]?){7}\d)\b`, odstranit mezery a ověřit kontrolní číslici: s = Σ dᵢ·(9−i) pro i = 1…7, c = (11 − s mod 11) mod 10, musí platit d₈ = c. Neplatné IČO = nález „k ověření“ (překlep). IČO úřadů v odstavci o ADR vyřadit.
- Skládání: `site_regex` (právní stránky, rám stránky, Kontakt).
- Závažnost (návrh): medium; high, pokud kód najde právní formu s. r. o. nebo a. s.
- Poznámky / nejistoty: Zda věta třetí § 435 odst. 1 platí i pro web, nebo jen pro obchodní listiny, je výkladově nejisté; ověřit právníkem. DIČ se nevyžaduje.

### cz_trader_register
- Ustanovení: § 435 odst. 1 věta druhá OZ; § 7 odst. 2 a 3 ZOK.
- Citace: „Podnikatel zapsaný v obchodním rejstříku uvede na obchodní listině též údaj o tomto zápisu včetně oddílu a vložky; podnikatel zapsaný v jiném veřejném rejstříku uvede údaj o svém zápisu do tohoto rejstříku; podnikatel nezapsaný ve veřejném rejstříku uvede údaj o svém zápisu do jiné evidence.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: S. r. o. a a. s. uvedou na webu zápis v obchodním rejstříku (soud, oddíl, vložka). Ostatní podnikatelé ho uvádějí na obchodních listinách; na webu sporné.
- Kde na webu: Kontakt, obchodní podmínky, patička.
- Kontrola: kód
- Kontrola kódem: nejdřív právní forma (cz_trader_name). U s. r. o. a a. s. vyžadovat `(?i)(?:zaps[aá]n\w*\s+(?:v|do)\s+obchodní\w*\s+rejstřík\w*|spisov\w+\s+značk\w+|sp\.\s?zn\.|oddíl\w*\s+[A-Z]{1,2}\b[^.]{0,40}vložk\w*\s+\d+)`. U fyzických osob jen informativně `(?i)živnostensk\w+\s+rejstřík`.
- Skládání: `site_regex`, jen když kód najde právní formu s. r. o. nebo a. s.
- Závažnost (návrh): medium
- Poznámky / nejistoty: Web není obchodní listina; povinnost na webu se opírá o § 7 ZOK, a ten míří jen na a. s. a s. r. o. U jiných forem nenahlašovat, nebo jen low „k ověření“.

### cz_trader_phone
- Ustanovení: § 1820 odst. 1 písm. c) OZ; souběžně § 1811 odst. 2 písm. a) OZ.
- Citace: „c) adresu sídla, telefonní číslo a adresu pro doručování elektronické pošty,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést telefonní číslo. V § 1820 odst. 1 písm. c) je telefon bez výhrady, kontaktní formulář ani chat ho nenahradí.
- Kde na webu: Kontakt, obchodní podmínky, patička.
- Kontrola: kód
- Kontrola kódem: `(?:\+|00)420[\s\u00a0]?\d{3}[\s\u00a0]?\d{3}[\s\u00a0]?\d{3}` nebo `\b[2-9]\d{2}[\s\u00a0]?\d{3}[\s\u00a0]?\d{3}\b` v blízkosti slov tel., telefon, mobil, volejte; navíc odkazy `tel:` z HTML (extrakce je dnes nezachytává). Čísla úřadů v odstavci o ADR vyřadit.
- Skládání: `site_regex` (právní stránky, rám stránky, Kontakt).
- Závažnost (návrh): high
- Poznámky / nejistoty: Číslo v obrázku nástroj nevidí.

### cz_trader_email
- Ustanovení: § 1820 odst. 1 písm. c) OZ; § 1811 odst. 2 písm. a) OZ.
- Citace: „c) adresu sídla, telefonní číslo a adresu pro doručování elektronické pošty,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést e-mailovou adresu. Kontaktní formulář ji nenahrazuje.
- Kde na webu: Kontakt, obchodní podmínky, patička.
- Kontrola: kód
- Kontrola kódem: `[\w.+-]+@[\w-]+(?:\.[\w-]+)+`; obfuskace `(?i)[\w.+-]+\s*(?:\(at\)|\[at\]|\{at\}|\(zavináč\)|\[zavináč\])\s*[\w-]+(?:\.[\w-]+)+`; odkazy `mailto:`; Cloudflare nahrazuje adresu textem „[email protected]“ a atributem `data-cfemail`, který jde dekódovat (XOR) a brát jako přítomnou adresu. Domény úřadů (`coi(\.gov)?\.cz`) vyřadit.
- Skládání: `site_regex`
- Závažnost (návrh): high
- Poznámky / nejistoty: Adresa v obrázku je pro nástroj nečitelná.

### cz_trader_other_channel
- Ustanovení: § 1820 odst. 1 písm. c) OZ, část „případně i údaje o jiném prostředku on-line komunikace“.
- Citace: „případně i údaje o jiném prostředku on-line komunikace, který podnikatel též poskytuje za účelem rychlé a účinné komunikace a který spotřebiteli umožňuje uchovat písemnou komunikaci s podnikatelem v textové podobě, včetně data a času jejího uskutečnění;“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Pokud obchod nabízí další on-line kanál (chat, messenger), který umožňuje uchovat komunikaci s datem a časem, uvede ho.
- Kde na webu: Kontakt.
- Kontrola: nelze z textu. Zda obchod takový kanál poskytuje a jaké má vlastnosti, z textu nepoznáme; chatovací okno je skript, ne text.
- Skládání: informativní (případně detekce skriptů chatu v HTML, bez nálezu).
- Závažnost (návrh): low, neaktivovat
- Poznámky / nejistoty: —

### cz_trader_principal
- Ustanovení: § 1820 odst. 1 písm. c) část za středníkem a písm. d) část druhá OZ; § 5a odst. 3 písm. b) ZOS.
- Citace: „v případě, že podnikatel jedná za jiného podnikatele, také údaje o jeho totožnosti a sídle,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „a v případě, že podnikatel jedná za jiného podnikatele, také adresu, na niž může spotřebitel zaslat stížnost,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když web prodává jménem jiné firmy (zprostředkovatel, obchodní zástupce), uvede i její totožnost, sídlo a adresu pro stížnosti.
- Kde na webu: obchodní podmínky, Kontakt.
- Kontrola: jev + kód (podmíněné)
- Otázky pro Jev:
  - `legal_acts_for_other_trader` — EN: "Does this text say that the goods or services are sold on behalf of another company, or that the shop only acts as an agent or intermediary for another seller?" — CS: „Uvádí tento text, že zboží nebo služby se prodávají jménem jiné firmy, nebo že obchod jedná jen jako zástupce či zprostředkovatel jiného prodávajícího?“
  - `legal_principal_identity` — EN: "Does this text state the name of the company on whose behalf the goods or services are sold? Answer no if the text does not mention selling on behalf of another company." — CS: „Uvádí tento text jméno firmy, jejímž jménem se zboží nebo služby prodávají? Pokud text prodej jménem jiné firmy nezmiňuje, odpověz ne.“
- Kontrola kódem: adresa (PSČ, viz cz_trader_address) v odstavci s `legal_principal_identity`.
- Skládání: `site_presence_if(legal_acts_for_other_trader ≥ 0,7)` → `legal_principal_identity` a adresa.
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: On-line tržiště viz cz_marketplace_seller_status.

### cz_establishment_address
- Ustanovení: § 1820 odst. 1 písm. d) OZ, první část.
- Citace: „d) adresu provozovny, pokud se liší od adresy sídla,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Má-li obchod provozovnu (prodejnu, výdejnu, sklad s osobním odběrem) jinde než v sídle, uvede její adresu.
- Kde na webu: Kontakt, Doprava (osobní odběr).
- Kontrola: nelze z textu. Zda provozovna existuje a kde je, je fakt mimo web. Pomocná otázka jen najde zmínku o prodejně bez adresy.
- Otázky pro Jev (pomocná):
  - `legal_premises_mentioned` — EN: "Does this text mention a shop, showroom, warehouse or pick-up point operated by the seller itself?" — CS: „Zmiňuje tento text prodejnu, showroom, sklad nebo výdejní místo, které provozuje sám prodávající?“
- Skládání: informativní; „k ověření“ jen když je zmínka bez adresy (PSČ).
- Závažnost (návrh): low
- Poznámky / nejistoty: —

### cz_premium_rate_contact
- Ustanovení: § 1820 odst. 1 písm. g) OZ; § 3a ZOS.
- Citace: „g) náklady na prostředky komunikace na dálku, pokud se liší od základní sazby,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Prodávající, který v souvislosti s uzavřenou smlouvou používá pro komunikaci se spotřebitelem veřejnou komunikační službu, nesmí použít takovou službu, jejíž využití by pro spotřebitele znamenalo účtování vyšších cen, než je běžná cena hovoru“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Placená linka musí mít uvedenou cenu. Pro komunikaci k uzavřené smlouvě (reklamace, odstoupení) ji obchod použít nesmí.
- Kde na webu: Kontakt, reklamační řád, patička.
- Kontrola: kód
- Kontrola kódem: čísla s prefixem prémiových služeb `(?:\+420[\s\u00a0]?)?\b90\d[\s\u00a0]?\d{3}[\s\u00a0]?\d{3}\b`; v témže odstavci hledat cenu `(?i)Kč\s*/\s*min`. Seznam prefixů NEOVĚŘENO (ověřit v číslovacím plánu ČTÚ).
- Skládání: `segment_regex`
- Závažnost (návrh): medium
- Poznámky / nejistoty: § 3a je zákaz, ne informační povinnost; je tu kvůli společné detekci. § 3b některé smlouvy vyjímá.

## B. Výrobek a cena

### cz_product_characteristics
- Ustanovení: § 1820 odst. 1 písm. a) OZ; § 5a odst. 3 písm. a) ZOS; souběžně § 1811 odst. 2 písm. b) OZ.
- Citace: „a) údaje o hlavních vlastnostech zboží nebo služby v rozsahu odpovídajícím použitému prostředku komunikace na dálku a povaze zboží nebo služby,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „a) hlavní znaky výrobku nebo služby v rozsahu odpovídajícím danému sdělovacímu prostředku, jakož i výrobku nebo službě,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Každá nabídka popíše, co zboží je a jaké má hlavní vlastnosti (materiál, rozměry, funkce, obsah balení), přiměřeně druhu zboží.
- Kde na webu: každá produktová stránka (hlavní text, JSON-LD `description`).
- Kontrola: jev + kód
- Otázky pro Jev:
  - `product_describes_properties` — EN: "Does the sentence (field sentence) describe a property of the product itself, such as what it is made of, its size, its capacity, its function or what the package contains? Answer no for sentences about price, delivery, payment or the shop." — CS: „Popisuje věta (pole sentence) vlastnost samotného výrobku, například z čeho je vyroben, jeho rozměry, objem, funkci nebo obsah balení? U vět o ceně, dopravě, platbě nebo o obchodě odpověz ne.“
- Kontrola kódem: počet slov popisu (hlavní text bez rámu plus JSON-LD `description`); pod prahem (návrh 15 slov, nastavitelné) je stránka kandidátem.
- Skládání: `page_presence`: stránka je v pořádku, když maximum `product_describes_properties` ≥ 0,7 nebo popis přesáhne práh slov.
- Závažnost (návrh): medium
- Poznámky / nejistoty: Rozsah „hlavních vlastností“ je hodnotový, u jednoduchého zboží může stačit název; proto nejvýš „k ověření“. Popis v cizím jazyce viz cz_language.

### cz_price_total
- Ustanovení: § 1820 odst. 1 písm. e) a § 1811 odst. 2 písm. c) OZ; § 12 ZOS; § 13 odst. 2 a 3 zákona o cenách; § 5a odst. 3 písm. c) ZOS.
- Citace: „c) celkovou cenu zboží nebo služby včetně všech daní, poplatků a jiných obdobných peněžitých plnění, a pokud povaha zboží nebo služby neumožňuje tuto cenu rozumně určit předem, způsob jejího výpočtu,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Prodávající informuje spotřebitele v souladu s cenovými předpisy … o ceně prodávaných výrobků nebo poskytovaných služeb.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „Prodávající je povinen při nabídce a prodeji zboží poskytnout informaci spotřebiteli tak, aby měl spotřebitel možnost seznámit se s cenou v české měně před jednáním o koupi zboží“ (predpisy-cz/cz-526-1990-zakon-o-cenach.txt)
  - „Cenou podle odstavce 2 se rozumí konečná nabídková cena, která zahrnuje všechny daně, poplatky a jiná obdobná peněžitá plnění (dále jen „prodejní cena“). Údaj o prodejní ceně musí být jednoznačný, snadno rozpoznatelný a dobře čitelný.“ (predpisy-cz/cz-526-1990-zakon-o-cenach.txt)
- Požadavek lidsky: U každého zboží konečná cena v korunách včetně DPH a všech poplatků.
- Kde na webu: produktové stránky (text, JSON-LD `offers`).
- Kontrola: kód
- Kontrola kódem: (1) cena v CZK: JSON-LD `offers.price` s `priceCurrency` „CZK“, nebo text `\d{1,3}(?:[\s\u00a0.]\d{3})*(?:,\d{1,2}|,-)?[\s\u00a0]?(?:Kč|CZK)`; (2) nález, když je jediná nebo zvýrazněná cena označena `(?i)bez\s+DPH` a v tomtéž bloku chybí `(?i)(?:s|vč\.?|včetně)\s+DPH`; (3) nález, když jsou ceny jen v cizí měně (`€`, `EUR`).
- Skládání: `page_regex` (cena v CZK na každé produktové stránce) a `segment_regex` (cena jen bez DPH).
- Závažnost (návrh): high
- Poznámky / nejistoty: B2B obchod může legitimně ukazovat ceny bez DPH, proto „k ověření“. Správnost účtování (§ 3 odst. 1 písm. c) ZOS) z textu nelze. Příplatky mimo cenu (`(?i)(?:\+|plus)\s*(?:poplat|příplat)`) jen k ověření.

### cz_unit_price
- Ustanovení: § 13 odst. 4 až 7 zákona o cenách; vyhláška č. 291/2024 Sb.
- Citace: „Stanoví-li tak tento zákon, je prodávající dále povinen při nabídce a prodeji balených výrobků poskytnout spotřebiteli také informaci o ceně za měrnou jednotku množství výrobku (dále jen „měrná cena“).“ (predpisy-cz/cz-526-1990-zakon-o-cenach.txt)
  - „U měrné ceny se jako měrná jednotka množství uvede s ohledem na povahu výrobku 1 kilogram, 1 litr, 1 metr, 1 metr čtvereční nebo 1 metr krychlový výrobku.“ (predpisy-cz/cz-526-1990-zakon-o-cenach.txt)
  - „Povinnost podle odstavce 4 se vztahuje na balené potravinářské výrobky, které jsou v souladu s přímo použitelným předpisem Evropské unie … označeny údajem o množství, objemu nebo hmotnosti výrobku“ (predpisy-cz/cz-526-1990-zakon-o-cenach.txt)
  - „Povinnost označení měrnou cenou se vztahuje na druhy balených nepotravinářských výrobků, které jsou uvedeny v seznamu v příloze k této vyhlášce“ (predpisy-cz/cz-291-2024-vyhlaska-merna-cena.txt)
- Požadavek lidsky: U balených potravin a u nepotravinářského zboží ze seznamu vyhlášky (barvy, stavební chemie, drogerie, kosmetika, prací prostředky, krmiva, oleje, nitě a další) uvést vedle ceny i cenu za 1 kg, 1 l, 1 m, 1 m² nebo 1 m³. Neplatí, je-li měrná cena shodná s prodejní.
- Kde na webu: produktové stránky.
- Kontrola: kód (volitelně jev jako klasifikátor sortimentu)
- Otázky pro Jev (volitelný klasifikátor):
  - `product_packaged_food` — EN: "Does the sentence (field sentence) describe a food or drink product that is sold in a package?" — CS: „Popisuje věta (pole sentence) potravinu nebo nápoj prodávaný v balení?“
- Kontrola kódem: spouštěč = množství v názvu nebo popisu `\b\d+(?:[.,]\d+)?[\s\u00a0]?(?:g|kg|ml|cl|l|m|m2|m²|m3|m³)\b` a zároveň kategorie potravin nebo klíčové slovo ze seznamu vyhlášky 291/2024 (drobečková navigace, JSON-LD `category`). Požadavek = `(?i)(?:Kč|CZK)\s*/\s*(?:1\s*)?(?:kg|l|m|m2|m²|m3|m³|100\s*(?:g|ml))|cena\s+za\s+(?:1\s*)?(?:kg|l|litr|metr|m2|m²|m3|m³)`.
- Skládání: `page_regex_if(spouštěč)`
- Závažnost (návrh): medium
- Poznámky / nejistoty: Výjimky (měrná cena „nevhodná nebo zavádějící“, kombinace výrobků za jednu cenu podle § 2 vyhlášky) kód spolehlivě nepozná, proto „k ověření“. Výklad MF k měrným cenám je v `predpisy-cz/mf-stanovisko-merne-ceny-*.txt` (stáhl jiný rešeršista, nečetl jsem). Seznam klíčových slov z přílohy vyhlášky je potřeba doplnit.

### cz_delivery_costs
- Ustanovení: § 1820 odst. 1 písm. e) a § 1811 odst. 2 písm. e) OZ; § 5a odst. 3 písm. c) ZOS; důsledek § 1821 OZ.
- Citace: „e) náklady na dodání, a pokud tyto náklady nelze stanovit předem, údaj, že mohou být dodatečně účtovány,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Pokud podnikatel spotřebiteli nesdělil údaje o dalších daních, poplatcích a jiných obdobných peněžitých plněních nebo nákladech, které spotřebitel ponese podle § 1820 odst. 1 písm. e) nebo j), není spotřebitel povinen tyto daně, poplatky, jiná obdobná peněžitá plnění nebo náklady podnikateli hradit.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Před objednávkou musí být vidět, kolik stojí doprava, že je zdarma, nebo že se doúčtuje. Co obchod nesdělí, spotřebitel neplatí.
- Kde na webu: Doprava a platba, obchodní podmínky, případně produktová stránka („doprava od 79 Kč“).
- Kontrola: jev + kód
- Otázky pro Jev:
  - `legal_delivery_cost` — EN: "Does this text say how much the customer pays for delivery or shipping, including that delivery is free?" — CS: „Říká tento text, kolik zákazník platí za dopravu nebo doručení, případně že je doprava zdarma?“
- Kontrola kódem: v odstavci s otázkou ≥ 0,7 částka `\d[\d\s\u00a0.]*(?:,\d{1,2}|,-)?[\s\u00a0]?(?:Kč|CZK)` nebo `(?i)zdarma|bezplatn\w*`.
- Skládání: `site_presence` (odstavec, kde platí jev i kód).
- Závažnost (návrh): high
- Poznámky / nejistoty: Ceník vykreslený JavaScriptem nebo zobrazený jen v košíku nástroj neuvidí, proto „k ověření“. ČOI 2. čtvrtletí 2026: písm. e) 10 případů (`coi/coi-kontroly-internet-2Q-2026.txt`).

### cz_subscription_price
- Ustanovení: § 1820 odst. 1 písm. e) OZ, část za středníkem.
- Citace: „v případě smlouvy uzavírané na dobu neurčitou nebo smlouvy, jejímž předmětem je opakované plnění, sdělí tento údaj také za jedno zúčtovací období, kterým je vždy jeden měsíc, pokud je tato cena neměnná,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: U předplatného a opakovaných dodávek uvést i cenu za měsíc.
- Kde na webu: stránka předplatného, obchodní podmínky, produkt s předplatným.
- Kontrola: jev + kód (podmíněné)
- Otázky pro Jev:
  - `legal_subscription` — EN: "Does this text describe a subscription, or a contract under which goods or services are supplied repeatedly for recurring payments?" — CS: „Popisuje tento text předplatné nebo smlouvu, podle které se zboží či služby dodávají opakovaně za opakované platby?“
- Kontrola kódem: `(?i)(?:Kč|CZK)\s*(?:/|za)\s*(?:1\s*)?měs\w*|měsíčně`.
- Skládání: `site_presence_if(legal_subscription ≥ 0,7)` → kód kdekoli na webu.
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: Spouštěč sdílí cz_contract_duration a cz_minimum_duration.

### cz_price_personalization
- Ustanovení: § 1820 odst. 1 písm. f) OZ.
- Citace: „f) údaj o přizpůsobení ceny osobě spotřebitele na základě automatizovaného rozhodování, byla-li cena takto přizpůsobena,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když obchod cenu přizpůsobuje konkrétnímu zákazníkovi algoritmem, musí to sdělit.
- Kde na webu: u ceny, obchodní podmínky.
- Kontrola: nelze z textu. Zda je cena personalizovaná, je fakt mimo web; crawler vidí jen jednu verzi ceny. Pomocná otázka zjistí jen to, zda web o personalizaci mluví.
- Otázky pro Jev (pomocná, informativní):
  - `legal_personalized_price` — EN: "Does this text say that prices may be personalised for individual customers based on automated decision-making or profiling?" — CS: „Uvádí tento text, že ceny mohou být jednotlivým zákazníkům přizpůsobeny na základě automatizovaného rozhodování nebo profilování?“
- Skládání: informativní
- Závažnost (návrh): neaktivovat
- Poznámky / nejistoty: —

### cz_discount_prior_price
- Ustanovení: § 12a ZOS.
- Citace: „(1) Informace o slevě z ceny výrobku obsahuje informaci o nejnižší ceně výrobku, za kterou jej prodávající nabízel a prodával“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „a) v době 30 dnů před poskytnutím slevy,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „(2) Odstavec 1 se nepoužije pro výrobky, které podléhají rychlé zkáze, nebo pro výrobky s krátkou dobou spotřeby.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: U každé slevy uvést nejnižší cenu za 30 dnů před slevou (u zboží v prodeji kratší dobu od začátku prodeje, u postupné slevy před první slevou).
- Kde na webu: produktové stránky, výpisy kategorií, bannery akcí.
- Kontrola: kód (jev jako spouštěč pro neobvyklé formulace)
- Otázky pro Jev (spouštěč):
  - `product_price_advantage` — EN: "Does the sentence (field sentence) present a price reduction, discount, saving or special price for the product (for example 'sale', 'save 20 %', 'super price', 'price with the app')?" — CS: „Prezentuje věta (pole sentence) snížení ceny, slevu, úsporu nebo zvýhodněnou cenu výrobku (např. ‚výprodej‘, ‚ušetříte 20 %‘, ‚super cena‘, ‚cena s aplikací‘)?“
- Kontrola kódem: spouštěč na stránce `(?i)\b(?:sleva|zlevněn\w*|výprodej|ušetří\w*|akční\s+cena|původní\s+cena|běžná\s+cena|dříve\s+za|doporučená\s+cena)\b|-\s?\d{1,2}\s?%`; požadavek ve stejném bloku nebo na stránce `(?i)nejnižší\s+cen\w*[^.]{0,80}?30\s*dn|30\s*dn\w*[^.]{0,80}?nejnižší\s+cen`. Volitelně kontrola výpočtu: je-li uvedena sleva X %, nejnižší cena L a aktuální cena P, porovnat X s (L − P) / L; odchylka nad 1 procentní bod = „k ověření“.
- Skládání: `page_regex_if(spouštěč kódem nebo product_price_advantage ≥ 0,7)`
- Závažnost (návrh): high
- Poznámky / nejistoty: Zda je uvedená cena opravdu nejnižší, z textu nelze (chybí historie cen). Výjimka odst. 2 (rychlá zkáza) → potraviny „k ověření“. ČOI popisuje obcházení přes „SUPER cena“, „cena s aplikací“ a „doporučená cena“ (`coi/coi-kontroly-slevy-rok-2025.txt`). § 13 odst. 11 zákona o cenách ukládá výrobci označit doporučenou cenu jako „nezávazná doporučená spotřebitelská cena“; zda to dopadá i na e-shop, který ji přebírá, NEOVĚŘENO.

## C. Platba, dodání a stížnosti

### cz_payment_methods
- Ustanovení: § 1820 odst. 1 písm. h) OZ; § 11a ZOS; souběžně § 1811 odst. 2 písm. d) OZ.
- Citace: „h) způsob platby, způsob a čas dodání nebo plnění a případně pravidla vyřizování stížností,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Při prodeji výrobků nebo poskytování služeb elektronickými prostředky prostřednictvím internetových stránek je prodávající povinen spotřebitele zřetelným způsobem informovat nejpozději na začátku objednávky o tom, … jaké způsoby platby jsou přijímány.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Uvést přijímané způsoby platby, nejpozději na začátku objednávky.
- Kde na webu: Doprava a platba, obchodní podmínky, patička (loga platebních metod jsou obrázky).
- Kontrola: jev (kód jako podpůrný signál)
- Otázky pro Jev:
  - `legal_payment_methods` — EN: "Does this text state which payment methods the shop accepts, for example card, bank transfer or cash on delivery?" — CS: „Uvádí tento text, jaké způsoby platby obchod přijímá, například kartou, převodem nebo na dobírku?“
- Kontrola kódem (podpůrná): seznam `(?i)dobírk|bankovní\w*\s+převod|převodem|platební\w*\s+kart|kartou|v\s+hotovosti|hotově|GoPay|Comgate|ThePay|PayPal|Apple\s?Pay|Google\s?Pay|QR\s+platb|splátk|Twisto|Skip\s?Pay` (návrh, doplnit).
- Skládání: `site_presence`
- Závažnost (návrh): medium
- Poznámky / nejistoty: Požadavek „nejpozději na začátku objednávky“ (umístění v procesu) z textu nelze ověřit, nástroj košík neprochází. ČOI 2025: písm. h) 46 případů.

### cz_delivery_methods
- Ustanovení: § 1820 odst. 1 písm. h) OZ („způsob … dodání“).
- Citace: „h) způsob platby, způsob a čas dodání nebo plnění a případně pravidla vyřizování stížností,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést, jak se zboží doručuje (dopravce, výdejní místa, osobní odběr).
- Kde na webu: Doprava a platba, obchodní podmínky.
- Kontrola: jev (kód jako podpůrný signál)
- Otázky pro Jev:
  - `legal_delivery_methods` — EN: "Does this text state how the goods are delivered, for example by which carrier, to a pick-up point or by personal collection?" — CS: „Uvádí tento text, jak se zboží doručuje, například kterým dopravcem, na výdejní místo nebo osobním odběrem?“
- Kontrola kódem (podpůrná): `(?i)\b(?:PPL|DPD|GLS|DHL|Zásilkovn\w*|Packeta|Česk\w+\s+pošt\w*|Balíkovn\w*|WEDO|Geis|Toptrans|Z-BOX|AlzaBox|osobní\w*\s+odběr\w*|výdejní\w*\s+míst\w*)`.
- Skládání: `site_presence`
- Závažnost (návrh): medium
- Poznámky / nejistoty: —

### cz_delivery_time
- Ustanovení: § 1820 odst. 1 písm. h) OZ („čas dodání“); § 2159 odst. 1 OZ.
- Citace: „h) způsob platby, způsob a čas dodání nebo plnění a případně pravidla vyřizování stížností,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(1) Není-li ujednán čas plnění, prodávající odevzdá věc kupujícímu bez zbytečného odkladu po uzavření smlouvy, nejpozději však do třiceti dnů.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést, kdy zboží dorazí nebo odejde (lhůta expedice či doručení).
- Kde na webu: Doprava a platba, obchodní podmínky, produktová stránka (dostupnost „skladem, u vás do 2 dnů“).
- Kontrola: jev + kód (lhůtu čte vždy kód)
- Otázky pro Jev:
  - `legal_delivery_time` — EN: "Does this text say when or how quickly the goods will be dispatched or delivered?" — CS: „Uvádí tento text, kdy nebo jak rychle bude zboží odesláno či doručeno?“
- Kontrola kódem: `(?i)(?:do|za|během)\s+\d{1,2}(?:\s*[–-]\s*\d{1,2})?\s*(?:pracovní\w*\s+)?(?:dn[íůy]|den|hodin\w*|týdn\w*)` nebo `(?i)\b(?:skladem|expedujeme|odesíláme|doručíme)\b`; uvedená lhůta nad 30 dnů = „k ověření“ (§ 2159 odst. 1).
- Skládání: `site_presence` (jev nebo kód na právních stránkách); kód na produktových stránkách jen informativně.
- Závažnost (návrh): medium
- Poznámky / nejistoty: ČOI 2025: písm. h) 46 případů (společně se způsobem platby a dodání).

### cz_delivery_restrictions
- Ustanovení: § 11a ZOS.
- Citace: „je prodávající povinen spotřebitele zřetelným způsobem informovat nejpozději na začátku objednávky o tom, zda platí nějaká omezení pro dodání výrobků nebo poskytnutí služby“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Říct, zda platí omezení dodání (země, oblasti, zboží, které nejde doručit všude).
- Kde na webu: Doprava a platba, obchodní podmínky.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_delivery_restrictions` — EN: "Does this text state limits on delivery, such as the countries or areas the shop delivers to, or goods that cannot be delivered to some places?" — CS: „Uvádí tento text omezení dodání, například do kterých zemí či oblastí obchod doručuje, nebo zboží, které nelze někam doručit?“
- Skládání: `site_presence`, nález nejvýš „k ověření“.
- Závažnost (návrh): medium
- Poznámky / nejistoty: Zákon chce informaci o tom, „zda“ omezení platí; věta „doručujeme po celé ČR“ ji splňuje. Umístění „na začátku objednávky“ z textu nelze ověřit.

### cz_cross_border_delivery
- Ustanovení: čl. 7 nařízení (EU) 2018/644; § 24 odst. 6 písm. b) ZOS (přestupek).
- Citace: „… všichni obchodníci, kteří se spotřebiteli uzavřou kupní smlouvu zahrnující zasílání přeshraničních balíků, zpřístupní tam, kde je to možné a relevantní, v předsmluvní fázi informace o tom, jaké možnosti přeshraničního dodání jsou u dané kupní smlouvy nabízeny“ (predpisy-eu/eu-2018-644-preshranicni-dodani-baliku-cs.txt)
  - „dále informace o cenách za přeshraniční dodání balíku hrazených spotřebiteli a případně informace o svých postupech pro řešení stížností.“ (predpisy-eu/eu-2018-644-preshranicni-dodani-baliku-cs.txt)
  - „b) nezpřístupní informace podle čl. 7 nařízení Evropského parlamentu a Rady (EU) 2018/644.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Obchod, který posílá do zahraničí (typicky na Slovensko), uvede možnosti a ceny přeshraniční dopravy.
- Kde na webu: Doprava a platba.
- Kontrola: jev + kód (podmíněné)
- Otázky pro Jev:
  - `legal_ships_abroad` — EN: "Does this text say that the shop delivers to countries other than the Czech Republic?" — CS: „Uvádí tento text, že obchod doručuje i do jiných zemí než do České republiky?“
- Kontrola kódem: v odstavcích s `legal_ships_abroad` ≥ 0,7 částka v Kč nebo EUR, případně „zdarma“.
- Skládání: `site_presence_if(legal_ships_abroad ≥ 0,7)`
- Závažnost (návrh): low
- Poznámky / nejistoty: „Tam, kde je to možné a relevantní“: nález jen „k ověření“.

### cz_complaint_handling
- Ustanovení: § 1820 odst. 1 písm. h) OZ („případně pravidla vyřizování stížností“); § 1811 odst. 2 písm. d) OZ.
- Citace: „d) způsob platby, způsob a čas dodání nebo plnění a případně pravidla vyřizování stížností,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Pokud má obchod pravidla pro vyřizování stížností (na služby a jednání, ne na vadné zboží), sdělí je.
- Kde na webu: obchodní podmínky, Kontakt.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_complaint_rules` — EN: "Does this text explain how the shop handles customer complaints about its service or conduct, as distinct from claims for defective goods?" — CS: „Vysvětluje tento text, jak obchod vyřizuje stížnosti zákazníků na své služby nebo jednání, na rozdíl od reklamací vadného zboží?“
- Skládání: `site_presence`, nález nejvýš „k ověření“.
- Závažnost (návrh): low
- Poznámky / nejistoty: Slovo „případně“: kdy je informace povinná, je nejisté. Nezaměňovat s ADR (cz_adr).

### cz_advance_payment
- Ustanovení: § 1820 odst. 1 písm. q) OZ.
- Citace: „q) údaj o povinnosti zaplatit zálohu nebo obdobnou platbu, je-li vyžadována, a o jejích podmínkách,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když obchod chce zálohu (zakázková výroba, drahé zboží), uvede to i podmínky zálohy.
- Kde na webu: obchodní podmínky, Doprava a platba, produktová stránka zakázkového zboží.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `legal_deposit_required` — EN: "Does this text say that the customer must pay a deposit or advance payment before the goods are made or delivered?" — CS: „Uvádí tento text, že zákazník musí před výrobou nebo dodáním zboží zaplatit zálohu?“
  - `legal_deposit_conditions` — EN: "Does this text state the conditions of the deposit, for example whether and when it is refunded? Answer no if no deposit is mentioned." — CS: „Uvádí tento text podmínky zálohy, například zda a kdy se vrací? Pokud text zálohu nezmiňuje, odpověz ne.“
- Skládání: `segment`: nález „k ověření“, když v odstavci platí `legal_deposit_required` ≥ 0,7 a `legal_deposit_conditions` < 0,5.
- Závažnost (návrh): low
- Poznámky / nejistoty: Zda je zálohou i platba celé ceny předem převodem, ověřit. Výši zálohy čte kód.

## D. Odstoupení od smlouvy

### cz_withdrawal (EXISTUJÍCÍ: legal_withdrawal_missing)
- Ustanovení: § 1820 odst. 1 písm. i) OZ; § 1829 odst. 1 OZ; alternativně § 1820 odst. 2 OZ a NV č. 29/2023 Sb. (vzorové poučení); důsledek § 1829 odst. 4 OZ.
- Citace: „i) podmínky, lhůtu a postup pro uplatnění práva na odstoupení od smlouvy, jakož i vzorový formulář pro odstoupení od smlouvy, pokud lze tohoto práva využít; náležitosti vzorového formuláře stanoví prováděcí právní předpis,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(1) Spotřebitel může odstoupit od smlouvy uzavřené distančním způsobem nebo od smlouvy uzavřené mimo obchodní prostory ve lhůtě čtrnácti dnů.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(2) Podnikatel splní povinnost sdělit údaje podle odstavce 1 písm. i) až k) také tehdy, poskytne-li spotřebiteli vyplněné vzorové poučení o možnosti odstoupení od smlouvy, jehož náležitosti stanoví prováděcí právní předpis.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Nebyl-li spotřebitel poučen o právu odstoupit od smlouvy podle § 1820 odst. 1 písm. i), může od smlouvy odstoupit do jednoho roku ode dne uplynutí lhůty podle odstavce 1, 2 nebo 3.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Do 14 [1] dnů máte právo odstoupit od této smlouvy bez udání důvodu.“ (predpisy-cz/cz-29-2023-nv-vzorove-pouceni-a-formular-odstoupeni.txt)
- Požadavek lidsky: Podmínky, 14denní lhůta a postup pro odstoupení.
- Kde na webu: obchodní podmínky, stránka Odstoupení nebo Vrácení zboží.
- Kontrola: jev + kód. EXISTUJE: otázka `legal_withdrawal` a `regex_required` na „14 dnů“. Nepsat znovu.
- Návrh doplnění (bez přepisu existujícího): (a) kód rozpozná vyplněné vzorové poučení podle NV 29/2023 (`(?i)do\s+14\s+dnů\s+máte\s+právo\s+odstoupit\s+od\s+této\s+smlouvy\s+bez\s+udání\s+důvodu`) i starší variantu podle NV 363/2013 (`(?i)máte\s+právo\s+odstoupit\s+od\s+smlouvy\s+bez\s+udání\s+důvodu\s+ve\s+lhůtě\s+14\s+dnů`); podle § 1820 odst. 2 tím obchod splní písm. i) až k). (b) Existující otázka se ptá na tři znaky najednou (právo, lhůta, postup); podle poučení z testů zvážit rozpad. (c) Správnost lhůty řeší cz_withdrawal_period_start a příloha A.
- Skládání: `site_presence` (existující)
- Závažnost (návrh): high (existující)
- Poznámky / nejistoty: ČOI 2025: písm. i) 240 případů, nejčastější porušení OZ. Bez poučení se lhůta prodlužuje o rok (§ 1829 odst. 4) a spotřebitel neodpovídá za snížení hodnoty zboží (§ 1833 věta druhá). Potvrzení přijetí odstoupení odeslaného formulářem na webu (§ 1830 odst. 2) je proces, z textu nelze.

### cz_withdrawal_form (EXISTUJÍCÍ: legal_withdrawal_form_missing)
- Ustanovení: § 1820 odst. 1 písm. i) OZ; příloha písm. b) NV č. 29/2023 Sb. ve znění NV č. 66/2026 Sb.
- Citace: „jakož i vzorový formulář pro odstoupení od smlouvy, pokud lze tohoto práva využít;“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „- Adresát [zde podnikatel vloží jméno a příjmení/obchodní firmu/název, adresu sídla a svou adresu pro doručování elektronické pošty]:“ (predpisy-cz/cz-29-2023-nv-vzorove-pouceni-a-formular-odstoupeni.txt)
  - „- Oznamuji/oznamujeme*), že tímto odstupuji/odstupujeme*) od smlouvy o koupi tohoto zboží*)/o poskytnutí těchto služeb*)“ (predpisy-cz/cz-29-2023-nv-vzorove-pouceni-a-formular-odstoupeni.txt)
- Požadavek lidsky: Poskytnout vzorový formulář pro odstoupení.
- Kde na webu: obchodní podmínky, stránka Odstoupení, odkaz na PDF nebo DOCX.
- Kontrola: jev. EXISTUJE: otázka `legal_withdrawal_form`. Nepsat znovu.
- Návrh doplnění kódem: formulář v textu poznat podle znaků NV: `(?i)oznamuji\s*/\s*oznamujeme`, `(?i)datum\s+objednání\s*/\s*datum\s+obdržení`, `(?i)jméno\s+a\s+příjmení\s+spotřebitele`, `(?i)adresa\s+spotřebitele`, `(?i)podpis\s+spotřebitele`; aspoň 3 z 5 = formulář přítomen. Odkaz na PDF nebo DOCX s formulářem = poznámka „může být v nezkontrolovaném dokumentu“ (už to dělá obecné pravidlo pro PDF).
- Skládání: `site_presence` (existující)
- Závažnost (návrh): medium (existující)
- Poznámky / nejistoty: NV 363/2013 je zrušené od 18. 2. 2023; starší formulář („od smlouvy o nákupu tohoto zboží“) je pořád formulářem, nehlásit jako chybějící. Nový vzor chce u adresáta i e-mail podnikatele (nízká priorita).

### cz_withdrawal_period_start
- Ustanovení: § 1829 odst. 1 věta druhá a odst. 2 OZ; § 1820 odst. 1 písm. i) OZ („lhůtu“).
- Citace: „Není-li dále stanoveno jinak, končí lhůta uplynutím čtrnácti dnů ode dne uzavření smlouvy.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(2) Je-li předmětem závazku koupě zboží, končí lhůta uplynutím čtrnácti dnů ode dne, kdy spotřebitel nebo jím určená třetí osoba odlišná od dopravce … zboží, nebo“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: U zboží běží 14 dnů od převzetí (u více kusů od převzetí posledního). Informace o lhůtě tomu musí odpovídat.
- Kde na webu: obchodní podmínky, stránka Odstoupení.
- Kontrola: jev + kód
- Otázky pro Jev:
  - `legal_withdrawal_from_receipt` — EN: "Does this text say that for goods the withdrawal period is counted from the day the consumer, or a person they designate, receives the goods?" — CS: „Uvádí tento text, že u zboží se lhůta pro odstoupení počítá ode dne, kdy zboží převezme spotřebitel nebo jím určená osoba?“
- Kontrola kódem: chybný začátek lhůty `(?i)(?:14|čtrnáct\w*)\s*-?\s*(?:dn\w*|den)\s+(?:od|ode)\s+(?:dne\s+)?(?:objednán\w*|nákup\w*|zakoupen\w*|uzavřen\w*|zaplacen\w*|odeslán\w*|expedic\w*|vystaven\w*)` = „k ověření“ (u služeb a digitálního obsahu je „od uzavření smlouvy“ správně).
- Skládání: `site_presence` (`legal_withdrawal_from_receipt`, jen u obchodů se zbožím) a `segment_regex`.
- Závažnost (návrh): medium
- Poznámky / nejistoty: V souboru OZ stojí v odst. 2 „převezeme“ (patrně chyba přepisu na zakonyprolidi.cz, věcně „převezme“); v citaci vynecháno, ověřit v oficiálním znění. Vzorové poučení NV 29/2023, pokyn [2] písm. b), formuluje totéž.

### cz_withdrawal_return_costs
- Ustanovení: § 1820 odst. 1 písm. j) OZ; důsledek § 1832 odst. 3 OZ.
- Citace: „j) údaj, že v případě odstoupení od smlouvy ponese spotřebitel náklady spojené s vrácením zboží, a jde-li o smlouvu uzavřenou prostřednictvím prostředku komunikace na dálku, výši nákladů spojených s vrácením zboží, nemůže-li být pro svou povahu vráceno obvyklou poštovní cestou,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(3) Podnikatel uhradí spotřebiteli náklady spojené s vrácením zboží, jestliže neupozornil spotřebitele na povinnost nést tyto náklady v souladu s ustanovením § 1820 odst. 1 písm. j).“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Říct, že vrácení zboží platí spotřebitel (jinak ho platí obchod). U nadrozměrného zboží, které nejde poslat poštou, uvést i výši nákladů.
- Kde na webu: obchodní podmínky, stránka Odstoupení nebo Vrácení zboží.
- Kontrola: jev + kód
- Otázky pro Jev:
  - `legal_return_cost_payer` — EN: "Does this text say who pays the cost of sending the goods back when the consumer withdraws from the contract?" — CS: „Uvádí tento text, kdo platí náklady na vrácení zboží, když spotřebitel od smlouvy odstoupí?“
  - `legal_nonpostal_goods` — EN: "Does this text say that some goods cannot be returned by ordinary post, for example bulky goods that must be sent by freight or a special carrier?" — CS: „Uvádí tento text, že některé zboží nelze vrátit běžnou poštou, například nadrozměrné zboží, které se musí poslat nákladní nebo speciální dopravou?“
- Kontrola kódem: je-li `legal_nonpostal_goods` ≥ 0,7, hledat v témže odstavci částku `\d[\d\s\u00a0.]*(?:,\d{1,2})?[\s\u00a0]?(?:Kč|CZK)`.
- Skládání: `site_presence` (`legal_return_cost_payer`); `segment` (nadrozměrné zboží bez částky = „k ověření“).
- Závažnost (návrh): high
- Poznámky / nejistoty: ČOI mezi opakovanými nedostatky jmenuje informace o nákladech na vrácení zboží (`coi/coi-kontroly-internet-rok-2025.txt`). Nadrozměrné zboží pozná i sortiment (produktové stránky s „dopravou paletou“ nebo „spedicí“) — možný další spouštěč.

### cz_withdrawal_service_payment
- Ustanovení: § 1820 odst. 1 písm. k) OZ.
- Citace: „k) údaj, že při odstoupení od smlouvy po předložení žádosti o započetí plnění již v průběhu lhůty pro odstoupení podle § 1824a odst. 3 nebo podle § 1828 odst. 5 musí spotřebitel podnikateli poskytnout úhradu podle § 1834,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: U služeb začatých na žádost spotřebitele už během lhůty pro odstoupení (a u dodávek energií) informovat, že při odstoupení zaplatí poměrnou část.
- Kde na webu: obchodní podmínky obchodů se službami (montáž, instalace, předplacené služby).
- Kontrola: jev (podmíněné; u čistého prodeje zboží se nevztahuje)
- Otázky pro Jev:
  - `legal_services_offered` — EN: "Does this text describe services (for example installation, repair or a subscription service) that the customer orders from the shop, rather than only the sale of goods?" — CS: „Popisuje tento text služby (například montáž, opravu nebo předplacenou službu), které si zákazník od obchodu objednává, a ne jen prodej zboží?“
  - `legal_early_service_payment` — EN: "Does this text say that a consumer who asked for a service to start during the withdrawal period and then withdraws must pay for the part of the service already provided?" — CS: „Uvádí tento text, že spotřebitel, který požádal o zahájení služby ve lhůtě pro odstoupení a pak odstoupí, zaplatí za již poskytnutou část služby?“
- Skládání: `site_presence_if(legal_services_offered ≥ 0,7)` → `legal_early_service_payment`.
- Závažnost (návrh): low
- Poznámky / nejistoty: —

### cz_withdrawal_exceptions
- Ustanovení: § 1820 odst. 1 písm. l) OZ; § 1837 OZ (výjimky).
- Citace: „l) údaj, že spotřebitel nemá právo odstoupit od smlouvy, je-li tomu tak, nebo údaj o tom, za jakých podmínek mu právo na odstoupení od smlouvy zanikne,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „d) o dodávce zboží vyrobeného podle požadavků spotřebitele nebo přizpůsobeného jeho osobním potřebám,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „g) o dodávce zboží v zapečetěném obalu, které z důvodu ochrany zdraví nebo z hygienických důvodů není vhodné vrátit poté, co jej spotřebitel porušil,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když na část sortimentu odstoupení neplatí (hygienické zboží v zapečetěném obalu, zakázková výroba, rychle se kazící zboží, digitální obsah), obchod to musí uvést.
- Kde na webu: obchodní podmínky, stránka Odstoupení, případně produktová stránka takového zboží.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_withdrawal_exceptions` — EN: "Does this text list cases in which the consumer cannot withdraw from the contract or loses the right to withdraw, for example sealed hygiene goods, goods made to the customer's specifications or perishable goods?" — CS: „Uvádí tento text případy, kdy spotřebitel nemůže od smlouvy odstoupit nebo kdy právo na odstoupení ztrácí, například u hygienického zboží v zapečetěném obalu, zboží vyrobeného na zakázku nebo rychle se kazícího zboží?“
- Skládání: `site_presence`, nález nejvýš „k ověření“ (povinné jen tehdy, když sortiment výjimky obsahuje).
- Závažnost (návrh): medium
- Poznámky / nejistoty: ČOI 2025: písm. l) 47 případů. Příliš široké výjimky („rozbalené zboží nelze vrátit“) jsou naopak v rozporu se zákonem, viz příloha A.

### cz_withdrawal_button (od 1. 1. 2027)
- Ustanovení: § 1820 odst. 1 písm. i) a § 1830a OZ ve znění zákona č. 159/2026 Sb., účinnost 1. 1. 2027; pokyn [4] přílohy NV 29/2023 ve znění NV 66/2026 (od 19. 6. 2026).
- Citace: „a v případě smlouvy uzavírané distančním způsobem prostřednictvím on-line rozhraní i údaje o možnosti odstoupit od smlouvy také použitím tlačítka nebo obdobného ovládacího prvku pro odstoupení od smlouvy a o jejich umístění;“ (predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt)
  - „Tlačítko nebo obdobný ovládací prvek pro odstoupení od smlouvy musí být v on-line rozhraní zobrazeny výrazným způsobem, snadno přístupné, dostupné nepřetržitě po celou lhůtu pro odstoupení od smlouvy a musí být označeny snadno čitelným nápisem „Odstoupit od smlouvy“ …“ (predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt)
  - „Tento zákon nabývá účinnosti dnem 1. ledna 2027.“ (predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt)
- Požadavek lidsky: Od 1. 1. 2027 musí e-shop umožnit odstoupení tlačítkem „Odstoupit od smlouvy“, výrazným a dostupným po celou lhůtu, a v informacích před uzavřením smlouvy říct, že tlačítko existuje a kde je.
- Kde na webu: celý web (patička, účet zákazníka, detail objednávky), obchodní podmínky.
- Kontrola: jev + kód
- Otázky pro Jev:
  - `legal_withdrawal_online_option` — EN: "Does this text say that the consumer can withdraw from the contract online, using a button, link or form on the website?" — CS: „Uvádí tento text, že spotřebitel může od smlouvy odstoupit online pomocí tlačítka, odkazu nebo formuláře na webu?“
  - `legal_withdrawal_button_location` — EN: "Does this text say where on the website the withdrawal button or form can be found, for example a web address, a page name or a menu item? Answer no if no such button or form is mentioned." — CS: „Uvádí tento text, kde na webu lze tlačítko nebo formulář pro odstoupení najít, například webovou adresu, název stránky nebo položku menu? Pokud text takové tlačítko ani formulář nezmiňuje, odpověz ne.“
- Kontrola kódem: text odkazu nebo tlačítka (`a`, `button`, `input[type=submit]`) `(?i)^\s*odstoupit\s+od\s+smlouvy\s*$` nebo blízká jednoznačná formulace, na kterékoli stažené stránce, ideálně v rámu stránky. Vyžaduje, aby extrakce ukládala texty odkazů a tlačítek zvlášť.
- Skládání: `site_regex` (odkaz nebo tlačítko) a `site_presence` (obě otázky). Aktivovat od 1. 1. 2027, do té doby informativně.
- Závažnost (návrh): high od 1. 1. 2027
- Poznámky / nejistoty: Tlačítko může být jen v přihlášeném účtu, kam crawler nevidí → nález nejvýš „k ověření“. Potvrzovací tlačítko „Potvrdit odstoupení od smlouvy“ (§ 1830a odst. 4) a potvrzení e-mailem (odst. 5) jsou proces, z textu nelze. Slovenská obdoba je § 20a zákona 108/2024 Z. z. (jiný rešeršista).

## E. Vady, reklamace a záruky

### cz_complaints (EXISTUJÍCÍ: legal_complaints_missing)
- Ustanovení: § 13 odst. 1 ZOS; § 1820 odst. 1 písm. m) OZ (první část); souběžně § 1811 odst. 2 písm. f) OZ.
- Citace: „(1) Prodávající je povinen spotřebitele řádně informovat o rozsahu, podmínkách a způsobu uplatnění práva z vadného plnění (dále jen „reklamace“), spolu s údaji o tom, kde lze reklamaci uplatnit.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „m) údaj o existenci práv z vadného plnění, případně také o záruce za jakost, poprodejním servisu a jejich podmínkách,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Rozsah, podmínky a způsob reklamace a kde ji lze uplatnit.
- Kde na webu: reklamační řád, obchodní podmínky.
- Kontrola: jev. EXISTUJE: otázka `legal_complaints`. Nepsat znovu.
- Návrh doplnění: existující otázka spojuje tři znaky („jak, kde a za jakých podmínek“). Rozpad podle poučení z testů: místo uplatnění (cz_complaint_place), doba pro vytknutí vady (cz_defect_period, kód) a volitelně rozsah práv:
  - `legal_defect_remedies` — EN: "Does this text list what the customer can ask for when goods are defective, such as repair, replacement, a price reduction or withdrawal from the contract?" — CS: „Uvádí tento text, co může zákazník u vadného zboží požadovat, například opravu, výměnu, slevu nebo odstoupení od smlouvy?“
- Skládání: `site_presence` (existující)
- Závažnost (návrh): high (existující)
- Poznámky / nejistoty: ČOI 2025: § 13 nejčastější porušení ZOS, 363 případů.

### cz_complaint_place
- Ustanovení: § 13 odst. 1 ZOS („kde lze reklamaci uplatnit“); § 2172 OZ.
- Citace: „spolu s údaji o tom, kde lze reklamaci uplatnit.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „Vadu lze vytknout prodávajícímu, u kterého věc byla koupena.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést konkrétní místo, kde lze reklamovat (adresa, provozovna, e-mail nebo formulář).
- Kde na webu: reklamační řád, obchodní podmínky.
- Kontrola: jev + kód (doplněk k existující kontrole)
- Otázky pro Jev:
  - `legal_complaint_where` — EN: "Does this text say where a complaint about defective goods can be made, for example at a stated address, in a shop, by e-mail or through an online form?" — CS: „Uvádí tento text, kde lze reklamovat vadné zboží, například na uvedené adrese, v prodejně, e-mailem nebo online formulářem?“
- Kontrola kódem: v odstavci s otázkou ≥ 0,7 adresa (PSČ) nebo e-mail.
- Skládání: `site_presence`
- Závažnost (návrh): high
- Poznámky / nejistoty: Odkaz „reklamaci řešte s výrobcem nebo servisem“ bez místa u prodávajícího je v rozporu s § 2172 (jiná osoba jen, je-li určena k opravě a je v místě prodávajícího nebo bližším) → kandidát do přílohy A.

### cz_defect_period
- Ustanovení: § 2165 odst. 1 OZ; § 2168 OZ.
- Citace: „(1) Kupující může vytknout vadu, která se na věci projeví v době dvou let od převzetí.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Při koupi použité věci mohou strany zkrátit dobu podle § 2165 až na jeden rok.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Informace o rozsahu a podmínkách reklamace má obsahovat dobu 24 měsíců (u použitého zboží případně 12).
- Kde na webu: reklamační řád, obchodní podmínky, produktové stránky („záruka“).
- Kontrola: kód
- Kontrola kódem: přítomnost `(?i)24\s*měsíc\w*|dvou\s+let|2\s+let|dva\s+roky|2\s+roky` v textu o reklamacích; kratší doba u nového zboží `(?i)(?:záruk\w*|reklamac\w*|vad\w*)[^.]{0,60}?\b(?:6|12)\s*měsíc\w*` = „k ověření“ (může jít o použité zboží nebo o dobrovolnou záruku na díl).
- Skládání: `site_regex` (informativně) a `segment_regex` (nález).
- Závažnost (návrh): medium
- Poznámky / nejistoty: Označení „záruka 24 měsíců“ pro zákonná práva z vadného plnění je v praxi běžné; samotné slovo „záruka“ nehlásit (výklad nejistý).

### cz_guarantee_after_sales
- Ustanovení: § 1820 odst. 1 písm. m) OZ, část „případně také o záruce za jakost, poprodejním servisu a jejich podmínkách“; § 2174a OZ (záruční list).
- Citace: „m) údaj o existenci práv z vadného plnění, případně také o záruce za jakost, poprodejním servisu a jejich podmínkách,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(1) Poskytovatel záruky vydá kupujícímu nejpozději při převzetí věci potvrzení o záruce za jakost (záruční list) v textové podobě.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když obchod nebo výrobce nabízí dobrovolnou záruku navíc (prodloužená záruka, záruka výrobce 5 let) nebo poprodejní servis, uvede jejich podmínky.
- Kde na webu: reklamační řád, obchodní podmínky, produktové stránky.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `legal_voluntary_guarantee` — EN: "Does this text mention a guarantee that the seller or manufacturer gives voluntarily on top of the statutory rights for defective goods, such as an extended warranty? Answer no if the text only describes the statutory period for claiming defects." — CS: „Zmiňuje tento text záruku, kterou prodávající nebo výrobce poskytuje dobrovolně nad rámec zákonných práv z vadného plnění, například prodlouženou záruku? Pokud text popisuje jen zákonnou dobu pro reklamaci vad, odpověz ne.“
  - `legal_guarantee_conditions` — EN: "Does this text describe the conditions of that voluntary guarantee, such as what it covers and how to claim it? Answer no if no voluntary guarantee is mentioned." — CS: „Popisuje tento text podmínky této dobrovolné záruky, například na co se vztahuje a jak ji uplatnit? Pokud text dobrovolnou záruku nezmiňuje, odpověz ne.“
- Skládání: `site_presence_if(legal_voluntary_guarantee ≥ 0,7)` → `legal_guarantee_conditions`; pro produktové stránky stejné otázky ve tvaru „the sentence (field sentence)“.
- Závažnost (návrh): low
- Poznámky / nejistoty: Záruční list (§ 2174a) se vydává při převzetí → z textu nelze. Délku záruky čte kód.

## F. Další údaje podle § 1820 OZ a mimosoudní řešení sporů

### cz_code_of_conduct
- Ustanovení: § 1820 odst. 1 písm. n) OZ.
- Citace: „n) údaj o kodexu chování, pokud se jej podnikatel zavázal dodržovat v souvislosti s některou obchodní praktikou nebo odvětvím jeho podnikání a o tom, jak lze obdržet jeho kopii,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Když se obchod zavázal dodržovat kodex (asociace, certifikace obchodů), uvede to a jak získat jeho znění.
- Kde na webu: obchodní podmínky, O nás, patička (odznaky).
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `legal_code_of_conduct` — EN: "Does this text say that the seller has committed to follow a code of conduct, or the rules of an association or a certification programme for online shops?" — CS: „Uvádí tento text, že se prodávající zavázal dodržovat kodex chování nebo pravidla asociace či certifikačního programu pro internetové obchody?“
  - `legal_code_copy` — EN: "Does this text say how the customer can get or read the code of conduct, for example through a link? Answer no if no code of conduct is mentioned." — CS: „Uvádí tento text, jak může zákazník kodex chování získat nebo si ho přečíst, například odkazem? Pokud text žádný kodex nezmiňuje, odpověz ne.“
- Skládání: `site_presence_if(legal_code_of_conduct ≥ 0,7)` → `legal_code_copy`; spouštěč i nad rámem stránky a alt texty odznaků.
- Závažnost (návrh): low
- Poznámky / nejistoty: Nepravdivé tvrzení o kodexu je na černé listině (příloha č. 1 písm. a) ZOS, jiný rešeršista).

### cz_contract_duration
- Ustanovení: § 1820 odst. 1 písm. o) OZ; souběžně § 1811 odst. 2 písm. g) OZ.
- Citace: „o) údaj o době trvání závazku a podmínky ukončení závazku, má-li být smlouva uzavřena na dobu neurčitou nebo má-li být závazek automaticky prodlužován,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: U předplatného a automaticky prodlužovaných smluv uvést dobu trvání a jak smlouvu ukončit.
- Kde na webu: obchodní podmínky, stránka předplatného.
- Kontrola: jev + kód (podmíněné)
- Otázky pro Jev:
  - spouštěč `legal_subscription` (viz cz_subscription_price)
  - `legal_subscription_termination` — EN: "Does this text explain how and under what conditions the customer can end the subscription or recurring contract? Answer no if no subscription or recurring contract is mentioned." — CS: „Vysvětluje tento text, jak a za jakých podmínek může zákazník předplatné nebo opakovanou smlouvu ukončit? Pokud text předplatné ani opakovanou smlouvu nezmiňuje, odpověz ne.“
- Kontrola kódem: doba trvání `(?i)na\s+dobu\s+(?:ne)?určitou|\d+\s*(?:měsíc\w*|rok\w*|let)|automaticky\s+prodlužuj\w*`.
- Skládání: `site_presence_if(legal_subscription ≥ 0,7)`
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: Upozornění na písm. o) a p) bezprostředně před objednávkou (§ 1826a odst. 1) je v košíku, viz cz_pre_order_summary.

### cz_minimum_duration
- Ustanovení: § 1820 odst. 1 písm. p) OZ.
- Citace: „p) nejkratší dobu, po kterou budou trvat spotřebitelovy povinnosti ze smlouvy, má-li být smlouvou určena,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Má-li smlouva minimální dobu (například předplatné na 12 měsíců), uvést ji.
- Kde na webu: obchodní podmínky, stránka předplatného.
- Kontrola: kód (podmíněné)
- Kontrola kódem: `(?i)(?:minimální|nejkratší)\s+(?:dob\w*|délk\w*)|závaz\w*\s+na\s+\d+\s*měsíc\w*|na\s+dobu\s+\d+\s*měsíc\w*`.
- Skládání: informativní při spouštěči `legal_subscription`; zda minimální doba existuje, je fakt mimo web, proto nejvýš „k ověření“.
- Závažnost (návrh): low
- Poznámky / nejistoty: —

### cz_digital_compatibility
- Ustanovení: § 1820 odst. 1 písm. r) OZ; § 1811 odst. 2 písm. h) a i) OZ.
- Citace: „r) údaje o funkčnosti, kompatibilitě a interoperabilitě podle § 1811 odst. 2 písm. h) a i), a“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „h) údaje o funkčnosti digitálního obsahu, služby digitálního obsahu a věci s digitálními vlastnostmi, včetně technických ochranných opatření, a“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: U digitálního obsahu a zboží s digitálními prvky (chytrá zařízení, software, e-knihy, hry) uvést funkčnost včetně technické ochrany a s čím výrobek funguje.
- Kde na webu: produktové stránky.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `product_digital` — EN: "Does the sentence (field sentence) describe a product that is software, an app, an e-book, a game or other digital content, or a device that needs an app or online service to work?" — CS: „Popisuje věta (pole sentence) výrobek, který je softwarem, aplikací, e-knihou, hrou nebo jiným digitálním obsahem, případně zařízení, které ke své funkci potřebuje aplikaci nebo on-line službu?“
  - `product_compatibility` — EN: "Does the sentence (field sentence) state which devices, operating systems, software or platforms the product works with?" — CS: „Uvádí věta (pole sentence), s jakými zařízeními, operačními systémy, softwarem nebo platformami výrobek funguje?“
- Skládání: `page_presence_if(product_digital ≥ 0,7)` → `product_compatibility`.
- Závažnost (návrh): low
- Poznámky / nejistoty: Technická ochranná opatření (DRM) jsou další znak, případně samostatná otázka. Minimální doba aktualizací softwaru je připravovaná povinnost (cz_empco_pending).

### cz_adr (EXISTUJÍCÍ: legal_adr_missing)
- Ustanovení: § 14 odst. 1 ZOS; § 1820 odst. 1 písm. s) OZ (první část); § 20e ZOS (subjekty).
- Citace: „Prodávající informuje spotřebitele jasným, srozumitelným a snadno dostupným způsobem o subjektu mimosoudního řešení spotřebitelských sporů“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „Informace musí zahrnovat též internetovou adresu tohoto subjektu.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „Pokud smlouva uzavřená mezi prodávajícím a spotřebitelem odkazuje na obchodní podmínky, uvede informace podle věty první a druhé rovněž v těchto obchodních podmínkách.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „s) údaj o existenci, způsobu a podmínkách mimosoudního vyřizování sporů spotřebitelů včetně údaje, zda se lze obrátit se stížností na orgán dohledu nebo státního dozoru.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „d) v případech, kdy není dána působnost orgánů uvedených v písmenech a) až c), Česká obchodní inspekce nebo jiný subjekt pověřený Ministerstvem průmyslu a obchodu;“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „Nařízení (EU) č. 524/2013 se zrušuje s účinkem ode dne 20. července 2025.“ (predpisy-eu/eu-2024-3228-zruseni-platformy-odr-cs.txt)
- Požadavek lidsky: Jmenovat příslušný subjekt mimosoudního řešení sporů (u běžného zboží ČOI) a jeho webovou adresu, na webu i v obchodních podmínkách.
- Kde na webu: obchodní podmínky, reklamační řád.
- Kontrola: jev. EXISTUJE: otázka `legal_adr`. Nepsat znovu.
- Návrh doplnění: (a) existující otázka připouští „name … or link“, zákon chce obojí → kód: v odstavci s `legal_adr` ≥ 0,7 vyžadovat web subjektu `(?i)\b(?:https?://)?(?:www\.)?(?:adr\.)?coi(?:\.gov)?\.cz\b` (ověřeno 25. 9. 2026: adr.coi.cz přesměruje na https://coi.gov.cz/informace-o-adr/); u odvětvových subjektů (ERÚ, ČTÚ, finanční arbitr) obecná doména. (b) `page_type_presence(obchodní podmínky)`: informace musí být přímo na stránce obchodních podmínek, pokud ji web má. (c) Odkaz na platformu ODR nevyžadovat.
- Skládání: `site_presence` (existující) a návrhy výše.
- Závažnost (návrh): high (existující)
- Poznámky / nejistoty: ČOI 2025: § 14 135 případů. § 24 odst. 5 písm. m) ZOS dosud odkazuje na čl. 14 nařízení 524/2013, to je ale od 20. 7. 2025 zrušené; povinnost odkazu na ODR zanikla a starý odkaz na platformu je nefunkční (případně low nález „zastaralý odkaz“, návrh).

### cz_supervisory_authority
- Ustanovení: § 1820 odst. 1 písm. s) OZ, část „včetně údaje, zda se lze obrátit se stížností na orgán dohledu nebo státního dozoru“.
- Citace: „včetně údaje, zda se lze obrátit se stížností na orgán dohledu nebo státního dozoru.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést, že se spotřebitel může se stížností obrátit na dozorový orgán (typicky ČOI, u živnostníků živnostenský úřad).
- Kde na webu: obchodní podmínky.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_supervisory_authority` — EN: "Does this text name an authority that supervises the seller's compliance with the law and to which consumers can complain (for example 'supervision is carried out by the Czech Trade Inspection Authority' or a trade licensing office)?" — CS: „Jmenuje tento text úřad, který dohlíží na dodržování zákona prodávajícím a na který si spotřebitel může stěžovat (např. ‚dozor vykonává Česká obchodní inspekce‘ nebo živnostenský úřad)?“
- Skládání: `site_presence`, nález nejvýš „k ověření“.
- Závažnost (návrh): low
- Poznámky / nejistoty: ČOI je zároveň subjekt ADR i dozorový orgán; zda stačí jedna zmínka ČOI v roli ADR, výkladově nejisté. Jev může role zaměnit, změřit na vzorku.

## G. Elektronické uzavírání smlouvy a objednávka

### cz_contract_storage
- Ustanovení: § 1826 odst. 1 písm. a) OZ; výjimka § 1826 odst. 3 OZ; přestupek § 24 odst. 15 písm. h) ZOS.
- Citace: „(1) Při použití elektronických prostředků uvede podnikatel i údaje“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „a) zda uzavřená smlouva bude u něho uložena a zda k ní umožní spotřebiteli přístup,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „h) při uzavírání smlouvy elektronickými prostředky neuvede některý z údajů podle § 1826 odst. 1 občanského zákoníku,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Uvést, zda obchod smlouvu (objednávku) archivuje a zda k ní má zákazník přístup.
- Kde na webu: obchodní podmínky.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_contract_archived` — EN: "Does this text say whether the seller stores (archives) the concluded contract or order?" — CS: „Uvádí tento text, zda prodávající uzavřenou smlouvu nebo objednávku ukládá (archivuje)?“
  - `legal_contract_access` — EN: "Does this text say whether the customer can access the stored contract or order, for example in their account? Answer no if the text does not mention storing the contract." — CS: „Uvádí tento text, zda má zákazník k uložené smlouvě nebo objednávce přístup, například ve svém účtu? Pokud text o ukládání smlouvy nemluví, odpověz ne.“
- Skládání: `site_presence` pro obě otázky (každá ≥ 0,7 v některém odstavci).
- Závažnost (návrh): medium
- Poznámky / nejistoty: Neplatí pro smlouvy uzavírané jen e-mailem (§ 1826 odst. 3).

### cz_contract_languages
- Ustanovení: § 1826 odst. 1 písm. b) OZ.
- Citace: „b) o jazycích, ve kterých lze smlouvu uzavřít,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Uvést, v jakém jazyce (jazycích) lze smlouvu uzavřít.
- Kde na webu: obchodní podmínky.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_contract_language` — EN: "Does this text state in which language or languages the contract can be concluded?" — CS: „Uvádí tento text, v jakém jazyce nebo jazycích lze smlouvu uzavřít?“
- Skládání: `site_presence`
- Závažnost (návrh): medium
- Poznámky / nejistoty: —

### cz_order_steps
- Ustanovení: § 1826 odst. 1 písm. c) OZ.
- Citace: „c) o jednotlivých technických krocích vedoucích k uzavření smlouvy a“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Popsat kroky objednávky až k uzavření smlouvy.
- Kde na webu: obchodní podmínky, Jak nakupovat.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_order_steps` — EN: "Does this text describe the steps a customer goes through to place an order and conclude the contract, for example adding goods to the cart, choosing delivery and payment and sending the order?" — CS: „Popisuje tento text kroky, kterými zákazník prochází při objednávce a uzavření smlouvy, například vložení zboží do košíku, volbu dopravy a platby a odeslání objednávky?“
- Skládání: `site_presence`
- Závažnost (návrh): medium
- Poznámky / nejistoty: —

### cz_input_error_correction
- Ustanovení: § 1826 odst. 1 písm. d) OZ.
- Citace: „d) o možnostech zjištění a opravování chyb vzniklých při zadávání dat před podáním objednávky.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Vysvětlit, jak zákazník před odesláním objednávky zjistí a opraví chyby v zadaných údajích.
- Kde na webu: obchodní podmínky, Jak nakupovat.
- Kontrola: jev
- Otázky pro Jev:
  - `legal_input_errors` — EN: "Does this text explain how the customer can find and correct mistakes in the data entered in the order before sending it?" — CS: „Vysvětluje tento text, jak může zákazník před odesláním objednávky zjistit a opravit chyby v zadaných údajích?“
- Skládání: `site_presence`
- Závažnost (návrh): medium
- Poznámky / nejistoty: Text o možnosti opravy nedokazuje, že ji pokladna skutečně umožňuje (cz_order_review).

### cz_order_review
- Ustanovení: § 1826 odst. 2 OZ.
- Citace: „(2) Před podáním objednávky musí být při použití elektronických prostředků spotřebiteli umožněno zkontrolovat a měnit vstupní údaje, které do objednávky vložil.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Pokladna musí před odesláním ukázat rekapitulaci a umožnit úpravu údajů.
- Kde na webu: pokladna (nástroj neprochází).
- Kontrola: nelze z textu. Jde o funkci objednávkového procesu.
- Skládání: sekce zprávy „Co nebylo zkontrolováno“.
- Závažnost (návrh): —
- Poznámky / nejistoty: —

### cz_pre_order_summary
- Ustanovení: § 1826a odst. 1 OZ.
- Citace: „(1) Uzavírá-li se za použití elektronických prostředků úplatná smlouva, podnikatel upozorní spotřebitele bezprostředně před tím, než učiní objednávku, jasným a výrazným způsobem na údaje podle § 1820 odst. 1 písm. a), e), o) a p).“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Na poslední obrazovce pokladny zřetelně zopakovat hlavní vlastnosti, celkovou cenu s dopravou a u předplatného dobu trvání.
- Kde na webu: pokladna (nástroj neprochází).
- Kontrola: nelze z textu. Týká se poslední obrazovky objednávky.
- Skládání: „Co nebylo zkontrolováno“.
- Závažnost (návrh): —
- Poznámky / nejistoty: —

### cz_order_button
- Ustanovení: § 1826a odst. 2 OZ.
- Citace: „Je-li objednávka činěna použitím tlačítka nebo obdobného ovládacího prvku, musejí být označeny snadno čitelným nápisem „Objednávka zavazující k platbě“ nebo jinou odpovídající jednoznačnou formulací.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Nesplní-li podnikatel tuto povinnost, je smlouva neplatná, ledaže se jí spotřebitel dovolá.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Tlačítko pro odeslání objednávky musí nést nápis „Objednávka zavazující k platbě“ nebo jednoznačný ekvivalent.
- Kde na webu: pokladna (nástroj neprochází).
- Kontrola: nelze z textu. Tlačítko je v pokladně. Pomocně: když obchodní podmínky popisují tlačítko v uvozovkách (`(?i)tlačítk\w*\s+[„"]([^“"]{2,40})[“"]`), vypsat nalezený nápis k ručnímu posouzení („Odeslat objednávku“ je podezřelé), bez automatického nálezu.
- Skládání: „Co nebylo zkontrolováno“, pomocný výpis informativně.
- Závažnost (návrh): mimo dosah, dopad vysoký
- Poznámky / nejistoty: ČOI 2025: 107 případů (třetí nejčastější porušení OZ).

### cz_order_confirmation_terms
- Ustanovení: § 1824a odst. 1, § 1827 odst. 1 a 2 OZ.
- Citace: „(1) Podnikatel vydá spotřebiteli potvrzení o uzavřené smlouvě v textové podobě v přiměřené době po jejím uzavření, nejpozději však v okamžiku dodání zboží nebo před tím, než začne poskytovat službu.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „(2) Uzavírá-li se smlouva za použití elektronických prostředků, poskytne podnikatel spotřebiteli v textové podobě kromě znění smlouvy i znění všeobecných obchodních podmínek.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Po objednávce poslat potvrzení a znění smlouvy i obchodních podmínek v textové podobě (typicky e-mailem s přílohou).
- Kde na webu: e-mail po objednávce (nástroj ho nevidí).
- Kontrola: nelze z textu. Děje se po objednávce mimo web.
- Skládání: „Co nebylo zkontrolováno“ s doporučením ověřit testovací objednávkou.
- Závažnost (návrh): mimo dosah, dopad vysoký
- Poznámky / nejistoty: ČOI 2025: § 1827 odst. 2 195 případů, druhé nejčastější porušení OZ.

### cz_extra_payment_consent
- Ustanovení: § 1817 OZ.
- Citace: „Podnikatel nesmí po spotřebiteli požadovat další platbu, než kterou je spotřebitel povinen uhradit na základě hlavního smluvního závazku, pokud spotřebitel nedal k této další platbě před uzavřením smlouvy výslovný souhlas.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
  - „Z předem připraveného nastavení, které by spotřebitel musel odmítnout, nelze výslovný souhlas dovodit.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Žádné předvyplněné placené doplňky (pojištění, prodloužená záruka) v košíku.
- Kde na webu: košík a pokladna (nástroj neprochází).
- Kontrola: nelze z textu. Předvyplněné volby jsou v košíku.
- Skládání: „Co nebylo zkontrolováno“.
- Závažnost (návrh): —
- Poznámky / nejistoty: Nejde o informační povinnost, uvedeno kvůli úplnosti oblasti objednávky.

## H. Jazyk a forma

### cz_language
- Ustanovení: § 11 odst. 1 ZOS; § 1811 odst. 1 OZ.
- Citace: „(1) Prodávající musí zajistit, aby informace uvedené v § 9 až 10a, 12, 13, § 16 odst. 1 a 3 a § 19 a v § 1811 odst. 2 písm. b) a § 1820 odst. 1 písm. a) občanského zákoníku, jsou-li poskytovány písemně, byly poskytnuty v českém jazyce.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „(1) Veškerá sdělení vůči spotřebiteli musí podnikatel učinit jasně a srozumitelně v jazyce, ve kterém se uzavírá smlouva.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Popisy zboží, cena a reklamační informace česky; ostatní sdělení v jazyce smlouvy.
- Kde na webu: produktové stránky (popis), reklamační řád, obchodní podmínky.
- Kontrola: kód
- Kontrola kódem: identifikace jazyka po odstavcích delších než 200 znaků; čeština proti slovenštině podle znaků (cs: ě, ř, ů; sk: ä, ô, ľ, ĺ, ŕ) a častých slov. Nález, když je právní stránka nebo popis produktu převážně v jiném jazyce než češtině.
- Skládání: `segment` u právních stránek; u produktových stránek souhrnně „X z N stránek“.
- Závažnost (návrh): medium; high u reklamačních informací v cizím jazyce
- Poznámky / nejistoty: Web cílený na slovenské spotřebitele patří do modulu `--country sk`. Krátké cizojazyčné názvy produktů a značek ignorovat.

### cz_units
- Ustanovení: § 11 odst. 3 ZOS.
- Citace: „(3) Fyzikální veličiny musí být vyjádřeny v měřicích jednotkách stanovených zvláštním právním předpisem“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Rozměry, hmotnost a objem v zákonných jednotkách (cm, kg, l); palce nebo libry nejvýš doplňkově.
- Kde na webu: produktové stránky.
- Kontrola: kód
- Kontrola kódem: `(?i)\b\d+(?:[.,]\d+)?\s?(?:"|″|palc\w*|inch\w*|lbs?|oz|ft|feet|yd|°F|gal\w*)` a v témže segmentu chybí metrická hodnota `\b\d+(?:[.,]\d+)?\s?(?:mm|cm|m|kg|g|l|ml|°C)\b`.
- Skládání: `segment_regex`
- Závažnost (návrh): low
- Poznámky / nejistoty: Označení typu „R16“, závity a úhlopříčka TV v palcích bývají součástí názvu; doplňkový údaj v palcích vedle centimetrů je v pořádku. Zvláštní předpis (zákon o metrologii) nestažen, NEOVĚŘENO, co přesně povoluje.

### cz_legibility
- Ustanovení: § 1824 odst. 1 OZ; § 1811 odst. 1 OZ.
- Citace: „Údaje poskytované v textové podobě musí být čitelné.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Informace musí být čitelné a srozumitelné.
- Kde na webu: všude.
- Kontrola: nelze z textu. Čitelnost (písmo, kontrast) je vizuální, srozumitelnost hodnotová. Pomocně lze hlásit extrémy: text jen v obrázku, PDF bez textové vrstvy.
- Skládání: informativní
- Závažnost (návrh): —
- Poznámky / nejistoty: —

## I. Recenze a on-line tržiště

### cz_reviews_verification
- Ustanovení: § 5a odst. 5 ZOS (ve spojení s § 4 odst. 4 ZOS); související černá listina příloha č. 1 písm. y) a z) ZOS (jiný rešeršista).
- Citace: „Poskytuje-li prodávající přístup k hodnocení výrobků nebo služeb … za podstatnou informaci se považuje také informace o tom, zda a jak prodávající zajišťuje, aby zveřejněná spotřebitelská recenze pocházela od spotřebitele, který výrobek nebo službu skutečně použil nebo si je zakoupil.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Když web ukazuje recenze nebo hodnocení zákazníků, musí říct, zda je ověřuje, a pokud ano, jak.
- Kde na webu: produktové stránky (sekce recenzí), stránka o recenzích, obchodní podmínky.
- Kontrola: jev + kód (kód jako spouštěč)
- Otázky pro Jev:
  - `legal_reviews_verification_stated` — EN: "Does this text state whether or not the shop checks that published customer reviews come from customers who actually bought or used the product? Answer yes also if the text says that reviews are not checked." — CS: „Uvádí tento text, zda obchod ověřuje, že zveřejněné recenze pocházejí od zákazníků, kteří výrobek skutečně koupili nebo použili? Odpověz ano i tehdy, když text říká, že recenze ověřovány nejsou.“
  - `legal_reviews_verification_method` — EN: "Does this text describe how the shop checks that reviews come from real customers, for example that only customers with a completed order can post a review? Answer no if the text only says that reviews are verified without saying how." — CS: „Popisuje tento text, jak obchod ověřuje, že recenze pocházejí od skutečných zákazníků, například že recenzi může napsat jen zákazník s dokončenou objednávkou? Pokud text jen tvrdí, že recenze ověřuje, a neříká jak, odpověz ne.“
  - `legal_reviews_not_verified` — EN: "Does this text say that the shop does not check whether reviews come from customers who bought or used the product?" — CS: „Uvádí tento text, že obchod neověřuje, zda recenze pocházejí od zákazníků, kteří výrobek koupili nebo použili?“
- Kontrola kódem (spouštěč): JSON-LD `Review` nebo `AggregateRating`; text `(?i)recenz\w*|hodnocení\s+zákazník\w*|\d(?:[.,]\d)?\s*/\s*5\b|\d+\s*hodnocení`; widgety recenzních služeb (skript nebo obrázek).
- Skládání: `site_presence_if(spouštěč recenzí)`: přítomno, když `stated` ≥ 0,7 a zároveň (`method` ≥ 0,7 nebo `not_verified` ≥ 0,7). Hledat v právních stránkách i ve větách produktových stránek u recenzí (stejné otázky ve tvaru „the sentence (field sentence)“ s kontextem).
- Závažnost (návrh): high
- Poznámky / nejistoty: ČOI 2025: § 5a odst. 5 91 případů; v cílených kontrolách recenzí 29× informace chyběla úplně a 3× obchod uvedl jen, že recenze ověřuje, bez toho jak (`coi/coi-kontroly-recenze-2025.txt`), proto dvě otázky. Pravdivost tvrzení o ověřování z textu nelze (černá listina písm. y).

### cz_marketplace_ranking
- Ustanovení: § 11b písm. a) ZOS; § 5a odst. 4 ZOS; definice § 2 odst. 2 písm. a) až c) ZOS.
- Citace: „a) obecnou informaci o hlavních parametrech určujících pořadí nabídek předkládaných spotřebiteli a o jejich relativní váze oproti ostatním parametrům; tuto informaci zpřístupní v konkrétním oddílu on-line rozhraní tak, aby byla přímo a snadno dostupná z místa, na němž jsou učiněny nabídky“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „b) on-line tržištěm služba umožňující spotřebiteli uzavírat distančním způsobem smlouvu s prodávajícím nebo jinou osobou za využití softwaru zahrnujícího internetovou stránku, část internetové stránky nebo aplikaci, provozovaného jiným podnikatelem, než je prodávající, nebo jeho jménem,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Jen on-line tržiště (prodávají i třetí strany): vysvětlit hlavní kritéria řazení výsledků vyhledávání a jejich váhu, dostupně přímo z výsledků.
- Kde na webu: stránka „Jak řadíme“, obchodní podmínky tržiště, odkaz u výsledků vyhledávání (výsledky vyhledávání nástroj z procházení vyřazuje).
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `product_third_party_seller` (spouštěč) — EN: "Does the sentence (field sentence) say that the product is sold by a seller other than the operator of the website, for example a partner seller on a marketplace?" — CS: „Uvádí věta (pole sentence), že výrobek prodává jiný prodávající než provozovatel webu, například partnerský prodejce na tržišti?“
  - `legal_ranking_parameters` — EN: "Does this text explain the main criteria that determine the order in which products or offers are shown in search results, for example relevance, price, sales or paid promotion?" — CS: „Vysvětluje tento text hlavní kritéria, podle kterých se řadí výrobky nebo nabídky ve výsledcích vyhledávání, například relevance, cena, prodejnost nebo placená propagace?“
- Kontrola kódem (spouštěč): `(?i)\b(?:prodejce|prodává\s+a\s+(?:doručuje|odesílá)|obchodní\s+partner|marketplace)\b` na produktových stránkách.
- Skládání: `site_presence_if(spouštěč tržiště)` → `legal_ranking_parameters`.
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: Pro běžný e-shop s vlastním zbožím se § 11b nevztahuje. Neoznačená placená reklama ve výsledcích vyhledávání je na černé listině (příloha č. 1 písm. k) ZOS, jiný rešeršista); to se týká každého obchodu s vyhledáváním.

### cz_marketplace_seller_status
- Ustanovení: § 11b písm. b), c) a d) ZOS; § 5a odst. 3 písm. f) ZOS.
- Citace: „b) informaci, zda je třetí strana nabízející výrobek nebo službu prodávajícím nebo nikoliv, a to na základě prohlášení této třetí strany určeného poskytovateli on-line tržiště,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „c) informaci, že se na smlouvu neuplatní práva spotřebitele vyplývající z předpisů Evropské unie na ochranu spotřebitele, není-li třetí strana nabízející výrobek nebo službu prodávajícím,“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „d) mají-li povinnosti ze smlouvy plnit třetí strana nabízející výrobek nebo službu a současně i poskytovatel on-line tržiště, informaci o tom, jaké jsou povinnosti každého z nich.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „f) u výrobku nebo služby nabízených na on-line tržištích také informace podle § 11b písm. b).“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: U nabídky třetí strany uvést, zda je prodávající podnikatel; pokud ne, že neplatí spotřebitelská práva; a jak jsou rozdělené povinnosti mezi tržiště a prodejce.
- Kde na webu: produktové stránky třetích stran, obchodní podmínky tržiště.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `product_seller_is_trader_stated` — EN: "Does the sentence (field sentence) say whether the seller of this offer is a business (trader) or a private person?" — CS: „Uvádí věta (pole sentence), zda je prodávající této nabídky podnikatel, nebo soukromá osoba?“
  - `legal_no_consumer_rights_private_seller` — EN: "Does this text say that consumer protection rights do not apply to purchases from sellers who are not businesses (private persons)?" — CS: „Uvádí tento text, že na nákupy od prodávajících, kteří nejsou podnikateli (soukromých osob), se nevztahují práva spotřebitele?“
  - `legal_obligations_split` — EN: "Does this text explain which obligations under the contract are the responsibility of the marketplace operator and which of the seller?" — CS: „Vysvětluje tento text, které povinnosti ze smlouvy má provozovatel tržiště a které prodávající?“
- Skládání: `page_presence_if(product_third_party_seller ≥ 0,7)` pro písm. b); `site_presence_if(spouštěč tržiště)` pro písm. c) (jen připouští-li tržiště neprofesionální prodejce) a d).
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: —

## J. Podle druhu zboží a sortimentu

### cz_used_goods
- Ustanovení: § 10 ZOS.
- Citace: „Při prodeji použitých nebo upravovaných výrobků, výrobků s vadou nebo výrobků, jejichž užitné vlastnosti jsou jinak omezeny, musí prodávající na tyto skutečnosti spotřebitele předem zřetelně upozornit.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: U použitého, repasovaného, rozbaleného nebo vadného zboží na to předem zřetelně upozornit.
- Kde na webu: produktové stránky (bazar, rozbaleno, outlet).
- Kontrola: nelze z textu, zda je výrobek použitý nebo vadný (fakt mimo web); pomocná jev u stránek, které stav zmiňují.
- Otázky pro Jev (pomocné):
  - `product_used_or_defective` — EN: "Does the sentence (field sentence) say that the product is used, refurbished, repaired, returned or unpacked, or damaged?" — CS: „Uvádí věta (pole sentence), že výrobek je použitý, repasovaný, opravený, vrácený či rozbalený, nebo poškozený?“
  - `product_defect_described` — EN: "Does the sentence (field sentence) describe a specific defect, damage, sign of wear or limitation of this particular product?" — CS: „Popisuje věta (pole sentence) konkrétní vadu, poškození, známky opotřebení nebo omezení tohoto konkrétního výrobku?“
- Skládání: `page_presence_if(product_used_or_defective ≥ 0,7)` → `product_defect_described`, nález nejvýš „k ověření“.
- Závažnost (návrh): medium, jen při spuštění
- Poznámky / nejistoty: „Zřetelnost“ upozornění je vizuální. Míra podrobnosti popisu u repasovaného zboží je hodnotová.

### cz_footwear_materials
- Ustanovení: § 10a odst. 1 ZOS (a § 11 odst. 1 ZOS, jazyk).
- Citace: „(1) Prodávající musí zajistit, aby jím prodávaná obuv byla přímo viditelně a srozumitelně označena údaji o materiálech použitých v jejích hlavních částech“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Obuv označit materiály hlavních částí (svršek, podšívka a stélka, podešev).
- Kde na webu: produktové stránky obuvi.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `product_is_footwear` — EN: "Does the sentence (field sentence) describe shoes, boots, sandals or other footwear?" — CS: „Popisuje věta (pole sentence) boty, holínky, sandály nebo jinou obuv?“
  - `product_footwear_materials` — EN: "Does the sentence (field sentence) state what material the upper, the lining or insole, or the sole of the footwear is made of?" — CS: „Uvádí věta (pole sentence), z jakého materiálu je svršek, podšívka či stélka nebo podešev obuvi?“
- Skládání: `page_presence_if(product_is_footwear ≥ 0,7)` → `product_footwear_materials`.
- Závažnost (návrh): low
- Poznámky / nejistoty: § 10a ukládá označit samotnou obuv; zda musí být údaj i v nabídce e-shopu, NEOVĚŘENO (lze opřít o hlavní vlastnosti podle § 1820 odst. 1 písm. a) OZ). Prováděcí předpis (piktogramy) nestažen. Složení textilu upravuje nařízení 1007/2011 (`predpisy-eu/eu-2011-1007-textil-cs.txt`, stáhl jiný rešeršista).

### cz_use_instructions
- Ustanovení: § 9 odst. 1 a 2 ZOS.
- Citace: „(1) Prodávající je povinen řádně informovat spotřebitele o způsobu použití a údržby výrobku a o nebezpečí, které vyplývá z jeho nesprávného použití nebo údržby, jakož i o riziku souvisejícím s poskytovanou službou.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
  - „(2) Prodávající poskytne spotřebiteli návod podle odstavce 1 na trvalém nosiči dat.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: Návod k použití a upozornění na nebezpečí se dodávají s výrobkem.
- Kde na webu: dodává se s výrobkem.
- Kontrola: nelze z textu. Web návod mít nemusí; varování v nabídce řeší cz_gpsr_warnings.
- Skládání: —
- Závažnost (návrh): —
- Poznámky / nejistoty: —

### cz_deposit_packaging
- Ustanovení: § 18 odst. 1 ZOS.
- Citace: „(1) Prodávající je povinen informovat spotřebitele o peněžní částce za výkup vratných zálohovaných obalů a tuto informaci na viditelném místě zpřístupnit.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- Požadavek lidsky: U zboží ve vratných zálohovaných obalech (lahve, přepravky, sudy) uvést částku zálohy nebo výkupu.
- Kde na webu: produktové stránky nápojů, Doprava a platba.
- Kontrola: jev + kód (podmíněné)
- Otázky pro Jev:
  - `product_deposit_packaging` — EN: "Does the sentence (field sentence) mention a returnable bottle, crate, keg or other packaging with a deposit?" — CS: „Zmiňuje věta (pole sentence) vratnou lahev, přepravku, sud nebo jiný obal se zálohou?“
- Kontrola kódem: `(?i)zálo\w*[^.]{0,40}?\d+(?:,\d{1,2})?[\s\u00a0]?Kč|\d+(?:,\d{1,2})?[\s\u00a0]?Kč[^.]{0,40}?zálo\w*`.
- Skládání: `page_regex_if(product_deposit_packaging ≥ 0,7)`
- Závažnost (návrh): low
- Poznámky / nejistoty: Případný zálohový systém na jednorázové obaly upravuje jiný předpis, NEOVĚŘENO.

### cz_best_before
- Ustanovení: § 2163 OZ.
- Citace: „U zuživatelné věci se vyznačí doba nejkratší trvanlivosti, popřípadě, u věci podléhající rychlé zkáze, doba, po kterou lze věc použít.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- Požadavek lidsky: Datum trvanlivosti se vyznačuje na věci.
- Kde na webu: na výrobku.
- Kontrola: nelze z textu. E-shop ho v nabídce uvádět nemusí; informace o potravinách v nabídce řeší nařízení 1169/2011 (NEOVĚŘENO, nestaženo).
- Skládání: —
- Závažnost (návrh): —
- Poznámky / nejistoty: —

### cz_gpsr_manufacturer
- Ustanovení: čl. 19 písm. a) a b) nařízení (EU) 2023/988 (GPSR), použitelné od 13. 12. 2024.
- Citace: „Pokud hospodářské subjekty dodávají výrobky na trh online nebo prostřednictvím jiných prostředků prodeje na dálku, musí nabídka těchto výrobků jasně a viditelně uvádět alespoň tyto údaje:“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
  - „jméno, zapsaný obchodní název nebo zapsanou ochrannou známku výrobce a poštovní a elektronickou adresu, na kterých lze výrobce kontaktovat;“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
  - „pokud výrobce není usazen v Unii, jméno a poštovní a elektronickou adresu odpovědné osoby ve smyslu čl. 16 odst. 1 tohoto nařízení nebo čl. 4 odst. 1 nařízení (EU) 2019/1020;“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
  - „Použije se ode dne 13. prosince 2024.“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
- Požadavek lidsky: V každé nabídce výrobku uvést výrobce (jméno nebo ochrannou známku) a jeho poštovní a elektronickou adresu; u výrobce mimo EU i odpovědnou osobu v EU.
- Kde na webu: každá produktová stránka (často záložka „Informace o výrobci“ nebo „Bezpečnost výrobku“).
- Kontrola: jev + kód
- Otázky pro Jev:
  - `product_manufacturer_named` — EN: "Does the sentence (field sentence) name the manufacturer or the brand owner of the product?" — CS: „Jmenuje věta (pole sentence) výrobce nebo vlastníka značky výrobku?“
  - `product_manufacturer_address` — EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) give a postal address of the product's manufacturer or of its responsible person in the EU? Answer no if the address belongs to the shop." — CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after) poštovní adresu výrobce výrobku nebo jeho odpovědné osoby v EU? Pokud adresa patří obchodu, odpověz ne.“
  - `product_manufacturer_contact` — EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) give an e-mail address or website of the product's manufacturer or of its responsible person in the EU? Answer no if it belongs to the shop." — CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after) e-mail nebo web výrobce výrobku nebo jeho odpovědné osoby v EU? Pokud patří obchodu, odpověz ne.“
- Kontrola kódem: JSON-LD `brand` a `manufacturer`; PSČ nebo adresa a e-mail nebo URL v blocích označených `(?i)výrobce|manufacturer|odpovědná\s+osoba|GPSR|bezpečnost\s+výrobku`; země mimo EU v adrese výrobce (seznam) → vyžadovat i odpovědnou osobu.
- Skládání: `page_presence` (všechny tři znaky na každé produktové stránce), ve zprávě souhrnně.
- Závažnost (návrh): medium
- Poznámky / nejistoty: Co přesně je „elektronická adresa“ (e-mail, nebo i web či formulář), ověřit v pokynech Komise ke GPSR (nestaženo). Údaje bývají v záložce vykreslené JavaScriptem → „k ověření“. Příslušný dozorový orgán v ČR NEOVĚŘENO (adaptační předpis nestažen).

### cz_gpsr_product_id
- Ustanovení: čl. 19 písm. c) GPSR.
- Citace: „údaje umožňující identifikaci výrobku, včetně jeho vyobrazení a typu a případných dalších identifikátorů výrobku, a“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
- Požadavek lidsky: Obrázek výrobku, typ nebo model a další identifikátor (EAN, číslo šarže).
- Kde na webu: produktové stránky.
- Kontrola: kód
- Kontrola kódem: obrázek (JSON-LD `image`, `og:image`); identifikátor (JSON-LD `gtin8`, `gtin12`, `gtin13`, `gtin14`, `mpn`, `sku`, `model`) nebo text `(?i)\b(?:EAN|GTIN|kód\s+(?:produktu|zboží)|katalogové\s+číslo|typ|model)\s*[:.]?\s*[\w-]{3,}`.
- Skládání: `page_regex`
- Závažnost (návrh): low
- Poznámky / nejistoty: Zda stačí interní kód obchodu (SKU), ověřit.

### cz_gpsr_warnings
- Ustanovení: čl. 19 písm. d) GPSR.
- Citace: „případné varovné nebo bezpečnostní informace, které mají být k výrobku či jeho obalu připojeny nebo uvedeny v průvodním dokumentu k němu v souladu s tímto nařízením nebo příslušnými harmonizačními právními předpisy Unie, v jazyce, který je spotřebitelům snadno srozumitelný“ (predpisy-eu/eu-2023-988-gpsr-cs.txt)
- Požadavek lidsky: Varování a bezpečnostní informace, které patří k výrobku, uvést i v nabídce.
- Kde na webu: produktové stránky.
- Kontrola: nelze z textu. Zda výrobek varování mít musí, určují jiné předpisy (hračky, chemie, elektro) a jeho obal; z webu to nepoznáme. Pomocná otázka jen zjistí, zda nabídka nějaké varování obsahuje.
- Otázky pro Jev (pomocná, informativní):
  - `product_safety_warning` — EN: "Does the sentence (field sentence) give a warning or safety information about using the product, for example an age limit, a choking hazard or a hazard statement?" — CS: „Uvádí věta (pole sentence) varování nebo bezpečnostní informaci k používání výrobku, například věkové omezení, riziko spolknutí malých částí nebo větu o nebezpečnosti?“
- Skládání: informativní; jazyk varování kontroluje cz_language.
- Závažnost (návrh): low
- Poznámky / nejistoty: —

### cz_take_back
- Ustanovení: § 18 odst. 3 zákona č. 542/2020 Sb.; vymezení vybraných výrobků § 3 odst. 1 písm. a).
- Citace: „Poslední prodejce, který jakýmkoliv způsobem, včetně použití prostředků komunikace na dálku … prodává vybrané výrobky, je povinen písemně informovat konečného uživatele o způsobu zajištění zpětného odběru těchto výrobků po ukončení jejich životnosti.“ (predpisy-cz/cz-542-2020-zakon-o-vyrobcich-s-ukoncenou-zivotnosti.txt)
  - „a) vybraným výrobkem elektrozařízení, baterie nebo akumulátor, pneumatika nebo vozidlo,“ (predpisy-cz/cz-542-2020-zakon-o-vyrobcich-s-ukoncenou-zivotnosti.txt)
- Požadavek lidsky: E-shop s elektrem, bateriemi nebo pneumatikami informuje, jak funguje zpětný odběr starých výrobků.
- Kde na webu: stránka „Zpětný odběr“, obchodní podmínky, Doprava.
- Kontrola: jev (podmíněné)
- Otázky pro Jev:
  - `product_take_back_goods` (spouštěč) — EN: "Does the sentence (field sentence) describe an electrical or electronic device, a battery or accumulator, or a tyre?" — CS: „Popisuje věta (pole sentence) elektrické nebo elektronické zařízení, baterii či akumulátor, nebo pneumatiku?“
  - `legal_take_back_info` — EN: "Does this text explain how customers can hand over old electrical equipment, batteries or tyres for free take-back?" — CS: „Vysvětluje tento text, jak mohou zákazníci bezplatně odevzdat staré elektrozařízení, baterie nebo pneumatiky ke zpětnému odběru?“
- Skládání: `site_presence_if(product_take_back_goods ≥ 0,7 na některé produktové stránce)` → `legal_take_back_info`.
- Závažnost (návrh): low
- Poznámky / nejistoty: Konečný uživatel není jen spotřebitel. ČOI kontroluje i tento zákon (zmínka v `coi/coi-kontroly-slevy-rok-2025.txt`).

## K. Připravované povinnosti

### cz_empco_pending
- Ustanovení: čl. 2 směrnice (EU) 2024/825 (mění čl. 5 a 6 směrnice 2011/83/EU, nový čl. 22a); prováděcí nařízení (EU) 2025/1960 (podoba oznámení a štítku, stáhl jiný rešeršista).
- Citace: „připomenutí existence zákonné záruky za soulad zboží se smlouvou a jejích hlavních prvků, včetně její minimální doby trvání v délce dvou let podle směrnice (EU) 2019/771, a to zřetelným způsobem za použití harmonizovaného oznámení podle článku 22a této směrnice;“ (predpisy-eu/eu-2024-825-empco-cs.txt)
  - „podmínky platby, dodání, včetně případných možností dodání způsobem šetrným k životnímu prostředí, plnění, lhůtu, v níž se obchodník zavazuje dodat zboží nebo poskytnout službu, a případně obchodníkovy podmínky pro vyřizování reklamací a stížností;“ (predpisy-eu/eu-2024-825-empco-cs.txt)
  - „Použijí tyto předpisy od 27. září 2026.“ (predpisy-eu/eu-2024-825-empco-cs.txt)
  - „Použije se ode dne 27. září 2026.“ (predpisy-eu/eu-2025-1960-harmonizovane-oznameni-a-stitek-cs.txt)
- Požadavek lidsky (budoucí): harmonizované oznámení o zákonné záruce; harmonizovaný štítek u obchodní záruky výrobce na trvanlivost delší než dva roky; minimální doba aktualizací softwaru; hodnocení opravitelnosti nebo informace o náhradních dílech; případná ekologická možnost dodání a lhůta dodání.
- Kde na webu: produktové stránky, Doprava a platba.
- Kontrola: zatím neaktivovat. V ČR k 25. 9. 2026 chybí národní předpis (směrnice mezi soukromými osobami přímo nepůsobí); zda čl. 2 přebírá sněmovní tisk 53, NEOVĚŘENO. Po transpozici: oznámení a štítek jsou grafika, z textu převážně nelze; aktualizace softwaru a opravitelnost → jev nebo kód na produktových stránkách.
- Skládání: —
- Závažnost (návrh): — (sledovat stav tisku 53)
- Poznámky / nejistoty: Na Slovensku se použije od 27. 9. 2026 (jiný rešeršista).

## Příloha A. Související kontroly správnosti a zakázaná ujednání (mimo zadání, kandidáti)

Nejde o informační povinnosti, ale o chybně uvedené informace nebo zakázaná ujednání, která jdou poznat z textu stejných stránek. ČOI je v kontrolách pravidelně nachází. Návrh k posouzení, bez podrobného rozpracování.

- **cz_bad_withdrawal_period** (kód): lhůta pro odstoupení kratší než 14 dnů, `(?i)odstoup\w*[^.]{0,80}?\b(?:[1-9]|1[0-3])\s*(?:dn\w*|den)\b`. Opora: „(1) Spotřebitel může odstoupit od smlouvy uzavřené distančním způsobem nebo od smlouvy uzavřené mimo obchodní prostory ve lhůtě čtrnácti dnů.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- **cz_withdrawal_reason_or_fee** (jev, dvě otázky): text vyžaduje důvod odstoupení; text účtuje za odstoupení poplatek nebo stornovné. Opora: „Má-li spotřebitel právo odstoupit od smlouvy podle ustanovení tohoto dílu, nevyžaduje se, aby uvedl důvod, a s právem odstoupit od smlouvy nelze spojit postih.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- **cz_refund_deadline** (kód): lhůta vrácení peněz delší než 14 dnů. Opora: „(1) Odstoupí-li spotřebitel od smlouvy, vrátí mu podnikatel bez zbytečného odkladu, nejpozději do čtrnácti dnů od odstoupení od smlouvy, všechny peněžní prostředky včetně nákladů na dodání, které od něho na základě smlouvy přijal, stejným způsobem.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- **cz_unpacked_goods_excluded** (jev): text říká, že rozbalené, vyzkoušené nebo neoriginálně zabalené zboží nelze vrátit. Opora: „Spotřebitel odpovídá podnikateli pouze za snížení hodnoty zboží, které vzniklo v důsledku nakládání s tímto zbožím jinak, než je nutné k tomu, aby se seznámil s povahou, vlastnostmi a funkčností zboží.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- **cz_complaint_resolution_period** (kód): lhůta pro vyřízení reklamace delší než 30 dnů. Opora: „(3) Reklamace včetně odstranění vady musí být vyřízena a spotřebitel o tom musí být informován nejpozději do 30 dnů ode dne uplatnění reklamace, pokud se prodávající se spotřebitelem nedohodne na delší lhůtě.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)
- **cz_restricted_defect_rights** (jev, jedna otázka na jednu podmínku): reklamace podmíněná originálním obalem, vyloučení druhů vad, odkaz jen na výrobce. Opora: „Ujednají-li strany ještě předtím, než kupující vytkl vadu věci, že se jeho práva omezí nebo že zanikají, nepřihlíží se k tomu.“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt) a „a) vylučují nebo omezují spotřebitelova práva z vadného plnění nebo na náhradu újmy,“ (predpisy-cz/cz-89-2012-obcansky-zakonik.txt)
- **cz_payment_surcharge** (kód, nález k ověření): poplatek za způsob platby; přímé náklady obchodu z textu nezjistíme. Opora: „(2) Prodávající nesmí po spotřebiteli v souvislosti s použitým způsobem placení požadovat poplatek převyšující přímé náklady, které prodávajícímu v souvislosti s tímto způsobem placení vznikají.“ (predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt)

## Příloha B. Povinnosti podle druhu zboží (nezpracováno, NEOVĚŘENO)

Tyto povinnosti existují mimo stažené předpisy a patří do samostatné rešerše. Čísla předpisů níže nejsou ověřená citací.

- Potraviny: povinné informace v nabídce na dálku před nákupem (nařízení (EU) č. 1169/2011, zákon o cenách na něj odkazuje v poznámce 15). NEOVĚŘENO.
- Energetické štítky elektrospotřebičů a štítky pneumatik v nabídce na dálku. NEOVĚŘENO.
- Složení textilních vláken (nařízení 1007/2011, stáhl jiný rešeršista do `predpisy-eu/eu-2011-1007-textil-cs.txt`).
- Alkohol a tabák, věkové omezení (zákon č. 65/2017 Sb., stáhl jiný rešeršista do `predpisy-cz/cz-65-2017-zakon-o-ochrane-zdravi-pred-navykovymi-latkami.txt`). NEOVĚŘENO, co se týká textu webu.
- Léčiva a internetové lékárny (společné logo EU), kosmetika, hračky, biocidy. NEOVĚŘENO.
- Mimo spotřebitelské právo a mimo tuto rešerši: informace o zpracování osobních údajů a souhlas s cookies.

## Příloha C. Černá listina (jen odkazy)

Příloha č. 1 ZOS se zpracovává zvlášť. Souvisí s touto rešerší v bodech: písm. a) nepravdivé tvrzení o kodexu chování (cz_code_of_conduct), písm. k) neoznačená placená reklama nebo platba za lepší pořadí ve výsledcích vyhledávání (cz_marketplace_ranking), písm. y) a z) tvrzení o ověřených recenzích bez přiměřených opatření a falešné recenze (cz_reviews_verification).

## Příloha D. Poznámky ke staženým předpisům

- Zákon č. 480/2004 Sb. jsem prošel celý: pro text webu e-shopu nemá informační povinnost. § 7 upravuje obchodní sdělení šířená e-mailem (souhlas, označení, možnost odhlášení), to se z webu nekontroluje.
- NV č. 363/2013 Sb. (vzorový formulář) je zrušené k 18. 2. 2023 a nahradilo ho NV č. 29/2023 Sb. Příloha NV 29/2023 je na zakonyprolidi.cz jen jako naskenované PDF; do .txt jsem přidal její přepis (označený, ověřit proti PDF). Pokyn [4] přílohy (tlačítko pro odstoupení) platí od 19. 6. 2026, ale povinnost tlačítka podle § 1830a OZ až od 1. 1. 2027.
- Zákon č. 159/2026 Sb. je nový (vyhlášen 2. 9. 2026). Mění OZ a ZOS od 1. 1. 2027; pro běžný e-shop jsou podstatné jen nové písm. i) v § 1820 odst. 1 a § 1830a, zbytek se týká finančních služeb.

## Souhrnná tabulka

| id | ustanovení | kontrola | kde |
| --- | --- | --- | --- |
| cz_trader_name | § 435 odst. 1 OZ; § 1820 odst. 1 písm. b) OZ; § 5a odst. 3 písm. b) ZOS | jev + kód | Kontakt, obchodní podmínky, patička |
| cz_trader_address | § 435 odst. 1 OZ; § 1820 odst. 1 písm. c) OZ | jev + kód | Kontakt, obchodní podmínky, patička |
| cz_trader_ico | § 435 odst. 1 OZ; § 7 odst. 2 a 3 ZOK | kód | Kontakt, obchodní podmínky, patička |
| cz_trader_register | § 435 odst. 1 OZ; § 7 odst. 2 a 3 ZOK | kód | Kontakt, obchodní podmínky, patička |
| cz_trader_phone | § 1820 odst. 1 písm. c) OZ | kód | Kontakt, obchodní podmínky, patička |
| cz_trader_email | § 1820 odst. 1 písm. c) OZ | kód | Kontakt, obchodní podmínky, patička |
| cz_trader_other_channel | § 1820 odst. 1 písm. c) OZ | nelze z textu | Kontakt |
| cz_trader_principal | § 1820 odst. 1 písm. c) a d) OZ | jev + kód | obchodní podmínky, Kontakt |
| cz_establishment_address | § 1820 odst. 1 písm. d) OZ | nelze z textu | Kontakt, Doprava |
| cz_premium_rate_contact | § 1820 odst. 1 písm. g) OZ; § 3a ZOS | kód | Kontakt, reklamační řád, patička |
| cz_product_characteristics | § 1820 odst. 1 písm. a) OZ; § 5a odst. 3 písm. a) ZOS | jev + kód | produktové stránky |
| cz_price_total | § 1820 odst. 1 písm. e) OZ; § 12 ZOS; § 13 odst. 2 a 3 zák. 526/1990 | kód | produktové stránky |
| cz_unit_price | § 13 odst. 4 až 7 zák. 526/1990; vyhl. 291/2024 | kód | produktové stránky |
| cz_delivery_costs | § 1820 odst. 1 písm. e) OZ; § 1821 OZ | jev + kód | Doprava a platba, obchodní podmínky |
| cz_subscription_price | § 1820 odst. 1 písm. e) OZ | jev + kód | předplatné, obchodní podmínky |
| cz_price_personalization | § 1820 odst. 1 písm. f) OZ | nelze z textu | u ceny, obchodní podmínky |
| cz_discount_prior_price | § 12a ZOS | kód | produktové stránky, akce |
| cz_payment_methods | § 1820 odst. 1 písm. h) OZ; § 11a ZOS | jev | Doprava a platba, obchodní podmínky |
| cz_delivery_methods | § 1820 odst. 1 písm. h) OZ | jev | Doprava a platba, obchodní podmínky |
| cz_delivery_time | § 1820 odst. 1 písm. h) OZ; § 2159 odst. 1 OZ | jev + kód | Doprava a platba, produktové stránky |
| cz_delivery_restrictions | § 11a ZOS | jev | Doprava a platba, obchodní podmínky |
| cz_cross_border_delivery | čl. 7 nař. 2018/644; § 24 odst. 6 písm. b) ZOS | jev + kód | Doprava a platba |
| cz_complaint_handling | § 1820 odst. 1 písm. h) OZ; § 1811 odst. 2 písm. d) OZ | jev | obchodní podmínky, Kontakt |
| cz_advance_payment | § 1820 odst. 1 písm. q) OZ | jev | obchodní podmínky, Doprava a platba |
| cz_withdrawal (existuje) | § 1820 odst. 1 písm. i) OZ; § 1829 odst. 1 OZ | jev + kód | obchodní podmínky, Odstoupení |
| cz_withdrawal_form (existuje) | § 1820 odst. 1 písm. i) OZ; NV 29/2023 | jev | obchodní podmínky, Odstoupení, PDF |
| cz_withdrawal_period_start | § 1829 odst. 1 a 2 OZ | jev + kód | obchodní podmínky, Odstoupení |
| cz_withdrawal_return_costs | § 1820 odst. 1 písm. j) OZ; § 1832 odst. 3 OZ | jev + kód | obchodní podmínky, Odstoupení |
| cz_withdrawal_service_payment | § 1820 odst. 1 písm. k) OZ | jev | obchodní podmínky |
| cz_withdrawal_exceptions | § 1820 odst. 1 písm. l) OZ; § 1837 OZ | jev | obchodní podmínky, Odstoupení |
| cz_withdrawal_button | § 1820 odst. 1 písm. i) a § 1830a OZ ve znění 159/2026 (od 1. 1. 2027) | jev + kód | celý web, účet, obchodní podmínky |
| cz_complaints (existuje) | § 13 odst. 1 ZOS; § 1820 odst. 1 písm. m) OZ | jev | reklamační řád, obchodní podmínky |
| cz_complaint_place | § 13 odst. 1 ZOS; § 2172 OZ | jev + kód | reklamační řád, obchodní podmínky |
| cz_defect_period | § 2165 odst. 1 OZ; § 2168 OZ | kód | reklamační řád, obchodní podmínky |
| cz_guarantee_after_sales | § 1820 odst. 1 písm. m) OZ; § 2174a OZ | jev | reklamační řád, produktové stránky |
| cz_code_of_conduct | § 1820 odst. 1 písm. n) OZ | jev | obchodní podmínky, patička |
| cz_contract_duration | § 1820 odst. 1 písm. o) OZ | jev + kód | obchodní podmínky, předplatné |
| cz_minimum_duration | § 1820 odst. 1 písm. p) OZ | kód | obchodní podmínky, předplatné |
| cz_digital_compatibility | § 1820 odst. 1 písm. r) OZ; § 1811 odst. 2 písm. h) a i) OZ | jev | produktové stránky |
| cz_adr (existuje) | § 14 odst. 1 ZOS; § 1820 odst. 1 písm. s) OZ | jev | obchodní podmínky, reklamační řád |
| cz_supervisory_authority | § 1820 odst. 1 písm. s) OZ | jev | obchodní podmínky |
| cz_contract_storage | § 1826 odst. 1 písm. a) OZ | jev | obchodní podmínky |
| cz_contract_languages | § 1826 odst. 1 písm. b) OZ | jev | obchodní podmínky |
| cz_order_steps | § 1826 odst. 1 písm. c) OZ | jev | obchodní podmínky, Jak nakupovat |
| cz_input_error_correction | § 1826 odst. 1 písm. d) OZ | jev | obchodní podmínky, Jak nakupovat |
| cz_order_review | § 1826 odst. 2 OZ | nelze z textu | pokladna |
| cz_pre_order_summary | § 1826a odst. 1 OZ | nelze z textu | pokladna |
| cz_order_button | § 1826a odst. 2 OZ | nelze z textu | pokladna |
| cz_order_confirmation_terms | § 1824a odst. 1, § 1827 OZ | nelze z textu | e-mail po objednávce |
| cz_extra_payment_consent | § 1817 OZ | nelze z textu | košík, pokladna |
| cz_language | § 11 odst. 1 ZOS; § 1811 odst. 1 OZ | kód | produktové stránky, právní stránky |
| cz_units | § 11 odst. 3 ZOS | kód | produktové stránky |
| cz_legibility | § 1824 odst. 1 OZ | nelze z textu | všude |
| cz_reviews_verification | § 5a odst. 5 ZOS | jev + kód | produktové stránky, recenze, obchodní podmínky |
| cz_marketplace_ranking | § 11b písm. a) ZOS; § 5a odst. 4 ZOS | jev | tržiště: řazení, obchodní podmínky |
| cz_marketplace_seller_status | § 11b písm. b) až d) ZOS; § 5a odst. 3 písm. f) ZOS | jev | tržiště: produktové stránky, obchodní podmínky |
| cz_used_goods | § 10 ZOS | nelze z textu | produktové stránky |
| cz_footwear_materials | § 10a odst. 1 ZOS | jev | produktové stránky obuvi |
| cz_use_instructions | § 9 ZOS | nelze z textu | s výrobkem |
| cz_deposit_packaging | § 18 odst. 1 ZOS | jev + kód | produktové stránky nápojů |
| cz_best_before | § 2163 OZ | nelze z textu | na výrobku |
| cz_gpsr_manufacturer | čl. 19 písm. a) a b) nař. 2023/988 | jev + kód | produktové stránky |
| cz_gpsr_product_id | čl. 19 písm. c) nař. 2023/988 | kód | produktové stránky |
| cz_gpsr_warnings | čl. 19 písm. d) nař. 2023/988 | nelze z textu | produktové stránky |
| cz_take_back | § 18 odst. 3 zák. 542/2020 | jev | Zpětný odběr, obchodní podmínky |
| cz_empco_pending | čl. 2 směrnice 2024/825 (v ČR netransponováno) | zatím neaktivovat | produktové stránky, Doprava a platba |
