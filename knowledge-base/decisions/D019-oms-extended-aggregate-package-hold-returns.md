---
id: D019
title: "OMS Extended Aggregate — Package Value Object, OnHold Snapshot, Returns Sub-Machine, Multi-Channel"
date: 2026-04-28
problem_id: P014
chosen_option: "Extended Order Aggregate with Package Value Object, OnHold State Snapshot, and In-Aggregate Returns Sub-Machine"
tags:
  - oms
  - order-lifecycle
  - ddd
  - aggregate
  - state-machine
  - returns
  - exception-handling
  - multi-channel
  - package-tracking
  - fulfillment
  - cqrs
  - outbox
  - domain-driven-design
  - dotnet
  - postgresql
related_snippets:
  - S019
supersedes: ~
extends: D018
---

# D019 — OMS Extended Aggregate: Package Value Object, OnHold Snapshot, Returns Sub-Machine, Multi-Channel

## Chosen Option

**Extended Order Aggregate with Package Value Object, OnHold State Snapshot, and In-Aggregate Returns Sub-Machine**

Extend the existing D018 DDD aggregate (P013) inward rather than introducing an external Saga
orchestrator. Package becomes a value object collection on the Order aggregate. OnHold stores a
PreHoldState snapshot for non-destructive resume. Returns (ReturnRequested → ReturnPickupScheduled →
Returned) are modelled as aggregate state transitions with domain events dispatched to the existing
Outbox — the SprintConnectAdapter ACL handles TMS return pickup coordination.

## Extends

This decision extends D018 (OMS Greenfield DDD+CQRS). D018 remains the authoritative baseline for
the Phase 1 architecture. D019 covers the Phase 2 requirements extensions.

## Lenses Evaluated

- **Lens A (Domain-Driven Design):** Extend Order aggregate with Package value object, PreHoldState
  snapshot, and Returns sub-state machine. All new states and entities live inside the aggregate
  boundary. TMS coordination for returns handled by existing Outbox + ACL pattern.
- **Lens B (Saga Pattern):** Introduce dedicated Saga orchestrators (ReturnSaga, PackageLostSaga)
  external to the Order aggregate. Order aggregate stays thin; Sagas own multi-step cross-service
  coordination with explicit compensating actions.

## Extended Order State Machine

```
PENDING
  → BOOKING_CONFIRMED  (ConfirmBooking)
  → PICK_STARTED       (StartPick)
  → PICK_CONFIRMED     (ConfirmPick)
  → PACKED             (AssignPackages)      [NEW — WMS calls after PickConfirmed]
  → OUT_FOR_DELIVERY   (MarkOutForDelivery)  [was: direct from PICK_CONFIRMED]
  → DELIVERED          (MarkDelivered)
  → INVOICED           (GenerateInvoice)
  → PAID               (NotifyPaid)          [terminal]
  → CANCELLED          (Cancel)              [terminal — from any pre-Delivered state]
  → PAYMENT_FAILED     (ReportPaymentFailure) [NEW — terminal — invoice rejected]
  → ON_HOLD            (PlaceOnHold)         [NEW — from any non-terminal state; stores PreHoldState]
      → [PreHoldState] (Release)             [non-destructive resume to exact prior state]
  → RETURN_REQUESTED   (RequestReturn)       [NEW — from DELIVERED only]
  → RETURN_PICKUP_SCHEDULED (ScheduleReturnPickup) [NEW — TMS ACL callback]
  → RETURNED           (ConfirmReturn)       [NEW — terminal]
```

PackageLost exception: ReportPackageLost(trackingId) marks Package.Status = Lost,
then calls PlaceOnHold internally. Staff resolves via Release + re-dispatch or Cancel.

## Architecture Decisions

### Package Entity

Shipment entity removed. Package is a value object (record type) owned by the Order aggregate:

```csharp
public record Package(
    Guid PackageId,
    string TrackingId,
    string VehicleType,
    PackageStatus Status,
    IReadOnlyList<Guid> OrderLineIds
);
public enum PackageStatus { Packed, OutForDelivery, Delivered, Lost }
```

