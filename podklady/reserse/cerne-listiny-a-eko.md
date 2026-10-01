# Černé listiny nekalých obchodních praktik: co jde poznat z textu e-shopu

25. 9. 2026 · rešerše pro `eshop-guard` (otázky pro Jev) · návrh k právní kontrole, ne právní výklad

## Shrnutí

- **47 bodů** má černá listina ve znění po EmpCo: směrnice 2005/29/ES, příloha I k 27. 9. 2026, 39 klamavých a 8 agresivních praktik. **Slovensko** jich má od 27. 9. 2026 také 47 (príloha č. 1 zákona 108/2024 Z. z.: 39 klamlivých a 8 agresívnych). **Česko** 34: příloha č. 1 písm. a)–z) (klamavé) a příloha č. 2 písm. a)–h) (agresivní); body EU 1 a 3 jsou sloučené v písm. a). Dvanáct bodů z EmpCo (2a, 4a–4c, 10a, 23d–23j) Česko nemá, novela (sněmovní tisk 53) k 25. 9. 2026 není schválená.
- **Přes Jev jde 31 bodů.** 9 rozhodne text sám („jev“). U 22 je tvrzení vidět, ale nekalé je jen při nepravdivosti nebo chybějícím faktu mimo web („jev + ověřit“, nález „k ověření“). 1 bod jen kódem (vábivá reklama), 15 z textu webu poznat nejde (agresivní praktiky v osobním a telefonickém kontaktu, aktualizace softwaru, přeprodej vstupenek, faktury v poště).
- Z 31 bodů už pokrývá `rules/eco.yaml` 4 (2a, 4a, 4b, 4c). Pro zbylých 27 navrhuji **39 otázek ano/ne** v novém modulu `ucp`, rozdělených podle priority do tří sad, plus doplňky do eco a legal.
- **Top 5 pro e-shopy:** (1) zákonná práva jako výhoda nabídky, bod 10; (2) „zdarma“ s poplatkem, bod 20; (3) „jen dnes“ a „poslední kusy“, bod 7; (4) léčebná tvrzení, bod 17; (5) recenze: „ověřené recenze“ bez ověřování a odměna za kladné hodnocení, body 23b a 23c.
- **Zjištění k eco.yaml:** (a) vysvětlení u `eco_neutrality` „zakázané vždy“ neplatí pro tvrzení o firmě jako celku (Q&A Komise, ot. 10) a bod 4c se týká jen tvrzení založených na kompenzacích; (b) `eco_neutral` nepokrývá „snížený“ a „pozitivní“ dopad z bodu 4c; (c) „udržitelný“ a „odpovědný“ nejde opřít jen o ekoznačku (bod 10 odůvodnění EmpCo), výjimka podle seznamu značek na stránce u nich nemá platit; (d) bod 2a platí i pro sociální značky (fair trade, dobré životní podmínky zvířat).
- **Staženo:** konsolidované znění směrnice 2005/29/ES k 27. 9. 2026 (česky), Pokyny Komise k výkladu směrnice 2005/29/ES z roku 2021 (česky), prováděcí nařízení (EU) 2025/1960 o harmonizovaném oznámení a štítku (česky).

## Zdroje

| Zkratka | Soubor v `D:\_github\Overko\podklady\` | Co to je |
| --- | --- | --- |
| EU | `predpisy-eu/eu-2005-29-ucpd-konsolidace-2026-09-27-cs.txt` (+ `.xhtml`) | Směrnice 2005/29/ES, konsolidované znění 02005L0029 k 27. 9. 2026, česky. Příloha I od ř. 888. Staženo 25. 9. 2026 z Cellaru `http://publications.europa.eu/resource/celex/02005L0029-20260927` (verze `-20240326` neexistuje, vrací 404). |
| EmpCo | `predpisy-eu/eu-2024-825-empco-cs.txt` | Směrnice (EU) 2024/825, odůvodnění a čl. 1–2 |
| CZ | `predpisy-cz/cz-634-1992-zakon-o-ochrane-spotrebitele.txt` | Zákon č. 634/1992 Sb.; příloha č. 1 ř. 854–882, příloha č. 2 ř. 883–893, § 4–5c ř. 151–200 |
| SK | `predpisy-sk/sk-108-2024-ochrana-spotrebitela-zneni-od-2026-09-27.txt` | Zákon č. 108/2024 Z. z. ve znění od 27. 9. 2026; príloha č. 1 ř. 7655–7849, § 9–12 ř. 1573–1810 |
| SK-nov | `predpisy-sk/sk-310-2025-novela-empco.txt` | Novela č. 310/2025 Z. z.: přečíslování přílohy (čl. I body 50–53, ř. 556–621), účinnost (čl. V, ř. 807–810) |
| QA | `eu-komise/komise-qa-empco-2026-09.txt` | Otázky a odpovědi Komise k EmpCo, září 2026 (anglicky) |
| Pokyny | `eu-komise/komise-pokyny-ucpd-2021-cs.txt` (+ `.xhtml`) | Pokyny Komise k výkladu a uplatňování směrnice 2005/29/ES, 2021/C 526/01, česky. Staženo z Cellaru `celex/52021XC1229%2805%29` |
| 1960 | `predpisy-eu/eu-2025-1960-harmonizovane-oznameni-a-stitek-cs.txt` (+ `.xhtml`) | Prováděcí nařízení Komise (EU) 2025/1960; samotné oznámení a štítek jsou obrázky jen v `.xhtml` |
| OZ-CZ, OZ-SK | `predpisy-cz/cz-89-2012-obcansky-zakonik.txt`, `predpisy-sk/sk-40-1964-obciansky-zakonnik.txt` | Zákonné lhůty pro bod 10 |

Citace mají tvar „…“ [zkratka ř. N], čísla řádků platí pro uvedené `.txt`. Tři tečky značí zkrácení.

## Jak číst sekce

- **Čísla bodů:** EU = příloha I směrnice 2005/29/ES. CZ = zákon č. 634/1992 Sb., příloha č. 1 (klamavé) nebo č. 2 (agresivní). SK = zákon č. 108/2024 Z. z., príloha č. 1, časť Klamlivé (body 1–39) nebo Agresívne (body 1–8), číslování od 27. 9. 2026; v závorce číslo do 26. 9. 2026, pokud se změnilo (ověřeno ve znění od 31. 7. 2026 a v novele 310/2025, čl. I body 50–53). Body z EmpCo mají v CZ „—“: netransponováno.
- **Kontrola:** `jev` = rozhodne věta s kontextem. `jev + ověřit` = tvrzení je vidět, ale nekalé je jen při nepravdivosti nebo chybějícím faktu mimo web, nález je „k ověření“ (checkability `verify`). `kód` = rozhodne kód nad daty stránky. `nelze z textu` = praktika se v textu webu neprojeví.
- **Priorita pro e-shop:** A = častá na e-shopech a Jev ji pozná. B = častá jen u části e-shopů nebo slabší signál. C = okrajová. – = nekontroluje se.
- **Otázky:** věta jde Jevu jako `{sentence, context_before, context_after}`. Každá otázka se ptá na jeden znak a bez něj musí vyjít „ne“; proto je „Answer no if…“ přímo v textu otázky (klient posílá otázky `noul` bez `criteria`, viz `SegmentEvaluator.BuildQuestions`).
- **Skládání:** výňatek YAML ve formátu `rules/eco.yaml`, bez výchozích hodnot `scope: segment` a `bands: {high: 0.85, review: 0.5}`. `legal_refs: # stejné jako …` znamená zkopírovat odkazy z uvedeného pravidla. Všechny právní odkazy mají status „ověřit“.

## Návrh zapojení do nástroje

1. **Nový modul `ucp`** (unfair commercial practices), `applies_to: sentence`, `jurisdictions: [cz, sk]`, nad větami ze všech typů stránek jako eco. Jev dostane v jednom požadavku všechny otázky sady, proto návrh dělí otázky do sad podle priority:
   - `rules/ucp_a.yaml`: 14 otázek (body 7, 10, 17, 20, 23b, 23c),
   - `rules/ucp_b.yaml`: 17 otázek (body 1–4, 9, 10a, 15, 18, 19, 23g, 23h, 23j, 28, 31; body 1 a 3 mají prioritu C, ale sdílejí téma s body 2 a 4),
   - `rules/ucp_c.yaml`: 8 otázek (body 12, 13, 14, 16, 22, 23, 30),
   - `rules/ucp_sk.yaml` (`jurisdictions: [sk]`): štítek GARAN, informační povinnost platí zatím jen na Slovensku (sekce „Nové informační povinnosti“).
   Zda smí více sad sdílet `module: ucp`, jsem v kódu neověřil; případně `ucp_a`, `ucp_b`, `ucp_c`.
2. **Omezení současného enginu** (ověřeno v `src/EshopGuard.Core/Rules/RuleValidator.cs` a `RuleEngine.cs`):
   - podmínka pravidla smí odkazovat jen na otázku ze stejné sady; otázky, které pravidlo potřebuje, musí být ve stejném souboru,
   - id otázek a pravidel musí být unikátní napříč sadami pro stejnou zemi (proto prefix `ucp_`),
   - `code_checks` znají jen `allowlist_absent` (nález zmizí, když je položka seznamu ve větě, nebo zůstane jen na stránkách bez položky) a `regex_required` (jen u `site_presence`, jen text právních stránek),
   - `site_presence` hlásí chybějící informaci vždy, nejde ji podmínit (například „jen když web zobrazuje recenze“).
3. **Navržené nové kontroly v kódu** (odkazuji na ně v sekcích):
   - `list_present` a `regex_present` na úrovni věty: nález jen když věta obsahuje položku seznamu nebo vzor (bod 10a: seznam látek zakázaných pro kategorii; bod 23g: číslo s jednotkou),
   - `regex_page` a `regex_site`: vzor kdekoli na stránce nebo webu včetně alt textů a odkazů, jako poznámka nebo výjimka (bod 20: „balné“, „manipulační poplatek“; bod 17: „léčivý přípravek“; harmonizované oznámení),
   - `jsonld`: `Offer.availability`, `Offer.priceValidUntil`, `aggregateRating` a `review` z JSON-LD (body 5, 7, 23b),
   - `persist_across_scans`: srovnání s předchozím skenem stejného webu (bod 7 „jen dnes“, bod 15 „končíme“); Pokyny Komise: nabídku lze formulovat jako časově omezenou jen tehdy, „nebudou-li k dispozici za stejnou cenu i později“ [Pokyny ř. 3801],
   - `site_presence_if`: podmíněná přítomnost informace (zobrazuje recenze → musí vysvětlit jejich ověřování).
4. **Cena:** navržené anglické otázky mají 140–450 znaků. Při odhadu nástroje 2,6 znaku na token má sada A asi 1,8 tis. tokenů otázek, všechny tři sady asi 4,8 tis.; k tomu věta s kontextem kolem 150 tokenů. Pro 10 000 vět to je asi 20 mil. vstupních tokenů (0,84 USD) za sadu A a 50 mil. (2,1 USD) za všechny tři při 0,042 USD za milion. Vliv počtu otázek v jednom požadavku na přesnost Jevu: NEOVĚŘENO.
5. **CZ odkazy u bodů EmpCo:** do schválení tisku 53 uvádět „netransponováno; do té doby jen obecný zákaz klamavých praktik, § 5 zákona 634/1992 Sb.“. Pro CZ je to posouzení případ od případu (test rozhodnutí o obchodní transakci), ne zákaz za všech okolností.

## Klamavé praktiky (EU body 1–23j)

### ucp_code_signatory

**Bod 1: tvrzení, že obchodník podepsal kodex chování**

- **Čísla bodů:** EU příloha I bod 1 · CZ příloha č. 1 písm. a) (spolu s bodem 3) · SK príloha č. 1 bod 1
- **Citace:**
  - EU: „Tvrzení, že obchodník podepsal kodex chování, ačkoli tomu tak není.“ [EU ř. 896]
  - CZ: „prohlašuje, že se zavázal dodržovat určitá pravidla chování (kodex chování) nebo že tato pravidla chování byla schválena určitým subjektem, ačkoli tomu tak není,“ [CZ ř. 857]
  - SK: „Tvrdenie obchodníka, že sa zaviazal dodržiavať kódex správania, pričom tomu tak nie je.“ [SK ř. 7659–7660]
