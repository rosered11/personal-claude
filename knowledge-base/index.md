# Knowledge Base Index

Auto-maintained by `kb-writer-agent`. Do not edit manually.

---

## Problems

| ID | Title | Tags | Severity | Decisions | Snippets |
|----|-------|------|----------|-----------|---------|
| [P001](problems/P001-getsuborder-n1-pool-exhaustion.md) | GetSubOrder API — N+1 Query + Pool Exhaustion | ef-core, n+1, connection-pool, performance, dotnet | high | D001 | S001, S014 |
| [P002](problems/P002-postgresql-autovacuum-index-bloat.md) | PostgreSQL Dead Tuple Bloat — stockadjustments | postgresql, autovacuum, index-bloat, maintenance, storage | high | D002 | S002 |
| [P003](problems/P003-mysql-etl-single-tx-timeout.md) | MySQL ETL — Single Transaction Spanning Full Job Causes Lock Timeout | etl, transaction, mysql, batch-processing, timeout, dotnet | high | D003 | S003 |
| [P004](problems/P004-etl-zero-observability-batch-metrics.md) | ETL Sync — Zero Observability During Batch Execution | etl, observability, prometheus, monitoring, metrics, dotnet | medium | D004 | — |
| [P005](problems/P005-etl-oom-changetracker-unbounded.md) | ETL Sync OOM Risk — Oversized Batch and EF ChangeTracker Accumulation | etl, memory, ef-core, batch-processing, oom, changetracker, dotnet | high | D005 | S005 |
| [P006](problems/P006-copy-paste-silent-wrong-table.md) | SyncProductBarcodeJda — Copy-Paste Bug: CheckPendingAsync Queries Wrong Table Silently | etl, copy-paste, correctness, testing, silent-failure, dotnet, debugging | high | D006 | — |
| [P007](problems/P007-airflow-dag-debug-multi-layer-bugs.md) | Airflow DAG Local Debug Setup — Multi-Layer Bug Discovery in ds_outbound_order | airflow, python, debugging, sqlalchemy, subprocess, locale, windows, etl | medium | D007 | — |
| [P008](problems/P008-orderjda-n1-savechanges-in-loop.md) | OrderJda ETL — N+1 SELECT + SaveChanges-in-Loop + Long Transaction on PostgreSQL | ef-core, n+1, etl, transaction, batch-processing, dotnet, postgresql, fk-constraint | high | D008 | S008 |
| [P009](problems/P009-airflow-subprocess-timeout-hang.md) | Airflow DAG — Dead subprocess.TimeoutExpired Branch and No Hard Subprocess Timeout | airflow, python, subprocess, timeout, orchestration, threading, dead-code | high | D009 | S009 |
| [P010](problems/P010-order-concurrent-running-number-idempotency.md) | Order Service — Concurrent Running-Number Race + Missing Idempotency on CreateOrder and ProcessActivity Events | ef-core, concurrency, optimistic-locking, running-number, idempotency, dotnet, mssql, integration-events, duplicate-order, null-safety, microservices | high | D015 | S015 |
| [P011](problems/P011-airflow-dag-pre-subprocess-batch-id-mutation.md) | Airflow DAG — spc_batch_id Incremented Before Subprocess Runs, No SQL Parameterization | airflow, python, etl, mysql, subprocess, correctness, sql-injection, orchestration, batch-processing | high | D016 | S016 |
| [P012](problems/P012-airflow-child-dag-no-status-trigger-chain.md) | Airflow DAG — Child DAGs Show No Status After Main DAG Completes | airflow, python, orchestration, trigger-dagrun, xcom, jinja, child-dag, dag-dependency, etl, debugging | high | D017 | S017 |
| [P013](problems/P013-oms-greenfield-order-lifecycle-integration.md) | OMS Design — Greenfield Order Lifecycle Orchestrator with Multi-System Integration | oms, order-management, microservices, distributed, integration, dotnet, postgresql, aks, event-driven, state-machine, cqrs, domain-driven-design, api, batch-processing, phased-rollout | high | D018 | S018 |
| [P014](problems/P014-oms-extensions-package-returns-hold-multichannel.md) | OMS Architecture Extensions — Package Tracking, Returns, Hold, and Multi-Channel Order Channels | oms, order-lifecycle, ddd, aggregate, state-machine, returns, exception-handling, multi-channel, package-tracking, fulfillment, cqrs, outbox, domain-driven-design, dotnet, postgresql | high | D019 | S019 |
| [P015](problems/P015-oms-modular-monolith-module-boundary-deployment.md) | OMS System Architecture — Confirmed Modular Monolith with DDD + CQRS + Outbox + ACL for 70K Order Lines/Day | oms, modular-monolith, order-management, domain-driven-design, cqrs, outbox, anti-corruption-layer, state-machine, dotnet, postgresql, redis, kubernetes, integration, webhook, multi-channel, security, jwt, hmac | high | D020 | S020 |
| [P016](problems/P016-datamart-rabbitmq-activity-log-ingestion.md) | DataMart Dashboard — RabbitMQ Activity Log Ingestion Channel | dotnet, rabbitmq, ef-core, postgresql, background-service, hosted-service, message-consumer, write-path, integration-events, logging, activity-log | medium | D021 | S021 |
| [P017](problems/P017-fms-adapter-shipment-provider-priority-inversion.md) | FMSUpdateAdapter — ShipmentProvider Priority Inversion in CreateUpdateStatusRequest | dotnet, correctness, priority-logic, adapter, shipment, fulfillment, null-safety, unit-testing, fms-adapter, activity-process | high | D022 | S022 |
| [P018](problems/P018-oms-service-boundary-coupling-bff-gateway-observability.md) | OMS Service-Boundary Coupling Undermines Planned BFF/Gateway/Observability Layers | oms, microservices, service-boundary-violation, api-gateway, bff, observability, read-model, dotnet | high | D023 | S023 |
| [P019](problems/P019-validate-service-order-save-timeout-duplicate-key.md) | Validate-Service Order Save Failures -- SQL Timeout + Duplicate-Key on Retry | ef-core, mssql, kafka, command-timeout, idempotency, duplicate-key, integration-events, dotnet | high | D024 | S024 |
| [P020](problems/P020-oms-bu-growth-database-load-scaling.md) | OMS Shared-Schema Database Load Risk From BU Growth | oms, database, postgresql, multi-tenancy, scalability, caching, read-replica, dotnet | high | D025 | S025 |
| [P021](problems/P021-activity-service-batched-merge-timeout-stacked-retry.md) | Activity Service -- Batched EF Core MERGE Timeout Under Stacked Retry Policies | ef-core, mssql, command-timeout, batch-processing, retry-policy, integration-events, dotnet, rabbitmq | high | D026 | S026 |
| [P022](problems/P022-mssql-online-index-rebuild-audit-false-positive.md) | Suspicious Audit-Logged Self-Insert Traced to Online Index Rebuild Job | mssql, sql-server-audit, index-rebuild, online-index-operation, observability, security-false-positive, database-maintenance, alert-fatigue | medium | D027 | S027 |
| [P023](problems/P023-mssql-index-rebuild-database-load-timeout.md) | TaskIndexRebuild Execution Causes Production SQL Timeout Spikes | mssql, index-rebuild, database-maintenance, sql-timeout, database-load-spike, throttling, resource-governance, online-index-operation, fragmentation-gating | high | D028 | S028 |
| [P024](problems/P024-oms-distributed-monolith-coupling-audit.md) | OMS Codebase Audit -- Distributed Monolith Coupling Behind a Microservices Deploy Topology | oms, architecture-audit, vibe-coding, service-boundary-violation, modular-monolith, testability, dotnet, grpc | high | D029 | S029 |
| [P025](problems/P025-oms-architecture-audit-ai-vibe-coding.md) | OMS Architecture Audit After AI Vibe-Coding | architecture-audit, distributed-monolith, microservices, grpc, secrets-management, dotnet, vibe-coding, technical-debt | high | D030 | S030 |
| [P026](problems/P026-ptl-manual-file-integration-to-api-driven-orchestration.md) | PTL Warehouse Integration -- Replace Manual Excel/File Exchange with API-Driven Task Orchestration | warehouse-management, put-to-light, wms-sap-integration, mhe-plc-integration, api-integration, partial-fulfillment, task-orchestration, exception-handling | high | D031 | S031 |
| [P027](problems/P027-rfid-gate-transfer-manifest-verification.md) | RFID Gate Transfer Verification -- Manifest-Based Unregistered Tag Detection for Intra-Site and Inter-Site Movement | rfid, edge-computing, gate-verification, manifest-sync, offline-first, warehouse-management, inter-site-transfer, event-driven-architecture, fail-safe, real-time | high | D032 | S032 |
| [P028](problems/P028-rfid-ingestion-wan-transport-protocol-selection.md) | RFID Ingestion Service -- Edge-to-Platform WAN Transport Protocol Selection | rfid, edge-computing, offline-first, transport-protocol, batch-processing, idempotency, horizontal-scaling, wan-integration | high | D033 | S033 |
| [P029](problems/P029-rfid-store-returns-reverse-flow-paid-epc-cache.md) | RFID Store Returns -- Reverse Flow for `returned` State, Paid-EPC Cache Removal, and Cross-Store Return Validation | rfid, returns, fraud-prevention, offline-first, edge-computing, cache-invalidation, event-driven-architecture, saga-pattern, loss-prevention, state-machine, retail, eas | high | D034 | S034 |

