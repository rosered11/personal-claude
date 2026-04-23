---
name: ETL Patterns
description: Established ETL batch processing patterns from K30-K32 and incidents I3/I5/I8 — use as defaults for any ETL batch code
type: project
---

# ETL Established Patterns

From K30–K32, K35, incidents I3/I5/I6/I8, and decisions D003/D005/D008.

---

## Per-Batch Transaction Scope (K30, D003)

**Rule:** BeginTransactionAsync / CommitAsync / RollbackAsync must be INSIDE the `while(true)` loop, not wrapping it.

```csharp
// Correct
while (true)
{
    var batch = await ReadBatch(lastId, ct);
    if (batch.Count == 0) break;

    await using var tx = await context.Database.BeginTransactionAsync(ct);
    await WriteBatch(batch, ct);
    await tx.CommitAsync(ct);
    lastId = batch.Last().Id;   // advance cursor AFTER commit
}

// Wrong — NEVER do this
await using var tx = await context.Database.BeginTransactionAsync(ct);  // OUTSIDE loop
while (true) { ... }
await tx.CommitAsync(ct);  // unreachable for large jobs
```

**Why:** TX hold = sum of all batch latencies when wrapped outside loop. MySQL times out at 50s (`innodb_lock_wait_timeout`). Per-batch TX hold = single batch duration (~200ms for 10K rows).

---

## BatchSize = 10,000 (D005)

**Rule:** Use 10,000 as the canonical batch size for EF Core ETL inserts. Validate in staging before production.

**Why:** 100K batch = ~5.7GB alloc per batch (100K entities × 30 fields × EF SqlParameter). 10K batch = ~570MB — manageable with GC. Config must be validated — a 10× misconfiguration is not detectable at compile time.

---

## Read Before Opening TX (TA19)

**Rule:** Fetch the batch from the staging table BEFORE calling `BeginTransactionAsync`. TX hold = write-only duration (~200ms), not read + write.

```csharp
// Correct
var batch = await GetProductStaging(lastId, ct);   // read BEFORE TX
if (batch.Count == 0) break;
await using var tx = await context.Database.BeginTransactionAsync(ct);
await WriteToProduction(batch, ct);
await tx.CommitAsync(ct);

// Wrong — TX held during staging read
await using var tx = await context.Database.BeginTransactionAsync(ct);
var batch = await GetProductStaging(lastId, ct);   // read INSIDE TX
```

---

## Two-Pass FK-Safe Batch Commit (K35, D008)

**Rule:** When child entities FK on a DB-generated parent ID, use two `SaveChangesAsync` calls per batch inside one TX.

```
Pass 1: Save parents → EF populates parent.Id from DB IDENTITY
Pass 2: Build children using parent.Id → Save children in one batch
Commit TX
ChangeTracker.Clear()
```

Total: 2 `SaveChangesAsync` + 1 `CommitAsync` per batch, regardless of batch size.

**Boundary:** If parent IDs are application-assigned (Guid.NewGuid()), skip two-pass — single-pass works.

---

## Prometheus Histogram Per Batch (K31, D004)

**Rule:** Any ETL batch loop writing to DB must emit a Histogram observation per batch at minimum: `{tx_hold_ms, record_count, staging_read_ms, alloc_bytes}`.

**Why:** Without per-batch metrics, OOM and timeout are diagnosed only after Airflow fires `execution_timeout`. With metrics, TX hold drift triggers an alert minutes before failure.

Alert rule: `p95(etl_sync_batch_duration_seconds) > innodb_lock_wait_timeout * 0.7`

---

## Polly Retry + Timeout Per Batch (TA19)

**Rule:** Wrap each batch's TX with a Polly policy: exponential backoff on transient MySQL errors (codes 1205, 1213, 2006, 2013) + hard 60s timeout per batch.

```csharp
var retryPolicy = Policy.Handle<MySqlException>(ex => IsTransient(ex))
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
var timeoutPolicy = Policy.TimeoutAsync(60, TimeoutStrategy.Optimistic);
var batchPolicy = Policy.WrapAsync(retryPolicy, timeoutPolicy);
```