- Packages assigned by WMS via `AssignPackages` command at `PickConfirmed` state.
- TMS reports events per `TrackingId` — Package.TrackingId is the correlation key.
- Packages stored as JSONB in `orders.packages` column (or normalized to `order_packages` table).

### OnHold Mechanism

Non-destructive pause implemented as:
1. `_preHoldState` backing field on Order aggregate (persisted to `orders.pre_hold_state` column).
2. `PlaceOnHold(reason)` stores current status in `_preHoldState`, sets status to `OnHold`.
3. `Release()` restores `_preHoldState`, clears it.
4. Invariant: `PlaceOnHold` throws if status is already terminal (Delivered, Paid, Cancelled,
   Returned, PaymentFailed).

### Returns Sub-Machine

Full returns lifecycle modelled as Order aggregate state transitions:

```
DELIVERED → RequestReturn() → RETURN_REQUESTED
RETURN_REQUESTED → ScheduleReturnPickup(returnTrackingId) → RETURN_PICKUP_SCHEDULED
RETURN_PICKUP_SCHEDULED → ConfirmReturn() → RETURNED
```

Domain events raised at each step are dispatched to the Outbox. The existing
`SprintConnectFulfillmentAdapter` ACL handles:
- `ReturnRequested` event → calls TMS return pickup scheduling API
- `ReturnPickupScheduled` event → updates order tracking
- `ReturnDelivered` TMS callback → triggers `ConfirmReturn` command on Order

Refund vs Credit Note distinction:
- **Refund**: raised on `OrderReturned` domain event — post-delivery return flow.
- **Credit Note**: raised on `PickConfirmed` event for partial pick shortages — unrelated to Returns state machine.

### PackageLost Exception

`Order.ReportPackageLost(trackingId)`:
1. Finds Package by TrackingId in `_packages` collection.
2. Updates `Package.Status = PackageStatus.Lost`.
3. Calls `PlaceOnHold($"PackageLost:{trackingId}")` internally.
4. Raises `PackageLostReported` domain event.

Staff resolution options (via command handlers):
- **Re-dispatch**: `Release()` → `MarkOutForDelivery()` (new TMS booking assigned by adapter).
- **Cancel**: `Release()` → `Cancel("PackageLost")`.

### Extended ChannelType

```csharp
public enum ChannelType
{
    Gateway,          // existing
    B2B,              // existing
    Kiosk,            // existing
    Marketplace,      // NEW — Shopee, Lazada, TikTok Shop
    POSTerminal,      // NEW — in-store POS orders
    BulkImport        // NEW — batch file import
}
```

Channel-specific validation rules isolated in `ChannelOrderFactory`:
- `Marketplace`: requires `MarketplaceOrderRef` and `MarketplaceName`.
- `POSTerminal`: requires `TerminalId` and `StoreCode`.
- `BulkImport`: requires `BatchFileRef`; skips RolloutPolicy gate (bulk imports are pre-authorized).

### Schema Extensions

```sql
-- Extend orders table
ALTER TABLE orders
    ADD COLUMN pre_hold_state VARCHAR(30)    NULL,   -- stores state before OnHold
    ADD COLUMN channel_type   VARCHAR(30)    NOT NULL DEFAULT 'Gateway',
    ADD COLUMN packages       JSONB          NULL;   -- Package[] as JSONB

-- New domain events to register in outbox
-- ReturnRequested, ReturnPickupScheduled, OrderReturned
-- PackageLostReported, OrderPlacedOnHold, OrderReleased
-- PackagesAssigned, PaymentFailed
```

### Why Not Saga Orchestrators

The Saga Pattern lens correctly identifies that Returns and PackageLost involve cross-service
coordination (OMS + TMS). However:

1. The existing Outbox + SprintConnectAdapter ACL (D018) already provides the compensation
   mechanism: domain events → outbox → adapter → TMS call. This is functionally a two-step saga
   without the Saga state machine overhead.
2. Returns involve exactly two services (OMS + TMS). D012 recommends reserving Saga orchestrators
   for 3+ service coordination — two-service flows are adequately handled by outbox+ACL.
