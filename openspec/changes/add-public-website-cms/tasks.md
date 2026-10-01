# Tasks

Cesty jsou relativně ke kořeni repozitáře `eshop-guard/`.

Pořadí:
1. skupina 2 změny 13 (kostra `web/`);
2. skupina 1 této změny (jazykové základy);
3. obrazovky změny 13 a skupiny 2–11 této změny souběžně.

## 1. Jazykové základy pro web i aplikaci

- [ ] 1.1 Nastavit vydání a směrování:
  - přidat `next-intl`;
  - `web/src/i18n/editions.ts`: vydání `sk` (sk-SK, `/sk`, `live`) a `cz` (cs-CZ, `/cz`, `preview`), `fallbackEdition`, `editionToUiLocale`;
  - `web/src/i18n/routing.ts`: `defineRouting` s `localePrefix.prefixes`, nebo `domains` podle `SITE_ROUTING`, `alternateLinks: false`, cookie `NEXT_LOCALE` na 1 rok;
  - `web/src/i18n/navigation.ts`.
- [ ] 1.2 Napsat `web/src/i18n/request.ts`: jazyk zpráv z vydání, nebo pro `/app` z `users.locale` → `eg_ui_locale` → `Accept-Language` → `sk`; `timeZone: 'Europe/Bratislava'`. Napsat `web/src/i18n/formats.ts` (formáty z tabulky v `design.md`, měna s `trailingZeroDisplay: 'stripIfInteger'`).
- [ ] 1.3 Založit `web/messages/sk.json` a `web/messages/cs.json` s jmennými prostory `common`, `errors`, `run`, `notifications`, `site`, `shell` a společnými texty. Klíče obrazovek doplňuje změna 13. Do `eslint.config.mjs` přidat `react/jsx-no-literals` pro `src/app/(site)`, `src/app/(app)` a `src/components`.
- [ ] 1.4 Napsat `web/src/lib/rule-texts.ts`: načtení katalogu `GET /api/rule-texts` s cache podle `ruleSetId`, složení přes `intl-messageformat`, náhrada `ruleId` a hlášení `rule_text_missing`.
- [ ] 1.5 Napsat test úplnosti:
  - `web/tests/i18n/completeness.test.ts`: stejné klíče, stejné argumenty ICU přes `@formatjs/icu-messageformat-parser`, platná syntaxe, žádné prázdné zprávy, zapnutý jazyk jen s úplnými texty;
  - `web/tests/i18n/plural.test.ts`: kategorie z `Intl.PluralRules`;
  - skript `pnpm i18n:check` v CI.
- [ ] 1.6 Test:
  - `tests/i18n/formats.test.ts` (`1 460` s U+00A0, `1. 10. 2026`, `8:19` v UTC i v `America/New_York`, `29 €`, `29,90 €`, `690 Kč`);
  - `tests/i18n/locale-resolution.test.ts` (pořadí pro web a aplikaci, `de-DE` → `sk`);
  - `tests/i18n/error-codes.test.ts` (každý kód chyby ze snímku OpenAPI má `errors.<code>` v obou jazycích);
  - `tests/i18n/rule-texts.test.ts` (český text, slovenský odkaz na zákon, chybějící text).

  Hotovo, když úmyslně odstraněný český klíč shodí `pnpm i18n:check`.

## 2. Payload CMS v aplikaci (schéma cms, role, migrace)

- [ ] 2.1 Přidat balíčky Payload 3 (`payload`, `@payloadcms/next`, `@payloadcms/db-postgres`, `@payloadcms/richtext-lexical`, `@payloadcms/plugin-seo`, `@payloadcms/storage-s3`, `@payloadcms/email-nodemailer`) ve verzi kompatibilní s verzí Next.js ze změny 13. Obalit konfiguraci `withPayload(withNextIntl(...))` v `web/next.config.ts`.
- [ ] 2.2 Napsat `web/src/payload.config.ts`:
  - `routes` `/admin` a `/cms-api`, `graphQL.disable`, `telemetry: false`, `localization: false`;
  - `postgresAdapter` se `schemaName: 'cms'`, `push: false`, `migrationDir: 'src/cms/migrations'`;
  - `lexicalEditor` s omezenými funkcemi, `csrf`, `email`, `admin.livePreview` (1440 a 390 px).

  Založit `src/app/(payload)/layout.tsx`, `(payload)/admin/[[...segments]]/page.tsx`, `(payload)/admin/importMap.js` a `(payload)/cms-api/[...slug]/route.ts`.
