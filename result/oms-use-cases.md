# OMS Use Cases & User Journeys

Derived from the Phase 1 System Integration Diagram.

> Sprint Connect owns and runs the OMS. All order management logic lives inside Sprint Connect — they are treated as a single system.

---

## Systems Reference

| System | Role |
|---|---|
| Gateway | Entry point — customer-facing API |
| Proxy Service | Routes requests between Gateway and Sprint Connect |
| Sprint Connect | Integration hub + OMS — owns order lifecycle and orchestration |
| File Gateway | Batch file transfers |
| WMS | Warehouse Management — picking/packing |
| TMS | Transport Management — delivery/scheduling |
| POS | Point-of-Sale — pricing & recalculation |
| Backend | Handles non-rolled-out stores |
| STS | Batch source for invoices, credit notes, item master |

---

## UC1 — Customer Places a Pre-paid Delivery Order

**Actors:** Customer, Gateway, Proxy Service, Sprint Connect, WMS, TMS, POS

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Searches for nearby branch (`Branch near me`) |
| 2 | Customer | Requests an available delivery time slot |
| 3 | Customer | Creates a booking for the selected slot |
| 4 | Customer | Confirms sale — Sprint Connect creates a Sale Order |
| 5 | Sprint Connect → POS | POS Recalculation: prices/promotions re-evaluated at order time |
| 6 | Sprint Connect → WMS | Pick Started: WMS notifies the store picker |
| 7 | WMS → Sprint Connect | Pick Confirmed: picker confirms with actual basket quantity |
| 8 | Sprint Connect → POS | POS Recalculation again: adjusts for any quantity changes from picking |
| 9 | WMS → Sprint Connect | AssignPackages: WMS packs items into Package(s) with TrackingId — Order → Packed |
| 10 | WMS → Sprint Connect | ABB/Tax Invoice (Pre-paid): invoice generated before dispatch |
| 11 | TMS → Sprint Connect | Out for Delivery: TMS notifies Sprint Connect that driver has started delivery |
| 12 | TMS → Sprint Connect | Delivered: TMS reports delivery completed, Sprint Connect closes order |
| 13 | Sprint Connect | Notify Paid: payment status updated |

**Key insight:** POS recalculation happens twice — once at order creation and once after picking — because actual picked quantities may differ from what the customer ordered.

---

## UC2 — Customer Places a Pay-on-Delivery (POD) Order

**Actors:** Customer, Gateway, Proxy Service, Sprint Connect, WMS, TMS, POS

Same as UC1 through Step 9, then diverges:

| Step | Who | What happens |
|---|---|---|
| 1–9 | (same as UC1) | Order created, picked |
| 10 | TMS → Sprint Connect | Out for Delivery: TMS notifies Sprint Connect driver has started delivery |
| 11 | TMS → Sprint Connect | Delivered: TMS reports driver completed delivery |
| 12 | Sprint Connect → TMS | ABB/Tax Invoice (POD): invoice generated **after** delivery |
| 13 | Sprint Connect | Inquiry payment / Get Payment link: Sprint Connect requests a payment link |
| 14 | Customer | Pays via the link |
| 15 | Sprint Connect | Notify Paid: payment confirmed, order fully closed |

**Key insight:** Invoice is issued post-delivery, not pre-dispatch. Payment link is requested dynamically after the Delivered event arrives from TMS.

---

## UC3 — Order is Cancelled

**Actors:** Customer or Store Staff, Sprint Connect, TMS

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Triggers cancellation (can happen at any lifecycle stage) |
| 2 | Sprint Connect | Cancels order internally, raises OrderCancelled event |
| 3 | Sprint Connect → TMS | TMS notified to cancel any scheduled delivery slot |
| 4 | Sprint Connect | If pre-paid: triggers Credit Note flow (see UC5) |

**Key insight:** Cancellation is a single event that fans out — TMS must release the slot, and the payment system must be notified if money already moved.

---

## UC4 — Delivery is Rescheduled

**Actors:** Customer or Store Staff, Sprint Connect, TMS

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Requests a new delivery time slot |
| 2 | Sprint Connect → TMS | Reschedule Order: TMS receives the new slot request |
| 3 | TMS | Reassigns driver/vehicle for the new slot |
| 4 | Sprint Connect | Delivery window updated internally |

**Key insight:** Reschedule does not re-trigger picking or invoicing — it only updates the TMS delivery window.

---

## UC5 — Partial Pick / Credit Note Issued

**Actors:** Store Picker, WMS, Sprint Connect, POS, TMS, STS

