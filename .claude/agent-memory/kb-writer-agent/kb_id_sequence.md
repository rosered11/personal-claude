---
name: KB ID Sequence
description: Current highest P/D/S IDs in the knowledge base, to avoid re-scanning the whole filesystem on every write
type: project
---

As of 2026-08-10: highest IDs are P026, D031, S031 (PTL Warehouse Integration -- CMG
Put-to-Light process, from inbox/push-to-light/req.md + spec-extracted.md). Next
CREATE-mode IDs should start at P027 / D032 / S032. Note: P/D/S numbering is not
always in lockstep across categories (e.g. D010-D014 have no matching P-number;
S004/S006/S007/S013 do not exist) -- always scan all three directories independently
rather than assuming P_n implies D_n and S_n both exist.

P026/D031/S031 is the first KB entry outside the Sprint-OMS/ETL lineage that produced
P001-P025 -- it establishes a new "warehouse/WMS-SAP-PTL integration" domain rather
than extending an existing precedent. kb-search top match against the existing 25
entries was near-zero overlap (~0.07), correctly triggering CREATE mode (not UPDATE)
with no dedup ambiguity.

This repo (Sprint-OMS, via inbox/oms/req.md) has previously produced four separate
audit consultations on overlapping code (P013/P014/P015/P018/P020/P024/P025) -- always
check overlap_score carefully since near-identical file paths across submissions do not
imply near-identical problems. inbox/push-to-light/req.md is a distinct source path
for a distinct system (CMG PTL, not Sprint-OMS) -- do not conflate the two lineages.
