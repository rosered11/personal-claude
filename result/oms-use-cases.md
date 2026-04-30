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
| WMS | Warehouse Management — picking/packing/receiving |
| TMS | Transport Management — delivery/scheduling |
| POS | Point-of-Sale — pricing & recalculation |
| Backend | Handles non-rolled-out stores |
| STS | Batch source for invoices, credit notes, item master |

### Terminology: Outbound vs Inbound

> **Outbound Order** — goods leaving the warehouse to a customer (delivery, click & collect, express).
> **Inbound Order** — goods arriving at the warehouse: customer returns, supplier deliveries (PO receipt), inter-store stock transfers, and damaged packages returned by drivers.
>
> In this document "inbound/outbound" always refers to the warehouse/logistics direction, not to API direction. API webhooks received from external systems are labelled "webhook received" in diagrams and `order_webhook_logs` in the data model.

---

## Outbound Order Flow

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

**Actors:** Customer or Store Staff, Sprint Connect, WMS, TMS, POS

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Triggers cancellation (can happen at any lifecycle stage) |
| 2 | Sprint Connect | Cancels order internally, raises `OrderCancelledEvent` |
| 3 | Sprint Connect → WMS | WMS notified to abort any in-progress picking task |
| 4 | Sprint Connect → TMS | TMS notified to release any scheduled delivery slot |
| 5 | Sprint Connect → POS | POS notified to void any pending recalculation or invoice |
| 6 | Sprint Connect | If pre-paid: triggers Credit Note flow (see UC5) |

**Key insight:** Cancellation is a single event that fans out to all three external systems — each must abort its in-flight task. The `OrderCancelledEvent` is routed via the Outbox to `WMS`, `TMS`, and `POS` in a single commit.

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

**Actors:** Customer or Store Staff, Sprint Connect, WMS, POS
**Allowed states:** `Pending`, `BookingConfirmed` — rejected after `PickStarted`

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | Requests modification — add lines, remove lines, or change quantity |
| 2 | Sprint Connect | Guard check: Order must be in `Pending` or `BookingConfirmed` |
| 3 | Sprint Connect | Applies `OrderLineModification` entries to Order Lines |
| 4 | Sprint Connect | Raises `OrderLinesModifiedEvent` → outbox → WMS + POS |
| 5 | Sprint Connect → WMS | WMS receives updated SKU list so pick task reflects latest items |
| 6 | Sprint Connect → POS | POS Recalculation triggered on updated lines |
| 7 | POS → Sprint Connect | Recalculated prices returned via webhook |
| 8 | Sprint Connect → Gateway | Updated total notified to customer |
| 9 | Sprint Connect | If PrePaid and total increased: re-authorize payment delta |

**Key insight:** WMS and POS must both receive line modifications — WMS needs the current item list before picking begins, and POS must recalculate prices because promotions or bundle pricing may shift.

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

## UC15 — Order On Hold / Release

**Actors:** Store Staff or System (automated compliance), Sprint Connect, WMS, TMS
**Trigger:** Compliance check, payment issue, manual intervention.
**Allowed states:** Any state before `Delivered`

| Step | Who | What happens |
|---|---|---|
| 1 | Staff / System | Calls `HoldOrder(reason)` — `PATCH /api/v1/orders/{orderId}/hold` |
| 2 | Sprint Connect | Stores `pre_hold_status`; Order status → `OnHold`; raises `OrderOnHoldEvent` |
| 3 | Sprint Connect → WMS | WMS notified to pause any in-progress picking task |
| 4 | Sprint Connect → TMS | TMS notified to pause any scheduled delivery |
| 5 | Sprint Connect | All lifecycle transitions blocked while `OnHold` |
| 6 | Staff | Resolves the issue and calls `ReleaseOrder()` — `PATCH /api/v1/orders/{orderId}/release` |
| 7 | Sprint Connect | Order resumes `pre_hold_status`; raises `OrderReleasedEvent` |
| 8 | Sprint Connect → WMS | WMS notified to resume picking |
| 9 | Sprint Connect → TMS | TMS notified to resume delivery scheduling |

