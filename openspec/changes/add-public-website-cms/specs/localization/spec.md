# Delta for Localization

## ADDED Requirements

### Requirement: Texty rozhraní: zprávy po jazycích přes next-intl
Systém MUST brát všechny texty rozhraní aplikace a pevné texty webu mimo CMS ze souborů zpráv `web/messages/sk.json` a `web/messages/cs.json` ve formátu ICU přes next-intl. Komponenty v `src/app/(app)`, `src/app/(site)` a `src/components` MUST NOT obsahovat pevný text pro uživatele (pravidlo `react/jsx-no-literals`). Jazyk zpráv MUST určit:
- na webu podle vydání (`sk-SK` → `sk`, `cs-CZ` → `cs`);
- v aplikaci podle `users.locale`, jinak podle cookie `eg_ui_locale`, jinak podle `Accept-Language`, jinak `sk`.

#### Scenario: Jazyk aplikace po přihlášení
- GIVEN uživatel má `users.locale = cs` a cookie `eg_ui_locale=sk` z přihlašovací stránky
- WHEN otevře `/app`
- THEN aplikace je česky a cookie `eg_ui_locale` se nastaví na `cs`
- AND `<html lang="cs">`

#### Scenario: Pevný text v komponentě
- GIVEN vývojář napíše do `src/components/fixes/FixTabs.tsx` text `Na riešenie` přímo do JSX
- WHEN běží `pnpm --dir web lint`
- THEN lint selže na pravidle `react/jsx-no-literals` s cestou k souboru
- AND sestavení v CI neprojde

#### Scenario: Neznámý jazyk prohlížeče
- GIVEN nepřihlášený návštěvník bez cookie posílá `Accept-Language: de-DE`
- WHEN otevře `/app/login`
- THEN rozhraní je slovensky (výchozí jazyk)
- AND přepínač jazyka nabízí Slovenčinu a Češtinu

### Requirement: Texty rozhraní: množná čísla ve formátu ICU
Systém MUST tvořit každý text s počtem přes ICU `plural` s kategoriemi `one`, `few` a `other` pro slovenštinu i češtinu, a `many`, kde parametr může být desetinný. Text MUST NOT skládat tvar slova v kódu.

#### Scenario: Tři tvary ve slovenštině
- GIVEN zpráva `fixes.chips.violation` = `{count, plural, one {# porušenie} few {# porušenia} other {# porušení}}`
- WHEN se zformátuje pro `count` 1, 3 a 14
- THEN výsledky jsou „1 porušenie“, „3 porušenia“ a „14 porušení“
- AND česká zpráva dá „1 porušení“, „3 porušení“ a „14 porušení“ podle textu v `cs.json`

#### Scenario: Chybějící kategorie
- GIVEN zpráva v `cs.json` má jen `one` a `other`
- WHEN běží `tests/i18n/plural.test.ts`
- THEN test selže s klíčem a chybějící kategorií `few`
- AND požadované kategorie se berou z `Intl.PluralRules(locale).resolvedOptions().pluralCategories`, ne z pevného seznamu

### Requirement: Texty rozhraní: čísla, data a měny přes Intl
Systém MUST formátovat čísla, procenta, data, časy a měny přes `Intl` podle jazyka rozhraní (`formats.ts`):
- oddělovač tisíců nezlomitelnou mezerou;
- datum `1. 10. 2026`;
- měnu z dat API bez desetinných míst u celých částek (`29 €`, `690 Kč`, `29,90 €`);
- časové pásmo `Europe/Bratislava` stejné na serveru i v prohlížeči.

Kód MUST NOT skládat formát čísla nebo data ručně.

#### Scenario: Ukázky formátů
- GIVEN jazyk `sk`
- WHEN se zformátuje `1460`, datum `2026-10-01T06:19:00Z`, částka `29` v `EUR` a `29.9` v `EUR`
- THEN výsledky jsou `1 460` (s U+00A0), `1. 10. 2026`, `8:19`, `29 €` a `29,90 €`
- AND v jazyce `cs` je částka `690` v `CZK` zobrazena jako `690 Kč`

#### Scenario: Stejný čas na serveru a v prohlížeči
- GIVEN server běží v UTC a prohlížeč v pásmu `America/New_York`
- WHEN se vykreslí „kontrola 30. 9. 2026 o 8:19“
- THEN serverové i klientské vykreslení ukáže `8:19`
- AND React nehlásí rozdíl při hydrataci

#### Scenario: Měna se neodvozuje z jazyka
- GIVEN uživatel má rozhraní v češtině a tenant platí v EUR
- WHEN se zobrazí částka faktury
- THEN měna je EUR podle dat API (`29 €`), ne Kč podle jazyka
- AND formát čísla je český

### Requirement: Texty rozhraní: test úplnosti překladů
Systém MUST mít test, který pro každou dvojici jazyků (`sk`, `cs`) ověří:
- stejnou sadu klíčů;
- stejné argumenty ICU v každé zprávě;
- platnou syntaxi ICU;
- žádnou prázdnou zprávu;
- úplné kategorie množného čísla.

Jazyk, kterému chybí jakýkoli klíč, MUST NOT jít zapnout (`ref.locales.enabled`, `web/src/i18n/editions.ts`).

