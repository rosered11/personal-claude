# OMS API — Field-to-Database Mapping

Maps every response field (read APIs) and every request body field (webhook APIs) to the exact table and column in the ER diagrams.

**Schema prefixes used throughout:**
| Schema | Module |
|---|---|
| `orders.*` | Order Module |
| `payment.*` | Payment Module |
| `returns.*` | Returns Module |
| `config.*` | Configuration Module |
| `inbound.*` | Inbound Module |

---

## Contents

1. [GET /orders — List Orders](#1-get-orders--list-orders)
2. [GET /orders/{id} — Get Order](#2-get-ordersid--get-order)
3. [GET /orders/{id}/timeline — Get Order Timeline](#3-get-ordersidtimeline--get-order-timeline)
4. [GET /inbound/purchase-orders — List Purchase Orders](#4-get-inboundpurchase-orders--list-purchase-orders)
5. [GET /inbound/purchase-orders/{id} — Get Purchase Order](#5-get-inboundpurchase-ordersid--get-purchase-order)
6. [GET /inbound/transfer-orders — List Transfer Orders](#6-get-inboundtransfer-orders--list-transfer-orders)
7. [GET /inbound/transfer-orders/{id} — Get Transfer Order](#7-get-inboundtransfer-ordersid--get-transfer-order)
8. [GET /stock/{sku}/ledger — Get Stock Ledger](#8-get-stockskuledger--get-stock-ledger)
9. [POST /webhooks/wms/pick-confirmed](#9-post-webhookswmspick-confirmed)
10. [POST /webhooks/wms/put-away-confirmed](#10-post-webhookswmsput-away-confirmed)
11. [POST /webhooks/wms/goods-receipt-confirmed](#11-post-webhookswmsgoods-receipt-confirmed)
12. [POST /webhooks/wms/purchase-order-put-away-confirmed](#12-post-webhookswmspurchase-order-put-away-confirmed)
13. [POST /webhooks/wms/transfer-pick-confirmed](#13-post-webhookswmstransfer-pick-confirmed)
14. [POST /webhooks/wms/transfer-received](#14-post-webhookswmstransfer-received)
15. [POST /webhooks/tms/package-dispatched](#15-post-webhookstmspackage-dispatched)
16. [POST /webhooks/tms/package-delivered](#16-post-webhookstmspackage-delivered)
17. [POST /webhooks/pos/recalculation-result](#17-post-webhooksposrecalculation-result)

---

## 1. GET /orders — List Orders

Response: `items[]` array (paginated).

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `id` | string | `orders.orders` | `order_id` | UUID displayed as order_number string |
| `customer` | string | `orders.order_addresses` | `first_name + last_name` | `address_type = 'Delivery'`; concat at query time |
| `items` | number | `orders.order_lines` | COUNT(`order_line_id`) | Grouped by `order_id` |
| `type` | string | `orders.orders` | `fulfillment_type` | Enum: `Delivery`, `Express`, `ClickAndCollect` |
| `status` | string | `orders.orders` | `status` | |
| `store` | string | `config.store_locations` | `store_name` | JOIN on `orders.store_id = store_locations.store_id` |
| `amount` | number | `payment.order_line_amounts` | `net_amount` SUM | Latest `recalc_round` per `order_line_id`; falls back to `orders.order_lines.original_unit_price × picked_amount` if no recalc yet |
| `holdReason` | string \| null | `orders.orders` | `hold_reason` | `null` when not on hold |
| `createdAt` | timestamp | `orders.orders` | `created_at` | |
| `updatedAt` | timestamp | `orders.orders` | `updated_at` | |
| `total` | number | — | — | COUNT of matching rows; pagination metadata |
| `page` | number | — | — | Query parameter echo |
| `limit` | number | — | — | Query parameter echo |

---

## 2. GET /orders/{id} — Get Order

Response: single order object with nested `customer`, `deliveryAddress`, `deliverySlot`, `lines[]`, `packages[]`.

### Root fields

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `id` | string | `orders.orders` | `order_id` | |
| `orderNumber` | string | `orders.orders` | `order_number` | |
| `type` | string | `orders.orders` | `fulfillment_type` | |
| `channel` | string | `orders.orders` | `channel_type` | |
| `status` | string | `orders.orders` | `status` | |
| `store` | string | `config.store_locations` | `store_name` | JOIN `orders.store_id` |
| `amount` | number | `payment.order_line_amounts` | SUM `net_amount` | Latest recalc round |
| `originalAmount` | number | `orders.order_lines` | SUM(`original_unit_price × requested_amount`) | Before POS recalculation |
| `paymentMethod` | string | `orders.orders` | `payment_method` | |
| `holdReason` | string \| null | `orders.orders` | `hold_reason` | |
| `createdAt` | timestamp | `orders.orders` | `created_at` | |
| `updatedAt` | timestamp | `orders.orders` | `updated_at` | |

### `customer` object

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `customer.name` | string | `orders.order_addresses` | `first_name + last_name` | `address_type = 'Delivery'` |
| `customer.phone` | string | `orders.order_addresses` | `mobile_phone` | |
| `customer.email` | string | `orders.order_addresses` | `email` | |

### `deliveryAddress` object

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `deliveryAddress.address1` | string | `orders.order_addresses` | `address1` | `address_type = 'Delivery'` |
| `deliveryAddress.subdistrict` | string | `orders.order_addresses` | `subdistrict` | |
| `deliveryAddress.district` | string | `orders.order_addresses` | `district` | |
| `deliveryAddress.province` | string | `orders.order_addresses` | `province` | |
| `deliveryAddress.postalCode` | string | `orders.order_addresses` | `postal_code` | |

### `deliverySlot` object

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `deliverySlot.date` | string | `orders.delivery_slots` | `scheduled_start` | Date portion (YYYY-MM-DD) |
| `deliverySlot.windowStart` | string | `orders.delivery_slots` | `scheduled_start` | Time portion (HH:MM) |
| `deliverySlot.windowEnd` | string | `orders.delivery_slots` | `scheduled_end` | Time portion (HH:MM) |

### `lines[]` array

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `orderLineId` | string | `orders.order_lines` | `order_line_id` | |
| `sku` | string | `orders.order_lines` | `sku` | |
| `productName` | string | `orders.order_lines` | `product_name` | Denormalized from product catalog at order creation |
| `requestedQty` | number | `orders.order_lines` | `requested_amount` | |
| `pickedQty` | number | `orders.order_lines` | `picked_amount` | Updated by `PickConfirmed` webhook |
| `unitPrice` | number | `payment.order_line_amounts` | `recalculated_unit_price` | Latest recalc round; falls back to `order_lines.original_unit_price` |
| `totalPrice` | number | `payment.order_line_amounts` | `net_amount` | Latest recalc round per line |
| `unitOfMeasure` | string | `orders.order_lines` | `unit_of_measure` | |

### `packages[]` array

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `packageId` | string | `orders.order_packages` | `package_id` | |
| `trackingId` | string | `orders.order_packages` | `tracking_id` | |
| `vehicleType` | string | `orders.order_packages` | `vehicle_type` | |
| `status` | string | `orders.order_packages` | `status` | |

---

## 3. GET /orders/{id}/timeline — Get Order Timeline

Response: `order` summary + `events[]` + `summary` metrics.

### `order` summary object

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `order.id` | string | `orders.orders` | `order_id` | |
| `order.customer` | string | `orders.order_addresses` | `first_name + last_name` | |
| `order.store` | string | `config.store_locations` | `store_name` | |
| `order.items` | number | `orders.order_lines` | COUNT(`order_line_id`) | |
| `order.status` | string | `orders.orders` | `status` | |
| `order.type` | string | `orders.orders` | `fulfillment_type` | |
| `order.amount` | number | `payment.order_line_amounts` | SUM `net_amount` | Latest recalc round |
| `order.linkedPoId` | string \| null | `orders.order_webhook_logs` | `detail` | Parsed from first `PurchaseOrderCreated` webhook log entry; or a separate FK if added later |

### `events[]` array

Each event row is built from one of three source tables depending on `type`:

| `type` value | Primary source table | |
|---|---|---|
| `domain` | `orders.order_status_history` | OMS state transition |
| `webhook` | `orders.order_webhook_logs` | Received from WMS / TMS / POS |
| `outbox` | `orders.order_outbox` | Dispatched to WMS / TMS / POS |
| `bridge` | Derived marker | No stored row; synthesized between PO put-away and first order event |

| Response Field | Type | `domain` source | `webhook` source | `outbox` source |
|---|---|---|---|---|
| `id` | number | Row sequence | Row sequence | Row sequence |
| `occurredAt` | timestamp | `order_status_history.changed_at` | `order_webhook_logs.received_at` | `order_outbox.created_at` |
| `time` | string | HH:MM from `changed_at` | HH:MM from `received_at` | HH:MM from `created_at` |
| `phase` | string | Annotated at query time (`inbound`/`outbound`) | Annotated at query time | Annotated at query time |
| `type` | string | `'domain'` (fixed) | `'webhook'` (fixed) | `'outbox'` (fixed) |
| `system` | string | `'OMS'` (fixed) | `order_webhook_logs.source_system` | Destination system derived from `order_outbox.event_type` |
| `event` | string | `order_status_history.to_status` | `order_webhook_logs.event_type` | `order_outbox.event_type` |
| `detail` | string | `order_status_history.detail` | `order_webhook_logs.detail` | Derived from `order_outbox.event_payload` |
| `outStatus` | string \| null | `null` | `null` | `order_outbox.status` |

### `summary` object

| Response Field | Type | Derived from |
|---|---|---|
| `totalEvents` | number | COUNT of all events across all three tables |
| `inboundPhaseEvents` | number | COUNT where `phase = 'inbound'` |
| `bridgeEvents` | number | COUNT of synthesized bridge markers |
| `outboundPhaseEvents` | number | COUNT where `phase = 'outbound'` |
| `inboundToStockAvailableMinutes` | number | `bridge.occurredAt − first_inbound_event.occurredAt` |
| `orderToDeliveredMinutes` | number | `order_status_history(Delivered).changed_at − order_status_history(Pending).changed_at` |
| `totalEndToEndMinutes` | number | `last_event.occurredAt − first_event.occurredAt` |

---

## 4. GET /inbound/purchase-orders — List Purchase Orders

Response: `items[]` array.

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `id` | string | `inbound.purchase_orders` | `purchase_order_id` | |
| `poNumber` | string | `inbound.purchase_orders` | `po_number` | |
| `supplier` | string | External supplier registry | — | Supplier name resolved via `supplier_id`; not stored in OMS ER (denormalized or external lookup) |
| `supplierId` | string | `inbound.purchase_orders` | `supplier_id` | |
| `lines` | number | `inbound.purchase_order_lines` | COUNT(`po_line_id`) | Grouped by `purchase_order_id` |
| `status` | string | `inbound.purchase_orders` | `status` | Enum: `Created`, `PartiallyReceived`, `FullyReceived`, `Closed` |
| `store` | string | `config.store_locations` | `store_name` | JOIN `inbound.purchase_orders.store_id` |
| `value` | number | `inbound.purchase_order_lines` | SUM(`ordered_qty × unit_cost`) | |
| `goodsReceiveNo` | string \| null | `inbound.purchase_orders` | `goods_receive_no` | Set when first `GoodsReceiptConfirmed` webhook received |
| `createdAt` | timestamp | `inbound.purchase_orders` | `created_at` | |
| `updatedAt` | timestamp | `inbound.purchase_orders` | `updated_at` | |

---

## 5. GET /inbound/purchase-orders/{id} — Get Purchase Order

Root fields are identical to the list item above. Additional fields:

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `storeId` | string | `inbound.purchase_orders` | `store_id` | |

### `lines[]` array

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `poLineId` | string | `inbound.purchase_order_lines` | `po_line_id` | |
| `sku` | string | `inbound.purchase_order_lines` | `sku` | |
| `productName` | string | External product catalog | — | Resolved by SKU; not stored in Inbound ER |
| `orderedQty` | number | `inbound.purchase_order_lines` | `ordered_qty` | |
| `receivedQty` | number | `inbound.purchase_order_lines` | `received_qty` | Updated by `GoodsReceiptConfirmed` webhook |
| `unitCost` | number | `inbound.purchase_order_lines` | `unit_cost` | |
| `currency` | string | `inbound.purchase_order_lines` | `currency` | |
| `condition` | string | `inbound.purchase_order_lines` | `condition` | Set by `PutAwayConfirmed` webhook: `Resellable`, `Repairable`, `Dispose` |
| `sloc` | string | `inbound.purchase_order_lines` | `sloc` | Storage location set at put-away |
| `receivedAt` | timestamp | `inbound.purchase_order_lines` | `received_at` | Set by `GoodsReceiptConfirmed` |
| `putAwayAt` | timestamp | `inbound.purchase_order_lines` | `put_away_at` | Set by `PurchaseOrderPutAwayConfirmed` |

---

## 6. GET /inbound/transfer-orders — List Transfer Orders

Response: `items[]` array.

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `id` | string | `inbound.transfer_orders` | `transfer_order_id` | |
| `transferNumber` | string | `inbound.transfer_orders` | `transfer_number` | |
| `source` | string | `config.store_locations` | `store_name` | JOIN on `transfer_orders.source_store_id` |
| `sourceStoreId` | string | `inbound.transfer_orders` | `source_store_id` | |
| `dest` | string | `config.store_locations` | `store_name` | JOIN on `transfer_orders.dest_store_id` |
| `destStoreId` | string | `inbound.transfer_orders` | `dest_store_id` | |
| `lines` | number | `inbound.transfer_order_lines` | COUNT(`to_line_id`) | |
| `status` | string | `inbound.transfer_orders` | `status` | Enum: `Created`, `PickConfirmed`, `InTransit`, `Received`, `Completed` |
| `tracking` | string \| null | `inbound.transfer_orders` | `tracking_id` | Set when TMS dispatch registered |
| `createdAt` | timestamp | `inbound.transfer_orders` | `created_at` | |
| `updatedAt` | timestamp | `inbound.transfer_orders` | `updated_at` | |

---

## 7. GET /inbound/transfer-orders/{id} — Get Transfer Order

Root fields identical to list item. Additional `lines[]`:

### `lines[]` array

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `toLineId` | string | `inbound.transfer_order_lines` | `to_line_id` | |
| `sku` | string | `inbound.transfer_order_lines` | `sku` | |
| `productName` | string | External product catalog | — | Resolved by SKU |
| `requestedQty` | number | `inbound.transfer_order_lines` | `requested_qty` | |
| `transferredQty` | number | `inbound.transfer_order_lines` | `transferred_qty` | Set by `TransferPickConfirmed` webhook |
| `confirmedAt` | timestamp | `inbound.transfer_order_lines` | `confirmed_at` | Set by `TransferPickConfirmed` webhook |

---

## 8. GET /stock/{sku}/ledger — Get Stock Ledger

OMS does not own stock counts; this endpoint aggregates stock-movement events OMS recorded.

### Root fields

| Response Field | Type | Source | Notes |
|---|---|---|---|
| `sku` | string | Query path parameter | Filter key |
| `skuName` | string | External product catalog | Resolved by SKU |
| `unitPrice` | number | External product catalog or `orders.order_lines.original_unit_price` | Latest known price for display |
| `currency` | string | External product catalog | |

### `locations[]` array

| Response Field | Type | Table | Column | Notes |
|---|---|---|---|---|
| `storeId` | string | `config.store_locations` | `store_id` | Grouped from event sources |
| `storeName` | string | `config.store_locations` | `store_name` | |
| `balance` | number | Computed | SUM(`qtyChange`) across all events for this location | |

### `locations[].events[]` array

Each event row is sourced from a different table depending on `event` type:

| `event` value | Source table | Key columns |
|---|---|---|
| `PurchaseOrderPutAwayConfirmed` | `inbound.purchase_order_lines` | `put_away_at`, `ordered_qty` |
| `TransferPickConfirmed` (out) | `inbound.transfer_order_lines` | `confirmed_at`, `transferred_qty` |
| `TransferReceived` (in) | `inbound.transfer_orders` | `updated_at` (when status→`Received`); qty from `transfer_order_lines.transferred_qty` |
| `PickConfirmed` | `orders.order_status_history` | `changed_at` where `to_status = 'PickConfirmed'`; qty from `order_lines.picked_amount` |

| Response Field | Type | `PurchaseOrderPutAwayConfirmed` | `TransferPickConfirmed` / `TransferReceived` | `PickConfirmed` |
|---|---|---|---|---|
| `id` | number | Row sequence | Row sequence | Row sequence |
| `occurredAt` | timestamp | `purchase_order_lines.put_away_at` | `transfer_order_lines.confirmed_at` | `order_status_history.changed_at` |
| `time` | string | HH:MM from `put_away_at` | HH:MM from `confirmed_at` | HH:MM from `changed_at` |
| `dir` | string | `'in'` | `'out'` (pick) / `'in'` (receive) | `'out'` |
| `ref` | string | `purchase_orders.po_number` | `transfer_orders.transfer_number` | `orders.order_number` |
| `refType` | string | `'PurchaseOrder'` | `'TransferOrder'` | `'Order'` |
| `event` | string | `'PurchaseOrderPutAwayConfirmed'` | `'TransferPickConfirmed'` / `'TransferReceived'` | `'PickConfirmed'` |
| `qtyChange` | number | `+purchase_order_lines.received_qty` | `±transfer_order_lines.transferred_qty` | `−order_lines.picked_amount` |
| `balance` | number | Running sum | Running sum | Running sum |
| `detail` | string | `order_webhook_logs.detail` | `order_webhook_logs.detail` | `order_status_history.detail` |

---

## 9. POST /webhooks/wms/pick-confirmed

**Trigger:** WMS confirms items physically picked from shelf.

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `orderId` | `orders.orders` | `status` → `'PickConfirmed'` | UPDATE |
| `orderId` | `orders.orders` | `updated_at` | UPDATE |
| `orderId` | `orders.order_status_history` | `from_status`, `to_status = 'PickConfirmed'`, `changed_at`, `detail` | INSERT |
| `orderId` | `orders.order_webhook_logs` | `order_id`, `source_system = 'WMS'`, `event_type = 'PickConfirmed'`, `detail`, `received_at` | INSERT |
| `lines[].orderLineId` | `orders.order_lines` | `picked_amount` | UPDATE per line |
| `lines[].substituted = true` | `orders.order_line_substitutions` | `order_line_id`, `substitute_sku`, etc. | INSERT if substituted |
| `pickedAt` | `orders.order_webhook_logs` | `received_at` | |
| *(derived)* | `orders.order_outbox` | `event_type = 'PickConfirmedEvent'`, `event_payload`, `status = 'Pending'` | INSERT (triggers POS recalculation) |

---

## 10. POST /webhooks/wms/put-away-confirmed

**Trigger:** WMS confirms returned goods are shelved after a return receipt (UC20).

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `returnId` | `returns.returns` | `status` → `'PutAway'`, `put_away_at` | UPDATE |
| `items[].sku` | `returns.return_items` | `sku` | Matched by `return_id + sku` |
| `items[].condition` | `returns.return_items` | `condition`, `put_away_status = 'PutAway'` | UPDATE |
| `items[].sloc` | `returns.return_items` | `assigned_sloc`, `put_away_at` | UPDATE |
| `items[].sku + sloc + condition` | `returns.return_put_away_logs` | `return_id`, `return_item_id`, `sku`, `assigned_sloc`, `condition`, `quantity`, `performed_at` | INSERT |
| `putAwayAt` | `returns.returns` | `put_away_at` | |
| *(derived)* | `returns.return_refunds` | `return_id`, `refund_amount`, `refund_method`, `status = 'Pending'` | INSERT (atomic with put-away) |
| *(derived)* | `payment.credit_notes` | `order_id`, `invoice_id`, `amount`, `reason = 'Return'`, `status = 'Issued'` | INSERT |

---

## 11. POST /webhooks/wms/goods-receipt-confirmed

**Trigger:** WMS confirms physical goods arrived at dock against a Purchase Order (UC21).

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `purchaseOrderId` | `inbound.purchase_orders` | `status` → `'FullyReceived'` or `'PartiallyReceived'` | UPDATE |
| `purchaseOrderId` | `inbound.purchase_orders` | `goods_receive_no`, `updated_at` | UPDATE |
| `goodsReceiveNo` | `inbound.purchase_orders` | `goods_receive_no` | |
| `lines[].sku` | `inbound.purchase_order_lines` | Matched by `purchase_order_id + sku` | |
| `lines[].receivedQty` | `inbound.purchase_order_lines` | `received_qty`, `received_at` | UPDATE per line |
| `receivedAt` | `inbound.purchase_order_lines` | `received_at` | |
| *(derived)* | `orders.order_outbox` | `event_type = 'GoodsReceiptConfirmedEvent'`, `event_payload` | INSERT |

> **Note:** `order_webhook_logs` is scoped to `orders.order_id`. PO webhooks do not have an order_id — a separate `inbound_webhook_logs` table may be needed for PO-level audit, or the log can reference the `purchase_order_id` via the `detail` field.

---

## 12. POST /webhooks/wms/purchase-order-put-away-confirmed

**Trigger:** WMS confirms inbound goods are on shelf; closes the PO (UC21).

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `purchaseOrderId` | `inbound.purchase_orders` | `status` → `'Closed'`, `updated_at` | UPDATE |
| `items[].sku` | `inbound.purchase_order_lines` | Matched by `purchase_order_id + sku` | |
| `items[].condition` | `inbound.purchase_order_lines` | `condition` | UPDATE |
| `items[].sloc` | `inbound.purchase_order_lines` | `sloc` | UPDATE |
| `items[].qty` | `inbound.purchase_order_lines` | Validates against `received_qty` | — |
| `putAwayAt` | `inbound.purchase_order_lines` | `put_away_at` | UPDATE per line |
| *(derived)* | `orders.order_outbox` | `event_type = 'PurchaseOrderClosedEvent'`, `event_payload` | INSERT (signals stock available to WMS) |

---

## 13. POST /webhooks/wms/transfer-pick-confirmed

**Trigger:** WMS at source store confirms items packed for transfer (UC22).

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `transferOrderId` | `inbound.transfer_orders` | `status` → `'PickConfirmed'`, `updated_at` | UPDATE |
| `lines[].sku` | `inbound.transfer_order_lines` | Matched by `transfer_order_id + sku` | |
| `lines[].transferredQty` | `inbound.transfer_order_lines` | `transferred_qty` | UPDATE per line |
| `confirmedAt` | `inbound.transfer_order_lines` | `confirmed_at` | UPDATE per line |
| *(derived)* | `orders.order_outbox` | `event_type = 'TransferPickConfirmedEvent'`, `event_payload` | INSERT (notifies TMS to dispatch) |

---

## 14. POST /webhooks/wms/transfer-received

**Trigger:** WMS at destination store confirms stock arrived and put away (UC22).

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `transferOrderId` | `inbound.transfer_orders` | `status` → `'Completed'`, `updated_at` | UPDATE |
| `receivedAt` | `inbound.transfer_orders` | `updated_at` | UPDATE (no separate `received_at` column — extend if needed) |
| *(derived)* | `orders.order_outbox` | `event_type = 'TransferReceivedEvent'`, `event_payload` | INSERT |

---

## 15. POST /webhooks/tms/package-dispatched

**Trigger:** TMS driver collected the package; goods out for delivery.

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `trackingId` | `orders.order_packages` | `status` → `'OutForDelivery'`, `updated_at` | UPDATE — lookup by `tracking_id` |
| *(derived from package)* | `orders.orders` | `status` → `'OutForDelivery'`, `updated_at` | UPDATE |
| *(derived)* | `orders.order_status_history` | `from_status`, `to_status = 'OutForDelivery'`, `changed_at` | INSERT |
| `dispatchedAt` | `orders.order_webhook_logs` | `received_at`; `event_type = 'PackageDispatched'` | INSERT |

---

## 16. POST /webhooks/tms/package-delivered

**Trigger:** TMS confirms package delivered to customer.

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `trackingId` | `orders.order_packages` | `status` → `'Delivered'`, `updated_at` | UPDATE |
| *(derived from package)* | `orders.orders` | `status` → `'Delivered'`, `updated_at` | UPDATE |
| *(derived)* | `orders.order_status_history` | `to_status = 'Delivered'`, `changed_at`, `detail` | INSERT |
| `deliveredAt` | `orders.order_webhook_logs` | `received_at`; `event_type = 'PackageDelivered'` | INSERT |
| `proofOfDelivery` | `orders.order_webhook_logs` | `detail` | Stored as audit detail |
| *(derived)* | `orders.order_outbox` | `event_type = 'DeliveredEvent'` (→ POS for invoice) | INSERT |
| *(derived)* | `payment.invoices` | `order_id`, `invoice_number`, `total_amount`, `status = 'Generated'`, `generated_at` | INSERT (triggered by handler) |
| *(derived)* | `orders.order_outbox` | `event_type = 'InvoiceGeneratedEvent'` (→ POS for payment link) | INSERT |

---

## 17. POST /webhooks/pos/recalculation-result

**Trigger:** POS returns adjusted totals after applying promotions to actual picked quantities.

### Request body → DB writes

| Request Field | Table Written | Column Updated | Action |
|---|---|---|---|
| `orderId` | `orders.orders` | `pos_recalc_pending` → `false`, `updated_at` | UPDATE |
| `adjustedAmount` | `payment.order_line_amounts` | Stored per recalc round | INSERT per line |
| `originalAmount` | `payment.order_line_amounts` | Validated against prior amounts | — |
| `currency` | `payment.order_line_amounts` | `currency` | |
| `promotionsApplied[].promoCode` | `payment.order_promotions` | `promo_code` | INSERT per promo |
| `promotionsApplied[].discountAmount` | `payment.order_promotions` | `discount_amount` | |
| `promotionsApplied[].description` | `payment.order_promotions` | `promo_name` | |
| `recalculatedAt` | `payment.order_line_amounts` | `recalculated_at`, `created_at` | |
| `recalculatedAt` | `orders.order_webhook_logs` | `received_at`; `event_type = 'RecalculationResult'` | INSERT |
| *(derived)* | `payment.order_line_amounts` | `recalc_round` = prior MAX + 1; `trigger_event = 'PickConfirmed'` | INSERT per order line |

---

## Cross-reference: which tables each API touches

| Table | List Orders | Get Order | Timeline | List POs | Get PO | List TOs | Get TO | Stock Ledger |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| `orders.orders` | R | R | R | | | | | |
| `orders.order_lines` | R (count) | R | | | | | | R |
| `orders.order_addresses` | R | R | R | | | | | |
| `orders.order_customers` | | R | | | | | | |
| `orders.order_packages` | | R | | | | | | |
| `orders.order_package_lines` | | R | | | | | | |
| `orders.delivery_slots` | | R | | | | | | |
| `orders.order_holds` | | | | | | | | |
| `orders.order_status_history` | | | R | | | | | R |
| `orders.order_webhook_logs` | | | R | | | | | R |
| `orders.order_outbox` | | | R | | | | | |
| `orders.order_line_substitutions` | | R | | | | | | |
| `payment.order_line_amounts` | R | R | | | | | | |
| `payment.order_promotions` | | | | | | | | |
| `payment.invoices` | | | | | | | | |
| `payment.credit_notes` | | | | | | | | |
| `config.store_locations` | R | R | R | R | R | R | R | R |
| `inbound.purchase_orders` | | | | R | R | | | |
| `inbound.purchase_order_lines` | | | | R | R | | | R |
| `inbound.transfer_orders` | | | | | | R | R | R |
| `inbound.transfer_order_lines` | | | | | | R | R | R |
| `inbound.damaged_goods_receipts` | | | | | | | | |
| `inbound.damaged_goods_items` | | | | | | | | |
