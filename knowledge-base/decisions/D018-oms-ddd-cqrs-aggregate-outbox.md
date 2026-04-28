---
id: D018
title: "OMS Architecture — DDD Bounded Context + CQRS Read/Write Split + Outbox Integration"
date: 2026-04-27
problem_id: P013
chosen_option: "OMS as DDD Bounded Context with CQRS Read/Write Split + Outbox Integration"
tags:
  - oms
  - order-management
  - domain-driven-design
  - cqrs
  - state-machine
  - outbox
  - integration
  - dotnet
  - postgresql
  - aks
  - microservices
  - phased-rollout
  - anti-corruption-layer
related_snippets:
  - S018
---

# D018 — OMS Architecture: DDD Bounded Context + CQRS Read/Write Split + Outbox Integration

## Chosen Option

**OMS as DDD Bounded Context with CQRS Read/Write Split + Outbox Integration**

Neither DDD alone nor CQRS alone is optimal. The winning architecture blends both:
DDD provides the domain model, aggregate root, and state machine as the organizing principle.
CQRS provides read/write separation and the outbox pattern for reliable Sprint Connect integration.

## Lenses Evaluated

- **Lens A (Domain-Driven Design):** OMS as DDD bounded context. Order aggregate root enforces
  state machine. Anti-Corruption Layers isolate Sprint Connect. RolloutPolicy gates phased rollout.
- **Lens B (CQRS):** OMS split into write model (command handlers) and read model (projection tables).
  Outbox pattern for reliable event delivery. Same handlers serve HTTP and batch entry points.

## Architecture Blueprint

### Bounded Context

OMS is a single bounded context. Aggregate roots:
- **Order** — central aggregate; owns the state machine, line items, delivery slot
- **Booking** — bookingRef, timeSlot, storeCode
- **Delivery** — pickerId, outForDeliveryAt, deliveredAt
- **Invoice** — orderId, invoiceRef, type (ABB/Tax/Credit)

### Order State Machine

```
PENDING
  → BOOKING_CONFIRMED  (ConfirmBooking)
  → PICK_STARTED       (StartPick)
  → PICK_CONFIRMED     (ConfirmPick)
  → OUT_FOR_DELIVERY   (MarkOutForDelivery)
  → DELIVERED          (MarkDelivered)
  → INVOICED           (GenerateInvoice)
  → PAID               (NotifyPaid)
  → CANCELLED          (Cancel — allowed from any state except PAID)
```

All transitions enforced inside the Order aggregate. No external code mutates Order.Status directly.

### Anti-Corruption Layers

- `SprintConnectOrderAdapter` — translates OMS domain events → Sprint Connect outbound API calls
- `SprintConnectFulfillmentAdapter` — translates incoming WMS/TMS events → OMS domain commands
- `BatchFileAdapter` — reads STS batch files, translates to domain commands

### CQRS Read/Write Split

**Write schema:**
```sql
orders(id UUID PK, store_code VARCHAR, status VARCHAR, created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
order_lines(id UUID PK, order_id UUID FK, sku VARCHAR, qty INT, basket_qty INT)
order_events(id UUID PK, order_id UUID FK, event_type VARCHAR, payload JSONB, created_at TIMESTAMPTZ)
```

**Read projections (maintained synchronously in Phase 1):**
```sql
order_status_view(order_id UUID, store_code VARCHAR, status VARCHAR, customer_name VARCHAR, delivery_eta TIMESTAMPTZ)
order_fulfillment_view(order_id UUID, picker_id VARCHAR, basket_qty JSONB, delivery_at TIMESTAMPTZ)
store_order_summary(store_code VARCHAR, date DATE, total_orders INT, status_breakdown JSONB)
```

### Phased Rollout

`RolloutPolicy` domain service:
```csharp
// Injected into command handlers; store codes loaded from config/DB
public class RolloutPolicy
{
    public bool IsEnabled(string storeCode) => _enabledStores.Contains(storeCode);
}
```
Encapsulated as a domain service — not spread across HTTP middleware.

### Integration Flows

**Online (Sprint Connect → OMS):**
Sprint Connect HTTP → OMS Controller → MediatR → Command Handler → Aggregate → Outbox

**Batch (STS files):**
STS batch file → BatchFileAdapter → Same Command Handlers → Aggregate → Outbox

