# Design: Nálezy, opravy, doklady a protokol v API

## Technical Approach

### Vrstvy

| Projekt | Co přidá tato změna |
|---|---|
| `src/EshopGuard.Application/Findings/` | Čtení nálezů a stránek, přehled, hledání, stavový automat, rozhodnutí, katalog textů pravidel, export CSV. |
| `src/EshopGuard.Application/Fixes/` | Revize stránky, návrhy, otázky, hromadné opravy, publikace (rozhraní `IFixPublisher`), sestavení textu, paměť rozhodnutí. |
| `src/EshopGuard.Application/Evidence/` | Doklady, soubory, stavy, vazby. |
| `src/EshopGuard.Application/Protocols/` | Číslování, sestavení obsahu, rozhraní `IPdfRenderer`. |
| `src/EshopGuard.Application/Notifications/` | Čtení, nastavení, `NotificationDispatcher`. |
| `src/EshopGuard.Application/Runs/` | Čtení běhů, zrušení, `RunEventStream` (SSE). |
| `src/EshopGuard.Api/Endpoints/` | Koncové body níže. Žádná doménová logika. |
| `src/EshopGuard.Jobs/` | Úlohy `fix.recheck` (P0, Jev), `fix.generate_for_answer` (P0, OpenAI), `protocol.render`, `evidence.refresh_status` (denně), `PdfRenderer`. |
| `src/EshopGuard.Data/Migrations/` | Spouštěče `pg_notify`, sloupce `protocols.status` / `error_code`, `fix_proposals.recheck_result`, jedinečnost čísla protokolu, indexy pro záložky. |

### Koncové body

Prefix `S` = `/api/t/{tenantId}/shops/{shopId}`, prefix `T` = `/api/t/{tenantId}`. Role: čtení `viewer`, rozhodnutí, odpovědi, doklady, schválení, publikace a protokol `editor`, zrušení běhu `admin`. Měnící metody mají CSRF. Úpravy se souběžností mají `If-Match` a vracejí `ETag`.

**A. Přehled, seznamy, hledání**

| Metoda a cesta | Odpověď | Chybové kódy |
|---|---|---|
| `GET S/overview` | `OverviewDto` | `shop.not_found` |
| `GET S/pages?tab=to_resolve\|to_approve\|needs_answer\|published&language=&q=&cursor=&limit=` | `Page<PageWorkItemDto>` | `validation.failed` |
| `GET S/pages/tabs?language=` | `PageTabsDto { toResolve, toApprove, needsAnswer, published, withFindings }` | – |
| `GET S/findings?checkability=&status=&module=&language=&jurisdiction=&q=&groupBy=page&cursor=&limit=` | `Page<FindingListItemDto>` | `validation.failed` |
| `GET S/findings/tabs?language=` | `FindingTabsDto { text, assess, verify, fixed }` | – |
| `GET S/findings/{findingId}` | `FindingDetailDto` | `finding.not_found` |
| `GET S/findings/export.csv?…` (stejné filtry) | `text/csv; charset=utf-8` | – |
| `GET S/search?q=` | `SearchResultDto { pages[], findings[] }` (nejvýš 10 + 10) | `search.query_too_short` |
| `GET /api/catalog/rule-texts?locale=&ruleSetIds=` | `RuleTextCatalogDto` (`ETag`, `Cache-Control: private, max-age=3600`) | `locale.not_enabled`, `catalog.locale_incomplete` (`409`) |

**B. Rozhodnutí o nálezu**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `POST S/findings/{findingId}/keep` | `{ reasonCode: no_promise\|other, note? }` → `FindingDetailDto` | `finding.transition_not_allowed` (`409`, `params.from`, `params.to`) |
| `POST S/findings/{findingId}/dismiss` | `{ reasonCode: false_positive }` → `FindingDetailDto` | `finding.transition_not_allowed` |
| `POST S/findings/{findingId}/reopen` | → `FindingDetailDto` | `finding.transition_not_allowed` |

**C. Oprava stránky a návrhy**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET S/pages/{pageId}/review?tab=&language=` | `PageReviewDto` | `page.not_found` |
| `GET S/pages/{pageId}/fixed-text?field=description\|short_description\|name\|block` | `FixedTextDto` | `page.no_accepted_changes` (`409`) |
| `GET S/proposals/{proposalId}` | `ProposalDto` (stav kontroly pro dotazování) | `proposal.not_found` |
| `PUT S/proposals/{proposalId}/alternative` | `{ key }` + `If-Match` → `ProposalDto` | `proposal.alternative_unknown`, `concurrency.conflict` |
| `PUT S/proposals/{proposalId}/text` | `{ text }` + `If-Match` → `202 ProposalDto` (`recheck.status = pending`) | `proposal.text_empty`, `proposal.text_unchanged`, `proposal.text_too_long`, `proposal.locked` (`409`) |
| `PUT S/proposals/{proposalId}/placeholders` | `{ values: { key: value } }` + `If-Match` → `202 ProposalDto` | `proposal.placeholder_unknown`, `proposal.placeholder_empty` |
| `POST S/proposals/{proposalId}/accept` | + `If-Match` → `ProposalDto` | `proposal.recheck_pending` (`409`), `proposal.recheck_failed` (`409`, `params.jurisdictions`, `params.ruleIds`), `proposal.placeholder_missing` (`409`, `params.keys`), `concurrency.conflict` |
| `POST S/proposals/{proposalId}/reject` | + `If-Match` → `ProposalDto` | `proposal.locked` |
| `POST S/proposals/{proposalId}/unaccept` | + `If-Match` → `ProposalDto` | `proposal.already_published` (`409`) |

**D. Otázky**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET S/questions?scope=finding\|site&status=open\|answered&limit=` | `QuestionDto[]` (`appliesTo` u každé) | – |
| `POST S/questions/{questionId}/answer` | `{ answer: yes\|no }` → `AnswerResultDto { affectedQuestions, affectedFindings, affectedPages, evidenceId?, generationPending }` | `question.answer_locked` (`409`), `budget.daily_limit_reached` (`429`) |

