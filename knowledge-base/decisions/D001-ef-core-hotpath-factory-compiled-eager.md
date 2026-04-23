---
id: D001
chosen_option: "IDbContextFactory + EF.CompileQuery + eager loading"
problem_id: P001
tags: [ef-core, dotnet, n+1, performance, connection-pool, compiled-query]
related_snippets: [S001, S014]
---

# Decision: EF Core Hot-Path — IDbContextFactory + Compiled Queries + Eager Loading

## Context

`GetSubOrderAsync` ran N+1 queries: one per order header to fetch line items via lazy navigation property. Under concurrency, 5 tasks sharing one DbContext caused thread-safety violations; each query also re-compiled the expression tree on every call, adding overhead at scale.

## Options Considered

1. **Single shared DbContext + lazy loading** — simple but thread-unsafe; N+1 persists.
2. **Scoped DbContext per call + Include()** — thread-safe, eliminates N+1, but expression tree compiled each call.
3. **IDbContextFactory + EF.CompileQuery static field + Include()** — thread-safe, no N+1, zero repeated compilation overhead.

## Decision

Use `IDbContextFactory<TContext>` to create a short-lived DbContext per parallel unit of work. Define `EF.CompileQuery(...)` as a `static readonly` field so the expression tree is compiled exactly once at class load. Use `.Include()` chains on all hot-path queries to eliminate lazy-loading N+1.

## Consequences

- Latency reduced 5,048ms → 741ms (6.8× improvement) under 5-way concurrency.
- `DynamicMethod` accumulation eliminated — compiled query reused across all calls.
- Short-lived DbContexts prevent cross-request state leakage.
- Slightly more boilerplate: factory injection and static compile-query declarations.
