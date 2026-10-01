# Proposal: Identita, přihlášení, tenanti a role v API

## Intent

**Problém.** EshopGuard zatím nemá API ani účty. Obrazovky 2a–2c (`Login.dc.html`, `LoginSent.dc.html`, `LoginConfirm.dc.html`), boční menu s menu účtu (`Sidebar.dc.html`, `AccountMenu.dc.html`: „Účet Bylinkovo s. r. o.“, „Jazyk“, „Odhlásiť sa“, „Majiteľka účtu“) i všechny další obrazovky potřebují:
- přihlášeného uživatele;
- účet zákazníka (tenant), ve kterém uživatel pracuje;
- jeho roli v tomto účtu.

Bez toho nejde postavit žádný další koncový bod: změny 10 a 11 stojí na pravidle „tenant z adresy ověřený proti členství“ a na konvenci „chyby jako kódy“.

**Proč teď.** F5 začíná identitou. Přihlášení odkazem v e-mailu je rozhodnuté (1. 10. 2026) jako výchozí způsob včetně pravidel. Fronta a `ops.outbox` (změna 4) a tabulky `iam` (změna 3) jsou podklad, na kterém se dá stavět.

**Přínos.**
- Přihlášení bez hesla odkazem v e-mailu jako výchozí. Heslo je volitelné a Google je další možnost. Nový účet vznikne až kliknutím na odkaz, takže e-mail je ověřený bez dalšího kroku.
- Odkaz nejde zneužít ani vyčerpat:
  - platí 15 minut a jen jednou;
  - v databázi je jen otisk SHA-256;
  - náhled v poště nebo antivir ho nespotřebuje;
  - z odpovědi nejde poznat, kdo je zákazník.
- Účet zákazníka s rolemi owner/admin/editor/viewer a pozvánkami. Agentura pracuje v několika účtech a přepíná mezi nimi.
- První úroveň izolace ze čtyř (architektura, část 3): API pustí do `/api/t/{tenantId}/…` jen člena tenanta a nastaví kontext tenanta pro RLS.
- Jazyk rozhraní a e-mailů podle uživatele (`users.locale`, jinak `Accept-Language`, jinak jazyk trhu).
- Bezpečnostní události v auditu:
  - přihlášení, neúspěšné pokusy a zablokování;
  - změny rolí a pozvánky.
- Společná konvence chyb (ProblemDetails s kódem) a popis OpenAPI. Na ní stojí změny 10–12 a generovaný klient frontendu (změna 13).

