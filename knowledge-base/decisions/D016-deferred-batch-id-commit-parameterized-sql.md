---
id: D016
chosen_option: "Defer spc_batch_id UPDATE to post-subprocess success + parameterize all SQL"
problem_id: P011
tags: [airflow, python, etl, mysql, subprocess, correctness, sql-injection, orchestration, batch-processing, saga]
related_snippets: [S016]
---

# Decision: Deferred Batch ID Commit + Parameterized SQL for Airflow DAG

## Context

`spc_batch_id` was committed (via `UPDATE spc_interface_info`) before the `.NET` subprocess ran, meaning a subprocess failure permanently consumed a batch ID with no work done. Two SQL statements also used Python `.format()` string interpolation with external `dag_run.conf` values, creating a SQL injection surface.

## Options Considered

1. **Saga restructure — defer batch ID UPDATE to post-subprocess (chosen):** Keep the SELECT before subprocess (required — .NET needs the value as an env var), move the UPDATE to after subprocess success. Parameterize all SQL using MySqlHook's native `parameters=` interface. No new infrastructure; surgical reorder of existing lines.

2. **Hexagonal Architecture — port-isolated adapters:** Extract subprocess execution, batch ID management, and control table writes into formal port/adapter classes. Application core becomes a clean sequence of port calls, structurally preventing sequencing errors. High complexity; fights Airflow's PythonOperator convention; disproportionate abstraction overhead for a single-task DAG.

3. **Transactional rollback — wrap everything in a DB transaction:** Wrap the batch ID UPDATE and subprocess call in a single MySQL transaction, rolling back the UPDATE if subprocess fails. Not viable: MySQL transactions cannot span a subprocess call; the DB connection would hold an open transaction for the entire subprocess duration (up to 58 minutes), causing lock contention.

## Decision

Apply a **Saga-structured reorder**:

- **SAGA Step 1 (pre-subprocess):** `SELECT spc_batch_id` — read-only; no state mutation.
- **SAGA Step 2:** Run `.NET` subprocess (with D009 daemon-thread timeout pattern).
- **SAGA Step 3 (post-subprocess, on success only):** `UPDATE spc_interface_info SET spc_batch_id = %s` — committed only after subprocess exits with code 0.
- **SAGA Step 4a:** `INSERT INTO wms_staging.st_control_table` using `parameters=()`.
- **SAGA Step 4b:** `UPDATE st_control_table SET status = %s, spc_batch_id = %s WHERE ... AND dih_batch_id = %s` using `parameters=()`.

All SQL statements that accept external input are converted to MySqlHook `run(sql, parameters=(...))` parameterized form.

## Consequences

- Batch ID only advances when the `.NET` job completes successfully — no wasted counter increments on failure.
- SQL injection surface eliminated on all four SQL statements in the function.
- If subprocess succeeds but SAGA Step 4a or 4b fails, spc_batch_id has been committed but WMS/SPC control tables are inconsistent. A task retry (Airflow) re-runs from Step 1 using the same `dih_batch_id`; Steps 4a/4b should be made idempotent (e.g., `INSERT ... ON DUPLICATE KEY UPDATE`) to handle this safely — this is the remaining partial-saga risk.
- The `.NET` subprocess still receives the correct (pre-increment) `spc_batch_id` via env var — the SELECT position is unchanged.

## References

- D009 — Subprocess Hard Timeout via Daemon Thread (already applied in this DAG; sequencing fix is additive)
- P007 — Documents the same spc_batch_id premature-increment bug class in `ds_outbound_order` DAG
