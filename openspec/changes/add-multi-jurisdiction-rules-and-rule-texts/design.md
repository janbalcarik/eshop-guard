# Design: Pravidla pro víc zemí, verdikty po zemích a texty pravidel

Cesty jsou po přejmenování ze změny 1 a po rozdělení na kroky ze změny 5 (kořen `eshop-guard/`).

## Technical Approach

### 1. Soubory pravidel a textů

Pravidlo v `rules/<sada>.yaml` (sada = název souboru bez přípony) po změně:

```yaml
version: "legal-sk-2026-09-26-draft4"   # verze sady otázek; mění se jen při změně otázek (je v klíči cache Jevu)
module: legal
applies_to: legal_paragraph
jurisdictions: [sk]
rules:
  - id: legal_withdrawal_function_missing
    scope: site_signal
    code_checks:
      - {type: site_pattern_required, where: links, pattern: '(?i)\bodst[uú]pi[tť]\s+od\s+zmluvy\b|…'}
    severity: high
    checkability: verify
    effective_from: {sk: 2026-06-19}        # nové: účinnost po jurisdikcích
    jurisdiction_overrides: {}               # nové: {cz: {severity, checkability, bands}} když se země liší
    user_questions: [function_available]     # nové: kódy otázek pro uživatele (texty v rules/texts)
    legal_refs:
      - {jurisdiction: sk, ref: "§ 20a zákona č. 108/2024 Z. z. v znení zákona č. 311/2025 Z. z. (účinnosť od 19. 6. 2026)", status: to_verify}
```

Texty v `rules/texts/<jazyk>/<sada>.yaml`:

```yaml
locale: sk
rule_set: legal_sk
source_version: "legal-sk-2026-09-26-draft4"   # verze sady, ke které texty patří (kontrola, ne klíč cache)
review:
  translated_by: "…"
  reviewed_by: "…"          # povinné; bez něj jazyk není úplný
  reviewed_at: 2026-10-…
rules:
  legal_withdrawal_function_missing:
    title: "Chýba funkcia „odstúpiť od zmluvy tu“"
    explanation: "…"
    explanation_by_jurisdiction: {}          # klíče = jurisdikce, kde se vysvětlení liší
    recommendation: "…"
    user_questions:
      function_available: "…"   # znění otázky schválí uživatel (proposal, K rozhodnutí 13)
```

- `YamlRuleSetProvider` čte dál jen `rules/*.yaml` v kořeni složky (`Directory.GetFiles(..., "*.yaml")` bez podsložek), takže `rules/texts/` se za pravidla nepovažuje.
- `YamlRuleTextProvider` načte `rules/texts/<jazyk>/*.yaml` pro jazyky z `RulesOptions.Locales` (výchozí `[cs, sk]`), `_engine.yaml` (kódy poznámek, upozornění, stavů odkazů, vestavěného nálezu) a `_labels.yaml` (poznámky ke značkám, klíčované id poznámky; `config/labels.yaml` dostane u každé `label_notes` položky `id` a ztratí `note`).
- Zástupné symboly: `{nazev}` s názvy z `Params`; formát čísel a dat podle jazyka (`cs-CZ`, `sk-SK`).

### 2. Model nálezu a verdiktu

```csharp
public sealed class Finding
{
    public required string RuleId { get; init; }          // logické pravidlo
    public required string Module { get; init; }
    public required string Scope { get; init; }           // segment | site
    public string? Text { get; init; }                    // kopie textu z webu (ne text pravidla)
    public string ContextBefore { get; init; } = "";
    public string ContextAfter { get; init; } = "";
    public IReadOnlyList<SegmentSource> Sources { get; init; } = [];
    public IReadOnlyList<string> Urls { get; init; } = [];
    public bool Boilerplate { get; init; }
    public string? SegmentHash { get; init; }
    public long? TextFingerprint { get; init; }           // SentenceFingerprint ze změny 5
    public IReadOnlyList<JurisdictionVerdict> Verdicts { get; init; } = [];
    public JurisdictionVerdict Strictest => …;            // podle VerdictOrder
    // Kvůli kompatibilitě odvozené z nejpřísnějšího verdiktu:
    public string Severity => Strictest.Severity;
    public string Checkability => Strictest.Checkability;
    public FindingBand Band => Strictest.Band;
    public double Score => Strictest.Score;
    public IReadOnlyDictionary<string, double> QuestionProbs { get; init; }   // klíč QuestionKey
    public IReadOnlyDictionary<string, object?> Params { get; init; }         // parametry pro texty
}

public sealed class JurisdictionVerdict
{
    public required string Jurisdiction { get; init; }   // sk | cz
    public VerdictStatus Status { get; init; }           // Finding | Upcoming
    public FindingBand Band { get; init; }
    public double Score { get; init; }
    public required string Severity { get; init; }
    public required string Checkability { get; init; }
    public IReadOnlyList<LegalReference> LegalRefs { get; init; } = [];   // eu + tato jurisdikce
    public required string RuleSet { get; init; }        // název sady (legal_sk)
    public required string RuleSetVersion { get; init; }
    public string ExplanationVariant { get; init; } = "default";          // nebo kód jurisdikce
    public DateOnly? EffectiveFrom { get; init; }
    public IReadOnlyList<FindingNote> Notes { get; init; } = [];
}

public sealed record FindingNote(string Code, IReadOnlyDictionary<string, object?> Params);
public sealed record ScanWarning(string Code, IReadOnlyDictionary<string, object?> Params);
```

