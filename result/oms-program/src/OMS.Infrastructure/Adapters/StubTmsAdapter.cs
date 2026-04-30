using Microsoft.Extensions.Logging;

namespace OMS.Infrastructure.Adapters;

public class StubTmsAdapter(ILogger<StubTmsAdapter> logger) : ITmsAdapter
{
    public Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[TMS STUB] Sending event '{EventType}' to TMS. Payload: {Payload}", eventType, payload);
        return Task.CompletedTask;
    }
}
