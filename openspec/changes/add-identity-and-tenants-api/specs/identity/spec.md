# Delta for Identity

## ADDED Requirements

### Requirement: Vyžádání odkazu na přihlášení
Systém MUST na `POST /api/auth/login-link` poslat na zadaný e-mail jednorázový odkaz na přihlášení.
- Odkaz platí 15 minut.
- V databázi je jen otisk SHA-256 tokenu, token je jen v e-mailu.
- Odpověď MUST být stejná (`202` se stejným tělem) bez ohledu na to, zda účet s e-mailem existuje.
- Nový odkaz MUST zneplatnit všechny dříve vydané nepoužité odkazy pro stejný e-mail.
- E-mail s tokenem se nesmí uložit do `ops.outbox`.

#### Scenario: Existující účet dostane odkaz
- GIVEN uživatel `jana@bylinkovo.sk` s účtem a jazykem `sk`
- WHEN pošle `POST /api/auth/login-link` s tímto e-mailem
- THEN API odpoví `202` s `{ expiresInSeconds: 900, resendAfterSeconds: 60 }`
- AND v `iam.user_tokens` přibude řádek `purpose = magic_link` s `token_hash` (32 bajtů) a `expires_at` za 15 minut
- AND odejde slovenský e-mail „login_link“, jehož odkaz obsahuje token za znakem `#`
- AND sloupce `user_tokens`, řádky `ops.outbox` ani logy token neobsahují

#### Scenario: Neexistující účet dostane stejnou odpověď
- GIVEN e-mail `novy@firma.sk` nemá účet
- WHEN pošle `POST /api/auth/login-link`
- THEN API odpoví `202` se stejným tělem jako u existujícího účtu
- AND vznikne řádek `user_tokens` s `user_id = NULL` a odejde e-mail se stejnou šablonou
- AND v `iam.users` žádný nový řádek nevznikne

#### Scenario: Nový odkaz zneplatní předchozí
- GIVEN uživatel si vyžádal odkaz A a po 2 minutách odkaz B
- WHEN pošle `POST /api/auth/login-link/consume` s tokenem A
- THEN API odpoví `410` s kódem `login_link.expired`
- AND token B zůstane platný

#### Scenario: Odeslání e-mailu selže
- GIVEN odesílání e-mailů je nedostupné
- WHEN uživatel pošle `POST /api/auth/login-link`
- THEN API odpoví `503` s kódem `email.send_failed`
- AND nově založený token má `expires_at <= now()`, takže nejde použít

### Requirement: Limity odesílání odkazu
Systém MUST omezit vydávání odkazů na přihlášení a obnovu hesla:
- nový odkaz pro stejný e-mail nejdřív 60 s po předchozím;
- nejvýš 5 odkazů za hodinu na e-mail;
- nejvýš 20 za hodinu na IP adresu.

Limity za hodinu MUST být uložené v `ops.rate_limit_buckets`, aby platily přes všechny instance API. Klíče MUST obsahovat jen otisk HMAC e-mailu nebo IP. Odmítnutý požadavek MUST vrátit `429 rate_limited` s `params.scope` a `params.retryAfterSeconds` a nesmí vydat token.

#### Scenario: Opakování dřív než za 60 sekund
- GIVEN uživatel si před 18 sekundami vyžádal odkaz
- WHEN pošle `POST /api/auth/login-link` znovu
- THEN API odpoví `429` s `code = rate_limited`, `params.scope = email_cooldown` a `params.retryAfterSeconds = 42`
- AND nový řádek `user_tokens` nevznikne a předchozí odkaz zůstane platný

#### Scenario: Šestý odkaz za hodinu
- GIVEN pro e-mail vzniklo během poslední hodiny 5 odkazů, každý s odstupem víc než 60 s
- WHEN uživatel požádá o šestý
- THEN API odpoví `429` s `params.scope = email_hourly`
- AND odpověď je stejná pro existující i neexistující účet

#### Scenario: Dvacátý první požadavek z jedné IP
- GIVEN z jedné IP adresy přišlo během hodiny 20 požadavků na různé e-maily
- WHEN přijde 21. požadavek z téže IP na další e-mail
- THEN API odpoví `429` s `params.scope = ip_hourly`
- AND v `ops.rate_limit_buckets` je klíč `auth:link:ip:{hmac}`, ve kterém není čitelná IP adresa