- [ ] 2.3 Napsat `deploy/sql/20_cms_schema.sql` (`CREATE SCHEMA IF NOT EXISTS cms AUTHORIZATION eshopguard_cms`, žádné `USAGE` na ostatní schémata), pokud to nedělá změna 3. Nastavit proměnné `CMS_DATABASE_URI`, `PAYLOAD_SECRET`, `CMS_MEDIA_*`, `SMTP_*` přes `.env.local` (mimo repozitář) a `web/.env.example` bez hodnot. Vytvořit první migraci `pnpm payload migrate:create initial` a skript `cms:migrate`.
- [ ] 2.4 Test: `web/tests/cms/db-isolation.test.ts` proti lokálnímu PostgreSQL s rolí `eshopguard_cms`:
  - `SELECT` z `iam.users`, `shop.shops`, `billing.invoices` skončí `42501`;
  - CRUD v `cms` projde;
  - `pnpm cms:migrate` na prázdném schématu skončí 0 a nezaloží nic mimo `cms`;
  - `POST /cms-api/graphql` vrátí 404.

## 3. Kolekce, bloky a validace

- [ ] 3.1 Napsat `web/src/cms/collections/CmsUsers.ts`:
  - auth s `maxLoginAttempts: 5`, `lockTime: 900000`, `tokenExpiration: 28800`, cookie `secure` a `sameSite: 'Strict'`;
  - pole `name`, `role` (`admin`/`editor`), `editions`;
  - bez registrace;
  - přístup v `src/cms/access/isAdmin.ts` a `canEditEdition.ts`.
- [ ] 3.2 Napsat `Pages.ts` a `Articles.ts`:
  - pole podle `design.md`;
  - `uniqueSlugPerEdition.ts`, `reservedSlug.ts` (`src/cms/reserved-slugs.ts`);
  - `versions: { drafts: { autosave: { interval: 2000 }, schedulePublish: true }, maxPerDoc: 50 }`;
  - čtení přes `publishedOrCmsUser.ts`;
  - `seoPlugin` pro obě kolekce.
- [ ] 3.3 Napsat `Media.ts` (`image/jpeg`, `image/png`, `image/webp`, `upload.limits.fileSize` 5 MB, povinné `alt`, `s3Storage` s předponou `cms/media/` a veřejnou adresou) a `SiteNavigation.ts` a `SiteFooter.ts` (jedinečné `edition`, povinné `disclaimer`, koncepty).
- [ ] 3.4 Napsat bloky `web/src/cms/blocks/Hero.ts`, `HowItWorks.ts`, `Connection.ts`, `WhatWeCheck.ts`, `Pricing.ts`, `RichText.ts` s poli a `anchorId` podle `design.md` a validace `noHardcodedPrices.ts` a `allowedTokens.ts` (seznam v `src/cms/tokens.ts`).
- [ ] 3.5 Napsat seed:
  - `web/src/cms/seed/sk.ts`: úvod `sk` s texty přesně z `Main.dc.html`, menu (Ako to funguje, Napojenie, Čo kontrolujeme, Cenník, Prihlásiť sa, Vyskúšať zadarmo), patička s upozorněním a odkazem Kontakt;
  - `seed/cz.ts`: stejná struktura vydání `cz` jako koncept s prázdnými texty k doplnění (nezveřejněné);
  - skript `pnpm cms:seed`.
- [ ] 3.6 Test: `web/tests/cms/collections.test.ts` a `validation.test.ts` (lokální API Payload nad testovací databází):
  - duplicitní `slug` ve vydání odmítnut, ve druhém vydání povolen;
  - rezervovaný `obchodni-podminky` odmítnut;
  - „od 9 € mesačne“ a „[CENA]“ v bloku `pricing` odmítnuty;
  - neznámý zástupce `{foo}` odmítnut;
  - SVG a obrázek bez `alt` odmítnuty;
  - `editor` nezaloží uživatele CMS;
  - patička bez `disclaimer` nejde zveřejnit.

