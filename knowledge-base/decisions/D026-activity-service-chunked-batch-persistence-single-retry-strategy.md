---
id: D026
chosen_option: "Bounded Two-Pass Chunked Persistence Adapter with Execution-Strategy-Only Retry (Layered/Hexagonal primary, Event-Driven-informed backstop)"
problem_id: P021
tags:
  - ef-core
  - mssql
  - command-timeout
  - batch-processing
  - retry-policy
  - integration-events
  - dotnet
  - rabbitmq
related_snippets: [S026]
lenses_evaluated:
  - Layered Architecture
  - Event-Driven Architecture
confidence: high
---

## Context

`activity-service` intermittently fails to persist `ProcessActivity` / `ProcessActivityDependency`
batches when `ActivityGenerator.SaveProcessActivityV2Async` issues one unbounded `SaveChangesAsync()`
call (25 entities, 331 params, two batched `MERGE` statements) that exceeds the 30-second
`CommandTimeout`. Source-grounded investigation (P021) found this call is also wrapped by two
independent retry layers -- EF Core's `EnableRetryOnFailure(3, 5s)` and an outer application-level
`DoTransactionAsyncWithRetryPolicy` (`catch (DbException)`, 3 retries, no backoff) -- that can
resubmit the identical oversized command up to 9 times before failing.

## Options Considered

### Lens A: Layered Architecture -- Bounded Two-Pass Batch Persistence Adapter
Chunk `SaveProcessActivityV2Async` into bounded batches (reusing the two-pass FK-safe batch commit
pattern already validated in D008/S008: all `ProcessActivity` chunks committed first, then
`ProcessActivityDependency` chunks referencing already-inserted `ProcessActivityCode`s), and collapse
the two stacked retry layers into a single execution-strategy-aware retry
(`context.Database.CreateExecutionStrategy().ExecuteAsync(...)`), removing the ad hoc
`DoTransactionAsyncWithRetryPolicy` loop entirely.
Pros: directly bounds the exact 331-param/25-row footprint that produced the 30,003ms timeout;
eliminates the up-to-9x retry-amplification anti-pattern; reuses an already-validated KB pattern
(D008/S008); confined to `ActivityGenerator` and the DI retry config, no topology or contract change;
MERGE's existing idempotency makes re-running a smaller chunk on retry safe.
Cons: does not eliminate lock contention from many concurrent consumer threads under sustained peak
load, only bounds each transaction's footprint; splitting one atomic SaveChanges into multiple round
trips loses per-event all-or-nothing atomicity, requiring RabbitMQ redelivery + MERGE idempotency to
correctly complete a partially-persisted event on retry; adds FK-ordering complexity (dependency rows
must follow their parent activity rows).

### Lens B: Event-Driven Architecture -- Chunked Fan-Out via Bounded Activity-Group Sub-Events
Decompose `ActivityCreateV2EventHandler` to publish one sub-event per activity group (or bounded
group of N) back onto RabbitMQ, each consumed independently and persisted via its own small,
bounded `SaveChangesAsync`, letting RabbitMQ's native redelivery/DLQ own failure recovery instead of
an in-process retry loop (extends the P016/D021 Hexagonal RabbitMQ-consumer precedent).
Pros: makes an oversized single-call batch structurally impossible, not just unlikely; each chunk is
independently retryable/scalable via existing RabbitMQ mechanics; isolates a slow/failing chunk from
the rest of the order's activity generation.
Cons: loses the current single-call consistency of "all activities for this order created together"
-- downstream consumers of `ProcessActivityStartEvent` could observe partial completion, and
`GenerateDependency()`/`AddGroupDependency()` today assume all groups exist before dependency edges
are wired, requiring new cross-event sequencing; materially higher implementation effort and rollout
risk on a live production order-processing path; does not, by itself, fix the double-retry-stacking
anti-pattern for whatever chunk size is chosen.

## Decision

Adopt Lens A (Layered/Hexagonal bounded batch persistence adapter) as the primary and immediate fix,
with one principle borrowed from the Lens B analysis: once the ad hoc `DoTransactionAsyncWithRetryPolicy`
loop is removed, a chunk that still fails after the single execution-strategy retry should be allowed
to throw and rely on RabbitMQ's own redelivery/DLQ semantics for final-failure recovery -- consistent
with the D024/P016 precedent already established in this system -- rather than reinventing an
in-process retry loop for the smaller chunks.

- Chunk `SaveProcessActivityV2Async` writes using the two-pass FK-safe pattern (D008/S008): persist
  `ProcessActivity` rows in bounded batches (e.g. max 5 rows / ~95 params per `SaveChangesAsync`,
  well under any practical MERGE/lock-footprint concern), then persist `ProcessActivityDependency`
  rows in bounded batches once their parent `ProcessActivityCode`s are confirmed committed.
- Delete `DoTransactionAsyncWithRetryPolicy`'s ad hoc `catch (DbException)` loop around
  `SaveChangesAsync`; rely solely on the already-configured EF Core `EnableRetryOnFailure(3, 5s)`
  execution strategy (invoked correctly via `CreateExecutionStrategy().ExecuteAsync(...)` per EF Core
  guidance for combining custom logic with a retrying strategy) as the single source of retry truth.
- Let a chunk that exhausts its execution-strategy retries throw; the RabbitMQ consumer's existing
  nack/redelivery handling becomes the outer safety net, matching how P016/D021's Hexagonal RabbitMQ
  consumer already treats final-failure recovery in this codebase.