---

## Decisions

| ID | Title | Chosen Option | Problem | Tags | Snippets |
|----|-------|---------------|---------|------|---------|
| [D001](decisions/D001-ef-core-hotpath-factory-compiled-eager.md) | EF Core Hot-Path — IDbContextFactory + Compiled Queries + Eager Loading | IDbContextFactory + EF.CompileQuery + eager loading | P001 | ef-core, dotnet, n+1, performance, connection-pool | S001, S014 |
| [D002](decisions/D002-postgresql-autovacuum-per-table-tuning.md) | PostgreSQL Per-Table Autovacuum Tuning | Per-table autovacuum scale_factor tuning | P002 | postgresql, autovacuum, index-bloat, maintenance, storage | S002 |
| [D003](decisions/D003-etl-per-batch-transaction-scope.md) | ETL Per-Batch Transaction Scope | Per-batch transaction inside the processing loop | P003 | etl, transaction, mysql, batch-processing, timeout | S003 |
| [D004](decisions/D004-etl-prometheus-batch-observability.md) | ETL Batch Observability — Prometheus Histogram + Structured Logging | Prometheus Histogram per batch + structured log per record | P004 | etl, observability, prometheus, monitoring, metrics | — |
| [D005](decisions/D005-etl-batch-size-changetracker-clear.md) | ETL Batch Size 10K + ChangeTracker.Clear() After Each Commit | BatchSize 10K + ChangeTracker.Clear() after every commit | P005 | etl, memory, ef-core, batch-processing, oom, changetracker | S005 |
| [D006](decisions/D006-etl-clone-verification-checklist.md) | ETL Clone Verification Checklist Mandatory Before Deploy | Mandatory 6-point clone verification checklist before deploy | P006 | etl, copy-paste, correctness, testing, silent-failure, process | — |
| [D007](decisions/D007-sqlalchemy-future-mode-airflow.md) | SQLAlchemy future=True + Explicit commit() for Airflow DAG Connections | SQLAlchemy create_engine(future=True) with explicit conn.commit() | P007 | airflow, python, sqlalchemy, debugging, etl, compatibility | — |
| [D008](decisions/D008-two-pass-fk-safe-batch-commit.md) | Two-Pass FK-Safe Batch Commit | Two-pass commit: parents (Pass 1) → children (Pass 2) in same per-batch TX | P008 | ef-core, etl, transaction, batch-processing, fk-constraint, dotnet | S008 |
| [D009](decisions/D009-subprocess-hard-timeout-daemon-thread.md) | Subprocess Hard Timeout via Daemon Thread + proc.wait(timeout) | Daemon thread for stdout streaming + proc.wait(timeout) for hard kill | P009 | airflow, python, subprocess, timeout, orchestration, threading | S009 |
| [D010](decisions/D010-database-type-selection-by-workload.md) | Database Type Selection by Workload | Workload-driven database type selection matrix | — | database, postgresql, mysql, mongodb, redis, architecture | — |
| [D011](decisions/D011-realtime-connection-strategy.md) | Real-Time Connection Strategy | Connection strategy selected by update interval and directionality | — | websocket, sse, polling, real-time, architecture, api | — |
| [D012](decisions/D012-distributed-transaction-strategy.md) | Distributed Transaction Strategy | Tiered strategy: local TX → Saga → TC/C | — | distributed, transaction, saga, tcc, consistency, microservices | S012 |
| [D013](decisions/D013-rate-limiter-algorithm-selection.md) | Rate Limiter Algorithm Selection | Token Bucket for burst-friendly; Sliding Window Counter for high-accuracy | — | rate-limiting, token-bucket, sliding-window, redis, api | S010 |
| [D014](decisions/D014-distributed-id-generation-strategy.md) | Distributed ID Generation — Snowflake vs UUID v4 | Snowflake ID for distributed time-sortable; UUID v4 for fully random | — | id-generation, snowflake, uuid, distributed, scalability | S011 |
| [D015](decisions/D015-mssql-sequence-idempotency-order-service.md) | MSSQL SEQUENCE + Idempotency Key for Order Service Running-Number Race | MSSQL SEQUENCE for running-number + D012 idempotency key for event consumers + API idempotency header | P010 | ef-core, concurrency, optimistic-locking, running-number, idempotency, dotnet, mssql, integration-events | S015 |
| [D016](decisions/D016-deferred-batch-id-commit-parameterized-sql.md) | Deferred Batch ID Commit + Parameterized SQL for Airflow DAG | Defer spc_batch_id UPDATE to post-subprocess success + parameterize all SQL | P011 | airflow, python, etl, mysql, subprocess, correctness, sql-injection, orchestration, batch-processing, saga | S016 |
| [D017](decisions/D017-child-dag-assertion-shortcircuit-callback-dedup.md) | Child DAG Assertion Task + ShortCircuitOperator + Callback Dedup | Hexagonal adapter hardening + Saga short-circuit | P012 | airflow, python, orchestration, trigger-dagrun, xcom, jinja, child-dag, dag-dependency, etl, debugging | S017 |
| [D018](decisions/D018-oms-ddd-cqrs-aggregate-outbox.md) | OMS Architecture — DDD Bounded Context + CQRS Read/Write Split + Outbox Integration | OMS as DDD Bounded Context with CQRS Read/Write Split + Outbox Integration | P013 | oms, order-management, domain-driven-design, cqrs, state-machine, outbox, integration, dotnet, postgresql, aks | S018 |
| [D019](decisions/D019-oms-extended-aggregate-package-hold-returns.md) | OMS Extended Aggregate — Package Value Object, OnHold Snapshot, Returns Sub-Machine, Multi-Channel | Extended Order Aggregate with Package Value Object, OnHold State Snapshot, and In-Aggregate Returns Sub-Machine | P014 | oms, order-lifecycle, ddd, aggregate, state-machine, returns, exception-handling, multi-channel, package-tracking, fulfillment, cqrs, outbox, domain-driven-design, dotnet, postgresql | S019 |
| [D020](decisions/D020-oms-modular-monolith-boundary-enforcement-outbox-worker.md) | OMS Modular Monolith — Module Boundary Enforcement + Outbox Worker Graceful Shutdown | Disciplined Modular Monolith with Enforced Module Boundaries, Schema Isolation, and Graceful-Shutdown Outbox Worker | P015 | oms, modular-monolith, order-management, domain-driven-design, cqrs, outbox, anti-corruption-layer, state-machine, dotnet, postgresql, redis, kubernetes, integration, webhook, multi-channel, security, jwt, hmac | S020 |
| [D021](decisions/D021-rabbitmq-hostedservice-hexagonal-activity-log.md) | RabbitMQ Consumer — IHostedService Hexagonal Adapter + RabbitMQ.Client + Service/Repository Ports | IHostedService as inbound Hexagonal adapter using RabbitMQ.Client with IActivityLogService port and IActivityLogRepository driven adapter | P016 | dotnet, rabbitmq, ef-core, postgresql, background-service, hosted-service, hexagonal-architecture, message-consumer, write-path | S021 |
| [D022](decisions/D022-fms-adapter-shipment-provider-resolver-private-helper.md) | FMSUpdateAdapter — Private ResolveShipmentProvider Helper with Explicit 3-Tier Priority | In-Place Private Helper ResolveShipmentProvider (Layered Architecture) | P017 | dotnet, correctness, priority-logic, adapter, shipment, fulfillment, null-safety, unit-testing, fms-adapter, activity-process | S022 |
| [D023](decisions/D023-oms-strangler-fig-facade-first-microservices-migration.md) | OMS Strangler Fig Facade-First Migration Toward Network-Isolated Microservices | Strangler Fig facade-first sequencing (Gateway/BFF/OTel first, incremental per-seam boundary strangling second, full-microservices-vs-monolith end-state deferred) | P018 | oms, microservices, service-boundary-violation, api-gateway, bff, observability, read-model, dotnet, strangler-fig | S023 |
| [D024](decisions/D024-resilient-upsert-adapter-idempotent-dlq-order-ingestion.md) | Resilient Upsert Persistence Adapter (Hexagonal) + Idempotency Dedup Table & DLQ (Event-Driven) for validate-service MAO Ingestion | Hexagonal adapter hardening (Polly retry + MERGE upsert) as primary, Event-Driven idempotency/DLQ as required companion | P019 | ef-core, mssql, kafka, command-timeout, idempotency, duplicate-key, integration-events, dotnet | S024 |
| [D025](decisions/D025-oms-cqrs-cache-replica-phased-tenant-partitioning.md) | OMS Database Scaling Strategy for Growing BU Count | CQRS read-scaling (cache-aside + read replica + Outbox read model) now, with per-BU write-volume instrumentation to trigger selective schema-per-BU-tier partitioning (DDD bounded-context) later | P020 | oms, database, postgresql, multi-tenancy, scalability, caching, read-replica, cqrs, domain-driven-design, dotnet | S025 |
| [D026](decisions/D026-activity-service-chunked-batch-persistence-single-retry-strategy.md) | Activity Service -- Chunked Batch Persistence + Single Retry Strategy | Bounded two-pass chunked persistence adapter (D008/S008 pattern) + execution-strategy-only retry, RabbitMQ redelivery as backstop | P021 | ef-core, mssql, command-timeout, batch-processing, retry-policy, integration-events, dotnet, rabbitmq | S026 |
| [D027](decisions/D027-mssql-index-maintenance-runid-logging-fragmentation-gate.md) | Instrumented, Fragmentation-Gated Index Maintenance Over External Event Publication | Structured maintenance-run logging (RunId + session correlation) + fragmentation-gated, config-driven rebuild scope | P022 | mssql, sql-server-audit, index-rebuild, observability, security-false-positive, database-maintenance, layered-architecture, event-driven-architecture | S027 |
| [D028](decisions/D028-mssql-throttled-low-priority-resumable-index-rebuild.md) | Consolidated Fragmentation-Gated + Logged + Throttled/Resumable Index Rebuild -- over Queue-Dispatched, Backpressure-Throttled Rebuild Workers | Fragmentation gate + RunId logging + WAIT_AT_LOW_PRIORITY + inter-rebuild pacing delay + off-peak window guard + RESUMABLE = ON, populated from the verified 194-entry index inventory | P023 | mssql, index-rebuild, database-maintenance, sql-timeout, database-load-spike, throttling, resource-governance, online-index-operation, fragmentation-gating, layered-architecture, event-driven-architecture | S028 |
| [D029](decisions/D029-oms-hexagonal-ports-fitness-function-boundary-enforcement.md) | Hexagonal Ports at Existing Cross-Service Seams + CI-Enforced Boundary Fitness Function | Hexagonal ports at confirmed crossing points, strangled seam-by-seam (D023 lineage), enforced via NetArchTest CI fitness function | P024 | oms, architecture-audit, hexagonal-architecture, strangler-fig, service-boundary-violation, modular-monolith, testability, dotnet, grpc | S029 |
| [D030](decisions/D030-oms-hexagonal-contracts-secrets-port-grpc-tls-hardening.md) | Hexagonal Contracts Extraction + ISecretProvider Port, with Interim gRPC TLS Hardening and Service Mesh as Phase 2 | Extract per-service Contracts/adapter assemblies with an ISecretProvider port (Hexagonal), interim gRPC cert-validation hardening folded in, Service Mesh mTLS/resilience routed as a parallel Phase-2 track | P025 | architecture-audit, distributed-monolith, microservices, grpc, secrets-management, dotnet, vibe-coding, technical-debt, hexagonal-architecture, service-mesh | S030 |
| [D031](decisions/D031-ptl-saga-orchestrated-task-lifecycle-event-driven-backbone.md) | PTL Task Saga -- Orchestrated Process Manager over an Event-Carried State Backbone | Orchestrated Saga (PTL Task Orchestrator) with event-bus transport for WMS/SAP/PTL/Marketplace notifications | P026 | warehouse-management, put-to-light, saga-pattern, event-driven-architecture, wms-sap-integration, mhe-plc-integration, partial-fulfillment, exception-handling | S031 |
| [D032](decisions/D032-rfid-gatesession-ddd-manifest-eda-prepositioning.md) | GateSession Domain Aggregate Enforcing Zero-Loss/Fail-Safe Manifest Evaluation, Fed by Event-Pre-Positioned Manifest Cache | GateSession Domain Aggregate (DDD) enforcing zero-loss/fail-safe manifest evaluation, fed by event-pre-positioned manifest cache (EDA transport) | P027 | rfid, edge-computing, gate-verification, manifest-sync, domain-driven-design, event-driven-architecture, offline-first, fail-safe, warehouse-management | S032 |
| [D033](decisions/D033-rfid-hexagonal-https-batch-ingestion-port.md) | Stateless HTTPS/mTLS Batch Ingestion API (Hexagonal Port) as the Edge-to-Central WAN Transport, with At-Least-Once EDA Publish Folded In | Stateless HTTPS/mTLS batch ingestion API (Hexagonal port) as WAN transport; internal EDA publish pipeline unchanged | P028 | rfid, edge-computing, offline-first, transport-protocol, hexagonal-architecture, event-driven-architecture, batch-processing, idempotency, horizontal-scaling | S033 |
| [D034](decisions/D034-rfid-return-saga-locality-scoped-verification.md) | ReturnSaga -- Locality-Scoped Verification with Event-Driven Paid-EPC Cache Invalidation | ReturnSaga (Saga Pattern), local-only verdict for same-store returns, bounded synchronous checkpoint for cross-store returns, event-driven cache invalidation routed to the originating store | P029 | rfid, returns, fraud-prevention, saga-pattern, event-driven-architecture, offline-first, cache-invalidation, retail, loss-prevention, state-machine | S034 |