| Step | Who | What happens |
|---|---|---|
| 1 | WMS | Pick Confirmed with reduced basket quantity (item out of stock) |
| 2 | Sprint Connect → POS | POS Recalculation: total recalculated on actual picked quantity |
| 3 | WMS → Sprint Connect | ABB/Tax Invoice: invoice reflects reduced amount |
| 4 | Sprint Connect → TMS | Credit Note (POD): difference refunded to customer post-delivery |
| 5 | STS → Sprint Connect (batch) | Credit Note: batch reconciliation from STS confirms the credit |

**Key insight:** The credit note can originate from two paths — online (WMS) for pre-paid and POD, and batch (STS) for reconciliation. Both paths must be idempotent.

---

## UC6 — Non-Rolled-Out Store Order (Legacy Path)

**Actors:** Customer, Sprint Connect, TMS, Backend

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Requests time slot (store not on Sprint Connect rollout) |
| 2 | Gateway → Proxy Service → TMS → Backend | Request Time slot: Proxy Service routes to TMS, TMS forwards to Backend for slot availability |
| 3 | Gateway → Proxy Service → TMS → Backend | Create Booking: same path — TMS creates booking in Backend |
| 4 | Gateway → Sprint Connect → Backend | Sale Order API: Sprint Connect sends sale order directly to Backend |
| 5 | Backend | Processes the order through its own fulfillment path |

**Key insight:** Same customer experience, different integration path. Sprint Connect's RolloutPolicy decides which route to take based on the store.

---

## UC7 — Item Master & Product Picture Sync (Background)

**Actors:** STS, File Gateway, Sprint Connect, WMS, POS

| Step | Who | What happens |
|---|---|---|
| 1 | STS → Sprint Connect (batch) | Item Master: product catalog batch file sent from STS |
| 2 | Sprint Connect → File Gateway | Item Master forwarded via file batch |
| 3 | Sprint Connect → WMS, POS | Catalog distributed to WMS and POS |
| 4 | File Gateway → Sprint Connect | Product Pictures: images pulled via URL |
| 5 | Sprint Connect → WMS | Product images made available for picking |

**Key insight:** Not triggered by a customer action — runs on a schedule. Product pictures use a pull-by-URL pattern rather than file push.

---

## UC8 — Click & Collect Order

**Actors:** Customer, Store Staff, Sprint Connect, WMS, POS
**FulfillmentType:** ClickAndCollect — no TMS, no Delivery Slot, customer collects at store.

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Finds nearby branch, requests a collection slot |
| 2 | Customer | Creates booking for the collection window |
| 3 | Customer | Confirms sale — Sprint Connect creates Order (`FulfillmentType=ClickAndCollect`) |
| 4 | Sprint Connect → WMS | Pick Started: no booking confirmation needed, picking begins immediately |
| 5 | WMS → Sprint Connect | Pick Confirmed (basket qty) |
| 6 | WMS → Sprint Connect | POS Recalculation trigger |
| 7 | Sprint Connect → POS | POS Recalculation (actual qty) |
| 8 | POS → Sprint Connect | Recalculated prices |
| 9 | Sprint Connect → Gateway | POS Recalculation (updated total to customer) |
| 10 | Sprint Connect | Status → ReadyForCollection; customer notified to come to store |
| 11 | Customer | Arrives at store |
| 12 | Store Staff → Sprint Connect | MarkCollected: staff confirms customer has collected the order |
| 13 | WMS → Sprint Connect | ABB/Tax Invoice generated |
| 14 | Sprint Connect | Notify Paid |

**Key insight:** TMS is not involved at all. The state machine takes the `PickConfirmed → ReadyForCollection → Collected` path instead of the delivery path.

---

## UC9 — Express Delivery Order

**Actors:** Customer, Sprint Connect, WMS, TMS, POS
**FulfillmentType:** Express — no booking required, immediate dispatch.

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Places order directly (`FulfillmentType=Express`) — no time slot selection |
| 2 | Sprint Connect | Order created; jumps straight to PickStarted (no Booking Confirmed step) |
| 3 | Sprint Connect → WMS | Pick Started immediately |
| 4 | WMS → Sprint Connect | Pick Confirmed (basket qty) |
| 5 | WMS → Sprint Connect | POS Recalculation trigger |
| 6 | Sprint Connect → POS | POS Recalculation (actual qty) |
| 7 | POS → Sprint Connect | Recalculated prices |
| 8 | Sprint Connect → Gateway | POS Recalculation (updated total) |
| 9 | WMS → Sprint Connect | ABB/Tax Invoice (Pre-paid) |
| 10 | TMS → Sprint Connect | Out for Delivery: TMS assigns next available driver |
| 11 | TMS → Sprint Connect | Delivered |
| 12 | Sprint Connect | Notify Paid |

