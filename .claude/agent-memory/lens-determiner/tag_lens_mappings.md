---
name: Tag-to-Lens Mappings
description: Which architectural lenses apply to problems with specific tag combinations — derived from KOS P# patterns and D# decisions
type: project
---

# Tag-to-Lens Mappings

Derived from KOS P# patterns and D# decisions. When LensDeterminerAgent receives a problem with these tag combinations, consider the paired lenses. Always verify the contrast between the two lenses selected.

## EF Core / .NET Performance

**Tags:** `ef-core`, `n+1`, `performance`, `dotnet`, `connection-pool`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Hexagonal Architecture | Layered Architecture | Hexagonal separates data access into explicit port/adapter — enables swap of EF Core for Dapper on hot paths. Layered accepts EF Core coupling as the norm. |

**KB precedent:** D001, P001

---

## ETL / Batch Processing

**Tags:** `etl`, `batch-processing`, `transaction`, `ef-core`, `dotnet`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Event-Driven Architecture | Layered Architecture | Event-Driven decouples staging from processing via events, enabling backpressure. Layered keeps the simple while-loop with per-batch TX. |

**KB precedent:** D003, D005, D008, P003, P005, P008

---

## Distributed Transaction / Multi-Service

**Tags:** `distributed`, `transaction`, `saga`, `microservices`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Saga Pattern | CQRS | Saga focuses on compensating transactions across services. CQRS separates read/write models, simplifying per-service consistency. |

**KB precedent:** D012

---

## Rate Limiting / API

**Tags:** `rate-limiting`, `api`, `scalability`, `redis`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Microservices | Serverless | Microservices embed rate limiting per service. Serverless delegates to API gateway. |

**KB precedent:** D013

---

## ID Generation / Distributed

**Tags:** `id-generation`, `distributed`, `snowflake`, `scalability`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Domain-Driven Design | Microservices | DDD treats ID generation as domain logic (aggregate root IDs). Microservices treat it as infrastructure (shared Snowflake service). |

**KB precedent:** D014

---

## Subprocess / Orchestration

**Tags:** `airflow`, `python`, `subprocess`, `orchestration`, `timeout`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Event-Driven Architecture | Hexagonal Architecture | Event-Driven replaces subprocess with async event queue. Hexagonal ports subprocess as an external adapter with explicit timeout interface. |

**KB precedent:** D009, P009

---

## Database Selection

**Tags:** `database`, `architecture`, `selection`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Domain-Driven Design | Microservices | DDD selects DB per bounded context (aggregate store). Microservices select DB per service (polyglot persistence). |

**KB precedent:** D010

---

## Real-Time / Connection

**Tags:** `real-time`, `websocket`, `sse`, `api`

| Lens A | Lens B | Contrast |
|--------|--------|----------|
| Event-Driven Architecture | Layered Architecture | Event-Driven uses pub-sub for push delivery. Layered uses REST polling or WebSocket as a controller layer. |

**KB precedent:** D011
