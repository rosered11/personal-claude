---
name: Tag Clusters
description: Common co-occurring tag groups from seeded KB — helps identify related entries when a new problem has partial overlap
type: project
---

# Tag Clusters from Seeded KB

Common co-occurring tag groups observed across P001–P009 and D001–D014. When a new problem's tags overlap significantly with one of these clusters, the corresponding KB entries are likely relevant.

---

## Cluster 1: EF Core N+1 / Pool Exhaustion

**Tags:** `ef-core`, `n+1`, `dotnet`, `performance`, `connection-pool`
**KB entries:** P001, D001, S001, S014
**Signal:** Latency spike under concurrency. Multiple DB queries per request. Heap dump shows DynamicMethod accumulation.

---

## Cluster 2: ETL Batch / Transaction

**Tags:** `etl`, `batch-processing`, `transaction`, `ef-core`, `dotnet`
**KB entries:** P003, P005, P008, D003, D005, D008, S003, S005, S008
**Signal:** TX timeout. OOM during batch processing. N+1 inside foreach. ChangeTracker heap growth.

---

## Cluster 3: PostgreSQL Maintenance

**Tags:** `postgresql`, `maintenance`, `autovacuum`, `index-bloat`, `storage`
**KB entries:** P002, D002, S002
**Signal:** Slow queries despite indexes. High `n_dead_tup`. Last_autovacuum NULL on large table.

---

## Cluster 4: Airflow / Python / Subprocess

**Tags:** `airflow`, `python`, `subprocess`, `orchestration`, `timeout`
**KB entries:** P007, P009, D007, D009, S009
**Signal:** Subprocess hangs after Airflow timeout. .NET process leaks on server. Dead except branches.

---

## Cluster 5: Distributed Transaction / Saga

**Tags:** `distributed`, `transaction`, `saga`, `microservices`, `consistency`
**KB entries:** D012, S012
**Signal:** Multi-service state changes. Payment double-spend. Need compensating transactions.

---

## Cluster 6: Rate Limiting / Scalability / API

**Tags:** `rate-limiting`, `scalability`, `api`, `redis`
**KB entries:** D013, S010
**Signal:** Need burst-tolerant or strict per-window API limiting. Redis-backed atomic counters.

---

## Cluster 7: Observability / Monitoring

**Tags:** `etl`, `observability`, `prometheus`, `monitoring`, `metrics`
**KB entries:** P004, D004, S005
**Signal:** No per-batch metrics. Cannot distinguish hung job from slow batch. No alerting.

---

## Cluster 8: Code Correctness / Clone Verification

**Tags:** `etl`, `copy-paste`, `correctness`, `silent-failure`, `testing`
**KB entries:** P006, D006
**Signal:** Zero records processed without error. Job completed but wrong table queried.
