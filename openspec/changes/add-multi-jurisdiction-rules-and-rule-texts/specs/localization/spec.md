# Delta for Localization

## ADDED Requirements

### Requirement: Texty pravidel: oddělené jazykové soubory
Systém MUST načítat názvy, vysvětlení, vysvětlení pro jurisdikci, doporučení a znění otázek pro uživatele ze souborů `rules/texts/<jazyk>/<sada>.yaml` a texty nástroje (poznámky, upozornění, vestavěné nálezy, poznámky ke značkám) z `rules/texts/<jazyk>/_engine.yaml` a `_labels.yaml`. Soubory pravidel `rules/<sada>.yaml` zapnutých sad MUST NOT obsahovat pole `title`, `explanation`, `explanation_by_jurisdiction` ani `recommendation`.

#### Scenario: Načtení textů obou jazyků
- GIVEN `rules/texts/cs/eco.yaml` a `rules/texts/sk/eco.yaml` s texty všech pravidel sady `eco`
- WHEN se načte katalog pravidel
- THEN `RuleTexts` vrátí pro `eco_generic_claim` název, vysvětlení a doporučení v `cs` i `sk`

#### Scenario: Chybí soubor textů zapnuté sady
- GIVEN zapnutá sada `dur` a chybějící `rules/texts/cs/dur.yaml`, přičemž `cs` je v `RulesOptions.RequiredLocales`
- WHEN se načte katalog
- THEN `RuleValidationException` uvede chybějící soubor a nic se nestáhne

#### Scenario: Text zůstal v souboru pravidel
- GIVEN `rules/ucp.yaml` s polem `title` u zapnutého pravidla
- WHEN se načte katalog
- THEN `RuleValidationException` uvede soubor a pravidlo, které má text na špatném místě

#### Scenario: Složka textů není sada pravidel
- GIVEN soubory v `rules/texts/sk/`
- WHEN `YamlRuleSetProvider` hledá sady pravidel v `rules/`
- THEN soubory z podsložky `texts` nenačte jako sady

### Requirement: Texty pravidel: úplnost klíčů všech jazyků
Systém MUST ověřit, že každý jazyk z `RulesOptions.Locales` má pro každé pravidlo zapnuté sady název, vysvětlení, doporučení, vysvětlení pro každou jurisdikci, pro kterou ho má kterýkoli jiný jazyk, a text každé otázky z `user_questions`, a že každý kód z `EngineCodes` má text v `_engine.yaml`. Zástupné symboly `{…}` MUST být ve všech jazycích stejné. Jazyk, který kontrolou neprojde, MUST NOT být v `RuleCatalog.CompleteLocales` a MUST NOT jít zapnout.

#### Scenario: Chybějící klíč ve slovenštině
- GIVEN `rules/texts/sk/ucp.yaml` bez `recommendation` u `ucp_reviews_only_positive`
- WHEN běží `RuleTextCompletenessTests`
- THEN test selže s názvem souboru, pravidla a klíče
- AND `RuleCatalog.CompleteLocales` neobsahuje `sk`

#### Scenario: Různé zástupné symboly
- GIVEN text kódu `presence_closest_paragraph` s `{probability}` a `{threshold}` v `cs` a jen `{probability}` v `sk`
- WHEN běží kontrola úplnosti
- THEN kontrola selže a uvede kód a chybějící symbol `{threshold}`

#### Scenario: Úplné jazyky
- GIVEN úplné a zkontrolované texty `cs` i `sk`
- WHEN se načte katalog
- THEN `RuleCatalog.CompleteLocales` je `[cs, sk]` a `eshopguard rules check-texts` vypíše oba jazyky jako úplné

### Requirement: Texty pravidel: skládání podle pravidla, jurisdikce a jazyka
Systém MUST skládat text nálezu podle trojice pravidlo × jurisdikce verdiktu × jazyk uživatele: vysvětlení pro jurisdikci verdiktu, když existuje, jinak výchozí vysvětlení. Parametry nálezu MUST se dosadit do zástupných symbolů a čísla a data MUST se formátovat podle jazyka. Chybějící text MUST být chyba, ne prázdný řetězec.

#### Scenario: Slovenský text pro český verdikt
- GIVEN nález `eco_generic_claim` s verdiktem `cz` a jazyk `sk`
- WHEN `RuleTextRenderer` složí text
- THEN vysvětlení je slovenský text z `rules/texts/sk/eco.yaml`, `explanation_by_jurisdiction.cz`

#### Scenario: Čeština a jedna země jako dnes
- GIVEN běh nad `Fixtures/site` s jurisdikcí `cz` a jazykem `cs`
- WHEN se zapíše `report.md`
- THEN je shodný s referenčním výstupem ze změny 5 (po normalizaci časů)

#### Scenario: Formát čísla v poznámce
- GIVEN poznámka `presence_closest_paragraph` s `probability` = 0,4213
- WHEN se složí v `cs` a `sk`
- THEN obě znění zobrazí „0,42“

