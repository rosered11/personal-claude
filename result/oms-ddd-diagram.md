# OMS Domain-Driven Design Diagram

> Sprint Connect owns and runs the OMS. The bounded context below represents Sprint Connect's internal domain model.
> Updated to support multiple fulfillment types (Delivery, Click & Collect, Express), weight-based order lines, and multi-vehicle split delivery (Package entity — TMS reports per TrackingId).

---

## Bounded Context Map

```mermaid
graph TD
    subgraph SC["Sprint Connect Bounded Context (OMS)"]
        ORDER["Order\n(Aggregate Root)"]
        ORDER_LINE["OrderLine\n(Entity)"]
        SUB_LINE["SubstitutedOrderLine\n(Entity)"]
        PACKAGE["Package\n(Entity)"]
        DELIVERY_SLOT["DeliverySlot\n(Entity)"]
        ORDER_STATUS["OrderStatus\n(Value Object)"]
        PACKAGE_STATUS["PackageStatus\n(Value Object)"]
        VEHICLE_TYPE["VehicleType\n(Value Object)"]
        FULFILLMENT_TYPE["FulfillmentType\n(Value Object)"]
        PAYMENT_METHOD["PaymentMethod\n(Value Object)"]
        ORDER_CHANNEL["OrderChannel\n(Value Object)"]
        UOM["UnitOfMeasure\n(Value Object)"]
        MONEY["Money\n(Value Object)"]
        ADDRESS["Address\n(Value Object)"]
        ROLLOUT["RolloutPolicy\n(Domain Service)"]
        FULFILLMENT_ROUTER["FulfillmentRouter\n(Domain Service)"]
    end

    subgraph ACL["Anti-Corruption Layers (inside Sprint Connect)"]
        BATCH_ADAPTER["BatchFileAdapter\n(implements IBatchFilePort)"]
        TMS_ADAPTER["TMSAdapter\n(implements ITMSPort)"]
        POS_ADAPTER["POSAdapter\n(implements IPOSPort)"]
        WMS_ADAPTER["WMSAdapter\n(implements IWMSPort)"]
        GW_ADAPTER["GatewayAdapter\n(implements IGatewayPort)"]
    end

    subgraph EVENTS["Domain Events"]
        E1["OrderCreated"]
        E2["BookingConfirmed"]
        E3["PickStarted"]
        E4["PickConfirmed"]
        E5["InvoiceGenerated"]
        E6["OutForDelivery"]
        E7["Delivered"]
        E8["OrderCancelled"]
        E9["OrderRescheduled"]
        E10["PaymentNotified"]
        E11["CreditNoteIssued"]
        E12["ReadyForCollection"]
        E13["OrderCollected"]
        E14["PackagesAssigned"]
        E15["PackageOutForDelivery"]
        E16["PackageDelivered"]
        E17["OrderFullyDelivered"]
        E18["OrderLinesModified"]
        E19["PackagesReassigned"]
        E20["OrderPacked"]
        E21["OrderOnHold"]
        E22["OrderReleased"]
        E23["ReturnRequested"]
        E24["RefundProcessed"]
        E25["PackageLost"]
    end

    subgraph EXTERNAL["External Systems"]
        GW["Gateway"]
        WMS["WMS"]
        TMS["TMS"]
        POS["POS"]
        STS["STS"]
        BE["Backend"]
        FG["File Gateway"]
    end

    ORDER --> ORDER_LINE
    ORDER --> PACKAGE
    ORDER --> DELIVERY_SLOT
    ORDER --> ORDER_STATUS
    ORDER --> FULFILLMENT_TYPE
    ORDER --> ORDER_CHANNEL
    ORDER --> PAYMENT_METHOD
    ORDER_LINE --> MONEY
    ORDER_LINE --> UOM
    ORDER_LINE --> SUB_LINE
    PACKAGE --> PACKAGE_STATUS
    PACKAGE --> VEHICLE_TYPE
    ROLLOUT --> ORDER
    FULFILLMENT_ROUTER --> ORDER

    ORDER --> E1
    ORDER --> E2
    ORDER --> E3
    ORDER --> E4
    ORDER --> E5
    ORDER --> E6
    ORDER --> E7
    ORDER --> E8
    ORDER --> E9
    ORDER --> E10
    ORDER --> E11
    ORDER --> E12
    ORDER --> E13
    ORDER --> E18
    ORDER --> E19
    ORDER --> E20
    ORDER --> E21
    ORDER --> E22
    ORDER --> E23
    ORDER --> E24
    ORDER --> E25

    GW_ADAPTER -->|inbound: order requests| ORDER
    WMS_ADAPTER -->|inbound: Pick Confirmed, POS Recalculation, Invoice, AssignPackages| ORDER
    TMS_ADAPTER -->|inbound: PackageOutForDelivery, PackageDelivered, POS Recalculation| ORDER

    E3 --> WMS_ADAPTER
    E8 --> TMS_ADAPTER
    E9 --> TMS_ADAPTER
    E5 --> TMS_ADAPTER
    E11 --> TMS_ADAPTER
    E14 --> TMS_ADAPTER
    E15 --> GW_ADAPTER
    E17 --> WMS_ADAPTER
    E12 --> GW_ADAPTER

    GW_ADAPTER <--> GW
    WMS_ADAPTER <--> WMS
    TMS_ADAPTER <--> TMS
    POS_ADAPTER <--> POS
    BATCH_ADAPTER <--> FG
    BATCH_ADAPTER <--> STS
```

