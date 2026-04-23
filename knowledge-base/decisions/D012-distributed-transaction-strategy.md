---
id: D012
chosen_option: "Tiered strategy: local TX → Saga → TC/C by service count and consistency requirement"
tags: [distributed, transaction, saga, tcc, consistency, microservices, architecture]
related_snippets: [S012]
---

# Decision: Distributed Transaction Strategy — Local TX / Saga / TC-C

## Context

Multi-service operations (order placement spanning inventory, payment, and shipping services) require coordinated state changes. Distributed transactions cannot use a single database transaction. The trade-off between consistency strength and operational complexity must be explicit.

## Options Considered

1. **2PC (Two-Phase Commit)** — strong consistency; coordinator is a SPOF; blocking protocol; poor for microservices at scale.
2. **Saga (choreography or orchestration)** — eventual consistency; each service has a compensating transaction; no coordinator SPOF.
3. **TC/C (Try-Confirm/Cancel)** — strong consistency approximation; two-phase reservation prevents double-spend; suited for payment flows.
4. **Local transaction only** — only viable when all state lives in one database.

## Decision

Select by service count and consistency requirement:

| Context | Strategy |
|---------|----------|
| All state in one database | Local TX |
| 2–3 services, eventual consistency acceptable | Saga |
| Payment / financial / must not double-spend | TC/C |

Saga is the default for multi-service workflows. TC/C is mandatory when money moves between accounts or inventory must be hard-reserved before confirmation.

## Consequences

- Saga: requires compensating transactions for every step; eventual consistency means downstream reads may lag.
- TC/C: requires a "Try" reservation phase; adds one round-trip per participant; complex but prevents double-spend.
- 2PC is explicitly rejected for service-to-service flows in this stack.
- Idempotency keys (see S012) are required for all Saga and TC/C participant endpoints to handle replay safely.