### Requirement: Texty pravidel: odkazy na zákon v jazyce zákona
Systém MUST zobrazit odkazy na národní předpisy v jazyce předpisu bez ohledu na jazyk uživatele: slovenské předpisy slovensky, české česky. Stav odkazu (`to_verify`, `to_complete`) MUST být kód s textem v jazyce uživatele.

#### Scenario: Český uživatel a slovenský verdikt
- GIVEN uživatel s jazykem `cs` a nález s verdiktem `sk`
- WHEN se složí text
- THEN odkaz „§ 5 ods. 1 písm. q) zákona č. 108/2024 Z. z.“ zůstane slovensky
- AND stav odkazu se zobrazí česky z `rules/texts/cs/_engine.yaml`

#### Scenario: Slovenský uživatel a český verdikt
- GIVEN uživatel s jazykem `sk` a nález s verdiktem `cz`
- WHEN se složí text
- THEN odkaz „§ 1820 odst. 1 písm. i) zákona č. 89/2012 Sb.“ zůstane česky a nepřekládá se

#### Scenario: Odkaz bez jurisdikce verdiktu se nezobrazí
- GIVEN pravidlo s odkazy `eu`, `sk` a `cz` a nález jen s verdiktem `sk`
- WHEN se složí text
- THEN se zobrazí odkazy `eu` a `sk`, odkaz `cz` ne

### Requirement: Texty pravidel: překlad jen po lidské kontrole
Systém MUST považovat jazyk za úplný jen tehdy, když každý jeho soubor textů je původní (`review.original: true`, texty napsané autory pravidel) nebo má v `review` vyplněné `reviewed_by` a `reviewed_at`. Soubor s `review.machine_draft: true` MUST NOT jazyk zpřístupnit. Texty sady, jejíž překlad v jazyce chybí nebo není zkontrolovaný, MUST se zobrazit v původním jazyce sady a výsledek MUST uvést, v jakém jazyce text je; zpráva CLI MUST jít napsat jen v jazyce s úplnými a zkontrolovanými texty nástroje (`_engine.yaml`, `_labels.yaml`).

#### Scenario: Neověřený návrh překladu
- GIVEN `rules/texts/sk/eco.yaml` s `review.machine_draft: true` a bez `reviewed_by`
- WHEN se načte katalog
- THEN `sk` není v `CompleteLocales` a `rules check-texts` vypíše soubor jako „čeká na kontrolu“

#### Scenario: Zkontrolovaný překlad
- GIVEN všechny soubory `rules/texts/sk/` s `reviewed_by` a `reviewed_at`
- WHEN se načte katalog
- THEN `sk` je v `CompleteLocales`

#### Scenario: Sada bez překladu v české zprávě
- GIVEN sada `legal_sk` s původními texty ve slovenštině a bez zkontrolovaného českého překladu
- WHEN se složí česká zpráva
- THEN texty `legal_sk` jsou slovensky jako před změnou a `RenderedFinding.Locale` je `sk`
- AND `cs` není v `CompleteLocales`

### Requirement: Texty pravidel: otázky pro uživatele
Systém MUST mít ke každému pravidlu s `checkability: verify` aspoň jednu otázku pro uživatele (`user_questions`) s textem v každém jazyce a MUST vracet u nálezu kódy otázek, ne jejich znění.

#### Scenario: Pravidlo k ověření bez otázky
- GIVEN zapnuté pravidlo `dur_lifetime_claim` (`verify`) bez `user_questions`
- WHEN se načte katalog
- THEN `RuleValidationException` uvede pravidlo bez otázky

#### Scenario: Otázka v jazyce uživatele
- GIVEN nález `eco_label_unrecognized` s otázkou `evidence_available`
- WHEN `RuleTextRenderer` složí text pro jazyk `sk`
- THEN `RenderedFinding.UserQuestions` obsahuje slovenské znění z `rules/texts/sk/eco.yaml`

### Requirement: Texty pravidel: zpráva CLI v jazyce běhu
Systém MUST v CLI volbou `--lang` (`cs`, `sk`, výchozí `cs`) určit jazyk textů pravidel a nástroje ve `report.md`, `findings.csv` a `findings.json`. `findings.json` MUST obsahovat kódy, parametry a verdikty a vedle nich texty v jazyce běhu, aby ho `ScanOutputReader` přečetl bez ztráty.

#### Scenario: Slovenská zpráva
- GIVEN `eshopguard scan http://localhost:8000 --allow-private-network --mock --lang sk` nad `Fixtures/site-sk`
- WHEN se zapíše `report.md`
- THEN názvy, vysvětlení a doporučení nálezů jsou slovensky

#### Scenario: Přepis ze staršího výstupu
- GIVEN složka skenu s `findings.json` v novém tvaru
- WHEN `eshopguard rewrite <složka>` načte nálezy
- THEN `ScanOutputReader` obnoví kódy, parametry a verdikty a texty pro zadání přepisu složí znovu

#### Scenario: Jazyk bez úplných textů
- GIVEN `--lang sk` a `sk` není v `CompleteLocales`
- WHEN se spustí `scan`
- THEN příkaz skončí chybou „Texty pravidel pro jazyk sk nejsou úplné nebo zkontrolované“ ještě před stahováním