**Key insight:** OnHold is a non-destructive pause — WMS and TMS are told to wait, and the Order returns to exactly the state it was in before the hold when released.

---

## UC16 — Package Lost (Exception Handling)

**Actors:** TMS, Sprint Connect, Store Staff
**Trigger:** TMS cannot locate or deliver a Package.

| Step | Who | What happens |
|---|---|---|
| 1 | TMS → Sprint Connect | `POST /webhooks/tms/package-lost` — reports `PackageLost(trackingId)` |
| 2 | Sprint Connect | Webhook received logged; `Order.MarkPackageLost(trackingId)` — raises `PackageLostEvent` |
| 3 | Sprint Connect | Order status → `OnHold` with reason `PackageLost`; `OrderOnHoldEvent` → outbox → WMS + TMS |
| 4 | Staff | Initiates manual intervention: re-dispatch or refund decision |
| 5a | Staff | If re-dispatch: `PATCH /api/v1/orders/{id}/reassign-packages` → `ReleaseOrder` |
| 5b | Staff | If cannot recover: `PATCH /api/v1/orders/{id}/cancel` or `POST /api/v1/returns` to process refund |

**Key insight:** PackageLost triggers OnHold automatically — the Order cannot progress until staff resolves it. This prevents the invoice/payment flow from running on an undelivered order.

---

## UC18 — View Order Timeline

**Actors:** Operations Staff, Support Team, Sprint Connect
**Trigger:** Any time someone needs to trace the full lifecycle of an order.

| Step | Who | What happens |
|---|---|---|
| 1 | Ops / Support | `GET /api/v1/orders/{orderId}/timeline` |
| 2 | Sprint Connect | Loads three data sources: `order_status_history`, `order_webhook_logs`, `order_outbox` |
| 3 | Sprint Connect | Merges and sorts all entries by timestamp |
| 4 | Sprint Connect | Returns `OrderTimelineDto` with typed entries |

**Three entry types in the timeline:**

| Type | Source | What it shows |
|---|---|---|
| `Domain` | `order_status_history` | Every state machine transition: `Pending → BookingConfirmed`, `PickStarted → PickConfirmed`, etc. |
| `WebhookReceived` | `order_webhook_logs` | Every webhook received from an external system: `PickConfirmed ← WMS`, `PackageDelivered ← TMS`, `RecalculationResult ← POS` |
| `Outbound` | `order_outbox` | Every event dispatched to an external system: `PickStartedEvent → WMS`, `OrderPackedEvent → TMS`, status = `Pending / Published / Failed` |

**Key insight:** A single API call gives a complete audit trail of who did what, when, and which external systems were notified or received callbacks. Outbound entries show retry counts and failure status for debugging integration issues.

---

## UC19 — Item Substitution During Pick

**Actors:** WMS, Customer, Store Staff, Sprint Connect, POS
**Trigger:** During picking, WMS finds one or more SKUs out of stock and proposes a substitute item.
**Precondition:** Order is in `PickStarted` state.

Two paths determined by `substitution_flag` set at order placement:

### Path A — Auto-Approve (`substitution_flag = true`)

Customer pre-consented to substitutions at checkout. No approval step needed.

| Step | Who | What happens |
|---|---|---|
| 1 | WMS → Sprint Connect | `POST /webhooks/wms/pick-confirmed` with `pickedLines` (original lines, `pickedAmount=0` for substituted item) and `substitutions` list |
| 2 | Sprint Connect | Webhook received — `source=WMS, event=PickConfirmed, substitutions=1` |
| 3 | Sprint Connect | `Order.ConfirmPick(pickedLines)` — original lines updated; status → `PickConfirmed` |
| 4 | Sprint Connect | `Order.RecordSubstitution(originalLineId, substituteSku, ...)` called for each substitution |
| 5 | Sprint Connect | New `OrderLine` added with `is_substitute=true`; `OrderLineSubstitution` record created with `customer_approved=true` (auto) |
| 6 | Sprint Connect | `pos_recalc_pending=true`; `SubstitutionProposedEvent` (auto-approved) → outbox (internal) |
| 7 | Sprint Connect → POS | `PickConfirmedEvent` → outbox → POS (includes substitute item pricing) |
| 8 | POS → Sprint Connect | `POST /webhooks/pos/recalculation-result` — recalculated total with substitute price |
| 9 | Sprint Connect | Webhook received — `source=POS, event=RecalculationResult`; `pos_recalc_pending=false` |
| 10 | Sprint Connect → Gateway | Updated total sent to customer (reflects substitute item price) |

