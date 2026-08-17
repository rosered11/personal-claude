---
id: P027
title: "RFID Gate Transfer Verification -- Manifest-Based Unregistered Tag Detection for Intra-Site and Inter-Site Movement"
date: 2026-08-10
tags: [rfid, edge-computing, gate-verification, manifest-sync, offline-first, warehouse-management, inter-site-transfer, event-driven-architecture, fail-safe, real-time]
related_decisions: [D032]
related_snippets: [S032]
---

# RFID Gate Transfer Verification -- Manifest-Based Unregistered Tag Detection for Intra-Site and Inter-Site Movement

## Problem

The RFID Event Platform (see `manual/rfid-architecture-summary.md` and
`inbox/RFID/docs/*`) currently documents exactly two gate flows: **inbound
auto-receive** (pallet through gate -> match against ASN/PO -> post GRN) and
**outbound pick-verify** (gate reads pallet -> match against sales order ->
red/green light). Neither flow covers a third, already-occurring business use
case: **internal warehouse/inter-site movement through a gate check** -- moving
RFID-tagged goods between zones inside the same DC (e.g. storage -> staging)
or between sites (DC<->DC, DC<->Store). When a batch of tags passes an RFID
gate during this kind of movement, the platform must immediately flag any EPC
that is not on the expected list ("manifest") for that specific movement
round, at the gate, in real time -- and must never let a single scanned EPC
go unevaluated.

## Root Cause

No existing gate flow models an arbitrary point-to-point movement round as a
first-class concept with its own expected-tag list. The two flows that do
exist are both anchored to an external business document (ASN/PO for inbound,
sales order for outbound) that already carries an expected-item list; internal
transfers have no equivalent document today. Additionally, for inter-site
transfers, the destination site's edge has no mechanism to receive the
expected-tag list ahead of the physical goods arriving -- the platform's
established principle is edge-local, cache-based decisioning with no
synchronous registry calls, but nothing yet defines how a manifest specific to
one movement round gets pre-positioned at a *different* site's edge in time.

## Summary

Business needs gate-level detection of unregistered/unexpected RFID tags
during internal warehouse movement (intra-DC zone-to-zone) and inter-site
transfer (DC->DC, DC->Store), matching each batch pass against a manifest
scoped to that specific movement round -- not a global registry check. The
decision must answer two coupled problems: (1) how to model the "movement
round" and its zero-loss, zero-delay evaluation as a domain concept
consistent with the platform's existing Serialization Service status state
machine, and (2) how to get that manifest to the correct edge -- trivially for
intra-site (manifest is created and consumed at the same site), non-trivially
for inter-site (must be synced to the destination edge before the goods
physically arrive, without a synchronous call). This is the first formal KB
consultation on the RFID Event Platform: the platform itself exists only as
source spec docs (`inbox/RFID/docs/*`) and a plain-English summary
(`manual/rfid-architecture-summary.md`), with no prior P/D/S entry.

## Context

- **Owning platform**: RFID Event Platform (SCM IT), the same 6-layer
  event-driven platform documented in `manual/rfid-architecture-summary.md`:
  Devices -> per-site Edge agent -> Event Processor / Serialization Service /
  API Gateway -> canonical event topics (partitioned by `site_id`) -> thin
  legacy adapters -> legacy systems (WMS, Merchandise, POS, ERP), with a
  Site & Config Service that already pushes edge config on heartbeat poll.
- **Existing gate hardware**: FX9600 gate portal with motion sensor + antenna
  array (already captures a read "session" per physical pass-through); light
  stack alarm (red/green) already exists at the outbound dock as a
  network-independent local alarm mechanism.
- **Existing relevant design principles** (already established, not open for
  re-litigation): no synchronous registry calls from site operations (cache +
  pre-allocated ranges only); gate matching against locally cached ASN/pick
  data (outbound pick-verify precedent); paid-EPC cache pattern at Store
  (EAS decision in <100ms from local cache, offline-safe >=24h).
