namespace OMS.Application.Common.Interfaces;

public interface IWebhookEventLogger
{
    void Stage(Guid orderId, string sourceSystem, string eventType, string? detail = null);
}
