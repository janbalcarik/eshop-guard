# Delta for Shops-onboarding

## ADDED Requirements

### Requirement: Založení e-shopu z adresy
Systém MUST na `POST /api/t/{tenantId}/shops` založit e-shop z adresy:
- doména se normalizuje: malá písmena, bez `www.`, IDN jako punycode, `base_path`;
- e-shop je jedinečný podle (`tenant_id`, `domain`, `base_path`) mezi nesmazanými e-shopy;
- adresa MUST projít předběžnou kontrolou: jen `http`/`https`, port 80 nebo 443, žádná adresa IP, žádné vnitřní jméno (`localhost`, `.local`, `.internal`, `.lan`, `.home.arpa`), žádné přihlašovací údaje v adrese;
- API adresu samo nestahuje;
- stejnou doménu MAY mít jiný tenant jako samostatný e-shop;
- smazání je měkké (`deleted_at`) a MUST být odmítnuto u e-shopu s aktivním předplatným nebo běžícím během.

#### Scenario: Nový e-shop
- GIVEN admin tenanta bez e-shopu
- WHEN pošle `POST /shops` s `{ url: "https://www.Bylinkovo.sk/" }`
- THEN API odpoví `201` s `domain = bylinkovo.sk`, `basePath = /`, `status = draft`, `sourceMode = web`
- AND ve stejné transakci vznikne úloha `shop.detect_platform` s `priority = 0`
- AND audit obsahuje `shop.created`

#### Scenario: Vnitřní adresa
- GIVEN admin tenanta v produkčním prostředí
- WHEN pošle `POST /shops` s `{ url: "http://169.254.169.254/latest/meta-data" }`
- THEN API odpoví `400` s kódem `shop.url_not_allowed`
- AND nevznikne e-shop ani úloha

#### Scenario: Duplicita a stejná doména u jiného tenanta
- GIVEN tenant A má e-shop `vegis.sk`
- WHEN admin tenanta A přidá `https://vegis.sk` znovu a admin tenanta B přidá `https://vegis.sk`
- THEN tenant A dostane `409 shop.already_exists` s `params.shopId` stávajícího e-shopu
- AND tenant B dostane `201` a samostatný e-shop

#### Scenario: Smazání e-shopu s předplatným
- GIVEN e-shop s předplatným ve stavu `active`
- WHEN admin zavolá `DELETE /shops/{shopId}`
- THEN API odpoví `409 shop.subscription_active` a e-shop zůstane

### Requirement: Rozpoznání platformy z adresy
Systém MUST rozpoznat platformu e-shopu (`shoptet`, `upgates`, `biznisweb`, `woocommerce`, `shopify`, `other`, `unknown`) úlohou P0 ve workeru:
- úvodní stránka se stáhne přes stahovač s ochranou SSRF a s dodržením robots.txt;
- rozpoznání porovnává technické podpisy platformy (meta `generator`, hostitelé souborů, hlavičky, cookies, cesty API), nikdy text stránky;
- konflikt podpisů nebo jejich absence MUST dát `unknown` a klient zvolí platformu ručně;
- API MUST počkat na výsledek nejvýš `Api:InteractiveWaitSeconds` a pak vrátit stav `pending`.

#### Scenario: Shoptet poznaný jistě
- GIVEN úvodní stránka s `meta generator` Shoptet a soubory z `cdn.myshoptet.com`
- WHEN doběhne úloha `shop.detect_platform`
- THEN `shops.platform = shoptet` a `DetectionDto.confidence = certain`
- AND `DetectionDto.connector.available = false`, dokud není změna 15, a `recommendedSource = web`

#### Scenario: Konflikt podpisů
- GIVEN úvodní stránka se znaky WooCommerce i Shopify
- WHEN doběhne rozpoznání
- THEN `platform = unknown` a `signals` obsahuje oba podpisy
- AND klient na obrazovce 3b zvolí platformu přes `PUT /shops/{shopId}/platform`

#### Scenario: Pomalý web
- GIVEN e-shop odpovídá déle než 8 s
- WHEN admin založí e-shop
- THEN API odpoví `201` s `DetectionDto.status = pending`
- AND `GET /shops/{shopId}/detection` později vrátí výsledek

