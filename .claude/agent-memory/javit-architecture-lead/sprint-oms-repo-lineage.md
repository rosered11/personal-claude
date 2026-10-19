---
name: sprint-oms-repo-lineage
description: Context on the recurring Sprint-OMS consultation lineage (P013-P015, P018, P020, P024...) and where its real source lives on disk.
type: project
---

The Sprint-OMS platform (Order, Portal, Master, Shared, Front, Report -- .NET 10, FastEndpoints, EF
Core + Npgsql/PostgreSQL, gRPC, Hangfire, Redis) is a recurring subject across many consultations,
not a one-off. Real source lives outside this repo at `D:\workspace\Sprint-OMS\{Order,Portal,Master,
Shared,Front,Report}` -- always read directly from there (Read/Grep/Glob/Bash), never assume prior
KB context is still accurate, since the code changes between consultations.

Established precedent lineage (do not silently contradict; extend or explicitly supersede):
- D020: Modular Monolith confirmed over Microservices for this lineage (small team, atomic-TX need,
  no broker, sub-inflection-point volume at the time).
- D023: Strangler Fig facade-first sequencing chosen for OMS service-boundary work (ship Gateway/
  BFF/OTel first, then strangle in-process coupling seam-by-seam later -- the "later" part was
  explicitly deferred, not completed).
- D025: CQRS read-scaling (cache-aside + read replica + Outbox read model) chosen over schema-per-
  BU-tier partitioning for database load risk from BU growth; partition trigger deferred pending
  per-BU write-volume instrumentation.
- D029 (2026-07-31): Hexagonal ports at confirmed cross-service crossing points + CI-enforced
  NetArchTest fitness function, operationalizing the "per-seam boundary strangling" phase D023 had
  deferred, using file-level evidence from a P024 source-only audit.

**Why:** Repeated audits of the same lineage (P018 2026-07-09, P020/P024 2026-07-22 and 2026-07-31)
keep re-confirming and sharpening the same root coupling problem rather than finding something new
each time -- treat new OMS consultations as likely extensions of this thread, and always check
whether a prior decision's recommendation has since been partially implemented in the real repo
(e.g. D025's read-model work had visibly started by the time P024 ran) before assuming nothing
changed.

**How to apply:** When a new OMS problem arrives, read P013/P015/P018/P020/P023-lineage KB entries
first, but verify every claim against the live Sprint-OMS source tree rather than trusting the KB
record as still-current -- the KB records what was true at audit time, not what is true now.

**Update 2026-10-16 (P034/D039/S039):** third OMS capacity/infra consultation (first one about
resource sizing, not code or DB-load design). Findings that outlive the consultation: (1) the planning
doc `docs/oms/oms-database-scaling-plan.md` section 2.1 is stale -- outbox dispatch now runs as
Hangfire jobs on Azure Managed Redis (full `OutboxDispatchRequest.RequestPayload` in job args), so
check code before quoting that doc; (2) Npgsql `Maximum Pool Size` and Hangfire `WorkerCount` are
unset; (3) observability (OTel/ELK) is designed in `monitoring/` and `kube-oms/` but commented out in
`Order.API/Program.cs`; (4) `spc_log` request logging writes about as many bytes per order as the
whole OLTP DB. Decision D039 (HYBRID: 2 PG servers + 2 Redis, pool otherwise) extends D020/D025; a
future sizing request with real volumes should UPDATE P034 (re-run S039), not create a new P-number.