### Path B — Customer Approval Required (`substitution_flag = false`)

| Step | Who | What happens |
|---|---|---|
| 1–6 | (same as Path A steps 1–6) | Pick confirmed, substitution recorded, `customer_approved=null` (pending) |
| 7 | Sprint Connect → Gateway | Substitution proposal notified to customer (substitute SKU, name, price, image) |
| 8a | Customer (approve) | `PATCH /api/v1/orders/{id}/substitutions/{subId}/approve` |
| 8a | Sprint Connect | `Order.ApproveSubstitution(substitutionId)` — `customer_approved=true`; `SubstitutionApprovedEvent` → outbox (internal) |
| 8b | Customer (reject) | `PATCH /api/v1/orders/{id}/substitutions/{subId}/reject` |
| 8b | Sprint Connect | `Order.RejectSubstitution(substitutionId)` — `customer_approved=false`; substitute `OrderLine` cancelled; `pos_recalc_pending=true` |
| 9 | Sprint Connect → POS | `PickConfirmedEvent` → outbox → POS (once all substitutions resolved — approve or reject) |
| 10 | POS → Sprint Connect | `POST /webhooks/pos/recalculation-result` |
| 11 | Sprint Connect → Gateway | Updated total sent to customer |

**If customer rejects:** substitute line is removed from basket. POS recalculates without it. `MarkPacked` is blocked until `pos_recalc_pending` is cleared by POS response.

**Key insight:** `substitution_flag` on the order determines whether the OMS auto-approves or waits for customer response. The `MarkPacked` guard (`pos_recalc_pending=true`) ensures the order cannot proceed to packaging until all substitution pricing is resolved regardless of path. Both paths produce the same atomic DB commit: order state + substitute order line + `OrderLineSubstitution` record + outbox + webhook log.

---

## UC20 — Package Damaged During Delivery

**Actors:** TMS, Store Staff, Customer, Sprint Connect
**Trigger:** TMS driver reports a package is damaged before or during handoff to the customer.
**Precondition:** Order is in `OutForDelivery` or `Delivering` state.

| Step | Who | What happens |
|---|---|---|
| 1 | TMS → Sprint Connect | `POST /webhooks/tms/package-damaged` — inbound webhook with `trackingId` |
| 2 | Sprint Connect | Webhook received — `source=TMS, event=PackageDamaged, tracking=TRK001` |
| 3 | Sprint Connect | `Order.MarkPackageDamaged(trackingId)` — raises `PackageDamagedEvent`; Order status → `OnHold` (`reason=PackageDamaged`); `pre_hold_status` stored |
| 4 | Sprint Connect → WMS / TMS | `OrderOnHoldEvent` → outbox → WMS + TMS (pause any related tasks) |
| 5 | Staff | Alerted; investigates damage severity and customer preference |

Three resolution paths:

### Path A — Re-dispatch (replacement item sent)

| Step | Who | What happens |
|---|---|---|
| 6a | Staff → Sprint Connect | `ReassignPackages` — new package with replacement stock and new `TrackingId` |
| 7a | Staff → Sprint Connect | `ReleaseOrder` — order resumes `OutForDelivery`; `OrderReleasedEvent` → WMS + TMS |
| 8a | TMS → Sprint Connect | New `PackageOutForDelivery` webhook for replacement package |
| 9a | TMS → Sprint Connect | `PackageDelivered` — order → `Delivered`; normal invoice + payment flow |

### Path B — Customer accepts damaged item with compensation