- Preserve the existing `MERGE ... WHEN NOT MATCHED` idempotent-upsert semantics unchanged --
  chunking does not require touching the upsert logic itself, only how many rows are submitted per
  `SaveChangesAsync` call.

### Rejected Options

- Event-Driven fan-out (Lens B) as the immediate fix: rejected as the primary move because it
  introduces a new partial-completion consistency gap (some activity groups persisted, others still
  in flight) that does not exist today, requires redesigning `GenerateDependency()`/`AddGroupDependency()`
  sequencing across events, and carries materially higher implementation and rollout risk on a live
  production order-processing path -- disproportionate to a problem whose primary cause (stacked
  retry policies re-submitting one oversized command) can be fixed with a confined, low-risk adapter
  change. It is retained as a documented future option if catalog complexity grows enough that even
  bounded chunked writes start approaching the timeout ceiling.
- Leaving both retry layers in place and only widening `CommandTimeout`: not proposed as a serious
  option -- it would mask, not fix, the retry-amplification anti-pattern (still up to 9x resubmission
  of an oversized command) and would only delay the next occurrence at a higher catalog-complexity
  threshold.

## Borrowed Insights

From the Event-Driven analysis: RabbitMQ redelivery/DLQ (already the pattern for this system's other
consumers per P016/D021) is retained as the correct backstop for genuine, non-transient failures --
once the internal ad hoc retry loop is removed, we deliberately do not replace it with another
in-process loop. The activity-group chunk boundaries chosen for the two-pass batching are also the
natural seam if a future migration to true per-group async fan-out (Lens B) becomes necessary as
catalog complexity grows. From the Layered analysis: the two-pass FK-safe commit ordering (D008/S008)
is reused directly rather than inventing a new chunking mechanism, and MERGE's existing idempotency
is what makes chunked, potentially-partial retries safe without new duplicate-key risk.

## Consequences

Benefits: removes the up-to-9x retry-amplification anti-pattern; bounds every future `SaveChangesAsync`
call's row/parameter footprint regardless of activity-catalog complexity; reuses a proven KB pattern
(D008/S008) instead of a novel mechanism; confined blast radius (one generator class + one DI retry
config change) satisfies the "no contract change, no new infra, zero-downtime" constraints.

Trade-offs Accepted:
- Loses per-event all-or-nothing atomicity across the full activity-catalog fan-out; a chunk failure
  after other chunks succeeded relies on MERGE idempotency + RabbitMQ redelivery to complete the
  remainder correctly on retry -- this must be explicitly tested, not assumed.
- Does not address concurrent-pod/thread lock contention directly -- if sustained peak load across
  many `ActivityPreProcess/N` workers is itself the dominant driver (not just single-event batch
  size), chunking reduces but does not eliminate contention risk; SQL Server wait-stats/DMV data
  (currently missing) should confirm this before ruling it out entirely.
- Defers the more structurally robust Event-Driven fan-out option; if activity catalogs grow
  significantly in complexity, this decision will need revisiting.

## Next Steps

1. Immediate: Implement two-pass bounded-chunk persistence in `ActivityGenerator.SaveProcessActivityV2Async`
   (max ~5 `ProcessActivity` rows per `SaveChangesAsync`, then chunked `ProcessActivityDependency`
   writes referencing committed parent codes), reusing the D008/S008 two-pass FK-safe pattern.
2. Immediate: Remove `DoTransactionAsyncWithRetryPolicy`'s ad hoc `catch (DbException)` loop; wrap
   each chunked `SaveChangesAsync` in `context.Database.CreateExecutionStrategy().ExecuteAsync(...)`
   so only the already-configured `EnableRetryOnFailure(3, 5s)` strategy owns retries.
3. Immediate: Let exhausted-retry failures propagate to the RabbitMQ consumer's existing
   nack/redelivery path rather than adding a new in-process retry loop.
4. Sprint: Instrument `SaveProcessActivityV2Async` with a metric/log for batch size (row count,
   param count) per call to establish the real-world activity-catalog complexity distribution and
   set an evidence-based chunk size rather than a guessed constant.
5. Sprint: Pull SQL Server wait-stats/DMV data (`sys.dm_exec_requests`, `sys.dm_os_wait_stats`) for
   comparable load windows to confirm whether concurrent-pod lock contention remains a factor after
   chunking ships, which would indicate the Event-Driven fan-out option should be revisited sooner.
6. Backlog: If catalog complexity or event-arrival concurrency keeps growing after chunking is
   deployed, revisit the deferred Event-Driven per-activity-group fan-out option (Lens B) as the
   next structural step, using the chunk boundaries established here as the natural seam.

## KB References

- P019 / D024 / S024 -- validate-service SQL timeout + duplicate-key on retry (same broader EF
  Core/SQL Server write-path reliability family; different root cause -- missing idempotency + tight
  timeout there, vs unbounded batch + stacked retry policies here; this service already has the
  idempotent MERGE half of that fix)
- P008 / D008 / S008 -- Two-pass FK-safe batch commit (chunking pattern reused directly here)
- P016 / D021 / S021 -- RabbitMQ Hexagonal consumer with redelivery/DLQ as failure backstop (pattern
  reused as the post-removal retry backstop instead of a new in-process loop)
- P005 / D005 -- ETL batch-size + ChangeTracker discipline (related unbounded-batch precedent, OOM
  rather than timeout, same "bound the batch" family of fix)
