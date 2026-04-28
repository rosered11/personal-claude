---
id: D020
title: "OMS Modular Monolith — Module Boundary Enforcement + Outbox Worker Graceful Shutdown"
date: 2026-04-28
problem_id: P015
chosen_option: "Disciplined Modular Monolith with Enforced Module Boundaries, Schema Isolation, and Graceful-Shutdown Outbox Worker"
tags:
  - oms
  - modular-monolith
  - order-management
  - domain-driven-design
  - cqrs
  - outbox
  - anti-corruption-layer
  - state-machine
  - dotnet
  - postgresql
  - redis
  - kubernetes
  - integration
  - webhook
  - multi-channel
  - security
  - jwt
  - hmac
extends: D018
related_snippets:
  - S020
---

# D020 — OMS Modular Monolith: Module Boundary Enforcement + Outbox Worker Graceful Shutdown

## Chosen Option

**Disciplined Modular Monolith with Enforced Module Boundaries, Schema Isolation, and Graceful-Shutdown Outbox Worker**

The confirmed Sprint Connect OMS architecture is a 4-module Modular Monolith (Order, Payment,
Returns, Configuration) built on .NET 8 / PostgreSQL 16 / Redis 7, deployed on Kubernetes.
The Modular Monolith lens confirms the choice as architecturally correct for the stated
constraints. The Microservices lens confirms the rejection rationale and, critically, identifies
the structural disciplines the monolith must maintain to prevent boundary erosion over time.

## Extends

This decision extends D018 (OMS DDD+CQRS+Outbox) and D019 (OMS Extended Aggregate). D018 is the
authoritative Phase 1 baseline; D019 covers Phase 2 aggregate extensions; D020 covers the full
system architecture confirmation: module topology, deployment, security, and outbox worker
operational discipline.

## Lenses Evaluated

- **Lens A (Modular Monolith):** Validates the confirmed decision. Examines module boundary
  enforcement via schema isolation and ID-only cross-module access. Identifies outbox worker
  graceful shutdown as the key operational gap. Recommends ArchUnit-style boundary assertions as
  a CI gate.
- **Lens B (Microservices):** Stress-tests the rejection. Confirms that atomic transaction
  requirements (Order + Payment), no-message-broker constraint, small team size, and 70K
  order lines/day scale collectively make microservices the wrong choice. The Microservices lens
  contributes the insight that module boundary disciplines must be enforced from day one — they
  are the service extraction enablers for the future.

## Full System Architecture

### Module Topology

```
OMS Modular Monolith
├── Order Module         (schema: orders)       ← aggregate root, state machine, outbox
├── Payment Module       (schema: payment)      ← payment records, invoice tracking
├── Returns Module       (schema: returns)      ← return requests, reverse logistics
└── Configuration Module (schema: config)       ← rollout policy, channel config, store settings
```

Cross-module rules:
- No cross-schema JOINs in SQL
- No navigation properties across module boundaries in EF Core
- Cross-module data access: read by primary key only (ID-only reference)
- Domain events for cross-module side effects (via Outbox, not direct method calls)

### Deployment Topology

```
Kubernetes Cluster
├── oms-api            (Deployment: 2 replicas, stateless, HPA-eligible)
│   ├── ASP.NET Core Web API
│   ├── MediatR command/query handlers
│   └── EF Core (write path to PostgreSQL)
├── oms-outbox-worker  (Deployment: 1 replica — single-writer constraint)
│   ├── BackgroundService polling outbox_events
│   ├── FOR UPDATE SKIP LOCKED (PostgreSQL advisory lock substitute)
│   └── ACL adapter dispatch (HTTP calls to WMS/TMS/POS/STS/LegacyBackend)
└── Redis              (read model cache — CQRS read side)

PostgreSQL (single instance)
├── schema: orders    (Order aggregate, order_lines, outbox_events, order_packages)
├── schema: payment   (payment_records, invoice_records)
├── schema: returns   (return_requests, return_pickups)
├── schema: config    (rollout_policy, channel_config, store_settings)
└── DB: audit         (separate audit database — all write operations logged)
```

### Security Model

| Layer | Mechanism | Scope |
|-------|-----------|-------|
| Inbound API | JWT (per channel) | Gateway, Marketplace, Kiosk, POSTerminal, BulkImport |
| Outbound integration | HMAC signature | WMS, TMS, POS, STS, LegacyBackend callbacks |
| Secret management | HashiCorp Vault | DB credentials, HMAC keys, JWT signing keys |
| Webhook inbound | HMAC verification | WMS pick/put-away callbacks, TMS delivery/lost callbacks |

### ACL Adapter Layer

```
WmsAdapter         ← translates WMS pick/put-away callbacks → OMS domain commands
TmsAdapter         ← translates TMS delivery/lost callbacks → OMS domain commands
PosAdapter         ← translates POS recalculation events → OMS domain commands
StsAdapter         ← translates STS batch files → OMS domain commands
LegacyBackendAdapter ← routes orders to Backend for non-rolled-out stores
```

Each adapter:
- Owns the external system's contract (request/response DTOs)
- Maps to OMS domain commands — the domain never sees external system types
- Handles retry logic for outbound calls
- Is the only component that changes when an external system's API changes

### Outbox Worker — Critical Operational Requirements

