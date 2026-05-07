FORMAT: 1A
HOST: https://api.sprintconnect.io/v1

# Sprint Connect OMS API

REST API for the Sprint Connect Order Management System.
All timestamps are ISO 8601 UTC. All monetary values are integers in the smallest currency unit (satang / stang).
Authentication uses Bearer JWT on every endpoint unless marked public.

---

# Group Authentication

## Token [/auth/token]

### Request Access Token [POST]

Exchange service credentials for a short-lived JWT.

+ Request (application/json)

    + Headers

            X-Service-Name: oms-dashboard

    + Body

            {
              "client_id": "dashboard-client",
              "client_secret": "••••••••"
            }

+ Response 200 (application/json)

        {
          "access_token": "eyJhbGciOiJSUzI1NiJ9...",
          "token_type": "Bearer",
          "expires_in": 3600
        }

+ Response 401 (application/json)

        {
          "error": "invalid_client",
          "error_description": "Client credentials are invalid."
        }

---

# Group Orders

Outbound customer orders — goods leaving the warehouse.

## Order Collection [/orders{?status,store,type,page,limit}]

### List Orders [GET]

Returns a paginated list of outbound orders. Use `status` (multi-value, comma-separated) to filter the Kanban Board columns.

+ Parameters

    + status (optional, string, `Pending,PickStarted`) ... Comma-separated order statuses. Allowed values: `Pending`, `BookingConfirmed`, `PickStarted`, `PickConfirmed`, `ReadyForCollection`, `Packed`, `Delivering`, `OutForDelivery`, `Delivered`, `Invoiced`, `Paid`, `OnHold`, `Cancelled`, `Returned`.
    + store (optional, string, `Central DC`) ... Filter by store or DC name.
    + type (optional, string, `Delivery`) ... Fulfillment type. Allowed: `Delivery`, `Express`, `ClickAndCollect`.
    + page (optional, number, `1`) ... 1-based page number. Default `1`.
    + limit (optional, number, `50`) ... Items per page. Max `200`. Default `50`.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "items": [
            {
              "id": "ORD-001",
              "customer": "Alice Johnson",
              "items": 5,
              "type": "Delivery",
              "status": "Pending",
              "store": "Central DC",
              "amount": 2450,
              "holdReason": null,
              "createdAt": "2024-05-06T14:02:00Z",
              "updatedAt": "2024-05-06T14:02:00Z"
            },
            {
              "id": "ORD-006",
              "customer": "Frank Lee",
              "items": 12,
              "type": "Delivery",
              "status": "OnHold",
              "store": "Central DC",
              "amount": 5600,
              "holdReason": "PackageDamaged",
              "createdAt": "2024-05-06T09:10:00Z",
              "updatedAt": "2024-05-06T18:30:00Z"
            }
          ],
          "total": 14,
          "page": 1,
          "limit": 50
        }

+ Response 400 (application/json)

        {
          "error": "invalid_parameter",
          "detail": "Unknown status value 'PENDING'. Use PascalCase enum values."
        }

+ Response 401 (application/json)

        {
          "error": "unauthorized",
          "detail": "Bearer token missing or expired."
        }

## Order [/orders/{id}]

### Get Order [GET]

Returns full detail for a single order, including all lines and packages.

