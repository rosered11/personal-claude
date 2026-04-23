# Knowledge Base Index

Auto-maintained by `kb-writer-agent`. Do not edit manually.

---

## Problems

| ID | Title | Tags | Severity | Decisions | Snippets |
|----|-------|------|----------|-----------|---------|
| [P001](problems/P001-getsuborder-n1-pool-exhaustion.md) | GetSubOrder API — N+1 Query + Pool Exhaustion | ef-core, n+1, connection-pool, performance, dotnet | high | D001 | S001, S014 |
| [P002](problems/P002-postgresql-autovacuum-index-bloat.md) | PostgreSQL Dead Tuple Bloat — stockadjustments | postgresql, autovacuum, index-bloat, maintenance, storage | high | D002 | S002 |
| [P003](problems/P003-mysql-etl-single-tx-timeout.md) | MySQL ETL — Single Transaction Spanning Full Job Causes Lock Timeout | etl, transaction, mysql, batch-processing, timeout, dotnet | high | D003 | S003 |
| [P004](problems/P004-etl-zero-observability-batch-metrics.md) | ETL Sync — Zero Observability During Batch Execution | etl, observability, prometheus, monitoring, metrics, dotnet | medium | D004 | — |
| [P005](problems/P005-etl-oom-changetracker-unbounded.md) | ETL Sync OOM Risk — Oversized Batch and EF ChangeTracker Accumulation | etl, memory, ef-core, batch-processing, oom, changetracker, dotnet | high | D005 | S005 |
| [P006](problems/P006-copy-paste-silent-wrong-table.md) | SyncProductBarcodeJda — Copy-Paste Bug: CheckPendingAsync Queries Wrong Table Silently | etl, copy-paste, correctness, testing, silent-failure, dotnet, debugging | high | D006 | — |
| [P007](problems/P007-airflow-dag-debug-multi-layer-bugs.md) | Airflow DAG Local Debug Setup — Multi-Layer Bug Discovery in ds_outbound_order | airflow, python, debugging, sqlalchemy, subprocess, locale, windows, etl | medium | D007 | — |
| [P008](problems/P008-orderjda-n1-savechanges-in-loop.md) | OrderJda ETL — N+1 SELECT + SaveChanges-in-Loop + Long Transaction on PostgreSQL | ef-core, n+1, etl, transaction, batch-processing, dotnet, postgresql, fk-constraint | high | D008 | S008 |
| [P009](problems/P009-airflow-subprocess-timeout-hang.md) | Airflow DAG — Dead subprocess.TimeoutExpired Branch and No Hard Subprocess Timeout | airflow, python, subprocess, timeout, orchestration, threading, dead-code | high | D009 | S009 |

---

## Decisions

