---
name: Lens Combinations
description: Effective lens pairs for this domain based on KOS patterns — gives Javit context for what lens-determiner is likely to return and whether the pair makes sense
type: project
---

# Effective Lens Combinations by Problem Domain

Derived from KOS P# patterns, D# decisions, and incident post-mortems. These pairings have been validated as producing meaningful contrast for problems in this domain.

---

## EF Core / .NET Performance Problems

**Tags:** `ef-core`, `n+1`, `performance`, `dotnet`, `connection-pool`

**Lens pair:** Hexagonal Architecture vs Layered Architecture

**Contrast rationale:** Hexagonal treats EF Core as an external adapter, making it possible to swap for Dapper/raw SQL on hot paths without touching domain logic. Layered accepts EF Core coupling throughout all layers. The contrast is: adapter isolation vs pragmatic coupling.

---

## ETL / Batch Processing Problems

**Tags:** `etl`, `batch-processing`, `transaction`, `ef-core`, `dotnet`

**Lens pair:** Event-Driven Architecture vs Layered Architecture

**Contrast rationale:** Event-Driven replaces the polling/staging-table pattern with event streams, enabling backpressure and parallel consumer scaling. Layered keeps the proven while-loop with per-batch TX. The contrast is: message-driven decoupling vs simplicity-first batch loop.

---

## Distributed System Design Problems

**Tags:** `distributed`, `transaction`, `saga`, `microservices`

**Lens pair:** CQRS vs Saga Pattern

**Contrast rationale:** CQRS separates read/write models, simplifying per-service consistency without distributed coordination. Saga sequences compensating transactions across services. The contrast is: model separation vs explicit compensation flow.

---

## Database Selection Problems

**Tags:** `database`, `architecture`, `selection`, `scalability`

**Lens pair:** Domain-Driven Design vs Microservices

**Contrast rationale:** DDD selects DB per bounded context (each aggregate owns its storage). Microservices select DB per service with polyglot persistence. The contrast is: bounded context alignment vs service autonomy.

---

## API / Rate Limiting Problems

**Tags:** `rate-limiting`, `api`, `scalability`, `redis`

**Lens pair:** Microservices vs Serverless

**Contrast rationale:** Microservices embed rate limiting per service with persistent process state. Serverless delegates to API gateway without persistent state. The contrast is: embedded control vs infrastructure delegation.

---

## Subprocess / Orchestration Problems

**Tags:** `airflow`, `python`, `subprocess`, `orchestration`, `timeout`

**Lens pair:** Event-Driven Architecture vs Hexagonal Architecture

**Contrast rationale:** Event-Driven replaces subprocess calls with async event queue (decouple trigger from execution). Hexagonal ports subprocess as an explicit adapter with a timeout interface. The contrast is: async decoupling vs explicit port isolation.

---

## Airflow Orchestration Chain Problems

**Tags:** `airflow`, `python`, `orchestration`, `trigger-dagrun`, `xcom`, `child-dag`, `dag-dependency`

**Lens pair:** Saga Pattern vs Hexagonal Architecture

**Contrast rationale:** Saga asks "does each step in the trigger chain have explicit failure boundaries and compensation?" — exposes missing short-circuit and silent failure propagation. Hexagonal asks "are the adapter contracts (XCom, Jinja, child DAG availability) explicitly validated?" — exposes render_template_as_native_obj type violations and missing child DAG health checks. Together: failure propagation vs contract soundness.

**First used:** P012 / D017 — confirmed as high-quality contrast for this domain.

---

## Greenfield OMS / Order Lifecycle System Design

**Tags:** `oms`, `order-management`, `state-machine`, `integration`, `microservices`, `dotnet`, `aks`

**Lens pair:** Domain-Driven Design vs CQRS

