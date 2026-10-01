# Tasks

> **Stav 1. 10. 2026:** přesun a přejmenování udělal uživatel ručně. Projekt je v `D:\_github\eshop-guard`, kód v `src/`.
> Úkoly s původními cestami (`D:\_github\Overko\…`) a se zálohou ZIP se tím nedělaly, protože stará složka už neexistuje.
> Dokončeno:
> - oprava rozbitého kódování regexu v `ContentExtractor.cs` (`€`, `Kč`, `zł`);
> - přejmenování testovacího projektu na `EshopGuard.Core.Tests` a oprava cest;
> - příkaz `checker` → `eshopguard`;
> - soubor `EshopGuardOptions.cs`;
> - komentáře.
> Ověření: sestavení 0 chyb a 0 varování, 190 z 190 testů bez placených. Cache `src/cache/jev-cache.sqlite` zůstala.


Cesty v úkolech skupiny 1 a 2 jsou ještě staré (`D:\_github\Overko\eshop-checker`), od skupiny 3 nové (`D:\_github\Overko\eshop-guard`). Příkazy jsou pro PowerShell 7.

## 1. Výchozí stav a záloha

- [x] 1.1 Sestavit dnešní řešení: `dotnet build EshopGuard.sln` ve `D:\_github\Overko\eshop-checker`; zapsat počet chyb a varování do poznámky k úkolu (porovnává se v 6.2).
- [x] 1.2 Zjistit syntaxi filtru testovacího exe bez spuštění testů: `tests\EshopGuard.Core.Tests\bin\Debug\net10.0\EshopGuard.Core.Tests.exe --help` (nativní runner xUnit v3 používá `-trait- "Category=Jev"`, režim Microsoft.Testing.Platform `--filter-not-trait "Category=Jev"`). Zapsat, která platí.
- [x] 1.3 Spustit testy bez placené kategorie `Jev` filtrem z 1.2 a zapsat počty prošlo / přeskočeno / selhalo (podle podkladů 192 testů, 0 selhání). Placené `JevLiveTests` nespouštět (viz 6.6).
- [x] 1.4 Spustit `dotnet run --project src/EshopGuard.Cli -- serve-fixture --port 8000` a v druhém terminálu `dotnet run --no-build --project src/EshopGuard.Cli -- scan http://localhost:8000 --mock --out out/rename-before`; zapsat počet nálezů ve `findings.json`. Server zastavit (Ctrl+C).
- [x] 1.5 Zapsat otisk a velikost cache: `Get-FileHash cache\jev-cache.sqlite -Algorithm SHA256` a `(Get-Item cache\jev-cache.sqlite).Length`. Ověřit, že vedle nejsou soubory `jev-cache.sqlite-wal` / `-shm` (otevřené spojení); pokud jsou, zavřít procesy, které cache drží.
- [x] 1.6 Zálohovat celou složku: `Compress-Archive -Path D:\_github\Overko\eshop-checker -DestinationPath D:\_github\Overko\eshop-checker-pred-prejmenovanim-2026-10.zip` (včetně `cache/`). Ověřit, že ZIP jde otevřít a obsahuje `cache\jev-cache.sqlite`.

## 2. Uvolnění souborů a přesun složky

- [x] 2.1 Zavřít Visual Studio / Rider / VS Code otevřené nad složkou, zastavit `serve-fixture` a běžící `checker.exe`, spustit `dotnet build-server shutdown`.
- [x] 2.2 Přesunout složku: `Move-Item D:\_github\Overko\eshop-checker D:\_github\Overko\eshop-guard`. Při chybě „Access denied“ najít proces, který drží soubor (Správce prostředků, `handle.exe`), nic nemazat.
- [x] 2.3 Smazat všechny výstupy sestavení se starými názvy: `Get-ChildItem D:\_github\Overko\eshop-guard -Recurse -Directory -Include bin,obj | Remove-Item -Recurse -Force` (jen `bin` a `obj` pod `src` a `tests`; `cache`, `out`, `TestResults` zůstávají).

