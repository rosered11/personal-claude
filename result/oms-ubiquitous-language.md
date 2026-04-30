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
| **POS Recalculation** | The process of re-computing Order totals and prices. Triggered by WMS after Pick Confirmed (to reflect actual Basket Quantity) and by TMS after Delivered (to finalize the POD invoice). Sprint Connect calls POS, receives updated prices, and notifies the customer via Gateway. May run up to two rounds: Round 1 after `PickConfirmed`, Round 2 after `Delivered` (POD only). |
| **Recalculation Round** | A numbered iteration of POS Recalculation for an Order. Round 1 is triggered by `PickConfirmed`; Round 2 is triggered by `Delivered` (POD only). Each round produces a new set of `order_line_amounts` rows. Invoice generation always uses the latest round. Stored as `recalc_round` on `order_line_amounts`. |
| **Original Unit Price** | The per-unit price captured from POS at the moment the Order was created. Never overwritten. Stored as `original_unit_price` on `order_lines`. Provides the baseline for price comparison and dispute resolution. |
| **Recalculated Unit Price** | The per-unit price returned by POS after a recalculation round. Stored in `order_line_amounts.recalculated_unit_price`. Used to compute the final invoice total. May differ from `original_unit_price` for weight-based items. |
| **POS Recalc Pending** | A flag (`pos_recalc_pending`) on the Order that is set to `true` when a POS Recalculation request is dispatched via the Outbox, and cleared to `false` when POS responds and amounts are applied. The `MarkPacked` command checks this flag — the Order cannot advance to `Packed` while recalculation is still in flight. Prevents TMS dispatch before the invoice total is finalised. |
| **Trigger Event** | The domain event that caused a specific POS Recalculation round. Values: `PickConfirmed` (Round 1), `Delivered` (Round 2, POD only). Stored as `trigger_event` on `order_line_amounts` for audit purposes. |
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
| **Delivery Note (DN)** | A document listing the items inside a specific Package. Generated at `Packed` state — one DN per Package. The Driver carries it during delivery and the Customer signs it as proof of receipt. TMS registers each Package using its `TrackingId` and `DeliveryNoteNumber` before dispatch. In multi-vehicle orders, each Package has its own DN because items travel on separate vehicles. |

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

## Field Glossary — ER Diagram Field Definitions

Definitions for every domain-significant field in the ER diagram. Self-explanatory audit fields (`created_at`, `updated_at`, `created_by`, `updated_by`) are omitted.

---

### Order Module — `orders`

| Field | Definition |
|---|---|
| `order_id` | UUID primary key. Assigned by Sprint Connect on creation. Never exposed externally as the order identity — use `order_number` for communication. |
| `order_number` | Human-readable sequential identifier (e.g. `ORD-2026-00001`). Used in customer-facing communication and support lookups. Unique across all orders. |
| `source_order_id` | The order reference assigned by the originating channel (Gateway, Marketplace, etc.). Used to correlate Sprint Connect orders back to the external system. Unique constraint prevents duplicate order ingestion. |
| `channel_type` | The channel through which this order was placed. See **Order Channels** section. Values: `Gateway`, `B2B`, `Kiosk`, `Marketplace`, `POSTerminal`, `BulkImport`. |
| `business_unit` | The retail business unit (BU) code this order belongs to. Determines which store group handles fulfilment and which POS instance is called for recalculation. |
| `store_id` | FK to `config.store_locations`. The specific store responsible for picking and dispatching this order. |
| `status` | Current lifecycle state of the order. See **Order Lifecycle States** section. Transitions are enforced by the Order aggregate state machine — never updated directly by SQL. |
| `pre_hold_status` | The status the order was in immediately before it was placed `OnHold`. Stored so `ReleaseOrder()` can restore the order to the exact prior state without guessing. Null when order is not on hold. |
| `hold_reason` | Human-readable explanation of why the order is on hold. Set by `HoldOrder(reason)`. Examples: `PaymentVerificationFailed`, `ComplianceReview`, `CustomerRequest`. |
| `fulfillment_type` | How this order is fulfilled. Drives routing, state machine path, and which external systems are involved. Values: `Delivery`, `ClickAndCollect`, `Express`. |
| `payment_method` | How the customer will pay. Values: `PrePaid` (payment before delivery), `PayOnDelivery` (payment after delivery). Determines when invoice is generated. |
| `substitution_flag` | Whether the customer has consented to receive substitute items if ordered products are out of stock. `true` = substitutions allowed. |
| `pos_recalc_pending` | `true` while the Order is waiting for a POS Recalculation response. Set to `true` when `PosRecalculationRequested` is written to the Outbox; cleared to `false` when the response is applied. `MarkPacked` is rejected while this flag is `true`. See **POS Recalc Pending**. |

