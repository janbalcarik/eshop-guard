# Delta for Job-queue

## ADDED Requirements

### Requirement: Zařazení úlohy v transakci
Úloha MUST vzniknout v `ops.jobs` ve stejné transakci jako data, která ji vyvolala (běh, událost konektoru). Při vrácení transakce MUST úloha nevzniknout a workery MUST NOT dostat oznámení. Úloha s `dedupe_key`, který už ve frontě je, MUST NOT vzniknout podruhé; volající MUST dostat identifikátor a stav existující úlohy.

#### Scenario: Běh a úloha spolu
- GIVEN transakce, která vloží `checks.runs` a zavolá `IJobQueue.EnqueueAsync` s `kind = test.record`
- WHEN se transakce potvrdí
- THEN v `ops.jobs` je úloha ve stavu `queued` s `attempts = 0` a `run_id` běhu
- AND worker poslouchající `eshopguard_jobs` dostane oznámení s druhem zdroje úlohy

#### Scenario: Vrácená transakce
- GIVEN stejná transakce
- WHEN se vrátí (`ROLLBACK`)
- THEN v `ops.jobs` žádná úloha není a žádné oznámení nepřišlo

#### Scenario: Stejný dedupe_key podruhé
- GIVEN úloha s `dedupe_key = monitor:{shop}:2026-10-05` už existuje
- WHEN se zařadí znovu se stejným klíčem
- THEN `EnqueueResult.Created = false` a `JobId` je identifikátor existující úlohy
- AND v `ops.jobs` je s tímto klíčem jen jeden řádek

#### Scenario: Zařazení mimo transakci
- GIVEN `EshopGuardDb` bez otevřené transakce
- WHEN se zavolá `EnqueueJobAsync`
- THEN vznikne `InvalidOperationException` s kódem `job.enqueue_requires_transaction` a nic se nezapíše

### Requirement: Převzetí úlohy bez dvojího zpracování
Worker MUST převzít úlohy jedním příkazem `UPDATE … WHERE id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING` jen pro svůj druh zdroje, v pořadí `priority, id`, jen úlohy s `not_before` v minulosti. Převzetí MUST nastavit `state = running`, zvýšit `attempts` a nastavit `lease_owner` a `lease_until`. Stejnou úlohu MUST NOT převzít dva workery zároveň.

#### Scenario: Čtyři workery a 1 000 úloh
- GIVEN 1 000 úloh `test.record` ve frontě a 4 workery se 8 sloty `cpu`
- WHEN workery zpracují celou frontu
- THEN tabulka účinků obsahuje 1 000 řádků s 1 000 různými `job_id`
- AND všech 1 000 úloh je `succeeded` s `attempts = 1`

#### Scenario: Pořadí podle priority
- GIVEN starší úloha P2 a novější úloha P0 stejného druhu
- WHEN worker s jedním volným slotem převezme úlohu
- THEN převezme úlohu P0; mezi úlohami stejné priority starší (`id`) dřív

#### Scenario: Úloha naplánovaná do budoucna
- GIVEN úloha s `not_before` za 10 minut
- WHEN worker převezme úlohy
- THEN tuto úlohu nepřevezme

### Requirement: Lease, heartbeat a fencing
Převzatá úloha MUST mít lease na `Worker:LeaseSeconds` (120 s), který worker prodlužuje heartbeatem každých `Worker:HeartbeatSeconds` (30 s). Heartbeat, dokončení, selhání i odložení MUST platit jen s `lease_owner` = vlastní worker, `attempts` = číslo pokusu z převzetí a `state = running`. Dokončení MUST zamknout řádek úlohy před zápisem výsledků obsluhy a zapsat výsledky i stav v jedné transakci. Worker, který lease ztratil, MUST NOT zapsat výsledek.

#### Scenario: Zaseknutý worker nezapíše
- GIVEN worker A převezme úlohu (pokus 1) a jeho obsluha i heartbeat se zastaví déle než lease
- AND plánovač úlohu vrátí a worker B ji převezme (pokus 2) a dokončí
- WHEN obsluha workeru A pokračuje a zavolá `CompleteAsync`
- THEN `CompleteAsync` vyhodí `LeaseLostException` a transakce A se vrátí
- AND tabulka účinků obsahuje pro úlohu jen zápis workeru B a úloha je `succeeded` s `attempts = 2`

#### Scenario: Heartbeat po ztrátě leasu
- GIVEN úlohu workeru A mezitím převzal worker B
- WHEN worker A pošle heartbeat
- THEN heartbeat nic nezmění, vrátí `LeaseHeld = false` a obsluze A se zruší `CancellationToken`

