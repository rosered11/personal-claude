# Architecture Transition Roadmap
**Goal:** Backend Developer → Software Architecture Specialist
**Current Phase:** Foundation
**Last Updated:** 2026-07-13
**Consultation Count:** 6

---

## Current Focus
P019/D024 was your first *incident-response* consultation grounded entirely in raw log evidence rather than a design brief or architecture review -- you had to mine ~19K lines across 15 pod logs to separate the two real failure signatures (SQL command timeout vs duplicate-key/silent-skip) from noise before any lens could be applied. This is a distinctly senior-architect skill: root-causing from production telemetry, not from a pre-digested problem statement. It also produced your first case of *deliberately blending two lenses into one decision* rather than picking a winner and rejecting the other outright (Hexagonal adapter hardening as primary + Event-Driven idempotency/DLQ as a required companion) -- pay attention to when this blending move is the right call vs when a single lens should decisively win. Next step: deepen resilience-engineering vocabulary (transient-fault classification, backoff/retry policy design, DLQ operational discipline) since it recurred here and in D020/D023 but has never been the primary lens.

## Skill Domains

### Distributed Systems
- [ ] CAP theorem and consistency models — foundational for every distributed design decision
- [ ] Failure modes: partial failures, network partitions, cascading failures
- [x] In-process coupling vs real network boundary — **encountered in D023 (Order.API project-referenced Master/Portal despite separate deploy pipelines; "independently deployable" does not equal "decoupled" unless the call path is actually swapped from assembly reference to HTTP/gRPC)**
- [x] Idempotency and exactly-once semantics — encountered in D018 (CreateOrderHandler idempotency check); reinforced in D020 (ACL adapter idempotency key on outbox worker retry); reinforced again in D024 (extended the same dedup-table pattern to a Kafka consumer for the first time, plus a MERGE/upsert as a database-level idempotency backstop)
- [ ] Backpressure and flow control
- [x] Transient-fault handling and retry-policy design (exponential backoff, error-code classification) — **first applied in D024 (Polly policy classifying SQL Server transient error codes including -2 Execution Timeout Expired, which EF Core's default SqlServerRetryingExecutionStrategy does not cover out of the box — a subtlety only visible by reading the actual stack trace in production logs)**
- [x] Single-writer enforcement — encountered in D020 (FOR UPDATE SKIP LOCKED for outbox worker; Kubernetes single-replica deployment constraint)

### Data Architecture Patterns
- [x] CQRS (Command Query Responsibility Segregation) — separating reads from writes — **encountered in D018 (OMS read/write split: command handlers vs order_status_view projections)**
- [ ] Event Sourcing — state as a sequence of events
- [ ] Database per service pattern
- [ ] Polyglot persistence

### System Design Fundamentals
- [ ] Load balancing strategies and tradeoffs
- [ ] Caching layers: CDN, application, database
- [x] API gateway patterns — **encountered in D023 (YARP Gateway + channel-based BFFs proposed as the facade layer in a Strangler Fig migration; the gateway ships before any internal boundary rewrite, not after)**
- [ ] Service discovery and health checking

### Architectural Patterns (Structural)
- [x] Hexagonal Architecture (Ports & Adapters) — **evaluated in D022 (FMSUpdateAdapter); rejected for a single-adapter fix in favor of Layered private helper, but the port/adapter framing was used to diagnose the correctness boundary. Won outright in D024 (IOrderPersistenceGateway) as the primary lens — first time Hexagonal was chosen as the winning option, not just a diagnostic frame**
- [x] Saga Pattern — distributed transaction coordination — **evaluated in D019 (Returns flow); rejected for 2-service case in favor of outbox+ACL; understand when Saga is warranted (3+ services) vs overkill**
- [x] Strangler Fig — incremental legacy migration — **encountered in D023 (facade-first: ship Gateway/BFF/OTel in front of the coupled Order.API immediately, then strangle Order-to-Master/Portal project references one seam at a time via a feature-flagged legacy-vs-HTTP port, instead of a big-bang network-boundary rewrite)**
- [x] Domain-Driven Design: bounded contexts, aggregates, ubiquitous language — **encountered in D018 (Order aggregate root, state machine, Anti-Corruption Layers, RolloutPolicy domain service) and D019 (Package value object, PreHoldState snapshot, Returns sub-machine invariants)**
- [x] Modular Monolith — module boundary enforcement, schema isolation, future service extraction path — **encountered in D020 (4-module OMS: Order/Payment/Returns/Configuration with separate PostgreSQL schemas, ID-only cross-module access, ACL adapters as boundary contracts)**

### Event-Driven Architecture
- [x] Message brokers: Kafka, RabbitMQ — when to use each — **first hands-on Kafka consultation in D024 (EventBus.Kafka consumer for validate-service); "Attempt=1/1" retry budget and at-least-once redelivery semantics were the direct trigger for the duplicate-key failures observed**
- [ ] Event schema design and evolution
- [x] Dead letter queues and poison pill handling — **encountered as a gap in D024 (production behavior was "skip after 1 retry" with no DLQ, meaning failed order events were silently dropped); DLQ + alerting adopted as a required companion to the primary fix, not optional**
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

| Priority chain resolution pattern — explicit 3-tier fallback with null guards | 2026-06-22 | P017/D022/S022 | Architectural Patterns | Medium |
| Hexagonal vs Layered tradeoff — when to extract a port vs refine in-place | 2026-06-22 | P017/D022 | Architectural Patterns | Medium |
| Adapter-layer correctness — branch asymmetry as a silent-failure risk in adapters | 2026-06-22 | P017/D022 | System Design Fundamentals | High |
| Test subclassing pattern — TestableFMSAdapter to expose private methods for unit testing | 2026-06-22 | P017/S022 | System Design Fundamentals | Medium |

| In-process coupling vs real network boundary — "independently deployable" != "decoupled" | 2026-07-09 | P018/D023 | Distributed Systems | High |
| API Gateway pattern — YARP as facade for AuthN/AuthZ, rate limiting, routing to BFFs | 2026-07-09 | P018/D023/S023 | System Design Fundamentals | High |
| BFF (Backend-for-Frontend) — organizing by channel, not by BU, to avoid combinatorial service growth | 2026-07-09 | P018/D023 | System Design Fundamentals | High |
| Strangler Fig — facade-first sequencing, per-seam feature-flagged legacy vs target adapter | 2026-07-09 | P018/D023/S023 | Architectural Patterns | High |
| Deferring a binary architecture decision pending real data — re-checking a prior precedent's (D020) rejection criteria instead of assuming they still hold | 2026-07-09 | P018/D023 | Organizational & Communication Skills | High |
| OpenTelemetry as the unifying tracing/metrics/logging layer (W3C traceparent across Gateway -> BFF -> domain services) | 2026-07-09 | P018/D023 | Distributed Systems | Medium |

| Root-causing from raw production logs (19K lines, 15 pods) rather than a pre-digested problem statement | 2026-07-13 | P019 | Organizational & Communication Skills | High |
| SQL Server transient-fault classification — error code -2 (Execution Timeout) not covered by EF Core's default retry strategy | 2026-07-13 | P019/D024/S024 | Distributed Systems | High |
| Kafka at-least-once delivery semantics — consumer retry budget ("Attempt=1/1") and the silent-skip data-loss risk when no DLQ exists | 2026-07-13 | P019/D024 | Event-Driven Architecture | High |
| MERGE ... WITH (HOLDLOCK) as a database-level idempotency backstop, complementary to (not a replacement for) an application-level dedup table | 2026-07-13 | P019/D024/S024 | Data Architecture Patterns | High |
| Blending two lenses into one decision (Hexagonal primary + Event-Driven required companion) instead of picking a single winner | 2026-07-13 | P019/D024 | Architectural Patterns | Medium |

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


### Consultation: FMSUpdateAdapter ShipmentProvider Priority Fix (2026-06-22) — KB: P017 / D022 / S022

This consultation was a targeted correctness fix, not a greenfield design problem — but it surfaced
two architectural concepts worth internalizing. Here is what to study:

**1. When NOT to Extract a Port (Hexagonal vs Layered Trade-Off)**
The Hexagonal lens proposed an `IShipmentProviderResolver` interface. The Layered lens proposed a
private helper method. Both achieve the same correctness outcome. The key insight: interface
extraction is appropriate when (a) multiple callers need the rule, or (b) the rule must be swappable
at runtime. When neither applies — single adapter, stable rule — the interface adds DI registration
overhead for zero benefit. Learn to ask "who else will call this?" before creating a new abstraction.
- Study: "Simple Made Easy" (Rich Hickey); the YAGNI principle applied to abstraction layers
- Practice: For each interface in your codebase with only one implementation, ask: is this port
  protecting against external volatility, or is it accidental complexity?

**2. Priority Chain Resolution as a Named Pattern**
The `ResolveShipmentProvider` method implements a specific pattern: ordered fallback resolution
with null guards at each tier. This pattern appears in many domains:
- Carrier resolution (this problem)
- Price resolution (price list > segment price > base price)
- Locale resolution (user preference > account locale > system default)
The key structural rule: each tier is evaluated independently with an IsNullOrWhiteSpace guard.
Merging two tiers into a single variable (like the original `thirdPartyLogistic`) destroys the
ability to override at the individual tier level. Named private helper + comments = self-documenting
contract that enforces the business rule visibly.
- Practice: Find one other place in the Activity Process Service codebase where two data sources
  are merged prematurely. Refactor using the same 3-tier pattern.

**3. Branch Asymmetry as a Hidden Correctness Risk**
The weighted-item and normal-item branches had asymmetric package handling — the weighted branch
lacked a null guard, the normal branch handled null correctly. This is a class of bug that is easy
to introduce when copy-pasting and modifying one branch without updating the other. The fix:
unify both branches to call a single shared helper. Any future change to the resolution rule only
touches one method. This also eliminates the "clone verification" risk recorded in D006.
- Study: P006 (D006) — copy-paste silent-failure; the DRY principle applied to adapter branching


---

### Consultation: OMS Service-Boundary Review (2026-07-09) -- KB: P018 / D023 / S023

This was your first *proposal-review* consultation rather than a greenfield design or incident --
you were handed an already-researched architecture review and asked to weigh in as the specialist.
That is a distinct skill from the earlier OMS consultations: you are validating/adjusting someone
else's reasoning, not producing the first draft. Here is what to study:

**1. "Independently Deployable" Is Not the Same As "Decoupled"**
Order.API, Master.API, and Portal.API ship on separate pipelines but Order.API still holds a
compile-time project reference into Master and Portal. This is the single most important
architectural smell in the whole review: deploy independence gives you nothing if a change to
Master's internals still forces an Order.API rebuild. The fix pattern -- keep the existing
IHandler-style port, but swap the *implementation* from an in-process call to an HTTP/gRPC call --
is the same seam-based technique you will see again and again in brownfield work.
- Study: Sam Newman, "Monolith to Microservices" (ch. 2-3, on identifying and cutting seams)
- Practice: In S023, trace how IMasterServiceClient lets Order.API code stay unaware of whether
  the call is in-process or over HTTP. Could you retrofit this pattern onto a coupled pair of
  modules in your own codebase?

**2. Strangler Fig vs "Big Bang" -- Choosing a Migration Strategy, Not Just an End State**
Both architect lenses agreed on the *target* architecture (Gateway, BFF, OpenTelemetry, broker-fed
read-model). The real decision was about *sequencing*: cut over all three services' boundaries at
once (Microservices lens), or ship the highest-value, lowest-risk pieces first and strangle the
coupling one seam at a time (Strangler Fig lens, which won). This is a pattern you will need
constantly once you are advising on live production systems rather than greenfield ones -- the
"right" end state is often not the hard part; the safe path to it is.
- Study: Martin Fowler, "StranglerFigApplication" (martinfowler.com); the microservices.io
  decomposition patterns
- Practice: Identify one coupled pair in a system you own. Sketch the legacy-vs-target adapter
  seam (like S023) and decide which single call-site you would strangle first, and why.

**3. Reconciling With a Prior Decision Instead of Silently Re-Deciding**
D020 (an earlier consultation on this same OMS) already established Modular Monolith over
Microservices under specific conditions (small team, atomic-TX need, no broker, sub-inflection-point
volume). This new proposal implies those conditions might no longer hold (services are already
split, a broker is being discussed) -- but the traffic/team data to actually confirm that shift
does not exist yet. The disciplined move was not to silently override D020, and not to blindly
re-apply it either, but to explicitly defer the binary choice until the missing data exists. This
is what "treating your knowledge base as institutional memory" looks like in practice.
- Study: How to write an ADR that supersedes vs extends a prior ADR (e.g., the "Superseded by"
  convention in Michael Nygard's original ADR post)
- Practice: Next time you revisit a past decision, explicitly write one sentence stating whether
  you are confirming it, extending it, or challenging it -- and why.

**4. BFF Organized By Channel, Not By Tenant/BU**
The review's own recommendation -- start with channel-based BFFs (Web-Admin, Marketplace, Mobile)
carrying BU/tenant context via JWT claim, rather than one BFF per BU -- is a good instinct to
internalize: avoid combinatorial service growth by defaulting to the coarsest split that still
works, and only fragment further when a specific consumer's release cadence or scaling profile
genuinely diverges.
- Study: Sam Newman's BFF pattern writeup; "Building Microservices" ch. 4 on decomposition axes
- Practice: For a multi-tenant system you know, would channel-based or tenant-based BFF splitting
  serve better -- and what evidence would change your answer?

---

### Consultation: Validate-Service Order Save Failures (2026-07-13) -- KB: P019 / D024 / S024

This was your first incident-response consultation grounded entirely in raw production log
evidence -- 19,228 lines across 15 Kubernetes pod logs -- rather than a design brief or a
pre-written architecture review. Here is what to study:

**1. Root-Causing From Logs, Not From a Problem Statement**
The user's own description ("some tables intermittently fail to save, retry usually works") was
correct but incomplete. Grepping for ERROR/Exception/Timeout/retry across all 15 pod logs found
that only 2 of 15 pods had any error-level entries at all -- confirming genuine intermittency --
and revealed two *distinct* failure signatures the user had conflated into one: a SQL command
timeout, and a duplicate-key/silent-skip failure. Always separate "what the user observed" from
"what the evidence actually shows"; they overlapped here but were not identical.
- Practice: Next time you get a vague "sometimes X fails" report, grep for the failure keywords
  across every available log source *before* forming a root-cause hypothesis, and check whether
  all observed failures are actually the same failure.

**2. Transient-Fault Classification Is Not Automatic**
The stack trace showed `SqlServerExecutionStrategy.ExecuteAsync` -- meaning EF Core's built-in
retry-on-failure strategy was already configured -- yet the exception still reached the caller on
the first hit. The reason: SQL error `-2` (Execution Timeout Expired) is not in EF Core's default
transient-error list unless explicitly added. This is a recurring lesson: "we already have retry
logic" is not the same as "we retry the specific failure we are seeing." Always check *which*
error codes a resilience policy actually classifies as transient.
- Study: Polly documentation on `WaitAndRetryAsync` and custom exception predicates;
  `SqlServerRetryingExecutionStrategy` source for its default transient error list
- Practice: For one resilience policy in your own codebase, list every error code it retries and
  every error code it does not, and check that list against your actual production error logs.

**3. Database-Level Idempotency (MERGE/Upsert) vs Application-Level Dedup Table**
D024 used both, deliberately: the `ProcessedEvents` dedup table is a fast-path guard (cheap,
avoids most duplicate writes before they reach SQL Server), while the `MERGE ... WITH (HOLDLOCK)`
upsert is the correctness backstop for the race the dedup check itself cannot fully prevent (two
pods processing the same event within milliseconds of each other, observed directly in the qh492
log at 08:51:29.217 and .243). Neither alone is sufficient; understand why both layers exist.
- Study: "Idempotent Receiver" pattern (Enterprise Integration Patterns); SQL Server MERGE
  documentation and its known race conditions without `HOLDLOCK`
- Practice: In S024, trace exactly which race the dedup table misses that the MERGE catches.

**4. When to Blend Two Lenses Instead of Picking a Winner**
Unlike most prior consultations (D022 rejected Hexagonal outright; D023 picked Strangler Fig over
Microservices), D024 explicitly adopted *both* lenses -- Hexagonal as the primary fix (it matched
the literal log evidence) and Event-Driven idempotency/DLQ as a required companion (without it,
silent data loss remained possible). Learn to recognize when two lenses address genuinely
different failure modes of the *same* problem rather than competing solutions to the *same*
failure mode -- that is the signal that blending, not choosing, is the correct move.
- Practice: Re-read D015 (P010) — the closest KB precedent — and identify why it also blended DDD
  (root cause fix) with EDA (idempotency), rather than picking one.


## Phase Progression Criteria

- **Foundation → Intermediate:** Can explain 5+ patterns with their tradeoffs; has encountered problems across 3+ distinct domains; no longer needs to look up what a pattern does before engaging with it
- **Intermediate → Advanced:** Anticipates which lenses will be selected before seeing them; spots org-level and team-topology constraints in problems; can run a design review solo
