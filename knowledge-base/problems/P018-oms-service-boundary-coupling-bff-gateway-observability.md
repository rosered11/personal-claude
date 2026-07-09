---
id: P018
title: "OMS Service-Boundary Coupling Undermines Planned BFF/Gateway/Observability Layers"
date: 2026-07-09
tags: [oms, microservices, service-boundary-violation, api-gateway, bff, observability, read-model, dotnet]
related_decisions: [D023]
related_snippets: [S023]
---

# OMS Service-Boundary Coupling Undermines Planned BFF/Gateway/Observability Layers

## Problem

`Order.API` directly project-references the `Master` and `Portal` .NET assemblies (in-process
calls) despite Order, Master, and Portal being deployed as independently-versioned services on
Tencent Kubernetes Engine (TKE). The system is therefore a modular monolith wearing a
"microservices" label: a change to Master's internals can force a rebuild/redeploy coupling with
Order even though the two ship on separate pipelines. The requesting team wants to layer a
BFF (Backend-for-Frontend), an API gateway, uniform OpenTelemetry-based observability, a
low-latency dashboard read-model, and broker-based event fan-out on top of this foundation as
more business units (BUs) are onboarded, each potentially with its own frontend/channel.

**Scope note:** this is a proposal under specialist review, not a live incident. Nothing described
has been implemented. The source document (inbox/oms/oms-architect-review.md) is a repo-audit plus
proposed target architecture prepared for architecture sign-off before implementation starts.

## Root Cause

Order, Master, and Portal were split into separately deployable projects/pipelines before a
genuine network-level contract was enforced between them. The codebase's own conventions
anticipated this: an `IHandler` pattern exists specifically to allow swapping in-process calls for
HTTP/gRPC later without touching callers, and `docs/CLAUDE.md` explicitly forbids injecting a
`UnitOfWork` across service boundaries. Neither convention has actually been enforced -- the
project references remain. This gap is compounded by a documented-but-never-built `Gateway/` and
`Manager/` service pair (marked WIP in `docs/CLAUDE.md` but absent from the repo tree), suggesting
the boundary-fixing work was previously planned and shelved rather than newly discovered. On top of
the coupling issue, the platform lacks the cross-cutting infrastructure a growing BU count will
need: no API gateway/BFF exists, `TraceIdMiddleware` is wired on `Portal.API` but not `Order.API`,
there are no metrics or health checks anywhere, the async backbone is a DB-polled Outbox with no
broker/fan-out, and there is no dedicated read-model -- the Looker dashboard likely queries the
primary transactional DB directly.

## Summary

The OMS platform (repo Sprint-OMS; services Order/Master/Portal/Shared; .NET 10, FastEndpoints,
EF Core plus Npgsql, Hangfire, Redis, deployed on TKE) is growing to onboard more business units
over time. The requesting engineer identified five forward-looking needs: a BFF layer per
frontend/channel, system metrics, low-maintenance logging/distributed tracing, low-latency
dashboard data, and async reporting. A repo audit surfaced that the biggest structural risk is not
any of those five gaps individually, but that the "microservices" framing is not actually true at
the code level today -- Order.API's direct assembly references to Master and Portal mean every new
layer (BFF, gateway, broker fan-out) would be built on an unenforced boundary. The stakes: every
additional BU onboarded compounds this coupling risk and adds more surface area with inconsistent
observability, and the Looker/dashboard path risks contending with the primary write path for DB
connections and locks as traffic grows.

## Context