### Requirement: Přihlášení potvrzením odkazu
Systém MUST oddělit otevření odkazu od přihlášení:
- `POST /api/auth/login-link/inspect` token MUST NOT spotřebovat;
- přihlásí až `POST /api/auth/login-link/consume`, který token spotřebuje atomicky a nejvýš jednou;
- u e-mailu bez účtu MUST vzniknout účet až tímto krokem, s `email_confirmed = true` a jazykem z požadavku;
- vypršelý, použitý nebo neznámý token MUST vrátit kód, podle kterého frontend nabídne „Poslať nový odkaz“.

#### Scenario: Náhled odkazu v poště odkaz nevyčerpá
- GIVEN platný odkaz
- WHEN frontend dvakrát zavolá `inspect` (stránka 2c otevřená náhledem pošty i uživatelem)
- THEN obě odpovědi jsou `200` s `email` a `isNewAccount`
- AND `user_tokens.used_at` zůstane `NULL`

#### Scenario: Nový účet vznikne kliknutím
- GIVEN odkaz vydaný pro `novy@firma.sk` bez účtu a požadavek s `Accept-Language: cs-CZ` a `market = cz`
- WHEN uživatel klikne „Prihlásiť sa“ a frontend pošle `consume`
- THEN vznikne `iam.users` s `email_confirmed = true` a `locale = cs`
- AND API nastaví cookie `__Host-eg_session` (`HttpOnly`, `Secure`, `SameSite=Lax`) a vrátí `isNewAccount = true`
- AND audit obsahuje `user.created`, `user.terms_accepted` s verzí podmínek a `auth.login_succeeded` s `amr = magic_link`

#### Scenario: Dvojí použití téhož odkazu
- GIVEN odkaz byl právě použit
- WHEN přijde druhý `consume` se stejným tokenem (i souběžně s prvním)
- THEN právě jeden požadavek přihlásí
- AND druhý dostane `410` s kódem `login_link.used`

#### Scenario: Odkaz po 15 minutách
- GIVEN odkaz vydaný v 10:00:00
- WHEN uživatel zavolá `consume` v 10:15:01
- THEN API odpoví `410` s kódem `login_link.expired` a uživatel se nepřihlásí

### Requirement: Přihlášení heslem a ochrana proti hádání
Systém MUST umožnit přihlášení e-mailem a heslem jen u účtu, který má heslo nastavené.
- Neexistující účet, účet bez hesla, špatné heslo i zablokovaný účet MUST vrátit stejnou odpověď `401 auth.invalid_credentials`.
- Po `Auth:Lockout:MaxFailedAttempts` po sobě jdoucích chybných heslech MUST být přihlášení heslem zablokované na `Auth:Lockout:Minutes`.
- Přihlášení odkazem nebo Googlem MUST zůstat možné a MUST zablokování zrušit.
- Hesla MUST být uložena jen jako haš ASP.NET Core Identity (formát V3).

#### Scenario: Špatné heslo a neexistující účet nejdou odlišit
- GIVEN účet `jana@bylinkovo.sk` s heslem a e-mail `nikto@nikde.sk` bez účtu
- WHEN přijde přihlášení se špatným heslem pro oba e-maily
- THEN obě odpovědi jsou `401` se stejným tělem `auth.invalid_credentials`

#### Scenario: Zablokování po opakovaném chybném hesle
- GIVEN 5 chybných hesel po sobě (výchozí návrh)
- WHEN přijde šesté přihlášení se správným heslem během 15 minut
- THEN API odpoví `401 auth.invalid_credentials`
- AND v auditu je `auth.locked_out`

#### Scenario: Odkaz zablokování zruší
- GIVEN účet je zablokovaný pro heslo
- WHEN se uživatel přihlásí odkazem v e-mailu
- THEN přihlášení projde a `access_failed_count = 0`, `lockout_end = NULL`

### Requirement: Volitelné heslo a jeho obnova
Systém MUST umožnit přihlášenému uživateli heslo nastavit, změnit a odebrat v Nastaveniach.
- Nastavení prvního hesla a odebrání hesla bez znalosti současného hesla MUST vyžadovat relaci ověřenou v posledních `Auth:ReauthenticationMinutes`.
- Změna existujícího hesla MUST vyžadovat současné heslo.
- „Zabudnuté heslo“ MUST vrátit stejnou odpověď pro existující i neexistující účet a poslat jednorázový odkaz (`purpose = reset`, jen otisk v databázi).
- Nastavení nového hesla MUST vyměnit `security_stamp`, čímž skončí ostatní relace.

#### Scenario: První heslo po přihlášení odkazem
- GIVEN uživatel bez hesla přihlášený odkazem před 3 minutami
- WHEN pošle `PUT /api/me/password` s `newPassword` dlouhým 12 znaků
- THEN API odpoví `204` a `MeDto.hasPassword = true`
- AND audit obsahuje `auth.password_set`

