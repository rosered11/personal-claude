# Architecture Transition Roadmap

Auto-maintained by `career-mentor` agent. Tracks architectural concepts encountered across consultations.

---

## Exposure Log

| Date | Problem | Concepts Encountered | Lenses | KB IDs |
|------|---------|---------------------|--------|--------|
| 2026-06-10 | DataMart RabbitMQ Activity Log Ingestion | IHostedService lifecycle, BackgroundService scoping, IServiceScopeFactory, RabbitMQ.Client consumer (AsyncEventingBasicConsumer, BasicQos, BasicAck/Nack, DLX), Hexagonal adapter pattern (inbound/outbound ports), EDA at-least-once delivery, idempotency (unique partial index), manual DTO mapping | Hexagonal Architecture, Event-Driven Architecture | P016, D021, S021 |
| 2026-07-22 | OMS Shared-Schema Database Load Risk From BU Growth | CQRS (read/write model split, read replicas, materialized/projection read models), Domain-Driven Design as tenant-partitioning (bounded-context data ownership, schema-per-tenant sharding), cache-aside pattern, multi-tenancy strategies (shared-schema discriminator column vs schema-per-tenant vs database-per-tenant), phased/evidence-driven architecture decisions (defer high-cost option behind an instrumentation trigger) | CQRS, Domain-Driven Design | P020, D025, S025 |
| 2026-07-22 | Activity Service Batched EF Core MERGE Timeout Under Stacked Retry Policies | EF Core SaveChanges auto-batching into multi-row MERGE statements, retry-amplification from stacking EnableRetryOnFailure with a custom app-level retry loop, EF Core Execution Strategy pattern (CreateExecutionStrategy().ExecuteAsync) as the correct way to combine custom logic with a retrying strategy, two-pass FK-safe batch chunking (reused), message-bus-owned failure recovery vs in-process retry loops, source-code-grounded root cause investigation (not just log reading) | Layered Architecture, Event-Driven Architecture | P021, D026, S026 |
| 2026-07-22 | Suspicious Audit-Logged Self-Insert Traced to Online Index Rebuild Job | SQL Server ONLINE index rebuild internals (legacy numeric table hints, clustered-index scan-and-populate mechanics), root-cause hypothesis ranking under uncertainty (5 Whys applied to a forensic/diagnostic question rather than a design question), audit-log/security false-positive triage, session/login correlation as a verification technique, fragmentation-gated vs unconditional maintenance scheduling, config-driven schedules replacing hardcoded branches as a defect-elimination technique | Layered Architecture, Event-Driven Architecture | P022, D027, S027 |
| 2026-07-29 | TaskIndexRebuild Execution Causes Production SQL Timeout Spikes (revised, grounded in verified script) | SQL Server WAIT_AT_LOW_PRIORITY (ABORT_AFTER_WAIT) as a load-yielding lock-acquisition mechanism for ONLINE index rebuilds, RESUMABLE = ON for large index rebuilds, off-peak execution-window guards as a self-limiting scheduling technique, distinguishing "which work is selected" (fragmentation gate) from "how work is paced" (throttling/yielding) as separate architectural concerns within the same maintenance job, evaluating whether to extend vs. replace a very recent prior decision (D027) on the same object, distinguishing "a design decision was made" from "a design decision was actually deployed" as separate facts requiring separate verification, programmatic extraction/verification of a real script's contents (regex over 195 statements) as a more rigorous grounding technique than visual inspection | Layered Architecture, Event-Driven Architecture | P023, D028, S028 |

---

## Learning Recommendations

### Active Study Items (from P016 consultation — 2026-06-10)

1. **IHostedService + IServiceScopeFactory pattern in .NET 8**
   - A BackgroundService is registered as a Singleton. EF Core DbContext is Scoped. The only safe bridge is IServiceScopeFactory.CreateScope() per unit of work (per message here). This is a required pattern any time you call Scoped services from long-lived background workers.
   - Study: Microsoft docs on BackgroundService + DI scope management

