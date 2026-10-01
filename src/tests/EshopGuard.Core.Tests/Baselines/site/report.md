# Kontrola textů e-shopu fixture.test

> Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.

> **Běh s falešným klientem (mock):** pravděpodobnosti nepocházejí z Jevu, nálezy slouží jen k ověření průchodu aplikací.

| | |
| --- | --- |
| Web | http://fixture.test/ |
| Datum | - |
| Moduly |  |
| Země | CZ |
| Sady otázek | ucp-2026-09-26-draft4 (ucp.yaml), legal-cz-2026-10-01-draft4 (legal_cz.yaml) |
| Model | mock |
| Jazyk otázek | angličtina |

## Statistika

- Stránky: 13 (úvodní 1, produktová 9, právní 2, ostatní 1)
- Viditelný text: zkontrolováno 73 % (hlavní text, hlavička a patička, ostatní text stránky), navigace a filtry 28 % záměrně vynechané, výpisy jiných produktů 0 % (kontrolují se na stránce produktu), jinak nezkontrolováno 0 %
- Profily šablon stránek: použito 0 (z toho nových 0, plánováno nových 1, nevytvořeny), stránky podle profilu 0, bez profilu 11 (kontrolované celé)
- Stahování: 15 požadavků
- Segmenty: 154 výskytů, 116 unikátních (108 vět, 8 právních odstavců, 3 šablonových)
- Volání Jevu: 100, odpovědi z cache: 0, chyby: 0
- Síto po odstavcích (úseky do 600 znaků, práh 0,2): 11 úseků, volání 11, z cache 0, nevyhodnoceno 0; z podrobné kontroly vynechalo 16 z 108 dvojic věta × modul (15 %)
- Vstupní tokeny: 31 221 (odhad falešného klienta)
- Odhad ceny: 0,001311 USD (nic se neplatilo)
- Doba běhu: -

## Souhrn

Porušení podle textu zákona: 3, K ověření: 1, Platí později: 3.

| Skupina | Pravidlo | Závažnost | Vysoká jistota | Nižší jistota |
| --- | --- | --- | ---: | ---: |
| Porušení podle textu zákona | Chybí informace o mimosoudním řešení sporů | vysoká | 1 | 0 |
| Porušení podle textu zákona | Odměna za kladnou recenzi | vysoká | 1 | 0 |
| Porušení podle textu zákona | Chybí vzorový formulář pro odstoupení | střední | 1 | 0 |
| K ověření | Tvrzení o ověřených recenzích k ověření | střední | 1 | 0 |
| Platí později | Chybí tlačítko „Odstoupit od smlouvy“ (povinné od 1. 1. 2027) | vysoká | 0 | 1 |
| Platí později | Chybí informace, kde je tlačítko pro odstoupení (povinná od 1. 1. 2027) | vysoká | 0 | 1 |
| Platí později | Chybí informace o možnosti odstoupit tlačítkem (povinná od 1. 1. 2027) | vysoká | 0 | 1 |

Jistota říká, jak si je Jev jistý, že text odpovídá popisu pravidla; zda jde o porušení, určuje skupina.

## Porušení podle textu zákona (3)

Text webu splňuje znaky zákazu tak, jak je popisuje zákon nebo jeho odůvodnění; výjimky (například ekoznačka EU) uvádí vysvětlení u pravidla.

### Chybí informace o mimosoudním řešení sporů

Pravidlo `legal_adr_missing`, závažnost vysoká, porušení podle textu zákona.

Prodávající musí spotřebitele informovat o subjektu mimosoudního řešení spotřebitelských sporů.

**Doporučení:** Doplňte do obchodních podmínek příslušný subjekt (typicky ČOI) a odkaz na jeho web.

**Předpisy:**

- CZ: § 14 odst. 1 zákona č. 634/1992 Sb., o ochraně spotřebitele (status: ověřit)
- CZ: § 1820 odst. 1 písm. s) zákona č. 89/2012 Sb., občanský zákoník (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,93, vysoká jistota
   - Nejbližší odstavec: „Lhůty – Práva z vadného plnění můžete uplatnit do 24 měsíců od převzetí zboží. Reklamaci vyřídíme nejpozději do 30 dnů od jejího uplatnění.“
   - Stránky (1): http://fixture.test/reklamacni-rad.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,08, práh přítomnosti je 0,70.

### Odměna za kladnou recenzi

Pravidlo `ucp_review_reward_positive`, závažnost vysoká, porušení podle textu zákona.

Odměna za kladné hodnocení vede k falešným recenzím; zadávání falešných recenzí je zakázané za všech okolností.

**Doporučení:** Odměnu za recenzi nepodmiňujte jejím obsahem ani hodnocením.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23c; Pokyny Komise 2021/C 526/01, oddíl 4.2.4 (odměna za zveřejnění pozitivních recenzí) (status: ověřit)
- CZ: § 4 odst. 4 a příloha č. 1 písm. z) zákona č. 634/1992 Sb. (status: ověřit)