## 3. Řešení, projekty a složky

- [x] 3.1 Přejmenovat `src\EshopGuard.Core` → `src\EshopGuard.Core` a v ní `EshopGuard.Core.csproj` → `EshopGuard.Core.csproj`; v souboru `RootNamespace` a `PackageId` → `EshopGuard.Core`, `InternalsVisibleTo Include="EshopGuard.Core.Tests"`.
- [x] 3.2 Přejmenovat `src\EshopGuard.Cli` → `src\EshopGuard.Cli` a `EshopGuard.Cli.csproj` → `EshopGuard.Cli.csproj`; `AssemblyName` `checker` → `eshopguard`, `RootNamespace` → `EshopGuard.Cli`, `ProjectReference` → `..\EshopGuard.Core\EshopGuard.Core.csproj`.
- [x] 3.3 Přejmenovat `tests\EshopGuard.Core.Tests` → `tests\EshopGuard.Core.Tests` a `EshopGuard.Core.Tests.csproj` → `EshopGuard.Core.Tests.csproj`; `ProjectReference` → `..\..\src\EshopGuard.Core\EshopGuard.Core.csproj`.
- [x] 3.4 Přejmenovat `EshopGuard.sln` → `EshopGuard.sln`; v něm názvy projektů a cesty (`src\EshopGuard.Core\EshopGuard.Core.csproj`, `src\EshopGuard.Cli\EshopGuard.Cli.csproj`, `tests\EshopGuard.Core.Tests\EshopGuard.Core.Tests.csproj`). GUID projektů ponechat beze změny.

## 4. Kód knihovny, CLI a testů

