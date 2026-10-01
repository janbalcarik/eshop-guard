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
  - Testy `src/tests/EshopGuard.Core.Tests`.
  - Pravidla `src/rules/*.yaml`, nastavení `src/config/*.yaml`.
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
- **Cache (odpovědi Jevu, přepisy, profily) jen v PostgreSQL**, stejně pro CLI i aplikaci. Žádné lokální soubory cache. Dnešní SQLite `src/cache/jev-cache.sqlite` je přechodná a převede se ve změně 5b, soubor nemazat bez souhlasu.
- Aplikace se připojuje jako role `eshopguard_app` nebo `eshopguard_worker`, ne jako superuživatel, protože ten obchází RLS. Databáze se jmenuje `eshopguard`.
- Žádné slovníky klíčových slov pro klasifikaci. Rozhoduje Jev, LLM nebo struktura stránky.
- Fail-closed: co nebylo zkontrolováno, se uvede, nic se tiše neskrývá ani neslučuje.
- Právo:
  - nevymýšlet čísla paragrafů;
  - řídit se textem zákona;
  - co z něj nejde rozhodnout, je „k ověření“ (a právník);
  - na výklad se uživatele neptat.
- Stahování: dodržovat robots.txt, User-Agent `EshopGuard/0.1`, skenovat jen weby, které uživatel určil.

## Sestavení a testy
- Sestavení: `dotnet build src/EshopGuard.sln`.
- Testy bez placených, Windows: `src/tests/EshopGuard.Core.Tests/bin/Debug/net10.0/EshopGuard.Core.Tests.exe -notrait "Category=Jev"`. Stav 1. 10. 2026: 190 prošlo.
- Testy bez placených, Linux: `dotnet run --project src/tests/EshopGuard.Core.Tests -- -notrait "Category=Jev"`.
- `dotnet test` hlásí 0 testů (Microsoft.Testing.Platform v `global.json`). Oprava je ve změně 2.
- Soubory ukládat v **UTF-8**. Při přejmenování se kvůli jinému kódování rozbil regex s `€`, `Kč`, `zł` v `ContentExtractor.cs`.

## Lokální prostředí
- PostgreSQL 18 na `localhost:5432`. Admin `postgres/postgres` jen pro zakládací skript rolí.
- Cloud: přípravný skript `scripts/cloud-setup.sh` (.NET SDK podle `src/global.json`, PostgreSQL 18, OpenSpec CLI).
