# Design: Webová aplikace EshopGuard (Next.js)

Cesty jsou relativně ke kořeni repozitáře `eshop-guard/` (po přejmenování ve změně 1).

## Technical Approach

### Technologie

| Oblast | Volba | Proč |
|---|---|---|
| Rámec | Next.js (App Router) ve verzi, kterou podporuje Payload CMS 3 (změna 14). Rozsah verzí ověřit při založení `web/`, `output: 'standalone'` | Jedna aplikace pro web, CMS i aplikaci (architektura, část 12) |
| Jazyk | TypeScript `strict`, React 19, Node.js 24 LTS (`web/.nvmrc`) | |
| Balíčky | pnpm, zamčený `pnpm-lock.yaml` | |
| Styly | Tailwind CSS 4 s tokeny z návrhu v `@theme` (`web/src/styles/tokens.css`) | Přesné hodnoty z `.dc.html`, žádné ruční barvy v komponentách |
| Přístupná primitiva | Radix UI (`dialog`, `dropdown-menu`, `tabs`, `switch`, `radio-group`, `checkbox`, `tooltip`) | Fokus, klávesnice a ARIA bez vlastní implementace |
| Písma | `next/font/google`: `Bricolage_Grotesque` (500–700, opsz 12–96) a `Figtree` (400–700), `display: swap` | Soubory se stáhnou při sestavení, prohlížeč nevolá Google |
| Data | Serverové komponenty pro první vykreslení, TanStack Query v5 v klientských částech (mutace, obnovení po SSE) | |
| API klient | `openapi-typescript` (typy) + `openapi-fetch` (volání), snímek `web/openapi/eshopguard-api.json` | Typy z jednoho zdroje, malý běhový kód |
| Jazyky | next-intl a `messages/{sk,cs}.json` ze změny 14 (požadavky „Texty rozhraní:“) | |
| Testy | Vitest + Testing Library + `vitest-axe` (komponenty), Playwright + `@axe-core/playwright` (e2e), modelové API `@mswjs/http-middleware` + `openapi-msw` | Bez reálného backendu a bez placených volání |

### Trasy aplikace (`web/src/app/(app)/app/…`)

| Trasa | Obrazovka návrhu | Poznámka |
|---|---|---|
| `/app` | – | Přesměrování: bez relace `/app/login`, bez e-shopu `/app/t/{t}/onboarding/connect`, jinak poslední e-shop z cookie `eg_ctx` (tenant, e-shop), nebo první |
| `/app/login` | 2a `Login.dc.html` | Režim odkazu (výchozí) a režim hesla, `?next=`, `?from=sk\|cz`, `?shop=`. Jazyk před přihlášením je v cookie `eg_ui_locale` (změna 14) |
| `/app/login/sent` | 2b `LoginSent.dc.html` | E-mail z `sessionStorage`, ne z adresy |
| `/app/login/confirm` | 2c `LoginConfirm.dc.html` | GET zobrazí, POST přihlásí. Stav „odkaz už neplatí“ (návrh doplnit) |
| `/app/t/[tenantId]/onboarding/connect` | 3a `Onboarding.dc.html`, 3b `OnboardingOther.dc.html` | Jedna trasa, varianta podle výsledku rozpoznání |
| `/app/t/[tenantId]/onboarding/[shopId]/scope` | 3c `OnboardingScope.dc.html` | Průběh ukázky, místa prodeje, verze, rozsah, moduly, objednávka |
| `/app/t/[tenantId]/onboarding/[shopId]/versions` | 3d `VersionDetails.dc.html` | |
| `/app/t/[tenantId]/checkout/return` | – (doplnit návrh) | Čekání na potvrzení platby z webhooku |
| `/app/t/[tenantId]/shops/[shopId]/overview` | 4 `Dashboard.dc.html`, 9 `Mobile.dc.html` | |
| `/app/t/[tenantId]/shops/[shopId]/fixes` | 5 `Fixes.dc.html` | `?tab=to_resolve\|to_approve\|needs_answer\|published&lang=all\|sk\|cs&view=pages\|findings` |
| `/app/t/[tenantId]/shops/[shopId]/fixes/pages/[pageId]` | 6a `Review.dc.html`, 6b `ReviewQuestion.dc.html` | `?change=3&mode=side\|inline\|full` |
| `/app/t/[tenantId]/shops/[shopId]/fixes/groups/[groupId]` | 6c `GroupFix.dc.html` | |
| `/app/t/[tenantId]/shops/[shopId]/monitoring` | 7 `Monitoring.dc.html` | `?days=7` |
| `/app/t/[tenantId]/evidence` | 10 `Evidence.dc.html` | Doklady platí pro všechny e-shopy tenanta |
| `/app/t/[tenantId]/billing` | 8 `Billing.dc.html`, 9b `BillingMobile.dc.html` | `?invShop=&invYear=` |
| `/app/t/[tenantId]/settings` | (doplnit návrh) | |
| `/app/t/[tenantId]/account` | (doplnit návrh, mobil „Účet“) | |