#### Scenario: Staré přihlášení vyžaduje nové ověření
- GIVEN uživatel bez hesla přihlášený před 2 dny
- WHEN pošle `PUT /api/me/password` bez `currentPassword`
- THEN API odpoví `403` s kódem `auth.reauthentication_required`

#### Scenario: Obnova hesla odhlásí ostatní zařízení
- GIVEN uživatel je přihlášený na dvou zařízeních a z třetího požádá o obnovu hesla
- WHEN zavolá `POST /api/auth/password/reset` s platným tokenem a novým heslem
- THEN je přihlášený na třetím zařízení s `amr = reset`
- AND po uplynutí intervalu kontroly `security_stamp` (5 minut) dostanou obě původní relace `401 auth.unauthenticated`

#### Scenario: Krátké heslo
- GIVEN přihlášený uživatel
- WHEN nastaví heslo kratší než `Auth:Password:MinLength`
- THEN API odpoví `400 validation.failed` s `errors.newPassword = ["password.too_short"]`

### Requirement: Přihlášení přes Google
Systém MUST umožnit přihlášení a založení účtu přes Google:
- účet se propojí podle `user_logins` (`provider = google`, `provider_key` = identifikátor Google);
- jinak podle e-mailu, ale jen když Google e-mail označí jako ověřený;
- návratová adresa MUST být relativní cesta aplikace, jinak `400 return_path.invalid`.

#### Scenario: Propojení existujícího účtu ověřeným e-mailem
- GIVEN účet `jana@bylinkovo.sk` přihlašovaný odkazem, bez propojení s Google
- WHEN se přihlásí přes Google se stejným ověřeným e-mailem
- THEN vznikne `iam.user_logins` s `provider = google` a uživatel je přihlášený
- AND audit obsahuje `auth.google_linked`

#### Scenario: Neověřený e-mail Google
- GIVEN Google vrátí `email_verified = false`
- WHEN `GET /api/auth/google/complete` zpracuje odpověď
- THEN API přesměruje na přihlašovací stránku s `?error=google.email_not_verified`
- AND žádný účet ani propojení nevznikne

#### Scenario: Otevřené přesměrování
- GIVEN útočník připraví `GET /api/auth/google/start?returnPath=//zly.example/`
- WHEN prohlížeč adresu otevře
- THEN API odpoví `400` s kódem `return_path.invalid` a na Google nepřesměruje

### Requirement: Relace, cookies a ochrana CSRF
Systém MUST držet přihlášení jen v cookie `__Host-eg_session` (`HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`). JavaScript nesmí dostat žádný token relace.
- Každý požadavek POST, PUT, PATCH a DELETE pod `/api` MUST nést platnou hlavičku `X-CSRF-TOKEN`, i před přihlášením. Výjimkou jsou jen koncové body pod `/api/webhooks/`.
- Nepřihlášený požadavek MUST dostat `401 auth.unauthenticated`, ne přesměrování.
- `POST /api/auth/logout-everywhere` MUST ukončit všechny relace uživatele výměnou `security_stamp`.

#### Scenario: Požadavek bez CSRF tokenu
- GIVEN přihlášený uživatel s platnou cookie
- WHEN přijde `PATCH /api/me` bez hlavičky `X-CSRF-TOKEN`
- THEN API odpoví `400` s kódem `csrf.invalid` a nic nezmění

#### Scenario: Koncový bod bez politiky nespustí aplikaci
- GIVEN vývojář přidá koncový bod `POST /api/t/{tenantId}/x` bez `.RequireTenantRole()`
- WHEN se spustí API nebo test `EndpointPolicyTests`
- THEN start selže s chybou, která koncový bod jmenuje

#### Scenario: Odhlášení všude
- GIVEN uživatel přihlášený v prohlížeči A a B
- WHEN v prohlížeči A zavolá `POST /api/auth/logout-everywhere`
- THEN prohlížeč A je odhlášený hned
- AND prohlížeč B dostane `401 auth.unauthenticated` nejpozději po intervalu kontroly `security_stamp`

### Requirement: Tenanti a členství s rolemi
Systém MUST vést účty zákazníků (tenanty) a členství uživatelů s rolí `owner`, `admin`, `editor` nebo `viewer` podle matice oprávnění v designu.
- Každý tenant MUST mít aspoň jednoho vlastníka. Odebrání nebo snížení role posledního vlastníka MUST skončit `409 membership.last_owner`.
- Nový účet bez čekající pozvánky MUST dostat vlastního tenanta s rolí `owner`.
- Uživatel může být členem více tenantů. `GET /api/me` je MUST vrátit všechny s rolí.

