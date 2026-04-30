# OMS Field Mapping — Old Schema → New ER Diagram

> Column-by-column mapping between `order-schema-old.sql` (SQL Server) and the new PostgreSQL design.
> Legend: ✅ Mapped | ➕ New field (no old equivalent) | ❌ Dropped | 🔀 Merged/Restructured | 📦 Moved to audit DB

---

## 1. Order Module

### `Order` → `orders`

| Old Field | Old Type | New Field | New Table | New Type | Note |
|---|---|---|---|---|---|
| `Id` | int IDENTITY | `order_id` | orders | uuid | ✅ PK type changed to UUID |
| `OrderNumber` | varchar(40) | `order_number` | orders | varchar UK | ✅ |
| `SourceOrderId` | varchar(50) | `source_order_id` | orders | varchar UK | ✅ |
| `Channel` | varchar(5) | `channel_type` | orders | varchar | ✅ Renamed; values expanded |
| `Bu` | varchar(10) | `business_unit` | orders | varchar | ✅ Renamed |
| `OrderDate` | datetime2 | `order_date` | orders | timestamptz | ✅ |
| `Status` | varchar(4) | `status` | orders | varchar | ✅ Readable enum (was 4-char code) |
| `StatusReason` | nvarchar(100) | `hold_reason` | orders | varchar | 🔀 Narrowed to hold context only |
| `StatusDate` | datetime2 | — | — | — | ❌ Dropped; use `updated_at` |
| `OrderFulfillmentType` | varchar(4) | `fulfillment_type` | orders | varchar | ✅ Renamed |
| `SubstitutionFlag` | varchar(1) | `substitution_flag` | orders | bool | ✅ Type changed to bool |
| `Agent` | varchar(30) | — | — | — | ❌ Dropped; not a domain concept |
| `FullTaxInvoice` | varchar(1) | — | — | — | ❌ Moved to invoice generation logic |
| `GiftWrapFlag` | varchar(1) | — | — | — | ❌ Not in new domain scope |
| `OrderSecretCode` | varchar(20) | — | — | — | ❌ Dropped |
| `ReceiptUrl` | varchar(300) | — | — | — | ❌ Moved to invoices table |
| `OrderType` | varchar(5) | — | — | — | ❌ Covered by `channel_type` + `fulfillment_type` |
| `IsActive` | bit | — | — | — | ❌ Soft-delete removed; use status |
| `IsDelete` | bit | — | — | — | ❌ Soft-delete removed |
| `CreatedBy` | varchar(40) | `created_by` | orders | varchar | ✅ |
| `CreatedDate` | datetime2 | `created_at` | orders | timestamptz | ✅ |
| `UpdatedBy` | varchar(40) | `updated_by` | orders | varchar | ✅ |
| `UpdatedDate` | datetime2 | `updated_at` | orders | timestamptz | ✅ |
| — | — | `store_id` | orders | varchar FK | ➕ New: explicit store reference |
| — | — | `payment_method` | orders | varchar | ➕ New: PrePaid / PayOnDelivery |
| — | — | `pre_hold_status` | orders | varchar | ➕ New: stores status before OnHold |

---

### `SubOrder` → ❌ Dropped (merged into `orders`)

The entire `SubOrder` table is removed. `SubOrder` duplicated `Order` fields with no distinct domain meaning. `FulfillmentType` on `orders` replaces the routing logic.

| Old Field | Disposition |
|---|---|
| `SubOrderNumber` | ❌ Dropped — single `order_number` is the identity |
| `SourceSubOrderid` | ❌ Dropped — no sub-order concept |
| All other fields | 🔀 Already present on `orders` |

---

### `OrderItem` → `order_lines`