---

### Order Module — `order_lines`

| Field | Definition |
|---|---|
| `order_line_id` | UUID primary key for this line. Used as the reference key when assigning lines to packages (`order_package_lines`). |
| `sku` | Stock Keeping Unit — the product identifier in the Item Master. Used to look up product info and to communicate with WMS. |
| `barcode` | Physical product barcode (EAN/UPC). Used by WMS pickers to scan and confirm items during picking. |
| `product_name` | Display name of the product at the time the order was placed. Denormalised from Item Master — does not change if the catalog is later updated. |
| `requested_amount` | The quantity or weight originally ordered by the customer. Stored as a decimal to support weight-based items (e.g. 0.5 kg chicken). See **Requested Amount**. |
| `picked_amount` | The actual quantity or weight confirmed by the Picker during Pick Confirmed. May be less than `requested_amount` if stock is short. Drives POS Recalculation and invoice total. See **Picked Amount**. |
| `unit_of_measure` | How the item is measured. Values: `Each`, `Kilogram`, `Gram`, `Litre`. Determines how `CalculateTotal()` computes the line amount. See **Unit of Measure (UoM)**. |
| `original_unit_price` | Price per one unit/gram/kg captured from POS at the moment the Order was created. This field is **never overwritten**. POS Recalculation results are stored in `order_line_amounts`, not here. See **Original Unit Price**. |
| `currency` | ISO 4217 currency code for `unit_price` (e.g. `THB`). |
| `status` | Lifecycle state of this specific line. Values: `Active`, `Cancelled`, `Substituted`, `OutOfStock`. Independent from the order-level `status`. |
| `is_substitute` | `true` if this line was added as a substitution for an out-of-stock item. Links to `order_line_substitutions` for the original item detail. |

---

### Order Module — `order_line_substitutions`

| Field | Definition |
|---|---|
| `order_line_id` | FK to the substitute line (the new item). Not the original line. |
| `substitute_sku` | SKU of the replacement product offered by the Picker. |
| `substitute_unit_price` | Price of the substitute item. May differ from the original item's price — used in POS Recalculation. |
| `substituted_amount` | The quantity of the substitute item provided. |
| `customer_approved` | `true` if the customer explicitly accepted the substitution. `false` means the substitution was applied but not yet confirmed. Required for audit and potential credit note issuance. |

---

### Order Module — `order_packages`

| Field | Definition |
|---|---|
| `package_id` | UUID primary key for the Package entity. Internal identifier. |
| `tracking_id` | The external tracking identifier assigned to this Package. Used by TMS, customers, and staff to track delivery status. This is the key TMS uses in `PackageOutForDelivery` and `PackageDelivered` callbacks. See **TrackingId**. |
| `carrier_package_id` | The logistics carrier's internal reference for this Package. Distinct from `tracking_id` — carriers sometimes have their own internal IDs separate from customer-facing tracking numbers. |
| `third_party_logistic` | Name of the logistics company assigned to deliver this Package (e.g. `Flash Express`, `Kerry`). Set by TMS when the package is dispatched. |
| `vehicle_type` | Class of vehicle assigned to this Package. Values: `StandardCar`, `Van`, `Truck`. Set by WMS during `AssignPackages` based on item size and weight. See **VehicleType**. |
| `status` | Lifecycle state of this Package. Values: `Pending`, `OutForDelivery`, `Delivered`. Independent per Package. See **PackageStatus**. |
| `package_weight` | Total physical weight of this Package in kilograms. Set by WMS. Used by TMS for vehicle load planning. |
| `delivery_note_number` | Unique reference number of the Delivery Note issued for this Package. Generated when the Order reaches `Packed` state. TMS uses this number when registering the package for dispatch. The Driver carries the physical DN document and obtains the Customer's signature upon delivery. See **Delivery Note (DN)**. |

---

### Order Module — `order_package_lines`

| Field | Definition |
|---|---|
| `package_id` | FK to the Package this line belongs to. |
| `order_line_id` | FK to the Order Line assigned to this Package. One line can only be in one package. |

Join table only — no additional domain fields.

---

### Order Module — `order_holds`

