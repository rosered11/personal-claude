# Architecture Transition Roadmap
**Goal:** Backend Developer → Software Architecture Specialist
**Current Phase:** Intermediate
**Last Updated:** 2026-08-10
**Consultation Count:** 11

---

## Current Focus
**Phase promotion: Foundation → Intermediate.** Across 8 consultations you have now explained and applied 8+ patterns with tradeoffs (DDD, CQRS, Outbox, Saga, Strangler Fig, Modular Monolith, Hexagonal, Event-Driven, Layered, Architecture Fitness Functions, Service Mesh) across distinct domains (greenfield OMS design, ETL/batch pipelines, production incident response, database maintenance, and now three rounds of pure codebase audit on the same live system). You are also starting to do Intermediate-track work early: in P025/D030 you (via the pipeline) recognized that two lenses were not competing for the same fix but attacking *different layers of the same problem* -- a form of tradeoff reasoning that goes beyond "pick the better option."

P025/D030 was the third same-day-adjacent audit of Sprint-OMS (after P018/D023, P024/D029), and it deliberately did not repeat the same lens pairing (Hexagonal+Strangler Fig) once secrets management and transport security became co-equal findings -- lens-determiner instead paired Hexagonal with Service Mesh, a lens that had never been evaluated in this KB before. The decision blended them by layer rather than by priority: Hexagonal (application/compile-time layer) as primary because only it satisfies the "secrets must be rotated via a seam" constraint, with Service Mesh's most urgent code-fixable finding (disabled TLS cert validation) folded into the Hexagonal implementation as an interim fix, and full mesh adoption (mTLS, proxy-level resilience) explicitly routed to a Phase-2 track pending external infrastructure. Next step: since Service Mesh has now entered your KB for the first time, spend deliberate study time on its actual capability boundary (what a sidecar proxy can and cannot fix) so you can anticipate, before the pipeline runs, which findings belong at the transport layer vs. the application layer.

Consultation 9 (P026/D031, 2026-08-10) is your first consultation entirely outside the
Sprint-OMS/ETL lineage: a warehouse Put-to-Light (PTL) integration problem spanning
WMS, SAP, a PTL/MHE hardware controller, and a Marketplace. You (via the pipeline)
correctly recognized this as a fresh domain requiring generalization of already-learned
vocabulary rather than reuse of an OMS-specific pattern: Saga Pattern vs Event-Driven
Architecture (orchestration vs choreography) is a pairing you first studied in P014/D019
purely as a service-count threshold rule; here it was re-applied as a layer split
(orchestration owns cross-system invariants, events carry state between systems) in a
domain that has nothing to do with order aggregates. Next step: notice that this is the
same "blend by layer, not by picking a winner" skill from D030 (Hexagonal + Service
Mesh), now demonstrated a second time with a completely different lens pair -- that
repetition across unrelated domains is the real signal this reasoning pattern has been
internalized, not just memorized for one system.


