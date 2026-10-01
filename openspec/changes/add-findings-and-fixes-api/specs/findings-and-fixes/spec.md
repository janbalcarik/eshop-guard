# Delta for Findings-and-fixes

## ADDED Requirements

### Requirement: Přehled a pohled po stránkách a po nálezech
Systém MUST pro e-shop vrátit:
- přehled: poslední kontrolu, počty nálezů podle skupiny, počty stránek po záložkách, nejvýš 5 stránek k řešení nejdřív, nejvýš 3 rychlé odpovědi s celkovým počtem;
- seznam stránek po záložkách `to_resolve`, `to_approve`, `needs_answer`, `published` podle definic v designu, včetně položek „Celý e-shop: šablóna“ a „Celý e-shop: košík a objednávka“;
- seznam nálezů se záložkami podle nejpřísnější skupiny (porušení, na posúdenie, na overenie) a „Opravené“, s filtry modulu, stavu, země a jazykové verze.

Filtr jazykové verze MUST omezit stránky podle `pages.language` a položky za celý web MUST ukázat u všech verzí. Seznamy MUST stránkovat kurzorem se stabilním pořadím.

#### Scenario: Záložky podle návrhu UI
- GIVEN testovací data „bylinkovo.sk“: 28 položek s nálezem, 12 s otevřenou otázkou, 12 s návrhy připravenými ke schválení bez otázky, 4 hotové
- WHEN klient zavolá `GET …/pages/tabs`
- THEN odpověď je `{ toResolve: 24, toApprove: 12, needsAnswer: 12, published: 4, withFindings: 28 }`

#### Scenario: Filtr české verze
- GIVEN nálezy na stránkách verze `sk` i `cs` a otázka za celý web
- WHEN klient zavolá `GET …/pages?tab=to_resolve&language=cs`
- THEN vrátí jen stránky s `language = cs` a položku „Celý e-shop: košík a objednávka“

#### Scenario: Stránka s otázkou je v záložce odpovědí, ne schválení
- GIVEN stránka „Zubná pasta + bambusová kefka“ s 5 návrhy a 1 otevřenou otázkou
- WHEN klient načte záložky `to_approve` a `needs_answer`
- THEN stránka je jen v `needs_answer` se `status = needs_answer`

#### Scenario: Neplatný filtr
- GIVEN klient pošle `GET …/findings?checkability=fatal`
- WHEN API ověří parametry
- THEN odpoví `400 validation.failed` s `errors.checkability = ["value.not_allowed"]`

### Requirement: Verdikty po zemích, řazení a texty z pravidel
Systém MUST u každého nálezu vrátit verdikt pro každou zemi kontroly (skupina, závažnost, pásmo, odkazy na zákon) a nejpřísnější z nich. Stránky a nálezy MUST řadit podle nejpřísnějšího verdiktu, pak podle počtu nálezů.

API MUST vracet kódy a parametry, ne věty. Názvy, vysvětlení po jurisdikcích, „Čo pomôže“ a znění otázek MUST být dostupné z katalogu textů pravidel `GET /api/catalog/rule-texts` v jazyce uživatele. Odkazy na zákon MUST zůstat v jazyce zákona. Jazyk s neúplnými texty MUST vrátit `409 catalog.locale_incomplete`.

#### Scenario: Různý verdikt na Slovensku a v Česku
- GIVEN nález „Všetky naše produkty balíme ekologicky.“ s verdikty `sk: text/high` a `cz: assess/medium`
- WHEN klient zavolá `GET …/findings/{findingId}`
- THEN `verdicts` obsahuje oba záznamy a `strictest.jurisdiction = sk`, `strictest.checkability = text`
- AND `legalRefs` u `sk` cituje zákon č. 108/2024 Z. z. slovensky i pro uživatele s jazykem `cs`

#### Scenario: Katalog v češtině
- GIVEN uživatel s jazykem `cs` a sady pravidel s úplnými texty `cs`
- WHEN zavolá `GET /api/catalog/rule-texts?locale=cs&ruleSetIds=…`
- THEN dostane názvy, vysvětlení pro `sk` i `cz` a „Čo pomôže“ česky s hlavičkou `ETag`

#### Scenario: Neúplný jazyk
- GIVEN sada pravidel bez úplných textů v jazyce `cs`
- WHEN klient požádá o katalog `cs`
- THEN API odpoví `409 catalog.locale_incomplete` s `params.ruleSetId`
- AND nevrátí částečný katalog