Protokol (11 `Protocol.dc.html`) je dokument PDF, který skládá backend (architektura, část 12). Aplikace ho jen vyžádá a stáhne.

### Návrhové tokeny (`web/src/styles/tokens.css`, hodnoty přesně z `.dc.html`)

| Token | Hodnota | Použití v návrhu |
|---|---|---|
| `--eg-bg` | `#F6F5F1` | pozadí stránek |
| `--eg-surface` / `--eg-surface-muted` | `#FFFFFF` / `#FBFAF7` | karty, hlavičky tabulek, vyhledávání |
| `--eg-text` / `--eg-text-2` / `--eg-text-3` / `--eg-text-muted` | `#17191C` / `#33373D` / `#4A4F57` / `#5F646C` | text, sekundární text, popisky |
| `--eg-border` / `--eg-border-strong` / `--eg-divider` | `#E2E0D8` / `#CFCCC2` / `#EFEEE8` | okraje karet, polí, oddělovače řádků |
| `--eg-primary` / `--eg-primary-hover` / `--eg-primary-soft` / `--eg-primary-ring` | `#0E5A52` / `#0B4A43` / `#E3F0ED` / `#CFE6E0` | tlačítka, aktivní položka menu, fokus pole |
| `--eg-auth-panel` / `--eg-auth-text` / `--eg-auth-text-2` / `--eg-auth-check` / `--eg-auth-muted` | `#0E3B36` / `#F3F1EA` / `#D5DCD8` / `#9FD3C5` / `#A9B4AF` | levý panel 2a–2c |
| `--eg-violation-bg` / `--eg-violation-fg` | `#FDECEC` / `#9A1C1C` | „Porušenie“, `<del>` |
| `--eg-assess-bg` / `--eg-assess-fg` | `#FEF1DC` / `#8A4B00` | „Na posúdenie“, „čiastočne“, „Končí o …“ |
| `--eg-verify-bg` / `--eg-verify-fg` | `#E6EEFB` / `#1E4E9C` | „Na overenie“ |
| `--eg-question-bg` / `--eg-question-fg` / `--eg-question-strong` / `--eg-question-border` / `--eg-question-tint` / `--eg-question-accent` / `--eg-question-line` | `#EFEAFB` / `#5B3E9A` / `#3F2A70` / `#B9A8E3` / `#FBF9FF` / `#7C63C7` / `#D9CFF3` | otázky, údaje k doplnění, hromadné opravy |
| `--eg-success-bg` / `--eg-success-fg` / `--eg-success-dot` | `#E4F4EA` / `#1C6B3E` / `#2E9E5B` | „Prijaté“, „Aktívne“, „Platný“, tečka připojení |
| `--eg-ins-bg` / `--eg-ins-fg` / `--eg-success-tint` / `--eg-success-line` | `#D6EFDF` / `#14532D` / `#F3FAF6` / `#CDE6D8` | `<ins>`, navrhovaný text |
| `--eg-danger-link` / `--eg-alert-dot` / `--eg-disabled-fg` | `#8A2A1F` / `#D92D20` / `#8A8F96` | „Zrušiť sledovanie“, tečka upozornění, neaktivní tlačítko |
| `--eg-tier-current-bg` | `#F3FAF8` | aktuální pásmo |
| poloměry | 4, 6, 8, 10, 12, 14, 16, 999 px | podle prvků návrhu |
| stín menu | `0 14px 36px rgba(23,25,28,0.16)` | menu jazyka, menu účtu |
| fokus | obrys 2 px `--eg-primary`, odsazení 2 px | návrh fokus neukazuje, doplněno kvůli přístupnosti |

