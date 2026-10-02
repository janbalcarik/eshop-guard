# Co spustit a ověřit lokálně (stav 2. 10. 2026, po změně 8)

Pořadí: nejdřív kroky zdarma, potom kroky se sítí bez placených volání, nakonec placené kroky. Každý placený krok se spustí jen po odhadu ceny a souhlasu (CLAUDE.md). Příkazy jsou pro PowerShell v kořeni repozitáře, **kromě CLI: to se spouští ze složky `src`** (`cd src`, potom `dotnet run --project EshopGuard.Cli -- …`), protože tam hledá `config/`, `rules/` a `.env`. Z kořene skončí chybou „Pravidla nejsou platná: … neexistuje“. Klíče patří do `.env` (CLI) nebo do proměnných prostředí (worker), nikdy do souborů v repozitáři.

## 0. Příprava (zdarma, bez sítě)

```powershell
git pull
pwsh deploy/dev/setup-local.ps1        # jen poprvé nebo po změně rolí; zapíše user-secrets (i eshopguard-worker)
dotnet tool restore
dotnet ef database update --project src/EshopGuard.Data   # migrace F4 změny 8 a F4LanguagesByCountry (cena za každou zemi: shop_languages.translated_share a description_languages místo own_text_share, comparison a counted)
dotnet build src/EshopGuard.sln
dotnet test --solution src/EshopGuard.sln --filter-not-trait "Category=Jev"
```

Očekávání: 1052 testů, 0 selhání, 10 explicitních přeskočeno (cloud, 2. 10. 2026). Měření 5 000 stránek (paměť a férovost fronty) se spouští zvlášť: `dotnet run --project src/tests/EshopGuard.Jobs.Tests -- -class "EshopGuard.Jobs.Tests.Runs.RulesMemoryTests" -explicit only -showLiveOutput` (asi 2 minuty; výsledek z cloudu je v README, oddíl Běhy analýzy ve workeru). Testy běhů ve workeru (`src/tests/EshopGuard.Jobs.Tests/Runs`) potřebují PostgreSQL a databázi `eshopguard_test_jobs`. Když testy `Db` selžou se jménem klíče, chybí user-secrets `eshopguard-tests`: spusťte znovu `setup-local.ps1`.

Pokud CLI ještě nemá tenanta `cli`, jednou ze složky `src`: `dotnet run --project EshopGuard.Cli -- cache init`.

## Test na skutečném e-shopu: doporučené pořadí

Worker zkontroluje skutečný e-shop stejně jako aplikace (tenant `cli`, takže sdílí cache Jevu s CLI a Jev se platí jen jednou).

1. **Krok 2.2** (0 USD): ukázka zdarma s falešným Jevem a OpenAI na skutečném webu. Ověří stahování, robots.txt, jazykové verze, plán 100 stránek, stavy a události, `estimate.basis` s `other_pages` po verzích a kódy chyb adres (`run_urls.http_status`, `error_code`).
2. **Krok 3.6** (≤ 1,00 USD, strop ukázky je souhlas s cenou): stejná ukázka se skutečným Jevem a OpenAI na jiné doméně, nebo po smazání nároku na doménu.
3. **Krok 3.7** (~2,75 USD na 500 stránek, neměřeno): úvodní analýza. Worker se zastaví na `awaiting_payment` a ukáže interní odhad; pokračuje se až po vašem souhlasu (`dev approve-run`).

Po každém kroku mi pošlete výstup dotazů z kroku 2.2 (stavy, události, `stats`, `estimate`, počty `run_urls`). Výsledky porovnám s CLI a s Jevem.

## 1. Zdarma, bez sítě

### 1.1 Nahrávky cizích e-shopů (změny 5 a 7)

```powershell
dotnet run --project src/tests/EshopGuard.Cli.Tests -- -explicit only -trait "Category=Snapshot"
```

Změna 7 opravila poznání produktové sitemap (název souboru „sitemap“ obsahuje „item“, odchylka 7 změny 7). Rozdíl proti `src/baselines/` je proto možný a čekaný jen ve značce produktů a v počtu produktových stránek. Výstup mi pošlete: posoudím ho a referenční výstupy přegeneruji.

