# Delta for Rules-and-verdicts

## ADDED Requirements

### Requirement: Vyhodnocení pro víc jurisdikcí nad společnými odpověďmi Jevu
Systém MUST v jednom běhu vyhodnotit pravidla pro všechny zvolené jurisdikce (`ScanOptions.Jurisdictions`, `AnalyzeOptions.Jurisdictions`). Otázku se stejným id a stejnou definicí MUST položit Jevu pro větu jen jednou a odpověď MUST použít pro všechny jurisdikce. Otázky se stejným id a různou definicí v sadách různých jurisdikcí MUST jít v samostatných požadavcích a jejich odpovědi MUST být uložené odděleně podle klíče `{sada}:{id}`.

#### Scenario: Věta se ptá jednou pro obě země
- GIVEN e-shop s jurisdikcemi `sk` a `cz` a věta „Za recenziu s 5 hviezdičkami dostanete zľavu 10 %.“
- WHEN proběhne vyhodnocení modulu `ucp` (`jurisdictions: [sk, cz]`)
- THEN Jev dostane pro tuto větu jeden požadavek s otázkami `ucp`
- AND nález `ucp_review_reward_positive` má verdikt pro `sk` i pro `cz`

#### Scenario: Stejné id otázky s jiným zněním
- GIVEN právní odstavec a sady `legal_sk` a `legal_cz`, obě s otázkou `legal_adr` v jiném znění
- WHEN proběhne vyhodnocení pro `sk` a `cz`
- THEN Jev dostane pro odstavec dva požadavky, jeden se zněním ze sady `legal_sk` a jeden z `legal_cz`
- AND `Segment.Probabilities` obsahuje `legal_sk:legal_adr` i `legal_cz:legal_adr` s vlastními hodnotami

#### Scenario: Jedna země beze změny počtu volání
- GIVEN běh jen pro `sk` nad `Fixtures/site-sk`
- WHEN se porovná se stejným během před změnou
- THEN počet požadavků na Jev, klíče cache a nálezy jsou stejné

#### Scenario: Modul bez pravidel pro zvolenou zemi
- GIVEN jurisdikce `sk` a `cz` a modul `eco`, který má pravidla jen pro `sk`
- WHEN běh skončí
- THEN nálezy `eco` mají verdikt jen pro `sk`
- AND `ScanResult.JurisdictionCoverage` pro `cz` uvádí modul `eco` jako nespuštěný s kódem `no_rules_for_jurisdiction`

### Requirement: Verdikt nálezu po zemích
Systém MUST ke každému nálezu připojit verdikt pro každou jurisdikci, ve které pravidlo dalo nález. Verdikt MUST obsahovat jurisdikci, stav (`finding`, `upcoming`), pásmo, skóre, závažnost, `checkability`, odkazy na zákon (EU a dané jurisdikce), sadu pravidel s verzí, variantu vysvětlení a účinnost. Závažnost, `checkability` a pásma MUST jít přepsat pro jednotlivou jurisdikci v `jurisdiction_overrides`.

#### Scenario: Odkazy na zákon podle jurisdikce
- GIVEN pravidlo s odkazy pro `eu`, `sk` a `cz` a nález s verdikty `sk` a `cz`
- WHEN se nález vytvoří
- THEN verdikt `sk` nese odkazy `eu` a `sk`, verdikt `cz` odkazy `eu` a `cz`

#### Scenario: Jiná skupina nálezu v jiné zemi
- GIVEN pravidlo s `checkability: text` a `jurisdiction_overrides: {cz: {checkability: assess}}`
- WHEN věta splní podmínky pravidla v obou zemích
- THEN verdikt `sk` má `checkability` `text` a verdikt `cz` `assess`
- AND varianta vysvětlení verdiktu `cz` je `cz`, když pravidlo má text `explanation_by_jurisdiction.cz`