- **Serialization Service** is the single System of Record for EPC identity/
  status (`epc_registry`: EPC<->GTIN<->serial<->SKU<->status<->site; status
  state machine `encoded -> in_stock -> picked -> shipped -> store_stock ->
  sold`, with `returned`/`voided` exception branches) -- it is the source for
  building a movement manifest, but the gate must never call it synchronously.

## Clarified Scope (already decided, not open questions)

- "Registered" means **manifest-based**: an EPC must be on the expected list
  for *that specific movement round* (analogous to ASN at inbound / sales
  order at outbound) -- **not** a check against the global cross-platform
  registry.
- The gate must support **both** intra-site (same-DC, cross-zone) and
  inter-site (DC->DC, DC->Store) movement.
- Solution must extend/reuse the existing platform (cache + pre-positioned
  data + local gate decisioning), not introduce a new mechanism from scratch.

## Constraints

| Rule | Detail |
|---|---|
| Zero-delay | Gate must decide (pass/alert) at the edge immediately; no round trip to the central Serialization Service. |
| Zero-loss | Every unique EPC read in a gate pass session must be evaluated; none may be dropped by dedupe/debounce. |
| Manifest scope | Must support intra-site (manifest cached at the same site it was created, no sync problem) and inter-site (manifest must be synced to the destination edge before the goods physically arrive). |
| Fail-safe behavior | When decode is ambiguous or no local manifest exists at all (e.g. inter-site and WAN has been down longer than the transit time), there must be an explicit fail-open vs fail-closed policy. |
| No legacy changes | Consistent with the platform's core principle -- legacy systems must not need to know about RFID; everything stays inside the RFID Event Platform + edge. |
| Reuse existing patterns | Should extend the outbound pick-verify precedent (gate + local cached manifest + immediate red/green) rather than invent a new mechanism. |

## Severity

high -- this is a loss-prevention and inventory-integrity control point for
all internal and inter-site product movement, not just a single flow; an
unenforced or delayed check here directly risks undetected shrinkage/
misrouting at exactly the moment goods leave positive control.

## Affected Components

- RFID Event Platform -- Event Processor (business rules / session logic)
- RFID Event Platform -- Serialization Service (source of manifest data, EPC
  identity/status System of Record)
- RFID Event Platform -- Site & Config Service (existing heartbeat-based
  config/edge push mechanism)
- Canonical event topics / message broker (partitioned by `site_id`)
- DC Site Server / Edge agent (local decisioning, offline buffer)
- FX9600 gate portal + light stack alarm (physical gate hardware)

## Open Items (Design Review, pre-implementation — logged 2026-08-10)

D032 was accepted as the primary decision, but a design review before
implementation surfaced gaps that must be closed or explicitly accepted
before pilot. Logged here rather than re-run through the full pipeline
because they refine D032 rather than contradict it.

1. **"Zero-loss" as coded today only covers software-side completeness, not
   physical RF miss-reads.** `GateSession.Close()` guarantees every EPC the
   antenna *actually captured* gets a verdict -- it cannot guarantee every
   physical tag that passed through the gate was captured by RF in the first
   place (tag orientation, occlusion, transit speed all affect real-world read
   rate). If the business requirement "must never lose a tag" is meant to
   include physical miss-reads, this needs a hardware/RF mitigation layer
   (dwell-time tuning, redundant antenna coverage, or a cross-check such as
   expected-item-count vs. read-count) in addition to the software invariant.
   **Highest-priority gap** -- directly affects whether the zero-loss
   constraint is actually satisfied end-to-end.