Typografie: nadpisy `font-family: var(--font-bricolage)` (H1 aplikace 30 px/700, Review 26 px, onboarding 36 px, login 32 a 44 px), text `var(--font-figtree)` 13–17 px podle návrhu. Velká čísla v kartách (40 px, 34 px, 28 px, 26 px) jsou v Bricolage Grotesque.

### Očekávané operace API (předpoklad, závazné jsou názvy z OpenAPI)

| Obrazovka | Operace | Změna |
|---|---|---|
| 2a–2c | `POST /api/auth/magic-link`, `GET /api/auth/magic-link/{token}` (stav bez spotřebování), `POST /api/auth/magic-link/{token}/consume`, `POST /api/auth/password/login`, `GET /api/auth/external/google?returnUrl=`, `POST /api/auth/logout`, `GET /api/me`, `PATCH /api/me` (`locale`) | 9 |
| 3a–3b | `POST /api/t/{t}/shops/detect`, `POST /api/t/{t}/shops`, `GET /api/t/{t}/shops/source-modes`, `POST /api/t/{t}/shops/{s}/connectors/{platform}` | 10 (15) |
| 3c–3d | `GET /api/t/{t}/shops/{s}/onboarding`, `GET /api/t/{t}/shops/{s}/languages`, `POST /api/t/{t}/shops/{s}/quote`, `POST /api/t/{t}/orders`, `POST /api/t/{t}/orders/{o}/pay-with-saved-card`, `GET /api/t/{t}/orders/{o}` | 10, 12 |
| průběh | `GET /api/t/{t}/runs/{r}`, `GET /api/t/{t}/runs/{r}/events` (`text/event-stream`), `POST /api/t/{t}/shops/{s}/runs` | 8, 10 |
| 4, 9 | `GET /api/t/{t}/shops/{s}/overview`, `POST /api/t/{t}/questions/{q}/answer` | 11 |
| 5 | `GET /api/t/{t}/shops/{s}/fix-pages`, `GET /api/t/{t}/shops/{s}/findings`, `GET /api/t/{t}/shops/{s}/fix-groups` | 11 |
| 6a–6b | `GET /api/t/{t}/shops/{s}/pages/{p}/review`, `POST /api/t/{t}/fix-proposals/{id}/decision` (`If-Match`), `POST /api/t/{t}/questions/{q}/answer`, `POST /api/t/{t}/shops/{s}/publications`, `GET /api/t/{t}/publications/{id}`, `GET /api/t/{t}/shops/{s}/pages/{p}/copy-text`, `GET /api/rule-texts?locale=&ruleSetIds=` | 11, 6 |
| 6c | `GET /api/t/{t}/fix-groups/{g}`, `PATCH /api/t/{t}/fix-groups/{g}` (údaje, způsob, vyřazené stránky), `POST /api/t/{t}/fix-groups/{g}/approve` | 11 |
| 7 | `GET /api/t/{t}/shops/{s}/monitoring`, `GET /api/t/{t}/shops/{s}/page-changes?days=`, `PUT /api/t/{t}/notification-settings` | 16, 9 |
| 10 | `GET /api/t/{t}/evidence`, `POST /api/t/{t}/evidence` | 11 |
| 11 | `POST /api/t/{t}/shops/{s}/protocols`, `GET /api/t/{t}/protocols/{id}` (podepsaný odkaz na PDF) | 11 |
| 8, 9b | `GET /api/t/{t}/billing/overview`, `GET /api/t/{t}/invoices?shopId=&year=&cursor=`, `GET /api/t/{t}/invoices/{id}/pdf`, `POST /api/t/{t}/invoices/zip`, `POST /api/t/{t}/subscriptions/{id}/cancel`, `POST /api/t/{t}/billing/payment-method-session` | 12 |
| sdílené | `GET /api/t/{t}/shops`, `GET /api/t/{t}/notifications`, `GET /api/t/{t}/search?q=` | 10, 9, 11 |

## Architecture Decisions

