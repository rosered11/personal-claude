using Microsoft.Extensions.Logging;

namespace OMS.Infrastructure.Adapters;

public class StubPosAdapter(ILogger<StubPosAdapter> logger) : IPosAdapter
{
    public Task SendAsync(string eventType, string payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[POS STUB] Sending event '{EventType}' to POS. Payload: {Payload}", eventType, payload);
        return Task.CompletedTask;
    }
}
