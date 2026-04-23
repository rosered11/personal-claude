---
id: S003
slug: etl-per-batch-commit-loop
language: csharp
when_to_use: "Drop-in replacement for any ETL ProcessSyncLoopAsync() that wraps a while(true) loop in a single transaction. Move BeginTransactionAsync/CommitAsync inside the loop. Read batch BEFORE opening TX to minimize hold to write-only duration. Add Polly timeout policy for hard per-batch ceiling."
related_problems: [P003]
related_decisions: [D003]
source: TA19
---

# EF Core Per-Batch Commit Loop (C# / .NET 8)

Template for `ProcessSyncLoopAsync()` with per-batch transaction scope, Polly retry/timeout policy, and cursor-based pagination.

## When to use

- Any ETL that reads from a staging table and writes to production in batches
- DB has a lock timeout shorter than the full job duration (MySQL: 50s, PostgreSQL: configurable)
- Partial failure must be recoverable (cursor advances only after commit)

## Key invariants

- TX hold ≈ write duration only (~200ms for 10K rows) — read happens BEFORE `BeginTransactionAsync`
- Cursor (`lastProcessedId`) advances AFTER successful commit — safe on retry
- Polly: exponential backoff on transient MySQL errors (codes 1205, 1213, 2006, 2013)
- Polly: hard 60s timeout per batch as a ceiling
