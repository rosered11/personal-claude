---
id: D027
chosen_option: "Structured Maintenance-Run Logging (RunId + session correlation) with Fragmentation-Gated, Config-Driven Rebuild Scope -- over Event-Driven Maintenance-Window Publication to an External SIEM"
problem_id: P022
tags: [mssql, sql-server-audit, index-rebuild, observability, security-false-positive, database-maintenance, layered-architecture, event-driven-architecture]
related_snippets: [S027]
---

# Decision: Instrumented, Fragmentation-Gated Index Maintenance Over External Event Publication

## Context
P022 established that the audited `INSERT...SELECT ... WITH (INDEX = 1)` against `SubOrderItem` is very likely an internal artifact of `TaskIndexRebuild`'s `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` calls, not a security incident -- but the audit trail has no way to prove or disprove that correlation today, and the maintenance job itself unconditionally rebuilds ~190 indexes every week regardless of fragmentation, generating the very audit noise that made this statement hard to triage in the first place.

## Options Considered

### Lens A -- Layered Architecture: Instrumentation-at-the-Source
Add a structured logging layer directly inside `TaskIndexRebuild`: generate a `RunId` (`NEWID()`) at the top of the procedure, set it via `sp_set_session_context`, log every `ALTER INDEX` call's table/index/start/end/duration/session_id to a new `dbo.IndexMaintenanceLog` table, and replace the hardcoded day-of-week branches with a config-driven, fragmentation-gated loop (`sys.dm_db_index_physical_stats`) so only indexes that actually need rebuilding are touched. Low blast radius (single stored procedure), no new infrastructure, immediately gives a queryable answer ("was RunId X active when audit row Y fired?") for this and every future occurrence, and directly reduces the volume of engine-internal DML the audit trail has to carry.

### Lens B -- Event-Driven Architecture: Maintenance-Window Event Publication
Publish explicit "maintenance window started/completed" events (e.g. to Service Broker, an Event Grid topic, or a lightweight events table tailed by the SIEM/Log Analytics pipeline) that the security monitoring layer subscribes to, so alerting logic can automatically annotate or suppress audit rows falling inside a signed, active maintenance window -- decoupling the correlation problem from SQL Server internals into the monitoring layer, and generalizing beyond this one stored procedure to any future scheduled job (backups, other ETL).

## Decision
**Lens A (Layered Architecture / instrumentation-at-the-source) is chosen as the primary fix**, with one concrete element borrowed from Lens B: the new `IndexMaintenanceLog` table is deliberately structured as an append-only, event-shaped log (RunId, StartedAt, CompletedAt, Status) specifically so a SIEM connector could tail it later without requiring any new pub/sub infrastructure today.

Rationale:
- No named SIEM/event pipeline exists yet for this problem (unlike the OMS application's own Kafka/RabbitMQ event infra covered in P013/P018/P021) -- building genuine event publication (Lens B) would be speculative new infrastructure investment against an unconfirmed consumer, and carries the highest-effort, lowest-certainty payoff of the two options.
- Lens A directly answers the question the user actually asked ("was this caused by the rebuild job?") for this specific incident and every future one, using only a stored-procedure change plus one new logging table -- fully reversible, no new production dependencies.
- Lens A also fixes the root contributor to audit noise volume: switching from "rebuild all ~190 indexes unconditionally every week" to "rebuild only indexes over a fragmentation threshold" cuts the number of ONLINE rebuild operations (and therefore the number of internally-generated DML statements landing in the audit trail) on most days, directly reducing future false-positive surface area rather than just logging around it.
- The config-driven index list (replacing the seven hardcoded `IF/ELSE` branches) eliminates the discovered `PK_StoreLocation` double-rebuild defect by construction -- a table-driven schedule cannot silently duplicate an entry the way copy-pasted branches can.
- Lens B's core idea is not discarded -- the log table is shaped so that if a SIEM integration is justified later (e.g. after this pattern recurs for a different job), it can subscribe to the same structured RunId events without another redesign.

## Consequences
- **Accepted trade-off:** Lens A does not, by itself, generalize automatically to *other* scheduled jobs (backups, ETL) the way a shared event bus would -- each job that needs this correlation must adopt the same logging pattern individually until/unless a real event-publication layer is built.
- **Accepted trade-off:** This still relies on someone manually querying `IndexMaintenanceLog` against the audit trail during an investigation -- it is not (yet) automatic suppression/annotation of audit alerts.
- **Benefit:** Immediately verifiable (query `IndexMaintenanceLog` for the RunId active at the audited timestamp), reversible, and low-risk to deploy against a live production database.
- **Benefit:** Reduces the daily volume of ONLINE-rebuild-generated audit noise by gating on actual fragmentation instead of an unconditional weekly sweep.
- **Benefit:** Removes an entire class of scheduling defects (the `PK_StoreLocation` duplicate) by replacing hardcoded day branches with a data-driven schedule.
- **Follow-up:** If a SIEM/monitoring pipeline is introduced later (e.g. Azure Sentinel, Splunk), promote `IndexMaintenanceLog` to a real event source (Lens B) rather than re-deriving the correlation logic from scratch.
