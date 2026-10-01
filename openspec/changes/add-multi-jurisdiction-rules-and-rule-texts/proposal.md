# Proposal: Pravidla pro víc zemí, verdikty po zemích a texty pravidel v jazykových souborech

## Intent

**Problém.**
- **Jedna země na běh.** `ScanOptions.Country` a `AnalyzeOptions.Country` jsou jediná hodnota. `EshopGuardService.SelectRuleSets` vybere sady jen pro ni a `RuleEngine.RefsFor` vrátí odkazy jen pro ni. Český e-shop, který prodává na Slovensko, musí podle architektury (část 12, Místa prodeje) splnit i slovenská pravidla. Dnes by na to potřeboval dva samostatné běhy a dvakrát zaplatil Jev.
- **Kolize při dvou zemích najednou.** `legal_sk.yaml` a `legal_cz.yaml` mají stejné id otázek (`legal_adr`, `legal_complaints`, `legal_withdrawal`, `legal_withdrawal_form`) s různým zněním a stejná id pravidel (`legal_adr_missing` a další). `RuleValidator.Validate` to dovoluje jen proto, že se jejich `jurisdictions` nepřekrývají. `SegmentEvaluator.Group` skládá otázky všech sad jedné věty do jednoho slovníku a `Segment.Probabilities` je klíčované jen id otázky. Při SK a CZ najednou by se odpovědi přepsaly nebo by `ToDictionary` spadlo.
- **Texty jen česky a uvnitř pravidel.** `title`, `explanation`, `explanation_by_jurisdiction` a `recommendation` jsou přímo v `rules/*.yaml`, většinou česky (33 zapnutých pravidel, ~21 000 znaků podle architektury, část 12). `legal_sk.yaml` je naopak slovensky a `dur.yaml` míchá oba jazyky (název „Navádzanie na skoršiu výmenu…“ s českým vysvětlením). Slovenský obchodník tak dostane české vysvětlení a český uživatel slovenského e-shopu slovenské.
- **Knihovna vrací hotové české věty.** `Finding.Title`, `Explanation`, `Recommendation`, `Finding.Notes` (např. „Nejbližší nalezený odstavec má pravděpodobnost …“) a `ScanResult.Warnings` jsou české věty. API má podle architektury (část 12, Aplikace) vracet kódy a parametry a text skládat v jazyce uživatele.
- **Účinnost povinností chybí.** Pravidlo `legal_withdrawal_function_missing` (SK, od 19. 6. 2026) má datum jen v názvu. České tlačítko „Odstoupit od smlouvy“ platí od 1. 1. 2027 a pravidlo pro něj zatím není.

**Co změna přinese.**
- Jedno vyhodnocení Jevem a nálezy pro každou zaškrtnutou zemi. Cena navíc za druhou zemi jsou jen otázky, které se liší (právní odstavce), ne celé věty znovu.
- Nález nese verdikt po zemích („SK · porušení“, „CZ · na posouzení“) a řadí se podle nejpřísnějšího.
- Návrh opravy projde kontrolou ve všech zvolených zemích, takže oprava pro Slovensko nezpůsobí problém v Česku.
- Texty pravidel slovensky i česky, lidsky zkontrolované, odkazy na zákon v jazyce zákona. Jazyk bez úplných textů nejde zapnout.
- Povinnosti za celý web po zemích s účinností: obchodník vidí, co platí teď a co od kdy.

**Fáze:** F3 Knihovna po krocích.

