# OMS Ubiquitous Language

A shared vocabulary for the Order Management System domain. All team members — business, product, and engineering — must use these terms consistently in conversations, documents, and code.

---

## Actors

| Term | Definition |
|---|---|
| **Customer** | The end user who places a delivery order through the Gateway. |
| **Picker** | Store staff responsible for physically collecting ordered items from store shelves. |
| **Driver** | Delivery personnel assigned by TMS to transport an order to the customer. |
| **Store Staff** | Generic term for any in-store employee who may initiate cancellations or reschedules. |

---

## Core Domain Objects

| Term | Definition |
|---|---|
| **Order** | The central aggregate representing a customer's confirmed purchase request. Owns the entire lifecycle from creation to payment. Carries a `FulfillmentType` and `OrderChannel` which determine routing and state machine path. |
| **Sale Order** | The act of formally creating an Order in the system after the customer confirms their basket. Triggers the fulfillment process. |
| **Order Line** | A single item entry within an Order, carrying the product, requested quantity, picked quantity, and price. |
| **Booking** | A reservation that links an Order to a specific Delivery Slot. An Order cannot proceed to picking without a confirmed Booking. |
| **Delivery Slot** | A time window (start–end) reserved for delivering an Order to the customer. Belongs to a specific Store. |
| **Basket** | The collection of items a customer intends to purchase, before it becomes a confirmed Order. |
| **Basket Quantity** | The actual quantity of items confirmed by the Picker during Pick Confirmed. May differ from the originally requested quantity (e.g., due to stock shortages). |
| **Item Master** | The product catalog data (SKU, name, price, category) synced from STS via batch. Source of truth for product information. |
| **Product Pictures** | Item images retrieved from an external URL source and synced to Sprint Connect via File Gateway. |
| **Requested Amount** | The quantity or weight a customer originally ordered. Stored as a decimal alongside a `UnitOfMeasure`. |
| **Picked Amount** | The actual quantity or weight confirmed by the Picker. May differ from Requested Amount. Drives `CalculateTotal()` and POS Recalculation. |
| **Unit of Measure (UoM)** | How an item is sold: `Each` (unit count), `Kilogram`, `Gram`, or `Litre`. Determines how `CalculateTotal()` computes the line total. |
| **Substituted Order Line** | A replacement item provided when the originally requested product is out of stock. Linked to the original Order Line. Carries the substitute product, its price, and whether the customer approved the substitution. |
| **Order Line Modification** | A change instruction applied to an Order Line: `Add` (new line), `Remove` (cancel a line), or `ChangeQuantity` (adjust amount). Only valid while the Order is in `Pending` or `BookingConfirmed`. |
| **Package** | A physical box or parcel that items are packed into by WMS. Carries a `TrackingId` that customers and staff can use to look up delivery status. One Shipment contains one or more Packages. The OMS tracks Package identity and tracking number; physical packing operations are managed inside WMS. |
| **TrackingId** | A unique identifier assigned to a Package that allows the customer, store staff, or TMS to track that specific physical parcel. Distinct from `OrderId` and `ShipmentId`. |
| **Substitution** | The act of replacing an out-of-stock Order Line item with an alternative product. Must be recorded on the Order Line — it cannot silently change the product. |
| **Package** | A physical box or parcel that items are packed into by WMS. Carries a `TrackingId` that customers and staff can use to look up delivery status. One Order contains one or more Packages. Each Package is assigned to a specific vehicle (`VehicleType`) and has its own `PackageStatus`. TMS reports delivery events per TrackingId — Package is the dispatch unit. |
| **TrackingId** | A unique identifier assigned to a Package that allows the customer, store staff, or TMS to track that specific physical parcel. Distinct from `OrderId`. TMS uses TrackingId in `PackageOutForDelivery` and `PackageDelivered` events. |
| **AssignPackages** | The act of grouping Order Lines into Packages after Pick Confirmed. Initiated by WMS based on item size and vehicle capacity. Creates `Package` entities inside the Order aggregate and raises `PackagesAssigned`. Replaces the former concept of splitting into Shipments. |
| **PackageStatus** | The lifecycle state of a single Package: `Pending` → `OutForDelivery` → `Delivered`. Independent per Package — one can be Delivered while another is still OutForDelivery. |
| **VehicleType** | The class of vehicle assigned to a Package: `StandardCar`, `Van`, or `Truck`. Set by WMS when calling `AssignPackages`. |
| **OrderFullyDelivered** | The condition where all Packages in an Order have status `Delivered`. Only when this is true does the Order transition to `Delivered` and invoice generation begin. `IsFullyDelivered()` on the Order aggregate checks this. |
| **Delivering** | Order lifecycle state indicating at least one Package is OutForDelivery or Delivered, but not all Packages are Delivered yet. Specific to multi-package orders. |

