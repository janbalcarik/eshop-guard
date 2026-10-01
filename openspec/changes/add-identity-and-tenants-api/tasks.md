# Tasks

## 1. Projekt Application a konvence API

- [ ] 1.1 Založit `src/EshopGuard.Application/EshopGuard.Application.csproj` (net10.0, odkaz na `EshopGuard.Data`) a `tests/EshopGuard.Application.Tests/` a zařadit je do řešení `EshopGuard.sln`.
- [ ] 1.2 `src/EshopGuard.Api/Program.cs`:
  - skupiny `/api/auth`, `/api/me`, `/api/tenants`, `/api/t/{tenantId:guid}`, `/api/invitations`, `/api/ref`;
  - `AddEshopGuardApplication(configuration)`.
- [ ] 1.3 `Application/Problems/ProblemCodes.cs` (konstanty všech kódů z `design.md`), `DomainException`, `ValidationResult`. `Api/Problems/EgProblem.cs`, `EgExceptionHandler` (`500 internal_error` bez podrobností, s `traceId`).
- [ ] 1.4 `Api/Problems/ValidationEndpointFilter<T>` a validátory v `Application/Identity/Validators/` vracející kódy (`email.invalid_format`, `password.too_short`, `password.same_as_email`, `name.too_long`).
- [ ] 1.5 OpenAPI:
  - `AddOpenApi("v1")`, `ProblemCodesDocumentTransformer` (`x-problem-codes`);
  - rozšíření `.ProducesProblemCodes(...)`;
  - dokument jen v prostředí Development a Test.
- [ ] 1.6 Test `ProblemDetailsTests`:
  - nečekaná výjimka → `500` bez textu výjimky;
  - validace → `errors` s kódy;
  - žádné tělo chyby neobsahuje pole `detail` s větou.
- [ ] 1.7 Test `OpenApiSnapshotTests` proti `tests/EshopGuard.Api.Tests/Snapshots/openapi-v1.json` (rozdíl = selhání s výpisem).

## 2. Identita nad `iam.users`

- [ ] 2.1 `Data/Identity/EgUser.cs` (adaptér nad entitou uživatele ze změny 3) a `EgUserStore`:
  - implementuje `IUserStore`, `IUserEmailStore`, `IUserPasswordStore`, `IUserSecurityStampStore`, `IUserLockoutStore`, `IUserLoginStore`;
  - e-mail bez rozlišení velikosti písmen.
- [ ] 2.2 `Api/Auth/IdentitySetup.cs`:
  - `AddIdentityCore<EgUser>().AddSignInManager()`;
  - `PasswordHasher` V3;
  - `IdentityOptions` z konfigurace `Auth:Password:*` a `Auth:Lockout:*` (návrh 10 znaků, 5 pokusů, 15 min).
- [ ] 2.3 `Data/Tenancy/IUserContext.cs` a rozšíření interceptoru ze změny 3 o `set_config('app.user_id', …, true)` v každé transakci.
- [ ] 2.4 Migrace `Data/Migrations/*_IdentityPolicies` + `Sql/identity_policies.sql`:
  - politika `memberships_select_own`;
  - funkce `iam.find_invitation_by_token_hash(bytea)` a `iam.pending_invitations_for_email(text)` (`SECURITY DEFINER`, `search_path` pevně, `GRANT EXECUTE` jen `eshopguard_app`);
  - politika `audit_log_insert_auth`;
  - index `ix_user_tokens_email_purpose_created`.
- [ ] 2.5 Test `EgUserStoreTests` (proti `eshopguard_test` jako `eshopguard_app`):
  - `Jana@Bylinkovo.SK` najde `jana@bylinkovo.sk`;
  - čítač zablokování a `security_stamp` se ukládají;
  - `user_logins` je jedinečné podle `provider_key`.
- [ ] 2.6 Test `IdentityPoliciesTests`:
  - jako `eshopguard_app` s `app.user_id` uživatel čte jen svá členství;
  - `find_invitation_by_token_hash` vrátí jen řádek se shodným otiskem;
  - zápis auditu s `tenant_id = NULL` a akcí `shop.x` selže.