2. **RabbitMQ.Client v6+ AsyncEventingBasicConsumer**
   - Low-level .NET RabbitMQ client. Key mechanics: DispatchConsumersAsync=true, BasicQos prefetchCount for backpressure, BasicAck/BasicNack for manual acknowledgment, x-dead-letter-exchange argument for DLQ routing, AutomaticRecoveryEnabled for broker reconnect.
   - Study: RabbitMQ.Client official samples + AMQP 0-9-1 model concepts

3. **Hexagonal Architecture (Ports and Adapters) in .NET**
   - Inbound adapter (consumer) calls an application port (interface). Outbound adapter (repository) implements a driven port. The application service coordinates between ports without knowing any infrastructure. Applied here: RabbitMqActivityLogConsumer (inbound) -> IActivityLogService (port) -> ActivityLogService -> IActivityLogRepository (driven port) -> ActivityLogRepository (EF Core).
   - Study: Alistair Cockburn original Hexagonal Architecture paper; Mark Seemann on composition root

4. **EDA Operational Safety: Idempotency + Dead-Letter + Backpressure**
   - At-least-once delivery means your consumer WILL see duplicate messages on redelivery. Idempotency guard (unique index on a natural key like TransactionID) is the correct defense. Dead-letter exchange prevents a single bad message from blocking the queue forever. prefetchCount limits consumer-side memory pressure.
   - Study: RabbitMQ DLX documentation; Martin Fowler on idempotent receiver

### Consultation 2 — OMS Shared-Schema Database Load Risk From BU Growth — 2026-07-22
**Concepts encountered:** CQRS (read/write split as a scaling tool, not just a testing/purity
concern), read replicas + cache-aside as a read-path scaling lever, multi-tenancy data-isolation
models (shared-schema/discriminator-column vs schema-per-tenant vs database-per-tenant) as a DDD
bounded-context/aggregate-boundary decision, and phased decision-making that defers a
higher-cost option behind a concrete instrumentation trigger instead of guessing upfront.
**Recommended study (priority order):**
1. **Multi-tenancy data models (shared schema vs schema-per-tenant vs DB-per-tenant)** -- this is
   the concept with the highest leverage right now: P020/D025 encountered it directly (BuCode
   discriminator column today, schema-per-BU-tier deferred as a later option). Study the tradeoffs
   of each model along isolation, operational cost, and migration-between-models difficulty --
   this recurs in almost every real multi-tenant SaaS-style system and is a first encounter here.
2. **CQRS as a scaling pattern, not just a modeling pattern** -- this is the second time CQRS has
   appeared in this KB (first: P013/D018 greenfield OMS design), but the first time it was chosen
   specifically to solve a *load/scaling* problem via read replicas + projections rather than as a
   default DDD-adjacent modeling choice. Study read-replica lag/consistency tradeoffs and
   Outbox-fed projection/materialized-view patterns specifically.
3. **Cache-aside pattern with existing infrastructure** -- straightforward but worth deliberate
   practice: study cache invalidation strategies (tag-based, as `ICacheService.RemoveByTagAsync`
   already supports) and how to reason about staleness windows for different read types (config
   lookups vs dashboard aggregates).
4. **Evidence-driven / phased architecture decisions** -- a decision-making pattern worth studying
   explicitly: D025 deliberately deferred the higher-cost option (schema-per-BU-tier) behind a
   cheap instrumentation signal (per-BU write volume) rather than committing upfront without load
   data. This "instrument first, commit later" sequencing is a reusable technique for any decision
   blocked on missing production data -- study how to design a minimal, cheap "tripwire" metric for
   other pending architecture bets in this system (e.g. Outbox backlog lag, mentioned as a gap in
   both this consultation and P018).