---

## Fulfillment Types

Every Order carries a `FulfillmentType` that determines its state machine path and which external systems are involved.

| Term | Definition |
|---|---|
| **Delivery** | Standard home delivery. Requires Booking, WMS picking, and TMS dispatch. State path: `Pending → BookingConfirmed → PickStarted → PickConfirmed → OutForDelivery → Delivered`. |
| **Click & Collect** | Customer collects the order at the store. No TMS involved. No delivery address needed. State path: `Pending → PickStarted → PickConfirmed → ReadyForCollection → Collected`. |
| **Express** | Immediate dispatch with no booking or slot selection. State machine skips `BookingConfirmed` and goes `Pending → PickStarted` directly. TMS assigns the next available driver. |

---

## Order Channels

Every Order carries an `OrderChannel` identifying where the order originated.

| Term | Definition |
|---|---|
| **Gateway** | Order originated from the customer-facing API (mobile app, web). The default channel. |
| **B2B** | Order originated from a business-to-business integration (EDI, API partner). |
| **Kiosk** | Order originated from an in-store self-service kiosk. |
| **Marketplace** | Order originated from a third-party marketplace (e.g. Shopee, Lazada, TikTok Shop). Routed into Sprint Connect via marketplace integration adapter. |
| **POSTerminal** | Order originated from an in-store POS terminal (as order source, distinct from the POS pricing system). Used when store staff places an order on behalf of a customer at the counter. |
| **BulkImport** | Order originated from a bulk file import (e.g. CSV, EDI batch). Used for B2B or wholesale orders submitted in bulk. |

---

## Order Lifecycle States

States are mutually exclusive. An Order moves forward through the lifecycle; the only non-forward transition is **Cancelled**.

| State | Meaning |
|---|---|
| **Pending** | Order has been created but Booking is not yet confirmed. |
| **Booking Confirmed** | Delivery Slot has been reserved. Order is waiting for picking to begin. |
| **Pick Started** | WMS has begun the picking process for this Order. |
| **Pick Confirmed** | Picker has completed picking and confirmed the actual Basket Quantity to WMS. |
| **Out for Delivery** | TMS has reported that the Driver has picked up the Order and started delivery. |
| **Delivered** | TMS has confirmed that the Order has been delivered to the Customer. |
| **Invoiced** | ABB/Tax Invoice has been generated for the Order. |
| **Paid** | Payment has been confirmed. The Order is fully closed. |
| **Cancelled** | The Order has been terminated. Can occur from any state before Delivered. |
| **ReadyForCollection** | `ClickAndCollect` only. Picking is complete and the order is waiting for the customer to arrive at the store. Sprint Connect notifies the customer. |
| **Collected** | `ClickAndCollect` only. Store Staff has confirmed the customer collected the order in person. Triggers invoice generation. |
| **Delivering** | Multi-package `Delivery` orders only. At least one Package is OutForDelivery or Delivered but the order is not yet fully delivered. See `OrderFullyDelivered`. |
| **Packed** | All Order Lines have been physically packed into Packages by WMS. `AssignPackages` has been called and all Packages have `TrackingId` assigned. The Order is ready for TMS dispatch. |
| **OnHold** | The Order has been temporarily paused. No further lifecycle transitions occur until `ReleaseOrder` is called. Can be triggered from any state before Delivered. Carries a hold reason. |
| **Returned** | The customer has returned the Order (or part of it) after delivery. Triggers a refund process. Terminal state alongside `Paid`. |
| **PaymentFailed** | The payment attempt after invoice was rejected. Terminal state — requires manual intervention or retry flow. |

---

## Processes

