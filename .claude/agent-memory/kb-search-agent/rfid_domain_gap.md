---
name: RFID Event Platform Domain Gap (Third Non-OMS/ETL Entry)
description: P027/D032/S032 is the first RFID Event Platform entry in the KB, distinct from both the OMS/ETL lineage and the PTL warehouse lineage
type: project
---

P027 (RFID Gate Transfer Verification) introduced a new tag vocabulary (rfid,
edge-computing, gate-verification, manifest-sync, offline-first,
inter-site-transfer, fail-safe, real-time) with near-zero intersection against
the prior 26-entry KB -- the highest overlap found was ~0.06 (a single shared
generic tag, warehouse-management, against P026/D031, the CMG Put-to-Light
entry).

**Why:** Both P026 (PTL) and P027 (RFID) are warehouse-domain problems and
both share the `warehouse-management` tag, which could tempt a search
algorithm into treating them as related. They are not -- PTL is a WMS/SAP/
hardware-controller/Marketplace order-fulfillment integration; RFID is a
gate-hardware tag-detection platform with its own separate source docs
(inbox/RFID/docs/* + manual/rfid-architecture-summary.md, vs inbox/
push-to-light/*.pptx for PTL). Report shared-tag overlap accurately but flag
explicitly when it is generic/domain-level rather than substantive.

**How to apply:** When a new problem's tags include rfid, epc, gate-
verification, tag-encoding, serialization-service, or similar, expect
near-zero overlap against the OMS/ETL corpus and check against P027
specifically as the RFID anchor, not against P026 (PTL) just because both are
tagged warehouse-management. If a second RFID problem arrives, P027/D032/S032
becomes the correct reuse-avoidance/dedup anchor.
