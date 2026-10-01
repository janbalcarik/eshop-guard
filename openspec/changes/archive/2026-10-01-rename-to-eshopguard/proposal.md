# Proposal: Přejmenování kódu na EshopGuard

## Intent

**Problém.** Produkt se jmenuje EshopGuard, ale kód, složka, řešení, projekty, jmenné prostory, spustitelný soubor i User-Agent crawleru nesou pracovní název EshopGuard. Všechny podklady pro další fáze (struktura aplikace, nové projekty `EshopGuard.Data`, `EshopGuard.Jobs`, `EshopGuard.Api`, `EshopGuard.Worker`, OpenSpec změny 2–17) už počítají s názvy `EshopGuard.*` a složkou `eshop-guard/`. Kdyby se přejmenovávalo později, šlo by přes víc projektů a souběžné změny 3–7.

**Proč teď.** Je to první krok fáze F0. Kód má dnes jen tři projekty (`EshopGuard.Core`, `EshopGuard.Cli`, `EshopGuard.Core.Tests`) a žádnou databázi, takže přejmenování je levné a nic dalšího na něm ještě nestojí.

**Přínos.**
- Jména v kódu odpovídají plánu: další změny můžou odkazovat přímo na `EshopGuard.*` bez překladu.
- Crawler se cizím e-shopům představuje skutečným názvem produktu (`EshopGuard/0.1`), stejně jako to bude ve webové verzi.
- Chování knihovny a CLI se nemění: stejné nálezy, stejné výstupy, stejná cache odpovědí Jevu a přepisů.

**Fáze:** F0 Základ, první krok.

**Podklad:**
- `databaze-a-plan-implementace-2026-10-01.md`, část 8, řádek F0 („Nejdřív přejmenování dnešního kódu na EshopGuard …“) a část 7 (struktura `eshop-guard/`);
- `architektura-multitenant-worker-2026-10-01.md`, část 2 (doporučená struktura v `eshop-guard/`);
- `eshop-checker/openspec/config.yaml` (Produkt: EshopGuard, dříve pracovně EshopGuard; User-Agent EshopGuard/0.1).

## Scope

In scope:
- Složka `D:\_github\Overko\eshop-checker` → `D:\_github\Overko\eshop-guard` (přesune se s ní i `openspec/`, `cache/`, `out/`, `config/`, `rules/`).
- Řešení `EshopGuard.sln` → `EshopGuard.sln` (stejné GUID projektů, jen názvy a cesty).
- Projekty a jejich složky:
  - `src/EshopGuard.Core/EshopGuard.Core.csproj` → `src/EshopGuard.Core/EshopGuard.Core.csproj` (`RootNamespace`, `PackageId`, `InternalsVisibleTo`);
  - `src/EshopGuard.Cli/EshopGuard.Cli.csproj` → `src/EshopGuard.Cli/EshopGuard.Cli.csproj` (`RootNamespace`, `ProjectReference`, `AssemblyName` `checker` → `eshopguard`);
  - `tests/EshopGuard.Core.Tests/EshopGuard.Core.Tests.csproj` → `tests/EshopGuard.Core.Tests/EshopGuard.Core.Tests.csproj` (`ProjectReference`).
- Jmenné prostory ve všech `.cs` (`namespace EshopGuard.*`, `using EshopGuard.*`) → `EshopGuard.*`.
- Typy a členy:
  - `IEshopGuard` (`src/EshopGuard.Core/IEshopGuard.cs`) → `IEshopGuard` (soubor `IEshopGuard.cs`);
  - `EshopGuardService` (`EshopGuardService.cs`) → `EshopGuardService` (soubor `EshopGuardService.cs`), parametr `checkerOptions` → `guardOptions`;
  - `EshopGuardOptions` (`Options/EshopGuardOptions.cs`) → `EshopGuardOptions` (soubor `Options/EshopGuardOptions.cs`);
  - `ServiceCollectionExtensions.AddEshopGuard` → `AddEshopGuard`;
  - lokální proměnné `checker` (`ScanCommand`, `CheckTextCommand`, `PageRewriter`, testy) → `guard`.
