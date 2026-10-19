---
id: P034
title: "OMS Back-of-the-Envelope Infrastructure Resource Estimation for the Infra Team (PostgreSQL, Redis, OTel/ELK, AKS)"
date: 2026-10-16
tags: [oms, capacity-planning, infrastructure-sizing, resource-estimation, postgresql, redis, hangfire, opentelemetry, elasticsearch, kubernetes, azure, dotnet, observability, cloud-infrastructure]
severity: medium
related_decisions: [D039]
related_snippets: [S039]
---

# OMS Back-of-the-Envelope Infrastructure Resource Estimation for the Infra Team

## Problem

The requester is preparing a software-architecture report for the infrastructure team on the
Sprint-OMS platform (Front, Master, Order, Portal, Report, Shared). Infra needs a justified
resource request: (1) which resources are required (e.g. PostgreSQL, Redis, OTel), (2) how much
of each (e.g. Redis N GB), and (3) worked examples of how much of each resource a given
transaction volume consumes. Every request must carry a reason and, where two viable angles
exist, an explicit pros/cons trade-off. Output goes to `output/oms-estimate-resource`.

## Root Cause

No capacity model exists for OMS. Production volume is not stated anywhere in the request, the
only quantitative anchors are the P015 design target (50K orders/day, ~70K order lines/day,
peak 3-5 orders/s) and an unverified "~1M writes/day" figure in an architect handoff. The code
shows resource consumers that earlier documentation does not (outbox dispatch now runs as
Hangfire jobs on Redis, request/response logging to a third database, a disabled-but-planned
OTel/ELK stack, no explicit Npgsql pool or Hangfire worker settings).

## Summary

Estimate infrastructure for a 5-service .NET 10 system from first principles: derive per-order
unit economics (rows, bytes, API calls, outbox jobs, CPU) from the real source tree, scale them
across four volume scenarios, and present topology choices (pooled vs isolated resources) with
trade-offs, as a repeatable worksheet rather than a one-off number.

## Context

- Source read live from `D:\workspace\Sprint-OMS` (not trusted from earlier KB entries): Azure
  PG Flexible Server hosting `spc_oms`/`spc_master`/`spc_log`; Azure Managed Redis used for output
  cache, `ICacheService`, Hangfire storage and token cache; AKS with 5 deployables; OTel + ELK
  designed in `monitoring/` and `kube-oms/` but disabled in `Order.API/Program.cs`.
- `docs/oms/oms-database-scaling-plan.md` section 2.1 (outbox dispatch is synchronous, no worker)
  is stale; code now enqueues `OutboxService.DispatchInternalAsync` into Hangfire (Redis) with the
  full `OutboxDispatchRequest.RequestPayload` as job arguments.
- Builds on P015/D020 (modular monolith, 50K orders/day target), P020/D025 (DB scaling, read
  replica and per-BU write-volume triggers), P025/D030 (secrets in appsettings) and P033/D038
  (Elastic sizing methodology, S038).

## Constraints

- Output must resolve to concrete resources, quantities and example volumes, each with a reason
  and trade-off where two angles exist.
- No production volume or retention policy supplied; all unknowns must be explicit and
  measurable (tagged assumptions with a measurement method).
- Repo standards: no MediatR/AutoMapper (n/a, no application code produced).
- Secrets found in source must not be reproduced in deliverables.

## Affected Components

- Azure PostgreSQL Flexible Server (`spc_oms`, `spc_master`, `spc_log`)
- Azure Managed Redis (cache and Hangfire job storage)
- AKS: Order.API, Portal.API, Front.API, Master.API, Report.API
- OTel Collector, Elasticsearch, Kibana
- SFTP target (Report exports)

## KB Search Result

kb-search against 33 problems (tag intersection / union): top matches P033 (0.31: elasticsearch,
azure, capacity-planning, cloud-infrastructure, observability), P015 (0.19: oms, dotnet,
postgresql, redis, kubernetes), P020 (0.16: oms, postgresql, dotnet). All below the 0.8 UPDATE
threshold, so this is a CREATE-mode record. It is the first OMS capacity-planning entry and the
first infra-procurement estimate covering PostgreSQL, Redis and AKS (P033 covered Elasticsearch
only); the ES portion deliberately reuses S038 rather than re-deriving it.
