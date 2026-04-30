using OMS.Domain.Exceptions;

namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderLineSubstitution
{
    public Guid SubstitutionId { get; private set; }
    public Guid OrderLineId { get; private set; }
    public Guid SubstituteOrderLineId { get; private set; }
    public string SubstituteSku { get; private set; } = null!;
    public string SubstituteProductName { get; private set; } = null!;
    public string SubstituteBarcode { get; private set; } = null!;
    public decimal SubstituteUnitPrice { get; private set; }
    public decimal SubstitutedAmount { get; private set; }
    public bool? CustomerApproved { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private OrderLineSubstitution() { }

    public static OrderLineSubstitution Create(
        Guid orderLineId,
        Guid substituteOrderLineId,
        string substituteSku,
        string substituteProductName,
        string substituteBarcode,
        decimal substituteUnitPrice,
        decimal substitutedAmount,
        bool autoApprove)
    {
        return new OrderLineSubstitution
        {
            SubstitutionId = Guid.NewGuid(),
            OrderLineId = orderLineId,
            SubstituteOrderLineId = substituteOrderLineId,
            SubstituteSku = substituteSku,
            SubstituteProductName = substituteProductName,
            SubstituteBarcode = substituteBarcode,
            SubstituteUnitPrice = substituteUnitPrice,
            SubstitutedAmount = substitutedAmount,
            CustomerApproved = autoApprove ? true : null,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Approve()
    {
        if (CustomerApproved.HasValue)
            throw new OrderDomainException(
                $"Substitution '{SubstitutionId}' is already {(CustomerApproved.Value ? "approved" : "rejected")}.");
        CustomerApproved = true;
    }

    public void Reject()
    {
        if (CustomerApproved.HasValue)
            throw new OrderDomainException(
                $"Substitution '{SubstitutionId}' is already {(CustomerApproved.Value ? "approved" : "rejected")}.");
        CustomerApproved = false;
    }
}
