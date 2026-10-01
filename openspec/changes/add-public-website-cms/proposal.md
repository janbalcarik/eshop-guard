# Proposal: Prezentační web s Payload CMS a jazykové základy webu i aplikace

## Intent

**Problém.**
- EshopGuard nemá prezentační web, kde by zákazník zjistil, co kontrolujeme, jak se napojit, kolik to stojí, a spustil ukázku 100 stránek zdarma.
- Texty webu by dnes musel měnit programátor v kódu a každá změna by znamenala nasazení.
- Aplikace (změna 13) potřebuje jazykové základy, tedy texty po jazycích, množná čísla a formáty čísel, dat a měn. Ty zatím nikdo nepostavil.

**Proč teď.**
- F7 v plánu implementace (databáze, část 8) má jako podmínku hotovosti: „text webu jde změnit v CMS bez nasazení“.
- Uživatel rozhodl 1. 10. 2026 (architektura, část 12):
  - web i aplikace jsou od začátku vícejazyčné (sk, cs);
  - texty webu se upravují v Payload CMS.
- Pilot začíná v říjnu až listopadu 2026 (strategie, část 5) a potřebuje vstupní stránku s ukázkou zdarma.

**Přínos.**
- Dvě vydání webu, slovenské `sk` (sk-SK) a české `cz` (cs-CZ). Každé má vlastní obsah, ne překlad: v Česku zatím neplatí stejné zákazy (EmpCo není převzatá) a cena může být v jiné měně.
- Obsah upravuje redaktor v Payload CMS:
  - koncepty, náhled na šířce 1440 a 390 px, historie verzí;
  - zveřejnění se na webu projeví bez nasazení.
- Ceny na webu jsou vždy ty, které účtuje aplikace: čtou se z API (`billing.price_tiers`, změna 12), CMS žádnou cenu nedrží.
- Vyhledávače dostanou správné `hreflang` jen mezi skutečně spárovanými stránkami a zvláštní sitemap pro každé vydání.
- Jazykové základy (next-intl, ICU, `Intl`, test úplnosti) slouží webu i aplikaci. Jazyk bez úplných textů nejde zapnout.
- Návštěvnost a konverzi webu zatím nejde změřit (neměřeno). Analytika není součástí této změny (K rozhodnutí bod 6).

**Fáze:** F7 (databaze-a-plan-implementace-2026-10-01.md, část 8).

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 2 (Frontend s Payload CMS; „do databáze smí jen CMS, a to jen do schématu `cms`“);
  - část 11, body 8 (domény) a 9 (nabídka na českém webu);
  - část 12: Čtyři různé věci (jazyk rozhraní ≠ vydání webu ≠ jazyk obsahu e-shopu ≠ fakturační trh), Prezentační web (technika, vydání, co je a není v CMS, adresy, výběr vydání, administrace), Aplikace (soubory zpráv, ICU, `Intl`, volba jazyka, kódy z API, kontrola úplnosti), Přidání dalšího trhu;
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 2 (role `eshopguard_cms`);
  - část 3.9 (`ref.markets.web_status`, `ref.locales.enabled`, schéma `cms`);
  - část 7 (`web/`, `messages/sk.json`, `messages/cs.json`);
  - F7;
- `strategie-a-cenik-2026-09-30.md`, část 3 (ukázka 100 stránek, pásma podle zveřejněných produktů);
- návrh UI: `Main.dc.html` (úvodní stránka: hlavička s přepínačem SK/CZ, úvod se 100 stránkami zdarma a ukázkou opravy, Ako to funguje, Napojenie s tabulkou platforem, Čo kontrolujeme, Cenník s příkladem, patička) a `Login.dc.html` (přepínač jazyka rozhraní).

## Scope

In scope:
- **Jazykové základy pro web i aplikaci** (požadavky „Texty rozhraní:“, skupina 1 v `tasks.md`, dělá se před obrazovkami změny 13):
  - next-intl, `web/messages/sk.json` a `web/messages/cs.json` (ICU), konfigurace vydání a jazyků;
  - formáty čísel, dat a měn přes `Intl`;
  - určení jazyka (vydání webu, `users.locale`, cookie `eg_ui_locale`, `Accept-Language`);
  - texty chyb z kódů API;
  - skládání textů pravidel z katalogu;
  - test úplnosti překladů.