---

## Order Aggregate Detail

```mermaid
classDiagram
    class Order {
        +OrderId id
        +OrderStatus status
        +FulfillmentType fulfillmentType
        +OrderChannel channel
        +PaymentMethod paymentMethod
        +StoreId storeId
        +Address deliveryAddress
        +DeliverySlot slot
        +List~OrderLine~ lines
        +List~Package~ packages
        +DateTime createdAt
        +DateTime updatedAt
        +CreateOrder(cmd) Order$
        +ConfirmBooking(slotId)
        +StartPick()
        +ConfirmPick(pickedLines)
        +AssignPackages(packages)
        +GenerateInvoice()
        +MarkOutForDelivery()
        +MarkDelivered()
        +MarkPackageOutForDelivery(trackingId)
        +MarkPackageDelivered(trackingId)
        +IsFullyDelivered() bool
        +MarkReadyForCollection()
        +MarkCollected()
        +Cancel(reason)
        +Reschedule(newSlot)
        +NotifyPaid(paymentRef)
        +ModifyOrderLines(modifications)
        +ReassignPackages(packages)
        +MarkPacked()
        +HoldOrder(reason)
        +ReleaseOrder()
        +RequestReturn(reason)
        -Guard(requiredStatus, operation)
        -RaiseDomainEvent(event)
    }

    class OrderLineModification {
        +OrderLineId orderLineId
        +ModificationType type
        +decimal newAmount
        +ProductId newProductId
    }

    class ModificationType {
        <<enumeration>>
        Add
        Remove
        ChangeQuantity
    }

    class Package {
        +PackageId id
        +TrackingId trackingId
        +List~OrderLineId~ orderLineIds
        +VehicleType vehicleType
        +PackageStatus status
        +MarkOutForDelivery()
        +MarkDelivered()
    }

    class PackageStatus {
        <<enumeration>>
        Pending
        OutForDelivery
        Delivered
    }

    class VehicleType {
        <<enumeration>>
        StandardCar
        Van
        Truck
    }

    class OrderLine {
        +OrderLineId id
        +ProductId productId
        +string productName
        +decimal requestedAmount
        +decimal pickedAmount
        +UnitOfMeasure unitOfMeasure
        +Money unitPrice
        +Money totalPrice
        +SubstitutedOrderLine substitution
        +UpdatePickedAmount(amount)
        +CalculateTotal() Money
    }

    class SubstitutedOrderLine {
        +ProductId substituteProductId
        +string substituteProductName
        +Money substituteUnitPrice
        +decimal substitutedAmount
        +bool customerApproved
    }

    class DeliverySlot {
        +SlotId id
        +DateTime scheduledAt
        +DateTime scheduledEnd
        +StoreId storeId
        +Reschedule(newTime)
    }

    class OrderStatus {
        <<enumeration>>
        Pending
        BookingConfirmed
        PickStarted
        PickConfirmed
        Packed
        ReadyForCollection
        Collected
        OutForDelivery
        Delivering
        Delivered
        Invoiced
        Paid
        OnHold
        Cancelled
        Returned
    }

    class FulfillmentType {
        <<enumeration>>
        Delivery
        ClickAndCollect
        Express
    }

    class PaymentMethod {
        <<enumeration>>
        PrePaid
        PayOnDelivery
    }

    class OrderChannel {
        <<value object>>
        +ChannelType type
        +string sourceId
    }

    class ChannelType {
        <<enumeration>>
        Gateway
        B2B
        Kiosk
        Marketplace
        POSTerminal
        BulkImport
    }

    class UnitOfMeasure {
        <<enumeration>>
        Each
        Kilogram
        Gram
        Litre
    }

    class Money {
        <<value object>>
        +decimal Amount
        +string Currency
        +Add(other) Money
        +Subtract(other) Money
        +MultiplyBy(factor) Money
    }

    class RolloutPolicy {
        <<domain service>>
        +bool IsRolledOut(storeId)
        +IntegrationPath Resolve(storeId)
    }

    class FulfillmentRouter {
        <<domain service>>
        +bool RequiresBooking(fulfillmentType)
        +bool RequiresTMS(fulfillmentType)
        +OrderStatus InitialPickedStatus(fulfillmentType)
    }

    class IOrderRepository {
        <<interface>>
        +GetById(id) Order
        +Save(order)
    }

    Order ..> OrderLineModification : modified via
    Order "1" *-- "1..*" OrderLine : contains
    Order "1" *-- "0..*" Package : contains
    Order "1" *-- "0..1" DeliverySlot : has
    Order --> OrderStatus : current state
    Order --> FulfillmentType : fulfillment path
    Order --> PaymentMethod : payment type
    Order --> OrderChannel : order source
    Package --> PackageStatus : current state
    Package --> VehicleType : assigned vehicle
    OrderLine "1" *-- "0..1" SubstitutedOrderLine : may have
    OrderLine --> Money : unit and total price
    OrderLine --> UnitOfMeasure : sold by
    RolloutPolicy ..> Order : used by handlers
    FulfillmentRouter ..> Order : routes flow
    IOrderRepository ..> Order : persists
```

