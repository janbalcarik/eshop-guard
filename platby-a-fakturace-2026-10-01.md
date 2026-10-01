# Platby a fakturace (slovenská s.r.o., B2B SaaS)

Rešerše z 1. 10. 2026 z oficiálních zdrojů. **Nejde o daňové poradenství**, daňové body potvrdí účetní (seznam na konci). Navazuje na ceník ve [strategie-a-cenik-2026-09-30.md](strategie-a-cenik-2026-09-30.md) a na [architektura-multitenant-worker-2026-10-01.md](architektura-multitenant-worker-2026-10-01.md).

## Doporučení v kostce

**Upřesnění uživatele 1. 10. 2026:** platit se má **přímo v aplikaci jako v běžném e-shopu**: tlačítko „Zaplatiť“ → platební brána → návrat do aplikace. Ne přes fakturu poslanou e-mailem.

SuperFaktúra takovou platbu nespustí. Její online platby začínají až z faktury (tlačítko na faktuře nebo v e-mailu) a API žádný platební odkaz ani návrat do aplikace nedokumentuje. Proto:

- **Platbu v aplikaci obstará platební brána přímo:**
  1. přesměrování na její platební stránku a návrat zpět;
  2. brána pošle serveru potvrzení o zaplacení;
  3. náš backend pak v SuperFaktúře vystaví fakturu a zapíše k ní platbu přes API `Invoice\Payment::create` s typem `CARD`;
  4. SuperFaktúra fakturu pošle, od 2027 jako e-faktúru přes Peppol.

  Takhle e-shopy se SuperFaktúrou běžně fungují.
- **Doporučená brána:** Stripe Checkout (varianta B). Integrace je nejjednodušší a automatické měsíční strhávání karty je v ní hotové.
- **Levnější slovenská alternativa:** Besteron nebo Barion přímo (0,89–0,99 %). Jednorázová platba je snadná, opakované strhávání karty ale programujeme sami.

Varianta A (jen SuperFaktúra) tedy platí jen pro platbu fakturou, třeba roční předplatné převodem.


1. **Platby a předplatné přes Stripe** (Checkout, Billing a zákaznický portál). Stripe pokrývá:
   - karty, Apple Pay a Google Pay;
   - SEPA inkaso a převod na virtuální IBAN;
   - změnu tarifu s poměrným přepočtem;
   - opakování neúspěšných plateb;
   - portál, kde si zákazník sám změní kartu nebo tarif a zruší předplatné;
   - webhooky pro náš backend.

   Slovenské firmy podporuje a výplaty jdou v EUR na slovenský účet.
2. **Daňové doklady vystavuje fakturační systém, ne Stripe**, nejspíš SuperFaktúra přes API. Rozhoduje to **slovenská povinná e-faktúra od 1. 1. 2027** (zákon 385/2025 Z. z.):
   - týká se všech tuzemských B2B faktur od plátce DPH, i k platbě předem kartou;
   - faktura musí být ve formátu XML podle EN 16931;
   - musí se poslat přes certifikovaného doručovatele (síť Peppol), a to do 15 dní.

   Stripe takový soubor neumí sám vytvořit ani odeslat. SuperFaktúra je uvedená jako zprostředkovatel doručení přes Peppol.
3. **Merchant of Record nedoporučuji.** Merchant of Record znamená, že prodejcem je zprostředkovatel a DPH řeší za nás (Paddle, Lemon Squeezy, Stripe Managed Payments). Pro B2B v SK/CZ je zhruba 2–3× dražší a u tarifu 9 € má Paddle jen individuální cenu. Smysl by měl až při velkém podílu prodeje spotřebitelům v cizině.
4. **Slovenské a české brány** (Comgate, GoPay, TrustPay, Besteron, Barion) mají nižší poplatky za kartu. Umí ale jen uložit kartu a strhnout částku. Tarify, poměrný přepočet, opakování plateb, portál i faktury bychom programovali sami, a to je víc práce, než kolik poplatky ušetří.

## Varianta A: jen SuperFaktúra (platba až z faktury, ne v aplikaci)

**Co SuperFaktúra umí** (ověřeno na superfaktura.sk 1. 10. 2026):
- **Online platba faktury:** tlačítko „Zaplatiť online“ na faktuře i v e-mailu. Brány a poplatky:
  - Barion: karta 0,99 % za daných podmínek, okamžitý převod 0,3 %;
  - Besteron: 0,89 % + 0,10 €, nebo pevný program 0,89–2,59 € za platbu;
  - PayPal: 1,9–3,4 % + ~0,37 €.
