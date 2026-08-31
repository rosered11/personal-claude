---
id: P030
title: "RFID Inbound Gate/Session Correlation Without Dock Scheduling -- Receiving-Zone-to-PO Matching Invalidates D032 Addendum 5/6/7's Universal Assumption"
date: 2026-08-24
tags: [rfid, edge-computing, gate-verification, manifest-sync, offline-first, warehouse-management, inbound-receiving, dock-scheduling, fail-safe, operational-validation]
related_decisions: [D035]
related_snippets: [S035]
---

# RFID Inbound Gate/Session Correlation Without Dock Scheduling -- Receiving-Zone-to-PO Matching Invalidates D032 Addendum 5/6/7's Universal Assumption

## Problem

D032 Addendum 5/6/7 built inbound/outbound gate correlation entirely on top of
WMS/TMS dock-door scheduling: the WMS Adapter reverse-syncs
`dock.appointment.confirmed` (`PoRef`, `SiteId`, `GateId`, `ScheduledWindow`),
Serialization Service joins it to the matching `MovementManifest` by `PoRef`
and republishes a higher-`Version` `manifest.updated` carrying `GateId`/
`ScheduledWindow`, and `GateSession` resolves the active manifest via
`IManifestCache.GetActiveManifestForGate(siteId, gateId, now)` instead of
needing an externally-supplied reference. P027 Open Item #12 explicitly
flagged this as an **unvalidated organizational assumption**: "every inbound/
outbound PO is assumed to get a dock appointment confirmed in WMS/TMS --
unvalidated with operations."

A real warehouse site visit -- asking exactly the P027 #11/#12 open
questions -- returned this answer verbatim: **"dock ไม่จำเป็นต้องมี เนื่องจากของที่ได้รับ
จะมากองอยู่ที่ zone เอาของเข้า แล้วจะเอาของเข้าตาม PO"** (there is no dock-scheduling
concept in this operation at all; incoming goods are unloaded and staged in a
general receiving zone, and correlation to a specific PO happens afterward,
when staff process that pile of goods against a PO they select/know -- not by
matching a physical gate + time window to a WMS/TMS dock appointment).

This is not a missing-detail gap: it means the entire `dock.appointment.confirmed`
pipeline (D032 Addendum 5/6/7) will never fire for this operational model.
`GateId`/`ScheduledWindow` will never be populated on any inbound
`MovementManifest` at this site, so `GetActiveManifestForGate` permanently
returns `null` and every inbound session falls back to `FailSafeMode` -- not as
an edge case, but as the **normal, universal case** for this warehouse's
entire inbound flow.

A related clarification from the same site visit, relevant to the
replacement design: completeness verification is scoped to each individual
partial delivery, not to the PO's total quantity -- if a PO is fulfilled
across multiple truck arrivals, each arrival/ASN is checked for its own
completeness independently, not accumulated against the PO's full ordered
quantity. This means the existing `GateSession`/`ComputeMissingExpectedEpcs()`
completeness check (D032 Addendum 4) already fits, provided each partial
delivery resolves to its own correct `MovementManifest` instance -- the open
question is only how that manifest gets resolved/opened at the zone without a
dock-appointment trigger, not how completeness is judged once it is open.

## Root Cause