1. „Za hodnocení 5 hvězdičkami vám vrátíme 100 Kč.“ – skóre 0,89, vysoká jistota
   - Kontext: …Cena: 189 Kč Recenze **[věta]** …
   - Pravděpodobnosti: ucp_review_reward=0.885; ucp_review_reward_positive=0.934
   - Stránky (1): http://fixture.test/produkt-1.html

### Chybí vzorový formulář pro odstoupení

Pravidlo `legal_withdrawal_form_missing`, závažnost střední, porušení podle textu zákona.

Spolu s informací o odstoupení musí spotřebitel dostat vzorový formulář.

**Doporučení:** Přidejte vzorový formulář pro odstoupení od smlouvy.

**Předpisy:**

- CZ: § 1820 odst. 1 písm. i) zákona č. 89/2012 Sb., občanský zákoník (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,91, vysoká jistota
   - Nejbližší odstavec: „Vyřízení reklamace – O vyřízení reklamace vás budeme informovat e-mailem a vydáme vám potvrzení o datu a způsobu vyřízení reklamace.“
   - Stránky (1): http://fixture.test/reklamacni-rad.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,09, práh přítomnosti je 0,70.

## K ověření (1)

Tvrzení nebo chybějící informace je na webu vidět, ale zda jde o porušení, záleží na faktech mimo web (certifikace značky, pravdivost údaje, košík a pokladna, které nástroj nestahuje).

### Tvrzení o ověřených recenzích k ověření

Pravidlo `ucp_reviews_verified_claim`, závažnost střední, k ověření, záleží na faktech mimo web.

Tvrdit, že recenze píší zákazníci, kteří produkt koupili nebo použili, je zakázané, pokud obchod nepřijal přiměřená opatření k ověření (například hodnotit smí jen zákazník s dokončenou objednávkou). Opatření na webu vidět nejsou, proto jde o nález k ověření.

**Doporučení:** Ověřte, že obchod skutečně kontroluje původ recenzí (například podle čísla objednávky), a popište to přímo u recenzí.

**Předpisy:**

- EU: Směrnice 2005/29/ES, příloha I bod 23b; Pokyny Komise 2021/C 526/01, oddíl 4.2.4 (opodstatněné a přiměřené kroky k ověření) (status: ověřit)
- CZ: § 4 odst. 4 a příloha č. 1 písm. y) zákona č. 634/1992 Sb. (status: ověřit)

1. „Všechny recenze jsou od ověřených zákazníků.“ – skóre 0,93, vysoká jistota
   - Kontext: …Krém je vhodný pro suchou pleť, obsah balení je 50 ml. Recenze **[věta]** …
   - Pravděpodobnosti: ucp_reviews_verified_claim=0.925
   - Stránky (1): http://fixture.test/produkt-4.html

## Platí později (3)

Povinnost zatím neplatí: pravidlo má datum účinnosti v budoucnu, nález je upozornění dopředu. Skupinu, do které po účinnosti patří, uvádí řádek pravidla.

### Chybí tlačítko „Odstoupit od smlouvy“ (povinné od 1. 1. 2027)

Pravidlo `legal_withdrawal_function_missing`, závažnost vysoká, k ověření, záleží na faktech mimo web.

Od 1. 1. 2027 musí e-shop umožnit spotřebiteli odstoupit od smlouvy také prohlášením v on-line rozhraní pomocí tlačítka nebo obdobného ovládacího prvku. Ten musí být zobrazený výrazným způsobem, snadno přístupný, dostupný nepřetržitě po celou lhůtu pro odstoupení a označený snadno čitelným nápisem „Odstoupit od smlouvy“ nebo jinou odpovídající jednoznačnou formulací. Na prohledaných stránkách se odkaz ani tlačítko s takovým nápisem nenašly.

**Doporučení:** Doplňte výrazně viditelné tlačítko nebo odkaz „Odstoupit od smlouvy“ (například v patičce a v zákaznickém účtu), které vede k vyplnění a potvrzení prohlášení o odstoupení.

**Předpisy:**

- EU: Směrnice 2011/83/EU, čl. 11a, vložený směrnicí (EU) 2023/2673 (použije se od 19. 6. 2026) (status: ověřit)
- CZ: § 1830a odst. 1 a 2 zákona č. 89/2012 Sb., občanský zákoník, ve znění zákona č. 159/2026 Sb. (účinnost od 1. 1. 2027) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 1,00, nižší jistota
   - Poznámka: Na žádné z 13 stažených stránek se nenašel obrázek, odkaz ani text, který by to ukazoval.
   - Poznámka: Košík, pokladnu a zákaznický účet nástroj nestahuje; tam to ověřte ručně.
   - Poznámka: Povinnost platí od 1. 1. 2027; do té doby jde o upozornění dopředu.

