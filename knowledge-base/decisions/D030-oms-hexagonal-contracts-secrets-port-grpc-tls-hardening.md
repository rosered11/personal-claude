---
id: D030
chosen_option: "Extract Per-Service Contracts/Adapter Assemblies with an ISecretProvider Port (Hexagonal), with Interim gRPC Cert-Validation Hardening Folded In and Service-Mesh mTLS/Resilience Routed as a Parallel Phase-2 Track"
problem_id: P025
tags: [architecture-audit, distributed-monolith, microservices, grpc, secrets-management, dotnet, vibe-coding, technical-debt, hexagonal-architecture, service-mesh]
related_snippets: [S030]
---

# Decision: Hexagonal Contracts Extraction + ISecretProvider Port, with Interim gRPC TLS Hardening and Service Mesh as Phase 2

## Context

P025's audit reconfirmed the compile-time Infrastructure-assembly coupling already found in
P018/D023 and P024/D029 (Front.API -> Order.Infrastructure, Order.API/Portal.Integration ->
Master.Infrastructure), and additionally surfaced two security-relevant findings neither prior
audit covered in depth: plaintext secrets committed across multiple services' appsettings*.json
(confirmed MySQL/Redis in Portal.API, PostgreSQL in Order.API/appsettings.AzureDevelop.json), and
~30 gRPC clients disabling TLS certificate validation via
`DangerousAcceptAnyServerCertificateValidator`. Any remediation must satisfy three hard
constraints: no renaming/moving of gRPC contracts already relied upon by multiple consumers, no
disruption to the live OMS, and committed secrets must be rotated (not merely deleted from
source), which requires an abstraction seam for supplying secrets, not just a one-time cleanup.

## Options Considered

Lens A -- Hexagonal Architecture: Split each service's monolithic `*.Infrastructure` project into
a `*.Contracts` assembly (ports -- interfaces/DTOs, no EF/Npgsql references) and a private adapter
assembly, and introduce an `ISecretProvider` port with swappable adapters (environment variables /
Key Vault today, legacy-appsettings fallback during migration). This removes the confirmed
cross-service assembly violations, gives secrets a rotation-ready seam, and is enforceable going
forward via a NetArchTest fitness function. Effort is medium; consumer `.csproj` reference swaps
still require coordinated releases, and Master/Front/Report have no test projects to safety-net
the change.

Lens B -- Service Mesh: Introduce sidecar proxies (e.g. Istio) across the gRPC fabric to get mTLS,
retry/timeout/circuit-breaking policy, and centralized observability with zero application code
changes, in PERMISSIVE mode for incremental rollout. This directly eliminates the disabled
certificate-validation problem at the transport layer and adds resilience Polly never actually
configures. However, it does nothing for compile-time coupling or plaintext secrets, and its
feasibility depends on infrastructure (TKE sidecar injection, mesh control plane) that lives
outside this codebase's visibility, in the external k8s-config-tke/product-k8s-config repos.

Both lenses independently confirmed real, distinct evidence: Lens A confirmed the coupling and
secrets findings; Lens B confirmed the disabled TLS validation and a second independent
plaintext-secret finding, plus the absence of any configured resilience policy.

## Decision

Hexagonal Architecture is chosen as the primary lens because it is the only option that satisfies
all three hard constraints -- in particular, secrets rotation requires an abstraction seam, which
only `ISecretProvider` provides; Service Mesh cannot rotate a secret or remove a compile-time
assembly reference. Service Mesh's most urgent, code-fixable finding (disabled certificate
validation) is not deferred to an uncertain future mesh rollout -- it is folded into the chosen
snippet as an interim fix, routed through the same `ISecretProvider` port (sourcing a pinned
internal CA thumbprint) rather than left disabled. Full mesh adoption is retained as an explicit
Phase-2 track rather than rejected outright, since it remains the right long-term home for mTLS
and proxy-level resilience once the team has TKE sidecar-injection confirmed and baseline test
coverage in Master/Front/Report.

Concrete next actions, in order:

0. Rotate the confirmed MySQL/Redis (Portal.API) and PostgreSQL
   (Order.API/appsettings.AzureDevelop.json) credentials immediately, independent of any refactor.
1. Introduce `ISecretProvider` port + adapter per service, with a logged legacy-config fallback so
   migration can proceed service-by-service with zero disruption.
2. Apply the interim gRPC certificate-validation fix: replace
   `DangerousAcceptAnyServerCertificateValidator` with real chain validation pinned to an internal
   CA thumbprint, sourced via `ISecretProvider`.
3. Split each service's `*.Infrastructure` into `*.Contracts` (ports, no EF/Npgsql references)
   plus a private adapter assembly, starting with `Master.Infrastructure` (highest fan-in --
   consumed by 3 services).
4. Add a NetArchTest fitness function to CI forbidding cross-service `*.Infrastructure`
   references (extends the same guardrail pattern already recommended in D029/S029).
5. Phase-2 spike: evaluate Istio/Linkerd PERMISSIVE-mode mTLS plus retry/circuit-breaker policy,
   contingent on confirming TKE sidecar-injection externally and on Master/Front/Report first
   gaining baseline test coverage.

## Consequences

Accepted trade-offs:
- The `*.Contracts`/adapter split is non-mechanical effort -- someone must triage which types are
  true external ports versus internal repo abstractions; Master/Front/Report's lack of test
  coverage means this triage has no automated safety net during the transition.
- Consumer `.csproj` `ProjectReference` swaps (pointing at `*.Contracts` instead of
  `*.Infrastructure`) still require coordinated, per-service releases; this is not a single
  atomic change.
- The secrets fix is only as strong as consistent adoption across all five services -- a partial
  rollout leaves some services still reading plaintext appsettings directly.
- Deferring full Service Mesh to Phase 2 means retries/timeouts/circuit-breaking at the proxy
  layer, and centralized mTLS across the whole gRPC fabric, remain unaddressed in the near term
  beyond the interim certificate-validation fix.

Benefits:
- Satisfies all three hard constraints simultaneously: no gRPC contract renames, no disruption
  (incremental per-service rollout with legacy fallback), and secrets are rotatable through a real
  abstraction rather than just removed from git history.
- Closes the most urgent security exposure (disabled TLS certificate validation on ~30 gRPC
  clients) immediately, without waiting on external mesh infrastructure.
- Converts the previously informal boundary convention into an enforceable NetArchTest fitness
  function, consistent with and extending the guardrail already adopted in D029.
- Keeps Service Mesh's genuine value (proxy-level resilience, centralized observability,
  mTLS) on an explicit, prioritized roadmap rather than discarding it.

Risks / follow-ups:
- If Phase 2 (Service Mesh) is never funded, retries/timeouts/circuit-breaking remain
  unconfigured at the application layer (Polly present only transitively today) -- recommend
  tracking this as a named backlog item, not an implicit "later."
- The interim certificate-pinning fix introduces an internal CA thumbprint as a new secret that
  itself must be rotatable via `ISecretProvider`, not hardcoded -- must not repeat the original
  mistake while fixing it.
- This decision does not address whether Master.Infrastructure's fan-in reflects a coherent
  bounded context; that remains an open question for a future audit, as in D029.
