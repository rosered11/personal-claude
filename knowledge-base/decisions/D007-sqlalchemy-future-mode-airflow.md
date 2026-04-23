---
id: D007
chosen_option: "SQLAlchemy create_engine(future=True) with explicit conn.commit()"
problem_id: P007
tags: [airflow, python, sqlalchemy, debugging, etl, compatibility]
related_snippets: []
---

# Decision: SQLAlchemy future=True + Explicit commit() for Airflow DAG Connections

## Context

Airflow DAG code used `engine.connect()` with `conn.commit()` — the SQLAlchemy 2.x API. The venv was pinned to SQLAlchemy 1.4.x (required by `flask-appbuilder 4.6.3`). In 1.4.x, `conn.commit()` does not exist; autocommit was the default and `conn.execute()` returned a different cursor type. Production writes silently succeeded only due to autocommit, masking the incompatibility.

## Options Considered

1. **Upgrade SQLAlchemy to 2.x** — correct API; breaks `flask-appbuilder 4.6.3` which pins 1.4.x.
2. **Use 1.4.x autocommit mode explicitly** — works but diverges from 2.x patterns the rest of the team uses.
3. **SQLAlchemy 1.4.x with `future=True` + explicit transaction context** — enables 2.x-style API on 1.4.x engine; `with conn.begin():` provides explicit transaction without `conn.commit()` call.

## Decision

Use `create_engine(conn_str, future=True)` to enable SQLAlchemy 2.x-compatible API on the 1.4.x library. Replace `conn.commit()` with `with conn.begin() as trans:` context manager. This pattern works identically on both 1.4.x (future=True) and 2.x without code changes when the library is eventually upgraded.

## Consequences

- DAG code now works correctly on SQLAlchemy 1.4.x without breaking flask-appbuilder.
- `future=True` is forward-compatible — no code change needed when upgrading to 2.x.
- `with conn.begin():` makes transaction scope explicit and visible.
- Engineers cloning DAG code must remember the `future=True` flag when creating new engines.
