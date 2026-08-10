---
name: Recurring Architectural Tensions
description: Common trade-off pairs that appear repeatedly in this codebase — helps synthesizer identify which tension the decision is resolving
type: project
---

# Recurring Architectural Tensions

From KOS D# decisions and I# incidents. When synthesizing between two lens options, identify which tension applies — it clarifies why one option wins over the other.

---

## Simplicity vs Safety

**Applies to:** ETL transaction scope, Saga vs local TX

- Per-batch commit loop is simple (while + BeginTransactionAsync inside) and safe (TX hold bounded)
- Single-job TX is simpler to read but fails at scale
- Saga is complex (compensating transactions) but safe for multi-service flows

**Resolution rule:** When safety means preventing data loss or corruption, choose safety over simplicity even with higher implementation cost.

**KB precedent:** D003, D012

---

## Performance vs Correctness

**Applies to:** EF Core eager load vs lazy load, batch size

- Eager loading (Include chains) is correct (no N+1) but slightly more complex query
- Lazy loading is simple but incorrect at scale (N+1 is guaranteed)
- Large batch size is faster but risks OOM and TX timeout
- Small batch size is correct but may be too slow for volume

**Resolution rule:** When the performance choice introduces N+1 or OOM risk, correctness wins. Optimize the correct path, not the incorrect one.

**KB precedent:** D001, D005

---

## Memory vs Throughput

**Applies to:** Batch size selection, ChangeTracker management

- Large batch = high throughput, high memory, OOM risk
- Small batch = low memory, more TX overhead, lower throughput
- 10K batch = empirically validated balance for EF Core on this stack

**Resolution rule:** Choose the batch size that stays below OOM threshold while meeting throughput SLA. 10K is the validated default.

**KB precedent:** D005

---

## Flexibility vs Predictability

**Applies to:** Real-time connection strategy, rate limiting algorithm

- WebSocket: flexible (bidirectional, any latency), complex (stateful, sticky sessions)
- SSE: predictable (server-push only), simpler, proxy-friendly
- Token Bucket: flexible (burst-tolerant), less predictable under burst
- Sliding Window Counter: predictable (strict per-window), no burst tolerance

**Resolution rule:** If the use case requires bidirectional or true real-time, choose flexibility. If server-push with occasional staleness is acceptable, choose predictability.

**KB precedent:** D011, D013

---

## Coordination vs Independence

**Applies to:** ID generation, distributed transactions

- Snowflake: requires worker ID coordination (Redis INCR or config), but IDs are time-sortable
- UUID v4: fully independent (no coordination), but random (index fragmentation, not sortable)
- 2PC: strong coordination, blocking failure mode
- Saga: independent compensating transactions, eventual consistency

**Resolution rule:** When time-sortability or strict ordering matters, accept coordination cost. When randomness is a security property, choose independence.

**KB precedent:** D012, D014

---

## Structural Coupling vs Network-Layer Hardening (Hexagonal vs Service Mesh)

**Applies to:** Distributed-monolith / grpc-fabric audits where both a code-boundary lens (Hexagonal, DDD) and a network lens (Service Mesh) are assigned.

- Hexagonal-style options (split Contracts/ports from Infrastructure/adapters) fix compile-time coupling (cross-service ProjectReferences) and can model a secrets-rotation seam (ISecretProvider port) — near-zero diff for consumers, no new runtime infra.
- Service Mesh options (sidecar mTLS, retries, circuit breaking) fix network-layer gaps (disabled cert validation, missing resilience) with zero code changes and PERMISSIVE incremental rollout, but do NOT touch compile-time coupling or config-file secret hygiene, and often depend on infra (k8s mesh config) that lives outside the audited repo and can't be verified from source alone.
- These two are usually **complementary, not competing** — they attack different layers of the same tag cluster (distributed-monolith/grpc/secrets-management).