1. **AD-1 Tři kořenové layouty v route groups.** `(app)/app/layout.tsx` má vlastní `<html lang>` (jazyk uživatele), tokeny a písma. `(site)` a `(payload)` patří změně 14. Přechod mezi layouty znamená plné načtení stránky, což mezi webem a aplikací nevadí. Styly Tailwindu se tak nedostanou do administrace Payload.
2. **AD-2 Stejný původ a cookie `HttpOnly`.**
   - Prohlížeč volá `/api/...` na stejné doméně (Caddy, změna 17; ve vývoji `rewrites` v `next.config.ts` na `API_INTERNAL_URL`).
   - Relace je jen v cookie od ASP.NET Core Identity (`HttpOnly`, `Secure`, `SameSite=Lax`).
   - Změnové požadavky nesou hlavičku `X-XSRF-TOKEN` z cookie `XSRF-TOKEN` (vzor antiforgery ASP.NET Core, změna 9).
   - Serverové komponenty volají `API_INTERNAL_URL` (`http://api:8080`) a předávají jen hlavičky `cookie` a `accept-language`.
   - `src/lib/api/server.ts` importuje `server-only`.
3. **AD-3 Klient z OpenAPI.**
   - `pnpm api:generate` vytvoří `src/lib/api/schema.d.ts` ze snímku `web/openapi/eshopguard-api.json`, který exportuje sestavení `EshopGuard.Api` (změna 9).
   - `pnpm api:check` porovná a v CI selže při rozdílu.
   - Ručně psané typy odpovědí API jsou zakázané (ESLint `no-restricted-syntax` na `interface *Response` v `src/`).
4. **AD-4 Chyby jako kódy.**
   - `src/lib/api/problem.ts` převede ProblemDetails (`type`, `status`, `code`, `params`) na `ApiError`.
   - Text se skládá z `errors.<code>` v jazyce uživatele (změna 14).
   - Neznámý kód ukáže obecný text s kódem a ID požadavku (`traceId`), nikdy úspěch.
   - 401 přesměruje na `/app/login?next=…`. 403 ukáže „Na túto akciu potrebujete rolu …“. 409/412 (souběžná úprava, `xmin`) ukáže „Návrh medzitým zmenil iný používateľ“ a nabídne načíst znovu.
5. **AD-5 Ceny počítá jen server.**
   - Komponenta `OrderSummary` zobrazuje pouze pole nabídky (`quoteId`, `lines[]`, `totalToday`, `currency`, `nextPayment`, volitelně `vatPreview`).
   - Formát částky dělá `useFormatter().number(…, {style: 'currency'})`, aritmetiku s cenami frontend nedělá.
   - Lint pravidlo zakazuje v `src/components/onboarding` a `src/components/billing` násobení a sčítání polí `price*`, `amount*`, `total*` (vlastní pravidlo `eg/no-client-price-math`).
6. **AD-6 Přepočet nabídky při změně.**
   - Každá změna zaškrtnutí míst prodeje nebo modulů okamžitě odešle `POST …/quote` s celým výběrem.
   - Předchozí požadavek se zruší (`AbortController`) a odpověď se přijme, jen pokud nese nejvyšší pořadové číslo požadavku.
   - Během přepočtu je souhrn ztlumený s textem „Prepočítavame cenu…“ (`aria-live="polite"`) a tlačítko platby je neaktivní.
   - Objednávka se zakládá s `quoteId`. Zastaralou nabídku (`billing.quote_expired`, `billing.quote_mismatch`) server odmítne a frontend nabídku hned obnoví a ukáže novou cenu.
7. **AD-7 Bez optimistických změn u akcí s následky.** Publikování, platba, zrušení sledování, odpověď na otázku, schválení hromadné opravy a nahrání dokladu ukážou stav až z odpovědi. Tlačítko je během požadavku neaktivní s textem průběhu, aby nevznikl dvojí požadavek. Volba alternativy a přepnutí zobrazení jsou čistě lokální.
8. **AD-8 Stav v adrese.** Záložky, filtr verze, zobrazení po stránkách / po nálezech, režim zobrazení změn, aktivní změna a filtr faktur jsou v `searchParams`. Funguje tlačítko Zpět i sdílení odkazu.
9. **AD-9 Živý průběh: EventSource + náhrada.**
   - `useRunEvents(runId)` otevře `EventSource` na `/api/t/{t}/runs/{r}/events` (stejný původ, cookie jde sama).
   - Při výpadku se připojí znovu s `Last-Event-ID`. Po 3 neúspěších přejde na dotazování `GET …/runs/{r}` každých 5 s.
   - Události jsou kódy s parametry (`run.step.started`, `run.progress`, `run.queue.position`, `run.finished`, `run.partial`, `run.failed`), text skládají zprávy.
   - Nahlas se oznamuje jen změna kroku a konec, ne každé číslo průběhu.
