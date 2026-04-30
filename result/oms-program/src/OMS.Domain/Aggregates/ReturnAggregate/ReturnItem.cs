using OMS.Domain.Enums;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.ReturnAggregate;

public class ReturnItem
{
    public Guid ReturnItemId { get; private set; }
    public Guid ReturnId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string Barcode { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; } = null!;
    public string? ItemReason { get; private set; }
    public ItemCondition? Condition { get; private set; }
    public string? PutAwayStatus { get; private set; }
    public string? AssignedSloc { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public DateTimeOffset? InspectedAt { get; private set; }
    public DateTimeOffset? PutAwayAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ReturnItem() { }

    public static ReturnItem Create(
        Guid returnId,
        Guid orderLineId,
        string sku,
        string productName,
        string barcode,
        decimal quantity,
        UnitOfMeasure unitOfMeasure,
        decimal unitPrice,
        string currency,
        PaymentMethod paymentMethod,
        string? itemReason = null)
    {
        if (quantity <= 0)
            throw new ReturnDomainException($"Quantity for return item SKU '{sku}' must be greater than zero.");

        return new ReturnItem
        {
            ReturnItemId = Guid.NewGuid(),
            ReturnId = returnId,
            OrderLineId = orderLineId,
            Sku = sku,
            ProductName = productName,
            Barcode = barcode,
            Quantity = quantity,
            UnitOfMeasure = unitOfMeasure,
            UnitPrice = unitPrice,
            Currency = currency,
            PaymentMethod = paymentMethod,
            ItemReason = itemReason,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void SetPutAway(ItemCondition condition, string assignedSloc)
    {
        if (string.IsNullOrWhiteSpace(assignedSloc))
            throw new ReturnDomainException($"Assigned SLOC cannot be empty for return item '{Sku}'.");

        Condition = condition;
        AssignedSloc = assignedSloc;
        PutAwayStatus = "PutAway";
        PutAwayAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkInspected()
    {
        InspectedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
