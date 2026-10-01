# Kontrola textů e-shopu fixture.test

> Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.

> **Běh s falešným klientem (mock):** pravděpodobnosti nepocházejí z Jevu, nálezy slouží jen k ověření průchodu aplikací.

| | |
| --- | --- |
| Web | http://fixture.test/ |
| Datum | - |
| Moduly |  |
| Země | CZ |
| Sady otázek | ucp-2026-09-26-draft4 (ucp.yaml), legal-cz-2026-09-29-draft3 (legal_cz.yaml) |
| Model | mock |
| Jazyk otázek | angličtina |

## Statistika

- Stránky: 5 (úvodní 1, produktová 2, právní 2, ostatní 0)
- Viditelný text: zkontrolováno 80 % (hlavní text, hlavička a patička, ostatní text stránky), navigace a filtry 20 % záměrně vynechané, výpisy jiných produktů 0 % (kontrolují se na stránce produktu), jinak nezkontrolováno 0 %
- Stahování: 7 požadavků
- Segmenty: 76 výskytů, 64 unikátních (56 vět, 8 právních odstavců, 3 šablonových)
- Volání Jevu: 64, odpovědi z cache: 0, chyby: 0
- Síto po odstavcích (úseky do 600 znaků, práh 0,2): 3 úseků, volání 3, z cache 0, nevyhodnoceno 0; z podrobné kontroly vynechalo 0 z 56 dvojic věta × modul (0 %)
- Vstupní tokeny: 19 396 (odhad falešného klienta)
- Odhad ceny: 0,000815 USD (nic se neplatilo)
- Doba běhu: -

## Souhrn

Porušení podle textu zákona: 3.

| Skupina | Pravidlo | Závažnost | Vysoká jistota | Nižší jistota |
| --- | --- | --- | ---: | ---: |
| Porušení podle textu zákona | Chybí informace o mimosoudním řešení sporů | vysoká | 1 | 0 |
| Porušení podle textu zákona | Odměna za kladnou recenzi | vysoká | 1 | 0 |
| Porušení podle textu zákona | Chybí vzorový formulář pro odstoupení | střední | 1 | 0 |

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

## Obrázky k ruční kontrole

Jev obrázky nevidí. Tyto obrázky mají v alt textu nebo názvu souboru environmentální slovo, zkontrolujte je ručně.

| Stránka | Soubor | Alt text | Slovo |
| --- | --- | --- | --- |
| http://fixture.test/produkt-2.html | mydlo-eco-obal.jpg | Mýdlo v papírovém obalu | eco |

## Co nebylo zkontrolováno

- Text v obrázcích (Jev obrázky nevidí; podezřelé obrázky jsou vypsané výše).
- Části stránek, které web dotahuje až JavaScriptem (widgety recenzí, odpočty, záložky načítané po kliknutí). Stránky, které by byly bez JavaScriptu celé prázdné, nástroj hlásí zvlášť; v tomto běhu žádné nebyly.
- Navigace a filtry (menu, seznamy kategorií, drobečková navigace, volby filtrů): jde o odkazy a volby, ne o tvrzení.
- Výpisy jiných produktů na stránce (podobné produkty, dlaždice s cenou a odkazem na jiný produkt): jejich texty se kontrolují na stránce toho produktu; při kontrole jen vzorku stránek se produkty mimo vzorek nekontrolují.
- Procesní povinnosti, které z textu webu nevyplývají (např. zda se reklamace skutečně vyřizují včas).
- Stránky nad limit: 8 nestažených, 0 produktových nad limit vzorku; 1 zakázaných v robots.txt.
- Právní dokumenty v PDF: žádné odkazy nenalezeny.

---

Jde o automatický screening, ne o právní posouzení. Nálezy „k posouzení“ a „k ověření“ vyžadují ruční kontrolu; právní odkazy se statusem „ověřit“ nebo „doplnit“ musí zkontrolovat právník.