1. **FOR UPDATE SKIP LOCKED**: Prevents duplicate processing if a second replica is accidentally
   deployed. Any row locked by the active worker is skipped by any competing replica.

2. **Graceful shutdown**: The worker must handle `CancellationToken` from Kubernetes SIGTERM.
   On shutdown, in-flight batch must complete before the pod terminates. Set
   `terminationGracePeriodSeconds: 60` in the Kubernetes Deployment spec.

3. **Dead-letter handling**: After N failed retry attempts, move unprocessable events to
   `outbox_events_dlq` table. Alert on DLQ depth > 0.

4. **Idempotent ACL calls**: All outbound HTTP calls to WMS/TMS/POS/STS must be idempotent
   (include `X-Idempotency-Key: {event_id}` header). Outbox events can be replayed on retry.

### Module Boundary Enforcement (CI Gate Recommendation)

Add ArchUnit or Roslyn-based assembly boundary tests as a CI gate:
- `OMS.Order.Domain` must not reference any type from `OMS.Payment.*`, `OMS.Returns.*`
- `OMS.Payment.Domain` must not reference any type from `OMS.Order.Domain` except by GUID ID
- EF Core model builder must not add cross-schema FKs (enforce via custom convention)

See S020 for the OutboxWorker implementation with graceful shutdown.

## Blended Rationale

1. **Transactional integrity**: Modular Monolith gives ACID guarantees across Order + Payment
   operations — atomic order creation with payment reservation in a single PostgreSQL transaction.
   Microservices would require a Saga orchestrator for this fundamental operation.

2. **Operational simplicity**: 2 API replicas + 1 outbox worker is the minimal viable topology
   for this scale. Microservices would require 4+ independent service deployments, each with
   their own health probes, HPA rules, and runbooks — not justified for a small team at
   70K order lines/day.

3. **Outbox correctness**: FOR UPDATE SKIP LOCKED is the PostgreSQL-native mechanism for
   single-writer enforcement without an external lock service. Graceful shutdown handling
   closes the pod-restart event delivery gap identified by the Modular Monolith lens analysis.

4. **ACL as the isolation contract**: Each ACL adapter encapsulates a full external system
   contract. Module boundary violations are most likely to appear as ACL bypasses (calling
   WMS directly from the domain). The ACL adapter rule must be as strictly enforced as
   the schema isolation rule.

5. **Future extraction readiness**: The module disciplines (schema isolation, ID-only cross-module
   access, ACL adapters, domain events via outbox) are exactly the pre-conditions for extracting
   a module into an independent service. When volume or team size justifies extraction, the
   change is: add an HTTP client, add a message broker, remove the in-process call. No domain
   rewrite needed.

## Rejected Options

- **Microservices (Order/Payment/Returns/Configuration as independent services):** Requires
  distributed transactions (Saga), message broker (contradicts explicit constraint), per-service
  operational overhead incompatible with team size, and no scaling ROI at current volume.
  Correctly rejected. Microservices remain the forward evolution path when volume grows 10x
  or team grows to support independent service ownership.

## Tradeoffs Accepted

- **Module boundary erosion risk**: Convention-enforced at development time, not runtime. Requires
  CI gate (ArchUnit/Roslyn tests) and code review discipline. Without enforcement, coupling
  creeps in within 6-12 months of production use.
- **Single-instance PostgreSQL bottleneck**: At 10x current volume, the DB becomes the scaling
  ceiling. Acceptable now; migration path is read replicas → CQRS async projections → module
  extraction.
- **Single-replica outbox worker restart gap**: During Kubernetes rolling updates, there is a
  brief window with no outbox worker. Mitigated by graceful shutdown handling and by the
  Outbox's at-least-once delivery guarantee on resume.
- **Shared release cycle**: A bug in Configuration module forces redeployment of Order module.
  Acceptable at current team size; module-level deployment independence is a future microservices
  benefit.

## Related KB Entries

- **D018** — OMS DDD+CQRS+Outbox baseline: D020 extends D018 with module topology, deployment
  architecture, security model, and outbox worker operational requirements.
- **D019** — OMS Extended Aggregate: D020 is the system-level complement to D019's aggregate-level
  extensions. Both extend D018.
- **D012** — Distributed Transaction Strategy: the decision to avoid microservices defers the
  need for Saga pattern. If modules are later extracted, apply D012 tiered strategy.
- **D015** — Idempotency Pattern: ACL adapter outbound calls must include idempotency keys
  following the D015 pattern.
- **D001** — EF Core Hot-Path: when loading the Order aggregate (with packages, order lines)
  for write commands, apply IDbContextFactory + eager loading to avoid N+1.

## Next Steps

1. Configure Kubernetes Deployment for `oms-outbox-worker` with `replicas: 1` and
   `terminationGracePeriodSeconds: 60`.
2. Add `FOR UPDATE SKIP LOCKED` SQL to outbox poller query (see S020).
3. Add `outbox_events_dlq` table and alerting on DLQ depth.
4. Implement HMAC verification middleware for inbound WMS/TMS webhooks.
5. Set up HashiCorp Vault integration for DB credentials and HMAC keys.
6. Add CI boundary assertion tests for cross-module reference violations.
7. Configure Redis TTL for read model cache entries (recommend 5-minute TTL aligned to
   business SLA for order status staleness).
8. Add `X-Idempotency-Key` header to all outbound ACL adapter HTTP calls.