| Term | Definition |
|---|---|
| **POS Recalculation** | The process of re-computing Order totals and prices. Triggered by WMS after Pick Confirmed (to reflect actual Basket Quantity) and by TMS after Delivered (to finalize the POD invoice). Sprint Connect calls POS, receives updated prices, and notifies the customer via Gateway. |
| **Picking** | The physical process in which the Picker collects ordered items from store shelves according to the Order Lines. |
| **Dispatch** | The act of handing a picked Order to TMS for delivery. Represented by the Out for Delivery state. |
| **Reschedule** | Changing the Delivery Slot of a Booking after it has been confirmed. Does not re-trigger picking or invoicing — only updates the TMS delivery window. |
| **Cancellation** | Terminating an Order at any point in its lifecycle. Notifies TMS to release the Delivery Slot. If Pre-paid, triggers a Credit Note. |
| **Rollout** | The process of enabling a Store to use the Sprint Connect integration path. A store is either **Rolled Out** (All Store) or **Not Rolled Out** (legacy path). |
| **FulfillmentRouter** | Domain service that answers routing questions about a `FulfillmentType`: Does it require a Booking? Does it involve TMS? What is the first state after PickConfirmed? Prevents `if fulfillmentType == X` conditionals from spreading across handlers. |
| **ModifyOrderLines** | The act of changing an Order's lines after the Order is created but before picking starts. Applies `OrderLineModification` entries (Add, Remove, ChangeQuantity). Triggers a POS Recalculation. Rejected if the Order is in `PickStarted` or later. |
| **ReassignPackages** | The act of replacing an Order's Package groupings after `PickConfirmed`, before any Package is dispatched. Only allowed when all Packages are `Pending`. Triggers TMS de-registration of old Packages and registration of new ones. |
| **Pack** | The physical act of placing picked items into boxes (Packages). Performed by WMS after `PickConfirmed`. Completed when WMS calls `AssignPackages` — transitions the Order to `Packed`. |
| **HoldOrder** | The act of placing an Order on hold. Stops all lifecycle progression. Can be triggered by compliance checks, payment issues, or manual staff intervention. Carries a `HoldReason`. |
| **ReleaseOrder** | The act of lifting a hold on an Order. Returns the Order to the state it was in before the hold. |
| **Return** | The process by which a customer sends delivered items back. Initiated by `RequestReturn`. Triggers pickup scheduling, Put Away, and refund processing. |
| **Refund** | Money returned to the customer as a result of a Return or cancellation of a pre-paid order. Distinct from a Credit Note (which covers partial-pick shortages). |
| **Put Away** | The warehouse process of placing returned items into their designated storage location (Sloc) after inspection. Performed by Warehouse Staff guided by WMS. Resellable items are Restocked; Damaged or expired items are Disposed. |
| **StorageLocation (Sloc)** | The specific warehouse bin, shelf, or zone assigned to a return item during Put Away. Set by WMS based on item category and available space. Referenced as `assigned_sloc` on `return_items`. |
| **ItemCondition** | The quality classification assigned to a returned item during inspection: `Resellable` (can be put back in stock), `Damaged` (cannot be sold, may be repaired), or `Dispose` (must be written off). Determines whether Put Away or Disposal happens. |
| **ReceivedAtWarehouse** | Return lifecycle state indicating items have arrived at the warehouse receiving dock after TMS pickup. Precondition for inspection and Put Away. |
| **PutAway** | Return lifecycle state indicating all items have been inspected and placed in their assigned Sloc (or disposal path). Triggers `RefundProcessed` if refund has not yet been issued. |
| **Batch Sync** | The scheduled process by which Item Master data and Product Pictures are transferred from STS to Sprint Connect via File Gateway. Not triggered by a customer action. |

---

## Payment Concepts

| Term | Definition |
|---|---|
| **Pre-paid** | Payment method where the Customer pays before delivery. The ABB/Tax Invoice is generated after Pick Confirmed, before dispatch. |
| **Pay-on-Delivery (POD)** | Payment method where the Customer pays after receiving the Order. The ABB/Tax Invoice is generated after TMS confirms Delivered, and a Payment Link is sent to the Customer. |
| **Payment Link** | A URL sent to the Customer after Delivered (POD only) to collect payment. Retrieved by Sprint Connect via "Inquiry payment / Get Payment link". |
| **Notify Paid** | The event confirming that payment has been received. Transitions the Order to the Paid state. |
| **Inquiry Payment** | Sprint Connect's action of requesting a payment link from the payment provider after a POD order is delivered. |

