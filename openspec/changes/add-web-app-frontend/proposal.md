# Proposal: Webová aplikace EshopGuard podle návrhu UI (Next.js)

## Intent

**Problém.** Po změnách 9–12 umí API přihlásit uživatele, založit e-shop, spočítat cenu, přijmout platbu a vrátit nálezy, opravy, doklady a faktury. Zákazník to ale nemá kde vidět. Dnes existuje jen CLI a návrh UI na plátně (`design/ui/*.dc.html`, popis v `design/ui/README.md`). Prodejní tok „ukázka zdarma → zaplacená analýza → schválené opravy“ tak nejde projít.

**Proč teď.** Fáze 3 „Web MVP“ v architektuře (část 10) končí, když projde „prodejní tok od bezplatné kontroly po zaplacenou analýzu“. F7 v plánu implementace (databáze, část 8) to upřesňuje: „Proklikání celého toku na lokálním prostředí slovensky i česky, mobilní šířka bez vodorovného posunu“. Bez frontendu nejde začít pilot (strategie, část 5: říjen–listopad 2026).

**Přínos.**
- Zákazník projde celý tok sám, bez CLI:
  1. přihlášení odkazem v e-mailu;
  2. připojení e-shopu;
  3. potvrzení míst prodeje s důvody;
  4. cena přepočítaná hned při každé změně;
  5. platba na stránce Stripe;
  6. živý průběh analýzy;
  7. schválení oprav po stránkách i hromadně;
  8. publikování do e-shopu;
  9. doklady, protokol a faktury.
- Obrazovky odpovídají schválenému návrhu (barvy, písma Bricolage Grotesque a Figtree, komponenty, texty), na počítači i na mobilu.
- Texty jsou v jazyce uživatele (sk, cs). Čísla, data a měny se formátují podle jazyka.
- Fail-closed:
  - cenu nikdy nepočítá prohlížeč;
  - chyba API se nikdy netváří jako úspěch;
  - nezkontrolované stránky se neschovávají.
- Konverzi z ukázky na placenou analýzu zatím nejde změřit (neměřeno), měří se od pilotu.

**Fáze:** F7 (databaze-a-plan-implementace-2026-10-01.md, část 8).

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 2 (Frontend: „k datům zákazníků přistupuje jen přes API“);
  - část 3 (izolace, `/api/t/{tenantId}/...`, role);
  - část 6 (stavy běhu, „Čeká na schválení“, SSE přes `LISTEN/NOTIFY`, odhad doby);
  - část 12 (Místa prodeje, Jazykové verze, Aplikace: texty, `Intl`, volba jazyka, API vrací kódy);
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 3 (tabulky, které obrazovky čtou: `findings`, `fix_proposals`, `fix_groups`, `questions`, `evidence_items`, `subscriptions`, `invoices`, `notifications`, `page_changes`);
  - část 7 (`web/`, `messages/sk.json`, `messages/cs.json`);
  - F7;
  - část 9, bod 1 (pravidla odkazu v e-mailu);
- `platby-a-fakturace-2026-10-01.md`: Ukázka zdarma → úvodní analýza → sledování, Opakovaná platba;
- návrh UI (plátno „EshopGuard – návrh webu“, `canvas.json`):
  - 2a `Login`, 2b `LoginSent`, 2c `LoginConfirm`;
  - 3a `Onboarding`, 3b `OnboardingOther`, 3c `OnboardingScope`, 3d `VersionDetails`;
  - 4 `Dashboard`, 5 `Fixes`;
  - 6a `Review`, 6b `ReviewQuestion`, 6c `GroupFix`;
  - 7 `Monitoring`, 8 `Billing`, 9 `Mobile`, 9b `BillingMobile`;
  - 10 `Evidence`, 11 `Protocol`;
  - sdílené: `Sidebar`, `AccountMenu`.

## Scope

In scope:
- **Kostra `web/`** (Next.js App Router, TypeScript strict, React 19):
  - skupina tras `(app)` s vlastním kořenovým layoutem pod `/app`;
  - návrhové tokeny z plátna;
  - písma přes `next/font` (bez volání Google za běhu);
  - Tailwind CSS 4, primitiva Radix UI pro přístupné dialogy, menu, záložky a přepínače.