- Repo / branch: Sprint-OMS, branch dev/oms, audited 2026-07-09.
- Services: Master/ (Master.Core/Infrastructure/API), Order/ (Order.Core/Infrastructure/Integration/API/Tests -- this is "the OMS"), Portal/ (Portal.Core/Infrastructure/Integration/API/Tests -- today's de facto "Proxy"/partner-webhook gateway toward CFW; not yet deployed per docs/CLAUDE.md, only Order.API runs today in dev), Shared/ (Shared.Infrastructure -- BaseHandler, TraceIdMiddleware, exception middleware, plus private NuGet packages for auth/logs/notifications).
- Stack: .NET 10 / ASP.NET Core, FastEndpoints v8.2.0 with a custom BaseHandler<TRequest,TResponse>, FluentValidation, EF Core 10 + Npgsql/PostgreSQL (older docs mentioning MySQL are stale), Redis (currently disabled in dev), Hangfire (Postgres storage), Refit for outbound partner HTTP, Asp.Versioning.Http.
- API Gateway/BFF: does not exist. No YARP/Ocelot/Kong/Envoy anywhere. Portal.Core/Modules/Webhooks/Gateway/ is inbound webhook handling for an external partner system also confusingly named "Gateway" -- not an API gateway for OMS's own frontends.
- Observability: Serilog to Postgres (spc_logs DB) via Sprint.Shared.Logs; only a custom request-scoped TraceIdMiddleware (not W3C traceparent), wired on Portal.API only; no metrics (no OpenTelemetry/Prometheus); no health checks (AddHealthChecks() absent everywhere in the .NET services).
- Async/eventing: DB-backed transactional Outbox (outbox_routing_rules table, named events like CreateOrderEvent/PickConfirmedEvent), polled and pushed onward to WMS/TMS/partner Gateway. No broker (no RabbitMQ/Kafka/Azure Service Bus/MassTransit). Reliable, but fan-out to new independent consumers (e.g. an analytics pipeline) requires bespoke routing-rule changes per consumer.
- Reporting/dashboard: no CQRS/read-model/materialized view in source; no dedicated reporting service; the only artifact is a Looker field-mapping doc, implying Looker likely reads the primary DB directly (not verified against actual Looker config).
- Deployment: kube-oms/ contains only 5 YAML files, none for Master.API/Order.API/Portal.API -- deploy topology for the actual domain services is unconfirmed from this repo (may live in a separate infra/Helm repo).
- KB precedent tension (flagged by orchestrator, not raw KB-search score): this same OMS lineage was previously analyzed in P013/D018 (greenfield DDD+CQRS+Outbox baseline), P014/D019 (aggregate extensions), and P015/D020 (confirmed Modular Monolith over Microservices at 70K order-lines/day, given small team + atomic-TX need + no-broker + sub-inflection-point volume). This new proposal describes Order/Master/Portal as already split into separate deployables with a broker relay under consideration, which may mean the D020 rejection criteria for microservices no longer hold, or may mean the split happened without re-validating those criteria. This tension must be reconciled explicitly, not silently contradicted.

## Constraints

- Must remain within the existing production stack: .NET 10, FastEndpoints, EF Core + Npgsql/PostgreSQL, Hangfire, Redis (already running).
- Repo-wide .NET coding standard: must NOT introduce MediatR or AutoMapper (not open source) -- use plain DI service interfaces and explicit manual mapping methods instead.
- Deploy target is TKE; any broker/observability choice must weigh Tencent-native services (CKafka, CMQ, Tencent APM) vs self-hosted (Kafka, Prometheus/Grafana, Loki) -- team's ecosystem standardization is unconfirmed (open question).
- This is a proposal under specialist review -- nothing has been implemented yet. Recommendations must remain adjustable/challengeable, not treated as a locked-in final design.
- No load/throughput numbers are available yet, so the read-model store choice (Postgres replica vs ClickHouse) and the broker cost/benefit cannot be fully sized.
- Multi-tenancy/data-isolation requirements per BU are undetermined -- materially affects BFF/gateway tenant-context design.
- Team/ownership model for Gateway + BFF + domain services is undetermined -- affects whether channel-based BFF (vs per-BU BFF) still holds.
- The existing DB-backed Outbox pattern already provides reliable, transactional delivery and must be preserved as the source of truth, not replaced outright.
- Whatever service-boundary fix is chosen must reconcile with, not silently contradict, the standing D020 precedent (Modular Monolith confirmed for this OMS lineage at the previously analyzed scale).

## Severity

High -- not a live incident, but a foundational risk that compounds with every additional BU onboarded and every new layer (BFF, gateway, broker fan-out) built on top of an unenforced service boundary.

## Affected Components

- Order.API (direct project references to Master/Portal)
- Master.API
- Portal.API (not yet deployed)
- Shared.Infrastructure (TraceIdMiddleware, exception middleware, logging)
- kube-oms/ manifests (missing for all three domain services)
- Looker dashboard integration (likely reading primary DB directly)
- DB-backed Outbox (outbox_routing_rules, OutboxTriggerEvents)