## 4. Vydání, adresy a výběr vydání

- [ ] 4.1 Doplnit `web/src/middleware.ts` (společný se změnou 13):
  - `/app/*` → ochrana relace (změna 13);
  - `/admin` a `/cms-api` → bez `X-EG-Admin-Allowed: 1` odpověď 404 (ve vývoji výjimka jen pro `localhost` a `NODE_ENV=development`);
  - `/api`, `/eg-internal`, `/cms-preview`, `/healthz`, `/sitemap.xml`, `/robots.txt`, `/_next` a soubory beze změny;
  - zbytek `createMiddleware(routing)`.
- [ ] 4.2 Napsat `src/app/(site)/[locale]/layout.tsx`: `<html lang>` podle vydání, písma a tokeny ze změny 13, hlavička a patička z CMS, stav vydání (`preview` → `noindex` v metadatech a `X-Robots-Tag`, `hidden` → `notFound()`).
- [ ] 4.3 Postavit `src/components/site/EditionSwitcher.tsx` podle `Main.dc.html`:
  - tlačítko 40 px s ikonou glóbu a kódem vydání;
  - menu se dvěma `menuitemradio` (Slovensko · slovenčina, Česko · čeština), výška položek 48 px;
  - zaškrtnutí u aktuálního vydání;
  - přechod na spárovanou stránku přes `hreflangGroup`, jinak na úvod;
  - nastavení `NEXT_LOCALE`.
- [ ] 4.4 Test: `web/tests/e2e/site-edition.spec.ts`:
  - `/` + `Accept-Language: cs` → `/cz`;
  - cookie `NEXT_LOCALE=sk-SK` + `Accept-Language: cs` → `/sk`;
  - `CF-IPCountry` bez vlivu;
  - režim `domains` přes hlavičku `Host: eshopguard.cz` → `cs-CZ` bez předpony;
  - přepínač se spárovanou a nespárovanou stránkou;
  - `/admin` bez hlavičky → 404.

## 5. Stránky webu podle návrhu Main

- [ ] 5.1 Doplnit na plátno a nechat schválit (bez kódu) mobilní variantu `Main` (390 px) a stránky seznamu a detailu článku, které na plátně nejsou. Zapsat verzi plátna do `web/DESIGN_VERSION`.
- [ ] 5.2 Napsat `src/app/(site)/[locale]/page.tsx` (stránka `slug = home` vydání), `[...slug]/page.tsx`, `src/components/site/RenderBlocks.tsx` a čtení přes `getPayload` s `overrideAccess: false` a tagy `cms:<edition>` a `cms:<edition>:<slug>`.
- [ ] 5.3 Postavit `SiteHeader.tsx` (80 px, okraje 80 px, kotvy, „Prihlásiť sa“, „Vyskúšať zadarmo“ → `/app/login?from=`) a `Hero.tsx`:
  - odznak `#E3F0ED`;
  - H1 60/64 px Bricolage 700;
  - pole adresy 56 px s validací `type=url` → `/app/login?from=&shop=`;
  - rámeček ukázky zdarma, tři výhody;
  - `ExampleFixCard.tsx` jako ilustrace (`role="img"`, popis, prvky bez fokusu).
- [ ] 5.4 Postavit `HowItWorks.tsx` (3 sloupce, čísla 44 px), `Connection.tsx` (karta s rámečkem 2 px `#0E5A52`, tabulka platforem 150 px / 1fr / 1.4fr, dvě boční karty, poznámka), `WhatWeCheck.tsx` (4 karty `#FBFAF7`) a `SiteFooter.tsx` (upozornění, odkazy na dokumenty z API a z CMS).
- [ ] 5.5 Postavit stránky článků `src/app/(site)/[locale]/clanky/page.tsx` a `clanky/[slug]/page.tsx` podle schváleného návrhu 5.1 a mobilní rozvržení všech bloků (mřížky se skládají do jednoho sloupce pod 768 px).
- [ ] 5.6 Test: `web/tests/e2e/site-home.spec.ts`:
  - kotvy menu;
  - formulář adresy (platná → přesměrování, neplatná → chyba);
  - ilustrace bez fokusu;
  - axe 0 porušení;
  - snímek 1440 proti schválenému výchozímu snímku;
  - `site-mobile.spec.ts`: na 390 px `scrollWidth ≤ 390` a cíle ≥ 44 px.

