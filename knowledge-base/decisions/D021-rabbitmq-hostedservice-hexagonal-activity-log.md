---
id: D021
title: "RabbitMQ Consumer — IHostedService Hexagonal Adapter + RabbitMQ.Client + Service/Repository Ports"
chosen_option: "IHostedService as inbound Hexagonal adapter using RabbitMQ.Client, with IActivityLogService port and IActivityLogRepository driven adapter — plus EDA-derived idempotency guard"
problem_id: P016
date: 2026-06-10
tags:
  - dotnet
  - rabbitmq
  - ef-core
  - postgresql
  - background-service
  - hosted-service
  - message-consumer
  - hexagonal-architecture
  - write-path
  - integration-events
  - logging
  - activity-log
related_snippets:
  - S021
---

# Decision: RabbitMQ Consumer — IHostedService Hexagonal Adapter + RabbitMQ.Client

## Chosen Option

**IHostedService as inbound Hexagonal adapter using `RabbitMQ.Client`, with `IActivityLogService` port and `IActivityLogRepository` driven adapter, plus EDA-derived idempotency guard.**

## Lenses Evaluated

- **Lens A: Hexagonal Architecture** — consumer as inbound adapter, EF Core as outbound adapter, service port as the true boundary
- **Lens B: Event-Driven Architecture** — consumer topology, at-least-once delivery, idempotency, retry, DLQ

## Rationale

Hexagonal Architecture provides the correct abstraction layering:
- `RabbitMqActivityLogConsumer : BackgroundService` = inbound adapter (driving port)
- `IActivityLogService` = application port (the true boundary)
- `ActivityLogService` = application service (manual mapping, no AutoMapper)
- `IActivityLogRepository` = driven port
- `ActivityLogRepository` = outbound adapter (EF Core)

`RabbitMQ.Client` chosen over MassTransit because existing producers use raw JSON payloads without a MassTransit envelope. MassTransit's default topology would require `RawJsonDeserializer` overrides and exchange topology alignment — complexity with no benefit for a single-queue insert use case.

The Event-Driven lens contributed three critical additions absorbed into the Hexagonal implementation:
1. **Idempotency**: unique index on `TransactionID` (partial — `WHERE TransactionID IS NOT NULL`) prevents duplicate inserts on message redelivery
2. **Dead-letter routing**: `BasicNack(requeue: false)` on processing failure routes to RabbitMQ DLX rather than infinite retry loop
3. **Backpressure**: `BasicQos(prefetchCount: 1)` prevents the consumer from pulling ahead of DB write capacity

`IServiceScopeFactory` is required in the `BackgroundService` to correctly scope `DataMartContext` per message — avoids the singleton-scoped DbContext anti-pattern (DbContext is registered as Scoped by EF Core convention; BackgroundService is Singleton).

## Tradeoffs Accepted

- Single-process failure domain: if the API crashes, both query serving and log ingestion stop. Acceptable given single-deployable constraint and medium severity.
- Manual RabbitMQ.Client connection/channel lifecycle management. The consumer owns `IConnection` and `IModel` lifecycle. Connection recovery must be configured via `AutomaticRecoveryEnabled = true` on `ConnectionFactory`.
- No built-in batch insert optimization: each message triggers a single `SaveChangesAsync`. Acceptable for activity log volume. If throughput grows, batch with a channel + timer flush can be added to the repository adapter without touching the service port.
- `prefetchCount: 1` trades throughput for backpressure safety. Increase to 10–50 if insert latency is consistently low.

## Next Steps

1. Add `RabbitMQ.Client` NuGet to `DataMartQueryService.Api.csproj`
2. Add `ActivityTransactionLog` entity to `DataMartQueryService.Infrastructure` following existing sealed class pattern
3. Add `DbSet<ActivityTransactionLog> ActivityTransactionLogs` to `DataMartContext`
4. Add `HasIndex` in `OnModelCreating` for `TransactionID` (unique, partial filter)
5. Run `dotnet ef migrations add AddActivityTransactionLog`
6. Register in DI: `services.AddScoped<IActivityLogRepository, ActivityLogRepository>()`, `services.AddScoped<IActivityLogService, ActivityLogService>()`, `services.AddHostedService<RabbitMqActivityLogConsumer>()`
7. Add `RabbitMQ` config section to `appsettings.json` (Host, User, Password, QueueName)
8. Configure DLX on RabbitMQ broker: `activity-logs` → DLX `activity-logs.dlx` → queue `activity-logs_error`
9. Set `AutomaticRecoveryEnabled = true` on `ConnectionFactory` for broker restart resilience

## KB References

- D001 (S001, S014): EF Core scoped DbContext pattern — IServiceScopeFactory in BackgroundService follows same principle
- D012 (S012): Idempotency key table pattern — TransactionID unique index is a lightweight version of this