## 2. Síť bez placených volání (stahuje cizí weby; robots.txt a User-Agent `EshopGuard/0.1` s kontaktem)

### 2.1 Změna 7, úkol 7.2: jazykové verze

```powershell
cd src
dotnet run --project EshopGuard.Cli -- markets https://www.goodie.sk/ --mock
# totéž pro bonami, footshop, freshlabels, panakeia, nutriadapt, havlikovaapoteka
```

Očekávání: každá verze SK a CS dostupná bez JavaScriptu má správný `html lang` (shoda s ověřením 7 ze 7 z 1. 10. 2026). Současně ostré stažení vegis.sk a naturfyt.sk a porovnání vytěženého textu s nahrávkami změny 5 (cookie se po změně 7 posílají po rozsazích, `UseCookies = false`). Odhad: 0 USD, desítky požadavků na web.

### 2.2 Změna 8: ukázka zdarma ve workeru s falešnými klienty (0 USD)

Mock Jevu a OpenAI ve workeru je povolený jen v Development (`launchSettings.json` ho nastaví). Do kontaktu dejte svůj e-mail.

```powershell
$env:EshopGuard__Jev__UseMock = "true"
$env:EshopGuard__Rewrite__UseMock = "true"
$env:EshopGuard__Crawl__UserAgent = "EshopGuard/0.1 (+mailto:VAS-EMAIL)"
dotnet run --project src/EshopGuard.Worker -- dev seed-run --tenant cli --shop-url https://www.naturfyt.sk/ --kind free_sample
dotnet run --project src/EshopGuard.Worker          # nechat běžet, než běh skončí (Ctrl+C)
```

Nárok na ukázku zdarma je jednou na doménu. Mock proto pusťte na jiné doméně, než bude placená ukázka v kroku 3.6, nebo nárok potom smažte jako `postgres`: `DELETE FROM shop.free_sample_claims WHERE domain = 'naturfyt.sk';`.

Kontrola (psql jako `postgres`):

```sql
SELECT id, status, error, stats -> 'sample', stats -> 'unchecked', estimate -> 'basis'
FROM checks.runs ORDER BY created_at DESC LIMIT 1;
SELECT code, data FROM checks.run_events WHERE run_id = '<id běhu>' ORDER BY id;
SELECT queue, state, count(*) FROM checks.run_urls WHERE run_id = '<id běhu>' GROUP BY 1, 2;
SELECT state, error_code, http_status, count(*) FROM checks.run_urls WHERE run_id = '<id běhu>' AND state NOT IN ('extracted', 'fetched') GROUP BY 1, 2, 3;
SELECT kind, max(attempts), count(*) FROM ops.jobs WHERE run_id = '<id běhu>' GROUP BY 1 ORDER BY 1;
```

Očekávání: stavy `discovering → crawling → … → finished | partial` (bez `awaiting_payment`), nejvýš 100 plánovaných stránek ve frontách `sample_*` (když má sitemap produkty), souhrn ukázky a `estimate.basis` (u zkontrolovaných verzí `product_count` a `other_pages`), v událostech žádný text stránky ani částka. Adresy s chybou mají kód (`http_404` → `gone`, `http_429`, `timeout`); `attempts` nad 1 znamená zopakovanou dávku (výpadek služby nebo pád workeru). Soubory běhu jsou v `.data/blobs/tenants/…/runs/<id>/`.

## 3. Placené kroky (každý jen po odhadu a souhlasu)

