// ── Static Prometheus field declarations (add to sync class) ─────────────────
private static readonly Histogram BatchDuration = Metrics
    .CreateHistogram("etl_sync_batch_duration_seconds",
        "Per-batch TX hold time (staging read excluded)",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 10), // 0.1s → 51.2s
            LabelNames = new[] { "sync_name", "business_unit" }
        });

private static readonly Counter RecordsProcessed = Metrics
    .CreateCounter("etl_sync_records_processed_total",
        "Cumulative records committed",
        new CounterConfiguration { LabelNames = new[] { "sync_name", "business_unit" } });

private static readonly Gauge CurrentBatchRound = Metrics
    .CreateGauge("etl_sync_current_batch_round",
        "Current batch round number (resets per job run)",
        new GaugeConfiguration { LabelNames = new[] { "sync_name", "business_unit" } });

private static readonly Histogram StagingReadDuration = Metrics
    .CreateHistogram("etl_sync_staging_read_seconds",
        "Per-batch staging read duration",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.05, 2, 8), // 50ms → 6.4s
            LabelNames = new[] { "sync_name", "business_unit" }
        });

private static readonly Summary BatchMemoryAlloc = Metrics
    .CreateSummary("etl_sync_batch_alloc_bytes",
        "GC allocation per batch",
        new SummaryConfiguration { LabelNames = new[] { "sync_name", "business_unit" } });


// ── Per-batch instrumentation block (inside while loop, after ReadBatch) ─────
var labels = new[] { SyncName, businessUnit.ToString() };

var readSw = Stopwatch.StartNew();
var productStagings = await GetProductStaging(lastProcessedId, cancellationToken);
readSw.Stop();
StagingReadDuration.WithLabels(labels).Observe(readSw.Elapsed.TotalSeconds);

if (productStagings.Count == 0) break;

long gcBefore = GC.GetTotalAllocatedBytes(precise: false);
var batchSw = Stopwatch.StartNew();

await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);
try
{
    await SyncProductMasterAsync(productStagings, productMasterActivityTracking, cancellationToken);
    await tx.CommitAsync(cancellationToken);

    batchSw.Stop();
    long gcAfter = GC.GetTotalAllocatedBytes(precise: false);

    BatchDuration.WithLabels(labels).Observe(batchSw.Elapsed.TotalSeconds);
    RecordsProcessed.WithLabels(labels).Inc(productStagings.Count);
    CurrentBatchRound.WithLabels(labels).Set(round);
    BatchMemoryAlloc.WithLabels(labels).Observe(gcAfter - gcBefore);

    // CRITICAL: prevent linear heap growth
    context.ChangeTracker.Clear();
    productMasterActivityTracking.Clear();

    logger.LogInformation(
        "[{SyncName}] Batch {Round}: {Count} records, TX {TxMs}ms, read {ReadMs}ms, alloc {AllocMB:F1}MB",
        SyncName, round, productStagings.Count,
        batchSw.ElapsedMilliseconds, readSw.ElapsedMilliseconds,
        (gcAfter - gcBefore) / 1_048_576.0);
    round++;
}
catch (Exception ex)
{
    batchSw.Stop();
    logger.LogError(ex, "[{SyncName}] Batch {Round} FAILED after {TxMs}ms", SyncName, round, batchSw.ElapsedMilliseconds);
    try { await tx.RollbackAsync(cancellationToken); } catch { }
    throw;
}