### Chybí informace, kde je tlačítko pro odstoupení (povinná od 1. 1. 2027)

Pravidlo `legal_withdrawal_button_location_missing`, závažnost vysoká, porušení podle textu zákona.

Od 1. 1. 2027 musí informace před uzavřením smlouvy uzavírané prostřednictvím on-line rozhraní obsahovat i údaj o umístění tlačítka nebo obdobného ovládacího prvku pro odstoupení od smlouvy.

**Doporučení:** Doplňte do obchodních podmínek nebo poučení o odstoupení, kde na webu tlačítko „Odstoupit od smlouvy“ najdete (například stránku, položku menu nebo zákaznický účet).

**Předpisy:**

- CZ: § 1820 odst. 1 písm. i) zákona č. 89/2012 Sb., občanský zákoník, ve znění zákona č. 159/2026 Sb. (účinnost od 1. 1. 2027) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,95, nižší jistota
   - Nejbližší odstavec: „2. Objednávka a uzavření smlouvy – Kupní smlouva vzniká odesláním potvrzení objednávky na e-mail kupujícího.“
   - Stránky (1): http://fixture.test/obchodni-podminky.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,05, práh přítomnosti je 0,70.
   - Poznámka: Povinnost platí od 1. 1. 2027; do té doby jde o upozornění dopředu.

### Chybí informace o možnosti odstoupit tlačítkem (povinná od 1. 1. 2027)

Pravidlo `legal_withdrawal_button_info_missing`, závažnost vysoká, porušení podle textu zákona.

Od 1. 1. 2027 musí informace před uzavřením smlouvy uzavírané prostřednictvím on-line rozhraní obsahovat i údaj o možnosti odstoupit od smlouvy také použitím tlačítka nebo obdobného ovládacího prvku pro odstoupení.

**Doporučení:** Doplňte do obchodních podmínek nebo poučení o odstoupení, že od smlouvy lze odstoupit také tlačítkem „Odstoupit od smlouvy“ na webu.

**Předpisy:**

- CZ: § 1820 odst. 1 písm. i) zákona č. 89/2012 Sb., občanský zákoník, ve znění zákona č. 159/2026 Sb. (účinnost od 1. 1. 2027) (status: ověřit)

1. **Informace na webu nenalezena** – skóre 0,90, nižší jistota
   - Nejbližší odstavec: „2. Objednávka a uzavření smlouvy – Kupní smlouva vzniká odesláním potvrzení objednávky na e-mail kupujícího.“
   - Stránky (1): http://fixture.test/obchodni-podminky.html
   - Poznámka: Nejbližší nalezený odstavec má pravděpodobnost 0,10, práh přítomnosti je 0,70.
   - Poznámka: Povinnost platí od 1. 1. 2027; do té doby jde o upozornění dopředu.

## Obrázky k ruční kontrole

Jev obrázky nevidí. Tyto obrázky mají v alt textu nebo názvu souboru environmentální slovo, zkontrolujte je ručně.

| Stránka | Soubor | Alt text | Slovo |
| --- | --- | --- | --- |
| http://fixture.test/produkt-2.html | mydlo-eco-obal.jpg | Mýdlo v papírovém obalu | eco |
| http://fixture.test/produkt-5.html | eu-ecolabel.png | EU Ecolabel | eco |

## Co nebylo zkontrolováno

- Text v obrázcích (Jev obrázky nevidí; podezřelé obrázky jsou vypsané výše).
- Části stránek, které web dotahuje až JavaScriptem (widgety recenzí, odpočty, záložky načítané po kliknutí). Stránky, které by byly bez JavaScriptu celé prázdné, nástroj hlásí zvlášť; v tomto běhu žádné nebyly.
- Navigace a filtry (menu, seznamy kategorií, drobečková navigace, volby filtrů): jde o odkazy a volby, ne o tvrzení.
- Výpisy jiných produktů na stránce (podobné produkty, dlaždice s cenou a odkazem na jiný produkt): jejich texty se kontrolují na stránce toho produktu; při kontrole jen vzorku stránek se produkty mimo vzorek nekontrolují.
- Procesní povinnosti, které z textu webu nevyplývají (např. zda se reklamace skutečně vyřizují včas).
- Stránky nad limit: 0 nestažených, 0 produktových nad limit vzorku; 1 zakázaných v robots.txt.
- Věty v úsecích, kde síto nenašlo téma modulu: 16 dvojic věta × modul bez podrobných otázek; úseky a pravděpodobnosti síta jsou v sieve.csv, bez síta běží sken s volbou --no-sieve.
- Právní dokumenty v PDF: žádné odkazy nenalezeny.

---

Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.