---

## Documents

| Term | Definition |
|---|---|
| **ABB/Tax Invoice** | The official tax invoice for an Order. For Pre-paid: generated by WMS after Pick Confirmed. For POD: generated after TMS confirms Delivered, then sent to TMS. |
| **Credit Note** | A document issued when the Order total decreases due to a partial pick (items out of stock). Can originate online (WMS/Sprint Connect) or via batch reconciliation (STS). Must be handled idempotently as both paths may deliver the same Credit Note. |

---

## Integration & System Terms

| Term | Definition |
|---|---|
| **Sprint Connect** | The integration hub that owns and runs the OMS. All Order lifecycle logic lives inside Sprint Connect. It orchestrates WMS, TMS, POS, and external systems. |
| **Gateway** | The customer-facing API entry point. Receives all customer requests (Branch near me, Time slot, Booking, Sale Order). |
| **Proxy Service** | The routing layer between Gateway and Sprint Connect (for All Store) or between Gateway and TMS/Backend (for Not Roll Out stores). Determines integration path based on store rollout status. |
| **WMS (Warehouse Management System)** | The system that manages picking and warehouse operations. Sends Pick Confirmed, POS Recalculation triggers, ABB/Tax Invoice (Pre-paid), and Credit Notes to Sprint Connect. |
| **TMS (Transport Management System)** | The system that manages delivery scheduling, Drivers, and transport. Sends Out for Delivery, Delivered, and POS Recalculation triggers to Sprint Connect. Receives Reschedule Order, ABB/Tax Invoice (POD), Credit Note (POD), and Cancelled Order from Sprint Connect. |
| **POS (Point of Sale)** | The pricing system. Receives POS Recalculation requests from Sprint Connect and returns updated prices. |
| **STS** | Batch data source for ABB/Tax Invoice reconciliation, Credit Notes, and Item Master data. Sends these to Sprint Connect on a schedule. |
| **Backend** | The legacy fulfillment system used for Not Roll Out stores. Receives Create Booking and Sale Order API from Sprint Connect via Proxy Service and TMS. |
| **File Gateway** | The component handling batch file transfers. Used to relay Item Master data and retrieve Product Pictures via URL. |
| **Anti-Corruption Layer (ACL)** | An adapter inside Sprint Connect that translates between Sprint Connect's domain language and an external system's contract. Prevents external volatility from leaking into Order domain logic. Examples: `WMSAdapter`, `TMSAdapter`, `POSAdapter`. |

---

## Store & Routing Concepts

| Term | Definition |
|---|---|
| **All Store** | A store that is fully integrated with Sprint Connect. Orders from All Store flow: Gateway → Proxy Service → Sprint Connect. |
| **Not Roll Out Store** | A store not yet on Sprint Connect. Orders use the legacy path: Gateway → Proxy Service → TMS → Backend. |
| **RolloutPolicy** | The domain service that determines which integration path to use for a given store. Returns `true` (All Store) or `false` (Not Roll Out Store). Must be configurable per store without redeployment. |
| **Branch near me** | The customer-facing feature to discover the nearest available store branch before placing an order. |

---

## Domain Events

Events are facts — something that happened in the domain. Named in past tense.

