---
name: inbox-oms-req-path-reused
description: inbox/oms/req.md gets overwritten and reused across unrelated OMS consultations -- do not assume its content matches a prior KB entry with the same source path.
type: feedback
---

The file `inbox/oms/req.md` has been reused at least twice for genuinely different requests: on
2026-07-22 it asked about database load risk from BU growth (became P020/D025/S025), and on
2026-07-31 it asked for a full six-area, source-code-only architecture audit (became P024/D029/
S029). Both runs are recorded in `knowledge-base/index.md`'s changelog footer referencing the same
inbox path with different content.

**Why:** If a future consultation is asked to "process inbox/oms/req.md" it is not safe to assume it
is a re-run of a previously-seen problem just because the KB already has an entry citing that exact
path -- always read the file's current content fresh and run kb-search on the actual extracted tags
before assuming CREATE vs UPDATE mode.

**How to apply:** Never skip reading an inbox file because a KB entry already references that path
in its changelog. Treat the KB dedup decision (overlap_score >= 0.8 -> UPDATE) as the only valid
signal for whether this is a repeat problem, not the file path.
