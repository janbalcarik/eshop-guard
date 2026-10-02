# Design: Identita, přihlášení, tenanti a role v API

## Technical Approach

### Vrstvy

| Projekt | Co přidá tato změna |
|---|---|
| `src/EshopGuard.Application` (nový) | Služby identity, tenantů, členství, pozvánek, jazyka, e-mailů a auditu. Validátory vracející kódy. Žádná závislost na ASP.NET Core kromě `Microsoft.Extensions.Identity.Core`. |
| `src/EshopGuard.Api` | Minimal API: skupiny koncových bodů, autentizace cookie, Google, antiforgery, limiter, ProblemDetails, OpenAPI, filtr přístupu k tenantovi. Žádná doménová logika. |
| `src/EshopGuard.Data` | Úložiště Identity nad `iam.users`, `iam.user_logins`, `iam.user_tokens`. Rozšíření interceptoru o `app.user_id`. Migrace politik a funkcí. |
| `src/EshopGuard.Jobs` | Obsluha `ops.outbox` druhu `email` (odesílání ostatních e-mailů), úklid tokenů a limitů. |
| `tests/EshopGuard.Api.Tests` | `WebApplicationFactory<Program>` proti lokálnímu PostgreSQL (`eshopguard_test`, role `eshopguard_app`), `FakeTimeProvider`, `CapturingEmailTransport`. |
| `tests/EshopGuard.Application.Tests` (nový) | Čisté testy služeb (jazyk, pravidla rolí, šablony). |

### Koncové body

Všechny cesty jsou pod `/api`. Před přihlášením jen `/api/auth/*`, `/api/invitations/*` a `/api/ref/*`. Všechny měnící požadavky (POST, PUT, PATCH, DELETE) vyžadují hlavičku `X-CSRF-TOKEN`.

| Metoda a cesta | Kdo | Tělo → odpověď | Chybové kódy |
|---|---|---|---|
| `GET /api/auth/csrf` | kdokoli | → `200 CsrfTokenDto { token }`, nastaví `__Host-eg_csrf` | – |
| `POST /api/auth/login-link` | kdokoli | `{ email }` → `202 LoginLinkRequestedDto { expiresInSeconds: 900, resendAfterSeconds: 60 }` | `validation.failed`, `rate_limited` (`429`, `params.scope` = `email_cooldown`/`email_hourly`/`ip_hourly`, `params.retryAfterSeconds`), `email.send_failed` (`503`) |
| `POST /api/auth/login-link/inspect` | kdokoli | `{ token }` → `200 LoginLinkInfoDto { email, isNewAccount, expiresAt }` | `login_link.invalid` (`404`), `login_link.expired` (`410`), `login_link.used` (`410`) |
| `POST /api/auth/login-link/consume` | kdokoli | `{ token, market? }` → `200 SessionDto { me: MeDto, isNewAccount, createdTenantId? }` + cookie relace | jako `inspect`, navíc `auth.account_disabled` (`403`) |
| `POST /api/auth/password/login` | kdokoli | `{ email, password }` → `200 SessionDto` | `auth.invalid_credentials` (`401`), `rate_limited` |
| `POST /api/auth/password/forgot` | kdokoli | `{ email }` → `202` (vždy stejně) | `rate_limited`, `email.send_failed` |
| `POST /api/auth/password/reset/inspect` | kdokoli | `{ token }` → `200 { email, expiresAt }` | `reset_link.invalid`, `reset_link.expired`, `reset_link.used` |
| `POST /api/auth/password/reset` | kdokoli | `{ token, newPassword }` → `200 SessionDto` | jako `inspect`, `password.too_short`, `password.same_as_email` |
| `GET /api/auth/google/start?returnPath=` | kdokoli | → `302` na Google | `return_path.invalid` (`400`) |
| `GET /api/auth/google/complete` | po návratu z Google | → `302` na `Frontend:BaseUrl` + `returnPath`, při chybě na přihlašovací stránku s `?error=<kód>` | `google.email_not_verified`, `google.failed` |
| `POST /api/auth/logout` | přihlášený | → `204` | – |
| `POST /api/auth/logout-everywhere` | přihlášený | → `204` (nový `security_stamp`) | – |
| `GET /api/me` | přihlášený | → `200 MeDto` | `auth.unauthenticated` (`401`) |
| `PATCH /api/me` | přihlášený | `{ displayName }` → `200 MeDto` | `validation.failed` |
| `PUT /api/me/locale` | přihlášený | `{ locale \| null }` → `204` | `locale.not_enabled` (`400`) |
| `PUT /api/me/password` | přihlášený | `{ currentPassword?, newPassword }` → `204` | `password.current_invalid`, `auth.reauthentication_required` (`403`), `password.too_short`, `password.same_as_email` |
| `DELETE /api/me/password` | přihlášený | → `204` | `auth.reauthentication_required` |
| `DELETE /api/me/logins/google` | přihlášený | → `204` | `login.not_linked` (`404`) |
| `POST /api/me/invitations/{invitationId}/accept` | přihlášený | → `200 MembershipDto` | `invitation.not_found`, `invitation.expired`, `invitation.email_mismatch` |
| `POST /api/tenants` | přihlášený | `{ name, market }` → `201 TenantDto` | `market.unknown`, `tenant.limit_reached` (`409`) |
| `GET /api/t/{tenantId}` | viewer | → `200 TenantDto` | `tenant.not_found` (`404`) |
| `PATCH /api/t/{tenantId}` | admin | `{ name }` → `200 TenantDto` | `validation.failed`, `concurrency.conflict` |
| `GET /api/t/{tenantId}/members` | admin | → `200 MemberDto[]` | – |
| `PATCH /api/t/{tenantId}/members/{userId}` | admin (admin↔ jen owner) | `{ role }` → `200 MemberDto` | `membership.not_found`, `membership.last_owner`, `membership.role_not_allowed` (`403`) |
| `DELETE /api/t/{tenantId}/members/{userId}` | admin, nebo kdokoli sám sebe | → `204` | `membership.last_owner`, `membership.role_not_allowed` |
| `POST /api/t/{tenantId}/ownership-transfer` | owner | `{ userId }` → `204` | `membership.not_found` |
| `GET /api/t/{tenantId}/invitations` | admin | → `200 InvitationDto[]` (bez tokenu) | – |
| `POST /api/t/{tenantId}/invitations` | admin (role `admin` jen owner) | `{ email, role, locale? }` → `201 InvitationDto` | `invitation.already_member`, `membership.role_not_allowed`, `rate_limited`, `email.send_failed` |
| `POST /api/t/{tenantId}/invitations/{id}/resend` | admin | → `204` (nový token, starý neplatí) | `invitation.not_found`, `invitation.used` |
| `DELETE /api/t/{tenantId}/invitations/{id}` | admin | → `204` | `invitation.not_found` |
| `POST /api/invitations/inspect` | kdokoli | `{ token }` → `200 InvitationInfoDto { tenantName, inviterDisplayName, role, email, expiresAt, accountExists }` | `invitation.invalid`, `invitation.expired`, `invitation.used` |
| `POST /api/invitations/accept` | kdokoli | `{ token, market? }` → `200 SessionDto` + `MembershipDto` | jako `inspect`, `invitation.email_mismatch` (`409`) |
| `GET /api/ref/locales` | kdokoli | → `200 LocaleDto[] { code, name }` (jen `enabled`) | – |
| `GET /api/ref/markets` | kdokoli | → `200 MarketDto[] { code, countryCode, defaultLocale, uiLocales, currency, webStatus, checksStatus }` (jen `web_status` `preview`/`live`) | – |

