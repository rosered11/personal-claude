using OMS.Domain.Aggregates.OrderAggregate;

namespace OMS.Application.Common.Interfaces;

public record OutboxEntrySnapshot(
    string EventType,
    string TargetSystem,
    string Status,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public record WebhookLogSnapshot(
    string SourceSystem,
    string EventType,
    string? Detail,
    DateTimeOffset ReceivedAt);

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken cancellationToken = default);
    Task<Order?> GetBySourceOrderIdAsync(string sourceOrderId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Order> Items, int TotalCount)> ListAsync(
        string? status,
        Guid? storeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxEntrySnapshot>> GetOutboxEntriesAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookLogSnapshot>> GetWebhookLogsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task SaveAsync(Order order, CancellationToken cancellationToken = default);
}