| # | Co | Odhad | Odkud |
|---|---|---|---|
| 3.1 | Živé testy Jevu nad `Fixtures/site` (země `cz`, nové otázky `legal_cz`) | ~0,02 USD | změna 6, úkol 6.4 |
| 3.2 | Jazyk verzí na 5 e-shopech s páry (bonami, freshlabels, goodie, havlikovaapoteka, panakeia) | ~0,04 USD | změna 7, úkol 7.4 |
| 3.3 | Místa prodeje na 9 e-shopech | ~0,42 USD | změna 7, úkol 7.3 |
| 3.4 | Nový přepis vegis.sk a naturfyt.sk a srovnání se starými návrhy | ~0,40 USD | změna 6, úkol 8.6 |
| 3.5 | Nahrávka vegis.sk s `--jurisdictions sk,cz` s Jevem a cache | ~1,5–2 USD | změna 6, úkol 8.5 |
| 3.6 | Ukázka zdarma vegis.sk (nebo naturfyt.sk) ve workeru se skutečným Jevem a OpenAI | ≤ 1,00 USD (strop ukázky) | změna 8, úkol 13.7 |
| 3.7 | Úvodní analýza vegis.sk ve workeru a srovnání s CLI | ~2,75 USD na 500 stránek, celý web ~32 USD (neměřeno) | změna 8, úkoly 13.5–13.6 |
| 3.8 | Kontrola slovenských překladů textů pravidel: sken 2–3 e-shopů a srovnání vlastního rozboru stránek s Jevem | podle e-shopů, z cache většinou 0 | změna 6, úkoly 4.4, 4.5 a 8.4 |
| 3.9 | Porovnání verzí goodie.sk na popisu z profilu (`markets --profiles`) | **hotovo 2. 10. 2026: 0,206 USD** (rozbor 0,057 + profil 0,149) | změna 7, odchylka 20 |
| 3.10 | Práh započtení verze a jazyk popisů: `markets --profiles` na bonami, freshlabels, havlikovaapoteka, panakeia a znovu goodie | ~0,55–0,95 USD (rozbor ~0,06 USD na e-shop, nový profil 0,07–0,15 USD; goodie má profil uložený) | změna 7, úkol 7.4 a odchylka 20 |
| 3.11 | Cena za každou zemi a produkty bez produktové sitemap: `markets --profiles --markets sk,cz` na stejných 5 e-shopech | ~0,30–0,60 USD (rozbor ~0,06 USD na e-shop; freshlabels a havlikovaapoteka dostanou poprvé produkty do vzorku, takže možná nový profil 0,07–0,15 USD) | změna 7, úkoly 8.4–8.6 |

Postup u jednotlivých kroků:

- **3.1:** `dotnet run --project src/tests/EshopGuard.Core.Tests -- -trait "Category=Jev"` (klíč v `.env`).
- **3.2 až 3.5:** CLI vypíše odhad z dotazu před prvním voláním a nad limitem se zeptá. Příkazy jsou v `tasks.md` změn 6 a 7. Výsledky 3.2 a 3.3 se zapíšou do README, oddíl Místa prodeje a jazykové verze (úkol 7.5 změny 7).
- **3.6:** jako krok 2.2, ale bez proměnných `UseMock`, s klíči v proměnných prostředí `TYPESAFE_API_KEY` a `OPENAI_API_KEY`. Strop `Runs:FreeSample:MaxInternalUsd` (1,00 USD) je souhlas s cenou: nad odhadem se nic nezaplatí a běh skončí `failed` s kódem `sample_budget_exceeded`. Skutečnou cenu ukáže `SELECT provider, operation, sum(calls), sum(input_tokens), sum(cost_usd) FROM usage.usage_records WHERE run_id = '<id>' GROUP BY 1, 2;`. Ověřit souhrn ukázky, rozbor verzí (`shop.shop_languages`) a `estimate.basis`.
- **3.7:** varianta 500 stránek: `$env:Runs__FullAnalysis__MaxPages = "500"`. Postup:
  1. `dev seed-run --tenant cli --shop-url https://vegis.sk/ --kind full_analysis` bez `--approve`;
  2. spustit worker; běh se zastaví ve stavu `awaiting_payment` s hrubým odhadem v `checks.runs.estimate -> 'internal'`;
  3. odhad mi pošlete, já ho porovnám s tabulkou a vy rozhodnete;
  4. po souhlasu `dotnet run --project src/EshopGuard.Worker -- dev approve-run --tenant cli --run <id>`.

  Worker běží pod tenantem `cli`, takže následný `eshopguard scan https://vegis.sk/ --max-pages 500` vezme odpovědi Jevu z jeho cache a Jev se zaplatí jen jednou. Srovnání nálezů (pravidlo, text, stránky, verdikty) udělám já.
