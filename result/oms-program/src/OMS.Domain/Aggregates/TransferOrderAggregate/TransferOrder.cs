using OMS.Domain.Enums;
using OMS.Domain.Events;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.TransferOrderAggregate;

public class TransferOrder : AggregateRoot
{
    public Guid TransferOrderId { get; private set; }
    public string TransferNumber { get; private set; } = null!;
    public Guid SourceStoreId { get; private set; }
    public Guid DestStoreId { get; private set; }
    public TransferOrderStatus Status { get; private set; }
    public string? TrackingId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public string UpdatedBy { get; private set; } = null!;

    private readonly List<TransferOrderLine> _lines = new();
    public IReadOnlyList<TransferOrderLine> Lines => _lines.AsReadOnly();

    private TransferOrder() { }

    public static TransferOrder Create(
        string transferNumber,
        Guid sourceStoreId,
        Guid destStoreId,
        string createdBy,
        IEnumerable<(string Sku, string ProductName, decimal RequestedQty, UnitOfMeasure UnitOfMeasure)> lines)
    {
        if (string.IsNullOrWhiteSpace(transferNumber))
            throw new OrderDomainException("Transfer number cannot be empty.");
        if (sourceStoreId == destStoreId)
            throw new OrderDomainException("Source and destination stores cannot be the same.");

        var to = new TransferOrder
        {
            TransferOrderId = Guid.NewGuid(),
            TransferNumber = transferNumber,
            SourceStoreId = sourceStoreId,
            DestStoreId = destStoreId,
            Status = TransferOrderStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };

        foreach (var line in lines)
            to._lines.Add(TransferOrderLine.Create(to.TransferOrderId, line.Sku, line.ProductName, line.RequestedQty, line.UnitOfMeasure));

        if (!to._lines.Any())
            throw new OrderDomainException("A transfer order must have at least one line.");

        to.RaiseDomainEvent(new TransferOrderCreatedEvent(to.TransferOrderId, to.TransferNumber));
        return to;
    }

    public void ConfirmPick(
        IEnumerable<(Guid LineId, decimal TransferredQty)> pickedLines,
        string updatedBy)
    {
        if (Status != TransferOrderStatus.Created)
            throw new OrderDomainException($"Transfer order '{TransferNumber}' must be in Created status to confirm pick.");

        foreach (var (lineId, qty) in pickedLines)
        {
            var line = _lines.FirstOrDefault(l => l.LineId == lineId)
                ?? throw new OrderDomainException($"Transfer line '{lineId}' not found.");
            line.ApplyTransferred(qty);
        }

        Status = TransferOrderStatus.PickConfirmed;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TransferPickConfirmedEvent(TransferOrderId));
    }

    public void MarkInTransit(string trackingId, string updatedBy)
    {
        if (Status != TransferOrderStatus.PickConfirmed)
            throw new OrderDomainException($"Transfer order '{TransferNumber}' must be PickConfirmed to mark in transit.");

        TrackingId = trackingId;
        Status = TransferOrderStatus.InTransit;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TransferOrderInTransitEvent(TransferOrderId, trackingId));
    }

    public void ConfirmReceived(string updatedBy)
    {
        if (Status != TransferOrderStatus.InTransit)
            throw new OrderDomainException($"Transfer order '{TransferNumber}' must be InTransit to confirm receipt.");

        Status = TransferOrderStatus.Received;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TransferReceivedEvent(TransferOrderId));
    }

    public void Complete(string updatedBy)
    {
        if (Status != TransferOrderStatus.Received)
            throw new OrderDomainException($"Transfer order '{TransferNumber}' must be Received before completing.");

        Status = TransferOrderStatus.Completed;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new TransferOrderCompletedEvent(TransferOrderId));
    }
}
