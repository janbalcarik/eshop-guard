# Tasks

## 1. Datový základ a testovací data

- [ ] 1.1 Migrace `Data/Migrations/*_FindingsApi`:
  - `fixes.protocols.status` a `error_code`;
  - jedinečný index (`tenant_id`, `number`);
  - `fixes.fix_proposals.recheck_result jsonb`;
  - funkce `checks.strictness_rank(verdicts jsonb)` a index `ix_findings_shop_status_rank`;
  - `CREATE EXTENSION IF NOT EXISTS pg_trgm` (jako `eshopguard_owner`) a trigramové indexy `content.pages (shop_id, title)`, `checks.findings (shop_id, text)`;
  - index `checks.questions (tenant_id, code, status)`.
- [ ] 1.2 `Data/Migrations/Sql/notify_triggers.sql`: spouštěče `pg_notify('eg_run', …)` na `checks.run_events` a `checks.runs` a `pg_notify('eg_shop', …)` na `fixes.fix_proposals`, `fixes.fix_groups`, `fixes.publications`, `checks.questions` (jen ID). Test `NotifyTriggersTests`: obsah oznámení neobsahuje text.
- [ ] 1.3 `tests/EshopGuard.Api.Tests/Findings/BylinkovoSeed.cs` podle návrhu UI:
  - 28 položek, 43 nálezů (14 / 23 / 6);
  - verze sk a cs, 7 skupin (včetně „Všetky naše produkty balíme ekologicky.“ na 38 stránkách, 2 k jednotlivému řešení);
  - otázky (Vodnár × 4, COSMOS, košík);
  - doklady z obrazovky Doklady.

  Data ve tvaru ze změny 8.

## 2. Stavy, verdikty a texty pravidel

- [ ] 2.1 `Application/Findings/FindingStatus.cs` a `FindingStatusMachine.cs` (tabulka z AD 1, výsledek `Allowed` / `NotAllowed(from, to)`).
- [ ] 2.2 Test `FindingStatusMachineTests`: všech 81 dvojic stavů proti tabulce; nepovolené dávají `finding.transition_not_allowed`.
- [ ] 2.3 `VerdictStrictness` (převzít ze změny 6, jinak `Application/Findings/VerdictStrictness.cs` s pořadím z K rozhodnutí 2). Test `VerdictStrictnessTests`: `sk text/high` přísnější než `cz assess/high`, shoda podle závažnosti, podle pásma.
- [ ] 2.4 `Application/Findings/RuleTextCatalog.cs`:
  - texty z `checks.rule_sets.texts`, náhradní jazyk z `ref.locales.fallback_code`;
  - `409 catalog.locale_incomplete`;
  - `IMemoryCache` po (`rule_set_id`, jazyk).
- [ ] 2.5 `GET /api/catalog/rule-texts` v `Api/Endpoints/CatalogEndpoints.cs` s `ETag` a `Cache-Control: private, max-age=3600`.
- [ ] 2.6 Test `RuleTextCatalogTests`:
  - `cs` a `sk` vrátí vysvětlení po jurisdikcích a „Čo pomôže“;
  - neúplný jazyk → `409`;
  - shodný `If-None-Match` → `304`.

## 3. Přehled, stránky, nálezy, hledání, export

- [ ] 3.1 `Application/Findings/PageTabs.cs` a `PageWorkQueryService.cs`:
  - definice záložek a virtuální položky `site_template` a `site_obligations` (AD 3);
  - filtr `language`, `q`;
  - řazení podle `strictness_rank`, počtu a názvu;
  - stránkování kurzorem.
- [ ] 3.2 `Application/Findings/FindingQueryService.cs`:
  - filtry `checkability` (nejpřísnější), `status`, `module`, `language`, `jurisdiction`, `q`;
  - `groupBy=page`;
  - záložky `FindingTabsDto` („Opravené“ = `published` + `resolved`);
  - detail s historií z `ops.audit_log`.
- [ ] 3.3 `Application/Findings/OverviewService.cs`: poslední běh, počty, záložky, 5 stránek k řešení, 3 rychlé odpovědi, `badges` (opravy, doklady `expiring` + `awaiting_answer`), `monitoring = null`.
- [ ] 3.4 `Application/Findings/SearchService.cs` (`q` aspoň 2 znaky, trigramy, nejvýš 10 + 10) a `FindingsCsvExporter.cs`:
  - proudově, UTF-8 BOM, `;`;
  - názvy pravidel v jazyce uživatele;
  - strop `Findings:ExportMaxRows`;
  - audit `findings.exported`.
