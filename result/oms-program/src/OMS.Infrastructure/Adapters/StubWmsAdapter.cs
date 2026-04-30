using Microsoft.Extensions.Logging;

namespace OMS.Infrastructure.Adapters;

public class StubWmsAdapter(ILogger<StubWmsAdapter> logger) : IWmsAdapter
{
    public Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[WMS STUB] Sending event '{EventType}' to WMS. Payload: {Payload}", eventType, payload);
        return Task.CompletedTask;
    }
}
