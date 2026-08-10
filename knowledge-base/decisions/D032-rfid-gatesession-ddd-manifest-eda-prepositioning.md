---
id: D032
chosen_option: "GateSession Domain Aggregate (DDD) Enforcing Zero-Loss/Fail-Safe Manifest Evaluation, Fed by Event-Pre-Positioned Manifest Cache (EDA transport)"
problem_id: P027
tags: [rfid, edge-computing, gate-verification, manifest-sync, domain-driven-design, event-driven-architecture, offline-first, fail-safe, warehouse-management]
related_snippets: [S032]
---

# Decision: GateSession Domain Aggregate Enforcing Zero-Loss/Fail-Safe Manifest Evaluation, Fed by Event-Pre-Positioned Manifest Cache

## Context

P027 needs a gate-level check that flags any RFID tag not on a movement
round's expected list, for both intra-site and inter-site transfer, with zero
delay and zero loss, without any synchronous call to the central
Serialization Service. The KB currently has no prior RFID Event Platform
entry -- kb-search against the existing 26 problems returned a top overlap of
~0.06 (a single generic shared tag, `warehouse-management`, against P026),
which is not a meaningful precedent; this decision establishes the first
formal RFID Event Platform precedent in the KB rather than extending or
contradicting anything prior.

## Options Considered

**Lens A -- Domain-Driven Design**: Model the movement round and its gate
evaluation as explicit domain concepts inside the Event Processor -- a
`MovementManifest` (expected EPC list, scoped to one round, intra- or
inter-site) and a `GateSession` aggregate (one instance per physical
pass-through) that owns the invariants directly: every EPC read must be
recorded and evaluated before the session can close (zero-loss), evaluation
happens synchronously in-process against the locally cached manifest
(zero-delay), and a fail-open/fail-closed policy is an explicit, auditable
field on the session rather than implicit behavior. This extends the
Serialization Service's existing `encoded -> in_stock -> ... -> sold` status
state machine philosophy into the transfer domain.

**Lens B -- Event-Driven Architecture**: Treat the whole problem as event
propagation with no new stateful domain object: publish a `manifest.created`/
`manifest.updated` event (partitioned by destination `site_id`) as soon as a
movement round's expected-list is known, let the destination edge's existing
config-pull/cache mechanism (same pattern as Site & Config Service's
heartbeat-based push) absorb it locally ahead of physical arrival, and let the
gate itself emit a `gate.transfer.evaluated` event per session for downstream
reconciliation (reusing the adapter reconciliation-job pattern already used
platform-wide to catch silent drift).

Both architects agreed these genuinely contrast: Lens A centralizes
correctness/invariant enforcement in an explicit, testable domain object;
Lens B decentralizes into pure pub-sub replication with no new stateful
service, leaning entirely on the platform's existing bus/cache idioms.

## Decision

Adopt **Lens A (DDD)** as the primary structure for *who enforces the
invariants*, with Lens B's event propagation folded in as the *transport*
that gets the manifest to the right edge in time -- not rejected.

A **`GateSession` aggregate**, opened per physical gate pass-through, is the
single place that:
- Enforces **zero-loss**: `RecordRead(epc)` adds every uniquely-seen EPC to
  the session; `Close()` is guarded and throws unless every recorded EPC has
  a corresponding verdict -- no EPC can be silently dropped by dedupe/
  debounce logic, because dedupe only prevents *duplicate* reads of the same
  EPC within a session, never removes an EPC from the evaluation set.
- Enforces **zero-delay**: evaluation against the manifest happens
  synchronously in-process, in the same method call that records the read --
  no network call, no queue hop, on the critical path of the gate decision
  (mirrors the existing outbound pick-verify precedent).
- Encodes **fail-safe policy explicitly**: if no local `MovementManifest` is
  cached for the session's movement round (inter-site + WAN down longer than
  transit time, or manifest never arrived), the session runs in a named
  `FailSafeMode` (`FailOpen` or `FailClosed`, configurable per site/movement
  type) instead of guessing -- every session records which mode it ran under,
  so the audit trail always shows whether a "pass" was a verified match or an
  unverifiable fail-open pass.
