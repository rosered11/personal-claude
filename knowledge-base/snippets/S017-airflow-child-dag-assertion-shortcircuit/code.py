"""
S017 — Airflow Child DAG Assertion + ShortCircuitOperator

Three-tier fix for TriggerDagRunOperator chains that show 'no status'.

Apply to: ds_inc_outbound_order (or any parent DAG using TriggerDagRunOperator chains)
Related: P012, D017
"""
from __future__ import annotations

from datetime import timedelta

import pendulum
from airflow.models import DagModel
from airflow.models.dag import DAG
from airflow.operators.python import PythonOperator, ShortCircuitOperator
from airflow.operators.trigger_dagrun import TriggerDagRunOperator

# ── Constants (same as parent DAG) ────────────────────────────────────────────
CO_LIST          = ['CDS', 'RBS']
_EXTRACT_TASK_ID = 'ds_inc_outbound_order_etl_data'

CHILD_DAG_IDS = [
    'spc_order_outbound_jda_staging_to_spc',
    'spc_order_outbound_jda_spc_to_wms',
]

# ── Tier 2, Fix 1: Child DAG availability assertion ───────────────────────────

def _assert_child_dags_active(**kwargs) -> None:
    """
    Fail fast and explicitly if any child DAG is missing or paused.

    Without this, a paused/missing child DAG causes the TriggerDagRunOperator
    to fail silently, leaving downstream tasks in 'no status'.

    Note: DagModel.get_dagmodel() is an internal Airflow API — test on version upgrades.
    """
    for dag_id in CHILD_DAG_IDS:
        dag = DagModel.get_dagmodel(dag_id)
        if dag is None:
            raise ValueError(
                f"Child DAG '{dag_id}' not found in Airflow — "
                f"ensure it is deployed and the scheduler has parsed it."
            )
        if dag.is_paused:
            raise ValueError(
                f"Child DAG '{dag_id}' is paused — "
                f"unpause it in the Airflow UI before running the parent DAG."
            )


assert_child_dags = PythonOperator(
    task_id='assert_child_dags_active',
    python_callable=_assert_child_dags_active,
    execution_timeout=timedelta(minutes=1),
)

# ── Tier 3: ShortCircuitOperator (Saga empty-batch short-circuit) ─────────────

def _has_any_batch(**kwargs) -> bool:
    """
    Returns True if at least one CO has a pending batch (non-empty dih_batch_id).

    When False, ShortCircuitOperator marks all downstream tasks as 'skipped' —
    a clean, expected state that distinguishes no-data runs from failures.
    """
    ti = kwargs['ti']
    return any(
        ti.xcom_pull(task_ids=_EXTRACT_TASK_ID, key=f'dih_batch_id_{co}')
        for co in CO_LIST
    )


short_circuit = ShortCircuitOperator(
    task_id='has_pending_batch',
    python_callable=_has_any_batch,
    # ignore_downstream_trigger_rules=False ensures ALL downstream tasks are skipped,
    # not just immediate successors.
    ignore_downstream_trigger_rules=False,
)

# ── DAG construction (partial — shows updated chain only) ────────────────────
# Full DAG setup (imports, constants, extract_task, trigger operators) unchanged.
#
# BEFORE:
#   extract_task >> staging_to_spc_cds >> spc_to_wms_cds >> staging_to_spc_rbs >> spc_to_wms_rbs
#
# AFTER:
#   assert_child_dags >> extract_task >> short_circuit >> staging_to_spc_cds >> ...

# assert_child_dags >> extract_task  (assert runs before extract)
extract_task = None  # placeholder — replace with actual PythonOperator reference
assert_child_dags >> extract_task  # type: ignore[operator]

# extract_task >> short_circuit >> [trigger chain]
extract_task >> short_circuit  # type: ignore[operator]

prev_task = short_circuit
for co in CO_LIST:
    co_lower = co.lower()

    staging_to_spc = TriggerDagRunOperator(
        task_id=f'spc_order_outbound_jda_staging_to_spc_{co_lower}',
        trigger_dag_id='spc_order_outbound_jda_staging_to_spc',
        poke_interval=5,
        reset_dag_run=False,
        wait_for_completion=True,
        allowed_states=['success'],
        failed_states=['failed'],
        conf={
            # Tier 2, Fix 3: or '' guard prevents None under render_template_as_native_obj=True
            'dih_batch_id': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='dih_batch_id_{co}') or '' }}}}"
            ),
            'app_name': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='app_name') or '' }}}}"
            ),
            'total_outbound_order_success': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='total_success_{co}') or '0' }}}}"
            ),
        },
    )

    spc_to_wms = TriggerDagRunOperator(
        task_id=f'spc_order_outbound_jda_spc_to_wms_{co_lower}',
        trigger_dag_id='spc_order_outbound_jda_spc_to_wms',
        poke_interval=5,
        reset_dag_run=False,
        wait_for_completion=True,
        allowed_states=['success'],
        failed_states=['failed'],
        conf={
            'dih_batch_id': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='dih_batch_id_{co}') or '' }}}}"
            ),
            'app_name': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='app_name') or '' }}}}"
            ),
            'total_outbound_order_success': (
                f"{{{{ ti.xcom_pull(task_ids='{_EXTRACT_TASK_ID}', key='total_success_{co}') or '0' }}}}"
            ),
        },
    )

    prev_task >> staging_to_spc >> spc_to_wms
    prev_task = spc_to_wms


# ── Tier 2, Fix 2: on_failure_callback deduplication ─────────────────────────
# BEFORE (in DAG definition):
#   with DAG(
#       ...,
#       on_failure_callback=_on_failure,           # DAG-level — fires on DAG failure
#       default_args={'on_failure_callback': _on_failure},  # REMOVE THIS LINE
#   ) as dag:
#
# AFTER:
#   with DAG(
#       ...,
#       on_failure_callback=_on_failure,           # DAG-level only
#       default_args={},                           # no callback in default_args
#   ) as dag:
#
# Reason: default_args callback fires per-task; DAG-level fires per-DAG.
# Both firing on the same failure sends double notifications and can mask
# the real failure if MsTeamsHook.send_failure() raises.
