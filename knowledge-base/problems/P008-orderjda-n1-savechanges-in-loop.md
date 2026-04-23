---
id: P008
title: "OrderJda ETL — N+1 SELECT + SaveChanges-in-Loop + Long Transaction on PostgreSQL"
date: 2026-04-21
tags: [ef-core, n+1, etl, transaction, batch-processing, dotnet, postgresql, fk-constraint]
severity: high
related_decisions: [D008]
related_snippets: [S008]
---

# OrderJda ETL — N+1 SELECT + SaveChanges-in-Loop + Long Transaction on PostgreSQL

## Problem

ETL batch degrades under concurrency with potential Airflow task timeout and pool exhaustion risk. Three compounding anti-patterns: N+1 SELECT per header, `SaveChangesAsync` inside `foreach`, and transaction held across the entire `while(true)` loop. BotE: 5 tasks × 600 queries × 20ms = 60,000ms hold → pool exhaustion; TX spans the entire job duration (minutes).

## Root Cause

(1) `context.OrderOutboundTb.FirstOrDefaultAsync()` and `context.OrderOutboundItemTb.Where().ToListAsync()` called inside `foreach (var orderHeader in orderHeaders)` — 1,000 queries per batch of 500 headers. (2) `await context.SaveChangesAsync()` called per header → N×batch_size DB writes, each briefly holding a transaction. (3) `BeginTransactionAsync()` placed outside the batch loop — TX hold = sum of all batch latencies = minutes. Additional cross-product filter bug: `jdaBatchIds.Contains() && dihBatchIds.Contains()` returns set-level matches, not tuple-level, fetching rows from wrong batch ID combinations.

## Constraints

- FK dependency: item activities require `headerActivity.Id` (DB-generated IDENTITY) — cannot save children before parents
- Two-pass approach required: Pass 1 saves headers to populate FK IDs, Pass 2 saves children
- PostgreSQL with EF Core; must use per-batch TX (D16/D20 standard)

## Affected Components

- `SyncOrderOutboundStagingToSpcJda` and `SyncOrderOutboundSpcToWmsJda` sync classes
- `GetDataStaging()` composite key filter
- `context.OrderOutboundTb`, `context.OrderOutboundItemTb`, `context.OrderOutboundActivityTb`
