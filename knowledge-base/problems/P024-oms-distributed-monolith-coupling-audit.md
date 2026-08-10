---
id: P024
title: "OMS Codebase Audit -- Distributed Monolith Coupling Behind a Microservices Deploy Topology"
date: 2026-07-31
tags: [oms, architecture-audit, vibe-coding, service-boundary-violation, modular-monolith, testability, dotnet, grpc]
related_decisions: [D029]
related_snippets: [S029]
---

# OMS Codebase Audit -- Distributed Monolith Coupling Behind a Microservices Deploy Topology

## Problem

The requesting engineer needs a source-code-only audit (explicitly excluding all existing
documentation) of the full Sprint-OMS platform -- Order, Portal, Master, Shared, Front, and Report
-- most of which was written via AI-assisted "vibe coding". Three answers are required: (1) what
architecture pattern(s) the system actually uses today, (2) what a software architect should be
concerned about given the current state, and (3) concrete next actions. This is a comprehension/audit
task, not a greenfield design decision -- no new capability is being requested.

## Root Cause

Five independently pipelined, independently Dockerized .NET 10 services (Order.API, Portal.API,
Master.API, Front.API, Report.API) plus a shared common library (Shared.Infrastructure) each follow
a consistent internal Layered/Onion structure ({Service}.API -> {Service}.Core ->
{Service}.Infrastructure -> {Service}.Integration), with {Service}.Core organized as
FastEndpoints vertical-slice modules (Modules/{Feature}/Endpoint.cs + Handler.cs). Real gRPC
contracts exist for many cross-service calls (.proto files plus generated *GrpcService.cs
servers in each Core project and matching Grpc*Service.cs clients in each Integration project),
which is the correct seam for turning "same process today" into "separate process tomorrow"
without touching callers.

However, the actual application code does not consistently use that seam. Confirmed via using-
statement analysis (not just unused ProjectReference entries in .csproj files):
Order.Core has 10 files importing Portal.Infrastructure and 18 importing Master.Infrastructure;
Front.Core has 26 files importing Order.Infrastructure, 10 importing Portal.Infrastructure,
and 2 importing Master.Infrastructure. A concrete example: Order.Core/Services/Slot/
PostponeDeliveryService.cs depends directly on Portal.Infrastructure.Interfaces.TMS.
ITmsPostponeService and its DTOs -- an interface that is a legitimate external-system adapter
(implemented by Portal.Integration/TMS/TmsPostponeService.cs, which calls the real external TMS
over HTTP) but is owned and defined inside Portal's assembly, not inside Order's own port/contract
layer. Order.API therefore compiles against, and its Docker image bundles, all of Portal's
third-party integration code (CFW, TMS, STS, CHG) merely to postpone a delivery slot. This means the
five services are a distributed monolith: independently deployable artifacts that remain
compile-time and dependency-graph coupled, so the "microservices" framing implied by five separate
Dockerfiles/pipelines is not true at the code level -- extending the finding already recorded in
P018/D023, now confirmed bidirectionally (Order-Master, Order-Portal, Portal-Master) and
newly including Front, which did not exist as an audited component in P018.

## Summary

A direct reading of the Sprint-OMS solution (no docs consulted, per explicit instruction) shows a
consistent per-service Layered/Onion plus FastEndpoints-vertical-slice internal architecture, DB-backed
transactional Outbox (still no message broker anywhere in the repo), shared-schema/BuCode
multi-tenancy on Postgres (confirmed again, matching P020), and Redis plus Hangfire wired but
inconsistently enabled per environment. Two encouraging changes since the last audits (P018 on
2026-07-09, P020 on 2026-07-22) were found: AddHealthChecks() is now present on all five API
projects (P018 flagged zero), and early CQRS read-model work has actually started --
LookerProjectionRefreshService, BuWriteVolumeFlushService, and a SyncWatermarkState entity now
exist in Order.Infrastructure/Order.Core/Services/Reporting, which is a partial, in-progress
implementation of the D025 recommendation. Set against that progress, the core structural problem
from P018 has not been fixed and is now confirmed more precisely and more broadly: application code
in three of the five services reaches directly into another service's Infrastructure/Integration
assembly instead of going through the gRPC seam that already exists for the same domains. Compounding
this, testability is uneven to the point of absence: Master.API, Front.API, and Report.API have
no test project at all; Order has 12 test files for 602 source files and Portal has 23 for
238. Report.API itself is a near-empty stub (14 files, only Ping/Diagnostics modules) that is
nonetheless already fully scaffolded with its own Dockerfile, appsettings, and a Master.Integration
project reference -- structure built ahead of function, a recognizable AI-vibe-coding pattern. Also
found: connection strings and Redis passwords are committed in plaintext across every
appsettings.*.json (even if pointed at non-prod hosts), and TraceIdMiddleware is still wired on
Portal.API only, unchanged since P018, so distributed trace correlation remains inconsistent across
the other four services. No architecture-fitness-function tooling (e.g. NetArchTest) exists anywhere
in the repo to prevent the coupling problem from growing with the next AI-assisted change.

## Context

Repo: Sprint-OMS (audited directly via source tree, .csproj files, appsettings*.json, and .cs
files only -- no README*.md, docs/, or in-repo markdown notes were read, per explicit user
instruction). Six areas requested: Order, Portal, Master, Shared, Front, Report.

