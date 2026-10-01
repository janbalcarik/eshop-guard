# Konektory e-shopových platforem: co pushují, co se musí dotazovat, co jde zapsat

Rešerše z 1. 10. 2026, jen oficiální dokumentace (bez přihlášení). „neověřeno“ znamená, že to dokumentace neříká nebo se to nepodařilo potvrdit. Zdroje jsou na konci.

## Hlavní zjištění

1. **Změny produktů pushují čtyři platformy z pěti:** Shoptet (webhooky produktů od 29. 7. 2026 v betě), Upgates, WooCommerce a Shopify. BiznisWeb nepushuje nic.
2. **Změny stránek a článků nepushuje nikdo.** Výjimky:
   - Shoptet má webhook jen na sekce článků;
   - WooCommerce má obecné `action.*`, ale použitelnost je neověřená.

   Stránky a články se proto musí dotazovat „změněno od“, kde to jde, jinak procházet.
3. **Texty šablony** (hlavička, patička, bannery, lišta výhod):
   - API je nevrací u Shoptetu, BiznisWebu ani u klasické šablony WooCommerce;
   - Upgates je ukáže jen u testovací verze grafiky;
   - jen Shopify dává soubory šablony.

   Procházení webu proto zůstává nutné i u e-shopů s konektorem.
4. **Webhookům nejde věřit jako jedinému zdroji.** Každá platforma umí zprávu ztratit:

   | Platforma | Jak se zpráva ztratí |
   |---|---|
   | Shoptet | 3 pokusy po 15 minutách a konec, bez ID pro odstranění duplicit |
   | Upgates | Překročení timeoutu 3 s bere jako úspěšné doručení |
   | WooCommerce | Neopakuje, po 5 selháních webhook vypne |
   | Shopify | 8 pokusů za 4 h, pak odběr smaže. Pořadí ani doručení nezaručuje a samo doporučuje dorovnávání |

   Webhook je proto rychlá cesta. Za zdroj pravdy slouží pravidelný dotaz „změněno od“.
5. **Háček „před uložením“ nemá žádná platforma.** Jediná výjimka je WooCommerce přes vlastní plugin instalovaný u obchodníka. Kontrola tedy proběhne až po uložení. Před zveřejněním jen tehdy, když obchodník uloží produkt jako skrytý nebo koncept:

   | Platforma | Stav konceptu |
   |---|---|
   | Shoptet | `visibility=hidden` |
   | Upgates | `active_yn` |
   | WooCommerce | draft |
   | Shopify | DRAFT |
   | BiznisWeb | přes API skrýt nejde |
6. **Přístup je u CZ/SK platforem obchodní věc, ne technická:**
   - Shoptet doplněk vyžaduje smlouvu a schválení (odpověď do 4 týdnů), privátní API má jen tarif Premium;
   - Upgates doplněk se schvaluje a má pravidla pro AI doplňky;
   - BiznisWeb zapisuje texty jen s partnerskou smlouvou a placeným balíčkem u klienta.

## Srovnání