---

## Snippets

| ID | Title | Language | Related Problems | Related Decisions |
|----|-------|----------|-----------------|------------------|
| [S001](snippets/S001-async-parallel-db-coordinator/) | Async Parallel DB Coordinator | C# | P001 | D001 |
| [S002](snippets/S002-postgresql-autovacuum-maintenance/) | PostgreSQL Autovacuum Maintenance | SQL | P002 | D002 |
| [S003](snippets/S003-etl-per-batch-commit-loop/) | EF Core Per-Batch Commit Loop | C# | P003 | D003 |
| [S005](snippets/S005-etl-batch-resource-tracking/) | ETL Batch Resource Tracking | C# | P005 | D005 |
| [S008](snippets/S008-two-pass-fk-safe-batch-commit/) | Two-Pass FK-Safe Batch Commit | C# | P008 | D008 |
| [S009](snippets/S009-subprocess-hard-timeout-daemon/) | Subprocess Hard Timeout via Daemon Thread | Python | P009 | D009 |
| [S010](snippets/S010-redis-token-bucket-rate-limiter/) | Redis Token Bucket Rate Limiter | Lua | — | D013 |
| [S011](snippets/S011-snowflake-id-generator/) | Snowflake ID Generator | Go | — | D014 |
| [S012](snippets/S012-idempotency-key-table/) | Idempotency Key Table | SQL | — | D012 |
| [S014](snippets/S014-ef-core-compile-query-static/) | EF.CompileQuery Static Field | C# | P001 | D001 |
| [S015](snippets/S015-mssql-sequence-idempotency-order/) | MSSQL SEQUENCE + Idempotency Guard + Null Safety for Order Service | C# | P010 | D015 |
| [S016](snippets/S016-deferred-batch-id-saga-dag/) | Deferred Batch ID Commit (Saga-Structured Airflow DAG) | Python | P011 | D016 |
| [S017](snippets/S017-airflow-child-dag-assertion-shortcircuit/) | Child DAG Assertion + ShortCircuitOperator + Jinja or-guard | Python | P012 | D017 |
| [S018](snippets/S018-oms-ddd-aggregate-cqrs-handler/) | OMS Order Aggregate (DDD State Machine) + CQRS CreateOrderHandler | C# | P013 | D018 |
| [S019](snippets/S019-oms-extended-aggregate-package-hold-returns/) | OMS Extended Order Aggregate — Package, OnHold, Returns, PackageLost, Multi-Channel | C# | P014, P013 | D019, D018 |
| [S020](snippets/S020-oms-outbox-worker-graceful-shutdown-skip-locked/) | OMS Outbox Worker — FOR UPDATE SKIP LOCKED + Graceful Shutdown + ACL Dispatcher | C# | P015 | D020, D018 |
| [S021](snippets/S021-rabbitmq-hostedservice-hexagonal-log-consumer/) | RabbitMQ IHostedService Hexagonal Log Consumer — Entity + Service Port + Repository + BackgroundService | C# | P016 | D021 |
| [S022](snippets/S022-fms-adapter-shipment-provider-resolver/) | FMSUpdateAdapter ResolveShipmentProvider Private Helper + Unit Tests | C# | P017 | D022 |
| [S023](snippets/S023-oms-strangler-fig-master-service-client/) | OMS Strangler Fig Seam -- IMasterServiceClient (Legacy In-Process vs Target HTTP) | C# | P018 | D023 |
| [S024](snippets/S024-order-persistence-gateway-polly-merge-dlq/) | Resilient Upsert Persistence Adapter -- Polly Retry + MERGE Upsert + Idempotency Guard + DLQ | C# | P019 | D024 |
| [S025](snippets/S025-oms-cache-aside-read-replica-tenant-tiering/) | OMS Cache-Aside Reads + Read-Replica Routing + Per-BU Write-Volume Metric | C# | P020 | D025 |
| [S026](snippets/S026-activity-generator-chunked-batch-execution-strategy/) | Activity Generator -- Chunked Two-Pass Persistence + Single Execution-Strategy Retry | C# | P021 | D026 |
| [S027](snippets/S027-mssql-fragmentation-gated-index-maintenance-runner/) | MSSQL Fragmentation-Gated, Logged Index Maintenance Runner | SQL | P022 | D027 |
| [S028](snippets/S028-mssql-throttled-resumable-index-rebuild-runner/) | MSSQL Consolidated Fragmentation-Gated, Logged, Throttled, Resumable Index Rebuild Runner | SQL | P023 | D028 |
| [S029](snippets/S029-netarchtest-service-boundary-fitness-function/) | NetArchTest Service-Boundary Fitness Function | C# | P024 | D029 |
| [S030](snippets/S030-secretprovider-port-grpc-cert-validation-hardening/) | ISecretProvider Port + Interim gRPC Certificate-Validation Hardening | C# | P025 | D030 |
| [S031](snippets/S031-ptl-task-saga-orchestrator/) | PTL Task Saga Orchestrator -- State Machine + Mixed-Carton Rejection + Allocation-vs-Stock Hold + Partial SO/STO | C# | P026 | D031 |
| [S032](snippets/S032-rfid-gatesession-manifest-cache/) | GateSession Domain Aggregate + Event-Pre-Positioned Manifest Cache | C# | P027 | D032 |
| [S033](snippets/S033-rfid-ingestion-http-batch-port/) | Stateless HTTPS Batch Ingestion Port + Edge Offline-Buffer Client | C# | P028 | D033 |
| [S034](snippets/S034-rfid-return-saga-cache-invalidation/) | ReturnSaga -- Locality-Scoped Verification + Event-Driven Paid-EPC Cache Invalidation | C# | P029 | D034 |

