---
id: D029
chosen_option: "Hexagonal Ports at Existing Cross-Service Seams + CI-Enforced Boundary Fitness Function"
problem_id: P024
tags: [oms, architecture-audit, hexagonal-architecture, strangler-fig, service-boundary-violation, modular-monolith, testability, dotnet, grpc]
related_snippets: [S029]
---

# Decision: Hexagonal Ports at Existing Cross-Service Seams + CI-Enforced Boundary Fitness Function

## Context

P024's source-code-only audit of Order, Portal, Master, Shared, Front, and Report confirmed that
Sprint-OMS is currently a distributed monolith: five independently deployed .NET services
following a consistent Layered/Onion + FastEndpoints-vertical-slice structure internally, with real
gRPC contracts already scaffolded for cross-service calls, but with application code in
Order.Core, Portal.Core, and Front.Core still holding direct compile-time dependencies into other
services' Infrastructure/Integration assemblies (confirmed by using-statement counts, not just
unused csproj references). Three of five services (Master, Front, Report) have zero automated
tests. The requesting engineer's explicit goal is to understand the system deeply for maintenance
and to prevent further "bad code" from slipping in during AI-assisted ("vibe coding") development --
meaning the answer must be as much about a durable guardrail as about a one-time fix.

## Options Considered

Lens A -- Hexagonal Architecture (Ports & Adapters): Give each service its own outbound port
interfaces for anything it needs from another service, with adapters implemented against the
already-existing gRPC clients (or, during transition, thin wrappers around the current
Integration-project calls). This makes every cross-service dependency an explicit, swappable
contract owned by the calling service, not a borrowed interface from the callee's assembly --
directly targeting both the coupling problem and the testability gap (ports are trivially mockable
in unit tests, which today require spinning up another service's Infrastructure/EF types).

Lens B -- Strangler Fig: Treat the coupling graph found in P024 as a migration backlog and
strangle it seam-by-seam: introduce a feature-flagged legacy-vs-target adapter at each of the
confirmed crossing points (Order to Portal.Infrastructure, Order to Master.Infrastructure, Front to
all three), cutting each over to the existing gRPC path on its own schedule, prioritized by
blast-radius (Front.Core's 26-file dependency on Order.Infrastructure first, being the newest and
least entrenched).

Both lenses agree on the target mechanism (route cross-service calls through the existing gRPC
seam, not a shared Infrastructure reference) and both explicitly build on, rather than contradict,
D020 (Modular Monolith confirmed for this lineage) and D023 (Strangler Fig facade-first sequencing
already adopted for OMS service-boundary work). They differ on emphasis: Lens A treats this as a
structural/ownership problem (who owns the interface, and is it unit-testable) to be fixed at each
call-site as it is touched; Lens B treats it as a phased migration program with an explicit
seam-by-seam rollout order and legacy/target feature flags.

## Decision

Hexagonal Architecture wins as the primary, immediately actionable lens, with Strangler Fig's
sequencing insight folded in as the required rollout discipline rather than treated as a rejected
alternative. Concretely:

1. Each service owns its own outbound port interfaces for anything it needs from another domain
   (e.g. Order defines IDeliveryPostponementGateway in Order.Infrastructure.Interfaces, instead
   of importing Portal's ITmsPostponeService). The adapter implementing that port lives in the
   consuming service's own Integration project and calls the target service's existing gRPC
   endpoint -- the network seam that already exists (28 gRPC pairs under Order, 6 under Master, 4
   under Portal) but is bypassed today.
2. This is executed strangler-fig style, not as a big-bang rewrite: start with the single
   highest-blast-radius seam (Front.Core's 26-file dependency into Order.Infrastructure, the
   newest and least load-bearing coupling), prove the port+gRPC-adapter pattern there, then work
   backward through Order-to-Portal, Order-to-Master, and Portal-to-Master in descending order of
   file count. This directly extends D023's already-adopted facade-first Strangler Fig sequencing
   into the "per-seam boundary strangling" phase that D023 explicitly deferred rather than
   completed.
3. A CI-enforced architecture fitness function (see S029) is added immediately -- before any seam
   is migrated -- so the coupling graph measured in P024 can no longer grow silently. This is the
   direct, concrete answer to the requesting engineer's stated goal of preventing "bad code" from a
   vibe-coding workflow: it does not rely on a developer (or an AI assistant) remembering the rule,
   it fails the build.
4. Test-backfill is sequenced together with each seam migration, not deferred: introducing a port
   at a seam is also the point at which that seam's logic becomes trivially unit-testable (mock the
   port instead of standing up another service's DbContext), which is the fastest lever available
   to close the zero-test gap in Master, Front, and Report without a dedicated "write tests" project.

## Consequences

Accepted trade-offs:
- This is deliberately scoped as a boundary-hardening and guardrail program, not a service-topology
  rewrite; the underlying decision of whether Order/Portal/Master/Front should ever become fully
  independent microservices (separate databases, no shared schema) remains explicitly out of scope
  and unchanged from D020/D023.
- Introducing a port per crossing point adds a small amount of interface/DI boilerplate at each
  seam; this is accepted because it is the same boilerplate cost already paid correctly for
  external-system integrations (WMS/TMS/CFW adapters) and is what makes the seam unit-testable.
- The CI fitness function will initially need an explicit allow-list of the pre-existing violations
  found in P024 (10+18 Order.Core imports, 26+10+2 Front.Core imports) so the build does not break
  immediately; the allow-list must shrink only, never grow, enforced by the same test.

Benefits:
- Converts an informal, easily-eroded convention (use gRPC for cross-service calls) into an
  enforced one, closing the exact gap that let the P018 finding recur and worsen by P024.
- Improves testability precisely where it is weakest today (Master, Front, Report) as a side effect
  of the same refactor, rather than as a separate initiative competing for priority.
- Directly operationalizes the still-open half of D023 (per-seam strangling) using concrete,
  file-level evidence from P024 instead of an abstract migration plan.
- Does not require any new infrastructure, broker, or deployment topology change -- works within
  the existing .NET/gRPC/EF Core stack and the existing D020 Modular Monolith precedent.

Risks / follow-ups:
- If the fitness-function allow-list is not actively driven down on a cadence, it will calcify into
  permanent debt exactly like the original convention did; recommend a visible metric (violation
  count over time) rather than a one-time pass/fail gate.
- Report.API's near-empty stub state (14 files, Ping/Diagnostics only, but already fully deployed
  with its own Dockerfile and Master.Integration reference) was flagged in P024 as scaffolding
  ahead of function; this decision does not resolve whether Report.API should continue to exist as
  a separate deployable -- that is a scope question for the requesting engineer, not an
  architecture-pattern question, and is called out explicitly as a next step rather than decided
  here.
- Secrets committed in plaintext across all appsettings.*.json files were also found in P024;
  this is a security-hygiene fix (move to a secrets manager / Kubernetes secrets), independent of
  the architecture decision above, and should not be deprioritized behind it.