**Fáze:** F5 API (`databaze-a-plan-implementace-2026-10-01.md`, část 8).

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`:
  - část 3.1 `iam`: `tenants`, `users`, `user_logins`, `user_tokens`, `memberships`, `invitations`;
  - část 3.8: `ops.rate_limit_buckets`, `ops.outbox`, `ops.audit_log`;
  - část 3.9: `ref.markets`, `ref.locales`;
  - část 9, bod 1: pravidla přihlášení odkazem;
  - část 8: F5.
- `architektura-multitenant-worker-2026-10-01.md`:
  - část 2: API nedělá dlouhou práci;
  - část 3: model tenanta, uživatele a členství, izolace na čtyřech úrovních;
  - část 7: bezpečnost;
  - část 11, bod 6: přihlášení rozhodnuto;
  - část 12, Aplikace: volba jazyka, API vrací kódy, backend skládá e-maily.
- Návrh UI: 2a `Login.dc.html`, 2b `LoginSent.dc.html`, 2c `LoginConfirm.dc.html`, `Sidebar.dc.html`, `AccountMenu.dc.html`, úvodní stránka `Main.dc.html` (přepínač vydání SK/CZ, „Prihlásiť sa“).

## Scope

In scope:
- **Identita nad `iam.users`:** ASP.NET Core Identity (`AddIdentityCore`) s vlastním úložištěm nad tabulkami ze změny 3, hašování hesel, `security_stamp`, zablokování po neúspěšných pokusech.
- **Přihlášení odkazem v e-mailu:**
  - vyžádání odkazu;
  - kontrola odkazu bez spotřebování;
  - přihlášení tlačítkem (POST);
  - vznik nového účtu kliknutím;
  - zneplatnění předchozích odkazů;
  - limity odesílání;
  - „Poslať nový odkaz“ u vypršelého nebo použitého odkazu.
- **Heslo:** přihlášení heslem, nastavení a odebrání hesla v Nastaveniach, „Zabudnuté heslo“ a nastavení nového hesla.
- **Google:** přihlášení a založení účtu přes Google, propojení s existujícím účtem podle ověřeného e-mailu, odpojení.
- **Relace a ochrana:**
  - cookie `HttpOnly` / `Secure` / `SameSite=Lax`;
  - ochrana CSRF hlavičkou;
  - odhlášení a odhlášení všude;
  - omezení počtu požadavků;
  - zablokování účtu po opakovaném chybném hesle.
- **Tenanti:**
  - založení tenanta pro nový účet bez pozvánky;
  - ruční založení dalšího tenanta;
  - čtení a přejmenování tenanta;
  - seznam tenantů uživatele pro přepínání účtu.
- **Členství a role** owner/admin/editor/viewer: seznam členů, změna role, odebrání, odchod, předání vlastnictví, ochrana posledního vlastníka.
- **Pozvánky:** vytvoření, odeslání v jazyce příjemce, přijetí odkazem (ověří e-mail), přijetí čekající pozvánky přihlášeným uživatelem, zrušení, opětovné odeslání, vypršení.
- **Přístup k tenantovi:**
  - skupina koncových bodů `/api/t/{tenantId}/…` s filtrem členství a minimální role;
  - nastavení kontextu tenanta (`ITenantContext`, `app.tenant_id`) pro RLS;
  - kontrola při startu, že žádný koncový bod tenanta nemá chybějící politiku.
- **Jazyk:**
  - `GET/PUT` jazyka uživatele;
  - pořadí `users.locale` → `Accept-Language` → jazyk trhu;
  - jen zapnuté jazyky z `ref.locales`;
  - veřejné číselníky jazyků a trhů pro přepínač na přihlašovací stránce.
- **E-maily v jazyce příjemce:**
  - šablony po jazycích (sk, cs);
  - přímé odeslání e-mailů s tokenem (odkaz na přihlášení, obnova hesla, pozvánka);
  - obsluha `ops.outbox` druhu `email` ve workeru pro ostatní e-maily (pro změny 8, 11, 12, 16);
  - šablony e-mailů o dokončení ukázky a běhu (`sample_finished`, `run_finished`, `run_partial`, `run_failed`), které změna 8 zapisuje do `ops.outbox`;
  - test úplnosti šablon.
- **Audit** bezpečnostních událostí v `ops.audit_log`.
- **Konvence chyb:** ProblemDetails s kódem a parametry, bez lidských vět. Katalog kódů. Popis OpenAPI (`/openapi/v1.json`) se snímkem v testech.
- **Testy:** koncové body, oprávnění rolí, izolace tenantů přes API, logy bez tokenů a hesel.

Out of scope:
- obrazovky přihlášení, Nastavenia a menu účtu (změna 13);
- fakturační údaje tenanta (IČO, DIČ, IČ DPH, adresa, `billing_email`) a jejich synchronizace do Stripe (změna 12); zde jen `name`;
- přihlášení přes Shoptet (doplněk později, architektura část 11 bod 6);
- dvoufázové ověření;
- změna e-mailové adresy účtu (K rozhodnutí 6);
- smazání účtu, smazání tenanta a export dat (otevřené rozhodnutí o uchování dat, databáze část 9 bod 2);
- administrátorský přístup podpory přes roli `eshopguard_admin`;
- upozornění a jejich nastavení (`iam.notifications`, `iam.notification_settings`): změna 11 (viz K rozhodnutí 12);
- e-maily o dokladech, protokolech, publikování, fakturách a sledování: šablony dodají změny 11, 12, 15 a 16 nad infrastrukturou této změny.

## Approach

1. **Logika v knihovně, API tenké.** Nový projekt `src/EshopGuard.Application` drží služby:
   - `LoginLinkService`, `PasswordService`, `ExternalLoginService`;
   - `TenantService`, `MembershipService`, `InvitationService`;
   - `LocaleResolver`, `EmailComposer`, `SecurityAuditWriter`.

   `EshopGuard.Api` jen mapuje HTTP na volání služeb (minimal API, skupiny koncových bodů).
2. **Vlastní úložiště Identity** nad `iam.users`, `iam.user_logins` a `iam.user_tokens` ze změny 3. Žádné tabulky `AspNet*`. `UserManager`, `SignInManager`, hašování hesel a zablokování se použijí z ASP.NET Core Identity.
3. **Tokeny e-mailových odkazů:**
   - 32 náhodných bajtů, v e-mailu jako base64url;
   - v databázi jen SHA-256 (`user_tokens.token_hash`, `invitations.token_hash`);
   - spotřebování je jeden příkaz `UPDATE … WHERE used_at IS NULL AND expires_at > now() RETURNING`, takže odkaz nejde použít dvakrát ani při souběžném kliknutí;
   - token je v odkazu za `#` (fragment), takže se nedostane do logů serveru ani do hlavičky `Referer`;
   - stránka frontendu (GET) ukáže tlačítko a token pošle až POSTem.