| | Shoptet | Upgates | BiznisWeb | WooCommerce | Shopify |
|---|---|---|---|---|---|
| **Přístup pro SaaS** | Doplněk: OAuth při instalaci, neomezený token, krátkodobé API tokeny po 30 min. Privátní token jen Premium | API uživatel přes Basic auth: příplatek 100 Kč/měs a limity tarifu. Ověřený doplněk: bez poplatku a limitu počtu volání, přísná pravidla | Token jen v tarifech Biznis a Premium. Zápis jen s partnerským klíčem a klientovým „Partner Package“ | Klíč přes `/wc-auth/v1/authorize` (read_write). Stránky WordPressu jen s Application Password | Aplikace s OAuth: veřejná (schválení v App Store) nebo custom pro jeden obchod. Jen GraphQL |
| **Push produktů** | ano: product/productVariant create/update/delete a hromadné (beta) | ano: produkty, kategorie, štítky… create/update/delete | **ne** | ano: product.created/updated/deleted/restored | ano: PRODUCTS_CREATE/UPDATE/DELETE, COLLECTIONS_* |
| **Push stránek a článků** | ne (jen sekce článků) | ne | ne | ne (action.* neověřeno) | ne |
| **Push šablony** | eshop:design (nastavení vzhledu) | ne | ne | ne | THEMES_* (úprava souborů neověřeno) |
| **Obsah zprávy** | ID; volitelně celý detail (`sendPayload: full`), ne u hromadných a smazání | jen ID (až 5 000 položek, jen poslední stav) | – | celý objekt (u smazání ID) | celý objekt (lze zúžit) |
| **Podpis** | HMAC-SHA1, hlavička `Shoptet-Webhook-Signature` | nezdokumentován (údaje v URL) | – | HMAC-SHA256, `X-WC-Webhook-Signature` | HMAC-SHA256, `X-Shopify-Hmac-Sha256` |
| **Potvrdit do** | 4 s | 1 s spojení, 3 s celkem | – | 60 s | 1 s spojení, 5 s celkem |
| **Opakování** | 3× po 15 min, pak neaktivní | každých 5 min do úspěchu, ale timeout = úspěch | – | žádné, po 5 selháních vypnut | 8× za 4 h, pak odběr smazán |
| **Pořadí a duplicity** | pořadí ne, ID pro duplicity ne | jen poslední stav, pořadí create → update → delete | – | neověřeno; stejný argument se do 600 s nepošle znovu | pořadí ne; ID duplicit `X-Shopify-Webhook-Id` |
| **Dotaz „změněno od“** | `/products/changes` (30 dní), `changeTimeFrom`, asynchronní snapshot JSONL | `last_update_time_from` u produktů, článků i kategorií | **ne**, jen procházet vše | `modified_after` u produktů i stránek | `updated_at` v dotazu, bulk operace |
| **Limity** | 3 souběžná spojení na token; leaky bucket 200, odtok 10/s, zvlášť pro doplněk × e-shop | API uživatel 10–100/h a 100–1 500/den podle tarifu; 3 souběžné | 2 000 operací/min na token, 25–50 tis. denně | v API žádné (záleží na hostingu) | 100–2 000 bodů/s podle tarifu |
| **Čtení textů** | produkt (název, doplňkový název, krátký a dlouhý popis, meta, příznaky, parametry), kategorie, stránky, články; **šablona ne** | produkt po jazycích, štítky, parametry, články, kategorie; šablona jen testovací | produkt, kategorie, novinky; **textové bloky stránek ne**; šablona ne | produkt, kategorie, stránky; klasická šablona ne | produkt, kolekce, stránky, články, soubory šablony |
| **Zápis textů** | PATCH produkt, kategorie, stránka, článek; batch JSONL | PUT po 100; **produkt bez kódu nejde upravit** | jen s partnerským klíčem; viditelnost produktu nejde | PUT produkt, batch po 100; stránky WordPressu | productUpdate, collectionUpdate, pageUpdate, articleUpdate; šablona jen s výjimkou |
| **Koncept / skrytí** | visibility=hidden, visible=false | active_yn (neviditelnost pro zákazníka neověřena) | produkt přes API skrýt nejde | draft, pending, private | DRAFT, UNLISTED |
| **Háček před uložením** | nenalezen; iframe v administraci jen pro nastavení doplňku | nenalezen; iframe OPEN | nenalezen | jen plugin u obchodníka (`wp_insert_post_data`) | žádný blokující; bloky na detailu produktu |
| **Jazyky** | parametr `language`; oddělené CZ a SK e-shopy = oddělené instalace | texty v poli podle jazyka | `lang_code` čtení, `lang_id` zápis | WPML `lang`; Polylang jen `/wp/v2` | API překladů, bez webhooku |

## Podrobnosti, na kterých záleží

**Shoptet**
- Instalace doplňku: na volání musíme odpovědět do 5 s a hned vyměnit jednorázový kód za token. Volání přichází z 185.184.254.0/24.
- Rozšíření práv musí schválit Shoptet i e-shop. Při pozastavení doplňku API odmítá, ale fakturace běží. Od 18. 9. 2026 se webhooky bez práva samy odregistrují.
- Na jeden event a instalaci jde jen 1 URL. Log notifikací je dostupný 7 dní (`/api/webhooks/notifications`).
- Shoptet sám doporučuje kontrolovat duplicity a denně dorovnávat.
- `product:update` spouští úpravy v administraci i PATCH. Import posílá hromadné eventy zhruba po 300 položkách.
- Při zápisu varianty se musí poslat všechny parametry, jinak se smažou. Stránka se zapisuje do pole `description`, ale čte z `content`.
- Dvě stejné zápisové žádosti po sobě: druhá dostane 423 (zámek 5 s).
- Iframe v administraci smí podle pravidel sloužit jen k nastavení doplňku, takže kontrolní obrazovka v administraci by s pravidly kolidovala.
- Poplatky a provize za doplněk: neověřeno.