- **Požadavek lidsky:** E-shop nesmí tvrdit, že dodržuje kodex (například etický kodex asociace e-commerce), když se k němu nezavázal.
- **Kontrola:** jev + ověřit. Tvrzení o kodexu je ve větě vidět, zda je obchodník signatářem, ukáže až seznam členů nebo signatářů.
- **Otázky pro Jev:**
  - `ucp_code_of_conduct` (společná pro body 1 a 3)
    - EN: "Does the sentence (field sentence) say that the shop or the company has signed, joined or follows a code of conduct, code of ethics or similar set of rules of an association or another body, or that such a code has been approved by someone? Answer no if no code of conduct or code of ethics is mentioned."
    - CS: „Uvádí věta (pole sentence), že obchod nebo firma podepsala kodex chování, etický kodex nebo podobná pravidla sdružení či jiného subjektu, přistoupila k nim nebo je dodržuje, případně že takový kodex někdo schválil? Pokud žádný kodex chování ani etický kodex nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_code_of_conduct_claim
    title: "Tvrzení o kodexu chování"
    logic:
      all: [{q: ucp_code_of_conduct, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I body 1 a 3", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. a)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 body 1 a 4", status: "ověřit"}
    explanation: "Tvrdit, že obchodník dodržuje kodex chování nebo že kodex někdo schválil, je zakázané, pokud to není pravda."
    recommendation: "Ověřte, že jste signatářem kodexu a že případné schválení kodexu skutečně existuje."
  ```
- **Závažnost:** low · verify
- **Poznámky:** Jedno pravidlo pro body 1 a 3, protože znak ve větě je stejný (zmínka o kodexu) a ověření taky. Kód by mohl porovnat jméno e-shopu se seznamy členů asociací (APEK, SAEC); zda seznamy existují strojově čitelné: NEOVĚŘENO. Pokyny Komise uvádějí příklad obchodníka, který na webu nesprávně uvádí, že podepsal kodex týkající se environmentální výkonnosti [Pokyny ř. 2998]. Když obchodník kodex podepsal, ale nedodržuje ho, jde o CZ § 5 odst. 3 písm. c) (případ od případu), ne o černou listinu. Priorita C.

### ucp_trust_mark

**Bod 2: značka důvěry nebo jakosti bez povolení**

- **Čísla bodů:** EU příloha I bod 2 · CZ příloha č. 1 písm. b) · SK príloha č. 1 bod 2
- **Citace:**
  - EU: „Používání značky důvěry, značky jakosti nebo rovnocenné značky bez získání potřebného povolení.“ [EU ř. 900]
  - CZ: „neoprávněně používá značku jakosti nebo jiné obdobné označení,“ [CZ ř. 858]
  - SK: „Zobrazenie známky dôveryhodnosti, známky kvality alebo ich ekvivalentu bez získania potrebného povolenia.“ [SK ř. 7662–7663]
- **Požadavek lidsky:** Certifikát obchodu, značku kvality nebo ocenění smí e-shop zobrazit, jen když má od vydavatele povolení (členství, licenci, platné ocenění).
- **Kontrola:** jev + ověřit. Zmínka o značce nebo certifikátu je ve větě vidět, povolení ne. Loga jsou často jen obrázky; ty Jev nevidí a zachytí je jen alt text nebo název souboru.
- **Otázky pro Jev:**
  - `ucp_trust_mark`
    - EN: "Does the sentence (field sentence) say that the shop or the product holds a trust mark, quality mark, certificate, seal or award (for example 'certified shop', 'APEK certificate', 'verified by customers', 'quality mark', 'Product of the Year', 'best in test')? Answer no if no mark, certificate or award is mentioned."
    - CS: „Uvádí věta (pole sentence), že obchod nebo produkt má značku důvěryhodnosti, značku kvality, certifikát, pečeť nebo ocenění (například „certifikovaný obchod“, „certifikát APEK“, „ověřeno zákazníky“, „značka kvality“, „Výrobek roku“, „vítěz testu“)? Pokud žádnou značku, certifikát ani ocenění nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_trust_mark_claim
    title: "Značka důvěry, jakosti nebo ocenění k ověření"
    logic:
      all: [{q: ucp_trust_mark, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 2", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. b)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 2", status: "ověřit"}
    explanation: "Značku důvěry, jakosti nebo podobné označení smí obchodník používat jen s povolením jejího vydavatele."
    recommendation: "Ověřte, že máte platné povolení nebo ocenění pro každou zobrazenou značku; neplatné odstraňte."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Environmentální a sociální značky řeší zvlášť bod 2a (`eco_label_unrecognized`), větu s ekoznačkou tedy mohou nahlásit obě pravidla; výjimku přes `eco_label` nejde napsat, protože otázka je v jiné sadě. Pokyny Komise jako příklad bodu 2 uvádějí používání ekoznačky EU, Nordic Swan nebo Blue Angel bez povolení [Pokyny ř. 3006]. Na CZ a SK e-shopech časté: APEK, SAEC „Dôveryhodný obchod“, Heureka „Ověřeno zákazníky“, Klasa, Regionální potravina. Priorita B.

### eco_label_unrecognized

**Bod 2a (EmpCo): označení udržitelnosti bez certifikace nebo veřejného orgánu**

- **Čísla bodů:** EU příloha I bod 2a · CZ — (netransponováno) · SK príloha č. 1 bod 3 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Uvedení označení udržitelnosti, které se nezakládá na systému certifikace nebo není zavedeno orgány veřejné správy.“ [EU ř. 906]
  - SK: „Zobrazenie značky udržateľnosti, ktorá nie je založená na certifikačnom systéme alebo ktorú nezaviedli orgány verejnej moci.“ [SK ř. 7665–7666]
- **Požadavek lidsky:** Značku udržitelnosti (environmentální i sociální) smí e-shop zobrazit, jen když stojí na certifikaci třetí stranou podle definice EmpCo nebo ji zavedl veřejný orgán EU nebo členského státu.
- **Kontrola:** jev + ověřit (+ kód: seznam `sustainability_labels`). Stávající stav.
- **Otázky pro Jev:** existující `eco_label`, `eco_organic_food` (`rules/eco.yaml`).
- **Skládání:** existující pravidlo `eco_label_unrecognized` (all `eco_label`, none `eco_organic_food`, `allowlist_absent` `sustainability_labels` ve větě), medium, verify. Beze změny.
- **Návrh doplnění:**
  - Definice označení udržitelnosti zahrnuje i sociální znaky („environmentální či sociální znaky, nebo obojí“ [EmpCo ř. 123]) a Q&A řadí mezi sociální znaky i dobré životní podmínky zvířat [QA ř. 609–615]. Nová otázka do eco:
    - `eco_social_label`
      - EN: "Does the sentence (field sentence) mention a label, seal or certificate about social or ethical characteristics, such as fair trade, working conditions or animal welfare (for example 'Fairtrade' or a 'cruelty free' logo)? Answer no if no such label is mentioned."
      - CS: „Zmiňuje věta (pole sentence) značku, pečeť nebo certifikát o sociálních či etických vlastnostech, například férovém obchodu, pracovních podmínkách nebo dobrých životních podmínkách zvířat (například „Fairtrade“ nebo logo „cruelty free“)? Pokud žádnou takovou značku nezmiňuje, odpověz ne.“
    - pravidlo `sust_social_label_unrecognized`: all `eco_social_label`, stejný `allowlist_absent` `sustainability_labels`, medium, verify.
  - Seznam `sustainability_labels` musí posoudit právník podle kritérií certifikačního systému (Q&A ot. 8: vlastník systému a kontrolující třetí strana musí být dvě právně oddělené osoby [QA ř. 579–583]; bez přechodného období [QA ř. 584–588]).
  - Značky zavedené veřejnými orgány mimo EU výjimku pro orgány veřejné správy nemají; povolené jsou jen tehdy, když stojí na certifikaci [QA ř. 843–845].
- **Poznámky:** Q&A: veganské a vegetariánské značky jsou značkou udržitelnosti jen podle kontextu, například „vegan = better for the planet“ [QA ř. 801–808]; zelené lístky a kapky u tvrzení mohou působit jako značka [QA ř. 402–406]. Priorita A (existující).

### ucp_code_endorsed

**Bod 3: tvrzení, že kodex schválil veřejný nebo jiný subjekt**

- **Čísla bodů:** EU příloha I bod 3 · CZ příloha č. 1 písm. a) (spolu s bodem 1) · SK príloha č. 1 bod 4 (do 26. 9. 2026 bod 3)
- **Citace:**
  - EU: „Tvrzení, že kodex chování byl schválen veřejným nebo jiným subjektem, ačkoli tomu tak není.“ [EU ř. 912]
  - CZ: stejné písm. a) jako u bodu 1 [CZ ř. 857]
  - SK: „Tvrdenie, že kódex správania je schválený orgánom verejnej moci alebo iným orgánom, pričom tomu tak nie je.“ [SK ř. 7668–7669]
- **Požadavek lidsky:** E-shop nesmí tvrdit, že kodex, který dodržuje, schválil úřad nebo jiná instituce, když to není pravda.
- **Kontrola:** jev + ověřit. Stejný znak jako bod 1.
- **Otázky pro Jev:** `ucp_code_of_conduct` (viz `ucp_code_signatory`).
- **Skládání:** stejné pravidlo `ucp_code_of_conduct_claim` s odkazem na body 1 a 3.
- **Závažnost:** low · verify
- **Poznámky:** Pokyny Komise: příklad nesprávného tvrzení, že kodex schválila národní agentura životního prostředí, ministerstvo nebo spotřebitelská organizace [Pokyny ř. 3014]. Priorita C.

### ucp_approval_claim

**Bod 4: tvrzení o schválení, potvrzení nebo povolení**

- **Čísla bodů:** EU příloha I bod 4 · CZ příloha č. 1 písm. c) · SK príloha č. 1 bod 5 (do 26. 9. 2026 bod 4)
- **Citace:**
  - EU: „Tvrzení, že obchodníku (jakož i jeho obchodním praktikám) nebo produktu bylo uděleno schválení, potvrzení nebo povolení veřejného nebo soukromého subjektu, ačkoli tomu tak není nebo takové tvrzení není v souladu s podmínkami schválení, potvrzení nebo povolení.“ [EU ř. 916]
  - CZ: „prohlašuje, že jemu, jeho výrobku nebo jím poskytované službě bylo uděleno schválení, potvrzení nebo povolení, ačkoli tomu tak není, …“ [CZ ř. 859]
  - SK: „Tvrdenie, že obchodník (vrátane jeho obchodných praktík) alebo produkt bol schválený, potvrdený alebo povolený orgánom verejnej moci alebo inou osobou, pričom tomu tak nie je, …“ [SK ř. 7671–7674]
- **Požadavek lidsky:** „Schváleno ministerstvem“, „doporučeno stomatology“, „certifikováno ústavem“ smí e-shop napsat, jen když schválení skutečně existuje a platí pro tento produkt v této podobě.
- **Kontrola:** jev + ověřit. Tvrzení o schválení je vidět, jeho existence ne.
- **Otázky pro Jev:**
  - `ucp_approval`
    - EN: "Does the sentence (field sentence) say that the product, the shop or the company has been approved, authorised, endorsed, certified or recommended by a public authority, a professional body or another organisation (for example 'approved by the Ministry of Health', 'recommended by the dental association', 'tested and approved by the State Health Institute')? Answer no if no such approval or endorsement is mentioned."
    - CS: „Uvádí věta (pole sentence), že produkt, obchod nebo firmu schválil, povolil, doporučil nebo certifikoval úřad, profesní organizace nebo jiná instituce (například „schváleno ministerstvem zdravotnictví“, „doporučeno stomatologickou komorou“, „testováno a schváleno Státním zdravotním ústavem“)? Pokud žádné takové schválení ani doporučení nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_approval_claim
    title: "Tvrzení o schválení nebo doporučení institucí"
    logic:
      all: [{q: ucp_approval, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 4", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. c)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 5", status: "ověřit"}
    explanation: "Tvrdit, že produkt nebo obchodník má schválení, potvrzení nebo povolení, je zakázané, pokud neexistuje nebo tvrzení neodpovídá jeho podmínkám."
    recommendation: "Mějte po ruce doklad o schválení a ověřte, že se týká přesně tohoto produktu a tvrzení."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Překryv s `ucp_trust_mark` („certifikováno“) je záměrný, jde o dva body. Odůvodnění EmpCo (bod 7) výslovně doplňuje bod 4 pravidlem pro certifikační ochranné známky [EmpCo ř. 33]. Typické na e-shopech s doplňky stravy, kosmetikou a zdravotními pomůckami. Priorita B.

### eco_generic_claim

**Bod 4a (EmpCo): obecné environmentální tvrzení bez uznaného vynikajícího profilu**

- **Čísla bodů:** EU příloha I bod 4a · CZ — (netransponováno) · SK príloha č. 1 bod 6 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Uvedení obecného environmentálního tvrzení, u kterého obchodník není schopen prokázat uznaný vynikající environmentální profil relevantní pro dané tvrzení.“ [EU ř. 922]
  - SK: „Všeobecné environmentálne tvrdenie v písomnej forme, ústnej forme alebo audiovizuálnej forme, ktoré sa neuvádza na značke udržateľnosti, a pri ktorom sa v tom istom komunikačnom prostriedku jasne a dôrazne neuvádza špecifikácia tvrdenia, …“ [SK ř. 7676–7680]
- **Požadavek lidsky:** „Ekologický“, „zelený“, „šetrný k přírodě“ bez konkrétního upřesnění hned u tvrzení smí nést jen produkt s EU Ecolabel, uznanou národní ekoznačkou typu I nebo nejvyšší úrovní podle jiného předpisu EU (například energetický štítek), a to relevantní pro dané tvrzení.
- **Kontrola:** jev (+ kód: seznam `excellent_performance_labels` na stránce). Stávající stav.
- **Otázky pro Jev:** existující `eco_claim`, `eco_generic`, `eco_organic_food`.
- **Skládání:** existující pravidlo `eco_generic_claim` (all `eco_claim`, `eco_generic`; none `eco_organic_food`; `allowlist_absent` `excellent_performance_labels` na stránce), high, text. Beze změny logiky.
- **Návrh doplnění:**
  - Profil musí být „relevantní pro dané tvrzení“ [EU ř. 922; QA ř. 490–496]: například „biologicky rozložitelný“ neomluví EU Ecolabel, pokud kritéria pro danou skupinu produktů rozložitelnost neobsahují. „Udržitelný“, „odpovědný“, „uvědomělý“ nejde opřít jen o environmentální profil, protože se týkají i sociálních znaků [EmpCo ř. 39]. Výjimka ze seznamu na stránce proto nemá platit pro tyto výrazy. Nová otázka a pravidlo (lze už teď):
    - `eco_sustainable_term`
      - EN: "Does the sentence (field sentence) describe the product or the company as 'sustainable', 'responsible', 'conscious' or 'ethical', or with a similar word? Answer no if none of these words is used."
      - CS: „Označuje věta (pole sentence) produkt nebo firmu jako „udržitelný“, „odpovědný“, „uvědomělý“ nebo „etický“, případně podobným slovem? Pokud žádné z těchto slov nepoužívá, odpověz ne.“
    - `eco_generic_claim` doplnit o `none: [{q: eco_sustainable_term, gte: 0.5}]` a přidat pravidlo `eco_sustainable_claim`: all `eco_claim`, `eco_generic`, `eco_sustainable_term`; none `eco_organic_food`; bez výjimky podle seznamu; high, text.
  - Energetická třída: tvrzení „energeticky úsporný“ lze podle Q&A opřít o nejvyšší třídu podle nařízení (EU) 2017/1369 [QA ř. 486–494]; seznam `excellent_performance_labels` ji zatím nezná (návrh: položka nebo vzor „energetická třída A“, posoudí právník).
  - Místo úplného vyřazení nálezu při ekoznačce na stránce zvážit pásmo nejvýš „k ověření“ (vyžaduje změnu enginu).
- **Poznámky:** Upřesnění má být na stejném nosiči „next to, or as part of, the claim“ [QA ř. 381–383]. Samotné barvy a obrázky obecným tvrzením nejsou, spolu s textem ano [QA ř. 251–254]. Název značky nebo produktu („Eco…“, „Green…“) může být environmentálním tvrzením, pokud u průměrného spotřebitele vyvolá environmentální asociaci [QA ř. 304–312]; počítat s opakovanými nálezy u titulků. Biopotraviny smí „bio“ a „eko“ používat [QA ř. 789–797]. Priorita A (existující).

### eco_part_as_whole

**Bod 4b (EmpCo): tvrzení o celém produktu nebo podniku, které se týká jen části**

- **Čísla bodů:** EU příloha I bod 4b · CZ — (netransponováno) · SK príloha č. 1 bod 7 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Uvedení environmentálního tvrzení o celém produktu nebo o celém podniku obchodníka, pokud se ve skutečnosti týká pouze určitého aspektu produktu nebo specifické činnosti podniku obchodníka.“ [EU ř. 926]
  - SK: „Environmentálne tvrdenie o celom produkte alebo o celom podniku obchodníka, ak sa týka len určitého aspektu produktu alebo konkrétnej činnosti obchodníka.“ [SK ř. 7682–7683]
- **Požadavek lidsky:** Když je z recyklátu jen obal, nesmí text tvrdit, že je „ekologický“ celý produkt; když firma jede na obnovitelné zdroje jen v jednom provozu, nesmí tvrdit, že celá.
- **Kontrola:** jev. Stávající pravidlo zachytí případy, kdy text sám prozradí, že výhoda se týká jen části. Když to text neprozradí (například „vyrobeno z recyklovaného materiálu“ a recyklovaný je jen obal), rozhodují fakta mimo web.
- **Otázky pro Jev:** existující `eco_claim`, `eco_whole_claim`, `eco_part_benefit`, `eco_organic_food`.
- **Skládání:** existující pravidlo `eco_part_as_whole` (all `eco_claim`, `eco_whole_claim`, `eco_part_benefit`; none `eco_organic_food`), medium, text. Beze změny.
- **Návrh doplnění:** Volitelné pravidlo „soupis“ `eco_claim_to_substantiate`: all `eco_claim`; none `eco_generic`, `eco_neutral`; low, verify. Hlásí každé konkrétní environmentální tvrzení k doložení (odůvodnění 11 EmpCo dává jako příklad „vyrobený s použitím recyklovaného materiálu“ [EmpCo ř. 41]). Užitečné pro e-shop před kontrolou, ale hodně nálezů; zapnout až po kalibraci.
- **Poznámky:** Zpráva `eshop-guard/out/localhost-8000-20260925-1800/report.md` ještě hlásí biomošt u `eco_part_as_whole` a mezi pravděpodobnostmi nemá `eco_organic_food`, takže běh zřejmě předcházel doplnění podmínky `none`; při dalším běhu ověřit. Priorita B (existující).

### eco_neutrality

**Bod 4c (EmpCo): neutrální, snížený nebo pozitivní dopad na klima díky kompenzacím**

- **Čísla bodů:** EU příloha I bod 4c · CZ — (netransponováno) · SK príloha č. 1 bod 8 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Tvrzení založené na kompenzaci emisí skleníkových plynů, že produkt má neutrální, snížený nebo pozitivní dopad na životní prostředí, pokud jde o emise skleníkových plynů.“ [EU ř. 930]
  - SK: „Tvrdenie založené na kompenzácii emisií skleníkových plynov, že produkt má neutrálny, znížený alebo pozitívny vplyv na životné prostredie z hľadiska emisií skleníkových plynov.“ [SK ř. 7685–7687]
- **Požadavek lidsky:** O produktu (zboží i službě, například dopravě) se nesmí tvrdit, že je uhlíkově neutrální, má snížený nebo pozitivní dopad na klima, pokud to stojí na kompenzacích mimo jeho hodnotový řetězec. Povolené je jen tvrzení opřené o skutečné snížení v životním cyklu produktu.
- **Kontrola:** jev + ověřit. Když věta nebo kontext sám uvede kompenzace, rozhodne text; jinak je základ tvrzení mimo web.
- **Otázky pro Jev:** existující `eco_neutral`.
- **Skládání:** existující pravidlo `eco_neutrality` (all `eco_neutral`), high, verify.
- **Návrh doplnění:**
  - `eco_neutral` rozšířit o příklady z odůvodnění 12 EmpCo: „uhlíkově pozitivní“, „bez dopadu na klima“, „se sníženým dopadem na klima“, „s omezenou uhlíkovou stopou“ [EmpCo ř. 43; QA ř. 650–652]. Nyní otázka zná jen neutralitu a kompenzované emise.
  - Opravit vysvětlení: zákaz 4c se týká produktu; podle Q&A „does not apply to claims based on offsetting at company level“ [QA ř. 673–674]. Firma jako celek spadá pod obecný zákaz a případně bod 4a.
  - Nové otázky do eco:
    - `eco_offset_basis`
      - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that a climate claim relies on offsetting, compensation, carbon credits or supporting projects such as tree planting? Answer no if the sentence makes no climate claim or no such basis is stated."
      - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že tvrzení o klimatu stojí na kompenzaci emisí, offsetech, uhlíkových kreditech nebo podpoře projektů, jako je sázení stromů? Pokud věta žádné tvrzení o klimatu neobsahuje nebo takový základ neuvádí, odpověz ne.“
    - `eco_company_level`
      - EN: "Is the environmental claim in the sentence (field sentence) made about the company or the brand as a whole rather than about a specific product, service or delivery? Answer no if the sentence makes no environmental claim."
      - CS: „Týká se environmentální tvrzení ve větě (pole sentence) firmy nebo značky jako celku, a ne konkrétního produktu, služby nebo dopravy? Pokud věta žádné environmentální tvrzení neobsahuje, odpověz ne.“
  - Nová pravidla:
    ```yaml
    - id: eco_neutrality_offset
      title: "Klimatické tvrzení o produktu založené na kompenzacích"
      logic:
        all: [{q: eco_neutral, gte: 0.5}, {q: eco_offset_basis, gte: 0.5}]
        none: [{q: eco_company_level, gte: 0.5}]
      severity: high
      checkability: text
      legal_refs: # stejné jako eco_neutrality (EU bod 4c, SK bod 8)
      explanation: "Tvrzení, že produkt má díky kompenzacím emisí neutrální, snížený nebo pozitivní dopad na klima, je zakázané vždy."
      recommendation: "Tvrzení odstraňte; o podpoře klimatických projektů lze informovat odděleně a bez tvrzení o dopadu produktu."
    # stávající eco_neutrality: doplnit none eco_offset_basis a eco_company_level, ponechat verify
    - id: eco_neutrality_company
      title: "Tvrzení o klimatické neutralitě firmy"
      logic:
        all: [{q: eco_neutral, gte: 0.5}, {q: eco_company_level, gte: 0.5}]
      severity: medium
      checkability: verify
      legal_refs:
        - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, čl. 6 a příloha I bod 4a, ve znění směrnice (EU) 2024/825", status: "ověřit"}
        - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., § 10 a príloha č. 1 bod 6", status: "ověřit"}
        - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., § 5", status: "ověřit"}
      explanation: "Tvrzení o klimatické neutralitě firmy nespadá pod zákaz kompenzací u produktu, ale posuzuje se jako obecné environmentální tvrzení a podle obecného zákazu klamání."
      recommendation: "Tvrzení upřesněte přímo u něj (na čem stojí, jaké emise zahrnuje) a mějte ho doložené."
    ```
- **Poznámky:** Obecné tvrzení o neutralitě bez kompenzací spadá pod 4a [QA ř. 438–442]. Tvrzení o budoucí neutralitě („do roku 2030“) je samostatná praktika případ od případu, čl. 6 odst. 2 písm. d) po EmpCo, SK § 10 ods. 2 písm. d) [SK ř. 1697–1701]; návrh otázky `eco_future_claim` je v sekci „Mimo černou listinu“. Priorita A (existující).

### ucp_bait_advertising

**Bod 5: vábivá reklama**

- **Čísla bodů:** EU příloha I bod 5 · CZ příloha č. 1 písm. d) · SK príloha č. 1 bod 9 (do 26. 9. 2026 bod 5)
- **Citace:**
  - EU: „Výzva ke koupi produktů za určitou cenu, aniž by obchodník zveřejnil důvody, na základě kterých se může domnívat, že nebude sám nebo prostřednictvím jiného obchodníka schopen zajistit dodávku uvedených nebo rovnocenných produktů za cenu platnou pro dané období …“ [EU ř. 936]
  - CZ: „nabízí ke koupi výrobky nebo služby za určitou cenu, aniž by zveřejnil důvody, na jejichž základě se může domnívat, že nebude … schopen zajistit dodávku …“ [CZ ř. 860]
  - SK: „Výzva na kúpu produktu za určitú cenu bez toho, že by obchodník zverejnil akékoľvek rozumné dôvody, … (vábivá reklama).“ [SK ř. 7689–7693]
- **Požadavek lidsky:** Když e-shop láká na akční cenu, ale ví, že zboží za tu cenu nebude mít v rozumném množství, musí to předem napsat (například „počet kusů omezen“).
- **Kontrola:** kód. Rozhoduje dostupnost zboží a skutečné množství, ne věta. Signál pro kód: produkt propagovaný na úvodní stránce nebo v akci s cenou, který má v JSON-LD `Offer.availability` hodnotu `OutOfStock` nebo `SoldOut`, a na stránce chybí upozornění na omezené množství.
- **Otázky pro Jev:** žádné povinné. Pomocná otázka pro výjimku (zda nabídka upozorňuje na omezené množství) je možná, ale bez kódu nemá smysl.
- **Skládání:** nová kontrola `jsonld` (dostupnost) × propagace akce; nález „k ověření“.
- **Závažnost:** medium · verify
- **Poznámky:** Pokyny Komise: u propagovaných cen „od“ musí být nabízená cena k dispozici v přiměřeném množství s ohledem na rozsah reklamy [Pokyny ř. 3801]. Priorita C.

### ucp_bait_and_switch

**Bod 6: přivábit a zaměnit**

- **Čísla bodů:** EU příloha I bod 6 · CZ příloha č. 1 písm. e) · SK príloha č. 1 bod 10 (do 26. 9. 2026 bod 6)
- **Citace:** EU: „Výzva ke koupi produktu za určitou cenu a poté a) odmítnutí ukázat inzerovaný předmět spotřebitelům nebo b) odmítnutí přijetí objednávek produktu nebo dodání produktu v přiměřené lhůtě nebo c) předvedení vadného vzorku produktu s úmyslem propagovat jiný produkt …“ [EU ř. 940–958]
- **Požadavek lidsky:** E-shop nesmí inzerovat levné zboží a pak odmítat objednávky nebo ho nedodat, aby zákazníka přesměroval na jiné.
- **Kontrola:** nelze z textu. Jde o jednání po výzvě ke koupi a o úmysl; web ukáže nanejvýš produkt bez možnosti objednat a doporučení jiného.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_false_urgency

**Bod 7: nepravdivé „jen dnes“, „poslední kusy“, odpočty času**

- **Čísla bodů:** EU příloha I bod 7 · CZ příloha č. 1 písm. f) · SK príloha č. 1 bod 11 (do 26. 9. 2026 bod 7)
- **Citace:**
  - EU: „Nepravdivé tvrzení, že produkt bude dostupný pouze po omezenou dobu nebo že bude dostupný pouze po omezenou dobu za určitých podmínek, za účelem přimět spotřebitele k okamžitému rozhodnutí bez příležitosti nebo času potřebného k učinění informované volby.“ [EU ř. 962]
  - CZ: „nepravdivě uvádí, že výrobek nebo služba budou nabízeny pouze po omezenou dobu nebo že budou nabízeny pouze po omezenou dobu za určitých podmínek s cílem přimět spotřebitele k okamžitému rozhodnutí, …“ [CZ ř. 862]
  - SK: „Nepravdivé vyhlásenie, že produkt je k dispozícii len veľmi obmedzený čas alebo že je k dispozícii za špecifických podmienok len veľmi obmedzený čas, s cieľom vyvolať okamžité rozhodnutie …“ [SK ř. 7705–7708]
- **Požadavek lidsky:** „Jen dnes“, „akce končí o půlnoci“, odpočet času nebo „poslední 2 kusy“ smí e-shop napsat, jen když je to pravda. Pokud stejná cena platí i zítra, nebo „poslední kusy“ svítí týdny, jde o zakázanou praktiku.
- **Kontrola:** jev + ověřit (+ kód). Tvrzení o omezené době nebo zásobě je vidět, pravdivost ne. Nepravdivost prokáže kód: opakovaný sken (stejné „jen dnes“ za několik dní), rozpor s `Offer.priceValidUntil` v JSON-LD.
- **Otázky pro Jev:**
  - `ucp_urgency_time`
    - EN: "Does the sentence (field sentence) say that an offer, price, discount or product is available only for a short or limited time, for example 'today only', 'last day', 'ends at midnight', 'only until Sunday', 'only 2 hours left' or a countdown? Answer no if no time limit is stated."
    - CS: „Uvádí věta (pole sentence), že nabídka, cena, sleva nebo produkt platí či je k dispozici jen krátce nebo omezenou dobu, například „jen dnes“, „poslední den“, „končí o půlnoci“, „jen do neděle“, „zbývají 2 hodiny“ nebo odpočet času? Pokud žádné časové omezení neuvádí, odpověz ne.“
  - `ucp_urgency_stock`
    - EN: "Does the sentence (field sentence) stress that only a few items are left or that stock is about to run out, for example 'last 3 pieces', 'only 2 left', 'almost sold out', 'selling fast'? Answer no if it only states availability neutrally, such as 'In stock' or 'In stock: 12 pcs'."
    - CS: „Zdůrazňuje věta (pole sentence), že zbývá jen pár kusů nebo že zásoby brzy dojdou, například „poslední 3 kusy“, „zbývají jen 2“, „téměř vyprodáno“, „rychle mizí“? Pokud jen neutrálně uvádí dostupnost, například „Skladem“ nebo „Skladem 12 ks“, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_urgency_claim
    title: "Tvrzení o omezené době nebo zásobě k ověření"
    logic:
      any: [{q: ucp_urgency_time, gte: 0.5}, {q: ucp_urgency_stock, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 7", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. f)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 11", status: "ověřit"}
    explanation: "Nepravdivé tvrzení, že nabídka platí jen omezenou dobu nebo že zboží rychle dochází, je zakázané vždy."
    recommendation: "Ověřte, že nabídka opravdu končí, jak je napsáno, a že zásoba odpovídá; jinak tvrzení odstraňte."
  ```
- **Závažnost:** medium · verify (kód při opakovaném skenu může zvednout na high · text)
- **Poznámky:**
  - Pokyny Komise řadí k bodu 7 výslovně „využívání falešných časovačů nebo tvrzení o omezených zásobách na webových stránkách“ [Pokyny ř. 3570]; „poslední kusy“ tedy patří sem, nejen do obecného zákazu (dostupnost, CZ § 5 odst. 2 písm. b)).
  - Odpočty času se často vykreslují JavaScriptem, který crawler nespouští; v textu zůstane jen popisek.
  - Příklady pro označený vzorek: ano „Akce platí jen dnes do půlnoci!“, „Poslední 2 kusy skladem – pospěšte si.“; ne „Skladem 12 ks.“, „Novinka v naší nabídce.“
  - Priorita A.

