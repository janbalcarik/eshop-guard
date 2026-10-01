# Delta for Analysis Cache

## ADDED Requirements

### Requirement: Cache jen v PostgreSQL
Systém MUST ukládat odpovědi Jevu, přepisy textů a profily šablon výhradně do PostgreSQL (`checks.jev_answers`, `fixes.rewrite_cache`, `shop.page_profiles`) po tenantech pod RLS a nesmí pro ně vytvářet žádný lokální soubor.

#### Scenario: Běh CLI nevytvoří soubory
- GIVEN prázdná pracovní složka a dostupná databáze s tenantem `cli`
- WHEN uživatel spustí `eshopguard scan http://localhost:8000 --mock`
- THEN po běhu neexistuje složka `cache/` ani soubor `*.sqlite`, `*.sqlite-shm` nebo `*.sqlite-wal`
- AND odpovědi z běhu jsou v `checks.jev_answers` u tenanta `cli`

#### Scenario: Izolace mezi tenanty
- GIVEN odpověď uložená u tenanta A
- WHEN se na stejný klíč zeptá tenant B
- THEN tenant B odpověď nenajde a Jev se pro něj volá zvlášť

### Requirement: Stejné úložiště pro CLI i aplikaci
Systém MUST v CLI i ve workeru používat tytéž implementace `PgJevCache`, `PgRewriteCache` a `PgPageProfileStore` registrované jedním rozšířením. Liší se jen připojení a určení tenanta.

#### Scenario: Registrace služeb
- GIVEN sestavený kontejner služeb CLI a kontejner služeb workeru
- WHEN se z obou vyžádá `IJevCache`, `IRewriteCache` a `IPageProfileStore`
- THEN oba vrátí instance tříd `PgJevCache`, `PgRewriteCache` a `PgPageProfileStore`

#### Scenario: Stejná odpověď v CLI i ve workeru
- GIVEN odpověď Jevu uložená workerem u tenanta `cli` v testovací databázi
- WHEN CLI vyhodnotí stejnou větu se stejnou sadou otázek
- THEN odpověď najde v cache a Jev nevolá

### Requirement: Klíč cache kompatibilní s dneškem
Systém MUST počítat klíč odpovědi Jevu stejně jako dnešní kód (`sha256:…`, `JevCacheKey.LegacyKey`), aby převedené a nové odpovědi měly shodné klíče.

#### Scenario: Referenční klíče
- GIVEN seznam klíčů segmentů testovacích e-shopů zachycený před změnou
- WHEN se klíče spočítají novým kódem
- THEN jsou všechny shodné

### Requirement: Převod dnešní SQLite cache
Systém MUST nabídnout příkaz `eshopguard cache import --from <soubor>`. Ten převede všechny odpovědi Jevu a přepisy ze SQLite do PostgreSQL u tenanta `cli`, je idempotentní, soubor jen čte a nevolá Jev ani OpenAI.

#### Scenario: Úplný převod
- GIVEN `src/cache/jev-cache.sqlite` se 66 159 odpověďmi a 36 přepisy
- WHEN uživatel spustí `eshopguard cache import --from src/cache/jev-cache.sqlite`
- THEN příkaz vypíše 66 159 převedených odpovědí a 36 přepisů
- AND náhodný vzorek 500 odpovědí se po normalizaci JSON shoduje se SQLite
- AND vedle souboru nevznikl `-shm` ani `-wal`

#### Scenario: Opakovaný převod
- GIVEN převod už jednou proběhl
- WHEN se spustí znovu
- THEN nepřibude žádný řádek a příkaz vypíše 0 nových

#### Scenario: Poškozený řádek
- GIVEN řádek SQLite s neplatným JSON
- WHEN převod narazí na tento řádek
- THEN řádek přeskočí, vypíše jeho klíč do seznamu nepřevedených a pokračuje
- AND na konci uvede počet nepřevedených (nic se tiše nezahodí)

### Requirement: Bez databáze žádné placené volání
Systém MUST ukončit CLI s jasnou chybou ještě před odhadem ceny a před prvním voláním Jevu nebo OpenAI, když PostgreSQL není dostupná nebo chybí tenant `cli`.

#### Scenario: Nedostupná databáze
- GIVEN neplatný `ConnectionStrings__Cli`
- WHEN uživatel spustí `eshopguard scan https://example.sk`
- THEN CLI skončí chybou „Databáze cache není dostupná“ s nenulovým návratovým kódem
- AND neproběhne žádné volání Jevu ani OpenAI

#### Scenario: Chybějící tenant
- GIVEN databáze bez tenanta `cli`
- WHEN uživatel spustí `eshopguard scan`
- THEN CLI skončí s radou spustit `eshopguard cache init` a nic nevolá

### Requirement: SQLite odstraněna z knihovny
Systém MUST po této změně neobsahovat v `EshopGuard.Core` žádnou implementaci SQLite ani balíček `Microsoft.Data.Sqlite`. Čtení SQLite smí zůstat jen v převodním příkazu CLI, dokud ho uživatel nezruší.

#### Scenario: Kontrola závislostí
- GIVEN sestavené řešení
- WHEN se projdou `PackageReference` všech projektů kromě `EshopGuard.Cli`
- THEN žádný neodkazuje na `Microsoft.Data.Sqlite`
- AND v `src/EshopGuard.Core` není třída začínající `Sqlite`
