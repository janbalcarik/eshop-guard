# Tasks

Cesty jsou relativně ke kořeni repozitáře `eshop-guard/`. Obrazovky se stavějí přesně podle `.dc.html` na plátně. Co na plátně chybí, se staví až po schválení (skupina 1).

Pořadí: skupina 2 → skupina 1 změny 14 (jazykové základy) → skupiny 3 a další.

## 1. Doplnění návrhu UI před kódem (bez kódu)

- [ ] 1.1 Doplnit na plátno a nechat schválit stavy onboardingu:
  - průběh ukázky zdarma na 3c (běží, hotovo, selhala);
  - „ukážka už bola na tejto doméne použitá“ na 3a;
  - neaktivní karta konektoru „Pripravujeme“;
  - tlačítko „Zaplatiť kartou •••• 4242“ pro další e-shop;
  - souhlas s obchodními podmínkami a řádek s DPH v „Objednávka“;
  - tlačítko Google podle pravidel značky;
  - návrat ze Stripe (ověřujeme, zrušeno, neověřeno do 60 s);
  - cíl odkazu „Porovnať verzie“ na 3d.
- [ ] 1.2 Doplnit a nechat schválit stavy Přehledu a dat:
  - běžící úvodní analýza (pozice ve frontě, odhad času);
  - pokrytí kontroly (nezkontrolované stránky a důvody), částečný a selhaný běh;
  - prázdný Přehled bez e-shopu;
  - pohled „Podľa nálezov“ (rozhodnout, zda platí `Findings.dc.html`);
  - panel upozornění, výsledky hledání Ctrl K;
  - dialogy „Pridať doklad“ a „Zrušiť sledovanie“;
  - „Nová cena od …“, neúspěšná platba.
- [ ] 1.3 Doplnit a nechat schválit další obrazovky:
  - Nastavenia (heslo, jazyk, vypnutí zkratek, výjimka verze, členové a pozvánky, fakturační údaje);
  - „Zabudnuté heslo“;
  - přepnutí účtu;
  - mobilní „Účet“ s umístěním Dokladů;
  - mobilní varianty 2a–2c, 3a–3d, 5, 6a–6c (spodní lišta nad navigací), 7, 8, 10;
  - popisek role bez rodu místo „Majiteľka účtu“ (proposal, K rozhodnutí bod 2).
- [ ] 1.4 Zapsat schválenou verzi plátna do `web/DESIGN_VERSION`. Test: snímkové testy (16.3) odkazují na tuto verzi a bez souboru `pnpm test:e2e` selže.

## 2. Kostra `web/` a návrhový systém

- [ ] 2.1 Založit `web/`:
  - Next.js App Router, TypeScript `strict`, React 19, pnpm, `.nvmrc` 24;
  - verze `next` v rozsahu `peerDependencies` balíčku `@payloadcms/next` (ověřit při založení);
  - `next.config.ts` s `output: 'standalone'`;
  - skripty `dev`, `build`, `start`, `lint`, `typecheck`, `test`, `test:e2e`, `api:generate`, `api:check`, `secrets:scan`.
- [ ] 2.2 Vytvořit `src/app/(app)/app/layout.tsx` s vlastním `<html lang>`, `src/fonts.ts` (`Bricolage_Grotesque` 500–700, `Figtree` 400–700 přes `next/font/google`), `src/styles/tokens.css` (všechny tokeny z `design.md`) a Tailwind CSS 4 `@theme` nad tokeny.
- [ ] 2.3 Postavit `src/components/ui/`:
  - `Button` s variantami a výškami z návrhu (28–52 px);
  - `Badge`, `CountPill`, `Card`, `SegmentedControl` (`aria-pressed`), `Tabs`, `Select`, `Checkbox`, `RadioCard`, `Switch` (`role="switch"`), `Dialog`, `Menu` (Radix);
  - `TextField`, `Kbd`, `ProgressBar`, `VisuallyHidden`, `Icon` (cesty SVG z `.dc.html`), `InfoNote`, `Stepper`;
  - `eslint.config.mjs` s `jsx-a11y`, `react/jsx-no-literals` a vlastními pravidly `eslint-rules/no-server-env-in-client.js` a `eslint-rules/no-client-price-math.js`.
