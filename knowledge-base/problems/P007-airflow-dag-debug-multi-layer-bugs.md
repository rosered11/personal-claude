---
id: P007
title: "Airflow DAG Local Debug Setup — Multi-Layer Bug Discovery in ds_outbound_order"
date: 2026-04-21
tags: [airflow, python, debugging, sqlalchemy, subprocess, locale, windows, etl]
severity: medium
related_decisions: [D007]
related_snippets: []
---

# Airflow DAG Local Debug Setup — Multi-Layer Bug Discovery in ds_outbound_order

## Problem

Engineer setting up local VS Code debugger for Airflow DAG (`ds_inc_outbound_order`) encountered 6 layered bugs: 3 environment issues blocking debugpy startup, and 3 code bugs in production DAG logic. The debug environment was unstable, and production bugs were discovered only through local debug attempts.

## Root Cause

Six independent bugs: (1) Windows Thai locale (cp874) causes `KeyboardInterrupt` in `platform._syscmd_ver()` blocking debugpy init. (2) `PYTHONUTF8=1` fix breaks pandas import via `_path_join` in SQLAlchemy 1.4 venv. (3) SQLAlchemy 1.x `engine.connect()` has no `conn.commit()` — production code written for 2.x. (4) `str.join()` on `list[int]` in `xcom_push` causes `TypeError`. (5) Guard `if not dih_batch_id: return` placed after `.NET` job ran — `spc_batch_id` already incremented. (6) Subprocess not killed on `AirflowTaskTimeout` — .NET process leaks on server.

## Constraints

- Windows Thai locale (cp874) environment — not reproducible on standard English locale machines
- SQLAlchemy 1.4.x pinned by `flask-appbuilder 4.6.3` (cannot upgrade without breaking venv)
- Airflow DAG code is not independently testable without a stub layer

## Affected Components

- `ds_outbound_order` DAG (`ds_inc_outbound_order`)
- `ds_spc_order_outbound_jda_spc_to_wms.py`
- `debug_runner.py` (new)
- Airflow worker server (.NET subprocess process leak)