**Podklad:**
- `architektura-multitenant-worker-2026-10-01.md`: část 12 „Čtyři různé věci, které se nesmí slít“, „Místa prodeje e-shopu“ (Vyhodnocení, body 4a–4e), „Aplikace“ (API nevrací hotové věty; texty nálezů a otázek z pravidel; dnešní stav pravidel; kontrola úplnosti), „Přidání dalšího trhu“;
- `databaze-a-plan-implementace-2026-10-01.md`: část 3.4 (`rule_sets.texts`, `question_set_hash`, `source_hash`; `findings.verdicts`, `legal_refs`, `params`; `questions.code`, `params`), 3.9 (`ref.locales`), část 8 fáze F3;
- `navrhy-rozvoje-2026-09-30.md`: návrh 5 („Čo pomôže“ u každého nálezu), 7 (otázka na doklad), 10 (Česko: zákon 159/2026 Sb.);
- `podklady/predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt` (§ 1830a a § 1820 odst. 1 písm. i) OZ, účinnost 1. 1. 2027) a `podklady/reserse/cz-informacni-povinnosti.md`, oddíl `cz_withdrawal_button`;
- kód: `rules/*.yaml`, `config/labels.yaml` (`label_notes`), `src/EshopGuard.Core/Rules/RuleModels.cs`, `RuleEngine.cs`, `RuleValidator.cs`, `YamlRuleSetProvider.cs`, `SegmentEvaluator.cs`, `Models/Finding.cs`, `Fix/PageRewriter.cs`, `Fix/RewritePrompt.cs`, `Fix/ScanOutputReader.cs`, `Report/*.cs`.

Cesty jsou po přejmenování ze změny 1 a po rozdělení na kroky ze změny 5 (`RulesStep`, `EvaluateStep`).

## Scope

In scope:
- Texty pravidel v jazykových souborech `rules/texts/cs/<sada>.yaml` a `rules/texts/sk/<sada>.yaml` pro každou sadu (`eco`, `dur`, `ucp`, `legal_sk`, `legal_cz`): název, vysvětlení, vysvětlení pro jurisdikci, doporučení, znění otázek pro uživatele. Texty nástroje (poznámky k nálezům, upozornění běhu, vestavěný nález `legal_pages_missing`, poznámky ke značkám) v `rules/texts/<jazyk>/_engine.yaml` a `_labels.yaml`.
- Z `rules/*.yaml` zmizí `title`, `explanation`, `explanation_by_jurisdiction` a `recommendation`. Zůstanou otázky pro Jev, logika, pásma, závažnost, `checkability` a `legal_refs` (v jazyce zákona). Přibude `jurisdiction_overrides` (závažnost, `checkability`, účinnost po zemích) a `user_questions` (kódy otázek pro uživatele).
- Přesun textů beze změny znění pro češtinu (strojově, s kontrolou shody), slovenský překlad českých textů a český překlad textů `legal_sk` s povinnou lidskou kontrolou. Žádný neověřený strojový překlad.
- `IRuleTextProvider`, `YamlRuleTextProvider` a `RuleTextRenderer`: text podle trojice pravidlo × jurisdikce × jazyk a parametrů.
- Knihovna vrací kódy a parametry: `Finding.Notes` → `FindingNote(Code, Params)`, `ScanResult.Warnings` → `ScanWarning(Code, Params)`, `Finding.Params`; `Finding` nenese `Title`, `Explanation` ani `Recommendation`.
- Vyhodnocení pro víc jurisdikcí nad společnými odpověďmi Jevu: `ScanOptions.Jurisdictions`, `AnalyzeOptions.Jurisdictions`, sjednocení sad, rozdělení požadavku na Jev při kolizi id otázek, odpovědi po sadách.
- Verdikt po zemích `JurisdictionVerdict` (pásmo, závažnost, `checkability`, odkazy na zákon, varianta vysvětlení, účinnost, stav) v `Finding.Verdicts`; nález ze stejného pravidla a stejné věty pro víc zemí je jeden; řazení podle nejpřísnějšího verdiktu.
- Účinnost povinností po zemích (`effective_from`) a datum vyhodnocení (`AsOf`); před účinností verdikt `upcoming`.
- Povinnosti za celý web po zemích (`ScanResult.SiteObligations`) a pokrytí (`ScanResult.JurisdictionCoverage`: které moduly v které zemi běžely a které ne).
- Nové pravidlo pro Česko: tlačítko „Odstoupit od smlouvy“ a údaj o něm v informacích před uzavřením smlouvy, účinnost 1. 1. 2027, podle textu zákona v podkladech.
- Návrh opravy musí projít kontrolou ve všech zvolených jurisdikcích (`PageRewriter.CheckAsync`).
- Popis verze sady pravidel pro databázi (`RuleSetDescriptor`: `module`, `version`, `jurisdictions`, `question_set_hash`, `definition`, `texts` po jazycích, `source_hash`) pro `checks.rule_sets`.
- Test úplnosti klíčů všech jazyků a `RuleCatalog.CompleteLocales`.
- CLI: `--jurisdictions sk,cz` a `--lang cs|sk`; výchozí běh s jednou zemí a češtinou dá stejnou zprávu jako dnes.

