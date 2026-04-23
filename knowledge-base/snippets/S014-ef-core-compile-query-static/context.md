---
id: S014
slug: ef-core-compile-query-static
language: csharp
when_to_use: "Apply to any EF Core query called > 100 times per second (hot path). EF compiles each unique query expression into a DynamicMethod on first call; without CompileQuery, high-throughput services accumulate thousands of DynamicMethod objects in heap. Define as static readonly field — compiled once at class load, reused for every call."
related_problems: [P001]
related_decisions: [D001]
source: TA15
---

# EF.CompileQuery Static Field Template (C# / EF Core 7+)

Eliminates `DynamicMethod` accumulation in the EF `CompiledQueryCache`. Without this, a hot-path query that runs 1000 times/minute generates 1000 `DynamicMethod` objects in the GC heap (confirmed via `dotnet-dump` + `dumpheap -type DynamicMethod`).

## Diagnostic signal

Run `dotnet-dump analyze` and check:
```
dumpheap -stat | grep DynamicMethod
```
If count grows unboundedly with load → apply `EF.CompileQuery` to the hot-path query.

## Constraints

- `AsSplitQuery()` requires EF Core 7.0+
- Async variant uses `EF.CompileAsyncQuery` → returns `IAsyncEnumerable<T>`
- Parameters are limited: EF.CompileQuery supports up to 8 parameters; use arrays for multi-value filters
- Static field is per-class — different DbContext types need separate compiled query fields
