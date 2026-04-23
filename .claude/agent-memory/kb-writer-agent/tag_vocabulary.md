---
name: Tag Vocabulary
description: Canonical tag list for KB records — use these tags exactly (no synonyms) when writing frontmatter
type: project
---

# Canonical Tag Vocabulary

Derived from all seeded KB entries (P001–P009, D001–D014, S001–S014). Use these exact strings — do not invent new tags or use synonyms unless the concept genuinely has no match here.

## Technology Tags

| Tag | Use When |
|-----|----------|
| `ef-core` | Entity Framework Core queries, ChangeTracker, DbContext, compiled queries |
| `dotnet` | .NET / C# services, EF Core, Polly, Prometheus-net |
| `go` | Go services, goroutines, sync primitives |
| `python` | Python scripts, Airflow callables, subprocess |
| `postgresql` | PostgreSQL DB, autovacuum, MVCC, indexes |
| `mysql` | MySQL DB, InnoDB, lock timeouts |
| `kafka` | Kafka topics, partitions, consumers, ISR |
| `airflow` | Apache Airflow DAGs, PythonOperator, XCom, execution_timeout |
| `redis` | Redis cache, Lua scripts, pub-sub, TTL |
| `sqlalchemy` | SQLAlchemy ORM, future=True, engine.connect() |
| `subprocess` | subprocess.Popen, stdout streaming, process management |
| `threading` | Python threading.Thread, daemon threads |
| `prometheus` | Prometheus metrics, Histogram, Counter, Gauge |

## Problem Domain Tags

| Tag | Use When |
|-----|----------|
| `n+1` | N+1 query pattern, one DB query per row in a loop |
| `connection-pool` | DB connection pool exhaustion, pool limits |
| `batch-processing` | Processing records in batches, chunk size |
| `etl` | Extract-Transform-Load jobs, staging tables, sync classes |
| `transaction` | DB transaction scope, commit, rollback, TX hold time |
| `timeout` | DB/network/execution timeouts |
| `memory` | Memory management, heap growth |
| `oom` | Out-of-memory risk or incident |
| `changetracker` | EF Core ChangeTracker accumulation |
| `autovacuum` | PostgreSQL autovacuum configuration and behavior |
| `index-bloat` | PostgreSQL index bloat, REINDEX |
| `maintenance` | DB maintenance operations (vacuum, reindex, analyze) |
| `storage` | Storage size, bloat, disk usage |
| `performance` | Latency, throughput optimization |
| `scalability` | Horizontal/vertical scaling, load handling |
| `observability` | Metrics, logging, tracing, monitoring |
| `monitoring` | Alert rules, dashboards, health checks |
| `metrics` | Quantitative measurements, counters, histograms |
| `debugging` | Local debugging, profiling, diagnosis |
| `copy-paste` | Code cloned from another file, touch points not updated |
| `correctness` | Data accuracy, wrong results without error |
| `silent-failure` | No exception raised but wrong/zero output |
| `testing` | Test coverage, test strategies |
| `fk-constraint` | Foreign key constraints in DB schema |
| `dead-code` | Code that cannot be reached at runtime |
| `orchestration` | Process/workflow orchestration |
| `locale` | Character encoding, locale settings |
| `windows` | Windows-specific behavior or platform |
| `process` | Operational process, team practices, checklists |
| `compatibility` | Version compatibility, library pinning |

## Architecture Tags

| Tag | Use When |
|-----|----------|
| `distributed` | Multi-node / multi-service architecture |
| `saga` | Saga pattern for distributed transactions |
| `tcc` | Try-Confirm/Cancel pattern |
| `consistency` | Data consistency guarantees |
| `microservices` | Microservice architecture style |
| `architecture` | High-level architectural decisions |
| `selection` | Choosing between technologies or patterns |
| `database` | Database technology selection |
| `api` | API design, REST, rate limiting |
| `real-time` | Low-latency real-time features |
| `websocket` | WebSocket connections |
| `sse` | Server-Sent Events |
| `polling` | HTTP polling patterns |
| `rate-limiting` | API rate limiting |
| `token-bucket` | Token Bucket algorithm |
| `sliding-window` | Sliding Window algorithm |
| `idempotency` | Idempotent operations, dedup |
| `snowflake` | Snowflake ID generation |
| `uuid` | UUID ID generation |
| `id-generation` | Distributed ID generation |
| `event-driven` | Event-driven architecture |
| `cqrs` | Command Query Responsibility Segregation |
| `compiled-query` | EF Core EF.CompileQuery static fields |