+ Parameters

    + id (required, string, `ORD-001`) ... Order ID.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "id": "ORD-001",
          "orderNumber": "ORD-001",
          "customer": {
            "name": "Alice Johnson",
            "phone": "+66812345678",
            "email": "alice@example.com"
          },
          "type": "Delivery",
          "channel": "Web",
          "status": "Paid",
          "store": "Central DC",
          "amount": 2380,
          "originalAmount": 2450,
          "paymentMethod": "CreditCard",
          "holdReason": null,
          "deliveryAddress": {
            "address1": "99/1 Sukhumvit Rd",
            "subdistrict": "Khlong Toei",
            "district": "Khlong Toei",
            "province": "Bangkok",
            "postalCode": "10110"
          },
          "deliverySlot": {
            "date": "2024-05-06",
            "windowStart": "18:00",
            "windowEnd": "20:00"
          },
          "lines": [
            {
              "orderLineId": "line-001",
              "sku": "APPLE-1KG",
              "productName": "Apple (1 kg bag)",
              "requestedQty": 5,
              "pickedQty": 5,
              "unitPrice": 120,
              "totalPrice": 600,
              "unitOfMeasure": "Each"
            }
          ],
          "packages": [
            {
              "packageId": "pkg-001",
              "trackingId": "TRK-2024-001",
              "vehicleType": "Van",
              "status": "Delivered"
            }
          ],
          "createdAt": "2024-05-06T14:02:00Z",
          "updatedAt": "2024-05-06T19:31:00Z"
        }

+ Response 404 (application/json)

        {
          "error": "not_found",
          "detail": "Order ORD-999 does not exist."
        }

## Order Timeline [/orders/{id}/timeline]

### Get Order Timeline [GET]

Returns the full event log for a single order in chronological order.
Each event has a `type`: `domain` (OMS state change), `webhook` (received from WMS/TMS/POS), `outbox` (dispatched to external system), or `bridge` (WMS stock marker).

+ Parameters

    + id (required, string, `ORD-001`) ... Order ID.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "order": {
            "id": "ORD-001",
            "customer": "Alice Johnson",
            "store": "Central DC",
            "items": 5,
            "status": "Paid",
            "type": "Delivery",
            "amount": 2380,
            "linkedPoId": "PO-001"
          },
          "events": [
            {
              "id": 1,
              "time": "08:00",
              "occurredAt": "2024-05-06T08:00:00Z",
              "phase": "inbound",
              "type": "domain",
              "system": "OMS",
              "event": "PurchaseOrderCreated",
              "detail": "PO-001 — Fresh Foods Ltd · 15 lines · ₿45,000. Status → Created",
              "outStatus": null
            },
            {
              "id": 6,
              "time": "10:15",
              "occurredAt": "2024-05-06T10:15:00Z",
              "phase": "bridge",
              "type": "bridge",
              "system": "WMS",
              "event": "Stock Available in WMS",
              "detail": "WMS incremented available inventory for all received SKUs. Picker can now fulfill orders.",
              "outStatus": null
            },
            {
              "id": 7,
              "time": "14:02",
              "occurredAt": "2024-05-06T14:02:00Z",
              "phase": "outbound",
              "type": "domain",
              "system": "OMS",
              "event": "OrderCreated",
              "detail": "ORD-001 — Alice Johnson · Delivery · 5 items · ₿2,450. Status → Pending",
              "outStatus": null
            },
            {
              "id": 27,
              "time": "19:31",
              "occurredAt": "2024-05-06T19:31:00Z",
              "phase": "outbound",
              "type": "domain",
              "system": "OMS",
              "event": "OrderPaid",
              "detail": "Order fully closed. Total time from order → payment: 5h 29m",
              "outStatus": null
            }
          ],
          "summary": {
            "totalEvents": 27,
            "inboundPhaseEvents": 5,
            "bridgeEvents": 1,
            "outboundPhaseEvents": 21,
            "inboundToStockAvailableMinutes": 75,
            "orderToDeliveredMinutes": 329,
            "totalEndToEndMinutes": 691
          }
        }

+ Response 404 (application/json)

        {
          "error": "not_found",
          "detail": "Order ORD-999 does not exist."
        }

---

# Group Inbound

Goods arriving at the warehouse: Purchase Orders (from suppliers) and Transfer Orders (between stores / DCs).

## Purchase Order Collection [/inbound/purchase-orders{?status,store,page,limit}]

### List Purchase Orders [GET]

Returns all Purchase Orders. Used by the Kanban Board Inbound tab (PO swimlane).

