# Architecture Transition Roadmap
**Goal:** Backend Developer → Software Architecture Specialist
**Current Phase:** Foundation
**Last Updated:** 2026-04-28
**Consultation Count:** 3

---

## Current Focus
Build foundational pattern literacy and systems thinking by working through real architecture problems with the team. Each consultation is a learning opportunity — the goal right now is exposure breadth across the core architectural domains.

## Skill Domains

### Distributed Systems
- [ ] CAP theorem and consistency models — foundational for every distributed design decision
- [ ] Failure modes: partial failures, network partitions, cascading failures
- [x] Idempotency and exactly-once semantics — encountered in D018 (CreateOrderHandler idempotency check); reinforced in D020 (ACL adapter idempotency key on outbox worker retry)
- [ ] Backpressure and flow control
- [x] Single-writer enforcement — encountered in D020 (FOR UPDATE SKIP LOCKED for outbox worker; Kubernetes single-replica deployment constraint)

### Data Architecture Patterns
- [x] CQRS (Command Query Responsibility Segregation) — separating reads from writes — **encountered in D018 (OMS read/write split: command handlers vs order_status_view projections)**
- [ ] Event Sourcing — state as a sequence of events
- [ ] Database per service pattern
- [ ] Polyglot persistence

### System Design Fundamentals
- [ ] Load balancing strategies and tradeoffs
- [ ] Caching layers: CDN, application, database
- [ ] API gateway patterns
- [ ] Service discovery and health checking

### Architectural Patterns (Structural)
- [ ] Hexagonal Architecture (Ports & Adapters)
- [x] Saga Pattern — distributed transaction coordination — **evaluated in D019 (Returns flow); rejected for 2-service case in favor of outbox+ACL; understand when Saga is warranted (3+ services) vs overkill**
- [ ] Strangler Fig — incremental legacy migration
- [x] Domain-Driven Design: bounded contexts, aggregates, ubiquitous language — **encountered in D018 (Order aggregate root, state machine, Anti-Corruption Layers, RolloutPolicy domain service) and D019 (Package value object, PreHoldState snapshot, Returns sub-machine invariants)**
- [x] Modular Monolith — module boundary enforcement, schema isolation, future service extraction path — **encountered in D020 (4-module OMS: Order/Payment/Returns/Configuration with separate PostgreSQL schemas, ID-only cross-module access, ACL adapters as boundary contracts)**

### Event-Driven Architecture
- [ ] Message brokers: Kafka, RabbitMQ — when to use each
- [ ] Event schema design and evolution
- [ ] Dead letter queues and poison pill handling
- [x] Choreography vs. orchestration — **actively evaluated in D019: Returns flow uses choreography (outbox+ACL) rather than orchestration (Saga) — understand the threshold (service count, failure isolation requirements) that tips the balance**
- [x] Outbox pattern — **encountered in D018 (reliable Sprint Connect event delivery); extended in D019 (new domain events for Returns, OnHold, PackageLost dispatched through same outbox table)**

### Organizational & Communication Skills
- [ ] Architecture Decision Records (ADRs) — how to document decisions
- [ ] Communicating tradeoffs to non-technical stakeholders
- [ ] Leading design reviews and RFC processes
- [ ] Defining and measuring non-functional requirements

### Cloud & Infrastructure
- [ ] Managed services vs. self-hosted tradeoffs
- [ ] Multi-region and disaster recovery patterns
- [ ] Infrastructure as Code concepts
- [ ] Cost modeling for architecture decisions
- [x] Kubernetes deployment topology for stateful workers — **encountered in D020 (single-replica outbox worker with terminationGracePeriodSeconds, graceful shutdown via CancellationToken, vs stateless API replicas with HPA)**

---

## Exposure Log (concepts encountered in consultations)

