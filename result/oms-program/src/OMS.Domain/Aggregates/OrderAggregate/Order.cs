using OMS.Domain.Enums;
using OMS.Domain.Events;
using OMS.Domain.Exceptions;
using OMS.Domain.ValueObjects;

namespace OMS.Domain.Aggregates.OrderAggregate;

public class Order : AggregateRoot
{
    public Guid OrderId { get; private set; }
    public string OrderNumber { get; private set; } = null!;
    public string? SourceOrderId { get; private set; }
    public ChannelType ChannelType { get; private set; }
    public string BusinessUnit { get; private set; } = null!;
    public Guid StoreId { get; private set; }
    public DateTimeOffset OrderDate { get; private set; }
    public OrderStatus Status { get; private set; }
    public OrderStatus? PreHoldStatus { get; private set; }
    public string? HoldReason { get; private set; }
    public FulfillmentType FulfillmentType { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public bool SubstitutionFlag { get; private set; }
    public bool PosRecalcPending { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public string UpdatedBy { get; private set; } = null!;

    private readonly List<OrderLine> _orderLines = new();
    public IReadOnlyList<OrderLine> OrderLines => _orderLines.AsReadOnly();

    private readonly List<OrderPackage> _packages = new();
    public IReadOnlyList<OrderPackage> Packages => _packages.AsReadOnly();

    private readonly List<OrderHold> _holds = new();
    public IReadOnlyList<OrderHold> Holds => _holds.AsReadOnly();

    private readonly List<OrderStatusHistory> _statusHistory = new();
    public IReadOnlyList<OrderStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    private readonly List<OrderLineSubstitution> _substitutions = new();
    public IReadOnlyList<OrderLineSubstitution> Substitutions => _substitutions.AsReadOnly();

    public bool HasPendingSubstitutions() => _substitutions.Any(s => s.CustomerApproved == null);

    private DeliverySlot? _deliverySlot;
    public DeliverySlot? DeliverySlot => _deliverySlot;

    private Order() { }

    private void Transition(OrderStatus to, string changedBy, string? detail = null)
    {
        _statusHistory.Add(OrderStatusHistory.Record(OrderId, Status, to, changedBy, detail));
        Status = to;
        UpdatedBy = changedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static Order Create(
        string orderNumber,
        string? sourceOrderId,
        ChannelType channelType,
        string businessUnit,
        Guid storeId,
        FulfillmentType fulfillmentType,
        PaymentMethod paymentMethod,
        bool substitutionFlag,
        string createdBy,
        IEnumerable<(string Sku, string ProductName, string Barcode, decimal RequestedAmount, UnitOfMeasure UnitOfMeasure, decimal UnitPrice, string Currency, bool IsSubstitute)> lines)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new OrderDomainException("Order number cannot be empty.");
        if (string.IsNullOrWhiteSpace(businessUnit))
            throw new OrderDomainException("Business unit cannot be empty.");

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            OrderNumber = orderNumber,
            SourceOrderId = sourceOrderId,
            ChannelType = channelType,
            BusinessUnit = businessUnit,
            StoreId = storeId,
            OrderDate = DateTimeOffset.UtcNow,
            Status = OrderStatus.Pending,
            FulfillmentType = fulfillmentType,
            PaymentMethod = paymentMethod,
            SubstitutionFlag = substitutionFlag,
            PosRecalcPending = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };

        foreach (var line in lines)
        {
            order._orderLines.Add(OrderLine.Create(
                order.OrderId,
                line.Sku,
                line.ProductName,
                line.Barcode,
                line.RequestedAmount,
                line.UnitOfMeasure,
                line.UnitPrice,
                line.Currency,
                line.IsSubstitute));
        }

        if (!order._orderLines.Any())
            throw new OrderDomainException("An order must have at least one order line.");

        order._statusHistory.Add(OrderStatusHistory.Record(order.OrderId, null, OrderStatus.Pending, createdBy, $"channel={channelType} fulfillment={fulfillmentType}"));
        order.RaiseDomainEvent(new OrderCreatedEvent(order.OrderId, order.OrderNumber, order.FulfillmentType, order.ChannelType));
        return order;
    }

    public void ConfirmBooking(string updatedBy)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidStateTransitionException(Status, nameof(ConfirmBooking));
        if (FulfillmentType != FulfillmentType.Delivery)
            throw new OrderDomainException("Booking confirmation is only applicable for Delivery orders.");

        Transition(OrderStatus.BookingConfirmed, updatedBy);
        RaiseDomainEvent(new BookingConfirmedEvent(OrderId));
    }