- [ ] 2.4 Test: `tests/unit/tokens-contrast.test.ts` (dvojice text–pozadí ≥ 4,5 : 1, prvky ≥ 3 : 1), testy komponent `ui` s `vitest-axe` (0 porušení) a testy obou pravidel ESLint na ukázkových souborech. Hotovo, když `pnpm --dir web lint typecheck test` projde.

## 3. API klient, relace, chyby a modelové API

- [ ] 3.1 Napsat `scripts/generate-api.mjs` (`openapi-typescript` → `src/lib/api/schema.d.ts`) a `scripts/check-api.mjs` (vygenerovat do dočasného souboru a porovnat) nad snímkem `openapi/eshopguard-api.json`. Ten exportuje sestavení `EshopGuard.Api` (změna 9).
- [ ] 3.2 Napsat volání a chyby:
  - `src/lib/api/client.ts`: `openapi-fetch`, stejný původ, hlavička `X-XSRF-TOKEN` z cookie `XSRF-TOKEN`;
  - `src/lib/api/server.ts`: `server-only`, `API_INTERNAL_URL`, předání jen `cookie` a `accept-language`;
  - `src/lib/api/problem.ts`: ProblemDetails → `ApiError{code, params, traceId}`; 401 → `/app/login?next=`, 403, 409/412.
- [ ] 3.3 Napsat relaci a ochranu:
  - `src/lib/session.ts`: `getMe`, aktivní tenant a e-shop, cookie `eg_ctx`;
  - `src/app/(app)/app/t/[tenantId]/layout.tsx`: členství, jinak 403;
  - pravidlo `/app/t/*` v `src/middleware.ts`;
  - `src/lib/log.ts`: pino s `redact` pro `cookie`, `authorization`, `x-xsrf-token` a `token` v adrese;
  - CSP s nonce pro `/app/*` (včetně `frame-ancestors 'none'`) v `src/middleware.ts`, `Referrer-Policy` a `X-Content-Type-Options` v `next.config.ts`.
- [ ] 3.4 Postavit modelové API a Playwright:
  - `tests/mock-api/server.ts` (MSW + `@mswjs/http-middleware`, port 5299);
  - handlery v `tests/mock-api/handlers/*.ts` typované přes `openapi-msw`;
  - data `tests/mock-api/data/bylinkovo.ts` podle čísel z návrhu;
  - `playwright.config.ts` s projekty `sk-desktop`, `cs-desktop` (1440 × 960), `sk-mobile`, `cs-mobile` (390 × 844).
- [ ] 3.5 Test: `tests/unit/problem.test.ts` (známý kód, neznámý kód s `traceId`, 401, 412) a `tests/e2e/navigation.spec.ts` (vypršelá relace → přihlášení → návrat na stejnou adresu). `pnpm api:check` v CI selže při změněném snímku.

## 4. Rozvržení a navigace

- [ ] 4.1 Postavit boční menu:
  - `components/shell/Sidebar.tsx` podle `Sidebar.dc.html` (248 px, položky s ikonami a počty z API, aktivní `#E3F0ED`/`#0B4A43`);
  - `ShopSwitcher.tsx` (e-shop, stav připojení s tečkou `#2E9E5B`, uložení do `eg_ctx`);
  - `MonitoringSidebarCard.tsx` (počet e-shopů, další platba z API).
- [ ] 4.2 Postavit `AccountMenu.tsx` (Radix `DropdownMenu`, `aria-expanded`, Escape vrací fokus):
  - hlavička s názvem firmy a e-mailem;
  - položky Predplatné a platby, Nastavenia, Jazyk (Slovenčina / Čeština → `PATCH /api/me`, při chybě beze změny), Odhlásiť sa (`POST /api/auth/logout`);
  - popisek role podle schváleného návrhu 1.3.