| Concept | First Seen | KB Ref | Skill Domain | Priority |
|---------|------------|--------|--------------|----------|
| DDD — Order aggregate root with state machine | 2026-04-27 | P013/D018/S018 | Architectural Patterns | High |
| DDD — Anti-Corruption Layer (ACL) toward Sprint Connect | 2026-04-27 | P013/D018 | Architectural Patterns | High |
| DDD — Bounded context definition for an OMS | 2026-04-27 | P013/D018 | Architectural Patterns | High |
| DDD — Domain events (OrderCreated, PickStarted, OrderDelivered) | 2026-04-27 | P013/D018/S018 | Event-Driven Architecture | High |
| DDD — Domain service (RolloutPolicy for phased rollout) | 2026-04-27 | P013/D018/S018 | Architectural Patterns | Medium |
| CQRS — Write model (command handlers) vs read model (projections) | 2026-04-27 | P013/D018/S018 | Data Architecture Patterns | High |
| CQRS — Synchronous projection update in Phase 1 to avoid eventual consistency | 2026-04-27 | P013/D018 | Data Architecture Patterns | Medium |
| Outbox pattern — order_events table as reliable event delivery mechanism | 2026-04-27 | P013/D018/S018 | Event-Driven Architecture | High |
| Idempotency — check-before-insert in CreateOrderHandler (D015 pattern reuse) | 2026-04-27 | P013/D018/S018 | Distributed Systems | High |
| State machine design — order lifecycle (Pending → BookingConfirmed → ... → Paid/Cancelled) | 2026-04-27 | P013/D018/S018 | System Design Fundamentals | High |
| Phased rollout — feature gating by store code via domain policy | 2026-04-27 | P013/D018 | System Design Fundamentals | Medium |
| DDD + CQRS combination — why neither alone is sufficient | 2026-04-27 | D018 | Architectural Patterns | High |
| DDD — Value object as aggregate child (Package on Order) | 2026-04-28 | P014/D019/S019 | Architectural Patterns | High |
| DDD — PreHoldState snapshot pattern for non-destructive pause | 2026-04-28 | P014/D019/S019 | Architectural Patterns | High |
| DDD — Returns sub-state machine as in-aggregate extension | 2026-04-28 | P014/D019/S019 | Architectural Patterns | High |
| Exception handling as aggregate method (ReportPackageLost → auto-triggers OnHold) | 2026-04-28 | P014/D019/S019 | Architectural Patterns | Medium |
| Saga Pattern — when warranted (3+ services) vs overkill (2-service outbox+ACL sufficient) | 2026-04-28 | P014/D019 | Architectural Patterns | High |
| Choreography vs. orchestration — service count as the threshold decision | 2026-04-28 | P014/D019 | Event-Driven Architecture | High |
| Multi-channel factory pattern — ChannelOrderFactory with per-channel validation | 2026-04-28 | P014/D019/S019 | System Design Fundamentals | Medium |
| Refund vs Credit Note distinction — post-delivery return flow vs partial pick shortage | 2026-04-28 | P014/D019 | System Design Fundamentals | Medium |
| Modular Monolith — 4-module topology with schema-per-module isolation | 2026-04-28 | P015/D020/S020 | Architectural Patterns | High |
| Module boundary erosion — why convention-only enforcement degrades without CI gates | 2026-04-28 | P015/D020 | Architectural Patterns | High |
| FOR UPDATE SKIP LOCKED — PostgreSQL single-writer advisory lock for outbox workers | 2026-04-28 | P015/D020/S020 | Distributed Systems | High |
| Graceful shutdown pattern — CancellationToken + terminationGracePeriodSeconds for Kubernetes workers | 2026-04-28 | P015/D020/S020 | Cloud & Infrastructure | High |
| Microservices rejection criteria — when team size + atomic TX + no broker = monolith wins | 2026-04-28 | P015/D020 | Architectural Patterns | High |
| ACL adapter per integration — encapsulating external contract volatility (WMS/TMS/POS/STS/LegacyBackend) | 2026-04-28 | P015/D020/S020 | Architectural Patterns | Medium |
| Security layering — JWT per channel + HMAC per integration + Vault for secrets | 2026-04-28 | P015/D020 | System Design Fundamentals | Medium |
| Dead-letter queue for outbox — DLQ table + alerting on DLQ depth for operational safety | 2026-04-28 | P015/D020/S020 | Event-Driven Architecture | Medium |

---

## Recent Learning Opportunities

### Consultation: OMS Design (2026-04-27) — KB: P013 / D018 / S018

This consultation introduced the two most important patterns for enterprise .NET system design.
Here is what to study:

**1. DDD Aggregate Root Pattern**
The `Order` class in S018 is a textbook aggregate root. Every method (`ConfirmBooking`, `StartPick`,
`MarkDelivered`) enforces a pre-condition on `Status` before mutating state. The aggregate CANNOT
reach an invalid state through normal code paths. Study the `Guard()` helper and how `DrainEvents()`
separates domain event collection from dispatch.
- Study: "Domain-Driven Design" by Eric Evans (chapters 5–6); .NET microservices e-book (Microsoft)
- Practice: Implement `ConfirmBookingHandler` and `MarkDeliveredHandler` following the S018 pattern.

