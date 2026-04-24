---
when_to_use: >
  Use when a .NET/EF Core service running multiple replicas concurrently generates sequential identifiers using optimistic concurrency retry loops (DbUpdateConcurrencyException storms). Replace with MSSQL SEQUENCE for atomic, contention-free number generation. Use the idempotency guard when integration event consumers must be safe against at-least-once delivery redelivery across pod instances.
related_problems:
  - P010
related_decisions:
  - D015
  - D012
language: csharp
---

## Context

These patterns fix three interrelated failures observed in a multi-pod .NET order service:

1. `DbUpdateConcurrencyException` storm in running-number generation — fixed by MSSQL SEQUENCE
2. `ProcessActivityStart already started by another process` — fixed by idempotency key check
3. `NullReferenceException` in domain function and event handler — fixed by null guards

The idempotency guard reuses the `processed_events` table approach from S012. The SEQUENCE replaces any EF Core optimistic concurrency retry loop on a counter row.