- `Title`, `Explanation`, `Recommendation` a textové `Notes` z `Finding` zmizí. `ScanResult.Warnings` bude `IReadOnlyList<ScanWarning>`.
- `VerdictOrder.Compare` (K rozhodnutí 3): `Status` (`Finding` < `Upcoming`, budoucí povinnost nikdy nezvýší přísnost), `checkability` (`text` < `assess` < `verify` < `not_checkable`), závažnost (`high` < `medium` < `low`), pásmo (`High` < `Review`), pak jurisdikce abecedně. Podle `Strictest` řadí nálezy zpráva (a později API); seznam ve výsledku zůstává v pořadí vyhodnocení (viz Odchylky).

### 3. Vyhodnocení pro víc jurisdikcí

- `ScanOptions.Jurisdictions` a `AnalyzeOptions.Jurisdictions` (`IReadOnlyList<string>`). `Country` zůstane jako zkratka pro jednu zemi (`Jurisdictions = [Country]`), aby CLI a testy fungovaly.
- `RuleSetSelector.Select(catalog, modules, jurisdictions, warnings)` (z `EshopGuardService.SelectRuleSets`): vybere sady, které mají průnik s jurisdikcemi; pro každou dvojici (sada, jurisdikce) vznikne `RuleSetBinding`. Upozornění zůstanou (jako kódy `module_other_jurisdiction`, `module_no_rule_set`, `module_disabled`) a k tomu `JurisdictionCoverage`.
- **Klíč otázky** `QuestionKey = "{sada}:{id}"` (např. `legal_sk:legal_adr`). `Segment.Probabilities` je klíčované `QuestionKey`. Do Jevu jde dál jen `id` otázky.
- **Skládání požadavků** v `SegmentEvaluator.Group`: položky jedné věty se seskupí do jednoho požadavku; když dvě sady mají stejné `id` otázky s jinou definicí (porovnání kanonického JSON otázky), dostane druhá sada samostatný požadavek. Při jedné zemi se nic nemění. Klíč cache se počítá po sadách jako dnes, takže `LegacyKey` zůstane stejný.
- `RuleEngine.Evaluate` projde vazby (sada, jurisdikce):
  - segmentová pravidla jedné sady pro víc zemí (`ucp: [sk, cz]`) se vyhodnotí jednou nad společnými pravděpodobnostmi a dostanou verdikt pro každou zemi (odkazy, varianta vysvětlení a přepsání podle `jurisdiction_overrides`);
  - pravidla `site_presence` a `site_signal` se vyhodnotí pro každou sadu zvlášť (otázky a vzory se liší);
  - výsledky se sloučí do jednoho nálezu podle (`RuleId`, `SegmentHash`) u segmentů a podle `RuleId` u celého webu. U celého webu se do sloučeného nálezu dostanou jen jurisdikce, kde pravidlo dalo nález; jurisdikce, kde je informace přítomná, jde do `SiteObligations` jako `met`.
  - `MergeRepeatedTexts` a `MergeSameTexts` slučují dál podle (`RuleId`, normalizovaný text) a verdikty spojují podle jurisdikce (vyšší skóre vyhrává).
- **Účinnost:** `RuleEngineInput.AsOf` (`DateOnly`, z `TimeProvider` v `RulesStep`, v CLI volba `--as-of`). Když `AsOf < effective_from[j]`, verdikt pro `j` má `Status = Upcoming`, pásmo `Review` a poznámku `effective_from` s datem.
- **Povinnosti za celý web:** `ScanResult.SiteObligations`: pro každou jurisdikci a každé pravidlo s rozsahem `site_presence` nebo `site_signal` stav `met`, `missing`, `upcoming` nebo `not_checked` (důvod: `evaluation_skipped`, `site_signals_not_evaluated` u `AnalyzeTextsAsync`, `no_legal_pages`, `legal_pages_not_loaded`), `effective_from`, URL důkazu.
- **Pokrytí:** `ScanResult.JurisdictionCoverage`: pro každou zvolenou jurisdikci moduly, které běžely, a moduly, které ne, s kódem důvodu (`no_rules_for_jurisdiction`, `rule_set_disabled`).

