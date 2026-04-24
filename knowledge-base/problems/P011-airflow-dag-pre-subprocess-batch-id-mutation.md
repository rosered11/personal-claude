---
id: P011
title: "Airflow DAG — spc_batch_id Incremented Before Subprocess Runs, No SQL Parameterization"
date: 2026-04-24
tags: [airflow, python, etl, mysql, subprocess, correctness, sql-injection, orchestration, batch-processing]
severity: high
related_decisions: [D016]
related_snippets: [S016]
---

# Airflow DAG — spc_batch_id Incremented Before Subprocess Runs, No SQL Parameterization

## Problem

In `ds_spc_order_outbound_jda_spc_to_wms.py`, the `spc_batch_id` counter in `spc_interface_info` is fetched and immediately **committed** (via `UPDATE`) before the `.NET` subprocess is launched. If the subprocess fails (non-zero exit code, timeout, or exception), the batch ID has already been permanently incremented — the counter advances even though no work was done. This causes batch ID gaps in SPC's tracking table and potential mismatches between SPC and WMS systems.

Additionally, two SQL statements (`INSERT INTO wms_staging.st_control_table` and `UPDATE st_control_table`) use Python `.format()` string interpolation with values sourced from `dag_run.conf` (external input: `owner_id`, `dih_batch_id`), creating a SQL injection surface.

## Root Cause

The original developer placed the `spc_batch_id` UPDATE immediately after the SELECT as a single logical block ("fetch and increment"), without recognizing that all DB state mutations must be deferred until after the subprocess confirms success. The subprocess is the critical gate — everything before it is setup; everything after it is commit. The premature UPDATE crosses that gate.

SQL injection: copy-paste from an earlier version of the DAG that used `.format()` for all SQL construction. MySqlHook's `run(sql, parameters=(...))` interface was not used, leaving `%s` parameterization unapplied.

## Constraints

- `max_active_runs=1` prevents concurrent batch ID collisions between DAG runs, but does not protect against intra-run failures
- The `.NET` subprocess must receive `spc_batch_id` as an environment variable before it runs — so the SELECT must happen before subprocess launch; only the UPDATE must be deferred
- `dag_run.conf` is the sole source of `owner_id` and `dih_batch_id` — these values come from the triggering parent DAG and cannot be sanitized at source

## Affected Components

- `ds_spc_order_outbound_jda_spc_to_wms.py` — `run_dotnet_exe()` function
- `spc_interface_info` table — `spc_batch_id` column
- `wms_staging.st_control_table` — INSERT statement
- `spc.st_control_table` — UPDATE statement
