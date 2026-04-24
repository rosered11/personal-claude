---
when_to_use: "When a TriggerDagRunOperator chain shows 'no status' for downstream tasks. Apply assertion task to fail fast on child DAG unavailability, ShortCircuitOperator to clean-skip empty-batch runs, and or-guard in Jinja XCom expressions under render_template_as_native_obj=True."
related_problems: [P012]
related_decisions: [D017]
---

# S017 — Airflow Child DAG Assertion + ShortCircuitOperator

Three-tier fix for TriggerDagRunOperator chains where child tasks show "no status":

1. `assert_child_dags_active` — PythonOperator that fails fast if any child DAG is missing or paused
2. `has_pending_batch` — ShortCircuitOperator that short-circuits the trigger chain when no batch was found for any CO
3. Jinja `or ''` guard — prevents None being passed as dih_batch_id under render_template_as_native_obj=True

Apply all three in sequence: `assert_child_dags >> extract_task >> short_circuit >> [trigger chain]`

Also remove `on_failure_callback` from `default_args` — keep only at DAG level.