### 4. Kódy poznámek a upozornění

Dnešní české věty se nahradí kódy (texty v `_engine.yaml`):

| Kde dnes | Kód | Parametry |
|---|---|---|
| `RuleEngine.EvaluateSitePresence` | `presence_closest_paragraph` | `probability`, `threshold` |
| | `presence_pattern_missing` | `pattern` |
| | `presence_found_but_pattern_missing` | `probability`, `pattern` |
| | `presence_paragraphs_not_evaluated` | `count` |
| `RuleEngine.PdfNote` | `unread_pdf_documents` | `urls`, `more` |
| `RuleEngine.NotLoadedNote` | `legal_pages_not_loaded`, `pages_not_loaded` | `urls`, `more` |
| `RuleEngine.EvaluateSiteSignal` | `signal_not_found`, `signal_cart_not_downloaded`, `signal_found_on` | `pages`, `count` |
| `RuleEngine.ClaimListHolds` (modul `lr`, vypnutý) | `claim_list_item`, `claim_list_other_category`, `claim_list_no_category`, `claim_list_not_in_list` | `category`, `feature`, `basis`, `since`, `note` |
| `LabelMatcher.NotesFor` | `label_note` | `label_id` |
| `RuleEngine.MissingLegalPages` | pravidlo `legal_pages_missing` v `_engine.yaml` | – |
| Účinnost | `effective_from` | `date` |
| `EshopGuardService` (dnes), `RulesStep`, `ScanResultAssembler` | `no_legal_pages`, `text_not_loaded`, `mostly_script_rendered`, `profiles_not_created`, `sieve_unevaluated`, `evaluation_not_confirmed`, `jev_errors`, `module_other_jurisdiction`, `module_no_rule_set`, `module_disabled` | podle dnešních vět |
| `DiscoveryStep`, `FetchStep` | `robots_home_disallowed`, `robots_unreachable`, `sitemap_unreadable`, `sitemap_invalid`, `sitemap_too_many`, `ssrf_blocked` | `url`, `reason`, `max` |
| `ProfileStep` | `profile_failed_fatal`, `profile_failed`, `profile_no_fit` | `url`, `reason`, `pages` |
| `OpenAiProfileModel.UnavailableReason` | `model_mock`, `model_missing_key` | – |
| `LegalReference.Status` | `to_verify`, `to_complete` | – |

Test `NoSentencesInResultTests` serializuje `ScanResult` a ověří, že poznámky a upozornění mají jen kódy z `_engine.yaml` a že výsledek neobsahuje pole `title`, `explanation` ani `recommendation`.

### 5. Skládání textů

`RuleTextRenderer.Render(Finding, JurisdictionVerdict, string locale)` → `RenderedFinding(Title, Explanation, Recommendation, Notes[], LegalRefs[], UserQuestions[])`:
- `Explanation` = `explanation_by_jurisdiction[verdict.Jurisdiction]`, jinak `explanation` (dnešní `RuleDefinition.ExplanationFor`).
- Odkazy na zákon se neformátují: text je v jazyce zákona (K rozhodnutí 11 u EU).
- Chybějící text je chyba načtení, ne prázdný řetězec (fail-closed).

Okraje, které text potřebují:
- `MarkdownReportWriter`, `FindingsCsvWriter`, `FindingsJsonWriter`: jazyk z `--lang` (výchozí `cs`), pro každý nález verdikty všech jurisdikcí. `findings.json` zapisuje `FindingDocument` = kódy, parametry, verdikty a vykreslené texty v jazyce běhu.
- `ScanOutputReader` čte `FindingDocument` (kódy) a pro přepis texty vykreslí znovu.
- `RewritePrompt` (K rozhodnutí 5).

### 6. Přepis ve všech jurisdikcích

`RewriteInput.Jurisdictions` (místo `Country`). `PageRewriter.CheckAsync` volá `AnalyzeTextsAsync` se všemi jurisdikcemi. Změna má stav `StillFinding`, když kterýkoli verdikt nového nálezu v jejím textu je `Finding` s `checkability` `text` nebo `assess`; verdikty `verify` jdou do `VerifyFindings` jako dnes; `Upcoming` stav nemění, ale přidá poznámku.