| Old Field | Old Type | New Field | New Table | New Type | Note |
|---|---|---|---|---|---|
| `Id` | int IDENTITY | `order_line_id` | order_lines | uuid | ✅ UUID |
| `SourceOrderId` | varchar(50) | `order_id` | order_lines | uuid FK | 🔀 FK to orders.order_id |
| `SourceItemId` | varchar(50) | — | — | — | ❌ Dropped; SKU is the item identifier |
| `SourceItemNumber` | int | — | — | — | ❌ Dropped |
| `ItemLineNumber` | varchar(50) | — | — | — | ❌ Dropped; use order_line_id |
| `Sku` | varchar(30) | `sku` | order_lines | varchar | ✅ |
| `Barcode` | varchar(50) | `barcode` | order_lines | varchar | ✅ |
| `ProductNameTH` | nvarchar(255) | `product_name` | order_lines | nvarchar | 🔀 Single name field (locale not in domain) |
| `ProductNameEN` | nvarchar(255) | — | — | — | ❌ Multi-locale dropped |
| `ProductNameIT/DE/VN` | varchar(255) | — | — | — | ❌ Dropped |
| `Qty` | decimal(8,2) | `requested_amount` | order_lines | decimal | ✅ Renamed for clarity |
| `OperateQty` | decimal(8,2) | `picked_amount` | order_lines | decimal | ✅ Renamed |
| `QtyUnit` | nvarchar(30) | `unit_of_measure` | order_lines | varchar | ✅ Renamed |
| `Weight` | decimal(8,3) | — | — | — | ❌ Weight tracked via UoM on picked_amount |
| `PickWeight` | decimal(8,3) | — | — | — | ❌ Same |
| `WeightUnit` | nvarchar(30) | — | — | — | ❌ Same |
| `Status` | varchar(30) | `status` | order_lines | varchar | ✅ |
| `IsSubstitute` | varchar(10) | `is_substitute` | order_lines | bool | ✅ Type changed to bool |
| `AmountId` | int | — | — | payment.order_line_amounts | 🔀 Moved to Payment module |
| `FulFillmentId` | int | — | — | — | ❌ FK to `OrderItemFulFillment`; replaced by fulfillment_type on orders |
| `PromotionId` | int | — | — | payment.order_promotions | 🔀 Moved to Payment module |
| `TrackNo` | varchar(50) | — | — | orders.order_packages | 🔀 TrackingId moved to Package entity |
| `ThirdPartyLogistic` | varchar(100) | — | — | orders.order_packages | 🔀 Moved to Package entity |
| `Maxbarcode` | varchar(50) | — | — | — | ❌ Dropped |
| `CatalogInfo` | varchar(30) | — | — | — | ❌ Dropped |
| `PickBarcode` | varchar(50) | — | — | — | ❌ Dropped |
| `Cat1–Cat6` | varchar(15) | — | — | — | ❌ Dropped; category not an order domain concept |
| `Brand/BrandCode` | varchar | — | — | — | ❌ Dropped; catalog data |
| `Gender/Color/Size` | varchar | — | — | — | ❌ Dropped; catalog data |
| `Floor/Zone` | varchar | — | — | — | ❌ Dropped; WMS internal data |
| `ItemLocCode/Desc` | varchar | — | — | — | ❌ Dropped; WMS internal |
| `HazardousType` | varchar(2) | — | — | — | ❌ Dropped |
| `ImageUrl` | varchar(1000) | — | — | — | ❌ Dropped; catalog concern |
| `SerialNo` | varchar(100) | — | — | — | ❌ Dropped |
| `PostTicket/TextNo` | varchar | — | — | — | ❌ Dropped; POS-internal refs |
| `CreatedDate/UpdatedDate` | datetime2 | `created_at` / `updated_at` | order_lines | timestamptz | ✅ |
| — | — | `unit_price` | order_lines | decimal | ➕ New: price at order time |
| — | — | `currency` | order_lines | varchar | ➕ New |

---

### `OrderAddress` / `SubOrderAddress` → `order_addresses`

Both old tables are merged into one. `SubOrderAddress` is dropped since SubOrder is removed.