#### Scenario: Nový účet dostane tenanta
- GIVEN e-mail bez účtu a bez čekající pozvánky
- WHEN uživatel použije odkaz na přihlášení s `market = sk`
- THEN vznikne `iam.tenants` (`market_code = sk`, `locale = sk`, `currency = NULL`) a členství `owner`
- AND `SessionDto.createdTenantId` obsahuje jeho ID

#### Scenario: Poslední vlastník nemůže odejít
- GIVEN tenant s jediným vlastníkem Janou
- WHEN Jana zavolá `DELETE /api/t/{tenantId}/members/{janaId}`
- THEN API odpoví `409 membership.last_owner` a členství zůstane

#### Scenario: Admin nesmí povýšit na admina
- GIVEN uživatel s rolí `admin`
- WHEN pošle `PATCH /api/t/{tenantId}/members/{userId}` s `role = admin`
- THEN API odpoví `403 membership.role_not_allowed`

#### Scenario: Předání vlastnictví
- GIVEN vlastník Jana a admin Peter
- WHEN Jana zavolá `POST /api/t/{tenantId}/ownership-transfer` s ID Petera
- THEN Peter má roli `owner` a Jana `admin`
- AND audit obsahuje `membership.ownership_transferred`

### Requirement: Přístup k tenantovi jen přes členství
Systém MUST brát tenanta u koncových bodů `/api/t/{tenantId}/…` z adresy a ověřit ho proti členství přihlášeného uživatele dřív, než sáhne na data:
- bez členství, i když tenant existuje, MUST vrátit `404 tenant.not_found`;
- s nižší než minimální rolí koncového bodu MUST vrátit `403 auth.forbidden_role`;
- při povolení MUST nastavit kontext tenanta, takže každá transakce běží s `app.tenant_id` pod rolí `eshopguard_app` (RLS).

#### Scenario: Cizí tenant vypadá jako neexistující
- GIVEN uživatel tenanta A a tenant B, oba s e-shopem `vegis.sk`
- WHEN uživatel A zavolá `GET /api/t/{tenantB}`
- THEN API odpoví `404 tenant.not_found` se stejným tělem jako pro náhodné ID

#### Scenario: Čtenář nemůže měnit
- GIVEN uživatel s rolí `viewer`
- WHEN zavolá `PATCH /api/t/{tenantId}` s novým názvem
- THEN API odpoví `403` s `code = auth.forbidden_role` a `params.requiredRole = admin`

#### Scenario: Chyba ve filtru dotazu nevrátí cizí data
- GIVEN testovací koncový bod, který čte `iam.invitations` čistým SQL bez podmínky na tenanta
- WHEN ho zavolá člen tenanta A
- THEN vrátí jen pozvánky tenanta A, protože RLS filtruje podle `app.tenant_id`

### Requirement: Pozvánky do účtu
Systém MUST umožnit adminům a vlastníkům pozvat uživatele e-mailem s rolí `admin` (jen vlastník), `editor` nebo `viewer`:
- pozvánka je jednorázový token platný `Invitations:ValidDays`, v databázi jen otisk SHA-256;
- e-mail MUST odejít v jazyce příjemce;
- přijetí odkazem MUST ověřit e-mail a vytvořit účet, pokud neexistuje;
- přihlášený uživatel s jiným e-mailem MUST dostat `409 invitation.email_mismatch`;
- zrušená, vypršelá nebo použitá pozvánka MUST vrátit kód a nesmí vytvořit členství.

#### Scenario: Pozvání editora bez účtu
- GIVEN vlastník tenanta „Bylinkovo“
- WHEN pošle `POST /api/t/{tenantId}/invitations` s `{ email: "peter@agentura.cz", role: "editor", locale: "cs" }`
- THEN vznikne `iam.invitations` s `token_hash` a platností 7 dní (návrh)
- AND odejde český e-mail „invitation“ s odkazem s tokenem za `#`

#### Scenario: Přijetí pozvánky vytvoří účet a členství
- GIVEN platná pozvánka pro `peter@agentura.cz` bez účtu
- WHEN nepřihlášený prohlížeč pošle `POST /api/invitations/accept` s tokenem
- THEN vznikne účet s `email_confirmed = true`, členství `editor` a relace s `amr = invitation`
- AND pro Petera se nezaloží vlastní tenant
- AND pozývajícímu se do `ops.outbox` zapíše e-mail „invitation_accepted“ bez tokenu

#### Scenario: Pozvánka pro jiný e-mail
- GIVEN Jana je přihlášená a otevře pozvánku pro `peter@agentura.cz`
- WHEN pošle `POST /api/invitations/accept`
- THEN API odpoví `409 invitation.email_mismatch` a členství nevznikne