### ucp_aftersales_language

**Bod 8: poprodejní servis v jiném jazyce**

- **Čísla bodů:** EU příloha I bod 8 · CZ příloha č. 1 písm. g) · SK príloha č. 1 bod 12 (do 26. 9. 2026 bod 8)
- **Citace:** EU: „Závazek poskytnout poprodejní servis spotřebitelům, s nimiž obchodník před uzavřením obchodní transakce komunikoval jazykem, který není úředním jazykem členského státu, v němž se obchodník nachází, a následné poskytování servisu pouze v jiném jazyce …“ [EU ř. 966]
- **Požadavek lidsky:** Kdo prodává česky nebo slovensky ze zahraničí a slíbí servis, nesmí ho pak poskytovat jen v jiném jazyce, aniž to předem jasně řekl.
- **Kontrola:** nelze z textu. Rozhoduje, v jakém jazyce se servis skutečně poskytuje. Upozornění na jazyk servisu v obchodních podmínkách praktiku naopak legalizuje.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_legality_claim

**Bod 9: dojem, že prodej je legální**

- **Čísla bodů:** EU příloha I bod 9 · CZ příloha č. 1 písm. h) · SK príloha č. 1 bod 13 (do 26. 9. 2026 bod 9)
- **Citace:**
  - EU: „Tvrzení nebo jiné vytváření dojmu, že prodej produktu je dovolený, i když tomu tak není.“ [EU ř. 970]
  - CZ: „tvrdí nebo vytváří dojem, že prodej výrobku nebo služby je dovolený, i když tomu tak není,“ [CZ ř. 864]
  - SK: „Vyhlásenie alebo iným spôsobom vytvorenie dojmu, že produkt možno legálne predávať, pričom tomu tak nie je.“ [SK ř. 7716–7717]
- **Požadavek lidsky:** E-shop nesmí tvrdit, že je produkt legální, když jeho prodej zakázaný je (typicky CBD a THC produkty, kratom, některé doplňky, pyrotechnika, zbraně, detektory radarů).
- **Kontrola:** jev + ověřit. Tvrzení o legalitě je vidět, zda platí, určuje právo pro daný produkt.
- **Otázky pro Jev:**
  - `ucp_legality`
    - EN: "Does the sentence (field sentence) state or suggest that selling, buying, owning or using the product is legal or permitted (for example '100% legal', 'legal in the Czech Republic', 'approved for sale in the EU')? Answer no if legality is not mentioned."
    - CS: „Tvrdí nebo naznačuje věta (pole sentence), že prodej, nákup, držení nebo užívání produktu je legální či povolené (například „100% legální“, „legální v ČR“, „schváleno k prodeji v EU“)? Pokud o legalitě nic neříká, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_legality_claim
    title: "Tvrzení o legalitě produktu k ověření"
    logic:
      all: [{q: ucp_legality, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 9", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. h)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 13", status: "ověřit"}
    explanation: "Vytvářet dojem, že je prodej produktu dovolený, je zakázané, pokud dovolený není."
    recommendation: "Ověřte právní režim produktu v zemi prodeje; tvrzení o legalitě uvádějte jen doložené."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Pokyny Komise: soud shledal porušení bodu 9 u cestovní agentury bez pojištění proti úpadku, protože vznikl mylný dojem, že nabídka je v souladu s právem [Pokyny ř. 2319]; tedy i dojem, ne jen výslovné tvrzení. Priorita B (úzké obory).

### ucp_legal_rights_as_advantage

**Bod 10: zákonná práva jako přednost nabídky**

- **Čísla bodů:** EU příloha I bod 10 · CZ příloha č. 1 písm. i) · SK príloha č. 1 bod 14 (do 26. 9. 2026 bod 10)
- **Citace:**
  - EU: „Uvádění práv, která spotřebitelům vyplývají ze zákona, jako přednosti obchodníkovy nabídky.“ [EU ř. 974]
  - CZ: „uvádí jako přednost nabídky práva, která vyplývají spotřebiteli přímo ze zákona,“ [CZ ř. 865]
  - SK: „Prezentovanie práv, ktoré spotrebiteľovi prislúchajú podľa právnych predpisov, ako charakteristickej črty ponuky obchodníka.“ [SK ř. 7719–7720]
- **Požadavek lidsky:** E-shop nesmí vydávat za svou výhodu to, co zákazník má ze zákona všude: 14 dní na odstoupení od nákupu na dálku (CZ § 1829 odst. 1 OZ [OZ-CZ ř. 6074]; SK § 19 ods. 1 zákona 108/2024 [SK ř. 2399–2402]), vrácení peněz do 14 dnů, dva roky na vytknutí vady (CZ § 2165 odst. 1 OZ [OZ-CZ ř. 7166]; SK § 619 ods. 1 OZ, v souboru OZ-SK na jednom řádku), bezplatnou opravu nebo výměnu. Výhodou smí být jen to, co jde nad zákon (30 dní na vrácení, záruka 3 roky, zpětné poštovné zdarma).
- **Kontrola:** jev. Věta s nadpisem nad ní („Proč nakoupit u nás“) ukáže prezentaci jako výhodu; zákonné minimum je pevné a je přímo v otázce.
- **Otázky pro Jev:**
  - `ucp_legal_right`
    - EN: "Does the sentence (field sentence) mention a right that the law gives every consumer, such as withdrawing from an online purchase or returning goods within 14 days without giving a reason, a refund within 14 days, a 2-year warranty for defects (legal guarantee), or free repair or replacement of defective goods? Answer no if no such right is mentioned."
    - CS: „Zmiňuje věta (pole sentence) právo, které zákon dává každému spotřebiteli, například odstoupit od nákupu přes internet nebo vrátit zboží do 14 dnů bez udání důvodu, vrácení peněz do 14 dnů, dvouletou záruku za vady (zákonnou záruku) nebo bezplatnou opravu či výměnu vadného zboží? Pokud žádné takové právo nezmiňuje, odpověz ne.“
  - `ucp_shop_advantage`
    - EN: "Does the sentence (field sentence), read together with the text right next to it (context_before, context_after), present something as an advantage, bonus or special feature of buying from this shop (for example 'with us you get', 'our advantages', 'why shop with us', 'as a bonus', 'only with us')? Answer no if it only informs about rules, conditions or a procedure."
    - CS: „Prezentuje věta (pole sentence) spolu s textem hned vedle ní (context_before, context_after) něco jako výhodu, bonus nebo zvláštnost nákupu právě v tomto obchodě (například „u nás získáte“, „naše výhody“, „proč nakoupit u nás“, „jako bonus“, „jen u nás“)? Pokud jen informuje o pravidlech, podmínkách nebo postupu, odpověz ne.“
  - `ucp_beyond_legal_minimum`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) state a period or condition that goes beyond the legal minimum, for example more than 14 days to return goods, a warranty longer than 2 years, or free return shipping? Answer no if nothing beyond the legal minimum is stated."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after) lhůtu nebo podmínku nad zákonné minimum, například víc než 14 dní na vrácení zboží, záruku delší než 2 roky nebo zpětné poštovné zdarma? Pokud nic nad zákonné minimum neuvádí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_legal_rights_as_advantage
    title: "Zákonné právo prezentované jako výhoda obchodu"
    logic:
      all: [{q: ucp_legal_right, gte: 0.5}, {q: ucp_shop_advantage, gte: 0.5}]
      none: [{q: ucp_beyond_legal_minimum, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 10", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. i)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 14 (do 26. 9. 2026 bod 10)", status: "ověřit"}
    explanation: "Práva, která má spotřebitel ze zákona u každého prodejce (14 dní na odstoupení, dvouletá odpovědnost za vady), nesmí obchodník prezentovat jako přednost své nabídky."
    recommendation: "Přesuňte tato práva z výhod obchodu do informací o nákupu, nebo jako výhodu uveďte jen to, co jde nad zákon."
  ```
- **Závažnost:** high · text
- **Poznámky:**
  - `ucp_shop_advantage` je obecná a lze ji použít i jinde. Kontext je v ní záměrně: odrážka „14 dní na vrácení zboží“ je výhodou jen díky nadpisu nad ní.
  - Povinná informace o odstoupení v obchodních podmínkách a harmonizované oznámení o zákonné záruce (obrázek, viz sekce o informačních povinnostech) nejsou prezentací výhody, `ucp_shop_advantage` má dát ne.
  - Pokyny Komise k bodu 10 uvádějí jako příklad tvrzení, že produkt neobsahuje látky, které jsou už zakázané zákonem [Pokyny ř. 3028–3032]; od 27. 9. 2026 to výslovně řeší bod 10a.
  - Příklady pro vzorek: ano věta „14 dní na vrácení zboží bez udání důvodu.“ s `context_before` „Proč nakoupit u nás?“, „Naše výhoda: na každé zboží dostanete dvouletou záruku.“; ne „Spotřebitel má právo odstoupit od smlouvy do 14 dnů od převzetí zboží.“, „U nás máte na vrácení zboží 30 dní.“
  - Priorita A.

### ucp_legal_requirement_as_feature

**Bod 10a (EmpCo): zákonný požadavek na celou kategorii jako přednost nabídky**

- **Čísla bodů:** EU příloha I bod 10a · CZ — (netransponováno) · SK príloha č. 1 bod 15 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Prezentace požadavků uložených právními předpisy na všechny produkty v příslušné kategorii produktů na trhu Unie jako charakteristického rysu nabídky obchodníka.“ [EU ř. 980]
  - SK: „Prezentovanie požiadaviek, ktoré sa podľa právnych predpisov vzťahujú na všetky produkty v príslušnej kategórii produktov na trhu Európskej únie, ako charakteristickej črty ponuky obchodníka.“ [SK ř. 7722–7724]
- **Požadavek lidsky:** Když zákon něco vyžaduje u všech produktů dané kategorie na trhu EU (látka je zakázaná, označení je povinné), nesmí to e-shop vydávat za přednost svého zboží („kojenecká láhev bez BPA“, „s certifikací CE“).
- **Kontrola:** jev + ověřit (+ kód). Tvrzení „bez látky X“ nebo „splňuje normy“ je vidět; zda jde o požadavek na celou kategorii, rozhoduje seznam, který musí sestavit právník.
- **Otázky pro Jev:**
  - `ucp_free_from`
    - EN: "Does the sentence (field sentence) state that the product does not contain a certain substance or ingredient (for example 'BPA-free', 'CFC-free', 'lead-free', 'mercury-free', 'no azo dyes')? Answer no if no absence of a substance is stated."
    - CS: „Uvádí věta (pole sentence), že produkt neobsahuje určitou látku nebo složku (například „bez BPA“, „bez freonů“, „bez olova“, „bez rtuti“, „bez azobarviv“)? Pokud nepíše o nepřítomnosti žádné látky, odpověz ne.“
  - `ucp_legal_compliance`
    - EN: "Does the sentence (field sentence) state that the product meets a legal requirement, a mandatory safety standard or a mandatory marking (for example 'CE certified', 'complies with EU standards', 'meets EU safety requirements')? Answer no if no such compliance is stated."
    - CS: „Uvádí věta (pole sentence), že produkt splňuje zákonný požadavek, povinnou bezpečnostní normu nebo povinné označení (například „s certifikací CE“, „splňuje normy EU“, „odpovídá bezpečnostním předpisům EU“)? Pokud žádný takový soulad neuvádí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_legal_requirement_as_feature
    title: "Zákonný požadavek prezentovaný jako přednost produktu"
    logic:
      any: [{q: ucp_free_from, gte: 0.5}, {q: ucp_legal_compliance, gte: 0.5}]
    # po zavedení kontroly list_present: code_checks: [{type: list_present, list: legal_requirement_claims}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 10a, ve znění směrnice (EU) 2024/825", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 15, účinnost od 27. 9. 2026", status: "ověřit"}
      - {jurisdiction: cz, ref: "Netransponováno (sněmovní tisk 53); do té doby jen § 5 zákona č. 634/1992 Sb.", status: "ověřit"}
    explanation: "Požadavky, které zákon ukládá všem produktům dané kategorie v EU, nesmí obchodník prezentovat jako přednost své nabídky."
    recommendation: "Ověřte, zda je vlastnost povinná pro všechny produkty kategorie; pokud ano, nepředstavujte ji jako výhodu."
  ```
- **Závažnost:** medium · verify
- **Poznámky:**
  - Hranice podle Q&A: zákaz se nepoužije, když požadavek platí jen pro část konkurenčních produktů na trhu EU, například mimo EU vyrobené zboží ho splňovat nemusí [QA ř. 816–825]; odůvodnění 15 uvádí jako povolený příklad udržitelně lovené ryby [EmpCo ř. 49].
  - Bez seznamu látek a kategorií bude `ucp_free_from` hlásit i legitimní „bez lepku“, „bez parabenů“, „bez laktózy“. Doporučení: do zavedení `list_present` hlásit jen `ucp_legal_compliance`, nebo pravidlo držet jako low. Seznam `legal_requirement_claims` (například BPA v kojeneckých lahvích, freony, rtuť v bateriích, označení CE) sestaví právník; konkrétní předpisy jsem v podkladech NEOVĚŘIL.
  - „Bezvýznamná výhoda“ (voda „bez lepku“) je jiná praktika, případ od případu (Q&A ot. 11), viz „Mimo černou listinu“.
  - Priorita B.

### ucp_advertorial

**Bod 11: placená reklama maskovaná jako redakční obsah**

- **Čísla bodů:** EU příloha I bod 11 · CZ příloha č. 1 písm. j) · SK príloha č. 1 bod 16 (do 26. 9. 2026 bod 11)
- **Citace:** EU: „Využití prostoru ve sdělovacích prostředcích k propagaci produktu, za kterou obchodník zaplatil, aniž by to byl spotřebitel schopen z obsahu, obrázků nebo zvuků jednoznačně poznat. (Placená reklama ve formě novinových článků, advertorial).“ [EU ř. 986]
- **Požadavek lidsky:** Placený článek v médiu musí být poznat jako reklama.
- **Kontrola:** nelze z textu. Týká se médií a obsahu, za který obchodník zaplatil; na vlastním e-shopu je obchodní záměr zřejmý a platba není z textu vidět.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Pokyny Komise vykládají „redakční obsah“ široce, včetně influencerů [Pokyny ř. 3490]; to je mimo web e-shopu. Priorita –.