10. **AD-10 Klávesové zkratky jen v oblasti revize** (WCAG 2.1.4).
    - A, U, J, K fungují, jen když je fokus v seznamu změn na 6a/6b, ne v textovém poli.
    - V Nastaveniach je jde vypnout (`users` předvolba, změna 9).
    - Každá akce má i tlačítko.
11. **AD-11 Jedna sada komponent pro desktop i mobil, hranice 768 px.**
    - Pod 768 px se boční menu nahradí mobilní hlavičkou (přepínač e-shopu, zvoneček) a spodní navigací 72 px se 4 položkami podle `Mobile.dc.html`.
    - Tabulky (faktury, sledování, doklady, předplatné) se mění na karty.
    - Text „vedľa seba“ se skládá pod sebe.
    - Na mobilu žádný vnořený posuvník (`BillingMobile.dc.html`: faktury rozbalením „Zobraziť všetky faktúry (18)“).
12. **AD-12 Texty pravidel z katalogu.**
    - Nález nese `ruleId`, `ruleSetId` a `params`. Frontend načte katalog textů pravidel pro jazyk uživatele (`GET /api/rule-texts`, neměnný podle verze pravidel, cache `immutable`) a složí text přes ICU s parametry.
    - Odkazy na zákon (`legalRefs`) zobrazí tak, jak přišly, v jazyce zákona.
    - Chybí-li text pro jazyk, ukáže `ruleId` s upozorněním „Text pravidla chýba“ a zapíše chybu do konzole, ne prázdné místo.
13. **AD-13 Žádná tajemství v klientu ani v logu.**
    - Do klientského kódu smí jen proměnné `NEXT_PUBLIC_*` (vlastní pravidlo ESLint `eg/no-server-env-in-client`).
    - Klíče konektorů zadané na 3b (Consumer key/secret, token API) jdou jen v těle `POST` na API. Neukládají se do stavu mimo formulář, `localStorage`, adresy ani telemetrie, pole má `autocomplete="off"` a po odeslání se vymaže.
    - Serverový log Next (`src/lib/log.ts`, pino s `redact`) maskuje `cookie`, `authorization`, `x-xsrf-token` a parametr `token` v adrese.
14. **AD-14 Testy proti modelovému API.**
    - `web/tests/mock-api/server.ts` (MSW + `@mswjs/http-middleware`, port 5299) obsluhuje serverové komponenty (`API_INTERNAL_URL`) i prohlížeč (`rewrites` v testovacím režimu).
    - Data `bylinkovo` odpovídají návrhu: 43 nálezů, 14 porušení, 23 na posúdenie, 6 na overenie, 28 stránek, Jana Kováčová, bylinkovo.sk, bylinkovo-darceky.sk, bylinkovo.cz.
    - Obslužné funkce jsou typované přes `openapi-msw`. Když se OpenAPI změní, testy se nepřeloží.

## Data Flow

### Přihlášení odkazem v e-mailu (2a → 2b → 2c)

```
2a  e-mail → POST /api/auth/magic-link {email, locale, returnUrl}
      202 {retryAfterSeconds} → sessionStorage[eg_login_email] → /app/login/sent
      429 auth.magic_link.rate_limited {retryAfterSeconds} → chyba u pole, zůstává 2a
2b  odpočet z retryAfterSeconds; „Poslať znova“ = stejný POST; „Použiť iný e-mail“ → 2a
e-mail  /app/login/confirm?token=T
2c  server: GET /api/auth/magic-link/T → {status: valid|expired|used, email, isNewAccount}   (nespotřebuje)
      valid  → „Pokračujete ako …“; klient: history.replaceState bez tokenu, token jen v paměti
      klik   → POST /api/auth/magic-link/T/consume → Set-Cookie relace → /app (podle /api/me)
      expired|used → „Odkaz už neplatí“ + „Poslať nový odkaz“ (návrh doplnit)
```