### 7. Nové české pravidlo pro tlačítko odstoupení

Podle `podklady/reserse/cz-informacni-povinnosti.md` (oddíl `cz_withdrawal_button`) a textu zákona v `podklady/predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt`:
- v `rules/legal_cz.yaml` pravidlo `legal_withdrawal_function_missing` (stejné logické id jako SK, K rozhodnutí 1), `scope: site_signal`, `where: links`, vzor pro nápis „Odstoupit od smlouvy“ (§ 1830a odst. 2 OZ ve znění zákona č. 159/2026 Sb.), `checkability: verify` (tlačítko může být jen v účtu zákazníka, kam crawler nevidí), `severity: high`, `effective_from: {cz: 2027-01-01}`, odkaz se stavem `to_verify`;
- v `rules/legal_cz.yaml` pravidlo `legal_withdrawal_button_info_missing`, `scope: site_presence`, otázky `legal_withdrawal_online_option` a `legal_withdrawal_button_location` z rešerše (§ 1820 odst. 1 písm. i) OZ ve znění zákona č. 159/2026 Sb.), `effective_from: {cz: 2027-01-01}`;
- nové otázky změní sadu `legal_cz`, proto nová `version`; cache českých právních odstavců se zaplatí znovu (úkol s odhadem ceny).

### 8. Popis verze sady pro databázi

`RuleSetDescriptor` (z `RuleCatalog.Describe()`): `Module`, `Version`, `File`, `Jurisdictions`, `QuestionLanguage`, `QuestionSetHash` (stejný výpočet jako `JevCacheKey.QuestionSetHash`), `Definition` (JSON sady bez textů), `Texts` (JSON po jazycích), `SourceHash` (SHA-256 souboru pravidel a jeho textů), `Enabled`, `CompleteLocales`. Změna 8 ho zapíše do `checks.rule_sets`.

Pojistka verze: `rules/question-set-hashes.json` drží pro každou dvojici (`module`, `version`) otisk `QuestionSetHash`. `RuleValidator` odmítne sadu, jejíž otázky se změnily bez nové `version` (jinak by `checks.rule_sets` dostala dvě definice se stejnou verzí). Nová verze se do souboru zapíše příkazem `eshopguard rules check-texts --update-hashes`.

### 9. Další trhy bez změny kódu (Německo, Polsko, Maďarsko …)

Uživatel 1. 10. 2026: nástroj bude později i pro Německo, Polsko, Maďarsko a další země. Tato změna obsahově řeší jen SK a CZ, ale nic v kódu nesmí znát seznam zemí ani jazyků. Další trh má znamenat jen nové soubory dat.

- **Jurisdikce jsou data.** Kód jurisdikce je text ze souboru pravidel (`jurisdictions: [de]`), ne výčet v kódu. Seznam známých jurisdikcí je v `config/jurisdictions.yaml`:

  ```yaml
  sk: {law_language: sk}
  cz: {law_language: cs}
  # de: {law_language: de}
  ```

  `law_language` je jazyk předpisů té země. Odkazy EU se u verdiktu té země zobrazí v tomto jazyce (`ref_by_language`), pokud překlad existuje; jinak výchozí `ref`.
  - `RuleValidator` odmítne sadu s jurisdikcí, která v souboru chybí (překlep `cs` místo `cz` se nikdy tiše nevyhodnotí jako jiná země).
  - Totéž platí pro klíče `effective_from`, `jurisdiction_overrides` a `explanation_by_jurisdiction`.
