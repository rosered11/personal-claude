---
id: D009
chosen_option: "Daemon thread for stdout streaming + proc.wait(timeout) for hard kill"
problem_id: P009
tags: [airflow, python, subprocess, timeout, orchestration, threading]
related_snippets: [S009]
---

# Decision: Subprocess Hard Timeout via Daemon Thread + proc.wait(timeout)

## Context

`for line in proc.stdout` blocks the main thread until stdout closes (process exits). `proc.wait(timeout=1680)` placed after the loop is unreachable while the process is running. `except subprocess.TimeoutExpired` is dead code — Airflow raises `AirflowTaskTimeout` (`BaseException`), not `subprocess.TimeoutExpired`. There is no independent subprocess kill before Airflow's 30-min `execution_timeout` fires.

## Options Considered

1. **proc.communicate(timeout=N)** — supports hard timeout; buffers all stdout in memory (unacceptable — live log streaming is a hard requirement).
2. **keep `for line in proc.stdout` + `proc.wait(timeout)` after loop** — `proc.wait()` is never reached while the process runs; effectively no timeout.
3. **Daemon thread for stdout streaming + `proc.wait(timeout=N)` on main thread** — stdout streams live via the thread; main thread enforces hard wall-clock timeout and kills the process if exceeded.

## Decision

Move `for line in proc.stdout` into a `daemon=True` thread. On the main thread, call `proc.wait(timeout=HARD_TIMEOUT_SECONDS)`. If `subprocess.TimeoutExpired` is raised, call `proc.kill()` and join the stdout thread. Set `HARD_TIMEOUT_SECONDS` to `execution_timeout_seconds - 120` (2-minute safety buffer before Airflow's own timeout fires). Remove the dead `except subprocess.TimeoutExpired` branch from the outer loop.

## Consequences

- Live stdout streaming preserved — daemon thread reads lines without blocking main thread.
- Hard wall-clock kill guaranteed — `proc.wait(timeout=N)` on main thread fires independently of stdout activity.
- 2-minute buffer ensures the subprocess is dead before Airflow kills the worker.
- Dead `except subprocess.TimeoutExpired` branch removed — no false confidence.
- Daemon thread is automatically joined when main thread exits, preventing zombie threads.
