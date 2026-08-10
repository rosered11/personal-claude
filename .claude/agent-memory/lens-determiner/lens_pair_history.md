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

- P027/D032 (RFID gate transfer verification -- unregistered-tag detection at gate for
  intra-site and inter-site warehouse movement, extending the existing RFID Event
  Platform): Domain-Driven Design vs Event-Driven Architecture -- DDD won as primary
  (GateSession aggregate owns the zero-loss invariant: Close() throws unless every
  recorded EPC has a verdict; also owns fail-safe-mode as an explicit auditable field),
  Event-Driven Architecture folded in as the manifest-distribution transport
  (manifest.created published ahead of physical transfer, partitioned by destination
  site_id, consumed into a local read-model before the gate event occurs --
  "pre-positioning", not just after-the-fact notification). Third KB entry entirely
  outside OMS/ETL, and distinct from the PTL lineage despite sharing the
  warehouse-management tag -- pair selection was driven by the problem's own zero-loss/
  zero-delay/fail-safe constraints and the already-decided Clarified Scope, not by any
  KB reuse-avoidance signal (kb-search overlap was ~0.06, not meaningful).

Rule for future RFID/edge-gate/tag-verification problems:
- Use Domain-Driven Design vs Event-Driven Architecture when the problem's hard
  constraints include (a) a "must never lose/skip an item" completeness invariant that
  needs one code-level place to enforce it, and (b) a distribution-timing problem (data
  must arrive at a remote edge before a physical event occurs, without a synchronous
  call). DDD owns the invariant (aggregate boundary + guarded state transition);
  Event-Driven Architecture owns getting the data to the right place in time
  (pre-positioning via existing canonical topics) -- same "decision vs transport" blend
  established for Saga vs Event-Driven (P026/D031) and Hexagonal vs Service Mesh
  (P025/D030), now a third recurring instance of the pattern.
- Do not assume PTL's Saga-vs-Event-Driven pairing applies to RFID/edge-gate problems
  just because both are warehouse-adjacent -- Saga is for orchestrating invariants that
  span *multiple external systems of record* (WMS/SAP/PTL/Marketplace); RFID gate
  problems so far are invariants owned *within a single platform's own service*
  (Event Processor), which is a DDD aggregate question, not a cross-system orchestration
  question.