| Field | Definition |
|---|---|
| `hold_reason` | The reason code or description for placing the order on hold. Examples: `PackageLost`, `PaymentVerificationRequired`, `ComplianceFlag`. |
| `held_at` | Timestamp when the hold was applied. |
| `released_at` | Timestamp when the hold was lifted. Null if still on hold. |
| `held_by` | Identity of who or what triggered the hold (staff ID, system process name). |
| `released_by` | Identity of who lifted the hold. Null if still on hold. |

---

### Order Module — `delivery_slots`

| Field | Definition |
|---|---|
| `slot_id` | UUID primary key. |
| `store_id` | The store whose TMS slot calendar this slot belongs to. |
| `scheduled_start` | Start of the agreed delivery window. |
| `scheduled_end` | End of the agreed delivery window. |

---

### Order Module — `order_outbox`

| Field | Definition |
|---|---|
| `event_type` | Name of the domain event to be delivered (e.g. `PickStarted`, `OrderPackaged`, `PackageDelivered`). Determines which ACL adapter the Outbox Worker routes the event to. |
| `target_system` | The external system this event must be delivered to. Values: `WMS`, `TMS`, `POS`, `STS`, `LEGACY`. One row per target — if an event goes to two systems, two rows are written in the same transaction. |
| `event_payload` | JSONB document containing the full event data. Schema is defined per `event_type`. Stored as JSONB to allow querying and schema evolution without column changes. |
| `status` | Delivery state of this outbox entry. Values: `Pending` (not yet sent), `Processing` (currently being sent by worker), `Published` (successfully delivered), `Failed` (exceeded retry limit — needs manual intervention). |
| `retry_count` | Number of delivery attempts made so far. Incremented on each failed attempt. Worker stops retrying when this exceeds the configured max (e.g. 5). |
| `next_retry_at` | Timestamp of the next allowed retry. Set using exponential backoff after each failure. Worker only processes entries where `next_retry_at <= NOW()`. |
| `published_at` | Timestamp when the event was successfully delivered to the target system. Null until status = `Published`. Used by the purge job to clean up old records. |

---

### Payment Module — `order_payments`

| Field | Definition |
|---|---|
| `source_payment_id` | Payment reference assigned by the external payment gateway (e.g. Omise, 2C2P). Used to correlate Sprint Connect payment records with the payment provider's records. |
| `status` | Payment lifecycle state. Values: `Pending`, `Authorised`, `Captured`, `Refunded`, `Failed`. |
| `payment_date` | When the payment was confirmed/captured. |

---

### Payment Module — `payment_transactions`

| Field | Definition |
|---|---|
| `payment_method` | How the customer paid. Examples: `CreditCard`, `PromptPay`, `Cash`, `Instalment`. |
| `payment_sub_method` | More specific payment variant. Examples: `Visa`, `MasterCard`, `KBank`. |
| `source_transaction_id` | The payment provider's transaction reference for this specific charge. Used for reconciliation. |
| `bill_address_ref` | Reference to the billing address used for this transaction. Points to an `order_addresses` record where `address_type = Billing`. |

---

### Payment Module — `invoices`

| Field | Definition |
|---|---|
| `invoice_number` | Unique sequential invoice number. The official document number used for tax filing and customer records. |
| `invoice_type` | Type of invoice issued. Values: `ABB` (Abbreviated Tax Invoice for standard purchases), `FullTax` (Full Tax Invoice for B2B/corporate). |
| `total_amount` | The final invoice total after POS Recalculation and any adjustments. |
| `status` | Values: `Draft`, `Issued`, `Cancelled`. |
| `generated_at` | When the invoice was produced. For PrePaid: after PickConfirmed. For POD: after Delivered. |

---

### Payment Module — `credit_notes`

| Field | Definition |
|---|---|
| `credit_note_number` | Unique document number for this credit note. Used by Finance for reconciliation. |
| `reason` | Why this credit note was issued. Values: `PartialPick` (items unavailable), `WeightShortage` (weight-based item was less than ordered), `Cancellation` (PrePaid order cancelled). |
| `source` | How this credit note originated. Values: `Online` (raised by WMS/Sprint Connect in real-time), `Batch` (reconciled from STS batch file). Both sources may produce a credit note for the same event — must be handled idempotently. |
| `invoice_id` | FK to the invoice this credit note reduces. A credit note is always issued against a specific invoice. |

---

### Payment Module — `order_line_amounts`