Consultation 10 (P027/D032, 2026-08-10) is your first RFID-domain consultation --
a sub-problem of an already-built RFID Event Platform whose base architecture had
never itself received a formal KB entry. You correctly treated the "Clarified
Scope" section as pre-decided constraints (manifest-based check, not global
registry; must support both intra-site and inter-site movement) rather than
re-litigating them, and picked Domain-Driven Design vs Event-Driven Architecture --
a pairing whose contrast this time was not "orchestration vs choreography"
(D031's framing) but "who owns the zero-loss invariant vs how does the manifest
physically get to the right edge in time." DDD won as primary because zero-loss
is exactly the kind of constraint that needs one aggregate whose Close() method
can refuse to complete if any read went unevaluated -- no event listener,
however well-designed, can offer that guarantee on its own. Event-Driven's
manifest pre-positioning insight was folded in as the transport, not rejected,
extending the "blend by concern, not by picking a winner" skill first seen in
D030 (Hexagonal + Service Mesh, by layer) and D031 (Saga + events, by decision
vs transport) into a third distinct domain and a third distinct kind of split
(invariant-ownership vs distribution-timing). Next step: notice that all three
blends (D030, D031, D032) share one underlying question -- "which of these two
lenses can make a promise the other cannot?" -- and try naming that promise
explicitly, before the pipeline runs, next time two lenses are proposed.

Consultation 11 (P028/D033, 2026-08-10) is your second RFID-domain consultation, and
the first time a lens pair was chosen specifically to illuminate a *transport/protocol*
decision rather than an invariant-ownership or coupling decision: Event-Driven
Architecture (extend the platform's broker fabric across the WAN) vs Hexagonal
Architecture (a single stateless HTTPS port as the edge-facing boundary). Unlike D032's
blend (DDD owns the invariant, EDA is folded in as transport), here EDA itself was the
option not chosen -- its proposed transport (persistent per-site broker sessions) was in
direct structural tension with two hard constraints (statelessness at scale, firewall
traversal across thousands of retail sites), not merely a stylistic tradeoff. This is a
sharper version of the "which lens can make a promise the other cannot" question you
named as your own next step after D032: Hexagonal could promise both zero per-client
session state and default-open firewall traversal; EDA could not promise either without
extra infrastructure. EDA's reliability instinct (at-least-once, idempotent event_id)
was still folded in -- as the platform's already-existing *internal* publish pipeline,
completely decoupled from the WAN hop's protocol. Next step: notice that Hexagonal has
now won as primary lens for three structurally different reasons across three
consultations -- a secrets-rotation seam (D030), a service-boundary port (D029), and now
an edge-facing transport-protocol boundary (D033) -- and try to name, before the next
consultation, what these three all have in common (hint: each is a place where something
volatile and *external* to the core meets something that must stay stable).

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
- [x] Transport protocol selection at a WAN/edge boundary — choosing between extending a message-broker fabric vs. a stateless synchronous API port, decided primarily by operational constraints (per-client session state, firewall/proxy traversal at scale) rather than raw feature richness — first evaluated in D033 (RFID edge-to-Ingestion-Service WAN hop)

### Architectural Patterns (Structural)
- [x] Hexagonal Architecture (Ports & Adapters) — **evaluated in D022 (FMSUpdateAdapter); rejected for a single-adapter fix in favor of Layered private helper, but the port/adapter framing was used to diagnose the correctness boundary. Won outright in D024 (IOrderPersistenceGateway) as the primary lens — first time Hexagonal was chosen as the winning option, not just a diagnostic frame. Reinforced in D029 (service-boundary ports) and extended into a new application area in D030 — a Hexagonal port (ISecretProvider) used as a *secrets rotation seam*, not just a service-boundary decoupling mechanism** — extended again in D033 (RFID Ingestion batch API): a Hexagonal port used as an *edge-facing transport-protocol boundary*, chosen specifically because it could satisfy statelessness and firewall-traversal constraints that the alternative (extending the message bus across the WAN) could not**
- [x] Service Mesh (sidecar proxy pattern) — mTLS, transport-level retry/circuit-breaking, and centralized observability with zero application code changes — **first evaluated in D030 (Istio/Linkerd PERMISSIVE-mode proposal for the Sprint-OMS gRPC fabric). Not chosen as primary because it cannot fix compile-time coupling or plaintext secrets (application-layer concerns), but its one urgent, code-fixable finding — disabled TLS certificate validation — was folded into the winning Hexagonal solution as an interim fix; full mesh adoption deferred to an explicit Phase-2 track pending external K8s sidecar-injection infrastructure. Key lesson: know the mesh's scope boundary before proposing it**
- [x] Saga Pattern — distributed transaction coordination — **evaluated in D019 (Returns flow); rejected for 2-service case in favor of outbox+ACL; understand when Saga is warranted (3+ services) vs overkill**
- [x] Saga Pattern — won outright as the *primary* lens for the first time in D031 (PTL Task Orchestrator), in a domain with no relationship to OMS; the threshold that mattered here was not service count but "who enforces the cross-cutting invariants" (1 order=1 box=1 invoice, single active box per slot, mixed-carton rejection) — a generalization of the D019 threshold rule beyond service-count alone
- [x] Strangler Fig — incremental legacy migration — **encountered in D023 (facade-first: ship Gateway/BFF/OTel in front of the coupled Order.API immediately, then strangle Order-to-Master/Portal project references one seam at a time via a feature-flagged legacy-vs-HTTP port, instead of a big-bang network-boundary rewrite)**
- [x] Domain-Driven Design: bounded contexts, aggregates, ubiquitous language — **encountered in D018 (Order aggregate root, state machine, Anti-Corruption Layers, RolloutPolicy domain service) and D019 (Package value object, PreHoldState snapshot, Returns sub-machine invariants)** Extended in D032 (GateSession aggregate): DDD applied outside greenfield OMS for the first time -- the aggregate here exists purely to make a code-level invariant (zero-loss) impossible to violate, not to model a rich business lifecycle.
- [x] Modular Monolith — module boundary enforcement, schema isolation, future service extraction path — **encountered in D020 (4-module OMS: Order/Payment/Returns/Configuration with separate PostgreSQL schemas, ID-only cross-module access, ACL adapters as boundary contracts)**
- [x] Architecture fitness functions — automated, CI-enforced tests of structural rules (e.g. dependency direction) — **first produced in D029/S029 (NetArchTest suite forbidding a service's Core project from depending on another service's Infrastructure/Integration assembly, seeded with a shrink-only allow-list of the exact P024 violations)**

### Event-Driven Architecture
- [x] Message brokers: Kafka, RabbitMQ — when to use each — **first hands-on Kafka consultation in D024 (EventBus.Kafka consumer for validate-service); "Attempt=1/1" retry budget and at-least-once redelivery semantics were the direct trigger for the duplicate-key failures observed**
- [ ] Event schema design and evolution
- [x] Dead letter queues and poison pill handling — **encountered as a gap in D024 (production behavior was "skip after 1 retry" with no DLQ, meaning failed order events were silently dropped); DLQ + alerting adopted as a required companion to the primary fix, not optional**
- [x] Choreography vs. orchestration — **actively evaluated in D019: Returns flow uses choreography (outbox+ACL) rather than orchestration (Saga) — understand the threshold (service count, failure isolation requirements) that tips the balance**
- [x] Event-carried state transfer as a saga's transport layer — encountered in D031: the saga (orchestration) still communicates over an async event bus (StockUpdated, PtlTaskConfirmed, SoStoCreated), showing choreography and orchestration are not mutually exclusive — events can be the *medium* while a saga remains the *decision-maker*
- [x] Manifest/state pre-positioning — publishing data ahead of a physical event so an edge/consumer already has what it needs when the event occurs, rather than fetching synchronously at decision time — first named explicitly in D032 (RFID manifest.created published to the destination site before goods physically arrive, consumed into a local read-model an aggregate evaluates against)
- [x] Recognizing EDA's scope limits, not just its strengths — in D033, extending the platform's broker fabric across a WAN to thousands of edge sites was evaluated and rejected as the primary transport (not because at-least-once/ordered-replay was wrong, but because persistent per-site broker sessions reintroduce per-client state and raise real firewall-traversal risk); EDA's reliability instincts were still reused, but only as the *internal* publish pipeline, never exposed across the WAN hop itself
- [x] Outbox pattern — **encountered in D018 (reliable Sprint Connect event delivery); extended in D019 (new domain events for Returns, OnHold, PackageLost dispatched through same outbox table)**

### Security & Secrets Management
- [x] Secrets as an abstraction seam, not a config value — **first deep dive in D030 (ISecretProvider port; environment-variable-first with a logged legacy-appsettings fallback so migration is zero-disruption); the underlying rule — committed secrets must be *rotated* via a seam, not merely deleted from source — is what made Hexagonal the only lens satisfying the hard constraints**
- [x] Transport security as an explicit architectural concern, not a config flag — **encountered in D030: ~30 gRPC clients had TLS certificate validation entirely disabled via `DangerousAcceptAnyServerCertificateValidator`; interim fix pins to an internal CA thumbprint sourced through the same ISecretProvider port, full mTLS deferred to Service Mesh Phase 2**
- [ ] Zero-trust networking principles (never trust the internal network by default)
- [ ] Secret rotation strategies and "secretless" architectures (workload identity, short-lived tokens)
- [ ] Threat modeling for architecture reviews (STRIDE or similar)
- [x] Security layering — JWT per channel + HMAC per integration + Vault for secrets — encountered earlier in D020; **this recurring theme (D020 → D030) is why this got promoted to its own domain rather than staying folded into System Design Fundamentals**

### Organizational & Communication Skills
- [ ] Architecture Decision Records (ADRs) — how to document decisions
- [ ] Communicating tradeoffs to non-technical stakeholders
- [ ] Leading design reviews and RFC processes
- [ ] Defining and measuring non-functional requirements
- [x] Distinguishing "unused but present" from "absent" as evidence — **encountered in D030: Polly was referenced in the project but never actually configured as a resilience policy — a materially different finding from "no resilience library exists," and only visible by reading the actual DI wiring, not just the .csproj**

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
| Distributed monolith — independently deployable services with a real gRPC seam that is bypassed by direct in-process Infrastructure references | 2026-07-31 | P024/D029 | Distributed Systems | High |
| Architecture fitness functions — encoding a structural rule (dependency direction) as an executable, CI-enforced test with a shrink-only allow-list | 2026-07-31 | P024/D029/S029 | Architectural Patterns | High |
| Codebase-comprehension audit as a distinct consulting mode — deriving architecture purely from source (ProjectReferences, using-statements, migrations, appsettings) with no docs | 2026-07-31 | P024 | Organizational & Communication Skills | Medium |
| Hexagonal ports owned by the consuming service vs. borrowing another service's interface — ownership of the contract, not just its existence, determines real decoupling | 2026-07-31 | P024/D029/S029 | Architectural Patterns | High |

| Complementary (not competing) lenses — Hexagonal (compile-time coupling + secrets rotation seam) and Service Mesh (transport-layer TLS/resilience) each confirmed distinct real evidence for the same audit; the decision blended by *layer*, not by picking a single winner | 2026-07-31 | P025/D030 | Architectural Patterns | High |
| ISecretProvider port — Hexagonal ports applied to secrets management as a rotation seam, a new application of Ports & Adapters beyond service-boundary decoupling | 2026-07-31 | P025/D030/S030 | Security & Secrets Management | High |
| Service Mesh (sidecar proxy pattern) — first real evaluation in this KB; scope boundary is transport-layer TLS/retry/circuit-breaking/observability, and it cannot fix compile-time coupling or plaintext secrets | 2026-07-31 | P025/D030 | Cloud & Infrastructure | High |
| Folding a deferred lens's single most urgent, code-fixable finding into the chosen lens's implementation, instead of discarding the whole lens or adopting it wholesale | 2026-07-31 | P025/D030 | Architectural Patterns | Medium |
| "Referenced but never configured" as a distinct audit finding from "absent" — Polly present in the project but no resilience policy actually wired up | 2026-07-31 | P025/D030 | Organizational & Communication Skills | Medium |
| Saga Pattern winning outright as primary orchestrator in a non-OMS domain (warehouse PTL) — invariant-ownership, not service count, as the deciding threshold | 2026-08-10 | P026/D031/S031 | Architectural Patterns | High |
| Event-carried state transfer as saga transport — an orchestrator can use an async event bus for I/O while remaining the sole decision-maker for cross-cutting invariants | 2026-08-10 | P026/D031/S031 | Event-Driven Architecture | High |
| Synchronous rejection vs eventual-consistency reaction — a hardware controller needing an immediate error (mixed-store carton) cannot be served by a pure event listener, only by a synchronous call into the orchestrator | 2026-08-10 | P026/D031 | Distributed Systems | Medium |
| Cross-domain lens reuse — applying an OMS-learned lens pairing (Saga vs Event-Driven) to a warehouse/hardware-integration domain with zero shared vocabulary, confirming the reasoning generalizes rather than being domain-specific | 2026-08-10 | P026/D031 | Organizational & Communication Skills | High |
| Aggregate as invariant-enforcer, not just lifecycle model — GateSession's only job is to make "every EPC must get a verdict" impossible to violate in code (Close() throws otherwise), a narrower and more targeted use of DDD than the rich Order aggregates in D018/D019 | 2026-08-10 | P027/D032/S032 | Architectural Patterns | High |
| Event-carried state transfer as pre-positioning, not just notification — manifest.created is published ahead of the physical transfer so the destination edge already holds the data it needs when the gate event happens, distinct from D031's use of events as after-the-fact status notification | 2026-08-10 | P027/D032/S032 | Event-Driven Architecture | High |
| Explicit, auditable fail-safe mode as a first-class field — FailSafeMode (Verified/FailOpen/FailClosed) is resolved once and stamped on every verdict, turning "what do we do when we can't verify" from an implicit runtime accident into a visible, queryable decision | 2026-08-10 | P027/D032/S032 | System Design Fundamentals | High |
| Blend-by-concern applied a third time in a third domain (invariant-ownership vs distribution-timing), reinforcing that this is a general architectural reasoning skill, not a one-off insight tied to Sprint-OMS or PTL | 2026-08-10 | P027/D032 | Architectural Patterns | High |
| Hexagonal port applied to a transport-protocol boundary — a stateless HTTPS batch API chosen as "the only edge-facing surface" specifically because it satisfies statelessness and firewall-traversal constraints a broker-based alternative could not | 2026-08-10 | P028/D033/S033 | Architectural Patterns | High |
| EDA evaluated and rejected as a primary transport, not just folded in — persistent per-site broker sessions over a WAN put statelessness and firewall-traversal constraints in direct structural tension, distinct from every prior D030/D031/D032 case where EDA was the winning or complementary transport | 2026-08-10 | P028/D033 | Event-Driven Architecture | High |
| Operational constraints (firewall/proxy traversal, per-client session state at fleet scale) as the deciding factor in a protocol choice, ahead of raw feature richness | 2026-08-10 | P028/D033 | System Design Fundamentals | High |

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


---

### Consultation: OMS Codebase Audit -- Distributed Monolith Coupling (2026-07-31) -- KB: P024 / D029 / S029

This was your first pure codebase-comprehension audit -- no incident, no design brief, just "read
six real solution areas and tell me the current pattern, the risks, and the next actions," with an
explicit constraint to derive everything from source and ignore in-repo documentation. Here is what
to study:

**1. "A Seam Exists" Is Not the Same As "The Seam Is Used"**
P018 (2026-07-09) found that Order.API project-referenced Master/Portal despite separate deploy
pipelines. P024 went further: it found that a real gRPC contract already exists for many of the same
domains (28 service/client pairs under Order alone) -- and application code still bypasses it via a
direct Infrastructure reference (e.g. Order.Core calling Portal.Infrastructure.Interfaces.TMS.
ITmsPostponeService directly instead of through the sibling gRPC client). This is a more advanced
diagnosis than "no decoupling mechanism exists" -- it is "the decoupling mechanism exists and is
being ignored," which points to a process/enforcement gap, not a design gap.
- Study: Sam Newman, "Monolith to Microservices" ch. 3 (identifying seams you already have vs. seams
  you need to create)
- Practice: In a codebase you own, find one place where a network-capable contract (gRPC/HTTP client)
  already exists for a dependency that is also reachable in-process. Which one does the actual code
  use, and why?

**2. Architecture Fitness Functions -- Turning a Convention Into a Build Failure**
D029/S029 is your first KB decision whose primary artifact is not a design pattern but an executable
CI test (NetArchTest) that fails the build when a service's Core project depends on another
service's Infrastructure/Integration assembly. The shrink-only allow-list technique -- seed the test
with today's known violations so it does not break the build on adoption, then only ever remove
entries, never add them -- is the standard way to introduce a fitness function into a codebase that
already has debt, without requiring a stop-the-world cleanup first.
- Study: "Building Evolutionary Architectures" by Ford, Parsons, Kua (the fitness function concept
  end to end); NetArchTest / ArchUnitNET documentation
- Practice: Write one fitness function for a codebase you own that encodes a rule you currently only
  enforce via code review (e.g. "the domain layer must not reference the web framework").

**3. Ownership of a Contract, Not Just Its Existence, Determines Real Decoupling**
The `ITmsPostponeService` interface in this audit already looked like a port -- Order.Core depends on
an interface, not a concrete class. But the interface is defined inside Portal's own assembly, so
Order still has a hard compile-time dependency on Portal, and Order's Docker image bundles Portal's
third-party integration code. The fix in D029 is not "add an interface" (one already existed) -- it
is "move the interface to be owned by the consumer, and call the existing gRPC seam from an adapter
behind it." This is a subtler version of the Hexagonal Ports & Adapters lesson from D022/D024: the
port must live on the side of the caller, in the caller's own bounded context.
- Study: Alistair Cockburn's original Hexagonal Architecture writeup (ports are defined by the
  application core that needs them, not by the thing being called)
- Practice: Find one interface in a codebase you own that is defined in the "wrong" project (owned
  by the callee rather than the caller). What would it take to move it?

**4. Auditing With a Hard Constraint (No Docs) Forces Evidence-Based Claims**
Being told explicitly not to read documentation and to derive every claim from `.csproj` files,
`using` statements, `appsettings.json`, and migration history is a useful discipline: it prevents an
audit from silently reproducing what a README claims rather than what the code does. Two genuinely
new, code-only findings came out of this constraint: `AddHealthChecks()` is now present on all five
services (a real fix since P018, not documented anywhere), and PII-encryption migrations were
added and revised five times in three days (visible only in migration file timestamps, not in any
document) -- a concrete signal of reactive, discovery-driven schema design under AI-assisted
iteration.
- Practice: Next time you inherit a codebase, spend the first hour deriving its architecture from
  `.csproj`/dependency-graph evidence alone before reading any of its documentation, then compare
  the two -- the gap between them is often the most important finding.

### Consultation: OMS Architecture Audit After AI Vibe-Coding (2026-07-31) -- KB: P025 / D030 / S030

This was your third audit pass on the same Sprint-OMS codebase, and the first where the pipeline
paired Hexagonal Architecture with a lens that had never appeared in your KB before: Service Mesh.
It is also the first time a decision explicitly blended two lenses *by architectural layer* rather
than by choosing one as primary and the other as an optional companion. Here is what to study:

**1. Recognizing When Two Lenses Attack Different Layers of the Same Problem**
D024 (P019) taught you to blend lenses when they address different *failure modes* of one problem
(Hexagonal fixed the persistence bug; Event-Driven closed the data-loss gap). D030 is a sharper
version of the same skill: Hexagonal and Service Mesh here address the *same audit* but at
genuinely different architectural layers -- application/compile-time (coupling, secrets) vs.
transport/infrastructure (TLS, retries, observability). Neither lens could have produced the
other's finding. Learning to ask "which layer does this lens actually operate at?" before treating
two options as mutually exclusive is a distinctly Intermediate-level skill -- it is the difference
between "pick the best option" and "map each option onto the part of the system it actually
governs."
- Study: Gregor Hohpe's "The Software Architect Elevator" (ch. on cross-cutting concerns living at
  different altitudes of a system); Istio/Linkerd architecture docs' own "what a service mesh does
  and does not do" sections
