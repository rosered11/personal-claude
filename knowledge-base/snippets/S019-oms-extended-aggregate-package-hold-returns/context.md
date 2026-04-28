---
id: S019
title: "OMS Extended Order Aggregate — Package, OnHold, Returns, PackageLost, Multi-Channel"
language: C#
when_to_use:
  - Extending the D018 OMS Order aggregate with Package entity, OnHold, and Returns lifecycle
  - When WMS reports physical packing via AssignPackages and TMS reports per-TrackingId
  - When you need a non-destructive OnHold/Release pattern with PreHoldState snapshot
  - Implementing Returns & Reverse Logistics in a DDD aggregate (ReturnRequested → ReturnPickupScheduled → Returned)
  - When PackageLost exception handling must auto-trigger OnHold and support re-dispatch or cancel resolution
  - Extending ChannelType to Marketplace, POSTerminal, BulkImport
related_problems:
  - P014
  - P013
related_decisions:
  - D019
  - D018
---

# S019 — OMS Extended Order Aggregate: Package, OnHold, Returns, PackageLost, Multi-Channel

## What this shows

1. **Extended OrderStatus enum** — adds Packed, OnHold, ReturnRequested, ReturnPickupScheduled,
   Returned, PaymentFailed to the existing P013/D018 state machine.
2. **Package value object** — replaces Shipment; carries TrackingId, VehicleType, PackageStatus,
   and OrderLineIds directly under the Order aggregate.
3. **OnHold/Release with PreHoldState snapshot** — non-destructive pause from any non-terminal
   state; aggregate stores prior state and restores it on Release.
4. **Returns sub-machine** — RequestReturn → ScheduleReturnPickup → ConfirmReturn; domain events
   dispatched to Outbox for TMS coordination via ACL.
5. **PackageLost exception** — marks Package.Status = Lost, auto-triggers OnHold, staff resolves
   via Release + re-dispatch or Cancel.
6. **ChannelType extension** — Marketplace, POSTerminal, BulkImport added with channel-specific
   factory validation.
7. **Domain events** for all new transitions (PackagesAssigned, OrderPlacedOnHold, OrderReleased,
   ReturnRequested, ReturnPickupScheduled, OrderReturned, PackageLostReported, PaymentFailed).

## How to extend

- Add `AssignPackagesHandler`, `PlaceOnHoldHandler`, `ReleaseHandler`, `RequestReturnHandler`,
  `ScheduleReturnPickupHandler`, `ConfirmReturnHandler`, `ReportPackageLostHandler` following the
  D018 `CreateOrderHandler` pattern.
- Extend `SprintConnectFulfillmentAdapter` to handle TMS return pickup callback and map it to
  `ScheduleReturnPickupCommand`.
- Add `order_packages` normalized table if Package-level SQL queries are needed (lost package reports,
  per-TrackingId delivery dashboards).
- Apply D015 idempotency check in each new command handler.
