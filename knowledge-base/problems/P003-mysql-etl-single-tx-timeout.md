---
id: P003
title: "MySQL ETL Sync — Long-Lived Transaction Timeout Causes Airflow Job Failure"
date: 2026-04-03
tags: [etl, transaction, mysql, batch-processing, timeout, airflow, dotnet]
severity: high
related_decisions: [D003]
related_snippets: [S003]
---

# MySQL ETL Sync — Long-Lived Transaction Timeout Causes Airflow Job Failure

## Problem

A .NET sync job processing 3M records from staging DB to MySQL production DB fails after a long run with exit code 1. Airflow marks the DAG run as failed. A single transaction wraps the entire 300-batch sync loop (~210s open). MySQL `innodb_lock_wait_timeout` (default 50s) kills the connection, triggering `RollbackAsync()`, exhausting Polly retry attempts, and propagating exit code 1 to Airflow. Zero records are committed — all work lost on each run.

## Root Cause

`BeginTransactionAsync()` placed before the `while(true)` batch loop wraps the entire job in a single DB transaction. BotE: 300 batches × 700ms = 210s hold >> `innodb_lock_wait_timeout` (50s). MySQL kills the connection → `catch` calls `RollbackAsync()` on the dead connection (second failure) → Polly `retries=0` → exception propagates → exit code 1. Airflow `retries=0` → DAG run marked failed with no retry.

## Constraints

- MySQL target with default `innodb_lock_wait_timeout = 50s`
- Airflow orchestration — job must be restartable from checkpoint, not from zero
- 3M records, batch size 10K, 300 batches

## Affected Components

- `ProcessSyncLoopAsync()` in `product.cs`
- `SyncProductMasterJda` / `SyncProductBarcodeJda` Airflow DAGs
- MySQL production database (ETL target)
