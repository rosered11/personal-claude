using OMS.Domain.Enums;
using OMS.Domain.Events;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.PurchaseOrderAggregate;

public class PurchaseOrder : AggregateRoot
{
    public Guid PurchaseOrderId { get; private set; }
    public string PoNumber { get; private set; } = null!;
    public string SupplierId { get; private set; } = null!;
    public Guid StoreId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = null!;
    public string UpdatedBy { get; private set; } = null!;

    private readonly List<PurchaseOrderLine> _lines = new();
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines.AsReadOnly();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(
        string poNumber,
        string supplierId,
        Guid storeId,
        string createdBy,
        IEnumerable<(string Sku, string ProductName, decimal OrderedQty, UnitOfMeasure UnitOfMeasure)> lines)
    {
        if (string.IsNullOrWhiteSpace(poNumber))
            throw new OrderDomainException("PO number cannot be empty.");

        var po = new PurchaseOrder
        {
            PurchaseOrderId = Guid.NewGuid(),
            PoNumber = poNumber,
            SupplierId = supplierId,
            StoreId = storeId,
            Status = PurchaseOrderStatus.Created,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            UpdatedBy = createdBy
        };

        foreach (var line in lines)
            po._lines.Add(PurchaseOrderLine.Create(po.PurchaseOrderId, line.Sku, line.ProductName, line.OrderedQty, line.UnitOfMeasure));

        if (!po._lines.Any())
            throw new OrderDomainException("A purchase order must have at least one line.");

        po.RaiseDomainEvent(new PurchaseOrderCreatedEvent(po.PurchaseOrderId, po.PoNumber));
        return po;
    }

    public void ConfirmGoodsReceipt(
        IEnumerable<(Guid LineId, decimal ReceivedQty, ItemCondition Condition)> receipts,
        string updatedBy)
    {
        if (Status == PurchaseOrderStatus.Closed)
            throw new OrderDomainException($"Purchase order '{PoNumber}' is already closed.");

        foreach (var (lineId, receivedQty, condition) in receipts)
        {
            var line = _lines.FirstOrDefault(l => l.LineId == lineId)
                ?? throw new OrderDomainException($"PO line '{lineId}' not found.");
            line.ApplyReceipt(receivedQty, condition);
        }

        bool fullyReceived = _lines.All(l => l.ReceivedQty >= l.OrderedQty);
        Status = fullyReceived ? PurchaseOrderStatus.FullyReceived : PurchaseOrderStatus.PartiallyReceived;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new GoodsReceiptConfirmedEvent(PurchaseOrderId, fullyReceived));
    }

    public void ConfirmPutAway(string updatedBy)
    {
        if (Status != PurchaseOrderStatus.PartiallyReceived && Status != PurchaseOrderStatus.FullyReceived)
            throw new OrderDomainException($"Purchase order '{PoNumber}' must be received before put-away.");

        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PurchaseOrderPutAwayConfirmedEvent(PurchaseOrderId));
    }

    public void Close(string updatedBy)
    {
        if (Status == PurchaseOrderStatus.Closed)
            throw new OrderDomainException($"Purchase order '{PoNumber}' is already closed.");

        Status = PurchaseOrderStatus.Closed;
        UpdatedBy = updatedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new PurchaseOrderClosedEvent(PurchaseOrderId));
    }
}
