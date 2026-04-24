// =============================================================================
// S015: MSSQL SEQUENCE + Idempotency Guard + Null Safety for Order Service
// =============================================================================
// Fixes P010: DbUpdateConcurrencyException storm, duplicate integration events,
// NullReferenceException in domain functions.
// See D015 for full rationale.
// =============================================================================

// -----------------------------------------------------------------------------
// PRIORITY 1: SQL Migration
// Run once before deploying the updated GenRunningNumberFunction.
// -----------------------------------------------------------------------------
// SQL:
// CREATE SEQUENCE dbo.OrderRunningNumberSeq
//     AS BIGINT
//     START WITH 1
//     INCREMENT BY 1
//     NO CACHE;        -- NO CACHE ensures no gaps on pod restart; use CACHE n for throughput if gaps are acceptable
//
// For per-prefix sequences (e.g. ORD, SHP):
// CREATE SEQUENCE dbo.OrderFrontRunningNumberSeq AS BIGINT START WITH 1 INCREMENT BY 1 NO CACHE;
// CREATE SEQUENCE dbo.ShipmentRunningNumberSeq   AS BIGINT START WITH 1 INCREMENT BY 1 NO CACHE;

// -----------------------------------------------------------------------------
// PRIORITY 1: Replace GenRunningNumberFunction retry loop
// Before: SaveChanges + retry on DbUpdateConcurrencyException (storm-prone)
// After:  Single atomic SEQUENCE call, no retry, no concurrency exception
// -----------------------------------------------------------------------------
public class GenRunningNumberFunction
{
    private readonly IDbContextFactory<OrderDbContext> _contextFactory;

    public GenRunningNumberFunction(IDbContextFactory<OrderDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns the next running number atomically using MSSQL SEQUENCE.
    /// No optimistic concurrency, no retry loop, safe for N concurrent pods.
    /// </summary>
    public async Task<long> GenNextRunningNumberAsync(
        string sequenceName,
        CancellationToken ct = default)
    {
        // Validate sequence name to prevent SQL injection
        if (!System.Text.RegularExpressions.Regex.IsMatch(sequenceName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException($"Invalid sequence name: {sequenceName}");

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var sql = FormattableStringFactory.Create(
            $"SELECT NEXT VALUE FOR dbo.{sequenceName}");
        return await ctx.Database.ExecuteScalarAsync<long>(sql.ToString(), ct);
    }
}

// -----------------------------------------------------------------------------
// PRIORITY 2: Idempotency guard for integration event consumers
// Pattern reused from D012/S012. Apply to:
//   - ProcessOrderStartIntegrationEventHandler
//   - ProcessOrderItemUpdateIntegrationEventHandler
// -----------------------------------------------------------------------------
public class ProcessOrderStartIntegrationEventHandler
{
    private readonly OrderDbContext _db;
    private readonly ILogger<ProcessOrderStartIntegrationEventHandler> _logger;

    public ProcessOrderStartIntegrationEventHandler(
        OrderDbContext db,
        ILogger<ProcessOrderStartIntegrationEventHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(ProcessOrderStartIntegrationEvent evt, CancellationToken ct)
    {
        // Idempotency check: skip if already processed
        // processed_events table: (correlation_id NVARCHAR(200) PK, processed_at DATETIMEOFFSET)
        bool alreadyProcessed = await _db.ProcessedEvents
            .AnyAsync(e => e.CorrelationId == evt.CorrelationId, ct);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "ProcessActivityStartEvent on [{CorrelationId}] already processed — skipping",
                evt.CorrelationId);
            return;
        }

        // Mark as processed BEFORE side effects (within same transaction)
        _db.ProcessedEvents.Add(new ProcessedEventRecord
        {
            CorrelationId = evt.CorrelationId,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct); // will throw on duplicate key if concurrent — safe to ignore/retry

        // ... actual handler logic
    }
}

// -----------------------------------------------------------------------------
// PRIORITY 3: Null guards in domain functions
// Fixes NullReferenceException in StandardActivityCreateBySubOrderFunction and
// ProcessOrderItemUpdateIntegrationEventHandler.
// -----------------------------------------------------------------------------

// In StandardActivityCreateBySubOrderFunction.cs, line ~44 (before current line 45):
public ActivityResult Execute(StandardActivityCreateInfo input)
{
    ArgumentNullException.ThrowIfNull(input, nameof(input));
    ArgumentNullException.ThrowIfNull(input.SubOrder, nameof(input.SubOrder));
    // ... existing logic (was throwing NullReferenceException at line 45)
}

// In ProcessOrderItemUpdateIntegrationEventHandler.cs, line ~171 (before current line 172):
public async Task Handle(ProcessOrderItemUpdateIntegrationEvent evt)
{
    if (evt?.OrderItem == null)
        throw new ArgumentException("OrderItem is required on ProcessOrderItemUpdateIntegrationEvent", nameof(evt));
    // ... existing logic (was throwing NullReferenceException at line 172)
}

// -----------------------------------------------------------------------------
// PRIORITY 4 (Sprint): API-level idempotency key on CreateOrder
// Prevents duplicate order submissions on client retry after network error.
// -----------------------------------------------------------------------------
[HttpPost("orders")]
public async Task<IActionResult> CreateOrder(
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    [FromBody] CreateOrderRequest request,
    CancellationToken ct)
{
    if (!string.IsNullOrEmpty(idempotencyKey))
    {
        var cached = await _idempotencyCache.GetAsync<CreateOrderResponse>(idempotencyKey, ct);
        if (cached != null)
            return Ok(cached); // Return cached result for duplicate submission
    }

    var result = await _mediator.Send(request, ct);

    if (!string.IsNullOrEmpty(idempotencyKey))
    {
        // Cache with TTL appropriate for order creation (e.g., 24 hours)
        await _idempotencyCache.SetAsync(idempotencyKey, result,
            TimeSpan.FromHours(24), ct);
    }

    return Ok(result);
}

// -----------------------------------------------------------------------------
// PRIORITY 5 (Sprint): Fix EF Core decimal precision warnings in OnModelCreating
// Prevents silent data truncation on InsuranceMinimumAmount, PackQuantity, etc.
// -----------------------------------------------------------------------------
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<SysInsuranceConfigurationModel>()
        .Property(e => e.InsuranceMinimumAmount)
        .HasPrecision(18, 4);

    modelBuilder.Entity<OrderItemModel>()
        .Property(e => e.PackQuantity)
        .HasPrecision(18, 4);

    modelBuilder.Entity<PackageTbModel>()
        .Property(e => e.PackageWeight)
        .HasPrecision(18, 4);

    modelBuilder.Entity<PackageTbModel>()
        .Property(e => e.Qty)
        .HasPrecision(18, 4);

    modelBuilder.Entity<SubOrderItemModel>()
        .Property(e => e.PackQuantity)
        .HasPrecision(18, 4);

    // Fix MultipleCollectionIncludeWarning: use SplitQuery on multi-collection navigations
    modelBuilder.Entity<OrderModel>()
        .Navigation(o => o.Items)
        .AutoInclude();
    // Or configure globally:
    // optionsBuilder.UseSqlServer(connectionString,
    //     o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
}
