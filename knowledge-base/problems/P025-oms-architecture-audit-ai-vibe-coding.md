---
id: P025
title: "OMS Architecture Audit After AI Vibe-Coding"
date: 2026-07-31
tags: [architecture-audit, distributed-monolith, microservices, grpc, secrets-management, dotnet, vibe-coding, technical-debt]
related_decisions: [D030]
related_snippets: [S030]
---

# OMS Architecture Audit After AI Vibe-Coding

## Problem

The Sprint-OMS system -- five separately-deployable .NET services (Order, Portal, Master, Front,
Report) built largely via AI-assisted "vibe coding" -- has undocumented, inconsistent service
boundaries: some cross-service calls go through gRPC adapters while others still rely on direct
project references to another service's Infrastructure layer (interfaces/DTOs, and historically EF
DbContexts), and plaintext credentials are committed to source-controlled configuration files
across multiple services.

## Root Cause

The codebase evolved through rapid, per-feature AI-generated changes without an enforced
architectural governance layer (no fitness functions, no shared-contracts package, no secrets
management policy). Interfaces and DTOs ("ports") still live inside each service's monolithic
Infrastructure project alongside its EF Core DbContext/entities/repositories ("adapters"), so
consumers must still reference the owning service's full Infrastructure assembly. Secrets were
hardcoded directly into appsettings*.json rather than externalized.

## Summary

Sprint-OMS is a five-service .NET 10 system using FastEndpoints with vertical-slice modules
layered into API/Core/Infrastructure(/Integration) per service, communicating over gRPC, but
mid-migration and undocumented outside code comments. Cross-service coupling still exists at the
project-reference/contract level. Plaintext secrets are committed for multiple services
(confirmed: MySQL/Redis in Portal.API/appsettings.json, PostgreSQL in
Order.API/appsettings.AzureDevelop.json). ~30 gRPC clients disable TLS certificate validation
(`DangerousAcceptAnyServerCertificateValidator`). Missing test projects for 3 of 5 services
(Master, Front, Report), ~87 rapid-fire EF migrations in Order alone.

## Context

Stack: .NET 10, FastEndpoints, EF Core/PostgreSQL, Redis, gRPC (Order.Integration,
Portal.Integration, Master.Integration), FluentValidation, JWT auth via external session service,
deployed to Tencent Kubernetes Engine (TKE). Five services: Order (largest, order
lifecycle/outbox), Portal (customer-facing, proxies to Order/Master via gRPC), Master
(product/reference data), Front (BFF/aggregator, no own DB), Report (thin diagnostics stub).
Shared.Infrastructure holds cross-cutting concerns. Evidence of in-flight refactor from
distributed-monolith toward gRPC-isolated services, incomplete.

Prior related KB lineage: P024/D029/S029 (2026-07-31, same-day prior audit on this repo --
confirmed the same Infrastructure-assembly coupling and added a NetArchTest fitness function, but
did not evaluate secrets management or gRPC transport security); P018/D023/S023 (Strangler Fig
facade-first sequencing, first real-code audit of this coupling); this problem sharpens the
picture along a dimension neither P024 nor P018 covered in depth -- plaintext secrets and disabled
gRPC certificate validation -- while confirming the same structural coupling finding a third time.

## Constraints

- Analysis derived solely from source code, not docs.
- This is a comprehension/audit task, not a request to pick a brand-new architecture wholesale.
- Remediation must not disrupt the active operational OMS.
- Existing gRPC contracts/interfaces already relied upon by multiple consumers cannot be
  renamed/moved without coordinated updates.
- Committed secrets are tied to real infrastructure and must be rotated, not just deleted.

## Severity

High -- compile-time coupling compounds with every AI-assisted change, and plaintext credentials
plus disabled TLS certificate validation on ~30 gRPC clients are live security exposures on a
production-adjacent (TKE) deployment, not just structural debt.

## Affected Components

- Order.API/Core/Infrastructure/Integration
- Portal.API/Core/Infrastructure/Integration
- Master.API/Core/Infrastructure/Integration
- Front.API/Core/Infrastructure (BFF)
- Report.API/Core/Infrastructure
- Shared.Infrastructure
- appsettings.json configs
- Order.Core.Services.Outbox.OutboxService