## 6. Ceník z API

- [ ] 6.1 Napsat `web/src/lib/public-offer.ts`: `GET {API_INTERNAL_URL}/api/public/offer?market=` s typy z OpenAPI, `next: { revalidate: 300, tags: ['offer:<market>'] }`, při chybě výsledek `unavailable`.
- [ ] 6.2 Postavit `Pricing.tsx` (dvě karty podle návrhu, odznak „V cene: prvý mesiac sledovania zmien“, příklad) a klientský `PricingTiers.tsx`:
  - řádky ze šablon CMS a `tiers` z API, `null` → `onRequestLabel`;
  - částky přes `useFormatter`;
  - řádek slevy jen při `volumeDiscount`;
  - kontrola `validUntil` → `unavailableText`.
- [ ] 6.3 Napsat `src/app/eg-internal/revalidate/route.ts`: `POST`, tajemství `REVALIDATE_SECRET` v hlavičce, povolené tagy `offer:*` a `cms:*`, jinak 401/400. Zvenku ho blokuje Caddy (změna 17).
- [ ] 6.4 Test: `web/tests/unit/PricingTiers.test.tsx` a `tests/e2e/site-pricing.spec.ts`:
  - čísla z modelového API;
  - API nedostupné při vykreslení → bez čísel;
  - `validUntil` v minulosti → bez čísel;
  - bez slevy → řádek chybí;
  - `revalidate` s tajemstvím obnoví, bez tajemství 401.

## 7. Koncepty, náhled, verze a zveřejnění bez nasazení

- [ ] 7.1 Napsat `web/src/cms/hooks/revalidate.ts` (`afterChange` a `afterDelete` u `pages`, `articles`, `site-navigation`, `site-footer` → `revalidateTag('cms:<edition>')`, `revalidateTag('cms:<edition>:<slug>')`). Ověřit, že plánované zveřejnění (`schedulePublish`) běží v procesu `web` (`jobs.autoRun`) a volá stejný háček.
- [ ] 7.2 Napsat `src/app/(site)/cms-preview/route.ts` (`payload.auth({ headers })`, bez relace CMS 401, `draftMode().enable()`, přesměrování), čtení `draft: true` v draft mode, `PreviewBanner.tsx` „Náhľad konceptu“ a `X-Robots-Tag: noindex`.
- [ ] 7.3 Test: `web/tests/e2e/cms-publish.spec.ts` (`next build && next start`, ne vývojový server):
  1. redaktor změní H1 úvodu `sk` a uloží koncept → `/sk` beze změny, náhled s novým textem;
  2. zveřejnit → `/sk` s novým textem do 5 s;
  3. obnovit předchozí verzi → původní text;
  4. náhled se zákaznickou relací → 401.

## 8. SEO: hreflang, sitemap, robots, metadata

- [ ] 8.1 Napsat `generateMetadata` stránek a článků:
  - titulek a popis z `meta` (náhrada titulkem stránky), `og:image`, kanonická adresa;
  - `alternates.languages` jen pro zveřejněné dokumenty se stejnou `hreflangGroup` ve vydáních `live`;
  - `x-default` na `sk`.
- [ ] 8.2 Napsat sitemap a robots:
  - `src/app/(site)/sitemap.xml/route.ts`: index v režimu `prefix`, v režimu `domains` sitemap vydání podle hostitele;
  - `src/app/(site)/[locale]/sitemap.xml/route.ts`: jen zveřejněné stránky a články, `lastmod`, `xhtml:link` u spárovaných, jen vydání `live`, tagy `cms:<edition>`;
  - `src/app/robots.txt/route.ts`: zakázané cesty `/app`, `/admin`, `/cms-api`, `/api`, `/eg-internal`, `/cms-preview` a odkazy na sitemap.
