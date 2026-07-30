---
id: D025
chosen_option: "CQRS Read-Scaling Now (Redis Cache-Aside + Postgres Read Replica + Outbox-Fed Read Model), With Per-BU Write-Volume Instrumentation to Trigger Selective Schema-per-BU-Tier Partitioning Later"
problem_id: P020
tags: [oms, database, postgresql, multi-tenancy, scalability, caching, read-replica, cqrs, domain-driven-design, dotnet]
related_snippets: [S025]
---

# Decision: OMS Database Scaling Strategy for Growing BU Count

## Context

P020 identified that OMS's shared-schema, `BuCode`-discriminated multi-tenancy on a single Postgres
instance has no active read/write scaling levers (Redis cache disabled, no read replica, no
CQRS/read-model), so both write volume and BU-scoped dashboard/report read volume will compound on
one database as more business units onboard. Two contrasting architectural lenses were evaluated:
CQRS (scale the read path, keep the shared schema) and Domain-Driven Design applied as bounded-context
tenant-data ownership (partition the write path itself, schema-per-BU-tier). Aggregate write volume
is confirmed at ~1M records/day (~11-12 records/sec average) -- well within single-instance Postgres
write capacity, reinforcing that near-term risk is contention/query-pattern-driven rather than
volume-driven -- but no per-BU breakdown, peak writes/sec, or growth trend exists yet, and the
decision must not contradict the standing D020 modular-monolith precedent for this same OMS lineage.

## Options Considered

1. **CQRS Read-Scaling** (Lens A) -- flip on the already-wired Redis cache-aside layer, add a
   Postgres streaming read replica, and extend the existing Outbox pattern to populate read
   projections; dashboards/reports/GetOrders-style reads move off the primary. Does not touch the
   write path or the tenancy model.
2. **DDD Bounded-Context Tenant Partitioning** (Lens B) -- tier BUs by write volume/SLA class and
   give higher tiers their own Postgres schema (or database), routed via a Master-owned
   `BuCode` -> tier/connection mapping, following the same pattern already used for
   `OutboxRoutingRule`/`ChannelCutoffConfig`. Attacks the write path and caps blast radius per BU,
   at the cost of new multi-schema EF Core migration/routing complexity and harder cross-BU
   reporting.

## Decision

**Chosen: CQRS Read-Scaling now, with a built-in trigger to selectively adopt DDD-style
schema-per-BU-tier partitioning later.**

Immediately: enable the existing but disabled Redis cache-aside layer (`ICacheService`/
`RedisCacheService`) for hot, low-churn reads (BU/channel config, product master lookups); add a
Postgres read replica and route dashboard/report/audit queries to it; extend the existing
transactional Outbox (already used for WMS/TMS/partner dispatch) to also populate read-optimized
projection tables, so Looker and any future hot-path dashboard read from the replica/read-model,
never the primary.

In parallel, add a lightweight per-BU write-volume metric (e.g. writes/min tagged by `BuCode`,
emitted from the same Outbox event stream) purely for instrumentation -- no infrastructure change
yet. This closes the "no throughput numbers" gap called out as an open question in the original
repo audit (`output/review-architech.md`, 2026-07-09) and in P020's constraints. If and when a
specific BU's write volume crosses a defined threshold and threatens the shared connection pool,
that BU (and only that BU, or its tier) is selectively promoted to its own Postgres schema per the
Lens B design -- not applied blanket, upfront, to every BU.

This sequencing was chosen over adopting Lens B immediately because: it is materially lower
complexity/effort and lower risk than building a tenant-routing + multi-schema migration layer
before there is evidence any specific BU needs it; it directly fixes the concretely-identified risk
from the original audit (dashboard reads contending with the primary write path); it reuses
already-provisioned infrastructure (Redis, Outbox) rather than introducing new operational surface
area; and it does not touch or reopen the standing D020 modular-monolith-vs-microservices decision.
The confirmed ~1M records/day aggregate volume (~11-12 records/sec average) further supports
deferring Lens B: this average is well within what a single tuned Postgres instance handles, so
sharding or immediate tenant partitioning would be premature without per-BU evidence that a specific
tenant, not aggregate volume, is the actual contention source.

## Consequences

- Dashboard/report reads stop competing with the transactional write path for connections and
  locks -- the concrete risk flagged in the original repo audit is resolved for the read side.
- Read-replica lag introduces eventual consistency for dashboard data; stakeholders must sign off on
  an explicit staleness tolerance (this was an open question in the original audit and remains one).
- The write path is **not** protected by this decision alone -- if a single BU's write volume (not
  just its dashboard reads) grows large enough, it can still contend with every other BU on the
  shared primary. This is why per-BU write-volume instrumentation is a required companion, not an
  optional extra, so the team has the data to know when to trigger Lens B for a specific BU.
- New async read-model pipeline (Outbox -> projections) must be monitored for lag/backlog -- this is
  the same class of observability gap already flagged in P018/D023 (no metrics/health checks exist
  yet); building this read-model without also closing that observability gap risks an invisible new
  failure mode.
- Schema-per-BU-tier partitioning (Lens B) is deferred, not rejected -- it remains the correct next
  move for any BU whose write volume is shown (via the new instrumentation) to threaten the shared
  pool, and it should be applied selectively per over-threshold BU/tier rather than as a system-wide
  redesign.
