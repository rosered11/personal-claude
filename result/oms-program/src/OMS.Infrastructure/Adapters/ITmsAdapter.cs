namespace OMS.Infrastructure.Adapters;

public interface ITmsAdapter
{
    Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