- Řetězcové konstanty: `HttpPageFetcher.HttpClientName` `"EshopGuard.Crawl"`, `JevClient.HttpClientName` `"EshopGuard.Jev"`, `OpenAiRewriteClient.HttpClientName` `"EshopGuard.OpenAI"` → `"EshopGuard.*"`; `prompt_cache_key` `"EshopGuard-rewrite-"` → `"eshopguard-rewrite-"` (`Fix/OpenAiRewriteClient.cs`).
- Spustitelný soubor: `AssemblyName` `checker` → `eshopguard`, `config.SetApplicationName("checker")` v `src/EshopGuard.Cli/Program.cs` → `"eshopguard"`, dokumentační komentáře `<c>checker scan …</c>`, `<c>checker check-text …</c>`, `<c>checker rewrite …</c>`, `<c>checker serve-fixture</c>` a `checker rewrite` v `CliConfiguration.cs` a `Fix/RewriteOptions.cs`.
- User-Agent: výchozí `CrawlOptions.UserAgent` `"EshopGuard/0.1"` → `"EshopGuard/0.1"` a `config/settings.yaml` `crawl.user_agent` → `"EshopGuard/0.1 (+mailto:doplnte-kontakt@example.cz)"` (kontakt zůstává zástupný, CLI na něj dál upozorňuje).
- Komentáře a cesty v konfiguraci a skriptech: hlavička `config/settings.yaml`, komentáře „checker rewrite“ v `config/settings.yaml` a `config/rewrite.yaml`, cesta v komentáři `config/legal_requirements.yaml`, docstringy `tests/…/Fixtures/generate_site.py` a `generate_site_sk.py`, výchozí `--root` v `Commands/ServeFixtureCommand.cs`.
- Testy: předpony dočasných složek `EshopGuard-*` → `eshopguard-*`, token robots.txt `"EshopGuard"` v `CrawlTests.cs` a `CrawlPaceTests.cs` → `"EshopGuard"`.
- `README.md` (nadpis, cesty, příkazy, ukázka `AddEshopGuard` / `IEshopGuard`), nový záznam v `rules/CHANGELOG.md`, `.claude/launch.json` (absolutní cesty), `openspec/config.yaml` (věta „knihovna EshopGuard.Core“ → „EshopGuard.Core“; „dříve pracovně EshopGuard“ zůstává jako historie).
- Zachování cache `cache/jev-cache.sqlite` (odpovědi Jevu, tabulky `rewrite_cache` a `page_profiles`).

Out of scope:
- Jakákoli změna chování, nové požadavky a specifikace (`.openspec.yaml`: `skip_specs: true`).
- Zprovoznění `dotnet test` (dnes hlásí 0 testů, testy se spouštějí přes `EshopGuard.Core.Tests.exe`): změna 2 `add-solution-foundation`.
- Text zadání pro model profilů v `Profiles/ProfileModel.cs` (`ProfilePrompt`, obecné slovo „text checker“): není to název produktu a změna textu by bez nové `ProfilePrompt.Version` tiše změnila chování modelu.
- Historické záznamy v `rules/CHANGELOG.md` (např. `checker rewrite <složka skenu>`), výstupy starých skenů v `out/`, logy v `TestResults/`, obsah `cache/`.
- Dokumenty v `D:\_github\Overko\*.md` a `podklady/` (podklady už používají `eshop-guard`), paměť Claude a skripty ve scratchpadu mimo projekt.
- Převod `.sln` na `.slnx`, centrální správa balíčků, nové projekty (změna 2).

## Approach

