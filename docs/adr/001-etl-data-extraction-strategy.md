# ADR-001: ETL Data Extraction Strategy — Polling vs Event-Driven

- **Status:** Accepted
- **Date:** 2026-04-23
- **Domain:** Supply Chain Platform — ETL Service

---

## Context

We are building an ETL service that extracts data from an internal database, transforms it, and loads it into a data warehouse and reporting database.

Key constraints:
- The source system (internal database) does not publish events — it only supports polling
- Data freshness requirement is **batch** (not real-time or near-real-time)
- The platform already has Kafka infrastructure in use by other services
- Destination systems are a data warehouse and a reporting DB, both optimized for bulk writes

The core question is: should we build this ETL as a **scheduled polling pipeline** or architect it around **event-driven ingestion via Kafka**?

---

## Options Considered

### Option A: Scheduled Batch Polling (Incremental Watermark)

A scheduler (e.g., cron, Airflow, or a simple Go/Python job) runs on a fixed interval, queries the source DB for records changed since the last successful run (using a `updated_at` watermark or sequence ID), transforms, and bulk-loads into the destination.

**Pros:**
- Matches the source system's capability — polling is the only interface available
- Simple to reason about: one job, one run, one outcome
- Bulk writes to the data warehouse are more efficient than streaming inserts
- Failure recovery is straightforward: re-run from the last committed watermark
- No additional infrastructure needed (avoids adding Kafka dependency to this pipeline)

**Cons:**
- Latency is bounded by the polling interval (acceptable given batch requirement)
- Watermark-based extraction requires source tables to have a reliable `updated_at` or CDC mechanism; hard deletes are invisible without tombstone records or soft deletes

### Option B: Event-Driven via Kafka

Use Change Data Capture (CDC) tooling (e.g., Debezium) to capture database changes and publish them to Kafka topics. A consumer service reads events and writes to the destination.

**Pros:**
- Captures all change types including hard deletes
- Enables near-real-time data freshness if needed in the future
- Decouples extraction from loading

**Cons:**
- The source system does not natively publish events — requires Debezium or equivalent running against the source DB, adding operational complexity
- Data warehouse and reporting DB are optimized for batch, not streaming micro-inserts; this would require buffering logic anyway
- Adds Kafka as a dependency to what is fundamentally a batch pipeline, increasing blast radius of Kafka outages
- Significant overengineering for a batch freshness requirement

---

## Decision

**Option A: Scheduled Batch Polling with incremental watermark.**

The source system constraint (poll-only) and the batch freshness requirement make event-driven architecture unnecessary complexity here. Kafka is the right tool when source systems produce events and consumers need low-latency reactions — neither condition applies to this pipeline.

---

## Implementation Notes

- Use a `watermark` table to persist the last successfully processed `updated_at` timestamp per source table. Commit the watermark only after successful load to the destination — never before.
- Source tables must use **soft deletes** (`deleted_at` column) to make deletions visible to the poll. If hard deletes exist, document which tables are affected and handle separately.
- All transformation and load steps must be **idempotent** — a re-run of any batch should produce the same result as the first run.
- Language boundary: Python (pandas/polars) handles transformation logic; the scheduler and orchestration layer is responsible for retry and watermark management.

---

## Consequences

- **Accepted limitation:** Hard deletes in the source DB are not automatically captured. A reconciliation job (weekly full-count check) will detect divergence.
- **Future trigger to revisit:** If any destination consumer requires data fresher than the batch interval, or if the source system gains event-publishing capability, revisit Option B.