| Old Field | New Field | Note |
|---|---|---|
| `SourceOrderId` | `order_id` (FK uuid) | 🔀 FK type changed |
| `SourceAddrId` | — | ❌ Dropped |
| `AddressType` | `address_type` | ✅ |
| `FirstName` | `first_name` | ✅ |
| `LastName` | `last_name` | ✅ |
| `CompanyName` | `company_name` | ✅ |
| `Address1` | `address1` | ✅ |
| `Address2` | `address2` | ✅ |
| `Subdistrict` | `subdistrict` | ✅ |
| `District` | `district` | ✅ |
| `Province` | `province` | ✅ |
| `PostalCode` | `postal_code` | ✅ |
| `CountryCode` | `country_code` | ✅ |
| `Latitude` | `latitude` | ✅ |
| `Longtitude` | `longitude` | ✅ (typo fixed) |
| `MobilePhone` | `mobile_phone` | ✅ |
| `Email` | `email` | ✅ |
| `TaxId` | `tax_id` | ✅ |
| `Language` | — | ❌ Dropped |
| `Title` | — | ❌ Dropped |
| `MiddleName` | — | ❌ Dropped |
| `Name` / `FullName` | — | ❌ Dropped; derive from first+last |
| `AttentionName` | — | ❌ Dropped |
| `BranchNo` / `BranchName` | — | ❌ Dropped |
| `DeliveryRoute` | — | ❌ TMS concern |
| `HomePhone` | — | ❌ Dropped |
| `LineId` / `FbId` | — | ❌ Dropped; social IDs not domain |
| `Building/HouseNo/Moo/Road/Soi` | — | ❌ Dropped; folded into address1/address2 |
| `City` | — | ❌ Dropped; use province |

---

### `OrderCustomer` → `order_customers`

| Old Field | New Field | Note |
|---|---|---|
| `Id` | `order_customer_id` (uuid) | ✅ |
| `SourceOrderId` | `order_id` (uuid FK) | 🔀 |
| `SourceCusId` | `source_customer_id` | ✅ Renamed |
| `CusMemType` | `member_type` | ✅ Renamed |
| `CusMemId` | `member_id` | ✅ Renamed |
| `OrderModelId` | — | ❌ Dropped; FK via order_id instead |

---

### `PackageTb` / `PackageAuditInfoTb` → `order_packages` + `order_package_lines`

Old `PackageTb` stored one row **per SKU per box**. New design stores one row **per package entity** with `order_package_lines` as the join.

| Old Field | Old Table | New Field | New Table | Note |
|---|---|---|---|---|
| `Id` (uniqueidentifier) | PackageTb | `package_id` (uuid) | order_packages | ✅ |
| `TrackingNo` | PackageTb | `tracking_id` | order_packages | ✅ Renamed |
| `PackageId` | PackageTb | `carrier_package_id` | order_packages | ✅ Renamed |
| `ThirdPartyLogistic` | PackageTb | `third_party_logistic` | order_packages | ✅ |
| `SourceOrderId` | PackageTb | `order_id` (uuid FK) | order_packages | 🔀 |
| `PackageWeight` | PackageTb | `package_weight` | order_packages | ✅ |
| `DeliveryNoteNo` | PackageTb | `delivery_note_number` | order_packages | ✅ Restored; generated at Packed state, one per Package |
| `PdfUrl` | PackageTb | — | — | ❌ Dropped; document link not domain |
| `KeyIdFromReturn` | PackageTb | — | — | ❌ Dropped |
| `Sku` | PackageTb | `order_line_id` (FK) | order_package_lines | 🔀 Per-SKU row → per-package join table |
| `Qty` | PackageTb | — | order_package_lines | ❌ Qty is on order_lines |
| `Barcode` | PackageTb | — | — | ❌ Dropped; on order_lines |
| `SourceSubOrderId` | PackageTb | — | — | ❌ SubOrder removed |
| — | — | `vehicle_type` | order_packages | ➕ New |
| — | — | `status` | order_packages | ➕ New: Pending/OutForDelivery/Delivered |
| `PackageInfo.*` | PackageInfo | — | — | ❌ Entire table dropped; pallet/carton counts are WMS internal |

---

