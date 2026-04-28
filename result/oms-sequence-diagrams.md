# OMS Sequence Diagrams

> Sprint Connect owns and runs the OMS. They are represented as a single participant in all diagrams.

---

## UC1 — Pre-paid Delivery Order

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant TMS as TMS
    participant POS as POS

    C->>GW: Branch near me
    GW-->>C: Nearby branches

    C->>GW: Request time slot
    GW->>PS: Request time slot
    PS->>SC: Request time slot
    SC-->>PS: Available slots
    PS-->>GW: Available slots
    GW-->>C: Available slots

    C->>GW: Create Booking
    GW->>PS: Create Booking
    PS->>SC: Create Booking
    SC-->>PS: Booking confirmed
    PS-->>GW: Booking confirmed
    GW-->>C: Booking confirmed

    C->>GW: Sale Order
    GW->>PS: Sale Order
    PS->>SC: Sale Order
    Note over SC: Order created and persisted

    SC->>WMS: Pick Started
    WMS-->>SC: Pick Started ACK

    WMS->>SC: Pick Confirmed (basket qty)
    WMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation (actual qty)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total to customer)

    WMS->>SC: ABB/Tax Invoice (Pre-paid)
    Note over SC: Invoice generated before dispatch

    TMS->>SC: Out for Delivery
    Note over SC: Order status → OutForDelivery

    TMS->>SC: Delivered
    Note over SC: Order status → Delivered
    SC-->>C: Notify Paid
```

---

## UC2 — Pay-on-Delivery (POD) Order

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant TMS as TMS
    participant POS as POS

    C->>GW: Branch / Timeslot / Booking / Sale Order
    GW->>PS: forward
    PS->>SC: forward
    Note over SC: Order created and persisted

    SC->>WMS: Pick Started
    WMS->>SC: Pick Confirmed (basket qty)
    WMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation (actual qty)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total to customer)

    TMS->>SC: Out for Delivery
    Note over SC: Order status → OutForDelivery

    TMS->>SC: Delivered
    TMS->>SC: POS Recalculation
    Note over SC: Order status → Delivered
    SC->>POS: POS Recalculation (for POD invoice)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (final total to customer)

    Note over SC,TMS: Invoice issued AFTER delivery for POD
    SC->>PS: ABB/Tax Invoice (POD)
    PS->>TMS: ABB/Tax Invoice (POD)

    SC->>SC: Inquiry payment / Get Payment link
    SC-->>C: Payment link sent

    C->>SC: Payment submitted
    SC-->>C: Notify Paid
```

---

## UC3 — Order Cancellation

```mermaid
sequenceDiagram
    actor C as Customer
    participant SC as Sprint Connect
    participant TMS as TMS

    C->>SC: Cancel Order (any lifecycle stage)
    SC->>SC: Order.Cancel(reason) — status → Cancelled

    SC->>TMS: Cancelled Order
    TMS-->>SC: Delivery slot released

    alt Pre-paid order
        SC->>TMS: Credit Note
        TMS-->>SC: Credit Note ACK
        SC-->>C: Refund initiated
    end
```

---

## UC4 — Delivery Reschedule

```mermaid
sequenceDiagram
    actor C as Customer
    participant SC as Sprint Connect
    participant TMS as TMS

    C->>SC: Request reschedule (new time slot)
    SC->>SC: Order.Reschedule(newSlot)

    SC->>TMS: Reschedule Order
    TMS-->>SC: New slot confirmed

    SC->>SC: DeliverySlot updated
    SC-->>C: New delivery window confirmed

    Note over SC: No re-picking, no new invoice triggered
```

---

## UC5 — Partial Pick & Credit Note

```mermaid
sequenceDiagram
    participant WMS as WMS
    participant SC as Sprint Connect
    participant POS as POS
    participant TMS as TMS
    participant STS as STS

    SC->>WMS: Pick Started
    Note over WMS: Picker finds item out of stock
    WMS->>SC: Pick Confirmed (reduced basket qty)

    SC->>POS: POS Recalculation (actual qty)
    POS-->>SC: Reduced total

    WMS->>SC: ABB/Tax Invoice (reduced amount)

    SC->>TMS: Credit Note (POD)
    TMS-->>SC: Credit Note ACK

    STS->>SC: Credit Note (batch reconciliation)
    Note over SC,STS: Batch path must be idempotent — same credit note may arrive twice
    SC->>SC: Deduplicate by invoice ref
```

