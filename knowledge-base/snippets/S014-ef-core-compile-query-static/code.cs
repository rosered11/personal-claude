// Step 1: Static field at class level — compiled once on first call to the class
// Replace: YourDbContext, YourEntity, YourFilterField
private static readonly Func<YourDbContext, string[], string[], IEnumerable<YourEntity>>
    _bulkQuery = EF.CompileQuery(
        (YourDbContext ctx, string[] ids1, string[] ids2) =>
            ctx.YourEntities
               .AsNoTracking()
               .Include(e => e.NavigationA)
               .Include(e => e.NavigationB).ThenInclude(b => b.ChildC)
               .AsSplitQuery()                              // EF Core 7.0+ required
               .Where(e => ids1.Contains(e.Field1) && ids2.Contains(e.Field2)));

// Step 2: Replace inline query with compiled query call
var results = _bulkQuery(
    _context,
    listOfIds1.ToArray(),
    listOfIds2.ToArray()
).ToList();


// ── Async variant (IAsyncEnumerable<T>) ──────────────────────────────────────
private static readonly Func<YourDbContext, string[], string[], IAsyncEnumerable<YourEntity>>
    _bulkQueryAsync = EF.CompileAsyncQuery(
        (YourDbContext ctx, string[] ids1, string[] ids2) =>
            ctx.YourEntities.AsNoTracking()
               .Include(e => e.NavigationA)
               .AsSplitQuery()
               .Where(e => ids1.Contains(e.Field1)));

// Usage:
var results = new List<YourEntity>();
await foreach (var item in _bulkQueryAsync(_context, ids1.ToArray(), ids2.ToArray()))
    results.Add(item);


// ── Verify impact with dotnet-dump ───────────────────────────────────────────
// dotnet-dump analyze ./Service.dmp
// > dumpheap -stat | grep DynamicMethod
// Before fix: count grows with each unique expression tree compilation
// After fix:  count is stable (one per static field, never grows)
