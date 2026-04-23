// Program.cs / DI setup — add CommandTimeout as safety net
services.AddDbContext<DbSpcProductContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        o => o.CommandTimeout(120)));   // 2-min per-statement ceiling

protected override async Task<long> ProcessSyncLoopAsync(
    long startingId,
    Dictionary<string, ProductMasterActivity> productMasterActivityTracking,
    CancellationToken cancellationToken)
{
    var hasData = await CheckPendingAsync(startingId, cancellationToken);
    if (!hasData)
    {
        logger.LogWarning("No pending data for {SyncName}", SyncName);
        return startingId;
    }

    // Polly: retry transient DB errors per batch (exponential backoff: 2s, 4s, 8s)
    var retryPolicy = Policy
        .Handle<MySqlException>(ex => IsTransient(ex))
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
            onRetry: (ex, delay, attempt, _) =>
                logger.LogWarning(ex, "Batch {Round} retry {Attempt} after {Delay}s",
                    round, attempt, delay.TotalSeconds));

    // Polly: hard timeout per batch (60s ceiling)
    var timeoutPolicy = Policy.TimeoutAsync(60, TimeoutStrategy.Optimistic);
    var batchPolicy = Policy.WrapAsync(retryPolicy, timeoutPolicy);

    long lastProcessedId = startingId;
    int round = 1;

    while (true)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read batch BEFORE opening TX — minimizes TX hold to write-only duration (~200ms)
        var productStagings = await GetProductStaging(lastProcessedId, cancellationToken);
        if (productStagings.Count == 0)
        {
            logger.LogInformation("End of batch for {SyncName}", SyncName);
            break;
        }

        await batchPolicy.ExecuteAsync(async ct =>
        {
            // Per-batch TX: hold time ≈ insert duration only
            await using var tx = await context.Database.BeginTransactionAsync(ct);
            try
            {
                await SyncProductMasterAsync(productStagings, productMasterActivityTracking, ct);
                await tx.CommitAsync(ct);

                // Advance cursor AFTER commit — safe: if commit fails, cursor stays back
                lastProcessedId = productStagings.Last().Id;
                logger.LogInformation("[{SyncName}] Batch {Round}: {Count} records committed",
                    SyncName, round, productStagings.Count);
                round++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{SyncName}] Batch {Round} FAILED — rolling back", SyncName, round);
                try { await tx.RollbackAsync(ct); }
                catch (Exception rbEx)
                {
                    logger.LogError(rbEx, "Rollback failed for batch {Round}", round);
                }
                throw; // rethrow for Polly retry
            }
        }, cancellationToken);

        // Persist staging state per-batch, after production commit
        await stagingContext.SaveChangesAsync(cancellationToken);
    }

    return lastProcessedId;
}

private static bool IsTransient(MySqlException ex) => ex.Number is
    1205 or  // ER_LOCK_WAIT_TIMEOUT
    1213 or  // ER_LOCK_DEADLOCK
    2006 or  // CR_SERVER_GONE_ERROR
    2013;    // CR_SERVER_LOST