**E. Hromadné opravy**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET S/fix-groups?status=` | `FixGroupListDto { groups[], totals: { groups, pages } }` | – |
| `GET S/fix-groups/{groupId}?cursor=` | `FixGroupDto` | `group.not_found` |
| `PUT S/fix-groups/{groupId}/values` | `{ values }` + `If-Match` → `202 FixGroupDto` | `group.placeholder_unknown`, `group.locked` |
| `PUT S/fix-groups/{groupId}/mode` | `{ mode: replace\|remove\|custom, customText? }` + `If-Match` → `FixGroupDto` (u `custom` `202`) | `group.custom_text_required`, `group.locked` |
| `PUT S/fix-groups/{groupId}/excluded-pages` | `{ pageIds[] }` + `If-Match` → `FixGroupDto` | `group.page_not_in_group`, `group.no_pages_left` |
| `POST S/fix-groups/{groupId}/approve` | + `If-Match` → `FixGroupDto` | `group.value_missing`, `group.recheck_pending`, `group.recheck_failed`, `concurrency.conflict` |
| `POST S/fix-groups/{groupId}/approve-page` | `{ pageId }` → `ProposalDto` | `group.page_not_in_group`, `group.page_needs_individual_fix` |
| `POST S/fix-groups/{groupId}/unapprove` | + `If-Match` → `FixGroupDto` | `group.already_published` |

**F. Publikace**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `POST S/publications` | `{ pageIds? \| proposalIds? \| groupId? }` → `202 PublicationBatchDto { publications[], skipped: [{ pageId, field, reasonCode }] }` | `publication.not_available` (`409`, `params.reason`: `no_connector`, `connector_error`, `read_only`, `ownership_not_verified`), `publication.connector_unavailable` (`409`), `publication.nothing_to_publish` (`409`) |
| `GET S/publications?status=&cursor=` | `Page<PublicationDto>` | – |
| `GET S/publications/{publicationId}` | `PublicationDto` | `publication.not_found` |
| `POST S/publications/{publicationId}/rollback` | → `202 PublicationDto` | `publication.not_rollbackable` (`409`) |

**G. Doklady (za celý tenant)**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET T/evidence?status=&q=&shopId=` | `EvidenceListDto { stats: { valid, expiring, awaitingAnswer, productsCovered }, items[] }` | – |
| `POST T/evidence` (`multipart/form-data`: `metadata` JSON + `file?`) | → `201 EvidenceDto` | `evidence.file_type_not_allowed`, `evidence.file_too_large`, `evidence.claim_required`, `evidence.valid_until_before_from` |
| `GET T/evidence/{evidenceId}` | `EvidenceDto` s vazbami | `evidence.not_found` |
| `PATCH T/evidence/{evidenceId}` | + `If-Match` → `EvidenceDto` | `concurrency.conflict` |
| `DELETE T/evidence/{evidenceId}` | → `204` (měkce; navázané nálezy → `open`) | – |
| `GET T/evidence/{evidenceId}/file` | → `302` na podepsaný odkaz (5 min) | `evidence.no_file` (`404`) |
| `POST T/evidence/{evidenceId}/links` | `{ findingIds[] }` → `EvidenceDto` | `finding.not_found`, `finding.transition_not_allowed` |
| `DELETE T/evidence/{evidenceId}/links/{linkId}` | → `204` | – |

