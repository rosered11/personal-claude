---
id: D015
chosen_option: "MSSQL SEQUENCE for Running-Number + D012 Idempotency Key Pattern for Integration Event Consumers + API Idempotency Key Header on CreateOrder"
problem_id: P010
date: 2026-04-24
tags:
  - ef-core
  - concurrency
  - optimistic-locking
  - running-number
  - idempotency
  - dotnet
  - mssql
  - integration-events
  - duplicate-order
  - null-safety
  - microservices
related_snippets:
  - S015
lenses_evaluated:
  - Domain-Driven Design
  - Event-Driven Architecture
confidence: high
---

## Decision

Replace `GenRunningNumberFunction`'s optimistic-concurrency SaveChanges retry loop with a MSSQL `SEQUENCE` object. Apply the idempotency key pattern (D012/S012) to all integration event consumers. Add an API-level idempotency key header on `CreateOrder`. Add null guards in `StandardActivityCreateBySubOrderFunction` and `ProcessOrderItemUpdateIntegrationEventHandler`.

## Lens Evaluations

### Lens A: Domain-Driven Design
**Option:** Running-Number as Dedicated Aggregate with DB Sequence + Transactional Outbox for Event Deduplication

DDD correctly identifies the running number as a domain invariant violated by the current optimistic concurrency approach. MSSQL SEQUENCE is the canonical solution: atomic, non-blocking, requires no retry loop. Null guards belong at Aggregate factory/constructor boundaries per DDD discipline.

**Pros:** Eliminates root cause, minimal throughput impact, single schema migration.
**Cons:** Outbox pattern adds operational complexity; API-level duplicate submissions not addressed.

### Lens B: Event-Driven Architecture
**Option:** Idempotency Key Enforcement at Consumer + Serialized Running-Number via Single-Writer Event Handler

EDA frames all consumers as idempotent replay-safe handlers. API idempotency key header prevents duplicate submission errors. Extends D012 naturally.

**Pros:** Full idempotency coverage at API and event consumer, consistent with existing KB precedent.
**Cons:** Redis dependency for running-number serialization adds infrastructure risk; single-writer is a throughput bottleneck. Doesn't fix the root cause.

## Rationale

- MSSQL SEQUENCE (DDD lens) eliminates the `DbUpdateConcurrencyException` root cause permanently — no race condition is possible on a SEQUENCE call
- Idempotency key pattern (EDA lens, D012/S012) is the correct and already-established KB mechanism for event consumer deduplication
- Redis is explicitly rejected: SEQUENCE already provides atomic increment; Redis adds a new infrastructure dependency for no gain
- Transactional Outbox is sound but deferred: it is a correctness improvement, not required for immediate incident resolution
- Null guards are straightforward defensive fixes that do not require architectural justification

## Tradeoffs Accepted

- Schema migration required (SEQUENCE creation) — acceptable; zero downtime with careful deployment ordering
- Idempotency table requires periodic TTL cleanup to avoid unbounded growth — monitor and schedule
- `MultipleCollectionIncludeWarning` and decimal precision warnings deferred to a separate EF Core model cleanup task — not blocking

## Next Steps

1. **Immediate:** Create MSSQL SEQUENCE `dbo.OrderRunningNumberSeq`; replace `GenRunningNumberFunction` retry loop with `SELECT NEXT VALUE FOR dbo.OrderRunningNumberSeq`
2. **Immediate:** Add idempotency key check to `ProcessOrderStartIntegrationEventHandler` using the `processed_events` table pattern from S012
3. **Immediate:** Add null guard `ArgumentNullException.ThrowIfNull(input)` in `StandardActivityCreateBySubOrderFunction.Execute` line 45 and null check for `event.OrderItem` in `ProcessOrderItemUpdateIntegrationEventHandler.Handle` line 172
4. **Sprint:** Add `Idempotency-Key` header support on `POST /orders` endpoint — cache result with TTL matching order creation SLA
5. **Sprint:** Add `HasPrecision` to all decimal properties in `OnModelCreating` (InsuranceMinimumAmount, PackQuantity, PackageWeight, Qty)
6. **Sprint:** Configure `QuerySplittingBehavior.SplitQuery` globally or per-query for multi-collection Includes
7. **Backlog:** Evaluate Transactional Outbox for ProcessActivityStart event publication correctness

## KB References

- D012 — Distributed Transaction Strategy (idempotency key table precedent)
- S012 — Idempotency Key Table SQL pattern (reuse directly)
- D001 — EF Core hot-path patterns (IDbContextFactory context for DbContext usage)
- D008 — Two-Pass FK-Safe Batch Commit (per-batch transaction discipline)
