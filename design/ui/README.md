# Návrh UI EshopGuard

Schválený návrh obrazovek. Živé plátno: https://claude.ai/artifact/34wYLsJzieFtmWdueAwYay

Je to kopie plátna verze 31 z 2. 10. 2026 (3c, 3d, ceník a předplatné upravené na cenu za každou zemi, čeká na schválení). Platí pravidlo: **nejdřív návrh, pak kód**. Když se obrazovka na plátně změní, je potřeba zkopírovat i soubor sem.

- Každá obrazovka je jeden soubor `*.dc.html`.
  - Rozvržení a texty jsou v HTML.
  - Ukázková data a chování jsou ve třídě `Component` dole v souboru.
  - Soubory se samostatně nevykreslí, potřebují běhové prostředí plátna (`support.js`). Pro frontend slouží jako přesná předloha rozvržení, textů, barev a stavů.
- `canvas.json` obsahuje rozmístění a názvy obrazovek na plátně.
- Písma: Bricolage Grotesque (nadpisy) a Figtree (text).
- Hlavní barva: `#0E5A52`. Pozadí: `#F6F5F1`.
- `mimo-platno/` obsahuje starší obrazovky, které na plátně nejsou. Nestavět podle nich, pohled „Podľa nálezov“ se teprve navrhne.

| Soubor | Obrazovka |
|---|---|
| `Main.dc.html` | 1 · Úvodní stránka: vydání SK/CZ (přepínač, obsah z CMS), 100 stránek zdarma, napojení na e-shop, ceník |
| `Login.dc.html` | 2a · Přihlášení: odkaz v e-mailu (výchozí), heslem, Google; přepínač jazyka; nový účet vznikne kliknutím na odkaz |
| `LoginSent.dc.html` | 2b · Odkaz odeslán (vlevo náhled e-mailu, platnost 15 min, jednorázový) |
| `LoginConfirm.dc.html` | 2c · Po kliknutí na odkaz: potvrzení tlačítkem (odkaz nespotřebuje náhled pošty); vypršelý odkaz → „Poslať nový odkaz“ |
| `Onboarding.dc.html` | 3a · Připojení e-shopu (platforma rozpoznána) |
| `OnboardingOther.dc.html` | 3b · Připojení e-shopu (platforma nerozpoznána) |
| `OnboardingScope.dc.html` | 3c · Kde predávate (len podporované krajiny), jazykové verzie vetou, rozsah, cena a spustenie |
| `VersionDetails.dc.html` | 3d · Jazykové verze z ukázky (jazyk popisů, produkty do ceny za každou zemi, popisy v jiném jazyce) |
| `Dashboard.dc.html` | 4 · Přehled |
| `Fixes.dc.html` | 5 · Opravy (po stránkách, filtr jazykové verze, hromadné opravy, šablona) |
| `Review.dc.html` | 6a · Oprava stránky (verdikt po zemích SK/CZ, text v kontextu, hromadná změna, co pomůže, zkratky) |
| `ReviewQuestion.dc.html` | 6b · Oprava stránky po odpovědi na otázku |
| `GroupFix.dc.html` | 6c · Hromadná oprava (stejný text na 38 stránkách) |
| `Monitoring.dc.html` | 7 · Sledování změn (i kontrola při uložení) |
| `Billing.dc.html` | 8 · Předplatné a platby (po e-shopech; stav po půl roce, 3 e-shopy; faktury s vlastním posuvníkem) |
| `BillingDetails.dc.html` | 8b · Fakturační údaje v Nastavenia (firma, sídlo, e-mail pro faktury; stav ověření IČ DPH; doplnění před první platbou). **Návrh čeká na schválení, na živém plátně zatím není.** Varianty `state`: `verified`, `pending` (česká firma, pole DIČ je její DIČ pro DPH), `invalid`, `missing`. |
| `Evidence.dc.html` | 10 · Doklady (zadané jednou, platí všude) |
| `Protocol.dc.html` | 11 · Protokol o kontrole (PDF) |
| `Mobile.dc.html` | 9 · Přehled na mobilu |
| `Sidebar.dc.html` | Sdílené: boční menu |
| `AccountMenu.dc.html` | Sdílené: menu účtu po kliknutí na jméno (předplatné, nastavení, jazyk) |
| `BillingMobile.dc.html` | 9b · Předplatné a platby na mobilu (faktury rozbalením, bez vnořeného posuvníku) |
