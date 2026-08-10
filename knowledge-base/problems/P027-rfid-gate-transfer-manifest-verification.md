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
2. **No defined mechanism correlates a physical gate pass to the correct
   `movementRoundId`/manifest.** Inbound/outbound flows have an explicit
   trigger (truck docks, sales-order dispatch) that ties a gate event to a
   specific document. Internal transfers have no equivalent described trigger
   -- how the edge knows "this physical pass belongs to round X" (vs. two
   transfers queued close together at the same gate) is undesigned.
   **Second-highest priority** -- a wrong or missing correlation silently
   invalidates the whole manifest check.
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

Next step, when this moves out of design phase: prioritize items 1 and 2
(both directly threaten the zero-delay/zero-loss guarantee itself), and
validate item 5 with operations before committing to an implementation plan.