**DTO (výběr):**
- `MeDto { id, email, displayName, locale, effectiveLocale, localeSource (user|accept_language|market), hasPassword, googleLinked, memberships: [{ tenantId, tenantName, legalName, role, status }], pendingInvitations: [{ invitationId, tenantName, role, expiresAt }] }`;
- `TenantDto { id, name, legalName, countryCode, marketCode, locale, currency, status, myRole, version }`;
- `MemberDto { userId, email, displayName, role, invitedBy, joinedAt, lastLoginAt }`;
- `InvitationDto { id, email, role, invitedBy, expiresAt, acceptedAt, status (pending|accepted|expired) }`.

Žádné DTO nevrací token, otisk tokenu ani heslo.

### Matice oprávnění (návrh, K rozhodnutí 2)

Role jsou uspořádané: `viewer` < `editor` < `admin` < `owner`. Koncový bod deklaruje minimální roli metadaty `.RequireTenantRole(TenantRole.X)`.

| Oblast | viewer | editor | admin | owner |
|---|---|---|---|---|
| Čtení dat účtu (e-shopy, nálezy, doklady, protokoly, průběh) | ano | ano | ano | ano |
| Rozhodnutí o nálezech, odpovědi, doklady, schválení a publikace oprav (změna 11) | – | ano | ano | ano |
| Vygenerovat protokol PDF (změna 11); stáhnout ho smí každý člen | – | ano | ano | ano |
| Zrušit běh ukázky nebo opakované kontroly (změna 11) | – | – | ano | ano |
| Vlastní upozornění a jejich nastavení (změna 11) | ano | ano | ano | ano |
| E-shopy: přidání, onboarding, nastavení, ověření vlastnictví (změna 10) | – | – | ano | ano |
| Objednávka, platby, předplatné (změny 10 a 12) | – | – | ano | ano |
| Členové: seznam, pozvat nebo odebrat editora či čtenáře, změna editor ↔ čtenář | – | – | ano | ano |
| Pozvat, povýšit nebo odebrat admina | – | – | – | ano |
| Předání vlastnictví | – | – | – | ano |
| Přejmenování účtu | – | – | ano | ano |
| Odchod z účtu (sám sebe) | ano | ano | ano | ano, pokud není poslední vlastník |

## Architecture Decisions

**AD 1. Vlastní úložiště Identity místo `IdentityDbContext`.**
- Datový model (změna 3) má vlastní tvar `iam.user_tokens` (`purpose`, `token_hash`, `expires_at`, `used_at`, `requested_ip_hash`), který neodpovídá `AspNetUserTokens` (`LoginProvider`, `Name`, `Value`).
- `EgUserStore` implementuje `IUserStore`, `IUserEmailStore`, `IUserPasswordStore`, `IUserSecurityStampStore`, `IUserLockoutStore` a `IUserLoginStore` nad entitami ze změny 3.
- Z Identity se použije `UserManager`, `SignInManager`, `PasswordHasher` (formát V3) a zablokování.
- Odmítnutá alternativa: tabulky `AspNet*` vedle `iam`. Znamenala by dva zdroje pravdy o uživateli.

**AD 2. Jednorázové tokeny (odkaz na přihlášení, obnova hesla, pozvánka).**
- `OneTimeTokenService.Create()` vrátí 32 bajtů z `RandomNumberGenerator`, kódovaných base64url (43 znaků). Do databáze jde `SHA256(bytes)` jako `bytea` (`token_hash`, jedinečný index ze změny 3).
- Spotřebování je jeden příkaz:

  ```sql
  UPDATE iam.user_tokens SET used_at = now()
  WHERE token_hash = @h AND purpose = @p AND used_at IS NULL AND expires_at > now()
  RETURNING id, user_id, email
  ```

  Při nule řádků jeden dotaz rozliší `used` / `expired` / `invalid`. Dvě souběžná kliknutí tak dají jedno přihlášení.