### Onboarding, nabídka ceny a platba (3a → 3c → Stripe → návrat)

```
3a  URL (debounce 600 ms) → POST …/shops/detect → {recognized, platform, normalizedUrl}
      volba způsobu podle GET …/shops/source-modes (dostupné konektory, feed, web)
      „Pokračovať“ → POST …/shops {url, sourceMode, platform?, feedUrl?}
        → {shopId, sampleRunId} → 3c
      (konektor: přesměrování na OAuth platformy, změna 15; 3b klíče → POST …/connectors/{platform})
3c  SSE …/runs/{sampleRunId}/events → průběh ukázky (stav „beží“)
      run.finished → GET …/shops/{s}/onboarding {markets[{code, evidence[], badgeCode}], languagesSummary, sample, modules}
      POST …/quote {markets:[sk,cz], modules:[eco,dur,ucp,legal]} → nabídka → OrderSummary
      změna zaškrtnutí → zrušit předchozí požadavek → POST …/quote → nová nabídka
      „Zaplatiť … a spustiť kontrolu“ → POST …/orders {quoteId}
        → {orderId, checkoutUrl} → window.location.assign(checkoutUrl)        (první e-shop)
        → POST …/orders/{o}/pay-with-saved-card → {status | requiresAction}    (další e-shop, změna 12)
Stripe  success_url → /app/t/{t}/checkout/return?order={o}
return  GET …/orders/{o} každé 2 s nejvýš 60 s
          paid → /app/t/{t}/shops/{s}/overview (průběh analýzy přes SSE)
          checkout_open po 60 s → „Platbu ešte overujeme, výsledok pošleme e-mailom“
          expired|canceled → 3c s upozorněním; návratová adresa sama nikdy není důkaz platby
```

### Revize stránky a publikování (6a/6b)

```
server  GET …/pages/{p}/review → {page, source, changes[], counters, queue{index,total,nextPageId}}
        GET /api/rule-texts?locale=sk&ruleSetIds=… (cache podle verze)
A/U/J/K nebo tlačítka → POST …/fix-proposals/{id}/decision {action, alternative?, editedText?} If-Match: "<xmin>"
        200 → aktualizovaná změna + počty pro spodní lištu → fokus na další nerozhodnutou změnu
        412 → „Návrh medzitým zmenil iný používateľ“ + Načítať znova
otázka  POST …/questions/{q}/answer {answer: yes|no} → návrh textu podle odpovědi
publikovat  POST …/publications {fixProposalIds[]} → 202 {publicationRunId}
        SSE nebo GET …/publications/{id} → po změnách published | conflict | failed
        conflict → „Text v e-shope sa medzitým zmenil, stránku kontrolujeme znova“
bez konektoru  „Kopírovať text“ → GET …/copy-text → schránka + potvrzení „Skopírované“
```

### Faktury (8 a 9b)

```
8   GET …/billing/overview (e-shopy, karta, pásma, součet)
    GET …/invoices?shopId&year&cursor (stránkování kurzorem, načítání při posunu ve vlastním posuvníku)
    „Stiahnuť ZIP“ → POST …/invoices/zip {shopId?, year?} → 202 → průběh → podepsaný odkaz → stažení
9b  stejná data; první 4 faktury, „Zobraziť všetky faktúry (18)“ rozbalí zbytek do toku stránky
```

## File Changes