- [ ] 3.5 Koncové body v `OverviewEndpoints.cs` a `FindingEndpoints.cs`: `GET S/overview`, `GET S/pages`, `GET S/pages/tabs`, `GET S/findings`, `GET S/findings/tabs`, `GET S/findings/{findingId}`, `GET S/findings/export.csv`, `GET S/search`.
- [ ] 3.6 Test `PageTabsTests` nad `BylinkovoSeed`:
  - 24 / 12 / 12 / 4 / 28;
  - stránka s otázkou jen v `needs_answer`;
  - filtr `cs` ponechá položky za celý web.
- [ ] 3.7 Test `PagesAndFindingsTests`:
  - řazení podle nejpřísnějšího;
  - stabilní kurzor při 120 nálezech;
  - `checkability=fatal` → `400`;
  - záložky nálezů 14 / 23 / 6 / 9.
- [ ] 3.8 Test `CsvExportTests` (hlavička, BOM, názvy pravidel česky pro uživatele `cs`, verdikty po zemích) a `OverviewTests`.

## 4. Oprava stránky a návrhy

- [ ] 4.1 `Application/Fixes/ExtractContextReader.cs`: čtení bloků z `page_versions.extract_blob_key` přes `IBlobStore` (gzip JSON), okolní věty a počet bloků beze změny.
- [ ] 4.2 `Application/Fixes/PageReviewService.cs`:
  - změny v pořadí textu, varianty, údaje, odkaz na skupinu, otázky, `recheck`;
  - pozice ve frontě záložky (předchozí, další);
  - zdroj textu;
  - `publish.available` a `reasonCode` (AD 4).
- [ ] 4.3 `GET S/pages/{pageId}/review` a `GET S/proposals/{proposalId}` v `PageReviewEndpoints.cs` a `ProposalEndpoints.cs`. `Api/Http/ETagExtensions.cs` (`ETag` / `If-Match` ↔ `xmin`).
- [ ] 4.4 `Application/Fixes/FixProposalService.cs`:
  - `SelectAlternativeAsync`;
  - `EditTextAsync` (prázdný, beze změny, délka, `status = edited`, `recheck_status = pending`, úloha `fix.recheck`);
  - `FillPlaceholdersAsync`;
  - `AcceptAsync` (kontrola `ok` ve všech aktivních zemích, údaje, automat, paměť rozhodnutí);
  - `RejectAsync`, `UnacceptAsync`.
- [ ] 4.5 Koncové body `PUT S/proposals/{id}/alternative`, `PUT …/text`, `PUT …/placeholders`, `POST …/accept`, `POST …/reject`, `POST …/unaccept`.
- [ ] 4.6 `Jobs/Fixes/FixRecheckHandler.cs`:
  - `AnalyzeTextsAsync` (změna 5) s kontextem, aktivními zeměmi a moduly nálezu;
  - zápis `recheck_status` a `recheck_result` jen při shodném `textHash`;
  - P0, `concurrency_key = recheck:{proposalId}`.
- [ ] 4.7 `Application/Fixes/DecisionMemoryWriter.cs` (`replace` / `keep` / `keep_with_evidence` / `remove`, `superseded_at` při vrácení) a test `DecisionMemoryWriterTests`.
- [ ] 4.8 Test `PageReviewTests`:
  - stránka „Zubná pasta + bambusová kefka“: 5 změn, skupina 38, zdroj Shoptet 2429, pozice 1 z 24;
  - kontext ze souboru;
  - e-shop `web` → `publish.available = false`.
- [ ] 4.9 Test `ProposalTests`:
  - úprava → `202` a úloha P0;
  - `MockJevClient` bez nálezu → `ok` a přijetí projde;
  - `still_finding` v `cz` → `409 proposal.recheck_failed`;
  - nevyplněný údaj → `409`;
  - souběžná úprava → `409 concurrency.conflict`;
  - zastaralý výsledek kontroly se nezapíše.
- [ ] 4.10 Test `FixRecheckHandlerTests` (`MockJevClient`, dvě jurisdikce, změněný text mezi založením a doběhem úlohy).

## 5. Otázky a odpovědi

- [ ] 5.1 `Application/Fixes/AnswerPropagation.cs`:
  - dotaz na shodné otevřené otázky (`code` + `segment_hash`) v tenantovi;
  - u `scope = site` jen v e-shopu;
  - výpočet `appliesTo`.

  Test `AnswerPropagationTests` (4 sviečky, dva e-shopy tenanta, otázka za celý web, druhý tenant nedotčen).