---

## Domain Events Flow (Outbox Pattern)

```mermaid
sequenceDiagram
    participant CMD as Command Handler
    participant AGG as Order Aggregate
    participant OUTBOX as order_events (Outbox)
    participant WORKER as Outbox Worker
    participant ADAPTER as ACL Adapter
    participant EXT as External System

    CMD->>AGG: e.g. ConfirmPick(pickedLines)
    AGG->>AGG: Guard(PickStarted)
    AGG->>AGG: Apply state change
    AGG->>OUTBOX: INSERT domain event (same transaction)
    OUTBOX-->>CMD: committed

    loop Outbox polling
        WORKER->>OUTBOX: SELECT unpublished events
        WORKER->>ADAPTER: Dispatch event
        ADAPTER->>EXT: HTTP / File / Batch call
        EXT-->>ADAPTER: ACK
        WORKER->>OUTBOX: Mark event as published
    end
```

---

## Aggregate State Machine

Branching by `FulfillmentType` and package count. Guards shown in `[brackets]`.

```mermaid
stateDiagram-v2
    [*] --> Pending : OrderCreated

    Pending --> BookingConfirmed : ConfirmBooking [Delivery]
    Pending --> PickStarted : StartPick [ClickAndCollect / Express]
    Pending --> Cancelled : Cancel
    Pending --> OnHold : HoldOrder

    BookingConfirmed --> PickStarted : StartPick
    BookingConfirmed --> Cancelled : Cancel
    BookingConfirmed --> BookingConfirmed : Reschedule (slot update only)
    BookingConfirmed --> OnHold : HoldOrder

    PickStarted --> PickConfirmed : ConfirmPick
    PickStarted --> Cancelled : Cancel
    PickStarted --> OnHold : HoldOrder

    PickConfirmed --> Packed : MarkPacked [AssignPackages complete]
    PickConfirmed --> Cancelled : Cancel

    Packed --> OutForDelivery : MarkPackageOutForDelivery [single package — TMS triggers per TrackingId]
    Packed --> Delivering : MarkPackageOutForDelivery [multi-package — first package dispatched]
    Packed --> ReadyForCollection : MarkReadyForCollection [ClickAndCollect]
    Packed --> OnHold : HoldOrder

    Delivering --> Delivering : MarkPackageOutForDelivery [remaining packages dispatched]
    Delivering --> Delivering : MarkPackageDelivered [partial — not all packages done]
    Delivering --> Delivered : OrderFullyDelivered [all packages delivered]

    OutForDelivery --> Delivered : MarkPackageDelivered [single package — TMS triggers per TrackingId]
    OutForDelivery --> OnHold : HoldOrder [e.g. compliance hold mid-delivery]

    ReadyForCollection --> Collected : MarkCollected [ClickAndCollect]

    Delivered --> Invoiced : GenerateInvoice
    Delivered --> Returned : RequestReturn [within return window]
    Collected --> Invoiced : GenerateInvoice
    Collected --> Returned : RequestReturn [within return window]
    Invoiced --> Paid : NotifyPaid
    Invoiced --> PaymentFailed : NotifyPaymentFailed
    Paid --> Returned : RequestReturn [post-payment return]

    OnHold --> Pending : ReleaseOrder [was Pending]
    OnHold --> BookingConfirmed : ReleaseOrder [was BookingConfirmed]
    OnHold --> PickStarted : ReleaseOrder [was PickStarted]
    OnHold --> Packed : ReleaseOrder [was Packed]

    Cancelled --> [*]
    Paid --> [*]
    Returned --> [*]
    PaymentFailed --> [*]
```

---

## Package State Machine (per Package entity)

```mermaid
stateDiagram-v2
    [*] --> Pending : AssignPackages

    Pending --> OutForDelivery : MarkOutForDelivery [TMS triggers per TrackingId]
    OutForDelivery --> Delivered : MarkDelivered [TMS triggers per TrackingId]

    Delivered --> [*]
```

---

## Weight-Based OrderLine Calculation

```mermaid
classDiagram
    class OrderLine {
        +decimal requestedAmount
        +decimal pickedAmount
        +UnitOfMeasure unitOfMeasure
        +Money unitPrice
        +CalculateTotal() Money
    }
    note for OrderLine "Each:  total = unitPrice × pickedAmount\nKilogram: total = unitPrice × pickedAmount\nGram: total = unitPrice × (pickedAmount / 1000)\nLitre: total = unitPrice × pickedAmount"
```