### ucp_search_ads_undisclosed

**Bod 11a: neoznačená placená reklama ve výsledcích vyhledávání**

- **Čísla bodů:** EU příloha I bod 11a · CZ příloha č. 1 písm. k) · SK príloha č. 1 bod 17 (do 26. 9. 2026 bod 12)
- **Citace:**
  - EU: „Poskytnutí výsledků vyhledávání v reakci na dotaz spotřebitele při online vyhledávání bez jasného uvedení placené reklamy nebo platby za účelem dosažení lepšího pořadí produktu v rámci výsledků vyhledávání.“ [EU ř. 994]
  - CZ: „poskytuje výsledky vyhledávání v reakci na dotaz spotřebitele při on-line vyhledávání bez jasného uvedení placené reklamy nebo platby za účelem dosažení lepšího pořadí …“ [CZ ř. 867]
- **Požadavek lidsky:** Když e-shop nebo tržiště řadí ve vyhledávání výš ty, kdo zaplatili, musí to u výsledku jasně označit („Reklama“).
- **Kontrola:** nelze z textu. Crawler stránky vyhledávání záměrně vyřazuje (zadání, krok 2), označení bývá grafické a hlavně nejde poznat, který výsledek je placený.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:**
  - Bod se týká každého, kdo umožňuje vyhledávat produkty [Pokyny ř. 3276]; označení musí být u výsledku a výrazné, doporučené slovo „reklama“ místo „sponzorováno“ [Pokyny ř. 3298, 3302].
  - Kontrolovatelná je příbuzná informační povinnost tržišť: hlavní parametry řazení nabídek (CZ § 5a odst. 4 a § 11b písm. a) [CZ ř. 184, 243]; SK § 11 ods. 6 písm. b) [SK ř. 1758–1765]). Kandidát na `site_presence` v legal, jen pro tržiště (vyžaduje `site_presence_if`).
  - Priorita – (tržiště C).

### ucp_safety_fear

**Bod 12: nepravdivé strašení rizikem, když si zákazník produkt nekoupí**

- **Čísla bodů:** EU příloha I bod 12 · CZ příloha č. 1 písm. l) · SK príloha č. 1 bod 18 (do 26. 9. 2026 bod 13)
- **Citace:**
  - EU: „Věcně nesprávné tvrzení o povaze a míře rizika pro osobní bezpečnost spotřebitele nebo jeho rodiny, pokud si produkt nezakoupí.“ [EU ř. 1000]
  - CZ: „uvádí nesprávné údaje o povaze a míře rizika pro osobní bezpečnost spotřebitele nebo jeho rodiny, pokud si jeho výrobek nebo službu nekoupí,“ [CZ ř. 868]
  - SK: „Vecne nesprávne tvrdenie o povahe a rozsahu rizika pre osobnú bezpečnosť spotrebiteľa alebo jeho rodiny, ak si spotrebiteľ produkt nekúpi.“ [SK ř. 7734–7735]
- **Požadavek lidsky:** E-shop nesmí zveličovat nebo vymýšlet nebezpečí, které zákazníkovi hrozí, když produkt nekoupí (například „záření 5G ničí zdraví vaší rodiny“ u „ochranných“ přívěsků).
- **Kontrola:** jev + ověřit. Strašení je vidět, věcná nesprávnost ne.
- **Otázky pro Jev:**
  - `ucp_safety_fear`
    - EN: "Does the sentence (field sentence) claim that the reader or their family faces a danger to health, life or personal safety if they do not buy the product? Answer no if no such danger linked to not buying is stated."
    - CS: „Tvrdí věta (pole sentence), že čtenáři nebo jeho rodině hrozí nebezpečí pro zdraví, život nebo osobní bezpečnost, pokud si produkt nekoupí? Pokud takové nebezpečí spojené s nekoupením neuvádí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_safety_fear_claim
    title: "Strašení rizikem pro bezpečnost k ověření"
    logic:
      all: [{q: ucp_safety_fear, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 12", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. l)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 18", status: "ověřit"}
    explanation: "Věcně nesprávné tvrzení o riziku pro bezpečnost zákazníka nebo jeho rodiny, pokud produkt nekoupí, je zakázané vždy."
    recommendation: "Ověřte, že popsané riziko odpovídá skutečnosti a je doložené; jinak tvrzení odstraňte."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Priorita C (zabezpečovací technika, „ochrana před zářením“).

### ucp_manufacturer_confusion

**Bod 13: napodobenina vydávaná za výrobek konkrétního výrobce**

- **Čísla bodů:** EU příloha I bod 13 · CZ příloha č. 1 písm. m) · SK príloha č. 1 bod 19 (do 26. 9. 2026 bod 14)
- **Citace:**
  - EU: „Propagace výrobku podobného výrobku konkrétního výrobce způsobem, jenž cíleně vede k uvedení spotřebitele v omyl tak, že uvěří, že daný výrobek je vyroben týmž výrobcem, i když tomu tak není.“ [EU ř. 1004]
  - CZ: „propaguje výrobek způsobem, který u spotřebitele může vyvolat dojem, že byl vyroben určitým výrobcem, ačkoliv tomu tak není,“ [CZ ř. 869]
  - SK: „Propagovanie podobného produktu, ako je produkt vyrobený konkrétnym výrobcom, a to spôsobom, ktorý úmyselne zavádza spotrebiteľa, …“ [SK ř. 7737–7739]
- **Požadavek lidsky:** Kompatibilní náplň nebo náhradní hlavice se nesmí prezentovat tak, aby zákazník uvěřil, že jde o originál značky.
- **Kontrola:** jev + ověřit (slabý signál). Věta „kompatibilní s HP“ je sama v pořádku; problém je, když titulek nebo název produktu nese značku bez slova „kompatibilní“. To je kontrola na úrovni stránky; skutečného výrobce z textu nepoznáme.
- **Otázky pro Jev:**
  - `ucp_compatible_brand`
    - EN: "Does the sentence (field sentence) describe the product as compatible with, a replacement for, or an alternative to another manufacturer's branded product (for example 'compatible with HP', 'replacement for Philips Sonicare heads', 'Nespresso-type capsules')? Answer no if no other manufacturer's brand is named as a reference."
    - CS: „Popisuje věta (pole sentence) produkt jako kompatibilní se značkovým produktem jiného výrobce, náhradu za něj nebo alternativu k němu (například „kompatibilní s HP“, „náhrada za hlavice Philips Sonicare“, „kapsle typu Nespresso“)? Pokud žádnou značku jiného výrobce jako vztažný bod neuvádí, odpověz ne.“
- **Skládání:** `ucp_compatible_brand` sama nález nedělá; je vstupem pro kontrolu `regex_page`: titulek stránky obsahuje stejnou značku bez slov „kompatibilní“, „náhradní“, „alternativní“ → nález `ucp_brand_confusion`, low, verify.
- **Závažnost:** low · verify
- **Poznámky:** CZ znění je širší než EU: stačí, že praktika „může vyvolat dojem“, bez „cíleně“ [CZ ř. 869 × EU ř. 1004]. Priorita C (tonerové a náhradní díly B).

### ucp_pyramid_scheme

**Bod 14: pyramidový program**

- **Čísla bodů:** EU příloha I bod 14 · CZ příloha č. 1 písm. n) · SK príloha č. 1 bod 20 (do 26. 9. 2026 bod 15)
- **Citace:**
  - EU: „Zahájení, provozování nebo propagace pyramidového programu, kdy spotřebitel zaplatí za možnost získat odměnu, která závisí především na získávání nových spotřebitelů do programu, a nikoli na prodeji nebo spotřebě produktů.“ [EU ř. 1008]
  - CZ: „vytvoří, provozuje nebo propaguje pyramidový program, kdy spotřebitel zaplatí za možnost získat odměnu, která závisí na získání dalších spotřebitelů do programu, …“ [CZ ř. 870]
  - SK: „Vytvorenie, prevádzkovanie alebo podporovanie pyramídovej schémy, v ktorej spotrebiteľ poskytne plnenie za možnosť získať kompenzáciu, ktorá vyplýva hlavne zo zapojenia ďalších spotrebiteľov …“ [SK ř. 7741–7743]
- **Požadavek lidsky:** E-shop nesmí nabízet program, kde člověk zaplatí vstup a vydělává hlavně na přivedení dalších platících členů.
- **Kontrola:** jev + ověřit. Oba znaky (placený vstup, odměna za nábor) bývají na stránkách „Staňte se partnerem“; zda odměna plyne „především“ z náboru, rozhodnou podmínky programu.
- **Otázky pro Jev:**
  - `ucp_pay_to_join`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that the reader must pay a fee or buy a starter package to join a programme, club or network? Answer no if no payment to join is mentioned."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že čtenář musí za vstup do programu, klubu nebo sítě zaplatit poplatek nebo koupit startovací balíček? Pokud žádnou platbu za vstup nezmiňuje, odpověz ne.“
  - `ucp_recruit_reward`
    - EN: "Does the sentence (field sentence) say that members earn money or rewards for bringing in new members who join the programme, rather than for selling products? Answer no for ordinary referral discounts where the reward comes from a friend's purchase."
    - CS: „Uvádí věta (pole sentence), že členové získávají peníze nebo odměny za přivedení nových členů do programu, a ne za prodej produktů? U běžných doporučovacích slev, kde odměna plyne z nákupu kamaráda, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_pyramid_scheme
    title: "Znaky pyramidového programu k ověření"
    logic:
      all: [{q: ucp_pay_to_join, gte: 0.5}, {q: ucp_recruit_reward, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 14", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. n)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 20", status: "ověřit"}
    explanation: "Program, kde spotřebitel platí za možnost odměny závislé hlavně na náboru dalších členů, je zakázaný vždy."
    recommendation: "Ověřte, z čeho plynou odměny v programu; odměna má záviset na prodeji produktů, ne na náboru."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Soudní dvůr: stačí i nepřímá souvislost mezi platbami nových členů a odměnami [Pokyny ř. 5337]. Priorita C.

### ucp_closing_down

**Bod 15: nepravdivé „končíme“ nebo „stěhujeme se“**

- **Čísla bodů:** EU příloha I bod 15 · CZ příloha č. 1 písm. o) · SK príloha č. 1 bod 21 (do 26. 9. 2026 bod 16)
- **Citace:**
  - EU: „Tvrzení, že obchodník zamýšlí ukončit obchodování nebo se stěhuje, ačkoli tomu tak není.“ [EU ř. 1012]
  - CZ: „učiní nepravdivé prohlášení, že zamýšlí ukončit svoji činnost nebo že přemísťuje provozovnu,“ [CZ ř. 871]
  - SK: „Tvrdenie obchodníka, že sa chystá ukončiť svoju činnosť alebo premiestniť svoju prevádzkareň, pričom tomu tak nie je.“ [SK ř. 7745–7746]
- **Požadavek lidsky:** „Totální výprodej, končíme“ smí e-shop napsat, jen když opravdu končí.
- **Kontrola:** jev + ověřit (+ kód). Tvrzení je vidět; nepravdivost ukáže opakovaný sken (tvrzení trvá měsíce) nebo rejstřík.
- **Otázky pro Jev:**
  - `ucp_closing_down`
    - EN: "Does the sentence (field sentence) say that the shop or the company is closing down, ending its business or moving to other premises, for example 'closing-down sale', 'we are closing', 'we are ending sales, everything must go', 'we are moving'? Answer no for an ordinary sale that does not mention closing or moving."
    - CS: „Uvádí věta (pole sentence), že obchod nebo firma končí, ukončuje činnost nebo se stěhuje, například „totální výprodej – končíme“, „zavíráme“, „končíme s prodejem, vše musí pryč“, „stěhujeme se“? U běžného výprodeje, který nezmiňuje konec ani stěhování, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_closing_down_claim
    title: "Tvrzení o ukončení činnosti nebo stěhování k ověření"
    logic:
      all: [{q: ucp_closing_down, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 15", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. o)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 21", status: "ověřit"}
    explanation: "Tvrdit, že obchodník končí nebo se stěhuje, je zakázané, pokud to není pravda."
    recommendation: "Tvrzení ponechte jen po dobu skutečného ukončení nebo stěhování."
  ```
- **Závažnost:** medium · verify (s `persist_across_scans` high)
- **Poznámky:** Priorita B.

### ucp_gambling_win

**Bod 16: produkt usnadní výhru ve hře založené na náhodě**

- **Čísla bodů:** EU příloha I bod 16 · CZ příloha č. 1 písm. p) · SK príloha č. 1 bod 22 (do 26. 9. 2026 bod 17)
- **Citace:**
  - EU: „Tvrzení, že produkty usnadní výhru ve hrách založených na náhodě.“ [EU ř. 1016]
  - CZ: „prohlašuje, že jím nabízené nebo prodávané výrobky nebo služby usnadní výhru ve hrách založených na náhodě,“ [CZ ř. 872]
  - SK: „Tvrdenie, že produkt je schopný uľahčiť výhru v hazardných hrách.“ [SK ř. 7748]
- **Požadavek lidsky:** Nesmí se tvrdit, že produkt (sázkový systém, „šťastný“ amulet, aplikace) zvýší šanci na výhru v loterii nebo kasinu. Pravdivost se nezkoumá, zakázané je tvrzení samo.
- **Kontrola:** jev.
- **Otázky pro Jev:**
  - `ucp_gambling_win`
    - EN: "Does the sentence (field sentence) claim that the product increases the chance of winning a lottery, a bet, a casino game or another game of chance? Answer no if games of chance are not mentioned."
    - CS: „Tvrdí věta (pole sentence), že produkt zvyšuje šanci na výhru v loterii, sázkách, kasinu nebo jiné hře založené na náhodě? Pokud hry založené na náhodě nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_gambling_win_claim
    title: "Tvrzení, že produkt usnadní výhru v hazardní hře"
    logic:
      all: [{q: ucp_gambling_win, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 16", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. p)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 22", status: "ověřit"}
    explanation: "Tvrzení, že produkt usnadní výhru ve hrách založených na náhodě, je zakázané vždy."
    recommendation: "Tvrzení odstraňte."
  ```
- **Závažnost:** high · text
- **Poznámky:** Priorita C.

### ucp_cure_claim

**Bod 17: nepravdivé tvrzení, že produkt léčí**

- **Čísla bodů:** EU příloha I bod 17 · CZ příloha č. 1 písm. q) · SK príloha č. 1 bod 23 (do 26. 9. 2026 bod 18)
- **Citace:**
  - EU: „Nepravdivé tvrzení, že produkt může vyléčit nemoci, poruchu nebo tělesné postižení.“ [EU ř. 1020]
  - CZ: „nepravdivě prohlašuje, že výrobek nebo služba může vyléčit nemoc, zdravotní poruchu nebo postižení,“ [CZ ř. 873]
  - SK: „Nepravdivé tvrdenie, že produkt je schopný liečiť chorobu, dysfunkciu alebo postihnutie.“ [SK ř. 7750]