1. **Výchozí stav změřit před přejmenováním:** sestavení, počet testů (prošlo / přeskočeno / selhalo) z `EshopGuard.Core.Tests.exe` bez placených testů kategorie `Jev`, otisk SHA-256 a velikost `cache/jev-cache.sqlite`. Bez toho by „stejný výsledek“ nešlo prokázat.
2. **Záloha:** složka není v gitu (v `D:\_github\Overko` ani v `eshop-checker` není `.git`), takže vrácení zajistí jen ZIP celé složky (včetně `cache/`).
3. **Uvolnit zámky souborů:** zastavit `serve-fixture`, IDE a sestavovací servery (`dotnet build-server shutdown`), jinak Windows přesun složky odmítne.
4. **Přesun a přejmenování na disku** (`Move-Item`), potom smazání všech `bin/` a `obj/` (obsahují staré názvy sestavení a `project.assets.json` se starými cestami).
5. **Náhrady textu podle výčtu ve Scope,** od nejdelších řetězců (`EshopGuardOptions`, `EshopGuardService`, `IEshopGuard`, `AddEshopGuard`) po obecné (`EshopGuard.` v jmenných prostorech), ne slepou náhradou slova `checker` (zasáhla by zadání modelu profilů a historii).
6. **Cache se neztratí:** cesta je relativní k pracovní složce (`CacheOptions.Path` = `cache/jev-cache.sqlite`, `Path.GetFullPath` v `SqliteJevCache`, `SqliteRewriteCache`, `SqlitePageProfileStore`) a soubor se přesune se složkou. Klíče cache název produktu neobsahují:
   - Jev: `JevCacheKey.Create` = SHA-256 z modelu, verze sady otázek, jazyka otázek, otázek a stavu;
   - přepisy: `PageRewriter.CacheKey` = SHA-256 z modelu, úsilí, verze zadání, společné části a stránky;
   - profily: podle `site`.

   `prompt_cache_key` je jen nápověda pro mezipaměť na straně OpenAI, ne lokální klíč: po změně první požadavky po přejmenování nedostanou slevu za mezipaměť OpenAI (neměřeno, řádově centy).
7. **Token robots.txt** se odvozuje z User-Agentu (`Crawler.ProductToken`, část před `/`), po změně je `EshopGuard`. Pravidla robots.txt psaná pro `EshopGuard` přestanou platit, pravidla pro `*` platí dál.

## Dependencies

- Žádné. Blokuje všechny ostatní změny (2–17), protože mění cesty a názvy, na které odkazují.
- Musí proběhnout, když na kódu neběží žádná jiná změna: přesouvá se i složka `openspec/` s rozpracovanými změnami.

## Done when

- `rg -n -i "eshop-?checker"` nad `D:\_github\Overko\eshop-guard` bez `bin/`, `obj/`, `cache/`, `out/`, `TestResults/`, `openspec/changes/` a `rules/CHANGELOG.md` nevrátí nic; v `openspec/config.yaml` zůstane jen „dříve pracovně EshopGuard“.
- `dotnet build EshopGuard.sln` skončí bez chyb a bez nových varování proti výchozímu stavu.
- `tests\EshopGuard.Core.Tests\bin\Debug\net10.0\EshopGuard.Core.Tests.exe` bez kategorie `Jev` dá stejné počty (prošlo / přeskočeno / selhalo) jako výchozí stav; podle podkladů je to 192 testů a 0 selhání.
- Vznikne `src\EshopGuard.Cli\bin\Debug\net10.0\eshopguard.exe` a `eshopguard --help` vypíše název `eshopguard`.
- `cache/jev-cache.sqlite` má po přesunu stejný SHA-256 jako před ním.
- `config/settings.yaml` i výchozí `CrawlOptions.UserAgent` začínají `EshopGuard/0.1` a testy robots.txt v `CrawlTests` a `CrawlPaceTests` s tokenem `EshopGuard` projdou.
- `eshopguard scan http://localhost:8000 --mock` nad testovacím e-shopem (`serve-fixture`) vytvoří stejnou sadu výstupů jako před přejmenováním (stejný počet nálezů ve `findings.json`).

## K rozhodnutí

1. **`dotnet test` hlásí 0 testů.** Podklad (část 8, F0) počítá s tím, že „`dotnet build` a `dotnet test` projdou (192 testů jako dnes)“, a `README.md` uvádí `dotnet test`. Ve skutečnosti se testy spouštějí přes `EshopGuard.Core.Tests.exe`. Tato změna to jen zachová a ověřuje přes exe; oprava je navržená ve změně 2 (požadavek „Základ: Sestavení a testy jedním příkazem“). Potvrdit, že to tam patří.
2. **Placené testy kategorie `Jev`** (`JevLiveTests`) se spustí samy, když je v prostředí `JEV_API_KEY` nebo `TYPESAFE_API_KEY`, včetně uživatelského prostředí Windows (asi 0,02 USD za běh podle `README.md`). Ověření v této změně je proto vylučuje filtrem. Spustit je i po přejmenování? Jen s odhadem ceny a souhlasem.
3. **Záloha místo historie:** bez gitu jde přejmenování vrátit jen ze ZIPu. Založit před přejmenováním git repozitář (doporučeno kvůli všem dalším změnám), nebo stačí ZIP?
