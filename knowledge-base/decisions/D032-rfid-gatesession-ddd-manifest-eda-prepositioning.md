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

## Addendum (2026-08-10): Resolving P027 Open Item 6 -- Corrupt/Incomplete Manifest at Sync

**Problem**: `IManifestCache.GetActiveManifestFor()` only had two outcomes --
a manifest object, or `null`. A manifest that arrived but was missing EPCs
(from an out-of-order `manifest.updated` overwriting a fuller version, a
truncated payload, or a source-side assembly bug) was indistinguishable from
a fully correct one -- `GateSession` would resolve `FailSafeMode.Verified`
and trust it completely, with no audit signal that the manifest itself was
wrong. This is strictly worse than the already-handled "no manifest at all"
case, because it produces confident false `Unexpected` verdicts (and real
alarms) for legitimate EPCs that were dropped from the manifest, not flagged
anywhere as unverifiable.

**Decision**: close this at the manifest **write boundary**
(`ManifestSyncConsumer` / `IManifestCacheWriter`), not by changing
`GateSession`. Two additions, both applied before a manifest is ever allowed
into `IManifestCache`:

1. **Completeness proof.** `ManifestCreatedOrUpdatedEvent` (and the resulting
   `MovementManifest`) carries `ExpectedEpcCount` and a checksum over the
   sorted `ExpectedEpcs` set. `ManifestSyncConsumer` recomputes and compares
   both before upserting; a mismatch means the payload is truncated or
   otherwise corrupt, and the event is routed to a DLQ instead of being
   cached -- it is never trusted as `Verified`.
2. **Version ordering.** `MovementManifest` carries a monotonic `Version` per
   `ManifestId`. `Upsert` discards any incoming manifest whose version is
   less than or equal to what is already cached. This is a distinct problem
   from completeness: an older manifest can be internally consistent (its own
   count/checksum match) yet still be stale relative to a newer, larger
   manifest for the same movement round if delivery arrives out of order --
   idempotent `event_id` dedup (already platform-wide) prevents duplicate
   *events* from being reapplied, but says nothing about the *order* in which
   distinct events for the same `ManifestId` are consumed.

**Also adopted, low-cost, reusing existing platform patterns rather than
inventing new ones**:
- DLQ + retry on `ManifestSyncConsumer` write failures, using the same
  `subscribe -> transform -> deliver -> outbox -> retry -> DLQ` skeleton
  already standard for every adapter in this platform.
- A daily reconciliation job comparing the manifest declared at the source
  (Serialization Service) against what is cached at the destination edge --
  the same reconciliation-job pattern already used platform-wide for
  adapters -- as a backstop that catches anything the two checks above miss.

**Explicitly deferred**: chunking the manifest into multiple `manifest.chunk`
events with a `manifest.finalized` closer. Solves a message-size problem that
has not been confirmed to exist for this platform's actual transfer volumes,
and introduces its own new failure surface (a partial-assembly state
machine). Revisit only if a real transfer's `ExpectedEpcs` payload approaches
the message broker's size limit -- do not build this speculatively.

**Why the fix lives at the cache-write boundary and not in `GateSession`**:
`GateSession`'s zero-loss/zero-delay invariants (from the original D032
decision) are untouched by this addendum. If `IManifestCache` can no longer
hold an incomplete or stale manifest, then everything `GateSession` already
does -- trust a non-null result as `Verified` -- becomes safe automatically.
This keeps the blast radius of the fix to a single, well-isolated boundary
rather than touching the aggregate that already carries the platform's
highest-stakes invariant.

**Confidence**: high. The completeness-proof check closes the gap regardless
of root cause (it is a single enforcement point, not a fix per failure mode);
version ordering is a necessary complement because it solves a distinct
ordering problem that a count/checksum check alone cannot catch; DLQ/retry
and reconciliation are adopted because they are marginal-cost reuse of
patterns already proven elsewhere in this platform, not new operational
models the team has to learn.

## Addendum 2 (2026-08-10): Correcting How `ManifestSyncConsumer` Actually Receives Manifests

