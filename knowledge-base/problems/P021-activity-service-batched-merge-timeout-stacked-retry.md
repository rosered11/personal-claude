---
id: P021
title: "Activity Service -- Batched EF Core MERGE Timeout Under Stacked Retry Policies"
date: 2026-07-22
tags:
  - ef-core
  - mssql
  - command-timeout
  - batch-processing
  - retry-policy
  - integration-events
  - dotnet
  - rabbitmq
related_decisions: [D026]
related_snippets: [S026]
---

# Activity Service -- Batched EF Core MERGE Timeout Under Stacked Retry Policies

## Problem

`activity-service` (`Activity.API`, Kubernetes pod `activity-service-68c5768f87-v8k48`, thread
`ActivityPreProcess/20`) fails to persist a batch of generated `ProcessActivity` and
`ProcessActivityDependency` rows when handling an `ActivityCreateEvent` via
`ActivityCreateV2EventHandler`. A single `DbContext.SaveChangesAsync()` call issued by
`ActivityGenerator.SaveProcessActivityV2Async` is batched by EF Core into one round trip
containing two `MERGE ... WHEN NOT MATCHED THEN INSERT ... OUTPUT INSERTED.[Id]` statements (14
rows / 19 params each into `[ProcessActivity]`, 11 rows / 6 params each into
`[ProcessActivityDependency]`, 331 SQL parameters total) against `Activity.Infrastructure.ActivityContext`
(SQL Server). The command ran for exactly `30,003ms` and failed with
`Microsoft.Data.SqlClient.SqlException (0x80131904): Execution Timeout Expired` (`CommandTimeout=30`),
surfaced as `DbUpdateException` from `SaveChangesAsync`.

## Root Cause

Grounded directly in the source tree at `D:\workspace\sprint-fm-v0\src\Services\Activity` (not just
the log):

1. **Two independent, mutually-unaware retry layers are stacked on the exact same call.**
   `Activity.API/Startup.cs:369-373` configures EF Core's built-in execution strategy with
   `sqlOptions.CommandTimeout(30)` and `sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5),
   errorNumbersToAdd: null)`. Separately, `ActivityGenerator.DoTransactionAsyncWithRetryPolicy<T>`
   (`ActivityGenerator.cs:440-466`) wraps the very same `_context.SaveChangesAsync()` call in an
   outer `catch (DbException ex)` loop that retries up to `maxRetries = 3` with **no backoff, no
   jitter, and no reduction in batch size between attempts** (`ActivityGenerator.cs:423`). Because
   `SaveChangesAsync()` already retries transient failures internally via the EF Core execution
   strategy before it ever throws, the outer application-level loop only engages after EF Core's own
   3 attempts are exhausted -- meaning a single failing batch can be resubmitted, byte-for-byte
   identical, up to 3 (EF Core) x 3 (application) = up to 9 times, each capped at the same 30-second
   `CommandTimeout`. This is a retry-amplification / self-inflicted-thundering-herd pattern: instead
   of backing off, the service repeatedly re-hammers the same lock-contended rows in
   `[ProcessActivity]` / `[ProcessActivityDependency]` with the identical oversized command, which
   can only make contention worse, not better. The exact `30,003ms` duration observed in the log (a
   full timeout exhaustion, not a marginal ~20ms overshoot) is consistent with this being deep into
   a retry sequence rather than a first-attempt borderline miss.

2. **The batch itself is unbounded by design.** `ActivityGenerator.SaveProcessActivityV2Async`
   loops over every `ProcessActivityViewModel` and `ProcessActivityDependency` produced by fanning
   out `@event.activityCatalog.ActivityGroups` for a single order event, calling
   `_context.ProcessActivity.Add(...)` / `_context.ProcessActivityDependency.Add(...)` for each one
   with no chunking or paging, then issuing exactly one `SaveChangesAsync()` at the end
   (`ActivityGenerator.cs:401,419,423`). The size (and therefore the lock footprint and duration) of
   every batch scales directly with the complexity of the activity catalog for that event -- there is
   no ceiling. Consumption happens via `EventBusRabbitMQMultiWorker` (`Startup.cs:256`,
   RabbitMQ, not Kafka), with many concurrent worker threads (`ActivityPreProcess/N`) processing
   activity-create events in parallel, so concurrent oversized batches against the same two hot
   tables is a realistic peak-load scenario, not just a single-event anomaly.

