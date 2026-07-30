---
when_to_use: "Use when a multi-tenant EF Core service (shared schema, tenant discriminator column) needs to move dashboard/report-style reads off the primary write path without redesigning tenancy, while also capturing the per-tenant write-volume signal needed to later decide which tenants deserve a dedicated schema. Applies when: (1) a cache layer already exists in the codebase but is disabled, (2) a read replica connection can be provisioned on the same database engine already in use, (3) there is an existing outbox/event stream that can be extended to populate read projections, (4) the team has no per-tenant throughput data yet and needs a low-risk way to start collecting it before committing to physical tenant partitioning."
related_problems:
  - P020
related_decisions:
  - D025
language: "C#"
---

# S025 -- OMS Cache-Aside Reads + Read-Replica Routing + Per-BU Write-Volume Metric

## What This Solves

D025 chose CQRS-style read-scaling (cache-aside + read replica + Outbox-fed read model) as the
immediate move for OMS's shared-schema multi-tenant database, plus lightweight per-`BuCode`
write-volume instrumentation so the team can decide, with real data, when a specific business unit
needs to be promoted to the schema-per-BU-tier partitioning design (Lens B, deferred in D025) --
instead of guessing upfront.

This snippet demonstrates three pieces working together, grounded in interfaces that already exist
in `Shared.Infrastructure` (`ICacheService`) and the connection-string pattern already used in
`Order.Infrastructure/DependencyInjection.cs`:

1. `ICacheService`-backed cache-aside read helper (`RedisCacheService` already implements the
   interface; this only needed the config flag flipped on and a call-site wrapper).
2. A read-only `OrderReadDbContext` pointed at a replica connection string, resolved separately from
   the existing primary-writer `OrderDbContext` -- no change to the write path or EF model.
3. A `BuWriteVolumeTracker` that increments a per-`BuCode` counter on every write, as the concrete
   instrumentation D025 requires before any tenant is promoted to its own schema.

## Why This Matters Architecturally

- Keeps the write path (`OrderDbContext`, primary) completely untouched -- this is a read-side-only
  change, which is why it is lower risk than the deferred DDD/tenant-partitioning option.
- The cache-aside helper and the read-replica context are both opt-in per call-site, so this can be
  rolled out incrementally (start with the highest-traffic dashboard/report endpoint) rather than as
  a big-bang migration.
- The write-volume tracker is the bridge between this decision and the deferred one: it produces the
  evidence needed to trigger Lens B (schema-per-BU-tier) selectively, per BU, instead of upfront for
  all BUs.