```
web/
  package.json                    skripty: dev, build, start, lint, typecheck, test, test:e2e, api:generate, api:check, secrets:scan
  pnpm-lock.yaml, .nvmrc (24), tsconfig.json (strict, paths "@/*")
  next.config.ts                  output 'standalone'; rewrites /api/* → API_INTERNAL_URL jen mimo produkci;
                                  headers pro /app/*: Referrer-Policy, X-Content-Type-Options; CSP s nonce (vč. frame-ancestors 'none') skládá src/middleware.ts pro každý požadavek
  eslint.config.mjs               next, jsx-a11y, react/jsx-no-literals (src/app/(app), src/components),
                                  eg/no-server-env-in-client, eg/no-client-price-math (web/eslint-rules/)
  eslint-rules/no-server-env-in-client.js, eslint-rules/no-client-price-math.js
  postcss.config.mjs
  playwright.config.ts            projekty: sk-desktop, cs-desktop (1440×960), sk-mobile, cs-mobile (390×844)
  vitest.config.ts
  openapi/eshopguard-api.json     snímek OpenAPI z EshopGuard.Api
  scripts/generate-api.mjs, scripts/check-api.mjs, scripts/scan-client-secrets.mjs
  src/
    middleware.ts                 (sdílený se změnou 14) /app/t/* bez cookie relace → /app/login?next=; nonce a hlavička CSP pro /app/*
    fonts.ts                      Bricolage_Grotesque, Figtree (next/font/google)
    styles/tokens.css, styles/app.css
    app/(app)/app/
      layout.tsx                  <html lang={locale}>, NextIntlClientProvider, QueryClientProvider, písma
      page.tsx                    přesměrování podle relace a cookie eg_ctx
      not-found.tsx, error.tsx    chybové stránky v jazyce uživatele
      login/page.tsx              2a
      login/sent/page.tsx         2b
      login/confirm/page.tsx      2c
      t/[tenantId]/layout.tsx     kontrola členství z /api/me (403 → chybová stránka)
      t/[tenantId]/onboarding/connect/page.tsx                 3a/3b
      t/[tenantId]/onboarding/[shopId]/scope/page.tsx          3c
      t/[tenantId]/onboarding/[shopId]/versions/page.tsx       3d
      t/[tenantId]/checkout/return/page.tsx
      t/[tenantId]/shops/[shopId]/layout.tsx                    AppShell (Sidebar | MobileHeader + MobileBottomNav)
      t/[tenantId]/shops/[shopId]/overview/page.tsx             4 / 9
      t/[tenantId]/shops/[shopId]/fixes/page.tsx                5
      t/[tenantId]/shops/[shopId]/fixes/pages/[pageId]/page.tsx 6a / 6b
      t/[tenantId]/shops/[shopId]/fixes/groups/[groupId]/page.tsx 6c
      t/[tenantId]/shops/[shopId]/monitoring/page.tsx           7
      t/[tenantId]/evidence/page.tsx                            10
      t/[tenantId]/billing/page.tsx                             8 / 9b
      t/[tenantId]/settings/page.tsx                            (po schválení návrhu)
      t/[tenantId]/account/page.tsx                             (po schválení návrhu)
    components/ui/
      Button.tsx (primary, secondary, outline-primary, ghost-link, danger-link; výšky 28/32/34/36/38/40/44/48/52)
      IconButton.tsx, Badge.tsx (VerdictBadge, StatusBadge, CountPill), Card.tsx, SegmentedControl.tsx,
      Tabs.tsx, Select.tsx, Checkbox.tsx, RadioCard.tsx, Switch.tsx, Dialog.tsx, Menu.tsx, TextField.tsx,
      Kbd.tsx, ProgressBar.tsx, VisuallyHidden.tsx, Icon.tsx (cesty SVG z návrhu), InfoNote.tsx, Stepper.tsx
    components/shell/
      AppShell.tsx, Sidebar.tsx, ShopSwitcher.tsx, MonitoringSidebarCard.tsx, AccountMenu.tsx,
      MobileHeader.tsx, MobileBottomNav.tsx, TopBar.tsx (hledání Ctrl K, zvoneček, Nová kontrola),
      OnboardingHeader.tsx (Účet → Pripojenie → Rozsah a spustenie), StickyActionBar.tsx
    components/auth/
      AuthLayout.tsx (tmavý panel), MagicLinkForm.tsx, PasswordForm.tsx, GoogleButton.tsx,
      ResendCountdown.tsx, UiLanguageSwitcher.tsx, ConfirmMagicLink.tsx
    components/onboarding/
      ShopUrlField.tsx, SourceModeOptions.tsx, ConnectionComparisonTable.tsx, PlatformPicker.tsx,
      ConnectorCredentialsForm.tsx, ConnectedShopCard.tsx, MarketsFieldset.tsx, LanguageVersionsSummary.tsx,
      ScopeCards.tsx, ModulesFieldset.tsx, OrderSummary.tsx, useQuote.ts, WhatHappensNext.tsx,
      VersionsTable.tsx, VersionComparison.tsx, CheckoutReturn.tsx
    components/runs/  RunProgress.tsx, useRunEvents.ts
    components/overview/  KpiCards.tsx, PriorityPagesList.tsx, QuickAnswers.tsx, MonitoringSummary.tsx, CoverageNote.tsx
    components/fixes/  FixesHeader.tsx, FixTabs.tsx, LanguageVersionFilter.tsx, GroupFixesBanner.tsx,
                       FixPageRow.tsx, FindingRow.tsx, FindingCountChips.tsx
    components/review/ ReviewHeader.tsx, ViewModeToggle.tsx, ChangeList.tsx, ChangeCard.tsx,
                       ChangeCardCollapsed.tsx, GroupChangeCard.tsx, QuestionChangeCard.tsx, DiffText.tsx,
                       AlternativesChips.tsx, PlaceholderInput.tsx, UnchangedParagraphsDivider.tsx,
                       ChangeDetailAside.tsx, LegalRefsByCountry.tsx, RecheckStatus.tsx, ShortcutsHelp.tsx,
                       useReviewShortcuts.ts, ReviewActionBar.tsx, CopyTextButton.tsx, PublishButton.tsx
    components/group-fix/ GroupFixForm.tsx, GroupFixModeRadios.tsx, GroupFixSamples.tsx,
                          GroupFixCheckSummary.tsx, GroupFixPagesList.tsx
    components/monitoring/ MonitoringCards.tsx, NotificationToggles.tsx, PageChangesTable.tsx, MonitoringEmptyState.tsx
    components/evidence/   EvidenceStats.tsx, EvidenceTable.tsx
    components/protocol/   ProtocolDownloadLink.tsx
    components/billing/    ShopSubscriptionsTable.tsx, ShopSubscriptionCard.tsx, PaymentCardPanel.tsx,
                           TierStrip.tsx, InvoicesPanel.tsx, InvoicesListMobile.tsx, InvoiceFilters.tsx,
                           InvoicesZipButton.tsx, CancelMonitoringDialog.tsx
    lib/api/  schema.d.ts (generováno), client.ts, server.ts, problem.ts, sse.ts, query-keys.ts
    lib/session.ts (getMe, aktivní tenant a e-shop, cookie eg_ctx), lib/permissions.ts (role → povolené akce)
    lib/rule-texts.ts (katalog a skládání ICU), lib/clipboard.ts, lib/log.ts (pino + redact)
  messages/sk.json, messages/cs.json      klíče obrazovek: auth.*, onboarding.*, overview.*, fixes.*, review.*,
                                          groupFix.*, monitoring.*, evidence.*, protocol.*, billing.*, shell.*, errors.*, run.*
  tests/
    mock-api/server.ts, mock-api/handlers/{auth,onboarding,quote,orders,runs,overview,fixes,review,
             groupFix,monitoring,evidence,protocols,billing,shell}.ts, mock-api/data/bylinkovo.ts
    unit/  useQuote.test.ts, useRunEvents.test.ts, useReviewShortcuts.test.ts, problem.test.ts,
           DiffText.test.tsx, OrderSummary.test.tsx, InvoicesPanel.test.tsx, MarketsFieldset.test.tsx,
           rule-texts.test.ts, tokens-contrast.test.ts
    e2e/   login.spec.ts, onboarding.spec.ts, quote-and-checkout.spec.ts, run-progress.spec.ts,
           overview.spec.ts, fixes.spec.ts, review.spec.ts, group-fix.spec.ts, monitoring.spec.ts,
           evidence-protocol.spec.ts, billing.spec.ts, navigation.spec.ts, a11y.spec.ts,
           mobile-layout.spec.ts, visual.spec.ts (snímky 1440 a 390)
.github/workflows/ci.yml          (když ještě neexistuje, tato změna ho založí jen s jobem web: lint, typecheck, test, api:check, build, secrets:scan, test:e2e; změna 17 ho rozšíří)
```
