---
id: D010
chosen_option: "Workload-driven database type selection matrix"
tags: [database, postgresql, mysql, mongodb, redis, architecture, selection]
related_snippets: []
---

# Decision: Database Type Selection by Workload

## Context

Teams routinely default to the most familiar database regardless of workload characteristics. Mismatches (relational DB for time-series data, document store for strongly-relational data) create performance problems and schema complexity that compound over time.

## Options Considered

1. **PostgreSQL for everything** — ACID, extensions (TimescaleDB, JSONB); works but over-engineered for cache or simple key-value workloads.
2. **MongoDB for everything** — flexible schema; poor fit for relational data with complex join requirements.
3. **Workload-driven selection** — match database capability to access pattern; accept operational overhead of multiple database types.

## Decision

Select database type by primary workload:

| Workload | Database | Reason |
|----------|----------|--------|
| ACID / complex queries / relational | PostgreSQL | Full SQL, FK enforcement, JSONB, extensions |
| High write throughput / simple queries | MySQL | InnoDB optimized for write-heavy simple selects |
| Flexible schema / document store | MongoDB | Schema-free, horizontal scale, aggregation pipeline |
| Cache / pub-sub / ephemeral | Redis | In-memory, sub-millisecond, TTL, pub-sub native |
| Time-series / metrics | InfluxDB / TimescaleDB | Optimized retention, downsampling, time-range queries |

## Consequences

- Optimal performance per workload; avoids relational overhead for cache, document overhead for ACID workloads.
- Operational cost: multiple database types require separate runbooks, backup strategies, and expertise.
- For greenfield services: start with PostgreSQL unless a specific workload characteristic clearly disqualifies it.
