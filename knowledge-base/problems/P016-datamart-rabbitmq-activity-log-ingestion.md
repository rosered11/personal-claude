---
id: P016
title: "DataMart Dashboard — RabbitMQ Activity Log Ingestion Channel"
date: 2026-06-10
tags:
  - dotnet
  - rabbitmq
  - ef-core
  - postgresql
  - background-service
  - hosted-service
  - message-consumer
  - write-path
  - integration-events
  - logging
  - activity-log
severity: medium
related_decisions:
  - D021
related_snippets:
  - S021
---

# Problem: DataMart Dashboard — RabbitMQ Activity Log Ingestion Channel

## Summary

The DataMartQueryService (.NET 8 Web API + PostgreSQL + EF Core) has no mechanism to receive activity transaction logs from RabbitMQ and persist them to the `ActivityTransactionLogTb` table. Clients are already producing these messages on RabbitMQ; the datamart needs a consumer and a persistence path.

## Root Cause

The service was built as a pure read/query API (dashboard stock/inventory queries). No consumer infrastructure exists. The team now needs to bolt a write-path (RabbitMQ consumer → EF Core persist) onto a read-optimized service without breaking the existing deployment unit or query SLAs.

## Context

- Stack: .NET 8 Web API, PostgreSQL, EF Core 8 + Npgsql, JWT auth, Serilog, OpenTelemetry
- MediatR is present in the project but must NOT be used for new code
- No RabbitMQ client package yet in the csproj
- Single deployable constraint applies unless architecturally justified
- Existing entity pattern: sealed class with `[Key][DatabaseGenerated]` and `[MaxLength]` attributes
- Existing infrastructure: `DataMartContext` with `DbSet`s + `HasIndex` in `OnModelCreating`

## Constraints

- Do NOT use MediatR for new code — plain service interfaces only
- Do NOT use AutoMapper — explicit manual mapping
- Must integrate with existing EF Core + Npgsql setup (DbSet + migration)
- Single deployable preferred (no separate worker process unless justified)
- RabbitMQ client package not yet chosen
- Stack: .NET 8, PostgreSQL, RabbitMQ

## Key Architectural Question

How should the RabbitMQ consumer be integrated — as a background `IHostedService` directly in the API, or as a separate consumer service/worker? And what is the right abstraction layer for saving logs (repository pattern, direct DbContext, service class)?