- [ ] 4.3 Postavit sdílené lišty:
  - `MobileHeader.tsx` (přepínač e-shopu, zvoneček 44 × 44);
  - `MobileBottomNav.tsx` (72 px, Prehľad, Opravy · N, Sledovanie, Účet);
  - `TopBar.tsx` (pole hledání s Ctrl K, zvoneček s tečkou `#D92D20`, „Nová kontrola“ neaktivní s vysvětlením do rozhodnutí);
  - `OnboardingHeader.tsx` (krokovač Účet → Pripojenie → Rozsah a spustenie, „Preskočiť“);
  - `StickyActionBar.tsx` (76 px).
- [ ] 4.4 Test: `tests/e2e/navigation.spec.ts`:
  - menu účtu klávesnicí;
  - změna jazyka na češtinu a zpět;
  - přepnutí e-shopu se zachováním filtrů;
  - aktivní položka podle trasy;
  - spodní navigace pod 768 px a boční menu nad 768 px.

## 5. Přihlášení 2a–2c

- [ ] 5.1 Postavit `components/auth/AuthLayout.tsx` (levý panel `#0E3B36`, nadpis, odrážky s ikonou `#9FD3C5`, „Nenahrádza právne poradenstvo.“; mobilní podoba podle 1.3) a `UiLanguageSwitcher.tsx` (`menuitemradio`, Slovenčina / Čeština s podtitulem, výchozí podle `?from=sk|cz`, uložení do cookie `eg_ui_locale` podle změny 14; cookie vydání webu `NEXT_LOCALE` se nemění).
- [ ] 5.2 Postavit `app/(app)/app/login/page.tsx`:
  - `MagicLinkForm` jako výchozí režim (e-mail, „Poslať odkaz na prihlásenie“, „Odkaz platí 15 minút…“);
  - `PasswordForm` (Heslo, „Zabudnuté heslo?“, `auth.invalid_credentials` s fokusem na pole);
  - přepínání režimů;
  - `GoogleButton` → `/api/auth/external/google?returnUrl=`;
  - chyba `auth.external_canceled`;
  - předání `?shop=` do onboardingu;
  - poznámka se souhlasem a odkazy na verzované dokumenty (změna 14).
- [ ] 5.3 Postavit odeslání a potvrzení odkazu:
  - `login/sent/page.tsx` s `ResendCountdown.tsx` (odpočet z `retryAfterSeconds`, `aria-live` jen při aktivaci, „Použiť iný e-mail“);
  - `login/confirm/page.tsx` s `ConfirmMagicLink.tsx`: stav odkazu serverově bez spotřebování, `history.replaceState`, `Referrer-Policy: no-referrer`, POST až tlačítkem, stav „odkaz už neplatí“ + „Poslať nový odkaz“, rámeček pro nový účet.
- [ ] 5.4 Test: `tests/e2e/login.spec.ts`:
  - odeslání a odpočet;
  - GET odkaz nespotřebuje (modelové API počítá volání `consume` = 0);
  - vypršelý odkaz;
  - limit `auth.magic_link.rate_limited`;
  - špatné heslo;
  - zrušený Google;
  - výchozí čeština z `?from=cz`.

## 6. Připojení e-shopu 3a–3b

- [ ] 6.1 Postavit `components/onboarding/ShopUrlField.tsx`:
  - debounce 600 ms, `POST …/shops/detect`;
  - štítek „Rozpoznané: …“ (`#E4F4EA`/`#1C6B3E`) nebo „Platformu sme nerozpoznali“ (`#EFEEE8`);
  - chyba neplatné adresy;
  - fokusový rámeček `#CFE6E0`.
- [ ] 6.2 Postavit volby připojení:
  - `SourceModeOptions.tsx` (RadioCard, dostupnost z `GET …/shops/source-modes`, nedostupné neaktivní s vysvětlením);
  - `ConnectionComparisonTable.tsx` (áno / nie / čiastočne s barvami z návrhu);
  - `PlatformPicker.tsx` (Shoptet, Upgates, BiznisWeb, WooCommerce, Shopify, Iná platforma).
