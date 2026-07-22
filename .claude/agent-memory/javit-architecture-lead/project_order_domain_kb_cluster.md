---
name: order-domain-kb-cluster
description: The Order/OMS domain is the most recurring problem cluster in this KB — tags and precedents to check first for any new order-related problem
type: project
---

Problems P001, P008, P010, P013-P015, P018, P019 (and their D/S pairs) all touch the same
Order/OMS bounded context across different services (Order.API, validate-service/Validator.API,
the OMS modular monolith). Recurring tag cluster: `ef-core`, `mssql`/`postgresql`, `dotnet`,
`integration-events`, `idempotency`, `microservices`/`oms`.

**Why:** P019/D024 (2026-07-13, validate-service SQL timeout + duplicate-key on Kafka retry)
scored only 0.45 tag-overlap against the closest precedent P010/D015 (Order.API concurrent
running-number race + missing event-consumer idempotency) — below the 0.8 KB-writer UPDATE
threshold, so it correctly became a new CREATE-mode record — but the two are clearly related:
D015 established the `processed_events` idempotency-key pattern (via D012/S012) that D024 extends
to a second, previously-uncovered consumer (the MAO/Kafka ingestion path in validate-service).

**How to apply:** For any new Order/OMS-related problem, explicitly check P010/D015/S015 and
P013-P015/D018-D020 (greenfield OMS design precedents) even if tag-overlap scoring alone doesn't
surface them at the top — they encode idempotency, aggregate, and outbox patterns this domain
reuses constantly. When a new order-service problem's idempotency mechanism doesn't yet use the
`processed_events` pattern, that is very likely a gap worth calling out explicitly (as happened
here), not a coincidence.