4. **E-maily s tokenem se odesílají přímo z API, ne přes `ops.outbox`.** Token by jinak ležel v databázi, což pravidlo „v databázi jen otisk“ zakazuje. Ostatní e-maily jdou přes `ops.outbox` a obsluhu ve workeru.
5. **Stejná odpověď pro existující i neexistující účet.** Obě cesty založí řádek tokenu, pošlou e-mail a vrátí stejné tělo `202`. Limity se počítají podle e-mailu a IP, ne podle existence účtu.
6. **Globální limity v `ops.rate_limit_buckets`** (klíče `auth:*` s otiskem e-mailu nebo IP), takže platí přes všechny instance API. Navíc limiter ASP.NET Core v paměti jako první síto.
7. **Tenant z adresy:**
   - filtr `TenantAccessFilter` na skupině `/api/t/{tenantId:guid}` načte členství přihlášeného uživatele;
   - neznámý tenant i cizí tenant vrací stejné `404 tenant.not_found`;
   - nastaví `ITenantContext` (změna 3), který při každé transakci provede `set_config('app.tenant_id', …, true)`;
   - každý koncový bod musí deklarovat minimální roli, jinak se aplikace nespustí (fail-closed).
8. **Jazyk:**
   - `users.locale`, jinak první podporovaný jazyk z `Accept-Language`, jinak `ref.markets.default_locale` trhu (tenanta nebo vydání webu);
   - přepínač na přihlašovací stránce posílá frontend jako první položku `Accept-Language`;
   - nový účet si uloží jazyk z požadavku, kterým vznikl;
   - API vrací kódy, věty skládá frontend. Backend skládá jen e-maily.
9. **Chyby jako kódy:** `application/problem+json` s polem `code` (např. `login_link.expired`) a `params`. `title` nese kód, žádnou větu. Validace vrací `validation.failed` a kódy po polích.

## Dependencies

- **Změna 3 `add-multitenant-data-model`:**
  - tabulky `iam.*`, `ops.rate_limit_buckets`, `ops.outbox`, `ops.audit_log`, `ref.markets`, `ref.locales`;
  - role `eshopguard_app`, RLS;
  - `ITenantContext` a interceptor `SET LOCAL app.tenant_id`.

  Tato změna doplní migrací uživatelské politiky RLS (`app.user_id`) a funkce `SECURITY DEFINER` pro pozvánky (viz design).
- **Změna 4 `add-job-queue-and-worker`:** `IJobQueue`, obsluha úloh ve workeru, atomická rezervace tokenů v `ops.rate_limit_buckets`.
- **Změna 2 `add-solution-foundation`:** projekty `EshopGuard.Api`, `EshopGuard.Data`, `EshopGuard.Jobs`, konfigurace přes user-secrets a proměnné prostředí, `deploy/sql/00_roles.sql`.
- **Externí:**
  - poskytovatel odesílání e-mailů (K rozhodnutí 1);
  - klient OAuth Google (Client ID a Secret v user-secrets, nikdy v repozitáři).
- **Navazují:**
  - změny 10, 11 a 12: skupina `/api/t/{tenantId}`, role, ProblemDetails, OpenAPI, e-maily přes `ops.outbox`;
  - změna 13: frontend, generovaný klient z OpenAPI;
  - změna 8: zapisuje e-maily do `ops.outbox`, tato změna je odesílá.

## Done when

- `tests/EshopGuard.Api.Tests` projdou proti lokálnímu PostgreSQL (databáze `eshopguard_test`, připojení jako `eshopguard_app`):
  - tok odkazu v e-mailu včetně vypršení po 15 minutách, jednorázovosti, zneplatnění předchozího odkazu a stejné odpovědi pro neexistující účet;
  - limity 60 s, 5 za hodinu na e-mail a 20 za hodinu na IP;
  - heslo, zablokování, obnova hesla, Google přes testovací obsluhu;
  - pozvánky a role;
  - CSRF.
- **Matice rolí:** test projde všechny koncové body skupiny `/api/t/{tenantId}` a pro každou roli ověří povolení nebo `403`.
- **Izolace přes API:** dva tenanti se stejnou doménou e-shopu. Uživatel tenanta A na adrese tenanta B dostane `404 tenant.not_found` a nikde nedostane řádek tenanta B.
- **Logy:** test zachycení logů nenajde token, heslo ani e-mailovou adresu v čitelné podobě.
- **Šablony:** test úplnosti najde každou šablonu e-mailu ve všech zapnutých jazycích se stejnými zástupnými poli.
- **OpenAPI:** snímek `/openapi/v1.json` sedí s `tests/EshopGuard.Api.Tests/Snapshots/openapi-v1.json`.
- **Validace:** `openspec validate add-identity-and-tenants-api` projde.

## K rozhodnutí