- **Volby CLI se ověřují proti katalogu.** `--country` a `--jurisdictions` přijmou každou jurisdikci z `config/jurisdictions.yaml`, která má aspoň jednu zapnutou sadu. Dnešní pevné `cz`/`sk` v `SettingsValidation` zmizí. Chyba vypíše seznam známých jurisdikcí.
- **Jazyky textů jsou složky.** `YamlRuleTextProvider` načte každou složku `rules/texts/<jazyk>/`. `RulesOptions.Locales` je nepovinné omezení, prázdné znamená všechny složky. Povinné jazyky jsou v `RulesOptions.RequiredLocales` (`settings.yaml`, `rules.required_locales`, výchozí `[cs]`). `--lang` přijme jen jazyk z `CompleteLocales`.
- **Formát čísel a dat podle jazyka bez seznamu v kódu.** `_engine.yaml` jazyka má v hlavičce `culture` (např. `de-DE`), výchozí je kód jazyka. `RuleTextRenderer` použije `CultureInfo.GetCultureInfo(culture)`.
- **Názvy zemí a stavů jsou texty.** Název jurisdikce ve zprávě (`jurisdiction.sk`: „Slovensko“) je kód v `_engine.yaml` každého jazyka. Test úplnosti chce název každé jurisdikce z `config/jurisdictions.yaml` v každém jazyce.
- **Otázky pro Jev.** Výchozí jazyk otázek je angličtina (`text_en`), takže nový trh nepotřebuje překlad otázek. `text_cs` je varianta v jazyce země (u slovenských sad slovensky); přejmenování na obecný název by změnilo definici otázek a cache, proto se nedělá.
- **Seznam zákonných požadavků** (`config/legal_requirements.yaml`) je už dnes po jurisdikcích (`LegalRequirementList.For(country)`). Nová země s prázdným seznamem znamená, že pravidla `claim_list_match` pro ni nic nenajdou; `RuleValidator` proto dovolí taková pravidla jen v sadách, pro jejichž jurisdikce seznam existuje.
- **Vzory `site_signal`** jsou nápisy předepsané zákonem dané země (K rozhodnutí 10). Pro novou zemi patří do její sady. Výběr vzoru podle jazyka verze webu řeší změna 7.
- **Co přidání trhu obnáší** (bez kódu):
  1. řádek v `config/jurisdictions.yaml`;
  2. sady `rules/<modul>_<země>.yaml`, nebo jurisdikce navíc u sady, která platí beze změny (např. `ucp` podle směrnice);
  3. složka `rules/texts/<jazyk>/` se všemi soubory, se zkontrolovaným překladem (`review`);
  4. otisk nových verzí v `rules/question-set-hashes.json` (`rules check-texts --update-hashes`);
  5. podklady a rešerše v `podklady/`.
- **Test** `Rules/NewMarketTests.cs` to dokládá: v dočasné složce vznikne vymyšlená jurisdikce `xx` s jazykem `xx` (sada, texty, řádek v `jurisdictions.yaml`). Kontrola textu pro ni dá nález a vykreslí ho v `xx`, a to bez jakékoli změny kódu.

## Architecture Decisions

1. **Texty mimo pravidla, otázky pro Jev v pravidlech.** Otázky určují odpovědi a cenu (klíč cache), texty jen vysvětlují. Oddělení znamená, že oprava překlepu v textu nikdy neznehodnotí cache ani verzi sady.
2. **Jeden logický nález, verdikty po zemích.** Obchodník opravuje jednu větu jednou; to, že porušuje pravidla dvou zemí, je vlastnost nálezu, ne dva úkoly. Odpovídá `findings.verdicts` v databázovém návrhu.
3. **Kolize id otázek se řeší rozdělením požadavku, ne přejmenováním.** Přejmenování by změnilo klíče cache (placené). Rozdělení je při jedné zemi neaktivní.
4. **Kódy a parametry v knihovně, věty na okraji.** Stejný nález se ukáže česky, slovensky i v protokolu PDF bez dalšího volání a bez ukládání textu do databáze (databázový návrh: „vysvětlení se skládá při čtení“).
5. **Účinnost vyhodnocovat, ne vypínat.** Pravidlo před účinností běží a dává `upcoming`: obchodník se dozví dopředu a po datu se nález sám změní na platný.
6. **Úplnost jazyka jako podmínka zapnutí.** Jazyk bez úplných a zkontrolovaných textů nejde zapnout (`RuleCatalog.CompleteLocales`), takže se nikdy neukáže prázdné nebo neověřené vysvětlení.

## Data Flow

```
rules/*.yaml ──YamlRuleSetProvider──► RuleCatalog (sady, otázky, logika, odkazy, účinnost)
rules/texts/<jazyk>/*.yaml ──YamlRuleTextProvider──► RuleTexts (+ CompleteLocales)
                                   │
ScanOptions.Jurisdictions [sk, cz] ▼
RuleSetSelector ─► RuleSetBinding[] (sada × jurisdikce) + JurisdictionCoverage
                                   │
EvaluateStep: věta × sady ─► požadavky Jevu (společné; rozdělené při kolizi id) ─► Segment.Probabilities[QuestionKey]
                                   │
RulesStep / RuleEngine: vazba (sada, jurisdikce) ─► dílčí výsledky ─► sloučení podle (RuleId, SegmentHash)
                                   ─► Finding.Verdicts[], Strictest, SiteObligations, FindingNote/ScanWarning (kódy)
                                   │
okraje: IReportWriter (--lang), RewritePrompt (jazyk obsahu), později API ─► RuleTextRenderer ─► texty
                                   │
RuleCatalog.Describe() ─► RuleSetDescriptor ─► (změna 8) checks.rule_sets
```