Out of scope:
- Texty webu, aplikace a e-mailů (`messages/*.json`, CMS): změny 13 a 14. Tato změna v `localization` řeší jen „Texty pravidel“.
- Tabulky `checks.rule_sets`, `checks.findings`, `checks.questions` a jejich zápis: změny 3 a 8.
- Zapnutí modulu `eco` nebo `dur` pro Česko (právní rozhodnutí, README: v Česku směrnice EmpCo zatím neplatí).
- Texty vypnutých sad `lr.yaml` a `ucp_parked.yaml` (K rozhodnutí 6) a seznam `config/legal_requirements.yaml`.
- Rozpoznání míst prodeje a jazykových verzí (změna 7); tato změna dostane seznam jurisdikcí hotový.
- Další země a jazyky než SK a CZ, sk a cs.
- Překlad otázek pro Jev (zůstávají anglicky a v jazyce země v `text_en`/`text_cs`).
- Protokol PDF (změna 11).

## Approach

1. **Nejdřív přesun bez změny významu.** Pomocný příkaz `eshopguard rules extract-texts` přenese texty z `rules/*.yaml` do `rules/texts/cs/` (a texty `legal_sk.yaml` do `rules/texts/sk/`) bajt po bajtu. Test porovná, že `RuleTextRenderer` pro češtinu a jednu zemi vrátí přesně dnešní texty a `report.md` testovacích e-shopů je stejný jako před změnou.
2. **Logické pravidlo, implementace po zemích.** Id pravidla je logický požadavek (např. `legal_adr_missing`); sada pro každou zemi ho může implementovat vlastními otázkami. Odpovědi Jevu jsou společné, kde jsou otázky shodné, a oddělené, kde se liší. Nález ze stejného pravidla a stejné věty je jeden a nese verdikty všech zemí, kde pravidlo platí (K rozhodnutí 1).
3. **Kódy místo vět.** Engine skládá `FindingNote` a `ScanWarning` s kódem a parametry. Texty k nim jsou v `_engine.yaml`. Hotové věty skládá jen okraj: zprávy CLI (`IReportWriter`), zadání přepisu (`RewritePrompt`) a později API a frontend.
4. **Účinnost jako data.** `effective_from` u pravidla po jurisdikci, datum vyhodnocení z `TimeProvider` (v testech pevné). Před účinností se pravidlo vyhodnotí, ale verdikt je `upcoming` a nezvyšuje přísnost nálezu.
5. **Překlad lidmi.** Kostra `rules/texts/sk/*.yaml` se vygeneruje s prázdnými hodnotami a metadaty `review`. Jazyk je úplný, jen když má každý soubor `review.reviewed_by` a `reviewed_at`. Pokud uživatel povolí návrh překladu modelem, je to placený úkol s odhadem ceny a souhlasem a výsledek stejně projde lidskou kontrolou.
6. **Žádné nové paragrafy.** Odkazy na zákon se jen přenesou z dnešních pravidel a z podkladů. Nové české pravidlo cituje jen ustanovení, která jsou doslova v `podklady/predpisy-cz/cz-159-2026-…txt` a v rešerši, se stavem „ověřit“.

## Dependencies