**Contrast rationale:** DDD organizes by domain cohesion — what the Order IS and how it transitions
through its lifecycle. CQRS organizes by access pattern — how the order is READ (POS, tracking,
reporting) vs WRITTEN (create, pick, deliver). DDD produces a richer domain model but risks
expensive aggregate loads on read paths. CQRS keeps access paths clean but risks fat command
handlers without aggregate discipline. The contrast reveals whether to structure the OMS around
domain model cohesion or around access pattern optimization.

**Outcome (P013 / D018):** Decision was a BLEND — DDD as primary organizing principle (aggregate
root + state machine + ACL) + CQRS for read/write separation (projections) + outbox for reliable
integration. Neither lens alone is sufficient for an OMS. DDD + CQRS is the canonical .NET pattern.

**First used:** P013 / D018 — confirmed as high-quality contrast for greenfield OMS design.

---

## OMS Aggregate Extension / Returns / Exception Handling

**Tags:** `oms`, `order-lifecycle`, `ddd`, `aggregate`, `state-machine`, `returns`, `exception-handling`, `outbox`

**Lens pair:** Domain-Driven Design vs Saga Pattern

**Contrast rationale:** DDD pushes new states and entities inward — Package as value object, OnHold
as aggregate state with snapshot, Returns as in-aggregate sub-state-machine. Cohesive but grows
aggregate surface area. Saga pushes Returns and PackageLost outward — treat as distributed flows
with explicit compensating steps coordinated by an external orchestrator. The tension reveals the
key boundary question: how much of the exception/returns domain belongs inside the aggregate vs in
an orchestrated multi-service flow?

**Outcome (P014 / D019):** DDD wins — Returns is 2-service (OMS+TMS), and the existing outbox+ACL
from D018 already provides compensation semantics. Saga lens contributed: each Returns step raises
an explicit domain event, enabling compensation without a Saga state machine. Rule crystallized:
Saga warranted for 3+ services; for 2-service flows with existing outbox, outbox+ACL is sufficient.

**First used:** P014 / D019 — confirmed as high-quality contrast for OMS aggregate extension problems.

---

## OMS Full System Architecture / Modular Monolith Decision

**Tags:** `oms`, `modular-monolith`, `kubernetes`, `outbox`, `anti-corruption-layer`, `multi-channel`, `security`

**Lens pair:** Modular Monolith vs Microservices

**Contrast rationale:** Modular Monolith validates the confirmed choice — examines boundary
discipline (schema isolation, ID-only cross-module access, ACL adapters), deployment topology
(single-writer outbox worker), and structural degradation risks (boundary erosion without CI gates).
Microservices stress-tests the rejection — forces explicit articulation of why atomic TX + small team
+ no-broker + sub-inflection-point volume collectively make microservices wrong at this stage.
The contrast grounds the monolith decision in concrete constraints rather than preference.

**Outcome (P015 / D020):** Modular Monolith wins — confirmed by both lenses. Microservices lens
contributed: module boundary disciplines are the pre-conditions for future service extraction.
Rule crystallized: Modular Monolith + schema isolation + ID-only cross-module + ACL adapters =
the four structural disciplines that keep the monolith extractable. Violation of any one degrades
the monolith to a distributed big-ball-of-mud.

**First used:** P015 / D020 — confirmed as high-quality contrast for full OMS system architecture
confirmation and Modular Monolith vs Microservices decisions generally.

---

## When KB Search Returns High Overlap (≥ 0.8 Jaccard)

If kb-search-agent returns a result with overlap_score ≥ 0.8, instruct lens-determiner to:
1. Note the existing decision's chosen lens
2. Select lenses that contrast with or refine the existing decision
3. Pass `kb_influence: "refinement"` in the lens output (not `"contrast"`)

This prevents re-selecting the same lenses for nearly-identical problems, encouraging KB growth with nuanced variations.

---

## RabbitMQ Consumer / Message Ingestion Problems

**Tags:** `dotnet`, `rabbitmq`, `ef-core`, `postgresql`, `background-service`, `hosted-service`, `message-consumer`

**Lens pair:** Hexagonal Architecture vs Event-Driven Architecture