### `OrderItemDeliveryWindow` / `OrderItemFulFillment` → `delivery_slots`

Old `OrderItemFulFillment` was per item. New `delivery_slots` is per order.

| Old Field | Old Table | New Field | New Table | Note |
|---|---|---|---|---|
| `StartTime` | OrderItemDeliveryWindow | `scheduled_start` | delivery_slots | ✅ |
| `EndTime` | OrderItemDeliveryWindow | `scheduled_end` | delivery_slots | ✅ |
| `SourceBU` / `SourceLoc` | OrderItemFulFillment | `store_id` | delivery_slots | 🔀 Combined into store_id |
| `FulfillmentType` | OrderItemFulFillment | — | orders | 🔀 Moved to orders.fulfillment_type |
| `Carrier` | OrderItemFulFillment | — | order_packages | 🔀 Moved to package |
| `DeliveryType/SubType` | OrderItemFulFillment | — | — | ❌ Covered by fulfillment_type |
| `FulfillmentOption` | OrderItemFulFillment | — | — | ❌ Dropped |
| `ShippingAddrRef` | OrderItemFulFillment | — | — | ❌ Resolved via order_addresses |

---

### `OrderOutboxTb` → `order_outbox`

| Old Field | New Field | Note |
|---|---|---|
| `Id` (int) | `outbox_id` (uuid) | ✅ UUID |
| `EvenType` | `event_type` | ✅ Typo fixed |
| `OutboxStatus` | `status` | ✅ |
| `Payload` (varchar max) | `event_payload` (jsonb) | ✅ Typed JSON |
| `ProcessDate` | `next_retry_at` | 🔀 Repurposed for retry scheduling |
| `IsActive` / `IsDelete` | — | ❌ Soft-delete removed |
| — | `order_id` | ➕ New: explicit FK |
| — | `target_system` | ➕ New: WMS/TMS/POS per row |
| — | `retry_count` | ➕ New |
| — | `published_at` | ➕ New |

---

### New tables with no old equivalent

| New Table | Replaces |
|---|---|
| `order_holds` | `Order.StatusReason` (hold reason was just a string) |
| `order_line_substitutions` | `OrderItem.IsSubstitute` flag only — no substitute detail stored |

---

## 2. Payment Module

### `OrderPayment` → `order_payments`

| Old Field | New Field | Note |
|---|---|---|
| `Id` (int) | `payment_id` (uuid) | ✅ |
| `SourceOrderId` | `order_id` (reference) | 🔀 |
| `SourcePaymentId` | `source_payment_id` | ✅ |
| `Currency` | `currency` | ✅ |
| `PaymentDate` | `payment_date` | ✅ |
| `OrderModelId` | — | ❌ Replaced by order_id reference |
| — | `status` | ➕ New |

---

### `OrderPaymentTransaction` → `payment_transactions`

| Old Field | New Field | Note |
|---|---|---|
| `Id` | `transaction_id` (uuid) | ✅ |
| `PaymentMethod` | `payment_method` | ✅ |
| `PaymentMethodDesc` | — | ❌ Dropped; enum is self-describing |
| `PaymentSubMethod` | `payment_sub_method` | ✅ |
| `PaymentSubMethodDesc` | — | ❌ Dropped |
| `Amount` | `amount` | ✅ |
| `BillAddrRef` | `bill_address_ref` | ✅ |
| `SourceTransactionId` | `source_transaction_id` | ✅ |
| `SourcePaymentId` | — | ❌ FK now via payment_id |
| `OrderPaymentModelId` | `payment_id` (FK) | 🔀 |

---

### `OrderFeeModel` / `SubOrderFeeModel` → `order_fees`

Both old tables merged. Sub-order fees had identical structure.

