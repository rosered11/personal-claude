---
id: P005
title: "ETL Sync OOM Risk — Oversized Batch and EF ChangeTracker Accumulation"
date: 2026-04-08
tags: [etl, memory, ef-core, batch-processing, oom, changetracker, dotnet]
severity: high
related_decisions: [D005]
related_snippets: [S005]
---

# ETL Sync OOM Risk — Oversized Batch and EF ChangeTracker Accumulation

## Problem

Live Airflow log shows Batch 1: 100K records, TX hold 144s, alloc 5,757MB, heap 1,191MB (+1,175MB). Batch 2: heap 1,780MB (+589MB). Heap growing every batch — OOM expected by batch 4–5. The BatchSize config was 100K (10× the intended value), EF ChangeTracker was never cleared after commit, and a per-batch activity tracking dictionary was never flushed (grows to 3M entries across the job).

## Root Cause

Three compounding issues: (1) `BatchSize` config returned 100K instead of 10K — 100K entities × 30 fields × EF SqlParameter per insert = ~5GB alloc per batch. (2) `context.ChangeTracker` not cleared after `tx.CommitAsync()` — committed entities remain tracked across batches, heap grows with each commit. (3) `productMasterActivityTracking Dictionary<string, Activity>` created once in `ProcessAsync()` and never flushed between batches — accumulates all 3M records in memory.

## Constraints

- EF Core memory model: committed entities remain in ChangeTracker until explicitly cleared or DbContext disposed
- Config value misconfiguration (100K instead of 10K) — not a code bug
- Must be validated in staging with heap metrics before deploying

## Affected Components

- `ProcessSyncLoopAsync()` in `product.cs` and `barcode.cs`
- `appsettings.json` BatchSize configuration
- EF Core ChangeTracker
- `productMasterActivityTracking` dictionary
