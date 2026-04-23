---
name: EF Core Patterns
description: Established EF Core patterns from K25-K28 and production incidents — use these as defaults when writing C# EF Core code
type: project
---

# EF Core Established Patterns

From K25–K28, incidents I1/I5/I8, and decisions D001/D005/D008.

---

## IDbContextFactory for Parallel Queries (K25)

**Rule:** 1 parallel task = 1 DbContext. Never share a DbContext across concurrent tasks.

```csharp
// Register as Singleton
services.AddDbContextFactory<AppDbContext>(options => ...);

// Use per parallel branch
await using var ctx1 = _contextFactory.CreateDbContext();
await using var ctx2 = _contextFactory.CreateDbContext();
await Task.WhenAll(
    GetOrderHeaderAsync(ctx1, id),
    GetOrderPaymentsAsync(ctx2, id)
);
```

**Why:** EF Core DbContext is not thread-safe. Sharing across threads causes race conditions in the change tracker. IDbContextFactory creates isolated contexts on demand.

---

## EF.CompileQuery as Static Field (K28)

**Rule:** Apply `EF.CompileQuery` to any hot-path query called > 100/sec. Define as `static readonly` field.

```csharp
private static readonly Func<AppDbContext, string[], IEnumerable<OrderModel>>
    _query = EF.CompileQuery(
        (AppDbContext ctx, string[] ids) =>
            ctx.Orders.AsNoTracking()
               .Include(o => o.Customer)
               .AsSplitQuery()
               .Where(o => ids.Contains(o.SourceId)));
```

**Why:** Without `CompileQuery`, EF re-compiles the expression tree on every call, accumulating non-reclaimable `DynamicMethod` objects. `DynamicMethod` count > 2000 = apply this pattern.

Diagnosis: `dotnet-dump analyze` → `dumpheap -stat | grep DynamicMethod`

---

## AsNoTracking for Read-Only Queries

**Rule:** Add `.AsNoTracking()` to every read-only query. Remove it only when you need to UPDATE the entity in the same DbContext scope.

**Why:** Without `AsNoTracking`, EF adds the entity to the ChangeTracker, consuming extra memory (entity + snapshot copy). Confirmed: reduces `SubOrderMessageViewModel` count by 94% in heap dump.

---

## Include Chains Over Lazy Loading (K25)

**Rule:** Never use navigation property lazy loading in high-throughput code. Always use `.Include()` chains on hot-path queries.

**Why:** Lazy loading fires a separate DB query per navigation property access — this is the definition of N+1. Include chains load all needed data in a single query (or two with `AsSplitQuery`).

---

## ChangeTracker.Clear() After Batch Commit (K32)

**Rule:** Call `context.ChangeTracker.Clear()` immediately after `tx.CommitAsync()` in every batch loop iteration.

```csharp
await tx.CommitAsync(ct);
context.ChangeTracker.Clear();    // detach committed entities
activityTracking.Clear();         // flush per-batch dictionaries
```

**Why:** After `CommitAsync`, entities remain tracked for the next `SaveChangesAsync`. In a batch loop with a long-lived DbContext, this accumulates millions of tracked objects — predictable OOM by batch 4–5 at BatchSize=10K.

---

## Batch Query (WHERE IN) Over N+1

**Rule:** When a loop requires data for N items, pre-load all N items in a single `WHERE IN` query before the loop.

```csharp
// Anti-pattern — N queries
foreach (var header in headers)
    var items = await context.Items.Where(i => i.OrderNo == header.OrderNo).ToListAsync();

// Pattern — 1 query
var orderNos = new HashSet<string>(headers.Select(h => h.OrderNo));
var itemDict = await context.Items
    .Where(i => orderNos.Contains(i.OrderNo))
    .GroupBy(i => i.OrderNo)
    .ToDictionaryAsync(g => g.Key, g => g.ToList());
```

**Why:** N+1 queries at N=500 with 20ms each = 10,000ms total. Single WHERE IN query at any N = ~20ms.
