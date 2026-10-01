# Porovnání se slovníkovými doplňky pro WooCommerce (30. 9. 2026)

Se svolením uživatele jsme stáhli tři doplňky z downloads.wordpress.org, přečetli jejich kód a slovníky přehráli doslova na stránkách našich dřívějších skenů:

| Soubor | Doplněk | Velikost | Aktivní instalace |
|---|---|---|---|
| green-claims-checker-for-woocommerce.0.1.0.zip | Jawnie | 17 663 B | 0 |
| lodestone-green-claims.1.0.1.zip | Lodestone | 1 150 357 B | 0 |
| claimprism.0.10.1.zip | ClaimPrism | 1 084 401 B | 0 |

Porovnávaly se stránky vegis.sk (108 stránek, běh vegis.sk-20260930-0919) a naturfyt.sk (43 stránek, běh naturfyt.sk-20260930-0926). Doplňky jsme neinstalovali ani nespouštěli; jejich hledání jsme přepsali do Pythonu (`scratchpad/plugins/compare.py`). Nic se neplatilo.

## Co doplňky umí

| | Jawnie | ClaimPrism | Lodestone |
|---|---|---|---|
| Metoda | výrazy ze slovníku, bez kontextu | výrazy ze slovníku; když je poblíž EU Ecolabel nebo podobná uznaná značka, sníží závažnost | výrazy ze slovníku a okno zhruba dvou vět: hledá upřesnění (název schématu, procenta, měřítka), „100 %“ bez části výrobku, budoucí čas, kompenzace; spolehlivost high/low |
| Počet výrazů | 49 (EN 17, PL 11, DE 9, kompenzace 5, budoucnost 4) | ~120 ve 4 pravidlech a 5 vzorů pro budoucnost (EN, IT, DE, FR, ES, NL) | 371 v 7 jazycích (EN 102, DE 49, FR 47, NL 45, ES 43, PT 43, IT 42) a 25 názvů schémat |
| Jazyky | EN, PL, DE | EN, IT, DE, FR, ES, NL | EN, DE, FR, ES, IT, NL, PT; na slovenském webu podle nastavení jen EN |
| Čeština, slovenština | ne | ne | ne |
| Co prochází | produkty a příspěvky: název, krátký popis, obsah | totéž, SEO titulek a popis (Yoast, RankMath, AIOSEO), alt texty obrázků, i koncepty | název, krátký popis, obsah; kontrola při uložení |
| Pravidla | obecné tvrzení, kompenzace, budoucnost | obecné tvrzení, neutralita, kompenzace, budoucnost, značka | 4a obecné, 4b část za celek, 4c kompenzace, 2a značka, 10a zákonný požadavek jako přednost (cruelty-free, BPA-free…), budoucnost |
| Co dál | nic | nic | záznamy dokladů (certifikát uložený jednou platí pro všechny výrobky), ruční výjimky pro vlastní značku |
| Oprava textu, zápis do e-shopu, životnost, recenze, povinné informace | ne | ne | ne |

## Výsledek na slovenských stránkách

| | vegis.sk (108 stránek) | naturfyt.sk (43 stránek) |
|---|---|---|
| **Naše nálezy eco/dur/ucp** | **40** (13 porušení, 23 k posouzení, 4 k ověření) | **32** (10 porušení, 18 k posouzení, 4 k ověření) |
| Jawnie | 8 zásahů na 2 stránkách | 7 zásahů na 5 stránkách |
| ClaimPrism | 1 zásah | 0 |
| Lodestone, jen EN (výchozí pro SK) | 17 zásahů na 7 stránkách | 10 na 6 stránkách |
| Lodestone, všech 7 jazyků | 18 na 8 stránkách | 13 na 6 stránkách |

- **Chytí jen anglická slova** v jinak slovenském textu: green, eco, eco-friendly, natural, clean, cruelty-free.
- **Z našich 13 porušení na vegis.sk** chytí slovníky jediné, „eco-friendly alternatíva“. Nechytí žádné slovenské tvrzení:
  - „ekologická CO₂ extrakcia“, „ekologickom sete“, „biologicky rozložiteľná rukoväť“, „ekologicky zmýšľajúcich“;
  - „z udržateľných zdrojov“, „pre pokožku aj pre planétu“;
  - „šetrný k životnému prostrediu“, „ekologická alternatíva“.
- **Mimo jejich záběr** zůstávají vůbec:
  - ověřování značek proti seznamu (BDIH, Ecogarantie, Ecocert, COSMOS, SGS/TÜV);
  - životnost („Doba horenia: 22 hodín“, „vymeňte každé 2–3 mesiace“);
  - povinné informace (poučení, harmonizované oznámení, tlačítko odstoupení, ODR).
- **Zásahy, které máme jen oni** (vegis 10, naturfyt 4), jsou skoro všechny falešné:
  - „Natural Moisturizing Factors“ je složka;
  - „Technologie Naturelle“ je název firmy;
  - „Medveď Natural“ je značka;
  - „Green“ a „natural“ jsou uvnitř seznamu složení;
  - „clean fresh“ popisuje vůni.
- **Sporné, k ručnímu ověření u nás:**
  - „Vegis - all natural, BIO obchod“ v titulku webu;
  - „clean beauty kozmetika“;
  - „Brečtanový olej natural“.

## Co převzít

1. **Doklad zadaný jednou platí všude** (Lodestone). Obchodník odpoví „máme certifikát COSMOS pro značku X“ jednou a odpověď se použije u všech výrobků. Hodí se do sekce „Potrebujeme vašu odpoveď“.
2. **Kontrola před zveřejněním** (ClaimPrism kontroluje koncepty, Lodestone kontroluje při uložení). U konektorů přes webhook při uložení produktu.
3. **U každého pravidla říct, co ho vyřeší**, podle `cured_by` u Lodestone:
   - upřesnění v textu;
   - doklad;
   - plán;
   - nic, text se musí změnit.
4. **Alt texty obrázků a SEO popisy.** Titulek, meta popis a JSON-LD kontrolujeme, obrázky s eko výrazy zatím jen vypisujeme k ruční kontrole.
5. **Zákonný požadavek jako přednost** (cruelty-free, BPA-free) máme v modulu `lr`, který je vypnutý do ověření na skutečném e-shopu.
6. **Poznámka Lodestone k „non-toxic“ podle čl. 25 odst. 4 nařízení CLP** (u nebezpečných látek a směsí). V našich podkladech to NEMÁME ověřené. Pro ekodrogérii zvážit.

## Poctivě k našim přednostem

- **Falešnou naléhavost** („len dnes“, „posledné kusy“) máme připravenou v `rules/ucp_parked.yaml`, ale je VYPNUTÁ (`enabled: false`). Jako hotovou přednost ji zatím uvádět nelze.
- **Naše nálezy nejsou ground truth.** Srovnání ukazuje záběr, ne přesnost. Přesnost jsme měřili ručně na starších bězích (naturfyt 26. 9., vegis 29. 9.).
