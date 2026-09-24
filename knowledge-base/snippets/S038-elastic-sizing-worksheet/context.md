---
when_to_use: "When you need a repeatable, procurement-ready worksheet to estimate Elasticsearch/Elastic storage and shard footprint on Azure (or any cloud), especially when query patterns for some candidate data sources are not yet known and a full-fidelity default is the safer starting assumption."
related_problems: [P033]
related_decisions: [D038]
---

# Snippet: Elastic Data-Sizing Worksheet (Event-Volume-Driven, CQRS-Gated)

This script operationalizes D038's chosen methodology: every candidate Elasticsearch
index is first classified as `FULL_MIRROR` (Event-Driven lens -- full-fidelity
event/log sink) or `SCOPED_PROJECTION` (CQRS lens -- query-driven, field-limited
read model) before its volume is computed. `FULL_MIRROR` sources are sized at full
source-document size; `SCOPED_PROJECTION` sources are sized at a reduced
`projection_ratio`, forcing an explicit decision about what actually needs to be
indexed instead of defaulting every source to "index everything."

Storage is split across hot/warm/cold tiers from each source's retention curve,
because that split is what actually maps to different Azure node SKUs/disk tiers
(hot = fast/expensive, warm/cold = cheaper), then multiplied by replica count and
Elastic's own index-overhead factor. Primary shard count is recommended from the hot
tier using Elastic's official 10-50GB/primary-shard guidance (midpoint 30GB used here).

**How to use it:** replace the `SOURCES` list at the bottom with real per-source
figures (docs/day, average document size, retention days, hot/warm day split, replica
count, and -- for `SCOPED_PROJECTION` sources -- a `projection_ratio` informed by an
actual query/field review) and run the script. The printed report is a per-source and
total GB/shard breakdown suitable for handing directly to an infra team as an Azure
Elastic procurement input.

**Known limitation, by design (D038):** the example `SOURCES` in `__main__` are
placeholders, not real measurements -- this is why D038's decision confidence is rated
medium. Do not quote the example output as a real capacity number; the worksheet's
structure is what this consultation validated, not any specific figure.
