---
when_to_use: >
  Airflow PythonOperator that (1) reads a counter/batch ID from DB, (2) passes it to an external
  subprocess as an env var, and (3) writes DB records only after subprocess success.
  Apply this pattern whenever a DB state mutation must be deferred until after an external
  process confirms success — i.e., the DB write is a commit of the subprocess outcome, not a
  prerequisite for it.
related_problems: [P011]
related_decisions: [D016]
---

# S016 — Deferred Batch ID Commit (Saga-Structured Airflow DAG)

## Pattern

Split the DB interaction around the subprocess into three explicit saga phases:

1. **Pre-subprocess (read-only):** SELECT the counter value. Pass it to the subprocess via env. No UPDATE yet.
2. **Subprocess gate:** Run the external process. Raise on any non-zero exit or timeout.
3. **Post-subprocess (write-only, success path):** UPDATE the counter. INSERT/UPDATE downstream records using parameterized SQL.

This ensures the counter only advances when the job succeeds, and eliminates SQL injection risk on all post-subprocess DB writes.
