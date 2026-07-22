---
id: P019
title: "Validate-Service Order Save Failures — SQL Timeout + Duplicate-Key on Retry"
date: 2026-07-13
tags:
  - ef-core
  - mssql
  - kafka
  - command-timeout
  - idempotency
  - duplicate-key
  - integration-events
  - dotnet
related_decisions: [D024]
related_snippets: [S024]
---

# Validate-Service Order Save Failures — SQL Timeout + Duplicate-Key on Retry

## Problem

`validate-service` (Kubernetes-deployed, 15 observed pod replicas, .NET/EF Core, `Validator.API`)
consumes MAO order events from Kafka (`Validator.API.EventHandling.MAO.MAOOrderEventHandler` /
`RawMAOOrderEventHandler` / `MAOEventHandler`, topic `mao.in.fc.order.release.ds`) and persists
them via EF Core (`Order.Infrastructure.OrderContext`) to SQL Server tables `dbo.Order` (unique
constraint `UN_SourceOrderId`) and `dbo.ProcessOrder`. The user reports that saves to some tables
intermittently fail at the moment data arrives, and a manual retry usually succeeds — but the
failure is not constant.

## Root Cause

Grounded in direct log evidence from `inbox/order-issue/logs/*` (15 pod log files, ~19,228 lines
total; only 2 of 15 files contained ERROR-level entries, confirming the intermittent, load/timing-
dependent nature reported by the user):

1. **SQL command timeout under write-path contention.** `log-20260713-jd8dr-copy.log` (lines 44-53,
   76-78) shows: `Failed executing DbCommand ("10,020"ms) ... CommandTimeout='10'` for
   `INSERT INTO [ProcessOrder] (...)`, followed by `DbUpdateException` →
   `SqlException (0x80131904): Execution Timeout Expired` (`Error Number:-2`). The command exceeded
   its 10-second timeout by only ~20ms — a borderline latency spike consistent with lock contention
   or resource pressure at burst arrival, not a fundamentally broken query. The stack trace passes
   through `SqlServerExecutionStrategy.ExecuteAsync`, confirming EF Core's retry-on-failure
   execution strategy is configured, but a bare `-2` (Execution Timeout Expired) is not retried by
   the default transient-error classification, so the very first hit surfaces as a hard failure to
   the caller.

2. **Non-idempotent write + limited retry causes duplicate-key collisions and silent message loss.**
   `log-20260713-qh492-copy.log` (lines 199-327) shows six `DbUpdateException` →
   `SqlException: Violation of UNIQUE KEY constraint 'UN_SourceOrderId'` for the same
   `SourceOrderId` (`CDS2607132611529289`), including two occurrences 26ms apart (08:51:29.217 and
   08:51:29.243), indicating concurrent/overlapping processing of the same order event across pod
   replicas or a retry racing an in-flight insert. Immediately after, `EventBus.Kafka.EventBusKafka`
   logs `Failed to process message ... Attempt=1/1` then `Message skipped after 1 retries` — the
   consumer allows exactly one retry, then silently drops the event with only a log line, no DLQ.

Two ranked, evidence-grounded hypotheses, both directly observed (not mutually exclusive):
- **(1) Primary:** DB write latency spikes past a too-tight 10s `CommandTimeout` under peak
  event-arrival load, causing transient save failures that succeed once contention subsides
  (explains "retry succeeds" for the common case).
- **(2) Compounding:** The persistence path has no idempotency/dedup guard, so at-least-once Kafka
  redelivery or concurrent pod processing can produce a duplicate `INSERT` that violates
  `UN_SourceOrderId`; because the consumer's retry budget is only 1 attempt, if both the original
  and the retry fail, the order event is dropped entirely rather than durably retried (explains why
  "retry succeeds" is not guaranteed in every case).

## Context

- Kubernetes-deployed `validate-service` (`Validator.API`), 15 pod replicas observed in log file
  names (suffixes: 2256n, 227lm, 257m9, 5hdw9, 8d2dt, cjjch, hcd4x, hzxqm, jd8dr, kbgx9, lgnp9,
  qh492, vzkkm, wc4dw, x9fss)
- Consumes MAO order events via `EventBus.Kafka` (topic `mao.in.fc.order.release.ds`), persists via
  EF Core to `Order.Infrastructure.OrderContext` against SQL Server (`dbo.Order`, `dbo.ProcessOrder`)
- Related KB precedent: **P010/D015/S015** addressed a similar reliability incident in the same
  Order bounded context (concurrent running-number race + missing event-consumer idempotency), but
  in `Order.API`'s `CreateOrder`/`ProcessActivity` path — not this `validate-service` MAO-ingestion
  path. The idempotency-key pattern established there (reusing D012/S012) was apparently never
  extended to this consumer.
- [MISSING: SQL Server sizing/DTU or vCore allocation, whether `CommandTimeout=10` is a deliberate
  global default or an accidental one, and SQL Server-side wait-stats/DMV data for the 08:51 burst]

## Constraints

- Must not change the Kafka topic contract or MAO event schema
- Should not require new infrastructure beyond what the team already operates (SQL Server, Kafka)
- Retry/idempotency handling must survive pod restarts and Kafka partition rebalances (multiple pod
  replicas consume concurrently)
- Zero-downtime deployment expected — this is a production order-processing path
