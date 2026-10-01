# Design: Prezentační web s Payload CMS a jazykové základy

> **Předloha UI:** `design/ui/` v kořeni repozitáře (úvodní stránka `Main.dc.html` a přihlášení `Login*.dc.html`); popis souborů v `design/ui/README.md`. Rozvržení, texty, barvy a stavy se přebírají odtud.


Cesty jsou relativně ke kořeni repozitáře `eshop-guard/` (po změně 1). Kostru `web/`, tokeny a písma zakládá změna 13 (skupina 2).

## Technical Approach

### Vydání a jazyky (`web/src/i18n/editions.ts`)

| Vydání | Jazyk webu (next-intl) | Jazyk zpráv | Předpona | Doména (`SITE_ROUTING=domains`) | Trh (`ref.markets.code`) | Stav | Záložní vydání |
|---|---|---|---|---|---|---|---|
| `sk` | `sk-SK` | `sk` | `/sk` | `SITE_DOMAIN_SK` | `sk` | `live` | – |
| `cz` | `cs-CZ` | `cs` | `/cz` | `SITE_DOMAIN_CZ` | `cz` | `preview` do rozhodnutí o obsahu (proposal, K rozhodnutí bod 9) | – |

Pozdější trhy přidají řádek (`de-DE`/`/de`, `de-AT`/`/at` se záložním `de`). Stav `live` se zveřejní v sitemap a indexuje. `preview` je dostupné s `noindex` a mimo sitemap. `hidden` vrací 404. Test porovná stav s `ref.markets.web_status` z API (`tests/i18n/editions-contract.test.ts`).

### Určení jazyka

| Kde | Pořadí |
|---|---|
| Web `(site)` | doména (režim `domains`) nebo předpona `/sk`, `/cz` → cookie `NEXT_LOCALE` (výslovná volba v přepínači) → `Accept-Language` (`sk*` → `sk-SK`, `cs*` → `cs-CZ`) → `sk-SK`. IP ani hlavičky zemí (`CF-IPCountry` apod.) se nečtou |
| Aplikace `(app)` | `users.locale` z `/api/me` (po přihlášení) → cookie `eg_ui_locale` (přepínač na 2a, `?from=` z webu) → `Accept-Language` → `sk` |
| E-maily a dokumenty | backend (změny 9, 11, 12), mimo tuto změnu |

### Formáty (`web/src/i18n/formats.ts`)

| Formát | Nastavení | sk | cs |
|---|---|---|---|
| `number.integer` | `Intl.NumberFormat(locale, {maximumFractionDigits: 0})` | `1 460` (nezlomitelná mezera U+00A0) | `1 460` |
| `number.percent` | `{style: 'percent', maximumFractionDigits: 0}` | `96 %` | `96 %` |
| `currency` | `{style: 'currency', currency, trailingZeroDisplay: 'stripIfInteger'}`, měna vždy z API | `29 €`, `29,90 €` | `690 Kč` |
| `dateTime.short` | `{day: 'numeric', month: 'numeric', year: 'numeric'}` | `1. 10. 2026` | `1. 10. 2026` |
| `dateTime.time` | `{hour: 'numeric', minute: '2-digit'}` | `8:19` | `8:19` |
| `dateTime.dayMonth` | `{day: 'numeric', month: 'numeric'}` | `28. 9.` | `28. 9.` |
| relativní den | ICU `select` nad `dayKind` (`today`, `yesterday`, `other`) | „Dnes 10:05“, „Včera 6:00“ | „Dnes 10:05“, „Včera 6:00“ |
| časové pásmo | `timeZone: 'Europe/Bratislava'` v `getRequestConfig`, stejné na serveru i v prohlížeči (bez rozdílu při hydrataci) | | |

### Množná čísla (ICU)

Slovenština i čeština mají kategorie `one`, `few`, `many` (desetinná čísla) a `other` (`Intl.PluralRules('sk').resolvedOptions().pluralCategories`). Každá zpráva s `plural` musí mít `one`, `few` a `other`. `many` je povinné tam, kde parametr může být desetinný. Příklad `fixes.chips.violation`:

```json
"violation": "{count, plural, one {# porušenie} few {# porušenia} other {# porušení}}"
```

### Katalog textů pravidel

