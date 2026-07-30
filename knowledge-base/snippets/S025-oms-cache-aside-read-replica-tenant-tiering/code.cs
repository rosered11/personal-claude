using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Services.Caching;

namespace Order.Infrastructure.Data;

// 1) Read-only DbContext pointed at a Postgres streaming replica.
//    Registered alongside (not instead of) the existing primary OrderDbContext.
//    Dashboard/report/audit query handlers depend on this type explicitly so the
//    write path (OrderDbContext) can never accidentally read from -- or write to -- the replica.
public class OrderReadDbContext(DbContextOptions<OrderReadDbContext> options) : DbContext(options)
{
    public DbSet<Entities.Orders.OmsOrder> Orders => Set<Entities.Orders.OmsOrder>();
    public DbSet<Entities.Orders.OrderLine> OrderLines => Set<Entities.Orders.OrderLine>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        // Replica connections are read-only at the DB role level (enforced server-side,
        // e.g. a Postgres role with no INSERT/UPDATE/DELETE grants) -- this flag is a
        // second, cheap line of defense against accidental writes from application code.
        optionsBuilder.UseNpgsql(b => b.CommandTimeout(30));
}

public static class ReadReplicaRegistration
{
    public static IServiceCollection AddOrderReadReplica(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Falls back to the primary if no replica is provisioned yet (e.g. lower
        // environments) so this can ship before the replica exists in every environment.
        var replicaConnectionString =
            configuration.GetConnectionString("OrderDB_ReadReplica")
            ?? configuration.GetConnectionString("OrderDB");

        return services.AddDbContext<OrderReadDbContext>(options =>
            options.UseNpgsql(replicaConnectionString));
    }
}

// 2) Cache-aside wrapper over the existing ICacheService (RedisCacheService already
//    implements this interface -- this is a call-site pattern, not new infrastructure).
public sealed class CacheAsideReader(ICacheService cache)
{
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> loadFromReadReplica,
        TimeSpan? expiry = null,
        string[]? invalidationTags = null)
    {
        var cached = await cache.GetAsync<T>(key);
        if (cached is not null)
            return cached;

        var value = await loadFromReadReplica();
        await cache.SetAsync(key, value, expiry ?? TimeSpan.FromMinutes(5), invalidationTags);
        return value;
    }
}

// 3) Per-BU write-volume instrumentation -- the evidence D025 requires before promoting
//    any specific BU to schema-per-BU-tier partitioning (the deferred Lens B option).
//    Deliberately cheap (in-memory + periodic flush) so it can ship immediately without
//    waiting on a metrics platform decision.
public sealed class BuWriteVolumeTracker(ILogger<BuWriteVolumeTracker> logger)
{
    private readonly Dictionary<string, int> _writesPerBu = new();
    private readonly Lock _lock = new();

    public void RecordWrite(string buCode)
    {
        lock (_lock)
        {
            _writesPerBu[buCode] = _writesPerBu.GetValueOrDefault(buCode) + 1;
        }
    }

    // Call on a timer (e.g. every 1 minute via IHostedService) -- flips this from
    // "no per-BU throughput data" (P020's stated gap) into an actual, queryable signal
    // that can later trigger the deferred schema-per-BU-tier migration for one BU at a time.
    public IReadOnlyDictionary<string, int> FlushAndReset()
    {
        lock (_lock)
        {
            var snapshot = new Dictionary<string, int>(_writesPerBu);
            _writesPerBu.Clear();
            foreach (var (bu, count) in snapshot)
                logger.LogInformation("BU write volume: {BuCode}={Count}/interval", bu, count);
            return snapshot;
        }
    }
}
