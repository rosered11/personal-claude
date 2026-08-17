---
id: P029
title: "RFID Store Returns -- Reverse Flow for `returned` State, Paid-EPC Cache Removal, and Cross-Store Return Validation"
date: 2026-08-17
tags: [rfid, returns, fraud-prevention, offline-first, edge-computing, cache-invalidation, event-driven-architecture, saga-pattern, loss-prevention, state-machine, retail, eas]
related_decisions: [D034]
related_snippets: [S034]
---

# RFID Store Returns -- Reverse Flow for `returned` State, Paid-EPC Cache Removal, and Cross-Store Return Validation

## Problem

The RFID Event Platform's `epc_registry` status state machine (see
`manual/rfid-component-reference.md` § ภาคผนวก 2) already names `returned` as
an exception branch ("ลูกค้าคืนสินค้าที่เคยขายไปแล้ว" -- a customer returning
something previously sold) that "reverts back into the cycle," distinct from
`voided`'s dead end. Every other store/DC flow in the platform has a fully
drawn sequence diagram, event payload schema, and operational-flow writeup
(`manual/rfid-operational-flows.md`, `manual/rfid-scenario-walkthrough.md`,
`manual/rfid-sequence-diagrams.md` Diagrams A-J) -- returns has none of these.
It is a named state with zero designed flow behind it.

While drawing Diagram J (EAS Exit Check), this surfaced as more than a
documentation gap: `manual/rfid-scenario-walkthrough.md` Stage 9 and Diagram I
both confirm that a successful sale immediately **adds** the sold EPC to the
Store Gateway's local paid-EPC cache (so EAS can decide offline in <100ms).
Nothing anywhere in the spec **removes** an EPC from that cache on return. If
a returned item is put back on the sales floor for resale without this
mechanism, its EPC stays permanently marked "paid" in the cache -- it will
walk through EAS silently forever after, regardless of whether it is ever
paid for again. The platform's own anti-theft control is provably ineffective
against any item that has ever been returned once.

## Root Cause

Returns were never modeled as a first-class flow. The state machine table
names `returned` as a destination but no component owns triggering the
transition, no event schema exists for it, and -- critically -- the paid-EPC
cache was designed with only an additive write path (`item.sold` -> add to
cache), never a corresponding removal path. Five concrete design questions
were never answered even once in the source spec:

1. Where can a return happen -- only the store that made the original sale,
   or any store (cross-store return)?
2. What event/actor transitions `epc_registry.status` to `returned`, and
   what state does it move to next -- straight back to `store_stock`
   (resellable immediately), or through an inspection/condition-check branch
   first?
