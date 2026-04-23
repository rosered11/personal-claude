---
name: PostgreSQL Internals
description: PostgreSQL MVCC, autovacuum, and index bloat internals — use when advising on PostgreSQL performance or maintenance problems
type: project
---

# PostgreSQL Internals — MVCC and Autovacuum

From K26–K27, incident I2, and decision D002.

---

## MVCC and Dead Tuples (K26)

**How it works:**
- `UPDATE` = insert new row version + mark old version **dead**
- `DELETE` = mark row dead
- Dead tuples remain in heap and indexes until VACUUM reclaims them
- Dead tuples in indexes require visibility checks on every index lookup — slow range queries even when indexed

**Diagnosis query:**
```sql
SELECT relname, n_live_tup, n_dead_tup,
  ROUND(n_dead_tup::numeric / NULLIF(n_live_tup + n_dead_tup, 0) * 100, 2) AS dead_ratio_pct,
  last_autovacuum
FROM pg_stat_user_tables
WHERE n_dead_tup > 10000
ORDER BY n_dead_tup DESC;
```

**Thresholds:**
- `dead_ratio < 5%` → healthy
- `dead_ratio 5–10%` → monitor, check autovacuum settings
- `dead_ratio > 10%` → run VACUUM immediately
- `dead_ratio > 20%` → incident — autovacuum broken or blocked

**Important:** VACUUM cleans heap but does NOT shrink index files. Only `REINDEX CONCURRENTLY` produces a compact B-tree.

---

## Autovacuum Scale Factor Trap (K27)

**Default trigger formula:**
```
threshold = autovacuum_vacuum_threshold + autovacuum_vacuum_scale_factor × n_live_tup
Default:   50 + 0.20 × n_live_tup
```

**Production incident:** 4M-row table → threshold = 828,932 dead rows to trigger. At 702,783 dead rows (14.5%), autovacuum had never fired. Working as configured — configuration was wrong.

**Per-table override decision matrix:**

| Table size | Set scale_factor to |
|-----------|---------------------|
| > 5M rows | 0.005 |
| > 500K rows | 0.01 |
| > 100K rows | 0.05 |
| < 100K rows | default (0.20) is fine |

**Apply with:**
```sql
ALTER TABLE stockadjustments SET (
  autovacuum_vacuum_scale_factor = 0.01,
  autovacuum_vacuum_threshold = 1000,
  autovacuum_analyze_scale_factor = 0.005,
  autovacuum_analyze_threshold = 500
);
```

---

## REINDEX CONCURRENTLY

**Use when:** Index bloat ratio > 30% (confirmed via `pg_stat_user_indexes`).

**Safe in production:** Does not lock the table. Requires PostgreSQL 12+.

**Run one index at a time:**
```sql
REINDEX INDEX CONCURRENTLY stockadjustments_pkey;
REINDEX INDEX CONCURRENTLY stockadjustments_adjusted_at_idx;
```

**Monitor:**
```sql
SELECT phase, blocks_done, blocks_total,
       ROUND(blocks_done::numeric / NULLIF(blocks_total, 0) * 100, 1) AS pct_complete
FROM pg_stat_progress_create_index
WHERE relid = 'stockadjustments'::regclass;
```

**Measured impact:** Before: ~1.7GB total indexes. After: 251MB. (6.8× reduction)