- **Pravidelné faktury:** automatické vystavení a odeslání týdně až ročně. Odeslání e-mailem je v tarifech Standard a Premium.
- **Automatické párování plateb** z bankovního účtu.
- **API s `callback_payment`:** adresa, kterou SuperFaktúra zavolá, když k faktuře přibude platba (automaticky nebo ručně, podle nastavení automatických zpráv). Náš backend se tak hned dozví, že je zaplaceno.
- **E-faktúra přes Peppol** (SuperFaktúra je zprostředkovatel doručení).

**Jak by to fungovalo:**
- **Úvodní analýza:**
  1. Backend přes API vystaví zálohovou fakturu s `callback_payment`.
  2. Zákazník zaplatí kartou nebo tlačítkem banky.
  3. Přijde callback a běh se posune ze stavu „Čeká na schválení a platbu“ dál.
- **Sledování:**
  - pravidelná faktura měsíčně nebo ročně (roční výhodněji);
  - zaplaceno → období prodlouženo;
  - nezaplaceno do splatnosti → upomínka, po lhůtě se sledování pozastaví.
- **Změna tarifu** platí od dalšího období, bez poměrného přepočtu. Je to jednodušší a pro B2B běžné.

**Výhody:**
- jeden slovenský systém pro platby, faktury i e-faktúru, žádné dvojí doklady;
- nejnižší poplatky (Barion a Besteron pod 1 %);
- nejméně programování.

**Nevýhody:**
- **Kartu nejde automaticky strhávat.** Každé období zákazník platí sám tlačítkem nebo převodem. U měsíčních 9–59 € to znamená víc upomínek a pozdních plateb. Proto výchozí roční platba.
- Samoobslužnou změnu tarifu stavíme v naší aplikaci (jednoduchá, bez přepočtu).
- Neověřeno:
  - zda SuperFaktúra po zaplacení zálohové faktury sama vystaví daňový doklad k přijaté platbě;
  - jak přesně funguje callback u plateb přes bránu.

  Obojí ověřit v jejich API dokumentaci nebo podpoře před stavbou.

## Varianta B: platba v aplikaci přes Stripe Checkout + faktury v SuperFaktúře (doporučeno)

Stripe strhává kartu automaticky každý měsíc. Umí poměrný přepočet, opakování neúspěšných plateb a portál zákazníka. SuperFaktúra dál vystavuje daňové doklady a e-faktúry (podrobnosti níže). Vyplatí se, až bude hodně měsíčních předplatných nebo zahraniční zákazníci platící kartou.

## Srovnání

| Varianta | Poplatky | Předplatné | Faktury | Práce s napojením |
|---|---|---|---|---|
| **Stripe** | karta z EHP 1,5 % + 0,25 € (prémiové karty 2,8 % + 0,25 €); SEPA inkaso 0,35 €; Billing 0,7 % z opakovaných plateb; Tax 0,5 % | tarify, poměrný přepočet, opakování plateb, portál | slovensky, IČ DPH obou stran, ověření ve VIES, text o přenesení daňové povinnosti; **Peppol ne** | nízká až střední |
| Stripe Managed Payments (MoR) | 3,5 % + zpracování + Billing | ano | prodává „Link“; B2B přenesení daňové povinnosti nezdokumentováno | nízká |
| Paddle (MoR) | 5 % + 0,50 USD; pod 10 USD individuálně | ano | fakturuje Paddle | nízká |
| Comgate | 0,67 % + 1 Kč (Profi) | jen uložená karta, zbytek sami | ne | vysoká |
| GoPay | 0,95 % | pevná částka nebo na vyžádání | ne | vysoká |
| TrustPay / finby | 0,99 % + 0,20 € (neověřeno) | uložená karta | ne | vysoká |
| Barion | 1,19–1,49 % + 0,2 % za opakované platby | uložená karta | ne | vysoká |

**Příklad:** tarif 29 € + 23 % DPH = 35,67 €. Poplatky zhruba:
- Stripe 1,21 € (karta 0,79 €, Billing 0,25 €, Tax 0,18 €);
- Managed Payments 2,28 €;
- Paddle 1,78 € + 0,50 USD.

## Jak to zapojit