3. When and how does the local paid-EPC cache get the EPC removed -- and for
   a cross-store return, the receiving store's cache never held that EPC to
   begin with (it was sold at a different Store Gateway's cache), so what
   actually needs reconciling, and does validating "this was genuinely sold"
   require knowing what a *different* site did?
4. Does `tid_registry` (anti-counterfeit binding, currently unused by any
   live flow) apply at return intake -- returns are a well-known fraud vector
   (cloned/counterfeit tags presented for refund)?
5. `tracking_mode = CountOnly` GTINs (see Dual EPC Tracking Mode decision,
   `manual/rfid-architecture-summary.md` §3) deliberately do not track
   per-EPC lifecycle to save storage/event overhead -- but validating a
   return requires knowing that *this specific serialized EPC* was part of a
   real prior sale, which is exactly the kind of per-item history CountOnly
   was designed to avoid persisting.

The central architectural tension: cross-store return validation needs to
know what a *different* site did (was this EPC really sold, and where) --
but the platform's core design principle, applied consistently in GateSession
(P027/D032), EAS, and checkout, is **no synchronous registry calls from site
operations**, only local cache + pre-positioned data. Returns are the first
flow where "verify against another site's truth" and "never call another
site synchronously" collide head-on, and something has to give.

## Summary

Design the return/refund flow for the RFID Event Platform, answering all five
open questions above, with special attention to the cross-store validation
vs. no-synchronous-call tension. Must close the real loss-prevention gap
(paid-EPC cache has no removal path) without silently reintroducing a
synchronous call into the checkout/EAS critical path those flows were
explicitly designed to avoid.

## Context

- **Owning platform**: RFID Event Platform (SCM IT), same platform as
  P027/D032 (gate/manifest verification) and P028/D033 (edge-to-Ingestion WAN
  transport) -- this is the platform's third formal KB consultation.
- **`epc_registry` status state machine** (Serialization Service, System of
  Record): `encoded -> in_stock -> picked -> shipped -> store_stock -> sold`,
  with `voided` (dead end) and `returned` (loops back) as exception branches.
- **Paid-EPC cache** (Store Gateway, edge-local): populated by the POS bridge
  the instant a basket checkout payment succeeds (`item.sold` -> add EPC);
  EAS exit check (Diagram J) decides pass/alarm from this cache alone, in
  <100 ms, fully offline-capable for >=24h -- this is the component with the
  missing removal path.
- **`tid_registry`**: exists since the platform's original design
  (`tid` <-> `epc` binding at encode time, for high-value SKU anti-counterfeit)
  but, per prior consultations, has never had a live flow that actually reads
  it -- this is the first candidate flow.
- **`tracking_mode` per GTIN** (`Serialized` | `CountOnly`, Serialization
  Service-owned): `Serialized` GTINs keep the full state machine per EPC;
  `CountOnly` GTINs are still individually serialized tags (SGTIN standard
  requires this) but Serialization Service deliberately does not persist a
  full lifecycle per EPC, aggregating to a GTIN-level quantity instead.
- **Kafka canonical event topics, partitioned by `site_id`**, are the only
  cross-site data path in this platform; Kafka never crosses the WAN (D032
  Addendum 2) -- edges only ever receive data via HTTPS/mTLS poll against a
  central cache (Site & Config Service), matching D033's WAN-transport
  decision.
- **GateSession** (P027/D032/S032) is the platform's existing precedent for
  "evaluate a batch against a scoped expected list, locally, with a defined
  fail-safe behavior" -- but it has no natural "expected list" analog for
  returns (there is no pre-existing manifest of "items about to be
  returned"), so it is not a direct fit without adaptation.

## Clarified Scope

- Both same-store and cross-store returns are in scope -- the problem
  explicitly does not get to assume returns only happen at the original
  point of sale; real retail return policy commonly allows cross-store
  returns, and the request explicitly names this as the open question to
  resolve, not avoid.
- The design must extend `epc_registry`/Kafka event patterns already
  established rather than invent a parallel mechanism where the existing one
  can be reused (Constraint: "Reuse ของเดิม").
- The design must not require legacy POS/WMS/ERP systems to learn anything
  new about RFID beyond what they already integrate with via existing thin
  adapters.

## Constraints

| Rule | Detail |
|---|---|
| Offline-safe (existing flows) | Checkout and EAS must continue to depend on zero network calls, exactly as designed today -- the returns solution must not leak a new synchronous dependency onto those two flows. |
| Fraud-aware | Returns carry real refund/money-out risk that sale flows do not; an unverified return is a materially worse risk than an unverified sale and must be treated accordingly. |
| Reuse existing mechanisms | Extend `epc_registry`'s state machine, the existing Kafka topic/partitioning pattern, and the paid-EPC cache's existing add/EAS-check mechanism rather than building a parallel system. |
| Close the loss-prevention gap for real | Must concretely specify how the paid-EPC cache gets reconciled after a return, for both same-store and cross-store cases -- "documented but unenforced" is not an acceptable outcome given this is a known, already-occurring gap. |
| No legacy changes | Consistent with the platform's core principle -- legacy POS/WMS/ERP need no new RFID awareness beyond what already exists. |

## Severity

high -- this is an active loss-prevention control failure (not a theoretical
gap): any item returned and put back for resale today would silently defeat
the store's EAS anti-theft check for the rest of its lifetime in that store,
and the fraud surface returns introduce (cloned tags, cross-store "return
what was never bought here" schemes) has no verification of any kind today.

## Affected Components

- RFID Event Platform -- Serialization Service (`epc_registry` status owner,
  `tid_registry`, `tracking_mode` per GTIN)
- RFID Event Platform -- Store Gateway edge agent (paid-EPC cache, POS
  bridge, EAS decisioning)
- Canonical event topics / message broker (partitioned by `site_id`)
- POS (via existing thin adapter -- refund transaction unaffected, RFID
  platform only observes/reacts)
- Loss Prevention / store operations (manual review workflow for held/
  flagged returns)

## Open Items (Design Review, pre-implementation -- logged 2026-08-17)

D034 was accepted as the primary decision, but the following were identified
as unresolved during synthesis and should be closed or explicitly accepted
before pilot -- logged here rather than re-run through the full pipeline,
matching the practice established in P027.

1. **`PendingVerification` retry SLA and manager-override timing are
   unspecified.** D034 names the mechanism (bounded auto-retry, then
   escalate) but not the actual duration -- this is an operations/Loss
   Prevention decision, not an architectural one, and directly affects how
   often store staff hit the manager-override path in practice.
2. **Whether cross-store returns are current or planned business policy was
   not validated with retail operations.** The design supports both
   same-store and cross-store returns per the Clarified Scope, but if the
   business does not actually allow cross-store returns today, the entire
   `CrossStore` branch (and its one exception to the no-sync-call principle)
   may be unnecessary scope -- validate before committing to build it.
3. **`ISoldEpcLedger` retention window (30-90 days suggested) is a
   placeholder, not a validated value.** Needs an actual answer from store
   return policy; too short risks legitimate returns falling through to the
   manual-exception path unnecessarily, too long re-accumulates the
   per-EPC storage cost `CountOnly` was designed to avoid.
4. **`ILocalTidBindingCache` population reliability for store-tagged items
   is assumed, not confirmed.** Backroom tagging (an existing store flow)
   is assumed to populate this cache at encode time, but this was not
   traced end-to-end during this consultation -- if it does not, every
   SameStore return of a store-tagged, high-value SKU silently falls back
   to "no TID check performed" rather than an explicit gap being surfaced.
5. **`FraudHold` resolution workflow (how Loss Prevention actually clears a
   held item back to sellable or removes it permanently) is named as a
   destination but not designed.** D034 deliberately scoped this out as an
   operational process, not an architectural gap, but it must exist before
   pilot or held items accumulate with no exit path.
6. **Refund timing relative to `ReturnSaga`'s verdict was assumed, not
   confirmed against actual POS refund-transaction sequencing.** The design
   assumes the POS refund step can be gated behind `RefundAuthorized` from
   `ReturnSagaResult`, but the actual integration point (does POS call out
   to check this, or does the RFID platform need to signal POS instead) was
   not traced against the existing thin POS adapter -- same category of
   unvalidated integration-sequencing assumption as P027 Open Items 5/12.