Outbox poller → SprintConnectOrderAdapter → WMS/TMS/POS outbound calls

### .NET Project Structure

```
src/
  OMS.Domain/
    Aggregates/Order.cs               ← aggregate root + state machine
    Aggregates/Booking.cs
    Aggregates/Delivery.cs
    Events/OrderCreated.cs            ← domain events (C# record types)
    Events/BookingConfirmed.cs
    Events/PickStarted.cs
    Events/OrderDelivered.cs
    Policies/RolloutPolicy.cs
    Interfaces/IOrderRepository.cs
  OMS.Application/
    Commands/CreateOrderCommand.cs
    Commands/ConfirmPickCommand.cs
    Commands/MarkDeliveredCommand.cs
    Commands/CancelOrderCommand.cs
    Queries/GetOrderStatusQuery.cs
    Queries/GetFulfillmentDetailsQuery.cs
    Handlers/CreateOrderHandler.cs
    Handlers/GetOrderStatusHandler.cs
  OMS.Infrastructure/
    Persistence/OrderRepository.cs     ← EF Core + PostgreSQL
    Persistence/OutboxWriter.cs        ← writes to order_events table
    Persistence/OutboxPoller.cs        ← polls order_events, calls Sprint Connect
    Projections/OrderStatusProjection.cs
    Adapters/SprintConnectAdapter.cs   ← ACL: OMS events → Sprint Connect
    Adapters/BatchFileAdapter.cs       ← ACL: STS batch files → commands
  OMS.API/
    Controllers/OrderController.cs     ← routes to IMediator
    Controllers/FulfillmentController.cs
```

## Blended Rationale

1. DDD provides the conceptual map (aggregate root, state machine, ACL) the developer needs to
   understand and implement the domain correctly — the diagram arrows map 1:1 to aggregate methods.
2. CQRS prevents the most dangerous OMS anti-pattern: read queries (POS tracking, order status)
   contaminating and blocking write transactions (order creation, pick confirmation).
3. Outbox pattern makes Sprint Connect event delivery reliable under AKS pod restarts and network
   partitions — no lost events on crash.
4. RolloutPolicy (DDD domain service) is testable in isolation — superior to infrastructure-level
   middleware for a phased rollout that will evolve over multiple phases.
5. The DDD + CQRS combination is canonical in the .NET ecosystem (MediatR + EF Core + MassTransit
   outbox) with well-established patterns the developer can reference.

## Rejected Options

- **Pure DDD without CQRS:** Read paths load full aggregates — expensive for frequent POS status
  queries. N+1 risk (see D001) when loading Order with line items for every status check.
- **Pure CQRS without DDD:** Command handlers accumulate unguarded business logic over time.
  Without aggregate invariants, invalid state transitions (e.g. MarkDelivered before PickConfirmed)
  are only caught by scattered if-checks, not by the model.

## Tradeoffs Accepted

- Higher file count (commands, handlers, queries, domain events) — accepted for long-term
  maintainability.
- Projection maintenance overhead — every write must update projections; bugs cause read-side
  staleness. Mitigated by synchronous projection updates in Phase 1.
- Full DDD vocabulary required — aggregate roots, invariants, ACL, domain events. Team must learn.

## Related KB Entries

- D012 — Distributed Transaction Strategy: if the OMS ever needs to coordinate across services
  (e.g., payment service), apply Saga pattern as per D012.
- D015 — MSSQL SEQUENCE + Idempotency: the idempotency check in CreateOrderHandler follows the
  same pattern as D015 (check-before-insert with idempotency key).
- D001 — EF Core Hot-Path: when loading Order aggregates for write commands, apply IDbContextFactory
  + eager loading (Include) to avoid N+1 on order_lines.

## Next Steps

1. Define Order aggregate fully in OMS.Domain/Aggregates/Order.cs — implement all state transitions
   and domain events as per the state machine above.
2. Set up PostgreSQL schema with orders, order_lines, order_events (outbox) tables.
3. Implement CreateOrderHandler first — it is the entry point for all order flows.
4. Add RolloutPolicy backed by a configuration table (allows enabling stores without redeployment).
5. Build SprintConnectAdapter as an ACL — map OMS domain events to Sprint Connect API contracts.
6. Add OrderStatusProjection updated synchronously in Phase 1.
7. Once stable, consider async outbox poller with retry/dead-letter for production reliability.