+ Parameters

    + status (optional, string, `Created,PartiallyReceived`) ... Comma-separated PO statuses. Allowed: `Created`, `PartiallyReceived`, `FullyReceived`, `Closed`.
    + store (optional, string, `Central DC`) ... Receiving store or DC.
    + page (optional, number, `1`) ... Default `1`.
    + limit (optional, number, `50`) ... Max `200`. Default `50`.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "items": [
            {
              "id": "PO-001",
              "poNumber": "PO-001",
              "supplier": "Fresh Foods Ltd",
              "supplierId": "sup-fresh-foods",
              "lines": 15,
              "status": "Closed",
              "store": "Central DC",
              "value": 45000,
              "goodsReceiveNo": "GRN-2024-001",
              "createdAt": "2024-05-06T08:00:00Z",
              "updatedAt": "2024-05-06T10:15:00Z"
            },
            {
              "id": "PO-002",
              "poNumber": "PO-002",
              "supplier": "Beverages Corp",
              "supplierId": "sup-beverages",
              "lines": 8,
              "status": "Created",
              "store": "Store A",
              "value": 12000,
              "goodsReceiveNo": null,
              "createdAt": "2024-05-06T11:00:00Z",
              "updatedAt": "2024-05-06T11:00:00Z"
            },
            {
              "id": "PO-003",
              "poNumber": "PO-003",
              "supplier": "Dairy Direct",
              "supplierId": "sup-dairy",
              "lines": 5,
              "status": "PartiallyReceived",
              "store": "Store B",
              "value": 8500,
              "goodsReceiveNo": "GRN-2024-002",
              "createdAt": "2024-05-06T07:30:00Z",
              "updatedAt": "2024-05-06T09:45:00Z"
            },
            {
              "id": "PO-004",
              "poNumber": "PO-004",
              "supplier": "Organic Valley",
              "supplierId": "sup-organic",
              "lines": 12,
              "status": "FullyReceived",
              "store": "Central DC",
              "value": 31000,
              "goodsReceiveNo": "GRN-2024-003",
              "createdAt": "2024-05-05T14:00:00Z",
              "updatedAt": "2024-05-06T08:30:00Z"
            }
          ],
          "total": 4,
          "page": 1,
          "limit": 50
        }

+ Response 401 (application/json)

        {
          "error": "unauthorized",
          "detail": "Bearer token missing or expired."
        }

## Purchase Order [/inbound/purchase-orders/{id}]

### Get Purchase Order [GET]

Returns full detail for a single PO including all order lines with received quantities and conditions.

+ Parameters

    + id (required, string, `PO-001`) ... Purchase Order ID.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "id": "PO-001",
          "poNumber": "PO-001",
          "supplier": "Fresh Foods Ltd",
          "supplierId": "sup-fresh-foods",
          "store": "Central DC",
          "storeId": "store-central-dc",
          "status": "Closed",
          "value": 45000,
          "goodsReceiveNo": "GRN-2024-001",
          "lines": [
            {
              "poLineId": "pol-001",
              "sku": "APPLE-1KG",
              "productName": "Apple (1 kg bag)",
              "orderedQty": 10,
              "receivedQty": 10,
              "unitCost": 45,
              "currency": "THB",
              "condition": "Resellable",
              "sloc": "A-12",
              "receivedAt": "2024-05-06T09:28:00Z",
              "putAwayAt": "2024-05-06T10:15:00Z"
            }
          ],
          "createdAt": "2024-05-06T08:00:00Z",
          "updatedAt": "2024-05-06T10:15:00Z"
        }

+ Response 404 (application/json)

        {
          "error": "not_found",
          "detail": "Purchase Order PO-999 does not exist."
        }

## Transfer Order Collection [/inbound/transfer-orders{?status,sourceStore,destStore,page,limit}]

### List Transfer Orders [GET]

Returns all Transfer Orders. Used by the Kanban Board Inbound tab (Transfer swimlane).