**H. Protokoly**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET S/protocols` | `ProtocolDto[]` | – |
| `POST S/protocols` | `{ periodFrom, periodTo, locale? }` → `202 ProtocolDto` (`status = rendering`, `number`) | `protocol.period_invalid`, `locale.not_enabled`, `protocol.no_completed_run` (`409`) |
| `GET S/protocols/{protocolId}` | `ProtocolDto` | `protocol.not_found` |
| `GET S/protocols/{protocolId}/pdf` | → `302` na podepsaný odkaz (5 min, `attachment; filename=EG-2026-0142.pdf`) | `protocol.not_ready` (`409`), `protocol.failed` (`409`) |

**I. Upozornění (vlastní uživatele)**

| Metoda a cesta | Tělo → odpověď | Chybové kódy |
|---|---|---|
| `GET T/notifications?unreadOnly=&shopId=&cursor=` | `NotificationListDto { items[], unreadCount }` | – |
| `POST T/notifications/{notificationId}/read` | → `204` | `notification.not_found` |
| `POST T/notifications/read-all` | `{ shopId? }` → `204` | – |
| `GET T/notification-settings` | `NotificationSettingsDto { account: Prefs, shops: [{ shopId, prefs }] }` | – |
| `PUT T/notification-settings` | `{ shopId?, emailNewViolation, emailWeeklySummary, emailRunFinished }` → `NotificationSettingsDto` | `shop.not_found` |

**J. Běhy a živý průběh**

| Metoda a cesta | Odpověď | Chybové kódy |
|---|---|---|
| `GET S/runs?kind=&cursor=` | `Page<RunDto>` | – |
| `GET T/runs/{runId}` | `RunDto` | `run.not_found` |
| `GET T/runs/{runId}/events` | `text/event-stream` | `run.not_found`, `sse.too_many_connections` (`429`) |
| `GET S/events` | `text/event-stream` (stav návrhů, skupin, publikací, otázek, běhů e-shopu) | `sse.too_many_connections` |
| `POST T/runs/{runId}/cancel` (admin) | → `202 RunDto` | `run.not_cancelable` (`409`, `params.kind`, `params.status`) |

Role viewer smí upozornění a nastavení jen pro sebe. Doklady a otázky čte každý člen, mění editor a výš.

### DTO (výběr)

- `VerdictDto { jurisdiction, checkability: text|assess|verify, severity: high|medium|low, band: high|review, legalRefs: [{ law, provision, version, url? }] }`: odkazy v jazyce zákona.
- `FindingListItemDto { findingId, ruleSetId, ruleId, module, scope, status, text, page: { id, title, path, url, language } | null, occurrences, verdicts[], strictest, params, proposal: { id, status, recheckStatus } | null, questionId? }`.
- `FindingDetailDto`: navíc `contextBefore`, `contextAfter`, `occurrencePages: [{ pageId, title, url, language }]`, `questions[]`, `proposals[]`, `evidence[]`, `history: [{ at, action, actor: { userId, displayName } }]` (z `ops.audit_log`).
- `PageWorkItemDto { kind: page|site_template|site_obligations, pageId?, title, path, url?, language?, pageType?, counts: { text, assess, verify }, work: { proposals, accepted, openQuestions }, preview: { kind: diff|question, before?, after?, questionCode?, questionParams? }, status: needs_answer|to_approve|open|published, strictest }`.
- `OverviewDto`:
  - `{ shop: { id, domain }, lastRun: { runId, kind, status, finishedAt, pagesChecked, notChecked, jurisdictions[] } | null }`;
  - `{ findingCounts: { total, text, assess, verify }, pageTabs: PageTabsDto, priorityPages: PageWorkItemDto[≤5] }`;
  - `{ quickQuestions: { total, items: QuestionDto[≤3] }, badges: { fixes, evidence, monitoring: null }, monitoring: null }`.
- `PageReviewDto`:
  - `page: { id, title, url, language, source: { kind: connector|crawl|feed, platform?, externalId? } }`;
  - `position: { tab, index, total, prevPageId?, nextPageId?, nextTitle? }`;
  - `changes: [ChangeDto]`, `unchangedBlocks`, `templateNote: bool`;
  - `summary: { accepted, total, groupsAwaitingValue }`;
  - `publish: { available: bool, reasonCode?, platform?, acceptedCount }`.
- `ChangeDto`:
  - `{ index, kind: replace|remove|question|group, proposalId?, questionId?, groupId?, findingIds[], ruleId, ruleSetId, verdicts[], strictest }`;
  - `{ originalText, proposedText?, contextBefore, contextAfter, alternatives: [{ key, text, recheckStatus }], selectedAlternative?, editedText? }`;
  - `{ placeholders: [{ key, value? }], group: { pageCount, fitsCount, individualCount, status }?, recheck: { status: pending|ok|still_finding, jurisdictions?, ruleIds? }, status, version }`.
- `QuestionDto { questionId, scope: finding|site, code, params, ruleSetId, ruleId, page: { id, title } | null, status, answer?, answeredBy?: { displayName }, answeredAt?, appliesTo: { questions, findings, pages } }`.
- `FixGroupDto`:
  - `{ id, kind: repeated_text|template|site_obligation, originalText, replacementTemplate, placeholders[], filledValues, mode, customText?, ruleId, verdicts[], status, recheck }`;
  - `samples: [{ pageId, title, before, after }]`;
  - `fit: { fits, individual: [{ pageId, title, reasonCode, params }] }`;
  - `pages: { total, excludedPageIds[], items: [{ pageId, title }], nextCursor? }`;
  - `version`.
- `PublicationDto { id, pageId, field, language, status: queued|published|failed|conflict|rolled_back, attempts, errorCode?, requestedBy, publishedAt?, rolledBackAt?, fixProposalIds[] }`: bez starého a nového textu v seznamu, v detailu ano.
- `FixedTextDto { field, text, appliedProposalIds[], pendingProposalIds[], sourceVersionId }`.
- `EvidenceDto { id, claimText, subjectKind, subjectLabel, kind, title, fileName?, hasFile, source: upload|registry|answer, validFrom?, validUntil?, status, daysToExpiry?, links: { findings, pages, shops }, createdBy, createdAt, version }`.
- `ProtocolDto { id, number, periodFrom, periodTo, locale, status: rendering|ready|failed, errorCode?, generatedBy, createdAt, summary? }`.
- `NotificationDto { id, kind, shopId?, params, route: { key, params }, createdAt, readAt? }`.
- `RunDto { id, shopId, kind, trigger, status, progress, jurisdictions[], modules[], createdAt, startedAt?, finishedAt?, errorCode?, queue: { position?, estimatedFinishAt? } }`.
- `RuleTextCatalogDto { locale, ruleSets: [{ ruleSetId, module, version, rules: { [ruleId]: { title, explanation: { default, byJurisdiction }, recommendation, helps: [{ code, text }], question?: { text, yes, no, hint } } } }] }`.

## Architecture Decisions

**AD 1. Stavový automat nálezu (`FindingStatusMachine`, čistá funkce).**

| Z | Do | Spouštěč | Kdo |
|---|---|---|---|
| `open` | `needs_answer` | k nálezu vznikla otázka | systém (8) |
| `open` | `proposed` | návrh s `recheck_status = ok` | systém (8, úloha `fix.generate_for_answer`) |
| `needs_answer` | `kept_with_evidence` | odpověď „Áno“ | editor |
| `needs_answer` | `proposed` | odpověď „Nie“ a připravená varianta existuje | editor |
| `needs_answer` | `open` | odpověď „Nie“, návrh se teprve generuje | editor |
| `proposed` | `approved` | přijaté všechny návrhy nálezu | editor |
| `proposed` | `open` | jediný návrh zamítnut | editor |
| `approved` | `proposed` | přijetí vráceno (`unaccept`, `unapprove`) | editor |
| `approved` | `published` | publikace úspěšná | systém (15) |
| `published` | `approved` | publikace vrácena | systém (15) |
| `approved`, `published` | `resolved` | další kontrola nález nenašla | systém (16) |
| `open`, `needs_answer`, `proposed` | `kept` | „Ponechať“ | editor |
| `open`, `needs_answer`, `proposed` | `dismissed` | „Nejde o problém“ | editor |
| `kept`, `kept_with_evidence`, `dismissed` | `open` | „Znovu otvoriť“; doklad vypršel nebo byl smazán | editor / systém |
| `resolved` | `open` | text se vrátil | systém (16) |

- Jiný přechod → `409 finding.transition_not_allowed`.
- Každý přechod zapíše audit `finding.status_changed` (`from`, `to`, `reasonCode`, `actor`). Texty se nezapisují.
- Stav nálezu za celý web (`scope = site`) se řídí stejnou tabulkou.

**AD 2. Verdikty, řazení a texty pravidel.**
- `verdicts` (jsonb ze změny 8) se vrací beze změny jako `VerdictDto[]`. `strictest` počítá `VerdictStrictness` ze změny 6. Když tam není, `Application/Findings/VerdictStrictness.cs` s pořadím z K rozhodnutí 2.
- Stránky a nálezy se řadí: nejpřísnější verdikt, počet nálezů (sestupně), název.
- Řazení se počítá v SQL. Pořadí verdiktu je uložené jako `strictness_rank smallint` ve výrazu nad `verdicts` (index `ix_findings_shop_status_rank`), aby stránkování přes kurzor bylo stabilní.
- `RuleTextCatalog` čte `checks.rule_sets.texts` pro požadovaný jazyk:
  - chybějící jazyk → `ref.locales.fallback_code`;
  - neúplné texty → `409 catalog.locale_incomplete` (fail-closed; změna 6 jazyk bez úplných textů nedovolí zapnout);
  - obsah se drží v `IMemoryCache` podle `rule_set_id` a jazyka.
- „Čo pomôže“ (`helps`) je seznam kódů s texty z pravidla (návrh rozvoje 5), nic se nevymýšlí.
- Zákon se cituje v jazyce zákona (`legalRefs` z verdiktu).

**AD 3. Záložky a virtuální položky „Celý e-shop“.**
- Stránka má nález, když existuje `finding_occurrences` na nález ve stavu jiném než `resolved`.
- Položky za celý web:
  - nálezy `scope = site` a otázky `scope = site` tvoří položku `site_obligations` („Celý e-shop: košík a objednávka“);
  - skupiny `fix_groups.kind = template` tvoří položku `site_template` („Celý e-shop: šablóna“).
- Definice záložek:
  - `needs_answer`: položky s aspoň jednou otázkou `open`;
  - `to_approve`: položky bez otevřené otázky, s aspoň jedním návrhem `proposed` nebo `edited` s `recheck_status = ok`, nebo se skupinou ve stavu připraveném ke schválení;
  - `to_resolve`: položky s aspoň jedním nálezem ve stavu `open`, `needs_answer` nebo `proposed`;
  - `published`: položky, kde žádný nález není `open`, `needs_answer`, `proposed` ani `approved` a aspoň jeden je `published` nebo `resolved`;
  - `withFindings`: všechny položky s nálezem (pro „4 z 28“).
- Filtr `language` omezuje podle `pages.language`. Položky za celý web se ukazují u všech jazyků.

**AD 4. Revize stránky.**
- `PageReviewService` sestaví změny z `fix_proposals` (aktuální verze stránky), otázek a odkazů na skupiny. Pořadí změn podle `block_index` a pozice věty.
- Kontext (`contextBefore`, `contextAfter`, počet odstavců beze změny) se čte ze souboru extrakce `page_versions.extract_blob_key` přes `IBlobStore`. Bloky se do databáze nekopírují.
- Pozice ve frontě (`Stránka 1 z 24`, „Ďalšia: Sviečka Vodnár“) = pořadí záložky z AD 3 se stejnými filtry jako seznam.
- `publish.available` je `true` jen když:
  - `source_mode = connector`;
  - konektor je `connected` s přístupem `read_write`;
  - `IFixPublisher` je registrovaný;
  - existuje přijatý návrh.

  Jinak `reasonCode` (`no_connector`, `connector_error`, `read_only`, `publisher_missing`, `nothing_accepted`).

**AD 5. Přijetí návrhu a kontrola Jevem.**
- Přijmout lze jen text, jehož kontrola je `ok`:
  - vybraná varianta (`alternatives[key].recheckStatus`, zapisuje změna 8);
  - nebo upravený text (`edited_text`, `recheck_status` sloupce).
- Kontrola musí proběhnout pro všechny aktivní země e-shopu (`shop_markets.status = active`) a pro moduly nálezu.
  - `pending` → `409 proposal.recheck_pending`;
  - `still_finding` → `409 proposal.recheck_failed` se zeměmi a pravidly z `recheck_result`.
- Nevyplněný údaj (`placeholders` bez hodnoty) → `409 proposal.placeholder_missing`. Údaj se vloží doslova („Údaj vložíme presne tak, ako ho napíšete“).
- Úprava textu nebo údaje:
  - nastaví `status = edited`, `recheck_status = pending`;
  - založí úlohu `fix.recheck` (P0, `resource_class = jev`, `dedupe_key = recheck:{proposalId}:{textHash}`, `concurrency_key = recheck:{proposalId}`);
  - obsluha `FixRecheckHandler` zavolá `AnalyzeTextsAsync` (změna 5) s kontextem a jurisdikcemi;
  - zapíše `recheck_status` a `recheck_result` jen pokud se text mezitím nezměnil (porovnání `textHash`);
  - pošle událost `proposal.rechecked` do SSE e-shopu.
- Souběžnost: `If-Match` (token `xmin`). Rozdíl → `409 concurrency.conflict` s aktuální verzí.
- Přijetí nastaví `status = accepted`, `decided_by` a `decided_at`, nález přes automat na `approved` (všechny návrhy nálezu přijaté) a zapíše paměť rozhodnutí (AD 9).

**AD 6. Odpovědi na otázky a doklady.**
- `QuestionService.AnswerAsync` v jedné transakci:
  1. Najde všechny otevřené otázky se stejným `code` a stejným otiskem textu (`findings.segment_hash`) ve všech e-shopech tenanta (K rozhodnutí 9). U `scope = site` jen v daném e-shopu.
  2. Uloží odpověď (`answer`, `answered_by`, `answered_at`).
  3. **„Áno“:**
     - založí `evidence_items` (`kind = answer`, `source = answer`, `claim_text` = text nálezu, `subject_label` z parametrů otázky, `status = valid`, bez souboru);
     - založí `evidence_links` pro každý nález a stránku;
     - nálezy přejdou na `kept_with_evidence`;
     - paměť rozhodnutí `keep_with_evidence`.
  4. **„Nie“:**
     - pokud návrh nálezu má variantu `answer_no` s `recheckStatus = ok`, vybere se a nález přejde na `proposed`;
     - jinak u otázky za celý web text z pravidla (K rozhodnutí 10);
     - jinak nález přejde na `open` a vznikne úloha `fix.generate_for_answer` (P0, `resource_class = llm`) v denním rozpočtu tenanta (kbelík `fixes:generate:tenant:{id}`); nad rozpočtem `429 budget.daily_limit_reached` a odpověď se neuloží.
  5. Audit `question.answered` (kód, odpověď, počty).
- Změna odpovědi je možná, dokud žádný dotčený návrh není `published`. Jinak `409 question.answer_locked`.
- `appliesTo` v `QuestionDto` se počítá stejným dotazem jako krok 1, takže obchodník vidí dopad předem.

**AD 7. Hromadné opravy.**
- `fix_groups` zakládá jiná změna (K rozhodnutí 1). Tato změna mění jen `filled_values`, `mode`, `excluded_page_ids`, `status`, `approved_by`, `approved_at` a `locked_at`.
- Režimy:
  - `replace` (šablona s údaji, kontrola po vyplnění);
  - `remove` („Vetu zo všetkých stránok odstrániť“, kontrola okolí není potřeba, protože stránky, kde na větu navazuje další, jsou v `fit.individual`);
  - `custom` („Napísať vlastné znenie“, kontrola Jevem).
- Schválení v jedné transakci:
  - zahrnuté stránky = stránky skupiny − `excluded_page_ids` − `fit.individual`;
  - pro každou zahrnutou stránku vznikne nebo se aktualizuje `fix_proposals` (`group_id`, `status = accepted`, `proposed_text` z bloku stránky s náhradou);
  - nálezy přes automat přejdou na `approved`;
  - paměť rozhodnutí `replace` / `remove` jednou za skupinu (`segment_hash`);
  - `status = approved`, `locked_at`;
  - audit `group.approved` s počtem stránek.
- `approve-page` („Len na tejto stránke“) přijme jen návrh dané stránky. Stránka z `fit.individual` → `409 group.page_needs_individual_fix`.
- `unapprove` je možné, dokud žádná publikace skupiny není `published`.

**AD 8. Publikace a sestavený text.**
- `PublicationService.RequestAsync`:
  - ověří předpoklady z AD 4 (jinak `409 publication.not_available` s důvodem, nebo `publication.connector_unavailable` bez implementace `IFixPublisher`);
  - seskupí přijaté návrhy po (`page_id`, `field`);
  - `new_value` sestaví `FixedTextComposer` z bloků verze stránky a přijatých změn;
  - `old_value_hash` = otisk aktuálního textu pole z verze stránky;
  - `idempotency_key = SHA-256(shopId | pageId | field | old_value_hash | SHA-256(new_value))`, takže opakovaný požadavek vrátí existující záznam;
  - pole, které platforma neumí zapsat (`IFixPublisher.CanPublish(platform, field, page)` = `false`: šablona, Upgates produkt bez kódu, BiznisWeb bez partnerské smlouvy), jde do `skipped` s `reasonCode = copy_only`;
  - založí `publications` (`status = queued`) a úlohu `publish.fix` (P1, `resource_class = io`, `concurrency_key = connector:{shopId}`);
  - audit `publication.requested`.
- Kontrolu konfliktu (text v e-shopu se mezitím změnil), zápis, uložení původního znění a nastavení `published` / `conflict` / `failed` dělá obsluha ze změny 15. API stav jen čte.
- `rollback` je možný jen u `published` a založí úlohu `publish.rollback` (změna 15).
- `FixedTextComposer` vrací text celého pole s přijatými změnami pro „Kopírovať text“ (i bez konektoru). Nepřijaté návrhy se nevkládají a jsou v `pendingProposalIds`.

**AD 9. Paměť rozhodnutí (`fixes.decision_memory`).**
- Zapisuje se při:
  - přijetí návrhu (`replace` s konečným textem, `source_proposal_id`);
  - schválení skupiny (`replace` / `remove`);
  - „Ponechať“ (`keep`);
  - odpovědi „Áno“ (`keep_with_evidence` s `evidence_id`);
  - návrhu „Riadok odstrániť“ (`remove`).
- Klíč: `shop_id`, `segment_hash`, `normalized_text`.
- Vrácení rozhodnutí (`unaccept`, `unapprove`, `reopen`, smazaný doklad) nastaví `superseded_at` a nic nemaže.
- `auto_publish` zůstává `false` (automatické zveřejnění je ve změně 16 a je standardně vypnuté).

**AD 10. Doklady.**
- Soubor:
  - `multipart/form-data` do `Evidence:MaxFileBytes` (návrh 20 MB);
  - typ podle obsahu (magická čísla), ne podle přípony: PDF, JPEG, PNG;
  - uložení `tenants/{tenantId}/evidence/{evidenceId}/{bezpečné jméno}` přes `IBlobStore`;
  - stažení jen přes podepsaný odkaz platný 5 minut s `Content-Disposition: attachment`.
- Stav (`EvidenceStatusCalculator`):
  - `awaiting_answer` (otázka bez odpovědi, řádek z „Čaká na odpoveď“);
  - `claim_removed` (tvrzení odstraněno, odpověď „Nie“);
  - `expired` (`valid_until < dnes`);
  - `expiring` (`valid_until − dnes ≤ Evidence:ExpiringDays`, návrh 30);
  - jinak `valid`;
  - `daysToExpiry` pro „Končí o 20 dní“.
- Úloha `evidence.refresh_status` (denně, `resource_class = system`):
  - přepočítá stavy;
  - při přechodu na `expiring` pošle upozornění `evidence_expiring` (jednou, `reminder_sent_at`);
  - při `expired` vrátí navázané nálezy `kept_with_evidence` → `open` a pošle `evidence_expired`.
- Smazání dokladu (měkké) vrátí navázané nálezy na `open` a nastaví `superseded_at` v paměti rozhodnutí.
- Doklad platí pro všechny e-shopy tenanta. Vazby (`evidence_links`) říkají, kde byl použit („19 produktov“).

**AD 11. Protokol PDF.**
- `ProtocolService.RequestAsync`:
  - ověří období (`periodFrom ≤ periodTo ≤ dnes`, e-shop má dokončený běh `full_analysis` nebo `free_sample`, jinak `409 protocol.no_completed_run`);
  - ověří jazyk (zapnutý, výchozí = `ref.markets.default_locale` pro `shops.home_country`, K rozhodnutí 5);
  - přidělí číslo a založí `protocols` (`status = rendering`);
  - založí úlohu `protocol.render` (P1, `resource_class = cpu`, `dedupe_key = protocol:{id}`);
  - audit `protocol.requested`.
- `ProtocolNumberAllocator` ve stejné transakci:

  ```sql
  SELECT pg_advisory_xact_lock(hashtextextended('protocol:' || @tenant || ':' || @year, 0));
  SELECT coalesce(max(substring(number FROM 9)::int), 0) + 1
  FROM fixes.protocols WHERE tenant_id = @tenant AND number LIKE 'EG-' || @year || '-%';
  ```

  Formát `EG-{rok}-{pořadí, aspoň 4 číslice}`. Rok podle data vystavení v `Localization:TimeZone` (`Europe/Bratislava`). Jedinečný index (`tenant_id`, `number`) je pojistka.
- `ProtocolDocumentBuilder` sestaví obsah z databáze:
  - e-shop, období, úvodní kontrola (datum, počet stránek, nezkontrolované podle důvodu);
  - sledování (z běhů a předplatného, když je);
  - verze pravidel (`rule_sets` z `runs.rule_set_ids` v období), země kontroly (`runs.jurisdictions`);
  - souhrn (nálezy při úvodní kontrole, opravené a zveřejněné, ponechané s dokladem, ponechané rozhodnutím obchodníka, čekající na rozhodnutí);
  - tabulka rozhodnutí a oprav z `publications`, `decision_memory` a auditu (datum, stránky nebo počet, původně, řešení, bod zákona);
  - doklady obchodníka (tvrzení, doklad, platnost, počet produktů);
  - prohlášení: protokol není právním posouzením ani certifikátem, návrhy připravila umělá inteligence a schválil je provozovatel.
- Texty protokolu jsou v `Protocols/Texts/{locale}.yaml` a v textech pravidel. Odkazy na zákon v jazyce zákona.
- `ProtocolRenderHandler`:
  - `IPdfRenderer.Render(document)` (K rozhodnutí 6);
  - uloží `tenants/{tenantId}/shops/{shopId}/protocols/{number}.pdf`;
  - nastaví `pdf_blob_key`, `summary`, `rule_set_ids`, `status = ready`;
  - pošle upozornění `protocol_ready`;
  - při chybě `status = failed`, `error_code`, upozornění `protocol_failed`.

**AD 12. Upozornění.**
- `NotificationDispatcher.NotifyAsync(tenantId, shopId?, kind, params, route, audience)` pro změny 8, 15, 16 a tuto změnu:
  - založí řádek `iam.notifications` pro každého příjemce (`user_id` vždy vyplněné, K rozhodnutí 8);
  - u druhů s e-mailem zapíše `ops.outbox` (`template = kind`, `toUserId`) podle `notification_settings`: řádek e-shopu > řádek účtu (`shop_id = NULL`) > `Notifications:Defaults`.
- Druhy a nastavení:
  - `new_violation` → `email_new_violation`;
  - `run_finished`, `run_partial`, `run_failed`, `sample_finished` → `email_run_finished`;
  - týdenní souhrn → `email_weekly_summary` (změna 16);
  - `evidence_expiring`, `evidence_expired`, `protocol_ready`, `protocol_failed`, `publication_failed`, `publication_conflict` → e-mail vždy rolím editor a výš;
  - `market_suggested`, `market_now_supported`, `invitation_accepted` → jen v aplikaci.
- `params` obsahuje jen kódy, počty a ID, ne texty stránek. `route` je jazykově neutrální klíč (`fixes.page`, `evidence.item`, `protocols.item`) s parametry. Frontend z něj složí odkaz.
- Šablony e-mailů pro druhy této změny jsou v `Application/Email/Templates/{sk,cs}/` (infrastruktura ze změny 9).

**AD 13. Živý průběh (SSE).**
- Migrace přidá spouštěče:
  - `AFTER INSERT ON checks.run_events` → `pg_notify('eg_run', tenant_id || ':' || run_id || ':' || id)`;
  - `AFTER UPDATE OF status, progress ON checks.runs` → `pg_notify('eg_run', tenant_id || ':' || id || ':0')`;
  - `AFTER UPDATE OF status, recheck_status ON fixes.fix_proposals`, `fixes.fix_groups`, `fixes.publications`, `AFTER UPDATE OF status ON checks.questions` → `pg_notify('eg_shop', tenant_id || ':' || shop_id || ':' || entita || ':' || id)`.

  Obsah je jen ID, nikdy texty.
- `RunEventStream`:
  - singleton s jedním spojením `LISTEN eg_run, eg_shop` na instanci API (role `eshopguard_app`), při výpadku se znovu připojí;
  - rozesílá oznámení odběratelům podle (`tenant_id`, `run_id`) nebo (`tenant_id`, `shop_id`);
  - každý odběratel si řádky načte vlastním dotazem s `app.tenant_id` svého požadavku (RLS). Oznámení cizího tenanta se k němu nedostane a ani by nic nepřečetl.
- Formát:
  - `event: snapshot` (`RunDto`) na začátku;
  - `event: progress`, `event: status`;
  - `event: run_event` (`{ id, at, level, code, data }`, `id:` = `run_events.id`);
  - komentář `: ping` každých 15 s;
  - `event: end` po konečném stavu.
- `Last-Event-ID` → doplní chybějící `run_events` s vyšším `id` z databáze.
- Strop `Sse:MaxConnectionsPerUser` (návrh 10) → `429 sse.too_many_connections`.
- Odpověď nese `Cache-Control: no-store` a `X-Accel-Buffering: no` (Caddy `flush_interval -1`, změna 17).

**AD 14. E-shop jen s ukázkou zdarma.**
Do rozhodnutí (K rozhodnutí 4) u e-shopu ve stavu `sample`:
- seznamy, revize a hledání vrátí jen nálezy z `topFindings` souhrnu ukázky (změna 10);
- ostatní nálezy se ukážou jen v počtech;
- rozhodnutí, odpovědi, skupiny a publikace vrátí `409 shop.sample_only`.

**AD 15. Export CSV a hledání.**
- Export:
  - proudově (nejvýš `Findings:ExportMaxRows`, návrh 50 000), UTF-8 s BOM, oddělovač `;`;
  - sloupce: text, stránka, adresa, jazyk verze, skupina, závažnost a stav po zemích, ustanovení, stav, název pravidla v jazyce uživatele (z katalogu);
  - text nálezu je kopie z webu zákazníka a zůstává v souboru jen pro tenanta;
  - audit `findings.exported`.
- Hledání:
  - `q` aspoň 2 znaky;
  - stránky podle `title` a `path` (index trigramů `pg_trgm` na `content.pages (shop_id, title)`), nálezy podle `findings.text` (trigramy);
  - jen v daném e-shopu a pod RLS.

## Data Flow

### Oprava stránky → přijetí → publikace

```
GET S/pages/{pageId}/review
  PageReviewService: fix_proposals (aktuální verze) + questions + fix_groups + bloky z extract_blob_key (IBlobStore)
  ◀── changes[5], position {index: 1, total: 24, next: "Sviečka Vodnár"}, publish {available: true, platform: shoptet}
