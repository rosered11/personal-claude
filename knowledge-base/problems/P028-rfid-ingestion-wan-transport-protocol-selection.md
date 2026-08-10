---
id: P028
title: "RFID Ingestion Service -- Edge-to-Platform WAN Transport Protocol Selection"
date: 2026-08-10
tags: [rfid, edge-computing, offline-first, transport-protocol, batch-processing, idempotency, horizontal-scaling, wan-integration]
related_decisions: [D033]
related_snippets: [S033]
---

# RFID Ingestion Service -- Edge-to-Platform WAN Transport Protocol Selection

## Problem

The RFID Event Platform's Ingestion Service (see `manual/rfid-architecture-summary.md`
and `inbox/RFID/docs/*`) is documented as "the only edge-facing surface" -- every edge
agent (DC Site Server, Store Gateway) across every site sends event batches into it.
The source spec (`RFID_Platform_Software_Architecture.svg`) precisely defines the
Ingestion Service's *behavior* (envelope validation, `schema_version` check, dedupe on
`event_id` for safe offline replay, append to event store, publish to canonical topics,
stateless horizontal scaling for campaign peaks) and the edge's *behavior* ("send event
batches with edge-generated `event_id`", "request serial ranges", "receive config &
sold-lists") -- but it never names a transport protocol for the WAN hop between them.
MQTT does appear in the spec, but scoped exclusively to the *local* hop inside a single
site (reader/vendor device -> edge agent, the documented vendor "acceptance boundary")
-- not the cross-WAN hop to the central platform. This decision closes that gap.

## Root Cause

The platform's documentation fully specified *behavioral* requirements on both ends of
this hop (idempotent envelope handling, stateless horizontal scaling, offline-tolerant
edge buffering) but never closed the "how do bytes actually cross the WAN" decision.
MQTT's presence elsewhere in the spec is not evidence of an implicit choice for this
hop -- it is explicitly scoped to a different hop with a different latency/ownership
profile (LAN, vendor-owned up to that boundary) -- so nothing in the existing
documentation constitutes an actual transport decision here, and none exists to extend
or contradict.

## Summary

The RFID Event Platform needs a formally decided transport protocol for the edge (DC
Site Server / Store Gateway) -> central Ingestion Service hop, distinct from the
already-reserved local MQTT hop (reader -> edge agent). The choice directly shapes how
the edge's offline buffer is implemented and flushed, how per-site auth and
firewall/proxy traversal are handled across a large number of geographically
distributed retail and warehouse sites, how the Ingestion Service scales horizontally
for campaign peaks (7.7/11.11) without accumulating per-client state, and how the
platform's existing at-least-once + idempotent-`event_id` design is honored end to end.
This is the second formal RFID Event Platform consultation in the KB (the first,
P027/D032/S032, addressed gate-level manifest verification for internal/inter-site
transfer) -- a distinct problem on the same platform, not a revision of that decision.

## Context

- **Owning platform**: RFID Event Platform (SCM IT), the same 6-service event-driven
  platform documented in `manual/rfid-architecture-summary.md`: Devices -> per-site
  Edge agent -> Ingestion Service / Event Processor / Serialization Service / API
  Gateway -> canonical event topics (partitioned by `site_id`) -> thin legacy adapters
  -> legacy systems.
- **Ingestion Service** is explicitly documented as stateless and the sole edge-facing
  surface: envelope validation, `schema_version` check, `event_id` idempotency/dedupe
  (safe offline replay), append-to-event-store, publish-to-canonical-topics -- and it
  must "scale with load; absorb 7.7/11.11 campaign peaks."
- **Edge context** (DC Site Server / Store Gateway) is documented as sending "event
  batches (edge-generated `event_id`)", requesting serial ranges, and receiving config
  & sold-lists -- and already implements an offline buffer (store-and-forward, >=24h,
  ordered replay) for the *local* device-to-edge path.
- **The local MQTT hop** (reader/vendor device -> edge agent, inside one site's LAN) is
  a distinct, already-reserved concern with its own latency profile and a documented
  vendor "acceptance boundary" -- it is not available to be silently reused for this
  WAN hop, per explicit platform design intent.
- **Prior RFID KB precedent**: P027/D032/S032 established this platform's first formal
  KB entry (GateSession aggregate for manifest-based tag verification, DDD as primary
  lens with EDA folded in as manifest-pre-positioning transport). That decision governs
  a different concern (gate-level invariant enforcement) and this decision does not
  revise it -- both share the `rfid`/`edge-computing`/`offline-first` tag family because
  they are the same platform, not because they are the same problem.

## Constraints

| Rule | Detail |
|---|---|
| At-least-once + idempotent | Every event carries an edge-generated `event_id`; Ingestion Service must dedupe reliably. |
| Offline-tolerant >= 24h | Edge must buffer locally through WAN outages and replay in order once reconnected. |
| Stateless, scale to peak | Ingestion Service must scale horizontally for campaign peaks (7.7/11.11) with no per-client session state. |
| Batch-oriented | Edge sends event batches, not individual events one at a time. |
| Multi-site, widely distributed | Must work identically for DC sites (few, better bandwidth) and Store sites (many, variable network quality). |
| Must not reuse the local MQTT hop's operational model | MQTT is reserved for the low-latency local reader->edge hop; the WAN hop has different requirements (batch, offline-tolerant, must cross enterprise firewalls). |
| Reliable ack required | Edge must know with certainty which batch the server actually received before purging its local offline buffer. |

## Severity

high -- this is a foundational integration decision that blocks concrete implementation
of the edge offline buffer, per-site auth, and Ingestion Service's peak-scaling design;
getting it wrong is expensive to unwind once thousands of Store Gateways are deployed
against it.

## Affected Components

- RFID Event Platform -- Ingestion Service (the only edge-facing surface)
- DC Site Server / Edge agent (offline buffer, batch sender)
- Store Gateway / Edge agent (offline buffer, batch sender)
- Canonical event topics / message broker (downstream of Ingestion Service, unaffected
  by this hop's protocol choice)
- Site & Config Service (per-site auth/config distribution, if reused for this hop's
  credentials)