- **API klient generovaný z OpenAPI:**
  - `openapi-typescript` + `openapi-fetch`;
  - snímek `web/openapi/eshopguard-api.json`;
  - kontrola v CI, že vygenerovaný klient odpovídá snímku;
  - chyby ProblemDetails převedené na kódy a texty.
- **Přihlášení 2a–2c:**
  - odkaz v e-mailu jako výchozí volba, heslo, Google;
  - přepínač jazyka před přihlášením;
  - odpočet „Poslať znova“;
  - potvrzení odkazu tlačítkem;
  - vypršelý odkaz → „Poslať nový odkaz“.
- **Onboarding 3a–3d:**
  - rozpoznání platformy a volba připojení;
  - klíče konektoru (3b);
  - „Kde predávate“ s důvodem u každé země;
  - jazykové verze jednou větou a podrobnosti na 3d;
  - rozsah a moduly;
  - **cena přepočítaná serverem hned při každé změně zaškrtnutí**;
  - objednávka, přechod na Stripe Checkout a návrat;
  - pro další e-shop platba uloženou kartou (dodává změna 12).
- **Živý průběh běhů** přes SSE (ukázka zdarma, úvodní analýza, publikování) s náhradním dotazováním.
- **Přehled (4) a mobilní přehled (9):**
  - souhrn nálezů ve třech skupinách;
  - „Stránky, ktoré riešiť najskôr“, „Rýchle odpovede“, souhrn sledování;
  - stažení protokolu.
- **Opravy (5):**
  - zobrazení po stránkách a po nálezech;
  - filtr jazykové verze;
  - záložky stavů s počty;
  - pruh hromadných oprav;
  - položka šablony.
- **Oprava stránky (6a, 6b):**
  - tři zobrazení změn (Vedľa seba, Zmeny v texte, Celý nový text);
  - verdikt po zemích;
  - alternativy, doplnění údaje, otázky a odpovědi;
  - „Prečo meniť“, „Čo pomôže“, odkazy na zákon po zemích, výsledek nové kontroly;
  - klávesové zkratky A, U, J, K;
  - spodní lišta s publikováním a „Kopírovať text“.
- **Hromadná oprava (6c):**
  - doplnění údaje a tři způsoby opravy;
  - ukázky na stránkách;
  - kontrola na každé stránce a výběr stránek.
- **Další obrazovky:**
  - Sledování změn (7): karty, přepínače upozornění, tabulka „Posledné zmeny“;
  - Doklady (10);
  - stažení protokolu PDF (11), který skládá backend;
  - Předplatné a platby (8, 9b): e-shopy, karta, pásma, faktury s vlastním posuvníkem, filtr, ZIP, zrušení sledování; na mobilu bez vnořeného posuvníku.
- **Sdílené prvky:**
  - boční menu s přepínačem e-shopu;
  - menu účtu (Predplatné a platby, Nastavenia, Jazyk, Odhlásiť sa);
  - mobilní hlavička a spodní navigace.
- **Přístupnost:**
  - vše ovladatelné klávesnicí, viditelný fokus;
  - kontrast podle WCAG 2.2 AA;
  - cíle ≥ 44 px na mobilu;
  - automatická kontrola axe na každé obrazovce.
- **Testy:**
  - Vitest a Testing Library pro komponenty;
  - Playwright lokálně proti modelovému API (sk i cs, 1440 × 960 a 390 × 844);
  - snímkové testy obrazovek.

Out of scope:
- prezentační web, Payload CMS a směrování vydání `/sk`, `/cz` (změna 14);
- **jazykové základy** (next-intl, `messages/*.json`, ICU, formátování, test úplnosti), které definuje a staví změna 14 v požadavcích „Texty rozhraní:“ (skupina 1 jejích úkolů). Tato změna je používá a doplňuje do nich klíče svých obrazovek;
- koncové body API a jejich logika (změny 9, 10, 11, 12, 16) a skládání protokolu PDF a e-mailů (backend);
- konektory platforem a jejich OAuth (změna 15). Do té doby obrazovka 3a nabízí jen volby, které API vrátí jako dostupné;
- data sledování změn (změna 16). Do té doby obrazovka 7 ukáže stav „Sledovanie ešte nebeží“;
- produkční obrazy Dockeru, Caddy a nasazení (změna 17);
- obrazovky, které na plátně chybějí (seznam v K rozhodnutí, bod 1). Postaví se až po doplnění návrhu a jeho schválení (skupina 1 v `tasks.md`).

