---
name: sprint-oms-repeat-audit-pattern
description: Sprint-OMS is being audited repeatedly by the same user; each pass adds a new dimension rather than repeating the last one
type: project
---

The Sprint-OMS codebase (5-service .NET system: Order, Portal, Master, Front, Report) has now
been the subject of four related consultations: P018/D023 (2026-07-09, service-boundary review),
P020/D025 (2026-07-22, database load scaling), P024/D029 (2026-07-31, full source-only audit +
NetArchTest fitness function), P025/D030 (2026-07-31, same-day follow-up audit surfacing secrets
management + gRPC TLS cert validation). kb-search overlap scores between these have stayed in the
0.375-0.5 range (below the 0.8 UPDATE threshold), so each became a new CREATE-mode record rather
than an update — each pass genuinely surfaces material not covered by the prior one, it is not
redundant re-auditing.

**Why:** When a career-mentor invocation involves this codebase, the user is likely to keep
returning to it as they audit deeper (structural coupling → fitness functions → secrets/transport
security → next likely: test-coverage backfill for Master/Front/Report, or the Phase-2 Service
Mesh spike once TKE sidecar-injection is confirmed). Treat each new Sprint-OMS consultation as
"what new layer did this pass expose" rather than re-explaining concepts already covered in
D023/D029 (distributed monolith, "independently deployable != decoupled", NetArchTest fitness
functions) — the user has already internalized those.

**How to apply:** Before writing "Recent Learning Opportunities" for a new Sprint-OMS
consultation, check whether the concept already appears in the Exposure Log tied to a prior
Sprint-OMS KB ID (P018/P020/P024/P025 and their D/S pairs). If it does, treat it as reinforcement
(cite it, do not re-teach it) and lead instead with whatever dimension is genuinely new this time
(e.g. secrets, transport security, test coverage, Phase-2 mesh rollout). Also anticipate the next
likely Sprint-OMS gap to flag proactively: Master/Front/Report have zero test projects (noted in
both P024 and P025) — this is a recurring, unaddressed finding worth calling out directly in a
future gap analysis rather than waiting for a fifth audit to surface it again.