The inbound/outbound correlation mechanism built in D032 Addendum 5/6/7
resolves exclusively through a `gate_id` + scheduled-window key sourced from
WMS/TMS dock-appointment data. That key structurally cannot exist for an
operational model with no dock-scheduling concept -- there is no appointment
to confirm, no gate/door assignment tied to a time window, and therefore no
event for the WMS Adapter to reverse-sync in the first place. The mechanism
was never wrong for the operational model it was designed against (DCs that
do run WMS/TMS dock scheduling); the gap is that Addendum 5/6/7 was treated
as *the* inbound/outbound resolution mechanism rather than *a* resolution
mechanism valid only under an assumption (P027 Open Item #12) that had never
been checked against real operations -- and has now been checked, and found
false for at least one real site.

## Summary

Business needs a replacement inbound gate/session correlation mechanism for a
warehouse with **no dock-scheduling system**: goods are staged at a general
receiving zone and matched to a PO afterward by staff, not via a physical
gate + scheduled-window match. The decision must explicitly answer three
coupled questions: (1) what reference opens a `GateSession`/scan session at
the zone if not `gate_id`+`ScheduledWindow` -- is it a reuse of the same
"human-supplied/selected operational reference" shape `movementRoundId`
already proves for internal transfer? (2) should D032 Addendum 5/6/7 be
superseded, deprecated as one-of-N supported paths, or kept as a fully
equal alternative for sites that do use dock scheduling? (3) how does the
zero-loss/`MissingExpectedEpcs` completeness check keep working correctly
per-partial-delivery under whatever new resolution mechanism is chosen? This
is the platform's fourth formal RFID Event Platform consultation, and the
first one that revisits and partially invalidates a specific prior decision
(D032 Addendum 5/6/7) rather than extending the platform into new,
previously-undesigned territory.

## Context

- **Owning platform**: RFID Event Platform (SCM IT), see
  `manual/rfid-architecture-summary.md` and `manual/rfid-component-reference.md`
  § ภาคผนวก 3 "Gate correlation via dock appointment."
- **Prior RFID KB entries**: P027/D032/S032 (gate/manifest verification,
  including Addendum 5/6/7's dock-appointment correlation and Addendum 9's
  `MovementManifest.PoRef` field), P028/D033/S033 (edge-to-Ingestion-Service
  WAN transport), P029/D034/S034 (store returns reverse flow). This problem
  is a direct, targeted refinement of P027/D032 -- specifically the
  inbound/outbound leg of Addendum 5/6/7 -- not a new flow or a new platform
  area.
- **What already works and is not being touched**: `GateSession`'s zero-loss
  (`Close()` throws unless every recorded EPC has a verdict), zero-delay
  (synchronous local evaluation), and fail-safe (`FailSafeMode` resolved once
  at session open) invariants; `MovementManifest.PoRef` (D032 Addendum 9),
  populated by the supplier's ASN for `InboundAsn` manifests; the
  `ComputeMissingExpectedEpcs()`/`ReconcileCountOnlyGtins()` per-session
  completeness reconciliation (D032 Addendum 3/4); `GetActiveManifestFor(
  siteId, movementRoundId)`, the existing generic "resolve by an opaque,
  ops-known key" path already used for internal/inter-site transfer.
- **What this problem targets specifically**: `IManifestCache.
  GetActiveManifestForGate(siteId, gateId, asOf)` (D032 Addendum 5) is the
  *only* resolution path ever built for Inbound Auto-Receive/Outbound
  Pick-verify, and it is now confirmed non-functional, by design, for any
  site without WMS/TMS dock scheduling.

## Clarified Scope (already decided, not open questions)

- Do **not** discard `MovementManifest.PoRef` (D032 Addendum 9) or the
  zero-loss/`MissingExpectedEpcs` completeness check (D032 Addendum 4) --
  both stay as-is and already produce correct per-delivery verification once
  a session is opened against the right manifest.
- Do **not** assume this generalizes to every DC in the platform -- this is
  validated field data from one site visit. The replacement must resolve
  **this operational model specifically** (receiving-zone-then-match-to-PO),
  and must state whether/how a dock-scheduling-based DC could still be
  supported if one exists elsewhere.
- Internal/inter-site transfer (`movementRoundId`-based resolution, D032
  core) is unaffected and out of scope -- this problem is specifically about
  the inbound leg Addendum 5/6/7 targeted.

## Constraints

| Rule | Detail |
|---|---|
| Zero-delay / zero-loss | Whatever replaces the dock-correlation resolution must still let `GateSession` evaluate synchronously at the edge with no round trip, and must not weaken the existing completeness guarantees. |
| No re-introduction of a scheduling system | The platform's Addendum 5 design principle (reuse existing systems, don't invent new ones) still applies -- do not build a net-new dock/appointment system just to recreate what Addendum 5 avoided duplicating. |
| Must resolve per-partial-delivery, not per-PO-total | Each arrival is its own completeness check -- the resolution mechanism must distinguish which specific delivery/manifest a receiving session belongs to, not just "which PO." |
| Reuse existing patterns where possible | `GateSession`'s `movementRoundId`-based resolution already solves "resolve by an operationally-known reference" for internal transfer -- evaluate whether the same pattern (a human-supplied/selected reference at receiving-zone processing time) fits here, rather than inventing something new. |
| Fail-safe behavior preserved | If no valid reference is supplied or it doesn't resolve, the existing `FailSafeMode` fallback must still apply -- this is about fixing the *normal* path, not removing the safety net. |

