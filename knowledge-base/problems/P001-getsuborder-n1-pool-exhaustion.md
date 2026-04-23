---
id: P001
title: "GetSubOrder API Latency Spike — N+1 Queries and Connection Pool Exhaustion"
date: 2026-03-25
tags: [ef-core, n+1, connection-pool, performance, dotnet, async, lazy-loading]
severity: high
related_decisions: [D001]
related_snippets: [S001, S014]
---

# GetSubOrder API Latency Spike — N+1 Queries and Connection Pool Exhaustion

## Problem

API timeout under high concurrent load. Approximately 33 DB queries per request due to N+1 loops, redundant reference resolution, and lazy loading inside loops. Under 100 concurrent requests × 33 queries × ~10ms = 33s DB hold time, leading to connection pool exhaustion and cascading timeouts. Latency scaled O(n) with sub-order count, not O(1).

## Root Cause

N+1 query patterns across 3 loops: `IsExistOrderReference` called 3× per request for the same ID (6–9 redundant queries), duplicate `Any()+FirstOrDefault()` on the same predicate (2 queries where 1 suffices), lazy `Entry().Reference().Load()` inside a for loop (1 query per promotion row), and missing `AsNoTracking()` on every read path. The EF compiled query cache also accumulated 17,557 DynamicMethod objects (~6 MB non-reclaimable static heap) from uncompiled bulk queries.

## Constraints

- Production API — cannot take offline for fixes; must be incremental phases
- EF Core + .NET stack; PostgreSQL database
- Connection pool: default size 100

## Affected Components

- `GetSubOrderAsync` coordinator method (`target.cs`)
- `IsExistOrderReference`, `GetOrderHeader`, `GetOrderMessagePayments`, `GetOrderPromotion`, `GetRewardItem`, `GetSubOrderMessage` sub-methods
- SubOrder Processing service connection pool
