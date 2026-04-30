using OMS.Domain.Enums;
using OMS.Domain.Events;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.ReturnAggregate;

public class OrderReturn : AggregateRoot
{
    public Guid ReturnId { get; private set; }
    public Guid OrderId { get; private set; }
    public string ReturnOrderNumber { get; private set; } = null!;
    public string? InvoiceId { get; private set; }
    public string? CreditNoteId { get; private set; }
    public ReturnStatus Status { get; private set; }
    public string? GoodsReceiveNo { get; private set; }
    public string ReturnReason { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? PickupScheduledAt { get; private set; }
    public DateTimeOffset? PickedUpAt { get; private set; }
    public DateTimeOffset? ReceivedAt { get; private set; }
    public DateTimeOffset? InspectedAt { get; private set; }
    public DateTimeOffset? PutAwayAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public string UpdatedBy { get; private set; } = null!;

    private readonly List<ReturnItem> _returnItems = new();
    public IReadOnlyList<ReturnItem> ReturnItems => _returnItems.AsReadOnly();

    private readonly List<ReturnPutAwayLog> _putAwayLogs = new();
    public IReadOnlyList<ReturnPutAwayLog> PutAwayLogs => _putAwayLogs.AsReadOnly();

    private OrderReturn() { }

    public static OrderReturn Create(
        Guid orderId,
        string returnOrderNumber,
        string returnReason,
        string? invoiceId,
        string createdBy,
        IEnumerable<(Guid OrderLineId, string Sku, string ProductName, string Barcode, decimal Quantity, UnitOfMeasure UnitOfMeasure, decimal UnitPrice, string Currency, PaymentMethod PaymentMethod, string? ItemReason)> items)
    {
        if (string.IsNullOrWhiteSpace(returnOrderNumber))
            throw new ReturnDomainException("Return order number cannot be empty.");
        if (string.IsNullOrWhiteSpace(returnReason))
            throw new ReturnDomainException("Return reason cannot be empty.");

        var ret = new OrderReturn
        {
            ReturnId = Guid.NewGuid(),
            OrderId = orderId,
            ReturnOrderNumber = returnOrderNumber,
            InvoiceId = invoiceId,
            ReturnReason = returnReason,
            Status = ReturnStatus.ReturnRequested,
            RequestedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };

        foreach (var item in items)
        {
            ret._returnItems.Add(ReturnItem.Create(
                ret.ReturnId,
                item.OrderLineId,
                item.Sku,
                item.ProductName,
                item.Barcode,
                item.Quantity,
                item.UnitOfMeasure,
                item.UnitPrice,
                item.Currency,
                item.PaymentMethod,
                item.ItemReason));
        }

        if (!ret._returnItems.Any())
            throw new ReturnDomainException("A return must have at least one item.");

        ret.RaiseDomainEvent(new ReturnRequestedEvent(orderId, ret.ReturnId));
        return ret;
    }

    public void SchedulePickup(DateTimeOffset pickupScheduledAt, string updatedBy)
    {
        if (Status != ReturnStatus.ReturnRequested)
            throw new ReturnDomainException($"Cannot schedule pickup for return in status '{Status}'.");

        PickupScheduledAt = pickupScheduledAt;
        Status = ReturnStatus.PickupScheduled;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ConfirmPickedUp(string updatedBy)
    {
        if (Status != ReturnStatus.PickupScheduled)
            throw new ReturnDomainException($"Cannot confirm pickup for return in status '{Status}'.");

        PickedUpAt = DateTimeOffset.UtcNow;
        Status = ReturnStatus.PickedUp;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ConfirmReceivedAtWarehouse(string goodsReceiveNo, string updatedBy)
    {
        if (Status != ReturnStatus.PickedUp)
            throw new ReturnDomainException($"Cannot confirm warehouse receipt for return in status '{Status}'.");

        GoodsReceiveNo = goodsReceiveNo;
        ReceivedAt = DateTimeOffset.UtcNow;
        Status = ReturnStatus.ReceivedAtWarehouse;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new ReturnReceivedAtWarehouseEvent(ReturnId));
    }

    public void ConfirmPutAway(
        IEnumerable<(Guid ReturnItemId, ItemCondition Condition, string AssignedSloc, decimal Quantity, string PerformedBy)> putAwayItems,
        string updatedBy)
    {
        if (Status != ReturnStatus.ReceivedAtWarehouse && Status != ReturnStatus.Inspected)
            throw new ReturnDomainException($"Cannot confirm put away for return in status '{Status}'.");

        InspectedAt ??= DateTimeOffset.UtcNow;

        foreach (var (returnItemId, condition, sloc, quantity, performedBy) in putAwayItems)
        {
            var item = _returnItems.FirstOrDefault(i => i.ReturnItemId == returnItemId)
                ?? throw new ReturnDomainException($"Return item '{returnItemId}' not found.");

            item.SetPutAway(condition, sloc);
            item.MarkInspected();

            _putAwayLogs.Add(ReturnPutAwayLog.Create(
                ReturnId,
                returnItemId,
                item.Sku,
                sloc,
                condition,
                quantity,
                performedBy));
        }

        PutAwayAt = DateTimeOffset.UtcNow;
        Status = ReturnStatus.PutAway;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new PutAwayConfirmedEvent(ReturnId));
    }

    public void ProcessRefund(string creditNoteId, string updatedBy)
    {
        if (Status != ReturnStatus.PutAway)
            throw new ReturnDomainException($"Cannot process refund for return in status '{Status}'.");

        CreditNoteId = creditNoteId;
        RefundedAt = DateTimeOffset.UtcNow;
        Status = ReturnStatus.Refunded;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new RefundProcessedEvent(ReturnId, OrderId));
    }
}
