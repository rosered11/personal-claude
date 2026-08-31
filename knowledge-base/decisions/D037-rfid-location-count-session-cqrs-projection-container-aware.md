---
id: D037
chosen_option: "LocationCountSession -- a GateSession-Sibling Aggregate (DDD) Enforcing Zero-Loss/Fail-Safe Location-Count Invariants, Fed by a Container-Aware CQRS Materialized Projection (location_contents)"
problem_id: P032
tags: [rfid, edge-computing, offline-first, domain-driven-design, cqrs, warehouse-management, cycle-count, gate-verification]
related_snippets: [S037]
---

# Decision: LocationCountSession as a GateSession-Sibling Aggregate, Fed by a Container-Aware CQRS Materialized Projection

## Context

P032 needs a location-scoped cycle-count capability whose expected-EPC
baseline is derived from the platform's own last-known state
(`epc_registry`), not an externally-declared document -- a fundamentally
different "expected list" shape than every prior `GateSession`-family flow
has ever needed. kb-search against the existing 31 KB entries found P031
as the closest precedent (~0.6 overlap: `rfid`, `edge-computing`,
`gate-verification`, `offline-first`, `domain-driven-design`,
`warehouse-management`), with P030 and P027 tied further behind (~0.38
each) -- all below the 0.8 UPDATE threshold, so this correctly became a new
CREATE-mode record. This is the sixth RFID Event Platform consultation and
the third and last of three queued from one real warehouse site visit
(after P030/D035 and P031/D036), and the first whose core design question
is not "how does a declared expected list reach the edge in time" but "how
does an expected list get *produced* at all, absent a declaring document."

## Options Considered

