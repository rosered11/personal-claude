using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OMS.Domain.Enums;
using OMS.Infrastructure.Adapters;
using OMS.Infrastructure.Persistence;

namespace OMS.Infrastructure.Outbox;

public class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private const int MaxRetries = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error in OutboxWorker.");
            }

            await Task.Delay(PollingInterval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }

        logger.LogInformation("OutboxWorker stopping — draining in-flight batch.");
        await DrainAsync();
        logger.LogInformation("OutboxWorker stopped.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var wms = scope.ServiceProvider.GetRequiredService<IWmsAdapter>();
        var tms = scope.ServiceProvider.GetRequiredService<ITmsAdapter>();
        var pos = scope.ServiceProvider.GetRequiredService<IPosAdapter>();

        var now = DateTimeOffset.UtcNow;

        var entries = await dbContext.OrderOutbox
            .FromSqlRaw(
                @"SELECT * FROM orders.order_outbox
                  WHERE status = 'Pending'
                    AND (next_retry_at IS NULL OR next_retry_at <= {0})
                  ORDER BY created_at
                  LIMIT {1}
                  FOR UPDATE SKIP LOCKED",
                now, BatchSize)
            .ToListAsync(cancellationToken);

        if (entries.Count == 0)
            return;

        foreach (var entry in entries)
        {
            entry.Status = OutboxStatus.Processing;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var entry in entries)
        {
            try
            {
                await DispatchAsync(entry, wms, tms, pos, cancellationToken);
                entry.Status = OutboxStatus.Published;
                entry.PublishedAt = DateTimeOffset.UtcNow;
                logger.LogInformation(
                    "Outbox entry {OutboxId} ({EventType} → {Target}) published.",
                    entry.OutboxId, entry.EventType, entry.TargetSystem);
            }
            catch (Exception ex)
            {
                entry.RetryCount++;

                if (entry.RetryCount >= MaxRetries)
                {
                    entry.Status = OutboxStatus.Failed;
                    logger.LogError(ex,
                        "Outbox entry {OutboxId} ({EventType} → {Target}) permanently failed after {RetryCount} retries.",
                        entry.OutboxId, entry.EventType, entry.TargetSystem, entry.RetryCount);
                }
                else
                {
                    entry.Status = OutboxStatus.Pending;
                    var backoffSeconds = (int)Math.Pow(2, entry.RetryCount);
                    entry.NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(backoffSeconds);
                    logger.LogWarning(ex,
                        "Outbox entry {OutboxId} ({EventType} → {Target}) failed, retry {RetryCount}/{MaxRetries} at {NextRetryAt}.",
                        entry.OutboxId, entry.EventType, entry.TargetSystem, entry.RetryCount, MaxRetries, entry.NextRetryAt);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task DispatchAsync(
        OrderOutbox entry,
        IWmsAdapter wms,
        ITmsAdapter tms,
        IPosAdapter pos,
        CancellationToken cancellationToken)
    {
        switch (entry.TargetSystem.ToUpperInvariant())
        {
            case "WMS":
                await wms.SendAsync(entry.EventType, entry.EventPayload, cancellationToken);
                break;
            case "TMS":
                await tms.SendAsync(entry.EventType, entry.EventPayload, cancellationToken);
                break;
            case "POS":
                await pos.SendAsync(entry.EventType, entry.EventPayload, cancellationToken);
                break;
            case "INTERNAL":
                break;
            default:
                throw new InvalidOperationException($"Unknown target system '{entry.TargetSystem}'.");
        }
    }

    private async Task DrainAsync()
    {
        try
        {
            using var drainCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await ProcessBatchAsync(drainCts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OutboxWorker drain.");
        }
    }
}
