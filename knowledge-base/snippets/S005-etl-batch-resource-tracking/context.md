---
id: S005
slug: etl-batch-resource-tracking
language: csharp
when_to_use: "Add to any ETL while(true) batch loop to gain per-batch observability: TX hold time, record count, GC allocation, and staging read latency. Always pair with ChangeTracker.Clear() + tracking dict Clear() after commit to prevent linear heap growth."
related_problems: [P005]
related_decisions: [D005]
source: TA20
---

# ETL Batch Resource Tracking — Prometheus + Stopwatch + GC (C# / .NET 8)

Drop-in instrumentation for EF Core ETL batch loops. Tracks four metrics per batch: TX hold duration (Histogram), records committed (Counter), batch round (Gauge), GC allocation (Summary). Structured log line per batch is visible in Airflow task logs.

## Critical: always call after tx.CommitAsync()

```csharp
context.ChangeTracker.Clear();   // prevents heap growth across batches
activityTracking.Clear();        // prevents tracking dict accumulation
```

Without these two calls, heap grows linearly across batches and OOM occurs by batch 4–5 at BatchSize=10K.

## When to use

- Any ETL job running unattended in Airflow where operators need visibility without SSH
- Any ETL with > 1 batch where memory growth across batches is a concern
- Greenfield ETL services — add instrumentation from the start