- [ ] 8.3 Test: `web/tests/e2e/site-seo.spec.ts`:
  - úvody `sk` a `cz` spárované, `cz` ve stavu `live` v testovací konfiguraci;
  - článek jen v `sk` bez alternativ;
  - koncept není v sitemap, po zveřejnění ano;
  - vydání `preview` má `noindex` a není v sitemap;
  - odpověď nemá hlavičku `Link` od middleware;
  - `robots.txt` obsahuje všechny zakázané cesty.

## 9. Administrace a zabezpečení

- [ ] 9.1 Napsat `web/src/cms/scripts/create-admin.ts` (`pnpm cms:create-admin --email …`): založí účet `admin` bez hesla a pošle e-mail Payload pro nastavení hesla. Registrace přes administraci nejde.
- [ ] 9.2 Doplnit do `payload.config.ts` `csrf` (původy webu) a cookie relace administrace. Ověřit, že zákaznická cookie relace API nemá v administraci žádný účinek. Ověřit, že `/cms-api` se z webu nevolá (web čte přes lokální API) a že obrázky jdou z `CMS_MEDIA_PUBLIC_URL`.
- [ ] 9.3 Test: `web/tests/e2e/cms-admin-access.spec.ts`:
  - bez hlavičky `X-EG-Admin-Allowed` → 404 na `/admin` i `/cms-api/pages`;
  - s hlavičkou přihlášení redaktora;
  - 5 špatných hesel → zámek;
  - `editor` nezaloží uživatele;
  - zákaznická relace nedá přístup.

## 10. Obchodní podmínky a ochrana soukromí mimo CMS

- [ ] 10.1 Napsat `src/lib/legal-documents.ts` (`GET /api/public/legal-documents/{kind}?market=`, předpoklad podle proposal, K rozhodnutí bod 4) a `src/app/(site)/[locale]/(legal)/[document]/page.tsx`:
  - mapování adres `obchodne-podmienky`, `ochrana-sukromia` (sk) a `obchodni-podminky`, `ochrana-soukromi` (cz);
  - `LegalDocument.tsx` s verzí a datem účinnosti;
  - výpadek API → 503 bez obsahu;
  - odkazy v patičce z `messages` (`site.footer.terms`, `site.footer.privacy`).
- [ ] 10.2 Test: `web/tests/e2e/site-legal.spec.ts` (verze 3 z modelového API, 503 bez staré kopie, odkaz v patičce obou vydání). Rezervované adresy v CMS pokrývá 3.6.

## 11. Ověření

- [ ] 11.1 Proklikat web **slovensky** na lokálním prostředí (`next build && next start`, modelové API ceníku nebo API změny 12 v testovacím režimu):
  - úvod, kotvy, napojení, co kontrolujeme, ceník s čísly z API;
  - přepínač na „Česko“ a zpět;
  - formulář adresy → `/app/login?from=sk&shop=…`;
  - obchodní podmínky a ochrana soukromí.
- [ ] 11.2 Proklikat web **česky** s vydáním `cz` přepnutým v lokální konfiguraci na `live`: obsah z CMS, ceník v měně z API (Kč, pokud je ceník CZ zveřejněný, jinak „nedostupné“), `hreflang` mezi spárovanými stránkami, `/cz/sitemap.xml`.
- [ ] 11.3 Změnit text webu v CMS bez nasazení:
  1. v `/admin` změnit H1 úvodu `sk`;
  2. ověřit náhled;
  3. zveřejnit;
  4. ověřit změnu na `/sk` do 5 s bez sestavení;
  5. obnovit předchozí verzi.
- [ ] 11.4 Na šířce 390 px (Playwright a skutečný telefon) projít úvod, ceník a článek bez vodorovného posunu. Spustit `pnpm --dir web lint typecheck test i18n:check test:e2e --project=site-*` a `openspec validate add-public-website-cms`.
