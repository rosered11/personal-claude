---
name: decided-vs-deployed-first-kb-update-precedent
description: First confirmed KB-writer UPDATE (not CREATE) event in this project -- P023/D028/S028 revised in place 2026-07-29 after a real source file surfaced that a prior decision was never actually deployed
type: project
---

On 2026-07-29, a second inbox submission (`inbox/rebuild-index-db/req.md`, `script-rebuild.sql`,
`schema-database.sql`) revisited the exact same problem as the same-day P023/D028/S028
(TaskIndexRebuild causing SQL Server load-spike timeouts). The first P023 consultation could not
obtain a real copy of the old script (the supplied file was a mislabeled duplicate of the plain
schema export) and proceeded on an explicit, stated assumption: that the D027/S027 fragmentation-gate
+ RunId-logging baseline was already live in production, scoping D028 as pure execution-pacing
add-on to that assumed state.

The second submission supplied the *real* `script-rebuild.sql` -- byte-for-byte identical to the
TaskIndexRebuild body already documented in P022 (confirmed programmatically via regex: 195
`ALTER INDEX` statements, 194 unique `(table,index)` pairs, exactly one duplicate --
`PK_StoreLocation` -- matching P022's manually-spotted finding exactly). This also revealed the
assumption was wrong: **neither D027 nor D028 had actually been deployed**, only decided. Tag
overlap between the new problem framing and P023 scored ~0.89 (adding one honest new tag,
`fragmentation-gating`, to P023's original 8 -- not gamed to hit the threshold), correctly
triggering the kb-writer UPDATE path (>= 0.8) for the first time in this KB's history. All five
prior "same-object, new-angle" consultations in this KB (P019 vs P010, P021 vs P019, P022 vs
P010/P019/P021, P023 vs P022, P020 vs P018) scored well below 0.8 and correctly became new CREATE
records instead -- see `project_order_domain_kb_cluster.md`.

**Why:** This is the first empirical evidence in this project of the >= 0.8 UPDATE threshold firing
correctly, and it happened for a legitimate reason: the second submission was not a new problem, it
was the *same* problem with corrected grounding evidence. Folding the correction into the existing
P023/D028/S028 (rather than creating P024/D029/S029) kept the KB from accumulating two records for
one underlying decision, and let the revision explicitly document *why* the decision changed
(deployment-state correction, not new architectural reasoning) directly in the existing record's
"Root Cause" section. The deeper lesson, worth generalizing: a KB (or any decision log) records
*intent*, not *deployment state* -- "this was decided" and "this was shipped" are separate facts
that must be independently verified, especially when a later consultation is asked to extend a very
recent prior decision on the same object (D027 -> D028 was only 0 days apart here).

**How to apply:**
1. When a new inbox problem looks like a near-duplicate of a recent KB entry (same object,
   same/near-identical problem statement, high tag overlap), always compute the tag-overlap score
   honestly before assuming CREATE is correct by default -- do not skip the >= 0.8 check just
   because the object has already been touched twice before (P022, P023) and "feels like" it should
   be a new record again by pattern-matching to the cluster's history.
2. When a new consultation is asked to extend or build on a *very recent* prior decision on the same
   object (same session, same day, or within days), explicitly verify whether that prior decision was
   actually deployed, not just documented as chosen -- do not silently carry forward the assumption
   from the current KB record's "Context" section without re-checking against any newly supplied
   ground-truth artifact (real source file, real screenshot, real log).
3. When an UPDATE does fire, revise the existing P/D/S content to explicitly explain *why* it
   changed (new evidence, corrected assumption) rather than silently overwriting -- this keeps the
   record honest as institutional memory and distinguishes "the architecture was wrong" from "the
   assumed deployment state was wrong," which are very different kinds of mistakes to learn from.
4. Prefer programmatic verification (e.g. a quick regex extraction/count script) over visual
   inspection when a claim needs to hold across a large, repetitive, copy-pasted structure (here:
   195 ALTER INDEX statements) -- it both confirms findings with certainty and can directly produce
   the deployment artifact (e.g. the schedule-table population INSERT) as a side effect.