- [ ] 6.3 Postavit `ConnectorCredentialsForm.tsx` (WooCommerce Consumer key / secret, Upgates jméno a klíč, BiznisWeb token):
  - `autocomplete="off"`, secret jako `type="password"`;
  - jen v těle `POST`, vymazání po odpovědi, mimo telemetrii (AD-13);
  - „Pokračovať“ → `POST …/shops` → 3c;
  - chyba `shops.free_sample_already_claimed` podle 1.1.
- [ ] 6.4 Test: `tests/e2e/onboarding.spec.ts` (rozpoznaná a nerozpoznaná platforma, nedostupný konektor viditelný, ukázka už použitá) a `tests/unit/credentials.test.ts`. Klíče se nezapíšou do `localStorage`, `sessionStorage`, adresy ani `console`.

## 7. Rozsah, nabídka ceny a objednávka 3c–3d

- [ ] 7.1 Postavit `onboarding/[shopId]/scope/page.tsx` (mřížka 7 + 5 sloupců, okraje 32 × 120 px):
  - `ConnectedShopCard.tsx` (zdroj, verze, počty, „Zmeniť“ → 3a);
  - `MarketsFieldset.tsx`: karty zemí se zaškrtávacím polem, důvodem a štítkem pravidel, text „Pri každom náleze uvidíte…“, chyba `shops.markets_none_selected`.
- [ ] 7.2 Postavit jazykové verze:
  - `LanguageVersionsSummary.tsx` (věty ze shrnutí API, „Podrobnosti“ jen při více verzích);
  - `onboarding/[shopId]/versions/page.tsx` s `VersionsTable.tsx` (6 sloupců, štítek „Do ceny“) a `VersionComparison.tsx` (štítky 17/2/1, významné produkty, „Vylúčiť verziu z kontroly v nastaveniach“);
  - „Späť na objednávku“ se zachovaným výběrem.
- [ ] 7.3 Postavit `ScopeCards.tsx` (karta „Ukážka zdarma · hotová“ s průběhem podle 1.1 a „Pozrieť výsledky ukážky“, karta „Celý e-shop“ s počty a časem z API) a `ModulesFieldset.tsx` (4 moduly z API, popisky přes zprávy).
- [ ] 7.4 Napsat `useQuote.ts` a `OrderSummary.tsx`:
  - `useQuote`: `POST …/quote` při každé změně, `AbortController`, pořadové číslo, stavy `pending` / `ready` / `error`;
  - `OrderSummary`: řádky, „Dnes zaplatíte … bez DPH“, další platba s datem, ztlumení a „Prepočítavame cenu…“ (`aria-live="polite"`);
  - tlačítko „Zaplatiť … a spustiť kontrolu“ neaktivní při `pending` nebo `error`, „Skúsiť znova“.
- [ ] 7.5 Napsat objednávku a návrat:
  - `POST …/orders {quoteId}` → `window.location.assign(checkoutUrl)`;
  - `pay-with-saved-card` pro další e-shop včetně 3-D Secure podle změny 12;
  - `billing.quote_expired` / `billing.quote_mismatch` → nová nabídka a upozornění;
  - `checkout/return/page.tsx` s `CheckoutReturn.tsx`: dotaz každé 2 s nejvýš 60 s, `paid` → Přehled, jinak stavy podle 1.1;
  - `WhatHappensNext.tsx` (texty podle zdroje e-shopu).
- [ ] 7.6 Test:
  - `tests/unit/useQuote.test.ts`: odpovědi v obráceném pořadí → zobrazená nabídka patří k poslednímu výběru; chyba → tlačítko neaktivní;
  - `tests/unit/OrderSummary.test.tsx`: žádná aritmetika, jen hodnoty nabídky;
  - `tests/e2e/quote-and-checkout.spec.ts`: odškrtnutí Česka, rychlé změny, chyba nabídky, žádná země, úspěšná, nepotvrzená a zrušená platba, zastaralá nabídka.

## 8. Živý průběh (SSE)