- **Požadavek lidsky:** Doplněk stravy, kosmetika, bylinky nebo přístroj nesmí tvrdit, že vyléčí nemoc, když to není pravda.
- **Kontrola:** jev + ověřit. Tvrzení o léčbě je vidět; nepravdivost je v černé listině podmínkou. U potravin a doplňků je ale tvrzení o léčení nemoci zakázané i odvětvovým právem (nařízení (EU) č. 1169/2011, čl. 7 odst. 3: NEOVĚŘENO v podkladech), takže u nich jde prakticky o jistý problém.
- **Otázky pro Jev:**
  - `ucp_cure`
    - EN: "Does the sentence (field sentence) claim or suggest that the product cures, heals or treats a disease, illness, health disorder or disability (for example 'cures acne', 'heals arthritis', 'treats diabetes', 'gets rid of migraines for good')? Answer no for cosmetic or general well-being effects such as 'moisturises the skin', 'supports immunity' or 'helps you relax'."
    - CS: „Tvrdí nebo naznačuje věta (pole sentence), že produkt vyléčí, zhojí nebo léčí nemoc, zdravotní poruchu či postižení (například „léčí akné“, „vyléčí artrózu“, „léčí cukrovku“, „zbaví vás migrén navždy“)? U kosmetických účinků nebo obecných účinků na pohodu, jako „hydratuje pokožku“, „podporuje imunitu“ nebo „pomáhá relaxovat“, odpověz ne.“
  - `ucp_is_medicine`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) state that the product is a registered medicinal product (medicine) or a medical device? Answer no if this is not stated."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že produkt je registrovaný léčivý přípravek (lék) nebo zdravotnický prostředek? Pokud to neuvádí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_cure_claim
    title: "Tvrzení, že produkt léčí nemoc"
    logic:
      all: [{q: ucp_cure, gte: 0.5}]
      none: [{q: ucp_is_medicine, gte: 0.5}]
    severity: high
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 17", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. q)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 23", status: "ověřit"}
    explanation: "Nepravdivé tvrzení, že produkt může vyléčit nemoc, poruchu nebo postižení, je zakázané vždy. U potravin a doplňků stravy je tvrzení o léčení nemoci zakázané i předpisy o potravinách."
    recommendation: "Tvrzení odstraňte, pokud nejde o registrovaný lék nebo zdravotnický prostředek s takto schváleným účelem."
  - id: ucp_cure_claim_medicine
    title: "Léčebné tvrzení u léku nebo zdravotnického prostředku k ověření"
    logic:
      all: [{q: ucp_cure, gte: 0.5}, {q: ucp_is_medicine, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs: # stejné jako ucp_cure_claim
    explanation: "U léku nebo zdravotnického prostředku smí léčebné tvrzení odpovídat jen schválenému účelu použití."
    recommendation: "Porovnejte tvrzení se souhrnem údajů o přípravku nebo s určeným účelem prostředku."
  ```
- **Závažnost:** high · verify (u léků low · verify)
- **Poznámky:**
  - Druh produktu bývá daleko od věty (parametry, kategorie). Výjimku lépe dělat kódem `regex_page` („léčivý přípravek“, „registrační číslo“, „kód SÚKL“, „zdravotnický prostředek“) než jen přes kontext.
  - Pokyny Komise: tvrzení u roušek a dezinfekcí, že zabrání infekci COVID-19 nebo ji vyléčí, mohou být zakázána podle bodu 17 [Pokyny ř. 2371].
  - Prevence nemoci („chrání před chřipkou“) do bodu 17 nepatří, je to kandidát na modul zdravotních tvrzení (zadání ho zmiňuje).
  - Příklady pro vzorek: ano „Bylinná mast vyléčí lupénku během 14 dnů.“, „Kapky, které léčí vysoký tlak.“; ne „Krém hydratuje a zklidňuje suchou pokožku.“, „Léčivý přípravek k léčbě bolesti hlavy.“ (lék, jde do low pravidla).
  - Priorita A.

### ucp_market_conditions

**Bod 18: nepravdivé informace o trhu nebo o možnosti produkt sehnat**

- **Čísla bodů:** EU příloha I bod 18 · CZ příloha č. 1 písm. r) · SK príloha č. 1 bod 24 (do 26. 9. 2026 bod 19)
- **Citace:**
  - EU: „Poskytování věcně nesprávných informací o tržních podmínkách nebo o možnosti opatřit si produkt s úmyslem přimět zákazníka k jeho pořízení za méně výhodných podmínek, než jsou běžné tržní podmínky.“ [EU ř. 1024]
  - CZ: „poskytuje nesprávné informace o tržních podmínkách nebo o možnosti opatřit si výrobek nebo službu, aby tak přiměl spotřebitele koupit si tento výrobek nebo službu za méně výhodných podmínek, …“ [CZ ř. 874]
  - SK: „Poskytnutie vecne nesprávnej informácie o podmienkach na trhu alebo o možnosti nájsť produkt s úmyslom donútiť spotrebiteľa, aby získal produkt za menej výhodných podmienok, …“ [SK ř. 7752–7754]
- **Požadavek lidsky:** E-shop nesmí nepravdivě tvrdit „jinde to nekoupíte“, „nejnižší cena na trhu“ nebo „výroba končí“, aby zákazník koupil dráž, než je běžné.
- **Kontrola:** jev + ověřit. Tvrzení je vidět, nepravdivost a méně výhodné podmínky ne.
- **Otázky pro Jev:**
  - `ucp_market_availability`
    - EN: "Does the sentence (field sentence) claim that the product cannot be bought elsewhere, is available only from this shop, or will soon not be available anywhere (for example 'only at our shop', 'you won't find it anywhere else', 'production is ending – last chance')? Answer no if no such claim is made."
    - CS: „Tvrdí věta (pole sentence), že produkt nelze koupit jinde, je k dostání jen v tomto obchodě, nebo že brzy nebude k dostání nikde (například „jen u nás“, „jinde nenajdete“, „výroba končí – poslední šance“)? Pokud nic takového netvrdí, odpověz ne.“
  - `ucp_market_price`
    - EN: "Does the sentence (field sentence) claim that the price is lower than anywhere else on the market, or that prices elsewhere on the market are higher or will soon rise (for example 'lowest price on the market', 'cheapest in the Czech Republic', 'gold prices will go up')? Answer no if no such claim about market prices is made."
    - CS: „Tvrdí věta (pole sentence), že cena je nižší než kdekoli jinde na trhu, nebo že ceny jinde na trhu jsou vyšší či brzy stoupnou (například „nejnižší cena na trhu“, „nejlevněji v ČR“, „ceny zlata porostou“)? Pokud takové tvrzení o cenách na trhu neobsahuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_market_conditions_claim
    title: "Tvrzení o trhu nebo o dostupnosti jinde k ověření"
    logic:
      any: [{q: ucp_market_availability, gte: 0.5}, {q: ucp_market_price, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 18", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. r)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 24", status: "ověřit"}
    explanation: "Věcně nesprávné informace o trhu nebo o možnosti sehnat produkt jinde, které vedou k nákupu za horších podmínek, jsou zakázané vždy."
    recommendation: "Ověřte, že tvrzení o dostupnosti a cenách jinde platí a že je umíte doložit."
  ```
- **Závažnost:** low · verify
- **Poznámky:** Pokyny Komise řadí bod 18 mezi „temné vzorce“ [Pokyny ř. 3574] a u letenek ho spojují s tvrzeními o dostupnosti [Pokyny ř. 3803]. „Nejnižší cena“ je zároveň srovnávací tvrzení podle obecného zákazu. Priorita B.

### ucp_prize_contest

**Bod 19: soutěž o ceny, které se neudělí**

- **Čísla bodů:** EU příloha I bod 19 · CZ příloha č. 1 písm. s) · SK príloha č. 1 bod 25 (do 26. 9. 2026 bod 20)
- **Citace:**
  - EU: „Obchodní praktika, v níž se tvrdí, že v rámci propagace probíhá soutěž o ceny, aniž by byly uděleny ceny, které odpovídají uvedenému popisu, nebo jejich odpovídající náhrada.“ [EU ř. 1028]
  - CZ: „nabízí výrobky nebo služby prostřednictvím soutěže o ceny, aniž by byly ceny uděleny nebo aniž by ceny odpovídaly původní nabídce …“ [CZ ř. 875]
  - SK: „Tvrdenie v obchodnej praktike, že obchodník ponúkne súťaž alebo vypíše cenu bez toho, že by opísanú cenu udelil alebo poskytol zodpovedajúcu náhradu.“ [SK ř. 7756–7757]
- **Požadavek lidsky:** Když e-shop vyhlásí soutěž, musí ceny opravdu udělit tak, jak je popsal.
- **Kontrola:** jev + ověřit. Oznámení soutěže je vidět, udělení cen ne.
- **Otázky pro Jev:**
  - `ucp_contest`
    - EN: "Does the sentence (field sentence) announce a competition, contest, raffle or prize draw? Answer no if no competition or draw is announced."
    - CS: „Oznamuje věta (pole sentence) soutěž, losování nebo slosování o ceny? Pokud žádnou soutěž ani slosování neoznamuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_prize_contest
    title: "Soutěž o ceny k ověření"
    logic:
      all: [{q: ucp_contest, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 19", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. s)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 25", status: "ověřit"}
    explanation: "Soutěž o ceny je zakázaná, pokud se popsané ceny nebo odpovídající náhrada neudělí."
    recommendation: "Ověřte, že máte zveřejněná pravidla soutěže a že výhry opravdu předáváte."
  ```
- **Závažnost:** low · verify
- **Poznámky:** Priorita C (seznam soutěží k ruční kontrole).

### ucp_free_claim

**Bod 20: „zdarma“, ačkoli zákazník něco platí**

- **Čísla bodů:** EU příloha I bod 20 · CZ příloha č. 1 písm. t) · SK príloha č. 1 bod 26 (do 26. 9. 2026 bod 21)
- **Citace:**
  - EU: „Popis produktu slovy „gratis“, „zdarma“, „bezplatně“ a podobnými, pokud musí spotřebitel zaplatit jakékoli jiné náklady, než jen nevyhnutelné náklady spojené s reakcí na obchodní praktiku a s vyzvednutím nebo doručením věci.“ [EU ř. 1032]
  - CZ: „uvádí u výrobku nebo služby slova „gratis", „zdarma", „bezplatně" nebo slova podobného významu, pokud spotřebitel musí za výrobek nebo službu vynaložit jakékoli náklady, s výjimkou nezbytných nákladů …“ [CZ ř. 876]
  - SK: „Opísanie produktu ako „gratis“, „zadarmo“, „bez poplatku“ alebo podobne, pričom spotrebiteľ musí zaplatiť čokoľvek iné okrem nevyhnutných nákladov na odpovedanie na obchodnú praktiku a vyzdvihnutie produktu alebo zaplatenie za jeho doručenie.“ [SK ř. 7759–7761]
- **Požadavek lidsky:** Co je „zdarma“, nesmí stát nic kromě skutečného poštovného nebo doručení. Manipulační, balicí nebo administrativní poplatek u věci zdarma je zakázaný vždy.
- **Kontrola:** jev, když poplatek stojí ve větě nebo hned vedle; jev + ověřit u nabídek podmíněných nákupem (zda se kvůli dárku nezvýšila cena placeného zboží).
- **Otázky pro Jev:**
  - `ucp_free`
    - EN: "Does the sentence (field sentence) describe a product, gift, sample, service or delivery as free, for example 'free', 'for free', 'gratis', 'free of charge' or 'for 0 CZK'? Answer no if nothing is described as free."
    - CS: „Označuje věta (pole sentence) produkt, dárek, vzorek, službu nebo dopravu jako bezplatné, například „zdarma“, „gratis“, „bezplatně“ nebo „za 0 Kč“? Pokud nic jako bezplatné neoznačuje, odpověz ne.“
  - `ucp_free_extra_fee`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that the customer must pay a fee to get something described as free, such as a handling, packaging, processing, administration, activation or service fee? Answer no if nothing is described as free or no such fee is mentioned. Postage, delivery costs and a surcharge for a payment method the customer chooses (such as cash on delivery) do not count."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že za získání něčeho označeného jako bezplatné musí zákazník zaplatit poplatek, například manipulační, balicí (balné), zpracovatelský, administrativní, aktivační nebo servisní poplatek? Pokud nic jako bezplatné neoznačuje nebo takový poplatek neuvádí, odpověz ne. Poštovné, náklady na doručení a příplatek za zvolený způsob platby (například dobírku) se nepočítají.“
  - `ucp_free_with_purchase`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that a free product, gift or sample (not free delivery) is given only when the customer buys something else or spends a minimum amount (for example 'a free gift with every order over 1,000 CZK', 'buy 2, get 1 free')? Answer no if no free product or gift is mentioned or no such condition is stated."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že produkt, dárek nebo vzorek zdarma (ne doprava zdarma) zákazník dostane jen při nákupu něčeho jiného nebo nad určitou částku (například „dárek zdarma ke každé objednávce nad 1 000 Kč“, „kupte 2, třetí zdarma“)? Pokud žádný produkt ani dárek zdarma nezmiňuje nebo takovou podmínku neuvádí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_free_with_fee
    title: "„Zdarma“ s poplatkem"
    logic:
      all: [{q: ucp_free, gte: 0.5}, {q: ucp_free_extra_fee, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 20", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. t)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 26 (do 26. 9. 2026 bod 21)", status: "ověřit"}
    explanation: "Věc označená jako zdarma nesmí zákazníka stát nic kromě nevyhnutelných nákladů na reakci na nabídku a na vyzvednutí nebo doručení."
    recommendation: "Poplatek zrušte, nebo věc přestaňte označovat jako zdarma."
  - id: ucp_free_gift_conditional
    title: "Dárek zdarma podmíněný nákupem k ověření"
    logic:
      all: [{q: ucp_free, gte: 0.5}, {q: ucp_free_with_purchase, gte: 0.5}]
      none: [{q: ucp_free_extra_fee, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs: # stejné jako ucp_free_with_fee
    explanation: "Dárek zdarma k nákupu je v pořádku, jen když se kvůli němu nezvýšila cena ani nezhoršila kvalita placeného zboží."
    recommendation: "Ověřte, že placené zboží stojí s dárkem i bez něj stejně."
  ```
- **Závažnost:** high · text (s poplatkem); low · verify (podmíněný nákupem)
- **Poznámky:**
  - Pokyny Komise: nabídku lze popsat jako zdarma, jen když spotřebitel nezaplatí víc než minimální náklady na reakci, skutečné náklady na přepravu nebo doručení a náklady na cestu pro vyzvednutí [Pokyny ř. 2470–2482]; žádné poplatky za balení, manipulaci ani správu [Pokyny ř. 2484]. Nabídky „kupte jeden, druhý zdarma“ jsou v pořádku, pokud je zákazníkovi jasné, co platí, placené zboží se nezhoršilo a jeho cena se nezvýšila [Pokyny ř. 2506–2518]. Jednotlivou součást balíčku za paušální cenu nelze nazvat zdarma [Pokyny ř. 2552].
  - Poplatek bývá jinde než ve větě (košík, obchodní podmínky). Návrh: `regex_site` pro „balné“, „manipulační poplatek“, „poplatek za zpracování“, „administrativní poplatek“ (SK „manipulačný poplatok“, „poplatok za spracovanie“) přidá k nálezům `ucp_free` poznámku „na webu je poplatek X, ověřte, zda se týká věcí zdarma“.
  - Hranice k ověření s právníkem: dobírka u „doprava zdarma“ (poplatek za způsob platby, ne za dopravu) je v otázce vyloučená.
  - „Vyhráli jste dárek, zaplaťte jen poštovné“ je podle bodu 31 zakázané i při samotném poštovném (viz `ucp_false_prize_win`).
  - Příklady pro vzorek: ano „Dárek zdarma ke každé objednávce – účtujeme jen manipulační poplatek 49 Kč.“, „Vzorek zdarma, stačí uhradit balné 29 Kč.“; ne „Doprava zdarma při nákupu nad 1 500 Kč.“, „Dárek zdarma, platíte jen skutečné poštovné.“
  - Priorita A.

### ucp_invoice_in_marketing

**Bod 21: faktura přiložená k reklamě**

- **Čísla bodů:** EU příloha I bod 21 · CZ příloha č. 1 písm. u) · SK príloha č. 1 bod 27 (do 26. 9. 2026 bod 22)
- **Citace:** EU: „Přiložení faktury nebo podobných dokladů pro provedení platby k marketingovým materiálům, čímž se ve spotřebiteli vyvolá dojem, že si inzerovaný produkt již objednal, ačkoli tomu tak není.“ [EU ř. 1036]
- **Požadavek lidsky:** Reklama nesmí vypadat jako faktura za něco, co si zákazník neobjednal.
- **Kontrola:** nelze z textu. Jde o zásilky a e-maily, ne o obsah webu.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_trader_as_consumer

**Bod 22: obchodník předstírá, že není obchodník**

- **Čísla bodů:** EU příloha I bod 22 · CZ příloha č. 1 písm. v) · SK príloha č. 1 bod 28 (do 26. 9. 2026 bod 23)
- **Citace:**
  - EU: „Nepravdivé tvrzení nebo vyvolávání dojmu, že obchodník nejedná za účelem spojeným s jeho obchodní nebo podnikatelskou činností, řemeslem nebo povoláním, nebo klamná prezentace obchodníka jako spotřebitele.“ [EU ř. 1040]
  - CZ: „vyvolává dojem nebo nepravdivě uvádí, že nejedná v rámci své podnikatelské činnosti nebo se prezentuje jako spotřebitel,“ [CZ ř. 878]
  - SK: „Nepravdivé tvrdenie alebo vytvorenie dojmu, že obchodník nekoná v zámere súvisiacom s jeho podnikateľskou činnosťou alebo povolaním, alebo nepravdivé prezentovanie sa ako spotrebiteľ.“ [SK ř. 7767–7769]
- **Požadavek lidsky:** Prodejce, který podniká, se nesmí tvářit jako soukromá osoba („prodávám ze sbírky“) a nesmí psát recenze nebo příběhy, jako by byl zákazník.
- **Kontrola:** jev + ověřit. Tvrzení o soukromém prodeji je vidět; kód může doplnit rozpor s IČO a obchodními podmínkami na témže webu. Že recenzi psal obchodník, z textu nepoznáme.
- **Otázky pro Jev:**
  - `ucp_private_seller`
    - EN: "Does the sentence (field sentence) state or suggest that the seller is a private person or not a business (for example 'selling as a private person', 'we are not a shop', 'hobby sale', 'from my private collection')? Answer no if the seller's status is not mentioned."
    - CS: „Tvrdí nebo naznačuje věta (pole sentence), že prodávající je soukromá osoba nebo nepodniká (například „prodávám jako soukromá osoba“, „nejsme obchod“, „prodej z hobby“, „z mé soukromé sbírky“)? Pokud o postavení prodávajícího nic neříká, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_private_seller_claim
    title: "Prodejce se prezentuje jako soukromá osoba"
    logic:
      all: [{q: ucp_private_seller, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 22", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. v)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 28", status: "ověřit"}
    explanation: "Obchodník nesmí nepravdivě tvrdit nebo vyvolávat dojem, že nepodniká, ani se prezentovat jako spotřebitel."
    recommendation: "Pokud prodej souvisí s podnikáním, tvrzení o soukromém prodeji odstraňte a uveďte údaje o prodávajícím."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Pokyny Komise vztahují bod 22 i na nepravdivé prohlášení prodejce na tržišti, že není obchodníkem [Pokyny ř. 3220]. Priorita C (tržiště a bazary B).

### ucp_service_other_state

**Bod 23: klamný dojem servisu v jiném členském státě**

- **Čísla bodů:** EU příloha I bod 23 · CZ příloha č. 1 písm. w) · SK príloha č. 1 bod 29 (do 26. 9. 2026 bod 24)
- **Citace:**
  - EU: „Vyvolávání klamného dojmu, že poprodejní servis k výrobku je dostupný v jiném členském státu, než ve kterém je výrobek prodáván.“ [EU ř. 1044]
  - CZ: „vyvolává dojem nebo nepravdivě uvádí, že poprodejní servis k výrobku je poskytován i v jiném členském státě, než ve kterém je výrobek prodáván,“ [CZ ř. 879]
  - SK: „Vytvorenie falošného dojmu, že servis produktu po jeho predaji je dostupný v členskom štáte inom ako ten, v ktorom sa produkt predáva.“ [SK ř. 7771–7772]