#### Scenario: Přesměrování na jinou doménu
- GIVEN `bylinkovo.cz` přesměrovává na `bylinkovo.sk`
- WHEN doběhne rozpoznání
- THEN `base_url` se nezmění a `DetectionDto.redirectedToOtherDomain.domain = bylinkovo.sk`

### Requirement: Způsob napojení e-shopu
Systém MUST umožnit napojení `web` (výchozí), `feed` (adresa feedu ve formátu `heureka` nebo `google`) a `connector`.
- `connector` MUST být přípustný jen tehdy, když má e-shop konektor ve stavu `connected`.
- Změna způsobu napojení během běžící analýzy MUST být odmítnuta.

#### Scenario: Feed místo webu
- GIVEN e-shop s neznámou platformou
- WHEN admin pošle `PUT /shops/{shopId}/source` s `{ mode: "feed", feed: { url: "https://eshop.sk/heureka.xml", format: "heureka" } }`
- THEN `sourceMode = feed` a v `shop.feeds` je řádek s adresou a formátem

#### Scenario: Konektor bez připojení
- GIVEN e-shop bez řádku v `shop.connectors`
- WHEN admin pošle `PUT /shops/{shopId}/source` s `{ mode: "connector" }`
- THEN API odpoví `409 shop.connector_not_connected` a `sourceMode` zůstane `web`

### Requirement: Ukázka zdarma jednou na doménu
Systém MUST spustit ukázku zdarma (`runs.kind = free_sample`) přes `RunService` ze změny 8:
- nárok domény v `shop.free_sample_claims`, běh a první úloha MUST vzniknout v jedné transakci;
- druhá ukázka na stejnou doménu (i od jiného tenanta) MUST být odmítnuta kódem `sample.already_used_for_domain` bez údaje o tom, kdo doménu použil;
- ukázku jde spustit jen u e-shopu ve stavu `draft`;
- počet ukázek na tenanta je omezený.

#### Scenario: První ukázka
- GIVEN e-shop `bylinkovo.sk` ve stavu `draft`, doména bez nároku
- WHEN admin pošle `POST /shops/{shopId}/sample`
- THEN API odpoví `202` se `SampleDto.status = queued` a `shops.status = sample`
- AND `shop.free_sample_claims` obsahuje doménu `bylinkovo.sk`

#### Scenario: Doménu už použil jiný tenant
- GIVEN tenant B má ukázku na `vegis.sk`
- WHEN admin tenanta A spustí ukázku pro svůj e-shop `vegis.sk`
- THEN API odpoví `409 sample.already_used_for_domain`
- AND odpověď neobsahuje ID ani název tenanta B
- AND nevznikne běh ani úloha

#### Scenario: Ukázka nad strop tenanta
- GIVEN tenant dnes spustil 5 ukázek (výchozí návrh)
- WHEN spustí šestou na jiné doméně
- THEN API odpoví `429 rate_limited` a nárok domény nevznikne

### Requirement: Stav a výsledky ukázky
Systém MUST na `GET /api/t/{tenantId}/shops/{shopId}/sample` vrátit:
- stav běhu ukázky, průběh a pozici ve frontě;
- po dokončení počet zkontrolovaných stránek a výčet nezkontrolovaných podle důvodu;
- počty nálezů podle skupiny (`text`, `assess`, `verify`) a závažnosti;
- nejvýš 5 nejzávažnějších nálezů seřazených podle nejpřísnějšího verdiktu;
- nejvýš 1 ukázku opravy, která prošla kontrolou.

Nálezy MUST být kódy a parametry. Vysvětlení skládá frontend z textů pravidel. Co nebylo zkontrolováno, MUST být uvedeno i u stavu `partial`.

#### Scenario: Hotová ukázka
- GIVEN ukázka skončila `finished`: 100 stránek, 11 nálezů, z toho 4 ve skupině `text`
- WHEN klient zavolá `GET /shops/{shopId}/sample`
- THEN `result.findingCounts.total = 11` a `result.findingCounts.byCheckability.text = 4`
- AND `result.topFindings` má 5 položek s `ruleId`, `params` a verdikty po zemích, bez textu vysvětlení
- AND `result.exampleFix` obsahuje původní a navržený text s `recheckStatus = ok`

#### Scenario: Částečná ukázka
- GIVEN ukázka skončila `partial`, 7 stránek zakázaných v robots.txt a 3 s textem vykreslovaným JavaScriptem
- WHEN klient zavolá `GET …/sample`
- THEN `result.notChecked.robotsBlocked = 7` a `result.notChecked.textNotLoaded = 3`