### Requirement: Stavy nálezu a povolené přechody
Systém MUST vést stav nálezu jako jeden z `open`, `needs_answer`, `proposed`, `approved`, `published`, `kept`, `kept_with_evidence`, `dismissed`, `resolved` a měnit ho jen přechody z tabulky v designu.
- Nepovolený přechod MUST vrátit `409 finding.transition_not_allowed` s `params.from` a `params.to`.
- Každý přechod MUST být zapsán do auditu.
- Rozhodnutí „Ponechať“, odpověď „Áno“, přijetí a schválení skupiny MUST zapsat paměť rozhodnutí. Vrácení rozhodnutí MUST paměť označit jako nahrazenou, ne smazat.

#### Scenario: Ponechat nález
- GIVEN nález „Set sme pripravili pre ekologicky zmýšľajúcich zákazníkov“ ve stavu `proposed`
- WHEN editor pošle `POST …/findings/{findingId}/keep` s `{ reasonCode: "no_promise" }`
- THEN nález má `status = kept`
- AND `fixes.decision_memory` obsahuje `decision = keep` pro jeho `segment_hash`
- AND audit obsahuje `finding.status_changed` z `proposed` na `kept`

#### Scenario: Publikovaný nález nejde ponechat
- GIVEN nález ve stavu `published`
- WHEN editor pošle `keep`
- THEN API odpoví `409 finding.transition_not_allowed` s `params.from = published` a `params.to = kept`

#### Scenario: Znovu otevření vrátí paměť
- GIVEN nález `kept` se záznamem paměti rozhodnutí
- WHEN editor pošle `reopen`
- THEN nález má `status = open`
- AND záznam paměti má vyplněné `superseded_at` a zůstal v tabulce

### Requirement: Oprava stránky v kontextu
Systém MUST pro stránku vrátit všechny změny v pořadí textu:
- původní a navržený text s okolním textem ze souboru extrakce;
- varianty, údaje k doplnění, verdikty po zemích, stav kontroly;
- odkaz na hromadnou opravu s počtem stránek;
- otázky;
- počet odstavců beze změny;
- pozici ve frontě záložky (pořadí, celkem, další stránka) a zdroj textu (konektor s číslem produktu, procházení, feed);
- zda jde stránku publikovat do e-shopu, a pokud ne, kód důvodu.

#### Scenario: Stránka se 5 změnami
- GIVEN stránka „Zubná pasta + bambusová kefka“ z Shoptetu (produkt č. 2429) s 5 návrhy, z toho 1 ze skupiny na 38 stránkách a 1 s otázkou
- WHEN klient zavolá `GET …/pages/{pageId}/review?tab=to_resolve`
- THEN `changes` má 5 položek; změna 2 má `group.pageCount = 38` a `placeholders` s klíčem materiálu obalu
- AND `page.source = { kind: connector, platform: shoptet, externalId: "2429" }` a `position.index = 1`, `position.total = 24`

#### Scenario: Okolní text ze souboru extrakce
- GIVEN návrh v bloku 4 stránky, jejíž extrakce je v úložišti
- WHEN klient načte revizi
- THEN `contextBefore` a `contextAfter` obsahují sousední věty bloku a `unchangedBlocks` počet bloků beze změny
- AND v databázi nevznikl žádný nový řádek s textem stránky

#### Scenario: E-shop bez konektoru
- GIVEN e-shop se `sourceMode = web`
- WHEN klient načte revizi
- THEN `publish.available = false` a `publish.reasonCode = no_connector`

### Requirement: Přijetí návrhu jen po kontrole ve všech zemích
Systém MUST dovolit přijmout návrh opravy jen tehdy, když:
- text (vybraná varianta nebo vlastní úprava) prošel kontrolou Jevem pro všechny aktivní země e-shopu a moduly nálezu;
- návrh nemá nevyplněný údaj.

Úprava textu nebo údaje MUST nastavit kontrolu na `pending` a založit úlohu P0 `fix.recheck`. Výsledek kontroly MUST se zapsat jen pro text, pro který byl spočítán. Souběžné úpravy MUST hlídat `If-Match`. Zamítnutí návrhu vrátí nález do `open`, přijetí do `approved`, když jsou přijaté všechny návrhy nálezu.

