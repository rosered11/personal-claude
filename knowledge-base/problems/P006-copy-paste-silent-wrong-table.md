---
id: P006
title: "SyncProductBarcodeJda — Copy-Paste Bug: CheckPendingAsync Queries Wrong Table Silently"
date: 2026-04-08
tags: [etl, copy-paste, correctness, testing, silent-failure, dotnet, debugging]
severity: high
related_decisions: [D006]
related_snippets: []
---

# SyncProductBarcodeJda — Copy-Paste Bug: CheckPendingAsync Queries Wrong Table Silently

## Problem

`SyncProductBarcodeJda` runs without error but commits 0 records. `barcode.cs CheckPendingAsync` queries `SpcJdaProductStaging` instead of `SpcJdaBarcodeStaging` — a copy-paste from `product.cs`. Silent failure: if the product staging table has no pending rows but the barcode staging table does, the entire barcode sync skips without any error. Additionally, `SyncProductMasterJda` was running at `BatchSize=20K`, causing TX hold 27–40s per batch (79% of MySQL's 50s timeout limit) with CRIT alerts firing.

## Root Cause

`barcode.cs` was cloned from `product.cs`. `GetProductStaging()` was correctly updated to `SpcJdaBarcodeStaging`, but `CheckPendingAsync()` retained the original `SpcJdaProductStaging` reference — a silent wrong-table query. Both are independent call sites; updating one does not update the other. The compiler does not catch DbSet reference errors.

## Constraints

- No compile-time error for wrong DbSet reference — purely a runtime/data correctness bug
- Silent failure: no exception, just zero records processed
- Requires manual verification of all 6 touch points when cloning an ETL service

## Affected Components

- `barcode.cs` `CheckPendingAsync()` method (line 204)
- `SyncProductBarcodeJda` Airflow DAG
- `SpcJdaBarcodeStaging` vs `SpcJdaProductStaging` DbSet references