`web/src/lib/rule-texts.ts` načte `GET /api/rule-texts?locale=&ruleSetIds=` (změna 6, cache podle `ruleSetId`, `immutable`) a složí text přes `IntlMessageFormat` s `params` nálezu. Odkazy na zákon (`legalRefs`) se zobrazí tak, jak je vrátí API, v jazyce zákona. Chybějící text ukáže `ruleId` a „Text pravidla chýba“ a zapíše chybu.

### Payload CMS (`web/src/payload.config.ts`)

| Nastavení | Hodnota |
|---|---|
| `routes` | `admin: '/admin'`, `api: '/cms-api'` |
| `graphQL` | `disable: true` |
| `telemetry` | `false` |
| `localization` | `false` (vydání je pole `edition`, ne lokalizace) |
| `db` | `postgresAdapter({ pool: { connectionString: CMS_DATABASE_URI }, schemaName: 'cms', push: false, migrationDir: 'src/cms/migrations' })`, role `eshopguard_cms` |
| `editor` | `lexicalEditor` jen s odstavcem, H2, H3, tučně, kurzívou, odkazem (interní stránka, kotva, `https:`), seznamy. Bez vlastního HTML |
| `collections` | `cms-users`, `pages`, `articles`, `media`, `site-navigation`, `site-footer` |
| `plugins` | `seoPlugin` (pages, articles), `s3Storage` (`media`, předpona `cms/media/`, `disablePayloadAccessControl: true`, adresa z `CMS_MEDIA_PUBLIC_URL`) |
| `email` | `nodemailerAdapter` (SMTP z prostředí) pro pozvánky a obnovu hesla redaktorů |
| `csrf` | původy webu (`SITE_ORIGIN`, domény vydání) |
| `secret` | `PAYLOAD_SECRET` (jen server) |
| `admin.livePreview` | šířky 1440 a 390 px, adresa `/cms-preview?collection=&id=` |

### Kolekce

| Kolekce | Pole | Přístup | Verze |
|---|---|---|---|
| `cms-users` (auth) | `name`, `role` (`admin`, `editor`), `editions` (vydání, která smí upravovat) | vytvořit, smazat a měnit role jen `admin`; bez registrace; `maxLoginAttempts: 5`, `lockTime: 15 min`, `tokenExpiration: 8 h`, cookie `Secure`, `SameSite=Strict` | – |
| `pages` | `edition`*, `title`*, `slug`* (jedinečný s `edition`, rezervované adresy zakázané), `hreflangGroup`, `layout` (bloky), `meta` (SEO) | čtení veřejnosti jen `_status = published`; úprava `editor` se svým vydáním | koncepty, autosave 2 s, plánované zveřejnění, 50 verzí |
| `articles` | `edition`*, `title`*, `slug`*, `excerpt`*, `coverImage`, `coverAlt`*, `body` (Lexical)*, `author`, `publishedAt`, `hreflangGroup`, `meta` | jako `pages` | jako `pages` |
| `media` (upload) | `alt`*, `edition` (prázdné = sdílené) | čtení veřejné ze S3; nahrání `editor` | – |
| `site-navigation` | `edition`* (jedinečné), `items[]` (`label`, cíl: kotva / stránka / `https:` odkaz), `loginLabel`*, `ctaLabel`* | jako `pages` | koncepty |
| `site-footer` | `edition`* (jedinečné), `disclaimer`*, `links[]` (`label`, cíl) | jako `pages` | koncepty |

Rezervované adresy (`src/cms/reserved-slugs.ts`): `obchodne-podmienky`, `ochrana-sukromia`, `obchodni-podminky`, `ochrana-soukromi`, `app`, `admin`, `cms-api`, `cms-preview`, `api`, `sitemap.xml`, `robots.txt`.

### Bloky podle `Main.dc.html` (`web/src/cms/blocks/`)