3. OnHold and PackageLost resolution are not automated compensating flows — they require staff
   intervention. Saga orchestrators are designed for automated compensation, not human-in-the-loop
   resolution paths.
4. Introducing MassTransit StateMachine or equivalent would require a Saga state store, new
   infrastructure, and new operational runbooks — significant cost for marginal benefit at this scale.

## Blended Rationale

1. **Aggregate cohesion**: All order lifecycle state (including Package, OnHold, Returns) lives in
   one aggregate boundary — a single `GetByIdAsync` gives the complete order picture. No distributed
   query required to determine order state.
2. **Invariant enforcement**: New state transitions (AssignPackages requires PickConfirmed; RequestReturn
   requires Delivered; PlaceOnHold blocks from terminal states) are enforced by the aggregate — not
   scattered across command handlers.
3. **Outbox handles TMS coordination**: The Returns flow TMS coordination is handled by raising domain
   events to the existing outbox — the ACL pattern already established in D018 is extended, not
   replaced.
4. **OnHold snapshot pattern**: PreHoldState stored in the aggregate (not reconstructed from event
   history) makes Resume deterministic and idempotent — no event replay needed.
5. **Saga lens value**: The Saga lens shaped the domain events design — each Returns step raises an
   explicit event (ReturnRequested, ReturnPickupScheduled, OrderReturned) enabling compensation if
   TMS coordination fails, without needing a full Saga orchestrator.

## Tradeoffs Accepted

- **Aggregate surface area growth**: Order aggregate now has 14 status values and a Package collection.
  Mitigated by strong invariant guards and clear state machine documentation.
- **JSONB for Package storage**: Packages stored as JSONB column avoids a join on the write path.
  Tradeoff: packages are not individually queryable via SQL without JSONB operators. If Package-level
  queries are needed (e.g., "find all orders with lost packages"), add `order_packages` normalized
  table alongside JSONB.
- **TMS return coordination via ACL**: If TMS return pickup API fails, the domain event sits
  unprocessed in the outbox. Dead-letter and retry handling in the outbox poller is required for
  production robustness (this is a known outbox operational requirement from D018).

## Related KB Entries

- **D018** — OMS Greenfield Architecture: this decision extends D018. The DDD aggregate, CQRS
  command handler, and Outbox patterns from D018 are the foundation.
- **D012** — Distributed Transaction Strategy: the decision to use outbox+ACL instead of Saga
  orchestrators is consistent with D012's guidance that Saga is reserved for 3+ service coordination.
- **D015** — Idempotency Pattern: all new command handlers (AssignPackages, RequestReturn,
  PlaceOnHold, Release) must implement the D015 idempotency check pattern.
- **D001** — EF Core Hot-Path: when loading Order aggregate with Package collection for write
  commands, apply eager loading on the packages collection.

## Next Steps

1. Extend `OrderStatus` enum with: Packed, OnHold, ReturnRequested, ReturnPickupScheduled, Returned,
   PaymentFailed.
2. Add `Package` record and `PackageStatus` enum to `OMS.Domain`.
3. Add `_packages`, `_preHoldState` fields to Order aggregate; implement `AssignPackages`,
   `PlaceOnHold`, `Release`, `RequestReturn`, `ScheduleReturnPickup`, `ConfirmReturn`,
   `ReportPackageLost`, `ReportPaymentFailure`.
4. Add corresponding domain events: `PackagesAssigned`, `OrderPlacedOnHold`, `OrderReleased`,
   `ReturnRequested`, `ReturnPickupScheduled`, `OrderReturned`, `PackageLostReported`,
   `PaymentFailed`.
5. Add `ChannelType` enum to `OMS.Domain`; update `Order.Create` factory and `ChannelOrderFactory`.
6. Run schema migration: add `pre_hold_state`, `channel_type`, `packages` columns.
7. Extend `SprintConnectFulfillmentAdapter` to handle TMS return pickup callbacks.
8. Add idempotency checks (D015 pattern) to all new command handlers.