+ Parameters

    + status (optional, string, `InTransit,Received`) ... Comma-separated TO statuses. Allowed: `Created`, `PickConfirmed`, `InTransit`, `Received`, `Completed`.
    + sourceStore (optional, string, `Central DC`) ... Source store or DC.
    + destStore (optional, string, `Store A`) ... Destination store.
    + page (optional, number, `1`) ... Default `1`.
    + limit (optional, number, `50`) ... Max `200`. Default `50`.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "items": [
            {
              "id": "TR-001",
              "transferNumber": "TR-001",
              "source": "Central DC",
              "sourceStoreId": "store-central-dc",
              "dest": "Store B",
              "destStoreId": "store-b",
              "lines": 6,
              "status": "InTransit",
              "tracking": "TRK-TR-001",
              "createdAt": "2024-05-06T10:00:00Z",
              "updatedAt": "2024-05-06T11:00:00Z"
            },
            {
              "id": "TR-002",
              "transferNumber": "TR-002",
              "source": "Store A",
              "sourceStoreId": "store-a",
              "dest": "Store C",
              "destStoreId": "store-c",
              "lines": 3,
              "status": "Created",
              "tracking": null,
              "createdAt": "2024-05-06T13:00:00Z",
              "updatedAt": "2024-05-06T13:00:00Z"
            },
            {
              "id": "TR-003",
              "transferNumber": "TR-003",
              "source": "Central DC",
              "sourceStoreId": "store-central-dc",
              "dest": "Store A",
              "destStoreId": "store-a",
              "lines": 10,
              "status": "Completed",
              "tracking": "TRK-TR-003",
              "createdAt": "2024-05-05T09:00:00Z",
              "updatedAt": "2024-05-05T16:30:00Z"
            },
            {
              "id": "TR-004",
              "transferNumber": "TR-004",
              "source": "Store B",
              "sourceStoreId": "store-b",
              "dest": "Central DC",
              "destStoreId": "store-central-dc",
              "lines": 4,
              "status": "Received",
              "tracking": "TRK-TR-004",
              "createdAt": "2024-05-06T08:00:00Z",
              "updatedAt": "2024-05-06T14:30:00Z"
            }
          ],
          "total": 4,
          "page": 1,
          "limit": 50
        }

+ Response 401 (application/json)

        {
          "error": "unauthorized",
          "detail": "Bearer token missing or expired."
        }

## Transfer Order [/inbound/transfer-orders/{id}]

### Get Transfer Order [GET]

Returns full detail for a single Transfer Order including line items and quantities.

+ Parameters

    + id (required, string, `TR-001`) ... Transfer Order ID.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "id": "TR-001",
          "transferNumber": "TR-001",
          "source": "Central DC",
          "sourceStoreId": "store-central-dc",
          "dest": "Store B",
          "destStoreId": "store-b",
          "status": "InTransit",
          "tracking": "TRK-TR-001",
          "lines": [
            {
              "toLineId": "tol-001",
              "sku": "APPLE-1KG",
              "productName": "Apple (1 kg bag)",
              "requestedQty": 6,
              "transferredQty": 6,
              "confirmedAt": "2024-05-06T11:00:00Z"
            }
          ],
          "createdAt": "2024-05-06T10:00:00Z",
          "updatedAt": "2024-05-06T11:00:00Z"
        }

+ Response 404 (application/json)

        {
          "error": "not_found",
          "detail": "Transfer Order TR-999 does not exist."
        }

---

# Group Stock

WMS stock movement ledger — read-only view of inbound/outbound events per SKU.
OMS does not own inventory counts. This endpoint reflects events OMS recorded, not WMS stock levels.

## Stock Ledger [/stock/{sku}/ledger{?storeId,from,to}]

### Get Stock Ledger [GET]

Returns per-location stock movement events for a SKU.
Each location has its own `events` list and current `balance`.
Used by the Stock Flow view (Case 1 = single location, Case 2 = multi-location after transfer).