| Blok | Pole (texty v jazyce vydání) | Z API |
|---|---|---|
| `hero` | `badge` („Nové pravidlá platia od 27. 9. 2026“), `heading`*, `lead`*, `urlLabel`, `urlPlaceholder`, `ctaLabel`*, `freeSampleTitle`*, `freeSampleText`*, `bullets[]` (max. 3), `exampleCard` (`shopLabel`, `title`, `verdictLabel`, `originalBefore`, `originalHighlight`, `originalAfter`, `proposedBefore`, `proposedHighlight`, `proposedAfter`, `explanation`, `checkedLabel`, `editLabel`, `acceptLabel`), `anchorId` | `{freeSamplePages}` (proposal, K rozhodnutí bod 10) |
| `howItWorks` | `heading`*, `steps[3]` (`title`, `text`, `linkLabel`, `linkAnchor`), `anchorId` | – |
| `connection` | `heading`*, `lead`, `platformCard` (`title`, `badge`, `text`, `columnPlatform`, `columnChanges`, `columnPublishing`, `rows[]` (`platform`, `changes`, `publishing`), `note`), `sideCards[2]` (`icon`: `feed`/`web`, `title`, `text`, `bullets[]`), `footerStrong`, `footerText`, `anchorId` | – |
| `whatWeCheck` | `heading`*, `cards[4]` (`title`, `text`, `legalRef`), `anchorId` | – |
| `pricing` | `heading`*, `intro` (Lexical, tučné části), `analysisCard` (`stepLabel`, `title`, `text`, `includedBadge`, `note`, `ctaLabel`), `monitoringCard` (`stepLabel`, `title`, `text`, `volumeDiscountTemplate`, `note`, `ctaLabel`), šablony pásma (`tierFirstTemplate` „E-shop do {maxProducts} produktov“, `tierTemplate` „do {maxProducts} produktov“, `tierAboveTemplate` „viac ako {minProducts} produktov“, `onRequestLabel` „dohodou“, `perMonthTemplate` „{price} / mesiac“), `exampleTitle`, `exampleText`, `unavailableText`, `anchorId` | `tiers[]`, `currency`, `volumeDiscount`, `validFrom`, `validUntil`, `trialDays` |
| `richText` | `content` (Lexical) | – |

Validace (`web/src/cms/validation/`):
- `noHardcodedPrices.ts` odmítne v textech bloku `pricing` (a v `hero.freeSample*`) výraz `\d[\d\s.,]*\s?(€|Kč|EUR|CZK)` i `[CENA]`;
- `allowedTokens.ts` povolí jen zástupce ze `src/cms/tokens.ts` (`{maxProducts}`, `{minProducts}`, `{price}`, `{volumeDiscountPercent}`, `{volumeDiscountFromShop}`, `{freeSamplePages}`, `{trialDays}`), každý jen v polích, kde má zdroj;
- test ověří, že každý zástupce má pole v OpenAPI veřejné nabídky.

## Architecture Decisions

1. **AD-1 Payload ve stejné aplikaci, vlastní kořenový layout `(payload)`.** Žádný další kontejner (architektura, část 12). Styly webu a aplikace se do administrace nedostanou.
2. **AD-2 `localization: false`, vydání jako pole.**
   - Lokalizace polí v Payload předpokládá překlad téhož dokumentu a nabízí záložní jazyk. To by ukázalo slovenský text v českém vydání.
   - Vlastní dokument pro každé vydání odpovídá rozhodnutí „vlastní obsah, ne překlad“.
   - Spárování pro `hreflang` dělá jen `hreflangGroup`.
3. **AD-3 Lokální API pro web, REST jen pro administraci.**
   - Server webu volá `payload.find({ overrideAccess: false })` bez uživatele, takže pravidla přístupu platí a vidí jen zveřejněné.
   - `/cms-api` jde za Caddy omezit celé na povolené adresy.
   - Média jdou ze S3, ne přes `/cms-api`.
4. **AD-4 Role `eshopguard_cms` vlastní jen schéma `cms`.**
   - Migrace Payload (`payload migrate`) běží touto rolí, `push: false` ve všech prostředích.
   - Role nemá `USAGE` na žádné jiné schéma, takže chyba v CMS nedosáhne na data zákazníků.
5. **AD-5 next-intl s předponami a doménami.**
   - `localePrefix: {mode: 'always', prefixes: {'sk-SK': '/sk', 'cs-CZ': '/cz'}}` v režimu `prefix`;
   - `domains` s `localePrefix: 'as-needed'` v režimu `domains`;
   - `alternateLinks: false`.

   Přechod mezi režimy je změna proměnné `SITE_ROUTING` a domén, ne kódu.
6. **AD-6 Middleware rozdělí trasy.** `web/src/middleware.ts`:
   - `/app/*` → ochrana relace (změna 13);
   - `/admin`, `/cms-api` → kontrola hlavičky `X-EG-Admin-Allowed: 1`, kterou nastaví jen Caddy pro povolené adresy (jinak 404);
   - `/api/*`, `/eg-internal/*`, `/cms-preview`, `/healthz`, `/sitemap.xml`, `/robots.txt`, `/_next/*`, soubory → bez zásahu (jinak by je next-intl přesměroval na `/sk/…`);
   - zbytek → `createMiddleware(routing)`.