| Step | Who | What happens |
|---|---|---|
| 6b | Staff → Sprint Connect | `ReleaseOrder` — order resumes `OutForDelivery` |
| 7b | TMS → Sprint Connect | `PackageDelivered` — customer accepted the damaged item; order → `Delivered` |
| 8b | Sprint Connect | `GenerateInvoice` — invoice generated as normal |
| 9b | Sprint Connect | Credit note issued for partial compensation (damage discount) |
| 10b | Sprint Connect | `NotifyPayment` — order closed |

### Path C — Customer refuses / unrecoverable damage

| Step | Who | What happens |
|---|---|---|
| 6c | Staff → Sprint Connect | `CancelOrder(reason=PackageDamaged)` |
| 7c | Sprint Connect | `OrderCancelledEvent` → outbox → WMS + TMS + POS |
| 8c | Sprint Connect | If pre-paid: credit note / refund initiated |

**Key insight:** Damage during delivery is a fulfillment exception — NOT a substitution. The item was picked and dispatched correctly; the damage occurred in transit. All three resolution paths reuse existing commands (`ReassignPackages`, `ReleaseOrder`, `CancelOrder`) — no new staff-side commands are needed. The `OrderOnHoldEvent` automatically pauses WMS and TMS via the existing outbox routing for `OnHold`.

**Distinction from UC16 (Package Lost):**

| | UC16 Package Lost | UC20 Package Damaged |
|---|---|---|
| Item exists? | No — cannot be recovered | Yes — in driver's possession |
| Resolution paths | Re-dispatch or cancel | Re-dispatch, accept + compensate, or cancel |
| Customer receives anything? | No | Possibly yes (Path B) |

---

## Inbound Order Flow

---

## UC14 — Return & Refund (Inbound)

**Actors:** Customer, TMS, WMS, Warehouse Staff, Sprint Connect, POS
**Trigger:** Customer requests a return after delivery.
**Precondition:** Order is in `Delivered`, `Collected`, or `Paid` state.

| Step | Who | What happens |
|---|---|---|
| 1 | Customer / Staff | `POST /api/v1/returns` — Sprint Connect creates an `OrderReturn` record linked to the order; status → `ReturnRequested`; `ReturnRequestedEvent` → outbox → TMS |
| 2 | Sprint Connect → TMS | TMS notified to schedule a return pickup from the customer |
| 3 | TMS → Sprint Connect | `POST /webhooks/tms/return-pickup-scheduled` — confirms pickup window; status → `PickupScheduled` |
| 4 | TMS → Sprint Connect | `POST /webhooks/tms/return-pickup-confirmed` — driver has collected the goods from the customer; status → `PickedUp` |
| 5 | WMS → Sprint Connect | `POST /webhooks/wms/return-received-at-warehouse` — goods arrive at warehouse receiving dock; GRN recorded; status → `ReceivedAtWarehouse` |
| 6 | Warehouse Staff | Inspects returned items; assigns condition per item |
| 7 | WMS → Sprint Connect | `POST /webhooks/wms/put-away-confirmed` — items placed on shelf with `ItemCondition` per item; triggers atomic refund step (see UC17) |
| 8 | Sprint Connect | `ret.ProcessRefund(creditNoteId)` — credit note generated (`CN-{returnId}`); `RefundProcessedEvent` → outbox → POS; status → `Refunded` |
| 9 | Sprint Connect | `order.MarkReturned(returnId)` — parent Order status → `Returned`; `OrderReturnedEvent` raised; both aggregates saved in one transaction |

**Return status flow:** `ReturnRequested → PickupScheduled → PickedUp → ReceivedAtWarehouse → PutAway → Refunded`

**Order status:** `Delivered / Collected / Paid → (unchanged during return journey) → Returned`

**API endpoints involved:**

| Direction | Endpoint | Purpose |
|---|---|---|
| Inbound (Staff/Customer) | `POST /api/v1/returns` | Create return request |
| Inbound (TMS webhook) | `POST /webhooks/tms/return-pickup-scheduled` | TMS confirms pickup window |
| Inbound (TMS webhook) | `POST /webhooks/tms/return-pickup-confirmed` | TMS driver collects from customer |
| Inbound (WMS webhook) | `POST /webhooks/wms/return-received-at-warehouse` | WMS confirms goods at dock with GRN |
| Inbound (WMS webhook) | `POST /webhooks/wms/put-away-confirmed` | WMS confirms items on shelf — triggers refund |