- **Payload CMS 3 ve stejné aplikaci Next.js:**
  - skupina tras `(payload)`, administrace `/admin`, REST `/cms-api`;
  - GraphQL i telemetrie Payload vypnuté;
  - PostgreSQL, schéma `cms`, role `eshopguard_cms`, migrace Payload.
- **Obsah v CMS:**
  - kolekce `pages`, `articles`, `media`, `site-navigation`, `site-footer` a `cms-users`;
  - bloky podle `Main.dc.html`: `hero`, `howItWorks`, `connection`, `whatWeCheck`, `pricing`, `richText`;
  - pole SEO (titulek, popis, obrázek pro sdílení).
- **Vydání `sk` a `cz`:**
  - pole `edition` u každého dokumentu (vlastní obsah, ne lokalizace polí Payload);
  - adresy `/sk`, `/cz`, nebo vlastní domény, přepínané konfigurací;
  - výběr vydání: doména, pak zapamatovaná volba v cookie, pak jazyk prohlížeče, nikdy IP;
  - přepínač vydání v hlavičce.
- **SEO:**
  - `hreflang` jen mezi stránkami se stejnou skupinou `hreflangGroup`;
  - sitemap pro každé vydání, `robots.txt`;
  - `noindex` pro náhledy a vydání ve stavu `preview`.
- **Ceník z API:**
  - veřejný endpoint nabídky trhu (změna 12), obnova po 300 s a na vyžádání;
  - kontrola v CMS, že texty ceníku neobsahují částky;
  - ceny se neukážou, když nejsou platné nebo dostupné.
- **Koncepty a zveřejnění:**
  - koncepty s automatickým ukládáním, plánované zveřejnění, verze (50 na dokument), obnovení verze;
  - náhled konceptu (draft mode) a živý náhled v administraci;
  - zveřejnění se na webu projeví bez nasazení (`revalidateTag`).
- **Administrace:**
  - jen pozvané účty CMS oddělené od zákaznických, role `admin` a `editor`;
  - zámek po 5 špatných heslech;
  - přístup jen z povolených adres (Caddy ve změně 17 a druhá kontrola v aplikaci).
- **Obchodní podmínky a ochrana soukromí:** trasy webu, které zobrazují aktuální verzovaný dokument z API. V CMS nejsou a jejich adresy jsou v CMS rezervované.
- **Přechod z webu do aplikace:** „Vyskúšať zadarmo“, „Prihlásiť sa“ a formulář adresy v úvodu → `/app/login?from=sk|cz&shop=…`.

Out of scope:
- obrazovky aplikace (změna 13);
- ceník, objednávky a veřejný endpoint nabídky (změna 12), verzované obchodní podmínky a jejich API (K rozhodnutí bod 4);
- kalkulačka ceny na webu (K rozhodnutí bod 1);
- bloky „Výhody“ a „Časté otázky“ z architektury, které nejsou v návrhu `Main.dc.html` (K rozhodnutí bod 2), a stránky článků (návrh chybí, skupina 5);
- analytika návštěvnosti a lišta souhlasu s cookies (K rozhodnutí bod 6);
- obsah českého vydání (texty napíše uživatel, nebo copywriter podle rozhodnutí o nabídce na českém webu, architektura, část 11, bod 9). Tato změna založí strukturu českého vydání jako koncept;
- e-maily a dokumenty skládané backendem (změny 9, 11, 12);
- produkční Caddy, domény a certifikáty (změna 17).

## Approach

1. **Jazykové základy jako první.** Skupina 1 se dělá hned po kostře `web/` ze změny 13 (skupina 2). Obrazovky obou změn pak používají jen klíče zpráv. Test úplnosti hlídá oba jazyky od prvního klíče.
2. **Vydání je obsah, ne překlad.**
   - Payload běží s `localization: false`. Každý dokument má povinné pole `edition` (`sk`, `cz`) a jedinečný `slug` v rámci vydání.
   - Stránky dvou vydání spojuje jen volitelné pole `hreflangGroup`, takže web nikdy neukáže slovenský text v českém vydání jako náhradu.
   - Pozdější `at` si chybějící obsah vezme z `de` podle `fallbackEdition` v konfiguraci vydání (`web/src/i18n/editions.ts`).
