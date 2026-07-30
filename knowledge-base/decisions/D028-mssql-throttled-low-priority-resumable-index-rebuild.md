---
id: D028
chosen_option: "Consolidated, Deployment-Ready Script: Fragmentation Gate + RunId Logging + Low-Priority Throttled/Resumable Rebuild, Populated From the Verified 194-Entry Index Inventory -- over Queue-Dispatched, Backpressure-Throttled Rebuild Workers"
problem_id: P023
tags: [mssql, index-rebuild, database-maintenance, sql-timeout, database-load-spike, throttling, resource-governance, online-index-operation, fragmentation-gating, layered-architecture, event-driven-architecture]
related_snippets: [S028]
---

# Decision: Consolidated Throttled + Fragmentation-Gated Rebuild Script Over Queue-Based Worker Decoupling

## Context
P023 (revised) established that the actual production `TaskIndexRebuild` script -- now confirmed via the real `script-rebuild.sql` -- has none of the previously-designed D027 (fragmentation gate, RunId logging) or D028 (throttled pacing) fixes applied. Both were designed against this exact object but never deployed. The real script also confirms the specific 194 unique `(table, index)` maintenance candidates and the `PK_StoreLocation` duplicate defect first identified in P022.

## Options Considered

### Lens A -- Layered Architecture: Single-Procedure Combined Fix
Extend `TaskIndexRebuild` in place with the full combined design in one deployable unit: config-driven `IndexMaintenanceSchedule` (populated with the real 194-row inventory extracted directly from the verified script, structurally eliminating the `PK_StoreLocation` duplicate), `IndexMaintenanceLog` for RunId-correlated observability, `sys.dm_db_index_physical_stats`-gated candidate selection, `WAIT_AT_LOW_PRIORITY` lock-yielding on every rebuild, an inter-rebuild `WAITFOR DELAY`, an off-peak execution-window guard, and `RESUMABLE = ON` for large indexes. No new infrastructure; fully additive and reversible.

### Lens B -- Event-Driven Architecture: Queue-Dispatched, Backpressure-Throttled Rebuild Workers
Decompose the rebuild job into a Service Broker producer/consumer pipeline: a producer enqueues fragmentation-gated candidates; a worker pool dequeues and executes rebuilds one at a time, checking live load signals before proceeding and backing off under load. Re-evaluated here specifically because this run confirms zero production infrastructure investment has been made toward *either* path yet, making this the last reasonable checkpoint to reconsider the queue/worker alternative before committing to the in-procedure approach for good.

## Decision
**Lens A (Layered Architecture / single consolidated procedure) is chosen and reconfirmed**, now delivered as one complete, deployment-ready script rather than two conceptually separate increments.

Rationale:
- The new evidence (neither D027 nor D028 was ever deployed) argues *against* adding a third layer of complexity (Lens B's queue/worker infrastructure) before the first, simplest layer has even been proven in production -- deploying the higher-risk option first would compound, not reduce, delivery risk.
- `WAIT_AT_LOW_PRIORITY` remains SQL Server's own purpose-built mechanism for the most likely direct cause of the reported timeouts (lock-wait contention on the final schema-modification lock), and the fragmentation gate remains the direct fix for unconditional I/O volume -- both root-cause components identified in the revised P023 must ship together now, since neither is live.
- This is now a genuinely low-risk *first* deployment rather than a third incremental change: the consolidated script is additive only (new tables, `CREATE OR ALTER PROCEDURE`), fully reversible, and for the first time backed by the real, complete 194-entry candidate list instead of an illustrative subset -- removing the guesswork the prior version of this decision had to work around.
- Lens B's infrastructure investment (Service Broker queue, activation procedures, poison-message handling) remains disproportionate to a single maintenance job with no existing queue/worker infrastructure in `OrderDb`, and would regress the RunId-correlated observability this design already provides in one queryable log.
- This decision does not contradict the original D027/D028 designs -- it merges and completes them using verified real-world data, correcting only the deployment-state assumption, not the architectural reasoning.

## Consequences
- **Accepted trade-off:** `RESUMABLE = ON` has edition/version prerequisites that must be verified against the actual `OrderDb` SQL Server edition before relying on it; the off-peak window guard alone is the fallback for large indexes if unavailable.
- **Accepted trade-off:** The maintenance loop remains single-threaded/sequential -- rebuilding all 194 candidates may now span multiple maintenance windows on first run, since none have been rebuilt under the new gated/paced regime yet.
- **Accepted trade-off:** `WAIT_AT_LOW_PRIORITY` can defer a rebuild indefinitely on an always-busy table -- requires monitoring `IndexMaintenanceLog` for repeated "Deferred" entries.
- **Benefit:** For the first time, this is a complete, self-contained, deployable artifact (schema + population data + procedure) rather than a design description -- directly answers the user's request for "a new/improved rebuild-index script," not just an architectural recommendation.
- **Benefit:** Structurally eliminates the confirmed `PK_StoreLocation` duplicate by construction (PK constraint on the schedule table) and gives immediate RunId-correlated observability from the very first production run.
- **Follow-up:** If the maintenance scope grows significantly beyond this one database/procedure, or if this first deployment reveals the single-threaded window is insufficient, revisit Lens B -- the deferred queue/worker model generalizes across jobs in a way a single stored procedure's throttling parameters do not.
