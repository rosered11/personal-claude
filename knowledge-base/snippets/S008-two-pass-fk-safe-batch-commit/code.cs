// ── Pre-materialize master lookups (2 queries, no N+1 inside foreach) ────────
HashSet<string> orderNos = new(orderHeaders.Select(x => x.OrderNo));
var headerMasterDict = await context.OrderOutboundTb
    .Where(x => orderNos.Contains(x.OrderNo))
    .ToDictionaryAsync(x => x.OrderNo, cancellationToken);
var itemMasterDict = await context.OrderOutboundItemTb
    .Where(x => orderNos.Contains(x.OrderNo))
    .GroupBy(x => x.OrderNo)
    .ToDictionaryAsync(x => x.Key, x => x.ToList(), cancellationToken);

await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);
try
{
    // ── Pass 1: save parents → DB generates headerActivity.Id ────────────────
    var headerState = new Dictionary<string, (OrderOutboundActivityTb Activity, OrderOutboundTb Order)>();
    foreach (var header in orderHeaders)
    {
        var headerActivity = BuildHeaderActivity(header);
        var order = BuildOrUpdateOrder(header, headerMasterDict);
        context.OrderOutboundActivityTb.Add(headerActivity);
        headerState[header.OrderNo] = (headerActivity, order);
    }
    await context.SaveChangesAsync(cancellationToken);  // ← headerActivity.Id now a real DB value

    // ── Pass 2: build children using populated parent IDs, save once ──────────
    var activityTracking = new Dictionary<string, OrderOutboundItemActivityTb>();
    foreach (var header in orderHeaders)
    {
        if (!headerState.TryGetValue(header.OrderNo, out var state)) continue;
        var orderItems = itemMasterDict.TryGetValue(header.OrderNo, out var items) ? items : [];
        CollectOrderActivities(header, activityTracking, state.Order, orderItems, state.Activity);
    }

    if (activityTracking.Count > 0)
    {
        context.OrderOutboundItemActivityTb.AddRange(activityTracking.Values);
        activityTracking.Clear();
    }
    await context.SaveChangesAsync(cancellationToken);  // ← all items in one save

    lastProcessedId = orderHeaders.Last().Id;
    await tx.CommitAsync(cancellationToken);
    context.ChangeTracker.Clear();
}
catch (Exception ex)
{
    logger.LogError(ex, "[{SyncName}] Batch failed — rolling back", SyncName);
    await tx.RollbackAsync(cancellationToken);
    throw;
}
