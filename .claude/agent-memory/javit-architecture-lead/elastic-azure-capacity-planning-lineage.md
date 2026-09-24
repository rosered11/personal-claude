---
name: elastic-azure-capacity-planning-lineage
description: P033/D038/S038 is the KB's first Elasticsearch/Azure capacity-planning entry and first consultation outside the Sprint-OMS/ETL and RFID/PTL warehouse lineages -- anchor here if a future inbox item revisits Elastic/Azure sizing.
type: project
---

`inbox/estimate-datasize-elastic/req.md` (processed 2026-09-22) produced P033/D038/S038,
a fourth distinct KB lineage alongside Sprint-OMS/ETL ([[sprint-oms-repo-lineage]]),
PTL warehouse ([[ptl-warehouse-lineage]]), and the RFID Event Platform
([[rfid-warehouse-lineage]]). Unlike those three, this was not a bug/incident/audit
against a real running system -- it was a bare, thin request ("I need to size Elastic
on Azure for procurement, don't know where to start") with no source systems, volumes,
retention, or query patterns supplied. kb-search correctly found no meaningful
precedent (top overlap ~0.14 on the generic `observability` tag only) and this became a
CREATE-mode record, not an update to any existing entry.

The deliverable was deliberately a **methodology/worksheet** (S038,
`elastic_sizing_worksheet.py`), not a single hardcoded number -- the problem's real
blocker was "no capacity-planning process exists," not "which of two known options is
better." If a future inbox item asks about Elastic/Azure sizing again with *real*
numbers (source systems, volumes, retention, query patterns now known), treat it as a
strong precedent candidate: recompute kb-search overlap against P033's tags
(`elasticsearch, azure, capacity-planning, data-sizing, cloud-infrastructure,
observability, index-lifecycle-management`) -- if overlap crosses the 0.8 UPDATE
threshold, this becomes an UPDATE to P033/D038/S038 (populate real figures into the
worksheet, not a new P-number), not a new CREATE record for the same underlying system.

**Why:** the KB's dedup logic depends on recognizing that a second, more-specific
Elastic/Azure sizing request is a refinement of P033, not a fresh problem -- exactly the
same reasoning already applied correctly across P023's two submissions
(rebuild-index-db) and should not need to be rediscovered from scratch.

**How to apply:** before running kb-search on any future Elastic/Elasticsearch/Azure
capacity-planning request, check this lineage first. Also see [[lens-pairing-patterns]]
for the sizing-scope lens axis (CQRS vs EDA) and the input-availability deciding factor
discovered in D038, both of which are likely to recur on other planning/estimation-
shaped (non-incident) problems, not just Elastic/Azure ones.