#### Scenario: Vlastní úprava projde kontrolou
- GIVEN návrh ve stavu `proposed` a e-shop s aktivními zeměmi SK a CZ
- WHEN editor pošle `PUT …/proposals/{id}/text` s novým zněním
- THEN API odpoví `202` s `recheck.status = pending` a vznikne úloha `fix.recheck` s `priority = 0`
- AND po doběhnutí s `MockJevClient` bez nálezu má návrh `recheck_status = ok` a SSE e-shopu pošle `proposal.rechecked`

#### Scenario: Text dál porušuje v Česku
- GIVEN kontrola upraveného textu vrátila `still_finding` pro `cz`
- WHEN editor pošle `POST …/proposals/{id}/accept`
- THEN API odpoví `409 proposal.recheck_failed` s `params.jurisdictions = ["cz"]` a `params.ruleIds`
- AND návrh zůstane neprijatý

#### Scenario: Nevyplněný údaj
- GIVEN návrh „Obal: [materiál obalu].“ bez hodnoty údaje
- WHEN editor ho chce přijmout
- THEN API odpoví `409 proposal.placeholder_missing` s `params.keys`

#### Scenario: Zastaralá kontrola po další úpravě
- GIVEN kontrola textu A ještě běží a editor mezitím uložil text B
- WHEN doběhne kontrola textu A
- THEN výsledek se nezapíše a návrh zůstane `pending` až do výsledku pro text B

### Requirement: Odpovědi na otázky platné pro stejný text
Systém MUST přijmout odpověď „Áno“ nebo „Nie“ na otázku k nálezu i na otázku za celý web:
- odpověď MUST se přenést na všechny otevřené otázky se stejným kódem a stejným otiskem textu ve všech e-shopech tenanta; u otázek za celý web jen v daném e-shopu;
- dopad (`appliesTo`: otázky, nálezy, stránky) MUST být vidět před odpovědí i v odpovědi;
- „Áno“ MUST založit doklad typu odpověď s vazbami na dotčené nálezy a převést je na `kept_with_evidence`;
- „Nie“ MUST vybrat připravenou variantu „Nie“, jinak založit úlohu návrhu v denním rozpočtu tenanta;
- nad rozpočtem MUST vrátit `429 budget.daily_limit_reached` a odpověď neuložit;
- změnit odpověď jde jen do publikace dotčených návrhů.

#### Scenario: Doba hoření na 4 sviečkách
- GIVEN 4 stránky sviečok Vodnár se stejnou větou „Doba horenia: 22 hodín“ a otevřenou otázkou se stejným kódem
- WHEN editor odpoví „Nie“ na jedné z nich a všechny mají připravenou variantu `answer_no` („Riadok odstrániť“) s kontrolou `ok`
- THEN odpověď má `affectedQuestions = 4`, `affectedPages = 4`, `generationPending = false`
- AND všechny 4 nálezy jsou `proposed` s vybranou variantou `answer_no`

#### Scenario: Áno uloží doklad
- GIVEN otázka „Má výrobok platný certifikát COSMOS?“ u „Rozjasňujúce sérum 30 ml“
- WHEN editor odpoví „Áno“
- THEN vznikne `evidence_items` s `kind = answer`, `source = answer`, `status = valid` a vazbou na nález
- AND nález má `status = kept_with_evidence`

#### Scenario: Rozpočet návrhů vyčerpaný
- GIVEN tenant dnes vyčerpal `Fixes:DailyGenerationsPerTenant` a otázka nemá připravenou variantu „Nie“
- WHEN editor odpoví „Nie“
- THEN API odpoví `429 budget.daily_limit_reached` a otázka zůstane `open`

#### Scenario: Odpověď v jednom tenantovi nemění druhý
- GIVEN tenant A i tenant B mají otevřenou otázku se stejným kódem a stejným textem
- WHEN editor tenanta A odpoví
- THEN otázka tenanta B zůstane `open`

### Requirement: Doklady zadané jednou platí všude
Systém MUST vést doklady za celý tenant (tvrzení, předmět, druh, soubor, platnost, zdroj, stav):
- soubor MUST být PDF, JPEG nebo PNG podle obsahu do `Evidence:MaxFileBytes`;
- soubor MUST být uložený pod předponou tenanta a stahovaný jen přes podepsaný odkaz s krátkou platností;
- stav MUST se počítat jako `valid`, `expiring` (do `Evidence:ExpiringDays` před koncem platnosti), `expired`, `awaiting_answer` nebo `claim_removed`;
- denní úloha MUST jednou připomenout blížící se konec platnosti;
- vypršený nebo smazaný doklad MUST vrátit navázané nálezy do `open`.

