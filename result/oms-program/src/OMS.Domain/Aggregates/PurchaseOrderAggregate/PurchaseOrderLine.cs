using OMS.Domain.Enums;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.PurchaseOrderAggregate;

public class PurchaseOrderLine
{
    public Guid LineId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public decimal OrderedQty { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; }
    public ItemCondition? Condition { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PurchaseOrderLine() { }

    public static PurchaseOrderLine Create(
        Guid purchaseOrderId,
        string sku,
        string productName,
        decimal orderedQty,
        UnitOfMeasure unitOfMeasure)
    {
        if (orderedQty <= 0)
            throw new OrderDomainException($"Ordered quantity for SKU '{sku}' must be greater than zero.");

        return new PurchaseOrderLine
        {
            LineId = Guid.NewGuid(),
            PurchaseOrderId = purchaseOrderId,
            Sku = sku,
            ProductName = productName,
            OrderedQty = orderedQty,
            ReceivedQty = 0,
            UnitOfMeasure = unitOfMeasure,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void ApplyReceipt(decimal receivedQty, ItemCondition condition)
    {
        if (receivedQty < 0)
            throw new OrderDomainException($"Received quantity for SKU '{Sku}' cannot be negative.");

        ReceivedQty = receivedQty;
        Condition = condition;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