## 3. Tokeny, e-maily a outbox

- [ ] 3.1 `Application/Security/OneTimeTokenService.cs`:
  - 32 bajtů z `RandomNumberGenerator`, base64url;
  - `SHA256` do `bytea`;
  - `ConsumeAsync` jedním `UPDATE … RETURNING`;
  - rozlišení `used` / `expired` / `invalid`.
- [ ] 3.2 `Application/Security/IpHasher.cs` a `EmailNormalizer.cs`:
  - HMAC-SHA256 s `Security:IpHashKey`; chybějící klíč zastaví start;
  - normalizace e-mailu (trim, malá písmena, IDN → punycode, nejvýš 254 znaků).
- [ ] 3.3 `Application/Email/EmailComposer.cs` a `EmailTemplateKind`:
  - `LoginLink`, `PasswordReset` a `Invitation` s `ContainsToken`; `InvitationAccepted`;
  - `SampleFinished`, `RunFinished`, `RunPartial`, `RunFailed` pro e-maily běhů, které do `ops.outbox` zapisuje změna 8 (jen kódy, počty a odkaz do aplikace, žádné texty stránek);
  - šablony `Email/Templates/{sk,cs}/{login_link,password_reset,invitation,invitation_accepted,sample_finished,run_finished,run_partial,run_failed}.{subject.txt,html,txt}` jako vložené prostředky;
  - HTML kódování parametrů.
- [ ] 3.4 `IEmailTransport`, `SmtpEmailTransport` (MailKit, `Email:Smtp:*` z user-secrets), `tests/…/CapturingEmailTransport.cs`. Služba `mailpit` v `deploy/docker-compose.dev.yml`.
- [ ] 3.5 `Jobs/Outbox/OutboxEmailDispatcher.cs`:
  - výběr `FOR UPDATE SKIP LOCKED`, odstup podle `attempts`, jazyk příjemce z `users.locale`;
  - druh s `ContainsToken` se z outboxu nesloží a jde upozornění provozu.
- [ ] 3.6 Test `EmailTemplateCompletenessTests`: každý `EmailTemplateKind` má ve všech jazycích z `ref.locales` (enabled) všechny tři soubory a stejnou sadu zástupných polí.
- [ ] 3.7 Test `OutboxEmailDispatcherTests`:
  - dva workery neodešlou stejný řádek dvakrát;
  - selhání zvýší `attempts` a zkusí znovu po odstupu;
  - druh s tokenem se neodešle.
- [ ] 3.8 Test `OneTimeTokenServiceTests`: 100 souběžných `ConsumeAsync` stejného tokenu → právě jeden úspěch.

## 4. Přihlášení odkazem v e-mailu

- [ ] 4.1 `Application/RateLimits/AuthRateLimits.cs`:
  - klíče `auth:link:email:*`, `auth:link:ip:*`, `auth:reset:*`, `auth:password:ip:*`, `auth:invite:tenant:*` s parametry z AD 5;
  - nad `IRateLimitBuckets` ze změny 4.
- [ ] 4.2 `Application/Identity/LoginLinkService.RequestAsync`: pořadí kontrol z `design.md` (Data Flow), zneplatnění předchozích tokenů, přímé odeslání, audit `auth.login_link_requested`.
- [ ] 4.3 `POST /api/auth/login-link` v `Api/Endpoints/AuthEndpoints.cs`: tělo `LoginLinkRequest`, odpověď `202 LoginLinkRequestedDto`, kódy `rate_limited` a `email.send_failed`.
- [ ] 4.4 `LoginLinkService.InspectAsync` a `POST /api/auth/login-link/inspect`: `200 LoginLinkInfoDto`, `404 login_link.invalid`, `410 login_link.expired` / `login_link.used`.
- [ ] 4.5 `LoginLinkService.ConsumeAsync` a `POST /api/auth/login-link/consume`:
  - nový účet (`email_confirmed`, `locale` z `LocaleResolver`);
  - `TenantService.CreateForNewAccountAsync`;
  - `auth.account_disabled` u smazaného účtu;
  - vynulování zablokování;
  - přihlášení s `amr = magic_link`;
  - `last_login_at`.
