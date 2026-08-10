---
name: Warehouse/PTL Domain Gap (First Non-OMS/ETL Entry)
description: P026/D031/S031 is the first KB entry outside the OMS-microservices/ETL-pipeline lineage; zero meaningful tag overlap with all prior entries
type: project
---

The knowledge base's first 25 problems (P001-P025) are entirely OMS-microservices or
ETL/batch-pipeline domain, all sharing a dense cluster of tags (oms, dotnet, ef-core,
postgresql, mssql, etl, airflow, etc.). P026 (CMG Put-to-Light warehouse integration:
WMS/SAP/PTL-MHE/Marketplace) introduced a genuinely new tag vocabulary
(warehouse-management, put-to-light, wms-sap-integration, mhe-plc-integration,
partial-fulfillment, task-orchestration) with essentially zero intersection against any
existing entry -- the highest overlap found was ~0.07 (a single shared generic tag like
event-driven-architecture against P013), well below anything meaningful.

**Why:** kb-search must not force a marginal match just because the KB is not empty --
an `empty_kb: false` KB can still produce a search result set that should functionally
be treated like "no relevant KB entries found" for lens-determiner's purposes.

**How to apply:** When a new problem's tags are warehouse/supply-chain/hardware-
integration flavored (WMS, SAP, MHE, PLC, put-to-light, pick-to-light, conveyor, AS/RS,
etc.), expect near-zero overlap against this KB's current OMS/ETL-heavy corpus and say
so explicitly rather than reporting a technically-nonzero but meaningless top match. If
a second warehouse/PTL problem arrives later, P026/D031/S031 becomes the correct anchor
to check for reuse (tag-overlap should be computed against it specifically, not just
generically against "high-severity" entries).