---

## UC6 — Non-Rolled-Out Store (Legacy Path)

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant TMS as TMS
    participant BE as Backend

    C->>GW: Request time slot (store not on rollout)
    GW->>PS: Request time slot
    PS->>TMS: Request time slot
    TMS->>BE: Request time slot
    BE-->>TMS: Available slots
    TMS-->>PS: Available slots
    PS-->>GW: Available slots
    GW-->>C: Available slots

    C->>GW: Create Booking
    GW->>PS: Create Booking
    PS->>TMS: Create Booking
    TMS->>BE: Create Booking
    BE-->>TMS: Booking confirmed
    TMS-->>PS: Booking confirmed
    PS-->>GW: Booking confirmed
    GW-->>C: Booking confirmed

    C->>GW: Sale Order
    GW->>SC: Sale Order API
    SC->>BE: Sale Order API
    BE-->>SC: Order accepted

    BE->>BE: Internal fulfillment
    BE-->>SC: Order status updates
    SC-->>C: Order confirmed
```

---

## UC7 — Item Master & Product Picture Sync

```mermaid
sequenceDiagram
    participant STS as STS
    participant FG as File Gateway
    participant SC as Sprint Connect
    participant WMS as WMS
    participant POS as POS

    Note over STS,SC: Scheduled batch job

    STS->>SC: Item Master (batch file)
    SC->>FG: Forward Item Master
    FG->>SC: Item Master (batch) — routed to WMS/POS
    SC-->>WMS: Catalog synced
    SC-->>POS: Catalog synced

    Note over FG,SC: Product pictures use pull-by-URL (not file push)
    FG->>SC: Product Pictures (retrieve via URL)
    SC-->>WMS: Product images available
```

---

## UC8 — Click & Collect Order

```mermaid
sequenceDiagram
    actor C as Customer
    actor SS as Store Staff
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant POS as POS

    C->>GW: Branch near me
    GW-->>C: Nearby branches

    C->>GW: Request collection slot
    GW->>PS: Request collection slot
    PS->>SC: Request collection slot
    SC-->>PS: Available slots
    PS-->>GW: Available slots
    GW-->>C: Available slots

    C->>GW: Create Booking (collection)
    GW->>PS: Create Booking
    PS->>SC: Create Booking
    SC-->>C: Booking confirmed

    C->>GW: Sale Order (FulfillmentType=ClickAndCollect)
    GW->>PS: Sale Order
    PS->>SC: Sale Order
    Note over SC: Order created — no TMS involved

    SC->>WMS: Pick Started
    WMS-->>SC: Pick Started ACK

    WMS->>SC: Pick Confirmed (basket qty)
    WMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation (actual qty)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total to customer)

    WMS->>SC: ABB/Tax Invoice
    Note over SC: Status → ReadyForCollection
    SC-->>C: Ready for Collection notification

    C->>SC: Customer arrives at store
    SS->>SC: MarkCollected (staff confirms collection)
    Note over SC: Status → Collected → Invoiced → Paid
    SC-->>C: Notify Paid
```

---

## UC9 — Express Delivery Order

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant TMS as TMS
    participant POS as POS

    C->>GW: Sale Order (FulfillmentType=Express, no booking)
    GW->>PS: Sale Order
    PS->>SC: Sale Order
    Note over SC: FulfillmentRouter.RequiresBooking(Express) = false\nOrder goes Pending → PickStarted directly

    SC->>WMS: Pick Started (immediate)
    WMS-->>SC: Pick Started ACK

    WMS->>SC: Pick Confirmed (basket qty)
    WMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation (actual qty)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total to customer)

    WMS->>SC: ABB/Tax Invoice (Pre-paid)
    Note over SC: Invoice generated before dispatch

    TMS->>SC: Out for Delivery
    Note over SC: Order status → OutForDelivery

    TMS->>SC: Delivered
    Note over SC: Order status → Delivered
    SC-->>C: Notify Paid
```

---