| Field | Definition |
|---|---|
| `recalc_round` | Which POS Recalculation round produced this record. `1` = after `PickConfirmed`, `2` = after `Delivered` (POD only). Multiple rows per `order_line_id` are allowed — one per round. Invoice generation uses the row with the highest `recalc_round`. See **Recalculation Round**. |
| `trigger_event` | The domain event that triggered this recalculation round. Values: `PickConfirmed`, `Delivered`. See **Trigger Event**. |
| `recalculated_unit_price` | The unit price returned by POS for this round. May differ from `order_lines.original_unit_price` for weight-based items or when promotions change the effective price. See **Recalculated Unit Price**. |
| `gross_amount` | Total line amount before tax deductions. `picked_amount × recalculated_unit_price` for this recalculation round. |
| `net_amount` | Total line amount after tax deductions for this recalculation round. |
| `unit_gross_amount` | Gross price per single unit for this recalculation round. |
| `unit_net_amount` | Net price per single unit for this recalculation round. |
| `recalculated_at` | When POS returned the amounts for this round. |

---

### Payment Module — `order_line_taxes`

| Field | Definition |
|---|---|
| `tax_type` | Type of tax applied. Examples: `VAT` (Value Added Tax), `WHT` (Withholding Tax). |
| `tax_description` | Human-readable label for this tax entry (e.g. `VAT 7%`). |
| `amount` | Absolute tax amount in currency. |
| `rate` | Tax rate as a decimal (e.g. `0.07` for 7% VAT). |

---

### Payment Module — `order_fees`

| Field | Definition |
|---|---|
| `source_fee_id` | Fee reference from the originating system (POS). Used for reconciliation. |
| `fee_code` | Machine-readable fee identifier. Examples: `DELIVERY_FEE`, `SERVICE_FEE`, `PACKAGING_FEE`. |
| `fee_name` | Human-readable fee label shown on the invoice. |
| `fee_type` | Category of fee. Examples: `Delivery`, `Service`, `Handling`. |

---

### Payment Module — `order_promotions`

| Field | Definition |
|---|---|
| `source_promo_id` | Promotion reference from the POS promotion engine. |
| `promo_code` | The promotion code applied (e.g. `SAVE10`, `FREESHIP`). |
| `promo_type` | How the discount is applied. Values: `Percentage` (reduces price by %), `FixedAmount` (reduces by fixed value), `FreeItem` (adds a free product line). |
| `discount_amount` | Absolute discount value in currency. Null if `promo_type = Percentage`. |
| `discount_percentage` | Percentage discount (e.g. `0.10` = 10% off). Null if `promo_type = FixedAmount`. |
| `order_line_id` | FK to the specific line this promotion applies to. Null if the promotion applies to the whole order. |

---

### Returns Module — `returns`

| Field | Definition |
|---|---|
| `return_order_number` | Human-readable sequential return reference. Used in customer communication and logistics pickup scheduling. |
| `invoice_id` | Reference to the invoice being reversed. Required to generate the credit note for the returned amount. |
| `credit_note_id` | Reference to the credit note issued for this return. Populated after refund amount is calculated. |
| `goods_receive_no` | WMS Goods Receive Number assigned when items arrive at the warehouse. External reference from WMS used to confirm physical receipt. |
| `return_reason` | Why the customer is returning the order. Examples: `WrongItem`, `DamagedOnArrival`, `ChangeOfMind`. |
| `status` | Return lifecycle state. Values: `ReturnRequested`, `PickupScheduled`, `PickedUp`, `ReceivedAtWarehouse`, `Inspected`, `PutAway`, `Refunded`. |
| `requested_at` | When the customer submitted the return request. |
| `pickup_scheduled_at` | When TMS scheduled the return pickup from the customer. |
| `picked_up_at` | When the Driver collected the items from the customer location. |
| `received_at` | When items arrived at the warehouse receiving dock. See **ReceivedAtWarehouse**. |
| `inspected_at` | When warehouse staff completed condition inspection of all items. |
| `put_away_at` | When all items were physically placed in their assigned Sloc or sent to disposal. See **PutAway**. |
| `refunded_at` | When the refund was processed to the customer's payment method. |

---

### Returns Module — `return_items`