2. **RESOLVED 2026-08-12 (D032 Addendum 5), inbound/outbound leg only --
   internal-transfer leg remains a smaller residual item (see note).**
   Originally assumed inbound/outbound already had "an explicit trigger
   (truck docks, sales-order dispatch)" -- on inspection that trigger was
   never actually a *system-resolvable key*: nothing gave `GateSession` a
   PO/ASN ref to look up, only a narrative assumption that the edge would
   "somehow know." Closed by correlating through the DC's **existing WMS/TMS
   dock appointment scheduling system**: WMS Adapter gains a reverse-sync
   responsibility (publishes `DockAppointmentConfirmedEvent` once WMS/TMS
   confirms an appointment), Serialization Service joins it to the matching
   `MovementManifest` by PO ref and republishes as a higher-`Version`
   `manifest.updated` carrying `GateId`/scheduled window -- rides the
   existing pre-positioning and version-ordering pipeline (Addendum 1/2),
   no new mechanism. `GateSession` resolves via the new
   `IManifestCache.GetActiveManifestForGate(siteId, gateId, asOf)` instead
   of needing an externally-supplied round id. Ambiguous/overlapping
   appointments are deliberately *not* distinguished from "nothing
   scheduled" at the `GateSession` level -- both fail-safe identically.
   **Residual note**: internal transfer's `movementRoundId` is still
   assumed to reach whoever opens the physical move session by some
   operational means (a screen, a scanned QR code) not specified here --
   smaller in scope than the inbound/outbound gap since Ops/WMS already
   assigns the id explicitly at planning time, but worth a follow-up pass
   if it turns out operations has no such hand-off today.
3. **Alarm-signal design does not yet distinguish failure modes at the
   physical alarm.** `FailSafeMode` (Verified/FailOpen/FailClosed) is captured
   correctly in the audit trail via `gate.transfer.evaluated`, but the light
   stack / audible alarm at the gate itself does not yet differentiate "no
   manifest arrived (system/sync issue)" from "tag genuinely not on manifest
   (real security/inventory event)". Without that split, repeated
   sync-related `FailClosed` alarms risk operator alarm fatigue and eventual
   disregard of real alerts.
4. **`MovementManifest` is resolved once, at `GateSession` construction.** A
   `manifest.updated` event arriving after a session has opened is not picked
   up by that in-flight session. Acceptable for the fast-pass gate case as
   designed, but should be stated as a known limitation rather than left
   implicit, and revisited if any movement type ends up with longer dwell
   times at the gate.
5. **Unverified organizational assumption: manifests must be declared before
   every internal movement.** The design assumes a manifest-creation step
   analogous to ASN (inbound) or sales order (outbound) exists or will exist
   for internal transfers. Unlike inbound/outbound, today's internal
   movement process may have no equivalent "declare before you move" step --
   if so, this is a **process change for DC/store operations**, not only a
   technical addition, and should be validated with operations before
   implementation planning.
6. **RESOLVED 2026-08-10 (see D032 addendum below) -- manifest sync can
   deliver a present-but-incomplete `MovementManifest`.** `IManifestCache`
   only distinguishes "manifest present" (-> `Verified`) from "manifest
   absent" (-> `FailSafeMode`) -- it had no way to detect a manifest that
   arrived but is missing EPCs (truncated payload, an out-of-order
   `manifest.updated` overwriting a more complete version, or a source-side
   assembly bug). This is worse than the fully-missing case from item 3,
   because a corrupted-but-present manifest is trusted as `Verified` with no
   audit signal that anything is wrong -- legitimate EPCs silently missing
   from the manifest get alarmed as `Unexpected` with full (false)
   confidence. Closed by adding completeness proof + version ordering at the
   cache-write boundary -- `GateSession` itself is unchanged.

