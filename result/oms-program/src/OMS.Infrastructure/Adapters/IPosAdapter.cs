namespace OMS.Infrastructure.Adapters;

public interface IPosAdapter
{
    Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
