---
id: P020
title: "OMS Shared-Schema Database Load Risk From BU Growth"
date: 2026-07-22
tags: [oms, database, postgresql, multi-tenancy, scalability, caching, read-replica, dotnet]
related_decisions: [D025]
related_snippets: [S025]
---

# OMS Shared-Schema Database Load Risk From BU Growth

## Problem

As additional business units (BUs) are onboarded to the OMS platform, all tenants share a single
Postgres primary (`spc_oms`) with no read replica, no active caching, and BU-scoped data
distinguished only by a `BuCode` discriminator column. Both the transactional write path and
ad-hoc dashboard/reporting reads compete for the same limited connection pool and I/O capacity,
so growth in BU count compounds load on one shared database instance rather than being absorbed
incrementally.

## Root Cause

The OMS data model uses shared-schema, discriminator-column multi-tenancy (`BuCode`) against a
single Postgres instance with no query-path or write-path scaling levers currently active: the
Redis cache already wired into `IOrderUnitOfWork` is disabled in configuration, there is no read
replica or CQRS/read-model for dashboard/report queries (which likely hit the primary directly,
per a prior repo audit), and BU count/tier has no bearing on physical data placement -- every BU,
regardless of volume, writes into and is read from the exact same tables and connection pool.
Compounding this, the same Postgres server also hosts the Master (`spc_master`) and structured-log
(`spc_log`) databases plus Hangfire job state, meaning transactional writes, background jobs, and
logging already share infrastructure capacity before any BU-driven growth is factored in.

## Summary

The OMS platform (Order/Master/Portal/Shared, .NET 10, EF Core + Npgsql) is expected to onboard a
growing number of business units over time. A source-code audit confirms all BU data is stored in
a single shared Postgres schema distinguished by a `BuCode` column, with no read replica, no
active caching (Redis is wired but disabled), and no CQRS/read-model separating dashboard/report
queries from the transactional write path. This mirrors a gap already flagged in a prior repo audit
(`output/review-architech.md`, 2026-07-09, which fed KB P018/D023) but was not resolved there. As
BU count grows, both write volume (more concurrent order flows) and read volume (more BU-scoped
dashboards/reports) will compound on the same single database instance, risking connection
exhaustion, lock contention, and I/O saturation -- a "database spike" that degrades the platform
for every BU simultaneously, not just the one that grew.

## Context

Repo: Sprint-OMS (services Order, Master, Portal, Shared, Front). Confirmed via source: .NET 10 /
ASP.NET Core, FastEndpoints, EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.2, single
Postgres server (per dev `appsettings.Development.json`, host redacted here for security) hosting
at least three logical databases -- `spc_oms` (Order.API, connection string key `OrderDB`),
`spc_master` (Master.API), and `spc_log` (Serilog sink via `Sprint.Shared.Logs`, batched 100 rows /
5s) -- plus Hangfire job storage, all on Postgres. `BuCode` is a plain string column on
Order-domain tables (e.g. `BookingOrder` has a composite index on `(BuCode, Status)`, confirmed in
`OrderDbContextModelSnapshot.cs`); there is no separate `BusinessUnit`/tenant master entity
anywhere in `Master.Infrastructure`, no schema-per-tenant, and no sharding. Redis
(StackExchange.Redis) is already wired into `Shared.Infrastructure/Services/Caching`
(`ICacheService`/`RedisCacheService`, injected into `OrderUnitOfWork`) but is toggled off in dev
(`CacheSetting:Redis:Enabled=false`). No read-replica connection string exists in any appsettings
file found. This is the same OMS lineage previously analyzed in KB P013-P015 (D018-D020: DDD+CQRS+
Outbox modular monolith, confirmed at 70K order-lines/day) and P018/D023 (strangler-fig fix for
Order.API's in-process coupling to Master/Portal) -- the same prior repo audit document
(`output/review-architech.md`, 2026-07-09) that fed P018 also flagged (open question #3) that
per-BU data isolation requirements were undetermined, and (section 2.6) that the Looker dashboard
likely queries the primary transactional DB directly. This new problem is a focused deep-dive on
that same audit's data-scaling gap, prompted directly by the requesting engineer's question about
"database spike" as BU count grows.

**Update (2026-07-22, post-consultation):** the requesting engineer confirmed current aggregate
write volume is **~1,000,000 records/day** (~11-12 records/sec average; estimated ~60-120
records/sec at peak assuming a 5-10x peak-to-average ratio, not yet confirmed). This average is well
within single-instance Postgres write capacity, which supports the read-side-first sequencing in
D025 -- the near-term spike risk is contention/query-pattern-driven (cross-BU lock/connection
contention, unindexed dashboard queries) rather than raw volume-driven. Still missing: per-BU
breakdown of this total (which BUs dominate, if any), whether the 1M figure covers
Order/order-lines only or all tables, confirmed growth trend per quarter, and confirmation of
whether Looker's actual datasource is the primary DB -- all flagged as open questions in the prior
audit and still unresolved here.

## Constraints

- Must remain within the existing production stack: .NET 10, EF Core + Npgsql/PostgreSQL, Redis
  (already provisioned), Hangfire -- no new database technology category without strong
  justification.
- Must not silently contradict the standing D020 decision (Modular Monolith over Microservices for
  this OMS lineage) -- any partitioning strategy must work within a single deployable, not force a
  service split.
- Repo-wide .NET coding standard: no MediatR, no AutoMapper.
- Aggregate write volume is now known (~1M records/day, ~11-12 records/sec average), but per-BU
  breakdown, peak writes/sec, and growth curve are still not available -- recommendations must
  remain viable without that finer-grained data and should include how to obtain it (see P020
  instrumentation requirement carried into D025/S025).
- The existing DB-backed Outbox pattern (transactional, polled) must be preserved as the source of
  truth for downstream integration, not replaced.

## Severity

High -- not a live incident, but a foundational risk that compounds with every additional BU
onboarded, consistent with the severity framing already established for the related P018 finding.

## Affected Components

- Order.API / `OrderDbContext` (`spc_oms`)
- Master.API / `MasterDbContext` (`spc_master`)
- Shared.Infrastructure Redis caching (`ICacheService`, currently disabled)
- Serilog logging sink (`spc_log`, same Postgres server)
- Hangfire job storage (same Postgres server)
- Looker dashboard (likely direct primary-DB reads, unconfirmed)