| Co | Jak ve Stripe |
|---|---|
| Úvodní analýza (39 / 69 / 99 / 199 € za e-shop) | Checkout v režimu jednorázové platby. Po potvrzení platby (webhook) se běh posune ze stavu „Čeká na schválení a platbu“ dál. |
| Sledování (9 / 19 / 29 / 59 € měsíčně **za každý e-shop** podle velikosti, i ročně; změněno 1. 10. 2026) | 4 pevné ceny pro měsíc a 4 pro rok, žádné účtování podle spotřeby. Portál totiž neumí měnit předplatné účtované podle spotřeby a to se nepřepočítává poměrně. Překročení počtu stránek = nabídka vyššího tarifu. |
| Roční předplatné na fakturu | Přes API (převod na virtuální IBAN nebo SEPA inkaso). Checkout v režimu předplatného převod neumí. |
| Zákaznický portál | Karta, tarif, zrušení. Faktury ukazuje naše aplikace z fakturačního systému. Zda jde v portálu vypnout seznam faktur Stripe, je neověřeno. |
| DIČ zákazníka | Stripe Tax a ověření ve VIES. Pozor: přenesení daňové povinnosti použije Stripe už podle tvaru DIČ, ne podle výsledku ověření. Backend proto sleduje webhook `customer.tax_id.updated` a neověřené DIČ řeší. |
| Daňový doklad | Po webhooku `invoice.paid` (a po jednorázové platbě) backend založí doklad přes API SuperFaktúry. Slovenské B2B faktury jdou přes Peppol, ostatní jako PDF. Jedna číselná řada, žádné dvojí doklady. |
| Dobropisy | Při refundaci nebo snížení tarifu backend vystaví opravný doklad stejnou cestou. |

V architektuře to obsluhuje backend API: tabulky předplatného a plateb po tenantech, příjem webhooků Stripe se stejnou deduplikací jako u konektorů a úloha „vystavit doklad“ ve frontě (opakuje se při výpadku fakturačního systému). Klíče Stripe a SuperFaktúry jsou jen v `.env` na serveru.

## Ukázka zdarma → úvodní analýza → sledování (návrh 1. 10. 2026)

1. **Ukázka zdarma:** po přidání e-shopu se zkontroluje 100 stránek. Bez karty.
2. **Celý e-shop + sledování jednou platbou** (doporučeno, odpovídá ceníku „analýza vč. 30 dní sledování“):
   - Stripe Checkout v režimu předplatného obsahuje dvě položky:
     - jednorázová položka „Úvodná analýza celého e-shopu“, zaplatí se hned;
     - předplatné sledování s 30denní zkušební dobou zdarma.
   - Karta se zadá a ověří (3-D Secure) jen jednou a Stripe ji uloží.
   - Po 30 dnech Stripe strhne sledování sám jako platbu iniciovanou obchodníkem, zákazník ji znovu neověřuje. Kdyby banka ověření přece chtěla, Stripe pošle zákazníkovi odkaz.
   - Na stránce platby musí být jasně uvedeno, že po 30 dnech se platí tarif měsíčně a že jde kdykoli zrušit. Týden před koncem zkušební doby přijde připomenutí.
   - Kdo sledování zruší do konce zkušební doby, zaplatí jen analýzu.
3. **Varianta „sledování zapnu později“:**
   - Checkout jen na analýzu (režim platby s `setup_future_usage=off_session`), karta se uloží.
   - Tlačítko „Zapnúť sledovanie“ později založí předplatné na uloženou kartu bez nového zadávání karty.
4. **Další e-shop v účtu:**
   - sledování je za účet, takže se jen zvýší tarif podle počtu stránek;
   - analýza dalšího e-shopu je nová jednorázová platba;
   - Checkout uloženou kartu znovu nenabídne, proto vlastní tlačítko „Zaplatiť kartou •••• 4242“, které ji použije (případně s ověřením 3-D Secure).
5. **Faktury:**
   - za analýzu hned po platbě;
   - během zkušební doby žádná;
   - pak za každé zaplacené období (oddíl níže).

Zdroje: https://docs.stripe.com/payments/checkout/free-trials, https://docs.stripe.com/billing/subscriptions/trials/free-trials (jednorázová položka se zaplatí hned i během zkušební doby), https://docs.stripe.com/payments/checkout/save-during-payment (uložení karty, platby iniciované obchodníkem; uložené karty se v Checkoutu znovu nenabízejí).

## Opakovaná platba a fakturace předplatného (návrh 1. 10. 2026)