| Old Field | New Field | Note |
|---|---|---|
| `SourceFeeId` | `source_fee_id` | ✅ |
| `SourceOrderId` | `order_id` | 🔀 |
| `FeeCode` | `fee_code` | ✅ |
| `FeeNameTH` | `fee_name` | 🔀 Single name field |
| `FeeNameEN` | — | ❌ Multi-locale dropped |
| `FeeType` | `fee_type` | ✅ |
| `Qty` | — | ❌ Dropped; fee is not quantity-based |
| `AmountId` | — | ❌ Amounts inline now |
| `SourceFeeNumber` | — | ❌ Dropped |
| — | `amount` / `currency` | ➕ Inline amount |

---

### `OrderItemAmout` → `order_line_amounts`

(Note: old table name has typo "Amout")

| Old Field | New Field | Note |
|---|---|---|
| `Id` | `amount_id` (uuid) | ✅ |
| `Currency` | `currency` | ✅ |
| `NormalGrossAmount` | `gross_amount` | 🔀 "Normal" prefix dropped |
| `NormalNetAmount` | `net_amount` | 🔀 |
| `NormalUnitGrossAmount` | `unit_gross_amount` | 🔀 |
| `NormalUnitNetAmount` | `unit_net_amount` | 🔀 |
| `PaidGrossAmount` | — | ❌ Dropped; "paid" amounts are in payment_transactions |
| `PaidNetAmount` | — | ❌ |
| `PaidUnitGrossAmount` | — | ❌ |
| `PaidUnitNetAmount` | — | ❌ |
| `RetailPriceGrossAmount` | — | ❌ Dropped; retail price is catalog data |
| `RetailPriceNetAmount` | — | ❌ |
| `NormalId/PaidId/RetailPriceId` | — | ❌ FK pattern replaced by direct amounts |

---

### `OrderItemTax` → `order_line_taxes`

| Old Field | New Field | Note |
|---|---|---|
| `Type` | `tax_type` | ✅ |
| `TypeDesc` | `tax_description` | ✅ |
| `Amount` | `amount` | ✅ |
| `Rate` | `rate` | ✅ |
| `OrderItemAmountDtlModelId` | `amount_id` (FK) | 🔀 |
| `OrderItemPaymentModelId` | — | ❌ Tax is on amount, not payment |

---

### `OrderPromotion` → `order_promotions`

| Old Field | New Field | Note |
|---|---|---|
| `SourcePromoId` | `source_promo_id` | ✅ |
| `SourceOrderId` | `order_id` | 🔀 |
| `PromoCode` | `promo_code` | ✅ |
| `PromoNameTH` | `promo_name` | 🔀 Single name |
| `PromoNameEN` | — | ❌ |
| `PromoType` | `promo_type` | ✅ |
| `AmountId` | — | ❌ Inline amounts now |
| `Qty` | — | ❌ Dropped |
| `SourcePromoNumber` | — | ❌ Dropped |
| — | `discount_amount` / `discount_percentage` | ➕ Inline discount |
| — | `order_line_id` | ➕ New: line-level promo link |

---

### New tables in Payment module with no old equivalent

| New Table | What it replaces |
|---|---|
| `invoices` | No dedicated invoice table in old schema — invoice number was a field on OrderReturn |
| `credit_notes` | `OrderReturn.CreditNote` was just a varchar field |

---

## 3. Returns Module

### `OrderReturn` → `returns`

| Old Field | New Field | Note |
|---|---|---|
| `Id` (int) | `return_id` (uuid) | ✅ |
| `SourceOrderId` | `order_id` | 🔀 |
| `ReturnOrderNumber` | `return_order_number` | ✅ |
| `Status` | `status` | ✅ Expanded: ReturnRequested→PutAway→Refunded |
| `InvoiceNo` | `invoice_id` | 🔀 |
| `CreditNote` | `credit_note_id` | 🔀 |
| `GoodsReceiveNo` | `goods_receive_no` | ✅ |
| `SoldTo` | — | ❌ Dropped |
| `SourceSubOrderid` | — | ❌ SubOrder removed |
| `OrderDate` | — | ❌ Redundant; get from order |
| `IsActive` / `IsDelete` | — | ❌ Soft-delete removed |
| — | `return_reason` | ➕ New |
| — | `requested_at` | ➕ New |
| — | `pickup_scheduled_at` | ➕ New |
| — | `picked_up_at` | ➕ New |
| — | `received_at` | ➕ New |
| — | `inspected_at` | ➕ New |
| — | `put_away_at` | ➕ New |
| — | `refunded_at` | ➕ New |