- [ ] 5.2 `Application/Fixes/QuestionService.AnswerAsync`:
  - „Áno“ → doklad `answer` + vazby + `kept_with_evidence` + paměť;
  - „Nie“ → varianta `answer_no`, text z pravidla u otázky za celý web, nebo úloha `fix.generate_for_answer`;
  - `question.answer_locked`;
  - audit.
- [ ] 5.3 `Application/Fixes/GenerationBudget.cs`: kbelík `fixes:generate:tenant:{id}` (`Fixes:DailyGenerationsPerTenant`), nad stropem `429 budget.daily_limit_reached` bez uložení odpovědi.
- [ ] 5.4 `Jobs/Fixes/FixGenerateForAnswerHandler.cs`:
  - přepis přes `IRewriteClient` (změna 5) s odpovědí jako faktem;
  - kontrola Jevem;
  - uložení varianty `answer_no`;
  - nález `open → proposed` jen při kontrole `ok`, jinak zůstane `open` s upozorněním.
- [ ] 5.5 Koncové body `GET S/questions`, `POST S/questions/{questionId}/answer` v `QuestionEndpoints.cs`.
- [ ] 5.6 Test `QuestionTests`:
  - Vodnár „Nie“ → 4 nálezy `proposed`;
  - COSMOS „Áno“ → doklad a `kept_with_evidence`;
  - rozpočet vyčerpaný → `429` a otázka `open`;
  - změna odpovědi po publikaci → `409`.
- [ ] 5.7 Test `FixGenerateForAnswerHandlerTests` (`MockRewriteClient`, `MockJevClient`; neprošlá kontrola nechá nález `open`).

## 6. Doklady

- [ ] 6.1 `Application/Evidence/EvidenceFileValidator.cs`: typ podle magických čísel (PDF, JPEG, PNG), `Evidence:MaxFileBytes`, bezpečné jméno souboru.
- [ ] 6.2 `Application/Evidence/EvidenceStatusCalculator.cs`: stavy a `daysToExpiry` (AD 10). Test `EvidenceStatusCalculatorTests` s hraničními dny (0, 1, 30, 31, po vypršení).
- [ ] 6.3 `Application/Evidence/EvidenceService.cs`:
  - seznam se statistikami, detail s vazbami;
  - vytvoření se souborem do `IBlobStore` (`tenants/{t}/evidence/{id}/…`);
  - úprava s `If-Match`;
  - měkké smazání (nálezy → `open`, paměť `superseded_at`);
  - vazby a jejich odebrání;
  - podepsaný odkaz 5 minut.
- [ ] 6.4 Koncové body v `EvidenceEndpoints.cs`: `GET/POST T/evidence`, `GET/PATCH/DELETE T/evidence/{id}`, `GET T/evidence/{id}/file`, `POST T/evidence/{id}/links`, `DELETE T/evidence/{id}/links/{linkId}`.
- [ ] 6.5 `Jobs/Evidence/EvidenceRefreshStatusHandler.cs` (denně): `expiring` → upozornění jednou (`reminder_sent_at`), `expired` → nálezy `open` + upozornění `evidence_expired`.
- [ ] 6.6 Test `EvidenceTests`:
  - nahrání PDF;
  - podvržený typ → `400`;
  - soubor nad limit → `400`;
  - stažení přes `302` s krátkou platností;
  - smazání otevře nálezy;
  - doklad platí ve dvou e-shopech tenanta.
- [ ] 6.7 Test `EvidenceRefreshStatusHandlerTests` (`FakeTimeProvider`: BDIH 20. 10. 2026, připomenutí jen jednou, vypršení otevře 6 nálezů).

## 7. Hromadné opravy

- [ ] 7.1 `Application/Fixes/FixGroupService.cs`:
  - seznam se součty;
  - detail se vzorky v kontextu, `fit` a stránkami (kurzor);
  - údaje, režim, vyřazení stránek s `If-Match` a úlohou `fix.recheck` u `replace` po vyplnění a u `custom`.
- [ ] 7.2 `FixGroupService.ApproveAsync`:
  - jedna transakce;
  - zahrnuté stránky = skupina − vyřazené − `fit.individual`;
  - návrhy `accepted` s `group_id`, nálezy `approved`;
  - paměť jednou za skupinu;
  - `locked_at`, audit `group.approved`.
