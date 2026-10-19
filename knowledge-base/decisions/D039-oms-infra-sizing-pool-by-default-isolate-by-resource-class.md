---
id: D039
chosen_option: "Pool by default, isolate only where resource classes conflict (HYBRID topology), sized from one per-order unit-economics model"
problem_id: P034
tags: [oms, capacity-planning, infrastructure-sizing, resource-estimation, postgresql, redis, hangfire, opentelemetry, elasticsearch, kubernetes, azure, dotnet, observability, cloud-infrastructure, microservices, modular-monolith]
related_snippets: [S039]
---

# Decision: Pool by Default, Isolate by Resource Class

## Context

P034 asks for a justified infrastructure request for Sprint-OMS with trade-offs where two
angles exist. Two lenses were evaluated against the same workload model.

## Options Considered

**Lens A -- Microservices (isolation by deployment unit).** One PostgreSQL server per data
domain (Order, Master, Log), separate Redis per service/concern, per-service node pools.
Pros: smallest blast radius, independent scaling, no noisy neighbour. Cons: fixed floor of
three HA servers and three Redis instances for a workload whose steady CPU is under 1 vCPU
average; ops cost x3 for a team of about four; contradicts D020 (modular monolith) and D025
(single-instance DB scaling handled by cache/replica, not by splitting).

**Lens B -- Modular Monolith (pooled shared tier, D020 lineage).** One PG server holding three
logical DBs, one Redis separated only by key prefix, one node pool. Pros: lowest fixed cost,
simplest ops, capacity shared across services. Cons: shared blast radius, and two *structural*
conflicts that tuning cannot remove: (1) Redis cache needs `allkeys-lru` while Hangfire job
storage needs `noeviction`; (2) `spc_log` writes (about 50 KiB/order) match the entire OLTP
write volume (about 56 KiB/order) yet need no HA, so pooling pays HA twice on low-value data.

## Decision

**HYBRID: pool by default; isolate only where two workloads need conflicting resource
classes.** Concretely: 2 PostgreSQL servers (OLTP `spc_oms`+`spc_master` with zone-redundant
HA; LOG `spc_log` without HA), 2 Redis instances (cache `allkeys-lru`; Hangfire `noeviction`
with HA), one AKS node pool with per-service Deployments and HPA, OTel Collector x2 and a
3-node Elasticsearch sized with S038. Sizing basis is Scenario B (50K orders/day sustained,
100K peak day = P015 target); Scenario A is the floor, C/D are reached by scale triggers, not
by up-front purchase. Isolation by *service name alone* (Lens A's default) is rejected as
unjustified at this volume; pooling by default (Lens B) is kept as the rule.

Rationale for the deciding factor: isolation is justified by a demonstrable resource-class
conflict (eviction policy, durability class), not by the existence of a service boundary.
Pooled-everything is rejected because the two conflicts above are structural, not tunable.

## Consequences

- Largest cost lever is not topology but **Hangfire succeeded-job retention** (24 h default
  needs about 8 GB Redis at Scenario B; 2 h needs about 0.7 GB) -- requires a one-line code
  change and OMS dev agreement before the Redis SKU is final.
- Findings to hand to dev/infra: Npgsql `Maximum Pool Size` unset (default 100/pod; worst-case
  3,600 connections vs 1,718 max on D4ds_v5), Hangfire `WorkerCount` unset (5 workers per 1-vCPU
  pod; becomes the first outbound bottleneck at Scenario C), outbox job args carry the full
  payload (about 10 KB/job), retention of log tables is the largest OLTP storage lever.
- Confidence **medium**: no production volume was supplied; storage and Redis within 2x, app CPU
  within 2x either way (sensitivity table in output). Every `[A#]` assumption has a named
  measurement method.
- Does not contradict D020 (pooled-by-default is its infrastructure corollary) or D025 (read
  replica stays deferred behind its Phase 4 data trigger); extends D030 (secret store listed as
  a prerequisite) and reuses D038/S038 for Elasticsearch sizing.
- Establishes this KB's first multi-resource OMS capacity-planning precedent; a later request
  with real volumes should UPDATE P034/D039/S039 (re-run the worksheet), not create a new P-number.
