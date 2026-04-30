using OMS.Domain.Enums;
using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderLine
{
    public Guid OrderLineId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string ProductName { get; private set; } = null!;
    public string Barcode { get; private set; } = null!;
    public decimal RequestedAmount { get; private set; }
    public decimal PickedAmount { get; private set; }
    public UnitOfMeasure UnitOfMeasure { get; private set; }
    public decimal OriginalUnitPrice { get; private set; }
    public string Currency { get; private set; } = null!;
    public OrderLineStatus Status { get; private set; }
    public bool IsSubstitute { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private OrderLine() { }

    public static OrderLine Create(
        Guid orderId,
        string sku,
        string productName,
        string barcode,
        decimal requestedAmount,
        UnitOfMeasure unitOfMeasure,
        decimal originalUnitPrice,
        string currency,
        bool isSubstitute = false)
    {
        if (requestedAmount <= 0)
            throw new OrderDomainException($"Requested amount for SKU '{sku}' must be greater than zero.");

        return new OrderLine
        {
            OrderLineId = Guid.NewGuid(),
            OrderId = orderId,
            Sku = sku,
            ProductName = productName,
            Barcode = barcode,
            RequestedAmount = requestedAmount,
            PickedAmount = 0,
            UnitOfMeasure = unitOfMeasure,
            OriginalUnitPrice = originalUnitPrice,
            Currency = currency,
            IsSubstitute = isSubstitute,
            Status = OrderLineStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateQuantity(decimal newQuantity)
    {
        if (newQuantity <= 0)
            throw new OrderDomainException($"Quantity for SKU '{Sku}' must be greater than zero.");

        RequestedAmount = newQuantity;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ApplyPickedAmount(decimal pickedAmount)
    {
        if (pickedAmount < 0)
            throw new OrderDomainException($"Picked amount for SKU '{Sku}' cannot be negative.");
        if (pickedAmount > RequestedAmount)
            throw new OrderDomainException($"Picked amount ({pickedAmount}) for SKU '{Sku}' cannot exceed requested amount ({RequestedAmount}).");

        PickedAmount = pickedAmount;
        Status = pickedAmount == 0
            ? OrderLineStatus.Cancelled
            : pickedAmount < RequestedAmount
                ? OrderLineStatus.PartiallyPicked
                : OrderLineStatus.FullyPicked;

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        Status = OrderLineStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
