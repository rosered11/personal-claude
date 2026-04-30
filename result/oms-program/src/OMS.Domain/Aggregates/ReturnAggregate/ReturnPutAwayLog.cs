using OMS.Domain.Enums;

namespace OMS.Domain.Aggregates.ReturnAggregate;

public class ReturnPutAwayLog
{
    public Guid LogId { get; private set; }
    public Guid ReturnId { get; private set; }
    public Guid ReturnItemId { get; private set; }
    public string Sku { get; private set; } = null!;
    public string AssignedSloc { get; private set; } = null!;
    public ItemCondition Condition { get; private set; }
    public decimal Quantity { get; private set; }
    public string PerformedBy { get; private set; } = null!;
    public DateTimeOffset PerformedAt { get; private set; }

    private ReturnPutAwayLog() { }

    public static ReturnPutAwayLog Create(
        Guid returnId,
        Guid returnItemId,
        string sku,
        string assignedSloc,
        ItemCondition condition,
        decimal quantity,
        string performedBy) =>
        new ReturnPutAwayLog
        {
            LogId = Guid.NewGuid(),
            ReturnId = returnId,
            ReturnItemId = returnItemId,
            Sku = sku,
            AssignedSloc = assignedSloc,
            Condition = condition,
            Quantity = quantity,
            PerformedBy = performedBy,
            PerformedAt = DateTimeOffset.UtcNow
        };
}