    public void StartPick(string updatedBy)
    {
        var allowedStatuses = FulfillmentType switch
        {
            FulfillmentType.Delivery => new[] { OrderStatus.BookingConfirmed },
            FulfillmentType.ClickAndCollect => new[] { OrderStatus.Pending },
            FulfillmentType.Express => new[] { OrderStatus.Pending },
            _ => Array.Empty<OrderStatus>()
        };

        if (!allowedStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(StartPick));

        Transition(OrderStatus.PickStarted, updatedBy);
        RaiseDomainEvent(new PickStartedEvent(OrderId));
    }

    public void ConfirmPick(
        IEnumerable<(Guid OrderLineId, decimal PickedAmount)> pickedLines,
        string updatedBy)
    {
        if (Status != OrderStatus.PickStarted)
            throw new InvalidStateTransitionException(Status, nameof(ConfirmPick));

        var pickedList = pickedLines.ToList();
        bool hasPartialPick = false;

        foreach (var (lineId, pickedAmount) in pickedList)
        {
            var line = _orderLines.FirstOrDefault(l => l.OrderLineId == lineId)
                ?? throw new OrderDomainException($"Order line '{lineId}' not found.");

            line.ApplyPickedAmount(pickedAmount);

            if (pickedAmount < line.RequestedAmount)
                hasPartialPick = true;
        }

        if (hasPartialPick)
            PosRecalcPending = true;

        Transition(OrderStatus.PickConfirmed, updatedBy, hasPartialPick ? "partial pick — POS recalc pending" : null);
        RaiseDomainEvent(new PickConfirmedEvent(OrderId, hasPartialPick));
    }

    public void AssignPackages(
        IEnumerable<(string TrackingId, VehicleType VehicleType, decimal PackageWeight, IEnumerable<Guid> OrderLineIds, string? CarrierPackageId, string? ThirdPartyLogistic, string? DeliveryNoteNumber)> packageGroups,
        string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(AssignPackages));

        _packages.Clear();

        foreach (var group in packageGroups)
        {
            var pkg = OrderPackage.Create(
                OrderId,
                group.TrackingId,
                group.VehicleType,
                group.PackageWeight,
                group.OrderLineIds,
                group.CarrierPackageId,
                group.ThirdPartyLogistic,
                group.DeliveryNoteNumber);

            _packages.Add(pkg);
        }

        if (!_packages.Any())
            throw new OrderDomainException("At least one package must be assigned.");

