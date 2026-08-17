---
when_to_use: "Use when a gate/checkpoint-style physical control point must evaluate a batch of scanned identifiers against a manifest scoped to one specific event/round (not a global registry), with hard zero-delay (local decision only) and zero-loss (every scanned identifier must be evaluated) requirements, and where the manifest itself may need to be pre-positioned to a remote site's edge ahead of a physical transfer."
related_problems: [P027]
related_decisions: [D032]
---

# Snippet: GateSession Domain Aggregate + Event-Pre-Positioned Manifest Cache

This snippet demonstrates the D032 decision: a `GateSession` aggregate (DDD)
that owns zero-loss/zero-delay/fail-safe invariants for one RFID gate
pass-through, evaluated against a `MovementManifest` read model that is kept
locally warm by a `ManifestSyncConsumer` (EDA transport half of the
decision). **Per Addendum 2**, "kept warm" means polled over HTTPS/mTLS
through Site & Config Service -- not a direct Kafka subscription at the edge;
Kafka never crosses the WAN anywhere in this platform.

It shows:
- **Manifest resolution: two keys, not one (Addendum 5)** -- `GateSession`'s
  constructor takes `movementRoundId` as nullable. Internal/Inter-site
  Transfer passes a real value (Ops/WMS hands the physical mover a known
  round id at planning time) and resolves via `GetActiveManifestFor`.
  Inbound Auto-Receive / Outbound Pick-verify pass `null` -- nothing at the
  gate knows a round id up front -- and resolve instead via
  `GetActiveManifestForGate(siteId, gateId, openedAt)`, correlating through
  a WMS/TMS dock appointment already bound to that `gate_id` and time
  window. Same `FailSafeMode` fallback either way if resolution is `null`.
- `GateSession.RecordRead(epc)` -- deduped-but-never-dropped read intake;
  every unique EPC gets exactly one verdict, evaluated synchronously against
  whatever `IManifestCache` currently holds locally (no network call).
- **Match granularity per GTIN (Addendum 3)** -- `Evaluate()` branches on
  `MovementManifest.IsCountOnlyGtin(gtin)`: `Serialized` GTINs get the
  original per-EPC `Expected`/`Unexpected` verdict against `ExpectedEpcs`;
  `CountOnly` GTINs get a `Counted` verdict immediately (judgment deferred)
  while a running per-GTIN tally accumulates, reconciled against
  `ExpectedCountsByGtin` in `Close()` via `ReconcileCountOnlyGtins()`. Every
  read still gets a verdict either way -- this costs zero extra work at the
  gate; the only real saving from `CountOnly` is ASN payload size upstream.
- `GateSession.Close()` -- guarded transition that throws
  `InvalidOperationException` if any recorded EPC has no verdict yet,
  making the "zero-loss" constraint a code-enforced invariant instead of a
  convention. Now returns a `GateSessionResult` (verdicts + `GtinCountMismatch`
  list from CountOnly reconciliation + `MissingExpectedEpcs` from Serialized
  reconciliation) rather than just the verdict list.
- **`MissingExpectedEpcs` (Addendum 4)** -- closes a gap zero-loss does NOT
  cover. Zero-loss guarantees every EPC the gate *read* gets a verdict; it
  says nothing about expected EPCs that were never read at all -- e.g. a
  whole pallet routed to the wrong DC/PO, with no substitute tag physically
  present to trip `Unexpected`. Before this fix, 400 EPCs read against an
  expected 500 produced 400 clean `Expected` verdicts and zero signal that
  100 were missing. `ComputeMissingExpectedEpcs()` closes this the same way
  `ReconcileCountOnlyGtins()` closes the analogous gap for `CountOnly`
  GTINs -- both are session-level reconciliations computed once at `Close()`,
  because "is anything missing" can only be answered after the whole
  pass-through is over, not per-read.