**Key insight:** The return lifecycle spans two aggregates: `OrderReturn` tracks the physical goods journey; `Order` tracks the overall order status. Both are updated atomically inside `ConfirmPutAwayHandler`: put-away confirmation + refund credit note generation + `Order.MarkReturned()` committed in a single `SaveChangesAsync()` call. If POS is unavailable, the outbox guarantees `RefundProcessedEvent` will be retried until delivered.

---

## UC17 — Put Away (Returned Items)

**Actors:** Warehouse Staff, WMS, Sprint Connect, POS
**Trigger:** WMS confirms returned items have been inspected and placed in their storage location.
**Precondition:** `OrderReturn` is in `ReceivedAtWarehouse` state (UC14 Step 5 has completed).

| Step | Who | What happens |
|---|---|---|
| 1 | Warehouse Staff | Inspects each returned item and assigns `ItemCondition`: `Resellable`, `Repairable`, or `Dispose` |
| 2 | WMS | Assigns a storage or disposal Sloc per item based on its condition |
| 3 | WMS → Sprint Connect | `POST /webhooks/wms/put-away-confirmed` — sends each item with `sku`, `condition`, and `sloc` |
| 4 | Sprint Connect | Webhook received logged; `ret.ConfirmPutAway(items, updatedBy)` — `OrderReturn` status → `PutAway` |
| 5 | Sprint Connect | `ret.ProcessRefund(creditNoteId, updatedBy)` — credit note `CN-{returnId}` generated; `OrderReturn` status → `Refunded`; `RefundProcessedEvent` → outbox |
| 6 | Sprint Connect | `order.MarkReturned(returnId, updatedBy)` — parent `Order` status → `Returned`; `OrderReturnedEvent` raised |
| 7 | Sprint Connect | Both aggregates (`OrderReturn` + `Order`) saved in one `SaveChangesAsync()` call |
| 8 | Sprint Connect → POS | `RefundProcessedEvent` dispatched — POS issues the credit note / refund to the customer |

**ItemCondition outcomes:**

| Condition | Outcome |
|---|---|
| `Resellable` | Stock returned to available WMS inventory — no write-off |
| `Repairable` | Flagged for repair workflow — held out of available inventory |
| `Dispose` | Written off — insurance / cost-of-goods journal entry triggered |

**Key insight:** `ConfirmPutAwayHandler` performs an atomic three-step transaction: (1) confirm put-away and record item conditions, (2) generate the refund credit note, (3) mark the parent Order as `Returned`. All three steps commit together in one `SaveChangesAsync()`. A failure in any step rolls back the entire transaction — preventing a state where the Order is marked `Returned` but no credit note was generated, or vice versa.

---

## UC21 — Supplier / Purchase Order Receipt (Inbound)

**Actors:** Supplier, WMS, Warehouse Staff, Sprint Connect
**Trigger:** Supplier arrives at warehouse dock with goods against an open Purchase Order.

| Step | Who | What happens |
|---|---|---|
| 1 | Staff / ERP | `POST /api/v1/inbound/purchase-orders` — OMS creates a `PurchaseOrder` record; status → `Created`; `PurchaseOrderCreatedEvent` → outbox → WMS |
| 2 | Sprint Connect → WMS | WMS receives PO details so it knows expected goods and quantities |
| 3 | Supplier | Arrives at receiving dock; WMS creates a GoodsReceipt against the PO |
| 4 | WMS → Sprint Connect | `POST /webhooks/wms/goods-receipt-confirmed` — actual quantities received per line, with `ItemCondition` per line |
| 5 | Sprint Connect | Webhook received logged; `PurchaseOrder.ConfirmGoodsReceipt(lines)` — each line's `ReceivedQty` updated; status → `PartiallyReceived` or `FullyReceived` |
| 6 | Warehouse Staff | Inspects goods; WMS assigns storage locations (Sloc) per item condition |
| 7 | WMS → Sprint Connect | `POST /webhooks/wms/purchase-order-put-away-confirmed` — confirms stock is on shelf |
| 8 | Sprint Connect | `PurchaseOrder.ConfirmPutAway()` — stock ledger updated; if partial receipt: discrepancy raised for buyer follow-up |