7. **Inbound ASN match has no independent ground truth -- unlike internal
   transfer manifest match.** For internal/inter-site transfer, the EPCs on
   `MovementManifest` already have a pre-existing `epc_registry` row (they were
   registered against a real prior physical event observed by this platform),
   so a mismatch at the gate is an anomaly against state the platform already
   knows to be true. For inbound auto-receive (source-tagged), the ASN is the
   **first time the platform has ever seen these EPCs** -- no `epc_registry`
   row exists until registration-on-first-read fires *after* the gate match
   succeeds (see `manual/rfid-sequence-diagrams.md` Diagram D). The gate match
   is therefore only a **physical-reality-vs-supplier-claim consistency
   check** (catches shipping errors: short/over count, wrong pallet/PO mixed
   in, damaged/unreadable tags) -- it has **no cryptographic or independent
   verification that the supplier's self-reported EPC list is authentic**. A
   colluding or compromised supplier system could report a fabricated EPC
   list and the gate check would still pass, since it is comparing physical
   reads against that same supplier's own claim. Logged as an accepted
   trade-off (this is an operational-error detector, not a security/anti-
   counterfeit control), but must not be mistaken for the stronger guarantee
   internal transfer matching provides.
8. **RESOLVED 2026-08-12 -- ASN match granularity now follows `tracking_mode`
   per GTIN, not one fixed mode for the whole ASN.** For `CountOnly` GTINs,
   the ASN carries only SKU + expected quantity (reusing the existing
   quantity-level ASN matching pattern already used for untagged goods --
   see `manual/rfid-component-reference.md` component #3c note) and
   `GateSession` does a GTIN-level count reconciliation instead of a
   per-serial match. For `Serialized` GTINs, the ASN must still carry the
   full expected-EPC list and `GateSession` still does per-serial matching --
   collapsing this to count-only for `Serialized` GTINs would defeat the
   reason those GTINs are serialized in the first place (batch/recall
   traceability, per-unit loss prevention) for no runtime savings, since
   `GateSession` already evaluates every physically-read EPC individually to
   satisfy the zero-loss invariant regardless of match granularity. See
   `manual/rfid-sequence-diagrams.md` Diagram D for the branched flow.
9. **RESOLVED 2026-08-12 -- zero-loss did not cover "expected item never
   physically present at all."** `GateSession.Close()`'s zero-loss check
   guarantees every EPC the gate *read* gets a verdict -- it said nothing
   about `Serialized`-mode expected EPCs that were never read at all (e.g. an
   entire pallet routed to the wrong DC/PO, with no substitute tag physically
   present to trip `Unexpected`). Before the fix, 400 EPCs read against an
   expected 500 produced 400 clean `Expected` verdicts with no signal that
   100 were missing -- a silent shortage. Distinct from item #1 (RF
   miss-read): item #1 is a tag that was physically present but the antenna
   failed to read; this is goods that were never physically present on the
   pass-through to begin with, a shipment/business-integrity gap, not a
   hardware one. Closed by adding `GateSession.ComputeMissingExpectedEpcs()`
   at `Close()` (symmetric to the `CountOnly` reconciliation from item #8,
   just at EPC instead of GTIN granularity), threaded through
   `GateSessionResult` and `gate.transfer.evaluated`. **Not** a real-time/
   alarm signal -- "missing" can only be known after the whole pass-through
   completes, by which point the truck/forklift has moved on, so it must not
   drive the physical light stack. Downstream consequence that makes the fix
   real rather than theoretical: the WMS Adapter must post GRN against the
   *actual* received quantity/EPCs when `MissingExpectedEpcs` is non-empty,
   flagged as an exception for review -- not silently auto-post the full
   ASN-declared quantity. Applies to all three `GateSession` flows (transfer,
   inbound ASN, outbound pick-verify), not only inbound receiving. See
   `knowledge-base/decisions/D032-*.md` Addendum 4 and
   `knowledge-base/snippets/S032-*/code.cs`.
