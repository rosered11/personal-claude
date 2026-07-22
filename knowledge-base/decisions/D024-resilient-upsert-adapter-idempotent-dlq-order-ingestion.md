---
id: D024
chosen_option: "Resilient Upsert Persistence Adapter (Hexagonal) + Idempotency Dedup Table and DLQ (Event-Driven) for validate-service MAO Ingestion"
problem_id: P019
tags:
  - ef-core
  - mssql
  - kafka
  - command-timeout
  - idempotency
  - duplicate-key
  - integration-events
  - dotnet
related_snippets: [S024]
lenses_evaluated:
  - Event-Driven Architecture
  - Hexagonal Architecture
confidence: high
---

## Context

validate-service intermittently fails to persist MAO order events to SQL Server. Log evidence
(P019) shows two concrete, non-overlapping failure signatures: (a) an INSERT INTO ProcessOrder
that exceeded a 10-second CommandTimeout by roughly 20ms, and (b) duplicate INSERTs into dbo.Order
violating UN_SourceOrderId followed by the Kafka consumer skipping the message after exactly one
retry attempt, with no DLQ. Both signatures needed a decision that fixes the write path itself and
guarantees no order event is ever silently dropped.

## Options Considered

### Lens A: Event-Driven Architecture -- Idempotent Kafka Consumer with Dedup Store + DLQ
Adds a durable processed_events dedup check (extending the D012/S012 pattern already reused in
D015/S015 for Order.API) before every write, and replaces "skip after 1 retry" with a bounded
retry followed by a Dead Letter Queue topic plus alerting.
Pros: closes the silent-data-loss gap; extends an already-proven KB pattern; fully consumer-side,
no topic or schema changes.
Cons: does not address the underlying SQL command timeout -- a perfectly idempotent consumer can
still fail to ever persist if the DB itself is the bottleneck at peak load; adds new operational
surface (dedup table TTL, DLQ monitoring).

### Lens B: Hexagonal Architecture -- Resilient Upsert Persistence Adapter
Isolates SQL Server access behind a driven adapter (IOrderPersistenceGateway) that uses a properly
tuned and monitored CommandTimeout, a Polly resilience policy that correctly classifies transient
SqlException codes (including -2, Execution Timeout -- not covered by EF Core default retry
strategy), and performs an idempotent MERGE/upsert instead of a blind INSERT.
Pros: directly targets the exact 10s/10,020ms boundary condition observed in the logs; upsert
semantics independently resolve the UN_SourceOrderId collisions; confined to one adapter class,
easy to unit test in isolation.
Cons: does not explain or fix why SQL Server write latency spikes (lock contention vs capacity
vs noisy-neighbor from 15 concurrent pods) -- tuning timeout/retry can mask a capacity problem;
does not solve the Kafka-level "message skipped, no DLQ" data-loss gap on its own; MERGE under
concurrent execution has known race caveats requiring HOLDLOCK and careful isolation-level handling.

## Decision

Adopt both lenses as a single blended option, with Hexagonal adapter hardening as the primary
mechanism (it directly targets the log-evidenced root cause -- the 10s CommandTimeout boundary
breach) and the Event-Driven idempotency/DLQ pattern as a required companion, not an optional
follow-up:

- IOrderPersistenceGateway driven adapter: tuned and monitored CommandTimeout, a Polly policy that
  classifies transient SQL error codes (including -2) for retry-with-backoff, and a
  MERGE ... WITH (HOLDLOCK) upsert against dbo.Order keyed on SourceOrderId -- this alone makes
  every write idempotent at the database boundary, independent of what triggered the retry.
- A durable ProcessedEvents dedup table (reusing the D012/S012 pattern) as a fast-path guard and
  audit trail in the consumer, checked before the adapter is invoked.
- Replace "skip after 1 retry" with routing to a Dead Letter Queue topic
  (mao.in.fc.order.release.ds.dlq) plus alerting whenever the adapter resilience policy is
  exhausted -- no order event is ever silently dropped again.

### Rejected Options