**2. CQRS — Write vs Read Model Separation**
The OMS diagnosis shows a common trap: if you build only one model, read queries (POS checking
order status 10x/min) will run joins on the same tables that order creation is writing to. The
CQRS split gives each path its own optimized schema. The `order_status_view` table in S018 is a
flat projection — no joins, no aggregate loading.
- Study: Martin Fowler's CQRS article (martinfowler.com); Greg Young's original CQRS paper
- Practice: Design the `order_fulfillment_view` projection and the `StartPickHandler` that updates it.

**3. Outbox Pattern**
The `order_events` table is the outbox. Every domain event is written to this table IN THE SAME
TRANSACTION as the order mutation. A background poller reads it and calls Sprint Connect. This
eliminates the "dual-write problem" — you can never have an order updated but the event lost
(which happens if you call an HTTP API directly in the handler).
- Study: "Transactional Outbox Pattern" on microservices.io
- Practice: Implement `OutboxPoller` that queries `order_events WHERE processed_at IS NULL`,
  calls Sprint Connect adapter, sets `processed_at`, handles retries.

**4. Anti-Corruption Layer (ACL)**
`SprintConnectAdapter` is an ACL. It translates OMS domain events into Sprint Connect's API
contract. When Sprint Connect changes their API, you change one file. The domain events never
change. This is the most underused pattern in integration projects.
- Study: DDD anti-corruption layer concept; hexagonal architecture ports and adapters
- Practice: Define the `ISprintConnectPort` interface and the `SprintConnectAdapter` implementation.

---

### Consultation: OMS Extensions (2026-04-28) — KB: P014 / D019 / S019

This consultation deepened the DDD aggregate model and introduced the Saga vs. outbox tradeoff
as a real decision point. Here is what to study:

**1. Value Objects vs Entities in DDD Aggregates**
The `Package` type in S019 is modelled as a record (value object), not a class (entity), even though
it has identity (`PackageId`, `TrackingId`). This is intentional: Package has no independent
lifecycle — it only exists as part of an Order. When do you use a value object vs a child entity?
The rule: if the object cannot live outside the aggregate and has no independent behavior, it is a
value object. Study the DDD blue book chapter on entities vs value objects.
- Study: "Domain-Driven Design" ch. 5 (Entities vs Value Objects); Vaughn Vernon's "Implementing DDD"
- Practice: Can you identify two other value objects in the OMS domain that could be modelled as records?

**2. The Saga vs. Outbox+ACL Decision Point**
The Saga Pattern was evaluated and rejected for the Returns flow. The key insight: Saga is a
pattern for automated compensating transactions across 3+ services. When you have only 2 services
(OMS + TMS) and already have an outbox, the outbox IS your lightweight saga — each domain event is a
saga step, and the ACL handles the external call. Memorize this threshold:
- 2 services, simple handoff: outbox + ACL is sufficient.
- 3+ services, or automated rollback required: introduce a Saga orchestrator (D012).
- Human-in-the-loop resolution (like PackageLost): neither Saga nor outbox — use OnHold state + staff command.
- Practice: Draw the Returns flow as a sequence diagram showing domain event → outbox poller → TMS API call → callback → ScheduleReturnPickup command.

**3. Non-Destructive State Suspension (OnHold Snapshot Pattern)**
The `_preHoldState` field is a pattern you will encounter again: how do you pause a state machine
without losing context? The naive approach (always resume from the state before hold) requires event
replay or storing the entire aggregate snapshot. The snapshot field is the lightweight alternative.
Understand its invariant: only writable when entering OnHold, nulled on Release, must never be
present when Status != OnHold.
- Practice: Write a unit test for `PlaceOnHold` → `Release` round-trip, and for `PlaceOnHold` from
  a terminal state (should throw).

**4. Choreography vs. Orchestration**
This consultation made the choreography vs. orchestration distinction concrete:
- **Choreography** (what D019 uses): each service reacts to domain events independently. OMS raises
  `ReturnRequested`, the outbox poller + TMS adapter react without a central coordinator.
- **Orchestration** (what Saga would add): a central state machine drives the process by explicitly
  calling each participant in sequence.
Choreography is simpler for low fan-out flows; orchestration gives visibility and compensation for
complex flows. This is one of the most important tradeoffs in distributed systems design.
- Study: "Enterprise Integration Patterns" — Process Manager vs. Routing Slip; Chris Richardson's
  microservices.io on Saga orchestration vs. choreography.