- [ ] 8.1 Napsat `src/lib/api/sse.ts` a `components/runs/useRunEvents.ts`: `EventSource` na `…/runs/{r}/events`, opětovné připojení s `Last-Event-ID`, po 3 neúspěších dotaz každých 5 s, po 60 s bez zprávy stav „Spojenie sa obnovuje“, uzavření při odchodu ze stránky.
- [ ] 8.2 Postavit `components/runs/RunProgress.tsx` (krok, průběh stránek, pozice ve frontě a odhad času z kódů `run.*`, `aria-live` jen při změně kroku a konci) a zapojit ho do 3c (ukázka), Přehledu (analýza) a `PublishButton` (publikování).
- [ ] 8.3 Test: `tests/unit/useRunEvents.test.ts` (modelový `EventSource`: přerušení, `Last-Event-ID`, přechod na dotazování) a `tests/e2e/run-progress.spec.ts` (průběh 63/100, konec ukázky načte nabídku, částečný běh podle 1.2).

## 9. Přehled (4, 9)

- [ ] 9.1 Postavit `shops/[shopId]/overview/page.tsx` (serverové načtení `GET …/overview`):
  - podtitul se souhrnem přes ICU (data, čas, počty ve třech skupinách);
  - `components/protocol/ProtocolDownloadLink.tsx`;
  - `components/overview/KpiCards.tsx` (3 karty, čísla 40 px Bricolage, průběh publikovaných `#2E9E5B`).
- [ ] 9.2 Postavit `PriorityPagesList.tsx` (5 řádků, stav „Na schválenie“ / „Potrebujeme odpoveď“), `QuickAnswers.tsx` (Áno / Nie, neaktivní při požadavku a pro roli `viewer`, obnovení počtu po odpovědi) a `MonitoringSummary.tsx`.
- [ ] 9.3 Postavit `CoverageNote.tsx` a stavy běžící, částečné a prázdné kontroly podle 1.2. Mobilní varianta podle `Mobile.dc.html` (3 dlaždice, seznam 4 stránek, zkrácený stav „Odpoveď“).
- [ ] 9.4 Test: `tests/e2e/overview.spec.ts`:
  - podtitul sk a cs s množnými čísly;
  - rychlá odpověď;
  - role `viewer`;
  - pokrytí s nezkontrolovanými stránkami;
  - mobilní varianta 390 px.

## 10. Opravy (5)

- [ ] 10.1 Postavit `shops/[shopId]/fixes/page.tsx` a `FixesHeader.tsx`:
  - podtitul „28 stránok s nálezom · spolu 43 nálezov · kontrola …“;
  - `LanguageVersionFilter.tsx` („Všetky verzie“ a verze z API);
  - `SegmentedControl` „Podľa stránok“ / „Podľa nálezov“;
  - odkaz „Protokol (PDF)“;
  - stav ve `searchParams` (`tab`, `lang`, `view`).
- [ ] 10.2 Postavit záložky a hromadné opravy:
  - `FixTabs.tsx` (Radix Tabs, šipky, počty v barvách návrhu, prázdný stav záložky);
  - `GroupFixesBanner.tsx` (rámeček `#D9CFF3`, nadpis „Hromadné opravy: N textov sa opakuje na M stránkach“, štítky textů s počty, „+ N ďalšie“, „Začať hromadné opravy“).
- [ ] 10.3 Postavit řádky:
  - `FixPageRow.tsx` (název a cesta, ukázka `<del>` → `<ins>` nebo otázka s ikonou, `FindingCountChips.tsx` s ICU, rozsah práce, stav, položky „Celý e-shop: šablóna“ a „Celý e-shop: košík a objednávka“);
  - `FindingRow.tsx` pro „Podľa nálezov“ podle 1.2.
- [ ] 10.4 Test: `tests/e2e/fixes.spec.ts`:
  - filtr `lang=cs` a tlačítko Zpět;
  - štítky „1 porušenie“, „2 porušenia“, „6 porušení“ (sk) a české tvary;
  - prázdná záložka;
  - přechod na 6a, 6b a 6c z řádků.