**Změna 1. 10. 2026:** sledování se platí **po e-shopech**. Každý e-shop má ve Stripe vlastní předplatné:
- jednorázová položka analýzy + předplatné s 30denní zkušební dobou, takže fakturační den = den analýzy;
- karta je jedna pro celý účet;
- sleva od 3. e-shopu jako kupón na předplatné 3. a dalšího e-shopu;
- faktura za sledování je za každý e-shop zvlášť, takže ji agentury můžou rovnou přeúčtovat klientovi.



**Každé prodloužení:**
1. Stripe na začátku období sám strhne kartu (měsíčně nebo ročně).
2. Po úspěchu pošle webhook `invoice.paid`. Faktura Stripe je jen interní záznam, zákazníkovi se neposílá.
3. Backend založí fakturu v SuperFaktúře:
   - položka „Sledovanie zmien, tarif …, obdobie 1. 11.–30. 11. 2026“;
   - DPH podle zákazníka;
   - zapsaná platba kartou s datem přijetí platby.
4. SuperFaktúra fakturu odešle (SK firmám e-faktúru přes Peppol, ostatním PDF).
5. Klíčem je ID faktury Stripe, takže i dvakrát doručený webhook dá jen jednu fakturu.

**Neúspěšná platba:**
- Stripe platbu opakuje a pošle zákazníkovi výzvu k aktualizaci karty. Aplikace ukáže upozornění.
- Daňový doklad nevzniká, dokud není zaplaceno.
- Po posledním neúspěšném pokusu se předplatné ukončí a sledování se v aplikaci pozastaví. Data zůstanou.

**Změna tarifu, aby nebylo potřeba dobropisů:**
- **Zvýšení:** hned, doplatek za zbytek období se strhne okamžitě (Stripe `always_invoice`). Na doplatek vznikne samostatná faktura.
- **Snížení a zrušení:** až od dalšího období, bez vracení peněz.
- Vrácení peněz jen výjimečně a ručně. Tehdy backend vystaví dobropis v SuperFaktúře přes API.

**Roční platba převodem** pro firmy, které nechtějí kartu:
- místo Stripe pravidelná zálohová faktura v SuperFaktúře s tlačítkem k platbě a převodem;
- po zaplacení callback `callback_payment` prodlouží období v aplikaci;
- SuperFaktúra vystaví daňový doklad.

**Ověřit s účetní:** datum zdanitelného plnění a 15denní lhůta e-faktury u platby předem za období (otevřený bod 3).

## Ceny v databázi a jejich změna (návrh 1. 10. 2026)

**Požadavek uživatele:** ceny a slevy za více e-shopů jsou v databázi, během roku se můžou měnit a platí vždy nově zveřejněný ceník. Změna ceny musí proběhnout automaticky.

