---
id: P004
title: "MySQL ETL Sync — Zero Observability on Batch Resource Consumption"
date: 2026-04-08
tags: [etl, observability, prometheus, monitoring, dotnet, batch-processing, alerting]
severity: medium
related_decisions: [D004]
related_snippets: [S005]
---

# MySQL ETL Sync — Zero Observability on Batch Resource Consumption

## Problem

After the I3 fix (per-batch commit), the ETL job runs without timeout failures but provides zero metrics on per-batch resource consumption: TX hold time, memory allocation, staging read latency, records throughput. If batch latency drifts (data volume growth, index degradation, connection pool contention), there is no early warning — the next symptom will be another timeout incident.

## Root Cause

`ProcessSyncLoopAsync()` in `product.cs` had no instrumentation beyond basic log messages. No Prometheus metrics, no Stopwatch per batch, no GC tracking. Observability gap: "a fix without measurement is anecdotal" — the per-batch commit fix eliminated the timeout but left the system blind to latency regression.

## Constraints

- ETL runs unattended on Airflow — alerting is non-negotiable
- Instrumentation overhead must be < 0.1% of batch duration (~0.12ms per batch is acceptable)
- Stack: .NET 8, Prometheus-net already in use

## Affected Components

- `ProcessSyncLoopAsync()` in `product.cs`
- `SyncProductMasterJda` Airflow DAG
- Grafana / Prometheus alerting stack
