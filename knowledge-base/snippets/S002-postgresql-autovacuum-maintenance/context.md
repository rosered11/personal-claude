---
id: S002
slug: postgresql-autovacuum-maintenance
language: sql
when_to_use: "Apply to any PostgreSQL table > 500K rows with heavy UPDATE/DELETE (ETL staging, order tables, stock adjustments). Run TA12 health monitor query weekly. Apply TA13 scale_factor override once. Run TA14 REINDEX CONCURRENTLY when index bloat ratio exceeds 30%."
related_problems: [P002]
related_decisions: [D002]
source: TA12, TA13, TA14
---

# PostgreSQL Autovacuum Maintenance (SQL)

Three-query toolkit for diagnosing and fixing dead tuple bloat and index bloat on high-churn PostgreSQL tables.

## Files

- `health_monitor.sql` — dead tuple ratio query (run on schedule; alert when dead_ratio > 5%)
- `autovacuum_override.sql` — per-table scale_factor override (run once per table)
- `reindex_concurrently.sql` — non-blocking index rebuild (run when bloat ratio > 30%)

## When to use

- After any bulk ETL operation that generates heavy UPDATE/DELETE
- Weekly maintenance check for tables > 500K rows
- After `pg_stat_user_tables` shows `last_autovacuum` is NULL or older than 24h on a high-churn table