- **Požadavek lidsky:** „Záruka platná v celé EU“ smí být jen tam, kde servis v ostatních státech opravdu funguje (typicky problém u zboží z dovozu mimo oficiální distribuci).
- **Kontrola:** jev + ověřit.
- **Otázky pro Jev:**
  - `ucp_service_abroad`
    - EN: "Does the sentence (field sentence) state that service, repair or warranty for the product is available in other countries (for example 'EU-wide warranty', 'service in all EU countries', 'international warranty')? Answer no if service in other countries is not mentioned."
    - CS: „Uvádí věta (pole sentence), že servis, oprava nebo záruka k produktu jsou dostupné i v jiných zemích (například „záruka platná v celé EU“, „servis ve všech zemích EU“, „mezinárodní záruka“)? Pokud servis v jiných zemích nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_service_abroad_claim
    title: "Tvrzení o servisu v jiných státech k ověření"
    logic:
      all: [{q: ucp_service_abroad, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. w)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 29", status: "ověřit"}
    explanation: "Klamný dojem, že poprodejní servis je dostupný i v jiném členském státě, je zakázaný vždy."
    recommendation: "Ověřte, že výrobce nebo vy servis v jiných státech skutečně zajišťujete."
  ```
- **Závažnost:** low · verify
- **Poznámky:** Priorita C.

### ucp_ticket_bots

**Bod 23a: přeprodej vstupenek získaných roboty**

- **Čísla bodů:** EU příloha I bod 23a · CZ příloha č. 1 písm. x) · SK príloha č. 1 bod 30 (do 26. 9. 2026 bod 25)
- **Citace:** EU: „Přeprodej vstupenek spotřebitelům, pokud je obchodník získal na základě využití automatizovaných prostředků s cílem obejít stanovené limity pro počet vstupenek, které může zakoupit jedna osoba …“ [EU ř. 1050]
- **Požadavek lidsky:** Přeprodejce nesmí prodávat vstupenky nakoupené roboty obcházejícími limity.
- **Kontrola:** nelze z textu. Rozhoduje způsob nákupu vstupenek.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_reviews_verified_claim

**Bod 23b: „ověřené recenze“ bez ověřování**

- **Čísla bodů:** EU příloha I bod 23b · CZ příloha č. 1 písm. y) · SK príloha č. 1 bod 31 (do 26. 9. 2026 bod 26)
- **Citace:**
  - EU: „Tvrzení, že recenze produktu podávají spotřebitelé, kteří produkt skutečně použili nebo jej zakoupili, aniž by byly přijaty rozumné a přiměřené kroky k ověření toho, zda pocházejí od těchto spotřebitelů.“ [EU ř. 1054]
  - CZ: „uvádí, že recenze výrobku nebo služby podává spotřebitel, který produkt skutečně použil nebo jej zakoupil, aniž by přijal přiměřená opatření k ověření toho, …“ [CZ ř. 881]
  - SK: „Vyhlásenie, že hodnotenia produktu poskytujú spotrebitelia, ktorí tento produkt skutočne použili alebo kúpili, bez prijatia náležitých a primeraných krokov na kontrolu toho, …“ [SK ř. 7779–7781]
- **Požadavek lidsky:** Kdo píše „recenze od ověřených zákazníků“, musí mít opatření, která to zajistí (například hodnotit smí jen ten, kdo objednal).
- **Kontrola:** jev + ověřit. Tvrzení o původu recenzí je vidět, opatření ne. Doplnit o informační povinnost (níže), kterou z textu webu zkontrolovat jde.
- **Otázky pro Jev:**
  - `ucp_reviews_verified_claim`
    - EN: "Does the sentence (field sentence) state or suggest that the reviews or ratings come from real or verified customers who bought or used the product (for example 'verified reviews', 'reviews from real customers', 'only buyers can rate')? Answer no if the sentence does not say who wrote the reviews."
    - CS: „Tvrdí nebo naznačuje věta (pole sentence), že recenze nebo hodnocení pocházejí od skutečných či ověřených zákazníků, kteří produkt koupili nebo použili (například „ověřené recenze“, „hodnocení skutečných zákazníků“, „hodnotit mohou jen kupující“)? Pokud věta neříká, kdo recenze napsal, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_reviews_verified_claim
    title: "Tvrzení o ověřených recenzích k ověření"
    logic:
      all: [{q: ucp_reviews_verified_claim, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23b", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. y)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 31", status: "ověřit"}
    explanation: "Tvrdit, že recenze píšou zákazníci, kteří produkt koupili nebo použili, smí jen obchodník, který to přiměřeně ověřuje."
    recommendation: "Ověřte, jak recenze sbíráte (například jen z objednávek), a popište to na webu u recenzí."
  ```
- **Závažnost:** medium · verify
- **Poznámky:**
  - Informační povinnost (klamavé opomenutí, ne černá listina): kdo zpřístupňuje recenze, musí uvést, zda a jak zajišťuje, že pocházejí od kupujících (CZ § 5a odst. 5 [CZ ř. 185]; SK § 11 ods. 6 písm. a) [SK ř. 1755–1757]). Informace má být tam, kde se recenze čtou, nebo na jasném odkazu [Pokyny ř. 3350]. Návrh otázky do legal (vyžaduje `site_presence_if`: jen když web recenze zobrazuje, kód podle JSON-LD `aggregateRating` nebo `review`):
    - `legal_reviews_info`
      - EN: "Does this text explain whether and how the shop checks that published customer reviews come from customers who actually bought or used the product?"
      - CS: „Vysvětluje tento text, zda a jak obchod ověřuje, že zveřejněné zákaznické recenze pocházejí od zákazníků, kteří produkt skutečně koupili nebo použili?“
  - Bod 23b se týká i obecnějších odkazů na recenze „zákazníků“, pokud je průměrný spotřebitel chápe jako recenze kupujících [Pokyny ř. 3362]. Přiměřené kroky: číslo objednávky, registrace, ověření e-mailem, pravidla proti falešným recenzím [Pokyny ř. 3368–3388].
  - Příklady pro vzorek: ano „Všechny recenze jsou od ověřených zákazníků.“, „Hodnotit mohou jen zákazníci, kteří u nás nakoupili.“; ne „Napište nám recenzi.“
  - Priorita A.

### ucp_reviews_fake_or_distorted

**Bod 23c: falešné nebo zkreslené recenze**

- **Čísla bodů:** EU příloha I bod 23c · CZ příloha č. 1 písm. z) · SK príloha č. 1 bod 32 (do 26. 9. 2026 bod 27)
- **Citace:**
  - EU: „Prezentace falešných spotřebitelských recenzí či doporučení nebo zadávání jiným právnickým či fyzickým osobám, aby takové recenze či doporučení podaly, nebo zkreslování spotřebitelských recenzí či doporučení na sociálních sítích s cílem propagovat produkty.“ [EU ř. 1058]
  - CZ: „zveřejňuje falešné spotřebitelské recenze či doporučení nebo zadává jiné osobě, aby takové spotřebitelské recenze či doporučení podala, nebo zkresluje spotřebitelské recenze …“ [CZ ř. 882]
  - SK: „Predloženie alebo poverenie inej osoby, aby poskytla falošné spotrebiteľské hodnotenia alebo odporúčania, alebo skresľovanie spotrebiteľských hodnotení alebo odporúčaní …“ [SK ř. 7783–7785]
