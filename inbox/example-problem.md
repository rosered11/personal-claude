# Problem: ETL Watermark Commit Strategy

## Context

We are building an ETL pipeline that polls a source database on a fixed schedule (every 15 minutes). The pipeline extracts records where `updated_at > last_watermark`, transforms them, and bulk-loads them into a data warehouse.

The current implementation commits the watermark to the `watermark` table **before** loading data to the destination. This was done to simplify the code, but we suspect it can cause data loss if the load step fails after the watermark was already advanced.

## Stack

- Python (extraction + transformation using polars)
- PostgreSQL (source DB, watermark table)
- Snowflake (destination data warehouse)
- Airflow (scheduler/orchestrator)

## Problematic Code

```python
def run_etl_batch(source_conn, dest_conn, watermark_conn, table: str):
    current_wm = get_watermark(watermark_conn, table)

    # Extract
    records = extract(source_conn, table, since=current_wm)
    if not records:
        return

    # Transform
    transformed = transform(records)

    # BUG: watermark is committed before load completes
    new_wm = records[-1]["updated_at"]
    save_watermark(watermark_conn, table, new_wm)  # <-- committed too early

    # Load
    load_to_warehouse(dest_conn, table, transformed)  # if this fails, data is lost
```

## Question

What is the correct strategy for watermark management to ensure the pipeline is safe to retry and does not lose or duplicate data?