| Field | Definition |
|---|---|
| `order_line_id` | FK to the original Order Line being returned. Links the return item back to the original order for refund calculation. |
| `condition` | Quality classification assigned during inspection. Values: `Resellable` (goes back to stock), `Damaged` (cannot be sold), `Dispose` (must be written off). See **ItemCondition**. |
| `item_reason` | Per-item return reason, more specific than the return-level `return_reason`. Example: `SealBroken`, `ExpiredDate`. |
| `put_away_status` | Whether this specific item has been put away. Values: `Pending`, `PutAway`, `Disposed`. |
| `assigned_sloc` | The specific warehouse storage location (bin/shelf) assigned to this item during Put Away. Only populated for `Resellable` items. See **StorageLocation (Sloc)**. |
| `payment_method` | The payment method to use for the refund of this item. May differ per item if the original order used split payment. |
| `quantity` | Number of units (or weight) being returned. Stored as decimal to support weight-based items. |

---

### Returns Module — `return_put_away_logs`

| Field | Definition |
|---|---|
| `assigned_sloc` | Storage location where this item was placed. |
| `condition` | Item condition recorded at the time of this put-away action. |
| `performed_by` | Identity of the warehouse staff member who physically performed the put-away action. |
| `performed_at` | Timestamp of the physical put-away action. |

Append-only audit log — records are never updated, only inserted.

---

### Returns Module — `return_refunds`

| Field | Definition |
|---|---|
| `refund_amount` | Total amount to be returned to the customer for this return. Calculated after inspection (actual condition of returned items). |
| `refund_method` | How the refund is issued back to the customer. Examples: `OriginalPaymentMethod`, `StoreCredit`, `BankTransfer`. |
| `reference_no` | Transaction reference from the payment provider confirming the refund was processed. |
| `status` | Values: `Pending`, `Processing`, `Processed`, `Failed`. |
| `processed_at` | When the refund was successfully issued. |

---

### Configuration Module — `store_locations`

| Field | Definition |
|---|---|
| `source_bu` | The BU code used in legacy external systems (e.g. WMS, TMS, POS). Used by ACL adapters to translate between Sprint Connect's `store_id` and external system identifiers. |
| `source_loc` | The location code used in legacy external systems. Paired with `source_bu` to form the external store key. |
| `is_active` | Whether this store is currently operating. Inactive stores do not accept new orders. |
| `is_rolled_out` | Whether this store is on the Sprint Connect integration path (`true`) or the legacy Backend path (`false`). Drives `RolloutPolicy` routing decisions. |

---

### Configuration Module — `rollout_policies`

| Field | Definition |
|---|---|
| `is_rolled_out` | The rollout state for this store at the given effective period. |
| `integration_path` | Which integration path to use. Values: `AllStore` (Sprint Connect), `LegacyBackend` (Proxy Service → Backend). |
| `effective_from` | Start date/time when this policy applies. Allows scheduling a future rollout without immediate cutover. |
| `effective_to` | End date/time. Null means the policy is currently active with no planned end. |

---

### Configuration Module — `fulfillment_routing_rules`

| Field | Definition |
|---|---|
| `channel_type` | The order channel this rule applies to. Null = applies to all channels. |
| `fulfillment_type` | The FulfillmentType this rule defines behaviour for. |
| `requires_booking` | Whether orders of this type must have a confirmed Delivery Slot before picking can start. `true` for Delivery, `false` for Express and ClickAndCollect. |
| `requires_tms` | Whether TMS must be involved for dispatch. `false` for ClickAndCollect. |
| `initial_pick_status` | The Order status to set when `PickStarted` is triggered. Normally `PickStarted`, but may vary per routing rule. |
| `priority` | Tie-breaking order when multiple rules match the same channel + fulfillment combination. Lower number = higher priority. |

---

### Configuration Module — `notification_templates`

| Field | Definition |
|---|---|
| `template_name` | Unique identifier for this template. Used by application code to look up the right template for an event. |
| `event_type` | The domain event that triggers this notification (e.g. `OrderCreated`, `ReadyForCollection`). |
| `channel` | Delivery channel for the notification. Values: `Email`, `SMS`, `Push`, `Line`. |
| `body_template` | Notification body with placeholders (e.g. `{{order_number}}`, `{{customer_name}}`). Resolved at send time. |

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
| "change package" (regroup for delivery) | **ReassignPackages** |
| "package" | **Package** — the physical box; TMS dispatch unit identified by TrackingId |
| "Shipment" | **Package** — Shipment is removed; Package is now both physical unit and dispatch unit |
| "order type" (informally) | **FulfillmentType** (Delivery / ClickAndCollect / Express) |
| "order source" / "channel" | **OrderChannel** (Gateway / B2B / Kiosk) |