- **Požadavek lidsky:** E-shop nesmí vymýšlet recenze, platit za kladné hodnocení ani zveřejňovat jen kladné recenze a mazat záporné.
- **Kontrola:** jev pro dva znaky, které web sám prozradí: odměna vázaná na kladné hodnocení a přiznané zveřejňování jen kladných recenzí. Falešnost jednotlivé recenze z textu nepoznáme (kód může hledat shodné texty recenzí napříč produkty, slabý signál).
- **Otázky pro Jev:**
  - `ucp_review_reward`
    - EN: "Does the sentence (field sentence) offer customers a reward, discount, gift, money or entry into a prize draw for writing a review or giving a rating? Answer no if no reward for a review is offered."
    - CS: „Nabízí věta (pole sentence) zákazníkům odměnu, slevu, dárek, peníze nebo zařazení do slosování za napsání recenze nebo za hodnocení? Pokud žádnou odměnu za recenzi nenabízí, odpověz ne.“
  - `ucp_review_reward_positive`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) make a reward conditional on a positive review or a top rating (for example 'for a 5-star review')? Answer no if no reward is mentioned or it is not tied to a positive review."
    - CS: „Podmiňuje věta (pole sentence) nebo text hned vedle ní (context_before, context_after) odměnu kladnou recenzí nebo nejvyšším hodnocením (například „za hodnocení 5 hvězdičkami“)? Pokud odměnu nezmiňuje nebo ji nepodmiňuje kladnou recenzí, odpověz ne.“
  - `ucp_reviews_only_positive`
    - EN: "Does the sentence (field sentence) say that only positive or favourable reviews are published, or that negative reviews are removed or not published? Answer no if it only says that reviews are checked for spam, offensive language or authenticity."
    - CS: „Uvádí věta (pole sentence), že se zveřejňují jen kladné či příznivé recenze, nebo že se záporné recenze mažou či nezveřejňují? Pokud jen říká, že se recenze kontrolují kvůli spamu, vulgaritám nebo pravosti, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_review_paid_positive
    title: "Odměna za kladnou recenzi"
    logic:
      all: [{q: ucp_review_reward, gte: 0.5}, {q: ucp_review_reward_positive, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23c", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 1 písm. z)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 32", status: "ověřit"}
    explanation: "Platit zákazníkům za kladné recenze je zadávání falešných nebo zkreslených recenzí, zakázané vždy."
    recommendation: "Odměnu za recenzi nevažte na hodnocení, nebo ji zrušte; u odměněných recenzí to uveďte."
  - id: ucp_reviews_only_positive
    title: "Zveřejňování jen kladných recenzí"
    logic:
      all: [{q: ucp_reviews_only_positive, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs: # stejné jako ucp_review_paid_positive (bod 23c, CZ písm. z), SK bod 32)
    explanation: "Zveřejňovat jen kladné recenze a záporné mazat je zkreslování spotřebitelských recenzí, zakázané vždy."
    recommendation: "Zveřejňujte kladné i záporné recenze; mazat smíte jen spam, vulgarity a prokazatelně falešné recenze."
  - id: ucp_review_reward_disclosure
    title: "Odměna za recenzi k ověření označení"
    logic:
      all: [{q: ucp_review_reward, gte: 0.5}]
      none: [{q: ucp_review_reward_positive, gte: 0.5}]
    severity: low
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, čl. 7 odst. 2 (obchodní záměr), souvisí s přílohou I bodem 23c", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., § 5a odst. 2", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., § 11 ods. 2", status: "ověřit"}
    explanation: "Odměna za recenzi není sama zakázaná, ale recenze za odměnu se musí dát poznat a odměna nesmí záviset na kladném hodnocení."
    recommendation: "U recenzí napsaných za odměnu to uveďte a odměnu dávejte za jakoukoli recenzi."
  ```
- **Závažnost:** high · text (placené kladné recenze, jen kladné recenze); low · verify (odměna za jakoukoli recenzi)
- **Poznámky:**
  - Pokyny Komise: bod 23c se vztahuje „zejména na praktiku zapojování skutečných spotřebitelů, kteří … dostávají odměnu za zveřejnění pozitivních recenzí“ [Pokyny ř. 3402] a na zkreslování typu „si vyžádají a zpřístupní pouze pozitivní recenze“ [Pokyny ř. 3406]; odůvodnění 49 směrnice 2019/2161 uvádí zveřejňování jen kladných a mazání záporných [Pokyny ř. 3416].
  - Odměna za jakoukoli recenzi není sama černá listina, ale obchodní záměr se musí uvést (čl. 7 odst. 2 směrnice; CZ § 5a odst. 2 [CZ ř. 176]), proto low · verify.
  - Příklady pro vzorek: ano „Za hodnocení 5 hvězdičkami vám vrátíme 100 Kč.“, „Zveřejňujeme pouze pozitivní recenze.“; ne „Recenze kontrolujeme kvůli spamu a vulgaritám.“, „Napište recenzi, moc nám pomůže.“
  - Priorita A.

### ucp_update_impact_withheld

**Bod 23d (EmpCo): zamlčení negativního dopadu aktualizace softwaru**

- **Čísla bodů:** EU příloha I bod 23d · CZ — (netransponováno) · SK príloha č. 1 bod 33 (nový od 27. 9. 2026)
- **Citace:** EU: „Zamlčení před spotřebitelem informace o skutečnosti, že aktualizace softwaru negativně ovlivní fungování zboží s digitálními prvky nebo používání digitálního obsahu nebo digitálních služeb.“ [EU ř. 1064]
- **Požadavek lidsky:** Kdo vyzývá k aktualizaci, nesmí zamlčet, že zpomalí zařízení nebo zkrátí výdrž baterie.
- **Kontrola:** nelze z textu. Jde o zamlčení v souvislosti s konkrétní aktualizací (typicky v zařízení), ne o prodejní text; chybějící informaci z věty nepoznáme.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Příbuzná informační povinnost: minimální doba aktualizací, pokud ji výrobce zpřístupnil (SK § 5 ods. 1 písm. p) [SK ř. 1437–1441]), viz sekce o informačních povinnostech. Priorita –.

### ucp_update_presented_necessary

**Bod 23e (EmpCo): aktualizace prezentovaná jako nezbytná**

- **Čísla bodů:** EU příloha I bod 23e · CZ — (netransponováno) · SK príloha č. 1 bod 34 (nový od 27. 9. 2026)
- **Citace:** EU: „Prezentace aktualizace softwaru jako nezbytné i v případě, kdy pouze zlepšuje prvky funkčnosti.“ [EU ř. 1068]
- **Požadavek lidsky:** Aktualizace, která jen přidává funkce, se nesmí vydávat za nutnou.
- **Kontrola:** nelze z textu. Týká se oznámení o aktualizaci v zařízení nebo aplikaci, ne prodejního textu e-shopu.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_durability_limiting_feature

**Bod 23f (EmpCo): propagace zboží se zabudovaným omezením trvanlivosti**

- **Čísla bodů:** EU příloha I bod 23f · CZ — (netransponováno) · SK príloha č. 1 bod 35 (nový od 27. 9. 2026)
- **Citace:** EU: „Jakékoli obchodní sdělení týkající se zboží obsahujícího prvek zavedený s cílem omezit jeho trvanlivost, přestože obchodník má k dispozici informace o tomto prvku a jeho účincích na trvanlivost zboží.“ [EU ř. 1072]
- **Požadavek lidsky:** Zboží, o kterém obchodník ví, že má prvek omezující životnost, nesmí vůbec propagovat.
- **Kontrola:** nelze z textu. Rozhoduje existence prvku a znalost obchodníka (odůvodnění 19 [EmpCo ř. 57–58]).
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_durability_claim

**Bod 23g (EmpCo): nepravdivé tvrzení o trvanlivosti**

- **Čísla bodů:** EU příloha I bod 23g (v konsolidaci „23 g.“) · CZ — (netransponováno) · SK príloha č. 1 bod 36 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Nepravdivé tvrzení, že za běžných podmínek používání má zboží určitou trvanlivost, pokud jde o dobu nebo intenzitu používání.“ [EU ř. 1076]
  - SK: „Nepravdivé tvrdenie, že za bežných podmienok používania má tovar určitú životnosť z hľadiska času alebo intenzity používania.“ [SK ř. 7800–7801]
- **Požadavek lidsky:** „Životnost 50 000 hodin“, „vydrží 10 let“, „5 000 pracích cyklů“ smí e-shop uvést, jen když to odpovídá skutečnosti; prodejce se může opřít o spolehlivé údaje výrobce (odůvodnění 20 [EmpCo ř. 60]).
- **Kontrola:** jev + ověřit. Tvrzení je vidět, pravdivost ne.
- **Otázky pro Jev:**
  - `ucp_durability`
    - EN: "Does the sentence (field sentence) state how long the product will last or how much use it will withstand under normal use, for example a lifetime in years or hours, a number of cycles, washes or uses, or words such as 'lasts a lifetime', 'indestructible' or 'will last for years'? Answer no if it says nothing about how long the product lasts. A warranty period alone, and battery life per charge, do not count."
    - CS: „Uvádí věta (pole sentence), jak dlouho produkt při běžném používání vydrží nebo kolik používání snese, například životnost v letech nebo hodinách, počet cyklů, praní či použití, nebo slova jako „vydrží celý život“, „nezničitelný“ či „vydrží roky“? Pokud o tom, jak dlouho produkt vydrží, nic neříká, odpověz ne. Samotná délka záruky a výdrž baterie na jedno nabití se nepočítají.“
- **Skládání:**
  ```yaml
  - id: ucp_durability_claim
    title: "Tvrzení o trvanlivosti k ověření"
    logic:
      all: [{q: ucp_durability, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23g, ve znění směrnice (EU) 2024/825", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 36, účinnost od 27. 9. 2026", status: "ověřit"}
      - {jurisdiction: cz, ref: "Netransponováno (sněmovní tisk 53); do té doby jen § 5 zákona č. 634/1992 Sb.", status: "ověřit"}
    explanation: "Nepravdivé tvrzení o trvanlivosti zboží za běžného používání je zakázané vždy."
    recommendation: "Ověřte, že údaj o trvanlivosti máte doložený od výrobce nebo zkouškou; nedoložený odstraňte."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Kód může z věty vytáhnout číslo s jednotkou pro ruční kontrolu (`regex_present`). Obchodní záruka výrobce na trvanlivost nad 2 roky spouští na SK povinnost štítku GARAN (sekce o informačních povinnostech). Priorita B.

### ucp_repairability_claim

**Bod 23h (EmpCo): zboží prezentované jako opravitelné, když není**

- **Čísla bodů:** EU příloha I bod 23h · CZ — (netransponováno) · SK príloha č. 1 bod 37 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Prezentace zboží v tom smyslu, že je lze opravit, pokud tomu tak není.“ [EU ř. 1080]
  - SK: „Prezentovanie tovaru ako tovaru, ktorý možno opraviť, pričom tomu tak nie je.“ [SK ř. 7803]
- **Požadavek lidsky:** „Snadno opravitelný“, „vyměnitelná baterie“, „náhradní díly 10 let“ smí být jen tam, kde to platí.
- **Kontrola:** jev + ověřit.
- **Otázky pro Jev:**
  - `ucp_repairable`
    - EN: "Does the sentence (field sentence) state or suggest that the product can be repaired, that spare parts are available, or that parts such as the battery can be replaced? Answer no if repair, spare parts and replaceable parts are not mentioned."
    - CS: „Uvádí nebo naznačuje věta (pole sentence), že produkt lze opravit, že jsou k dispozici náhradní díly nebo že lze vyměnit jeho části, například baterii? Pokud opravu, náhradní díly ani vyměnitelné části nezmiňuje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_repairability_claim
    title: "Tvrzení o opravitelnosti k ověření"
    logic:
      all: [{q: ucp_repairable, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23h, ve znění směrnice (EU) 2024/825", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 37, účinnost od 27. 9. 2026", status: "ověřit"}
      - {jurisdiction: cz, ref: "Netransponováno (sněmovní tisk 53); do té doby jen § 5 zákona č. 634/1992 Sb.", status: "ověřit"}
    explanation: "Prezentovat zboží jako opravitelné je zakázané, pokud ho opravit nelze."
    recommendation: "Ověřte u výrobce, že oprava a náhradní díly jsou skutečně dostupné."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Q&A: povinnost aktivně informovat o neopravitelnosti obecně není, ale když obchodník informace o opravě sám poskytuje, má uvést i omezení [QA ř. 938–942]. Věty o záručním a pozáručním servisu obchodu také vyjdou „ano“ (tvrzení o možnosti opravy); počítat s nálezy k ověření. Priorita B.

### ucp_consumables_early_replacement

**Bod 23i (EmpCo): nabádání k předčasné výměně spotřebního materiálu**

- **Čísla bodů:** EU příloha I bod 23i · CZ — (netransponováno) · SK príloha č. 1 bod 38 (nový od 27. 9. 2026)
- **Citace:** EU: „Snaha přimět spotřebitele k výměně nebo doplnění spotřebního materiálu ve zboží dříve, než je z technických důvodů nezbytné.“ [EU ř. 1084]
- **Požadavek lidsky:** Zařízení ani prodejce nesmí tlačit na výměnu náplně nebo filtru dřív, než je technicky nutné.
- **Kontrola:** nelze z textu (spolehlivě). Typický případ je hlášení tiskárny (odůvodnění 23 [EmpCo ř. 66]); doporučený interval výměny v popisu produktu z věty posoudit nejde, technickou nutnost zná jen výrobce.
- **Otázky pro Jev:** —
- **Skládání:** —
- **Závažnost:** —
- **Poznámky:** Pokud by bylo potřeba seznam doporučených intervalů k ruční kontrole, šla by přidat otázka „věta radí měnit spotřební materiál v určitém intervalu“; čekám hodně legitimních návodů, proto ji nenavrhuji. Priorita –.

### ucp_non_original_parts_claim

**Bod 23j (EmpCo): neoriginální spotřební materiál a díly**

- **Čísla bodů:** EU příloha I bod 23j · CZ — (netransponováno) · SK príloha č. 1 bod 39 (nový od 27. 9. 2026)
- **Citace:**
  - EU: „Zamlčení informace o narušení funkčnosti zboží v případě, že se použije spotřební materiál, náhradní díly nebo příslušenství, které nedodal původní výrobce, nebo nepravdivé tvrzení, že k takovému narušení funkčnosti dojde.“ [EU ř. 1088]
  - SK: „Zamlčanie informácie o poškodení funkčnosti tovaru pri použití komponentu tovaru, … ktoré neboli dodané pôvodným výrobcom, alebo nepravdivé tvrdenie, že k takémuto poškodeniu dôjde.“ [SK ř. 7809–7813]
- **Požadavek lidsky:** Prodejce nesmí zamlčet, že tiskárna s neoriginálními náplněmi nefunguje, a nesmí nepravdivě strašit, že neoriginální náplň nebo nabíječka zařízení poškodí.
- **Kontrola:** jev + ověřit pro druhou část (tvrzení o poškození je vidět, pravdivost ne); první část (zamlčení) nelze z textu.
- **Otázky pro Jev:**
  - `ucp_non_original_warning`
    - EN: "Does the sentence (field sentence) claim that using non-original or third-party consumables, spare parts, chargers or accessories will damage the product, impair how it works or void the warranty? Answer no if no such consequence is claimed."
    - CS: „Tvrdí věta (pole sentence), že použití neoriginálního spotřebního materiálu, náhradních dílů, nabíječek nebo příslušenství produkt poškodí, zhorší jeho funkci nebo zruší záruku? Pokud žádný takový následek netvrdí, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_non_original_parts_claim
    title: "Tvrzení o škodlivosti neoriginálních dílů k ověření"
    logic:
      all: [{q: ucp_non_original_warning, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 23j, ve znění směrnice (EU) 2024/825", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1 bod 39, účinnost od 27. 9. 2026", status: "ověřit"}
      - {jurisdiction: cz, ref: "Netransponováno (sněmovní tisk 53); do té doby jen § 5 zákona č. 634/1992 Sb.", status: "ověřit"}
    explanation: "Nepravdivé tvrzení, že neoriginální spotřební materiál, díly nebo příslušenství naruší funkčnost zboží, je zakázané vždy."
    recommendation: "Ověřte u výrobce, zda k narušení funkčnosti opravdu dochází; nedoložené tvrzení odstraňte."
  ```
- **Závažnost:** medium · verify
- **Poznámky:** Tvrzení „neoriginální náplň = ztráta záruky“ může zároveň klamat o právech z vadného plnění (CZ § 5 odst. 2 písm. g) [CZ ř. 167]); právník posoudí, zda zvýšit na high. Priorita B.

## Agresivní praktiky (EU body 24–31)

### ucp_cannot_leave_premises

**Bod 24: dojem, že spotřebitel nemůže odejít bez smlouvy**

- **Čísla bodů:** EU příloha I bod 24 · CZ příloha č. 2 písm. a) · SK príloha č. 1, Agresívne bod 1
- **Citace:** EU: „Vytvoření dojmu, že spotřebitel nemůže provozovnu opustit bez uzavření smlouvy.“ [EU ř. 1096]
- **Požadavek lidsky:** Zákazníka nelze držet v provozovně, dokud nepodepíše.
- **Kontrola:** nelze z textu. Fyzická situace v provozovně.
- **Otázky pro Jev:** — · **Skládání:** — · **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_home_visits

**Bod 25: opakované návštěvy doma přes odmítnutí**

- **Čísla bodů:** EU příloha I bod 25 · CZ příloha č. 2 písm. b) · SK príloha č. 1, Agresívne bod 2
- **Citace:** EU: „Osobní návštěvy u spotřebitele a nedbání požadavku spotřebitele opustit jeho byt a nevracet se, kromě situací a v rozsahu odůvodněném podle vnitrostátních právních předpisů za účelem vymáhání smluvních závazků.“ [EU ř. 1100]
- **Požadavek lidsky:** Obchodník nesmí chodit k zákazníkovi domů, když ho zákazník vykázal.
- **Kontrola:** nelze z textu. Osobní kontakt.
- **Otázky pro Jev:** — · **Skládání:** — · **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_persistent_solicitation

**Bod 26: vytrvalé nevyžádané nabídky na dálku**

- **Čísla bodů:** EU příloha I bod 26 · CZ příloha č. 2 písm. c) · SK príloha č. 1, Agresívne bod 3
- **Citace:** EU: „Vytrvalé a nevyžádané nabídky prostřednictvím telefonu, faxu, e-mailu nebo jiných prostředků přenosu na dálku, kromě situací a v rozsahu odůvodněném podle vnitrostátních právních předpisů za účelem vymáhání smluvních závazků.“ [EU ř. 1104]
- **Požadavek lidsky:** Zákazníka nelze zahlcovat telefonáty a e-maily s nabídkami.
- **Kontrola:** nelze z textu. Komunikace mimo web; opakované vyskakovací okno na webu Pokyny zmiňují jen jako možnost [Pokyny ř. 3582] a crawler ho neuvidí.
- **Otázky pro Jev:** — · **Skládání:** — · **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_insurance_claim_obstruction

**Bod 27: obstrukce při pojistném plnění**

- **Čísla bodů:** EU příloha I bod 27 · CZ příloha č. 2 písm. d) · SK príloha č. 1, Agresívne bod 4
- **Citace:** EU: „Požadavek, aby spotřebitel žádající plnění z důvodu pojistné události předložil doklady, které nelze odůvodněně pokládat za důležité pro stanovení oprávněnosti nároku, nebo systematické neodpovídání na související korespondenci …“ [EU ř. 1110]
- **Požadavek lidsky:** Pojistitel nesmí odrazovat od plnění nesmyslnými doklady nebo mlčením.
- **Kontrola:** nelze z textu. Týká se pojištění a jednání při plnění.
- **Otázky pro Jev:** — · **Skládání:** — · **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_children_direct_exhortation

**Bod 28: přímé nabádání dětí ke koupi**

- **Čísla bodů:** EU příloha I bod 28 · CZ příloha č. 2 písm. e) · SK príloha č. 1, Agresívne bod 5
- **Citace:**
  - EU: „Začlenění do reklamy přímého nabádání určeného dětem, aby si inzerované produkty koupily nebo aby přesvědčily své rodiče nebo jiné dospělé, aby jim je koupili.“ [EU ř. 1114]
  - CZ: „prostřednictvím reklamy přímo nabádá děti, aby si nabízené výrobky nebo služby koupily nebo aby k jejich koupi přesvědčily dospělou osobu,“ [CZ ř. 890]
  - SK: „Zahrnutie priameho nabádania pre deti do reklamy, aby si kúpili alebo aby presvedčili svojich rodičov alebo iných dospelých, aby im kúpili propagované produkty.“ [SK ř. 7832–7833]
- **Požadavek lidsky:** E-shop s hračkami nebo hrami nesmí dětem psát „kup si“ nebo „řekni mamince, ať ti to koupí“.
- **Kontrola:** jev. Oba znaky (přímá výzva ke koupi, oslovení dětí) jsou v textu.
- **Otázky pro Jev:**
  - `ucp_buy_appeal`
    - EN: "Does the sentence (field sentence) directly urge the reader to buy the product or to get a parent or another adult to buy it (for example 'buy it now', 'get yours today', 'ask your mum to buy it for you')? Answer no if it only describes the product."
    - CS: „Vyzývá věta (pole sentence) čtenáře přímo, aby produkt koupil nebo aby ho koupit přiměl rodiče či jiného dospělého (například „kup si ho hned“, „pořiď si ho ještě dnes“, „řekni mamince, ať ti ho koupí“)? Pokud jen popisuje produkt, odpověz ne.“
  - `ucp_addressed_to_children`
    - EN: "Is the sentence (field sentence) addressed to children, for example by speaking directly to a child reader, using children's language or referring to the reader's parents? Answer no if it is addressed to adults or to no one in particular."
    - CS: „Je věta (pole sentence) určená dětem, například tím, že přímo oslovuje dětského čtenáře, používá dětský jazyk nebo zmiňuje čtenářovy rodiče? Pokud je určená dospělým nebo nikomu konkrétnímu, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_children_direct_exhortation
    title: "Přímá výzva dětem ke koupi"
    logic:
      all: [{q: ucp_buy_appeal, gte: 0.5}, {q: ucp_addressed_to_children, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 28", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 2 písm. e)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1, agresívne obchodné praktiky bod 5", status: "ověřit"}
    explanation: "Přímé nabádání dětí, aby si produkt koupily nebo aby ke koupi přesvědčily rodiče, je agresivní praktika zakázaná vždy."
    recommendation: "Výzvu ke koupi směřujte na dospělé; texty pro děti nechte popisné."
  ```
- **Závažnost:** high · text
- **Poznámky:** Pokyny Komise: posuzuje se jazyk, témata a postavy lákavé pro děti, přímé odkazy na nákup [Pokyny ř. 2618]; soud zakázal výzvy „nakup více“, „teď upgraduj“ ve hře pro děti [Pokyny ř. 2624] a stačí oslovení ve druhé osobě s dětskými výrazy, i když cena je až po kliknutí [Pokyny ř. 2640]. Obecné „Kupte teď!“ pro dospělé má `ucp_addressed_to_children` vyjít ne. Priorita B (hračky, hry, dětské zboží).

### ucp_inertia_selling

**Bod 29: platba za neobjednané zboží**

- **Čísla bodů:** EU příloha I bod 29 · CZ příloha č. 2 písm. f) · SK príloha č. 1, Agresívne bod 6
- **Citace:** EU: „Požadování okamžité nebo odložené platby za produkty dodané obchodníkem, avšak nevyžádané spotřebitelem, nebo vrácení nebo uschování takových produktů …“ [EU ř. 1118]
- **Požadavek lidsky:** Za neobjednané zboží nelze chtít zaplatit ani ho vracet.
- **Kontrola:** nelze z textu. Jednání po dodání.
- **Otázky pro Jev:** — · **Skládání:** — · **Závažnost:** —
- **Poznámky:** Priorita –.

### ucp_trader_livelihood_appeal

**Bod 30: „když nekoupíte, přijdeme o práci“**

- **Čísla bodů:** EU příloha I bod 30 · CZ příloha č. 2 písm. g) · SK príloha č. 1, Agresívne bod 7
- **Citace:**
  - EU: „Výslovné sdělení spotřebiteli, že pokud si produkt nebo službu nekoupí, ohrozí to pracovní místo nebo živobytí obchodníka.“ [EU ř. 1122]
  - CZ: „prohlašuje, že pokud si spotřebitel výrobek nebo službu nekoupí, ohrozí tím jeho podnikání, pracovní místo nebo existenci,“ [CZ ř. 892]
  - SK: „Výslovné informovanie spotrebiteľa, že ak si nekúpi produkt, bude ohrozené zamestnanie alebo živobytie obchodníka.“ [SK ř. 7840–7841]
- **Požadavek lidsky:** E-shop nesmí zákazníkovi výslovně psát, že když nenakoupí, obchodník nebo zaměstnanci přijdou o práci nebo firma zavře.
- **Kontrola:** jev. Zakázané je samo výslovné sdělení.
- **Otázky pro Jev:**
  - `ucp_livelihood_appeal`
    - EN: "Does the sentence (field sentence) say that if the reader does not buy, the seller or its staff will lose their job or income, or the business will have to close? Answer no if it only asks for support without saying so."
    - CS: „Uvádí věta (pole sentence), že pokud čtenář nenakoupí, prodávající nebo jeho zaměstnanci přijdou o práci či příjem, nebo že firma bude muset zavřít? Pokud jen prosí o podporu a nic takového neříká, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_trader_livelihood_appeal
    title: "Nátlak ohrožením živobytí obchodníka"
    logic:
      all: [{q: ucp_livelihood_appeal, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 30", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 2 písm. g)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1, agresívne obchodné praktiky bod 7", status: "ověřit"}
    explanation: "Výslovné sdělení, že bez nákupu přijde obchodník o práci nebo živobytí, je agresivní praktika zakázaná vždy."
    recommendation: "Tvrzení odstraňte."
  ```
- **Závažnost:** high · text
- **Poznámky:** CZ znění mluví o ohrožení „jeho podnikání, pracovního místa nebo existence“; „jeho“ je v kontextu obchodník, jako v EU. Priorita C.

### ucp_false_prize_win

**Bod 31: klamný dojem výhry**

- **Čísla bodů:** EU příloha I bod 31 · CZ příloha č. 2 písm. h) · SK príloha č. 1, Agresívne bod 8
- **Citace:**
  - EU: „Vytváření klamného dojmu, že spotřebitel již vyhrál nebo vyhraje, popřípadě že vyhraje, pokud bude jednat určitým způsobem, cenu nebo jinou obdobnou výhodu, ačkoli ve skutečnosti … pro získání ceny nebo jiné obdobné výhody musí spotřebitel vynaložit finanční prostředky …“ [EU ř. 1126–1136]
  - CZ: „vytváří klamný dojem, že spotřebitel vyhrál nebo vyhraje, … ačkoli ve skutečnosti žádná taková cena ani obdobná výhra neexistuje nebo pro získání ceny … musí spotřebitel vynaložit finanční prostředky nebo mu vznikají výdaje.“ [CZ ř. 893]
  - SK: „Vytváranie falošného dojmu, že spotrebiteľ už vyhral, vyhrá, alebo potom, čo niečo urobí, vyhrá cenu alebo získa iný rovnocenný prospech, keď v skutočnosti a) neexistuje cena … alebo b) … je podmienená tým, že spotrebiteľ uhradí hotovosť alebo si spôsobí náklady.“ [SK ř. 7843–7849]
- **Požadavek lidsky:** „Gratulujeme, vyhráli jste“ je zakázané, když výhra neexistuje nebo za ni zákazník musí cokoli zaplatit, i jen poštovné.
- **Kontrola:** jev, když věta nebo kontext uvádí platbu; jev + ověřit, když neuvádí (existence výhry).
- **Otázky pro Jev:**
  - `ucp_you_won`
    - EN: "Does the sentence (field sentence) tell the reader that they have already won, will win, or have been selected to receive a prize, gift or reward (for example 'Congratulations, you have won', 'You have been selected for a gift')? Answer no for a mere invitation to enter a competition, such as 'Win an iPhone!'."
    - CS: „Sděluje věta (pole sentence) čtenáři, že už vyhrál, vyhraje nebo byl vybrán k získání ceny, dárku či odměny (například „Gratulujeme, vyhráli jste“, „Byli jste vybráni pro dárek“)? U pouhé výzvy k účasti v soutěži, jako „Vyhrajte iPhone!“, odpověz ne.“
  - `ucp_prize_cost`
    - EN: "Does the sentence (field sentence) or the text right next to it (context_before, context_after) say that the reader must pay something, buy something or call a premium-rate number to receive a prize or gift, including postage or a fee? Answer no if no prize or gift is mentioned or no payment or cost is required."
    - CS: „Uvádí věta (pole sentence) nebo text hned vedle ní (context_before, context_after), že čtenář musí za získání ceny nebo dárku něco zaplatit, koupit nebo zavolat na placenou linku, včetně poštovného či poplatku? Pokud cenu ani dárek nezmiňuje nebo žádnou platbu ani náklad nevyžaduje, odpověz ne.“
- **Skládání:**
  ```yaml
  - id: ucp_false_prize_win
    title: "Výhra, za kterou se platí"
    logic:
      all: [{q: ucp_you_won, gte: 0.5}, {q: ucp_prize_cost, gte: 0.5}]
    severity: high
    checkability: text
    legal_refs:
      - {jurisdiction: eu, ref: "Směrnice 2005/29/ES, příloha I bod 31", status: "ověřit"}
      - {jurisdiction: cz, ref: "Zákon č. 634/1992 Sb., příloha č. 2 písm. h)", status: "ověřit"}
      - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., príloha č. 1, agresívne obchodné praktiky bod 8", status: "ověřit"}
    explanation: "Oznámit zákazníkovi výhru, za kterou musí cokoli zaplatit, je agresivní praktika zakázaná vždy, i když jde jen o poštovné."
    recommendation: "Výhru předávejte bez jakýchkoli nákladů pro zákazníka, nebo text o výhře odstraňte."
  - id: ucp_prize_win_claim
    title: "Oznámení výhry k ověření"
    logic:
      all: [{q: ucp_you_won, gte: 0.5}]
      none: [{q: ucp_prize_cost, gte: 0.5}]
    severity: medium
    checkability: verify
    legal_refs: # stejné jako ucp_false_prize_win (bod 31, CZ příloha č. 2 písm. h), SK agresívne bod 8)
    explanation: "Oznámení výhry je zakázané, pokud výhra neexistuje nebo za ni zákazník musí cokoli zaplatit."
    recommendation: "Ověřte, že výhra existuje a zákazník ji dostane bez jakýchkoli nákladů."
  ```
- **Závažnost:** high · text (s platbou); medium · verify (bez platby)
- **Poznámky:** Soudní dvůr: zakázané i tehdy, když jsou náklady minimální, například poštovní známka [Pokyny ř. 2682]; příklad „výrobek je zdarma“ s poplatkem 19,99 EUR porušuje body 31 i 20 [Pokyny ř. 2686]. Priorita B (vyskakovací okna, „kolo štěstí“).

## Nové informační povinnosti z EmpCo, které jde kontrolovat na webu

EmpCo mění i směrnici 2011/83/EU o právech spotřebitelů (čl. 2 EmpCo [EmpCo ř. 164–238]). Na Slovensku jsou povinnosti převzaté od 27. 9. 2026 (novela 310/2025, čl. I body 4–8 a 24, účinnost podle čl. V [SK-nov ř. 807–810]). V Česku převzaté nejsou; zda je obsahuje sněmovní tisk 53 nebo jiná novela občanského zákoníku: NEOVĚŘENO. Nejde o černou listinu, ale o chybějící informaci, proto patří do modulu legal nebo do sady jen pro SK.

| Povinnost | Směrnice 2011/83/EU po EmpCo | SK (od 27. 9. 2026) | Jak kontrolovat |
| --- | --- | --- | --- |
| Harmonizované oznámení o zákonné záruce (obrázek „ZÁKONNÁ ZÁRUKA“ s kódem QR na `europa.eu/youreurope/záruky_cs`, SK `…/záruky_sk`), na webu jako obecné upozornění | čl. 5 odst. 1 písm. e), čl. 6 odst. 1 písm. l), čl. 22a [EmpCo ř. 184, 210, 233]; podoba: nařízení 2025/1960, příloha I [1960 ř. 137–179] | § 5 ods. 1 písm. f) [SK ř. 1401–1404] | kód: obrázek nebo odkaz s `youreurope`, alt text „Zákonná záruka“ kdekoli na webu (`regex_site`). Oznámení nelze upravovat a online musí být barevné [1960 ř. 147, 179]; Jev obrázek nevidí |
| Harmonizovaný štítek „GARAN“: obchodní záruka výrobce na trvanlivost celého zboží nad 2 roky bez příplatku, pokud ji výrobce obchodníkovi zpřístupnil; online „přímo vedle obrázku zboží“ [EmpCo ř. 76] | čl. 5 odst. 1 písm. ea), čl. 6 odst. 1 písm. la), čl. 8 odst. 2 [EmpCo ř. 188, 214, 228] | § 5 ods. 1 písm. g) [SK ř. 1405–1409] | Jev: věta uvádí záruku výrobce delší než 2 roky; kód: slovo „GARAN“ na stránce (text, alt, název souboru) |
| Hodnocení opravitelnosti | čl. 5 odst. 1 písm. i), čl. 6 odst. 1 písm. u) [EmpCo ř. 198, 222] | § 5 ods. 1 písm. k) [SK ř. 1419–1420] | kód: produktové stránky telefonů a tabletů (od 20. 6. 2025 energetický štítek s hodnocením opravitelnosti [QA ř. 904–910]); štítek je obrázek, kontrola jen přes alt text nebo odkaz, nízká jistota |
| Informace o opravách (náhradní díly, cena, návody) | čl. 5 odst. 1 písm. j), čl. 6 odst. 1 písm. v) | § 5 ods. 1 písm. l) [SK ř. 1421–1425] | jen pokud je výrobce zpřístupnil → z webu nevynutitelné |
| Minimální doba aktualizací softwaru | čl. 5 odst. 1 písm. ed), čl. 6 odst. 1 písm. lc) | § 5 ods. 1 písm. p) [SK ř. 1437–1441] | jen pokud je výrobce zpřístupnil → nanejvýš „k ověření“ u zboží s digitálními prvky |
| Ekologické možnosti dodání | čl. 6 odst. 1 písm. g) [EmpCo ř. 206] | § 15 ods. 1 písm. l) [SK ř. 2072–2073] | „ak sú dostupné“ → nevynutitelné; tvrzení „ekologická doprava“ je ale environmentální tvrzení pro eco |

Návrh pro `rules/ucp_sk.yaml` (`jurisdictions: [sk]`), funguje s dnešním enginem:

- `ucp_producer_durability_guarantee`
  - EN: "Does the sentence (field sentence) state that the manufacturer or producer gives a guarantee (warranty) on the product for more than two years, for example '5-year manufacturer's warranty'? Answer no if no manufacturer's guarantee longer than two years is stated."
  - CS: „Uvádí věta (pole sentence), že výrobce poskytuje na produkt záruku delší než dva roky, například „záruka výrobce 5 let“? Pokud žádnou záruku výrobce delší než dva roky neuvádí, odpověz ne.“

```yaml
- id: sk_durability_label_missing
  title: "Záruka výrobce nad 2 roky bez štítku GARAN"
  logic:
    all: [{q: ucp_producer_durability_guarantee, gte: 0.5}]
  code_checks: [{type: allowlist_absent, list: garan_label, where: page}]   # garan_label: ["GARAN"]
  severity: medium
  checkability: verify
  legal_refs:
    - {jurisdiction: eu, ref: "Směrnice 2011/83/EU, čl. 6 odst. 1 písm. la) a čl. 22a, ve znění směrnice (EU) 2024/825; prováděcí nařízení (EU) 2025/1960", status: "ověřit"}
    - {jurisdiction: sk, ref: "Zákon č. 108/2024 Z. z., § 5 ods. 1 písm. g), účinnost od 27. 9. 2026", status: "ověřit"}
  explanation: "Pokud výrobce dává bezplatnou záruku na trvanlivost celého zboží delší než dva roky a obchodník o ní ví, musí u zboží zobrazit harmonizovaný štítek GARAN."
  recommendation: "Ověřte, zda jde o záruku výrobce na trvanlivost celého zboží; pokud ano, zobrazte štítek GARAN vedle obrázku produktu."
```

`allowlist_absent` s `where: page` hledá i v alt textech a názvech souborů a nález nechá jen na stránkách bez položky; to přesně odpovídá kontrole štítku. Harmonizované oznámení potřebuje novou kontrolu `regex_site` (celý web, ne jen právní stránky).

## Obecné zákazy (CZ § 4, 5, 5a, 5b; SK § 9–12) stručně

- **CZ § 4:** nekalá je praktika v rozporu s odbornou péčí, která podstatně narušuje nebo může narušit ekonomické chování spotřebitele [CZ ř. 153]; praktiky nekalé „za všech okolností“ jsou v přílohách č. 1 a 2 [CZ ř. 155]; zakázané před rozhodnutím o koupi, během něj i po něm [CZ ř. 156].
- **CZ § 5 (klamavé konání):** nepravdivá informace [CZ ř. 159], nebo pravdivá, ale klamavá ohledně výčtu v odst. 2 (dostupnost, výhody, rizika, složení, zkoušky, cena, nutnost opravy, práva spotřebitele…) [CZ ř. 160–167]; odst. 3: záměna, porušení kodexu, k jehož dodržování se prodávající zavázal, dvojí kvalita [CZ ř. 168–172]. **Do schválení tisku 53 je to jediný český základ pro eko tvrzení a body 10a, 23g–23j**, vždy případ od případu s testem rozhodnutí o koupi.
- **CZ § 5a (klamavé opomenutí):** chybějící nebo nejasně podaná podstatná informace a neuvedený obchodní záměr [CZ ř. 175–176]; odst. 3 podstatné informace u nabídky [CZ ř. 177–183]; odst. 4 řazení výsledků na tržišti [CZ ř. 184]; odst. 5 informace o ověřování recenzí [CZ ř. 185].
- **CZ § 5b (agresivní praktika):** obtěžování, donucování, nepatřičné ovlivňování [CZ ř. 190–196].
- **CZ § 5c (prokazování tvrzení):** ČOI může žádat, aby prodávající prokázal správnost skutkových tvrzení; když důkazy nepředloží nebo nestačí, považují se tvrzení za nesprávná [CZ ř. 199–200]. Pro nálezy „k ověření“ podstatné: důkazní břemeno nese e-shop.
- **SK:** § 9 obecně a odkaz na prílohu č. 1 [SK ř. 1573–1608]; § 10 klamlivé konanie, od 27. 9. 2026 výslovně i environmentálne a sociálne vlastnosti a obehovosť (ods. 1 písm. b) [SK ř. 1653–1655]), budoucí environmentální tvrzení (ods. 2 písm. d) [SK ř. 1697–1701]) a bezvýznamné výhody (ods. 2 písm. e) [SK ř. 1703–1704]); § 11 klamlivé opomenutie, ods. 6 písm. a) recenze, b) řazení, c) srovnávací služby [SK ř. 1753–1771]; § 12 agresívna praktika [SK ř. 1782–1810].

## Mimo černou listinu, ale blízko (kandidáti)

- **Sleva a předchozí cena** (CZ § 12a [CZ ř. 249–252]; SK § 7 [SK ř. 1541–1554]): kód nad historií cen, z textu nejde. U e-shopů velmi časté.
- **Budoucí environmentální tvrzení** (čl. 6 odst. 2 písm. d) směrnice 2005/29/ES ve znění EmpCo [EmpCo ř. 155]; SK § 10 ods. 2 písm. d)): případ od případu, vyžaduje veřejný prováděcí plán a nezávislého ověřovatele [QA ř. 722–752]. Otázka do eco:
  - `eco_future_claim`
    - EN: "Does the sentence (field sentence) state a future environmental goal or commitment, such as becoming climate neutral or reaching net zero by a certain year? Answer no if no future goal is stated."
    - CS: „Uvádí věta (pole sentence) budoucí environmentální cíl nebo závazek, například dosáhnout klimatické neutrality nebo nulových emisí do určitého roku? Pokud žádný budoucí cíl neuvádí, odpověz ne.“
  - pravidlo `eco_future_claim`: all `eco_future_claim`, medium, verify (odkaz SK § 10 ods. 2 písm. d); CZ § 5).
- **Bezvýznamné výhody** (čl. 6 odst. 2 písm. e) směrnice 2005/29/ES ve znění EmpCo [EmpCo ř. 157]; SK § 10 ods. 2 písm. e)): například balená voda „bez lepku“; ale „bez niklu“ u šperků relevantní je [QA ř. 700–714]. Jev by potřeboval vědět, co produkty dané kategorie běžně obsahují; až po kalibraci.
- **Srovnávací služby** (čl. 7 odst. 7 po EmpCo [EmpCo ř. 161]; SK § 11 ods. 6 písm. c)): jen pro porovnávače.

## Souhrnná tabulka

| id | body CZ / SK / EU | kontrola | priorita pro e-shop |
| --- | --- | --- | --- |
| ucp_code_signatory | CZ př. 1 a) / SK 1 / EU 1 | jev + ověřit | C |
| ucp_trust_mark | CZ př. 1 b) / SK 2 / EU 2 | jev + ověřit | B |
| eco_label_unrecognized | CZ — / SK 3 / EU 2a | jev + ověřit (+ kód), existuje | A |
| ucp_code_endorsed | CZ př. 1 a) / SK 4 (dříve 3) / EU 3 | jev + ověřit | C |
| ucp_approval_claim | CZ př. 1 c) / SK 5 (dříve 4) / EU 4 | jev + ověřit | B |
| eco_generic_claim | CZ — / SK 6 / EU 4a | jev (+ kód), existuje | A |
| eco_part_as_whole | CZ — / SK 7 / EU 4b | jev, existuje | B |
| eco_neutrality | CZ — / SK 8 / EU 4c | jev + ověřit, existuje | A |
| ucp_bait_advertising | CZ př. 1 d) / SK 9 (dříve 5) / EU 5 | kód | C |
| ucp_bait_and_switch | CZ př. 1 e) / SK 10 (dříve 6) / EU 6 | nelze z textu | – |
| ucp_false_urgency | CZ př. 1 f) / SK 11 (dříve 7) / EU 7 | jev + ověřit (+ kód) | A |
| ucp_aftersales_language | CZ př. 1 g) / SK 12 (dříve 8) / EU 8 | nelze z textu | – |
| ucp_legality_claim | CZ př. 1 h) / SK 13 (dříve 9) / EU 9 | jev + ověřit | B |
| ucp_legal_rights_as_advantage | CZ př. 1 i) / SK 14 (dříve 10) / EU 10 | jev | A |
| ucp_legal_requirement_as_feature | CZ — / SK 15 / EU 10a | jev + ověřit (+ kód) | B |
| ucp_advertorial | CZ př. 1 j) / SK 16 (dříve 11) / EU 11 | nelze z textu | – |
| ucp_search_ads_undisclosed | CZ př. 1 k) / SK 17 (dříve 12) / EU 11a | nelze z textu | – |
| ucp_safety_fear | CZ př. 1 l) / SK 18 (dříve 13) / EU 12 | jev + ověřit | C |
| ucp_manufacturer_confusion | CZ př. 1 m) / SK 19 (dříve 14) / EU 13 | jev + ověřit (slabé, + kód) | C |
| ucp_pyramid_scheme | CZ př. 1 n) / SK 20 (dříve 15) / EU 14 | jev + ověřit | C |
| ucp_closing_down | CZ př. 1 o) / SK 21 (dříve 16) / EU 15 | jev + ověřit (+ kód) | B |
| ucp_gambling_win | CZ př. 1 p) / SK 22 (dříve 17) / EU 16 | jev | C |
| ucp_cure_claim | CZ př. 1 q) / SK 23 (dříve 18) / EU 17 | jev + ověřit | A |
| ucp_market_conditions | CZ př. 1 r) / SK 24 (dříve 19) / EU 18 | jev + ověřit | B |
| ucp_prize_contest | CZ př. 1 s) / SK 25 (dříve 20) / EU 19 | jev + ověřit | C |
| ucp_free_claim | CZ př. 1 t) / SK 26 (dříve 21) / EU 20 | jev | A |
| ucp_invoice_in_marketing | CZ př. 1 u) / SK 27 (dříve 22) / EU 21 | nelze z textu | – |
| ucp_trader_as_consumer | CZ př. 1 v) / SK 28 (dříve 23) / EU 22 | jev + ověřit | C |
| ucp_service_other_state | CZ př. 1 w) / SK 29 (dříve 24) / EU 23 | jev + ověřit | C |
| ucp_ticket_bots | CZ př. 1 x) / SK 30 (dříve 25) / EU 23a | nelze z textu | – |
| ucp_reviews_verified_claim | CZ př. 1 y) / SK 31 (dříve 26) / EU 23b | jev + ověřit | A |
| ucp_reviews_fake_or_distorted | CZ př. 1 z) / SK 32 (dříve 27) / EU 23c | jev (jen přiznané znaky) | A |
| ucp_update_impact_withheld | CZ — / SK 33 / EU 23d | nelze z textu | – |
| ucp_update_presented_necessary | CZ — / SK 34 / EU 23e | nelze z textu | – |
| ucp_durability_limiting_feature | CZ — / SK 35 / EU 23f | nelze z textu | – |
| ucp_durability_claim | CZ — / SK 36 / EU 23g | jev + ověřit | B |
| ucp_repairability_claim | CZ — / SK 37 / EU 23h | jev + ověřit | B |
| ucp_consumables_early_replacement | CZ — / SK 38 / EU 23i | nelze z textu | – |
| ucp_non_original_parts_claim | CZ — / SK 39 / EU 23j | jev + ověřit (jen tvrzení) | B |
| ucp_cannot_leave_premises | CZ př. 2 a) / SK agr. 1 / EU 24 | nelze z textu | – |
| ucp_home_visits | CZ př. 2 b) / SK agr. 2 / EU 25 | nelze z textu | – |
| ucp_persistent_solicitation | CZ př. 2 c) / SK agr. 3 / EU 26 | nelze z textu | – |
| ucp_insurance_claim_obstruction | CZ př. 2 d) / SK agr. 4 / EU 27 | nelze z textu | – |
| ucp_children_direct_exhortation | CZ př. 2 e) / SK agr. 5 / EU 28 | jev | B |
| ucp_inertia_selling | CZ př. 2 f) / SK agr. 6 / EU 29 | nelze z textu | – |
| ucp_trader_livelihood_appeal | CZ př. 2 g) / SK agr. 7 / EU 30 | jev | C |
| ucp_false_prize_win | CZ př. 2 h) / SK agr. 8 / EU 31 | jev (s platbou) / jev + ověřit | B |

Součet: 47 bodů; jev 9, jev + ověřit 22, kód 1, nelze z textu 15. Priorita A 9 (z toho 3 existující eco), B 12 (z toho 1 existující eco), C 11, – 15.