#### Scenario: Zrušená pozvánka
- GIVEN admin pozvánku zrušil přes `DELETE /api/t/{tenantId}/invitations/{id}`
- WHEN pozvaný pošle `accept` s jejím tokenem
- THEN API odpoví `410 invitation.expired` a členství nevznikne

### Requirement: Jazyk uživatele a e-mailů
Systém MUST určovat jazyk uživatele v tomto pořadí:
1. `users.locale`;
2. první podporovaný jazyk z `Accept-Language`;
3. výchozí jazyk trhu tenanta, u nepřihlášeného trhu vydání webu.

Povolené jsou jen jazyky se `ref.locales.enabled = true`. API MUST vracet kódy a parametry, ne věty. E-maily MUST být v jazyce příjemce.

#### Scenario: Uložená volba má přednost
- GIVEN uživatel s `users.locale = cs` a prohlížečem s `Accept-Language: sk-SK`
- WHEN zavolá `GET /api/me`
- THEN `effectiveLocale = cs` a `localeSource = user`

#### Scenario: Nepodporovaný jazyk
- GIVEN zapnuté jsou jen `sk` a `cs`
- WHEN uživatel pošle `PUT /api/me/locale` s `{ locale: "de" }`
- THEN API odpoví `400` s kódem `locale.not_enabled` a `users.locale` se nezmění

#### Scenario: Bez volby a bez známého jazyka prohlížeče
- GIVEN nový uživatel bez `users.locale` s `Accept-Language: en-US`, tenant s `market_code = cz`
- WHEN systém skládá e-mail pro tohoto uživatele
- THEN e-mail je česky (výchozí jazyk trhu `cz`)

#### Scenario: Šablona chybí v jednom jazyce
- GIVEN šablona `invitation` existuje slovensky, ale ne česky
- WHEN běží test úplnosti šablon
- THEN test selže a jmenuje chybějící soubor `Email/Templates/cs/invitation.*`

### Requirement: Audit bezpečnostních událostí
Systém MUST zapsat do `ops.audit_log`:
- každé přihlášení (úspěšné i neúspěšné, s metodou);
- zablokování;
- nastavení, odebrání a obnovu hesla;
- propojení Google;
- vznik účtu a tenanta;
- změnu role, odebrání člena, předání vlastnictví;
- vytvoření, zrušení a přijetí pozvánky.

Záznam MUST NOT obsahovat token, otisk tokenu, heslo ani čitelnou e-mailovou adresu. Provozní logy MUST NOT obsahovat token, heslo ani čitelný e-mail.

#### Scenario: Neúspěšné přihlášení v auditu
- GIVEN chybné heslo pro existující účet
- WHEN API vrátí `401`
- THEN `ops.audit_log` obsahuje `auth.login_failed` s `actor_user_id` účtu, `tenant_id = NULL` a `data.method = password`
- AND `data` neobsahuje heslo ani e-mail, jen `emailHash`

#### Scenario: Logy bez tajemství
- GIVEN zachytávání všech logů během testu toku odkazu, obnovy hesla a pozvánky
- WHEN test projde logy
- THEN žádný záznam neobsahuje token z e-mailu, heslo ani testovací e-mailovou adresu

### Requirement: Chybové odpovědi jako kódy a popis OpenAPI
Systém MUST vracet chyby jako `application/problem+json`:
- s polem `code`, objektem `params` a `traceId`;
- bez lidsky čitelné věty;
- validační chyby jako `validation.failed` s kódy po polích;
- neočekávaná výjimka MUST vrátit `500 internal_error` bez podrobností.

Popis API MUST být dostupný jako OpenAPI dokument `v1`, který u operací uvádí možné kódy chyb (`x-problem-codes`).

#### Scenario: Neočekávaná chyba neprozradí vnitřek
- GIVEN služba vyhodí nečekanou výjimku s textem SQL dotazu
- WHEN klient dostane odpověď
- THEN je to `500` s `code = internal_error` a `traceId`, bez textu výjimky a bez výpisu zásobníku

#### Scenario: Neplatný e-mail
- GIVEN `POST /api/auth/login-link` s `{ email: "jana@" }`
- WHEN API požadavek ověří
- THEN odpoví `400` s `code = validation.failed` a `errors.email = ["email.invalid_format"]`

#### Scenario: Snímek OpenAPI se nezmění nečekaně
- GIVEN uložený snímek `Snapshots/openapi-v1.json`
- WHEN změna upraví tvar DTO bez aktualizace snímku
- THEN test `OpenApiSnapshotTests` selže a ukáže rozdíl