#### Scenario: Stejné pravidlo z různých sad je jeden nález
- GIVEN sady `legal_sk` a `legal_cz` s pravidlem `legal_adr_missing` a právní stránky, kde chybí informace podle obou sad
- WHEN proběhne vyhodnocení
- THEN vznikne jeden nález `legal_adr_missing` s verdiktem `sk` (sada `legal_sk`) a `cz` (sada `legal_cz`)

### Requirement: Řazení nálezů podle nejpřísnějšího verdiktu
Systém MUST určit u každého nálezu nejpřísnější verdikt podle pořadí: stav (`finding` před `upcoming`), `checkability` (`text`, `assess`, `verify`, `not_checkable`), závažnost (`high`, `medium`, `low`), pásmo (`high`, `review`), jurisdikce abecedně. Zpráva MUST řadit nálezy podle nejpřísnějšího verdiktu a souhrnné vlastnosti nálezu (`Severity`, `Checkability`, `Band`, `Score`) MUST odpovídat nejpřísnějšímu verdiktu. Nález, jehož všechny verdikty jsou `upcoming`, MUST NOT být ve zprávě mezi porušeními.

#### Scenario: Porušení v jedné zemi, posouzení v druhé
- GIVEN nález s verdiktem `sk` (`text`, `high`) a `cz` (`assess`, `high`)
- WHEN se nálezy seřadí
- THEN nejpřísnější je verdikt `sk` a nález je mezi „porušeními podle textu zákona“

#### Scenario: Shoda skupiny rozhodne závažnost
- GIVEN dva nálezy, oba s nejpřísnějším `checkability` `verify`, jeden `high`, druhý `medium`
- WHEN se seřadí
- THEN nález `high` je první

#### Scenario: Budoucí povinnost nezvyšuje přísnost
- GIVEN nález s verdiktem `cz` ve stavu `upcoming` (`text`, `high`) a `sk` ve stavu `finding` (`verify`, `medium`)
- WHEN se určí nejpřísnější verdikt
- THEN je to verdikt `sk`

### Requirement: Účinnost povinností po zemích
Systém MUST u pravidla podporovat datum účinnosti pro každou jurisdikci (`effective_from`) a MUST vyhodnotit pravidlo k datu `AsOf` (výchozí dnešní datum z `TimeProvider`, v CLI volba `--as-of`). Před účinností MUST verdikt mít stav `upcoming`, pásmo `review` a poznámku `effective_from` s datem; pravidlo MUST NOT být před účinností vynechané.

#### Scenario: Slovenské tlačítko odstoupení po účinnosti
- GIVEN pravidlo `legal_withdrawal_function_missing` s `effective_from: {sk: 2026-06-19}` a web bez tlačítka
- WHEN se vyhodnotí s `AsOf` = 1. 10. 2026
- THEN verdikt `sk` má stav `finding`

#### Scenario: České tlačítko před účinností
- GIVEN české pravidlo pro tlačítko „Odstoupit od smlouvy“ s `effective_from: {cz: 2027-01-01}` a web bez tlačítka
- WHEN se vyhodnotí s `AsOf` = 1. 10. 2026
- THEN verdikt `cz` má stav `upcoming`, pásmo `review` a poznámku `effective_from` s datem 1. 1. 2027
- AND s `AsOf` = 2. 1. 2027 má stejný verdikt stav `finding`

#### Scenario: Neplatné datum účinnosti v pravidle
- GIVEN pravidlo s `effective_from: {pl: 2027-01-01}` v sadě s `jurisdictions: [sk]`
- WHEN se pravidla načtou
- THEN `RuleValidationException` uvede soubor, pravidlo a jurisdikci mimo sadu
- AND nic se nestáhne