- `FailSafeMode` (`FailOpen` / `FailClosed`) -- resolved once per session at
  open time from the current `IManifestCache` state (manifest present, stale,
  or entirely missing) and stamped onto every verdict, so a fail-open pass is
  always visibly distinguishable from a verified pass in the audit trail.
- `IManifestCache` (port) -- the local, edge-resident read model the
  `GateSession` depends on; it is written to only by `ManifestSyncConsumer`,
  which is invoked from an HTTPS/mTLS poll response (`GET
  /v1/manifests/pending`, served by Site & Config Service, which itself
  consumes `manifest.created`/`manifest.updated` centrally and caches per
  `site_id` in Redis) -- the exact mechanism that makes inter-site manifest
  pre-positioning work without any synchronous registry call, and without a
  broker connection ever crossing the WAN.
- `IGateEventPublisher` (port) -- publishes `gate.transfer.evaluated` once
  per closed session, feeding the same reconciliation-job pattern already
  used platform-wide for adapters, so a `FailOpen` pass is never a silent
  gap.
- Plain constructor DI throughout -- no MediatR, no AutoMapper, per this
  repository's .NET standards.

**D032 addendum (2026-08-10, closes P027 Open Item 6)**: `ManifestSyncConsumer`
now enforces two checks *before* a manifest is ever allowed into
`IManifestCache`, so `GateSession` itself needed zero changes:
- `MovementManifest.IsInternallyConsistent()` -- rejects (to a DLQ, via
  `IManifestDeadLetterPublisher`) any manifest whose declared
  `ExpectedEpcCount`/`ExpectedEpcsChecksum` doesn't match what it actually
  carries, regardless of whether the corruption came from a truncated
  payload, a source-side bug, or transport issues.
- Version ordering -- `Upsert` discards any incoming manifest whose `Version`
  is not strictly greater than what's already cached for that `ManifestId`,
  since an internally-consistent-but-stale manifest (arrived out of order)
  would otherwise pass the checksum check yet still be wrong for the current
  movement round.

Architecturally, this is the boundary to reuse for any future RFID gate flow
that needs "evaluate a batch against a scoped expected-list, locally, with a
defined fail-safe behavior" -- new movement types should add a new
`MovementManifest` source, not a new gate-decisioning mechanism. Any new
manifest producer must populate `Version`/`ExpectedEpcCount`/
`ExpectedEpcsChecksum` correctly -- that contract, not `GateSession`, is what
now guarantees a cached manifest is trustworthy.

**D032 Addendum 2 (2026-08-10, found while drafting
`manual/rfid-deployment-architecture.drawio`)**: fixes a documentation
inconsistency between D032 and D033 -- both this file's original wording and
the code comments described `ManifestSyncConsumer` as "subscribing to
canonical event topics" directly, which would mean a persistent Kafka
connection across the WAN. D033 already ruled that pattern out for the
Ingestion Service hop for concrete reasons (per-site broker state at
thousands-of-sites scale, broker ports blocked on retail networks); the same
reasoning applies here. The fix is delivery mechanism only, not logic:
`ManifestSyncConsumer.OnManifestEvent` is unchanged, it is simply invoked
from an HTTP poll-response translator instead of a broker callback. The
edge-side completeness/version check from Addendum 1 stays exactly as coded
-- it is now understood as the zero-trust check on the WAN response, with
Site & Config Service independently re-running the same validation centrally
on what actually left Kafka (defense in depth, not redundancy).

**D032 Addendum 3 (2026-08-12)**: this same aggregate/cache mechanism is
reused, unchanged in mechanism, for the inbound ASN flow (source-tagged
receiving) via a new supplier-facing API on Serialization Service --
`MovementManifest.MovementType = "InboundAsn"` flows through the identical
`ManifestSyncConsumer` path. Two additions, both isolated to the manifest
shape and `GateSession.Evaluate()`, not the invariants themselves:
- `IEpcGtinResolver` + `MovementManifest.ExpectedCountsByGtin` -- lets a
  single manifest mix `Serialized` GTINs (full EPC list, per-serial match)
  and `CountOnly` GTINs (expected quantity only, GTIN-level count match) in
  the same ASN, following whichever `tracking_mode` Serialization Service
  has already assigned per GTIN (Dual EPC Tracking Mode decision). Rejected
  alternative: collapsing everything to count-only, which would defeat
  per-unit traceability for `Serialized` GTINs for no runtime saving, since
  `Close()`'s zero-loss check already requires a verdict per physical read
  regardless of granularity.
