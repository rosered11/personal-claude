import subprocess
import threading
import datetime
import logging
from airflow.providers.mysql.hooks.mysql import MySqlHook

# ---------------------------------------------------------------------------
# SAGA-STRUCTURED AIRFLOW PYTHONOPERATOR
# Demonstrates deferred batch ID commit pattern + parameterized SQL.
# The subprocess receives the pre-increment batch ID; the UPDATE that increments
# the counter only fires after the subprocess exits with code 0.
# ---------------------------------------------------------------------------

TIMEOUT_SUBPROCESS = 3480  # seconds; set to (execution_timeout_seconds - 120)


def run_dotnet_with_deferred_batch_id(
    spc_mysql_hook: MySqlHook,
    wms_mysql_hook: MySqlHook,
    app_path: str,
    process_type: str,
    env: dict,
    dih_batch_id: str,
    owner_id: str,
    total_outbound_order_success: str,
) -> None:
    """
    Saga-structured execution:
      Step 1 — read-only batch ID fetch (no UPDATE)
      Step 2 — subprocess run (gate)
      Step 3 — commit batch ID increment (only on success)
      Step 4 — write WMS + SPC control tables (parameterized)
    """

    # -----------------------------------------------------------------------
    # SAGA STEP 1: Read-only fetch. Do NOT update spc_batch_id here.
    # The subprocess needs the current value as env var — only the SELECT
    # belongs before subprocess launch.
    # -----------------------------------------------------------------------
    interface_info = spc_mysql_hook.get_first(
        sql=(
            "SELECT spc_batch_id FROM spc_interface_info"
            " WHERE interface_name = 'DS_INC_OUTBOUND_ORDER' LIMIT 1"
        )
    )
    spc_outbound_batch_id = interface_info[0]
    new_spc_outbound_batch_id = spc_outbound_batch_id + 1
    str_spc_outbound_batch_id = str(spc_outbound_batch_id)
    logging.info(
        f"spc_outbound_batch_id={spc_outbound_batch_id}  "
        f"new_spc_outbound_batch_id={new_spc_outbound_batch_id}"
    )

    # Inject batch ID into subprocess environment
    env["ETLNETJOB_SPC_BATCH_ID"] = str_spc_outbound_batch_id

    # -----------------------------------------------------------------------
    # SAGA STEP 2: Run subprocess. Raises on failure — no DB state mutated yet.
    # -----------------------------------------------------------------------
    proc = subprocess.Popen(
        ["dotnet", f"{app_path}/ETLCronjob.dll", f"--process-type={process_type}"],
        cwd=app_path,
        env=env,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
    )

    def _stream_output():
        for line in proc.stdout:
            print(line, end="")

    stream_thread = threading.Thread(target=_stream_output, daemon=True)

    try:
        print("=== .NET Logging Start ===")
        stream_thread.start()

        try:
            proc.wait(timeout=TIMEOUT_SUBPROCESS)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait()
            raise Exception(
                f".NET job exceeded {TIMEOUT_SUBPROCESS // 60}-minute hard limit and was killed"
            )

        stream_thread.join(timeout=5)
        print("=== .NET Logging End ===")

        exit_code = proc.returncode
        print("Exit code:", exit_code)
        if exit_code != 0:
            raise Exception(f".NET job failed with exit code {exit_code}")

    except Exception:
        if proc.poll() is None:
            proc.kill()
            proc.wait()
        raise
    finally:
        if proc.poll() is None:  # safety net — always clean up on any exit path
            proc.kill()
            proc.wait()

    # -----------------------------------------------------------------------
    # SAGA STEP 3: Subprocess succeeded — commit the batch ID increment.
    # This is the first DB write. If this fails, nothing downstream has run.
    # -----------------------------------------------------------------------
    spc_mysql_hook.run(
        sql=(
            "UPDATE spc_interface_info SET spc_batch_id = %s"
            " WHERE interface_name = 'DS_INC_OUTBOUND_ORDER'"
        ),
        parameters=(new_spc_outbound_batch_id,),
    )
    logging.info(f"spc_batch_id committed: {spc_outbound_batch_id} -> {new_spc_outbound_batch_id}")

    # -----------------------------------------------------------------------
    # SAGA STEP 4a: Insert WMS staging control table row — parameterized.
    # -----------------------------------------------------------------------
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    logging.info(
        f"Inserting WMS control table: batch={str_spc_outbound_batch_id} "
        f"owner={owner_id} dih={dih_batch_id}"
    )
    wms_mysql_hook.run(
        sql=(
            "INSERT INTO wms_staging.st_control_table"
            " (batch_number, interface_name, owner_id, tot_rec, tot_success, tot_fail,"
            "  control_table_status, create_by, create_date, update_by, update_date,"
            "  etl_batch_id, etl_system, source_batch_id, source_system)"
            " VALUES (%s, 'so', %s, %s, %s, %s, 'N', 'spc_etl', %s, 'spc_etl', %s, %s, %s, %s, %s)"
        ),
        parameters=(
            str_spc_outbound_batch_id, owner_id,
            total_outbound_order_success, 0, 0,
            now, now,
            dih_batch_id, "informatica", "", "JDA",
        ),
    )

    # -----------------------------------------------------------------------
    # SAGA STEP 4b: Update SPC control table status to 'C' — parameterized.
    # -----------------------------------------------------------------------
    logging.info(
        f"Updating SPC control table: dih_batch_id={dih_batch_id} "
        f"spc_batch_id={str_spc_outbound_batch_id}"
    )
    spc_mysql_hook.run(
        sql=(
            "UPDATE st_control_table"
            " SET status = %s, spc_batch_id = %s"
            " WHERE interface_name = 'DS_INC_OUTBOUND_ORDER'"
            "   AND dih_batch_id = %s"
        ),
        parameters=("C", str_spc_outbound_batch_id, dih_batch_id),
    )