---

### `OrderReturnItem` → `return_items`

| Old Field | New Field | Note |
|---|---|---|
| `Id` | `return_item_id` (uuid) | ✅ |
| `SKU` | `sku` | ✅ |
| `ProductBarcode` | `barcode` | ✅ |
| `ProductName` | `product_name` | ✅ |
| `Quantity` (int) | `quantity` (decimal) | ✅ Type widened for weight |
| `UOM` | `unit_of_measure` | ✅ Renamed |
| `Price` | `unit_price` | ✅ Renamed |
| `ItemReason` | `item_reason` | ✅ |
| `Sloc` | `assigned_sloc` | ✅ Renamed |
| `Status` | — | ❌ Item status derived from return status |
| `PaymentMethod` | `payment_method` | ✅ |
| `Source` | — | ❌ Dropped |
| `Brand` | — | ❌ Catalog data |
| `ProductImg` | — | ❌ Catalog data |
| `SourceSubOrderId` | — | ❌ SubOrder removed |
| `SourceItemNumber` | — | ❌ Dropped |
| `OrderReturnModelId` | `return_id` (FK) | 🔀 |
| — | `order_line_id` | ➕ New: link to original line |
| — | `condition` | ➕ New: Resellable/Damaged/Dispose |
| — | `put_away_status` | ➕ New |
| — | `inspected_at` / `put_away_at` | ➕ New |

---

### New tables in Returns module with no old equivalent

| New Table | Purpose |
|---|---|
| `return_put_away_logs` | Audit trail for warehouse put-away process (no equivalent in old schema) |
| `return_refunds` | Structured refund record (old schema had no refund entity) |

---

## 4. Configuration Module

### `BuTbl` → `business_units`

| Old Field | New Field | Note |
|---|---|---|
| `Id` (int) | `bu_id` (uuid) | ✅ |
| `Bu` | `bu_code` | ✅ Renamed |
| `BuName` | `bu_name` | ✅ |
| `CompanyCode` | `company_code` | ✅ |
| `CompanyName` | `company_name` | ✅ |
| `IsActive` | `is_active` | ✅ |
| `IsDelete` | — | ❌ Soft-delete removed |

---

### `StoreLocation` → `store_locations`

| Old Field | New Field | Note |
|---|---|---|
| `Id` (int) | `store_id` (uuid) | ✅ |
| `BU` | `business_unit` | ✅ FK to business_units |
| `SourceBU` | `source_bu` | ✅ |
| `SourceLoc` | `source_loc` | ✅ |
| `StoreName` | `store_name` | ✅ |
| `IsActive` | `is_active` | ✅ |
| `Address1–Province` | Same fields | ✅ |
| `PostalCode` | `postal_code` | ✅ |
| `CountryCode` | `country_code` | ✅ |
| `Latitude` | `latitude` | ✅ |
| `Longtitude` | `longitude` | ✅ (typo fixed) |
| `MobilePhone` | `mobile_phone` | ✅ |
| `Email` | `email` | ✅ |
| `IsDelete` | — | ❌ Soft-delete removed |
| `BuildingID/BuildingName` | — | ❌ Dropped |
| `Title/FirstName/LastName` | — | ❌ Store contact; not domain |
| `AttentionName/CompanyName` | — | ❌ Dropped |
| `HomePhone/LineId/FbId` | — | ❌ Dropped |
| `ToStoreLoc` | — | ❌ Dropped |
| — | `is_rolled_out` | ➕ New |

---

### `AllowedStatusSetting` → ❌ Dropped

State machine was stored as config rows. Now enforced in Order aggregate code.