#### Scenario: Ukázka ještě neběžela
- GIVEN e-shop ve stavu `draft` bez běhu ukázky
- WHEN klient zavolá `GET …/sample`
- THEN API odpoví `404 sample.not_started`

### Requirement: Místa prodeje jen podporovaná a s důvodem
Systém MUST na obrazovce 3c ukázat jen země s podporou kontrol (`ref.markets.checks_status` ≠ `none`):
- každá s důvodem: kódy technických znaků a jen ověřené doslovné citace;
- předvybrané jsou země se silným důkazem nebo s doručením, obecné tvrzení („doručujeme do celej EÚ“) předvybrané není;
- nepodporované zjištěné země MUST zůstat uložené a MUST se nevracet.

Potvrzení (`PUT …/markets`) MUST:
- uložit aktivní a odmítnuté země s `confirmed_by` a `confirmed_at`;
- vyžadovat aspoň jednu zemi;
- odmítnout nepodporovanou zemi.

#### Scenario: Slovensko a Česko s důvodem
- GIVEN rozbor ukázky uložil SK (`strong`: sídlo Trenčín, `.sk`, €) a CZ (`strong`: česká verze s Kč; citace „Doprava do Českej republiky 3,90 €“ ověřená)
- WHEN klient zavolá `GET /shops/{shopId}/markets`
- THEN vrátí SK a CZ s `preselected = true`, znaky `seat`, `tld`, `currency` a citaci s `pageUrl`

#### Scenario: Neověřená citace se nevrátí
- GIVEN `shop_markets.evidence` obsahuje citaci s `verified = false`
- WHEN klient zavolá `GET …/markets`
- THEN citace v odpovědi není

#### Scenario: Polsko se neukáže
- GIVEN rozbor našel i Polsko a `ref.markets` pro `pl` má `checks_status = none`
- WHEN klient zavolá `GET …/markets`
- THEN Polsko v odpovědi není a řádek v `shop.shop_markets` zůstane `unsupported`

#### Scenario: Žádná země
- GIVEN e-shop s SK a CZ
- WHEN admin pošle `PUT …/markets` s `{ active: [] }`
- THEN API odpoví `400 markets.none_selected` a uložený stav se nezmění

### Requirement: Jazykové verze, potvrzení a vyloučení
Systém MUST vrátit jazykové verze e-shopu pro souhrn (3c) a podrobnosti (3d):
- jazyk popisů produktů, podíl přeložených produktů ze vzorku a počet produktů (nebo kód, proč je neznámý);
- zda se verze kontroluje a podle kterých zemí, spočítané stejným pravidlem jako cena;
- verze pro nepodporované trhy MUST NOT vracet.

Verze na jiné doméně ve stavu `needs_confirmation` MUST čekat na potvrzení klienta. Vyloučení verze v nastavení nesmí vyloučit poslední kontrolovanou verzi.

#### Scenario: Česká verze s přeloženými popisy
- GIVEN verze sk (hlavní, 5 834 produktů) a cs (`/cz/`, 5 834 produktů, popisy 96 % produktů ze vzorku česky) a aktivní země SK a CZ
- WHEN klient zavolá `GET /shops/{shopId}/languages`
- THEN obě verze mají `checked = true` a `jurisdictions = [sk, cz]`
- AND cs má `translatedShare = 0,96`

#### Scenario: Verze na jiné doméně
- GIVEN verze cs na `goodie.cz` ve stavu `needs_confirmation`
- WHEN admin pošle `POST /shops/{shopId}/languages/cs/confirmation` s `{ belongsToShop: false }`
- THEN verze má `status = excluded` a audit obsahuje `language.rejected_other_domain`
- AND při `true` by měla `status = active`

#### Scenario: Vyloučení poslední kontrolované verze
- GIVEN aktivní je jen SK a kontroluje se jen verze sk
- WHEN admin pošle `PUT /shops/{shopId}/languages/sk/exclusion` s `{ excluded: true }`
- THEN API odpoví `400 language.last_checked_version`