## 11. Oprava stránky (6a, 6b)

- [ ] 11.1 Postavit `fixes/pages/[pageId]/page.tsx` (serverové načtení revize a katalogu `src/lib/rule-texts.ts`) a `ReviewHeader.tsx`:
  - drobečková navigace;
  - „Stránka 1 z 24 na riešenie“, „Ďalšia: …“;
  - adresa stránky, zdroj textu „Text zo Shoptetu, produkt č. …“ / „Text z webu“;
  - `ViewModeToggle.tsx` (Vedľa seba, Zmeny v texte, Celý nový text, `?mode=`).
- [ ] 11.2 Postavit `DiffText.tsx` a karty změn:
  - `DiffText`: `<del>`/`<ins>` se skrytými „odstránené:“ / „pridané:“, šedý kontext;
  - `ChangeCardCollapsed.tsx` („Prijaté“, „V kontexte“);
  - `ChangeCard.tsx`: aktivní rámeček `#0E5A52`, Zamietnuť / Upraviť / Prijať, `AlternativesChips.tsx` („S upresnením“, „Bez environmentálneho slova“), editace textu s novou kontrolou, „Zobraziť celý odsek“;
  - `UnchangedParagraphsDivider.tsx`, poznámka o šabloně (přerušovaný rámeček).
- [ ] 11.3 Postavit karty hromadné změny a otázky:
  - `GroupChangeCard.tsx`: rámeček `#7C63C7`, verdikty po zemích, „Rovnaký text na N stránkach“, `PlaceholderInput.tsx`, „vetu odstrániť zo všetkých stránok“, „Len na tejto stránke“ / „Prijať na N stránkach“;
  - `QuestionChangeCard.tsx`: Áno / Nie s `aria-pressed`, „Odpovedala … dnes o 10:14“, návrh podle odpovědi, odkaz na Doklady;
  - panel „Prečo sa pýtame“ (6b).
- [ ] 11.4 Postavit `ChangeDetailAside.tsx`:
  - „Zmena N z M“ s předchozí / další;
  - „Prečo meniť“ a „Čo pomôže“ z katalogu textů pravidel;
  - `LegalRefsByCountry.tsx` (země · verdikt, odkaz z API, „Zobraziť znenie ustanovení“);
  - `RecheckStatus.tsx` (`ok` / `still_finding` / `pending`);
  - `ShortcutsHelp.tsx`.
- [ ] 11.5 Napsat `useReviewShortcuts.ts` (A, U, J, K jen při fokusu v `ChangeList` mimo `input`/`textarea`/`[contenteditable]`, vypnutí podle předvolby uživatele) a postavit `ReviewActionBar.tsx`:
  - „Prijaté N z M zmien · N hromadná čaká na údaj“;
  - upozornění, že návrhy připravila umělá inteligence;
  - `CopyTextButton.tsx` (schránka, potvrzení „Skopírované“);
  - `PublishButton.tsx` (`POST …/publications`, průběh, výsledek po změnách včetně `conflict` a `failed`, 412 → „Načítať znova“).
- [ ] 11.6 Test:
  - `tests/unit/useReviewShortcuts.test.ts` (zkratka v poli nic nepřijme, vypnuté zkratky);
  - `tests/unit/DiffText.test.tsx` (skryté popisky v sk a cs);
  - `tests/e2e/review.spec.ts`: tři zobrazení, přijetí zkratkou A s posunem fokusu, alternativa, údaj na hromadné změně, odpověď na otázku (6b), souběžná úprava 412, publikování s jedním konfliktem.

## 12. Hromadná oprava (6c)

- [ ] 12.1 Postavit `fixes/groups/[groupId]/page.tsx` a `GroupFixForm.tsx`:
  - verdikt, pravidlo a odkaz na zákon;
  - „Pôvodný text“ / „Navrhovaný text“;
  - pole údaje s poznámkou „Údaj vložíme presne tak…“ a novou kontrolou po vyplnění;
  - `GroupFixModeRadios.tsx` (nahradiť / odstrániť / vlastné znenie s polem a novou kontrolou).
