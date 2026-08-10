---
name: RFID Event Platform Lineage -- Third Distinct KB Domain
description: inbox/RFID/ is SCM IT's RFID Event Platform (gate/edge/tag domain), a separate lineage from both Sprint-OMS/ETL and PTL warehouse; now two entries, P027/D032/S032 and P028/D033/S033
type: project
---

`inbox/RFID/*` (plus `manual/rfid-architecture-summary.md`) describe SCM IT's RFID
Event Platform -- a gate/edge-hardware tag-detection and identity-tracking system
(DC + Store), distinct from both the Sprint-OMS/ETL lineage (P001-P025) and the CMG
Put-to-Light warehouse lineage (P026/D031/S031), even though PTL and RFID are both
warehouse-adjacent. The platform now has two formal KB consultations:

- **P027/D032/S032** (`inbox/RFID/gate-transfer-verification-req.md`) -- gate-level
  manifest verification for internal/inter-site tag movement. Chosen: Domain-Driven
  Design as primary (GateSession aggregate enforcing zero-loss/fail-safe invariants),
  with Event-Driven Architecture folded in as the manifest pre-positioning transport.
- **P028/D033/S033** (`inbox/RFID/ingestion-transport-protocol-req.md`) -- the edge
  (DC Site Server / Store Gateway) -> central Ingestion Service WAN transport
  protocol, explicitly distinct from the platform's already-reserved *local* MQTT hop
  (reader/vendor device -> edge agent, a vendor acceptance boundary). Chosen:
  Hexagonal Architecture as primary (stateless HTTPS/mTLS batch API as the sole
  edge-facing port), with Event-Driven Architecture evaluated and REJECTED as the
  primary transport (a persistent per-site broker session over the WAN was in direct
  structural tension with "no per-client session state" and firewall/proxy-traversal
  constraints at retail-fleet scale) -- EDA's reliability instincts were still reused,
  but only as the platform's existing *internal* publish pipeline.

**Why:** This repo now has three active, unrelated consultation lineages distinguished
by inbox subfolder (`inbox/oms/`, `inbox/push-to-light/`, `inbox/RFID/`) -- the risk
pattern already seen with PTL (see ptl-warehouse-lineage.md) repeats here in a third
direction: assuming "warehouse-adjacent" implies "same lineage" would incorrectly
conflate RFID with PTL just because both happen to share a generic
`warehouse-management` tag. Within the RFID lineage itself, P027 and P028 both carry
`rfid`/`edge-computing`/`offline-first` tags (kb-search overlap ~0.3) despite being
genuinely different problems on the same platform -- don't mistake that partial tag
overlap for grounds to UPDATE rather than CREATE; the 0.8 threshold is the real gate.

**How to apply:** When a new RFID/gate/edge/tag/transport problem arrives, check tag
overlap against P027 *and* P028 specifically (not against P026/PTL, and not against
the OMS/ETL corpus). Reuse the platform's already-documented design principles as
fixed context rather than re-deriving them each time -- read
`manual/rfid-architecture-summary.md` first, and note it now has two documented
sub-consultations (§6 gate/manifest, and the Ingestion Service transport decision
referenced from P028/D033). Also note the local-hop-vs-WAN-hop MQTT distinction is a
recurring point of confusion risk in this platform: the local reader->edge hop and the
WAN edge->Ingestion-Service hop are deliberately different transports with different
operational models -- never assume one implies or extends the other.