| Old Field | Disposition |
|---|---|
| `FromStatus` / `ToStatus` | ❌ Now in `Order.Apply()` state machine logic |
| `Channel` / `Bu` | ❌ State machine is uniform across channels |
| `Action` | ❌ Commands in application layer |
| `IsGenerateActivity` | ❌ Events emitted by domain automatically |

---

### `OrderProcessConditionTb` → `fulfillment_routing_rules`

| Old Field | New Field | Note |
|---|---|---|
| `Id` | `rule_id` (uuid) | ✅ |
| `Channel` | `channel_type` | ✅ |
| `FulfillmentType` | `fulfillment_type` | ✅ |
| `Bu` | `business_unit` | ✅ |
| `priority` | `priority` | ✅ |
| `IsActive` | `is_active` | ✅ |
| `ConditionCode` | — | ❌ Replaced by typed fields |
| `ConditionDescription` | — | ❌ |
| `ProcessFunction` | — | ❌ Replaced by FulfillmentRouter domain service |
| `DeliveryType/SubType` | — | ❌ Covered by fulfillment_type |
| `FulfillmentOption` | — | ❌ |
| `SourceBU/SourceLoc` | — | ❌ Routing is by type, not by location |
| `OrderFulfillmentType` | — | ❌ Duplicate of FulfillmentType |
| — | `requires_booking` | ➕ New |
| — | `requires_tms` | ➕ New |
| — | `initial_pick_status` | ➕ New |

---

## 5. Dropped Tables (No Mapping)

These tables are entirely removed from the new design:

| Old Table | Reason Dropped |
|---|---|
| `__EFMigrationsHistory` | Framework table; not domain |
| `BackgroudSeviceSetting` | Infrastructure config; not domain |
| `EmailMasterTb` / `EmailRecipientTb` | Replaced by `notification_templates` in config module |
| `ExternalLogTb` / `HttpLogs` / `InternalLogTb` | 📦 Moved to separate audit DB (Serilog) |
| `fm_log_incre` | 📦 Sync log; audit DB |
| `Functions` | ❌ Function registry pattern replaced by typed command handlers |
| `ItemOtherInfo` | ❌ SOH / item metadata is WMS/catalog concern |
| `MessageLogs` | 📦 Audit DB |
| `OrderItemBarcode` | ❌ Multiple barcodes per item not in new scope; single barcode on order_line |
| `OrderItemFulFillmentServiceType` | ❌ Service type not a domain concept |
| `OrderItemPromotion` / `OrderItemPromotionsTb` | 🔀 Consolidated into `order_promotions` |
| `OrderItemRemark` | ❌ Remarks not in new domain scope |
| `OrderFeeAmount` / `OrderFeeAmountDtl` | 🔀 Fee amounts now inline on `order_fees` |
| `OrderFeeTax` | 🔀 Merged into `order_line_taxes` |
| `OrderFeePayment` | 🔀 Fee payment linked via `order_payments` |
| `OrderPromotionAmount` | 🔀 Discount inline on `order_promotions` |
| `OrderReference` | ❌ Source ID references handled by UUID FKs |
| `OrderRemark` | ❌ Not in new domain scope |
| `OrderSagaTb` | ❌ Replaced by Outbox pattern |
| `OrderStaging` | ❌ Replaced by Outbox + FulfillmentRouter |
| `PackageAuditInfoTb` | 📦 Audit DB |
| `PackageInfo` | ❌ Pallet/carton counts are WMS internal |
| `ProcessOrder` / `ProcessOrderItem` | ❌ Process tracking replaced by state machine on Order aggregate |
| `PromotionItemConditionTb` / `PromotionItemTb` | ❌ Promotion engine is an external concern (not OMS domain) |
| `StateProcessMonitor` | ❌ Replaced by OpenTelemetry / Outbox worker metrics |
| `SubOrderAddress` | 🔀 Merged into `order_addresses` |
| `SubOrderFeeAmount/Dtl/Model/Payment/Tax` | 🔀 All Sub-order fee tables merged into order-level equivalents |
| `OrderItemAmoutDetail` | 🔀 Merged into `order_line_amounts` |