- Stack: .NET 10, ASP.NET Core, FastEndpoints + FluentValidation, EF Core 10 + Npgsql/PostgreSQL,
  Hangfire (Postgres storage), StackExchange.Redis (cache + output-cache), gRPC
  (Grpc.AspNetCore/Grpc.Net.Client) for defined but inconsistently used cross-service calls,
  Microsoft.Extensions.Http.Polly for outbound HTTP resilience. No MediatR, no AutoMapper found
  anywhere in the repo (already compliant with this org's .NET coding standard).
- Deployables confirmed via per-project Dockerfile: Order.API, Portal.API, Master.API,
  Front.API, Report.API. kube-oms/ only contains manifests for mock-api and web-ui; no
  Kubernetes manifests exist in this repo for the five real domain services (same gap as P018).
- Cross-service coupling graph (from actual .csproj ProjectReference entries, corroborated by
  using-statement counts in source): Order.Core -> Master.Infrastructure,
  Portal.Infrastructure; Portal.Core -> Master.Infrastructure, Order.Infrastructure;
  Front.Core/Front.API -> Order.Infrastructure, Order.Integration, Portal.Infrastructure,
  Portal.Integration, Master.Infrastructure, Master.Integration. Master is the only service
  with no dependency on any other domain service's Infrastructure (it is correctly the most
  "core" / least coupled). gRPC exists in parallel for many of the same domains: 28 gRPC
  service/client pairs under Order, 6 under Master, 4 under Portal.
- Testing: Order.Tests (12 files / 602 source files), Portal.Tests (23 files / 238 source
  files); Master, Front, Report have no *.Tests project at all.
- Observability: AddHealthChecks() present on all 5 API projects (improved since P018).
  Shared.Infrastructure.Middlewares.ExceptionHandlerMiddleware used by all 5.
  Shared.Infrastructure.Middlewares.TraceIdMiddleware used only by Portal.API (unchanged gap
  from P018). No OpenTelemetry/Prometheus package anywhere. No NetArchTest/ArchUnitNET or any
  architecture-fitness-function tooling found anywhere.
- Secrets hygiene: OrderDB, MasterDB, LogDB Postgres connection strings and Redis passwords
  are committed in plaintext in appsettings.Development.json / appsettings.AzureDevelop.json
  across all five API projects (hostnames/credentials redacted here for security; all point at
  *-nonprd.* hosts per filename, not confirmed to be limited to non-production use only).
- Evidence of partial prior-recommendation adoption: Order.Infrastructure now contains
  LookerOrderProjection, BuWriteVolumeDaily, and SyncWatermarkState-related EF Core migrations
  (dated 2026-07-22, the same day as P020/D025), plus Order.Core/Services/Reporting/
  LookerProjectionRefreshService.cs and BuWriteVolumeFlushService.cs -- an in-progress,
  partial implementation of the D025 CQRS read-model/instrumentation recommendation.
- Rapid, reactive schema churn signal: Order's PII-encryption migrations were added and then
  immediately adjusted multiple times within the same short window (EncryptOrderCustomerPiiColumns
  -> MakeOrderCustomerPiiColumnsDeterministicBytea -> RelaxEncryptedColumnsNullability ->
  EncryptOrderAddressAndShipmentGeoColumns -> EncryptOrderAddressRemainingPiiColumns, all dated
  2026-07-08), consistent with iterative, discovery-driven schema design rather than an
  upfront-modeled data contract.
- Prior related KB lineage on this exact repo: P013/D018 (greenfield DDD+CQRS+Outbox design),
  P014/D019 (aggregate extensions), P015/D020 (Modular Monolith confirmed at 70K order-lines/day),
  P018/D023 (first real-code audit: Order->Master/Portal in-process coupling; Strangler Fig
  facade-first chosen), P020/D025 (shared-schema DB load risk from BU growth; CQRS read-scaling
  chosen). This problem does not contradict any of them; it operationalizes and sharpens the
  boundary-coupling half of P018/D023 with confirmed, bidirectional, Front-inclusive evidence, and
  confirms partial progress on P020/D025.

## Constraints

- Source-code-only analysis -- no documentation, README, or markdown notes inside Sprint-OMS may be
  used as evidence for this problem record (explicit user instruction).
- Must not silently contradict D020 (Modular Monolith over Microservices, confirmed for this
  lineage) or D023 (Strangler Fig facade-first sequencing already chosen for OMS service-boundary
  work) -- any recommendation must extend or operationalize these, not re-litigate them.
- Repo-wide .NET coding standard: no MediatR, no AutoMapper (already satisfied -- verified absent).
- Must remain within the existing stack: .NET 10, FastEndpoints, EF Core + Npgsql/PostgreSQL,
  gRPC, Redis, Hangfire -- no new technology category without strong justification.
- Recommendations must be actionable for a small team already relying on AI-assisted coding --
  process/tooling guardrails (e.g. CI-enforced fitness functions) are preferred over recommendations
  that depend purely on developer discipline, since the coupling problem itself was introduced under
  exactly that assumption.

## Severity

High -- not a live incident, but a foundational, compounding risk: every new AI-assisted change has
an unenforced, low-friction path (add a ProjectReference) to deepen the exact coupling this audit
flags, and three of five services currently ship with zero automated test coverage to catch
regressions from that coupling.

## Affected Components

- Order.Core, Order.API, Order.Infrastructure (heaviest cross-service importer: 18 files -> Master,
  10 files -> Portal)
- Front.Core, Front.API (newly confirmed as the most cross-service-coupled component: 26 files ->
  Order, 10 -> Portal, 2 -> Master; zero tests)
- Portal.Core, Portal.Integration (TMS/CFW/STS/CHG external adapters bundled into Order.API's and
  Front.API's build via direct Integration-project reference)
- Master.Infrastructure (least coupled outward; most depended-upon)
- Report.API (functional stub -- 14 files -- already fully scaffolded/deployed)
- Shared.Infrastructure (TraceIdMiddleware, ExceptionHandlerMiddleware, Redis caching, BaseHandler)
- All five appsettings.*.json files (plaintext committed connection strings/secrets)