- EDA-only (idempotent consumer + DLQ, no adapter hardening): Rejected because it treats a
  symptom, not the primary observed cause. Even a perfectly idempotent consumer will keep retrying
  into the same contention window that produced the original 10,020ms timeout; under sustained load
  the DLQ would become the default path rather than an exception path, which is unacceptable for a
  high-severity production order pipeline.
- Hexagonal-only (adapter timeout/retry tuning, no consumer idempotency/DLQ): Rejected because it
  leaves silent order loss possible if the adapter own Polly retries are exhausted during a
  genuine outage -- today's "message skipped after 1 retries" behavior with no DLQ would still
  apply, with only a log line as the operational signal. Unacceptable given severity=high and the
  constraint that the fix must survive redelivery and rebalance scenarios durably.

## Borrowed Insights

From the EDA analysis: the idempotency-dedup-table pattern (extending D012/S012/D015 precedent) and
DLQ-on-exhausted-retry are retained as first-class requirements because the log evidence shows real
silent-skip risk today, not a hypothetical one. From the Hexagonal analysis: MERGE/upsert at the
adapter boundary is adopted as the primary write mechanism -- this doubles as idempotency
protection, so the dedup table becomes a fast-path guard and audit trail while the DB-level upsert
is the correctness backstop against races the dedup check itself might miss (e.g., two pods racing
within the same few milliseconds, as observed at 08:51:29.217 and 08:51:29.243).

## Consequences

Benefits: Both observed failure signatures (timeout boundary breach; duplicate-key plus silent
skip) are addressed at their respective sources; reuses an already-validated KB idempotency pattern
instead of inventing a new mechanism; confined blast radius (one adapter class, one consumer-side
guard, one new DLQ topic) satisfies the "no topic/schema change" and "no new infra beyond what is
already operated" constraints.

Trade-offs Accepted:
- Does not, by itself, prove or fix the ultimate cause of SQL Server write latency (lock contention
  vs capacity vs concurrent-pod noisy-neighbor effects) -- timeout tuning and retries can mask a
  capacity problem if the real cause is undersized SQL Server compute.
- New operational surface: ProcessedEvents table requires periodic TTL cleanup (same discipline as
  D015), and the new DLQ topic requires depth monitoring and alerting to be genuinely useful rather
  than a second place for orders to silently pile up unnoticed.
- MERGE ... WITH (HOLDLOCK) adds minor lock overhead per write compared to a bare INSERT; acceptable
  given it removes an entire class of duplicate-key race.

## Next Steps

1. Immediate: Implement IOrderPersistenceGateway (SqlOrderPersistenceGateway) wrapping OrderContext
   writes in a Polly policy that retries SQL error codes -2, 1205, 4060, 40197, 40501, 40613 with
   exponential backoff, and replace the blind INSERT into dbo.Order with a MERGE ... WITH (HOLDLOCK)
   upsert keyed on SourceOrderId.
2. Immediate: Add a ProcessedEvents dedup table and check (reuse the S012 processed_events pattern)
   in MAOOrderEventHandler before invoking the gateway.
3. Immediate: Replace "Message skipped after 1 retries" with a DLQ publish
   (mao.in.fc.order.release.ds.dlq) whenever the gateway resilience policy is exhausted; wire an
   alert on DLQ depth greater than 0.
4. Sprint: Investigate SQL Server wait stats (sys.dm_exec_requests, sys.dm_os_wait_stats) during
   comparable load windows to confirm whether lock contention or raw resource exhaustion
   (DTU/vCore) is driving the 10s-boundary latency spikes -- this determines whether timeout tuning
   alone is sufficient or SQL Server capacity/indexing also needs remediation.
5. Sprint: Add a dashboard/alert on ProcessedEvents growth and DLQ depth.
6. Backlog: Evaluate whether SourceOrderId alone is a safe dedup key, or whether a composite key
   (event id or changeLog hash) is needed to distinguish legitimate order amendments from
   redelivery duplicates.

## KB References

- P010 / D015 / S015 -- Order Service concurrent running-number race and missing event-consumer
  idempotency (same Order bounded context, different sub-path; idempotency pattern reused here)
- D012 / S012 -- Distributed Transaction Strategy / Idempotency Key Table (dedup pattern origin)
- D001 -- EF Core hot-path patterns (DbContext usage discipline)
