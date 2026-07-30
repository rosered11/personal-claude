---
id: P022
title: "Suspicious Audit-Logged Self-Insert Traced to Online Index Rebuild Job"
date: 2026-07-22
tags: [mssql, sql-server-audit, index-rebuild, online-index-operation, observability, security-false-positive, database-maintenance, alert-fatigue]
severity: medium
related_decisions: [D027]
related_snippets: [S027]
---

# Suspicious Audit-Logged Self-Insert Traced to Online Index Rebuild Job

## Problem
A SQL Server audit log captured `insert [dbo].[SubOrderItem] select * from [dbo].[SubOrderItem] with (index = 1)` around the same time window as the scheduled `[dbo].[TaskIndexRebuild]` stored procedure runs. The security/DBA team cannot currently confirm whether this statement is a benign artifact of the index-rebuild job or a genuine unauthorized data-modification event, because the audit trail carries no explicit correlation back to the maintenance job that (very likely) produced it.

## Root Cause
Undetermined with high confidence toward Hypothesis 1 -- verification requires one additional query against the audit data itself (see Constraints/Verification below):

1. **(Most likely)** The captured statement is SQL Server's own internally generated DML, not application- or attacker-issued SQL. `WITH (INDEX = 1)` is the legacy numeric table-hint syntax that always resolves to the table's clustered index (or base heap ordering). `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` against a clustered index (here, `PK_SubOrderItem` on `SubOrderItem`, rebuilt on the `@day=3` branch) requires SQL Server to scan the existing row order via the current clustered index while building the new index structure -- exactly what an ONLINE clustered rebuild does under the covers. The exact table match (`SubOrderItem`, rebuilt via multiple indexes across `@day=3, 5, 6`) and the tight time correlation the user already observed are strong corroborating evidence.
2. **(Less likely)** A separate scheduled job or ETL process explicitly issues a self-copy `INSERT...SELECT` for reorg/refresh purposes. Inconsistent with this codebase's established EF Core write path (see P010/P019/P021 in this KB) -- EF Core does not emit legacy numeric index hints, and no other scheduled job of this shape is referenced in the provided context.
3. **(Least likely, no supporting evidence)** Genuine unauthorized activity timed to coincide with the maintenance window as camouflage. Nothing in the evidence supports this beyond the coincidence itself.

**Verification action (should be performed before closing this as benign):** Pull `session_id`, `login_name`, `application_name`, and `host_name` for the flagged audit row and compare against the SQL Agent job session that runs `TaskIndexRebuild` (via `sys.dm_exec_sessions` / job history at the time of the run). If they match the Agent job's session identity, hypothesis 1 is confirmed without ambiguity.

## Summary
The specific query is very likely a harmless internal artifact of SQL Server's own ONLINE index-rebuild mechanism, not a security incident. However, the underlying architectural gap is real and will keep recurring: the SQL Server Audit trail has no built-in way to distinguish "engine-generated DML from a known, scheduled maintenance job" from "an attacker-issued statement that happens to look similar," creating false-positive investigation overhead today and risking alert fatigue (or a masked true incident) over time. Compounding this, `TaskIndexRebuild` unconditionally rebuilds ~190 indexes across a fixed day-of-week schedule regardless of actual fragmentation, generating a large, constant stream of engine-internal DML noise in the audit trail every day of the week -- the volume of noise is itself a contributing architectural cause, not just the lack of correlation metadata.

## Context
- `OrderDb` is the production order-domain SQL Server database (`SubOrderItem`, `Order`, `SubOrder`, `OrderItem`, `OrderPromotion`, and ~40 other tables), the same domain covered by prior KB entries P010, P019, P021 (all EF Core write-path problems against this SQL Server-backed order/activity system).
- `TaskIndexRebuild` is a single stored procedure (author-dated 2021-09-27) that branches on `DATEPART(dw, GETDATE())` into 7 hardcoded `IF/ELSE` blocks (`@day = 1` through `7`), each containing 20-30+ hardcoded `ALTER INDEX ... REBUILD WITH (ONLINE = ON)` statements against specific named indexes -- roughly 190 individual rebuild statements across the full week, none gated by an actual fragmentation check (e.g. `sys.dm_db_index_physical_stats`).
- A concrete secondary defect was found while reading the script: `PK_StoreLocation` on `OrderDb.dbo.StoreLocation` is rebuilt on **both** the `@day=4` and `@day=6` branches -- a duplicate/redundant entry that illustrates the maintainability risk of a hardcoded, copy-pasted branch structure (easy to introduce silent duplication or omission when moving an index between days).
- [MISSING: which tool captured the audit row (SQL Server Audit vs. Extended Events vs. a third-party SIEM), and whether session/login columns were already available but simply not checked yet]
- [MISSING: whether `TaskIndexRebuild` is invoked by a SQL Agent job, and what login/application_name that job step runs under]

## Constraints
- Cannot take `OrderDb` offline or interrupt production order traffic to investigate (implied by continued use of `ONLINE = ON` rebuilds).
- Any fix must not change the write-path application code (EF Core order services) -- this is purely a database-maintenance and observability concern.
- Verification and any remediation must be low-risk to apply against a live production database (no schema-breaking changes to order tables themselves).

## Affected Components
- `[dbo].[TaskIndexRebuild]` stored procedure (OrderDb)
- `OrderDb.dbo.SubOrderItem`, `.Order`, `.SubOrder`, `.OrderItem`, `.OrderPromotion`, and ~35 other OrderDb tables covered by the rebuild schedule
- SQL Server Audit / query-audit tooling consuming `OrderDb`'s DML activity
- `OrderDb.dbo.StoreLocation` (duplicate-rebuild defect specifically)