- Nový odkaz pro stejný e-mail a účel nastaví nepoužitým tokenům `expires_at = now()` ve stejné transakci, ve které vzniká nový token.
- Token je ve fragmentu adresy (`{Frontend:BaseUrl}{Frontend:LoginLinkPath}#t=…`). Prohlížeč ho neposílá serveru, takže se nedostane do logů Caddy, Next.js ani API a ani do hlavičky `Referer`. Stránka frontendu (GET) jen ukáže tlačítko „Prihlásiť sa“. Token pošle až `POST /api/auth/login-link/consume`. Náhled odkazu v poště ani antivir proto odkaz nespotřebují.
- `inspect` token nespotřebuje. Slouží stránce 2c k zobrazení „Pokračujete ako jana@bylinkovo.sk“ a ke zjištění, jestli jde o nový účet.

**AD 3. E-maily s tokenem se posílají přímo z API.**
- Řádek v `ops.outbox` by musel obsahovat token. Pravidlo „v databázi jen otisk“ to vylučuje (databáze, část 9, bod 1).
- `LoginLinkService`, `PasswordService` a `InvitationService` volají `IEmailTransport.SendAsync` v požadavku s časovým limitem `Email:SendTimeoutSeconds` (návrh 10 s) a jedním opakováním.
- Když odeslání selže, token se v téže transakci zneplatní a API vrátí `503 email.send_failed`. Odpověď je stejná pro existující i neexistující účet.
- Ostatní e-maily (bez tokenu) jdou přes `ops.outbox` a `OutboxEmailDispatcher` ve workeru. `EmailComposer` odmítne z outboxu složit druh s příznakem `ContainsToken` (fail-closed).

**AD 4. Stejná odpověď a stejná práce pro existující i neexistující účet.**
- Obě cesty projdou stejnými limity, založí řádek `user_tokens` (u nového účtu `user_id = NULL`) a odešlou e-mail ze stejné šablony `login_link`.
- Rozdíl je jen parametr `isNewAccount` uvnitř e-mailu. Ten vidí jen majitel schránky.
- Odpověď API má pevné tělo `202`, bez e-mailu i bez příznaku účtu.

**AD 5. Limity v `ops.rate_limit_buckets` (globální přes instance).**

| Klíč | Kapacita | Doplnění | Účel |
|---|---|---|---|
| `auth:link:email:{hmac(email)}` | 5 | 5 za 3 600 s | 5 odkazů za hodinu na e-mail |
| `auth:link:ip:{hmac(ip)}` | 20 | 20 za 3 600 s | 20 odkazů za hodinu na IP |
| `auth:reset:email:{hmac(email)}` | 5 | 5 za 3 600 s | obnova hesla |
| `auth:reset:ip:{hmac(ip)}` | 20 | 20 za 3 600 s | obnova hesla |
| `auth:password:ip:{hmac(ip)}` | 30 | 30 za 3 600 s | hádání hesel z jedné IP (návrh) |
| `auth:invite:tenant:{tenantId}` | 30 | 30 za 3 600 s | rozesílání pozvánek (návrh) |

- Pauza 60 s mezi odkazy: `SELECT max(created_at) FROM iam.user_tokens WHERE email = @e AND purpose = 'magic_link'` (index `ix_user_tokens_email_purpose_created` z migrace této změny). Kontroluje se jako první, protože nic nespotřebuje.
- Pořadí kontrol odkazu: formát e-mailu → pauza 60 s → kbelík IP → kbelík e-mailu → zneplatnění předchozích → nový token → odeslání → audit. Odmítnutý požadavek nespotřebuje další kbelíky.
- Rezervace tokenu je atomická funkce ze změny 4 (`IRateLimitBuckets.TryTakeAsync(key, cost, capacity, refillPerSecond)`).
- Klíče obsahují jen HMAC (`Security:IpHashKey`, user-secrets). Nikdy čitelný e-mail nebo IP: tabulka je globální.
- Úloha `auth.cleanup` (denně, `resource_class = system`) maže plné kbelíky `auth:%` starší než 2 h a tokeny vypršelé před víc než 30 dny.
- Limiter ASP.NET Core v paměti je jen první síto proti zahlcení (`/api/auth/*` 30 požadavků za minutu na IP, ostatní 300 za minutu na uživatele; návrh). Neodpovídá za pravidla ze specifikace.

**AD 6. Tenant z adresy a role.**
- Skupina `app.MapGroup("/api/t/{tenantId:guid}")` má `TenantAccessFilter`:
  1. Bez přihlášení vrátí `401 auth.unauthenticated`.
  2. Načte členství (`iam.memberships`, politika `memberships_select_own`) a stav tenanta.
  3. Žádné členství, nebo `tenants.status = deleted` → `404 tenant.not_found`. Cizí tenant tak nejde odlišit od neexistujícího.
  4. `suspended` → `403 tenant.suspended`, kromě koncových bodů s metadaty `.AllowSuspendedTenant()` (K rozhodnutí 10).
  5. Role pod minimem z metadat → `403 auth.forbidden_role` s `params.requiredRole`.
  6. Nastaví `ITenantContext.TenantId` a `HttpContext.Items["eg.role"]`.
- Interceptor ze změny 3 pak v každé transakci volá `set_config('app.tenant_id', …, true)`. Tato změna ho rozšíří o `set_config('app.user_id', …, true)`.
- `EndpointPolicyValidator` (`IHostedService`, běží při startu) projde `EndpointDataSource`:
  - každý koncový bod pod `/api/t/` musí mít `TenantRoleMetadata`;
  - každý měnící koncový bod musí mít CSRF, nebo `.DisableCsrf()` pod `/api/webhooks/`.

  Jinak aplikace nenastartuje (fail-closed). Test totéž ověří i bez startu hostitele.