- **Změna 5 `refactor-library-into-pipeline-steps`:** `RulesStep`, `EvaluateStep`, `JevCacheKey` (texty nesmí měnit `QuestionSetHash`), `InMemoryPipelineRunner`.
- Navazují: změna 7 (dodá seznam jurisdikcí a verze, které čtou zákazníci kterých zemí), změna 8 (zápis `findings.verdicts`, `checks.rule_sets` z `RuleSetDescriptor`), změna 11 (API skládá texty nálezů a otázek z `IRuleTextProvider`), změna 13 (frontend), změna 3 (sloupce `findings.verdicts`, `rule_sets.texts`).

## Done when

- `report.md`, `findings.csv` a texty ve `findings.json` testovacích e-shopů pro jednu zemi a češtinu jsou stejné jako před změnou (referenční výstupy ze změny 5).
- Běh se `--jurisdictions sk,cz` nad `Fixtures/site-sk` dá nálezy s verdikty pro SK i CZ, Jev dostane pro věty stejný počet požadavků jako běh jen pro SK a pro právní odstavce dva požadavky tam, kde se otázky SK a CZ liší.
- Test úplnosti projde pro `cs` i `sk`: každé zapnuté pravidlo má v obou jazycích název, vysvětlení, doporučení, vysvětlení pro každou jurisdikci, kterou má v češtině, a otázky pro uživatele; každý kód z `_engine.yaml` má text; zástupné symboly sedí.
- `rules/texts/sk/*.yaml` a český překlad `legal_sk` mají vyplněné `review.reviewed_by` a `reviewed_at` od člověka.
- Nové české pravidlo pro tlačítko odstoupení dá před 1. 1. 2027 verdikt `upcoming` a od 1. 1. 2027 nález (test s pevným datem).
- Návrh opravy, který nález odstraní v SK, ale v CZ zůstane, má stav `StillFinding` (test).
- Žádný `Finding`, `ScanWarning` ani `FindingNote` v `ScanResult` neobsahuje hotovou českou nebo slovenskou větu (test serializace).
- `dotnet test` projde.

## K rozhodnutí