**Problem found while drafting the deployment/protocol diagram
(`manual/rfid-deployment-architecture.drawio`)**: the original D032 text and
the S032 code comments both describe `ManifestSyncConsumer` as "subscribing
to the canonical `manifest.created`/`manifest.updated` topics" directly at
the edge. Read literally, that means an edge process (DC Site Server / Store
Gateway) would hold a persistent Kafka consumer connection across the WAN.
That is **the exact thing D033 rejected** for the Ingestion Service hop, and
for the same two reasons: a persistent per-site broker session multiplied
across thousands of Store Gateways reintroduces per-client state at the
broker, and raw broker ports are far more likely to be blocked on retail
store networks than HTTPS/443. D032 and D033 would have been quietly
inconsistent with each other on this point if left uncorrected.

**Decision**: manifests reach the edge exactly the way Site & Config Service
already delivers config today -- **HTTPS/mTLS poll, not a broker
subscription**. Concretely:

- **Kafka never crosses the WAN, anywhere in this platform, full stop.**
  `manifest.created`/`manifest.updated` are consumed centrally, inside the
  datacenter, by **Site & Config Service** (which already owns the
  heartbeat-config-push mechanism edges use). It runs the completeness/
  version validation described in Addendum 1 *again* on the central side
  (defense in depth: catch garbage as early as possible, alert ops via
  `manifest.dlq` immediately, before an edge ever asks for it) and caches the
  latest valid manifest per site in Redis (fast, derived data --
  reconstructable from Kafka/Serialization Service, not a new source of
  truth).
- Site & Config Service exposes a new stateless endpoint, on the same
  contract shape as the existing config endpoint: `GET
  /v1/manifests/pending?site_id={id}&since_version={v}`. `ManifestSyncConsumer`
  (edge-side) polls this on the same heartbeat cadence `ConfigPoller` already
  uses -- reusing an existing client pattern rather than adding a second
  polling mechanism.
- The edge-side completeness/version check from Addendum 1 (`
  IsInternallyConsistent()`, version-ordering `Upsert`) is **kept, unchanged,
  as a second, independent validation** on the response payload. This is not
  redundant: it is the zero-trust boundary check for what actually arrived
  over the WAN, versus Site & Config Service's check being about what
  actually left Kafka. Neither check substitutes for the other.
- Because delivery is now poll-based rather than push-based, a rejected/
  invalid manifest at the edge needs no formal DLQ or redelivery mechanism of
  its own -- the next poll cycle simply re-fetches. Retry is inherent to
  polling; this removes a queue/redelivery mechanism the original wording
  implied was needed at the edge.

**Consequence**: `S032`'s code and context need a documentation fix (not a
logic fix) -- `ManifestSyncConsumer.OnManifestEvent` is unchanged in
behavior, it is simply invoked from an HTTP poll-response translator instead
of a Kafka consumer callback. No new architectural risk is introduced;
this closes a documentation inconsistency between D032 and D033 before it
became a real one at implementation time.

**Confidence**: high. This isn't a new trade-off being weighed -- it's
D033's already-accepted reasoning applied to a second WAN hop that had
accidentally been described inconsistently with it.

## Addendum 3 (2026-08-12): Reusing the Pre-Positioning Pattern for Inbound ASN, and Its Weaker Trust Model

