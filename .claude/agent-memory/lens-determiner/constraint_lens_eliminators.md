---
name: Constraint-Based Lens Eliminators
description: Lenses to eliminate from consideration when specific constraints are present — prevents recommending architectures the team cannot adopt
type: project
---

# Constraint-Based Lens Eliminators

When problem constraints include any of the following signals, eliminate the corresponding lenses **before** selecting the final pair. Eliminated lenses must not appear in the output.

## Eliminate Serverless when

- Problem involves stateful, long-running workloads (ETL jobs, batch processing, persistent DB connections)
- EF Core or similar ORM is in use (connection pooling incompatible with serverless cold starts)
- Tags include: `etl`, `batch-processing`, `subprocess`, `airflow`, `orchestration`

**Why:** Serverless functions terminate after execution; EF Core connection pools assume long-lived processes. Long-running ETL jobs exceed typical serverless timeout limits (15 min for Lambda).

---

## Eliminate Event-Driven Architecture when

- Problem description explicitly states team has no Kafka or message queue experience
- System is a simple synchronous request-response API with no async requirements
- Constraints mention "no message broker", "single process", or "synchronous only"

**Why:** Event-Driven requires producers, brokers, consumers, and dead-letter queues — operational overhead the team must sustain. Wrong choice when synchronous is sufficient.

---

## Eliminate Microservices when

- Team size is fewer than 5 engineers
- Latency budget is < 50ms end-to-end (inter-service network calls add ~1–5ms each)
- Problem is explicitly about a single service or monolith and decomposition is not in scope
- Tags include: `debugging`, `copy-paste`, `correctness` without `distributed` or `scalability`

**Why:** Microservices introduce network boundaries, distributed tracing, and deployment complexity. Wrong choice when the problem is internal to one service.

---

## Eliminate CQRS when

- System has simple CRUD requirements with no read/write contention
- Team has no experience with separate read and write models
- Problem is not about query performance vs write consistency

**Why:** CQRS adds two code paths for every entity. The cost is only justified when read and write patterns genuinely differ enough to warrant separate models.

---

## Eliminate Service Mesh when

- The problem does not involve service-to-service communication, mTLS, or traffic management
- Team is < 10 engineers with no existing Kubernetes or Istio infrastructure

**Why:** Service Mesh solves cross-cutting concerns in multi-service environments. Adding it to solve a single-service problem is over-engineering.