#### Scenario: Dlouhá dávka s heartbeatem
- GIVEN obsluha, která běží 3× déle než lease a worker posílá heartbeat
- WHEN obsluha skončí
- THEN úloha je `succeeded` s `attempts = 1` (žádný jiný worker ji mezitím nepřevzal)

### Requirement: Vrácení po pádu workeru a opakování s odstupem
Úlohu s propadlým leasem MUST plánovač vrátit do stavu `queued` s `not_before` posunutým o rostoucí odstup, nebo ji označit `failed`, pokud vyčerpala `max_attempts`. Dočasná chyba obsluhy MUST vést k opakování s odstupem `BaseSeconds × 2^(pokus − 1)` (nejvýš `MaxSeconds`, rozptyl ±20 %); trvalá chyba nebo vyčerpané pokusy MUST vést ke stavu `failed` s kódem chyby v `last_error`. `last_error` MUST NOT obsahovat texty stránek ani klíče.

#### Scenario: Zabitý worker
- GIVEN worker A převezme úlohu a skončí bez uvolnění leasu (žádný heartbeat, žádné dokončení)
- WHEN uplyne lease a proběhne takt plánovače
- THEN úloha je `queued` s `last_error = job.lease_expired`
- AND worker B ji převezme a dokončí, úloha je `succeeded` s `attempts = 2` a tabulka účinků má jeden řádek

#### Scenario: Dočasná chyba
- GIVEN obsluha vrátí `JobResult.Retry("jev.http_503")` při pokusu 1 a 2
- WHEN se úloha znovu převezme
- THEN odstup před pokusem 3 je přibližně dvojnásobek odstupu před pokusem 2
- AND úloha nakonec skončí `succeeded`, když pokus 3 projde

#### Scenario: Vyčerpané pokusy
- GIVEN úloha s `max_attempts = 3`, jejíž obsluha vždy vrátí `Retry`
- WHEN proběhnou tři pokusy
- THEN úloha je `failed`, `finished_at` je vyplněné a `last_error` začíná kódem chyby

#### Scenario: Neznámý druh úlohy
- GIVEN úloha s `kind = run.unknown`, pro kterou worker nemá obsluhu
- WHEN ji worker převezme
- THEN úloha je hned `failed` s `last_error` začínajícím `job.unknown_kind`, bez dalších pokusů

### Requirement: Zrušení úloh
Čekající úlohu MUST jít zrušit (`state = canceled`). Zrušení běhu MUST zrušit všechny jeho čekající úlohy ve stejné transakci, ve které se nastaví `checks.runs.cancel_requested`. Běžící úloha zrušeného běhu MUST zrušení zjistit nejpozději při dalším heartbeatu, skončit stavem `canceled` a MUST NOT zařadit pokračování.

#### Scenario: Zrušení běhu s čekajícími úlohami
- GIVEN běh s 5 čekajícími úlohami
- WHEN se v jedné transakci nastaví `cancel_requested = true` a zavolá `CancelRunJobsAsync`
- THEN všech 5 úloh je `canceled` a žádný worker je nepřevezme

#### Scenario: Zrušení běžící úlohy
- GIVEN běžící úloha běhu, jejíž obsluha mezi dílčími dávkami kontroluje zrušení
- WHEN se běh zruší
- THEN obsluha do jednoho heartbeatu vrátí `JobResult.Canceled` a úloha je `canceled`
- AND pokus o zařazení pokračování vrátí `Created = false` a nic nevznikne

#### Scenario: Zrušení už běžící úlohy bez běhu
- GIVEN běžící úloha údržby bez `run_id`
- WHEN se zavolá `CancelAsync`
- THEN vrátí `false` a úloha doběhne

### Requirement: Odstranění duplicit a výlučný souběh
Úlohy se stejným `concurrency_key` MUST NOT běžet současně, ani na různých workerech. Databáze MUST tento stav vynucovat jedinečným indexem nad běžícími úlohami. Úlohy se stejným `concurrency_key` MUST jít na řadu v pořadí `priority, id`.

#### Scenario: Deset úloh se stejným klíčem
- GIVEN 10 úloh s `concurrency_key = run:R:rules` a 4 workery
- WHEN se zpracují
- THEN záznamy časů z obsluhy se nepřekrývají (nikdy neběží dvě současně)
- AND všech 10 je `succeeded`

#### Scenario: Souběžné převzetí dvou úloh se stejným klíčem
- GIVEN dva workery převezmou ve stejné chvíli dvě různé úlohy se stejným klíčem
- WHEN se potvrzují jejich transakce
- THEN jedna skončí chybou 23505 na indexu `ux_jobs_concurrency_running` a její úloha zůstane `queued`
- AND nejvýš jedna úloha s klíčem je `running`

