# Delta for Public-website

## ADDED Requirements

### Requirement: Payload CMS v aplikaci Next.js se schématem cms
Systém MUST provozovat Payload CMS 3 ve stejné aplikaci Next.js jako web a aplikaci, s administrací na `/admin` a REST na `/cms-api`. Obsah MUST ukládat v PostgreSQL výhradně do schématu `cms` přes roli `eshopguard_cms`, která MUST NOT mít přístup k žádnému jinému schématu. GraphQL a telemetrie Payload MUST být vypnuté.

#### Scenario: Role CMS nepřečte data zákazníků
- GIVEN aplikace je připojená k databázi `eshopguard` jako `eshopguard_cms`
- WHEN test spustí `SELECT 1 FROM iam.users LIMIT 1` a `SELECT 1 FROM shop.shops LIMIT 1`
- THEN oba dotazy skončí chybou `42501` (permission denied)
- AND zápis a čtení v tabulkách schématu `cms` projdou

#### Scenario: Migrace na prázdné databázi
- GIVEN prázdné schéma `cms`
- WHEN se spustí `pnpm --dir web cms:migrate` s `CMS_DATABASE_URI` role `eshopguard_cms`
- THEN migrace Payload založí tabulky jen ve schématu `cms` a skončí kódem 0
- AND ve schématech `iam`, `shop`, `content`, `checks`, `fixes`, `billing`, `usage`, `ops` nevznikne žádný objekt

#### Scenario: Vypnutý GraphQL
- GIVEN běžící aplikace
- WHEN někdo pošle `POST /cms-api/graphql`
- THEN odpověď je 404
- AND Payload neodesílá telemetrii (`telemetry: false`)

### Requirement: Vydání webu sk a cz s vlastním obsahem
Systém MUST vést obsah webu po vydáních `sk` (sk-SK) a `cz` (cs-CZ) jako samostatné dokumenty s povinným polem `edition`, ne jako překlad polí. Stránka jednoho vydání MUST NOT zobrazit obsah jiného vydání jako náhradu. Vydání ve stavu `preview` MUST mít `noindex` a MUST NOT být v sitemap. Vydání `hidden` MUST vracet 404.

#### Scenario: Stejná adresa ve dvou vydáních
- GIVEN stránka `cennik` existuje ve vydání `sk` a ve vydání `cz` neexistuje
- WHEN návštěvník otevře `/cz/cennik`
- THEN odpověď je 404 v češtině
- AND slovenský obsah se v českém vydání neukáže

#### Scenario: Jedinečná adresa v rámci vydání
- GIVEN vydání `sk` už má stránku se `slug` `o-nas`
- WHEN redaktor uloží další stránku vydání `sk` se stejným `slug`
- THEN Payload uložení odmítne s chybou u pole `slug`
- AND stránka `o-nas` ve vydání `cz` uložit jde

#### Scenario: České vydání ve stavu preview
- GIVEN vydání `cz` má v `web/src/i18n/editions.ts` stav `preview`
- WHEN vyhledávač načte `/cz`
- THEN odpověď obsahuje `<meta name="robots" content="noindex">` a hlavičku `X-Robots-Tag: noindex`
- AND `/cz/sitemap.xml` ani index sitemap neobsahují žádnou adresu `/cz`

### Requirement: Adresy vydání a výběr vydání
Systém MUST podporovat adresy vydání buď předponou (`/sk`, `/cz`), nebo vlastními doménami podle `SITE_ROUTING`, bez změny kódu. Na adrese bez vydání MUST vybrat vydání v pořadí: doména → cookie `NEXT_LOCALE` (výslovná volba) → `Accept-Language` → `sk`. Systém MUST NOT vybírat vydání podle IP adresy ani podle hlaviček země. Přepínač vydání MUST vést na spárovanou stránku druhého vydání, jinak na jeho úvod.

#### Scenario: Jazyk prohlížeče bez cookie
- GIVEN `SITE_ROUTING=prefix`, návštěvník nemá cookie `NEXT_LOCALE` a prohlížeč posílá `Accept-Language: cs-CZ,cs;q=0.9`
- WHEN otevře `/`
- THEN dostane přesměrování 307 na `/cz`
- AND hlavička `CF-IPCountry: SK` ani jiná hlavička země výsledek nezmění

#### Scenario: Zapamatovaná volba má přednost
- GIVEN návštěvník dříve v přepínači zvolil „Slovensko“ (cookie `NEXT_LOCALE=sk-SK`) a prohlížeč posílá `Accept-Language: cs`
- WHEN otevře `/`
- THEN dostane přesměrování na `/sk`
- AND volba vydržela i po zavření prohlížeče (cookie s platností 1 rok)