- Practice: Next time two lenses are proposed, before reading the decision, sketch which layer
  each one modifies (code, deployment topology, network, data) and predict whether they compete or
  compose.

**2. Hexagonal Ports as a Secrets-Rotation Seam, Not Just a Service Boundary**
Every prior Hexagonal encounter in your KB (D021, D022, D024, D029) used ports to decouple a
service from another service or external system. D030 applies the identical mechanism to a
different problem: `ISecretProvider` is a port whose adapters can be swapped from
"environment-variable-with-legacy-fallback" today to "Key Vault" tomorrow, without touching a
single call site. The generalizable insight: Hexagonal Architecture is not really about
"services" -- it is about isolating *any* volatile external dependency (a downstream service, a
secrets store, a certificate authority) behind an interface owned by the code that needs it.
- Study: Alistair Cockburn's original Hexagonal write-up again, specifically the framing of "ports
  for anything the application core depends on that can change independently of it"
- Practice: List three things in a codebase you own that are not "services" but are still volatile
  external dependencies (feature flag providers, clocks, ID generators, secrets). Which ones
  already have a port? Which ones are called directly?

**3. Service Mesh's Real Scope Boundary**
This is your first hands-on evaluation of Service Mesh as a lens, and the decision is explicit
about what it cannot do: it has zero effect on compile-time coupling (Order.Core still directly
referencing Portal.Infrastructure) and zero effect on plaintext secrets in appsettings.json --
both are application-layer problems that a sidecar proxy never touches. What it is genuinely good
for -- mTLS, proxy-level retry/circuit-breaking, centralized observability -- was still captured,
just routed to an explicit Phase-2 track instead of silently dropped. Internalize this scope
boundary now, before you are the one proposing Service Mesh in a design review and someone asks
"does this fix our secrets problem?"
- Study: Istio documentation's own architecture overview (sidecar vs. control plane
  responsibilities); William Morgan's (Linkerd) writing on what problems a mesh does and does not
  solve