### Requirement: Druhy zdrojů, sloty a priority
Každá úloha MUST mít druh zdroje (`fetch`, `cpu`, `jev`, `llm`, `io`, `system`) a prioritu 0–4 (P0 interaktivní … P4 údržba). Worker MUST mít pro každý druh vlastní počet slotů z konfigurace a MUST NOT převzít víc úloh daného druhu, než má volných slotů. Počet běžících úloh jednoho tenanta v jednom druhu MUST být omezen stropem `Worker:TenantCaps` (měkký strop při převzetí).

#### Scenario: Sloty po druzích
- GIVEN worker se sloty `cpu = 2` a `fetch = 10` a ve frontě 20 úloh každého druhu, které trvají 2 s
- WHEN worker běží 1 s
- THEN běží současně nejvýš 2 úlohy `cpu` a nejvýš 10 úloh `fetch`

#### Scenario: Strop tenanta
- GIVEN tenant A má ve frontě 50 úloh `jev`, tenant B 5 a strop `jev` je 2
- WHEN dva workery zpracovávají frontu
- THEN v žádném okamžiku neběží víc než 2 úlohy A (měřeno vzorkováním `ops.jobs` po 100 ms, odchylka nejvýš o počet workerů)
- AND první úloha B začne dřív, než skončí všechny úlohy A

#### Scenario: Neplatná konfigurace slotů
- GIVEN `Worker:HeartbeatSeconds` = 60 a `Worker:LeaseSeconds` = 120
- WHEN se worker spustí
- THEN start selže s kódem `config.worker_invalid` (heartbeat musí být pod třetinou leasu)

### Requirement: Pozastavení druhu úloh při selhání služby
Když obsluha zjistí došlý kredit nebo odmítnutý klíč externí služby, MUST pozastavit celý druh zdroje (`ops.system_settings`, klíč `jobs.paused_classes`) s kódem důvodu a vrátit úlohu do fronty bez započtení pokusu. Žádný worker MUST NOT převzít úlohu pozastaveného druhu, dokud se druh neobnoví. Pozastavení MUST být zapsané v logu jako chyba s kódem důvodu.

#### Scenario: Došlý kredit Jevu
- GIVEN obsluha úlohy `jev` vrátí `JobResult.PauseClass("jev.credit_exhausted")`
- WHEN worker výsledek zpracuje
- THEN `jobs.paused_classes` obsahuje `jev` s důvodem `jev.credit_exhausted`
- AND úloha je `queued` se stejným `attempts` jako před převzetím
- AND do obnovení žádný worker nepřevezme žádnou úlohu `jev`, úlohy `fetch` a `cpu` běží dál

#### Scenario: Obnovení
- GIVEN pozastavený druh `jev`
- WHEN se zavolá `ResumeResourceClassAsync(Jev)`
- THEN workery do 5 s znovu převezmou čekající úlohy `jev`

### Requirement: Zámky domén pro zdvořilé stahování
Stahování jedné domény MUST běžet nejvýš v jedné úloze najednou napříč všemi tenanty. Stahovací úloha MUST mít `concurrency_key = domain:{doména}` a MUST před stahováním získat zámek v `ops.domains` (`FOR UPDATE SKIP LOCKED`, `lease_until`), prodlužovat ho s heartbeatem a po dávce ho uvolnit se stavem zdvořilosti (tempo, Crawl-delay, robots.txt, chyby). Úlohy dvou běhů na stejnou doménu MUST se po dávkách střídat.

#### Scenario: Dva tenanti a stejná doména
- GIVEN běh tenanta A a běh tenanta B, každý s 5 dávkami stahování domény `shop.test`, a 2 workery
- WHEN se zpracují
- THEN intervaly stahování `shop.test` se nepřekrývají
- AND pořadí dávek je A, B, A, B … (žádný běh nedostane dvě dávky po sobě, dokud druhý čeká)

#### Scenario: Zámek drží jiná úloha
- GIVEN zámek `shop.test` drží úloha 1 s `lease_until` za 30 s
- WHEN úloha 2 zavolá `TryAcquireDomainAsync("shop.test", 2, …)`
- THEN dostane `null` a zámek zůstane úloze 1

#### Scenario: Propadlý zámek domény
- GIVEN úloha 1 držela zámek a její worker spadl
- WHEN uplyne `lease_until` a úloha 2 požádá o zámek
- THEN zámek dostane úloha 2 a stav zdvořilosti (`rate`, `crawl_delay_ms`, `robots_txt`) zůstane z poslední dávky

#### Scenario: Uvolnění cizího zámku
- GIVEN zámek drží úloha 2
- WHEN úloha 1 zavolá `ReleaseDomainAsync`
- THEN zámek zůstane úloze 2 beze změny

