---
id: D004
chosen_option: "Prometheus Histogram per batch + structured log per record"
problem_id: P004
tags: [etl, observability, prometheus, monitoring, metrics, dotnet, airflow]
related_snippets: []
---

# Decision: ETL Batch Observability — Prometheus Histogram + Structured Logging

## Context

Unattended ETL jobs ran with zero instrumentation. Operators could not distinguish a hung job from a slow batch. No metrics meant no alerting on duration anomalies, no visibility into per-batch record counts, and no post-mortem data after OOM or timeout failures.

## Options Considered

1. **No instrumentation** — zero overhead; completely blind to failures until Airflow task timeout fires.
2. **Airflow task logs only** — visible in UI but not queryable or alertable; no time-series retention.
3. **Prometheus Histogram per batch + structured log (JSON) per record** — queryable, alertable, persisted; slight overhead per batch boundary.

## Decision

Emit a `Histogram` observation at every batch commit: `{batch_number, record_count, duration_ms, heap_mb}`. Write a structured JSON log line per batch at INFO level (visible in Airflow task log). Set alert rules on `p99 batch duration > 30s` and `heap_mb > 2000`. Use the same Prometheus push-gateway pattern already used by other services.

## Consequences

- Operators can detect hung or slow batches within one batch window instead of waiting for Airflow's 30-min timeout.
- OOM failures become diagnosable via `heap_mb` trend across batches.
- Batch metrics feed Grafana dashboards with no additional code.
- Adds ~1ms overhead per batch for metric push — negligible against batch durations of seconds.