## UC10 — Weight-Based Item Order

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant TMS as TMS
    participant POS as POS

    C->>GW: Sale Order (weight-based items e.g. 500g chicken, 1kg tomatoes)
    GW->>PS: Sale Order
    PS->>SC: Sale Order
    Note over SC: OrderLine.requestedAmount=500, unitOfMeasure=Gram\nOrderLine.requestedAmount=1000, unitOfMeasure=Gram

    SC->>WMS: Pick Started (with weight-based order lines)
    WMS-->>SC: Pick Started ACK

    Note over WMS: Picker physically weighs each item
    WMS->>SC: Pick Confirmed (actual weights per OrderLine\ne.g. pickedAmount=480g chicken, 950g tomatoes)
    WMS->>SC: POS Recalculation (weight-based)
    SC->>POS: POS Recalculation (actual gram/kg quantities)
    Note over SC,POS: CalculateTotal = unitPrice × (pickedAmount / 1000) for gram items
    POS-->>SC: Final prices based on actual weight
    SC-->>GW: POS Recalculation (updated total — lower than estimate)

    WMS->>SC: ABB/Tax Invoice (reflects actual picked weight)

    alt actual weight < requested weight
        SC->>TMS: Credit Note (weight shortage difference)
        TMS-->>SC: Credit Note ACK
    end

    TMS->>SC: Out for Delivery
    TMS->>SC: Delivered
    Note over SC: Order status → Delivered
    SC-->>C: Notify Paid
```

---

## UC11 — Multi-Vehicle Split Delivery (Large Items)

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant WMS as WMS
    participant TMS as TMS
    participant POS as POS

    C->>GW: Sale Order (sofa, wardrobe, bed frame, mattress)
    GW->>PS: Sale Order
    PS->>SC: Sale Order
    Note over SC: Order created — shipments list empty

    SC->>WMS: Pick Started
    WMS-->>SC: Pick Started ACK

    WMS->>SC: Pick Confirmed (all items)
    WMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation (full order)
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total)

    WMS->>SC: AssignPackages
    Note over SC,WMS: PKG1: sofa + wardrobe → Truck (TrackingId=TRK001)\nPKG2: bed frame + mattress → Van (TrackingId=TRK002)
    Note over SC: Order.AssignPackages(packages)\nraises PackagesAssigned

    SC->>TMS: Register Package PKG1 (TrackingId=TRK001, VehicleType=Truck)
    SC->>TMS: Register Package PKG2 (TrackingId=TRK002, VehicleType=Van)

    TMS->>SC: PackageOutForDelivery (TrackingId=TRK001)
    Note over SC: PKG1 → OutForDelivery\nOrder status → Delivering
    SC-->>GW: Package 1 of 2 is on its way

    TMS->>SC: PackageOutForDelivery (TrackingId=TRK002)
    Note over SC: PKG2 → OutForDelivery

    TMS->>SC: PackageDelivered (TrackingId=TRK001)
    Note over SC: PKG1 Delivered — IsFullyDelivered() = false
    SC-->>GW: Package 1 of 2 delivered

    TMS->>SC: POS Recalculation
    SC->>POS: POS Recalculation
    POS-->>SC: Recalculated prices
    SC-->>GW: POS Recalculation (updated total)

    TMS->>SC: PackageDelivered (TrackingId=TRK002)
    Note over SC: PKG2 Delivered — IsFullyDelivered() = true\nraises OrderFullyDelivered\nOrder status → Delivered

    WMS->>SC: ABB/Tax Invoice (full order)
    Note over SC: Single invoice for all packages
    SC-->>C: Notify Paid
```

---

## UC12 — Modify Order Lines After Placement

```mermaid
sequenceDiagram
    actor C as Customer
    participant GW as Gateway
    participant PS as Proxy Service
    participant SC as Sprint Connect
    participant POS as POS

    C->>GW: Modify order lines (add / remove / change qty)
    GW->>PS: ModifyOrderLines(modifications)
    PS->>SC: ModifyOrderLines(modifications)
    Note over SC: Guard: Pending or BookingConfirmed only\nReject if PickStarted or later

    SC->>SC: Apply OrderLineModification entries
    Note over SC: Raises OrderLinesModified

    SC->>POS: POS Recalculation (updated lines)
    POS-->>SC: Recalculated prices
    SC-->>GW: Updated total notified to customer

    alt PrePaid and total increased
        Note over SC: Re-authorize payment delta
    end
```