### Requirement: Povinnosti za celý web po zemích
Systém MUST v `ScanResult.SiteObligations` uvést pro každou zvolenou jurisdikci každé pravidlo s rozsahem `site_presence` nebo `site_signal` se stavem `met`, `missing`, `upcoming` nebo `not_checked`, datem účinnosti a URL, na kterých se znak našel. Stav `not_checked` MUST nést kód důvodu; povinnost, kterou nešlo ověřit, MUST NOT mít stav `met`.

#### Scenario: Povinnosti dvou zemí vedle sebe
- GIVEN e-shop s jurisdikcemi `sk` a `cz`, kde obchodní podmínky uvádějí ČOI, ale neuvádějí subjekt ARS podle slovenského zákona
- WHEN běh skončí
- THEN `SiteObligations` má pro `cz` `legal_adr_missing` ve stavu `met` a pro `sk` ve stavu `missing`

#### Scenario: Kontrola textu bez celého webu
- GIVEN `AnalyzeTextsAsync` s právním textem a jurisdikcí `sk`
- WHEN se vyhodnotí
- THEN pravidla `site_signal` mají stav `not_checked` s kódem `site_signals_not_evaluated`

#### Scenario: Právní stránky se nenačetly
- GIVEN e-shop, jehož právní stránky vykresluje JavaScript (`TextNotLoaded`)
- WHEN se vyhodnotí `site_presence`
- THEN povinnosti mají stav `missing` s pásmem `review` a poznámkou `legal_pages_not_loaded`, nikdy `met`

### Requirement: Kódy a parametry místo hotových vět
Systém MUST vracet v `ScanResult`, `AnalysisResult` a `Finding` jen kódy a parametry: poznámky k nálezu jako `FindingNote(Code, Params)`, upozornění běhu jako `ScanWarning(Code, Params)`, parametry textů v `Finding.Params`. `Finding` MUST NOT obsahovat název, vysvětlení ani doporučení pravidla. Každý kód MUST mít text v souboru `_engine.yaml` každého jazyka.

#### Scenario: Poznámka o nejbližším odstavci
- GIVEN chybějící informace, kde nejbližší odstavec má pravděpodobnost 0,42 a práh je 0,70
- WHEN vznikne nález
- THEN verdikt nese poznámku s kódem `presence_closest_paragraph` a parametry `probability` = 0,42 a `threshold` = 0,70
- AND výsledek neobsahuje českou větu „Nejbližší nalezený odstavec má pravděpodobnost …“

#### Scenario: Výsledek bez vět
- GIVEN běh nad `Fixtures/site-sk`
- WHEN test `NoSentencesInResultTests` serializuje `ScanResult`
- THEN všechny kódy poznámek a upozornění jsou z `EngineCodes`
- AND výsledek nemá pole `title`, `explanation` ani `recommendation`

#### Scenario: Neznámý kód
- GIVEN kód v `EngineCodes`, který chybí v `rules/texts/sk/_engine.yaml`
- WHEN běží test úplnosti
- THEN test selže s názvem kódu a souboru

### Requirement: Návrh opravy projde kontrolou ve všech jurisdikcích
Systém MUST zkontrolovat každý navržený text přepisu pravidly všech zvolených jurisdikcí. Změna MUST mít stav `StillFinding`, když v kterékoli jurisdikci zůstane verdikt `finding` s `checkability` `text` nebo `assess`; verdikty `verify` MUST přejít do `VerifyFindings` a verdikty `upcoming` MUST přidat poznámku a stav neměnit.

#### Scenario: Oprava vyhoví jen jedné zemi
- GIVEN návrh přepisu, po kterém pravidlo dá verdikt `finding` (`assess`) jen v `cz`
- WHEN proběhne kontrola přepisu s jurisdikcemi `sk` a `cz`
- THEN změna má stav `StillFinding` a `RemainingFindings` obsahuje nález s verdiktem `cz`

#### Scenario: Oprava vyhoví oběma zemím
- GIVEN návrh přepisu bez nálezu v `sk` i `cz`
- WHEN proběhne kontrola
- THEN změna má stav `Resolved` (nebo `WaitingForFacts`, když obsahuje „[doplňte: …]“)