---

_Last updated: 2026-07-22 -- added P020/D025/S025 from inbox/oms/req.md (real Sprint-OMS source-tree audit: OMS uses shared-schema BuCode-discriminated multi-tenancy on a single Postgres instance also hosting Master/log DBs and Hangfire, with Redis wired but disabled and no read replica or CQRS read-model, so BU growth compounds write+read load on one DB; CQRS lens (cache-aside + read replica + Outbox read model) chosen as the immediate move over DDD-lens schema-per-BU-tier partitioning, deferred and triggered later via new per-BU write-volume instrumentation -- extends the P018/D023 repo-audit lineage and does not contradict the D020 modular-monolith precedent)_

_Also 2026-07-22 -- added P021/D026/S026 from inbox/issue.md (real activity-service production log, grounded against the actual Sprint-FM-V0 source at D:\workspace\sprint-fm-v0\src\Services\Activity: ActivityGenerator.SaveProcessActivityV2Async issues one unbounded SaveChangesAsync batched by EF Core into two oversized MERGE statements (25 rows / 331 params) that exceeded CommandTimeout=30, compounded by a previously-unnoticed double-stacked retry policy -- EF Core's own EnableRetryOnFailure(3, 5s) plus an outer ad hoc DoTransactionAsyncWithRetryPolicy catch(DbException) loop (also 3 retries, no backoff) -- capable of resubmitting the identical oversized command up to 9 times. Layered/Hexagonal lens (bounded two-pass chunked persistence reusing D008/S008 + single execution-strategy retry) chosen over Event-Driven fan-out (deferred: higher rollout risk, new partial-completion consistency gap on a live order-processing path); related to but distinct from P019/D024 -- this service already had idempotent MERGE upserts, so the failure mode here is batch-sizing/retry-amplification, not missing idempotency)_


_Also 2026-07-22 -- added P022/D027/S027 from inbox/database-index.md (SQL Server audit log flagged `insert [dbo].[SubOrderItem] select * from [dbo].[SubOrderItem] with (index = 1)` co-occurring with the scheduled TaskIndexRebuild job; traced with high confidence to SQL Server's own internal engine-generated DML during an ONLINE clustered-index rebuild -- WITH (INDEX = 1) is the legacy numeric hint that always resolves to the clustered index, and SubOrderItem's clustered index PK_SubOrderItem is rebuilt on the @day=3 branch. Also found, while reading the script directly: PK_StoreLocation is redundantly rebuilt on both @day=4 and @day=6. Layered Architecture lens (RunId-correlated maintenance logging + fragmentation-gated, config-driven rebuild scope) chosen over Event-Driven Architecture (maintenance-window event publication to a SIEM, deferred: no named event/SIEM consumer exists yet for this problem) -- new database-observability/maintenance-hygiene precedent, distinct from but adjacent to the EF Core write-path family (P010/P019/P021) since it concerns the DB-engine-internal side of the same OrderDb, not the application write path)_

_Also 2026-07-29 -- added P023/D028/S028 from inbox/rebuild-index-db/req.md (user reported that running the index-rebuild job causes an OrderDb load spike and concurrent SQL command timeouts; the two supplied reference files, script-rebuild.sql and schema-database.sql, were found byte-for-byte identical -- both plain 98-table schema exports with no rebuild logic, so script-rebuild.sql was not usable as the claimed 'old script'. Per explicit user direction, this consultation instead treated the KB-documented TaskIndexRebuild procedure at its S027 baseline (fragmentation-gated, RunId-logged) as the real prior state, using schema-database.sql only as table/index inventory. New root cause identified: D027/S027 gated *which* indexes rebuild but never paced *how* the rebuild executes against live traffic -- no WAIT_AT_LOW_PRIORITY, no inter-rebuild delay, no off-peak window guard, no resumable rebuild for large indexes. Top KB match was P022 at overlap_score=0.5, below the 0.8 UPDATE threshold, so this correctly became a new CREATE-mode record rather than overwriting P022/D027/S027 -- a distinct problem angle (load/timeout impact) on the same stored procedure, not a repeat of the audit-correlation problem. Layered Architecture lens (in-procedure WAIT_AT_LOW_PRIORITY + pacing delay + off-peak window guard + RESUMABLE = ON) chosen over Event-Driven Architecture (queue-dispatched, backpressure-throttled rebuild workers via Service Broker), extending D027 rather than contradicting it.)_

_Also 2026-07-29 -- UPDATED P023/D028/S028 (in place, not a new record) from a second submission at inbox/rebuild-index-db/req.md. kb-search overlap of the new problem against P023 scored ~0.89 (8/9 tags shared, above the 0.8 UPDATE threshold), so this correctly revised the existing record rather than creating P024. This submission finally supplied the real script-rebuild.sql (previously only a mislabeled schema-export duplicate) -- byte-for-byte identical to the TaskIndexRebuild body documented in P022, confirming both the exact 195-statement/194-unique-candidate index inventory and the PK_StoreLocation double-rebuild defect programmatically (extracted via regex, not just visual inspection). Critically, this also revealed that neither the D027 (fragmentation gate + RunId logging) nor D028 (throttled pacing) design had ever actually been deployed to production -- the prior D028 consultation had assumed the S027 baseline was already live. The revised decision merges both designs into one consolidated, deployment-ready script (S028), populated with the real 194-entry candidate list, still choosing Layered Architecture (single in-procedure fix) over Event-Driven Architecture (Service Broker queue/worker pool) -- reinforced rather than weakened by the new evidence, since committing to more infrastructure before the simplest fix is even proven in production would compound delivery risk._

_Also 2026-07-31 -- added P024/D029/S029 from inbox/oms/req.md (second, differently-scoped submission at this same inbox path -- the first, on 2026-07-22, produced P020/D025/S025 on BU-growth database load; this one asked a broader source-code-only audit question across all six areas: Order, Portal, Master, Shared, Front, Report, explicitly excluding all in-repo docs/README as evidence). kb-search top match was P018 at overlap_score=0.375 (oms, service-boundary-violation, dotnet shared; below the 0.8 UPDATE threshold), so this correctly became a new CREATE-mode record. Confirmed and sharpened the P018 finding with file-level using-statement evidence (not just unused ProjectReference entries): Order.Core imports Portal.Infrastructure in 10 files and Master.Infrastructure in 18; Front.Core -- newly audited, did not exist in P018's scope -- imports Order.Infrastructure in 26 files, Portal.Infrastructure in 10, Master.Infrastructure in 2, despite a real gRPC contract seam already existing for the same domains (28 gRPC pairs under Order, 6 under Master, 4 under Portal). Also found: AddHealthChecks() now present on all 5 API projects (P018 gap closed); early CQRS read-model work already begun (LookerProjectionRefreshService, BuWriteVolumeFlushService, SyncWatermarkState, dated the same day as P020/D025 -- partial adoption of that decision); Master/Front/Report have zero test projects; TraceIdMiddleware still Portal.API-only; plaintext secrets committed across all appsettings.*.json; no architecture-fitness-function tooling anywhere. Hexagonal Architecture (ports at each confirmed crossing point, adapters calling the existing gRPC seam) chosen over Strangler Fig as the primary lens -- not because Strangler Fig was wrong, but because its seam-by-seam sequencing insight was folded into the Hexagonal rollout plan as the required rollout discipline, directly extending D023's already-adopted facade-first Strangler Fig sequencing into the "per-seam boundary strangling" phase D023 had explicitly deferred. Does not contradict D020 (Modular Monolith) or D023 (Strangler Fig facade-first); operationalizes both with file-level specificity. S029 ships a NetArchTest CI fitness function seeded with a shrink-only allow-list of the exact violations found, directly answering the requesting engineer's stated goal of preventing future AI-vibe-coded boundary regressions._

_Also 2026-07-31 -- added P025/D030/S030, a third same-day submission on this repo (after P020/D025/S025 and P024/D029/S029). kb-search top match was P024 at overlap_score=0.5 (shared tags: architecture-audit, vibe-coding, dotnet, grpc; below the 0.8 UPDATE threshold), so this correctly became a new CREATE-mode record rather than overwriting P024/D029/S029. This submission reconfirms the same Order/Portal/Master/Front Infrastructure-assembly coupling a third time, but its distinguishing evidence is two dimensions P024 did not evaluate in depth: plaintext secrets committed across appsettings*.json (MySQL/Redis in Portal.API, PostgreSQL in Order.API/appsettings.AzureDevelop.json) and ~30 gRPC clients disabling TLS certificate validation via DangerousAcceptAnyServerCertificateValidator. lens-determiner deliberately paired Hexagonal Architecture with Service Mesh (rather than reusing D029's Hexagonal+Strangler-Fig pairing) because secrets management was a previously-unaddressed dimension. Hexagonal won as primary (only option satisfying the hard constraint that secrets must be ROTATED via an abstraction seam, not just deleted); Service Mesh's most urgent code-fixable finding (disabled cert validation) was folded into the chosen S030 snippet as an interim fix routed through the same ISecretProvider port, with full mesh adoption retained as an explicit Phase-2 track rather than rejected. Does not contradict D020 (Modular Monolith), D023 (Strangler Fig facade-first), or D029 (Hexagonal ports + NetArchTest fitness function) -- extends D029's guardrail pattern to the secrets seam and folds in the one concrete Service-Mesh-lens finding (gRPC cert validation) that was immediately fixable in code without waiting on external TKE mesh infrastructure._

_Also 2026-08-10 -- added P026/D031/S031 from inbox/push-to-light/req.md +
spec-extracted.md (a text extraction of CMG Put to Light - SPC.pptx, an 8-slide deck
where slides 7-8 are diagram/image-only and could not be extracted as text). This is
the first warehouse/WMS-SAP-PTL integration problem in the KB -- kb-search found no
meaningful tag overlap against the existing 25 entries (all OMS-microservices or
ETL-pipeline domain), so this correctly became a new CREATE-mode record establishing a
new domain precedent rather than extending an OMS/ETL lineage. Problem: CMG's
Put-to-Light process coordinates WMS, SAP, the PTL/MHE hardware controller, and
Marketplace almost entirely via manual Excel file export/import and manual SO/STO
creation (7 documented manual/❌ touchpoints); the TO-BE direction calls for API-driven
task generation, task confirmation, and SO/STO creation, while preserving partial
SO/STO creation and adding allocation-vs-stock mismatch and mixed-store-carton
validation. lens-determiner paired Saga Pattern against Event-Driven Architecture
(orchestration vs. choreography) -- a pairing not previously used in this KB. Saga
Pattern won as the primary lens because every hard invariant in the problem (1
order=1box=1invoice, 1 active box per PLT slot, mixed-carton rejection, partial
SO/STO, allocation-vs-stock mismatch handling) requires a component that can see and
gate the whole multi-system sequence, which pure choreography cannot provide without
re-introducing an orchestrator by another name; Event-Driven Architecture's strongest
insight (replace file exchange with an async event bus) was folded in as the saga's
transport/notification layer rather than rejected, and the already-working Marketplace
auto-sync automation was left as pure choreography since it needs no cross-system
invariant enforcement. S031 ships a PtlTaskSaga state machine (C#, no MediatR/
AutoMapper per repo standard) demonstrating synchronous mixed-carton rejection,
two-directional allocation-vs-stock hold, and idempotency-keyed partial SO/STO
creation.

_Also 2026-08-10 -- added P027/D032/S032 from inbox/RFID/gate-transfer-verification-req.md
(a sub-problem of the RFID Event Platform, whose base architecture -- documented in
inbox/RFID/docs/* and summarized in manual/rfid-architecture-summary.md -- has never had
a formal KB consultation of its own). Problem: business needs gate-level detection of
unregistered/unexpected RFID tags during internal warehouse movement (intra-DC
zone-to-zone) and inter-site transfer (DC->DC, DC->Store), matching each gate pass
against a manifest scoped to that specific movement round (explicitly NOT a global
registry check, per the Clarified Scope section, which was treated as already-decided
and not re-litigated), with zero-delay (edge-local decision only) and zero-loss (every
scanned EPC must be evaluated) requirements. kb-search against the existing 26 entries
found only ~0.06 overlap (a single shared generic tag, warehouse-management, against
P026) -- not a meaningful precedent -- so this correctly became a new CREATE-mode record
establishing the RFID Event Platform's first formal KB anchor, distinct from the PTL
warehouse lineage (P026/D031/S031) despite both being warehouse-domain. lens-determiner
paired Domain-Driven Design against Event-Driven Architecture: DDD won as the primary
lens because the zero-loss and fail-safe-policy constraints require a single, testable
place that owns the invariant ("every EPC read must get a verdict before a gate session
can close"), which no event consumer alone can guarantee; Event-Driven Architecture's
manifest pre-positioning insight (publish manifest.created partitioned by destination
site_id, consumed ahead of physical arrival by the destination edge -- the same pattern
already used for serial-range pre-allocation and Site & Config's heartbeat-pushed config)
was folded in as the transport layer that feeds the DDD aggregate's local cache, rather
than rejected -- the same "who decides vs how data arrives" blend already demonstrated in
D031 (Saga + event transport) and D030 (Hexagonal + Service Mesh by layer), now applied a
third time in a third distinct domain. S032 ships a GateSession aggregate (C#, no
MediatR/AutoMapper per repo standard) whose Close() throws unless every recorded EPC has
a verdict, plus a ManifestSyncConsumer demonstrating the event-driven pre-positioning
half of the decision.

_Also 2026-08-10 -- added P028/D033/S033 from inbox/RFID/ingestion-transport-protocol-req.md
(the second formal RFID Event Platform consultation, after P027/D032/S032). Problem: the
platform spec fully documents Ingestion Service *behavior* (envelope validation, event_id
idempotency/dedupe for offline replay, stateless horizontal scaling for 7.7/11.11 peaks) and
edge *behavior* (batch send with edge-generated event_id, offline buffering) but never names a
transport protocol for the edge (DC Site Server / Store Gateway) -> central Ingestion Service
WAN hop -- the only MQTT reference in the spec is scoped to a different hop entirely (local
reader -> edge agent, inside one site's LAN, a vendor acceptance boundary), so nothing in the
existing documentation constitutes an implicit choice for this hop. kb-search found P027 as the
only meaningful precedent (overlap_score ~0.3 on rfid/edge-computing/offline-first -- same
platform, different problem), correctly producing a new CREATE-mode record rather than an update
to P027/D032. lens-determiner paired Event-Driven Architecture against Hexagonal Architecture --
a pairing not previously used in this KB, and deliberately not a repeat of P027/D032's DDD+EDA
pairing, since this problem is fundamentally a transport/integration-boundary decision rather
than an invariant-ownership one. EDA proposed extending the platform's broker fabric (MQTT/AMQP)
across the WAN with broker-native persistent sessions for offline buffering and ack; Hexagonal
proposed a stateless HTTPS/mTLS batch API as a single explicit "driving port," matching the
spec's own description of the Ingestion Service as "the only edge-facing surface." Hexagonal won
as primary because the Clarified Scope's hardest constraints -- no per-client session state at
any scale, and reliable firewall/proxy traversal across a large number of geographically
distributed retail and warehouse sites -- put EDA's persistent-broker-session approach in direct,
structural tension with the requirements, not just at a stylistic disadvantage; EDA's reliability
instincts (at-least-once, idempotent event_id) were folded in as the internal publish pipeline
the platform already runs, entirely decoupled from this hop's protocol choice, rather than
rejected. gRPC streaming, named as a candidate in the original problem, was noted and set aside
rather than developed as a third lens, since it shares EDA's core weakness for this hop (a
long-lived connection poorly handled by corporate/retail proxies) without adding benefit for a
batch-oriented, WAN-latency-tolerant workload. S033 ships an ASP.NET Core batch ingestion
endpoint (C#, no MediatR/AutoMapper per repo standard) with per-event_id synchronous acks, paired
with an edge-side EdgeIngestionClient that only purges its offline buffer on explicit
server-confirmed event_ids.

_Also 2026-08-17 -- added P029/D034/S034 from inbox/RFID/returns-flow-req.md
(the platform's third formal RFID Event Platform consultation, after P027/D032/S032
and P028/D033/S033). Problem discovered as a real gap while drawing
manual/rfid-sequence-diagrams.md Diagram J (EAS Exit Check): the paid-EPC cache has a
fully documented "add on sale" path but no "remove on return" path -- a live
loss-prevention control failure, not a doc gap, since a returned-then-resold item would
silently defeat EAS forever after. kb-search against the existing 28 entries found P027
(~0.28 overlap on rfid/edge-computing/offline-first/event-driven-architecture) and P028
(~0.19 overlap on rfid/edge-computing/offline-first) as the only meaningful precedents,
both below the 0.8 UPDATE threshold -- correctly a new CREATE-mode record. The
requesting brief named one explicit architectural tension to resolve rather than dodge:
cross-store return validation needs to know what a *different* site did, which directly
conflicts with the platform's established "no synchronous registry calls from site
operations" principle (GateSession/P027/D032, EAS, checkout all rely on local cache +
pre-positioned data only). lens-determiner paired Saga Pattern against Event-Driven
Architecture -- a fresh contrast axis for this platform, distinct from D032's
invariant-ownership-vs-transport split and D033's transport-vs-transport split: how
much eventual consistency a return can tolerate before authorizing an irreversible
action (a refund). Saga won as primary because a return is a genuine multi-step process
needing a real compensating action (deny refund, quarantine item) that a single
event-reaction cannot express as cleanly (the same reasoning that won Saga over
choreography in D031/PTL); EDA was folded in, not rejected, as (a) the only path for
same-store returns and (b) the transport that routes the resulting paid-EPC cache
invalidation to the correct store, partitioned by the *originating* site_id rather than
broadcast fleet-wide. The decision's one deliberate architectural concession: cross-store
return verification is now the platform's first and only named, scoped exception to
"no synchronous registry calls," justified by the fact that (unlike checkout/EAS/gate
flows) the customer and item are still physically present with the refund not yet
issued, which also enabled a genuinely new third fail-safe outcome
(`PendingVerification`) that GateSession's binary FailOpen/FailClosed never needed,
since GateSession's failure cases all occur after goods have already left custody. Also
resolves, for the first time in this KB, a real answer for `tid_registry` (previously
noted across prior RFID consultations as existing but never consumed by any live flow)
and for `CountOnly` GTIN returns (a new short-lived `ISoldEpcLedger`, scoped narrowly to
the return-window duration, since CountOnly's lifecycle-tracking omission otherwise
leaves zero ground truth to validate a CountOnly return against). Does not contradict
D032 or D033 -- extends the platform's no-sync-call principle with its first explicit,
narrow, audited exception rather than eroding it silently. Six open items logged in P029
(retry SLA duration, whether cross-store returns are validated business policy, the
CountOnly ledger retention window, local TID-cache population reliability, the
FraudHold-to-resolution workflow, and POS refund-timing integration) -- all operational
validation gaps in the same style already established by P027, not architectural gaps in
D034 itself._