**Resolution rule:** When the problem includes a hard constraint that only the code-layer option can satisfy (e.g., "secrets must be rotated not deleted" needs a swappable provider abstraction; "cannot rename existing contracts" needs the Hexagonal split's near-zero-diff property), pick the code-layer (Hexagonal) option as the primary/chosen decision. Route the network-layer (Service Mesh) option into the KB as a parallel/phase-2 follow-on rather than a hard reject — cite specifically: (1) feasibility depends on infra outside the audited repo's visibility, (2) new control-plane operational burden is a poor match for teams with thin/zero test coverage found in the same audit. Still borrow the mesh analysis's most urgent code-fixable finding (e.g., disabled TLS cert validation) as an immediate interim code patch inside the chosen option's snippet — don't wait for the mesh rollout to fix an active vulnerability that's fixable in code today.

**KB precedent:** (first observed) Sprint-OMS audit, 2026-07-31

---

## Centralized Invariant Enforcement vs Decoupled Reactivity (Saga vs Event-Driven)

**Applies to:** Multi-system integration problems replacing manual/batch/file exchange
with real-time coordination (warehouse/WMS-SAP-hardware integration, but generalizes to
any "N independently-owned systems, M cross-cutting invariants" shape).

- Saga/orchestration centralizes decision-making: one component sees the whole
  multi-step sequence and can enforce invariants that span more state than any single
  participant's local event stream contains (uniqueness across systems, capacity
  limits, synchronous rejection requirements). Cost: a new stateful, critical-path
  service; participants need command-style (request/await-response) APIs.
- Event-Driven/choreography decouples systems completely: each reacts independently,
  scales independently, and requires no shared process owner. Cost: no natural home
  for cross-cutting invariants — enforcing them from pure choreography effectively
  re-introduces an aggregating consumer (an orchestrator by another name); and any
  requirement phrased as "reject/return an error" synchronously cannot be satisfied by
  a listener reacting to an already-published event.

**Resolution rule:** If the problem's hard constraints include (a) an invariant that
spans more than one external system's local state, or (b) a requirement for a
synchronous rejection/gate rather than an asynchronous flag, choose Saga/orchestration
as the primary decision-maker. Do not reject Event-Driven Architecture outright in this
case — fold its event bus in as the saga's transport (event-carried state transfer),
since the two lenses are complementary at different layers (decision vs. transport),
the same pattern already established for Hexagonal vs Service Mesh above.

**KB precedent:** D031 (PTL Task Saga, first non-OMS domain in this KB)

## Invariant Ownership vs Distribution Timing (DDD vs Event-Driven)

**Applies to:** Edge/gate-verification problems where a completeness invariant ("every
scanned/read item must be evaluated, none may be dropped") must be enforced locally and
in real time, and the data needed for that evaluation (a manifest, an expected-item
list) must reach the right edge before a physical event occurs.

- DDD-style options (an aggregate like GateSession) centralize *who enforces the
  invariant*: a guarded state transition (e.g. Close() throws unless every recorded item
  has a verdict) makes the completeness guarantee a code-level fact, not a convention
  every edge implementation has to separately get right.
- Event-Driven options centralize *how the data arrives in time*: publishing the
  expected-item list as soon as it is known (pre-positioning), ahead of the physical
  event, reusing existing canonical topics/partitioning/idempotency rather than a new
  synchronous call.
- These are usually **complementary, not competing** — the aggregate still needs a
  locally-cached read-model to evaluate against, and that read-model still needs a
  distribution mechanism. Rejecting either one outright typically means quietly
  re-implementing it under a different name (an aggregate with no data source, or an
  event consumer that silently becomes the invariant-enforcer by accretion).

**Resolution rule:** When the problem has both (a) a hard completeness/loss-prevention
invariant and (b) a cross-site or cross-process data-timing problem, pick the DDD
aggregate as the primary/chosen decision-maker for the invariant, and fold the
Event-Driven option in as the aggregate's data-feed/transport rather than treating them
as mutually exclusive. This is the same "decision vs transport" split as the Saga vs
Event-Driven resolution above (P026/D031) and the same "which layer does this lens
govern" split as Hexagonal vs Service Mesh (P025/D030) -- a third recurring instance of
blending two lenses by concern rather than picking one winner.

**KB precedent:** D032 (RFID GateSession aggregate + manifest pre-positioning, first
RFID Event Platform entry)
