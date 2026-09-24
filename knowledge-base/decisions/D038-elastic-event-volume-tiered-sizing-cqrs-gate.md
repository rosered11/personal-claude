---
id: D038
chosen_option: "Event-Volume-Driven Tiered Sizing Methodology, Gated by a CQRS Per-Source Index-Scope Review"
problem_id: P033
tags: [elasticsearch, azure, capacity-planning, data-sizing, cloud-infrastructure, observability, index-lifecycle-management, event-driven-architecture, cqrs]
related_snippets: [S038]
---

# Decision: Event-Volume-Driven Tiered Sizing, Gated by a CQRS Index-Scope Review

## Context

P033 asked for a starting guideline to estimate Elasticsearch data size for an Azure
procurement decision, with no query patterns, source systems, volumes, or retention
requirements supplied. Two contrasting lenses were evaluated: CQRS (size only the
query-driven projection a source needs) and Event-Driven Architecture (size the
full-fidelity event/log volume a source produces). This is the KB's first
Elasticsearch/Azure capacity-planning consultation -- no prior decision exists to
extend, contradict, or supersede.

## Options Considered

**Lens A -- CQRS: Query-Driven Minimal-Projection Sizing Model.** Treat every
Elasticsearch index as a purpose-built CQRS read-model projection scoped strictly to
the fields required by known query/filter/aggregation patterns; size capacity from
projected-document size, not source-record size. Produces the smallest, most
predictable footprint and forces early identification of which upstream systems
genuinely need to be searchable. Weakness: requires query patterns to already be known
before any number can be produced -- exactly the input this request does not yet have.

**Lens B -- Event-Driven Architecture: Event-Volume-Driven Log/Audit Sizing Model.**
Treat Elasticsearch as a durable, replayable sink for one or more event/log streams;
size capacity from throughput x average event size x retention window x replica
factor, expressed through Index Lifecycle Management hot/warm/cold tiers. Works
immediately from inputs (events/sec, bytes/event, retention days) that are usually
already measurable today, and produces the hot/warm/cold tiering breakdown infra needs
for Azure SKU selection. Weakness: applied uniformly and without scoping discipline, it
tends toward "index everything at full fidelity," inflating cost and leaving
field/mapping explosion unaddressed.

## Decision

**Event-Driven Architecture's volume/throughput-driven tiered sizing is the primary
engine** (chosen because it is the only one of the two that produces a number given
today's actual inputs -- volume and retention are answerable now, query patterns are
not). **CQRS's scoping discipline is folded in as a mandatory gate, not an optional
companion**: before any candidate data source's volume is computed, it must be
explicitly classified as either a `FULL_MIRROR` (full-fidelity event/log sink, sized at
source fidelity) or a `SCOPED_PROJECTION` (query-driven, field-limited, sized at a
reduced projection ratio). This prevents the EDA lens's own most likely failure mode --
blind full-fidelity indexing of every candidate source -- while still letting sources
with no known query pattern yet default safely to `FULL_MIRROR` rather than blocking
the whole exercise on a query-design workshop.

**Rejected as a standalone methodology: CQRS-only, "size after query patterns are
defined."** Not rejected as a lens -- its discipline survives as the mandatory gate
above -- but rejected as the sole starting methodology, because it cannot produce any
number today and the stated constraint requires a deliverable the infra team can act on
now, not after a query-design exercise that has not been scheduled.

## Consequences

- The deliverable is a worksheet/methodology (S038), not a single hardcoded number --
  every figure in the example is a placeholder the requester must replace with real
  per-source inputs before handing the result to infra.
- Sizing confidence is capped at **medium**: neither lens's inputs (event volume,
  retention window, or projection ratio) were supplied in the original request, so the
  worksheet's structure is validated but its output numbers are not yet grounded in
  real measurements.
- Retention-window and projection-ratio assumptions are the single most sensitive
  inputs -- a wrong assumption silently invalidates the whole procurement number, so the
  next concrete step is measuring real per-source volumes before this worksheet is run
  for real, not treating today's example figures as final.
- Establishes this KB's first Elasticsearch/Azure capacity-planning precedent; no
  existing decision was extended, contradicted, or superseded.