**Upgates**
- Pravidla pro AI doplňky:
  - pracovat jen přes API;
  - před každou změnou ukázat, co a na kolika položkách se změní, a nechat to potvrdit;
  - zajistit zálohu a vrácení;
  - získat souhlas se zpracováním třetí stranou (OpenAI);
  - produkty stáhnout jednou a dál pracovat přes webhooky;
  - každou novou funkci nechat schválit.

  Náš návrh (náhled, schválení, protokol, vrácení) tomu odpovídá. Zda vadí pravidelný dotaz „změněno od“ jako pojistka, je potřeba ověřit při schvalování.
- Webhook musí odpovědět do 1 s, jinak se zpráva ztratí bez opakování. Když se API uživateli změní práva, webhook se deaktivuje.
- Limity API uživatele (Bronze 100 volání denně) jsou na průběžnou práci malé, cesta přes doplněk je nutná.

**BiznisWeb**
- Změny se dají zjistit jen projitím všech produktů: 30 na stránku, každá vnořená entita je operace, denní kvóta 25–50 tis. operací. Noční projití ano, hodinové ne.
- Bez partnerské smlouvy jen čtení. Viditelnost produktu přes API změnit nejde, takže kontrola konceptu před zveřejněním není možná.

**WooCommerce**
- Webhook se po 5 selháních vypne a znovu se nezapne sám.
- Stránky a příspěvky WordPressu nejdou klíčem WooCommerce, potřebují Application Password (vlastní autorizační tok WordPressu).
- Kontrolu před uložením umožní jen náš plugin u obchodníka (`wp_insert_post_data`, `woocommerce_before_product_object_save`).

**Shopify**
- Povinné GDPR webhooky pro aplikace v App Store. Od 1. 4. 2025 jen GraphQL. Veřejné aplikace musí do 1. 1. 2027 přejít na expirující offline tokeny.
- Zúžené payloady, které vyjdou stejně, Shopify slučuje a pozdější zahodí. Proto se má vždy zahrnout `updated_at`.
- Bulk operace (JSONL, konec ohlásí webhook) se nepočítají do limitu: vhodné pro úvodní načtení velkého katalogu.

**Maďarsko (k plánu na rok 2027)**
- Shoprenter: webhooky jen pro objednávky, newsletter a opuštěný košík, pro produkty ne. Zápis popisu produktu jde.
- UNAS: automatické procesy jen pro objednávky a zákazníky (balíček VIP), ne pro produkty. `getProduct` filtruje podle data změny, `setProduct` zapisuje.

## Co z toho plyne pro EshopGuard

1. **Tři cesty ke změnám, podle platformy:** webhook (rychle) + dotaz „změněno od“ (pojistka, např. každou hodinu a v noci) + procházení webu (šablona, odznaky a stránky tam, kde je API nevrací). U BiznisWebu jen noční projití a porovnání otisků textů.
2. **Přijímač webhooků musí odpovědět do 1 s** (Upgates). Ověří podpis, uloží událost a vrátí 200. Zpracování běží až ve workeru.
3. **Pořadí zpráv se nezaručuje, obsah bývá jen ID.** Worker proto vždy načte aktuální stav přes API a nespoléhá na obsah zprávy. Události jednoho produktu za pár minut se sloučí.
4. **Hlídač webhooků:** pravidelně ověří, že odběry existují a jsou aktivní (Shopify maže, WooCommerce vypíná, Shoptet a Upgates ruší při změně práv), a obnoví je. Výpadek se ukáže v aplikaci.
5. **Limit na každý konektor zvlášť** podle pravidel platformy (Shoptet 3 spojení a 10/s na e-shop, BiznisWeb denní kvóta…), odděleně od limitu Jevu.
6. **Zápis oprav:**
   - před zápisem znovu načíst text a porovnat s textem, ze kterého vznikl nález, jinak konflikt a nová kontrola;
   - uložit původní znění pro vrácení;
   - u Upgates produkt bez kódu a u BiznisWebu bez partnerské smlouvy nabídnout „Kopírovať text“.