**Key insight:** Express skips `BookingConfirmed` entirely. `FulfillmentRouter.RequiresBooking(Express)` returns `false` so the state machine goes `Pending → PickStarted` directly.

---

## UC10 — Weight-Based Item Order

**Actors:** Customer, Picker, Sprint Connect, WMS, POS, TMS
**FulfillmentType:** Delivery (standard) but with weight-based OrderLines.

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Orders weight-based items (e.g. 500g chicken, 1kg tomatoes) |
| 2 | Sprint Connect | Order created with `requestedAmount=500`, `unitOfMeasure=Gram` per OrderLine |
| 3–5 | (same as UC1 up to Pick Started) | Booking, slot, Pick Started |
| 6 | WMS → Sprint Connect | Pick Confirmed with **actual weighed amounts** per OrderLine (e.g. `pickedAmount=480`) |
| 7 | WMS → Sprint Connect | POS Recalculation trigger (weight-based) |
| 8 | Sprint Connect → POS | POS Recalculation with actual gram/kg quantities |
| 9 | POS → Sprint Connect | Final prices calculated as `unitPrice × (pickedAmount / 1000)` for gram items |
| 10 | Sprint Connect → Gateway | Updated total (lower than original estimate if weight is short) |
| 11 | WMS → Sprint Connect | ABB/Tax Invoice (reflects actual picked weight) |
| 12 | Sprint Connect → TMS | Credit Note if actual weight is less than requested weight |
| 13 | TMS → Sprint Connect | Out for Delivery / Delivered (same as UC1) |
| 14 | Sprint Connect | Notify Paid |

**Key insight:** The only structural difference from UC1 is that `OrderLine.requestedAmount` and `pickedAmount` are decimal values with a `UnitOfMeasure`, and `CalculateTotal()` divides grams by 1000 before multiplying by price-per-kg. A Credit Note is always issued when actual weight < requested weight.

---

## UC11 — Multi-Vehicle Split Delivery (Large Items)

**Actors:** Customer, Sprint Connect, WMS, TMS (multiple vehicles)
**FulfillmentType:** Delivery — but order lines are split across multiple `Package` entities.
**When:** Order contains large/bulky items (furniture, appliances) that cannot fit in a single vehicle.

| Step | Who | What happens |
|---|---|---|
| 1 | Customer | Places order with large items (e.g. sofa, wardrobe, bed frame) |
| 2 | Sprint Connect | Order created — `FulfillmentType=Delivery`, `packages` list is empty at this point |
| 3–5 | (same as UC1) | Booking confirmed, Pick Started |
| 6 | WMS → Sprint Connect | Pick Confirmed (all items across all future packages) |
| 7 | WMS → Sprint Connect | POS Recalculation trigger |
| 8 | Sprint Connect → POS / POS → Sprint Connect | POS Recalculation (full order) |
| 9 | WMS → Sprint Connect | AssignPackages: WMS sends groupings (e.g. Package 1: sofa+wardrobe → Truck; Package 2: bed+mattress → Van) with TrackingIds |
| 10 | Sprint Connect | Creates `Package 1` and `Package 2` inside the Order aggregate, raises `PackagesAssigned` |
| 11 | Sprint Connect → TMS | Registers Package 1 (TrackingId, OrderLineIds, VehicleType=Truck) |
| 12 | Sprint Connect → TMS | Registers Package 2 (TrackingId, OrderLineIds, VehicleType=Van) |
| 13 | TMS → Sprint Connect | PackageOutForDelivery (TrackingId=TRK001) — Truck departs |
| 14 | Sprint Connect | Order status → Delivering; customer notified Package 1 is on its way |
| 15 | TMS → Sprint Connect | PackageOutForDelivery (TrackingId=TRK002) — Van departs |
| 16 | TMS → Sprint Connect | PackageDelivered (TrackingId=TRK001) — Truck arrives |
| 17 | Sprint Connect | Package 1 marked Delivered; Order still in Delivering (TRK002 pending) |
| 18 | Sprint Connect → Gateway | Package 1 of 2 delivered (customer progress notification) |
| 19 | TMS → Sprint Connect | PackageDelivered (TrackingId=TRK002) — Van arrives |
| 20 | Sprint Connect | `IsFullyDelivered()` → true → raises `OrderFullyDelivered` → Order status → Delivered |
| 21 | WMS → Sprint Connect | ABB/Tax Invoice (full order total) |
| 22 | Sprint Connect | Notify Paid |