**AD 7. Relace a CSRF.**
- Cookie Identity `__Host-eg_session`:
  - `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`;
  - platnost 30 dní s posouváním (návrh);
  - `SecurityStampValidator` každých 5 minut.

  Odhlášení všude a změna hesla vymění `security_stamp` a ostatní relace do 5 minut skončí.
- `Lax`, ne `Strict`: odkaz z upozornění v e-mailu musí otevřít aplikaci přihlášenou.
- Přesměrování na přihlášení je vypnuté. Nepřihlášený dostane `401 auth.unauthenticated` a bez oprávnění `403`.
- Nároky relace: `sub`, `amr` (`magic_link`/`password`/`google`/`invitation`/`reset`), `auth_time`.
- Opětovné ověření: nastavení nebo odebrání hesla bez znalosti současného hesla smí jen relace s `auth_time` mladším než `Auth:ReauthenticationMinutes` (návrh 15).
- Antiforgery: hlavička `X-CSRF-TOKEN`, cookie `__Host-eg_csrf` (`SameSite=Strict`). `CsrfEndpointFilter` volá `IAntiforgery.ValidateRequestAsync` pro POST, PUT, PATCH a DELETE. Chybějící nebo neplatný token vrátí `400 csrf.invalid`. Platí i pro nepřihlášené koncové body (`login-link`, `password/login`) kvůli podvržení přihlášení.
- API je na stejném původu jako frontend pod `/api` (Caddy, změna 17), proto bez CORS.
- Klíče Data Protection: `PersistKeysToFileSystem(DataProtection:KeysPath)` na svazku Dockeru (K rozhodnutí 13).

**AD 8. Heslo a zablokování.**
- `IdentityOptions` (návrhy, K rozhodnutí 3):
  - `Password.RequiredLength = 10`, ostatní požadavky na skladbu vypnuté;
  - `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`, `AllowedForNewUsers = true`.
- Neexistující účet, účet bez hesla, špatné heslo i zablokovaný účet vrátí stejné `401 auth.invalid_credentials`. Z odpovědi nejde poznat, že účet existuje.
- Úspěšné přihlášení odkazem nebo Googlem prokazuje schránku, proto vynuluje `access_failed_count` a `lockout_end`.
- Obnova hesla: token `purpose = reset`, platnost `Auth:ResetLinkMinutes` (návrh 60).
  - U neexistujícího účtu se e-mail neodešle, odpověď je stejná `202`.
  - Po nastavení hesla se vymění `security_stamp`, vynuluje zablokování, nastaví `email_confirmed = true` a uživatel se přihlásí (`amr = reset`).

**AD 9. Google.**
- `AddGoogle`:
  - `SignInScheme = IdentityConstants.ExternalScheme`;
  - `CallbackPath = /api/auth/google/signin`;
  - rozsahy `openid email profile`;
  - mapování `verified_email` → `email_verified`.
- `ExternalLoginService.CompleteAsync`:
  1. Najde `user_logins` podle `provider = google` a `provider_key` (sub).
  2. Jinak, když `email_verified = true`, najde uživatele podle e-mailu a propojí ho (audit `auth.google_linked`).
  3. Jinak založí účet s `email_confirmed = true` a tenanta podle AD 11.

  Neověřený e-mail Google → `google.email_not_verified`, účet se nezaloží ani nepropojí.
