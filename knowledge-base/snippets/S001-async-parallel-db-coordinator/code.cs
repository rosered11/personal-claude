// Register in Program.cs
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Service constructor
private readonly IDbContextFactory<AppDbContext> _contextFactory;

// Async coordinator
public async Task<Result> GetSubOrderAsync(string orderId, string subOrderId)
{
    // Step 1: Serial prerequisites
    var subOrders = GetSubOrderMessage(orderId, subOrderId);
    string resolvedId = ResolveOnce(orderId);

    // Step 2: Parallel independent DB calls — each with own DbContext
    await using var ctx1 = _contextFactory.CreateDbContext();
    await using var ctx2 = _contextFactory.CreateDbContext();
    await using var ctx3 = _contextFactory.CreateDbContext();
    await using var ctx4 = _contextFactory.CreateDbContext();

    await Task.WhenAll(
        GetOrderHeaderAsync(ctx1, resolvedId),
        GetOrderPaymentsAsync(ctx2, resolvedId),
        GetOrderPromotionAsync(ctx3, resolvedId),
        GetRewardItemsBatchedAsync(ctx4, resolvedId, subOrders)
    );

    // Step 3: Assemble in memory — zero DB calls
}

// Private async method owns its context
private async Task<OrderModel> GetOrderHeaderAsync(DbContext ctx, string id)
{
    return await ctx.Set<OrderModel>()
        .AsNoTracking()
        .Include(o => o.Customer)
        .Where(o => o.SourceOrderId == id)
        .FirstOrDefaultAsync();
}