- Practice: For a system you know that uses (or is considering) a service mesh, list every
  finding from your last security or coupling review and mark each one "mesh can fix this" or
  "mesh cannot touch this."

**4. Reading Evidence: "Referenced" vs. "Actually Configured"**
The audit's resilience finding was not "Polly is missing" -- it was "Polly is referenced in the
`.csproj` but no resilience policy is ever wired up in DI." This is the same class of discipline
you first exercised in D024 (checking *which* error codes a retry policy actually classifies as
transient, not just whether retry logic exists), now applied one level higher: whether a
dependency's mere presence in the dependency graph implies it is doing anything at all. This
distinction — dependency present vs. dependency active — is a habit worth carrying into every
future audit.
- Practice: Pick a NuGet/npm package in a codebase you own that "should" be providing some
  cross-cutting behavior (retries, caching, logging). Trace whether it is actually wired into the
  request path, or just sitting in the dependency graph unused.

---

### Consultation: PTL Warehouse Integration -- Manual File Exchange to API-Driven Orchestration (2026-08-10) -- KB: P026 / D031 / S031

This is your first consultation entirely outside the Sprint-OMS/ETL lineage that has
produced every prior KB entry. The domain (a physical warehouse Put-to-Light process
integrating WMS, SAP, a hardware controller, and a Marketplace) shares no vocabulary
with OMS -- which makes it the clearest test yet of whether the patterns you have been
building are genuinely internalized or just familiar within one codebase. Here is what
to study:

**1. The Saga-vs-Choreography Threshold Generalizes Beyond "Service Count"**
D019 taught you a simple rule: 2 services -> outbox+ACL is enough; 3+ services or
required automated rollback -> Saga. P026/D031 could not use that rule directly (it
is not primarily about *how many* systems are involved) -- the real driver was *who is
allowed to enforce a cross-cutting invariant*. "1 order = 1 box = 1 invoice" and "only
1 active box per PLT slot" cannot be enforced by any single event listener because no
single event carries enough context; only a component that sees the whole sequence
(the saga) can. Practice restating the D019 rule as: "introduce an orchestrator
wherever an invariant spans more state than any one participant's local event stream
contains" -- and check that this reformulation still explains the D019 decision too.
- Study: Chris Richardson, "Microservices Patterns" ch. 4 (Saga) -- specifically the
  distinction between per-step local transactions and cross-step invariants
- Practice: For a workflow you own, list every invariant that spans more than one
  service's local state. For each, ask whether an event consumer could actually check
  it, or whether it needs a process manager.

**2. Orchestration and Choreography Are Not Mutually Exclusive**
The chosen design in D031 is not "Saga instead of events" -- it is a saga whose
messaging *is* an event bus (event-carried state transfer). The orchestrator owns
decisions (state transitions, compensations); events are just the transport it uses to
talk to WMS/SAP/PTL/Marketplace. This resolves the false binary that D019's framing
("choreography vs orchestration") can accidentally imply. Learn to ask two separate
questions about any integration problem: "who decides?" (orchestration question) and
"how do systems hear about it?" (transport question) -- they can have different
answers.
- Study: Bernd Rücker's writing on "orchestration vs choreography is a false dichotomy"
  (camunda.com blog); Enterprise Integration Patterns' "Event-Carried State Transfer"