1. **Poskytovatel odesílání e-mailů.** V podkladech není vybrán (SMTP služba, nebo API poskytovatele). Návrh počítá s rozhraním `IEmailTransport` a implementací přes SMTP (MailKit). Konkrétní služba, odesílací doména a záznamy SPF, DKIM a DMARC zbývá rozhodnout. Bez nich odkaz na přihlášení často skončí ve spamu.
2. **Matice oprávnění rolí.** Podklady říkají jen, že vlastník je majitel účtu a publikovat smí editor a výš. Matice v `design.md` (AD 6) je návrh:
   - placení a předplatné jen owner a admin;
   - pozvat admina smí jen owner;
   - čtenář vidí jen data, ne členy.

   Potvrdit.
3. **Parametry ochrany (návrhy, neměřeno):**
   - zablokování po 5 chybných heslech na 15 minut;
   - heslo aspoň 10 znaků bez pravidel skladby;
   - relace 30 dní s posouváním;
   - opětovné ověření 15 minut před nastavením hesla;
   - obnova hesla platí 60 minut;
   - pozvánka platí 7 dní.
4. **Založení tenanta pro nový účet.** Změna 13 předpokládá automatické založení při prvním přihlášení (její K rozhodnutí 15). Návrh:
   - tenant s rolí owner se založí při vzniku účtu, jen když pro e-mail neexistuje platná pozvánka;
   - `name` se dočasně vyplní e-mailovou adresou;
   - `legal_name` doplní Stripe Checkout (změna 12) nebo Nastavenia.

   Potvrdit, nebo zvolit krok „Účet“ v onboardingu s názvem firmy. Návrh UI ukazuje krok „Účet“ jen jako hotový.
5. **Přepínač účtů** (agentura ve více tenantech) v návrhu UI chybí. `Sidebar.dc.html` má přepínač e-shopu a menu účtu s jedním účtem. API seznam tenantů vrací (`GET /api/me`), návrh obrazovky zbývá.
6. **Změna e-mailové adresy účtu.** Datový model má `user_tokens.purpose = confirm`, plán ani návrh UI změnu e-mailu neobsahují. Návrh: samostatná malá změna po pilotu. Do té doby se e-mail mění jen přes podporu.
7. **Pozvánka jako přihlášení.** Návrh: kliknutí na pozvánku ověří e-mail stejně jako odkaz na přihlášení, takže pozvaný nepotřebuje druhý e-mail. Alternativa je nejdřív se přihlásit odkazem a pak pozvánku přijmout. Potvrdit.
8. **IP adresa v auditu.** `ops.audit_log.ip` se drží 10 let (databáze, část 6). Návrh: u událostí přihlášení ukládat jen otisk HMAC (stejně jako `user_tokens.requested_ip_hash`), plnou IP ne. Potvrdit s ohledem na GDPR.
9. **Souhlas s obchodními podmínkami při přihlášení.** Přihlašovací stránka uvádí: „Prihlásením súhlasíte s obchodnými podmienkami a spracovaním údajov“. Návrh: při vzniku účtu zapsat do auditu verzi podmínek a zásad z konfigurace `Legal:TermsVersion` a `Legal:PrivacyVersion`. Kde se verze dokumentů vedou, není rozhodnuté (architektura, část 12: „verzované dokumenty v aplikaci“).
10. **Pozastavený tenant** (`tenants.status = suspended`). Chování není popsané. Návrh (fail-closed): všechny koncové body tenanta vrací `403 tenant.suspended` kromě čtení účtu a koncových bodů plateb (změna 12 doplní výjimky). Potvrdit.
11. **Nesrovnalosti s datovým modelem (změna 3):**
    - `iam.memberships` a `iam.invitations` mají RLS po tenantovi. Seznam „moje účty“ a přijetí pozvánky ale probíhají před výběrem tenanta. Návrh: politika `memberships_by_user` (`user_id = app.user_id`) a funkce `SECURITY DEFINER` pro hledání pozvánky podle otisku tokenu.
    - `ops.audit_log` má mít RLS po tenantovi, ale události přihlášení tenanta nemají. Návrh: politika pro zápis řádků s `tenant_id IS NULL` jen pro `action LIKE 'auth.%'` a `'user.%'`.
    - `user_tokens` nemá sloupec pro označení „nahrazen novějším odkazem“. Návrh: nahrazený odkaz dostane `expires_at = now()` a uživatel uvidí stejnou nabídku „Poslať nový odkaz“ jako u vypršelého.
12. **Upozornění a jejich nastavení.** Tabulky jsou ve schématu `iam` a změna 13 je čeká ve změně 9. Zadání plánu je dává do změny 11. Tato změna se řídí zadáním: upozornění jsou ve změně 11.
13. **Úložiště klíčů Data Protection** (šifrování cookie relace) při více instancích API. Na jednom serveru stačí svazek Dockeru (`DataProtection:KeysPath`). Kde budou klíče při více serverech (databáze, nebo trezor), rozhodne změna 17 společně se šifrováním klíčů konektorů.
