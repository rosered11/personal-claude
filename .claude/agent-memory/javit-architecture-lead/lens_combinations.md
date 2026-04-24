---
name: Lens Combinations
description: Effective lens pairs for this domain based on KOS patterns — gives Javit context for what lens-determiner is likely to return and whether the pair makes sense
type: project
---

# Effective Lens Combinations by Problem Domain

Derived from KOS P# patterns, D# decisions, and incident post-mortems. These pairings have been validated as producing meaningful contrast for problems in this domain.

---

## EF Core / .NET Performance Problems

**Tags:** `ef-core`, `n+1`, `performance`, `dotnet`, `connection-pool`

**Lens pair:** Hexagonal Architecture vs Layered Architecture

**Contrast rationale:** Hexagonal treats EF Core as an external adapter, making it possible to swap for Dapper/raw SQL on hot paths without touching domain logic. Layered accepts EF Core coupling throughout all layers. The contrast is: adapter isolation vs pragmatic coupling.

---

## ETL / Batch Processing Problems

**Tags:** `etl`, `batch-processing`, `transaction`, `ef-core`, `dotnet`

**Lens pair:** Event-Driven Architecture vs Layered Architecture

**Contrast rationale:** Event-Driven replaces the polling/staging-table pattern with event streams, enabling backpressure and parallel consumer scaling. Layered keeps the proven while-loop with per-batch TX. The contrast is: message-driven decoupling vs simplicity-first batch loop.

---

## Distributed System Design Problems

**Tags:** `distributed`, `transaction`, `saga`, `microservices`

**Lens pair:** CQRS vs Saga Pattern

**Contrast rationale:** CQRS separates read/write models, simplifying per-service consistency without distributed coordination. Saga sequences compensating transactions across services. The contrast is: model separation vs explicit compensation flow.

---

## Database Selection Problems

**Tags:** `database`, `architecture`, `selection`, `scalability`

**Lens pair:** Domain-Driven Design vs Microservices

**Contrast rationale:** DDD selects DB per bounded context (each aggregate owns its storage). Microservices select DB per service with polyglot persistence. The contrast is: bounded context alignment vs service autonomy.

---

## API / Rate Limiting Problems

**Tags:** `rate-limiting`, `api`, `scalability`, `redis`

**Lens pair:** Microservices vs Serverless

**Contrast rationale:** Microservices embed rate limiting per service with persistent process state. Serverless delegates to API gateway without persistent state. The contrast is: embedded control vs infrastructure delegation.

---

## Subprocess / Orchestration Problems

**Tags:** `airflow`, `python`, `subprocess`, `orchestration`, `timeout`

**Lens pair:** Event-Driven Architecture vs Hexagonal Architecture

**Contrast rationale:** Event-Driven replaces subprocess calls with async event queue (decouple trigger from execution). Hexagonal ports subprocess as an explicit adapter with a timeout interface. The contrast is: async decoupling vs explicit port isolation.

---

## Airflow Orchestration Chain Problems

**Tags:** `airflow`, `python`, `orchestration`, `trigger-dagrun`, `xcom`, `child-dag`, `dag-dependency`

**Lens pair:** Saga Pattern vs Hexagonal Architecture

**Contrast rationale:** Saga asks "does each step in the trigger chain have explicit failure boundaries and compensation?" — exposes missing short-circuit and silent failure propagation. Hexagonal asks "are the adapter contracts (XCom, Jinja, child DAG availability) explicitly validated?" — exposes render_template_as_native_obj type violations and missing child DAG health checks. Together: failure propagation vs contract soundness.

**First used:** P012 / D017 — confirmed as high-quality contrast for this domain.

---

## When KB Search Returns High Overlap (≥ 0.8 Jaccard)

If kb-search-agent returns a result with overlap_score ≥ 0.8, instruct lens-determiner to:
1. Note the existing decision's chosen lens
2. Select lenses that contrast with or refine the existing decision
3. Pass `kb_influence: "refinement"` in the lens output (not `"contrast"`)

This prevents re-selecting the same lenses for nearly-identical problems, encouraging KB growth with nuanced variations.
