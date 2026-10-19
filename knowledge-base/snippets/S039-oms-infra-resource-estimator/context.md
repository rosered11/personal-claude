---
when_to_use: "When you must hand an infra team a justified resource request (PostgreSQL, Redis/Hangfire, AKS pods, OTel/Elasticsearch) for an order-processing system and only have per-order behaviour from the code plus a few volume anchors, not production metrics."
related_problems: [P034]
related_decisions: [D039]
---

# Snippet: OMS Infrastructure Resource Estimator (per-order unit economics, pooled vs isolated vs hybrid)

`code.py` (a copy of `output/oms-estimate-resource/oms_resource_estimator.py`) implements D039. One
`Workload` dataclass holds every assumption (tagged `[A#]`, each with a measurement method in
`01-method-and-evidence.md`); `size()` turns a `Scenario` (sustained orders/day for storage,
peak-day orders and peak-hour share for compute) into: AKS pods and nodes, Hangfire worker slots
needed vs available, PostgreSQL SKU / disk tier / IOPS / connection budget, Redis memory at 24 h vs
2 h Hangfire retention, and Elasticsearch retained GB. `topologies()` renders the same numbers as
POOLED, ISOLATED and HYBRID so the infra trade-off is visible.

Run: `python oms_resource_estimator.py > 02-scenarios.md` (standard library only).

Limitations by design: CPU-per-call and bytes-per-row are estimates (sensitivity table in the
output docs: storage and Redis within 2x, CPU within 2x either way); SKU catalogues were verified
against Microsoft Learn on 2026-10-16 and must be re-checked before purchase; Elasticsearch numbers
are a projection (observability is disabled in the code today) and should be re-run through S038.
