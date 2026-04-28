// S020 — OMS Outbox Worker: FOR UPDATE SKIP LOCKED + Graceful Shutdown
// Pattern: single-writer PostgreSQL outbox poller with Kubernetes SIGTERM handling
// Related: P015 (OMS Modular Monolith), D020 (Modular Monolith + Outbox Worker)

public class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly IAclAdapterDispatcher _dispatcher;

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxWorker> logger,
        IAclAdapterDispatcher dispatcher)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _dispatcher = dispatcher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);

                // Backpressure: if batch was full, poll immediately; otherwise sleep
                var delay = processed == 50
                    ? TimeSpan.Zero
                    : TimeSpan.FromSeconds(5);

                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Kubernetes SIGTERM received — exit cleanly
                _logger.LogInformation("OutboxWorker received shutdown signal — exiting gracefully");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxWorker batch failed — backing off 30s before retry");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("OutboxWorker stopped");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OmsDbContext>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // FOR UPDATE SKIP LOCKED:
        //   - Locks selected rows so no competing worker can process them
        //   - SKIP LOCKED means a second replica skips these rows rather than blocking
        //   - Rows are unlocked when the transaction commits or rolls back
        var events = await db.OutboxEvents
            .FromSqlRaw("""
                SELECT * FROM orders.outbox_events
                WHERE processed_at IS NULL
                  AND retry_count < 5
                ORDER BY created_at
                LIMIT 50
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(ct);

        if (events.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return 0;
        }

        foreach (var evt in events)
        {
            try
            {
                // ACL adapter dispatch — must be idempotent (include evt.Id as idempotency key)
                await _dispatcher.DispatchAsync(evt, ct);
                evt.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OutboxWorker: failed to dispatch event {EventId} (type={EventType}), retry_count={RetryCount}",
                    evt.Id, evt.EventType, evt.RetryCount);

                evt.RetryCount++;
                evt.LastError = ex.Message;

                if (evt.RetryCount >= 5)
                {
                    // Move to dead-letter: mark as dead so DLQ alerting fires
                    evt.DeadLetteredAt = DateTime.UtcNow;
                    _logger.LogError(
                        "OutboxWorker: event {EventId} dead-lettered after {RetryCount} failures",
                        evt.Id, evt.RetryCount);
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return events.Count;
    }
}

// -----------------------------------------------------------------------
// ACL Adapter Dispatcher — routes outbox events to the correct adapter
// Each adapter adds X-Idempotency-Key: {eventId} to outbound HTTP calls
// -----------------------------------------------------------------------

public interface IAclAdapterDispatcher
{
    Task DispatchAsync(OutboxEvent evt, CancellationToken ct);
}

public class AclAdapterDispatcher : IAclAdapterDispatcher
{
    private readonly IWmsAdapter _wms;
    private readonly ITmsAdapter _tms;
    private readonly IPosAdapter _pos;
    private readonly IStsAdapter _sts;
    private readonly ILegacyBackendAdapter _legacy;

    public AclAdapterDispatcher(
        IWmsAdapter wms, ITmsAdapter tms, IPosAdapter pos,
        IStsAdapter sts, ILegacyBackendAdapter legacy)
    {
        _wms = wms; _tms = tms; _pos = pos; _sts = sts; _legacy = legacy;
    }

    public Task DispatchAsync(OutboxEvent evt, CancellationToken ct) =>
        evt.TargetSystem switch
        {
            "WMS"           => _wms.HandleAsync(evt, ct),
            "TMS"           => _tms.HandleAsync(evt, ct),
            "POS"           => _pos.HandleAsync(evt, ct),
            "STS"           => _sts.HandleAsync(evt, ct),
            "LegacyBackend" => _legacy.HandleAsync(evt, ct),
            _ => throw new InvalidOperationException(
                     $"Unknown outbox target system: {evt.TargetSystem}")
        };
}

// -----------------------------------------------------------------------
// OutboxEvent entity (EF Core)
// -----------------------------------------------------------------------

public class OutboxEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EventType { get; set; } = null!;
    public string TargetSystem { get; set; } = null!;
    public string Payload { get; set; } = null!;       // JSON
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public DateTime? DeadLetteredAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public string? LastError { get; set; }
}