- [ ] 7.3 `ApprovePageAsync` („Len na tejto stránke“, `group.page_needs_individual_fix`) a `UnapproveAsync` (jen před první publikací).
- [ ] 7.4 Koncové body v `FixGroupEndpoints.cs`: `GET S/fix-groups`, `GET S/fix-groups/{groupId}`, `PUT …/values`, `PUT …/mode`, `PUT …/excluded-pages`, `POST …/approve`, `POST …/approve-page`, `POST …/unapprove`.
- [ ] 7.5 Test `FixGroupTests`:
  - 38 stránek → 36 přijatých, 2 bez návrhu, 1 záznam paměti;
  - chybí údaj → `409 group.value_missing`;
  - `custom` čeká na kontrolu → `409 group.recheck_pending`;
  - „jen na této stránce“;
  - `unapprove` po publikaci → `409`.

## 8. Publikace a sestavený text

- [ ] 8.1 `Application/Fixes/IFixPublisher.cs` (smlouva pro změnu 15) a `tests/…/FakeFixPublisher.cs`. Bez registrace → `409 publication.connector_unavailable`.
- [ ] 8.2 `Application/Fixes/FixedTextComposer.cs`: text pole z bloků verze stránky a přijatých návrhů (`block_index`), nepřijaté v `pendingProposalIds`. Test `FixedTextComposerTests` (více změn v jednom bloku, změna na hranici bloků, pole `name`).
- [ ] 8.3 `Application/Fixes/PublicationService.cs`:
  - předpoklady (AD 4);
  - seskupení po (stránka, pole), `old_value_hash`, `idempotency_key`;
  - `skipped` s `copy_only`;
  - úloha `publish.fix` (P1, `io`, `concurrency_key = connector:{shopId}`);
  - `rollback` → `publish.rollback`;
  - audit.
- [ ] 8.4 Koncové body v `PublicationEndpoints.cs` (`POST S/publications`, `GET S/publications`, `GET S/publications/{id}`, `POST …/rollback`) a `GET S/pages/{pageId}/fixed-text`.
- [ ] 8.5 Test `PublicationTests`:
  - Shoptet s `FakeFixPublisher` → `202` a úloha P1;
  - opakovaný požadavek vrátí stejnou publikaci;
  - `web` → `409 no_connector` a `fixed-text` funguje;
  - šablona → `skipped copy_only`;
  - viewer → `403`;
  - `rollback` u `queued` → `409`.

## 9. Protokol PDF

- [ ] 9.1 `Application/Protocols/ProtocolNumberAllocator.cs` (`pg_advisory_xact_lock`, formát `EG-{rok}-{NNNN}`, rok v `Localization:TimeZone`). Test `ProtocolNumberAllocatorTests`: 20 souběžných → 20 po sobě jdoucích čísel; přelom roku 31. 12. 23:30 UTC = 1. 1. v Bratislavě → nová řada.
- [ ] 9.2 `Application/Protocols/ProtocolDocumentBuilder.cs` + `Texts/sk.yaml`, `Texts/cs.yaml`:
  - souhrn, nezkontrolované, verze pravidel, země;
  - rozhodnutí a opravy, doklady, čekající na rozhodnutí;
  - prohlášení.

  Test `ProtocolDocumentBuilderTests` nad `BylinkovoSeed` (obsah odpovídá `Protocol.dc.html`, odkazy na zákon slovensky i v českém protokolu).
- [ ] 9.3 `Application/Protocols/IPdfRenderer.cs` a `Jobs/Protocols/PdfRenderer.cs` (knihovna podle K rozhodnutí 6; do rozhodnutí jen rozhraní a test s náhradní implementací).
- [ ] 9.4 `Application/Protocols/ProtocolService.cs` (období, jazyk, `protocol.no_completed_run`, číslo, úloha `protocol.render`, audit) a `Jobs/Protocols/ProtocolRenderHandler.cs` (uložení do `IBlobStore`, `status`, upozornění `protocol_ready` / `protocol_failed`).
- [ ] 9.5 Koncové body v `ProtocolEndpoints.cs`: `GET S/protocols`, `POST S/protocols`, `GET S/protocols/{id}`, `GET S/protocols/{id}/pdf`.
- [ ] 9.6 Test `ProtocolTests`:
  - žádost → `202 EG-2026-0142`;
  - stažení před dokončením → `409`;
  - po úloze → `302`;
  - výchozí jazyk podle domovské země;
  - nevalidní období → `400`.

## 10. Upozornění

