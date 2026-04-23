---
id: D005
chosen_option: "BatchSize 10K + ChangeTracker.Clear() after every commit"
problem_id: P005
tags: [etl, memory, ef-core, batch-processing, oom, changetracker, dotnet]
related_snippets: [S005]
---

# Decision: ETL Batch Size 10K + ChangeTracker.Clear() After Each Commit

## Context

Live Airflow log showed Batch 1: 100K records, 5,757MB alloc, 1,191MB heap. Batch 2: 1,780MB heap (+589MB). Heap growing every batch — OOM projected by Batch 4–5. Root causes: `BatchSize` config returned 100K (10× intended), ChangeTracker not cleared after commit, and a per-batch tracking dictionary accumulated 3M entries across the job.

## Options Considered

1. **Large batch (100K) + no ChangeTracker.Clear()** — maximum throughput; causes unbounded heap growth and OOM.
2. **Small batch (1K) + ChangeTracker.Clear()** — minimal memory; too many round trips for throughput requirements.
3. **BatchSize 10K + ChangeTracker.Clear() + flush per-batch dictionaries** — balanced: bounded memory, acceptable throughput, clear heap profile.

## Decision

Set `BatchSize = 10_000` as the canonical value for EF Core ETL inserts. Call `context.ChangeTracker.Clear()` immediately after `tx.CommitAsync()` in every batch iteration. Flush any per-batch accumulation dictionaries (e.g., `activityTracking`) at the end of each batch. Validate with heap metrics in staging before deploying to production.

## Consequences

- Heap stabilizes per batch (grows then drops back) instead of accumulating linearly.
- 100K → 10K batch size reduces peak alloc from ~5.7GB to ~570MB per batch.
- ChangeTracker.Clear() means EF stops tracking committed entities — no unintended update detection across batches.
- Per-batch dictionary flush prevents the 3M-entry accumulation seen in production.
- `BatchSize` config must be validated in staging — a 10× misconfiguration is not detectable at compile time.
