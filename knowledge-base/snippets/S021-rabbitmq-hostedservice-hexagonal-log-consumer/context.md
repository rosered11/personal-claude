---
id: S021
title: "RabbitMQ IHostedService Hexagonal Log Consumer — Entity + Service Port + Repository + BackgroundService"
language: csharp
when_to_use:
  - "Adding a RabbitMQ consumer to an existing .NET 8 Web API that must remain a single deployable"
  - "Log/event ingestion into PostgreSQL via EF Core from RabbitMQ"
  - "Any BackgroundService that calls Scoped DI services (requires IServiceScopeFactory)"
  - "Hexagonal adapter pattern: inbound consumer + service port + outbound repository"
related_problems:
  - P016
related_decisions:
  - D021
---

# S021 — RabbitMQ IHostedService Hexagonal Log Consumer

Full working implementation for a RabbitMQ consumer integrated into an existing .NET 8 Web API using the Hexagonal Architecture pattern.

## Files

- `code.cs` — complete C# implementation: entity, ports, service, repository, and BackgroundService consumer

## Structure

```
Api (inbound adapter):
  RabbitMqActivityLogConsumer : BackgroundService
    → IServiceScopeFactory (scope per message)
    → IActivityLogService (port)

Application (ports):
  IActivityLogService
  IActivityLogRepository

Application (service):
  ActivityLogService → manual mapping → IActivityLogRepository

Infrastructure (outbound adapter):
  ActivityLogRepository : IActivityLogRepository → DataMartContext
  ActivityTransactionLog entity (sealed, [Key][DatabaseGenerated], [MaxLength])
```

## Key Implementation Notes

- `IServiceScopeFactory` in BackgroundService: required because BackgroundService is Singleton but DataMartContext is Scoped. Create a new scope per message delivery.
- `DispatchConsumersAsync = true`: required for `AsyncEventingBasicConsumer`.
- `BasicNack(requeue: false)`: failed messages route to DLQ instead of infinite requeueing.
- `BasicQos(prefetchCount: 1)`: backpressure — consumer only pulls 1 message at a time.
- `AutomaticRecoveryEnabled = true` on `ConnectionFactory`: handles broker restart without manual reconnect logic.
- Unique index on `TransactionID`: idempotency guard for at-least-once redelivery.
