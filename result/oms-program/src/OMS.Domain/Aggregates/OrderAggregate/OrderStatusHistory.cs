using OMS.Domain.Enums;

namespace OMS.Domain.Aggregates.OrderAggregate;

public class OrderStatusHistory
{
    public Guid HistoryId { get; private set; }
    public Guid OrderId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public string ChangedBy { get; private set; } = null!;
    public string? Detail { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    private OrderStatusHistory() { }

    internal static OrderStatusHistory Record(
        Guid orderId,
        OrderStatus? fromStatus,
        OrderStatus toStatus,
        string changedBy,
        string? detail = null) => new()
    {
        HistoryId = Guid.NewGuid(),
        OrderId = orderId,
        FromStatus = fromStatus,
        ToStatus = toStatus,
        ChangedBy = changedBy,
        Detail = detail,
        ChangedAt = DateTimeOffset.UtcNow
    };
}
