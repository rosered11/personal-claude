---
id: S001
slug: async-parallel-db-coordinator
language: csharp
when_to_use: "Use when a service method must call multiple independent DB queries before assembling a response. Fan out with Task.WhenAll + IDbContextFactory — each task gets its own DbContext instance."
related_problems: [P001]
related_decisions: [D001]
source: TA7
---

# Async Parallel DB Coordinator (C# / EF Core)

Coordinator pattern for parallel EF Core queries using `IDbContextFactory<T>`. Each parallel branch gets its own `DbContext` so there are no thread-safety violations. Results are assembled in-memory after `Task.WhenAll` returns.

## When to use

- Hot-path method that makes ≥ 2 independent DB queries sequentially today
- Concurrency is expected (multiple callers at the same time)
- Eager loading is required (Include chains to eliminate lazy-load N+1)

## When NOT to use

- Queries have data dependencies (output of query A feeds query B) — keep those sequential
- Single-threaded background jobs where simplicity matters more than latency