- **3.8:** po 3.5 nebo 3.7 projdu nálezy a texty pravidel `rules/texts/sk/` proti stránkám. Kde souhlasí, odstraní se `machine_draft` a vyplní `review` (kontrolu potvrdíte vy).
- **3.9:** `cd src` a `dotnet run --project EshopGuard.Cli -- markets https://www.goodie.sk/ --profiles` (klíč OpenAI v `.env`, databáze jako u `scan`). CLI vypíše odhad nejdřív pro rozbor a potom znovu s novými profily („z toho nové profily šablon …“); nad limitem se zeptá. Pod tabulkou verzí je u každé verze, co se porovnalo (popis z profilu, nebo celý text), podíl spárovaných produktů s vlastním textem a počet popisů v jiném jazyce. `markets.json` mi pošlete: ověřím, že recenze z porovnání vypadly a že české popisy na slovenské verzi vyšly jako `untranslated_text`. Profil zůstane v databázi a `scan https://www.goodie.sk/` ho použije bez nového volání. Výsledek 2. 10. 2026: vlastní texty sk 87 %, cs-cz 88 % (bez profilu 49 a 52 %), profil `goodie.sk#1`; podrobnosti v odchylce 20 změny 7. *(Od 2. 10. 2026 se texty verzí neporovnávají: výpis ukazuje u verze popisy v jejím jazyce, např. „19 z 20“, a pod tabulkou produkty za každou zemi.)*
- **3.10:** ze složky `src`, s klíčem OpenAI v `.env` a databází jako v kroku 3.9:

  ```powershell
  $out = "out/verze-2026-10-02"
  foreach ($url in 'https://www.bonami.sk/', 'https://www.freshlabels.sk/', 'https://www.havlikovaapoteka.cz/', 'https://www.panakeia.sk/', 'https://www.goodie.sk/') {
      dotnet run --project EshopGuard.Cli -- markets $url --profiles --out $out
  }
  Get-ChildItem $out -Recurse -Filter markets.json | Select-Object FullName
  ```

  Každý běh vypíše odhad a nad limitem `rewrite.max_usd_without_confirm` (1,00 USD) se zeptá. Když web přesměruje jinam (bez `www`, jiná doména), použijte adresu z prohlížeče. Pošlete mi všech pět `markets.json`: v `details[].language_fragments` jsou věty, kterým model určil jazyk, takže ověřím, jestli goodie.sk má české popisy (výzkum 1. 10. 2026: 2 z 10) a jestli nové pravidlo většiny vět žádný nepřehlédne. *(Hotovo 2. 10. 2026, 0,561 USD; výsledky v odchylce 21 změny 7. Práh vlastních textů pak uživatel zrušil: cena za každou zemi.)* Z vlastních textů a druhů párů navrhnu práh `markets.counted_min_own_share` a porovnám je s výzkumem (bonami 10× překlad, havlikovaapoteka 6× překlad a 1× zkráceno, panakeia 9× jiný text).

- **3.11:** ze složky `src` jako 3.10, jen s `--markets sk,cz` a novou složkou:

  ```powershell
  $out = "out/zeme-2026-10-02"
  foreach ($url in 'https://www.bonami.sk/', 'https://www.freshlabels.sk/', 'https://www.havlikovaapoteka.cz/', 'https://www.panakeia.sk/', 'https://www.goodie.sk/') {
      dotnet run --project EshopGuard.Cli -- markets $url --profiles --markets sk,cz --out $out
  }
  ```

  Pod tabulkou verzí je řádek „Produkty do ceny za každou zemi: SK … + CZ … = …“ a u verze „popisy v jazyku verze“ (např. 19 z 20). Pošlete mi pět `markets.json`. Ověřím, že freshlabels.sk a havlikovaapoteka.cz mají ve vzorku produkty nalezené podle struktury stránky (dřív `version_sample_insufficient`) a počet produktů neznámý, a že u goodie.sk zůstaly české popisy jako `untranslated_text`.

## 4. Rozhodnutí, která zůstávají na vás

- Znění otázek pro uživatele u 13 pravidel `verify` (změna 6, K13; návrh v `otazky-pro-uzivatele-navrh.md`).
- Strop ukázky zdarma: výchozí hodnota 1,00 USD (změna 8, K2).
- Změna 8, K6: má nárok na ukázku blokovat i domény jazykových verzí?
- Změna 8, odchylka 11: verze přepínané jen cookie nebo Accept-Language se ve workeru zatím nestahují, běh je vyjmenuje.
