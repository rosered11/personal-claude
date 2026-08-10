---
name: RFID Event Platform Context
description: Domain facts for SCM IT's RFID Event Platform (P027/D032/S032) -- layered architecture, existing design principles, hard invariants, and why gate-verification problems in this domain are DDD-aggregate-shaped
type: project
---

The RFID Event Platform (SCM IT) is a 6-layer event-driven platform: Devices ->
per-site Edge agent -> Event Processor / Serialization Service / API Gateway ->
canonical event topics (partitioned by `site_id`) -> thin legacy adapters -> legacy
(WMS, Merchandise, POS, ERP). Source docs live at `inbox/RFID/docs/*` (HTML/SVG
architecture exports); a curated summary is maintained at
`manual/rfid-architecture-summary.md`. The platform itself has no prior formal KB
entry -- P027 is a sub-problem consultation on an already-designed but
never-KB-documented platform; check that summary file first for any future RFID
problem before treating a new inbox submission as greenfield.

Already-established design principles in this domain (treat as fixed constraints,
not open design questions, unless a new consultation explicitly revisits them):
- No synchronous registry calls from site/edge operations -- cache + pre-allocated
  ranges only (Serialization Service's `epc_registry`/`serial_range` are never
  called synchronously from a gate or handheld).
- Gate decisions are always made against a **locally cached, scope-specific**
  expected-item list (ASN at inbound, sales order at outbound, movement manifest
  for internal/inter-site transfer) -- never a global cross-platform registry check.
- At-least-once delivery + idempotent `event_id` everywhere; RFID multi-read and
  offline replay both produce natural duplicates, so the platform is designed to
  absorb duplicates rather than prevent them.
- Legacy systems never know about RFID -- everything stays inside the platform +
  edge, legacy only sees adapter-translated events.

Hard invariants that recur in this domain's gate-verification problems (push lens
choice toward a DDD aggregate that owns the invariant, not pure choreography):
- Zero-loss: every uniquely scanned tag in a session must get an evaluated verdict --
  no silent drop via dedupe/debounce.
- Zero-delay: the pass/alert decision must happen at the edge, synchronously, on the
  critical path -- no round trip anywhere.
- Fail-safe policy must be explicit (fail-open vs fail-closed) when no local data is
  available to evaluate against, and that choice must be auditable after the fact.

**Stack default for this domain:** C#, matching this repo's overall .NET-heavy
pattern library and the platform's existing services (Event Processor, Serialization
Service). Not directly confirmed against the real platform's implementation
language in the source docs -- flag this assumption if a future RFID consultation
reveals a different actual stack.
