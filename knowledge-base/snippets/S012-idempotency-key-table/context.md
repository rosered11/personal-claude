---
id: S012
slug: idempotency-key-table
language: sql
when_to_use: "Use for any endpoint that creates orders, payments, or triggers financial state changes. Client sends an idempotency key (UUID) in the request header. Server checks the table before processing — returns cached response if key exists. Prevents double-spend on network retry."
related_problems: []
related_decisions: [D012]
source: TA4
---

# Idempotency Key Table (PostgreSQL)

Schema and application logic for request idempotency. Prevents duplicate processing when clients retry on network errors. Stores the response alongside the key so retries receive the same response without re-executing the operation.

## Application flow

1. `SELECT response FROM idempotency_keys WHERE key = $1`
2. If found → return cached `response` (skip processing, return `status_code`)
3. Process request normally
4. `INSERT INTO idempotency_keys ... ON CONFLICT DO NOTHING` — handles race condition (two concurrent retries)

## TTL

Keys expire after 24 hours. A scheduled cleanup job (pg_cron or Airflow) runs `DELETE WHERE created_at < NOW() - INTERVAL '24 hours'`.

## When NOT to use

- Read-only endpoints — idempotency is only needed for state-changing operations
- Very high request volume — the idempotency table becomes a bottleneck; consider Redis with TTL instead
