---
name: Lens Pair History -- Across Domains
description: Which lens pairs have been used for which problems (OMS sub-problems and, from P026/D031, non-OMS domains like warehouse/PTL integration), to avoid uninsightful repetition and guide future lens selection
type: project
---

OMS consultations so far and the lens pair used:
- P013/D018 (greenfield design): Domain-Driven Design vs CQRS -> blended (both needed)
- P014/D019 (aggregate extensions): Domain-Driven Design vs Saga Pattern -> DDD won, Saga's
  service-count threshold (3+) absorbed as a rule
- P015/D020 (full system architecture confirmation): Modular Monolith vs Microservices ->
  Modular Monolith confirmed
- P018/D023 (service-boundary-fix + BFF/gateway/observability layering proposal review):
  Microservices vs Strangler Fig -> Strangler Fig won as sequencing strategy; deliberately did NOT
  reuse Modular Monolith vs Microservices even though this is the same OMS lineage, because the
  KB overlap score was low (0.111, well under the 0.8 reuse-avoidance threshold) and the question
  here is migration sequencing, not internal boundary confirmation.
- P024/D029 (codebase audit -- distributed monolith coupling behind microservices deploy
  topology): decision synthesized to "Hexagonal ports at confirmed crossing points, strangled
  seam-by-seam (D023 lineage), enforced via NetArchTest CI fitness function" -- i.e. Hexagonal
  Architecture + Strangler Fig framing, S029 fitness-function snippet produced.
- 2026-07-31 re-audit (near-duplicate of P024, overlap_score 0.5 -- below 0.8 reuse threshold, so
  a new CREATE-mode record): chose Hexagonal Architecture vs Service Mesh instead of repeating
  Hexagonal vs Strangler Fig. Rationale: this re-run's problem JSON weighted secrets-management
  (plaintext credentials in appsettings across services) as heavily as the ports/adapters coupling
  issue, and neither Hexagonal Architecture nor Strangler Fig natively addresses secrets custody/
  rotation -- Service Mesh does (sidecar secret injection, mTLS identity). Diverged deliberately
  per KB-dedup rule to generate new institutional knowledge and cover the untouched secrets
  dimension.

Rules for future OMS problems:
- Use Modular Monolith vs Microservices for "is our internal module boundary/deployment topology
  correct" questions.
- Use Microservices vs Strangler Fig for "how do we safely evolve from a coupled-today state to a
  decoupled-target state" questions (pure sequencing, no major secrets angle).
- Use Hexagonal Architecture vs Strangler Fig when the audit is purely about ports/adapters
  entanglement plus incremental, non-disruptive migration, with no significant secrets/credentials
  finding.
- Use Hexagonal Architecture vs Service Mesh when the audit combines (a) ports/adapters coupling
  AND (b) a secrets-management / plaintext-credentials finding -- Service Mesh is the only lens in
  the pool that natively resolves secret injection/rotation and mTLS, so it out-competes Strangler
  Fig as the contrasting lens whenever secrets-management is a co-equal tag/finding, not just
  boundary coupling.
- Do not default to the same pair just because the domain tag (oms) or even the specific finding
  (distributed-monolith coupling) matches a prior KB entry with overlap_score < 0.8 -- check
  whether a new dominant tag (e.g. secrets-management) changes which lens pair best covers the
  full problem surface.

Non-OMS domains:
- P026/D031 (CMG Put-to-Light warehouse integration -- WMS/SAP/PTL-MHE/Marketplace,
  replacing manual Excel/file exchange with API-driven task generation/confirmation/
  SO-STO creation): Saga Pattern vs Event-Driven Architecture -- Saga won as primary
  (orchestrator owns cross-system invariants: 1 order=1box=1invoice, 1 active box per
  PLT slot, mixed-carton rejection, allocation-vs-stock mismatch), Event-Driven folded
  in as the saga's transport (event-carried state transfer for WMS/SAP/PTL/Marketplace
  notifications) rather than rejected. First KB entry entirely outside the OMS/ETL
  lineage -- no prior KB overlap existed to reuse or avoid, so pair selection was
  driven purely by the problem's own hard invariants (which require a component that
  sees the whole cross-system sequence) rather than any dedup rule.

Rule for future warehouse/PTL/hardware-integration problems:
- Use Saga Pattern vs Event-Driven Architecture (orchestration vs choreography) when
  the problem's hard constraints include invariants that span multiple external
  systems' local state (e.g. "exactly one of X across N systems", "reject Y
  synchronously") -- these cannot be enforced by any single event consumer alone.
  Fold Event-Driven's transport insight (async event bus / event-carried state
  transfer) into the Saga option as its I/O layer rather than treating the two lenses
  as fully mutually exclusive -- see D031 for the precedent write-up of this blend.