+ Parameters

    + sku (required, string, `APPLE-1KG`) ... Product SKU.
    + storeId (optional, string, `store-central-dc`) ... Filter to a single location. Omit to get all locations.
    + from (optional, string, `2024-05-06`) ... ISO 8601 date. Filters events `occurredAt >= from`.
    + to (optional, string, `2024-05-06`) ... ISO 8601 date. Filters events `occurredAt <= to`.

+ Request

    + Headers

            Authorization: Bearer eyJhbGciOiJSUzI1NiJ9...

+ Response 200 (application/json)

        {
          "sku": "APPLE-1KG",
          "skuName": "Apple (1 kg bag)",
          "unitPrice": 120,
          "currency": "THB",
          "locations": [
            {
              "storeId": "store-central-dc",
              "storeName": "Central DC",
              "balance": 0,
              "events": [
                {
                  "id": 1,
                  "time": "10:15",
                  "occurredAt": "2024-05-06T10:15:00Z",
                  "dir": "in",
                  "ref": "PO-001",
                  "refType": "PurchaseOrder",
                  "event": "PurchaseOrderPutAwayConfirmed",
                  "qtyChange": 10,
                  "balance": 10,
                  "detail": "Fresh Foods Ltd — 10 bags shelved at Sloc A-12. WMS stock now available."
                },
                {
                  "id": 2,
                  "time": "11:00",
                  "occurredAt": "2024-05-06T11:00:00Z",
                  "dir": "out",
                  "ref": "TR-001",
                  "refType": "TransferOrder",
                  "event": "TransferPickConfirmed",
                  "qtyChange": -4,
                  "balance": 6,
                  "detail": "4 bags picked & packed for transfer → Store A (TRK-TR-001)"
                },
                {
                  "id": 3,
                  "time": "15:31",
                  "occurredAt": "2024-05-06T15:31:00Z",
                  "dir": "out",
                  "ref": "ORD-A",
                  "refType": "Order",
                  "event": "PickConfirmed",
                  "qtyChange": -6,
                  "balance": 0,
                  "detail": "Alice Johnson picks 6 bags for delivery"
                }
              ]
            },
            {
              "storeId": "store-a",
              "storeName": "Store A",
              "balance": 1,
              "events": [
                {
                  "id": 1,
                  "time": "14:30",
                  "occurredAt": "2024-05-06T14:30:00Z",
                  "dir": "in",
                  "ref": "TR-001",
                  "refType": "TransferOrder",
                  "event": "TransferReceived",
                  "qtyChange": 4,
                  "balance": 4,
                  "detail": "4 bags received from Central DC via TRK-TR-001"
                },
                {
                  "id": 2,
                  "time": "16:00",
                  "occurredAt": "2024-05-06T16:00:00Z",
                  "dir": "out",
                  "ref": "ORD-C",
                  "refType": "Order",
                  "event": "PickConfirmed",
                  "qtyChange": -3,
                  "balance": 1,
                  "detail": "Charlie Wong picks 3 bags for delivery"
                }
              ]
            }
          ]
        }

+ Response 404 (application/json)

        {
          "error": "not_found",
          "detail": "SKU UNKNOWN-SKU has no stock ledger entries."
        }

+ Response 400 (application/json)

        {
          "error": "invalid_parameter",
          "detail": "'from' must be a valid ISO 8601 date (YYYY-MM-DD)."
        }

---

# Group Webhooks

Inbound callbacks from external systems (WMS, TMS, POS).
All webhook endpoints return `202 Accepted` immediately.
Processing is synchronous inside the handler; the response does NOT wait for outbox dispatch.
Each handler stages an `OrderWebhookLog` entry atomically with the domain state change.

**Shared webhook headers:**

| Header | Description |
|---|---|
| `X-Source-System` | Sending system: `WMS`, `TMS`, or `POS` |
| `X-Idempotency-Key` | UUID — duplicate requests with same key are ignored |
| `X-Webhook-Signature` | HMAC-SHA256 of request body using shared secret |

