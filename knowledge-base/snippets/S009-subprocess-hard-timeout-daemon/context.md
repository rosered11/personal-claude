---
id: S009
slug: subprocess-hard-timeout-daemon
language: python
when_to_use: "Use when an Airflow PythonOperator calls a long-running subprocess (dotnet, java, binary) AND live stdout streaming to Airflow logs is required AND a hard wall-clock kill before Airflow's execution_timeout is needed. Set TIMEOUT_SUBPROCESS = execution_timeout_seconds - 120."
related_problems: [P009]
related_decisions: [D009]
source: TA25
---

# Subprocess Hard Timeout via Daemon Thread (Python / Airflow)

Solves the conflict between `for line in proc.stdout` (blocks main thread) and `proc.wait(timeout=N)` (unreachable while process runs). Moves stdout streaming to a daemon thread so the main thread can enforce a wall-clock kill via `proc.wait(timeout=N)`.

## Why daemon=True

The stream thread is a daemon, so it is automatically terminated when the main thread exits. No `thread.join()` needed on the failure path — avoids hanging the worker if the stream thread blocks on a dead pipe.

## TIMEOUT_SUBPROCESS formula

```python
TIMEOUT_SUBPROCESS = int(execution_timeout.total_seconds()) - 120
```

The 2-minute buffer ensures the subprocess is dead before Airflow kills the Python worker process itself, which would leave an orphan subprocess on the server.