## Approach

1. **Nejdřív doplnit a schválit chybějící návrhy UI, pak kód** (zásada uživatele). Každá obrazovka se staví podle svého `.dc.html`: rozměry, barvy, typografie a texty se přebírají přesně. Odchylka od návrhu jde jen přes úpravu plátna a schválení.
2. **Jedna aplikace Next.js, tři kořenové layouty** v route groups:
   - `(app)` pro aplikaci pod `/app`;
   - `(site)` pro prezentační web (změna 14);
   - `(payload)` pro administraci CMS (změna 14).

   Styly aplikace se nemíchají se styly administrace Payload.
3. **API je na stejném původu pod `/api`:**
   - Caddy (změna 17) ho směruje na ASP.NET Core, ve vývoji to dělá přesměrování v `next.config.ts`;
   - přihlášení drží jen cookie `HttpOnly` vydaná API, JavaScript žádný token nevidí;
   - serverové komponenty volají API po vnitřní síti (`API_INTERNAL_URL`) a předávají cookie uživatele.
4. **Klient se generuje z OpenAPI.** Typy jsou jediný zdroj pravdy o tvaru dat. Názvy operací v `design.md` jsou předpoklad a závazné jsou ty ze snímku OpenAPI změn 9–12 a 16.
5. **Cenu i všechny částky počítá jen server.** Obrazovka 3c při každé změně míst prodeje nebo modulů zavolá nabídku ceny (`billing.price_quotes`, změna 12) a zobrazí její výsledek. Platba se zakládá s ID nabídky. Během přepočtu a po chybě je tlačítko platby neaktivní.
6. **Bez optimistických změn u akcí s následky** (publikování, platba, zrušení sledování, odpověď na otázku). Stav po akci se ukáže až z odpovědi API. Chyba se ukáže kódem přeloženým do jazyka uživatele, nikdy tiše.
7. **Texty jen přes klíče zpráv** ze změny 14. Texty nálezů, otázek a odkazy na zákon se skládají z katalogu textů pravidel, který vrací API v jazyce uživatele (změna 6). Frontend žádný paragraf nevymýšlí.
8. **Testy proti modelovému API:**
   - Playwright běží lokálně proti modelovému serveru (`@mswjs/http-middleware`) s daty „bylinkovo.sk“ z návrhu;
   - placený Jev ani OpenAI se v testech nevolá;
   - celý tok s reálným backendem je zvláštní úkol ověření se stuby, reálné služby jen s odhadem ceny a souhlasem.

## Dependencies

- **9 `add-identity-and-tenants-api`:**
  - přihlášení odkazem, heslem a Google;
  - `users.locale`, členství a role;
  - založení tenanta u nového účtu (předpoklad, K rozhodnutí bod 15);
  - `notifications`, `notification_settings`.
- **10 `add-shops-and-onboarding-api`:**
  - rozpoznání platformy, založení e-shopu, ukázka zdarma;
  - místa prodeje s důkazy, jazykové verze, moduly.
- **11 `add-findings-and-fixes-api`:**
  - přehled, opravy po stránkách a po nálezech, revize stránky;
  - rozhodnutí o návrzích, otázky, hromadné opravy, publikování;
  - doklady, protokol PDF, katalog textů pravidel, hledání.
- **12 `add-billing-and-invoicing`:**
  - nabídka ceny `POST …/quote`, objednávka a Checkout;
  - platba uloženou kartou;
  - přehled předplatného, faktury, ZIP, zrušení sledování;
  - změna karty (portál Stripe nebo SetupIntent).
