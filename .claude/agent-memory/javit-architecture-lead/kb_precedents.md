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

---

## D018 — OMS DDD + CQRS Standard (Phase 1 baseline)

**Established:** Greenfield OMS on .NET + PostgreSQL + AKS = DDD bounded context (Order aggregate
root + state machine + Anti-Corruption Layers) + CQRS read/write split (command handlers + projection
tables) + outbox pattern (order_events table for reliable Sprint Connect delivery).

**Trigger:** Any problem designing or implementing an OMS, order lifecycle service, or any system
with multi-step state machines and multi-system integration via a middleware layer.

**Key sub-decisions carried:**
- Order state machine: Pending → BookingConfirmed → PickStarted → PickConfirmed → OutForDelivery → Delivered → Invoiced → Paid / Cancelled
- Phased rollout: RolloutPolicy domain service (not middleware)
- Idempotency: check-before-insert on OrderId (reuses D015 pattern)
- Integration: outbox table polled by SprintConnectAdapter (ACL)

**KB:** P013, D018, S018

**Extended by:** D019 (Phase 2 extensions — Package, OnHold, Returns, multi-channel)

---

## D019 — OMS Phase 2 Extensions Standard

**Established:** OMS aggregate extensions are done in-aggregate (DDD) rather than via external Saga
orchestrators when: (a) the flow involves 2 services only; (b) the outbox+ACL pattern is already in
place; (c) resolution requires human-in-the-loop (OnHold → staff command).

**Key sub-decisions carried:**
- Shipment replaced by Package value object (record) on Order aggregate; TMS reports per TrackingId
- Packed state required between PickConfirmed and OutForDelivery (WMS calls AssignPackages)
- OnHold = non-destructive pause; _preHoldState snapshot field stores prior state for Resume
- Returns sub-machine (ReturnRequested → ReturnPickupScheduled → Returned) as aggregate states; TMS coordination via outbox+ACL
- PackageLost auto-triggers OnHold; staff resolves via Release + re-dispatch or Cancel
- ChannelType extended: Marketplace, POSTerminal, BulkImport; ChannelOrderFactory for per-channel validation
- Saga Pattern evaluated and rejected for 2-service Returns flow; D012 confirms Saga threshold is 3+ services

**Trigger:** Any OMS extension involving Pack step, returns flow, operational hold, or multi-channel
order ingestion. Also: when Saga is proposed for a 2-service outbox-capable flow — reference D019 as
the precedent for outbox+ACL being sufficient.

**KB:** P014, D019, S019

---

## D020 — OMS Modular Monolith System Architecture Standard

**Established:** The Sprint Connect OMS full system architecture = 4-module Modular Monolith
(Order, Payment, Returns, Configuration) with schema-per-module isolation, ID-only cross-module
access, ACL adapter per external system, Kubernetes 2×API-replicas + 1×outbox-worker (single-writer
with FOR UPDATE SKIP LOCKED), JWT per channel, HMAC per integration, Vault for secrets.

**Key sub-decisions carried:**
- FOR UPDATE SKIP LOCKED is the PostgreSQL-native single-writer enforcement for outbox workers
- Graceful shutdown via CancellationToken + terminationGracePeriodSeconds:60 prevents event delivery gaps on rolling updates
- Module boundary erosion is the primary long-term monolith risk — CI boundary assertion tests (Roslyn/ArchUnit) are a required gate
- Dead-letter queue (outbox_events_dlq) + DLQ-depth alerting required for outbox worker operational safety
- Microservices rejection criteria: small team + atomic TX requirement + no-broker constraint + sub-inflection-point volume
- ACL adapter idempotency key (X-Idempotency-Key: {event_id}) required on all outbound adapter HTTP calls

**Trigger:** Any problem asking about monolith vs microservices for small-team OMS; any outbox
worker operational issue; any Kubernetes deployment topology for stateful background workers.

**Extends:** D018 (Phase 1 DDD+CQRS baseline), D019 (Phase 2 aggregate extensions)

**KB:** P015, D020, S020

---

## D017 — TriggerDagRunOperator Chain "No Status" Fix

**Established:** When child tasks show "no status" in a TriggerDagRunOperator chain: (1) check extract_task logs first — failure propagates silently; (2) add assertion task before extract to fail fast on paused/missing child DAGs; (3) add ShortCircuitOperator after extract to skip trigger chain cleanly on empty batch; (4) add `or ''` guard in Jinja XCom expressions under `render_template_as_native_obj=True`; (5) remove `on_failure_callback` from `default_args` — keep only at DAG level.

**Trigger:** Any Airflow DAG using TriggerDagRunOperator where downstream tasks show "no status" or child DAGs fail silently.

**KB:** P012, D017, S017