- [ ] 4.6 `Jobs/Maintenance/AuthCleanupHandler.cs` (úloha `auth.cleanup` denně): smaže tokeny vypršelé před víc než 30 dny a plné kbelíky `auth:%` starší než 2 h.
- [ ] 4.7 Test `LoginLinkTests`:
  - existující a neexistující účet mají stejné tělo i stejný druh e-mailu;
  - `inspect` nespotřebuje;
  - `consume` vytvoří účet a tenanta;
  - nový odkaz zneplatní starý;
  - vypršení v 15:01 (`FakeTimeProvider`);
  - souběžné `consume` → jeden úspěch;
  - token není v `user_tokens`, `ops.outbox` ani v logu.
- [ ] 4.8 Test `LoginLinkRateLimitTests`:
  - 60 s pauza s `retryAfterSeconds`;
  - 6. odkaz za hodinu na e-mail;
  - 21. požadavek z IP;
  - dvě instance `ApiFactory` sdílí limit přes databázi.

## 5. Heslo a Google

- [ ] 5.1 `Application/Identity/PasswordService.LoginAsync` a `POST /api/auth/password/login`:
  - stejné `401 auth.invalid_credentials` pro neexistující účet, účet bez hesla, špatné heslo a zablokování;
  - kbelík `auth:password:ip:*`;
  - audit `auth.login_failed` / `auth.locked_out`.
- [ ] 5.2 `PUT /api/me/password` a `DELETE /api/me/password`: současné heslo nebo `auth_time` do `Auth:ReauthenticationMinutes`, výměna `security_stamp`, audit `auth.password_set` / `auth.password_removed`.
- [ ] 5.3 `POST /api/auth/password/forgot`, `POST /api/auth/password/reset/inspect`, `POST /api/auth/password/reset`:
  - token `purpose = reset`, platnost `Auth:ResetLinkMinutes`;
  - u neexistujícího účtu `202` bez e-mailu;
  - po nastavení výměna `security_stamp` a přihlášení s `amr = reset`.
- [ ] 5.4 `Api/Auth/GoogleSetup.cs` (registrace jen při vyplněném `Authentication:Google:ClientId`), `GET /api/auth/google/start`, `GET /api/auth/google/complete`, `Application/Identity/ExternalLoginService.cs`, `Application/Security/ReturnPathValidator.cs`.
- [ ] 5.5 `DELETE /api/me/logins/google` s auditem `auth.google_unlinked`.
- [ ] 5.6 Test `PasswordTests`:
  - nerozlišitelné chyby;
  - zablokování a zrušení odkazem;
  - nové ověření po 15 minutách;
  - obnova hesla odhlásí ostatní relace po intervalu `SecurityStampValidator`.
- [ ] 5.7 Test `GoogleLoginTests` s `FakeGoogleHandler`:
  - propojení podle ověřeného e-mailu;
  - neověřený e-mail;
  - nový účet s tenantem;
  - `returnPath` `//zly.example/` → `400`.

## 6. Relace, CSRF a limity požadavků

- [ ] 6.1 `Api/Auth/CookieSetup.cs`:
  - `__Host-eg_session`, `HttpOnly`, `Secure`, `SameSite=Lax`;
  - 30 dní s posouváním;
  - `SecurityStampValidator` 5 minut;
  - 401/403 místo přesměrování;
  - `PersistKeysToFileSystem(DataProtection:KeysPath)`.
