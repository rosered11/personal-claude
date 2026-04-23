---
id: D002
chosen_option: "Per-table autovacuum scale_factor tuning"
problem_id: P002
tags: [postgresql, autovacuum, index-bloat, maintenance, storage, performance]
related_snippets: [S002]
---

# Decision: PostgreSQL Per-Table Autovacuum Tuning for High-Churn Tables

## Context

High-churn ETL tables (500K+ rows, heavy UPDATE/DELETE) accumulated dead tuples faster than autovacuum's default threshold triggered cleanup. Default `autovacuum_vacuum_scale_factor = 0.2` means vacuum fires only after 20% of rows are dead — on a 1M-row table that is 200K dead tuples. Index bloat reached 1.07GB before vacuum ever ran.

## Options Considered

1. **Default autovacuum settings** — simple, no config; fails for large high-churn tables (too infrequent).
2. **Manual VACUUM ANALYZE on schedule** — reliable but requires cron/Airflow overhead and misses burst churn.
3. **Per-table `autovacuum_vacuum_scale_factor` override** — targets only affected tables; autovacuum remains automatic.

## Decision

Override autovacuum settings per table using `ALTER TABLE ... SET (autovacuum_vacuum_scale_factor = 0.01, autovacuum_analyze_scale_factor = 0.01)` for any table exceeding 500K rows with frequent UPDATE/DELETE. Run `REINDEX CONCURRENTLY` to reclaim existing index bloat without locking. Set `autovacuum_vacuum_cost_delay = 2` to reduce vacuum I/O throttling on ETL tables.

## Consequences

- Autovacuum triggers at 1% dead tuples instead of 20% — far more responsive to burst churn.
- `REINDEX CONCURRENTLY` reclaims bloat without table lock (safe in production).
- Table-scoped settings do not affect other tables — no global risk.
- Requires DBA access to alter table storage parameters.