### Requirement: Rozsah kontroly a dynamický výpočet ceny
Systém MUST počítat rozsah kontroly na serveru jedním pravidlem pro kontrolu i cenu:
1. kontroluje se verze v jazyce každé zaškrtnuté země; když taková aktivní verze není, hlavní verze;
2. každá kontrolovaná verze se posuzuje podle všech zaškrtnutých zemí, jejichž zákazníci ji můžou číst (čeština a slovenština navzájem);
3. do pásma jde součet produktů za každou zaškrtnutou zemi: pro každou zemi počet produktů verze, kterou pro ni kontrolujeme (rozhodnutí 2. 10. 2026); když počet produktů kterékoli z nich neznáme, MUST jít do pásma součet stránek ke kontrole (všechny stránky sitemap verze za každou zemi, `priceBasis.unit = pages`, rozhodnutí 2. 10. 2026); když chybí i ten, MUST vést ke kódu `scope.product_count_unknown` bez ceny;
4. základ (počty po verzích) MUST pocházet z dokončené ukázky, aby cena z ukázky byla garantovaná.

`POST …/quote` MUST:
- přepočítat rozsah pro zadanou kombinaci zemí a vyloučených verzí bez uložení;
- vrátit `scopeHash`;
- vrátit cenu z `IPriceQuoteService`, bez implementace `503 billing.unavailable`.

#### Scenario: Odškrtnutí Česka přepočítá cenu
- GIVEN verze sk a cs, každá s 5 834 produkty
- WHEN klient pošle `POST /shops/{shopId}/quote` s `{ activeMarkets: ["sk","cz"] }` a pak s `{ activeMarkets: ["sk"] }`
- THEN první odpověď má `scope.productTotal = 11668` a 2 kontrolované verze
- AND druhá má `scope.productTotal = 5834`, jednu verzi sk a jiný `scopeHash`
- AND `shop.shop_markets` se žádným z volání nezměnilo

#### Scenario: Jedna verze a dvě země
- GIVEN jen verze sk s 5 834 produkty a aktivní SK a CZ
- WHEN klient zavolá `POST …/quote`
- THEN `checkedVersions` obsahuje sk s `jurisdictions = [sk, cz]`
- AND `productTotal = 11668` (5 834 za každou zemi)

#### Scenario: Ukázka ještě neskončila
- GIVEN ukázka ve stavu `evaluating`
- WHEN klient zavolá `POST …/quote`
- THEN API odpoví `409 quote.sample_not_finished`

#### Scenario: Platby ještě nejsou nasazené
- GIVEN v aplikaci není registrovaná implementace `IPriceQuoteService`
- WHEN klient zavolá `POST …/quote`
- THEN API odpoví `503 billing.unavailable`
- AND `GET /shops/{shopId}/scope` vrátí rozsah bez ceny

### Requirement: Připravenost k objednávce
Systém MUST poskytnout stav onboardingu (`GET …/onboarding`) a rozhraní `IShopOrderReadiness` pro objednávku (změna 12). Obojí vrací blokující kódy:
- `sample.not_finished`;
- `markets.not_confirmed` a `markets.none_selected`;
- `languages.confirmation_pending`;
- `shop.ownership_not_verified` (podle politiky);
- `shop.status_not_orderable`;
- `scope.basis_missing`.

`IShopOrderReadiness` MUST přepočítat rozsah z uloženého stavu a vrátit jeho `scopeHash`, aby objednávka odmítla zastaralou nabídku.

#### Scenario: Neověřená verze blokuje objednávku
- GIVEN dokončená ukázka, potvrzené země a verze `goodie.cz` ve stavu `needs_confirmation`
- WHEN změna 12 zavolá `IShopOrderReadiness.CheckAsync`
- THEN výsledek má `ready = false` a `blocking = ["languages.confirmation_pending"]`

#### Scenario: Nabídka po změně zemí
- GIVEN klient získal nabídku se SK i CZ a pak v jiném okně uložil jen SK
- WHEN objednávka porovná `scopeHash` nabídky s `IShopOrderReadiness.CheckAsync`
- THEN hodnoty se liší a objednávka (změna 12) skončí `409 quote.stale`

### Requirement: Ověření vlastnictví e-shopu
Systém MUST umožnit ověřit vlastnictví e-shopu metodou `meta` (značka v `<head>` úvodní stránky) nebo `dns` (záznam TXT `_eshopguard.{domain}`) s náhodným tokenem:
- kontrola běží jako úloha P0 ve workeru a výsledek MUST být `verified` nebo `failed` s kódem;
- metodu `connector` zapíše změna 15;
- systém MUST vynucovat ověření před kroky uvedenými v `Shops:Ownership:RequiredBefore` (`sample`, `full_analysis`);
- chybějící nastavení MUST zastavit start API i workeru.

