# EshopGuard: zásady pro práci v repozitáři

EshopGuard kontroluje texty e-shopů podle spotřebitelského práva. Primárně jde o Slovensko (zákon 108/2024 Z. z. ve znění 310/2025, EmpCo od 27. 9. 2026), dále o Česko.
- Texty vyhodnocuje TypeSafe Jev (klasifikátor, otázka → pravděpodobnost).
- Přepisy textů dělá OpenAI gpt-6.1-sol.
- LLM jen OpenAI, ne Anthropic.

## Struktura
- Kořen: dokumenty návrhu.
  - Architektura: `architektura-multitenant-worker-2026-10-01.md`.
  - Databáze a fáze F0–F10: `databaze-a-plan-implementace-2026-10-01.md`.
  - Další: `platby-a-fakturace-…`, `strategie-a-cenik-…`, `hostovani-srovnani-…`.
  - Podklady: `podklady/` (v gitu jen `reserse/` a texty zákonů `.txt`).
- `src/`: řešení .NET 10 (`EshopGuard.sln`).
  - Projekty `EshopGuard.Core` (knihovna) a `EshopGuard.Cli` (tenké CLI).
  - Webová aplikace (změna 2): `EshopGuard.Data` (EF Core, role, migrace), `.Storage` (`IBlobStore`, zatím jen lokální souborové úložiště), `.Jobs` (fronta úloh v PostgreSQL, zpracování a plánovač; běhy ukázky a úvodní analýzy v `Runs/`; README, oddíly Fronta úloh a worker, Běhy analýzy ve workeru), `.Billing`, `.Connectors`, `.Api`, `.Worker`. Povolený směr závislostí hlídá `ProjectReferenceTests`.
  - Testy `src/tests/EshopGuard.*.Tests`, společné nastavení `src/tests/Directory.Build.props`, verze balíčků `src/Directory.Packages.props`.
  - Pravidla `src/rules/*.yaml`, jejich texty `src/rules/texts/<jazyk>/` (`_engine.yaml`, `_labels.yaml`, soubor po sadě), nastavení `src/config/*.yaml` (známé země v `jurisdictions.yaml`).
- `deploy/`: `sql/00_roles.sql` (role a databáze), `dev/setup-local.ps1` (lokální nastavení).
- `openspec/`: implementační plán.
  - Pořadí 18 změn je v `openspec/README.md`.
  - Otevřená rozhodnutí jsou v `openspec/K-ROZHODNUTI.md`.
- `design/ui/`: schválený návrh UI, 21 obrazovek `*.dc.html` a `canvas.json`.
  - Popis je v `design/ui/README.md`.
  - Živé plátno: https://claude.ai/artifact/34wYLsJzieFtmWdueAwYay
  - Frontend se staví podle těchto souborů.
- `research/` (jen lokálně, mimo git): výzkumné skripty a výsledky (místa prodeje, jazykové verze).

## Jak pracovat
- **Česky.** Odpovědi, dokumenty i specifikace. Kód a identifikátory anglicky jako dosud.
- **Před každou změnou vysvětlit:**
  - proč se dělá;
  - co přinese;
  - jak funguje;
  - o kolik zlepší, číslem z měření, jinak napsat „neměřeno“.
- **U větších změn počkat na souhlas.** Nápad nebo otázka uživatele není zadání: nejdřív posoudit a na úpravy se zeptat.
- **Postup podle OpenSpec:** `/opsx:apply <změna>` v pořadí z `openspec/README.md`. Před každou fází krátce vysvětlit a počkat na souhlas. Validace: `DO_NOT_TRACK=1 OPENSPEC_TELEMETRY=0 openspec validate --all --no-interactive`.
- **Nejdřív návrh UI, pak kód.** Obrazovky, které na plátně chybí, se nejdřív navrhnou a schválí.
- **Kvalita před cenou.**

## Peníze a klíče
- Každé placené volání (Jev, OpenAI, Stripe live) jen po **odhadu ceny a souhlasu uživatele**.
  - Testy používají falešné klienty.
  - Živé testy Jevu mají značku `Category=Jev` a spouští se jen se souhlasem.
- Klíče (`JEV_API_KEY`/`TYPESAFE_API_KEY`, `OPENAI_API_KEY`, Stripe, SuperFaktúra) nikdy do logu, výstupu, commitu ani do souborů v repozitáři. `.env` je mimo git.