- Practice: Take a choreographed flow you have built. Identify one invariant it
  silently cannot enforce today. Would adding a thin orchestrator on top (still using
  the same events) fix it without a full rewrite?

**3. Synchronous Rejection Needs a Synchronous Owner**
The "reject mixed-store cartons, don't silently allow" requirement is a concrete,
memorable example of a constraint that rules out pure choreography: the PTL hardware
controller needs an answer *now*, not an eventually-consistent correction after the
fact. Any time a requirement includes the word "return an error" (as opposed to "flag
for review" or "alert"), check whether your design can actually produce that error
synchronously, in the same call, or whether you have accidentally designed an
async-only reaction to something that needed a gate.
- Practice: Audit one exception-handling requirement in a system you own. Is the
  current implementation a synchronous gate or an asynchronous reaction? Does the
  original requirement's wording ("reject", "prevent", "block") match which one you
  built?

**4. Applying Learned Patterns to an Unfamiliar Domain Is Itself a Skill**
Every OMS-lineage consultation let you lean on accumulated domain context (order
aggregates, BU multi-tenancy, gRPC seams already mapped in prior audits). This
consultation had none of that -- the problem came from an 8-slide PowerPoint extract
about physical box-picking and light-directed warehouse hardware. Notice that the
lens-selection and decision-synthesis reasoning (invariant mapping, layer-blending)
transferred cleanly anyway. This is the concrete evidence the Phase Progression
Criteria below are asking for: pattern fluency that survives a change of domain, not
just repetition within one.
- Practice: Next time you are handed an unfamiliar domain, deliberately list the hard
  invariants first (before picking a lens) -- as this consultation's problem-analyst
  step did with the 7 explicit business rules from the spec -- and see whether the
  lens choice becomes obvious once the invariants are named.


### Consultation: RFID Gate Transfer Verification (2026-08-10) -- KB: P027 / D032 / S032

This is your first RFID-domain consultation, and the first KB entry for an
already-built platform (the RFID Event Platform) that had never itself received
a formal architecture consultation -- you were extending a live system from its
own documented design principles, not designing greenfield or auditing an
unknown codebase. Here is what to study:

**1. An Aggregate Can Exist Purely to Enforce One Invariant**
Every prior DDD aggregate in your KB (Order in D018/D019) modeled a rich
business lifecycle with many states and behaviors. GateSession is narrower and,
in some ways, more instructive: its entire reason to exist is to make one
sentence -- "every EPC read must be evaluated before the session can close" --
impossible to violate in code. `Close()` throws if any EPC lacks a verdict.
This is DDD used as a correctness mechanism, not a modeling exercise. Learn to
recognize when a requirement is really a hard invariant in disguise ("must
never lose a tag") and ask whether an aggregate boundary, not just a validation
check, is the right place to enforce it.
- Study: Vaughn Vernon, "Implementing DDD" ch. 10 (aggregate invariants and
  transactional boundaries); Eric Evans' original framing of aggregates as
  consistency boundaries, not just object clusters
- Practice: Find one "must never X" requirement in a system you own that is
  currently enforced by convention or code review. Could an aggregate method
  make violating it a compile-time or runtime impossibility instead?

**2. Pre-Positioning: Using Events to Arrive Before the Physical Event, Not After**
Every prior use of events in your KB (Outbox in D018, DLQ in D024, saga
transport in D031) modeled events as *reactions* to something that already
happened. D032 uses events differently: `manifest.created` is published when a
movement round is *planned*, deliberately ahead of the goods physically
leaving, so the destination edge already has what it needs by the time the
gate event occurs. This is the same idea as CDN cache warming or DNS
pre-fetching, applied to a physical logistics event instead of a web request.
- Study: Enterprise Integration Patterns' "Event-Carried State Transfer" (again,
  but now contrast the *timing* -- pre-positioned vs reactive); any offline-first
  mobile sync design that pre-fetches data before a predictable state change
- Practice: In a system you own, find one place where a consumer currently
  fetches data synchronously right before it is needed. Could the producer
  instead push that data earlier, based on a predictable upstream signal?

**3. Fail-Safe Policy as Data, Not Behavior**
The requirement "decide fail-open or fail-closed when you can't verify" could
have been implemented as an if/else buried in the evaluation method. Instead,
D032 resolves `FailSafeMode` once, at session open, and stamps it on every
verdict as an explicit field. The result: a fail-open pass is never
indistinguishable from a verified pass in the data -- you can query "how many
passes this month ran unverifiable" without re-deriving it from logs. This is
the same discipline as D030's `ISecretProvider` seam: make an operational
policy an explicit, inspectable value, not an implicit code path.
- Study: "Making Illegal States Unrepresentable" (a recurring functional-design
  idea -- if a state is possible, model it explicitly rather than inferring it)
- Practice: Find one place in a system you own where an exception-handling
  policy (retry vs fail, allow vs block) is implicit in control flow. Could you
  hoist it into an explicit, loggable field instead?

**4. Extending a Live Platform From Its Own Stated Design Principles**
Unlike a greenfield design or a from-scratch audit, this consultation's
"Clarified Scope" section had already answered several architecture questions
(manifest-based, not registry-based; must reuse existing patterns) before the
pipeline ran. The discipline here was recognizing which parts were genuinely
open (how to model the invariant, how to distribute the manifest) versus which
parts were already decided and should not be re-argued. Practice separating
"constraints I must design within" from "constraints I get to choose" as the
very first step of any consultation on an existing system.
- Practice: Next time you inherit a spec with a "decided" section, write down
  explicitly which of your own instincts it overrides, and why you are
  deferring to it rather than proposing an alternative.

---

### Consultation: RFID Ingestion Service WAN Transport Protocol Selection (2026-08-10) -- KB: P028 / D033 / S033

This is your second RFID-domain consultation, and the first where the deciding question
was not "who owns an invariant" but "which side of a WAN boundary should carry which
kind of state." Here is what to study:

**1. A Lens Can Be Evaluated in Depth and Still Lose Outright**
Every prior blended decision in your KB (D024, D030, D031, D032) folded the
non-primary lens's insight into the winner. D033 is your first case where the
non-primary lens's *core proposed mechanism* (a persistent broker session over the
WAN) was evaluated seriously and then explicitly rejected as unfit for two hard
constraints (statelessness, firewall traversal) -- only its narrower reliability
insight (at-least-once, idempotent event_id) survived, repositioned as an *internal*
concern the winning option never had to touch. Learn to separate "this lens's overall
proposed mechanism" from "this lens's underlying values" -- you can keep the values
while rejecting the mechanism.
- Study: Mark Richards & Neal Ford, "Fundamentals of Software Architecture" ch. 17
  (architecture decision records) -- specifically how to document a rejected option's
  *partial* validity, not just a binary win/lose