#### Scenario: Režim domén
- GIVEN `SITE_ROUTING=domains`, `SITE_DOMAIN_SK=eshopguard.sk`, `SITE_DOMAIN_CZ=eshopguard.cz`
- WHEN návštěvník otevře `https://eshopguard.cz/`
- THEN dostane české vydání bez předpony v adrese a s `<html lang="cs-CZ">`
- AND odkazy uvnitř vydání předponu nemají

#### Scenario: Přepínač bez spárované stránky
- GIVEN návštěvník je na článku vydání `sk` bez `hreflangGroup`
- WHEN v přepínači zvolí „Česko“
- THEN přejde na úvod vydání `cz` a cookie `NEXT_LOCALE` je `cs-CZ`
- AND u spárované stránky by přešel přímo na její českou verzi

### Requirement: hreflang a sitemap po vydáních
Systém MUST uvádět `hreflang` jen mezi zveřejněnými stránkami nebo články se stejnou hodnotou `hreflangGroup` ve více vydáních, s `x-default` na vydání `sk`. Automatické hlavičky alternativ next-intl MUST být vypnuté. Systém MUST pro každé vydání ve stavu `live` vydat vlastní sitemap jen se zveřejněnými dokumenty a s `lastmod`. `robots.txt` MUST zakázat `/app`, `/admin`, `/cms-api`, `/api`, `/eg-internal` a `/cms-preview`.

#### Scenario: Spárované stránky
- GIVEN úvod `sk` a úvod `cz` mají `hreflangGroup = home` a obě vydání jsou `live`
- WHEN vyhledávač načte `/sk`
- THEN hlavička stránky obsahuje `<link rel="alternate" hreflang="sk-SK" href=".../sk">`, `hreflang="cs-CZ"` na `/cz` a `hreflang="x-default"` na `/sk`
- AND odpověď nemá hlavičku `Link` s alternativami od middleware

#### Scenario: Článek jen v jednom vydání
- GIVEN článek existuje jen ve vydání `sk`
- WHEN se vykreslí
- THEN nemá žádný `hreflang` na vydání `cz`
- AND je v `/sk/sitemap.xml` a není v `/cz/sitemap.xml`

#### Scenario: Koncept v sitemap
- GIVEN stránka `novinky-2027` je uložená jen jako koncept
- WHEN se vygeneruje `/sk/sitemap.xml`
- THEN stránka v ní není
- AND po zveřejnění se objeví s `lastmod` rovným času zveřejnění bez nového nasazení

### Requirement: Bloky úvodní stránky podle návrhu Main
Systém MUST skládat stránky webu z bloků CMS, které odpovídají návrhu `Main.dc.html`:
- hlavička: logo, kotvy menu, přepínač vydání, „Prihlásiť sa“, „Vyskúšať zadarmo“;
- `hero`: odznak, nadpis, úvod, pole adresy e-shopu s tlačítkem, rámeček ukázky zdarma, tři výhody a ilustrační karta „Návrh opravy textu“;
- `howItWorks` se 3 kroky;
- `connection` s tabulkou platforem a dvěma kartami (feed, adresa webu);
- `whatWeCheck` se 4 kartami a odkazem na právní předpis;
- `pricing`;
- patička s upozorněním a odkazy.

Rozměry, barvy a písma MUST odpovídat návrhu. Formulář adresy MUST vést na `/app/login?from=<vydání>&shop=<adresa>`.

#### Scenario: Úvod podle návrhu
- GIVEN obsah `sk` je naplněný ze seedu podle `Main.dc.html`
- WHEN se `/sk` vykreslí na šířce 1440 px
- THEN snímek obrazovky odpovídá schválenému výchozímu snímku (nadpis 60/64 px Bricolage Grotesque 700, pozadí `#F6F5F1` a `#FFFFFF` se střídají, tlačítka `#0E5A52`)
- AND axe nenahlásí porušení WCAG 2.2 A ani AA

#### Scenario: Ukázka zdarma z úvodu
- GIVEN návštěvník vydání `cz` zadá do pole adresy `https://bylinkovo.cz` a zvolí tlačítko
- WHEN se odešle formulář
- THEN prohlížeč přejde na `/app/login?from=cz&shop=https%3A%2F%2Fbylinkovo.cz`
- AND neplatná adresa (např. `bylinkovo`) formulář neodešle a ukáže chybu u pole v jazyce vydání