- [ ] 12.2 Postavit `GroupFixSamples.tsx` (ukázky na 2 stránkách), `GroupFixCheckSummary.tsx` (oprava sedí `#E4F4EA`, riešiť jednotlivo `#EFEAFB`, důvod a odkazy) a `GroupFixPagesList.tsx` (zaškrtnutí, „Vyradiť označené“, „Zobraziť ďalších N“). Spodní lišta: „Jedno rozhodnutie namiesto N“, „Späť na opravy“, „Prijať na N stránkach“ s počtem z API.
- [ ] 12.3 Test: `tests/e2e/group-fix.spec.ts` (vyplnění údaje → nová kontrola `ok` / `still_finding`, vyřazení 2 stránek → 34, stránky k řešení jednotlivě mimo seznam, vlastní znění).

## 13. Sledování změn (7)

- [ ] 13.1 Postavit `shops/[shopId]/monitoring/page.tsx` a `MonitoringCards.tsx` (rozsah sledování, pásmo a konec prvního měsíce, dnešní kontrola) a `MonitoringEmptyState.tsx` (`monitoring.not_started`, žádná ukázková čísla).
- [ ] 13.2 Postavit `NotificationToggles.tsx` (Radix Switch 40 × 24, uložení `PUT …/notification-settings`, návrat stavu při chybě, e-mail příjemce; „Kontrola pri uložení…“ jen s konektorem) a `PageChangesTable.tsx` (období 7 dní, výsledky v barvách návrhu, „Otvoriť návrh“, karty na mobilu).
- [ ] 13.3 Test: `tests/e2e/monitoring.spec.ts` (přepnutí Mezerníkem, chyba → návrat, prázdný stav, tabulka na 1440 a karty na 390 bez vodorovného posunu).

## 14. Doklady a protokol (10, 11)

- [ ] 14.1 Postavit `t/[tenantId]/evidence/page.tsx`:
  - `EvidenceStats.tsx` (4 čísla);
  - `EvidenceTable.tsx` (stavy Platný, Overené v registri, Končí o N dní, Čaká na odpoveď, Tvrdenie odstránené; „Platí pre“ jako odkaz na opravy);
  - poznámka o registru;
  - „Pridať doklad“ podle 1.2: PDF, JPG nebo PNG do 20 MB přes `POST …/evidence`, chyby velikosti a typu.
- [ ] 14.2 Dokončit `ProtocolDownloadLink.tsx`: `POST …/protocols` → 202 → průběh „Pripravujeme protokol…“ → podepsaný odkaz → stažení; jazyk podle domovské země e-shopu do rozhodnutí; chyba s kódem.
- [ ] 14.3 Test: `tests/e2e/evidence-protocol.spec.ts` („Končí o 20 dní“ sk a „Končí za 20 dní“ cs podle zpráv, nahrání dokladu, příliš velký soubor, protokol 202 → stažení, protokol selže).

## 15. Předplatné a platby (8, 9b)

- [ ] 15.1 Postavit `t/[tenantId]/billing/page.tsx`:
  - `ShopSubscriptionsTable.tsx` (desktop, 12 sloupců mřížky);
  - `ShopSubscriptionCard.tsx` (mobil);
  - součet sledování (sleva jen z API);
  - `PaymentCardPanel.tsx` („Zmeniť kartu“ → adresa relace z `POST …/billing/payment-method-session`);
  - `TierStrip.tsx` (5 pásem, aktuální rámeček `#0E5A52` a `#F3FAF8`).
- [ ] 15.2 Postavit `InvoicesPanel.tsx`:
  - vlastní posuvník (`role="region"`, `tabindex="0"`, `aria-label`);
  - načítání kurzorem při posunu;
  - stavy Zaplatená / Naplánovaná, PDF;
  - `InvoiceFilters.tsx` (E-shop, Rok, `searchParams`);
  - `InvoicesZipButton.tsx` (202 → průběh → stažení).
