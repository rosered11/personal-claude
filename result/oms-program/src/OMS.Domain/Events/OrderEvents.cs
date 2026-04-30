using OMS.Domain.Enums;

namespace OMS.Domain.Events;

public sealed class OrderCreatedEvent(Guid orderId, string orderNumber, FulfillmentType fulfillmentType, ChannelType channelType) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string OrderNumber { get; } = orderNumber;
    public FulfillmentType FulfillmentType { get; } = fulfillmentType;
    public ChannelType ChannelType { get; } = channelType;
}

public sealed class BookingConfirmedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class PickStartedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class PickConfirmedEvent(Guid orderId, bool hasPartialPick) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public bool HasPartialPick { get; } = hasPartialPick;
}

public sealed class OrderPackedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class PackagesAssignedEvent(Guid orderId, IReadOnlyList<Guid> packageIds) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public IReadOnlyList<Guid> PackageIds { get; } = packageIds;
}

public sealed class PackagesReassignedEvent(Guid orderId, IReadOnlyList<Guid> packageIds) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public IReadOnlyList<Guid> PackageIds { get; } = packageIds;
}

public sealed class PackageOutForDeliveryEvent(Guid orderId, Guid packageId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid PackageId { get; } = packageId;
    public string TrackingId { get; } = trackingId;
}

public sealed class PackageDeliveredEvent(Guid orderId, Guid packageId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid PackageId { get; } = packageId;
    public string TrackingId { get; } = trackingId;
}

public sealed class OrderFullyDeliveredEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class OutForDeliveryEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class DeliveredEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class InvoiceGeneratedEvent(Guid orderId, string invoiceNumber) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string InvoiceNumber { get; } = invoiceNumber;
}

public sealed class PaymentNotifiedEvent(Guid orderId, PaymentMethod paymentMethod) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public PaymentMethod PaymentMethod { get; } = paymentMethod;
}

public sealed class OrderCancelledEvent(Guid orderId, string reason) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string Reason { get; } = reason;
}

public sealed class OrderRescheduledEvent(Guid orderId, DateTimeOffset newStart, DateTimeOffset newEnd) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public DateTimeOffset NewStart { get; } = newStart;
    public DateTimeOffset NewEnd { get; } = newEnd;
}

public sealed class OrderOnHoldEvent(Guid orderId, string holdReason, OrderStatus preHoldStatus) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public string HoldReason { get; } = holdReason;
    public OrderStatus PreHoldStatus { get; } = preHoldStatus;
}

public sealed class OrderReleasedEvent(Guid orderId, OrderStatus restoredStatus) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public OrderStatus RestoredStatus { get; } = restoredStatus;
}

public sealed class OrderLinesModifiedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class ReadyForCollectionEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class OrderCollectedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class PackageLostEvent(Guid orderId, Guid packageId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid PackageId { get; } = packageId;
    public string TrackingId { get; } = trackingId;
}

public sealed class PackageDamagedEvent(Guid orderId, Guid packageId, string trackingId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid PackageId { get; } = packageId;
    public string TrackingId { get; } = trackingId;
}

public sealed class ReturnRequestedEvent(Guid orderId, Guid returnId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid ReturnId { get; } = returnId;
}

public sealed class ReturnReceivedAtWarehouseEvent(Guid returnId) : DomainEvent
{
    public Guid ReturnId { get; } = returnId;
}

public sealed class PutAwayConfirmedEvent(Guid returnId) : DomainEvent
{
    public Guid ReturnId { get; } = returnId;
}

public sealed class RefundProcessedEvent(Guid returnId, Guid orderId) : DomainEvent
{
    public Guid ReturnId { get; } = returnId;
    public Guid OrderId { get; } = orderId;
}

public sealed class OrderReturnedEvent(Guid orderId, Guid returnId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid ReturnId { get; } = returnId;
}

public sealed class PosRecalculationAppliedEvent(Guid orderId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
}

public sealed class SubstitutionProposedEvent(Guid orderId, Guid substitutionId, string substituteSku, bool autoApproved) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid SubstitutionId { get; } = substitutionId;
    public string SubstituteSku { get; } = substituteSku;
    public bool AutoApproved { get; } = autoApproved;
}

public sealed class SubstitutionApprovedEvent(Guid orderId, Guid substitutionId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid SubstitutionId { get; } = substitutionId;
}

public sealed class SubstitutionRejectedEvent(Guid orderId, Guid substitutionId) : DomainEvent
{
    public Guid OrderId { get; } = orderId;
    public Guid SubstitutionId { get; } = substitutionId;
}
