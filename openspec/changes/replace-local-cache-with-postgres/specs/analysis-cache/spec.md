# Delta for Analysis Cache

## ADDED Requirements

### Requirement: Cache jen v PostgreSQL
Systém MUST ukládat odpovědi Jevu, přepisy textů a profily šablon výhradně do PostgreSQL (`checks.jev_answers`, `checks.sieve_answers`, `fixes.rewrite_cache`, `shop.page_profiles`) po tenantech pod RLS a nesmí pro ně vytvářet žádný lokální soubor.

#### Scenario: Běh CLI nevytvoří soubory
- GIVEN prázdná pracovní složka
- WHEN uživatel spustí `eshopguard scan http://localhost:8000 --mock --allow-private-network`
- THEN po běhu neexistuje složka `cache/` ani soubor `*.sqlite`, `*.sqlite-shm` nebo `*.sqlite-wal`

#### Scenario: Odpovědi ostrého běhu jsou v databázi
- GIVEN testovací databáze s tenantem `cli` a falešný klient Jevu
- WHEN se testovací e-shop zkontroluje dvakrát s úložišti PostgreSQL
- THEN odpovědi prvního běhu jsou v `checks.jev_answers` a `checks.sieve_answers` u tenanta `cli`
- AND druhý běh nepošle na Jev žádný požadavek a dá stejné výstupy

#### Scenario: Izolace mezi tenanty
- GIVEN odpověď uložená u tenanta A
- WHEN se na stejný klíč zeptá tenant B
- THEN tenant B odpověď nenajde a Jev se pro něj volá zvlášť

### Requirement: Stejné úložiště pro CLI i aplikaci
Systém MUST v CLI i ve workeru používat tytéž implementace `PgJevCache`, `PgRewriteCache` a `PgPageProfileStore` registrované jedním rozšířením. Liší se jen připojení a určení tenanta.

#### Scenario: Registrace služeb
- GIVEN sestavený kontejner služeb CLI bez `--mock`
- WHEN se z něj vyžádá `IJevCache`, `IRewriteCache` a `IPageProfileStore`
- THEN vrátí instance tříd `PgJevCache`, `PgRewriteCache` a `PgPageProfileStore` z rozšíření `AddEshopGuardPostgresStores`

### Requirement: Klíč cache kompatibilní s dneškem
Systém MUST počítat klíč odpovědi Jevu stejně jako dnešní kód (`sha256:…`, `JevCacheKey.LegacyKey`), aby CLI i worker měly pro stejnou otázku a stav shodný klíč.

#### Scenario: Referenční klíče
- GIVEN seznam klíčů segmentů testovacích e-shopů zachycený před změnou 5
- WHEN se klíče spočítají novým kódem
- THEN jsou všechny shodné

### Requirement: Falešný klient nikdy neplní cache
Systém MUST při běhu s `--mock` nepoužít cache ani databázi, aby se vymyšlené odpovědi nikdy nedostaly mezi skutečné.

#### Scenario: Běh s falešným klientem bez databáze
- GIVEN žádné připojení k databázi
- WHEN uživatel spustí `eshopguard scan <url> --mock`
- THEN sken proběhne bez připojení k databázi a nic do cache nezapíše

### Requirement: Bez databáze žádné placené volání
Systém MUST ukončit CLI s jasnou chybou ještě před stažením, odhadem ceny a prvním voláním Jevu nebo OpenAI, když PostgreSQL není dostupná, chybí migrace nebo chybí tenant `cli`.

#### Scenario: Nedostupná databáze
- GIVEN neplatný `ConnectionStrings__Cli`
- WHEN uživatel spustí `eshopguard scan https://example.sk`
- THEN CLI skončí chybou „Databáze cache není dostupná“ s nenulovým návratovým kódem
- AND neproběhne žádné stažení ani volání Jevu nebo OpenAI

#### Scenario: Chybějící tenant
- GIVEN databáze bez tenanta `cli`
- WHEN uživatel spustí `eshopguard scan`
- THEN CLI skončí s radou spustit `eshopguard cache init` a nic nevolá

### Requirement: SQLite odstraněna
Systém MUST po této změně neobsahovat žádnou implementaci SQLite ani balíček `Microsoft.Data.Sqlite`.

#### Scenario: Kontrola závislostí
- GIVEN sestavené řešení
- WHEN se projdou `PackageReference` všech projektů
- THEN žádný neodkazuje na `Microsoft.Data.Sqlite`
- AND v `src/` není třída začínající `Sqlite`
