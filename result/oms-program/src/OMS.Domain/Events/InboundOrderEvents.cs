namespace OMS.Domain.Events;

public sealed class PurchaseOrderCreatedEvent(Guid purchaseOrderId, string poNumber) : DomainEvent
{
    public Guid PurchaseOrderId { get; } = purchaseOrderId;
    public string PoNumber { get; } = poNumber;
}

public sealed class GoodsReceiptConfirmedEvent(Guid purchaseOrderId, bool isFullyReceived) : DomainEvent
{
    public Guid PurchaseOrderId { get; } = purchaseOrderId;
    public bool IsFullyReceived { get; } = isFullyReceived;
}

public sealed class PurchaseOrderPutAwayConfirmedEvent(Guid purchaseOrderId) : DomainEvent
{
    public Guid PurchaseOrderId { get; } = purchaseOrderId;
}

public sealed class PurchaseOrderClosedEvent(Guid purchaseOrderId) : DomainEvent
{
    public Guid PurchaseOrderId { get; } = purchaseOrderId;
}

public sealed class TransferOrderCreatedEvent(Guid transferOrderId, string transferNumber) : DomainEvent
{
    public Guid TransferOrderId { get; } = transferOrderId;
    public string TransferNumber { get; } = transferNumber;
}

public sealed class TransferPickConfirmedEvent(Guid transferOrderId) : DomainEvent
{
    public Guid TransferOrderId { get; } = transferOrderId;
}

public sealed class TransferOrderInTransitEvent(Guid transferOrderId, string trackingId) : DomainEvent
{
    public Guid TransferOrderId { get; } = transferOrderId;
    public string TrackingId { get; } = trackingId;
}

public sealed class TransferReceivedEvent(Guid transferOrderId) : DomainEvent
{
    public Guid TransferOrderId { get; } = transferOrderId;
}

public sealed class TransferOrderCompletedEvent(Guid transferOrderId) : DomainEvent
{
    public Guid TransferOrderId { get; } = transferOrderId;
}

public sealed class DamagedGoodsReceivedEvent(Guid orderId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string TrackingId { get; } = trackingId;
}

public sealed class DamagedGoodsPutAwayConfirmedEvent(Guid orderId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string TrackingId { get; } = trackingId;
}