#### Scenario: Přidání certifikátu se souborem
- GIVEN editor nahraje PDF certifikátu Vegan pro značku Biopurus s platností do 30. 9. 2027
- WHEN pošle `POST /api/t/{tenantId}/evidence`
- THEN API odpoví `201` se `status = valid` a `hasFile = true`
- AND soubor je v úložišti pod `tenants/{tenantId}/evidence/{evidenceId}/`

#### Scenario: Podvržený typ souboru
- GIVEN soubor `certifikat.pdf`, který je ve skutečnosti spustitelný soubor
- WHEN editor ho nahraje
- THEN API odpoví `400 evidence.file_type_not_allowed` a nic se neuloží

#### Scenario: Blížící se konec platnosti
- GIVEN doklad BDIH s platností do 20. 10. 2026 a dnešek 30. 9. 2026
- WHEN proběhne úloha `evidence.refresh_status`
- THEN doklad má `status = expiring` a `daysToExpiry = 20`
- AND vznikne upozornění `evidence_expiring` a `reminder_sent_at`; další běh úlohy upozornění nezopakuje

#### Scenario: Vypršený doklad otevře nálezy
- GIVEN doklad navázaný na 6 nálezů ve stavu `kept_with_evidence`
- WHEN uplyne `valid_until` a proběhne denní úloha
- THEN doklad má `status = expired` a všech 6 nálezů je `open`
- AND vznikne upozornění `evidence_expired`

### Requirement: Hromadné opravy
Systém MUST umožnit vyřešit opakovaný text jedním rozhodnutím:
- skupina ukáže původní text, navržené znění s údaji, verdikty, vzorky v kontextu, kolik stránek oprava sedí a které stránky je třeba řešit jednotlivě s kódem důvodu, a seznam stránek;
- režimy `replace` (s údaji), `remove` a `custom` (vlastní znění s kontrolou Jevem);
- vyřazení stránek;
- schválení MUST vytvořit nebo přijmout návrh pro každou zahrnutou stránku v jedné transakci a MUST vynechat vyřazené stránky a stránky k jednotlivému řešení;
- schválit nejde skupinu s nevyplněným údajem nebo s neprošlou kontrolou;
- vrátit schválení jde jen do první publikace.

#### Scenario: Jedno rozhodnutí místo 36
- GIVEN skupina „Všetky naše produkty balíme ekologicky.“ na 38 stránkách, z toho 2 k jednotlivému řešení, údaj „papierová krabica bez plastovej výplne“ vyplněný a kontrola `ok`
- WHEN editor pošle `POST …/fix-groups/{groupId}/approve`
- THEN vznikne 36 přijatých návrhů s `group_id` a odpovídajících 36 nálezů je `approved`
- AND 2 stránky z `fit.individual` zůstanou bez přijatého návrhu
- AND paměť rozhodnutí má jeden záznam `replace` pro otisk věty

#### Scenario: Chybí údaj
- GIVEN skupina v režimu `replace` s nevyplněným údajem materiálu obalu
- WHEN editor pošle `approve`
- THEN API odpoví `409 group.value_missing` a nic se nezmění

#### Scenario: Jen na této stránce
- GIVEN stránka skupiny, která není v `fit.individual`
- WHEN editor pošle `POST …/fix-groups/{groupId}/approve-page` s jejím `pageId`
- THEN je přijatý jen návrh této stránky a skupina zůstane neschválená

### Requirement: Publikace přes konektor a kopírování textu
Systém MUST na požádání editora vytvořit publikace přijatých oprav, jednu na stránku a pole:
- s původním a novým textem, otiskem původního textu a jedinečným klíčem idempotence;
- s úlohou `publish.fix`, kterou obslouží implementace `IFixPublisher` ze změny 15;
- publikovat jde jen u e-shopu napojeného konektorem se zápisem; jinak, nebo bez implementace `IFixPublisher`, MUST vrátit `409` a nic nezapsat;
- pole, která platforma zapsat neumí, MUST být vrácena v `skipped` s kódem `copy_only`.

Pro každé pole MUST být k dispozici sestavený text s přijatými změnami pro „Kopírovať text“.

