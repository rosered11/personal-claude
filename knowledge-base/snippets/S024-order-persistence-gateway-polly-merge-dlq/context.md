---
when_to_use: >
  Use when a .NET/EF Core service consumes messages from a broker (Kafka/RabbitMQ) under
  at-least-once delivery and writes to SQL Server, and is experiencing intermittent
  DbUpdateException failures from (a) SqlException Execution Timeout Expired (error -2) on
  SaveChanges under write-path contention, and/or (b) UNIQUE KEY constraint violations caused by
  duplicate/concurrent redelivery of the same logical record. Combine the resilient upsert adapter
  with a consumer-side idempotency guard and DLQ so writes are both timeout-resilient and safe to
  retry, and no message is ever silently dropped.
related_problems: [P019]
related_decisions: [D024]
language: csharp
---

# Snippet: Resilient Upsert Persistence Adapter with Idempotency Guard and DLQ

Demonstrates the Hexagonal driven-adapter pattern (IOrderPersistenceGateway) hardened with:

1. A Polly resilience policy that classifies transient SQL Server error codes -- including -2
   (Execution Timeout Expired), which is not covered by EF Core's default SqlServerRetryingExecutionStrategy
   unless explicitly configured, and is the exact error observed in production logs (P019).
2. A MERGE ... WITH (HOLDLOCK) upsert against dbo.Order keyed on SourceOrderId, making the write
   itself idempotent and race-safe regardless of what triggered a retry.
3. A durable ProcessedEvents dedup table check (extends the idempotency-key pattern from S012 and
   S015) as a fast-path guard before the write is attempted.
4. A Dead Letter Queue publish path replacing the "message skipped after 1 retries, no DLQ"
   behavior observed in the qh492 pod log -- so an exhausted retry policy always produces an
   operationally visible artifact, never a silent drop.

These four pieces work together: the dedup check avoids most duplicate writes cheaply; the MERGE
upsert is the correctness backstop for the rare race the dedup check itself might miss (two pods
processing the same event within milliseconds of each other); the Polly policy absorbs transient
SQL Server latency spikes instead of surfacing them to the caller on the first hit; and the DLQ
guarantees that if all of the above still fails, the order event is durably preserved for replay
rather than lost.
