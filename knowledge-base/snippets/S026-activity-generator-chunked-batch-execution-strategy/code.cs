// Activity.API.Applications.Generators.ActivityGenerator (excerpt)
// Chunked two-pass persistence + single execution-strategy retry.
// Replaces one unbounded SaveChangesAsync() call (25 entities / 331 params observed in production,
// batched by EF Core into two oversized MERGE statements that exceeded CommandTimeout=30) and
// removes the ad hoc DoTransactionAsyncWithRetryPolicy loop that stacked on top of EF Core's own
// EnableRetryOnFailure(3, 5s), which together could resubmit the identical oversized command up to
// 3 x 3 = 9 times. See P021 / D026.

private const int ProcessActivityChunkSize = 5;
private const int ProcessActivityDependencyChunkSize = 5;

/// <summary>
/// Persists generated ProcessActivity and ProcessActivityDependency rows using bounded,
/// two-pass chunked SaveChanges calls (parents, then FK-dependent children), reusing the
/// FK-safe batch-commit pattern from D008/S008. Each chunk is executed through the DbContext's
/// configured execution strategy so EF Core's EnableRetryOnFailure is the single source of
/// retry truth -- no separate application-level retry loop wraps this call.
/// </summary>
public async Task<List<ProcessActivityViewModel>> SaveProcessActivityV2ChunkedAsync()
{
    var autoStartProcessActivitys = new List<ProcessActivityViewModel>();

    // Pass 1: ProcessActivity rows, chunked. Each chunk is its own bounded SaveChangesAsync,
    // executed via the DbContext's own execution strategy (owns EF Core's EnableRetryOnFailure).
    foreach (var chunk in _pendingProcessActivityModels.Chunk(ProcessActivityChunkSize))
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ProcessActivity.AddRange(chunk);
            // Idempotent by design: MERGE ... WHEN NOT MATCHED means a re-run of this chunk
            // after a transient failure does not produce duplicate rows.
            await _context.SaveChangesAsync();
        });
    }

    // Pass 2: ProcessActivityDependency rows, chunked -- only after Pass 1 has committed, so
    // every DependencyProcessActivityCode / ProcessActivityCode FK reference is guaranteed to
    // already exist (same ordering guarantee as D008/S008's two-pass FK-safe commit).
    foreach (var chunk in _pendingProcessActivityDependencyModels.Chunk(ProcessActivityDependencyChunkSize))
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _context.ProcessActivityDependency.AddRange(chunk);
            await _context.SaveChangesAsync();
        });
    }

    // NOTE: no catch/retry block here. If a chunk's execution strategy exhausts its own
    // retries (EnableRetryOnFailure(3, 5s), configured once in Startup.cs), the exception
    // propagates to ActivityCreateV2EventHandler.Handle(...) and up to the RabbitMQ consumer,
    // whose existing nack/redelivery handling (see P016/D021) is the intended single backstop --
    // deliberately not re-implemented here as a second retry loop.
    return autoStartProcessActivitys;
}

// DI configuration reminder (Activity.API/Startup.cs) -- unchanged, this is the ONLY retry
// policy that should apply to SaveChangesAsync after this change:
//
// options.UseSqlServer(connectionString, sqlOptions =>
// {
//     sqlOptions.CommandTimeout(30);
//     sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
// });
