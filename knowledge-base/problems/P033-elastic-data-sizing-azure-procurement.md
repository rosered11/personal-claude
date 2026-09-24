---
id: P033
title: "Elasticsearch Data-Size Estimation for Azure Procurement"
date: 2026-09-22
tags: [elasticsearch, azure, capacity-planning, data-sizing, cloud-infrastructure, observability, index-lifecycle-management]
related_decisions: [D038]
related_snippets: [S038]
---

# Elasticsearch Data-Size Estimation for Azure Procurement

## Problem

The team must produce a data-sizing estimate for an Elastic (Elasticsearch) deployment
on Azure so the infrastructure team can select and provision the correct SKU/tier, but
no methodology, baseline data volumes, or query/retention requirements have been defined
yet. This is a capacity-planning/procurement-support request, not an incident.

## Root Cause

Undetermined -- the request itself states the requester does not know where to start.
The likely underlying causes are (1) no existing capacity-planning process for
search/analytics infrastructure in this organization, and (2) the candidate data
sources, document shapes, and retention requirements for the planned Elastic deployment
have never been enumerated.
[MISSING: no information on which upstream systems would feed Elastic, expected
document/event volume, growth rate, or query use case]

## Summary

The requester needs to give the infra team a data-sizing estimate to select an Elastic
(Elasticsearch) SKU on Azure but has no starting methodology. The real blocker is that
neither the scope of data to be indexed (which systems, what fields, what retention)
nor the query/access patterns are yet defined, and both directly determine the
storage/compute estimate. Getting this wrong risks either under-provisioning (missed
SLAs, hot-tier storage pressure, shard-per-node limits) or over-provisioning
(unnecessary Azure spend on an unproven workload). The consultation's output is a
repeatable sizing methodology and worksheet the requester can populate and hand to
infra, not a single fixed number.

## Context

The organization already runs Azure-based systems in this KB's lineage (Sprint-OMS on
AKS, RFID Event Platform, PTL warehouse integration), but this is the first request in
the KB for an Elasticsearch/search-analytics data store. No details were given on:
which system(s) would source data into Elastic (logs? order/activity events? RFID
events? product search?), current data volume or growth rate, required retention
window, query/aggregation patterns, or whether this is a new deployment or a resizing
of an existing one.

- [MISSING: source systems]
- [MISSING: current or projected document volume]
- [MISSING: retention/compliance requirements]
- [MISSING: query patterns / use case -- logging vs. product search vs. analytics]

Given this KB's existing observability gaps (P004 lacked batch metrics; P022 traced
audit noise to index-rebuild internals), a plausible use case is centralizing
application/activity logs or search, but this is an inference, not a stated fact -- the
resulting guideline is written to remain valid regardless of which use case applies.

## Constraints

- Output must be usable by the infra team to select/procure an Elastic tier on Azure --
  it must resolve to concrete storage/vCPU/node-tier numbers, not just qualitative advice.
- No data volume, retention, or query-pattern inputs have been supplied yet, so the
  deliverable must be a methodology/worksheet the requester can populate, not a single
  hardcoded estimate.

## Severity

Medium -- blocks a procurement decision but is not an operational incident.

## Affected Components

- Elastic/Elasticsearch cluster (Azure)
- Infra/platform team procurement process
- Unspecified upstream source system(s) [MISSING]

## KB Search Result

kb-search against the existing 32 problems / 37 decisions found no meaningful precedent:
top problem matches were P004 (etl/observability, overlap ~0.14), P022 and P018
(observability, overlap ~0.13 each, tied and broken by recency) -- all sharing only the
generic `observability` tag, well below the 0.8 UPDATE threshold. This correctly became
a new CREATE-mode record establishing the KB's first Elasticsearch/Azure
capacity-planning precedent.
