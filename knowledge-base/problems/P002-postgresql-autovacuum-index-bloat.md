---
id: P002
title: "PostgreSQL Dead Tuple Bloat — stockadjustments Autovacuum Never Fired"
date: 2026-03-30
tags: [postgresql, autovacuum, index-bloat, maintenance, storage, performance]
severity: high
related_decisions: [D002]
related_snippets: [S002]
---

# PostgreSQL Dead Tuple Bloat — stockadjustments Autovacuum Never Fired

## Problem

The `stockadjustments` table (spc_inventory) accumulated 702,783 dead rows (14.50% dead ratio) with `last_autovacuum = NULL` — autovacuum had never fired. Silent degradation: slower range queries, ~1.07 GB index bloat across 6 indexes (63% sparse B-tree), every PK lookup traversing 85% empty pages.

## Root Cause

Default `autovacuum_vacuum_scale_factor = 0.20` requires 828,932 dead rows to trigger on a 4M-row table. Actual dead rows (702,783) never reached the threshold — autovacuum never fired. After manual VACUUM, index files retained bloat: VACUUM marks pages "reusable" but does not compact or shrink index files. REINDEX CONCURRENTLY was required separately.

## Constraints

- Production inventory system — cannot take table offline (no VACUUM FULL)
- Must reclaim ~1.07 GB index space without downtime
- PostgreSQL 12+

## Affected Components

- `stockadjustments` table: 4,144,411 live rows
- 6 indexes: pkey (85% bloat), adjusted_at_idx (85%), sync_stock_seq_idx (85%), adjustment_type_idx (76%), product_id_idx (26%), stock_id_idx (13%)
- `spc_inventory` schema