- [x] 4.1 Ve všech `*.cs` pod `src` a `tests` nahradit `namespace EshopGuard.` → `namespace EshopGuard.` a `using EshopGuard.` → `using EshopGuard.` (včetně `EshopGuard.Cli.Commands` v `Program.cs`).
- [x] 4.2 `src\EshopGuard.Core\IEshopGuard.cs` → `IEshopGuard.cs`, rozhraní `IEshopGuard` → `IEshopGuard`; opravit všechny odkazy (`<see cref="IEshopGuard…"/>` v `Models/AnalysisModels.cs`, `Options/AnalyzeOptions.cs`, `Fix/PageRewriter.cs`, `ServiceCollectionExtensions.cs`, `GetRequiredService<IEshopGuard>()` v CLI a testech).
- [x] 4.3 `EshopGuardService.cs` → `EshopGuardService.cs`, třída `EshopGuardService` → `EshopGuardService`, `ILogger<EshopGuardService>` → `ILogger<EshopGuardService>`, parametr `checkerOptions` → `guardOptions` (4 použití); test `TextNotLoadedTests` volá `EshopGuardService.NotLoadedWarnings`.
- [x] 4.4 `Options\EshopGuardOptions.cs` → `Options\EshopGuardOptions.cs`, třída `EshopGuardOptions` → `EshopGuardOptions`; opravit `IOptions<EshopGuardOptions>` ve všech konstruktorech (`SqliteJevCache`, `SqliteRewriteCache`, `SqlitePageProfileStore`, `PageClassifier`, `Crawler`, `HttpPageFetcher`, `PageProfiler`, `OpenAiProfileModel`, `PageSieve`, `SegmentEvaluator`, `YamlRuleSetProvider`, `SegmentBuilder`, `SentenceSplitter`) a `new EshopGuardOptions()` v testech; komentář „Root options of the checker library“ → „of the EshopGuard library“.
- [x] 4.5 `ServiceCollectionExtensions.AddEshopGuard` → `AddEshopGuard` (také `Action<EshopGuardOptions>`, `services.TryAddSingleton<IEshopGuard, EshopGuardService>()`); volání v `CliHost.BuildServices`, `TestServices.Create`, `CacheTests`, `RewriteTests`; komentář `<c>AddEshopGuard</c>` v `Fix/IRewriteClient.cs` a `Options/EshopGuardOptions.cs`.
- [x] 4.6 Konstanty `HttpClientName`: `HttpPageFetcher` `"EshopGuard.Crawl"`, `JevClient` `"EshopGuard.Jev"`, `OpenAiRewriteClient` `"EshopGuard.OpenAI"`; `prompt_cache_key` v `OpenAiRewriteClient` → `"eshopguard-rewrite-" + request.PromptVersion`.
- [x] 4.7 `CrawlOptions.UserAgent` výchozí `"EshopGuard/0.1"` a komentář `<c>EshopGuard/0.1 (+mailto:...)</c>` v `Options/EshopGuardOptions.cs`.
- [x] 4.8 CLI: `config.SetApplicationName("eshopguard")` v `Program.cs`; komentáře `<c>checker …</c>` → `<c>eshopguard …</c>` v `Commands/ScanCommand.cs`, `CheckTextCommand.cs`, `RewriteCommand.cs`, `ServeFixtureCommand.cs`, `CliConfiguration.cs` (`OpenAiApiKey`) a `Core/Fix/RewriteOptions.cs`; lokální proměnné `checker` → `guard` v `ScanCommand`, `CheckTextCommand`, `PageRewriter` (parametr konstruktoru) a `ScanFixtureTests`.
- [x] 4.9 `ServeFixtureCommand`: `[DefaultValue("tests/EshopGuard.Core.Tests/Fixtures/site")]` a výchozí hodnota `Root`.
- [x] 4.10 Testy: `Directory.CreateTempSubdirectory("EshopGuard-…")` → `"eshopguard-…"` (`CacheTests`, `ProfileTests`, `RestTextTests`, `RulesTests`, `ScanFixtureTests`, `TextNotLoadedTests`); token robots.txt `"EshopGuard"` → `"EshopGuard"` v `CrawlTests` (3×) a `CrawlPaceTests` (`InlineData` s `User-agent: EshopGuard` a volání `RobotsTxt.Parse`).
- [x] 4.11 Nechat beze změny `Profiles/ProfileModel.cs` (`ProfilePrompt`, text „consumer-law text checker“) a do poznámky úkolu zapsat proč (zadání modelu, `ProfilePrompt.Version` = `profile-2026-10-01`).

## 5. Konfigurace, dokumentace a nástroje