### Consultation 3 -- Activity Service Batched EF Core MERGE Timeout Under Stacked Retry Policies -- 2026-07-22
**Concepts encountered:** EF Core's automatic multi-row SaveChanges batching into single MERGE
commands (and how batch size/param count scale with entity count, not a fixed constant), the
retry-amplification anti-pattern that results from stacking two independent retry layers
(EF Core's `EnableRetryOnFailure` execution strategy plus a hand-written `catch (DbException)` retry
loop) on the same operation, the EF Core Execution Strategy pattern
(`context.Database.CreateExecutionStrategy().ExecuteAsync(...)`) as the documented correct way to
combine custom transactional logic with a retrying strategy, a second application of the two-pass
FK-safe batch-chunking pattern (first seen in P008/D008), and the principle of letting a message
bus's own redelivery/DLQ mechanics own final-failure recovery instead of duplicating that logic as
an in-process retry loop.
**Recommended study (priority order):**
1. **Retry-policy composition and retry-amplification** -- this is the highest-value new concept
   from this consultation and a genuinely easy mistake to make: EF Core's `EnableRetryOnFailure`
   already retries transient failures internally, so any additional retry loop wrapped around
   `SaveChangesAsync` compounds rather than helps, and can turn one slow write into many repeated
   full-timeout attempts against the same contended rows. Study the EF Core docs section
   "Connection Resiliency" and the explicit warning against combining custom retry logic with a
   configured execution strategy -- this pattern likely exists elsewhere in the codebase and is
   worth an audit.
2. **EF Core Execution Strategy pattern** -- understand why `CreateExecutionStrategy().ExecuteAsync(...)`
   exists (it ensures retries replay the *entire* unit of work, including any explicit transaction,
   not just the final SQL command) and why bypassing it with ad hoc try/catch loops silently breaks
   that guarantee. Study `IExecutionStrategy` and `SqlServerRetryingExecutionStrategy` source/docs.
3. **Batch-sizing as a first-class design decision, not a default to accept** -- this is the second
   time an unbounded EF Core batch has appeared in this KB (first: P005's ChangeTracker OOM risk;
   now: P021's CommandTimeout from an unbounded MERGE). Practice recognizing "loop that calls
   `.Add()` N times then one `SaveChangesAsync()` at the end" as a code smell worth chunking by
   default in any write path whose N is driven by external input (event fan-out, file size, etc.)
   rather than a small fixed constant.
4. **Source-grounded root cause investigation** -- this consultation's root cause (the stacked
   retry layers) was only discoverable by reading the actual `Startup.cs` and `ActivityGenerator.cs`
   source, not from the log alone. Reinforces a pattern already noted from the Sprint-OMS
   consultations: when an inbox problem points at a real external codebase, grounding the
   investigation in the actual source (DI configuration, retry wrappers, adapter code) before
   proposing a fix catches root causes that log-only analysis would miss or misdiagnose.

### Consultation 4 -- Suspicious Audit-Logged Self-Insert Traced to Online Index Rebuild Job -- 2026-07-22
**Concepts encountered:** This consultation was a *diagnostic/forensic* question rather than a
greenfield design question -- a useful contrast to the previous three, and worth noticing as its
own skill: SQL Server ONLINE index rebuild internals (why `ALTER INDEX ... REBUILD WITH (ONLINE = ON)`
against a clustered index generates an internal scan-and-populate that can surface in an audit trail
as a self-referencing `INSERT...SELECT` with a legacy `WITH (INDEX = 1)` numeric hint), ranked
root-cause-hypothesis reasoning applied to security/audit triage (distinguishing "very likely benign
engine artifact" from "confirmed benign" -- and naming the exact verification query that would close
the gap, rather than asserting certainty without evidence), and a maintenance-scheduling anti-pattern
(unconditional, hardcoded day-of-week index rebuilds instead of fragmentation-gated, config-driven
scheduling) as a root contributor to audit-log noise volume, not just a missing-correlation problem.
**Recommended study (priority order):**
1. **SQL Server ONLINE index operations internals** -- first encounter with SQL Server storage-engine
   internals in this KB (prior mssql entries were all EF Core *application*-layer problems: P010, P019,
   P021). Study how ONLINE index rebuilds use row-versioning and a background scan-and-copy to keep the
   table available for concurrent DML, and why this can produce plan-cache/audit entries that look like
   ordinary T-SQL even though no application code issued them. This is foundational for correctly
   triaging any future "why does the audit/plan cache show this weird statement" question.