---

### Consultation: OMS System Architecture (2026-04-28) — KB: P015 / D020 / S020

This consultation confirmed the full production architecture and introduced three concepts that
architects work with daily: Modular Monolith boundary discipline, single-writer worker patterns,
and the Microservices rejection argument. Here is what to study:

**1. Modular Monolith — What Separates It From a Big Ball of Mud**
The difference between a disciplined Modular Monolith and a tangled monolith is three rules:
(a) schema isolation — no cross-schema JOINs, no cross-schema FK constraints;
(b) ID-only cross-module references — you pass a GUID, not an EF Core navigation property;
(c) module boundary assertions in CI — automated tests that fail if OMS.Order.Domain references
OMS.Payment.Domain. Without the CI gate, rule (a) and (b) erode within 6-12 months under
feature pressure. The D020 analysis identifies boundary erosion as the primary long-term risk.
- Study: Sam Newman's "Building Microservices" ch. 4 (decomposition strategies); modular monolith
  pattern on martinfowler.com
- Practice: Write a Roslyn-based unit test that asserts the Order module does not reference
  Payment module types. Add it to the CI pipeline.

**2. FOR UPDATE SKIP LOCKED — The PostgreSQL Single-Writer Pattern**
Every outbox worker, job scheduler, or task queue that uses PostgreSQL eventually needs this.
`FOR UPDATE SKIP LOCKED` selects rows and locks them atomically. Any competing query with
`SKIP LOCKED` skips locked rows instead of blocking. The practical effect: if you accidentally
run two outbox worker instances, they process disjoint sets of rows rather than racing on the
same rows and producing duplicates.
The key operational requirement that follows: every downstream HTTP call (ACL adapter) must
include an idempotency key (`X-Idempotency-Key: {event_id}`), because the outbox guarantees
at-least-once delivery, not exactly-once.
- Study: PostgreSQL documentation on explicit row locking (`FOR UPDATE`, `SKIP LOCKED`);
  "Transactional Outbox Pattern" on microservices.io
- Practice: Write a load test that launches two outbox worker instances simultaneously and
  verifies that no outbox event is processed twice.

**3. When to Reject Microservices — Stating the Argument Precisely**
The Microservices lens evaluation made the rejection argument concrete. Memorize these four
conditions that collectively justify a Modular Monolith over microservices:
(a) Small team: <5-8 engineers cannot sustain independent per-service CI/CD, runbooks, and
    on-call rotations.
(b) Atomic transactions required: if two modules (Order + Payment) must commit together,
    microservices require a Saga pattern. A Modular Monolith gets this for free.
(c) No message broker: reliable cross-service event delivery at microservices scale requires
    Kafka or RabbitMQ. If the constraint says "no broker," microservices are not viable.
(d) Volume below the inflection point: independent scaling delivers ROI only when specific
    modules have dramatically different load profiles. At 70K order lines/day uniform load,
    the cost of independent scaling exceeds the benefit.
Being able to state these four conditions with the corresponding evidence from a real system
is exactly the kind of reasoning a senior architect must do in design reviews.

**4. Kubernetes Deployment Topology for Stateful vs Stateless Workers**
The OMS has two Kubernetes deployment types with fundamentally different constraints:
- `oms-api` (stateless): scale horizontally with HPA, any replica can handle any request
- `oms-outbox-worker` (single-writer): must stay at replicas=1, must handle SIGTERM gracefully
The `terminationGracePeriodSeconds` setting (60s) gives the outbox worker time to finish its
in-flight batch before Kubernetes kills the pod. Without this, rolling updates can cause a
delivery gap where events are neither processed by the old pod (already killed) nor the new
pod (not yet started). The `CancellationToken` + `OperationCanceledException` handling in S020
is the .NET pattern for receiving SIGTERM and exiting cleanly.
- Study: Kubernetes documentation on pod lifecycle and graceful termination; .NET
  `BackgroundService` and `IHostedService` shutdown documentation
- Practice: Deploy the OutboxWorker from S020 to a local k3d cluster and observe the
  graceful shutdown sequence using `kubectl logs`.

---

## Phase Progression Criteria

- **Foundation → Intermediate:** Can explain 5+ patterns with their tradeoffs; has encountered problems across 3+ distinct domains; no longer needs to look up what a pattern does before engaging with it
- **Intermediate → Advanced:** Anticipates which lenses will be selected before seeing them; spots org-level and team-topology constraints in problems; can run a design review solo