3. **Směrování přes next-intl:**
   - jazyky webu `sk-SK` a `cs-CZ` s předponami `/sk` a `/cz` (`localePrefix.prefixes`), nebo `domains` podle `SITE_ROUTING`;
   - automatické `Link` hlavičky alternativ jsou vypnuté (`alternateLinks: false`), protože by tvrdily, že každá stránka existuje v obou vydáních;
   - `hreflang` se skládá v `generateMetadata` jen pro spárované zveřejněné stránky.
4. **Web čte CMS přes lokální API Payload na serveru** (`getPayload`, `overrideAccess: false`, bez uživatele, takže vidí jen zveřejněné). REST `/cms-api` potřebuje jen administrace a dostanou se k němu jen povolené adresy. Obrázky se servírují z veřejné části zvláštního bucketu `cms/media/*`, ne přes `/cms-api`.
5. **Ceny jen z API:**
   - blok `pricing` v CMS má jen texty a šablony popisků pásma (`do {maxProducts} produktov`);
   - čísla, měna, sleva a platnost přicházejí z `GET /api/public/offer?market=` (změna 12);
   - validace v CMS odmítne v textech ceníku částku s měnou i zástupný text `[CENA]`;
   - bez platných dat se ceník ukáže bez čísel s odkazem na ukázku zdarma (fail-closed).
6. **Zveřejnění bez nasazení.** Payload běží ve stejném procesu jako Next.js, takže háčky `afterChange` a `afterDelete` volají přímo `revalidateTag('cms:<edition>')`. Na jednom serveru (změna 17) je jedna instance `web`. Při více instancích bude potřeba sdílená cache Next.js (K rozhodnutí bod 7).
7. **Design podle `Main.dc.html`.**
   - Rozměry, barvy a písma jsou stejné jako v aplikaci (tokeny ze změny 13).
   - Mobilní varianta úvodní stránky a stránky článků nejsou na plátně. Nejdřív se doplní návrh a schválí (zásada uživatele), teprve pak se staví.

## Dependencies

- **13 `add-web-app-frontend`:** skupina 2 (kostra `web/`, tokeny, písma, komponenty `ui`) před skupinou 1 této změny. Aplikace pak používá jazykové základy z této změny a přebírá z webu `?from=` a `?shop=`.
- **12 `add-billing-and-invoicing`:** veřejný endpoint nabídky trhu bez přihlášení (`tiers`, `currency`, `validFrom`, volitelně `validUntil`, `volumeDiscount`, `trialDays`). Volání `POST /eg-internal/revalidate` po zveřejnění ceníku je volitelné, jinak platí obnova po 300 s.
- **9 `add-identity-and-tenants-api`:** `users.locale`, `PATCH /api/me`, kódy chyb v OpenAPI. Verzované obchodní podmínky a zásady ochrany soukromí (K rozhodnutí bod 4).
- **6 `add-multi-jurisdiction-rules-and-rule-texts`:** katalog textů pravidel po jazycích (požadavky „Texty pravidel:“) a jeho endpoint.
- **3 `add-multitenant-data-model`:** role `eshopguard_cms` a `ref.markets`, `ref.locales`. Když schéma `cms` nezakládá změna 3, založí ho tato změna (`deploy/sql/20_cms_schema.sql`).
- **17 `add-single-server-operations`:**
  - Caddy: omezení `/admin` a `/cms-api` na povolené adresy s hlavičkou `X-EG-Admin-Allowed`, blokace `/eg-internal/*` zvenku;
  - kontejner `cms-migrate`;
  - bucket pro média CMS.

## Done when

- **Proklikání webu** slovensky a česky (skupina „Ověření“ v `tasks.md`):
  - úvod, kotvy menu, přepínač vydání se zachováním spárované stránky;
  - ceník s čísly z modelového API;
  - přechod na `/app/login?from=…&shop=…`.
- **Text webu změněný v CMS bez nasazení:**
  1. redaktor změní nadpis H1 úvodní stránky `sk`;
  2. web ho do zveřejnění nezobrazí, v náhledu ano;
  3. po zveřejnění ho web ukáže do 5 s bez nového sestavení;
  4. obnovení předchozí verze vrátí původní text.