#### Scenario: Ilustrační karta není ovládací prvek
- GIVEN úvod obsahuje kartu „Návrh opravy textu“ s tlačítky „Upraviť“ a „Prijať“
- WHEN uživatel prochází stránku klávesou Tab
- THEN prvky karty nedostanou fokus a odečítač je čte jako obrázek s popisem (ilustrace)
- AND nikdo je nemůže zaměnit za funkční tlačítka

### Requirement: Ceník s cenami z API
Systém MUST zobrazovat ceny, měnu, pásma, slevu za počet e-shopů a platnost ceníku jen z veřejného endpointu nabídky trhu (změna 12). CMS MUST NOT obsahovat žádnou částku. Validace bloku `pricing` MUST odmítnout text s částkou a měnou nebo se zástupným `[CENA]`. Bez platných dat (API nedostupné při prvním vykreslení, nebo po `validUntil`) MUST ceník ukázat bez čísel s textem `unavailableText`.

#### Scenario: Ceny z API
- GIVEN API vrátí pro trh `sk` pásma s `maxProducts` 500, 2000, 5000, 20000 a `null`, ceny v EUR a žádnou slevu
- WHEN se vykreslí blok `pricing`
- THEN řádky zní „E-shop do 500 produktov“, „do 2 000 produktov“, …, „viac ako 20 000 produktov – dohodou“ a ceny jsou z API ve formátu `Intl` (`39 €`)
- AND řádek o slevě od 3. e-shopu se nezobrazí

#### Scenario: Částka v textu CMS
- GIVEN redaktor napíše do textu karty sledování „už od 9 € mesačne“
- WHEN dokument uloží
- THEN Payload uložení odmítne s chybou u pole, že ceny se berou z ceníku aplikace
- AND koncept s chybou nejde zveřejnit

#### Scenario: Ceník vypršel při výpadku API
- GIVEN stránka byla vykreslena s ceníkem, který má `validUntil` 31. 12. 2026 23:59, a API je od té doby nedostupné
- WHEN návštěvník otevře stránku 1. 1. 2027
- THEN klientská část ceníku čísla schová a ukáže `unavailableText` s odkazem na ukázku zdarma
- AND žádná cena ze starého ceníku se nezobrazí

#### Scenario: Okamžitá obnova po zveřejnění ceníku
- GIVEN změna 12 zveřejní nový ceník trhu `sk`
- WHEN zavolá `POST /eg-internal/revalidate` s tajemstvím `REVALIDATE_SECRET` a tagem `offer:sk`
- THEN další návštěva `/sk` ukáže nové ceny
- AND požadavek bez tajemství nebo s tagem mimo povolené `offer:*` a `cms:*` vrátí 401 nebo 400 a nic neobnoví

### Requirement: Koncepty, náhled, verze a zveřejnění bez nasazení
Systém MUST u stránek, článků, menu a patičky:
- ukládat koncepty s automatickým ukládáním;
- vést historii verzí (nejvýš 50 na dokument) s obnovením a plánovaným zveřejněním;
- nabízet náhled konceptu přihlášenému redaktorovi.

Zveřejnění MUST se na webu projevit bez nového sestavení a nasazení. Náhled MUST mít `noindex` a MUST NOT být dostupný bez relace CMS.

#### Scenario: Změna textu bez nasazení
- GIVEN redaktor změní nadpis úvodu `sk` a uloží koncept
- WHEN návštěvník otevře `/sk`
- THEN vidí původní nadpis, zatímco náhled redaktora ukazuje nový s pruhem „Náhľad konceptu“
- AND po „Zverejniť“ ukáže `/sk` nový nadpis do 5 s bez sestavení aplikace

#### Scenario: Obnovení předchozí verze
- GIVEN úvod `sk` má 3 zveřejněné verze
- WHEN redaktor v administraci obnoví verzi 2 a zveřejní ji
- THEN web ukáže obsah verze 2
- AND verze 3 zůstane v historii

#### Scenario: Náhled bez relace CMS
- GIVEN někdo bez přihlášení do CMS, nebo jen se zákaznickou relací aplikace, otevře `/cms-preview?collection=pages&id=…`
- WHEN požadavek dorazí
- THEN odpověď je 401 a draft mode se nezapne
- AND koncept se neukáže

