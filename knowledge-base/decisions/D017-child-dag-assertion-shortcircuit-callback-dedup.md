---
id: D017
title: "Child DAG Assertion Task + ShortCircuitOperator + Callback Dedup for TriggerDagRunOperator Chain"
date: 2026-04-24
chosen_option: "Hexagonal adapter contract hardening (assertion task + callback dedup + Jinja or-guard) with Saga ShortCircuitOperator as hardening layer"
problem_id: P012
tags: [airflow, python, orchestration, trigger-dagrun, xcom, jinja, child-dag, dag-dependency, etl, debugging]
related_snippets: [S017]
---

# Child DAG Assertion Task + ShortCircuitOperator + Callback Dedup for TriggerDagRunOperator Chain

## Chosen Option

Hexagonal adapter contract hardening (assertion task + callback dedup + Jinja `or ''` guard), supplemented by a Saga-pattern `ShortCircuitOperator` for empty-batch short-circuiting.

## Context

The "no status" symptom in a `TriggerDagRunOperator` chain is caused by one or more adapter contract violations: upstream task failure (including import errors), child DAG unavailability, or Jinja/XCom type mismatch under `render_template_as_native_obj=True`. The fix is tiered by immediacy and invasiveness.

## Decision

### Tier 1 — Diagnostic (immediate, no code change)

Before touching code, perform these checks in the Airflow UI:

1. Open `ds_inc_outbound_order` → Grid view → find the run where tasks show "no status"
2. Click `ds_inc_outbound_order_etl_data` (extract_task) → check logs for:
   - ImportError on `MsTeamsHook` or any other module
   - MySQL connection failure
   - Any uncaught exception in the task function
3. In Airflow UI → DAGs list, verify `spc_order_outbound_jda_staging_to_spc` and `spc_order_outbound_jda_spc_to_wms` are listed, Active (toggle = on), and have no import errors (no red warning icon)
4. If child DAGs are paused: unpause them and re-trigger the parent DAG run

### Tier 2 — Code Fixes (Hexagonal adapter hardening)

**Fix 1: Add `assert_child_dags_active` task**

Add a `PythonOperator` task before `extract_task` that verifies all child DAGs are deployed and active. Fails fast with a clear error message if any child DAG is missing or paused, making the root cause visible in the DAG graph rather than in "no status" downstream.

**Fix 2: Remove `on_failure_callback` from `default_args`**

Keep `on_failure_callback=_on_failure` at DAG level only. Remove it from `default_args`. Double-registration causes dual notifications and potential error cascade if the callback itself raises.

**Fix 3: Add `or ''` guard in Jinja XCom expressions**

Under `render_template_as_native_obj=True`, `xcom_pull()` returning `None` produces a Python `None` object in `conf`. Use `or ''` to coerce to empty string:

```
{{ ti.xcom_pull(task_ids='...', key='dih_batch_id_CDS') or '' }}
```

### Tier 3 — Hardening (Saga pattern short-circuit)

**Add `ShortCircuitOperator` after `extract_task`**

When no batch is found for any CO, the `ShortCircuitOperator` short-circuits all downstream tasks, marking them as "skipped" rather than triggering child DAGs with empty `dih_batch_id`. "Skipped" is a clean, expected state — distinguishable from "no status" (unexpected failure).

## Rationale

- **Lens A (Saga):** The trigger chain is a multi-step workflow with no explicit failure boundaries. Adding `ShortCircuitOperator` makes the empty-batch no-op path explicit and visible. Without it, child DAGs are triggered unnecessarily.
- **Lens B (Hexagonal):** `render_template_as_native_obj=True` is an adapter that changes the Jinja output type contract. Child DAG availability is an external adapter dependency. Both must be explicitly validated at the adapter boundary before the workflow proceeds.
- **Synthesis:** Hexagonal fixes are lower-risk and more immediate (assertion task + 1-line callback fix + Jinja guard). Saga's `ShortCircuitOperator` is additive hardening. Combined, they address all five root causes identified in P012.

## Tradeoffs Accepted

- `DagModel.get_dagmodel()` is an internal Airflow API — may need adjustment on Airflow version upgrades
- Adding two new tasks (`assert_child_dags_active`, `has_pending_batch`) changes the DAG graph — Airflow will create new task instances and may require clearing old task states on first deploy
- `ShortCircuitOperator` with `ignore_downstream_trigger_rules=False` marks ALL downstream tasks as skipped — acceptable for this sequential chain

## Supersedes / Extends

- Extends D007 (SQLAlchemy future=True pattern for Airflow DAGs) — same DAG family, different failure mode
- Extends D016 (Deferred batch ID commit) — same DAG family, same `dih_batch_id` flow
- Does not contradict D009 (subprocess hard timeout) — subprocess pattern applies to child DAGs, not parent trigger chain
