using System.Text.Json;
using OMS.Application.Common.Interfaces;
using OMS.Domain.Enums;
using OMS.Domain.Events;

namespace OMS.Infrastructure.Outbox;

public class OutboxPublisher(OMS.Infrastructure.Persistence.OrderDbContext dbContext) : IOutboxPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task PublishAsync(Guid orderId, DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var eventTypeName = domainEvent.GetType().Name;
        var targets = OutboxEventTargetMapper.GetTargets(eventTypeName);

        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);

        if (targets.Length == 0)
        {
            var entry = new OrderOutbox
            {
                OutboxId = Guid.NewGuid(),
                OrderId = orderId,
                EventType = eventTypeName,
                TargetSystem = "INTERNAL",
                EventPayload = payload,
                Status = OutboxStatus.Pending,
                RetryCount = 0,
                NextRetryAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.OrderOutbox.Add(entry);
        }
        else
        {
            foreach (var target in targets)
            {
                var entry = new OrderOutbox
                {
                    OutboxId = Guid.NewGuid(),
                    OrderId = orderId,
                    EventType = eventTypeName,
                    TargetSystem = target,
                    EventPayload = payload,
                    Status = OutboxStatus.Pending,
                    RetryCount = 0,
                    NextRetryAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                dbContext.OrderOutbox.Add(entry);
            }
        }

        await Task.CompletedTask;
    }
}
