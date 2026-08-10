---
name: career-mentor-not-invoked-every-run
description: The roadmap's consultation count tracks career-mentor invocations, not total KB decisions — several KB decisions have no roadmap entry
type: feedback
---

The roadmap's "Consultation Count" and "Recent Learning Opportunities" sections do not cover
every decision in knowledge-base/index.md. As of 2026-07-31 the KB has 30 decisions (D001-D030),
but the roadmap's Recent Learning Opportunities section only has 8 entries (D018, D019, D020,
D022, D023, D024, D029, D030). Notably P020/D025, P021/D026, P022/D027, and P023/D028 were never
processed into the roadmap — career-mentor was evidently not invoked after those pipeline runs.

**Why:** If asked to do a standalone gap analysis or to reconcile "how many consultations has the
user had," do not assume the KB index count equals the roadmap consultation count. Trust the
roadmap's own Recent Learning Opportunities section as the source of truth for what has actually
been extracted into the learning system, and treat any KB decision missing from it as a real gap
worth backfilling if the user asks for a standalone review.

**How to apply:** When invoked standalone for gap analysis, cross-check knowledge-base/index.md
against the roadmap's Exposure Log / Recent Learning Opportunities sections. If there are KB
decisions with no corresponding roadmap entry (e.g. D025-D028 as of this writing), flag them
explicitly as unextracted learning opportunities rather than silently skipping them — the user may
want them backfilled, especially D025 (CQRS cache-aside/read-replica for OMS scaling) and D026-028
(index-rebuild throttling / retry-amplification), which touch skill domains (Data Architecture,
Distributed Systems resilience) still marked incomplete in the roadmap.