#### Scenario: Publikace do Shoptetu
- GIVEN e-shop s připojeným konektorem Shoptet (`read_write`), registrovaný `FakeFixPublisher` a stránka s 1 přijatou změnou v popisu
- WHEN editor pošle `POST …/publications` s `{ pageIds: [pageId] }`
- THEN API odpoví `202` s jednou publikací `status = queued`, `field = description`
- AND vznikla úloha `publish.fix` s `priority = 1`

#### Scenario: Opakovaný požadavek nic nezdvojí
- GIVEN publikace stránky je ve stavu `queued`
- WHEN editor pošle stejný požadavek znovu
- THEN API vrátí stejnou publikaci a nevznikne nový řádek ani úloha

#### Scenario: E-shop napojený jen přes web
- GIVEN e-shop se `sourceMode = web`
- WHEN editor pošle `POST …/publications`
- THEN API odpoví `409 publication.not_available` s `params.reason = no_connector`
- AND `GET …/pages/{pageId}/fixed-text?field=description` vrátí celý text pole s přijatými změnami

#### Scenario: Čtenář nesmí publikovat
- GIVEN uživatel s rolí `viewer`
- WHEN pošle `POST …/publications`
- THEN API odpoví `403 auth.forbidden_role`

### Requirement: Protokol PDF s číslem po tenantovi
Systém MUST na požádání vytvořit protokol o kontrole e-shopu za zvolené období:
- číslo `EG-{rok}-{pořadí}` je jedinečné v tenantovi, roste po tenantovi a roce (aspoň 4 číslice) a přiděluje se pod zámkem;
- jazyk je z požadavku, jinak výchozí jazyk trhu domovské země e-shopu;
- obsah: e-shop a období, úvodní kontrola a co nebylo zkontrolováno, verze pravidel, země kontroly, souhrn, rozhodnutí a opravy s body zákona, doklady, nálezy čekající na rozhodnutí, prohlášení, že nejde o právní posouzení a že návrhy připravila umělá inteligence a schválil je provozovatel;
- PDF vykreslí úloha ve workeru, uloží se do úložiště tenanta a stahuje se jen přes podepsaný odkaz;
- dokud není hotové, MUST stažení vrátit `409 protocol.not_ready`.

#### Scenario: Protokol za první měsíc
- GIVEN e-shop „bylinkovo.sk“ s úvodní kontrolou 30. 9. 2026 a poslední protokol tenanta v roce 2026 s číslem `EG-2026-0141`
- WHEN editor pošle `POST …/protocols` s obdobím 30. 9. – 31. 10. 2026 bez jazyka
- THEN API odpoví `202` s `number = EG-2026-0142`, `locale = sk`, `status = rendering`
- AND po doběhnutí úlohy je `status = ready` a `GET …/protocols/{id}/pdf` vrátí `302` na podepsaný odkaz

#### Scenario: Souběžné žádosti o číslo
- GIVEN 20 souběžných požadavků na protokol v jednom tenantovi
- WHEN všechny doběhnou
- THEN mají 20 různých čísel bez mezer a bez duplicit

#### Scenario: Protokol uvádí nezkontrolované a nerozhodnuté
- GIVEN úvodní kontrola skončila `partial` se 7 stránkami zakázanými v robots.txt a 6 nálezů čeká na rozhodnutí
- WHEN se protokol sestaví
- THEN `summary` obsahuje `notChecked.robotsBlocked = 7` a `awaitingDecision = 6` a obojí je v PDF

#### Scenario: Stažení před dokončením
- GIVEN protokol ve stavu `rendering`
- WHEN klient zavolá `GET …/protocols/{id}/pdf`
- THEN API odpoví `409 protocol.not_ready`

### Requirement: Upozornění a jejich nastavení
Systém MUST vést upozornění pro každého příjemce zvlášť:
- druh, e-shop, parametry jako kódy a počty, jazykově neutrální cíl odkazu, čas přečtení;
- přečíst jedno nebo všechna;
- nastavení e-mailů (nové porušení, týdenní souhrn, konec běhu) za celý účet a po e-shopech, přičemž nastavení e-shopu má přednost.

`NotificationDispatcher` MUST založit řádek pro každého člena v okruhu příjemců a e-mail zapsat do `ops.outbox` jen podle nastavení. Upozornění ani e-mail MUST NOT obsahovat texty stránek.

#### Scenario: Přečtení jedním uživatelem neskryje upozornění druhému
- GIVEN upozornění `protocol_ready` pro Janu i Petera
- WHEN Jana zavolá `POST …/notifications/{id}/read`
- THEN Peter má upozornění dál nepřečtené a `unreadCount` se mu nezměnil