1. **Stejné id pravidla ve dvou zemích = jeden nález s verdikty, nebo dva nálezy?** Návrh: jeden nález (`legal_adr_missing` s verdiktem SK i CZ). Alternativa: přejmenovat česká pravidla a otázky (`legal_cz_adr_missing`, `legal_cz_adr`); pak by se znehodnotila cache českých právních odstavců (malá částka, neměřeno) a verdikty po zemích by se u právních povinností nespojovaly.
2. **`findings.rule_set_id` je jeden sloupec**, ale nález s verdikty SK a CZ vzniká ze dvou sad (`legal_sk`, `legal_cz`). Návrh: každý verdikt nese svůj `rule_set` (modul a verzi), sloupec dostane sadu nejpřísnějšího verdiktu. Upravit změnu 3.
3. **Pořadí přísnosti.** Návrh: `checkability` (`text` porušení > `assess` na posouzení > `verify` k ověření > `not_checkable`), pak závažnost (`high` > `medium` > `low`), pak pásmo (`high` > `review`), verdikt `upcoming` vždy za platnými. Potvrdit.
4. **Země, kde modul nemá pravidla** (např. `eco` a `dur` jen pro SK u e-shopu, který prodává i do Česka). Návrh: verdikt pro takovou zemi u nálezu není; běh uvede v `JurisdictionCoverage`, že modul pro CZ neběžel a proč. Alternativa: u každého nálezu verdikt „pro CZ se nekontroluje“ (víc šumu).
5. **Jazyk textů v zadání přepisu.** `RewritePrompt` dnes vkládá české `title`, `explanation` a `recommendation` do slovenského zadání. Návrh: texty v jazyce obsahu e-shopu (slovenský e-shop → `sk`) a vysvětlení všech verdiktů. Mění zadání, proto nová `RewritePrompt.Version` a přepisy se znovu zaplatí (cache přepisů neplatí).
6. **Vypnuté sady `lr.yaml` a `ucp_parked.yaml`:** přeložit teď, nebo až před zapnutím po pilotu (návrh rozvoje 8)? Návrh: až před zapnutím; validátor nedovolí sadu zapnout bez úplných textů.
7. **Rozsah překladu je větší než 21 000 znaků:** k textům pravidel přibývají poznámky ke značkám v `config/labels.yaml` (`label_notes`), texty nástroje (`_engine.yaml`, ~30 kódů) a český překlad `legal_sk` (8 pravidel). Neměřeno přesně.
8. **Kdo překládá a kontroluje.** Zásada je „žádný neověřený strojový překlad“. Smí návrh překladu napsat model OpenAI (placené, odhad a souhlas), pokud ho pak zkontroluje člověk? Kdo je kontrolor (rodilý mluvčí, právník)?
9. **Nesrovnalost se zadáním:** zákon 159/2026 Sb. **je** v podkladech (`podklady/predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt`, účinnost 1. 1. 2027, § 1830a odst. 2 s nápisem „Odstoupit od smlouvy“) a rešerše `podklady/reserse/cz-informacni-povinnosti.md` už navrhuje pravidlo `cz_withdrawal_button`. Je tam i NV 66/2026 Sb. Úkol „stáhnout předpis“ proto není potřeba; místo něj úkol ověřit znění v podkladech a podle něj pravidlo napsat. Ustanovení posoudí právník.
10. **Vzory `site_signal` jsou závislé na jazyce.** `legal_withdrawal_function_missing` hledá „odstúpiť od zmluvy“, české pravidlo bude hledat „Odstoupit od smlouvy“. Na české verzi slovenského e-shopu (změna 7) by slovenský vzor nic nenašel a dal falešný nález. Zákon připouští i „obdobnou jednoznačnou formuláciu“, kterou vzor nepozná, proto zůstává `verify`. Návrh: vzor podle jazyka verze, ne jen podle jurisdikce. Rozhodnout spolu se změnou 7. Vzory jsou předepsané nápisy ze zákona, ne slovník pro klasifikaci, ale vztah k zásadě „žádné slovníky klíčových slov“ je potřeba potvrdit.
11. **Jazyk odkazů na zákon.** Slovenské odkazy dnes míchají češtinu a slovenštinu („ve znění zákona č. 310/2025 Z. z. (účinnosť …)“). Návrh: slovenské odkazy celé slovensky, české česky. Odkazy na předpisy EU (`jurisdiction: eu`) nemají „jazyk zákona“: návrh je zobrazit je v jazyce verdiktu (u SK verdiktu slovensky, u CZ česky), což znamená nové pole `ref_by_language` u odkazů EU. Potvrdit.
12. **Stav odkazu** (`status: "ověřit"`, „doplnit“) je dnes český text. Návrh: kódy `to_verify`, `to_complete` s texty v `_engine.yaml`.
13. **Otázky pro uživatele.** Návrh: každé pravidlo s `checkability: verify` (dnes 13 zapnutých) má aspoň jednu otázku; znění vychází z návrhu rozvoje 7 („Máte od výrobcu podklad (napr. protokol o skúške), alebo ho viete na požiadanie získať?“). Konkrétní znění u každého pravidla schválí uživatel.
14. **Hodnoty `effective_from`.** Vyplní se jen tam, kde je datum v dnešním odkazu na zákon nebo v podkladech: SK `eco`, `dur`, `legal_harmonized_notice_missing` 27. 9. 2026; `legal_withdrawal_function_missing` 19. 6. 2026; CZ tlačítko 1. 1. 2027. Ostatní bez data (platí). Potvrdit.
15. **Zpráva CLI ve slovenštině.** `MarkdownReportWriter` a `ReportFormat` mají vlastní české texty (nadpisy, názvy skupin). Návrh: `--lang sk` přeloží jen texty pravidel a nástroje; nadpisy zprávy CLI zůstanou české (CLI je interní nástroj). Potvrdit.
