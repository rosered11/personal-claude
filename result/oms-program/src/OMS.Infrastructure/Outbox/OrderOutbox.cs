using OMS.Domain.Enums;

namespace OMS.Infrastructure.Outbox;

public class OrderOutbox
{
    public Guid OutboxId { get; set; }
    public Guid OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string TargetSystem { get; set; } = string.Empty;
    public string EventPayload { get; set; } = string.Empty;
    public OutboxStatus Status { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
