---
name: RFID Event Platform Lineage -- Third Distinct KB Domain
description: inbox/RFID/ is SCM IT's RFID Event Platform (gate/edge/tag domain), a separate lineage from both Sprint-OMS/ETL and PTL warehouse; now three entries, P027/D032/S032, P028/D033/S033, P029/D034/S034
type: project
---

`inbox/RFID/*` (plus `manual/rfid-architecture-summary.md`) describe SCM IT's RFID
Event Platform -- a gate/edge-hardware tag-detection and identity-tracking system
(DC + Store), distinct from both the Sprint-OMS/ETL lineage (P001-P025) and the CMG
Put-to-Light warehouse lineage (P026/D031/S031), even though PTL and RFID are both
warehouse-adjacent. The platform now has three formal KB consultations:

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
- **P029/D034/S034** (`inbox/RFID/returns-flow-req.md`) -- store returns reverse flow:
  no "remove on return" path for the paid-EPC cache (real loss-prevention gap), plus
  5 unanswered design questions (return locality, epc_registry state transition,
  cache reconciliation, tid_registry applicability, CountOnly handling). Chosen: Saga
  Pattern as primary (ReturnSaga owning verify->inspect->refund-authorize/compensate),
  with Event-Driven Architecture folded in as (a) the sole mechanism for same-store
  returns and (b) the cache-invalidation transport, partitioned by *originating*
  site_id. Notable: this is the platform's first and only deliberate, narrowly-scoped
  exception to "no synchronous registry calls from site operations" -- confined to the
  cross-store return leg only, with a bounded timeout and a third fail-safe outcome
  (`PendingVerification`) that GateSession's binary FailOpen/FailClosed never needed
  (because unlike a gate pass, the customer/item are still physically present and the
  refund has not yet been authorized). Also gave `tid_registry` its first live
  consumer, and added a short-lived `ISoldEpcLedger` to patch a validation gap the
  CountOnly tracking-mode decision otherwise left open, without reversing CountOnly.

**Why:** This repo now has three active, unrelated consultation lineages distinguished
by inbox subfolder (`inbox/oms/`, `inbox/push-to-light/`, `inbox/RFID/`) -- the risk
pattern already seen with PTL (see ptl-warehouse-lineage.md) repeats here in a third
direction: assuming "warehouse-adjacent" implies "same lineage" would incorrectly
conflate RFID with PTL just because both happen to share a generic
`warehouse-management` tag. Within the RFID lineage itself, P027/P028/P029 all carry
`rfid`/`edge-computing`/`offline-first` tags at modest overlap (~0.2-0.3) despite being
genuinely different problems on the same platform -- don't mistake that partial tag
overlap for grounds to UPDATE rather than CREATE; the 0.8 threshold is the real gate.

**How to apply:** When a new RFID/gate/edge/tag/transport/returns problem arrives,
check tag overlap against P027, P028, *and* P029 specifically (not against P026/PTL,
and not against the OMS/ETL corpus). Reuse the platform's already-documented design
principles as fixed context rather than re-deriving them each time -- read
`manual/rfid-architecture-summary.md` first (now has three documented
sub-consultations: §6 gate/manifest, §6b ingestion transport, §6c store returns).
Also note: as of D034, "no synchronous registry calls from site operations" is no
longer an absolute platform rule -- it has exactly one named, scoped exception
(cross-store return verification). Do not silently treat it as unconditional in any
future RFID consultation; also do not treat this one exception as license to add
further sync calls elsewhere without the same explicit, narrow justification. The
local-hop-vs-WAN-hop MQTT distinction from P028/D033 remains a recurring point of
confusion risk in this platform: never assume the local reader->edge hop and the WAN
edge->Ingestion-Service hop imply or extend each other.