**Lens A -- Domain-Driven Design**: introduce `LocationCountSession`, a new
type living alongside `GateSession` in the Event Processor's aggregate
family, reusing its proven invariant shape field-for-field -- `RecordRead`
(zero-loss), synchronous in-process evaluation (zero-delay), an explicit
`FailSafeMode`, and a `Close()`-time reconciliation that is this flow's
exact counterpart to `ComputeMissingExpectedEpcs` (D032 Addendum 4).
Crucially, it resolves its baseline through a **new, structurally parallel
port**, `ILocationContentsCache`, rather than a fourth `IManifestCache` key:
a location baseline is a continuously-refreshed, self-asserted snapshot with
no `Created -> Distributed -> Active -> Consumed/Expired` lifecycle the way
`MovementManifest` has (D032's original decision text) -- it is never
"consumed," just periodically superseded. Overloading `IManifestCache` with
a fourth key would force this genuinely different kind of expected list to
carry lifecycle semantics it does not have.

**Lens B -- CQRS**: treat the entire "how is the baseline produced" question
as a read-model materialization problem. `location_contents` is a
projection built centrally in the Serialization DB, folded from the same
location-stamping events that already update `epc_registry` (no new source
of truth -- a derived view over write-side facts, the same "read model from
write events" pattern this KB's own OMS lineage already established, D018/
D025, applied here for the first time on the RFID platform). Crucially, the
projection-build query can freely **join `epc_registry.location_id` against
`container_contents`** (D036) at build time, in the same PostgreSQL
database, so a container's resolved contents are folded into
`location_contents` for the container's location automatically -- without
requiring every item sealed inside it to ever receive its own location
stamp. Fanout to the edge reuses D036's asymmetric-scope discipline: a site
only ever receives snapshots for its own locations.

Both architects agreed on one thing without prompting: whichever lens is
chosen, the container-interaction risk P031/D036 flagged (Open Item 1) is
directly in scope here, since a location count is exactly the session type
most likely to encounter a sealed container sitting at a location. Lens B's
core mechanism turns out to close that risk **by construction**, at the
baseline's source, rather than requiring every downstream consumer to
remember a cross-reference rule -- a materially stronger answer than D036's
own caveat ("must be cross-referenced ... not implemented in this
snippet") ever proposed.

## Decision

Adopt **Lens A (DDD)** as the primary invariant-enforcement structure --
directly following the Clarified Scope's own steer to reuse `GateSession`'s
zero-loss/zero-delay/fail-safe machinery as the starting point -- with
**Lens B's baseline-production mechanism adopted as essential
infrastructure, not an optional companion**: `LocationCountSession` cannot
exist without something producing what it evaluates against, and CQRS is
the only lens on the table with an actual answer to that question.

**1. Schema change: `epc_registry.location_id`.** A new nullable
`location_id` column (granularity -- `Zone`/`Bin`/`Shelf` -- is a per-site
config value, defaulting to `Zone`, mirroring D035's "config value, not a
new system" discipline) plus `location_updated_at`. `site_id` continues to
answer "which DC/store"; `location_id` answers "where within that site,"
and is `null` until the first location-stamping write path below ever sets
it.

**2. Which write paths stamp location, and how.** Rather than inventing a
new write path per flow, this decision extends the **existing** event
consumer that already drives `epc_registry` status transitions from
`GateSessionResult`-shape events (Appendix 4 group 1: `gate.transfer.
evaluated`, `inbound.received`, `pick.verified`, `store.received`) to also
stamp `location_id` when the closing session knows a destination location.
Concretely: `GateSessionResult` gains an optional `DestinationLocationId`,
populated by whichever flow's `GateSession.Close()` actually has one
(intra-site zone-to-zone internal transfer, zone receiving, store backroom
inbound); flows with no location concept (outbound pick-verify, POS sale)
simply never populate it. Symmetrically, `item.sold` and `tag.voided`
**clear** `location_id` on consume (an EPC that has left the building or
been voided is not "at" any location). This reuses 100% of the existing
consumer/write-path infrastructure -- no new integration surface.

**3. How the baseline is derived and pre-positioned, without a synchronous
call.** `location_contents` (new Serialization DB table) is a materialized
projection, rebuilt (fully or incrementally, an implementation choice not
fixed here) whenever a location-affecting event lands, joining
`epc_registry` (direct location stamps) with `container_contents` (D036,
for container-resolved contents) -- see `S037`'s
`LocationContentsProjectionSql` for the concrete join. Each rebuild produces
a versioned `LocationContentsSnapshot` carrying `ExpectedEpcCount` +
`ExpectedEpcsChecksum`, reusing D032 Addendum 1's completeness-proof pattern
verbatim. Snapshots reach the edge via the **exact same proven transport**
every other `GateSession`-family cache already uses -- Kafka (central-only,
never crosses the WAN, D032 Addendum 2) -> Site & Config Service -> Redis ->
HTTPS/mTLS poll -> edge cache -- through a new but structurally parallel
port, `ILocationContentsCache.GetExpectedEpcsFor(siteId, locationId)`. Site
scoping is deliberate: an edge only ever receives snapshots for its own
site's locations, the tightest fanout scope this platform has used yet.

**4. Not a fifth `GateSession` resolution mode.** `LocationCountSession` is
a new type, not a `GateSession.OpenForXxx` variant, because the thing it
resolves against is semantically different from every `MovementManifest`
(a declared, versioned, eventually-*consumed* document) -- a location
snapshot is continuously live and never consumed. Forcing it through
`IManifestCache`'s existing resolution methods would conflate two different
meanings of "expected list" under one abstraction. `LocationCountSession`
does, however, reuse `GateSession`'s invariant-enforcement *shape*
(RecordRead/zero-loss, synchronous evaluation/zero-delay, explicit
`FailSafeMode`, Header-based scheme dispatch including the SSCC branch from
D036) field-for-field -- this is the correct generalization of D035's
premature-abstraction lesson: reuse the *pattern*, not the literal
interface, when the thing being modeled is genuinely different in kind.

**5. Closing P031/D036 Open Item 1 for this flow.**
`LocationCountSession.Close()` computes missing expected EPCs the same way
`ComputeMissingExpectedEpcs` does, but an expected EPC that never received
its own `Expected` verdict is **not** automatically flagged missing if it
is `ViaContainer` in the baseline (i.e. present in `location_contents` only
because its container's contents were folded in at projection-build time)
**and** that specific container was itself read (`ContainerRead`, resolved)
during this same session. An expected, container-resolved EPC whose
container was **not** read this session is **not** suppressed -- that
container, and everything sealed inside it, is a real, legitimate absence a
location count exists to catch. See `S037`'s
`LocationCountSession.ComputeMissingExpectedEpcs` for the exact logic. This
is the first implementation of the cross-reference D036 mandated but never
built -- see Consequences for its remaining platform-wide scope.

**6. The existing site-wide flow is untouched.** `count.completed` / Event
Processor flow #3 book-stock variance calculation is not modified in any
way. `LocationCountSession` publishes a new, additive event,
`location.count.evaluated`, reusing the Appendix 4 group-1
`GateSessionResult` payload shape plus the `ContainerReads` extension from
D036 -- not `count.completed`, and not `gate.transfer.evaluated`.

## Consequences

**Accepted trade-offs**:
- This is the platform's first genuinely new sibling type to `GateSession`
  rather than an extension of `GateSession` itself (unlike D032's original
  design, Addendum 8's flow #9, D035's third resolution mode, or D036's
  SSCC branch) -- new discipline this platform has not needed before:
  invariant-enforcement code must now either be duplicated between
  `GateSession` and `LocationCountSession` or factored into a shared base,
  a refactor not undertaken by this decision.
- `location_contents` is this platform's first true continuously-
  materialized projection, distinct in kind from a `MovementManifest`
  (bounded, versioned, tied to one planning event) -- refresh cadence and
  staleness tolerance are new operational concerns with no established
  tuning precedent on this platform yet (P032 Open Item 3).
- P031/D036 Open Item 1 (container-packed items falsely appearing missing)
  is closed **only for `LocationCountSession`** by this decision. The other
  three `GateSession` flows still lack the cross-reference check -- the
  underlying risk remains open platform-wide until retrofitted there too
  (P032 Open Item 1).
- A full audit of every existing `epc_registry` write path (pick, ship,
  sale, void, return) for whether it should set, clear, or leave
  `location_id` untouched is not exhaustively specified here -- only the
  `GateSessionResult`-shape group's stamp/clear behavior is defined (P032
  Open Item 4).

**Benefits**:
- Directly answers the hardest, previously-unprecedented question this
  problem posed -- "how does a self-asserted expected list get produced
  without a synchronous call" -- with a concrete, buildable mechanism
  (materialized projection + existing pre-positioning transport), not just
  a policy statement.
- Reuses 100% of the platform's proven transport infrastructure (Kafka
  central-only, Site & Config Service, Redis, HTTPS/mTLS poll, edge
  SQLite cache) for the new `ILocationContentsCache` snapshot -- no new
  integration surface, directly satisfying the Clarified Scope's "reuse
  over invention" instruction.
- Solves the container-interaction risk **by construction** at the
  baseline's source (the projection join) rather than relying solely on
  every downstream consumer remembering a cross-reference rule -- a
  materially stronger design than D036's own unimplemented caveat, and the
  first time this platform's container-contents relationship (D036) has
  been consumed by a second, independent flow.
- `epc_registry`'s existing status state machine, `item_master` enrichment,
  and every other `GateSession` flow are structurally untouched -- location
  tracking is an additive column plus an additive projection, not a
  modification to any existing table's meaning.
- Directly extends the same "config value, not a new system" discipline
  (D035) for location granularity, and the same asymmetric-fanout
  discipline (D036) for projection scoping -- consistent generalization of
  two already-validated platform principles, not new ad hoc judgment calls.

**Confidence**: medium. High confidence in the invariant-reuse structure
(directly follows the Clarified Scope's own steer and four times-validated
`GateSession`-family precedent) and in the transport reuse (zero new
infrastructure). Confidence is capped at medium because this decision
introduces the platform's first true continuously-materialized projection
with no prior operational tuning experience on this platform (refresh
cadence, staleness tolerance -- P032 Open Item 3), and because it closes
P031/D036's highest-priority open item only partially (this flow only, not
the three pre-existing `GateSession` flows that share the same underlying
risk).
