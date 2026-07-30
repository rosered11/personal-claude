---
id: S028
slug: mssql-throttled-resumable-index-rebuild-runner
language: sql
when_to_use: "Use as a complete, standalone deployment script when a SQL Server ONLINE index-rebuild maintenance job (like TaskIndexRebuild) is causing production SQL command timeouts and has NOT yet had any fragmentation-gating, logging, or throttling applied. Creates the IndexMaintenanceSchedule and IndexMaintenanceLog tables, populates the schedule with the verified real index inventory (deduplicated), and replaces the procedure body with a combined fragmentation-gated + RunId-logged + WAIT_AT_LOW_PRIORITY-throttled + resumable rebuild loop -- all in one deployable unit."
related_problems: [P023]
related_decisions: [D028]
source: "Consolidated D027 (fragmentation gate + logging) + D028 (throttled pacing) design, populated from the verified TaskIndexRebuild source (inbox/rebuild-index-db/script-rebuild.sql, 2026-07-29) after confirming neither prior design had been deployed to production"
---

# S028 -- Consolidated Fragmentation-Gated, Logged, Throttled, Resumable Index Rebuild Runner (T-SQL)

## What This Solves
The revised P023 confirmed, via the real production script, that `TaskIndexRebuild` still has none of the fixes previously designed in D027 or D028 applied -- it is still the original ~195-statement, hardcoded day-of-week procedure with no fragmentation check, no logging, and no execution pacing. This script is the single, complete deployment artifact that closes that gap in one pass:

1. `dbo.IndexMaintenanceSchedule` -- one row per unique `(SchemaName, TableName, IndexName)` candidate, PK-constrained so duplicates (like the confirmed `PK_StoreLocation` double-entry on the old `@day=4`/`@day=6` branches) are impossible by construction. Populated here with the actual **194 unique candidates** extracted programmatically from the real script (195 total `ALTER INDEX` statements minus 1 confirmed duplicate).
2. `dbo.IndexMaintenanceLog` -- append-only, RunId-correlated log of every rebuild attempt (started/completed/status/duration), including `AbortedByLowPriority` and `WasResumable` flags.
3. `dbo.TaskIndexRebuild` (replaced) -- selects only candidates whose live fragmentation (`sys.dm_db_index_physical_stats`) exceeds a threshold, rebuilds each with `WAIT_AT_LOW_PRIORITY` (so the final schema-modification lock yields to live OLTP traffic instead of blocking it), paces successive rebuilds with a configurable delay, stops issuing new rebuilds once an off-peak window closes (untouched candidates simply stay fragmented and are naturally retried next run), and uses `RESUMABLE = ON` for indexes over a page-count threshold.

## Why This Matters Architecturally
- Directly targets both components of the P023 root cause: unconditional I/O volume (fragmentation gate) and lock-wait contention from unpaced back-to-back rebuilds (`WAIT_AT_LOW_PRIORITY` + pacing delay).
- Is now a complete, idempotent-to-deploy artifact grounded in the real candidate inventory, not an illustrative subset -- a DBA can run this once against the actual `OrderDb` baseline.
- The fragmentation gate doubles as free "resume across runs" state: anything deferred by the window guard or a low-priority abort simply remains fragmented and is picked up by the next scheduled run, with no separate checkpoint bookkeeping required.
- Structurally prevents the P022-identified `PK_StoreLocation` duplicate-rebuild defect from recurring, since the schedule table's primary key makes a duplicate entry a constraint violation rather than a silent copy-paste bug.