### Requirement: Administrace jen pro pozvané s omezeným přístupem
Systém MUST pustit do `/admin` a `/cms-api` jen pozvané účty kolekce `cms-users`, které nejsou zákaznické, a jen z povolených adres. Caddy nastaví hlavičku `X-EG-Admin-Allowed: 1` jen pro povolené adresy a aplikace ji MUST zkontrolovat znovu. Registrace MUST být vypnutá. Role a pozvánky MUST spravovat jen role `admin`. Po 5 neúspěšných přihlášeních MUST být účet zamčený na 15 minut.

#### Scenario: Nepovolená adresa
- GIVEN požadavek na `/admin` přijde bez hlavičky `X-EG-Admin-Allowed: 1`
- WHEN ho zpracuje middleware
- THEN odpověď je 404
- AND hlavička poslaná přímo klientem se nepočítá, protože Caddy ji u příchozích požadavků maže

#### Scenario: Redaktor nespravuje účty
- GIVEN přihlášený uživatel CMS má roli `editor`
- WHEN se pokusí založit nového uživatele CMS
- THEN Payload akci odmítne (403)
- AND redaktor může upravovat jen dokumenty vydání ze svého pole `editions`

#### Scenario: Zámek po špatných heslech
- GIVEN účet CMS `redaktor@eshopguard.sk`
- WHEN někdo zadá 5× špatné heslo
- THEN šesté přihlášení i se správným heslem odmítne s informací o zamčení na 15 minut
- AND zákaznický účet aplikace se stejným e-mailem do administrace nepustí

### Requirement: Obchodní podmínky a ochrana soukromí mimo CMS
Systém MUST zobrazovat obchodní podmínky a zásady ochrany soukromí jako aktuální verzovaný dokument z API s číslem verze a datem účinnosti, ne z CMS. Adresy těchto dokumentů MUST být v CMS rezervované. Při nedostupném API MUST stránka vrátit 503 bez obsahu, nikdy starou kopii. Odkazy v patičce MUST vést na tyto trasy v jazyce vydání.

#### Scenario: Aktuální verze dokumentu
- GIVEN API vrátí obchodní podmínky trhu `sk` ve verzi 3 účinné od 1. 11. 2026
- WHEN návštěvník otevře `/sk/obchodne-podmienky`
- THEN stránka ukáže text verze 3 s „Verzia 3, účinná od 1. 11. 2026“
- AND odkaz „Obchodné podmienky“ v patičce vede sem

#### Scenario: Rezervovaná adresa v CMS
- GIVEN redaktor založí stránku vydání `cz` se `slug` `obchodni-podminky`
- WHEN ji uloží
- THEN Payload uložení odmítne s vysvětlením, že dokument je verzovaný v aplikaci
- AND web dál zobrazuje dokument z API

#### Scenario: API dokumentů nedostupné
- GIVEN endpoint dokumentů vrací 503
- WHEN návštěvník otevře `/cz/ochrana-soukromi`
- THEN odpověď je 503 se stránkou „Dokument je dočasne nedostupný“ v jazyce vydání
- AND žádný text dokumentu ze staré cache se neukáže

### Requirement: Články, menu, patička a SEO
Systém MUST spravovat v CMS pro každé vydání:
- menu (položky s kotvou, stránkou nebo adresou `https:`, texty „Prihlásiť sa“ a hlavního tlačítka);
- patičku (upozornění a odkazy);
- články (titulek, perex, obrázek s povinným popisem, obsah, datum);
- údaje SEO stránek a článků (titulek, popis, obrázek pro sdílení).

Obrázky MUST přijímat jen JPEG, PNG a WebP do 5 MB, bez SVG. Do patičky MUST vždy patřit upozornění, že EshopGuard nenahrazuje právní poradenství.

#### Scenario: SEO údaje stránky
- GIVEN úvod `sk` má v CMS titulek SEO, popis a obrázek pro sdílení
- WHEN vyhledávač načte `/sk`
- THEN stránka má `<title>`, `<meta name="description">`, `og:image` a kanonickou adresu `/sk` (nebo doménu vydání)
- AND bez vyplněného titulku SEO se použije titulek stránky

#### Scenario: Obrázek SVG
- GIVEN redaktor nahraje do médií soubor `logo.svg`
- WHEN nahrání odešle
- THEN Payload soubor odmítne s chybou typu souboru
- AND obrázek JPEG bez vyplněného popisu (`alt`) uložit nejde

#### Scenario: Patička bez upozornění
- GIVEN redaktor smaže text upozornění v patičce vydání `cz`
- WHEN chce patičku zveřejnit
- THEN Payload zveřejnění odmítne, protože pole `disclaimer` je povinné
- AND web dál ukazuje dříve zveřejněnou patičku