**Status flow:** `Created → PartiallyReceived / FullyReceived → Closed`

**Key insight:** The OMS acts as the source of truth for expected vs received quantities. WMS owns the physical receiving and put-away; Sprint Connect records the outcome. Partial receipts stay open until the PO is explicitly closed by the buyer.

---

## UC22 — Inter-store Stock Transfer (Inbound)

**Actors:** Source Store Staff, Sprint Connect, TMS, Destination Store Staff, WMS
**Trigger:** A store or DC needs to replenish stock from another store or central DC.

| Step | Who | What happens |
|---|---|---|
| 1 | Staff | `POST /api/v1/inbound/transfer-orders` — destination store raises a `TransferOrder` (source store, dest store, SKUs + quantities); status → `Created`; `TransferOrderCreatedEvent` → outbox → WMS (source) |
| 2 | Sprint Connect → WMS (source) | Transfer order dispatched for picking at source store |
| 3 | WMS (source) → Sprint Connect | `POST /webhooks/wms/transfer-pick-confirmed` — items picked and packed at source; status → `PickConfirmed`; `TransferPickConfirmedEvent` → outbox → TMS |
| 4 | Sprint Connect → TMS | Transfer shipment registered with `TrackingId` |
| 5 | TMS → Sprint Connect | `POST /webhooks/tms/package-dispatched` (reuses existing endpoint) — goods in transit; status → `InTransit` |
| 6 | TMS → Sprint Connect | `POST /webhooks/tms/package-delivered` (reuses existing endpoint) — goods arrive at destination store |
| 7 | WMS (destination) → Sprint Connect | `POST /webhooks/wms/transfer-received` — destination staff confirms receipt and put-away; status → `Received → Completed` |
| 8 | Sprint Connect | `TransferOrder.Complete()` — stock balances updated at both stores; `TransferOrderCompletedEvent` raised |

**Status flow:** `Created → PickConfirmed → InTransit → Received → Completed`

**Key insight:** The TransferOrder aggregate is separate from Order and Return. It owns stock movement between two stores. TMS webhooks reuse the existing package-dispatched / package-delivered endpoints with `transferOrderId` context.

---

## UC23 — Damaged Goods Return Receipt (Inbound)

**Actors:** TMS, WMS, Warehouse Staff, Sprint Connect
**Trigger:** A damaged package from UC20 Path A (replacement sent, damaged item retrieved from driver) is returned to the warehouse dock.
**Precondition:** UC20 Path A was executed — replacement was sent and the driver returned the damaged package.

| Step | Who | What happens |
|---|---|---|
| 1 | TMS driver | Returns damaged package to warehouse receiving dock |
| 2 | WMS → Sprint Connect | `POST /webhooks/wms/damaged-goods-received` — damaged package checked in at dock (`trackingId`) |
| 3 | Sprint Connect | Webhook received logged; `Order.ConfirmDamagedGoodsReceived(trackingId)` — links to original order (still `OnHold`); `DamagedGoodsReceivedEvent` raised |
| 4 | Warehouse Staff | Inspects each item — assigns `ItemCondition`: `Resellable`, `Repairable`, or `Dispose` |
| 5 | WMS | Assigns storage or disposal location (Sloc) per condition |
| 6 | WMS → Sprint Connect | `POST /webhooks/wms/damaged-goods-put-away-confirmed` — condition + Sloc per item |
| 7 | Sprint Connect | `Order.ConfirmDamagedGoodsPutAway(trackingId)` — records result; `DamagedGoodsPutAwayConfirmedEvent` raised |
| 8 | Sprint Connect | Resolution by condition: `Resellable` → stock restored; `Repairable` → flagged for repair workflow; `Dispose` → written off, insurance/cost-of-goods adjustment triggered |
| 9 | Sprint Connect | Damaged goods record closed; linked to original order for audit trail |

**ItemCondition outcomes:**