10. **RESOLVED 2026-08-12 (D032 Addendum 6) -- item 2's fix silently assumed
    ASN always arrives before its dock appointment confirmation.** Not
    guaranteed: a DC can book a dock slot as soon as a PO is issued, well
    before the supplier submits the corresponding ASN. If
    `dock.appointment.confirmed` reached Serialization Service before any
    `MovementManifest` existed for that `PoRef`, the join had nothing to
    attach to and the appointment was silently dropped -- reopening item 2's
    gap specifically for early-booked shipments. Closed by staging unmatched
    appointments as `PendingDockAppointment` (keyed by `PoRef`) instead of
    discarding them; when the ASN later creates the manifest, Serialization
    Service checks staging first and populates `GateId`/window directly into
    the first `manifest.created`. Entirely internal to Serialization
    Service -- `ManifestSyncConsumer`/`IManifestCache`/`GateSession` need no
    changes for either ordering. Unresolved sub-point: a staged appointment
    whose ASN never arrives (cancelled shipment, PO reissued) has no expiry
    policy yet -- needs an operations-defined retention window, not an
    architectural answer.

11. **`gate_id` namespace must match WMS/TMS's dock-door numbering, and
    nothing currently guarantees that.** `gate_id` is assigned at physical
    gate provisioning in the Device/Config Registry (component #9);
    `dock.appointment.confirmed` (item 2's fix) carries a `gate_id` sourced
    from WMS/TMS's own dock scheduling data instead. If the two are
    different identifier spaces, the join item 2/10 depends on silently
    never matches -- no error, just permanent `FailSafeMode` for every
    inbound/outbound gate. Recommend registering gates using WMS/TMS's
    existing dock-door identifiers directly as `gate_id` rather than minting
    a separate numbering scheme, to eliminate the mismatch risk by
    construction rather than requiring a mapping table to be built and kept
    in sync. See `knowledge-base/decisions/D032-*.md` Addendum 7.
12. **Every inbound/outbound PO is assumed to get a dock appointment
    confirmed in WMS/TMS -- unvalidated with operations.** If some class of
    shipment doesn't go through dock scheduling in practice (small/urgent
    deliveries, walk-in trucks), `dock.appointment.confirmed` never arrives
    for those POs, `gate_id` is never populated, and resolution permanently
    falls back to `FailSafeMode` for that entire class of shipment, every
    time. Architecturally identical in shape to item 5 (unvalidated
    "manifest declared before every movement" assumption) -- same category
    of risk, different upstream process. See `knowledge-base/decisions/
    D032-*.md` Addendum 7.

13. **RESOLVED 2026-08-12 (D032 Addendum 9) -- the item 2/10 dock-appointment
    join was never actually implementable, for ANY flow, until this fix.**
    Found while discussing an unrelated process change (Ops/WMS entering the
    PO number explicitly at `MovementManifest` creation for internal
    transfer). Addendum 5/6 both describe Serialization Service joining
    `dock.appointment.confirmed` to a `MovementManifest` "by PoRef" -- but
    `MovementManifest` never declared a `PoRef` field. This was not
    imprecise wording; the join as coded had nothing on the manifest side to
    match `DockAppointmentConfirmedEvent.PoRef` against, so it could not
    have worked for inbound ASN either, despite item 2 being marked resolved.
    Closed by adding `MovementManifest.PoRef` (nullable), populated by the
    supplier's ASN for `InboundAsn` (already true in practice, just never
    modeled) and by Ops/WMS at creation for `IntraSite`/`InterSite` (the
    surfacing process change). `GateSession`'s resolution strategy is
    unchanged -- `movementRoundId` stays primary for internal transfer;
    `PoRef` only makes the already-documented join structurally possible.
    See `knowledge-base/decisions/D032-*.md` Addendum 9.

Next step, when this moves out of design phase: prioritize item 1 (directly
threatens the zero-loss guarantee itself), and validate items 5 and 12 with
operations before committing to an implementation plan (same category of
risk -- unverified "always happens before the gate event" assumptions).
Items 6, 8, 9, 10, 13 are resolved at the design level (see D032 and S032);
item 2 is resolved for inbound/outbound (see D032 Addendum 5/6/9) with a
smaller residual noted for internal transfer; items 1, 3, 4, 5, 7, 11, 12
remain open.
