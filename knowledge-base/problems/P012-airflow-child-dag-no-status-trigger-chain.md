---
id: P012
title: "Airflow DAG — Child DAGs Show 'No Status' After Main DAG Completes"
date: 2026-04-24
tags: [airflow, python, orchestration, trigger-dagrun, xcom, jinja, child-dag, dag-dependency, etl, debugging]
severity: high
related_decisions: [D017]
related_snippets: [S017]
---

# Airflow DAG — Child DAGs Show 'No Status' After Main DAG Completes

## Problem

After `ds_inc_outbound_order` (main DAG) completes its `extract_task`, the first `TriggerDagRunOperator` task (`spc_order_outbound_jda_staging_to_spc_cds`) and all downstream tasks show "no status" in the Airflow UI. The child DAG chain (`spc_order_outbound_jda_staging_to_spc` → `spc_order_outbound_jda_spc_to_wms`) never initiates.

## Root Cause

Multiple compounding causes in priority order:

1. **Upstream `extract_task` failure** — When `extract_task` fails (import error in `MsTeamsHook`, MySQL connection failure, or any uncaught exception), all downstream `TriggerDagRunOperator` tasks are never scheduled. Airflow marks them as task instances in the DAG run but they remain in "no status" (None state) because their `trigger_rule=ALL_SUCCESS` requirement is not met.

2. **Child DAG paused or not deployed** — `TriggerDagRunOperator` fires and creates a child DAG run that immediately fails (or the trigger itself raises), causing the parent trigger task to fail and all further downstream tasks to go `upstream_failed` / "no status".

3. **`render_template_as_native_obj=True` type contract violation** — The main DAG sets `render_template_as_native_obj=True`. Jinja templates in `TriggerDagRunOperator.conf` that call `xcom_pull()` return `None` (Python native) rather than empty string when no value exists. The child DAG receives `conf={'dih_batch_id': None}` instead of `''`.

4. **`on_failure_callback` double-registration** — Registered at both DAG-level (`on_failure_callback=_on_failure`) and in `default_args={'on_failure_callback': _on_failure}`. If `MsTeamsHook.send_failure()` raises, both fire and can produce confusing cascading errors that mask the real failure.

5. **No empty-batch short-circuit** — When no batch is found for any CO, the chain still triggers all child DAGs, which succeed immediately via their own `if not dih_batch_id: return` guard. This wastes scheduling resources and pollutes DAG run history.

## Constraints

- Child DAGs must run sequentially: CDS chain completes before RBS chain starts
- `TriggerDagRunOperator` uses `wait_for_completion=True` — parent task blocks until child DAG finishes
- `CO_LIST = ['CDS', 'RBS']` — both CO chains trigger the same `trigger_dag_id` targets
- XCom values are explicitly pushed as strings (empty string `''` when no batch found)
- `render_template_as_native_obj=True` is set on the main DAG

## Affected Components

- `ds_inc_outbound_order` DAG — `TriggerDagRunOperator` chain construction and `on_failure_callback` registration
- `spc_order_outbound_jda_staging_to_spc` child DAG — must be deployed and active
- `spc_order_outbound_jda_spc_to_wms` child DAG — must be deployed and active
- XCom / Jinja template rendering under `render_template_as_native_obj=True`
- `MsTeamsHook` import path — parse-time import failure causes entire DAG to not load