| Condition | Outcome |
|---|---|
| `Resellable` | Stock returned to available inventory — no write-off |
| `Repairable` | Flagged for repair workflow — held out of available inventory |
| `Dispose` | Written off — insurance / cost-of-goods journal entry triggered |

**Key insight:** This use case always follows UC20 Path A. The Order remains `OnHold` throughout the damaged goods processing; it is closed only after put-away is confirmed. `ItemCondition` is recorded per item at put-away time, not at receipt — allowing staff time to inspect before committing the outcome.

---

## UC24 — End-to-End: Inbound Receipt to Outbound Delivery

**Actors:** Supplier, WMS, Warehouse Staff, Customer, Sprint Connect, POS, TMS
**Trigger:** A supplier delivers goods against a Purchase Order; those same goods are later picked to fulfill a customer order.
**Purpose:** This use case shows the complete product journey through the warehouse — from goods arriving at the dock to goods arriving at the customer's door. It is not a new workflow; it is the sequence of UC21 → UC1 with the WMS stock bridge made explicit.

---

### Phase 1 — Inbound: Goods Arrive at Warehouse (UC21)

| Step | Who | What happens |
|---|---|---|
| 1 | Staff / ERP | `POST /api/v1/inbound/purchase-orders` — OMS creates `PurchaseOrder` for expected SKUs and quantities; status → `Created` |
| 2 | Sprint Connect → WMS | `PurchaseOrderCreatedEvent` dispatched — WMS registers expected delivery at the receiving dock |
| 3 | Supplier | Arrives at dock with physical goods |
| 4 | WMS → Sprint Connect | `POST /webhooks/wms/goods-receipt-confirmed` — WMS scans and counts received items; OMS records `ReceivedQty` per line; PO status → `FullyReceived` or `PartiallyReceived` |
| 5 | WMS | Assigns storage location (Sloc) per item |
| 6 | WMS → Sprint Connect | `POST /webhooks/wms/purchase-order-put-away-confirmed` — goods physically placed on shelf; OMS records put-away confirmation |
| 7 | WMS | **Stock is now available** — WMS inventory for the received SKUs is incremented and available for picking |

> **Bridge:** After Step 7, the WMS holds available stock. Any customer order that includes those SKUs can now be fulfilled by picking from the shelf where the inbound goods were placed.

---

### Phase 2 — Outbound: Customer Order Fulfilled (UC1)

| Step | Who | What happens |
|---|---|---|
| 8 | Customer | Places a delivery order that includes one or more SKUs from the received PO |
| 9 | Sprint Connect | Order created → `OrderCreatedEvent` → outbox → WMS; status → `Pending` |
| 10 | Sprint Connect → WMS | `BookingConfirmedEvent` / `PickStartedEvent` — WMS schedules a pick task |
| 11 | WMS | Picker walks to the Sloc assigned during put-away (Step 5) and picks the goods |
| 12 | WMS → Sprint Connect | `POST /webhooks/wms/pick-confirmed` — actual picked quantities reported; OMS updates order lines; `PickConfirmedEvent` → POS |
| 13 | POS → Sprint Connect | `POST /webhooks/pos/recalculation-result` — final prices confirmed |
| 14 | WMS | Items packed into packages with TrackingIds |
| 15 | Sprint Connect → TMS | `OrderPackedEvent` — TMS schedules driver for delivery |
| 16 | TMS → Sprint Connect | `POST /webhooks/tms/package-dispatched` — driver en route; order → `OutForDelivery` |
| 17 | TMS → Sprint Connect | `POST /webhooks/tms/package-delivered` — customer receives goods; order → `Delivered` |
| 18 | Sprint Connect | Invoice generated → `NotifyPayment` — order closed |

---

### End-to-End Status Flow

```
PurchaseOrder:  Created → FullyReceived → Closed
                                      ↓
                              WMS stock available
                                      ↓
Order:          Pending → BookingConfirmed → PickStarted → PickConfirmed
                       → Packed → OutForDelivery → Delivered → Invoiced → Paid
```

### What OMS Owns at Each Phase

