using OMS.Domain.Events;

namespace OMS.Application.Common.Interfaces;

public interface IOutboxPublisher
{
    Task PublishAsync(Guid orderId, DomainEvent domainEvent, CancellationToken cancellationToken = default);
}