- **8 `add-analysis-runs-in-worker`:** stavy běhu a události `run_events` pro SSE.
- **14 `add-public-website-cms`:**
  - skupina 1 (jazykové základy: next-intl, `messages/sk.json`, `messages/cs.json`, ICU, formáty, test úplnosti) se dělá hned po skupině 2 této změny (kostra `web/`) a před první obrazovkou;
  - přechod z webu do aplikace (`/app/login?from=sk|cz&shop=…`).
- **16 `add-change-monitoring`:** data obrazovky 7. Bez nich obrazovka ukáže prázdný stav.
- **15 `add-shoptet-connector`:** volba „Pripojiť cez Shoptet“ na 3a.
- **17 `add-single-server-operations`:** Caddy směruje `/api` na API a nastavuje `flush_interval -1` pro SSE.

## Done when

- **Proklikání celého toku** (skupina „Ověření“ v `tasks.md`) projde slovensky i česky:
  - přihlášení odkazem;
  - 3a → 3c se změnou míst prodeje a přepočtem ceny;
  - Checkout (testovací režim Stripe nebo modelové API);
  - průběh analýzy;
  - přehled, oprava stránky s klávesovými zkratkami, hromadná oprava, publikování;
  - doklady, protokol, faktury.
- **Playwright** (`pnpm --dir web test:e2e`) projde ve 2 jazycích × 2 šířkách (1440 × 960, 390 × 844) bez chyby.
- **axe** nenahlásí na žádné obrazovce porušení úrovně WCAG 2.2 A ani AA.
- **Mobil:** na šířce 390 px nemá žádná obrazovka vodorovný posun (`document.scrollingElement.scrollWidth <= 390`) a každý interaktivní prvek má cíl aspoň 44 × 44 px.
- **Test úplnosti překladů** ze změny 14 projde i s klíči obrazovek této změny.
- **Klient API:** `pnpm --dir web api:check` nenajde rozdíl mezi vygenerovaným klientem a snímkem OpenAPI.
- **Žádná tajemství v klientu:** kontrola `.next/static` nenajde klíč ani tajemství (vzory `sk_live_`, `sk_test_`, `whsec_`, `rk_`, hodnoty `PAYLOAD_SECRET`, `TYPESAFE_API_KEY`, `OPENAI_API_KEY`).
- **Validace:** `openspec validate add-web-app-frontend` projde.

## K rozhodnutí

1. **Chybějící návrhy UI (zásada: nejdřív návrh a schválení).** Na plátně nejsou tyto obrazovky a stavy, bez kterých tok nejde dokončit. Jsou skupina 1 v `tasks.md` a do schválení se nestaví:
   - průběh ukázky zdarma mezi 3a/3b a 3c, protože 3c předpokládá „Ukážka zdarma je hotová“;
   - běžící úvodní analýza na Přehledu (pozice ve frontě, odhad času);
   - **pokrytí kontroly** na Přehledu a v protokolu: nezkontrolované stránky a důvod (robots.txt, nenačtený text, limit velikosti). Plyne ze zásady „co nebylo zkontrolováno, se uvede“. Na Přehledu (4) chybí;
   - částečný nebo selhaný běh;
   - prázdný Přehled po „Preskočiť“ bez e-shopu;
   - Nastavenia: heslo, jazyk, vypnutí klávesových zkratek, výjimka „túto verziu nekontrolovať“ z 3d, členové a pozvánky, fakturační údaje firmy;
   - „Zabudnuté heslo“ a nastavení nového hesla;
   - panel upozornění (zvoneček) a výsledky hledání Ctrl K;
   - dialogy „Pridať doklad“ a „Zrušiť sledovanie“;
   - „Nová cena od …“ a upozornění na neúspěšnou platbu (data dodá změna 12);
   - přepnutí účtu u uživatele ve více tenantech (agentura);
   - stránka „Účet“ ze spodní navigace mobilu. Spodní navigace nemá Doklady, takže není jasné, kde jsou Doklady na mobilu;
   - mobilní varianty 2a–2c, 3a–3d, 5, 6a–6c, 7, 8, 10 (na plátně jsou jen 9 a 9b), včetně spodní lišty 6a nad spodní navigací;
   - chybové stavy onboardingu: „ukážka už bola na tejto doméne použitá“ (`free_sample_claims`), zrušená nebo neověřená platba;
   - zobrazení „Podľa nálezov“: `Findings.dc.html` je ve složce návrhu, ale není na plátně, takže není schválené;
   - cíl odkazu „Porovnať verzie“ na 3d;
   - tlačítko „Zaplatiť kartou •••• 4242“ pro další e-shop (změna 12 ho dodává, na 3c chybí);
   - řádek s DPH v souhrnu objednávky (změna 12, K rozhodnutí bod 12: `vat_preview`).
