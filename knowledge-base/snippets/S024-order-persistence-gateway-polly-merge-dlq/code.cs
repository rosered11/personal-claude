// IOrderPersistenceGateway.cs -- Hexagonal driven-adapter port for validate-service order writes.
// Combines: (1) MERGE-based idempotent upsert, (2) Polly transient-retry classification
// including SqlException -2 (Execution Timeout), (3) a durable dedup guard + DLQ publish
// so a message is never silently skipped -- only ever safely retried or dead-lettered.

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public interface IOrderPersistenceGateway
{
    Task<PersistResult> UpsertOrderAsync(MaoOrderEvent evt, CancellationToken ct);
}

public enum PersistResult { Persisted, AlreadyProcessed }

public sealed class OrderPersistenceExhaustedException : Exception
{
    public MaoOrderEvent Event { get; }
    public OrderPersistenceExhaustedException(MaoOrderEvent evt, Exception inner)
        : base($"Persistence exhausted for order {evt.OrderId}", inner) => Event = evt;
}

public sealed class SqlOrderPersistenceGateway : IOrderPersistenceGateway
{
    private readonly OrderContext _db;
    private readonly AsyncRetryPolicy _resiliencePolicy;
    private readonly ILogger<SqlOrderPersistenceGateway> _log;

    // SQL Server transient error codes. -2 (Execution Timeout Expired) is the exact error
    // observed in production (log-20260713-jd8dr): "10,020"ms against CommandTimeout='10'.
    // EF Core's default SqlServerRetryingExecutionStrategy does NOT retry -2 unless the
    // error is explicitly added -- this is why the original failure surfaced to the caller
    // on the very first attempt instead of being absorbed transparently.
    private static readonly HashSet<int> TransientSqlErrors = new() { -2, 1205, 4060, 40197, 40501, 40613 };

    public SqlOrderPersistenceGateway(OrderContext db, ILogger<SqlOrderPersistenceGateway> log)
    {
        _db = db;
        _log = log;

        _resiliencePolicy = Policy
            .Handle<SqlException>(ex => TransientSqlErrors.Contains(ex.Number))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)),
                onRetry: (ex, delay, attempt, ctx) =>
                {
                    var sqlEx = ex as SqlException;
                    _log.LogWarning(ex,
                        "Transient SQL error {ErrorNumber} on attempt {Attempt}, retrying in {Delay}",
                        sqlEx?.Number, attempt, delay);
                });
    }

    public async Task<PersistResult> UpsertOrderAsync(MaoOrderEvent evt, CancellationToken ct)
    {
        // Idempotency dedup guard (extends D012/S012/D015 pattern) -- fast-path, avoids even
        // attempting a duplicate write when this event was already processed by another pod.
        var alreadyProcessed = await _db.ProcessedEvents.AnyAsync(p => p.EventId == evt.Id, ct);
        if (alreadyProcessed)
        {
            _log.LogInformation(
                "Event {EventId} for order {OrderId} already processed -- skipping (idempotent no-op)",
                evt.Id, evt.OrderId);
            return PersistResult.AlreadyProcessed;
        }

        try
        {
            await _resiliencePolicy.ExecuteAsync(async token =>
            {
                // MERGE-based upsert: retryable and race-safe even if the dedup check above
                // races with a concurrent redelivery on another pod. HOLDLOCK avoids the exact
                // duplicate-key race that produced UN_SourceOrderId violations in production
                // (two DbUpdateExceptions 26ms apart for the same SourceOrderId).
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    MERGE dbo.[Order] WITH (HOLDLOCK) AS target
                    USING (SELECT {evt.OrderId} AS SourceOrderId) AS source
                    ON target.SourceOrderId = source.SourceOrderId
                    WHEN NOT MATCHED THEN
                        INSERT (SourceOrderId, CreatedDate, Status)
                        VALUES ({evt.OrderId}, {DateTime.UtcNow}, {"Received"});
                ", token);

                _db.ProcessedEvents.Add(new ProcessedEvent { EventId = evt.Id, ProcessedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync(token);
            }, ct);

            return PersistResult.Persisted;
        }
        catch (SqlException ex)
        {
            // Resilience policy exhausted -- this must NEVER be a silent skip.
            // Caller routes this to the DLQ instead of the old "Message skipped" log line.
            throw new OrderPersistenceExhaustedException(evt, ex);
        }
    }
}

// MAOOrderEventHandler.cs -- Kafka consumer using the resilient gateway. Replaces the
// production behavior "Failed to process message ... Attempt=1/1" -> "Message skipped after
// 1 retries" (no DLQ) with a guaranteed DLQ publish on exhaustion.
public sealed class MAOOrderEventHandler
{
    private readonly IOrderPersistenceGateway _gateway;
    private readonly IDeadLetterPublisher _dlq;
    private readonly ILogger<MAOOrderEventHandler> _log;

    public MAOOrderEventHandler(
        IOrderPersistenceGateway gateway, IDeadLetterPublisher dlq, ILogger<MAOOrderEventHandler> log)
    {
        _gateway = gateway;
        _dlq = dlq;
        _log = log;
    }

    public async Task HandleAsync(MaoOrderEvent evt, CancellationToken ct)
    {
        try
        {
            var result = await _gateway.UpsertOrderAsync(evt, ct);
            _log.LogInformation("Order {OrderId} handled with result {Result}", evt.OrderId, result);
        }
        catch (OrderPersistenceExhaustedException ex)
        {
            // No more silent "Message skipped" -- every exhausted event lands in the DLQ
            // with full payload for replay and triggers a depth alert.
            await _dlq.PublishAsync("mao.in.fc.order.release.ds.dlq", evt, ex, ct);
            _log.LogError(ex, "Order {OrderId} routed to DLQ after resilience policy exhaustion", evt.OrderId);
        }
    }
}
