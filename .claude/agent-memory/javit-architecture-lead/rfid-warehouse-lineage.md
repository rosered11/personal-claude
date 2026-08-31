---
name: RFID Event Platform Lineage -- Third Distinct KB Domain
description: inbox/RFID/ is SCM IT's RFID Event Platform (gate/edge/tag domain), a separate lineage from both Sprint-OMS/ETL and PTL warehouse; now six entries, P027/D032/S032, P028/D033/S033, P029/D034/S034, P030/D035/S035, P031/D036/S036, P032/D037/S037
type: project
---

`inbox/RFID/*` (plus `manual/rfid-architecture-summary.md`) describe SCM IT's RFID
Event Platform -- a gate/edge-hardware tag-detection and identity-tracking system
(DC + Store), distinct from both the Sprint-OMS/ETL lineage (P001-P025) and the CMG
Put-to-Light warehouse lineage (P026/D031/S031), even though PTL and RFID are both
warehouse-adjacent. The platform now has five formal KB consultations:

- **P027/D032/S032** (`inbox/RFID/gate-transfer-verification-req.md`) -- gate-level
  manifest verification for internal/inter-site tag movement. Chosen: Domain-Driven
  Design as primary (GateSession aggregate enforcing zero-loss/fail-safe invariants),
  with Event-Driven Architecture folded in as the manifest pre-positioning transport.
  Accumulated 10 addendums post-decision during design review, including Addendum 5/6/7
  (dock-appointment-based inbound/outbound correlation) and Addendum 9 (MovementManifest.PoRef).
- **P028/D033/S033** (`inbox/RFID/ingestion-transport-protocol-req.md`) -- the edge
  (DC Site Server / Store Gateway) -> central Ingestion Service WAN transport
  protocol. Chosen: Hexagonal Architecture as primary (stateless HTTPS/mTLS batch API),
  with Event-Driven Architecture evaluated and REJECTED as the primary transport.
- **P029/D034/S034** (`inbox/RFID/returns-flow-req.md`) -- store returns reverse flow:
  no "remove on return" path for the paid-EPC cache. Chosen: Saga Pattern as primary
  (ReturnSaga), with Event-Driven Architecture folded in for same-store returns and
  cache-invalidation transport. First and only named, scoped exception to "no
  synchronous registry calls from site operations" (cross-store return leg only).
- **P030/D035/S035** (`inbox/RFID/inbound-no-dock-correlation-req.md`) -- the first
  RFID consultation that revisits and partially invalidates a specific prior decision
  from real field data, rather than extending into new territory. A site visit
  confirmed P027 Open Item #12 (unvalidated assumption "every inbound PO gets a dock
  appointment") is FALSE for one real warehouse -- no dock scheduling exists there at
  all; goods are staged at a receiving zone and matched to a PO afterward by staff.
  lens-determiner paired DDD (in-aggregate extension: a third named GateSession
  resolution mode, `OpenForZoneReceiving`, keyed on `ManifestId` not `PoRef` since a PO
  can have multiple concurrent partial-delivery manifests) against Hexagonal
  (formal `IManifestResolutionStrategy` port). DDD won as primary because the problem's
  own constraints explicitly steered toward reusing the already-proven Addendum-5-style
  nullable-key branch rather than building new abstraction for 3 known modes; Hexagonal's
  strategy-port insight was deferred (not rejected), with an explicit named trigger
  ("the moment a 4th resolution mode is confirmed") for when to promote it -- the
  platform's second deliberate YAGNI deferral (after D032 Addendum 3's manifest-chunking
  deferral). **D032 Addendum 5/6/7 is explicitly retained, not superseded or deprecated**
  -- kept as a fully equal, per-site-configurable alternative (`inbound_correlation_mode`
  config value) for sites that do have real dock scheduling, since the Clarified Scope
  explicitly warned against assuming one site's field data generalizes platform-wide.
  P027 Open Item #12 is now resolved (confirmed false for this site, generalized into a
  second supported mode); Open Item #11 (gate_id/WMS-TMS namespace matching) remains
  fully open, scoped only to sites still on the DockAppointment mode.

**Why:** This repo now has three active, unrelated consultation lineages distinguished
by inbox subfolder (`inbox/oms/`, `inbox/push-to-light/`, `inbox/RFID/`) -- the risk
pattern already seen with PTL (see ptl-warehouse-lineage.md) repeats here in a third
direction: assuming "warehouse-adjacent" implies "same lineage" would incorrectly
conflate RFID with PTL just because both happen to share a generic
`warehouse-management` tag. Within the RFID lineage itself, P027/P028/P029/P030 all carry
`rfid`/`edge-computing`/`offline-first` tags at moderate overlap (~0.2-0.6, P030 highest
against P027 at ~0.6 given how tightly coupled the problem is) despite being genuinely
different problems on the same platform -- don't mistake that partial tag overlap for
grounds to UPDATE rather than CREATE; the 0.8 threshold is the real gate, and P030 staying
just under it while being this closely related to P027/D032 is itself a useful calibration
data point for future RFID consultations.