#### Scenario: Vypnutý e-mail o konci běhu
- GIVEN Peter má pro e-shop `bylinkovo.sk` `emailRunFinished = false` a pro účet `true`
- WHEN `NotificationDispatcher` pošle `run_finished` pro `bylinkovo.sk`
- THEN Peter dostane upozornění v aplikaci, ale do `ops.outbox` se pro něj e-mail nezapíše

#### Scenario: Čtenář nemění cizí nastavení
- GIVEN uživatel s rolí `viewer`
- WHEN pošle `PUT …/notification-settings`
- THEN změní se jen jeho vlastní řádek `notification_settings`

### Requirement: Průběh běhů přes SSE
Systém MUST poskytovat živý průběh běhu (`GET /api/t/{tenantId}/runs/{runId}/events`) a změny e-shopu (`GET …/shops/{shopId}/events`) jako server-sent events:
- na začátku stav běhu;
- pak události běhu, průběh a změny stavu;
- obnovení od `Last-Event-ID` bez ztráty událostí;
- pravidelný signál spojení;
- konec po konečném stavu běhu.

Oznámení z databáze MUST nést jen ID. Data se MUST číst až pod kontextem tenanta daného požadavku. Počet souběžných spojení na uživatele MUST být omezený.

#### Scenario: Obnovení po výpadku spojení
- GIVEN klient přijal události běhu do `id: 812` a odpojil se
- WHEN se znovu připojí s `Last-Event-ID: 812` a mezitím vznikly události 813–815
- THEN dostane nejdřív `snapshot` a pak události 813, 814, 815 v tomto pořadí

#### Scenario: Cizí běh
- GIVEN běh tenanta B
- WHEN člen tenanta A otevře `GET /api/t/{tenantA}/runs/{runIdB}/events`
- THEN API odpoví `404 run.not_found` a žádný proud neotevře

#### Scenario: Konec běhu
- GIVEN běh přejde do stavu `finished`
- WHEN otevřený proud dostane oznámení
- THEN pošle `event: status` s `finished`, pak `event: end` a spojení uzavře

#### Scenario: Příliš mnoho spojení
- GIVEN uživatel má otevřených `Sse:MaxConnectionsPerUser` proudů
- WHEN otevře další
- THEN API odpoví `429 sse.too_many_connections`

### Requirement: Oprávnění rolí a izolace tenantů přes API
Systém MUST pro koncové body nálezů, oprav, dokladů, protokolů, upozornění a běhů:
- dovolit čtení každému členovi tenanta;
- dovolit rozhodnutí, odpovědi, doklady, hromadné opravy, publikace a protokoly rolím `editor`, `admin` a `owner`;
- dovolit zrušení běhu rolím `admin` a `owner`;
- vrátit pro objekt jiného tenanta `404`, i když je v adrese vlastního tenanta;
- dovolit u e-shopu jen s ukázkou zdarma jen čtení souhrnu ukázky, rozhodnutí MUST vrátit `409 shop.sample_only`.

#### Scenario: Návrh jiného tenanta
- GIVEN tenant A a tenant B, oba s e-shopem `vegis.sk` a nálezem se stejným textem
- WHEN editor tenanta A pošle `POST /api/t/{tenantA}/shops/{shopA}/proposals/{proposalIdB}/accept`
- THEN API odpoví `404 proposal.not_found` a návrh tenanta B se nezmění

#### Scenario: Matice rolí přes všechny koncové body
- GIVEN test projde všechny koncové body této změny z `EndpointDataSource`
- WHEN je zavolá s rolemi viewer, editor, admin a owner
- THEN výsledek odpovídá matici: viewer jen čtení, editor rozhodnutí a publikace, admin a owner navíc zrušení běhu
- AND koncový bod bez záznamu v očekávání test shodí

#### Scenario: Ukázka zdarma bez rozhodnutí
- GIVEN e-shop ve stavu `sample`
- WHEN editor pošle `POST …/findings/{findingId}/keep`
- THEN API odpoví `409 shop.sample_only`

#### Scenario: Doklad jiného tenanta
- GIVEN doklad tenanta B se souborem
- WHEN člen tenanta A zavolá `GET /api/t/{tenantA}/evidence/{evidenceIdB}/file`
- THEN API odpoví `404 evidence.not_found` a podepsaný odkaz nevznikne
