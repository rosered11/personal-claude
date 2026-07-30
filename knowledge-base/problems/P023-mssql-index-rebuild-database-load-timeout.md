---
id: P023
title: "TaskIndexRebuild Execution Causes Production SQL Timeout Spikes"
date: 2026-07-29
tags: [mssql, index-rebuild, database-maintenance, sql-timeout, database-load-spike, throttling, resource-governance, online-index-operation, fragmentation-gating]
severity: high
related_decisions: [D028]
related_snippets: [S028]
---

# TaskIndexRebuild Execution Causes Production SQL Timeout Spikes

## Problem
Every time the `[dbo].[TaskIndexRebuild]` maintenance stored procedure runs against `OrderDb`, the database experiences a sharp load spike and concurrent application SQL transactions begin timing out. The user needs a rebuild approach that minimizes impact on live database traffic so it stops causing execution timeouts, delivered as one deployable "new/improved rebuild-index script."

## Root Cause (revised -- grounded in the actual production script, 2026-07-29)
This update corrects an assumption made in the original version of this record. The prior consultation could not obtain a real copy of `script-rebuild.sql` (the file supplied was byte-for-byte identical to the plain schema export) and so proceeded on the assumption that the KB-documented D027/S027 baseline (fragmentation gate + RunId logging) was already live in production, and scoped D028 purely to add execution-time pacing on top of that assumed state.

A second inbox submission (`inbox/rebuild-index-db/`) supplied the *actual* `script-rebuild.sql`. It is confirmed to be the original, pre-remediation `TaskIndexRebuild` body -- byte-for-byte matching the script body documented in P022 (unconditional day-of-week branching, ~195 hardcoded `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` statements, no fragmentation check, no logging, no throttling), including the same `PK_StoreLocation` double-rebuild defect on `@day=4` and `@day=6` identified in P022 (verified programmatically: 195 total `ALTER INDEX` statements, 194 unique `(table, index)` pairs, exactly one duplicate -- `StoreLocation.PK_StoreLocation`).

**This means neither D027's fix nor D028's fix has actually been applied to production.** The load-spike/timeout root cause is therefore the full compound of all three previously-separated concerns:
1. Unconditional rebuild of ~195 index operations regardless of actual fragmentation (I/O and transaction-log-flush volume).
2. No `WAIT_AT_LOW_PRIORITY` lock-yielding and no inter-rebuild pacing delay during execution (lock-wait contention against concurrent OLTP transactions -- the most direct cause of the reported command timeouts).
3. No off-peak execution-window guard and no `RESUMABLE = ON` for large indexes.

## Summary
The user's rebuild job for `OrderDb` is causing real production impact: SQL command timeouts on concurrent transactions whenever the maintenance window runs. This second submission finally provided a genuine copy of the old script (previously unavailable), which serendipitously also gives full byte-for-byte confirmation of both P022's and this problem's prior architectural analysis of `TaskIndexRebuild`'s structure. Because the real baseline turns out to have *none* of D027/D028's previously-designed fixes applied yet, this consultation's deliverable is a single, consolidated, production-ready script that merges the fragmentation gate + RunId logging (D027/S027 design) with the throttled/low-priority/resumable pacing (D028/S028 design) into one deployable unit, populated with the actual 194 verified `(table, index)` pairs extracted directly from the real script -- something the prior consultation could not produce without the real source file.

## Context
- `OrderDb` production SQL Server database; `[dbo].[TaskIndexRebuild]` is the same stored procedure covered by P022/D027/S027.
- `inbox/rebuild-index-db/script-rebuild.sql` (this submission) is the real, previously-unavailable "old script" -- 252 lines, 7 day-of-week branches, 195 `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` statements, 194 unique after removing the confirmed `PK_StoreLocation` duplicate.
- `inbox/rebuild-index-db/schema-database.sql` (this submission) is a genuine, distinct 3107-line / 98 `CREATE TABLE` schema export (confirmed via diff against `script-rebuild.sql` -- the two files are no longer identical, unlike the prior submission), consistent with the 98-table inventory referenced in the original version of this record.
- `SubOrderItem` (confirmed present in the schema export, ~90+ columns) is one of the widest and most heavily-indexed hot tables in the rebuild list (13 of its indexes appear across the week), consistent with its role in the EF Core order/activity write-path family (P010/P019/P021).
- [MISSING: SQL Server edition/version (Standard vs. Enterprise vs. Azure SQL Managed Instance) -- affects availability/behavior of `RESUMABLE = ON` and Resource Governor workload-group configuration]
- [MISSING: the application-side `CommandTimeout` value(s) actually configured for OrderDb connections]
- [MISSING: current SQL Agent job schedule/window for `TaskIndexRebuild`]

## Constraints
- Must not remove or regress the fragmentation-gating or `RunId`-correlated logging design established by D027/S027 -- the consolidated fix must include both, not just pacing, now that neither is confirmed live.
- Must minimize/eliminate SQL command-timeout incidents on concurrent OLTP transactions during the maintenance run -- primary success criterion.
- Prefer `ONLINE = ON` rebuild operations to avoid taking tables offline during business-adjacent hours.
- No changes to the application EF Core write path -- pure database-maintenance/ops concern.
- Deliverable must be an actual deployable script (not a conceptual description only), grounded in the real 194-entry index inventory rather than an illustrative subset.
- Low-risk, reversible change preferred; this is now effectively a first-time production deployment of the full D027+D028 design, not an incremental third change.

## Affected Components
- `[dbo].[TaskIndexRebuild]` stored procedure (OrderDb)
- `dbo.IndexMaintenanceSchedule`, `dbo.IndexMaintenanceLog` (S027/S028 design -- confirmed not yet created in production)
- `OrderDb.dbo.SubOrderItem`, `.Order`, `.SubOrder`, `.OrderItem`, `.OrderPromotion`, `.StoreLocation`, and ~92 other `OrderDb` tables per `schema-database.sql`
- Concurrent OLTP application transactions against `OrderDb` during the maintenance execution window