7. **AD-7 Ceník: data z API, texty z CMS.**
   - `fetch` s `next: { revalidate: 300, tags: ['offer:<market>'] }`;
   - interval vychází z toho, že ceník se mění řádově za měsíce a změna 12 smí obnovu vyžádat hned přes `/eg-internal/revalidate`;
   - klientský ostrůvek `PricingTiers` porovná `validUntil` s časem prohlížeče. Po vypršení čísla schová a ukáže `unavailableText`, protože stránka z cache mohla přežít změnu ceníku při výpadku API.
8. **AD-8 Zveřejnění bez nasazení.**
   - `afterChange` a `afterDelete` (`src/cms/hooks/revalidate.ts`) volají `revalidateTag('cms:<edition>')` a `revalidateTag('cms:<edition>:<slug>')`;
   - stránky webu jsou statické s tagy;
   - plánované zveřejnění Payload (`schedulePublish`) spouští stejný háček přes frontu úloh Payload. Ve vývoji se ověří, že úlohy Payload běží v procesu `web` (`jobs.autoRun`).
9. **AD-9 Náhled.**
   - `/cms-preview` ověří relaci CMS (`payload.auth({ headers })`), zapne `draftMode()` a přesměruje na stránku;
   - stránky v draft mode čtou `draft: true`, mají `X-Robots-Tag: noindex` a ukážou pruh „Náhľad konceptu“;
   - zákaznická relace náhled nezapne.
10. **AD-10 Právní dokumenty mimo CMS.** Trasy `obchodne-podmienky` a `ochrana-sukromia` (sk), `obchodni-podminky` a `ochrana-soukromi` (cz) vykreslí dokument z API s číslem verze a datem účinnosti. Adresy jsou v CMS rezervované. Při výpadku API se vrátí 503, nikdy stará kopie.

## Data Flow

### Zobrazení stránky webu

```
prohlížeč GET /cz/            (nebo https://eshopguard.cz/ v režimu domains)
  Caddy → web:3000
  middleware: next-intl → locale cs-CZ (předpona/doména) → hlavička bez Link alternates
  (site)/[locale]/page.tsx (statická, tagy cms:cz, cms:cz:home, offer:cz)
    payload.find(pages, where edition=cz, slug=home, draft=false, overrideAccess=false)
    payload.find(site-navigation|site-footer, edition=cz)
    fetch API_INTERNAL_URL/api/public/offer?market=cz  (revalidate 300)
    generateMetadata: title/description/og z meta; alternates jen pro stránky se stejnou hreflangGroup
  → HTML s <html lang="cs-CZ">
```

### Úprava textu v CMS

```
redaktor (povolená IP) → Caddy nastaví X-EG-Admin-Allowed: 1 → /admin
  upraví pages[sk/home].hero.heading → autosave konceptu (verze)
  „Náhľad“ → /cms-preview?collection=pages&id=… → draftMode → /sk s konceptem
  „Zverejniť“ → afterChange → revalidateTag('cms:sk'), revalidateTag('cms:sk:home')
  další požadavek na /sk → nové vykreslení → nový text (bez nasazení)
```

### Přechod z webu do aplikace

```
/sk úvod: adresa e-shopu + „Skontrolovať zadarmo“
  → /app/login?from=sk&shop=https%3A%2F%2Fbylinkovo.sk
  aplikace (změna 13): cookie eg_ui_locale=sk, adresa e-shopu předvyplněná v 3a po přihlášení
```

### Ceník

```
změna 12 zveřejní ceník → (volitelně) POST web:3000/eg-internal/revalidate {tag: offer:sk} s REVALIDATE_SECRET
jinak po ≤ 300 s nové vykreslení s novými tiers
prohlížeč: PricingTiers zkontroluje validUntil; po vypršení schová čísla
```

## File Changes