PUT S/proposals/{p3}/alternative {key: with_detail}            (If-Match)
PUT S/proposals/{p3}/text {text: "…rukoväť kefky je z bambusu…"}  → 202, recheck pending
  úloha fix.recheck (P0, jev) → FixRecheckHandler → AnalyzeTextsAsync(text, kontext, [sk, cz])
  → recheck_status ok, recheck_result {sk: ok, cz: ok} → pg_notify eg_shop → SSE "proposal.rechecked"
POST S/proposals/{p3}/accept (If-Match)
  FindingStatusMachine: proposed → approved (všechny návrhy nálezu přijaté)
  decision_memory (replace), audit proposal.accepted
POST S/publications {pageIds: [page]}
  PublicationService: předpoklady → seskupení po (page, field) → FixedTextComposer → idempotency_key
  INSERT publications (queued) + job publish.fix (P1, io)      → 202
  změna 15: konflikt? zápis → published → FindingStatusMachine approved → published → SSE
```

### Odpověď na otázku

```
POST S/questions/{q}/answer {answer: no}
  QuestionService: shodné otevřené otázky (code, segment_hash) v tenantovi → 4 otázky, 4 nálezy, 4 stránky
  varianta answer_no s recheck ok? → ano → nálezy needs_answer → proposed, výběr varianty
                                   → ne  → kbelík fixes:generate:tenant → job fix.generate_for_answer (P0, llm)
                                           nálezy needs_answer → open; po vygenerování a kontrole → proposed (worker)
  audit question.answered (paměť rozhodnutí vznikne až přijetím návrhu, AD 9)
  ◀── {affectedQuestions: 4, affectedFindings: 4, affectedPages: 4, generationPending: false}