- [ ] 10.1 `Application/Notifications/NotificationKind.cs` (druhy z AD 12 s vazbou na nastavení) a `NotificationPreferences.cs` (e-shop > účet > `Notifications:Defaults`).
- [ ] 10.2 `Application/Notifications/NotificationDispatcher.cs`: řádek pro každého příjemce, e-mail do `ops.outbox` podle nastavení, kontrola, že `params` neobsahují texty.
- [ ] 10.3 Šablony e-mailů `Application/Email/Templates/{sk,cs}/{evidence_expiring,evidence_expired,protocol_ready,protocol_failed,publication_failed,publication_conflict,new_violation}.*`. Rozšířit `EmailTemplateCompletenessTests` (změna 9).
- [ ] 10.4 `Application/Notifications/NotificationService.cs` a koncové body `GET T/notifications`, `POST T/notifications/{id}/read`, `POST T/notifications/read-all`, `GET T/notification-settings`, `PUT T/notification-settings`.
- [ ] 10.5 Test `NotificationTests`:
  - přečtení Janou nezmění Peterovi;
  - vypnutý e-mail e-shopu přebije účet;
  - výchozí hodnoty bez řádku;
  - nastavení jen vlastní;
  - upozornění bez textů stránek.

## 11. Běhy a živý průběh

- [ ] 11.1 `Application/Runs/RunQueryService.cs` (seznam, detail, pozice ve frontě ze změny 8, když je) a `RunCancelService.cs` (jen `free_sample` a `recheck` v nekonečném stavu → `cancel_requested`; jinak `409 run.not_cancelable`).
- [ ] 11.2 `Application/Runs/RunEventStream.cs`:
  - `IHostedService`, jedno spojení `LISTEN eg_run, eg_shop`, opětovné připojení;
  - rozesílání odběratelům podle (`tenant`, `run`) a (`tenant`, `shop`);
  - strop `Sse:MaxConnectionsPerUser`.
- [ ] 11.3 `Api/Http/SseWriter.cs` a koncové body `GET T/runs/{runId}/events`, `GET S/events`:
  - `snapshot`, `progress`, `status`, `run_event` s `id`, `: ping` po 15 s, `end`;
  - `Last-Event-ID`;
  - `Cache-Control: no-store`, `X-Accel-Buffering: no`.
- [ ] 11.4 Koncové body `GET S/runs`, `GET T/runs/{runId}`, `POST T/runs/{runId}/cancel` (admin).
- [ ] 11.5 Test `RunSseTests`:
  - pořadí událostí;
  - obnovení od `Last-Event-ID: 812`;
  - `end` po `finished`;
  - cizí běh → `404`;
  - 11. spojení → `429`;
  - událost tenanta B se odběrateli tenanta A neodešle (kontrola na úrovni proudu).

## 12. Ověření

- [ ] 12.1 `FindingsRoleAndIsolationTests`:
  - všechny koncové body této změny pro viewer / editor / admin / owner podle matice;
  - dva tenanti se stejnou doménou a stejným textem nálezu;
  - objekt tenanta B pod adresou tenanta A → `404` na každém koncovém bodu;
  - žádný řádek tenanta B se nezmění.
- [ ] 12.2 Test `SampleOnlyShopTests`: e-shop ve stavu `sample` → seznamy jen s 5 nálezy ukázky a počty, rozhodnutí `409 shop.sample_only` (AD 14).
- [ ] 12.3 Doplnit nové koncové body do `RoleMatrixTests`, `CsrfTests` a snímku `Snapshots/openapi-v1.json` (změna 9), včetně `x-problem-codes`.
- [ ] 12.4 `LogRedactionTests`: logy toků 4–9 neobsahují texty nálezů, návrhů ani obsah dokladů.
- [ ] 12.5 Celý tok nad `BylinkovoSeed` bez placených služeb (`MockJevClient`, `MockRewriteClient`, `FakeFixPublisher`): revize, úprava a kontrola, přijetí, hromadná oprava, odpověď, doklad, publikace, protokol, upozornění, SSE.
- [ ] 12.6 Živá kontrola úpravy textu a návrhu po „Nie“ na testovacím e-shopu (Jev a OpenAI jsou placené):
  - před spuštěním odhad ceny (přepis ~1 cent za stránku a kontrola ~0,03 centu podle návrhu rozvoje 1a);
  - souhlas uživatele;
  - bez souhlasu se nespouští.
- [ ] 12.7 `dotnet build` a `dotnet test` projdou.
- [ ] 12.8 `openspec validate add-findings-and-fixes-api` projde.
