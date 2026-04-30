namespace OMS.Infrastructure.Inbound;

public class OrderWebhookLog
{
    public Guid WebhookLogId { get; set; }
    public Guid OrderId { get; set; }
    public string SourceSystem { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string? Detail { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