**Co umí Stripe a co musíme udělat my:**
- Cenu ve Stripe (objekt Price) nejde změnit. Nová cena = nový objekt Price. Dosavadní předplatná zůstávají na staré ceně, dokud je náš backend nepřevede (https://docs.stripe.com/billing/subscriptions/change-price).
- Převod umí Stripe dvěma způsoby:
  - výměna ceny u položky předplatného bez poměrného přepočtu, takže platí od příští platby;
  - plán předplatného (Subscription Schedule), který cenu přepne na konci období.
- `lookup_key` s `transfer_lookup_key` přesune „aktuální cenu pásma“ na nový objekt Price, takže nové objednávky hned berou novou cenu (https://docs.stripe.com/products-prices/manage-prices).
- Slevy za více e-shopů = kupóny (procentní sleva) na předplatném.
- Stripe zákazníkovi změnu ceny sám neoznámí, upozornění posíláme my.

**Datový model u nás (zdroj pravdy):**
- `price_lists`: verze ceníku s `valid_from` a `published_at`.
- `price_tiers`: pásmo podle počtu produktů, cena analýzy, cena sledování, ID objektů Price ve Stripe.
- `volume_discounts`: od kolikátého e-shopu, procento, ID kupónu ve Stripe.
- U předplatného e-shopu: podle které verze ceníku, jakého pásma a jaké slevy se aktuálně platí.

**Postup při zveřejnění nového ceníku:**
1. Administrátor zveřejní novou verzi.
2. Backend vytvoří ve Stripe nové objekty Price a kupóny a přesune na ně `lookup_key`. Nové objednávky (analýza i sledování) platí novou cenu okamžitě.
3. Úloha ve frontě projde běžící předplatná a každému naplánuje novou cenu od první platby po výpovědní lhůtě (návrh 30 dní). Zákazník dostane e-mail se starou a novou cenou a datem. Na obrazovce Predplatné a platby uvidí „Nová cena od …“.
4. Stejným mechanismem běží:
   - přechod do vyššího pásma, když e-shopu přibudou produkty (od dalšího období);
   - přepočet slevy, když zákazník přidá nebo zruší e-shop (od dalšího období).
5. Každá změna se zapíše do auditu, faktura v SuperFaktúře nese skutečně účtovanou cenu.

**K rozhodnutí a do obchodních podmínek:**
- Má nový ceník platit pro stávající zákazníky hned, nebo po výpovědní lhůtě s možností zrušit? Doporučení: po lhůtě. Okamžité zdražení bez upozornění vede ke stížnostem a vráceným platbám.
- Délku lhůty a právo zrušit předplatné před změnou uvést v obchodních podmínkách. Ověří právník.
- U zlevnění: od příští platby, bez lhůty.

## E-faktúra na Slovensku (od 1. 1. 2027)

- Vystavují ji všichni plátci DPH za tuzemské B2B a B2G plnění. Přijímat ji musí každý podnikatel, i neplátce DPH.
- Prodej spotřebitelům (B2C) je vyňatý. Přeshraniční plnění přijdou na řadu až od 1. 7. 2030.
- Vystavení a odeslání do 15 dní, datum vystavení se musí shodovat s datem odeslání.
- Údaje finanční správě hlásí doručovatel. Pokuta až 10 000 €, při opakování až 100 000 €.
- Seznam certifikovaných doručovatelů: vpds.financnasprava.sk.
- V Česku povinnost zatím není. Přeshraniční e-fakturace přijde od poloviny roku 2030, tuzemská se zvažuje.

## Otevřené body pro účetní

1. Firmy bez DIČ (v ČR „identifikovaná osoba“ podle § 6h): přenesení daňové povinnosti, slovenská DPH, nebo OSS?
2. Je předplatné „elektronicky poskytovaná služba“? Je potřeba registrace do OSS?
3. Faktura k platbě kartou předem za každé období předplatného a dodržení 15denní lhůty pro e-faktúru.
4. Dobropisy a refundace při poměrném přepočtu, i jako e-faktúra.
5. Stačí jedna číselná řada ze SuperFaktúry a Stripe dokumenty jen jako interní záznam?
6. Jak Stripe ve slovenštině formuluje text o přenesení daňové povinnosti a zda faktura obsahuje datum dodání (neověřeno).
7. DPH z poplatků, které účtuje Stripe Technology Europe.
8. Týká se zákon 384/2025 Z. z. o evidenci tržeb online plateb kartou? (neověřeno)

## Zdroje

- Stripe:
  - https://stripe.com/en-sk/pricing, https://stripe.com/en-sk/billing/pricing, https://stripe.com/en-sk/tax/pricing;
  - https://docs.stripe.com/payments/sepa-debit, …/payments/bank-transfers, …/payouts, …/invoicing/customize, …/billing/customer/tax-ids, …/tax/supported-countries/european-union, …/customer-management, …/billing/subscriptions/prorations, …/invoicing/e-invoicing;
  - https://support.stripe.com/questions/managed-payments-pricing.
- Merchant of Record: https://www.paddle.com/pricing, https://www.paddle.com/help/sell/tax/how-paddle-handles-vat-on-your-behalf, https://www.lemonsqueezy.com/pricing.
- Brány:
  - https://www.comgate.eu/online-payments-pricing, https://help.comgate.cz/docs/en/recurring-payments;
  - https://www.gopay.com/en/pricing/;
  - https://doc.trustpay.eu/;
  - ceník Barion od 17. 1. 2026;
  - https://www.besteron.com/media/spphrndj/sazobnik_besteron.pdf.
- Fakturace: https://www.superfaktura.sk/efaktura/, https://github.com/superfaktura/apiclient, https://www.fakturoid.cz/api, https://developer.idoklad.cz/.
- E-faktúra:
  - https://www.financnasprava.sk/_img/pfsedit/Dokumenty_PFS/Podnikatelia/Dan_z_pridanej_hodnoty/efaktura/2026/2026.09.14_eFak_FaQ.pdf;
  - https://vpds.financnasprava.sk/;
  - metodický pokyn k fakturaci (§ 74), https://www.financnasprava.sk/sk/podnikatelia/dane/dan-z-pridanej-hodnoty/sadzby-dane.