2. **„Majiteľka účtu“ v bočním menu** je v ženském rodě a rod uživatele neukládáme. Návrh: popisek role bez rodu („Vlastník účtu“ / „Správca“ podle role), nebo nové pole s oslovením. Do rozhodnutí neutrální tvar.
3. **Konektory až ve změně 15.** Obrazovka 3a doporučuje „Pripojiť cez Shoptet“. Dokud konektor není, nesmí se volba tiše ztratit. Návrh: neaktivní karta s „Pripravujeme“ a vysvětlením, výchozí volba „Len adresa webu“. Vyžaduje úpravu návrhu.
4. **Obrazovka 7 je v této změně, data až ve změně 16** (F9 po pilotu). Do té doby se ukazují jen přepínače upozornění (`notification_settings`, změna 9) a prázdný stav. Přepínač „Kontrola pri uložení, aj skrytých produktov“ se ukáže až s konektorem.
5. **Souhlas s obchodními podmínkami při objednávce.** Obrazovka 3c ho neukazuje a obchodní podmínky jsou verzované dokumenty v aplikaci (architektura, část 12). Možnosti: `consent_collection` ve Stripe Checkoutu, nebo vlastní zaškrtávací pole nad tlačítkem platby. V obou případech se k objednávce uloží verze. Návrh: vlastní pole kvůli verzi dokumentu, chce úpravu návrhu.
6. **„Nová kontrola“ na Přehledu:** spouští `recheck`? Je v předplatném zdarma? Frontend ukáže odhad z API a spustí běh až po potvrzení. Bez rozhodnutí je tlačítko neaktivní s vysvětlením.
7. **Jazyk protokolu PDF.** Architektura navrhuje jazyk trhu e-shopu, volba v návrhu chybí. Do rozhodnutí se stahuje v jazyce domovské země e-shopu (`shops.home_country`).
8. **Adresa aplikace:** `/app` na doméně vydání webu (předpoklad této změny), nebo samostatná `app.eshopguard.…`. Závisí na doménách (architektura, část 11, bod 8).
9. **Ověření vlastnictví e-shopu** (architektura, část 11, bod 4): krok v onboardingu chybí v návrhu i v plánu frontendu.
10. **Tlačítko Google** má v návrhu ikonu glóbu. Pravidla značky Google pro přihlášení vyžadují logo „G“ a předepsaný text. Návrh: upravit podle pravidel Google, chce úpravu plátna.
11. **Roční sledování** („ročně 2 měsíce zdarma“, strategie) v návrhu chybí. Změna 12 objednává jen měsíc, zde se rok nestaví.
12. **Kódy zakládajících zákazníků:** pole v návrhu chybí. Návrh: `allow_promotion_codes` ve Stripe Checkoutu (změna 12), v aplikaci nic.
13. **Pořadí jazykových základů.** Požadavky „Texty rozhraní:“ jsou podle zadání ve změně 14. Obrazovky této změny na nich stojí, proto se skupina 1 změny 14 dělá před skupinou 5 této změny. Alternativa je přesunout jazykové základy sem.
14. **Názvy operací API** v `design.md` jsou předpoklad. Závazné jsou názvy ze snímku OpenAPI změn 9–12 a 16. Rozdíl se řeší úpravou volání, ne API.
15. **Založení tenanta u nového účtu:** předpoklad je, že změna 9 založí tenanta s rolí owner při prvním přihlášení odkazem nebo Googlem. Fakturační údaje firmy sbírá Stripe Checkout (změna 12), nebo Nastavenia (chybí návrh).