- Extends the Serialization Service philosophy of an explicit status state
  machine, rather than introducing an unrelated ad hoc mechanism: a
  `MovementManifest` moves through `Created -> Distributed -> Active ->
  Consumed/Expired`, mirroring `encoded -> in_stock -> ... -> sold`.

The **manifest reaches the correct edge** exactly as Lens B proposed --
via the existing canonical event topics, partitioned by destination
`site_id`, at-least-once with idempotent `event_id` like every other platform
event:
- Intra-site: `manifest.created` is created and consumed at the same site --
  no distribution problem, the local `IManifestCache` just reads it directly.
- Inter-site: `manifest.created` is published as soon as the movement round
  is planned (before goods physically leave), the destination edge's
  `ManifestSyncConsumer` subscribes and upserts it into the same local
  `IManifestCache` ahead of arrival -- pre-positioning, the identical pattern
  already used for serial-range pre-allocation and Site & Config's
  heartbeat-pushed edge config.
- `gate.transfer.evaluated` (one event per closed `GateSession`, carrying the
  full per-EPC verdict list and the `FailSafeMode` used) is published for
  central visibility and reused by the existing adapter-style reconciliation
  job pattern, closing the audit gap that a fail-open pass would otherwise
  leave invisible until someone asks.

## Consequences

**Accepted trade-offs**:
- A new domain aggregate (`GateSession`) and read-model (`MovementManifest`
  via `IManifestCache`) must be designed, versioned, and maintained inside
  the Event Processor -- the platform's already-busiest service ("the
  business brain") gains one more responsibility area, though it is a
  natural sibling to ASN-matching and pick-verify session logic already
  living there.
- Inter-site fail-safe correctness now depends on the manifest event reliably
  arriving *before* the physical transfer completes; if planning-to-departure
  lead time is ever shorter than realistic WAN-recovery time, `FailSafeMode`
  will trigger more often than desired -- this is an operational tuning
  problem (how early manifests are published), not an architecture gap, and
  should be monitored via the `gate.transfer.evaluated` reconciliation event.
- Two new event types (`manifest.created`/`manifest.updated`,
  `gate.transfer.evaluated`) join the existing topic set and need the same
  schema-versioning discipline as `inbound.received`, `pick.verified`, etc.

**Benefits**:
- Zero-loss and fail-safe-mode are compiler-and-test-enforceable invariants
  on a single aggregate, not a convention every edge implementation has to
  independently get right -- directly answering the "never lose a tag"
  constraint with code, not policy documentation.
- Zero-delay is trivially satisfied because evaluation never leaves the local
  process -- consistent with, and reusing, the exact "gate matching against
  locally cached ASN/pick data" and "paid-EPC cache <100ms" precedents already
  proven in production for inbound/outbound/EAS.
- The distribution half reuses 100% existing platform mechanics (canonical
  topics partitioned by `site_id`, at-least-once + idempotent `event_id`,
  heartbeat-style edge config pull) -- no new infrastructure, consistent with
  the Clarified Scope's explicit "reuse existing patterns" requirement.
- Central visibility (via `gate.transfer.evaluated` + reconciliation) means a
  fail-open pass is never a silent gap -- it becomes a queryable, auditable
  event just like every other platform event, closing the one real audit risk
  a pure edge-only design would otherwise leave open.

**Confidence**: high. The Clarified Scope section pre-decided the hard
constraints (manifest-based, not global registry; must support both
movement types; must reuse the existing pattern), which maps cleanly onto
"domain aggregate owns invariants, event bus owns distribution" -- the same
blend-by-concern skill already demonstrated in D031 (Saga owns invariants,
events are transport) and D030 (Hexagonal owns application-layer coupling,
Service Mesh owns transport-layer TLS), now applied a third time in a third
distinct domain.

**Design review follow-up (2026-08-10, pre-implementation)**: this decision
stands, but see `P027` § Open Items for five refinements identified before
pilot -- most notably that the coded "zero-loss" invariant only covers
software-side completeness (every *captured* EPC gets a verdict), not
physical RF miss-reads, and that no mechanism yet correlates a physical gate
pass to the correct `movementRoundId`/manifest. Both should be closed before
this moves from design to implementation.
