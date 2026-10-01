# Návrh otázek pro uživatele (úkol 4.7, K rozhodnutí 13)

Pravidla s `checkability: verify` dávají nález, o kterém rozhodují fakta mimo web. Otázka pro uživatele se ptá na fakt, ne na výklad zákona. Vychází z návrhu rozvoje 7: „Máte od výrobcu podklad (napr. protokol o skúške), alebo ho viete na požiadanie získať?“

Stav: **návrh, čeká na schválení uživatelem.** Do pravidel (`user_questions`) a textů (`rules/texts/cs/<sada>.yaml`) se zapíše až po schválení. Potom se texty přeloží do slovenštiny (K rozhodnutí 8). Do té doby `eshopguard rules check-texts` vypisuje pravidla bez otázky jako „čekají na schválení znění“.

| Sada | Pravidlo | Kód otázky | Znění (cs) |
| --- | --- | --- | --- |
| eco | `eco_climate_claim_unsupported` | `evidence_available` | Máte doklad, že tvrzení o dopadu na klima vychází ze snížení emisí během životního cyklu produktu, a ne z kompenzací (například výpočet uhlíkové stopy ověřený nezávislou stranou)? |
| eco | `eco_company_climate_claim` | `evidence_available` | Máte doklad, že tvrzení o klimatickém dopadu firmy nestojí na kompenzacích emisí a že ho ověřila nezávislá strana? |
| eco | `eco_label_unrecognized` | `label_certified` | Je značka udělená v certifikačním systému s nezávislou kontrolou, nebo ji zavedl orgán veřejné moci? Máte k tomu doklad (certifikát, pravidla systému)? |
| eco | `eco_future_claim` | `plan_available` | Máte k závazku podrobný a realistický plán s měřitelnými cíli a termíny, který pravidelně ověřuje nezávislý odborník a který je veřejně dostupný? |
| dur | `dur_lifetime_claim` | `evidence_available` | Máte od výrobce podklad k uvedené životnosti (například protokol o zkoušce nebo normu), nebo ho umíte na požádání získat? |
| dur | `dur_repairable_claim` | `repair_possible` | Dá se výrobek skutečně opravit (náhradní díly, návod nebo servis jsou dostupné)? Máte k tomu podklad od výrobce? |
| dur | `dur_consumable_early` | `interval_documented` | Odpovídá doporučený interval výměny spotřebního materiálu technické nutnosti podle podkladů výrobce? |
| dur | `dur_non_original_damage` | `evidence_available` | Máte od výrobce doklad, že neoriginální díly nebo příslušenství výrobek skutečně poškodí nebo omezí jeho funkci? |
| dur | `dur_update_necessary` | `update_necessary_documented` | Je zmíněná aktualizace softwaru podle podkladů výrobce skutečně nutná pro zachování funkce výrobku? |
| ucp | `ucp_review_reward_any` | `reward_disclosed` | Dostávají zákazníci odměnu za každou recenzi bez ohledu na hodnocení, a je to u recenzí uvedené? |
| ucp | `ucp_reviews_verified_claim` | `verification_process` | Jak ověřujete, že recenzi napsal zákazník, který produkt koupil (například hodnotit smí jen zákazník s dokončenou objednávkou)? Je to u recenzí popsané? |
| legal_sk | `legal_harmonized_notice_missing` | `shown_in_checkout` | Zobrazujete harmonizované oznámení o zákonné odpovědnosti za vady v košíku nebo v pokladně (tam nástroj nevidí)? |
| legal_sk, legal_cz | `legal_withdrawal_function_missing` | `available_in_account` | Je tlačítko nebo funkce pro odstoupení od smlouvy v zákaznickém účtu nebo v potvrzení objednávky (tam nástroj nevidí)? Kde přesně? |

Poznámky:
- Kód otázky je stejný v každé sadě, která pravidlo implementuje (`legal_withdrawal_function_missing` v SK i CZ). Odpověď se tak dá použít pro obě země.
- U `eco_company_climate_claim` a `eco_climate_claim_unsupported` je jedna otázka na doklad. Zda kompenzace „stojí mimo hodnotový řetězec“, posoudí člověk podle dokladu; nástroj se na výklad neptá.
