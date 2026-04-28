---
id: P015
title: "OMS System Architecture — Confirmed Modular Monolith with DDD + CQRS + Outbox + ACL for 70K Order Lines/Day"
date: 2026-04-28
tags:
  - oms
  - modular-monolith
  - order-management
  - domain-driven-design
  - cqrs
  - outbox
  - anti-corruption-layer
  - state-machine
  - dotnet
  - postgresql
  - redis
  - kubernetes
  - integration
  - webhook
  - multi-channel
  - security
  - jwt
  - hmac
severity: high
related_decisions:
  - D020
related_snippets:
  - S020
---

# P015 — OMS System Architecture: Confirmed Modular Monolith with DDD + CQRS + Outbox + ACL for 70K Order Lines/Day

## Problem

Sprint Connect needs a complete, production-ready system architecture for its greenfield OMS
handling 70,000 order lines/day across five order channels and four external system integrations.
The architecture must enforce a complex 16-status Order state machine, provide reliable event
delivery without a message broker, support horizontal scaling on Kubernetes, and maintain clean
module separation to enable future service extraction.

## Root Cause

No production architecture blueprint existed for the Sprint Connect OMS. The Phase 1 design
(P013/D018) established the DDD+CQRS+Outbox pattern; this problem records the confirmed full
system architecture that operationalises that design into a Modular Monolith with 4 modules,
separate DB schemas per module, specific deployment topology (2 API replicas + 1 outbox worker),
security model (JWT/HMAC/Vault), and explicit ACL adapter per external system.

## Context

- Technology: C# .NET 8, ASP.NET Core Web API, PostgreSQL 16, Redis 7
- Scale: ~70K order lines/day (~50K orders/day), peak ~3-5 orders/second
- Deployment: Kubernetes — 2 replicas for oms-api (stateless), 1 replica for oms-outbox-worker
  (single-writer with FOR UPDATE SKIP LOCKED)
- DB layout: 1 PostgreSQL instance, 4 schemas (orders, payment, returns, config), separate audit DB
- Security: JWT per channel, HMAC per integration, Vault for secrets
- Decision confirmed: Modular Monolith chosen over microservices (small team, atomic transactions,
  scale fit)

## Constraints

- Must not use a message queue — Outbox pattern provides sufficient reliability at this scale
- All cross-module access is by ID only — no cross-schema joins
- Outbox worker must be single-writer (1 Kubernetes replica) to prevent duplicate event publishing
- State machine enforced in domain code, not DB config tables
- 16-status Order state machine with strict transition rules
- ACL adapter per external integration — no direct coupling between OMS domain and external contracts
- Inbound webhook processing for WMS pick/put-away and TMS delivery/lost callbacks

## Affected Components

- OMS.Domain (Order aggregate, state machine, domain events, value objects)
- OMS.Application (CQRS command/query handlers via MediatR)
- OMS.Infrastructure (EF Core, OutboxWriter, OutboxPoller, ACL adapters)
- OMS.API (ASP.NET Core Web API controllers)
- PostgreSQL (4 schemas: orders, payment, returns, config + audit DB)
- Redis (CQRS read model cache)
- oms-outbox-worker (Kubernetes single-replica deployment)
- WmsAdapter, TmsAdapter, PosAdapter, StsAdapter, LegacyBackendAdapter (ACL layer)

## KB Search Results (at time of consultation)

Top matches by tag-intersection (all below 0.8 threshold — new records created):
1. P013/D018 — OMS Greenfield Design — overlap score 0.320 (parent architecture; this confirms and operationalises it)
2. P014/D019 — OMS Extended Aggregate — overlap score 0.320 (peer Phase 2 record; same domain, different scope)
3. D010 — Database Type Selection — overlap score 0.091 (postgresql, redis)

Note: this problem is semantically a confirmation and operationalisation of P013/D018, but the
strict Jaccard score is 0.320 (below 0.8 threshold), and the scope is distinct: P015 addresses
module boundary enforcement, deployment topology, security architecture, and outbox worker
operational concerns — none of which were the focus of P013. New records created.
