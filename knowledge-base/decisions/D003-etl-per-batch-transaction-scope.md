---
id: D003
chosen_option: "Per-batch transaction inside the processing loop"
problem_id: P003
tags: [etl, transaction, mysql, batch-processing, timeout, dotnet]
related_snippets: [S003]
---

# Decision: ETL Per-Batch Transaction Scope — Never Single TX for Full Job

## Context

ETL sync job wrapped the entire `while(true)` batch loop in a single `BeginTransactionAsync()` call. Transaction hold time equaled the sum of all batch latencies — measured at 210 seconds against MySQL's 50-second `innodb_lock_wait_timeout`. Job failed on the second or third batch with lock timeout errors.

## Options Considered

1. **Single transaction spanning the entire job** — atomic across all batches; fails on any DB with a lock timeout shorter than job duration.
2. **No transaction (autocommit)** — avoids timeout; partial failures leave inconsistent staging data with no rollback.
3. **Per-batch transaction inside the loop** — each batch is atomic; TX hold = single batch latency (seconds, not minutes); rollback scope is one batch.

## Decision

Move `BeginTransactionAsync()` / `CommitAsync()` / `RollbackAsync()` inside the `while(true)` loop, wrapping only the current batch. Each batch begins its own transaction, commits on success, rolls back on failure, and the loop continues to the next batch. Transaction hold time is bounded by a single batch duration.

## Consequences

- TX hold reduced from 210s → single-batch duration (typically 2–8s at BatchSize 10K).
- MySQL lock timeout no longer fires during normal operation.
- Failure in batch N does not roll back batches 1..N-1 (accepted trade-off for ETL idempotency via staging table status flags).
- Pattern is now the standard for all ETL sync services in this stack (see D008 for FK-dependent variant).