```

### Protokol

```
POST S/protocols {periodFrom: 2026-09-30, periodTo: 2026-10-31}
  ProtocolNumberAllocator (advisory lock tenant+2026) → EG-2026-0142
  INSERT protocols (rendering) + job protocol.render              → 202 ProtocolDto
worker ProtocolRenderHandler → ProtocolDocumentBuilder (DB, texty pravidel, Protocols/Texts/sk.yaml)
  → IPdfRenderer → IBlobStore tenants/{t}/shops/{s}/protocols/EG-2026-0142.pdf → status ready
  → NotificationDispatcher protocol_ready (aplikace + e-mail editorům)
GET S/protocols/{id}/pdf → 302 podepsaný odkaz (5 min)
```

### SSE běhu

```
GET T/runs/{runId}/events (cookie, Last-Event-ID: 812)
  TenantAccessFilter → run existuje pod RLS? jinak 404
  snapshot RunDto + run_events id > 812 (pod RLS)
  RunEventStream.Subscribe(tenant, run)
worker: INSERT run_events → spouštěč pg_notify('eg_run', 't:r:813')
API: oznámení → odběratel (tenant, run) → SELECT run_events WHERE id = 813 (RLS) → "id: 813 / event: run_event"
konečný stav běhu → "event: end" → uzavření
```

## File Changes

**`src/EshopGuard.Application/Findings/`:**
- `FindingStatusMachine.cs`, `FindingStatus.cs`, `VerdictStrictness.cs` (jen když ho nedodá změna 6).
- `FindingQueryService.cs`, `PageWorkQueryService.cs`, `PageTabs.cs`, `OverviewService.cs`, `SearchService.cs`.
- `FindingDecisionService.cs`, `RuleTextCatalog.cs`, `FindingsCsvExporter.cs`.

**`src/EshopGuard.Application/Fixes/`:**
- `PageReviewService.cs`, `ExtractContextReader.cs`.
- `FixProposalService.cs`, `QuestionService.cs`, `AnswerPropagation.cs`, `FixGroupService.cs`.
- `PublicationService.cs`, `IFixPublisher.cs` (smlouva pro změnu 15: `CanPublish(platform, field, page)`; zápis a vrácení dělá obsluha úlohy), `FixedTextComposer.cs`.
- `DecisionMemoryWriter.cs`, `GenerationBudget.cs`.

**`src/EshopGuard.Application/Evidence/`:** `EvidenceService.cs`, `EvidenceStatusCalculator.cs`, `EvidenceFileValidator.cs`.

**`src/EshopGuard.Application/Protocols/`:**
- `ProtocolService.cs`, `ProtocolNumberAllocator.cs`, `ProtocolDocument.cs`, `ProtocolDocumentBuilder.cs`, `IPdfRenderer.cs`.
- `Texts/sk.yaml`, `Texts/cs.yaml`.

**`src/EshopGuard.Application/Notifications/`:** `NotificationService.cs`, `NotificationDispatcher.cs`, `NotificationKind.cs`, `NotificationPreferences.cs`.

**`src/EshopGuard.Application/Runs/`:** `RunQueryService.cs`, `RunCancelService.cs`, `RunEventStream.cs` (`IHostedService` s `NpgsqlConnection.WaitAsync`).

**`src/EshopGuard.Application/Email/Templates/{sk,cs}/`:** `evidence_expiring`, `evidence_expired`, `protocol_ready`, `protocol_failed`, `publication_failed`, `publication_conflict`, `new_violation` (`*.subject.txt|*.html|*.txt`).

**`src/EshopGuard.Api/Endpoints/`:**
- `OverviewEndpoints.cs`, `FindingEndpoints.cs`, `PageReviewEndpoints.cs`, `ProposalEndpoints.cs`, `QuestionEndpoints.cs`;
- `FixGroupEndpoints.cs`, `PublicationEndpoints.cs`, `EvidenceEndpoints.cs`, `ProtocolEndpoints.cs`;
- `NotificationEndpoints.cs`, `RunEndpoints.cs` (včetně SSE), `CatalogEndpoints.cs`.
- `Contracts/Findings/*.cs`, `Contracts/Fixes/*.cs`, `Contracts/Evidence/*.cs`, `Contracts/Protocols/*.cs`, `Contracts/Notifications/*.cs`, `Contracts/Runs/*.cs`.
- `Http/ETagExtensions.cs` (`If-Match` ↔ `xmin`), `Http/SseWriter.cs`.

**`src/EshopGuard.Jobs/`:**
- `Fixes/FixRecheckHandler.cs`, `Fixes/FixGenerateForAnswerHandler.cs`;
- `Protocols/ProtocolRenderHandler.cs`, `Protocols/PdfRenderer.cs` (implementace `IPdfRenderer` podle K rozhodnutí 6);
- `Evidence/EvidenceRefreshStatusHandler.cs`.

**`src/EshopGuard.Data/Migrations/`:**
- `*_FindingsApi`:
  - `fixes.protocols.status` (`rendering` / `ready` / `failed`), `error_code`, jedinečný index (`tenant_id`, `number`);
  - `fixes.fix_proposals.recheck_result jsonb`;
  - funkce a index `strictness_rank`;
  - trigramové indexy `content.pages (shop_id, title)` a `checks.findings (shop_id, text)`;
  - index `checks.questions (tenant_id, code, status)`.
- `Sql/notify_triggers.sql`: spouštěče `pg_notify` z AD 13.

**Testy:**
- `tests/EshopGuard.Application.Tests/Findings/`: `FindingStatusMachineTests.cs`, `VerdictStrictnessTests.cs`, `PageTabsTests.cs`.
- `tests/EshopGuard.Application.Tests/Fixes/`: `AnswerPropagationTests.cs`, `FixedTextComposerTests.cs`, `DecisionMemoryWriterTests.cs`.
- `tests/EshopGuard.Application.Tests/Evidence/EvidenceStatusCalculatorTests.cs`.
- `tests/EshopGuard.Application.Tests/Protocols/`: `ProtocolNumberAllocatorTests.cs`, `ProtocolDocumentBuilderTests.cs`.
- `tests/EshopGuard.Api.Tests/Findings/`:
  - `BylinkovoSeed.cs` (data z návrhu UI: 28 stránek, 43 nálezů, 7 skupin);
  - `OverviewTests.cs`, `PagesAndFindingsTests.cs`, `PageReviewTests.cs`, `ProposalTests.cs`, `QuestionTests.cs`;
  - `FixGroupTests.cs`, `PublicationTests.cs` (s `FakeFixPublisher`), `EvidenceTests.cs`, `ProtocolTests.cs`;
  - `NotificationTests.cs`, `RunSseTests.cs`, `RuleTextCatalogTests.cs`, `CsvExportTests.cs`;
  - `FindingsRoleAndIsolationTests.cs`.
- `tests/EshopGuard.Jobs.Tests/`: `FixRecheckHandlerTests.cs` (`MockJevClient`), `FixGenerateForAnswerHandlerTests.cs` (`MockRewriteClient`), `ProtocolRenderHandlerTests.cs`, `EvidenceRefreshStatusHandlerTests.cs`.

**Tabulky:**
- čtení a zápis: `checks.findings`, `checks.questions`, `fixes.fix_proposals`, `fixes.fix_groups`, `fixes.publications`, `fixes.decision_memory`, `fixes.evidence_items`, `fixes.evidence_links`, `fixes.protocols`, `iam.notifications`, `iam.notification_settings`;
- zápis: `checks.runs.cancel_requested`, `ops.jobs`, `ops.outbox`, `ops.audit_log`, `ops.rate_limit_buckets`;
- čtení: `checks.finding_occurrences`, `checks.runs`, `checks.run_events`, `checks.rule_sets`, `content.pages`, `content.page_versions`, `shop.shops`, `shop.shop_markets`, `shop.connectors`.

## Odchylky při implementaci (2. 10. 2026)

Implementace se řídí tímto návrhem s odchylkami níže. Většina vychází z povoleného směru závislostí (`Jobs` nesmí odkazovat
na `Application`, `ProjectReferenceTests`), z datového modelu změny 8 nebo z pravidla fail-closed.

### Skupiny 4, 5, 6 a 10 (revize, otázky, doklady, upozornění)
- Skupina 4: FixEndpoints.cs místo PageReviewEndpoints.cs + ProposalEndpoints.cs; ExtractContextReader, ProposalText, GroupValues v Jobs/Fixes (sdílí API i worker); DecisionMemoryWriterTests v Api.Tests (potřebuje DB).
- Skupina 5: Vodnár = 1 otázka / 1 nález / 4 stránky (identita nálezů změny 8), scénář spec 4/4/4 → test 1/1/4.
- Otázka za celý web „Nie“: nález open, náprava = doporučení pravidla z katalogu textů, bez úlohy a bez LLM (nález za celý web nemá text k přepisu); fail-closed.
- Knihovna: RewritePageInput.Answers (MerchantAnswer); odpovědi jsou v části stránky zadání jen když existují (bez nich beze změny, baseline beze změny); nález k ověření se přepisuje jen s odpovědí; po „Nie“ nalezené znovu stejné pravidlo k ověření = StillFinding.
- WaitingForFacts po „Nie“ = still_finding (zástupný údaj chce fakt, který obchodník nemá).
- Změna odpovědi: nejdřív se vezme zpět předchozí (doklad odpovědi smazán, paměť nahrazena, varianta answer_no odvybrána, přijetí vráceno, nálezy přes povolené přechody zpět na needs_answer).
- Úloha fix.generate_for_answer: jeden přepis na nález (první stránka), výsledek pro návrhy dalších stránek se stejným blokem; nález bez návrhu dostane nový návrh; přechod open→proposed s auditem actor_kind system.
- Rozpočet: kbelík se bere až nakonec v transakci odpovědi (nad rozpočtem rollback); tokeny se nevrací, pokud by transakce padla až po odběru.
- Skupina 10 před skupinou 6: NotificationDispatcher, NotificationKinds, NotificationsOptions v Jobs/Notifications (ne Application), protože je volají úlohy workeru (doklady, protokol, publikace, konec běhu) a Jobs nesmí odkazovat na Application; e-mailová fronta (OutboxEmails) už v Jobs je.
- FinalizeHandler (změna 8) posílá konec běhu přes dispatcher: řádek pro každého člena (user_id vyplněné, K8), druhy run_finished/run_partial/run_failed/sample_finished (dřív run.finished… s user_id NULL), e-mail i bez řádku nastavení podle Notifications:Defaults (návrh: vše zapnuto).
- Odkazy e-mailů upozornění z route přes Frontend:RoutePaths (cesty frontendu jsou návrh).
- Upozornění odmítne parametry, které nejsou kód/číslo/ID (regex), místo pouhé kontroly v testu.
- Šablony e-mailů nových druhů (sk, cs) jsou návrh k hromadné kontrole.
- Skupina 6: EvidenceStatusCalculator, EvidenceOptions a obnova stavů (EvidenceStatusRefresher, handler, plánovač po tenantech) v Jobs/Evidence (sdílí API i worker); úloha evidence.refresh_status je po tenantech (iam.tenants bez RLS), dedupe tenant+den, plánovač každou hodinu.
- Platnost dokladu je datum (uloženo jako půlnoc UTC), „dnes“ v Localization:TimeZone.
- Stažení: 302 na podepsaný odkaz; souborové úložiště neumí podepsat → API vydá vlastní /api/files/{token} (Data Protection, 5 min, attachment, jen pro přihlášené).
- Odpověď „Nie“ u otázky k nálezu založí doklad typu odpověď ve stavu claim_removed („Nevieme doložiť“) s vazbami; seznam dokladů přidává otevřené otázky jako řádky awaiting_answer (questionId) po jednom na text.
- EvidenceDto navíc questionId a items (vazby jen v detailu); createdBy jako { userId, displayName }.
- POST links: nález přejde na kept_with_evidence přes stavový automat (jen z needs_answer, jinak 409 transition_not_allowed); odebrání poslední vazby a smazání vrací nález na open jen když ho nedrží jiný platný doklad.
- Soubor se čte celý do paměti (nejvýš Evidence:MaxFileBytes) a typ se ověřuje na uložených bajtech.

### Skupina 7 (hromadné opravy)
- Scénář „Jedno rozhodnutí místo 36“ píše „36 nálezů je approved“. Nález opakované věty je ale v datovém modelu změny 8 jeden
  (unikátní index `shop_id, rule_id, segment_hash` pro `scope = segment`) s 38 výskyty. Po schválení skupiny zůstane `proposed`,
  dokud nemají přijatý návrh i 2 stránky k jednotlivému řešení (fail-closed: věta na stránce dál je, nález se neschválí).
  Test ověřuje 36 přijatých návrhů s `group_id`, 2 stránky bez návrhu, nález `proposed`, 1 záznam paměti `replace`.
  Nález, jehož žádná stránka opravu skupiny nedostala (vše vyřazeno nebo k jednotlivému řešení), se nemění.
- `PUT …/mode` a `PUT …/values` vracejí `202`, jen když znění čeká na kontrolu (`recheck.status = pending`); jinak `200`
  (např. `remove` kontrolu nepotřebuje).
- `approve-page` bez `pageId` → `400 validation.failed` (`errors.pageId = value.required`) až po ověření e-shopu a skupiny (404 izolace).
- Vzorky v kontextu: text těsně před a za větou (v rámci bloku, jinak sousední bloky), nejvýš 3; stránky po 30 s kurzorem (název, id).

### Skupina 8 (publikace a sestavený text)
- Pole a jejich zdroj v extrakci verze stránky podle mapování workeru (`RewriteBatchHandler.Field`): `name` = titulek,
  `short_description` = meta popis, `description` = popis z JSON-LD, `block` = hlavní text (blok na řádek). Změna se hledá
  s volnými mezerami (i přes konec řádku, takže může přesáhnout hranici bloků), nejdřív od svého bloku.
- `FixedTextDto` má navíc `unplacedProposalIds`: přijaté změny, jejichž původní text na stránce už není (stránka se změnila),
  ani překrývající se změny. Nevloží se a nic se tiše nezahodí (fail-closed). Zveřejněná změna, jejíž nový text už ve verzi je,
  se počítá jako použitá.
- Publikace pole s nenalezenou přijatou změnou se nezaloží: `skipped` s `reasonCode = not_located` (kromě `copy_only`).
- `publication.not_available` s `ownership_not_verified`: zápis do e-shopu jen s ověřeným vlastnictvím (připojení konektoru ho ověří,
  `verification_method = connector`). Stejný důvod vrací i `publish.reasonCode` revize stránky (`PublishAvailability`, až po
  `publisher_missing`).
- Šablona (`copy_only` přes `IFixPublisher.CanPublish(..., IsTemplate)`): návrh skupiny druhu `template` nebo blok bez indexu (rám stránky).
- Opakovaný požadavek vrátí existující publikaci (stejný `idempotency_key`) bez nové úlohy; `old_value` ukládá až obsluha změny 15,
  API zapíše jen `old_value_hash`.
- Seznam publikací: nejnovější první, kurzor = id (UUIDv7), `limit` 1–100 jako ostatní seznamy.
- `rollback` ověřuje stejné předpoklady jako publikace (konektor, zápis, `IFixPublisher`, vlastnictví) a zakládá `publish.rollback` (P1, `io`).

### Skupina 9 (protokol)
- Umístění kvůli povolenému směru závislostí (Jobs nesmí na Application): `ProtocolDocument` (obsah) a `IPdfRenderer` jsou v
  `Jobs/Protocols` (ne v Application), `ProtocolRenderHandler` také; `ProtocolDocumentBuilder`, `ProtocolNumberAllocator`,
  `ProtocolTexts` a `ProtocolService` v `Application/Protocols`.
- Obsah protokolu se sestaví už při žádosti (stav ke dni vystavení) a uloží jako JSON vedle budoucího PDF
  (`…/protocols/{number}.json`); worker jen vykresluje. Souhrn jde do `protocols.summary`.
- K rozhodnutí 6 (knihovna PDF) je otevřené: žádná implementace `IPdfRenderer` není registrovaná, úloha skončí
  `failed` s `pdf_renderer_unavailable` a upozorněním `protocol_failed`. Test s náhradním rendererem (`TextPdfRenderer`).
- Texty protokolu jsou oddíl `protocol:` v existujících `Protocols/Texts/{sk,cs}.yaml` (texty exportu CSV beze změny);
  zástupná třída `ProtocolTexts` z `DocumentTexts.cs` nahrazena.
- Řádek „Sledovanie“ z návrhu chybí: předplatné je změna 12. Část „(zákon č. … v znení …)“ za pravidly chybí: sady pravidel
  citaci zákona v datech nemají (nevymýšlet). Body zákona v tabulce jsou `legal_refs[].ref` verdiktů (jazyk zákona).
- Navíc proti návrhu: řádek „Nekontrolované“ s důvody a počty (fail-closed), řádek „Krajiny kontroly“, v souhrnu i
  „schválených, čaká na zverejnenie“; „ponechaných rozhodnutím prevádzkovateľa“ = kept + dismissed.
- Rozhodnutí v tabulce: platné záznamy paměti rozhodnutí vzniklé v období (jeden řádek na větu, hromadné s počtem stránek
  a „(hromadne)“, zveřejnění podle `publications`), u nálezů bez vlastní věty poslední změna stavu z auditu v období.
- `ProtocolNumberAllocatorTests` a `ProtocolDocumentBuilderTests` jsou v `Api.Tests/Findings/ProtocolTests.cs` (potřebují
  PostgreSQL a `BylinkovoSeed`, které Application.Tests nemá).

### Skupina 11 (běhy a SSE)
- `RunEventStream` otevře spojení `LISTEN` až s prvním odběratelem (ne při startu API) a po výpadku znovu s rostoucí pauzou
  (1–30 s). Proud před snímkem počká (nejvýš 5 s), až spojení poslouchá, aby se mezi snímkem a prvním oznámením nic neztratilo.
- Proud běhu při každém signálu i při každém pingu (15 s) přečte z databáze nové `run_events` a stav běhu; ztracené oznámení
  tak jen zpozdí. `progress` a `status` se posílají, jen když se změnily; `run_event` po dávkách 500.
- Bez `Last-Event-ID` pošle proud po snímku všechny dosavadní události běhu.
- Proud e-shopu (`GET S/events`) začne událostí `ready`; pak události pojmenované podle entity (`proposal`, `group`,
  `publication`, `question`, `run`) s `{ entity, id, status, recheckStatus }` přečtenými pod RLS.
- `RunDto` má navíc `cancelRequested`; `errorCode` je `runs.error` (kód ze změny 8).
- Zrušení běhu zapisuje audit `run.cancel_requested`.
- Strop spojení platí na instanci API (počítadlo v paměti).

### Skupina 12 (ověření)
- `FindingsRoleAndIsolationTests`: objekty tenanta B (e-shop, stránka, nález, návrh, skupina, otázka, doklad, publikace,
  protokol, běh, upozornění) pod adresou tenanta A vrátí `404` na každém koncovém bodu této změny (štítky `fixes`, `evidence`,
  `runs`); koncové body změn 9 a 10 smějí prázdné tělo odmítnout dřív (`400`), nikdy neuspějí. Řádky B se nezmění (otisk
  řádků přes všechny dotčené tabulky). Role všech koncových bodů hlídá `RoleMatrixTests`.
- `FindingsFlowTests` (12.4 a 12.5) prochází celý tok v API; výsledky kontroly Jevem zapíše test tak, jak je zapisuje worker
  (úlohy `fix.recheck` a `fix.generate_for_answer` mají vlastní testy ve `Jobs.Tests` s falešnými klienty). Logy celého toku
  neobsahují texty nálezů, návrhů, údajů ani obsah dokladu.
- 12.6 (živá kontrola s placeným Jevem a OpenAI) se nespustila: čeká na odhad ceny a souhlas uživatele.