- [ ] 6.2 `GET /api/auth/csrf`, `AddAntiforgery` (`X-CSRF-TOKEN`, `__Host-eg_csrf`), `CsrfEndpointFilter` na všech měnících koncových bodech a `.DisableCsrf()` povolené jen pod `/api/webhooks/`.
- [ ] 6.3 `POST /api/auth/logout` a `POST /api/auth/logout-everywhere` s auditem `auth.logout_everywhere`.
- [ ] 6.4 `Api/RateLimiting/RateLimiterSetup.cs`: `/api/auth/*` 30 požadavků za minutu na IP, ostatní 300 za minutu na uživatele (návrh), odpověď `429 rate_limited`.
- [ ] 6.5 `Api/Security/SecurityHeadersMiddleware.cs`:
  - HSTS, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`;
  - vypnuté logování těl a dotazů pro `/api/auth/*` a `/api/invitations/*`.
- [ ] 6.6 Test `CsrfTests`:
  - každý měnící koncový bod bez hlavičky → `400 csrf.invalid`, tabulkově přes `EndpointDataSource`;
  - `login-link` bez tokenu → `400`.
- [ ] 6.7 Test `SessionTests`: atributy cookie, `401` bez přesměrování, odhlášení všude ukončí druhou relaci po intervalu (`FakeTimeProvider`).

## 7. Tenanti, členství a přístup k tenantovi

- [ ] 7.1 `Application/Tenants/TenantRole.cs` (pořadí viewer < editor < admin < owner) a `Api/Tenancy/TenantRoleMetadata.cs`:
  - `.RequireTenantRole(TenantRole)`;
  - `.AllowSuspendedTenant()`.
- [ ] 7.2 `Api/Tenancy/TenantAccessFilter.cs`:
  - `401` bez přihlášení;
  - `404 tenant.not_found` bez členství nebo u smazaného tenanta;
  - `403 tenant.suspended`;
  - `403 auth.forbidden_role` s `requiredRole`;
  - nastavení `ITenantContext`.
- [ ] 7.3 `Api/Tenancy/EndpointPolicyValidator.cs`: start selže u koncového bodu pod `/api/t/` bez role a u měnícího koncového bodu bez CSRF mimo `/api/webhooks/`.
- [ ] 7.4 `Application/Tenants/TenantService.cs`:
  - `CreateForNewAccountAsync` (AD 11), `CreateAsync` se stropem `Tenants:MaxOwnedPerUser`, `RenameAsync` se souběžností přes `xmin`;
  - koncové body `POST /api/tenants`, `GET /api/t/{tenantId}`, `PATCH /api/t/{tenantId}`.
- [ ] 7.5 `GET /api/me` a `PATCH /api/me` (`MeDto` s členstvími a čekajícími pozvánkami přes `iam.pending_invitations_for_email`).
- [ ] 7.6 `Application/Tenants/MembershipRules.cs` (čistá matice z `design.md`) a `MembershipService`:
  - `GET /api/t/{tenantId}/members`, `PATCH …/members/{userId}`, `DELETE …/members/{userId}`, `POST …/ownership-transfer`;
  - ochrana posledního vlastníka.
- [ ] 7.7 Test `MembershipRulesTests`: všechny kombinace (role volajícího × cílová role × akce) proti matici z `design.md`.
- [ ] 7.8 Test `TenantTests`:
  - nový účet bez pozvánky dostane tenanta;
  - nový účet s čekající pozvánkou tenanta nedostane;
  - strop vlastněných tenantů;
  - `PATCH` se starou verzí → `409 concurrency.conflict`.

## 8. Pozvánky

- [ ] 8.1 `Application/Tenants/InvitationService.CreateAsync`, `ResendAsync`, `RevokeAsync`:
  - kódy `invitation.already_member` a `membership.role_not_allowed`;
  - zneplatnění předchozí pozvánky stejného e-mailu;
  - kbelík `auth:invite:tenant:*`;
  - e-mail v jazyce příjemce.
- [ ] 8.2 Koncové body `GET/POST /api/t/{tenantId}/invitations`, `POST …/{id}/resend`, `DELETE …/{id}` (role admin).
- [ ] 8.3 `InvitationService.InspectAsync` / `AcceptAsync` a koncové body `POST /api/invitations/inspect`, `POST /api/invitations/accept`, `POST /api/me/invitations/{invitationId}/accept`:
  - atomické `accepted_at`;
  - `invitation.email_mismatch`;
  - přihlášení s `amr = invitation`.
- [ ] 8.4 Zápis e-mailu „invitation_accepted“ pozývajícímu do `ops.outbox` (bez tokenu).
- [ ] 8.5 Test `InvitationTests`:
  - pozvání editora bez účtu;
  - přijetí vytvoří účet bez vlastního tenanta;
  - jiný e-mail;
  - zrušená a vypršelá pozvánka;
  - admin nesmí pozvat admina;
  - stávající člen si ponechá roli.

## 9. Jazyk uživatele

- [ ] 9.1 `Application/Localization/LocaleResolver.cs`, `RefCatalog.cs` (`ref.locales` a `ref.markets` v `IMemoryCache` 5 minut), pořadí z AD 10.
- [ ] 9.2 `PUT /api/me/locale` (kód `locale.not_enabled`), `GET /api/ref/locales`, `GET /api/ref/markets` (jen `web_status` `preview`/`live`).
- [ ] 9.3 Test `LocaleResolverTests`:
  - `users.locale` vyhraje;
  - `Accept-Language: sk-SK;q=0.8, cs;q=0.9` → `cs`;
  - `en-US` → jazyk trhu;
  - vypnutý jazyk se ignoruje.
- [ ] 9.4 Test `EmailLanguageTests`:
  - odkaz pro uživatele s `locale = cs` je česky, i když přihlašovací stránka poslala `sk`;
  - pozvánka pro e-mail bez účtu je v jazyce `locale` z požadavku.

## 10. Audit

- [ ] 10.1 `Application/Audit/AuditActions.cs` a `SecurityAuditWriter.cs` (akce z AD 13, `emailHash`, `ip` jako HMAC, nikdy token ani heslo).
- [ ] 10.2 Zapojit audit do `LoginLinkService`, `PasswordService`, `ExternalLoginService`, `TenantService`, `MembershipService` a `InvitationService`. Při vzniku účtu `user.terms_accepted` s `Legal:TermsVersion` a `Legal:PrivacyVersion`.
- [ ] 10.3 Test `SecurityAuditTests`: každý tok z testů 4–8 zapíše očekávanou akci se správným `tenant_id` (nebo `NULL`) a `data` bez tajemství.

## 11. Ověření

- [ ] 11.1 `Security/RoleMatrixTests`:
  - projde všechny koncové body skupiny `/api/t/{tenantId}` z `EndpointDataSource`;
  - pro role viewer, editor, admin a owner ověří povolení nebo `403 auth.forbidden_role` podle metadat a matice;
  - nový koncový bod bez záznamu v očekávání test shodí.
- [ ] 11.2 `Security/TenantIsolationApiTests` s `TwoTenantsFixture` (dva tenanti, oba s e-shopem `vegis.sk`):
  - uživatel A na všech koncových bodech `/api/t/{tenantB}/…` dostane `404 tenant.not_found`;
  - `/api/me` neukáže tenanta B.
- [ ] 11.3 `Security/LogRedactionTests`: zachycené logy všech testů toků 4–8 neobsahují token, heslo ani čitelný e-mail.
- [ ] 11.4 Kontrola repozitáře: `git grep` nenajde `ClientSecret`, heslo SMTP ani `Security:IpHashKey` s hodnotou. Hodnoty jsou jen v user-secrets.
- [ ] 11.5 Ruční proklik lokálně přes Mailpit (bez placených služeb, bez Jevu a OpenAI):
  1. vyžádání odkazu;
  2. e-mail v Mailpitu;
  3. `inspect`;
  4. `consume`;
  5. `GET /api/me`;
  6. přepnutí jazyka;
  7. pozvánka a její přijetí ve druhém prohlížeči.
- [ ] 11.6 `dotnet build` a `dotnet test` projdou (včetně dosavadních testů knihovny).
- [ ] 11.7 `openspec validate add-identity-and-tenants-api` projde.
