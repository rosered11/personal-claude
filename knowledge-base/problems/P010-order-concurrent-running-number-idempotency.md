---
id: P010
title: "Order Service — Concurrent Running-Number Race + Missing Idempotency on CreateOrder and ProcessActivity Events"
date: 2026-04-24
tags:
  - ef-core
  - concurrency
  - optimistic-locking
  - running-number
  - idempotency
  - dotnet
  - mssql
  - integration-events
  - duplicate-order
  - null-safety
  - microservices
severity: high
affected_components:
  - Order.Domain.Functions.GenRunningNumber.GenRunningNumberFunction (line 276)
  - Order.Business.Mediator.Handlers.OrderFront.CreateOrderFrontHandler
  - Order.API.Controllers.Front.OrderFrontController.CreateOrder
  - Order.API.Applications.IntegrationEvents.EventHandling.ProcessOrder.ProcessOrderStartIntegrationEventHandler
  - Order.Domain.AggregatesModel.Function.AllowedStatusSetting.StandardActivityCreateBySubOrderFunction (line 45)
  - Order.API.Applications.IntegrationEvents.EventHandling.OrderProcess.ProcessOrderItemUpdateIntegrationEventHandler (line 172)
related_decisions:
  - D015
related_snippets:
  - S015
---

## Problem

8 pods of order-service (Kubernetes, .NET/C#, EF Core, MSSQL) exhibit a cluster of interrelated runtime failures observed on 2026-04-24 across pods: 74t4n, gc4pm, pzdf9, rvbf4, tj9hv, vhqlk, vnjw8, zqwx7.

## Root Cause

`GenRunningNumberFunction` uses EF Core optimistic concurrency (RowVersion/timestamp-based) to generate running numbers without a distributed lock or database-level sequence. When all 8 pods concurrently attempt to update the same running-number counter row at the same time, `SaveChanges` throws `DbUpdateConcurrencyException` on all but one pod. The retry loop causes a storm. The upstream caller retries the CreateOrder request on failure without a deduplication check, producing duplicate order submissions caught by FluentValidation. Integration event consumers (`ProcessActivityStart`, `ProcessOrderItemUpdate`) have no idempotency key check, allowing the same event to execute across multiple pod instances.

## Error Inventory (Prioritized)

### Priority 1 — Critical (fix immediately)
| Error | Component | Frequency | Impact |
|-------|-----------|-----------|--------|
| `DbUpdateConcurrencyException` in GenRunningNumberFunction | GenRunningNumberFunction.cs:276 | Storm at 07:41–07:42 UTC, all 8 pods | Order creation blocked; retries cascade |
| `ValidationException: duplicate` on CreateOrder | OrderFrontController CreateOrder | Multiple pods 08:02–08:54 UTC | Caller-visible error; order not created |
| `ProcessActivityStartEnvent already start by another process` | ProcessOrderStartIntegrationEventHandler | Sporadic, all pods | Duplicate process execution |

### Priority 2 — High (fix in sprint)
| Error | Component | Frequency | Impact |
|-------|-----------|-----------|--------|
| `NullReferenceException` in StandardActivityCreateBySubOrderFunction | StandardActivityCreateBySubOrderFunction.cs:45 | 3 occurrences on 74t4n pod | Activity creation silently aborts |
| `NullReferenceException` in ProcessOrderItemUpdateIntegrationEventHandler | ProcessOrderItemUpdateIntegrationEventHandler.cs:172 | 2 occurrences (rvbf4, zqwx7) | Order item update dropped |

### Priority 3 — Medium (address as tech debt)
| Warning | Component | Impact |
|---------|-----------|--------|
| `MultipleCollectionIncludeWarning` | EF Core queries across all pods | Potential slow queries under load |
| Decimal precision model warnings | EF Core OnModelCreating | Silent data truncation risk on InsuranceMinimumAmount, PackQuantity, PackageWeight, Qty |
| DataProtection key ephemeral storage | Container startup | Keys lost on pod restart |

## Context

- Environment: Kubernetes, 8 pod replicas, shared MSSQL database
- Stack: .NET/C#, EF Core, MediatR, FluentValidation, integration events via message bus
- Pods started ~00:00 UTC; concurrency storms occur at 07:41–08:54 UTC
- Log source: `inbox/order-service-20260424/` (8 files)

## Constraints

- Must not introduce distributed locking that significantly degrades order creation throughput
- Solution must be implementable in .NET/C#/EF Core targeting MSSQL
- Fix must be backward-compatible with existing order number format
- Integration event idempotency must survive pod restarts and message bus redelivery