| ID | Title | Chosen Option | Problem | Tags | Snippets |
|----|-------|---------------|---------|------|---------|
| [D001](decisions/D001-ef-core-hotpath-factory-compiled-eager.md) | EF Core Hot-Path — IDbContextFactory + Compiled Queries + Eager Loading | IDbContextFactory + EF.CompileQuery + eager loading | P001 | ef-core, dotnet, n+1, performance, connection-pool | S001, S014 |
| [D002](decisions/D002-postgresql-autovacuum-per-table-tuning.md) | PostgreSQL Per-Table Autovacuum Tuning | Per-table autovacuum scale_factor tuning | P002 | postgresql, autovacuum, index-bloat, maintenance, storage | S002 |
| [D003](decisions/D003-etl-per-batch-transaction-scope.md) | ETL Per-Batch Transaction Scope | Per-batch transaction inside the processing loop | P003 | etl, transaction, mysql, batch-processing, timeout | S003 |
| [D004](decisions/D004-etl-prometheus-batch-observability.md) | ETL Batch Observability — Prometheus Histogram + Structured Logging | Prometheus Histogram per batch + structured log per record | P004 | etl, observability, prometheus, monitoring, metrics | — |
| [D005](decisions/D005-etl-batch-size-changetracker-clear.md) | ETL Batch Size 10K + ChangeTracker.Clear() After Each Commit | BatchSize 10K + ChangeTracker.Clear() after every commit | P005 | etl, memory, ef-core, batch-processing, oom, changetracker | S005 |
| [D006](decisions/D006-etl-clone-verification-checklist.md) | ETL Clone Verification Checklist Mandatory Before Deploy | Mandatory 6-point clone verification checklist before deploy | P006 | etl, copy-paste, correctness, testing, silent-failure, process | — |
| [D007](decisions/D007-sqlalchemy-future-mode-airflow.md) | SQLAlchemy future=True + Explicit commit() for Airflow DAG Connections | SQLAlchemy create_engine(future=True) with explicit conn.commit() | P007 | airflow, python, sqlalchemy, debugging, etl, compatibility | — |
| [D008](decisions/D008-two-pass-fk-safe-batch-commit.md) | Two-Pass FK-Safe Batch Commit | Two-pass commit: parents (Pass 1) → children (Pass 2) in same per-batch TX | P008 | ef-core, etl, transaction, batch-processing, fk-constraint, dotnet | S008 |
| [D009](decisions/D009-subprocess-hard-timeout-daemon-thread.md) | Subprocess Hard Timeout via Daemon Thread + proc.wait(timeout) | Daemon thread for stdout streaming + proc.wait(timeout) for hard kill | P009 | airflow, python, subprocess, timeout, orchestration, threading | S009 |
| [D010](decisions/D010-database-type-selection-by-workload.md) | Database Type Selection by Workload | Workload-driven database type selection matrix | — | database, postgresql, mysql, mongodb, redis, architecture | — |
| [D011](decisions/D011-realtime-connection-strategy.md) | Real-Time Connection Strategy | Connection strategy selected by update interval and directionality | — | websocket, sse, polling, real-time, architecture, api | — |
| [D012](decisions/D012-distributed-transaction-strategy.md) | Distributed Transaction Strategy | Tiered strategy: local TX → Saga → TC/C | — | distributed, transaction, saga, tcc, consistency, microservices | S012 |
| [D013](decisions/D013-rate-limiter-algorithm-selection.md) | Rate Limiter Algorithm Selection | Token Bucket for burst-friendly; Sliding Window Counter for high-accuracy | — | rate-limiting, token-bucket, sliding-window, redis, api | S010 |
| [D014](decisions/D014-distributed-id-generation-strategy.md) | Distributed ID Generation — Snowflake vs UUID v4 | Snowflake ID for distributed time-sortable; UUID v4 for fully random | — | id-generation, snowflake, uuid, distributed, scalability | S011 |

---

## Snippets

| ID | Title | Language | Related Problems | Related Decisions |
|----|-------|----------|-----------------|------------------|
| [S001](snippets/S001-async-parallel-db-coordinator/) | Async Parallel DB Coordinator | C# | P001 | D001 |
| [S002](snippets/S002-postgresql-autovacuum-maintenance/) | PostgreSQL Autovacuum Maintenance | SQL | P002 | D002 |
| [S003](snippets/S003-etl-per-batch-commit-loop/) | EF Core Per-Batch Commit Loop | C# | P003 | D003 |
| [S005](snippets/S005-etl-batch-resource-tracking/) | ETL Batch Resource Tracking | C# | P005 | D005 |
| [S008](snippets/S008-two-pass-fk-safe-batch-commit/) | Two-Pass FK-Safe Batch Commit | C# | P008 | D008 |
| [S009](snippets/S009-subprocess-hard-timeout-daemon/) | Subprocess Hard Timeout via Daemon Thread | Python | P009 | D009 |
| [S010](snippets/S010-redis-token-bucket-rate-limiter/) | Redis Token Bucket Rate Limiter | Lua | — | D013 |
| [S011](snippets/S011-snowflake-id-generator/) | Snowflake ID Generator | Go | — | D014 |
| [S012](snippets/S012-idempotency-key-table/) | Idempotency Key Table | SQL | — | D012 |
| [S014](snippets/S014-ef-core-compile-query-static/) | EF.CompileQuery Static Field | C# | P001 | D001 |

---

_Last updated: 2026-04-23 — seeded from KOS incidents I1–I9, decisions D1–D21, tech assets TA1–TA25_