**Key insight:** Each TMS event carries a `TrackingId`. Sprint Connect does not mark the Order `Delivered` until `IsFullyDelivered()` returns true (all `Package.Status == Delivered`). Invoice is generated once — after `OrderFullyDelivered`, not per package.

---

## UC12 — Modify Order Lines After Placement

**Actors:** Customer or Store Staff, Sprint Connect, POS
**Allowed states:** `Pending`, `BookingConfirmed` — rejected after `PickStarted`

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Requests modification — add lines, remove lines, or change quantity |
| 2 | Sprint Connect | Guard check: Order must be in `Pending` or `BookingConfirmed` |
| 3 | Sprint Connect | Applies `OrderLineModification` entries to Order Lines |
| 4 | Sprint Connect | Raises `OrderLinesModified` event |
| 5 | Sprint Connect → POS | POS Recalculation triggered on updated lines |
| 6 | POS → Sprint Connect | Recalculated prices returned |
| 7 | Sprint Connect → Gateway | Updated total notified to customer |
| 8 | Sprint Connect | If PrePaid and total increased: re-authorize payment delta |

**Key insight:** Once `PickStarted`, the picker is already working — modifications are rejected. POS Recalculation must re-run after every modification because promotions or bundle pricing may shift.

---

## UC13 — Reassign Packages

**Actors:** WMS, Sprint Connect, TMS
**Trigger:** WMS sends incorrect groupings, or a vehicle becomes unavailable before dispatch.
**Allowed state:** `PickConfirmed`, and only if **all** existing Packages are still `Pending` (none dispatched to TMS).

| Step | Who | What happens |
|---|---|---|
| 1 | WMS | Sends `ReSplitPackages(newGroups)` with updated line groupings and VehicleTypes |
| 2 | Sprint Connect | Guard check: Order must be `PickConfirmed`; all Packages must be `Pending` |
| 3 | Sprint Connect | Replaces all Package entities with new groupings |
| 4 | Sprint Connect | Raises `PackagesRepackaged` event |
| 5 | Sprint Connect → TMS | De-registers old Packages |
| 6 | Sprint Connect → TMS | Registers new Packages (updated PackageId, OrderLineIds, VehicleType) |
| 7 | TMS | Confirms new Packages registered |
| 8 | (continues) | Delivery proceeds with new shipment groupings as per UC11 |

**Key insight:** If even one Package is `OutForDelivery`, repackaging is rejected — the vehicle is already on the road and TMS cannot recall it. The guard is enforced at the Package level, not just the Order level.

---

## UC17 — Put Away (Returned Items)

**Actors:** WMS, Warehouse Staff, Sprint Connect
**Trigger:** Returned items arrive at the warehouse receiving dock after return pickup.
**Precondition:** Return is in `PickedUp` state — TMS has collected items from customer.

| Step | Who | What happens |
|---|---|---|
| 1 | WMS | Items arrive at warehouse receiving dock — `ReceivedAtWarehouse` |
| 2 | WMS → Sprint Connect | `ReceivedAtWarehouse(returnId)` — return status updated |
| 3 | Warehouse Staff | Inspects each return item — assigns `ItemCondition`: `Resellable`, `Damaged`, or `Dispose` |
| 4 | WMS | For `Resellable` items: assigns `StorageLocation (Sloc)` based on category and availability |
| 5 | Warehouse Staff | Physically moves each item to its assigned Sloc |
| 6 | WMS | Confirms put away — inventory count updated in WMS |
| 7 | WMS → Sprint Connect | `PutAwayConfirmed(returnId, items)` — carries condition and Sloc per item |
| 8 | Sprint Connect | `Resellable` items → `return_items.put_away_status = Restocked` |
| 9 | Sprint Connect | `Damaged` / `Dispose` items → `return_items.put_away_status = Disposed` |
| 10 | Sprint Connect | Writes `return_put_away_logs` per item for audit |
| 11 | Sprint Connect | Return status → `PutAway` |
| 12 | Sprint Connect | If refund not yet issued → triggers `RefundProcessed` |

**Key insight:** Put Away is WMS-owned — Sprint Connect only receives the confirmation event. `ItemCondition` determines whether an item goes back to stock (`Restocked`) or is written off (`Disposed`). Both outcomes must be recorded in `return_put_away_logs` for inventory audit.

---

## UC14 — Return & Refund

