---
name: Lens-to-Tag Fit Patterns
description: Observed fit/misfit between architectural lenses and problem tag combinations across consultations, to speed up rationale-building
type: project
---

# Service Mesh lens

- Fits well when tags include `grpc` + `secrets-management` + `microservices`/`distributed-monolith` AND the affected system already deploys to Kubernetes (verify this — don't assume; check azure-pipelines/CI for AKS/EKS/TKE/GKE references or manifest repos before committing to mesh feasibility claims).
- Strongest, most concrete pros come from finding REPEATED per-client boilerplate for cross-cutting concerns (TLS validation bypass, missing retries/timeouts, missing mTLS) across many service-to-service call sites — mesh centralizes exactly that class of problem via sidecar interception, no app code changes.
- Always caveat explicitly: service mesh secures transport (mTLS, retries, timeouts, L7 observability) between workloads — it does NOT replace application-level end-user identity/authorization propagation (e.g. a custom gRPC interceptor forwarding a JWT/current-user claim). Keep this distinction sharp in rationale/cons so it doesn't read as overselling.
- Constraint tension to always flag: mesh sidecar rollout requires access to the Kubernetes manifest layer. If that layer lives in a separate repo/pipeline outside the audited codebase, `fits_constraints` should note that verification/coordination with platform/infra team is required, even if the option itself is otherwise sound.
- Incremental rollout via `PeerAuthentication` mode `PERMISSIVE` (Istio) or Linkerd's opt-in per-namespace annotation is the standard way to satisfy "must not disrupt the active operational OMS" constraints — always mention this rollout gate in the option itself, not just as an afterthought.

# Saga Pattern lens

- Fits strongly when tags/constraints include cross-system uniqueness or capacity
  invariants (e.g. `partial-fulfillment`, `task-orchestration`, "exactly one active X
  across N systems") that no single event consumer could enforce alone — first
  concretely confirmed winning outright (not just evaluated) in D031 (PTL Task Saga),
  contrasted against Event-Driven Architecture rather than against Outbox+ACL (the
  D019 contrast) because the problem involved 4 *external, independently-owned*
  systems, not services within one team's control.
- Do not treat Saga vs Event-Driven as fully mutually exclusive: the strongest Saga
  option folds an async event bus in as its transport (event-carried state transfer)
  rather than requiring synchronous RPC to every participant — this is what keeps the
  loose-coupling benefit of the rejected Event-Driven option without giving up
  centralized invariant enforcement.
- Concrete tell that Saga (not choreography) is required: any requirement phrased as
  "reject/return an error" (needs a synchronous gate) as opposed to "flag/alert"
  (tolerates an asynchronous reaction) — the former cannot be satisfied by a listener
  reacting to an already-published event.
