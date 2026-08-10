---
name: KB ID Sequence
description: Current highest P/D/S IDs in the knowledge base, to avoid re-scanning the whole filesystem on every write
type: project
---

As of 2026-08-10: highest IDs are P027, D032, S032 (RFID Gate Transfer
Verification -- from inbox/RFID/gate-transfer-verification-req.md). Next
CREATE-mode IDs should start at P028 / D033 / S033. Note: P/D/S numbering is
not always in lockstep across categories (e.g. D010-D014 have no matching
P-number; S004/S006/S007/S013 do not exist) -- always scan all three
directories independently rather than assuming P_n implies D_n and S_n both
exist.

P027/D032/S032 is the first RFID Event Platform entry in the KB. It is a
*third* distinct non-OMS/ETL lineage (after P026/D031/S031 for CMG's PTL
warehouse system) -- do not conflate RFID and PTL even though both are
warehouse-domain problems; they are unrelated systems with separate KB
anchors. kb-search overlap of P027 against the full 26-entry prior KB was
~0.06 (a single shared generic tag, warehouse-management, against P026),
correctly triggering CREATE mode.

The RFID Event Platform itself (source: inbox/RFID/docs/*, summarized in
manual/rfid-architecture-summary.md) has no prior formal P/D/S entry of its
own -- P027 is a sub-problem consultation on top of an already-designed but
never-KB-documented platform. If a future consultation asks to formally
document the base RFID platform architecture itself (not a sub-problem), that
would be a new, broader problem record, not an update to P027.

This repo (Sprint-OMS, via inbox/oms/req.md) has previously produced multiple
audit consultations on overlapping code (P013/P014/P015/P018/P020/P024/P025)
-- always check overlap_score carefully since near-identical file paths across
submissions do not imply near-identical problems.
