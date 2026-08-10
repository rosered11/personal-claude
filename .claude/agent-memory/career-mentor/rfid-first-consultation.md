---
name: RFID first consultation, third non-OMS/ETL domain
description: P027/D032 is the third distinct KB domain (after Sprint-OMS/ETL and PTL warehouse) — a sub-problem on a never-KB-documented RFID Event Platform; treat DDD and Event-Driven vocabulary as already portable, focus learning summary on the new blend variant (invariant-ownership vs distribution-timing) rather than re-teaching either lens
type: project
---

Consultation 10 (2026-08-10, P027/D032/S032) is the third KB domain entirely
outside Sprint-OMS/ETL, after PTL (P026/D031/S031). Do not conflate RFID with
PTL even though both are warehouse-adjacent -- they are separate systems with
separate KB anchors and separate source doc locations (inbox/RFID/docs/* +
manual/rfid-architecture-summary.md vs inbox/push-to-light/*.pptx).

The user already has DDD (D018/D019 -- rich aggregates) and Event-Driven
Architecture (multiple prior decisions) well established in the roadmap, so
this consultation's learning summary should NOT re-teach either pattern from
first principles. The genuinely new material is: (1) an aggregate whose sole
purpose is enforcing one completeness invariant rather than modeling a rich
lifecycle, and (2) events used for pre-positioning (arriving before a
physical event) rather than as after-the-fact notification -- both are
sharper, narrower applications of already-known patterns, consistent with the
Intermediate-phase pattern-fluency-across-domains signal already noted for
P026/D031.
