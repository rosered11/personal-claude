---
id: S027
slug: mssql-fragmentation-gated-index-maintenance-runner
language: sql
when_to_use: "Use when a SQL Server maintenance job (index rebuild/reorganize, or any other engine-internal-DML-generating job) needs to be correlatable against audit/security logs, and/or when a hardcoded day-of-week or copy-pasted index list has grown error-prone (duplicate or missing entries). Replaces a fixed unconditional rebuild list with a fragmentation-gated, config-driven schedule plus a queryable per-run log."
related_problems: [P022]
related_decisions: [D027]
source: TaskIndexRebuild refactor
---

# S027 -- Fragmentation-Gated, Logged Index Maintenance Runner (T-SQL)

## What This Solves
D027 chose to replace `TaskIndexRebuild`'s seven hardcoded day-of-week `IF/ELSE` branches (~190 unconditional `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` statements, including a discovered duplicate rebuild of `PK_StoreLocation` on both `@day=4` and `@day=6`) with:

1. A config table (`dbo.IndexMaintenanceSchedule`) listing each table/index pair once, with an optional day-of-week affinity -- a duplicate entry becomes a primary-key violation instead of a silent copy-paste bug.
2. A fragmentation gate (`sys.dm_db_index_physical_stats`) so only indexes actually over a threshold get rebuilt, cutting the daily volume of engine-generated DML that lands in the audit trail.
3. A `RunId`-correlated log (`dbo.IndexMaintenanceLog`) that records session_id/start/end per rebuild, set into `sp_set_session_context` so any future "was this audited statement caused by maintenance?" question is a single query away.

## Why This Matters Architecturally
- Directly closes the P022 gap: any future audit row occurring inside an active `RunId`'s time window is now provably attributable to a known maintenance run, not a guess based on rough time correlation.
- Fragmentation gating is the actual root-cause fix for audit noise volume -- rebuilding indexes that are not fragmented was pure waste generating pure noise.
- The log table is intentionally shaped as an append-only run/event record so it can later be tailed by a SIEM connector (the deferred Lens B option from D027) without a redesign.