Two ranked, code-grounded hypotheses (not mutually exclusive):
- **(1) Primary:** The stacked EF-Core-execution-strategy + application-level retry loop turns one
  slow/contended write into repeated full-timeout attempts against the identical oversized command,
  compounding the very contention that caused the first failure, before finally surfacing to the
  caller.
- **(2) Compounding:** `SaveProcessActivityV2Async`'s batch size is unbounded and grows with activity
  catalog complexity, so larger catalogs (more activity groups / dependencies per order) push every
  call closer to, or past, the 30-second ceiling even without external contention.

Note: unlike P019/D024 (validate-service), this write path already uses idempotent
`MERGE ... WHEN NOT MATCHED` upserts rather than a blind `INSERT`, so duplicate-key collisions are
not the failure mode here -- this is purely a batch-sizing and retry-policy design problem.

## Context

- `Activity.API.Applications.IntegrationEvents.EventHandling.ActivityCreateV2EventHandler` consumes
  `ActivityCreateEvent` from RabbitMQ (`EventBusRabbitMQMultiWorker`, `Activity.API/Startup.cs`),
  builds `ProcessActivity` / `ProcessActivityDependency` view models via
  `Activity.API.Applications.Generators.ActivityGenerator`, and persists them through
  `Activity.Infrastructure.ActivityContext` (EF Core, SQL Server, `Microsoft.Data.SqlClient`).
- `Activity.API/Startup.cs:369-373`: `options.UseSqlServer(..., sqlOptions =>
  { sqlOptions.CommandTimeout(30); sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5),
  errorNumbersToAdd: null); })`.
- `ActivityGenerator.cs:440-466`: `DoTransactionAsyncWithRetryPolicy<T>` -- outer `catch (DbException)`
  retry loop, `maxRetries` passed as `3` at the call site (`ActivityGenerator.cs:423`), no backoff.
  Thread name `ActivityPreProcess/20` in the log implies a worker pool of at least 20 concurrent
  consumer threads.
- Related KB precedent: **P019/D024** (validate-service, same broader EF-Core/SQL-Server write-path
  reliability family) addressed a *different* failure signature in the same problem family
  (blind-INSERT duplicate-key + a too-tight 10s timeout with no idempotency guard). This service
  already has the idempotent-upsert half of that fix (MERGE) but has a distinct problem: an
  unbounded batch size combined with a previously-unnoticed doubled retry policy.
- [MISSING: production activity-catalog size distribution (how large do `ActivityGroups` fan-outs
  typically get, and what is the largest observed), SQL Server-side wait-stats/DMV data for the
  03:00 window, and whether `EnableRetryOnFailure`'s default `SqlServerTransientExceptionDetector`
  actually classifies error `-2` (Execution Timeout) as transient in the installed EF Core version --
  this determines exactly how many of the up-to-9 possible attempts actually fired for this event.]

## Constraints

- Must not change the RabbitMQ `ActivityCreateEvent` contract or schema
- Zero-downtime deployment required -- this is a production order-processing write path
- Must preserve (not regress) the existing idempotent `MERGE ... WHEN NOT MATCHED` upsert semantics
- No new infrastructure beyond what is already operated (SQL Server, RabbitMQ) -- CommandTimeout=30s
  and current SQL Server sizing are treated as fixed inputs, not something this decision can resize
- Retry/resilience handling must survive pod restarts and RabbitMQ redelivery (multiple concurrent
  consumer threads/pods process events)
