using OMS.Application.Common.Interfaces;
using OMS.Infrastructure.Persistence;

namespace OMS.Infrastructure.Inbound;

public class WebhookEventLogger(OrderDbContext dbContext) : IWebhookEventLogger
{
    public void Stage(Guid orderId, string sourceSystem, string eventType, string? detail = null)
    {
        dbContext.OrderWebhookLogs.Add(new OrderWebhookLog
        {
            WebhookLogId = Guid.NewGuid(),
            OrderId = orderId,
            SourceSystem = sourceSystem,
            EventType = eventType,
            Detail = detail,
            ReceivedAt = DateTimeOffset.UtcNow
        });
    }
}