## File Changes

### Nové soubory

- `rules/texts/cs/eco.yaml`, `dur.yaml`, `ucp.yaml`, `legal_sk.yaml`, `legal_cz.yaml`, `_engine.yaml`, `_labels.yaml`.
- `rules/texts/sk/eco.yaml`, `dur.yaml`, `ucp.yaml`, `legal_sk.yaml`, `legal_cz.yaml`, `_engine.yaml`, `_labels.yaml`.
- `src/EshopGuard.Core/Rules/Texts/IRuleTextProvider.cs`, `YamlRuleTextProvider.cs`, `RuleTexts.cs` (texty pravidel, nástroje a značek po jazycích), `RuleTextRenderer.cs`, `RenderedFinding.cs`, `RuleTextValidator.cs` (úplnost, zástupné symboly, `review`).
- `src/EshopGuard.Core/Rules/RuleSetSelector.cs` (z `EshopGuardService.SelectRuleSets`), `RuleSetBinding.cs`, `QuestionKey.cs`, `VerdictOrder.cs`, `RuleSetDescriptor.cs`.
- `src/EshopGuard.Core/Models/JurisdictionVerdict.cs` (`VerdictStatus`), `FindingNote.cs`, `ScanWarning.cs`, `SiteObligation.cs`, `JurisdictionCoverage.cs`.
- `src/EshopGuard.Core/Rules/EngineCodes.cs`: konstanty všech kódů poznámek a upozornění (test úplnosti je porovná s `_engine.yaml`).
- `src/EshopGuard.Cli/Commands/RulesExtractTextsCommand.cs` (`rules extract-texts`, jednorázový přesun) a `RulesTextsCheckCommand.cs` (`rules check-texts`: úplnost a stav kontroly po jazycích).
- `src/EshopGuard.Core/Report/FindingDocument.cs` (tvar `findings.json`).

### Měněné soubory

- `rules/eco.yaml`, `dur.yaml`, `ucp.yaml`, `legal_sk.yaml`, `legal_cz.yaml`: bez `title`, `explanation`, `explanation_by_jurisdiction`, `recommendation`; `effective_from`, `jurisdiction_overrides`, `user_questions`; `status` odkazů jako kód; slovenské odkazy slovensky (K rozhodnutí 11). `version` se nemění kromě `legal_cz.yaml` (nové pravidlo a otázky).
- `rules/lr.yaml`, `rules/ucp_parked.yaml`: zůstávají s texty v sobě, dokud nebudou zapnuté (K rozhodnutí 6); validátor jim nedovolí `enabled: true` bez souborů textů.
- `rules/CHANGELOG.md`: přesun textů, nová verze `legal_cz`.
- `config/labels.yaml`: `label_notes[].id` místo `note`.
- `src/EshopGuard.Core/Rules/RuleModels.cs`: `RuleDefinition` bez textových polí, s `EffectiveFrom`, `JurisdictionOverrides`, `UserQuestions`; `LegalReference.RefByLanguage` (jen EU, K rozhodnutí 11); `RuleCatalog.Texts`, `CompleteLocales`; `LabelNote.Id`.
- `src/EshopGuard.Core/Rules/YamlRuleSetProvider.cs`: načtení textů přes `IRuleTextProvider`, `RuleValidationException` při chybějícím souboru textů zapnuté sady v povinném jazyce.
- `src/EshopGuard.Core/Rules/RuleValidator.cs`: kolize id pravidel a otázek mezi sadami stejné jurisdikce zůstává chybou; mezi různými jurisdikcemi se povolí, jen když mají sady stejný `module` a `applies_to`; kontrola `effective_from` a `jurisdiction_overrides` (jen jurisdikce sady), `user_questions` jen u existujících textů.
- `src/EshopGuard.Core/Rules/SegmentEvaluator.cs`: `QuestionKey`, rozdělení požadavku při kolizi.
- `src/EshopGuard.Core/Rules/PageSieve.cs`: beze změny logiky (síto je společné pro všechny jurisdikce; otázky síta jsou po modulech).
- `src/EshopGuard.Core/Rules/RuleEngine.cs`: vazby (sada, jurisdikce), verdikty, sloučení, účinnost, `SiteObligations`, kódy poznámek; `RuleEngineInput.Jurisdictions`, `AsOf`.
- `src/EshopGuard.Core/Rules/LabelMatcher.cs`: `NotesFor` vrací id poznámek.
- `src/EshopGuard.Core/Models/Finding.cs`, `Models/ScanResult.cs` (`Jurisdictions`, `Warnings` jako `ScanWarning`, `SiteObligations`, `JurisdictionCoverage`), `Models/AnalysisModels.cs` (`AnalysisResult` obdobně).
- `src/EshopGuard.Core/Options/ScanOptions.cs`, `Options/AnalyzeOptions.cs`: `Jurisdictions`, `AsOf`.
- `src/EshopGuard.Core/Options/EshopGuardOptions.cs`: `RulesOptions.TextsDirectory` (výchozí `rules/texts`), `RulesOptions.Locales` (`[cs, sk]`), `RulesOptions.RequiredLocales` (`[cs]` do dokončení překladu, pak `[cs, sk]`).
- `src/EshopGuard.Core/Pipeline/RulesStep.cs`, `EvaluateStep.cs`, `InMemoryPipelineRunner.cs`, `ScanResultAssembler.cs` (ze změny 5): jurisdikce, kódy upozornění, `TimeProvider`.
- `src/EshopGuard.Core/Fix/RewriteModels.cs` (`RewriteInput.Jurisdictions`), `Fix/PageRewriter.cs` (`CheckAsync` ve všech jurisdikcích), `Fix/RewritePrompt.cs` (texty přes `RuleTextRenderer`, nová `Version`), `Fix/ScanOutputReader.cs` (čte `FindingDocument`).
- `src/EshopGuard.Core/Report/MarkdownReportWriter.cs`, `FindingsCsvWriter.cs`, `FindingsJsonWriter.cs`, `ReportFormat.cs`: texty přes `RuleTextRenderer`, verdikty po zemích, `SiteObligations`, `JurisdictionCoverage`.
- `src/EshopGuard.Core/ServiceCollectionExtensions.cs`: `IRuleTextProvider`, `TimeProvider.System` přes `TryAdd`.
- `src/EshopGuard.Cli/Commands/ScanCommand.cs`, `CheckTextCommand.cs`, `RewriteCommand.cs`: `--jurisdictions`, `--lang`, `--as-of`; `Commands/SettingsValidation.cs`.
- `README.md`: více zemí, jazyky textů, postup překladu.