- Practice: Next time two lenses are proposed, before reading the decision, ask
  separately: "what does each lens want to be true?" and "what mechanism does each
  lens propose to make it true?" -- then check whether you can accept the first while
  rejecting the second.

**2. Protocol Choice Is an Architecture Decision, Not an Implementation Detail**
It would have been easy to treat "REST vs MQTT vs gRPC" as a downstream engineering
choice made after "the architecture" was decided. D033 treats it as the architecture
decision itself, because the constraint set (statelessness at fleet scale, firewall
traversal across thousands of retail sites) is exactly the kind of non-functional
requirement that architecture, not implementation, is responsible for satisfying.
Whenever a "just pick a protocol" question surfaces, check first whether it is
actually gating a hard operational constraint -- if so, it deserves the same lens-pair
rigor as any other architectural decision.
- Study: Michael Nygard, "Release It!" ch. 5 (stability patterns) for how connection
  and session models interact with fleet-scale failure modes
- Practice: For an integration you own, list every persistent connection/session your
  system holds against external parties. For each, ask: does this scale linearly with
  client count, and is that a state footprint anyone has actually sized?

**3. Firewall/Proxy Reality Is a First-Class Architectural Constraint**
The decisive evidence against the broker-based option was not a performance number --
it was an operational fact about retail store networks (broker ports are commonly
blocked or require special allowances; HTTPS/443 is not). This is a category of
constraint you have not weighted heavily before: not CAP-theorem tradeoffs, not team
size, but literal network-policy reality at the physical sites your system must run
in. Senior architects treat "will this actually get through the customer's firewall"
as seriously as any theoretical scalability argument.
- Practice: For a system you own that talks to external or field-deployed clients,
  find out (don't assume) which outbound ports/protocols those networks actually
  allow by default. Would your current design survive if a new site's firewall
  policy were maximally restrictive?

**4. Reusing a Platform's Own Self-Description as the Architecture**
The RFID platform's own spec already called the Ingestion Service "the only
edge-facing surface" -- language that is, in effect, already describing a Hexagonal
port, even though no one had framed it that way. D033's real work was recognizing
that an existing informal description already implied a formal pattern, then making
that implicit boundary protocol-concrete. This is a different skill from applying a
pattern to greenfield code: it is *noticing* that a system's own vocabulary has
already committed to a pattern before you arrive.
- Practice: Re-read a design doc or README for a system you did not build. Find one
  sentence that is already describing a named architectural pattern in plain
  language, without using the pattern's name.

---

## Phase Progression Criteria

- **Foundation → Intermediate:** Can explain 5+ patterns with their tradeoffs; has encountered problems across 3+ distinct domains; no longer needs to look up what a pattern does before engaging with it — **met as of consultation 8 (2026-07-31, P025/D030): 11+ distinct patterns encountered (DDD, CQRS, Outbox, Saga, Strangler Fig, Modular Monolith, Hexagonal, Event-Driven, Layered, Fitness Functions, Service Mesh) across greenfield design, ETL, incident response, database maintenance, and repeat codebase audits**
- **Intermediate → Advanced:** Anticipates which lenses will be selected before seeing them; spots org-level and team-topology constraints in problems; can run a design review solo — **not yet met; explicit next-step focus is anticipating the layer each lens governs (see Current Focus) before the pipeline output confirms it**
