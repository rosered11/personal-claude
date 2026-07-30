---
name: ef-core-stacked-retry-amplification-pattern
description: Recurring EF Core/.NET anti-pattern to check for in any consultation touching EF Core + SQL Server write paths -- combining EnableRetryOnFailure with a custom app-level retry loop around SaveChangesAsync
type: project
---

Discovered while grounding P021/D026 (activity-service, 2026-07-22) directly in the real source
tree (`Activity.API/Startup.cs` + `ActivityGenerator.cs`), not just the log: the service configured
EF Core's built-in `EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), errorNumbersToAdd: null)`
execution strategy AND separately wrapped `SaveChangesAsync()` in an application-level
`catch (DbException)` retry loop (`DoTransactionAsyncWithRetryPolicy`, also 3 retries, no backoff).
Because the EF Core execution strategy already retries transient failures internally before
`SaveChangesAsync` throws, the outer loop only fires after EF Core's own attempts are exhausted --
so a single failing write can be resubmitted, identically, up to 3 x 3 = 9 times against the same
CommandTimeout ceiling, worsening exactly the contention/latency that caused the first failure
instead of backing off from it.

**Why:** This is not a one-off bug -- it is a structurally easy mistake to make in any .NET/EF Core
codebase that (a) uses `EnableRetryOnFailure` (a common, often copy-pasted default) and (b) also has
a pre-existing custom retry wrapper around a database call (equally common, especially in codebases
that predate the EF Core execution-strategy feature or that added `EnableRetryOnFailure` later
without auditing existing retry wrappers). The log symptom (a hard timeout at exactly the configured
`CommandTimeout` value, not a marginal overshoot) is a useful tell that retry amplification may be
in play, but confirming it requires reading the actual DI configuration and the call site, not just
the log -- log-only analysis would have stopped at "batch too big," missing the compounding cause.

**How to apply:** For any new consultation involving EF Core + SQL Server (or any provider with a
configurable execution strategy) where the symptom is a `DbUpdateException` / `SqlException`
timeout, always check: (1) is `EnableRetryOnFailure` (or equivalent) configured in the DI/Startup
code, and (2) is `SaveChangesAsync` (or the surrounding unit of work) also wrapped in a custom
try/catch retry loop by application code. If both are true, flag retry-stacking explicitly as a
root-cause contributor, not just the raw batch size or timeout value -- the fix should collapse to a
single retry authority (prefer the execution-strategy-owned retry via
`context.Database.CreateExecutionStrategy().ExecuteAsync(...)`, removing the ad hoc loop) rather than
just tuning the timeout or batch size in isolation. See P021/D026/S026 for the reference fix pattern
(bounded two-pass chunking + execution-strategy-only retry + message-bus-owned redelivery as the
final backstop).