### Testy (`tests/EshopGuard.Core.Tests/`)

- `Rules/RuleTextCompletenessTests.cs`: úplnost klíčů všech jazyků, zástupné symboly, kódy `EngineCodes` × `_engine.yaml`, `review`.
- `Rules/RuleTextExtractionTests.cs`: vykreslené české texty = dnešní texty z pravidel (referenční `Baselines/rule-texts-cs.json` zachycený před přesunem).
- `Rules/MultiJurisdictionTests.cs`: společné odpovědi, rozdělení požadavku, verdikty, sloučení, pokrytí.
- `Rules/VerdictOrderTests.cs`, `Rules/EffectiveDateTests.cs`, `Rules/SiteObligationTests.cs`, `Rules/CzWithdrawalButtonTests.cs`.
- `Rules/NoSentencesInResultTests.cs`.
- `RewriteMultiJurisdictionTests.cs`.
- Úpravy `RulesTests.cs`, `AnalyzeTextsTests.cs`, `ScanFixtureTests.cs`, `ScanSlovakFixtureTests.cs`, `LegalRequirementTests.cs`, `RewriteTests.cs` na kódy a verdikty; `Fixtures/expected_findings*.json` beze změny očekávaných nálezů.

## Odchylky při implementaci (1. 10. 2026)

1. **Pořadí verdiktů.** Stav (`Finding` před `Upcoming`) je první kritérium, ne čtvrté. Vyžaduje to scénář specifikace „Budoucí povinnost nezvyšuje přísnost“ a K rozhodnutí 3 („upcoming vždy za platnými“).
2. **Pořadí nálezů ve výsledku.**
   - `ScanResult.Findings` zůstává v pořadí vyhodnocení: sady, pravidla v pořadí souboru, segmenty.
   - Podle nejpřísnějšího verdiktu řadí zpráva (skupina, závažnost, skóre). Totéž udělá API.
   - Důvod: `findings.csv` a `findings.json` pro jednu zemi zůstaly shodné s referenčními výstupy změny 5.