**How to apply:** When a new RFID/gate/edge/tag/transport/returns/correlation problem
arrives, check tag overlap against P027, P028, P029, P030, *and* P031 specifically (not
against P026/PTL, and not against the OMS/ETL corpus). Reuse the platform's already-
documented design principles as fixed context rather than re-deriving them each time --
read `manual/rfid-architecture-summary.md` first. Also note: as of D034, "no
synchronous registry calls from site operations" has exactly one named, scoped
exception (cross-store return verification); as of D035, D032 Addendum 5/6/7's
dock-appointment correlation is no longer universal -- it is one of two site-configurable
inbound resolution modes, selected via `inbound_correlation_mode`. Do not assume either
platform-wide default without checking the relevant decision. The local-hop-vs-WAN-hop
MQTT distinction from P028/D033 remains a recurring point of confusion risk: never assume
the local reader->edge hop and the WAN edge->Ingestion-Service hop imply or extend each
other. New pattern worth watching: P030 is this platform's first case of a KB decision's
own logged "open item" (P027 #12) later being confirmed/denied by real field data and
triggering a full new consultation -- treat other RFID open items (especially P027 #11,
#1, #3, #5, #7) as live candidates for the same trigger, not archived risk notes.

- **P031/D036/S036** (`inbox/RFID/container-sscc-modeling-req.md`) -- the second of
  three sequential consultations queued from the same real warehouse site visit that
  produced P030/D035/S035, and the first RFID consultation to extend `GateSession`'s
  Header-validation branch itself (D032 Addendum 10) rather than its manifest-
  resolution strategy. `manual/rfid-component-reference.md` Appendix 6 had already
  flagged SSCC-alongside-SGTIN reads as a forward-looking risk, not hypothetical; a
  site visit turned it into a stated requirement: model container-level EPCs with a
  queryable relationship to the item-level EPCs packed inside ("what is inside this
  box" / "which box is this item in"), not just tolerate them via
  `GateVerdict.UnsupportedScheme`. lens-determiner paired DDD against CQRS -- a fresh
  axis, motivated directly by the requirement naming two distinct query directions.
  DDD won as primary (container identity/contents modeled relationally in the
  Serialization DB, owned by Serialization Service like every other identity table),
  but CQRS's two sharpest insights were folded in: (1) asymmetric edge fanout -- only
  the container-to-contents direction is pushed to the edge cache (the only direction
  `GateSession` needs at zero-delay); the reverse item-to-container lookup stays
  central-only (Query/Admin API) -- this platform's first deliberately asymmetric
  fanout design, since every prior manifest type was pushed to edges uniformly; (2)
  write-boundary completeness validation, reusing D032 Addendum 1's count/checksum
  pattern for the new `ContainerPackedEvent`. Confidence rated medium, not high: this
  consultation surfaced a genuine, previously-unknown correctness risk -- items inside
  a sealed, RF-occluded container may never be individually read and would falsely
  surface in `ComputeMissingExpectedEpcs()` (D032 Addendum 4) unless downstream
  cross-references the new `ContainerReads` list first -- logged as P031 Open Item 1,
  highest priority, explicitly not resolved by D036 itself.

- **P032/D037/S037** (`inbox/RFID/location-cycle-count-req.md`) -- the third
  and last of three consultations queued from the same site visit as
  P030/D035 and P031/D036, and the first RFID consultation whose core
  question is not "how does a declared expected list reach the edge in
  time" but "how does an expected list get *produced* at all, absent a
  declaring document." The site's existing cycle-count flow only does
  site-wide book-stock variance; the ask was a location-scoped count whose
  baseline is the platform's own last-known state (`epc_registry`), which
  had no location column at all. lens-determiner paired DDD against CQRS --
  the same lens pair as D036, but a fresh axis: not "first-class modeled
  entity vs. bidirectional query-shape problem" (D036's trigger, two named
  query directions), but "declared-document reuse vs. a self-asserted,
  continuously-live materialized state" as the shape of the expected list
  itself. DDD won as primary for invariant enforcement (`LocationCountSession`,
  a new `GateSession`-sibling type reusing its zero-loss/zero-delay/
  fail-safe shape field-for-field -- deliberately NOT a fifth
  `GateSession.OpenForXxx` mode, since a continuously-refreshed snapshot has
  no `Created->Distributed->Active->Consumed/Expired` lifecycle the way
  `MovementManifest` does), but CQRS's mechanism was adopted as essential
  infrastructure, not optional: `location_contents`, a materialized
  projection folded from the same location-stamping events that already
  update `epc_registry`, joined against D036's `container_contents` at
  build time. This is this platform's second consultation to directly
  consume D036's container-contents relationship, and the first to close
  P031/D036 Open Item 1 (container-packed items falsely appearing missing)
  BY CONSTRUCTION for one flow -- though only for `LocationCountSession`;
  the three pre-existing `GateSession` flows still lack the cross-reference
  check, so the underlying risk remains open platform-wide (P032 Open Item
  1). Confidence rated medium: this is the platform's first genuinely new
  `GateSession`-sibling type (not an extension of `GateSession` itself, the
  pattern every prior extension used) and its first true continuously-
  materialized projection, with no prior operational tuning precedent
  (refresh cadence/staleness) on this platform.

**Update**: when a new RFID/gate/edge/tag/transport/returns/correlation/
cycle-count problem arrives, check tag overlap against P027-P032
specifically (not P026/PTL, not the OMS/ETL corpus). As of P032/D037, this
platform has two consultations (D036, D037) that pair DDD against CQRS --
watch for a third; if it recurs again, the axis distinguishing each
pairing (query-shape asymmetry for D036, declared-document-vs-self-
asserted-state for D037) is the thing worth re-verifying is genuinely
fresh, not the lens pair itself.