| Phase | OMS record | OMS role |
|---|---|---|
| Inbound | `PurchaseOrder` + `PurchaseOrderLine` | Records expected vs received quantities; triggers WMS put-away |
| Stock bridge | (WMS-internal) | WMS holds actual stock levels — OMS does not track inventory counts |
| Outbound | `Order` + `OrderLine` + `OrderPackage` | Orchestrates pick → pack → dispatch → delivery; drives POS and TMS |

**Key insight:** OMS is the orchestration layer on both sides, but it never owns inventory counts — that is WMS's responsibility. The link between inbound and outbound is entirely in the WMS: after `PurchaseOrderPutAwayConfirmed`, the WMS increments available stock for those SKUs; when `PickStarted` arrives, the WMS picks from that available stock. Sprint Connect coordinates both events but does not track the stock number itself.

**Partial receipt edge case:** If the PO was `PartiallyReceived` (Step 4) and a customer orders the same SKU, WMS may report a short pick in Step 12. This feeds directly into the partial-pick credit note flow (UC5).

---

## Summary

**Outbound Order Flow** — goods leaving the warehouse to a customer

| Use Case | Trigger | FulfillmentType | Key Systems |
|---|---|---|---|
| UC1 Pre-paid order | Customer checkout | Delivery | Sprint Connect, WMS, POS, TMS |
| UC2 Pay-on-Delivery | Customer checkout | Delivery | Sprint Connect, WMS, POS, TMS |
| UC3 Cancellation | Customer / staff | Any | Sprint Connect, WMS, TMS, POS |
| UC4 Reschedule | Customer / staff | Delivery | Sprint Connect, TMS |
| UC5 Partial pick + credit note | Picker / WMS | Any | Sprint Connect, WMS, POS, TMS, STS |
| UC6 Non-rolled-out store | Customer checkout | Delivery (legacy) | Sprint Connect, TMS, Backend |
| UC7 Item/product sync | Scheduled batch | — | Sprint Connect, STS, File Gateway, WMS, POS |
| UC8 Click & Collect | Customer checkout | ClickAndCollect | Sprint Connect, WMS, POS |
| UC9 Express delivery | Customer checkout | Express | Sprint Connect, WMS, POS, TMS |
| UC10 Weight-based order | Customer checkout | Delivery / Express | Sprint Connect, WMS, POS, TMS |
| UC11 Multi-vehicle split delivery | WMS AssignPackages | Delivery | Sprint Connect, WMS, POS, TMS (multiple) |
| UC12 Modify order lines | Customer / Staff request | Any | Sprint Connect, WMS, POS |
| UC13 Reassign packages | WMS ReassignPackages | Delivery | Sprint Connect, WMS, TMS |
| UC15 Order On Hold / Release | Staff / System | Any | Sprint Connect, WMS, TMS |
| UC16 Package Lost | TMS exception report | Delivery | Sprint Connect, TMS, Staff |
| UC18 View Order Timeline | Ops / Support query | Any | Sprint Connect |
| UC19 Item Substitution | WMS pick-confirmed with substitutions | Any | Sprint Connect, WMS, POS, Customer |
| UC20 Package Damaged | TMS damage report during transit | Delivery | Sprint Connect, TMS, WMS, Staff |

**Inbound Order Flow** — goods arriving at the warehouse

| Use Case | Trigger | FulfillmentType | Key Systems |
|---|---|---|---|
| UC14 Return & Refund | Customer / Staff request | Any | Sprint Connect, TMS, WMS, POS |
| UC17 Put Away (returned items) | WMS PutAwayConfirmed | Returns | Sprint Connect, WMS, POS, Warehouse Staff |
| UC21 Supplier PO Receipt | Supplier arrives at warehouse dock | Inbound | Sprint Connect, WMS, Supplier |
| UC22 Inter-store Transfer | Stock replenishment between stores | Inbound | Sprint Connect, WMS (source + dest), TMS |
| UC23 Damaged Goods Return | Driver returns damaged package to warehouse | Inbound | Sprint Connect, WMS, TMS, Staff |
| UC24 Inbound → Outbound (end-to-end) | Supplier delivers goods; same SKUs ordered by customer | Inbound + Outbound | Sprint Connect, WMS, Supplier, Customer, POS, TMS |