- **Mobil:** na šířce 390 px nemají stránky webu vodorovný posun, po schválení mobilního návrhu úvodní stránky.
- **Testy jazyků:** test úplnosti (`tests/i18n/completeness.test.ts`), test formátů a test kódů chyb projdou. Úmyslně chybějící český klíč test shodí.
- **Izolace CMS:** role `eshopguard_cms` nepřečte žádnou tabulku mimo schéma `cms` (`tests/cms/db-isolation.test.ts`, chyba `42501`).
- **SEO:** `hreflang` je jen u spárovaných stránek a sitemap každého vydání obsahuje jen zveřejněné dokumenty (`tests/e2e/site-seo.spec.ts`).
- **Validace:** `openspec validate add-public-website-cms` projde.

## K rozhodnutí

1. **Kalkulačka ceny na webu.** Návrh `Main.dc.html` ji nemá a říká „Presnú cenu uvidíte po ukážke 100 stránok zadarmo, ešte pred platbou.“ Možnosti:
   - **A:** žádná, jen tabulka pásem (doporučeno na start);
   - **B:** posuvník podle počtu produktů nad veřejnými pásmy z API, bez volání e-shopu a bez nákladů;
   - **C:** odhad z adresy e-shopu (sitemap). Zatěžuje cizí web, má náklady a riziko zneužití, a pásmo se stejně určuje podle produktů ve verzích s vlastními texty, které zjistí až ukázka.

   Doporučení A, později B po návrhu UI. Cena by vždy pocházela z API.
2. **Bloky „Výhody“ a „Časté otázky“** jmenuje architektura (část 12, Co je v CMS), v návrhu `Main.dc.html` nejsou. Postaví se až po návrhu na plátně.
3. **Domény** (architektura, část 11, bod 8): `eshopguard.sk` + `eshopguard.cz`, nebo jedna doména s `/sk` a `/cz`. Změna umí obojí přes `SITE_ROUTING=prefix|domains`, výchozí je `prefix`.
4. **Kde žijí verzované obchodní podmínky a zásady ochrany soukromí.** Architektura je řadí do aplikace („k objednávce se ukládá odsouhlasená verze“), ale tabulka pro ně v databázi (část 3) chybí. Předpoklad je veřejný endpoint `GET /api/public/legal-documents/{kind}?market=`, který dodá změna 12 nebo 9. Do té doby vrací stránka 503 „Dokument je dočasne nedostupný“, žádnou kopii v CMS.
5. **Pořadí výběru vydání.** Zadání uvádí „doména → jazyk prohlížeče → cookie“. Cookie ale nese výslovnou volbu uživatele z přepínače a musí mít přednost před jazykem prohlížeče, jinak by přepnutí na kořenové adrese nefungovalo. Tato změna proto používá pořadí doména (nebo předpona v adrese) → cookie `NEXT_LOCALE` → `Accept-Language` → `sk`. IP adresa se nepoužívá nikdy.
6. **Analytika a lišta souhlasu s cookies.** Web zatím ukládá jen nezbytné cookies (`NEXT_LOCALE`, relace administrace, draft mode), takže lišta souhlasu není potřeba. Při přidání analytiky je nutná lišta s volbou „odmítnout“ a návrh UI.
7. **Více instancí `web`.** `revalidateTag` platí pro cache jedné instance. Při přechodu na víc serverů (architektura, část 7.1) je potřeba sdílený `cacheHandler` (Redis nebo souborový systém), jinak by se zveřejnění projevilo jen na jedné instanci.
8. **Mobilní varianta `Main` a stránky článků (seznam a detail)** nejsou na plátně. Doplnit a schválit před kódem (skupina 5, úkol 5.1).
9. **Obsah českého vydání a nabídka na českém webu** (architektura, část 11, bod 9): prodávat kontrolu českých e-shopů, nebo kontrolu podle slovenského zákona pro české e-shopy prodávající na Slovensko? Do rozhodnutí je vydání `cz` jen koncept a není zveřejněné (`status: preview`, `noindex`).
10. **Počet stránek ukázky zdarma a délka zkušební doby na webu.** Když veřejný endpoint změny 12 vrátí `freeSamplePages` a `trialDays`, smí je texty CMS použít jako zástupce `{freeSamplePages}` a `{trialDays}`. Jinak je redaktor píše číslem, s rizikem, že se rozejdou s aplikací.
11. **Média CMS na S3.** Zvláštní bucket (nebo předpona) s veřejným čtením jen pro `cms/media/*`, oddělený od dat zákazníků. Poskytovatele určí změna 17.
