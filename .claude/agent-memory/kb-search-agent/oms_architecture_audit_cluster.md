---
name: OMS Architecture Audit Tag Cluster
description: Tag cluster and cross-reference anchors for OMS distributed-monolith / service-boundary-violation / vibe-coding audit problems
type: project
---

# Cluster: OMS Distributed-Monolith / Architecture-Audit

**Tags:** `oms`, `architecture-audit`, `vibe-coding`, `service-boundary-violation`,
`modular-monolith`, `microservices`, `grpc`, `dotnet`, `hexagonal-architecture`,
`strangler-fig`, `testability`

**Anchor entries (highly cross-referenced, treat as the canonical OMS audit lineage):**
- P024/D029/S029 (2026-07-31) — source-code-only audit confirming Sprint-OMS is a
  distributed monolith (5 independently-deployed .NET services sharing compile-time
  Infrastructure/Integration references despite real gRPC seams existing). Decision:
  Hexagonal ports at existing gRPC seams + NetArchTest CI fitness function
  (`S029-netarchtest-service-boundary-fitness-function`). This is the newest and most
  specific match for any future "OMS audit" or "vibe-coding technical debt" problem —
  check its `date` field before assuming a new run is a genuine duplicate vs. an update
  to the same record (kb-writer-agent uses overlap_score >= 0.8 vs P024 as the
  update-in-place threshold per CLAUDE.md).
- P018/D023/S023 (2026-07-09) — earlier stage of the same OMS audit lineage: found
  service-boundary coupling undermining planned BFF/Gateway/observability layers.
  Decision was Strangler Fig facade-first sequencing. D023 explicitly lineages into
  D029 ("strangled seam-by-seam (D023 lineage)").
- P020/D025/S025 (2026-07-22) — same lineage, different angle (DB load/scaling from BU
  growth via shared-schema multi-tenancy). Low direct tag overlap with pure
  "architecture-audit" queries (shares only `oms`/`dotnet`) but same underlying
  Sprint-OMS codebase and explicitly extends "the P018/D023 repo-audit lineage" per
  index.md's changelog note.
- P015/D020/S020 (2026-04-28) — earliest OMS modular-monolith precedent (DDD+CQRS+
  Outbox+ACL, module boundary enforcement). D025's changelog note says it "does not
  contradict the D020 modular-monolith precedent" — useful context when a new OMS
  problem's tags skew toward `modular-monolith`/`domain-driven-design` rather than
  `architecture-audit`.

**Signal:** A new problem titled around "OMS ... audit", "vibe coding", "distributed
monolith", or naming the same component list (Order/Portal/Master/Front/Report
.API/.Core/.Infrastructure/.Integration + Shared.Infrastructure) is very likely either
(a) a genuine re-run of the same audit (near-duplicate — check overlap_score against
P024 first, it will usually be the highest score in the KB for this kind of query), or
(b) the next phase of this same lineage (treat P018/D023, P020/D025, P015/D020 as
required secondary context even when their raw tag-overlap score is lower than P024's).
