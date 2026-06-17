# Architecture Transition Roadmap

Auto-maintained by `career-mentor` agent. Tracks architectural concepts encountered across consultations.

---

## Exposure Log

| Date | Problem | Concepts Encountered | Lenses | KB IDs |
|------|---------|---------------------|--------|--------|
| 2026-06-10 | DataMart RabbitMQ Activity Log Ingestion | IHostedService lifecycle, BackgroundService scoping, IServiceScopeFactory, RabbitMQ.Client consumer (AsyncEventingBasicConsumer, BasicQos, BasicAck/Nack, DLX), Hexagonal adapter pattern (inbound/outbound ports), EDA at-least-once delivery, idempotency (unique partial index), manual DTO mapping | Hexagonal Architecture, Event-Driven Architecture | P016, D021, S021 |

---

## Learning Recommendations

### Active Study Items (from P016 consultation — 2026-06-10)

1. **IHostedService + IServiceScopeFactory pattern in .NET 8**
   - A BackgroundService is registered as a Singleton. EF Core DbContext is Scoped. The only safe bridge is IServiceScopeFactory.CreateScope() per unit of work (per message here). This is a required pattern any time you call Scoped services from long-lived background workers.
   - Study: Microsoft docs on BackgroundService + DI scope management

2. **RabbitMQ.Client v6+ AsyncEventingBasicConsumer**
   - Low-level .NET RabbitMQ client. Key mechanics: DispatchConsumersAsync=true, BasicQos prefetchCount for backpressure, BasicAck/BasicNack for manual acknowledgment, x-dead-letter-exchange argument for DLQ routing, AutomaticRecoveryEnabled for broker reconnect.
   - Study: RabbitMQ.Client official samples + AMQP 0-9-1 model concepts

3. **Hexagonal Architecture (Ports and Adapters) in .NET**
   - Inbound adapter (consumer) calls an application port (interface). Outbound adapter (repository) implements a driven port. The application service coordinates between ports without knowing any infrastructure. Applied here: RabbitMqActivityLogConsumer (inbound) -> IActivityLogService (port) -> ActivityLogService -> IActivityLogRepository (driven port) -> ActivityLogRepository (EF Core).
   - Study: Alistair Cockburn original Hexagonal Architecture paper; Mark Seemann on composition root

4. **EDA Operational Safety: Idempotency + Dead-Letter + Backpressure**
   - At-least-once delivery means your consumer WILL see duplicate messages on redelivery. Idempotency guard (unique index on a natural key like TransactionID) is the correct defense. Dead-letter exchange prevents a single bad message from blocking the queue forever. prefetchCount limits consumer-side memory pressure.
   - Study: RabbitMQ DLX documentation; Martin Fowler on idempotent receiver
