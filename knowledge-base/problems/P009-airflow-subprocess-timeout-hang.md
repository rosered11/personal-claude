---
id: P009
title: "Airflow DAG — Dead subprocess.TimeoutExpired Branch and No Hard Subprocess Timeout"
date: 2026-04-22
tags: [airflow, python, subprocess, timeout, orchestration, threading, dead-code]
severity: high
related_decisions: [D009]
related_snippets: [S009]
---

# Airflow DAG — Dead subprocess.TimeoutExpired Branch and No Hard Subprocess Timeout

## Problem

The .NET process called by an Airflow PythonOperator can run indefinitely. The Airflow task timeout does not kill the subprocess cleanly. A `except subprocess.TimeoutExpired` branch in the code gives false confidence of timeout handling but is dead code — it is never reached.

## Root Cause

`for line in proc.stdout` blocks the main thread until stdout closes (process exits). `proc.wait(timeout=1680)` placed after the loop is unreachable while the process is running. `except subprocess.TimeoutExpired` is dead code — Airflow raises `AirflowTaskTimeout` (a `BaseException`), not `subprocess.TimeoutExpired`. The existing `except Exception` block catches Airflow's timeout and kills the process, but only after Airflow's 30-min `execution_timeout` fires — there is no independent subprocess wall-clock kill before that.

## Constraints

- Live stdout streaming to Airflow task logs is a hard requirement — `proc.communicate(timeout=N)` buffers all output and is unacceptable
- Must kill subprocess before Airflow's `execution_timeout` fires (2-min safety buffer required)
- `proc.wait(timeout=N)` and `for line in proc.stdout` cannot coexist in the same thread

## Affected Components

- Airflow PythonOperator `run_dotnet_exe()` function
- `.NET ETL subprocess` (dotnet binary called from Python)
- `subprocess.Popen` streaming loop