- [x] 5.1 `config/settings.yaml`: hlavička „# Nastavení EshopGuard …“, `crawl.user_agent: "EshopGuard/0.1 (+mailto:doplnte-kontakt@example.cz)"`, komentář `(eshopguard rewrite)` u `rewrite`. Hodnotu `cache.path: cache/jev-cache.sqlite` nechat.
- [x] 5.2 `config/rewrite.yaml` řádek 1: `(eshopguard rewrite)`; ověřit, že se mění jen komentář (YAML komentáře nejsou součástí společné části zadání ani klíče cache přepisů).
- [x] 5.3 `config/legal_requirements.yaml` řádek 5: cesta `tests/EshopGuard.Core.Tests/Fixtures/legal_requirements_cases.json`.
- [x] 5.4 Docstringy `tests/EshopGuard.Core.Tests/Fixtures/generate_site.py` a `generate_site_sk.py`: cesta ke skriptu.
- [x] 5.5 `README.md`: nadpis `# EshopGuard`, věta o dřívějším pracovním názvu EshopGuard se nepřidává; cesty `src/EshopGuard.Cli`, `tests/EshopGuard.Core.Tests/Fixtures/site/`; ukázka knihovny `services.AddEshopGuard(...)`, `GetRequiredService<IEshopGuard>()`, proměnná `guard`. Do oddílu „Sestavení a testy“ doplnit skutečné spuštění testů přes `tests\EshopGuard.Core.Tests\bin\Debug\net10.0\EshopGuard.Core.Tests.exe` s filtrem z 1.2 a poznámku, že `dotnet test` dnes hlásí 0 testů (opraví změna 2).
- [x] 5.6 `rules/CHANGELOG.md`: nový záznam nahoře „## Přejmenování na EshopGuard (datum)“: složka `eshop-guard`, projekty `EshopGuard.*`, spustitelný soubor `eshopguard`, User-Agent a token robots.txt `EshopGuard`, sady otázek a jejich `version` beze změny, cache zachovaná. Starší záznamy (např. `checker rewrite`) nechat beze změny.
- [x] 5.7 `.claude/launch.json`: `D:/_github/Overko/eshop-guard/src/EshopGuard.Cli` a `D:/_github/Overko/eshop-guard/tests/EshopGuard.Core.Tests/Fixtures/site`.
- [x] 5.8 `openspec/config.yaml`: „knihovna EshopGuard.Core“ → „knihovna EshopGuard.Core“; „dříve pracovně EshopGuard“ ponechat.
- [x] 5.9 Projít zbylé výskyty slova `checker` mimo identifikátory: `rg -n -w "checker" --glob "!**/bin/**" --glob "!**/obj/**" --glob "!out/**" --glob "!cache/**" --glob "!openspec/changes/**"`. Povolené zůstávají jen `Profiles/ProfileModel.cs` (zadání modelu) a historie v `rules/CHANGELOG.md`.

## 6. Ověření

- [x] 6.1 Žádný starý název: `rg -n -i "eshop-?checker" D:\_github\Overko\eshop-guard --glob "!**/bin/**" --glob "!**/obj/**" --glob "!cache/**" --glob "!out/**" --glob "!TestResults/**" --glob "!openspec/changes/**" --glob "!rules/CHANGELOG.md"` vrátí jen řádek „dříve pracovně EshopGuard“ v `openspec/config.yaml`. Názvy souborů: `Get-ChildItem -Recurse -Filter *EshopGuard* | Where-Object FullName -notmatch '\\(out|cache|TestResults)\\'` nevrátí nic.
- [x] 6.2 `dotnet build EshopGuard.sln` ve `D:\_github\Overko\eshop-guard`: 0 chyb, počet varování nejvýš jako v 1.1.
- [x] 6.3 `tests\EshopGuard.Core.Tests\bin\Debug\net10.0\EshopGuard.Core.Tests.exe` s filtrem z 1.2 (bez kategorie `Jev`): počty prošlo / přeskočeno / selhalo stejné jako v 1.3 (192 testů, 0 selhání podle podkladů). Zapsat, že `dotnet test` stále hlásí 0 testů (oprava ve změně 2).
- [x] 6.4 Existuje `src\EshopGuard.Cli\bin\Debug\net10.0\eshopguard.exe`; `dotnet run --project src/EshopGuard.Cli -- --help` uvede v části USAGE název `eshopguard` (ne `checker`) a příkazy `scan`, `check-text`, `rewrite`, `serve-fixture`.
- [x] 6.5 Cache: `Get-FileHash cache\jev-cache.sqlite -Algorithm SHA256` a velikost se shodují s 1.5. `serve-fixture` z `.claude/launch.json` (název `fixture-eshop`) nastartuje a `eshopguard scan http://localhost:8000 --mock --out out/rename-after` dá stejný počet nálezů ve `findings.json` jako 1.4.
- [x] 6.6 Odhad ceny + souhlas uživatele: živé testy `JevLiveTests` (kategorie `Jev`, asi 0,02 USD podle `README.md`) spustit jen po souhlasu; očekávání: obě prošly jako před přejmenováním.
- [x] 6.7 Po úspěšném ověření smazat ZIP z 1.6 až se souhlasem uživatele (ne automaticky).