### Requirement: Globální limity volání s vyhrazeným podílem
Volání Jevu a OpenAI MUST čerpat tokeny ze sdílených bucketů v `ops.rate_limit_buckets`, rezervované pro celou dávku jedním příkazem `UPDATE … RETURNING` s doplňováním podle času. Úlohy P2–P4 MUST smět čerpat jen do `capacity × (1 − p0_p1_share)`, P0–P1 MUST smět čerpat vše. Rezervace z neexistujícího bucketu MUST skončit chybou, MUST NOT projít bez omezení.

#### Scenario: Vyhrazený podíl pro rychlé kontroly
- GIVEN bucket s kapacitou 100, plný, doplňováním 10/s a `p0_p1_share = 0.2`
- WHEN úloha P2 rezervuje 80 a hned potom další úloha P2 1 token
- THEN první rezervace projde, druhá vrátí `Denied` s `RetryAfter` > 0
- AND úloha P0 hned potom rezervuje 20 tokenů úspěšně

#### Scenario: Souběh rezervací
- GIVEN plný bucket s kapacitou 100, doplňováním 0, bez vyhrazeného podílu a 8 souběžných úloh P2, každá rezervuje 10 tokenů 5×
- WHEN rezervace proběhnou
- THEN počet úspěšných rezervací je přesně 10 a zůstatek je 0

#### Scenario: Chybějící bucket
- GIVEN bucket `openai` neexistuje
- WHEN se zavolá `TryReserveAsync("openai", 5, P2)`
- THEN vznikne `RateLimitBucketMissingException` s kódem `ratelimit.bucket_missing`

#### Scenario: Příliš velká dávka
- GIVEN bucket s kapacitou 100 a `p0_p1_share = 0.2`
- WHEN úloha P2 chce rezervovat 90 tokenů
- THEN vznikne chyba `ratelimit.batch_too_large` (dávka by se nikdy nesplnila)

### Requirement: Plánovač s jednou aktivní instancí
Plánovač MUST běžet uvnitř workeru a v každém taktu MUST pracovat jen instance, která získala zámek `pg_try_advisory_xact_lock`. Plánovač MUST vracet propadlé leasy, MUST každý den zařadit úlohu `system.ensure_partitions` (volá `PartitionMaintainer` ze změny 3) a úlohu `system.cleanup_jobs` s `dedupe_key` obsahujícím datum v UTC a MUST mazat záznamy workerů bez heartbeatu. Plánovač MUST NOT sám provádět dlouhou práci.

#### Scenario: Dva workery s plánovačem
- GIVEN dva workery se zapnutým plánovačem a taktem 0,5 s
- WHEN běží 10 s
- THEN v `ops.jobs` je pro dnešek právě jedna úloha `system.ensure_partitions` a jedna `system.cleanup_jobs`
- AND takty obou instancí se nikdy nepřekrývají (záznamy taktu v testu)

#### Scenario: Výpadek instance s plánovačem
- GIVEN plánovač workeru A provedl takt a worker A skončí
- WHEN uplyne jeden takt
- THEN takt provede worker B

#### Scenario: Úklid hotových úloh
- GIVEN úlohy `succeeded` a `canceled` starší než 7 dní a úloha `failed` stará 8 dní
- WHEN proběhne `system.cleanup_jobs`
- THEN úlohy `succeeded` a `canceled` jsou smazané a úloha `failed` zůstala (podle K rozhodnutí 3)

### Requirement: Korektní ukončení workeru
Worker MUST při ukončení (SIGTERM, Ctrl+C, nasazení) přestat brát nové úlohy, označit se v `ops.workers` jako `draining`, dokončit rozdělané úlohy nejvýš do `Worker:ShutdownSeconds` a úlohy, které nestihl, vrátit do fronty bez započtení pokusu. Potom MUST svůj záznam v `ops.workers` smazat. `ShutdownSeconds` MUST být menší než lease.

#### Scenario: Dávka se stihne
- GIVEN worker zpracovává úlohu, která skončí za 1 s, a ve frontě čekají další
- WHEN worker dostane požadavek na ukončení
- THEN rozdělaná úloha je `succeeded`, žádná další úloha není převzata tímto workerem a čekající úlohy zůstanou `queued`
- AND záznam workeru v `ops.workers` zmizí a log obsahuje `worker.stopped`

#### Scenario: Dávka se nestihne
- GIVEN úloha, která trvá déle než `ShutdownSeconds`
- WHEN worker dostane požadavek na ukončení
- THEN úloha je po `ShutdownSeconds` zpět `queued` se stejným `attempts` jako před převzetím a s `last_error = worker.shutdown`
- AND jiný worker ji později převezme a dokončí