| Event | Raised when |
|---|---|
| **OrderCreated** | A Sale Order is confirmed and persisted by Sprint Connect. Carries `FulfillmentType` and `OrderChannel`. |
| **BookingConfirmed** | A Delivery Slot has been successfully reserved. |
| **PickStarted** | Sprint Connect has instructed WMS to begin picking. |
| **PickConfirmed** | WMS reports that picking is complete with actual Basket Quantity. |
| **InvoiceGenerated** | ABB/Tax Invoice has been produced for the Order. |
| **OutForDelivery** | TMS reports the Driver has started delivery. |
| **Delivered** | TMS reports the Order has been delivered to the Customer. |
| **OrderCancelled** | The Order has been cancelled by Customer or Staff. |
| **OrderRescheduled** | The Delivery Slot has been changed. |
| **PaymentNotified** | Payment has been confirmed and the Order moves to Paid. |
| **CreditNoteIssued** | A Credit Note has been generated due to a partial pick, weight shortage, or cancellation refund. |
| **ReadyForCollection** | Picking is complete for a ClickAndCollect order and the customer has been notified. |
| **OrderCollected** | Store Staff confirmed the customer collected a ClickAndCollect order in person. |
| **OrderPacked** | All Packages have been assigned and the Order transitions to `Packed`. Raised by `MarkPacked()` after `AssignPackages` is complete. Signals readiness for TMS dispatch. |
| **OrderOnHold** | The Order has been placed on hold. Carries `HoldReason`. |
| **OrderReleased** | The hold has been lifted and the Order resumes its lifecycle. |
| **ReturnRequested** | A customer or staff member has requested a return for the Order. Initiates pickup scheduling and refund evaluation. |
| **ReturnReceivedAtWarehouse** | WMS reports that returned items have arrived at the warehouse receiving dock. Triggers the inspection and Put Away process. |
| **PutAwayConfirmed** | WMS reports that all return items have been inspected and placed in their assigned storage locations (or disposed). Carries `ItemCondition` and `Sloc` per item. Triggers `RefundProcessed` if not yet issued. |
| **RefundProcessed** | A refund has been issued to the customer following a return or pre-paid cancellation. |
| **PackageLost** | TMS reports that a specific Package (by `TrackingId`) cannot be located or delivered. Requires manual intervention or re-dispatch. |
| **PackagesAssigned** | Order lines have been grouped into Packages after Pick Confirmed. Carries the full package groupings, TrackingIds, and vehicle types. |
| **PackageOutForDelivery** | TMS reports that the vehicle carrying one specific Package has started delivery. Carries `TrackingId`. |
| **PackageDelivered** | TMS reports that one specific Package has been delivered. Carries `TrackingId`. Does not mean the full Order is delivered. |
| **OrderFullyDelivered** | All Packages in the Order are Delivered. Raised by the Order aggregate when `IsFullyDelivered()` returns true. Triggers invoice generation. |
| **OrderLinesModified** | Raised when one or more Order Lines are added, removed, or changed after Order creation. Carries the full list of `OrderLineModification` entries. Triggers POS Recalculation. |
| **PackagesReassigned** | Raised when WMS replaces existing Package groupings before any Package is dispatched. Carries the updated groupings and vehicle types. Triggers TMS re-registration. |

---

## What to Avoid

These terms are ambiguous or belong to a specific system — avoid using them to describe the domain:

| Avoid | Use instead |
|---|---|
| "Order record" | **Order** |
| "Fulfillment status" | The specific **Order Lifecycle State** (e.g., Pick Confirmed, Out for Delivery) |
| "The OMS" (when talking to business) | **Sprint Connect** |
| "Sync the catalog" | **Batch Sync** of Item Master |
| "Final price" | **POS Recalculation result** |
| "Old store" | **Not Roll Out Store** |
| "New store" / "integrated store" | **All Store (Rolled Out Store)** |
| "Pickup order" | **Click & Collect** |
| "Fast order" / "instant order" | **Express** |
| "quantity" (for all items) | **Amount** with **Unit of Measure** — distinguish `Each` from `Gram`/`Kilogram` |
| "swap item" / "replace item" | **Substitution** — must be recorded on the Order Line |
| "marketplace order" | **Order** with `OrderChannel.type = Marketplace` |
| "POS order" (from terminal) | **Order** with `OrderChannel.type = POSTerminal` |
| "bulk order" (import) | **Order** with `OrderChannel.type = BulkImport` |
| "freeze order" / "pause order" | **OnHold** — use `HoldOrder(reason)` |
| "return refund" vs "shortage refund" | **Refund** (return) vs **Credit Note** (partial pick shortage) — they are different flows |
| "OrderItem" / "order item" | **Order Line** |
| "SubOrderItem" / "sub order item" | **Substituted Order Line** |
| "change package" (modify contents) | **ModifyOrderLines** |
| "change package" (regroup for delivery) | **ReSplitShipments** |
| "package" | **Package** — the physical box; TMS dispatch unit identified by TrackingId |
| "Shipment" | **Package** — Shipment is removed; Package is now both physical unit and dispatch unit |
| "order type" (informally) | **FulfillmentType** (Delivery / ClickAndCollect / Express) |
| "order source" / "channel" | **OrderChannel** (Gateway / B2B / Kiosk) |
