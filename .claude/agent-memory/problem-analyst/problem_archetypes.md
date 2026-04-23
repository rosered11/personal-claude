---
name: Problem Archetypes
description: 5 recurring problem archetypes from production incidents I1-I9 — helps classify new problems quickly and assign correct tags
type: project
---

# Recurring Problem Archetypes

Derived from I1–I9. When analyzing a new problem, check if it fits one of these archetypes to ensure correct tag assignment and severity assessment.

---

## Archetype 1: N+1 Query / Pool Exhaustion

**Pattern:** A loop or parallel operation fires one DB query per row/item instead of one batch query. Under concurrency, DB connection pool exhausts.

**Symptoms:** API latency spikes under concurrent load. DB connection wait times in logs. 5,000ms+ for requests that should be < 500ms.

**Tags:** `ef-core`, `n+1`, `connection-pool`, `performance`, `dotnet`
**Severity:** high
**KB precedent:** P001

**Root cause trigger:** Navigation property lazy loading, or fetching related data inside a `foreach` loop.

---

## Archetype 2: ETL Transaction Scope

**Pattern:** A batch processing loop is wrapped in a single transaction spanning the entire job, causing the TX to exceed the DB's lock timeout.

**Symptoms:** ETL job fails after N batches (not always on the first). `innodb_lock_wait_timeout` or PostgreSQL lock error. Job worked with small data volumes but fails at scale.

**Tags:** `etl`, `transaction`, `timeout`, `batch-processing`
**Severity:** high
**KB precedent:** P003, P008

**Root cause trigger:** `BeginTransactionAsync()` called before the batch loop, not inside it.

---

## Archetype 3: Storage Bloat / Maintenance

**Pattern:** Database tables accumulate dead rows or index bloat over time due to missing or misconfigured maintenance settings. Performance degrades gradually, not suddenly.

**Symptoms:** Slow range queries despite indexes. `pg_stat_user_tables` shows high `n_dead_tup`. `last_autovacuum` is NULL on large tables.

**Tags:** `postgresql`, `autovacuum`, `index-bloat`, `maintenance`, `storage`
**Severity:** high (escalates if unaddressed)
**KB precedent:** P002

**Root cause trigger:** Default `autovacuum_vacuum_scale_factor = 0.20` is too high for tables > 500K rows.

---

## Archetype 4: Memory / Resource Leak

**Pattern:** A long-running process accumulates objects in heap across iterations without releasing them, growing unboundedly until OOM.

**Symptoms:** Heap grows linearly with batch count. OOM kill after N batches. Memory profiling shows accumulation of entity objects or tracking dictionaries.

**Tags:** `memory`, `oom`, `etl`, `changetracker`, `ef-core`
**Severity:** high
**KB precedent:** P005

**Root cause trigger:** `ChangeTracker.Clear()` not called after commit. Per-batch tracking dictionary never flushed. Batch size config misconfiguration.

---

## Archetype 5: Silent Wrong-Behavior

**Pattern:** A service runs to completion without error but produces wrong output (0 records, wrong data, wrong table queried). Copy-paste residue in independent call sites.

**Symptoms:** Job completes successfully but downstream data is missing or incorrect. No exception in logs. Root cause found via data analysis, not error logs.

**Tags:** `correctness`, `copy-paste`, `silent-failure`, `etl`, `testing`
**Severity:** high
**KB precedent:** P006

**Root cause trigger:** Code cloned from another service; not all DbSet references or config values updated independently.
