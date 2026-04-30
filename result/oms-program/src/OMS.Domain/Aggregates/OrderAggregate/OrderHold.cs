namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderHold
{
    public Guid HoldId { get; private set; }
    public Guid OrderId { get; private set; }
    public string HoldReason { get; private set; } = null!;
    public DateTimeOffset HeldAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public string HeldBy { get; private set; } = null!;
    public string? ReleasedBy { get; private set; }

    private OrderHold() { }

    public static OrderHold Create(Guid orderId, string holdReason, string heldBy) =>
        new OrderHold
        {
            HoldId = Guid.NewGuid(),
            OrderId = orderId,
            HoldReason = holdReason,
            HeldAt = DateTimeOffset.UtcNow,
            HeldBy = heldBy
        };

    public void Release(string releasedBy)
    {
        ReleasedAt = DateTimeOffset.UtcNow;
        ReleasedBy = releasedBy;
    }
}
