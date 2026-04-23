---
name: Stack Decision Rules
description: Concrete decision rules from KOS D1-D21 for this codebase's stack — use when choosing between options for a specific technology or pattern
type: project
---

# Concrete Decision Rules

Derived from KOS D1–D21 validated decisions. These are rules that apply to specific technology choices, not general principles.

---

## Database Selection (D010)

| Workload | Choose |
|----------|--------|
| ACID + complex queries + relational | PostgreSQL |
| High write throughput + simple queries | MySQL |
| Flexible schema + document store | MongoDB |
| Cache + pub-sub + ephemeral | Redis |
| Time-series + metrics | InfluxDB / TimescaleDB |

**Default:** Start with PostgreSQL unless a specific workload characteristic clearly disqualifies it.

---

## Distributed Transactions (D012)

| Context | Strategy |
|---------|----------|
| All state in one DB | Local TX |
| 2–3 services, eventual consistency OK | Saga |
| Payment / financial / must not double-spend | TC/C |

**Rule:** 2PC is explicitly rejected for service-to-service flows in this stack.

---

## Rate Limiter Algorithm (D013)

| Requirement | Choose |
|-------------|--------|
| Burst tolerance desirable (external APIs) | Token Bucket |
| High accuracy, no burst (internal service-to-service) | Sliding Window Counter |

**Implementation:** Redis Lua script for atomic operation under concurrent requests (see S010).

---

## ID Generation (D014)

| Requirement | Choose |
|-------------|--------|
| Time-sortable + distributed + B-tree friendly | Snowflake ID |
| Random + security-sensitive (session tokens, API keys) | UUID v4 |
| No coordination infrastructure available | UUID v4 |

---

## EF Core Hot-Path (D001)

| Condition | Rule |
|-----------|------|
| Query called > 100/sec | Apply EF.CompileQuery as static field |
| Parallel Task.WhenAll | Use IDbContextFactory, 1 context per task |
| Read-only query | Add AsNoTracking() always |
| Related data needed | Use Include() chain, never lazy load |

---

## ETL Transaction Scope (D003)

| Condition | Rule |
|-----------|------|
| batch_count × avg_batch_latency > 10s | Per-batch commit mandatory |
| Child entities FK on DB-generated parent ID | Two-pass: save parents → save children |
| After tx.CommitAsync() | Always call ChangeTracker.Clear() |

---

## Subprocess Timeout (D009)

**Rule:** Daemon thread for stdout streaming + `proc.wait(timeout=HARD_TIMEOUT)` on main thread.

```
HARD_TIMEOUT = execution_timeout_seconds - 120  (2-min buffer before Airflow kills worker)
```

Never use `proc.communicate(timeout=N)` when live stdout streaming is required.

---

## Real-Time Connection (D011)

| Latency + direction | Protocol |
|--------------------|----------|
| < 100ms + bidirectional | WebSocket |
| 100ms–1s + server-push only | SSE |
| > 1s acceptable | Polling (interval = expected update rate) |
| Behind proxy blocking upgrades | Polling (always works) |

---

## PostgreSQL Autovacuum (D002)

| Table size | scale_factor |
|-----------|-------------|
| > 5M rows | 0.005 |
| > 500K rows | 0.01 |
| > 100K rows | 0.05 |
| < 100K rows | default (0.20) |

Apply via `ALTER TABLE ... SET (autovacuum_vacuum_scale_factor = ...)`. Run `REINDEX CONCURRENTLY` when index bloat > 30%.