- `returnPath` ověří `ReturnPathValidator`: musí začínat `/` a nesmí začínat `//` ani `/\`; bez schématu, nejvýš 512 znaků. Brání otevřenému přesměrování.
- Client ID a Secret: `Authentication:Google:ClientId` a `ClientSecret` z user-secrets nebo proměnných prostředí. Bez nich se Google nezaregistruje a `start` vrátí `404`. Aplikace jinak běží.

**AD 10. Jazyk.**
- `LocaleResolver.Resolve(user, acceptLanguage, marketHint)`:
  1. `users.locale`, pokud je v `ref.locales` zapnutý;
  2. jinak první položka `Accept-Language` (podle `q`), jejíž primární podznačka je zapnutý jazyk (`sk-SK` → `sk`);
  3. jinak `ref.markets.default_locale` trhu: tenant (`tenants.market_code`), u nepřihlášeného `market` z těla požadavku (vydání webu `sk`/`cz`), jinak `Localization:DefaultMarket` (`sk`).
- Katalog jazyků a trhů se drží v `IMemoryCache` 5 minut.
- Nový účet si uloží výsledek do `users.locale` (z požadavku `consume`, Google `complete` nebo `invitations/accept`).
- `PUT /api/me/locale` s `null` vrátí uživatele k automatice.
- Jazyk e-mailu je jazyk příjemce:
  - existující uživatel: `users.locale` a pak pořadí výše;
  - pozvaný bez účtu: `locale` z požadavku pozvání, jinak `tenants.locale`.

**AD 11. Tenant pro nový účet (návrh, K rozhodnutí 4).**
- Při vzniku účtu (`consume`, Google, ne při přijetí pozvánky) zavolá `TenantService.CreateForNewAccountAsync`:
  - když `iam.pending_invitations_for_email(email)` nic nevrátí, založí tenanta (`name = e-mail`, `market_code = market` nebo výchozí, `locale = ref.markets.default_locale`, `country_code` trhu, `currency = NULL`, `status = active`) a členství `owner`;
  - když pozvánka čeká, tenant se nezakládá a `MeDto.pendingInvitations` ji ukáže.
- `POST /api/tenants` zakládá další účty (agentura). Strop `Tenants:MaxOwnedPerUser` (návrh 20) → `409 tenant.limit_reached`.

**AD 12. Chyby jako kódy.**
- `EgProblem`: `type = "urn:eshopguard:problem:{code}"`, `title = code`, `status`, `code`, `params` (objekt), `errors` (u `validation.failed`: pole → seznam kódů), `traceId`.
- Žádná lidská věta. Text skládá frontend z `messages/{locale}.json` (změny 13 a 14).
- `DomainException(code, status, params)` vyhazují služby. `EgExceptionHandler` ji převede. Neočekávaná výjimka → `500 internal_error` bez podrobností, s `traceId`.
- Katalog kódů je `ProblemCodes` (konstanty).
- OpenAPI nese u operací rozšíření `x-problem-codes` (`ProblemCodesDocumentTransformer` z metadat `.ProducesProblemCodes(...)`). Frontend tak ví, které kódy přeložit.

**AD 13. Audit a logy.**
- `SecurityAuditWriter` zapisuje do `ops.audit_log`:
  - akce `auth.login_link_requested`, `auth.login_succeeded`, `auth.login_failed`, `auth.locked_out`, `auth.password_set`, `auth.password_removed`, `auth.password_reset`, `auth.google_linked`, `auth.google_unlinked`, `auth.logout_everywhere`;
  - akce `user.created`, `user.terms_accepted`;
  - akce `tenant.created`, `tenant.renamed`;
  - akce `membership.role_changed`, `membership.removed`, `membership.left`, `membership.ownership_transferred`;
  - akce `invitation.created`, `invitation.resent`, `invitation.revoked`, `invitation.accepted`.
- Události bez tenanta mají `tenant_id = NULL` (politika `audit_log_insert_auth`).
- Pole `data` nikdy neobsahuje token, otisk tokenu, heslo ani čitelný e-mail (jen `emailHash`, HMAC). `ip` je HMAC (K rozhodnutí 8).
- Provozní logy (`ILogger`, `[LoggerMessage]`) nesou jen `userId`, `tenantId`, `emailHash` a `traceId`. Logování těl a dotazů HTTP je pro `/api/auth/*` a `/api/invitations/*` vypnuté.

## Data Flow

### Vyžádání a použití odkazu (2a → 2b → 2c)

```
Prohlížeč 2a ──POST /api/auth/login-link {email}──▶ API
  LoginLinkService.RequestAsync
    1. EmailNormalizer.Normalize (trim, malá písmena, IDN → punycode); neplatný → 400 validation.failed
    2. pauza 60 s podle user_tokens.created_at       → 429 rate_limited {scope: email_cooldown}
    3. kbelík auth:link:ip:{hmac}                     → 429 {scope: ip_hourly}
    4. kbelík auth:link:email:{hmac}                  → 429 {scope: email_hourly}
    5. transakce: UPDATE nepoužité magic_link tokeny e-mailu SET expires_at = now()
                  INSERT user_tokens (user_id | NULL, email, 'magic_link', sha256, now()+15 min, hmac(ip))
    6. IEmailTransport.SendAsync(EmailComposer.Compose("login_link", locale, {link, minutes: 15, isNewAccount}))
       selhání → token zneplatnit → 503 email.send_failed
    7. audit auth.login_link_requested (emailHash, userId | null)
  ◀── 202 {expiresInSeconds: 900, resendAfterSeconds: 60}   (stejné pro existující i neexistující účet)

E-mail: {Frontend:BaseUrl}{Frontend:LoginLinkPath}#t=TOKEN
Prohlížeč 2c (GET stránka frontendu, nic nespotřebuje)
  ──POST /api/auth/login-link/inspect {token}──▶ {email, isNewAccount, expiresAt} | 410 expired/used | 404 invalid
  uživatel klikne „Prihlásiť sa“
  ──POST /api/auth/login-link/consume {token, market}──▶ LoginLinkService.ConsumeAsync
    1. atomický UPDATE … RETURNING (AD 2)            → 410/404 → UI nabídne „Poslať nový odkaz“
    2. user_id NULL → UserManager.CreateAsync(email, email_confirmed = true, locale = LocaleResolver)
                      audit user.created, user.terms_accepted
                      TenantService.CreateForNewAccountAsync (AD 11)
       jinak: deleted_at → 403 auth.account_disabled; email_confirmed = true; vynulovat zablokování
    3. SignInManager.SignInWithClaimsAsync(amr = magic_link, auth_time = now)
    4. users.last_login_at = now(); audit auth.login_succeeded
  ◀── 200 SessionDto + Set-Cookie __Host-eg_session
```

### Požadavek na data tenanta

```
GET /api/t/{tenantId}/… (cookie)
  autentizace cookie → ClaimsPrincipal (sub)
  IUserContext.UserId = sub
  TenantAccessFilter
    SELECT role FROM iam.memberships WHERE tenant_id = @t AND user_id = @u   (politika memberships_select_own)
    SELECT status FROM iam.tenants WHERE id = @t
    žádné členství / deleted → 404 tenant.not_found
    role < minimum z metadat → 403 auth.forbidden_role
    ITenantContext.TenantId = @t
  služba z EshopGuard.Application → EF Core (globální filtr tenanta)
    interceptor: set_config('app.tenant_id', @t, true), set_config('app.user_id', @u, true)
    PostgreSQL RLS: tenant_id = current_setting('app.tenant_id')::uuid
```

### Pozvánka

```
admin ──POST /api/t/{t}/invitations {email, role, locale?}──▶ InvitationService.CreateAsync
  člen se stejným e-mailem → 409 invitation.already_member
  role admin od admina → 403 membership.role_not_allowed
  čekající pozvánka stejného e-mailu → expires_at = now()
  INSERT iam.invitations (token_hash, expires_at = now()+7 d)
  e-mail „invitation“ přímo (jazyk příjemce podle AD 10)
  audit invitation.created
pozvaný ──POST /api/invitations/accept {token}──▶ InvitationService.AcceptAsync
  iam.find_invitation_by_token_hash(sha256)  (SECURITY DEFINER, vrací jen id, tenant_id, email, role, expires_at, accepted_at)
  přihlášený s jiným e-mailem → 409 invitation.email_mismatch
  nepřihlášený → najít nebo založit uživatele (email_confirmed = true), přihlásit (amr = invitation)
  ITenantContext = tenant pozvánky
  UPDATE invitations SET accepted_at = now() WHERE id = @id AND accepted_at IS NULL AND expires_at > now()
  INSERT memberships ON CONFLICT DO NOTHING (stávající člen si roli ponechá)
  ops.outbox: e-mail „invitation_accepted“ pozývajícímu (bez tokenu)
  audit invitation.accepted
```

### Ostatní e-maily (outbox)

```
změny 8, 11, 12, 16: INSERT ops.outbox (kind = 'email', payload = {toUserId | to, template, locale?, params})
worker OutboxEmailDispatcher (resource_class io):
  SELECT … WHERE kind = 'email' AND sent_at IS NULL AND attempts < Email:MaxAttempts
         AND created_at + backoff(attempts) <= now() FOR UPDATE SKIP LOCKED LIMIT 20
  jazyk: users.locale příjemce → payload.locale → výchozí trh
  EmailComposer.Compose (šablona s ContainsToken → chyba, řádek se neodešle a jde upozornění provozu)
  IEmailTransport.SendAsync → sent_at = now() | attempts + 1, error = kód
```

## File Changes

**Nový projekt `src/EshopGuard.Application/`:**
- `EshopGuard.Application.csproj` (net10.0; odkazy na `EshopGuard.Data`, `Microsoft.Extensions.Identity.Core`, `MailKit` jen v `Email/Smtp`).
- `ServiceCollectionExtensions.cs`: `AddEshopGuardApplication(IConfiguration)`.
- `Problems/ProblemCodes.cs`, `Problems/DomainException.cs`, `Problems/ValidationResult.cs`.
- `Security/OneTimeTokenService.cs`, `Security/OneTimeTokenPurpose.cs` (`MagicLink`, `Reset`, `Confirm`), `Security/IpHasher.cs`, `Security/EmailNormalizer.cs`, `Security/ReturnPathValidator.cs`.
- `Identity/LoginLinkService.cs`, `Identity/PasswordService.cs`, `Identity/ExternalLoginService.cs`, `Identity/SessionFactory.cs`, `Identity/Validators/*.cs`.
- `Tenants/TenantRole.cs`, `Tenants/TenantService.cs`, `Tenants/MembershipService.cs`, `Tenants/InvitationService.cs`, `Tenants/MembershipRules.cs` (čistá pravidla matice rolí).
- `Localization/LocaleResolver.cs`, `Localization/ILocaleCatalog.cs`, `Localization/IMarketCatalog.cs`, `Localization/RefCatalog.cs`.
- `Email/EmailComposer.cs`, `Email/IEmailTransport.cs`, `Email/EmailMessage.cs`, `Email/Smtp/SmtpEmailTransport.cs`.
- `Email/EmailTemplateKind.cs`:
  - `LoginLink`, `PasswordReset` a `Invitation` s `ContainsToken = true`;
  - `InvitationAccepted`;
  - `SampleFinished`, `RunFinished`, `RunPartial`, `RunFailed` pro řádky `ops.outbox`, které zapisuje změna 8 (ta šablony čeká v této změně).
- `Email/Templates/sk/*.subject.txt|*.html|*.txt`, `Email/Templates/cs/*` (vložené prostředky).
- `Audit/SecurityAuditWriter.cs`, `Audit/AuditActions.cs`.
- `RateLimits/AuthRateLimits.cs` (klíče a parametry z AD 5 nad `IRateLimitBuckets` ze změny 4).

**`src/EshopGuard.Api/`:**
- `Program.cs`:
  - registrace autentizace, Identity, Google, antiforgery, limiteru, ProblemDetails a OpenAPI;
  - skupiny `/api/auth`, `/api/me`, `/api/tenants`, `/api/t/{tenantId:guid}`, `/api/invitations`, `/api/ref`.
- `Auth/IdentitySetup.cs`, `Auth/CookieSetup.cs`, `Auth/GoogleSetup.cs`, `Auth/CsrfEndpointFilter.cs`, `Auth/CsrfMetadata.cs` (`.DisableCsrf()`).
- `Tenancy/TenantAccessFilter.cs`, `Tenancy/TenantRoleMetadata.cs` (`.RequireTenantRole()`, `.AllowSuspendedTenant()`), `Tenancy/EndpointPolicyValidator.cs`, `Tenancy/HttpUserContext.cs`.
- `Endpoints/AuthEndpoints.cs`, `Endpoints/MeEndpoints.cs`, `Endpoints/TenantEndpoints.cs`, `Endpoints/MemberEndpoints.cs`, `Endpoints/InvitationEndpoints.cs`, `Endpoints/RefEndpoints.cs`.
- `Contracts/*.cs`: `LoginLinkRequest`, `LoginLinkRequestedDto`, `LoginLinkInfoDto`, `ConsumeLoginLinkRequest`, `SessionDto`, `MeDto`, `TenantDto`, `MemberDto`, `InvitationDto`, `InvitationInfoDto`, `LocaleDto`, `MarketDto`, `CsrfTokenDto`.
- `Problems/EgProblem.cs`, `Problems/EgExceptionHandler.cs`, `Problems/ValidationEndpointFilter.cs`, `Problems/ProblemCodesMetadata.cs`.
- `OpenApi/ProblemCodesDocumentTransformer.cs`.
- `RateLimiting/RateLimiterSetup.cs`.
- `Security/SecurityHeadersMiddleware.cs` (HSTS, `X-Content-Type-Options`, `Referrer-Policy: no-referrer` pro `/api`).
- `appsettings.json`:
  - klíče bez tajemství: `Frontend:BaseUrl`, `Frontend:LoginLinkPath`, `Frontend:ResetPath`, `Frontend:InvitationPath`, `Frontend:LoginPath`;
  - `Auth:*` (`Password:MinLength`, `Lockout:MaxFailedAttempts`, `Lockout:Minutes`, `ReauthenticationMinutes`, `ResetLinkMinutes`), `Invitations:ValidDays`, `Email:From`, `Email:SendTimeoutSeconds`, `Email:MaxAttempts`, `Localization:DefaultMarket`, `Localization:TimeZone`, `Tenants:MaxOwnedPerUser`, `Legal:TermsVersion`, `Legal:PrivacyVersion`.

**`src/EshopGuard.Data/`:**
- `Identity/EgUserStore.cs`, `Identity/EgUser.cs` (nebo adaptér nad entitou `User` ze změny 3).
- `Tenancy/IUserContext.cs` a rozšíření interceptoru ze změny 3 o `app.user_id`.
- `Migrations/2026xxxx_IdentityPolicies.cs` + `Migrations/Sql/identity_policies.sql`:
  - `CREATE POLICY memberships_select_own ON iam.memberships FOR SELECT USING (user_id = nullif(current_setting('app.user_id', true), '')::uuid)`;
  - `CREATE FUNCTION iam.find_invitation_by_token_hash(bytea) … SECURITY DEFINER SET search_path = iam, pg_temp` (vlastník `eshopguard_owner`, `GRANT EXECUTE` jen `eshopguard_app`);
  - `CREATE FUNCTION iam.pending_invitations_for_email(text) … SECURITY DEFINER`;
  - `CREATE POLICY audit_log_insert_auth ON ops.audit_log FOR INSERT WITH CHECK (tenant_id IS NULL AND (action LIKE 'auth.%' OR action LIKE 'user.%'))`;
  - `CREATE INDEX ix_user_tokens_email_purpose_created ON iam.user_tokens (email, purpose, created_at DESC)`.

**`src/EshopGuard.Jobs/`:**
- `Outbox/OutboxEmailDispatcher.cs`.
- `Maintenance/AuthCleanupHandler.cs` (úloha `auth.cleanup`, plán denně).

**`deploy/docker-compose.dev.yml`:** služba `mailpit` (zachytává e-maily lokálně, SMTP 1025, web 8025).

**Testy:**
- `tests/EshopGuard.Api.Tests/`:
  - `ApiFactory.cs`, `TwoTenantsFixture.cs`, `CapturingEmailTransport.cs`, `FakeGoogleHandler.cs`;
  - `Auth/LoginLinkTests.cs`, `Auth/LoginLinkRateLimitTests.cs`, `Auth/PasswordTests.cs`, `Auth/GoogleLoginTests.cs`, `Auth/CsrfTests.cs`, `Auth/SessionTests.cs`;
  - `Tenants/TenantTests.cs`, `Tenants/MembershipTests.cs`, `Tenants/InvitationTests.cs`;
  - `Security/RoleMatrixTests.cs`, `Security/TenantIsolationApiTests.cs`, `Security/LogRedactionTests.cs`, `Security/EndpointPolicyTests.cs`;
  - `OpenApiSnapshotTests.cs`, `Snapshots/openapi-v1.json`.
- `tests/EshopGuard.Application.Tests/`: `LocaleResolverTests.cs`, `MembershipRulesTests.cs`, `EmailTemplateCompletenessTests.cs`, `OneTimeTokenServiceTests.cs`, `ReturnPathValidatorTests.cs`, `EmailNormalizerTests.cs`.
- `tests/EshopGuard.Jobs.Tests/Outbox/OutboxEmailDispatcherTests.cs`.

**Tabulky (čtení a zápis):**
- `iam.users`, `iam.user_logins`, `iam.user_tokens`, `iam.tenants`, `iam.memberships`, `iam.invitations`;
- `ops.rate_limit_buckets`, `ops.outbox`, `ops.audit_log`;
- jen čtení: `ref.locales`, `ref.markets`.

## Odchylky při implementaci (2. 10. 2026)

Implementace se řídí návrhy z oddílu K rozhodnutí v `proposal.md` (body 2, 3, 4, 7, 8, 9, 10 a 11 jako výchozí volby; potvrzení uživatele čeká). Proti návrhu výše se liší:

1. **Úložiště Identity je v `EshopGuard.Application`** (`Identity/EgUserStore.cs`), ne v `EshopGuard.Data`. Data nezávisí na Identity a nemá znát přihlašování. Uživatelské jméno je e-mail normalizovaný `EmailNormalizer` (malá písmena, punycode), vyhledání jde přes `lower(email)`; `ConcurrencyStamp` je token souběžnosti (migrace F5 mění jen model, ne schéma).
2. **Bez funkcí `SECURITY DEFINER`.** Vlastník tabulek nemá `BYPASSRLS` a tabulky mají `FORCE ROW LEVEL SECURITY`, takže by funkce vlastníka cizí řádky stejně neviděla. Místo funkcí `iam.find_invitation_by_token_hash` a `iam.pending_invitations_for_email` jsou politiky:
   - transakce uživatele bez tenanta má `app.tenant_id` nulové UUID (`TenantSql.NoTenant`), které žádný řádek nevlastní, a `app.user_id` uživatele (`ITenantContext.SetUser`, `ExecuteInUserTransactionAsync`, `TenantSql.BeginUserAsync`);
   - `memberships_select_own`, `invitations_select_own_email` (pozvánky na e-mail přihlášeného) a `invitations_select_by_token` (`app.invitation_hash` = otisk tokenu v transakci) platí jen v takové transakci (`ops.in_user_scope()`); transakce tenanta dál vidí jen svého tenanta.
   - `IUserContext` nevznikl: uživatel je v `ITenantContext` (`UserId`, `UserScope`).
3. **`iam.users.locale` může být prázdný** (migrace F5): `PUT /api/me/locale` s `null` vrací uživatele k automatice (AD 10). Nový účet si jazyk uloží.
4. **Výměna kontextu uvnitř transakce** (`SwitchTransactionContextAsync`): tenant nového účtu a jeho členství `owner`, přijetí pozvánky (členství, outbox, audit pod tenantem pozvánky) a čtení jako uživatel, který právě prokázal schránku, proběhnou v jedné transakci. Hodnoty jsou parametry, RLS kontroluje každý řádek.
5. **Odesílání z outboxu přes úlohy, ne `OutboxEmailDispatcher`.** `ops.outbox` má RLS po tenantovi a worker ho nemůže procházet napříč tenanty. Každý řádek proto dostane ve stejné transakci úlohu `email.send` svého tenanta (`Jobs/Outbox/OutboxEmails.cs`, deduplikace podle řádku) a pošle ho `Application/Email/EmailSendHandler.cs`:
   - fronta dává opakování s odstupem a jednoho odesílatele naráz; řádek drží `sent_at`, `attempts` a kód chyby;
   - jazyk: `users.locale` příjemce (zapnutý), jinak `payload.locale`, jinak jazyk tenanta;
   - druh s tokenem se z outboxu nesloží (`email.token_template_in_outbox`, log `Critical`).

   Změna 8 (`FinalizeHandler`) teď zapisuje jeden řádek na příjemce se šablonou `sample_finished`, `run_finished`, `run_partial` nebo `run_failed`; parametry (počty, e-shop, odkaz) čte až odesílání. Worker má `AddEshopGuardEmailDelivery` a validuje `Email` a `Frontend` při startu.
6. **Čas z `TimeProvider`:** tokeny, kbelíky limitů a audit používají čas aplikace (`@now`), ne `now()` databáze, aby testy s `FakeTimeProvider` ověřily vypršení v 15:01 a pauzu 60 s.
7. **Audit:** IP je jen `data.ipHash` (HMAC), sloupec `ip` zůstává prázdný (K rozhodnutí 8). Navíc akce `auth.password_reset_requested`. Zapisovač odmítne klíče `email`, `token`, `password` v `data`.
8. **Relace:** cookie nese jen `sub` a otisk `security_stamp` (žádný e-mail); `amr` a `auth_time` se při obnově principalu převezmou. Token CSRF je vázaný na uživatele, po přihlášení si ho frontend vyžádá znovu (`GET /api/auth/csrf`). Selhání u Googlu (odmítnutí, podvržený `state`) přesměruje na přihlašovací stránku s `?error=google.failed`.
9. **Pozastavený tenant:** `GET /api/t/{tenantId}` funguje i v pozastaveném tenantovi (čtení účtu, K rozhodnutí 10), ostatní koncové body vrací `403 tenant.suspended`.
10. **DTO odpovědí** jsou v `Application/Contracts`, těla požadavků v `Api/Contracts`. Chybějící tělo vrací `validation.failed` s `errors.body`, nečitelný JSON `request.invalid`.
11. **Jazyky v testech:** `ref.locales` má `sk` i `cs` vypnuté, dokud nebudou hotové texty frontendu; testy API je zapínají jen v katalogu své instance, sdílená testovací databáze zůstává podle seedu. Do zapnutí je jazyk každého uživatele jazyk trhu.
12. **Testy:**
    - „Chyba ve filtru dotazu nevrátí cizí data“ ověřuje čisté SQL bez podmínky na tenanta ve službách API v kontextu tenanta A (`TenantIsolationApiTests`), ne zvláštní testovací koncový bod;
    - 100 souběžných `ConsumeAsync` běží nejvýš po 20 spojeních (lokální PostgreSQL má `max_connections = 100` pro všechny testy);
    - Google se testuje přes skutečný handler s falešným back channel a adresami `google.invalid`.
13. **E-maily kromě odkazu na přihlášení nemají schválený návrh** (na plátně je jen 2b). Šablony `password_reset`, `invitation`, `invitation_accepted`, `sample_finished`, `run_finished`, `run_partial` a `run_failed` jsou ve stejném stylu a čekají na schválení textů; české znění je návrh ke kontrole.
14. **`Legal:TermsVersion` a `Legal:PrivacyVersion` jsou `0`**, dokud nejsou zveřejněné obchodní podmínky a zásady (K rozhodnutí 9).
15. **`/api` jen přes HTTPS** (nález ručního prokliku): cookies relace a CSRF jsou `Secure` a antiforgery čisté HTTP odmítá výjimkou. Požadavek pod `/api` bez HTTPS proto dostane `400 request.https_required` (`HttpsRequiredMiddleware`). Za Caddy (změna 17) se schéma bere z `X-Forwarded-Proto` jen od známé proxy (`Proxy:KnownProxies`, výchozí loopback); ve vývoji profil `https` v `launchSettings.json` (`https://localhost:5443`, `dotnet dev-certs https --trust`).
