---
name: KB Precedents
description: Key decisions now in the knowledge base that should be referenced when similar problems arise — prevents re-deciding already-validated choices
type: project
---

# KB Precedent Decisions

These decisions are in the knowledge base and represent validated, production-tested choices. When a new problem closely matches one of these, reference the existing decision rather than re-deriving it from scratch.

---

## D001 — EF Core Hot-Path Standard

**Established:** IDbContextFactory + EF.CompileQuery (static field) + Include chains = the standard for EF Core hot-path queries.

**Trigger:** Any problem involving EF Core N+1, concurrency, or DynamicMethod accumulation.

**KB:** P001, D001, S001, S014

---

## D003 — ETL Transaction Scope Law

**Established:** Per-batch transaction is the law. `BeginTransactionAsync` must be inside the `while(true)` loop. Single-job TX is never acceptable for ETL processing > 10K records.

**Trigger:** Any ETL sync service problem involving TX timeout, lock timeout, or long-running transactions.

**KB:** P003, D003, S003

---

## D005 — ETL Batch Size + ChangeTracker Standard

**Established:** BatchSize = 10,000 for EF Core ETL. `ChangeTracker.Clear()` after every commit. Per-batch tracking dictionaries must be flushed after each batch.

**Trigger:** Any ETL OOM, unbounded heap growth, or batch size misconfiguration.

**KB:** P005, D005, S005

---

## D008 — Two-Pass FK-Safe Batch Commit

**Established:** When child entities FK on a DB-generated parent ID, use two-pass commit within the same per-batch TX. 2 SaveChangesAsync per batch replaces N saves.

**Trigger:** Any ETL with parent/child entities and DB-generated PKs (IDENTITY/SEQUENCE).

**KB:** P008, D008, S008

---

## D009 — Subprocess Hard Timeout

**Established:** Daemon thread for stdout streaming + `proc.wait(timeout=N)` for hard kill. TIMEOUT = execution_timeout_seconds − 120.

**Trigger:** Any Airflow PythonOperator calling a long-running subprocess with live log streaming required.

**KB:** P009, D009, S009

---

## D010 — DB Type Selection Matrix

**Established:** Workload-driven selection: PostgreSQL for ACID/relational, MySQL for write-heavy simple, MongoDB for documents, Redis for cache/ephemeral, InfluxDB for time-series.

**Trigger:** Any problem asking "which database should we use?"

**KB:** D010

---

## D012 — Distributed Transaction Tiers

**Established:** Single DB → local TX. 2–3 services → Saga. Payment/financial → TC/C. 2PC explicitly rejected for service-to-service.

**Trigger:** Any multi-service operation needing coordinated state change.

**KB:** D012, S012
