---
id: D023
chosen_option: "Strangler Fig Facade-First Migration Toward Network-Isolated Microservices"
problem_id: P018
tags: [oms, microservices, service-boundary-violation, api-gateway, bff, observability, read-model, dotnet, strangler-fig]
related_snippets: [S023]
---

# Decision: Strangler Fig Facade-First Migration Toward Network-Isolated Microservices

## Context

Order.API project-references Master and Portal in-process despite all three being deployed
separately on TKE. The team wants to layer a BFF, API gateway, OpenTelemetry observability, a
dashboard read-model, and broker-based fan-out on top as more business units onboard (P018). This
is a proposal under specialist review, not an approved implementation plan, and several open
questions (deploy topology ownership, real traffic/scale numbers, multi-tenancy isolation depth,
team ownership model) remain unanswered. The standing KB precedent D020 confirmed Modular Monolith
over Microservices for this same OMS lineage at 70K order-lines/day, conditioned on small team,
atomic-TX requirement, no broker, and sub-inflection-point volume. Those four conditions are not
yet re-confirmed true or false at the current proposal's scale, so any decision here must
explicitly reconcile with D020 rather than silently override or contradict it.

## Options Considered

Lens A -- Microservices: Replace the in-process project references with real HTTP/gRPC calls
behind the existing IHandler abstraction across all three services simultaneously; stand up a
YARP API Gateway plus channel-based BFFs; add per-service OpenTelemetry, health checks, and
metrics; relay the Outbox to a broker (Kafka/CKafka) for fan-out; build the dashboard read-model
and async report jobs. This is a faithful embrace of the review's target-state diagram and
recommendations A through H, executed as a single coordinated migration.

Lens B -- Strangler Fig: Ship the Gateway plus channel BFFs plus OpenTelemetry as a facade in
front of today's coupled Order.API immediately (no internal rewrite required), then strangle the
Order-to-Master/Portal project references one call-site at a time behind the existing IHandler
ports, toggled via per-seam feature flag with a legacy (in-process) and target (HTTP)
implementation coexisting temporarily. The Outbox-to-broker relay and read-model are introduced
only once the first seam is fully strangled and OpenTelemetry is rolled out platform-wide.

Both lenses agreed on the same target-state components (Gateway, channel BFF, OpenTelemetry,
broker-relayed Outbox, read-model, async Hangfire reports); they diverged only on whether to
commit to full network-isolated microservices now, in one coordinated change (Lens A), or to
sequence the same end-state incrementally behind a facade while deferring the highest-risk,
highest-blast-radius step (Lens B).

## Decision

Chosen: Strangler Fig facade-first sequencing, with the Microservices end-state adopted as the
long-term direction but not mandated as a phase-1 commitment.

Concretely:

1. Stand up the YARP Gateway plus channel-based BFFs (Web-Admin, Marketplace, Mobile) as a facade
   in front of the current system immediately. This alone resolves inconsistent tracing (only
   Portal.API has TraceIdMiddleware today), gives every service a place to add health checks and
   metrics, and gives BU onboarding a real entry point without touching Order/Master/Portal
   internals.
2. Roll out OpenTelemetry (traces, metrics, logs) across all services next, threading one W3C
   traceparent from Gateway through to the Outbox dispatch. This is foundational and must land
   before the boundary work, per the review's own phasing rationale, since retrofitting tracing
   later is harder.
3. Strangle the Order-to-Master/Portal project references one seam at a time: introduce
   IMasterServiceClient and IPortalServiceClient ports (reusing the existing IHandler convention),
   with a legacy in-process implementation and a target HTTP implementation behind a per-seam
   feature flag, each swap independently tested and revertible. Start with the Portal seam, since
   Portal.API is not even deployed yet, the lowest-risk seam to strangle first.
4. Only after the first seam is strangled and OTel is live: relay the Outbox to a broker for
   fan-out, and stand up the read-model consumer for the dashboard hot path. Reports move to async
   Hangfire jobs writing to object storage, decoupled from the transactional/hot-path DB, in
   parallel with step 2 since it has no dependency on the boundary fix.
5. Defer the binary choice between full microservices and re-consolidating into a disciplined
   Modular Monolith until the open questions in P018 are answered (real traffic/scale numbers,
   team ownership model, multi-tenancy depth). Re-run this analysis against D020's four rejection
   criteria (team size, atomic-TX need, broker availability, volume inflection point) once that
   data exists, rather than assuming either answer today.

## Consequences

Trade-offs accepted:

- The system remains cosmetically microservices but structurally a modular monolith for longer
  than Lens A would allow. Residual coupling risk (shared UnitOfWork leakage, tight-coupling
  regressions) persists until each seam is fully strangled, and Strangler Fig migrations are known
  to stall if not tracked to completion.
- Temporary dual-maintenance of both the legacy in-process path and the new HTTP path exists
  during each migration window, mitigated by feature-flagging and characterization tests per seam.
- This decision explicitly does not answer the target end-state topology question. It sequences
  the how, and defers the what (full microservices vs re-consolidated modular monolith) to a
  follow-up review once traffic and team data exists.

Benefits:

- Delivers the highest-visible-value layer (Gateway, BFF, consistent tracing, health checks)
  fastest, without waiting on the riskiest, highest-blast-radius step.
- Each project-reference-to-HTTP swap is small, independently testable, and revertible, rather
  than a single coordinated multi-service cutover with an unclear rollback path.
- Reconciles with D020 rather than silently contradicting it: no phase-1 commitment is made to
  full network-isolated microservices until D020's four rejection criteria are explicitly
  re-evaluated against current data.
- Reuses Lens A's concrete HTTP-adapter pattern (typed HttpClient plus Polly retry/circuit-breaker
  plus traceparent propagation) inside each strangled seam, so no architectural insight from the
  Microservices lens is lost, only its all-at-once sequencing is rejected.

Rejected options:

- Pure Microservices (Lens A, executed immediately and in full): rejected as the sole/immediate
  path because it forces a coordinated, highest-blast-radius rewrite across all three services
  before the open questions (traffic, team ownership, multi-tenancy) are answered, and risks
  committing to a topology that may contradict the still-unconfirmed D020 rejection criteria for
  this same OMS lineage.
- Do nothing / defer indefinitely: not proposed by either architect but implicitly rejected. The
  cost of inaction compounds with every additional BU onboarded through the coupled Order.API, so
  the review's own phasing plan is endorsed as directionally correct, just re-sequenced with
  Strangler Fig discipline and D020 as an explicit checkpoint before locking in an end-state.

Confidence: medium. The phased, incremental strategy is deliberately robust to the unresolved open
questions (that robustness is the primary reason it was chosen over committing to full
microservices now), but the eventual end-state decision (full microservices vs re-consolidated
Modular Monolith) still depends on data that does not yet exist in this proposal.