---

## WMS: Pick Confirmed [/webhooks/wms/pick-confirmed]

### Pick Confirmed [POST]

WMS reports actual picked quantities per line. Triggers POS recalculation if any line quantity differs.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: a1b2c3d4-e5f6-7890-abcd-ef1234567890
            X-Webhook-Signature: sha256=abc123...

    + Body

            {
              "orderId": "ORD-001",
              "lines": [
                {
                  "orderLineId": "line-001",
                  "sku": "APPLE-1KG",
                  "pickedQty": 5,
                  "substituted": false
                }
              ],
              "pickedAt": "2024-05-06T15:31:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "orderId": "ORD-001",
          "newStatus": "PickConfirmed"
        }

+ Response 409 (application/json)

        {
          "error": "invalid_transition",
          "detail": "Order ORD-001 is in status Cancelled. PickConfirmed is not allowed from this state."
        }

---

## WMS: Put Away Confirmed (Returns) [/webhooks/wms/put-away-confirmed]

### Put Away Confirmed [POST]

WMS confirms returned items are on shelf with condition assigned. Triggers atomic refund.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: b2c3d4e5-f6a7-8901-bcde-f12345678901
            X-Webhook-Signature: sha256=def456...

    + Body

            {
              "returnId": "RET-001",
              "items": [
                {
                  "sku": "APPLE-1KG",
                  "condition": "Resellable",
                  "sloc": "B-05"
                }
              ],
              "putAwayAt": "2024-05-06T11:00:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "returnId": "RET-001",
          "newReturnStatus": "PutAway",
          "refundInitiated": true,
          "creditNoteId": "CN-RET-001"
        }

---

## WMS: Goods Receipt Confirmed [/webhooks/wms/goods-receipt-confirmed]

### Goods Receipt Confirmed [POST]

WMS confirms physical goods received against a Purchase Order. Updates received quantities per line.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: c3d4e5f6-a7b8-9012-cdef-123456789012
            X-Webhook-Signature: sha256=ghi789...

    + Body

            {
              "purchaseOrderId": "PO-001",
              "goodsReceiveNo": "GRN-2024-001",
              "lines": [
                {
                  "sku": "APPLE-1KG",
                  "receivedQty": 10
                }
              ],
              "receivedAt": "2024-05-06T09:28:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "purchaseOrderId": "PO-001",
          "newStatus": "FullyReceived"
        }

---

## WMS: Purchase Order Put Away Confirmed [/webhooks/wms/purchase-order-put-away-confirmed]

### PO Put Away Confirmed [POST]

WMS confirms inbound goods are shelved. Closes the PO and signals stock availability.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: d4e5f6a7-b8c9-0123-defa-234567890123
            X-Webhook-Signature: sha256=jkl012...

    + Body

            {
              "purchaseOrderId": "PO-001",
              "items": [
                {
                  "sku": "APPLE-1KG",
                  "condition": "Resellable",
                  "sloc": "A-12",
                  "qty": 10
                }
              ],
              "putAwayAt": "2024-05-06T10:14:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "purchaseOrderId": "PO-001",
          "newStatus": "Closed"
        }

---

## WMS: Transfer Pick Confirmed [/webhooks/wms/transfer-pick-confirmed]

### Transfer Pick Confirmed [POST]

WMS at source store confirms items are picked and packed for transfer. Triggers TMS dispatch.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: e5f6a7b8-c9d0-1234-efab-345678901234
            X-Webhook-Signature: sha256=mno345...

    + Body

            {
              "transferOrderId": "TR-001",
              "lines": [
                {
                  "sku": "APPLE-1KG",
                  "transferredQty": 4
                }
              ],
              "confirmedAt": "2024-05-06T11:00:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "transferOrderId": "TR-001",
          "newStatus": "PickConfirmed"
        }

---

## WMS: Transfer Received [/webhooks/wms/transfer-received]

### Transfer Received [POST]