## Severity

high -- for this warehouse, 100% of inbound receiving currently degrades to
`FailSafeMode` on every session, every time, since Addendum 5/6/7's
resolution key can never be populated under this operational model. Unlike
P027's original open items (partial/edge-case gaps), this is not a residual
risk to monitor -- it is the platform's sole inbound correlation mechanism
being confirmed completely non-functional for a real, already-operating site.

## Affected Components

- RFID Event Platform -- Event Processor (`GateSession`, `IManifestCache`)
- RFID Event Platform -- Serialization Service (manifest creation/join logic)
- RFID Event Platform -- Site & Config Service (existing heartbeat-based
  edge config push -- carries the new per-site correlation-mode flag)
- WMS Adapter (dock-appointment reverse-sync -- becomes optional per site)
- DC receiving-zone staff application / handheld reader (new call site;
  outside RFID platform's own scope, but this decision defines the contract
  it depends on)

## Open Items (flagged by decision-synthesizer, logged 2026-08-24)

D035 was accepted as the decision, but the following are explicitly not
resolved by it and should be tracked before/during rollout:

1. **`MovementManifest.ConsumedAt` reliability.** The new field must be set
   on every successful `GateSession.Close()` that resolved via the zone-
   receiving path, or already-processed partial deliveries will keep
   resurfacing in the staff picklist. A missed write is a UX/ops annoyance,
   not a correctness break (zero-loss/fail-safe are unaffected either way),
   but should be monitored.
2. **Staff picklist UX for ambiguous `PoRef` matches is not designed here.**
   When `GetPendingManifestsByPoRef` returns more than one candidate, this
   decision only specifies that selection must be explicit (never
   auto-guessed) -- the actual disambiguation UI (which fields help staff
   tell two pending deliveries apart) is a receiving-zone application
   question, not resolved by this platform-side contract.
3. **Per-site `inbound_correlation_mode` config schema/ownership** needs a
   concrete home in Site & Config Service (naming, validation, default
   value, migration for existing sites) -- named as a requirement here, not
   fully specified.
4. **Retention/expiry for never-consumed manifests at zone-receiving sites**
   is unresolved, the same open question already flagged for
   `PendingDockAppointment` in D032 Addendum 6 -- a manifest whose delivery
   is cancelled or never processed will sit in the pending picklist
   indefinitely without an ops-defined purge policy.
5. **Whether other DCs beyond this one site visit also lack dock
   scheduling is unvalidated**, per the Clarified Scope's explicit
   instruction not to assume this generalizes -- a broader operations
   survey is needed before treating `ZoneReceiving` as anything more than a
   second supported mode alongside `DockAppointment`.
6. **P027 Open Item #11** (`gate_id` namespace must match WMS/TMS's
   dock-door numbering) is untouched by this decision and remains fully
   open for any site that keeps the `DockAppointment` resolution mode.
7. **Fail-safe policy tuning for the new mode is not addressed.** Whether
   `FailOpen`/`FailClosed` should default differently for zone receiving
   (goods already physically staged, arguably lower urgency than a live
   truck at a dock) versus dock-scheduled receiving is an operations-tunable
   parameter, not decided here.

**Status**: P027 Open Item #12 ("every inbound PO is assumed to get a dock
appointment -- unvalidated") is now **resolved for this site, and generalized
into a supported second mode** by D035 -- see
`knowledge-base/decisions/D035-*.md`.
