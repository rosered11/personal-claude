-- ── TA12: Dead Tuple Health Monitor ─────────────────────────────────────────
-- Run on schedule (daily/weekly). Alert when dead_ratio_pct > 5 on tables > 100K rows.

SELECT
  schemaname,
  relname AS table_name,
  n_live_tup AS live_rows,
  n_dead_tup AS dead_rows,
  ROUND(n_dead_tup::numeric / NULLIF(n_live_tup + n_dead_tup, 0) * 100, 2) AS dead_ratio_pct,
  last_vacuum,
  last_autovacuum,
  last_analyze,
  n_mod_since_analyze
FROM pg_stat_user_tables
WHERE n_dead_tup > 10000
   OR (n_dead_tup::numeric / NULLIF(n_live_tup + n_dead_tup, 0)) > 0.05
ORDER BY n_dead_tup DESC;


-- ── TA13: Per-Table Autovacuum Scale Factor Override ─────────────────────────
-- Apply once to any table > 500K rows. Triggers vacuum at ~1% dead rows instead of 20%.

-- For large tables (> 500K rows)
ALTER TABLE stockadjustments SET (
  autovacuum_vacuum_scale_factor = 0.01,
  autovacuum_vacuum_threshold = 1000,
  autovacuum_analyze_scale_factor = 0.005,
  autovacuum_analyze_threshold = 500
);

-- For very large tables (> 5M rows)
ALTER TABLE your_large_table SET (
  autovacuum_vacuum_scale_factor = 0.005,
  autovacuum_vacuum_threshold = 1000
);

-- Verify the setting was applied
SELECT relname, reloptions
FROM pg_class
WHERE relname = 'stockadjustments';


-- ── TA14: REINDEX CONCURRENTLY — Non-Blocking Index Rebuild ──────────────────
-- Run after bloat > 30%. One index at a time. Does NOT lock the table (PostgreSQL 12+).

REINDEX INDEX CONCURRENTLY stockadjustments_pkey;
REINDEX INDEX CONCURRENTLY stockadjustments_adjusted_at_idx;
REINDEX INDEX CONCURRENTLY stockadjustments_sync_stock_seq_idx;

-- Monitor progress (run in a separate session)
SELECT phase, blocks_done, blocks_total,
       ROUND(blocks_done::numeric / NULLIF(blocks_total, 0) * 100, 1) AS pct_complete
FROM pg_stat_progress_create_index
WHERE relid = 'stockadjustments'::regclass;

-- Verify sizes after rebuild
SELECT indexrelname, pg_size_pretty(pg_relation_size(indexrelid)) AS size
FROM pg_stat_user_indexes
WHERE relname = 'stockadjustments'
ORDER BY pg_relation_size(indexrelid) DESC;