```
web/
  package.json                  + next-intl, payload, @payloadcms/next, @payloadcms/db-postgres,
                                  @payloadcms/richtext-lexical, @payloadcms/plugin-seo, @payloadcms/storage-s3,
                                  @payloadcms/email-nodemailer, @formatjs/icu-messageformat-parser, intl-messageformat;
                                  skripty payload, cms:migrate, cms:create-admin, i18n:check
  next.config.ts                withPayload(withNextIntl(config))
  messages/sk.json, messages/cs.json      jmenné prostory common, errors, run, site, shell, auth, onboarding, …
  src/i18n/editions.ts          konfigurace vydání (tabulka výše), editionToUiLocale, isEdition
  src/i18n/routing.ts           defineRouting (prefix | domains), alternateLinks: false, localeCookie NEXT_LOCALE
  src/i18n/request.ts           getRequestConfig: vydání → jazyk zpráv; aplikace → users.locale / eg_ui_locale / Accept-Language
  src/i18n/formats.ts           formáty čísel, měn, dat, časové pásmo
  src/i18n/navigation.ts        createNavigation(routing): Link, redirect, getPathname
  src/lib/rule-texts.ts         katalog textů pravidel a skládání ICU
  src/lib/public-offer.ts       čtení veřejné nabídky trhu z API (typy z OpenAPI)
  src/lib/legal-documents.ts    čtení verzovaných dokumentů z API
  src/middleware.ts             rozdělení tras (AD-6), sdílené se změnou 13
  src/payload.config.ts
  src/cms/collections/CmsUsers.ts, Pages.ts, Articles.ts, Media.ts, SiteNavigation.ts, SiteFooter.ts
  src/cms/blocks/Hero.ts, HowItWorks.ts, Connection.ts, WhatWeCheck.ts, Pricing.ts, RichText.ts
  src/cms/access/{publishedOrCmsUser,isAdmin,canEditEdition}.ts
  src/cms/validation/{noHardcodedPrices,allowedTokens,uniqueSlugPerEdition,reservedSlug}.ts
  src/cms/hooks/revalidate.ts
  src/cms/tokens.ts, src/cms/reserved-slugs.ts
  src/cms/migrations/           migrace Payload (schéma cms)
  src/cms/seed/sk.ts            obsah sk přesně podle Main.dc.html
  src/cms/seed/cz.ts            struktura cz jako koncept (texty doplní uživatel)
  src/cms/scripts/create-admin.ts   pozvání prvního admina (e-mail pro nastavení hesla)
  src/app/(payload)/layout.tsx, (payload)/admin/[[...segments]]/page.tsx, (payload)/admin/importMap.js,
  src/app/(payload)/cms-api/[...slug]/route.ts
  src/app/(site)/[locale]/layout.tsx       <html lang>, hlavička, patička, stav vydání (noindex/404)
  src/app/(site)/[locale]/page.tsx         úvod (pages, slug home)
  src/app/(site)/[locale]/[...slug]/page.tsx
  src/app/(site)/[locale]/clanky/page.tsx, clanky/[slug]/page.tsx   (po schválení návrhu)
  src/app/(site)/[locale]/(legal)/[document]/page.tsx              obchodné podmienky, ochrana súkromia
  src/app/(site)/[locale]/sitemap.xml/route.ts, src/app/(site)/sitemap.xml/route.ts (index), src/app/robots.txt/route.ts
  src/app/(site)/cms-preview/route.ts, src/app/eg-internal/revalidate/route.ts, src/app/healthz/route.ts
  src/components/site/SiteHeader.tsx, EditionSwitcher.tsx, Hero.tsx, ExampleFixCard.tsx, HowItWorks.tsx,
                      Connection.tsx, WhatWeCheck.tsx, Pricing.tsx, PricingTiers.tsx, PricingExample.tsx,
                      SiteFooter.tsx, RenderBlocks.tsx, PreviewBanner.tsx, LegalDocument.tsx
  tests/i18n/completeness.test.ts, formats.test.ts, plural.test.ts, error-codes.test.ts, locale-resolution.test.ts,
             editions-contract.test.ts, rule-texts.test.ts
  tests/cms/db-isolation.test.ts, collections.test.ts, validation.test.ts
  tests/e2e/site-edition.spec.ts, site-home.spec.ts, site-pricing.spec.ts, site-seo.spec.ts, cms-publish.spec.ts,
            cms-admin-access.spec.ts, site-legal.spec.ts, site-mobile.spec.ts
deploy/sql/20_cms_schema.sql    CREATE SCHEMA IF NOT EXISTS cms AUTHORIZATION eshopguard_cms; REVOKE na ostatních schématech
                                (jen když to nedělá změna 3)
```