2. **Root-cause reasoning under incomplete evidence (forensic vs. design questions)** -- this
   consultation differed from the prior three in kind: there was no architecture to *choose*, only a
   claim to *verify*. Practice explicitly separating "most likely explanation, stated with the specific
   evidence that would confirm it" from "confirmed fact" -- and always naming the cheapest verification
   step (here: comparing audit `session_id`/`login_name` against the SQL Agent job's session) instead of
   stopping at a plausible-sounding narrative. This is a core habit for both security incident response
   and production debugging generally, and is a different skill from the design-tradeoff reasoning
   practiced in Consultations 1-3.
3. **Fragmentation-gated maintenance scheduling (`sys.dm_db_index_physical_stats`)** -- the discovery
   that `TaskIndexRebuild` rebuilds ~190 indexes unconditionally every week (including a duplicate
   `PK_StoreLocation` entry on two different days) regardless of actual fragmentation is a concrete,
   reusable DBA/observability pattern: unconditional maintenance schedules both waste resources and
   inflate audit-log noise, while conditional (fragmentation-threshold-gated) scheduling addresses the
   root cause of the noise rather than only adding correlation metadata around it. Study
   `sys.dm_db_index_physical_stats` and standard SQL Server index-maintenance best practices (e.g. Ola
   Hallengren's maintenance solution) as the reference implementation of this pattern.
4. **Config-driven schedules as defect elimination, not just tidiness** -- the `PK_StoreLocation`
   double-rebuild bug is a small but clear example of a broader principle already touched in
   Consultation 3 (batch-sizing as a first-class decision): replacing copy-pasted branching logic with
   a data-driven table (unique-keyed, so a duplicate becomes a constraint violation instead of a silent
   bug) eliminates an entire class of defects by construction. Practice spotting this pattern in other
   hardcoded branch/list structures encountered in this codebase.

### Consultation 5 -- TaskIndexRebuild Execution Causes Production SQL Timeout Spikes -- 2026-07-29
**Concepts encountered:** This consultation is a direct sequel to Consultation 4 on the *same*
stored procedure (`TaskIndexRebuild`), which is itself a useful pattern to notice: a single
production object can have multiple, genuinely distinct architectural problems layered on top of
each other over time (first: audit-attribution/noise-volume; now: execution-time load pacing), and
each deserves its own problem/decision record rather than being folded into the prior one just
because they touch the same code. New technical concepts: SQL Server's `WAIT_AT_LOW_PRIORITY`
option on `ALTER INDEX ... REBUILD` (a purpose-built mechanism letting a maintenance operation's
brief final schema-lock acquisition yield to live OLTP sessions instead of blocking them --
directly different from just reducing I/O volume), `RESUMABLE = ON` for large index rebuilds
(page-level checkpointing so an interrupted rebuild doesn't restart from zero), and using an
existing fragmentation gate as *free* cross-run resume state (a deferred/aborted candidate simply
stays fragmented and is re-selected next run, no separate checkpoint table needed). Also a source
hygiene lesson: two user-supplied reference files turned out to be byte-for-byte duplicates of a
plain schema export with no actual rebuild logic in either, requiring an explicit check-in with the
user before proceeding rather than fabricating a plausible-looking "old script" from assumptions.
**Recommended study (priority order):**
1. **SQL Server lock-priority and yielding mechanisms (`WAIT_AT_LOW_PRIORITY`, blocking vs.
   yielding operations)** -- the highest-value new concept here: most engineers reach for "reduce
   batch size" or "add a delay" to fix a maintenance-job timeout problem, but the actual documented
   root cause in cases like this is lock-acquisition *ordering* at the end of an ONLINE operation,
   which has a dedicated engine feature rather than needing a workaround. Study the `ALTER INDEX`
   documentation section on `WAIT_AT_LOW_PRIORITY` and `ABORT_AFTER_WAIT`, and more generally, the
   distinction between "operations that block" and "operations that can be told to yield."
2. **Distinguishing "what work is selected" from "how work is paced" as separate architectural
   concerns** -- D027 solved the *selection* problem (fragmentation gate) but this consultation
   shows that selection and pacing are genuinely independent axes of the same job, and fixing one
   does not imply the other is fixed. Practice explicitly asking, for any batch/maintenance job,
   "what determines which work gets done?" and "what determines how fast/aggressively it gets
   done?" as two separate design questions.
3. **Deciding whether to extend or replace a very recent prior decision** -- this consultation had
   to explicitly reason about whether the new fix should sit alongside D027 (extend) or supersede it
   (replace), given both touch the exact same stored procedure within the same week. Practice this
   judgment call explicitly: extend when the prior decision's mechanism (fragmentation gate, RunId
   logging) is still valid and orthogonal to the new problem; replace only when the new fix
   genuinely contradicts or subsumes the old one.
4. **Verifying user-supplied reference material before building on it** -- both `script-rebuild.sql`
   and `schema-database.sql` turned out to be identical plain schema exports with zero rebuild logic,
   which would have produced a fabricated or misgrounded "old script" analysis if taken at face
   value. Reinforces a pattern already touched in Consultation 3 (source-grounded investigation):
   always verify that a referenced file actually contains what it's claimed to contain before using
   it as the basis of an architectural decision, and surface the discrepancy to the user rather than
   silently guessing.

**Update -- 2026-07-29, same day, second submission:** A follow-up inbox submission
(`inbox/rebuild-index-db/`) finally supplied the real `script-rebuild.sql` that item 4 above
correctly refused to fabricate a conclusion from. Two things followed: first, full confirmation
that the file matches the TaskIndexRebuild body documented in P022 byte-for-byte (verified
programmatically, not just by eye: 195 `ALTER INDEX` statements, 194 unique candidates, exactly
one duplicate -- `PK_StoreLocation` -- matching the earlier finding exactly). Second, and more
consequential: it revealed that *neither* D027's nor D028's design had actually been deployed to
production -- the original consultation had assumed the D027/S027 baseline was already live and
scoped D028 as a pure add-on to that assumption. This is a distinct, higher-value lesson from item
4: verifying a *reference file's contents* catches fabrication risk, but this also surfaced a
second, easier-to-miss failure mode -- assuming a *previously decided* fix is already deployed,
when "decided" and "deployed" are separate facts that need separate verification. P023/D028/S028
were revised in place (not superseded by a new record) once kb-search scored the new submission at
~0.89 tag-overlap against the existing P023, exercising the KB-writer UPDATE path for the first
time in this roadmap's history (all five prior new-information consultations had scored below the
0.8 threshold and correctly became new CREATE records instead).
5. **"Decided" vs. "deployed" as separate facts** -- when reviewing or extending any prior
   architectural decision, explicitly check whether its recommended change was actually shipped, not
   just documented as chosen. A KB (or any design-decision log) records intent; it does not by
   itself prove production state. Practice asking "has this actually been deployed?" as a distinct
   verification step before building the *next* increment on top of an assumed-applied prior one --
   here, skipping that check meant D028's design was scoped as if a foundation existed that didn't.
6. **Programmatic verification over visual inspection at scale** -- extracting and counting the 195
   `ALTER INDEX` statements via a regex pass (rather than eyeballing a 250-line script) both
   confirmed the exact candidate list needed for a real deployment artifact and caught the
   duplicate with certainty rather than plausibility. Practice reaching for a quick extraction
   script instead of manual reading whenever a claim needs to hold across a large, repetitive,
   copy-pasted structure like this one.