7. **„Kontrola při uložení“ (bod 14 seznamu návrhů) je ve skutečnosti „kontrola do pár minut po uložení“.** Před zveřejněním jen u produktu uloženého jako skrytý nebo koncept (ne BiznisWeb). Skutečnou kontrolu před uložením by dal jen plugin pro WooCommerce.

## Zdroje

Shoptet: https://api.docs.shoptet.com/shoptet-api/openapi (OpenAPI bundle cdd90bf, 1. 10. 2026), https://developers.shoptet.com/api/documentation/installing-the-addon/, …/getting-api-access-token/, …/creating-the-addon/, …/pausing-and-resuming-addon/, …/webhooks/, …/rate-limiter/, …/addon-settings-in-shoptet-administration/, https://developers.shoptet.com/webhooks-with-payload-in-shoptet-api/, …/product-webhooks-pilot/, …/correct-api-usage/, …/asynchronous-requests/, …/api-release-news-from-september-4-2026/, …/api-release-news-from-july-29-2026/, https://developers.shoptet.com/home/premium/private-api/, https://doplnky.shoptet.cz/chcete-delat-vlastni-doplnky, https://doplnky.shoptet.cz/cizi-jazyky, https://podpora.shoptet.cz/jazykove-mutace/

Upgates: https://docs.upgates.com/api/intro, …/api/rate-limiting, …/webhooks/intro, …/webhooks/products, …/api-reference/webhooky, …/api-reference/produkty, …/api-reference/produkty-seznamy, …/api-reference/clanky, …/api-reference/kategorie, …/api-reference/grafika-editor-kodu, …/api-reference/jazyky, …/api/best-practices, https://www.upgates.cz/cenik, https://www.upgates.cz/a/api-dokumentace-doplnku, https://www.upgates.cz/a/api-dokumentace-aktivace-doplnku

BiznisWeb: https://www.biznisweb.sk/api/docs/schema.graphql (23. 9. 2026), https://www.biznisweb.sk/a/1268/volanie-api, …/a/1270/token-api, …/a/1256/kvoty-limity-api, …/a/1257/prevadzkove-naklady, …/a/1359/integratori-api, https://www.biznisweb.sk/cennik, https://www.byznysweb.cz/partner-api

WooCommerce a WordPress: https://woocommerce.github.io/woocommerce-rest-api-docs/ (zdroj: github.com/woocommerce/woocommerce-rest-api-docs, části authentication, webhooks, products), https://woocommerce.com/document/webhooks/, https://github.com/woocommerce/woocommerce (class-wc-webhook.php, wc-webhook-functions.php, class-wc-rest-authentication.php), https://developer.wordpress.org/rest-api/, https://make.wordpress.org/core/2020/11/05/application-passwords-integration-guide/, https://developer.wordpress.org/reference/hooks/wp_insert_post_data/, https://wpml.org/documentation/related-projects/woocommerce-multilingual/using-wordpress-rest-api-woocommerce-multilingual/, https://polylang.pro/documentation/support/developers/rest-api/

Shopify: https://shopify.dev/docs/apps/build/authentication-authorization, …/access-tokens/offline-access-tokens, https://shopify.dev/docs/api/usage/access-scopes, https://shopify.dev/docs/apps/build/compliance/privacy-law-compliance, https://shopify.dev/docs/apps/launch/distribution, …/shopify-app-store/app-store-requirements, https://shopify.dev/docs/apps/build/webhooks, …/webhooks/subscribe/https, …/webhooks/customize/modify-payloads, https://shopify.dev/docs/api/admin-graphql/latest/enums/WebhookSubscriptionTopic, https://shopify.dev/docs/apps/build/apis/graphql-admin/rate-limits, https://shopify.dev/docs/api/usage/bulk-operations/queries, https://shopify.dev/docs/api/admin-extensions/latest/targets, https://shopify.dev/docs/apps/build/markets/manage-translated-content

Maďarsko: https://doc.shoprenter.hu/api/webhook.html, https://doc.shoprenter.hu/api/product_description.html, https://unas.hu/tudastar/api/automata-folyamatok, https://unas.hu/tudastar/api/termekek