**Actors:** Customer or Store Staff, Sprint Connect, TMS
**Trigger:** Customer requests a return after Delivered or Collected.
**Allowed states:** `Delivered`, `Collected`, `Paid`

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Requests return with reason |
| 2 | Sprint Connect | Guard check: Order must be `Delivered`, `Collected`, or `Paid` |
| 3 | Sprint Connect | Raises `ReturnRequested` event |
| 4 | Sprint Connect → TMS | Schedule return pickup |
| 5 | TMS → Sprint Connect | Return pickup confirmed — Driver collects items from customer |
| 6 | Sprint Connect | Items received — triggers refund evaluation |
| 7 | Sprint Connect | `RefundProcessed` raised — refund issued to customer |
| 8 | Sprint Connect | Order status → `Returned` |

**Key insight:** Refund is distinct from Credit Note. Credit Note covers partial-pick shortages (issued at fulfillment time). Refund is issued post-delivery as part of the return process.

---

## UC15 — Order On Hold / Release

**Actors:** Store Staff or System (automated compliance), Sprint Connect
**Trigger:** Compliance check, payment issue, manual intervention.
**Allowed states:** Any state before `Delivered`

| Step | Who | What happens |
|---|---|---|
| 1 | Staff / System | Calls `HoldOrder(reason)` |
| 2 | Sprint Connect | Order status → `OnHold`; raises `OrderOnHold` with `HoldReason` |
| 3 | Sprint Connect | All lifecycle transitions blocked while `OnHold` |
| 4 | Staff | Resolves the issue and calls `ReleaseOrder()` |
| 5 | Sprint Connect | Order resumes from its pre-hold state; raises `OrderReleased` |

**Key insight:** OnHold is a non-destructive pause — the Order returns to exactly the state it was in before the hold. The previous state must be stored alongside the hold.

---

## UC16 — Package Lost (Exception Handling)

**Actors:** TMS, Sprint Connect, Store Staff
**Trigger:** TMS cannot locate or deliver a Package.

| Step | Who | What happens |
|---|---|---|
| 1 | TMS → Sprint Connect | Reports `PackageLost(trackingId)` |
| 2 | Sprint Connect | Raises `PackageLost` domain event |
| 3 | Sprint Connect | Order status → `OnHold` with reason `PackageLost` |
| 4 | Staff | Initiates manual intervention: re-dispatch or refund decision |
| 5a | Staff | If re-dispatch: `ReassignPackages` with new package → `ReleaseOrder` |
| 5b | Staff | If cannot recover: `Cancel` or `RequestReturn` to process refund |

**Key insight:** PackageLost triggers OnHold automatically — the Order cannot progress until staff resolves it. This prevents the invoice/payment flow from running on an undelivered order.

---

## Summary

| Use Case | Trigger | FulfillmentType | Key Systems |
|---|---|---|---|
| UC1 Pre-paid order | Customer checkout | Delivery | Sprint Connect, WMS, POS, TMS |
| UC2 Pay-on-Delivery | Customer checkout | Delivery | Sprint Connect, WMS, POS, TMS |
| UC3 Cancellation | Customer / staff | Any | Sprint Connect, TMS |
| UC4 Reschedule | Customer / staff | Delivery | Sprint Connect, TMS |
| UC5 Partial pick + credit note | Picker / WMS | Any | Sprint Connect, WMS, POS, TMS, STS |
| UC6 Non-rolled-out store | Customer checkout | Delivery (legacy) | Sprint Connect, TMS, Backend |
| UC7 Item/product sync | Scheduled batch | — | Sprint Connect, STS, File Gateway, WMS, POS |
| UC8 Click & Collect | Customer checkout | ClickAndCollect | Sprint Connect, WMS, POS |
| UC9 Express delivery | Customer checkout | Express | Sprint Connect, WMS, POS, TMS |
| UC10 Weight-based order | Customer checkout | Delivery / Express | Sprint Connect, WMS, POS, TMS |
| UC11 Multi-vehicle split delivery | WMS AssignPackages | Delivery | Sprint Connect, WMS, POS, TMS (multiple) |
| UC12 Modify order lines | Customer / Staff request | Any | Sprint Connect, POS |
| UC13 Reassign packages | WMS ReassignPackages | Delivery | Sprint Connect, WMS, TMS |
| UC14 Return & Refund | Customer / Staff request | Any | Sprint Connect, TMS |
| UC17 Put Away (returned items) | WMS PutAwayConfirmed | Returns | Sprint Connect, WMS, Warehouse Staff |
| UC15 Order On Hold / Release | Staff / System | Any | Sprint Connect |
| UC16 Package Lost | TMS exception report | Delivery | Sprint Connect, TMS, Staff |