- [ ] 15.3 Postavit `InvoicesListMobile.tsx` (první 4, „Zobraziť všetky faktúry (N)“ ≥ 48 px, bez vnořeného posuvníku) a `CancelMonitoringDialog.tsx` (datum konce období z API, potvrzení, stav z odpovědi).
- [ ] 15.4 Test: `tests/unit/InvoicesPanel.test.tsx` a `tests/e2e/billing.spec.ts`:
  - filtr a ZIP;
  - mobil bez vnořeného posuvníku (žádný prvek kromě dokumentu nemá `scrollHeight > clientHeight` s `overflow-y: auto`);
  - zrušení sledování;
  - bez slevy se neobjeví „[X] %“.

## 16. Přístupnost, mobil a bezpečnost klienta

- [ ] 16.1 Napsat `tests/e2e/a11y.spec.ts`: axe (`wcag2a`, `wcag2aa`, `wcag21aa`, `wcag22aa`) na všech trasách aplikace ve 4 projektech, 0 porušení. Průchod klávesou Tab na 2a, 3c a 6a bez pasti fokusu, s viditelným obrysem fokusu.
- [ ] 16.2 Napsat `tests/e2e/mobile-layout.spec.ts`: na všech trasách při 390 px `scrollWidth ≤ 390` a každý `button`, `a`, `input`, `select`, `[role=switch]`, `[role=tab]` má rozměry aspoň 44 × 44 px.
- [ ] 16.3 Napsat `tests/e2e/visual.spec.ts`: snímky (`toHaveScreenshot`) každé obrazovky na 1440 × 960 a 390 × 844 ve slovenštině. Výchozí snímky schválí uživatel proti plátnu verze z `web/DESIGN_VERSION`.
- [ ] 16.4 Napsat `scripts/scan-client-secrets.mjs` (`.next/static`: `sk_live_`, `sk_test_`, `whsec_`, `rk_`, hodnoty `PAYLOAD_SECRET`, `TYPESAFE_API_KEY`, `OPENAI_API_KEY` z prostředí sestavení) a přidat job `web` do `.github/workflows/ci.yml`. Když soubor ještě neexistuje, založit ho jen s tímto jobem (změna 17 ho rozšíří). Test: úmyslně vložený `process.env.STRIPE_SECRET_KEY` v klientské komponentě shodí lint i kontrolu.

## 17. Ověření

- [ ] 17.1 Odhad ceny + souhlas uživatele před proklikáním s reálným Jevem a OpenAI:
  - ukázka zdarma na testovacím e-shopu stojí podle strategie ~0,5–1 USD;
  - rozbor míst prodeje 0,03–0,08 USD na e-shop;
  - rozbor verzí ~0,04 USD.

  Bez souhlasu běží ověření jen se stuby Jevu a OpenAI ze změn 4 a 8.
- [ ] 17.2 Proklikat celý tok **slovensky** na lokálním prostředí (API, PostgreSQL, worker, testovací režim Stripe přes Stripe CLI):
  1. přihlášení odkazem;
  2. 3a → 3c, odškrtnutí Česka a přepočet ceny, 3d a zpět;
  3. Checkout s testovací kartou;
  4. průběh analýzy;
  5. Přehled, Opravy, oprava stránky se zkratkami A a J, hromadná oprava, publikování nebo „Kopírovať text“;
  6. Doklady, protokol, Predplatné a platby, ZIP faktur.
- [ ] 17.3 Proklikat stejný tok **česky** (jazyk přepnutý v menu účtu) a zkontrolovat:
  - české tvary množného čísla;
  - data `1. 10. 2026`;
  - měny podle API (`29 €` / `690 Kč`);
  - texty nálezů v češtině s odkazy na zákon v jazyce zákona.
- [ ] 17.4 Projít obrazovky 9, 9b, 6a a 3c na šířce 390 px v Playwright i na skutečném telefonu (Android Chrome, iOS Safari): žádný vodorovný posun, cíle ≥ 44 px. Pak `pnpm --dir web lint typecheck test test:e2e api:check secrets:scan` a `openspec validate add-web-app-frontend` projdou.
