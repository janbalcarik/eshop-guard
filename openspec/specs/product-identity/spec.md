# product-identity Specification

## Purpose
TBD - created by archiving change rename-to-eshopguard. Update Purpose after archive.
## Requirements
### Requirement: Název produktu v kódu
Systém MUST používat název EshopGuard v řešení, projektech a jmenných prostorech (`EshopGuard.*`), ve veřejném rozhraní knihovny (`IEshopGuard`, `AddEshopGuard`) a ve složce `eshop-guard`; starý název EshopGuard smí zůstat jen v historii (`rules/CHANGELOG.md`).

#### Scenario: Žádný starý název v kódu
- GIVEN dokončené přejmenování
- WHEN se v `src`, `tests`, `config`, `rules` (mimo CHANGELOG) a `README.md` hledá „EshopGuard“ nebo „eshop-checker“
- THEN se nenajde žádný výskyt

#### Scenario: Testy beze změny
- GIVEN počet testů změřený před přejmenováním
- WHEN se spustí `EshopGuard.Core.Tests.exe` bez placených živých testů
- THEN projde stejný počet testů jako před přejmenováním

### Requirement: Identita při stahování a spouštění
Systém MUST se při stahování stránek představovat User-Agentem `EshopGuard/0.1 (...)`, řídit se robots.txt pro token `EshopGuard` a spouštět se příkazem `eshopguard`.

#### Scenario: User-Agent a robots.txt
- GIVEN robots.txt e-shopu se skupinou pro `EshopGuard`
- WHEN crawler stahuje stránky
- THEN posílá User-Agent začínající `EshopGuard/0.1`
- AND řídí se pravidly skupiny `EshopGuard`

#### Scenario: Starý příkaz neexistuje
- GIVEN sestavené CLI
- WHEN uživatel spustí `eshopguard scan --help`
- THEN se vypíše nápověda
- AND spustitelný soubor `checker` už ve výstupu sestavení není

### Requirement: Zachování cache při přejmenování
Systém MUST po přejmenování dál používat existující cache odpovědí Jevu, přepisů a profilů, aby se již zaplacené odpovědi neplatily znovu.

#### Scenario: Cache přežije přesun složky
- GIVEN `cache/jev-cache.sqlite` s otiskem SHA-256 změřeným před přesunem
- WHEN se složka přejmenuje na `eshop-guard` a spustí se `eshopguard scan --mock` na fixture
- THEN má soubor po přesunu stejný otisk
- AND odhad ceny ukáže odpovědi z cache jako už zaplacené