3. **Pravděpodobnosti nálezu.** `Segment.Probabilities` je klíčované `{sada}:{id}`. Verdikt nese `QuestionProbs` klíčované id otázek své sady (sada je ve verdiktu, klíč je tedy jednoznačný). `Finding.QuestionProbs` patří nejpřísnějšímu verdiktu, takže zpráva vypisuje id otázek jako dřív.
4. **Původní texty a záložní jazyk.**
   - Každá zapnutá sada má právě jeden soubor textů s `review.original: true`: texty napsané autory pravidel a přesunuté beze změny. Ten je použitelný bez další kontroly.
   - Překlad je použitelný, jen když je úplný, zkontrolovaný (`reviewed_by`, `reviewed_at`, ne `machine_draft`) a ke stejné `source_version`.
   - Chybí-li překlad sady, ukážou se texty v původním jazyce sady. Proto je `legal_sk` v české zprávě dál slovensky, stejně jako před změnou. `RenderedFinding.Locale` říká, v jakém jazyce text je.
   - `RuleCatalog.CompleteLocales` jsou jazyky bez jediné takové náhrady. Ty smí nabídnout aplikace.
   - `RuleTexts.ToolLocales` jsou jazyky s úplnými texty nástroje (`_engine.yaml`, `_labels.yaml`). V nich jde napsat zprávu CLI (`--lang`).
   - `RulesOptions.RequiredLocales` (`[cs]`) vyžaduje úplné texty nástroje. Bez nich se pravidla nenačtou.
   - Bez tohoto rozlišení by čeština nebyla „úplná“ až do lidského překladu `legal_sk` (úkol 4.5) a výchozí běh by nešel spustit.
5. **Varianta vysvětlení** se určuje z původních textů sady (`RuleSet.ExplanationVariants`). Engine texty nečte, verdikt nese jen kód varianty.
6. **České tlačítko: tři pravidla místo dvou.** Pravidlo `site_presence` hodnotí jednu otázku. Proto jsou `legal_withdrawal_button_info_missing` (otázka `legal_withdrawal_online_option`) a `legal_withdrawal_button_location_missing` (otázka `legal_withdrawal_button_location`) zvlášť. Vzor tlačítka `(?im)^\s*odstoupit\s+od\s+smlouvy\b` musí být na začátku textu odkazu, takže odkaz „Jak odstoupit od smlouvy“ na stránku s poučením ho nesplní.
7. **Zpráva:** nález, jehož všechny verdikty platí až později, je ve skupině „Platí později“, ne mezi porušeními. `findings.csv` má sloupec `verdicts` jen při víc zemích, takže běh pro jednu zemi má stejné sloupce jako dřív.
8. **Zadání přepisu.**
   - Texty jsou v jazyce obsahu e-shopu: `RewriteInput.ContentLanguage`, výchozí jazyk předpisů první jurisdikce. Chybějící překlad se nahradí původním textem.
   - Nález s víc verdikty přidá řádky dalších zemí.
   - Pro jednu zemi je zadání beze změny, proto `config/rewrite.yaml` nedostal novou verzi a uložené přepisy platí. Nová verze přijde se slovenskými překlady.
9. **Otázky pro uživatele** (K rozhodnutí 13) zatím nejsou povinné. Mechanismus je hotový: `user_questions` v pravidle, texty v souboru textů, kontrola úplnosti. Znění čeká na schválení (`otazky-pro-uzivatele-navrh.md`). `rules check-texts` vypisuje pravidla `verify` bez otázky.
10. **Kódy navíc proti tabulce v oddílu 4:**
    - `ssrf_blocked_urls`, `pages_not_processed`, `no_response`, `evaluation_not_confirmed_texts`;
    - části vět `list_more`, `claim_list_since`, `claim_list_remark` (parametrem poznámky může být další poznámka nebo null, takže knihovna neskládá ani kousky vět);
    - důvody `legal_texts_not_given` a `site_not_crawled`.
11. **`RuleSetDescriptor`** nese `QuestionSetHash` pro zadaný model a jazyk otázek (stejný výpočet jako klíč cache) a navíc `QuestionsHash` (otisk samotných otázek z `rules/question-set-hashes.json`). `SourceHash` se počítá z kanonického JSON definice a textů, takže komentáře v YAML ho nemění.
12. **Testy mají pevné datum** 1. 10. 2026 (`TestServices`). Bez něj by se české scénáře 1. 1. 2027 samy změnily. Referenční výstupy českých scénářů (`site`, `site-limits`) se po přidání `legal_cz` draft4 přegenerovaly. Rozdíl byl zkontrolován: nová pravidla, verze sady, sloupce nových otázek a klíče cache českých odstavců. Slovenské `report.md` a CSV jsou beze změny.
13. **Odkazy EU v jazyce verdiktu** (K rozhodnutí 11): pole `ref_by_language` a jazyk předpisů země (`law_language`) jsou hotové. Překlady odkazů EU zatím nejsou (překladatelská práce, skupina 4), takže se ukazuje původní české znění.
14. **Vzory `site_signal` podle jazyka verze webu** (K rozhodnutí 10) zůstávají na změnu 7.
