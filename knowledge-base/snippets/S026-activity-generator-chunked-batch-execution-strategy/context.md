---
when_to_use: "When an EF Core SaveChangesAsync call batches an unbounded number of entities (rows/params scale with input fan-out) into one SQL Server round trip and is wrapped by both EnableRetryOnFailure and a hand-rolled retry loop -- causing retry-amplified CommandTimeout failures. Apply this pattern to bound batch size via two-pass FK-safe chunking and collapse to a single execution-strategy-owned retry."
related_problems: [P021]
related_decisions: [D026]
---

# Snippet: Chunked Two-Pass Persistence with Single Execution-Strategy Retry

Demonstrates replacing an unbounded single `SaveChangesAsync()` call (which EF Core batches into
oversized `MERGE` statements that can exceed `CommandTimeout`) with bounded, two-pass chunked writes
(parents first, then FK-dependent children, per the D008/S008 pattern), executed through
`context.Database.CreateExecutionStrategy().ExecuteAsync(...)` so only EF Core's configured
`EnableRetryOnFailure` policy owns retries -- eliminating the double-stacked retry-amplification
anti-pattern found in `ActivityGenerator.DoTransactionAsyncWithRetryPolicy` (P021/D026). A failure
that exhausts the execution strategy's retries is allowed to propagate to the caller so the RabbitMQ
consumer's own redelivery/DLQ handling (P016/D021 precedent) becomes the outer safety net, instead of
a second in-process retry loop.