---

## UC17 — Put Away (Returned Items)

```mermaid
sequenceDiagram
    actor WS as Warehouse Staff
    participant WMS as WMS
    participant SC as Sprint Connect

    Note over WMS,SC: Precondition: Return is PickedUp — items collected from customer

    WMS->>SC: ReceivedAtWarehouse(returnId)
    Note over SC: return.status → ReceivedAtWarehouse

    Note over WMS,WS: Staff inspects each item
    WS->>WMS: Assign ItemCondition per item\n(Resellable / Damaged / Dispose)
    WMS->>WMS: Assign StorageLocation (Sloc) to Resellable items
    Note over WS: Staff physically moves items to assigned Sloc

    WMS->>WMS: Confirm put away — inventory updated
    WMS->>SC: PutAwayConfirmed(returnId, items[sku, condition, sloc, qty])

    Note over SC: For each item:\n  Resellable → put_away_status = Restocked\n  Damaged/Dispose → put_away_status = Disposed
    SC->>SC: Write return_put_away_logs (audit per item)
    Note over SC: return.status → PutAway

    alt Refund not yet issued
        SC->>SC: Trigger RefundProcessed
        SC-->>WS: Refund confirmation
    end
```

---

## UC14 — Return & Refund

```mermaid
sequenceDiagram
    actor C as Customer
    participant SC as Sprint Connect
    participant TMS as TMS

    C->>SC: RequestReturn(reason)
    Note over SC: Guard: Delivered, Collected, or Paid only
    SC->>SC: Order status note — return initiated
    Note over SC: Raises ReturnRequested

    SC->>TMS: Schedule return pickup
    TMS-->>SC: Return pickup confirmed

    Note over TMS: Driver collects items from customer
    TMS->>SC: Items collected

    SC->>SC: Evaluate refund amount
    Note over SC: Raises RefundProcessed
    SC-->>C: Refund issued

    Note over SC: Order status → Returned
```

---

## UC15 — Order On Hold / Release

```mermaid
sequenceDiagram
    actor SS as Store Staff
    participant SC as Sprint Connect

    SS->>SC: HoldOrder(reason)
    Note over SC: Store pre-hold state\nOrder status → OnHold\nRaises OrderOnHold

    Note over SC: All lifecycle transitions blocked

    SS->>SC: ReleaseOrder()
    Note over SC: Order status → [pre-hold state]\nRaises OrderReleased

    Note over SC: Lifecycle resumes from where it paused
```

---

## UC16 — Package Lost (Exception Handling)

```mermaid
sequenceDiagram
    actor SS as Store Staff
    participant TMS as TMS
    participant SC as Sprint Connect

    TMS->>SC: PackageLost(trackingId)
    Note over SC: Raises PackageLost\nOrder status → OnHold (reason=PackageLost)

    SC-->>SS: Alert: package lost — intervention required

    alt Re-dispatch path
        SS->>SC: ReassignPackages(newGroups)
        SS->>SC: ReleaseOrder()
        Note over SC: Order resumes from Packed
        SC->>TMS: Register replacement Package
    else Cannot recover — cancel
        SS->>SC: Cancel(reason=PackageLost)
        Note over SC: Order → Cancelled\nTrigger Credit Note if PrePaid
    end
```

---

## UC13 — Reassign Packages

```mermaid
sequenceDiagram
    participant WMS as WMS
    participant SC as Sprint Connect
    participant TMS as TMS

    Note over SC: Order is PickConfirmed — all Packages are Pending
    WMS->>SC: ReassignPackages(newGroups)
    Note over SC: Guard: all Packages must be Pending\nReject if any Package is OutForDelivery

    SC->>SC: Replace Package entities with new groupings
    Note over SC: Raises PackagesReassigned

    SC->>TMS: De-register old Packages (by TrackingId)
    SC->>TMS: Register new Packages (updated TrackingIds, OrderLineIds, VehicleTypes)
    TMS-->>SC: New Packages registered

    Note over SC,TMS: Delivery continues with new package groupings
```