## Zásady produktu (závazné)
- Logika patří do knihovny. CLI, API a worker jsou tenké vrstvy.
  - Knihovna je rozdělená na kroky `EshopGuard.Core.Pipeline` se serializovatelnými vstupy a výstupy; CLI je spouští v paměti (`InMemoryPipelineRunner`), worker jako úlohy. Úložiště jsou za rozhraními `EshopGuard.Core.Storage`.
  - Výstupy CLI musí zůstat shodné s referenčními (`PipelineEquivalenceTests`, `Baselines/`); nahrávky cizích webů `src/snapshots/` a jejich výstupy `src/baselines/` jsou jen lokálně (test kategorie `Snapshot`).
- **Cache (odpovědi Jevu, přepisy, profily) jen v PostgreSQL**, stejně pro CLI i aplikaci (`AddEshopGuardPostgresStores`, CLI jako tenant `cli`, připojení `ConnectionStrings:Cli`, jednou `eshopguard cache init`). Žádné lokální soubory cache. `--mock` se k databázi nepřipojuje.
  - Stará `src/cache/jev-cache.sqlite` se nepřevádí (rozhodnutí 1. 10. 2026) a kód ji nečte; soubor nemazat bez souhlasu.
- Aplikace se připojuje jako role `eshopguard_app` nebo `eshopguard_worker`, ne jako superuživatel, protože ten obchází RLS. Databáze se jmenuje `eshopguard`.
- Data tenanta jen v transakci s kontextem tenanta (`ITenantContext.Set` + `ExecuteInTenantTransactionAsync`, čisté SQL `TenantSql.BeginAsync`). Nová tabulka tenanta: RLS v migraci, zápis do `TableNames` a řádek v `TenantDataSeeder` (README, oddíl Databáze); jinak selžou katalogové testy.
- Žádné slovníky klíčových slov pro klasifikaci. Rozhoduje Jev, LLM nebo struktura stránky.
- Země a jazyky jsou data, ne kód: další trh (DE, PL, HU…) = řádek v `config/jurisdictions.yaml` (i údaje trhu pro místa prodeje: `country`, `language`, `readable_languages`, `tlds`), sady pravidel a složka `rules/texts/<jazyk>/` (README, oddíl Země a jazyky). Knihovna vrací kódy a parametry, věty skládá `RuleTextRenderer`; překlad textů se použije jen po kontrole člověkem (`review`).
- Fail-closed: co nebylo zkontrolováno, se uvede, nic se tiše neskrývá ani neslučuje.
- Právo:
  - nevymýšlet čísla paragrafů;
  - řídit se textem zákona;
  - co z něj nejde rozhodnout, je „k ověření“ (a právník);
  - na výklad se uživatele neptat.
- Stahování: dodržovat robots.txt, User-Agent `EshopGuard/0.1`, skenovat jen weby, které uživatel určil.

## Sestavení a testy
- Sestavení: `dotnet build src/EshopGuard.sln`.
- Testy bez placených (Windows i Linux, z kořene repozitáře): `dotnet test --solution src/EshopGuard.sln --filter-not-trait "Category=Jev"`. Stav 2. 10. 2026 (po změně 8 a porovnání verzí na popisu z profilu): 1045 testů v sedmi projektech (8 explicitních běží jen na vyžádání), z toho 471 v `EshopGuard.Core.Tests` a 172 v `EshopGuard.Jobs.Tests` (běhy ve workeru v `Runs/`, shoda s CLI `CliParityTests`).
  - Kategorie `Db` potřebuje PostgreSQL (databáze `eshopguard_test` a `eshopguard_test_jobs`) a user-secrets `eshopguard-tests` (`deploy/dev/setup-local.ps1`). Bez prostředí selže se jménem klíče; vynechat jen filtrem `--filter-not-trait "Category=Db"`.
  - Jeden projekt přímo: `dotnet run --project src/tests/EshopGuard.Core.Tests -- -trait- "Category=Jev"`.
- `global.json` (pin SDK a `test.runner` = Microsoft.Testing.Platform) je v kořeni repozitáře, proto `dotnet test` funguje odkudkoli v repozitáři.
- Soubory ukládat v **UTF-8**. Při přejmenování se kvůli jinému kódování rozbil regex s `€`, `Kč`, `zł` v `ContentExtractor.cs`.

## Lokální prostředí
- PostgreSQL 18 na `localhost:5432`. Admin `postgres/postgres` jen pro zakládací skript rolí (`deploy/dev/setup-local.ps1` → `deploy/sql/00_roles.sql`).
- Migrace: `dotnet tool restore` a `dotnet ef database update --project src/EshopGuard.Data` (jako `eshopguard_owner`).
- Cloud: přípravný skript `scripts/cloud-setup.sh` (.NET SDK podle `global.json`, PostgreSQL 18, OpenSpec CLI, PowerShell, role, user-secrets a migrace).