**Context**: `GateSession`/`IManifestCache` were generalized (2026-08-11) to
serve three flows off the same mechanism: internal/inter-site transfer
(`MovementManifest`), outbound pick-verify (Sales Order/Load Plan), and
inbound auto-receive (ASN/PO). The first two already had their manifest
pre-positioning path fully specified (Addendum 2). The third -- how an ASN
actually reaches a DC's `IManifestCache` *before* the truck carrying that
ASN's goods arrives -- had never been drawn out; `manual/rfid-sequence-
diagrams.md` Diagram D simply showed `Session->>Session: match against
cached ASN/PO` with no upstream steps. This is now closed by reusing
Addendum 2's exact path, with a new source-side entry point.

**Decision (path)**: a new **supplier-facing API** on Serialization Service
(distinct from D033's edge-facing batch ingestion API -- suppliers have none
of our edge infrastructure) accepts two calls from external suppliers: (1)
serial-range requests before tagging, (2) ASN submission before goods leave
the factory. ASN submission publishes `manifest.created` onto the same Kafka
topic `MovementManifest` already uses (differentiated by `manifest_type`,
not a separate topic) -- Site & Config Service consumes it centrally, caches
to Redis, and `ManifestSyncConsumer` at the destination DC polls and
validates it exactly per Addendum 1/2, with no new mechanism. `GateSession`
itself needed no change.

**Decision (match granularity)**: ASN payload and match granularity now
follow `tracking_mode` per GTIN (the Dual EPC Tracking Mode decision,
`manual/rfid-architecture-summary.md` §3) rather than being fixed for the
whole ASN: `Serialized` GTINs carry a full expected-EPC list and get
per-serial matching (unchanged behavior); `CountOnly` GTINs carry only
SKU + expected quantity and get a GTIN-level count reconciliation at
`Close()` instead, reusing the quantity-level ASN matching pattern already
used for untagged goods. Collapsing `Serialized` GTINs to count-only was
considered and rejected: it would defeat per-unit traceability for exactly
the SKUs chosen for it, for zero runtime savings -- `GateSession` already
evaluates every physically-read EPC individually to satisfy the zero-loss
invariant regardless of match granularity, so the only real saving from
count-only is ASN payload size for `CountOnly` GTINs.

**Trust-model distinction worth stating explicitly**: unlike internal
transfer, where every EPC on a `MovementManifest` already has a prior
`epc_registry` row (the platform previously observed that EPC as a real
physical event), an inbound ASN describes EPCs the platform has **never
seen before** -- no `epc_registry` row exists until registration-on-first-
read fires *after* the gate match succeeds. The gate match in Diagram D is
therefore a **physical-reality-vs-supplier-claim consistency check**, not a
verification against independent ground truth: it catches shipping errors
(short/over count, wrong pallet/PO, damaged tags) but cannot detect a
colluding or compromised supplier reporting a fabricated EPC list, since
both sides of the comparison trace back to the same supplier's own
declaration. This is an accepted trade-off appropriate to an operational-
error detector, not a security/anti-counterfeit control, and must not be
conflated with the stronger guarantee internal transfer matching provides.
Logged as P027 open items #7 (limitation, open) and #8 (granularity split,
resolved by this addendum).

**Confidence**: high on the path (direct reuse of an already-validated
pattern); medium on the trust-model framing (correct given today's design,
but if a future requirement needs supplier-authenticity verification -- not
just shipment-accuracy verification -- this addendum's accepted trade-off
would need to be revisited, e.g. via `tid_registry` cross-checks or supplier
digital signatures on the ASN).

## Addendum 4 (2026-08-12): Closing the "Expected but Never Physically Present" Gap

**Context**: found while checking whether Addendum 3's `Serialized`-mode
per-EPC matching actually catches lot/batch substitution end to end. It
catches the case where wrong-batch EPCs are physically read at the gate
(any EPC not on `ExpectedEpcs` gets `Unexpected` regardless of GTIN). It did
**not** catch the case where correct EPCs are simply absent -- nothing
physically present to substitute for them (e.g. an entire pallet routed to
the wrong DC/PO by mistake). `GateSession`'s zero-loss invariant guarantees
every EPC the gate *read* gets a verdict; it says nothing about expected
EPCs that were never read at all. 400 EPCs read against an expected 500
produced 400 clean `Expected` verdicts and zero signal that 100 were
missing -- a silent shortage indistinguishable, from the manifest's
perspective, from a fully correct partial-fulfillment ASN.

**Distinction from Open Item 1** (RF miss-read): item 1 is a tag physically
present at the gate that the antenna failed to read -- a hardware
limitation requiring RF-layer mitigation. This is goods that were never
physically present on the pass-through at all -- a shipment/business-
integrity gap that software can close directly, since the data needed
(what was expected vs. what got an `Expected` verdict) is already fully
available inside `GateSession` at `Close()` time.

**Decision**: add `GateSession.ComputeMissingExpectedEpcs()`, computed once
at `Close()` -- `ExpectedEpcs` minus the set of EPCs that received an
`Expected` verdict. This is the `Serialized`-mode counterpart to Addendum
3's `ReconcileCountOnlyGtins()`; both are session-level reconciliations run
at `Close()` rather than per-read, because "is anything missing" is
unanswerable until the entire pass-through has completed. The result
threads through the existing `GateSessionResult` and
`gate.transfer.evaluated` (`IGateEventPublisher.PublishTransferEvaluated`)
-- no new event type, no new transport mechanism.

**Two constraints on how this must be used**, not just computed:
- **Not a real-time/alarm signal.** By construction it can only be known
  after the pass-through is over -- typically after the truck/forklift has
  already moved on -- so it must never drive the physical light stack the
  way an `Unexpected` verdict does. It exists solely for the audit event.
- **Downstream must act on it or the fix is theater.** The WMS Adapter must
  post GRN against the actual received quantity/EPCs when
  `MissingExpectedEpcs` is non-empty, flagged as an exception for review --
  not silently auto-post the full ASN-declared quantity as if the shipment
  were complete. Computing the field without a downstream consumer would
  leave the underlying business risk exactly where it was.

**Scope**: applies to all three flows sharing `GateSession` (internal/
inter-site transfer, inbound ASN, outbound pick-verify) -- any of them can
have a manifest-declared item simply absent with nothing physically
substituted in its place. Closes P027 Open Item 9.

**Confidence**: high. Same shape as the already-accepted `CountOnly`
reconciliation (Addendum 3) applied to close a symmetric gap on the
`Serialized` side; no new mechanism, no change to the zero-loss/zero-delay/
fail-safe invariants themselves -- this is an additional reconciliation
alongside them, not a modification to them.

## Addendum 5 (2026-08-12): Correlating a Physical Gate Pass to the Correct Manifest (Closes P027 Open Item 2, Inbound/Outbound Leg)

**Context**: `GetActiveManifestFor(siteId, movementRoundId)` requires a
`movementRoundId` -- fine for internal/inter-site transfer, where Ops/WMS
assigns that id explicitly at planning time and it's known to whoever opens
the physical move. Inbound Auto-Receive and Outbound Pick-verify never had
an equivalent: the original P027 text assumed "truck docks" / "sales-order
dispatch" functioned as an implicit trigger, but neither was ever a
system-resolvable key -- nothing told `GateSession` *which* PO/ASN applied
to the truck physically at the gate right now, especially at a DC with more
than one inbound gate or more than one shipment due around the same time.

**Decision**: correlate via the destination DC's **existing WMS/TMS dock
appointment scheduling system**, rather than inventing a parallel scheduling
mechanism inside the RFID platform. Concretely:

1. **WMS Adapter gains a reverse-sync responsibility.** It was previously
   push-only toward WMS (posting GRN/adjustments to staging/interface
   tables). It now also reads dock appointment confirmations back and
   publishes a new event, `dock.appointment.confirmed` (`PoRef`, `SiteId`,
   `GateId`, `ScheduledWindowStart/End`), onto Kafka. This is the **only**
   new integration surface this addendum introduces -- everything downstream
   reuses mechanism that already exists.
2. **Serialization Service joins, does not originate.** It consumes
   `dock.appointment.confirmed`, matches it to the `MovementManifest`
   already created from the ASN (Addendum 3) by `PoRef`, and republishes it
   as an ordinary `manifest.updated` -- a higher `Version` of a manifest
   that, in the common case, is already sitting in the destination DC's
   `IManifestCache` from ASN pre-positioning. No new event type reaches the
   edge; no new pre-positioning pipeline. This rides the version-ordering
   mechanism Addendum 1 already built for exactly this purpose (superseding
   a cached manifest with a more complete/current one).
3. **`GateSession` resolves by gate + time instead of by an externally-
   supplied round id.** A new `IManifestCache.GetActiveManifestForGate(
   siteId, gateId, asOf)` filters cached manifests to the one whose
   `GateId` matches and whose scheduled window contains `asOf` (with an
   operationally-tuned grace buffer for early/late arrivals). `GateSession`
   is constructed with `movementRoundId: null` for these two flows and
   resolves through this path instead.

**Ambiguity is deliberately conflated with absence, not distinguished from
it, at the `GateSession` level.** If two appointments' windows overlap the
same gate (should be rare -- that is precisely what dock scheduling exists
to prevent -- but not impossible, e.g. a late-running truck overlapping the
next slot), `GetActiveManifestForGate` returns `null` exactly as it would
for "nothing scheduled," and the already-built `FailSafeMode` fallback
applies unchanged. `GateSession` has no way to safely guess which of two
candidates is correct, and guessing wrong would be worse than fail-safe.
Surfacing *which* case occurred (never-scheduled vs. ambiguous-overlap) is
an ops observability/alerting concern layered on top of this event, not a
decision embedded in the domain aggregate.

**Scope note -- internal transfer is explicitly out of scope for this
addendum.** `movementRoundId` continues to resolve `MovementManifest`
directly, unchanged. How that id physically reaches whoever opens the move
session (a handheld screen, a scanned QR code on a pick list) is an
operational hand-off question, not an architectural gap of the same shape --
noted as a smaller residual item in P027 rather than folded into this fix.

**Confidence**: high on reusing the existing WMS/TMS dock scheduling system
rather than building a parallel one, and on riding the existing version-
ordering pipeline for delivery. Medium on the grace-buffer window size for
early/late arrivals -- that is an operations-tuned parameter, not an
architectural one, and needs a real answer from DC operations before pilot.

## Addendum 6 (2026-08-12): The Join Must Be Order-Independent

**Context**: Addendum 5's join logic reads "Serialization Service joins
`dock.appointment.confirmed` to the `MovementManifest` **already created**
from the ASN" -- an unstated ordering assumption. In practice a DC may book
a dock appointment as soon as a PO is issued, well before the supplier ever
submits the corresponding ASN (dock scheduling and ASN submission are two
independent business processes with no enforced sequencing between them).
If `dock.appointment.confirmed` arrives before any `MovementManifest` exists
for that `PoRef`, the join as originally specified finds nothing to attach
to -- the appointment is silently dropped, and the gap Addendum 5 closed
reopens for exactly the shipments whose dock slot was booked early.

**Decision**: the join must work regardless of which event arrives first.
Serialization Service stages unmatched appointments rather than discarding
them:

- **`dock.appointment.confirmed` arrives, no matching manifest yet**: store
  as a `PendingDockAppointment` (`PoRef`, `SiteId`, `GateId`,
  `ScheduledWindowStart/End`, `ReceivedAt`), keyed by `PoRef`. No event
  published yet -- there is no manifest to update.
- **ASN arrives, `MovementManifest` is being created**: before publishing
  `manifest.created`, check `PendingDockAppointment` for a matching
  `PoRef`. If found, populate `GateId`/`ScheduledWindowStart/End` directly
  into the manifest at creation -- a single `manifest.created` carries
  everything, no follow-up `manifest.updated` needed for this ordering.
  Consume (delete) the pending record.
- **ASN arrives first (the ordering Addendum 5 originally assumed)**:
  unchanged -- manifest is created without gate/window fields, and a later
  `dock.appointment.confirmed` triggers the join via `manifest.updated`
  exactly as already specified.

Both orderings converge on the same end state: `IManifestCache` eventually
holds a manifest with `HasGateAppointment == true`, regardless of which
event happened to arrive first.

**Not addressed here, flagged rather than solved**: a `PendingDockAppointment`
whose ASN never arrives (shipment cancelled, PO reissued under a different
number) will sit indefinitely with nothing to consume it. This needs a
retention/expiry policy (e.g. purge after N days past `ScheduledWindowEnd`)
but the specific value is an operations question, not resolved by this
addendum.

**Scope**: this is a Serialization Service-internal staging concern --
`ManifestSyncConsumer`, `IManifestCache`, and `GateSession` are all
unaffected, since by the time either event reaches the edge it always
arrives as a complete, self-consistent manifest (or manifest update) exactly
as those components already expect. No change to the edge-side contract at
all.

**Confidence**: high. Staging an unmatched event until its counterpart
arrives is a standard pattern for order-independent joins across two
independent upstream processes; the alternative (assuming an order that
isn't actually guaranteed) is what caused this gap in the first place.

## Addendum 7 (2026-08-12): Where `gate_id` Actually Comes From, and Two Assumptions That Follow From It

**Context**: Addendums 5 and 6 both treat `gate_id` as a value `GateSession`
and `dock.appointment.confirmed` simply already have. Neither ever specified
where it originates. Tracing it back surfaces one implementation answer and
two assumptions that were never stated, let alone validated.

**Where `gate_id` comes from**: it is not computed or looked up at runtime.
It is assigned once, at **physical gate provisioning** -- when a DC installs
or recommissions an inbound/outbound RFID gate, ops/IT registers it in the
**Device/Config Registry** (component #9, PostgreSQL, owned by Site/Config
Service), associated with `site_id`, its physical Device Connector/LLRP
endpoint, and a `gate_id` value. `ConfigPoller` (component #3b) delivers
this down to the Edge Agent Process on its normal heartbeat config poll --
by the time `GateSession` is constructed for an actual gate pass, `gate_id`
is already known locally as static config, not fetched. This costs nothing
new: it rides the config-poll mechanism that already exists for every other
piece of edge configuration.

**Assumption 1 -- `gate_id` namespace must match WMS/TMS's dock-door
numbering, and nothing currently guarantees that.** `dock.appointment.confirmed`
(Addendum 5) carries a `gate_id` sourced from WMS/TMS's own dock scheduling
data -- almost certainly keyed by however that system already numbers dock
doors (e.g. "Door 3"), a numbering scheme this platform's Device/Config
Registry has no reason to already agree with. If the two are different
identifier spaces, the join Addendum 5/6 depends on silently never matches
-- not a crash, not an error, just permanent `FailSafeMode` for every
inbound/outbound gate. **Recommendation**: register gates in the Device/
Config Registry using WMS/TMS's existing dock-door identifiers directly as
`gate_id`, rather than minting a separate platform-internal numbering
scheme -- this eliminates the mismatch risk by construction instead of
requiring an explicit mapping table to be built and kept in sync. A mapping
table remains the fallback if the direct-reuse approach turns out to be
operationally infeasible (e.g. IT has already assigned incompatible device
IDs before this is caught).

**Assumption 2 -- every inbound/outbound PO is assumed to get a dock
appointment confirmed in WMS/TMS, and this has not been validated with
operations.** If some class of shipment does not go through dock
scheduling in practice (small/urgent deliveries, walk-in trucks at some
DCs), `dock.appointment.confirmed` never arrives for those POs, `gate_id`
is never populated, and `GetActiveManifestForGate` permanently resolves to
`null` for them -- falling back to `FailSafeMode` on every pass, every
time, for that entire class of shipment. This is architecturally identical
in shape to the existing unvalidated assumption in P027 Open Item 5 ("a
manifest-creation step exists before every internal movement") -- same
category of risk, different upstream process.

**Scope**: documentation and an operational validation question, not a code
change -- `GateSession`'s existing fail-safe path already handles "no
manifest resolved" correctly regardless of *why* resolution failed.

**Confidence**: high that these are real, currently-unverified assumptions
worth surfacing before pilot; the actual answers (does WMS/TMS's dock-door
numbering already align with what Device/Config Registry would use, and
does every inbound PO actually get a booked appointment today) are
operations questions this document cannot resolve on its own.

## Addendum 8 (2026-08-12): A Fourth Flow -- Store Backroom Inbound

**Context**: found while designing a JSON payload schema for `store.received`
(flow #9, Store Backroom Inbound + Geiger Search -- a handheld sled scans
through closed boxes, compares reads against a manifest the source DC
pre-positioned, and opens a "Geiger mode" search when something expected is
missing). Its shape turned out to be identical to `GateSessionResult`: the
"item expected but missing, go search for it" behavior described for this
flow in `manual/rfid-operational-flows.md` is exactly the
`MissingExpectedEpcs` case (Addendum 4). This flow had never been formally
listed alongside the three `GateSession` already serves.

**Decision**: `GateSession` is generalized to a **fourth flow**, Store
Backroom Inbound (flow #9), joining internal/inter-site transfer, inbound
ASN, and outbound pick-verify. No code change required -- the aggregate was
never actually scoped to physical gate hardware; "GateSession" names the
first use case, not a hardware dependency. The concept it implements is "one
evaluate-a-batch-against-an-expected-list session, evaluated locally,
zero-loss, fail-safe," which fits a handheld sled scan through boxes
exactly as well as a fixed FX9600 gate pass. One difference worth stating
explicitly: flow #9 has no physical Light Stack Alarm to drive -- the
`FailSafeMode`/verdict output still applies, but the "immediate red light"
consequence some other flows have does not exist here; feedback is on the
sled's own display/haptics instead.

**Confidence**: high -- this is a naming/scoping correction, not a new
mechanism. The invariants (`zero-loss`, `zero-delay`, `fail-safe`,
`MissingExpectedEpcs` reconciliation) all already applied to this flow in
substance; they simply weren't listed as covering it.

## Addendum 9 (2026-08-12): `MovementManifest.PoRef` -- Closing a Join Bug, Not Just a Gap

**Context**: found while discussing an unrelated, user-proposed process
change -- entering the PO number explicitly at `MovementManifest` creation
time for internal/inter-site transfer, since Ops/WMS already has that
number at planning time. Checking where this number would actually live
surfaced that Addendum 5/6's join ("Serialization Service joins
`dock.appointment.confirmed` to the matching `MovementManifest` by PoRef")
was never actually implementable: `MovementManifest` had no `PoRef` field.
This was not just an underspecified detail -- the join as coded could not
have worked, full stop, since there was nothing on the manifest side to
match `DockAppointmentConfirmedEvent.PoRef` against.

**Decision**: add `MovementManifest.PoRef` (nullable `string`), mirrored on
`ManifestCreatedOrUpdatedEvent`. Populated two ways, matching how the two
manifest-creating flows already work:
- **`InboundAsn`**: the supplier already provides a PO ref when submitting
  the ASN (Diagram D) -- this was always conceptually true, just never
  modeled as a field on the shared manifest type.
- **`IntraSite`/`InterSite`**: Ops/WMS provides it at `MovementManifest`
  creation -- the process change that surfaced this bug. Diagram A's
  `Planner->>Serial: create MovementManifest(...)` step gains a `poRef`
  parameter.

Nullable, not required: a pure intra-site zone move may have no PO-like
reference document at all. For those, dock-appointment correlation is
simply unavailable on that manifest (same as today), and nothing else
changes.

**Explicitly not changed**: `GateSession`'s resolution strategy.
`movementRoundId` remains the primary correlation key for internal
transfer. Making `PoRef` universally available does not imply every
internal transfer should switch to `GetActiveManifestForGate` (Addendum 5)
-- most intra-site zone moves have no real "dock appointment" to correlate
against in the first place, so `movementRoundId`-based resolution stays
correct and necessary for that case. An inter-site transfer that *does*
route through an actual dock could opt into gate+time resolution now that
`PoRef` exists on its manifest, but that is a case-by-case option to
evaluate later, not a default this addendum forces.

**Confidence**: high that this was a genuine implementability bug, not
imprecise wording -- worth stating plainly, since it means the
dock-appointment correlation mechanism described across Addendum 5/6/7 has
never actually been functional for any flow until this fix, including
inbound ASN. Medium on the internal-transfer scope boundary (keeping
`movementRoundId` primary) -- reasonable given intra-site moves generally
lack a dock concept, but worth revisiting if inter-site transfers turn out
to route through real dock scheduling in practice.