#### Scenario: Ověření přes DNS
- GIVEN admin vytvořil ověření `dns` a doplnil záznam TXT s tokenem
- WHEN zavolá `POST /shops/{shopId}/ownership/verifications/{id}/check`
- THEN ověření má `status = verified` a `shops.ownership_verified_at` je vyplněné
- AND audit obsahuje `ownership.verified`

#### Scenario: Značka chybí
- GIVEN ověření `meta` a úvodní stránka bez značky
- WHEN doběhne kontrola
- THEN ověření má `status = failed` a `failureCode = meta_not_found`

#### Scenario: Povinné ověření před analýzou
- GIVEN `Shops:Ownership:RequiredBefore = [full_analysis]` a neověřený e-shop
- WHEN změna 12 zavolá `IShopOrderReadiness.CheckAsync`
- THEN `blocking` obsahuje `shop.ownership_not_verified`
- AND `RunService.CreateFullAnalysisAsync` přes `IShopOwnershipPolicy` vrátí stejný kód

#### Scenario: Chybějící nastavení
- GIVEN konfigurace bez `Shops:Ownership:RequiredBefore`
- WHEN se spouští API
- THEN start selže s chybou, která nastavení jmenuje

### Requirement: Nastavení e-shopu
Systém MUST umožnit adminovi změnit:
- název e-shopu;
- moduly kontroly („Čo kontrolovať“), jen z modulů dostupných pro aktivní země a aspoň jeden;
- „Kontrola pri uložení, aj skrytých produktov“, jen u e-shopu napojeného konektorem na platformě, která skryté produkty hlásí (ne BiznisWeb).

Souběžné úpravy MUST hlídat `If-Match` a při rozdílu vrátit `409 concurrency.conflict`.

#### Scenario: Modul jen pro Slovensko u českého e-shopu
- GIVEN e-shop s jedinou aktivní zemí CZ a modul `eco` dostupný jen pro `sk`
- WHEN admin pošle `PATCH /shops/{shopId}/settings` s `{ modules: ["eco","ucp"] }`
- THEN API odpoví `400` s kódem `settings.module_unavailable` a `params.module = eco`

#### Scenario: Kontrola při uložení bez konektoru
- GIVEN e-shop se `sourceMode = web`
- WHEN admin nastaví `checkHiddenOnSave = true`
- THEN API odpoví `409 settings.hidden_check_requires_connector`

#### Scenario: Souběžná úprava
- GIVEN dva admini načetli nastavení se stejnou verzí a první uložil změnu
- WHEN druhý uloží se starou hlavičkou `If-Match`
- THEN dostane `409 concurrency.conflict` a změna prvního zůstane

### Requirement: Oprávnění rolí a izolace e-shopů přes API
Systém MUST:
- dovolit čtení e-shopů, ukázky, zemí, verzí, rozsahu a nastavení každému členovi tenanta;
- dovolit změny (založení, napojení, ukázka, potvrzení zemí a verzí, nabídka ceny, ověření, nastavení, smazání) jen rolím `admin` a `owner`;
- vracet pro e-shop jiného tenanta `404`, i když je v adrese vlastního tenanta.

#### Scenario: Editor nesmí spustit ukázku
- GIVEN uživatel s rolí `editor`
- WHEN pošle `POST /shops/{shopId}/sample`
- THEN API odpoví `403 auth.forbidden_role` s `params.requiredRole = admin`

#### Scenario: E-shop jiného tenanta pod vlastním tenantem
- GIVEN tenant A a tenant B, oba s e-shopem `vegis.sk`
- WHEN admin tenanta A zavolá `GET /api/t/{tenantA}/shops/{shopIdB}`
- THEN API odpoví `404 shop.not_found`
- AND `PUT /api/t/{tenantA}/shops/{shopIdB}/markets` také `404` a řádky tenanta B se nezmění

#### Scenario: Seznam e-shopů jen vlastních
- GIVEN tenant A má 2 e-shopy a tenant B 3
- WHEN člen tenanta A zavolá `GET /shops`
- THEN dostane právě 2 e-shopy tenanta A