- **Trust-model caveat this snippet does NOT solve**: for `InboundAsn`
  manifests, none of `IsInternallyConsistent()`, version ordering, or the
  match itself establishes that the ASN's EPCs/quantities are authentic --
  only that the payload arrived intact and current, and that physical reads
  agree with what the supplier declared. Contrast `IntraSite`/`InterSite`
  manifests, whose EPCs already have a prior `epc_registry` row this
  platform itself created from an earlier observed event. `InboundAsn`
  matching is therefore an operational-error detector (catches short/over
  count, wrong pallet/PO, damaged tags), not a supplier-authenticity or
  anti-counterfeit control. Logged as `P027` open item #7 (accepted
  limitation) and #8 (granularity split, resolved by this addendum).

**D032 Addendum 4 (2026-08-12, closes P027 Open Item 9)**: found while
tracing whether Addendum 3's `Serialized`-mode matching actually catches
"correct EPCs simply absent, nothing substituted" (as opposed to "wrong-batch
EPCs physically present" -- which per-EPC matching already caught). It did
not: `GateVerdict.Unexpected` only fires for EPCs that were *read but not
expected*; an expected EPC that was never read at all produced no verdict of
any kind and therefore no signal. Fixed by adding
`GateSession.ComputeMissingExpectedEpcs()`, called from `Close()` alongside
`ReconcileCountOnlyGtins()`, and threading the result through
`GateSessionResult` and `IGateEventPublisher.PublishTransferEvaluated`.

Two design points worth keeping explicit:
- **Not a real-time/alarm signal.** Unlike `Unexpected` (known the instant a
  bad tag is read, while the truck/forklift is still at the gate),
  "missing" can only be known once the entire pass-through has completed --
  by definition too late to drive the physical light stack. It exists
  purely in the `gate.transfer.evaluated` audit event.
- **Downstream must act on it, or the fix is theater.** Specifically: the
  WMS Adapter must post GRN against the *actual* received quantity/EPCs
  when `MissingExpectedEpcs` is non-empty, flagged as an exception for
  review -- not silently auto-post the full ASN-declared quantity as if
  nothing were wrong. Computing the field and not consuming it downstream
  would leave the gap practically open despite being "fixed" in code.

This applies to all flows sharing `GateSession` (internal/inter-site
transfer, inbound ASN, outbound pick-verify, and store backroom inbound --
see the fourth-flow note below), not only inbound receiving -- any of them
can have a manifest-declared item simply absent with nothing physically
substituted in its place.

**Confidence**: high. Same shape as the already-accepted `CountOnly`
reconciliation (Addendum 3), applied to close a symmetric gap on the
`Serialized` side; no new mechanism, no change to the zero-loss/zero-delay/
fail-safe invariants themselves.

**D032 Addendum 5 (2026-08-12, closes P027 Open Item 2)**: Inbound Auto-
Receive / Outbound Pick-verify never had an answer for "which PO/ASN applies
to the truck at this gate, right now" -- `GetActiveManifestFor` needed a
round id nothing supplied it with. Closed by correlating through the DC's
**existing WMS/TMS dock appointment scheduling system** rather than
inventing a new one:

1. WMS/TMS confirms a dock appointment (PO ref, site, `gate_id`, scheduled
   window). The **WMS Adapter** -- previously push-only toward WMS
   (`staging/interface tables`) -- gains a reverse-sync responsibility: it
   reads appointment confirmations back and publishes
   `DockAppointmentConfirmedEvent` onto Kafka. This is the *only* new
   integration surface this addendum requires.
2. Serialization Service consumes it, joins to the matching
   `MovementManifest` by `PoRef`, and republishes as an ordinary
   `ManifestCreatedOrUpdatedEvent` with `GateId`/`ScheduledWindowStart`/
   `ScheduledWindowEnd` populated -- a higher `Version` of a manifest that
   likely already exists in cache from ASN pre-positioning (Addendum 3).
   No new event type reaches the edge; no new pre-positioning pipeline --
   this rides the version-ordering mechanism Addendum 1 already built.
3. At the gate, `GateSession(..., movementRoundId: null, ...)` resolves via
   `GetActiveManifestForGate(siteId, gateId, openedAt)`.

**Ambiguity is deliberately not distinguished from absence** at the
`GateSession` level: if two appointments' windows overlap the same gate
(should be rare -- that's what dock scheduling exists to prevent, but not
impossible), `GetActiveManifestForGate` returns `null` exactly as it would
for "nothing scheduled," and the existing `FailSafeMode` fallback applies
unchanged. Surfacing *which* case occurred is an ops alerting concern
layered on top, not a fact `GateSession` needs to reason about.

**Confidence**: high on reusing WMS/TMS rather than building new scheduling
(the org already runs one) and on riding the existing version-ordering
pipeline; medium on the overlap-window grace period (how early/late a truck
can arrive and still match its window) -- that parameter needs an
operations answer, not an architectural one.

**D032 Addendum 6 (2026-08-12)**: Addendum 5 quietly assumed the ASN always
arrives before its dock appointment confirmation -- true often enough to
miss in a first pass, but not guaranteed: DCs commonly book a dock slot as
soon as a PO is issued, before the supplier has submitted an ASN at all. If
`DockAppointmentConfirmedEvent` arrived with no `MovementManifest` yet for
that `PoRef`, the original join found nothing to attach to and the
appointment was silently lost -- reopening exactly the gap Addendum 5 had
just closed, for early-booked shipments specifically.

Fixed by making Serialization Service's join **order-independent**: an
appointment with no matching manifest yet is staged as a
`PendingDockAppointment` (keyed by `PoRef`) instead of discarded; when the
ASN later creates the manifest, Serialization Service checks staging first
and populates `GateId`/window directly into the first `manifest.created`
(no follow-up `manifest.updated` needed for this ordering). The
already-designed direction -- ASN first, appointment joins via
`manifest.updated` -- is unchanged. This staging is entirely internal to
Serialization Service: `ManifestSyncConsumer`, `IManifestCache`, and
`GateSession` never see it and required zero changes -- by the time either
event reaches the edge it is always a complete, self-consistent manifest,
regardless of which upstream order produced it.

One thing this addendum flags without solving: a `PendingDockAppointment`
whose ASN never shows up (cancelled shipment, PO reissued) has no natural
expiry here -- needs a retention/purge policy, but the actual duration is
an operations question.

**Confidence**: high -- staging an unmatched event until its counterpart
arrives is the standard fix for an order-independent join between two
processes with no enforced sequencing between them.

**D032 Addendum 7 (2026-08-12)**: no code change here -- `gate_id` is
already just an opaque `string` this snippet passes around
(`GateSession`'s constructor param, `MovementManifest.GateId`,
`DockAppointmentConfirmedEvent.GateId`) and the existing `FailSafeMode`
fallback already handles "resolution failed" correctly regardless of *why*.
But two assumptions surfaced tracing where that string actually comes from,
both operational rather than architectural: (1) `gate_id` is assigned once
at physical gate provisioning (Device/Config Registry, delivered to the
edge via the normal `ConfigPoller` config poll) -- it must be the *same*
identifier space `dock.appointment.confirmed` uses for WMS/TMS's dock
doors, or the Addendum 5/6 join never matches, silently, for every
inbound/outbound gate; (2) the whole mechanism assumes every inbound PO
gets a dock appointment confirmed in WMS/TMS -- if some shipment class
doesn't (walk-ins, urgent deliveries), those POs never get a `gate_id` and
permanently fall back to `FailSafeMode`. Neither is fixable in code; both
need an operations answer before pilot. Logged as `P027` open items #11 and
#12.

**Fourth flow discovered while documenting event payload schemas
(2026-08-12)**: designing a JSON schema for `store.received` (Store
Backroom Inbound + Geiger Search, flow #9 -- sled scans through closed
boxes, compares against a manifest the source DC pre-positioned, and opens
a "Geiger mode" search when something's missing) surfaced that it is
*exactly* the same shape as `GateSessionResult` -- the "missing item, go
search for it" behavior is literally the `MissingExpectedEpcs` case this
snippet already computes. `GateSession` was never formally extended to this
flow even though nothing about it requires physical gate hardware --
`GateSession` was already scoped to "one evaluate-a-batch-against-an-
expected-list session," not to a fixed reader. Flow #9 is now the fourth
flow this component serves, alongside internal/inter-site transfer, inbound
ASN, and outbound pick-verify. See `manual/rfid-component-reference.md`
component #3c and § ภาคผนวก 4 for the full payload catalogue.

**D032 Addendum 9 (2026-08-12) -- a bug, not just a documentation gap.**
Addendum 5/6 repeatedly describe Serialization Service joining
`dock.appointment.confirmed` to a `MovementManifest` "by PoRef" -- but
`MovementManifest` never actually declared a `PoRef` field until now. The
join as previously coded literally could not have worked; there was nothing
on the manifest side to match `DockAppointmentConfirmedEvent.PoRef`
against. Found while discussing an unrelated flow change (entering the PO
number explicitly when creating a `MovementManifest` for internal/inter-site
transfer -- operations already has this number at planning time).

Fixed by adding `MovementManifest.PoRef` (nullable `string`) and the
matching field on `ManifestCreatedOrUpdatedEvent`. Populated by the
supplier's ASN for `InboundAsn` (already the case in practice, just never
modeled), and by Ops/WMS at manifest creation for `IntraSite`/`InterSite` --
the flow change that surfaced this bug. Nullable because a pure intra-site
zone move may have no PO-like document at all; for those, dock-appointment
correlation simply isn't available on that manifest, and `movementRoundId`-
based resolution (unaffected by this change) remains how `GateSession`
resolves it.

**Deliberately not changed**: `GateSession`'s resolution logic itself.
`movementRoundId` stays the primary key for internal transfer -- `PoRef`
existing on more manifests doesn't mean every internal transfer should
switch to gate+time resolution, since intra-site moves generally have no
real "dock appointment" concept to correlate against in the first place.
`PoRef` on `MovementManifest` exists specifically to make the
already-documented dock-appointment join actually possible, not to replace
`movementRoundId` as `GateSession`'s resolution strategy.

**Confidence**: high that this was a real bug (not just imprecise wording --
the field was structurally absent). Medium-high that keeping
`movementRoundId` as primary for internal transfer is correct: an
inter-site transfer that *does* route through a real dock could
alternatively resolve via `GetActiveManifestForGate` now that `PoRef` is
available, but that's an option to consider case by case, not a default
switch made here.

**D032 Addendum 10 (2026-08-12): `IEpcGtinResolver` Must Validate Scheme Before Decoding, and Should Support Two Schemes, Not One.**

**Context**: every `ExtractGtin(epc)` call site in this snippet, and every
piece of GTIN-based logic downstream (`tracking_mode`, `item_master`
lookup), silently assumed the EPC is always SGTIN-96. That assumption was
never validated. GS1's EPC Tag Data Standard defines several schemes beyond
SGTIN-96 -- SGTIN-198 (also GTIN-bearing, but with a wider/alphanumeric
serial field instead of SGTIN-96's 38-bit numeric one), and several
schemes that carry no GTIN at all: SSCC-96 (shipping container), GRAI
(returnable asset), GIAI (individual asset), SGLN (location). Decoding a
non-SGTIN EPC as if it were SGTIN-96 would not throw -- it would silently
produce a plausible-looking but meaningless "GTIN" from unrelated bits,
which could coincidentally collide with a real GTIN in `item_master`. This
is strictly worse than an explicit error, and was completely unguarded
until now.

**Why this risk is not hypothetical**: SSCC in particular is the standard
scheme for pallet/carton-level logistics tagging -- a natural next step for
any organization that starts with item-level RFID (this platform's entire
scope so far) and later adds case/pallet tagging too. A gate could
plausibly read SGTIN tags on individual garments *and* an SSCC tag on the
pallet wrap in the same pass, today's design has zero defense against that.

**Decision, part 1 -- validate Header before decoding.** `ExtractGtin`
must read the EPC's Header field first and dispatch based on declared
scheme, never assume SGTIN-96's bit layout blindly. Anything that isn't
SGTIN-96 or SGTIN-198 throws `UnsupportedEpcSchemeException`.
`GateSession.Evaluate()` catches it and returns a new
`GateVerdict.UnsupportedScheme` rather than letting it propagate -- a
foreign-scheme tag must not crash the whole session and take down
evaluation for every other (valid) EPC read in the same batch. This is
the same zero-loss reasoning that shaped every other verdict path: every
read gets *some* verdict, no matter how malformed.

**Decision, part 2 -- support SGTIN-96 *and* SGTIN-198, not just the
former.** The first instinct (raised in discussion, not implemented) was
to reject SGTIN-198 too, reasoning that `serial_range`'s numeric
chunked-allocation design (`bigint`, ranges like "1000-2000") can't
represent SGTIN-198's wider/alphanumeric serial field. That reasoning
doesn't hold up: `epc_registry` keys on the full EPC string, not the
decoded serial value, and SGTIN-96 vs SGTIN-198 tags differ in Header, so
their raw EPC strings can never collide even for "the same" GTIN --
there's no actual collision risk to guard against. A supplier using
SGTIN-198 likely doesn't even need to request a serial range from this
platform at all (their serial space doesn't overlap the internal numeric
one Tagging Station App draws from), so onboarding a second scheme is
mostly localized to this one decode method, not a system-wide redesign.
Everything downstream of `ExtractGtin` (tracking_mode lookup, ASN
matching, item_master enrichment) already operates on GTIN/EPC-as-opaque-
string and doesn't care which scheme produced them.

**Decision, part 3 -- why nothing outside SGTIN-96/198 is worth
supporting.** Every other scheme identifies a fundamentally different kind
of thing (a container, an asset, a place) with no GTIN embedded at all --
not "a different GTIN encoding" but structurally incapable of joining to
`item_master` no matter what decode logic is written. Rejecting them isn't
a policy choice this platform made to reduce scope; there is no way to
make them serve this platform's core need (GTIN-based product
identification) regardless of engineering effort.

**Also worth stating plainly**: no GS1 EPC scheme encodes "GTIN with no
serial number." The S in SGTIN stands for Serialized, and the bit layout
enforces a serial field structurally -- it isn't an optional field. This is
the deeper reason `tracking_mode = CountOnly` was designed as "still a
uniquely-serialized EPC per tag, Serialization Service just chooses to
aggregate rather than track lifecycle" rather than "tags without serials" --
the latter was never achievable within the standard, not merely
undesirable. A business that genuinely doesn't need per-unit
identification should use the barcode (EAN-13/GTIN), not RFID -- RFID's
entire value proposition over barcode is serialization.

See `manual/rfid-component-reference.md` "EPC Tag Data Standards" for the
full scheme reference table and reasoning.

**Confidence**: high on the Header-validation requirement itself (an
unguarded, silently-wrong decode path is a real risk, not speculative).
Medium-high on supporting SGTIN-198 rather than rejecting it -- the
collision-risk reasoning that would have justified rejection turned out
not to apply, but whether any supplier actually needs SGTIN-198 in
practice is still an open business question, not something this addendum
resolves.