WMS at destination store confirms stock has arrived and been put away. Completes the Transfer Order.

+ Request (application/json)

    + Headers

            X-Source-System: WMS
            X-Idempotency-Key: f6a7b8c9-d0e1-2345-fabc-456789012345
            X-Webhook-Signature: sha256=pqr678...

    + Body

            {
              "transferOrderId": "TR-001",
              "receivedAt": "2024-05-06T14:30:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "transferOrderId": "TR-001",
          "newStatus": "Completed"
        }

---

## TMS: Package Dispatched [/webhooks/tms/package-dispatched]

### Package Dispatched [POST]

TMS driver has collected the package and is out for delivery. Updates order/package status.

+ Request (application/json)

    + Headers

            X-Source-System: TMS
            X-Idempotency-Key: a7b8c9d0-e1f2-3456-abcd-567890123456
            X-Webhook-Signature: sha256=stu901...

    + Body

            {
              "trackingId": "TRK-2024-001",
              "dispatchedAt": "2024-05-06T17:47:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "orderId": "ORD-001",
          "newOrderStatus": "OutForDelivery",
          "newPackageStatus": "OutForDelivery"
        }

---

## TMS: Package Delivered [/webhooks/tms/package-delivered]

### Package Delivered [POST]

TMS confirms delivery to customer. Triggers invoice generation and payment notification flow.

+ Request (application/json)

    + Headers

            X-Source-System: TMS
            X-Idempotency-Key: b8c9d0e1-f2a3-4567-bcde-678901234567
            X-Webhook-Signature: sha256=vwx234...

    + Body

            {
              "trackingId": "TRK-2024-001",
              "deliveredAt": "2024-05-06T19:22:00Z",
              "recipientName": "Alice Johnson",
              "proofOfDelivery": "https://tms.example.com/pod/TRK-2024-001.jpg"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "orderId": "ORD-001",
          "newStatus": "Delivered",
          "invoiceTriggered": true
        }

---

## POS: Recalculation Result [/webhooks/pos/recalculation-result]

### Recalculation Result [POST]

POS returns the final adjusted total after applying promotions to actual picked quantities.

+ Request (application/json)

    + Headers

            X-Source-System: POS
            X-Idempotency-Key: c9d0e1f2-a3b4-5678-cdef-789012345678
            X-Webhook-Signature: sha256=yz0123...

    + Body

            {
              "orderId": "ORD-001",
              "originalAmount": 2450,
              "adjustedAmount": 2380,
              "currency": "THB",
              "promotionsApplied": [
                {
                  "promoCode": "FRESH10",
                  "discountAmount": 70,
                  "description": "10% fresh produce discount"
                }
              ],
              "recalculatedAt": "2024-05-06T15:36:00Z"
            }

+ Response 202 (application/json)

        {
          "accepted": true,
          "orderId": "ORD-001",
          "finalAmount": 2380,
          "posRecalcPendingCleared": true
        }

---

# Group Error Reference

All error responses follow a consistent envelope:

```json
{
  "error": "<machine_readable_code>",
  "detail": "<human_readable_explanation>",
  "traceId": "<opentelemetry_trace_id>"
}
```

| HTTP Status | `error` code | When it occurs |
|---|---|---|
| `400` | `invalid_parameter` | Query param or body field fails validation |
| `400` | `invalid_transition` | Order state machine rejects the requested transition |
| `401` | `unauthorized` | Bearer token missing, expired, or invalid |
| `403` | `forbidden` | Token valid but lacks permission for this resource |
| `404` | `not_found` | Resource ID does not exist |
| `409` | `conflict` | Idempotency key already processed with a different body |
| `422` | `unprocessable` | Body is valid JSON but semantically incorrect (e.g. receivedQty > orderedQty) |
| `500` | `internal_error` | Unexpected server error — check `traceId` in Grafana |
| `503` | `service_unavailable` | Outbox worker is paused or DB is unhealthy |
