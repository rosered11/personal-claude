namespace OMS.Infrastructure.Adapters;

public interface IWmsAdapter
{
    Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