        PosRecalcPending = false;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PackagesAssignedEvent(OrderId, _packages.Select(p => p.PackageId).ToList()));
    }

    public void MarkPacked(string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(MarkPacked));

        if (PosRecalcPending)
            throw new OrderDomainException("Cannot mark order as packed while POS recalculation is pending.");

        Transition(OrderStatus.Packed, updatedBy);
        RaiseDomainEvent(new OrderPackedEvent(OrderId));
    }

    public void ReassignPackages(
        IEnumerable<(string TrackingId, VehicleType VehicleType, decimal PackageWeight, IEnumerable<Guid> OrderLineIds, string? CarrierPackageId, string? ThirdPartyLogistic, string? DeliveryNoteNumber)> packageGroups,
        string updatedBy)
    {
        if (_packages.Any(p => p.Status != PackageStatus.Pending))
            throw new OrderDomainException("Cannot reassign packages because one or more packages are not in Pending status.");

        _packages.Clear();

        foreach (var group in packageGroups)
        {
            var pkg = OrderPackage.Create(
                OrderId,
                group.TrackingId,
                group.VehicleType,
                group.PackageWeight,
                group.OrderLineIds,
                group.CarrierPackageId,
                group.ThirdPartyLogistic,
                group.DeliveryNoteNumber);

            _packages.Add(pkg);
        }

        if (!_packages.Any())
            throw new OrderDomainException("At least one package must be assigned.");

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PackagesReassignedEvent(OrderId, _packages.Select(p => p.PackageId).ToList()));
    }

    public void ModifyOrderLines(
        IEnumerable<(string Sku, string ProductName, string Barcode, decimal RequestedAmount, UnitOfMeasure UnitOfMeasure, decimal UnitPrice, string Currency)> addLines,
        IEnumerable<Guid> removeLineIds,
        IEnumerable<(Guid OrderLineId, decimal NewQuantity)> changeQuantities,
        string updatedBy)
    {
        var modifiableStatuses = new[] { OrderStatus.Pending, OrderStatus.BookingConfirmed };
        if (!modifiableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(ModifyOrderLines));

        foreach (var id in removeLineIds)
        {
            var line = _orderLines.FirstOrDefault(l => l.OrderLineId == id)
                ?? throw new OrderDomainException($"Order line '{id}' not found for removal.");
            line.Cancel();
        }

        foreach (var (lineId, newQty) in changeQuantities)
        {
            var line = _orderLines.FirstOrDefault(l => l.OrderLineId == lineId)
                ?? throw new OrderDomainException($"Order line '{lineId}' not found for quantity change.");
            line.UpdateQuantity(newQty);
        }

        foreach (var add in addLines)
        {
            _orderLines.Add(OrderLine.Create(
                OrderId,
                add.Sku,
                add.ProductName,
                add.Barcode,
                add.RequestedAmount,
                add.UnitOfMeasure,
                add.UnitPrice,
                add.Currency));
        }

        if (_orderLines.All(l => l.Status == OrderLineStatus.Cancelled))
            throw new OrderDomainException("An order must retain at least one active order line.");

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new OrderLinesModifiedEvent(OrderId));
    }

    public void HoldOrder(string holdReason, string heldBy)
    {
        var nonHoldableStatuses = new[]
        {
            OrderStatus.Delivered, OrderStatus.Invoiced, OrderStatus.Paid,
            OrderStatus.Cancelled, OrderStatus.OnHold, OrderStatus.Returned
        };

        if (nonHoldableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(HoldOrder));

        PreHoldStatus = Status;
        HoldReason = holdReason;

        var hold = OrderHold.Create(OrderId, holdReason, heldBy);
        _holds.Add(hold);

        Transition(OrderStatus.OnHold, heldBy, $"reason={holdReason}");
        RaiseDomainEvent(new OrderOnHoldEvent(OrderId, holdReason, PreHoldStatus.Value));
    }

    public void ReleaseOrder(string releasedBy)
    {
        if (Status != OrderStatus.OnHold)
            throw new InvalidStateTransitionException(Status, nameof(ReleaseOrder));

        if (PreHoldStatus is null)
            throw new OrderDomainException("Pre-hold status is not set; cannot release order.");

        var activeHold = _holds.LastOrDefault(h => h.ReleasedAt is null);
        activeHold?.Release(releasedBy);

        var restoredStatus = PreHoldStatus.Value;
        PreHoldStatus = null;
        HoldReason = null;

        Transition(restoredStatus, releasedBy, $"released from OnHold → {restoredStatus}");
        RaiseDomainEvent(new OrderReleasedEvent(OrderId, restoredStatus));
    }

    public void CancelOrder(string reason, string updatedBy)
    {
        var nonCancellableStatuses = new[]
        {
            OrderStatus.Delivered, OrderStatus.Invoiced, OrderStatus.Paid,
            OrderStatus.Cancelled, OrderStatus.Returned
        };

        if (nonCancellableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(CancelOrder));

        foreach (var line in _orderLines)
            line.Cancel();

        Transition(OrderStatus.Cancelled, updatedBy, reason);
        RaiseDomainEvent(new OrderCancelledEvent(OrderId, reason));
    }

    public void RescheduleDelivery(DateTimeOffset newStart, DateTimeOffset newEnd, string updatedBy)
    {
        if (FulfillmentType != FulfillmentType.Delivery)
            throw new OrderDomainException("Only Delivery orders can be rescheduled.");

        var nonReschedulableStatuses = new[]
        {
            OrderStatus.Delivered, OrderStatus.Invoiced, OrderStatus.Paid,
            OrderStatus.Cancelled, OrderStatus.Returned
        };

        if (nonReschedulableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(RescheduleDelivery));

        if (newEnd <= newStart)
            throw new OrderDomainException("Delivery slot end time must be after start time.");

        _deliverySlot = new DeliverySlot(
            _deliverySlot?.SlotId ?? Guid.NewGuid(),
            StoreId,
            newStart,
            newEnd);

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new OrderRescheduledEvent(OrderId, newStart, newEnd));
    }

    public void MarkPackageOutForDelivery(string trackingId, string updatedBy)
    {
        if (Status != OrderStatus.Packed && Status != OrderStatus.Delivering)
            throw new InvalidStateTransitionException(Status, nameof(MarkPackageOutForDelivery));

        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new OrderDomainException($"Package with tracking ID '{trackingId}' not found.");

        pkg.MarkOutForDelivery();

        var allOutForDeliveryOrDelivered = _packages.All(p =>
            p.Status == PackageStatus.OutForDelivery || p.Status == PackageStatus.Delivered);

        var nextStatus = (allOutForDeliveryOrDelivered && _packages.Any(p => p.Status == PackageStatus.Delivered))
            ? OrderStatus.Delivering
            : allOutForDeliveryOrDelivered
                ? OrderStatus.OutForDelivery
                : OrderStatus.Delivering;

        Transition(nextStatus, updatedBy, $"trackingId={trackingId}");
        RaiseDomainEvent(new PackageOutForDeliveryEvent(OrderId, pkg.PackageId, trackingId));
    }

    public void MarkPackageDelivered(string trackingId, string updatedBy)
    {
        if (Status != OrderStatus.OutForDelivery && Status != OrderStatus.Delivering)
            throw new InvalidStateTransitionException(Status, nameof(MarkPackageDelivered));

        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new OrderDomainException($"Package with tracking ID '{trackingId}' not found.");

        pkg.MarkDelivered();

        RaiseDomainEvent(new PackageDeliveredEvent(OrderId, pkg.PackageId, trackingId));

        if (_packages.All(p => p.Status == PackageStatus.Delivered))
        {
            Transition(OrderStatus.Delivered, updatedBy, $"all packages delivered — last={trackingId}");
            RaiseDomainEvent(new OrderFullyDeliveredEvent(OrderId));
            RaiseDomainEvent(new DeliveredEvent(OrderId));
        }
        else
        {
            Transition(OrderStatus.Delivering, updatedBy, $"trackingId={trackingId} delivered, others pending");
        }
    }

    public void MarkPackageLost(string trackingId, string updatedBy)
    {
        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new OrderDomainException($"Package with tracking ID '{trackingId}' not found.");

        RaiseDomainEvent(new PackageLostEvent(OrderId, pkg.PackageId, trackingId));

        HoldOrder($"Package lost: {trackingId}", updatedBy);
    }

    public void MarkPackageDamaged(string trackingId, string updatedBy)
    {
        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new OrderDomainException($"Package with tracking ID '{trackingId}' not found.");

        RaiseDomainEvent(new PackageDamagedEvent(OrderId, pkg.PackageId, trackingId));

        HoldOrder($"Package damaged: {trackingId}", updatedBy);
    }

    public void MarkReadyForCollection(string updatedBy)
    {
        if (Status != OrderStatus.Packed)
            throw new InvalidStateTransitionException(Status, nameof(MarkReadyForCollection));
        if (FulfillmentType != FulfillmentType.ClickAndCollect)
            throw new OrderDomainException("Only Click & Collect orders can be marked ready for collection.");

        Transition(OrderStatus.ReadyForCollection, updatedBy);
        RaiseDomainEvent(new ReadyForCollectionEvent(OrderId));
    }

    public void MarkCollected(string updatedBy)
    {
        if (Status != OrderStatus.ReadyForCollection)
            throw new InvalidStateTransitionException(Status, nameof(MarkCollected));

        Transition(OrderStatus.Collected, updatedBy);
        RaiseDomainEvent(new OrderCollectedEvent(OrderId));
    }

    public void GenerateInvoice(string invoiceNumber, string updatedBy)
    {
        var invoicableStatuses = new[] { OrderStatus.Delivered, OrderStatus.Collected };
        if (!invoicableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(GenerateInvoice));

        Transition(OrderStatus.Invoiced, updatedBy, $"invoiceNumber={invoiceNumber}");
        RaiseDomainEvent(new InvoiceGeneratedEvent(OrderId, invoiceNumber));
    }

    public void NotifyPayment(string updatedBy)
    {
        if (Status != OrderStatus.Invoiced)
            throw new InvalidStateTransitionException(Status, nameof(NotifyPayment));

        Transition(OrderStatus.Paid, updatedBy);
        RaiseDomainEvent(new PaymentNotifiedEvent(OrderId, PaymentMethod));
    }

    public void ApplyPosRecalculation(string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(ApplyPosRecalculation));

        PosRecalcPending = false;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PosRecalculationAppliedEvent(OrderId));
    }

    public void RecordSubstitution(
        Guid originalOrderLineId,
        string substituteSku,
        string substituteProductName,
        string substituteBarcode,
        UnitOfMeasure substituteUnitOfMeasure,
        decimal substituteUnitPrice,
        decimal substitutedAmount,
        string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(RecordSubstitution));

        var originalLine = _orderLines.FirstOrDefault(l => l.OrderLineId == originalOrderLineId)
            ?? throw new OrderDomainException($"Order line '{originalOrderLineId}' not found.");

        var substituteLine = OrderLine.Create(
            OrderId,
            substituteSku,
            substituteProductName,
            substituteBarcode,
            substitutedAmount,
            substituteUnitOfMeasure,
            substituteUnitPrice,
            originalLine.Currency,
            isSubstitute: true);
        substituteLine.ApplyPickedAmount(substitutedAmount);
        _orderLines.Add(substituteLine);

        var substitution = OrderLineSubstitution.Create(
            originalOrderLineId,
            substituteLine.OrderLineId,
            substituteSku,
            substituteProductName,
            substituteBarcode,
            substituteUnitPrice,
            substitutedAmount,
            autoApprove: SubstitutionFlag);

        _substitutions.Add(substitution);

        PosRecalcPending = true;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new SubstitutionProposedEvent(OrderId, substitution.SubstitutionId, substituteSku, SubstitutionFlag));
    }

    public void ApproveSubstitution(Guid substitutionId, string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(ApproveSubstitution));

        var substitution = _substitutions.FirstOrDefault(s => s.SubstitutionId == substitutionId)
            ?? throw new OrderDomainException($"Substitution '{substitutionId}' not found.");

        substitution.Approve();
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new SubstitutionApprovedEvent(OrderId, substitutionId));
    }

    public void RejectSubstitution(Guid substitutionId, string updatedBy)
    {
        if (Status != OrderStatus.PickConfirmed)
            throw new InvalidStateTransitionException(Status, nameof(RejectSubstitution));

        var substitution = _substitutions.FirstOrDefault(s => s.SubstitutionId == substitutionId)
            ?? throw new OrderDomainException($"Substitution '{substitutionId}' not found.");

        substitution.Reject();

        var substituteLine = _orderLines.FirstOrDefault(l => l.OrderLineId == substitution.SubstituteOrderLineId);
        substituteLine?.Cancel();

        PosRecalcPending = true;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new SubstitutionRejectedEvent(OrderId, substitutionId));
    }

    public void SetDeliverySlot(Guid storeId, DateTimeOffset scheduledStart, DateTimeOffset scheduledEnd)
    {
        if (scheduledEnd <= scheduledStart)
            throw new OrderDomainException("Delivery slot end time must be after start time.");

        _deliverySlot = new DeliverySlot(Guid.NewGuid(), storeId, scheduledStart, scheduledEnd);
    }

    public void MarkReturned(Guid returnId, string updatedBy)
    {
        var returnableStatuses = new[]
        {
            OrderStatus.Delivered, OrderStatus.Collected, OrderStatus.Paid, OrderStatus.OnHold
        };

        if (!returnableStatuses.Contains(Status))
            throw new InvalidStateTransitionException(Status, nameof(MarkReturned));

        Transition(OrderStatus.Returned, updatedBy, $"returnId={returnId}");
        RaiseDomainEvent(new OrderReturnedEvent(OrderId, returnId));
    }

    public void ConfirmDamagedGoodsReceived(string trackingId, string updatedBy)
    {
        if (Status != OrderStatus.OnHold)
            throw new InvalidStateTransitionException(Status, nameof(ConfirmDamagedGoodsReceived));

        var pkg = _packages.FirstOrDefault(p => p.TrackingId == trackingId)
            ?? throw new OrderDomainException($"Package with tracking ID '{trackingId}' not found.");

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new DamagedGoodsReceivedEvent(OrderId, pkg.TrackingId));
    }

    public void ConfirmDamagedGoodsPutAway(string trackingId, string updatedBy)
    {
        if (Status != OrderStatus.OnHold)
            throw new InvalidStateTransitionException(Status, nameof(ConfirmDamagedGoodsPutAway));

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new DamagedGoodsPutAwayConfirmedEvent(OrderId, trackingId));
    }
}