#### Scenario: Zbude jen věc k ověření
- GIVEN návrh, po kterém zůstane jen verdikt `verify` (jmenovaná značka)
- WHEN proběhne kontrola
- THEN nález je ve `VerifyFindings` a stav změny se řídí ostatními nálezy

### Requirement: Pravidlo pro tlačítko odstoupení v Česku
Systém MUST v sadě `legal_cz` kontrolovat od 1. 1. 2027 tlačítko nebo obdobný ovládací prvek s nápisem „Odstoupit od smlouvy“ (§ 1830a odst. 2 OZ ve znění zákona č. 159/2026 Sb.) a údaj o možnosti odstoupit tlačítkem a o jeho umístění v informacích před uzavřením smlouvy (§ 1820 odst. 1 písm. i) OZ ve znění zákona č. 159/2026 Sb.). Odkazy MUST mít stav `to_verify` a MUST pocházet z textu předpisu v `podklady/predpisy-cz/cz-159-2026-novela-financni-sluzby-na-dalku-tlacitko-odstoupeni.txt`. Chybějící tlačítko MUST být nález `verify`, protože crawler nevidí účet zákazníka.

#### Scenario: Tlačítko na webu je
- GIVEN český e-shop s odkazem „Odstoupit od smlouvy“ v patičce a `AsOf` = 2. 1. 2027
- WHEN se vyhodnotí jurisdikce `cz`
- THEN povinnost má v `SiteObligations` stav `met` a URL stránek s odkazem

#### Scenario: Odkaz na poučení není tlačítko
- GIVEN český e-shop s odkazem „Jak odstoupit od smlouvy“ na stránku s poučením a `AsOf` = 2. 1. 2027
- WHEN se vyhodnotí jurisdikce `cz`
- THEN nález `legal_withdrawal_function_missing` zůstane, protože nápis nezačíná „Odstoupit od smlouvy“

#### Scenario: Tlačítko chybí
- GIVEN český e-shop bez takového odkazu a `AsOf` = 2. 1. 2027
- WHEN se vyhodnotí jurisdikce `cz`
- THEN nález `legal_withdrawal_function_missing` má verdikt `cz` s `checkability` `verify` a poznámkou `signal_cart_not_downloaded`

#### Scenario: Slovenský e-shop s jinou jurisdikcí
- GIVEN e-shop jen s jurisdikcí `sk`
- WHEN se vyhodnotí
- THEN české pravidlo se nespustí a `SiteObligations` ho neuvádí

### Requirement: Popis verze sady pravidel pro uložení
Systém MUST pro každou sadu pravidel vrátit `RuleSetDescriptor` s modulem, verzí, jurisdikcemi, jazykem otázek, `QuestionSetHash`, definicí bez textů, texty po jazycích, `SourceHash` a seznamem úplných jazyků. Změna textů MUST změnit `SourceHash` a MUST NOT změnit `QuestionSetHash` ani verzi sady.

#### Scenario: Oprava překlepu v textu
- GIVEN sada `eco` a oprava jednoho slova ve `rules/texts/sk/eco.yaml`
- WHEN se načte katalog
- THEN `SourceHash` sady `eco` je jiný a `QuestionSetHash` i `Version` stejné
- AND cache odpovědí Jevu zůstane platná

#### Scenario: Změna otázky pro Jev
- GIVEN změna znění otázky `eco_generic` v `rules/eco.yaml` bez zvýšení `version`
- WHEN se načte katalog
- THEN `RuleValidationException` upozorní, že se změnil `QuestionSetHash` a `version` zůstala stejná (porovnání s otiskem zapsaným pro tuto verzi v `rules/question-set-hashes.json`)
- AND tabulka `checks.rule_sets` tak nikdy nedostane dvě různé definice se stejnou dvojicí (`module`, `version`)