**Contrast rationale:** Hexagonal asks "how do we structure the code layers?" — yields adapter isolation, testability, clean port boundaries. Event-Driven asks "how do we handle the message flow reliably?" — yields at-least-once semantics, idempotency, DLQ routing, backpressure. Neither alone is sufficient: Hexagonal without EDA misses operational safety; EDA without Hexagonal produces a tangled consumer.

**Outcome (P016 / D021):** Hexagonal wins as primary organizing principle; EDA lens contributed three absorbed improvements (idempotency, DLQ, backpressure). Rule crystallized: for single-queue, single-consumer, insert-only use cases — RabbitMQ.Client over MassTransit when producers use raw JSON.

**First used:** P016 / D021 — confirmed as high-quality contrast for RabbitMQ consumer integration on existing .NET APIs.

---

## Adapter-Layer Correctness / Priority Logic Problems

**Tags:** `dotnet`, `correctness`, `adapter`, `null-safety`, `priority-logic`, `unit-testing`

**Lens pair:** Hexagonal Architecture vs Layered Architecture

**Contrast rationale:** Hexagonal asks "should the resolution rule be a named port (interface) testable in isolation?" — reveals whether extraction creates reuse or just overhead. Layered asks "can a private helper method in the same class serve as the contract?" — reveals whether the rule is truly adapter-internal or cross-cutting.

**Outcome (P017 / D022):** Layered wins for single-adapter, single-caller, brownfield context. Hexagonal lens contributed: explicit priority tier naming and null guard discipline. Rule crystallized: extract to an interface only when 2+ callers exist or when the rule must be swappable at runtime. Private helper + named priority comments is the correct pattern for isolated adapter resolution chains.

**First used:** P017 / D022 — confirmed as high-quality contrast for adapter correctness / priority resolution problems.

---

## OMS Service-Boundary-Fix + Cross-Cutting-Layer Proposal Review

**Tags:** `oms`, `microservices`, `service-boundary-violation`, `api-gateway`, `bff`,
`observability`, `read-model`, `dotnet`

**Lens pair:** Microservices vs Strangler Fig

**Contrast rationale:** Microservices asks "what should the target service boundary look like once
Order/Master/Portal are truly network-decoupled, with Gateway+BFF+broker fan-out as first-class
citizens?" Strangler Fig asks "given production traffic already flows through the coupled
Order.API today, how do we incrementally introduce the Gateway/BFF/observability facade and peel
off project-references one seam at a time without a risky big-bang rewrite?" The tension is
target-state design vs safe incremental migration sequencing -- both matter for a proposal-review
of a live production system (not greenfield).

**Outcome (P018/D023):** Strangler Fig wins as the sequencing strategy; Microservices lens's
target-state components (Gateway, BFF, OTel, broker-relayed Outbox, read-model) and its concrete
HTTP-adapter code pattern (typed HttpClient + Polly + traceparent) are both absorbed into each
strangled seam. Rule crystallized: for a system with an existing Modular Monolith precedent (D020)
and unresolved traffic/team/multi-tenancy open questions, do not let a proposal review commit to
full microservices in one step -- sequence via Strangler Fig and re-evaluate the end-state once
data exists.

**Note on lens reuse:** deliberately avoided reusing the Modular Monolith vs Microservices pair
from P015/D020 even though this is the same OMS lineage, since KB overlap was low (0.111, well
below the 0.8 dedup/reuse-avoidance threshold) and a facade/migration-sequencing question is a
genuinely different tension than an internal module-boundary confirmation question. Reserve
Modular Monolith vs Microservices for full internal-boundary confirmation problems; use
Microservices vs Strangler Fig for "how do we safely get from coupled-today to decoupled-target"
sequencing problems.

**First used:** P018/D023 -- new lens pair for this domain, confirmed as high-quality contrast for
proposal-review consultations on brownfield service-boundary fixes with layered cross-cutting
concerns (gateway/BFF/observability/read-model) riding on top.
