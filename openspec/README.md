# Implementační plán EshopGuard (OpenSpec)

Plán je rozdělený na **změny** (`changes/`). Každá změna je jeden ověřitelný krok:

- `proposal.md`: proč a co;
- `design.md`: jak, u složitějších změn;
- `specs/<schopnost>/spec.md`: požadavky se scénáři, které musí platit po dokončení;
- `tasks.md`: úkoly k odškrtání.

Po dokončení a archivaci se požadavky přesunou do `specs/` jako popis toho, co systém umí.

Podklady jsou v nadřazené složce `D:\_github\Overko`:
- `architektura-multitenant-worker-2026-10-01.md`;
- `databaze-a-plan-implementace-2026-10-01.md`;
- `platby-a-fakturace-2026-10-01.md`;
- `hostovani-srovnani-2026-10-01.md`;
- `strategie-a-cenik-2026-09-30.md`;
- `navrhy-rozvoje-2026-09-30.md`;
- `podklady/reserse/konektory-api-2026-10-01.md`.

Návrh UI je v repozitáři v `design/ui/` (21 obrazovek `*.dc.html`, popis v `design/ui/README.md`). Živé plátno: https://claude.ai/artifact/34wYLsJzieFtmWdueAwYay

## Jak s plánem pracovat v Claude Code

1. Jednorázově nainstalovat CLI a příkazy pro Claude Code:

   ```bash
   npm install -g @fission-ai/openspec@latest
   ```

   ```bash
   openspec init --tools claude
   ```

   `init` doplní příkazy do `.claude/commands`. Existující `config.yaml` a `changes/` zůstanou.
2. Kontrola formátu: `openspec validate --all` (1. 10. 2026: 17 z 17 změn prošlo, CLI 1.3.1; bez telemetrie s `DO_NOT_TRACK=1`).
3. Práce na změně:
   - `/opsx:apply <změna>`: Claude provede úkoly z `tasks.md` a odškrtá je;
   - `/opsx:verify`: porovná výsledek se specifikací;
   - `/opsx:archive`: přesune změnu do archivu a požadavky do `specs/`.
4. Před každou fází vysvětlit, co přinese, a počkat na souhlas uživatele. Placené běhy (Jev, OpenAI) jen s odhadem ceny a souhlasem.

## Pořadí změn

| # | Změna | Fáze | Závisí na | Schopnosti (specs) |
|---|---|---|---|---|
| 1 | `rename-to-eshopguard` ✓ hotovo 1. 10. 2026 (archiv) | F0 | – | `product-identity` |
| 2 | `add-solution-foundation` | F0 | 1 | `operations` |
| 3 | `add-multitenant-data-model` | F1 | 2 | `tenancy` |
| 4 | `add-job-queue-and-worker` | F2 | 3 | `job-queue` |
| 5 | `refactor-library-into-pipeline-steps` | F3 | 1 (souběžně s 3–4) | `site-analysis` |
| 5b | `replace-local-cache-with-postgres` | F3 | 3, 5 | `analysis-cache` |
| 6 | `add-multi-jurisdiction-rules-and-rule-texts` | F3 | 5 | `rules-and-verdicts`, `localization` |
| 7 | `add-places-of-sale-and-language-versions` | F3 | 5, 6 | `markets-and-languages` |
| 8 | `add-analysis-runs-in-worker` | F4 | 4, 5–7, 5b | `analysis-runs` |
| 9 | `add-identity-and-tenants-api` | F5 | 3 | `identity` |
| 10 | `add-shops-and-onboarding-api` | F5 | 7, 8, 9 | `shops-onboarding` |
| 11 | `add-findings-and-fixes-api` | F5 | 8, 9 | `findings-and-fixes` |
| 12 | `add-billing-and-invoicing` | F6 | 10 | `billing` |
| 13 | `add-web-app-frontend` | F7 | 9–12 | `web-app` |
| 14 | `add-public-website-cms` | F7 | 12 (ceny) | `public-website`, `localization` |
| 15 | `add-shoptet-connector` | F8 | 8, 11 | `connectors` |
| 16 | `add-change-monitoring` | F9 | 8, 15 | `monitoring` |
| 17 | `add-single-server-operations` | F10 | 2–16 | `operations` |

Souběh:
- 5–7 (knihovna) nesahají na databázi a můžou běžet souběžně s 3–4;
- 13 a 14 se dají dělat průběžně po obrazovkách;
- 12 je potřeba před prvním prodejem;
- 15 a 16 až po pilotu.

## Pravidla psaní

- Česky. Normativní slovo `MUST` nebo `SHALL` zůstává anglicky kvůli validaci (`Systém MUST …`).
- Každý požadavek má scénář `GIVEN / WHEN / THEN`, včetně chybového případu.
- Názvy požadavků (`### Requirement:`) jsou v rámci jedné schopnosti jedinečné napříč změnami.
- Úkoly odkazují na skutečné soubory a třídy a končí testem, který hotovo dokazuje.

## Stav plánu (1. 10. 2026)

- 17 změn, 212 požadavků, 701 scénářů, 1 006 úkolů; `openspec validate --all` prošlo u všech.
- Otevřená rozhodnutí roztříděná v [K-ROZHODNUTI.md](K-ROZHODNUTI.md):
  - A: rozhoduje uživatel;
  - B: technické výchozí volby;
  - C: mezery v plánu;
  - D: ověřit v dokumentaci.
- Hranice mezi změnami dohodnuté při psaní:
  - nabídka ceny: 10 rozsah a `quote`, 12 částky a objednávka, 8 jen základ;
  - cache v PostgreSQL pro CLI i aplikaci (bez SQLite): změna 5b; ostatní úložiště: změna 8;
  - koncový bod publikace: změna 15;
  - upozornění: změna 11.