#### Scenario: Chybějící český klíč
- GIVEN `sk.json` obsahuje `billing.invoices.zip` a `cs.json` ne
- WHEN běží `pnpm --dir web i18n:check`
- THEN test selže s výpisem `cs: chybí billing.invoices.zip`
- AND CI zastaví sestavení

#### Scenario: Jiný argument ve zprávě
- GIVEN `sk.json` má `overview.subtitle` s argumenty `{shop}`, `{date}`, `{count}` a `cs.json` místo `{count}` používá `{total}`
- WHEN běží test úplnosti
- THEN test selže s klíčem a rozdílem argumentů
- AND zpráva s neplatnou syntaxí ICU (neuzavřená závorka) test také shodí

#### Scenario: Nový jazyk bez úplných textů
- GIVEN někdo přidá `messages/hu.json` se 40 % klíčů a v `editions.ts` nastaví jazyk jako zapnutý
- WHEN běží test úplnosti
- THEN test selže, protože zapnutý jazyk nemá úplné texty
- AND jazyk ve stavu vypnutém test povolí jako rozpracovaný

### Requirement: Texty rozhraní: volba jazyka rozhraní
Systém MUST umožnit:
- před přihlášením zvolit jazyk rozhraní přepínačem na 2a (uložení do cookie `eg_ui_locale`);
- po přihlášení položkou „Jazyk“ v menu účtu (uložení `users.locale` přes API).

Výchozí jazyk aplikace MUST odpovídat vydání webu, ze kterého uživatel přišel (`?from=sk` → `sk`, `?from=cz` → `cs`). Jazyk rozhraní MUST NOT měnit místa prodeje, jazyk obsahu e-shopu ani měnu tenanta.

#### Scenario: Příchod z českého webu
- GIVEN nepřihlášený návštěvník klikne na „Vyskúšať zadarmo“ ve vydání `cz`
- WHEN se otevře `/app/login?from=cz`
- THEN přihlášení je česky a cookie `eg_ui_locale=cs`
- AND nový účet vznikne s `users.locale = cs`

#### Scenario: Čech spravuje slovenský e-shop
- GIVEN uživatel má rozhraní v češtině a e-shop bylinkovo.sk má místo prodeje Slovensko
- WHEN otevře opravu stránky
- THEN popisky a vysvětlení jsou česky, návrh opravy je slovensky (jazyk obsahu e-shopu) a odkaz na zákon je slovensky (jazyk zákona)
- AND změna jazyka rozhraní nezmění nabídku ceny ani místa prodeje

### Requirement: Texty rozhraní: chyby a stavy z API jako kódy
Systém MUST skládat texty chyb, stavů běhů, druhů upozornění a stavů dokladů a faktur z kódů a parametrů, které vrací API, přes klíče `errors.<code>`, `run.<code>`, `notifications.<kind>` a další v jazyce uživatele. Každý kód chyby uvedený ve snímku OpenAPI MUST mít text v obou jazycích. Neznámý kód MUST zobrazit obecný text s kódem, nikdy prázdné místo ani anglickou zprávu serveru.

#### Scenario: Kód bez textu
- GIVEN snímek OpenAPI obsahuje kód chyby `billing.quote_expired` a `cs.json` nemá `errors.billing.quote_expired`
- WHEN běží `tests/i18n/error-codes.test.ts`
- THEN test selže s chybějícím klíčem a jazykem
- AND kódy mimo OpenAPI test nevyžaduje

#### Scenario: Neznámý kód za běhu
- GIVEN API vrátí kód `shops.unexpected_state`, který frontend nezná
- WHEN se chyba zobrazí
- THEN uživatel vidí obecný text v jazyce rozhraní s kódem `shops.unexpected_state` a `traceId`
- AND text `detail` z ProblemDetails (anglicky ze serveru) se nezobrazí

### Requirement: Texty rozhraní: texty pravidel v jazyce uživatele
Systém MUST zobrazovat názvy, vysvětlení, doporučení a otázky nálezů z katalogu textů pravidel pro jazyk uživatele (změna 6) složené přes ICU s parametry nálezu. Odkazy na zákon MUST zobrazit tak, jak je vrátí API, v jazyce zákona. Frontend MUST NOT vymýšlet ani překládat ustanovení. Chybějící text pro jazyk MUST zobrazit viditelnou náhradu s `ruleId`, ne prázdné místo.

#### Scenario: Vysvětlení v češtině, zákon slovensky
- GIVEN nález pravidla `eco.generic_claim` u slovenského e-shopu a uživatel s rozhraním `cs`
- WHEN se zobrazí panel „Proč změnit“
- THEN vysvětlení je z českého textu pravidla s doplněnými parametry
- AND odkaz zní „Zákon č. 108/2024 Z. z., príloha č. 1 bod 6“ beze změny

#### Scenario: Chybí český text pravidla
- GIVEN katalog pro `cs` nemá text pravidla `dur.new_rule`
- WHEN se nález zobrazí
- THEN místo vysvětlení je `dur.new_rule` s upozorněním, že text pravidla chybí
- AND do konzole a do hlášení chyb se zapíše `rule_text_missing` s `ruleId` a jazykem
