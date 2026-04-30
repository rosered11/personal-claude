using OMS.Domain.Enums;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.TransferOrderAggregate;

public class TransferOrderLine
{
    public Guid LineId { get; private set; }
    public Guid TransferOrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public decimal RequestedQty { get; private set; }
    public decimal TransferredQty { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TransferOrderLine() { }

    public static TransferOrderLine Create(
        Guid transferOrderId,
        string sku,
        string productName,
        decimal requestedQty,
        UnitOfMeasure unitOfMeasure)
    {
        if (requestedQty <= 0)
            throw new OrderDomainException($"Requested quantity for SKU '{sku}' must be greater than zero.");

        return new TransferOrderLine
        {
            LineId = Guid.NewGuid(),
            TransferOrderId = transferOrderId,
            Sku = sku,
            ProductName = productName,
            RequestedQty = requestedQty,
            TransferredQty = 0,
            UnitOfMeasure = unitOfMeasure,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void ApplyTransferred(decimal transferredQty)
    {
        if (transferredQty < 0)
            throw new OrderDomainException($"Transferred quantity for SKU '{Sku}' cannot be negative.");

        TransferredQty = transferredQty;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
