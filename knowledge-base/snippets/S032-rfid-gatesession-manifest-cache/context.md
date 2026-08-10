---
when_to_use: "Use when a gate/checkpoint-style physical control point must evaluate a batch of scanned identifiers against a manifest scoped to one specific event/round (not a global registry), with hard zero-delay (local decision only) and zero-loss (every scanned identifier must be evaluated) requirements, and where the manifest itself may need to be pre-positioned to a remote site's edge ahead of a physical transfer."
related_problems: [P027]
related_decisions: [D032]
---

# Snippet: GateSession Domain Aggregate + Event-Pre-Positioned Manifest Cache

This snippet demonstrates the D032 decision: a `GateSession` aggregate (DDD)
that owns zero-loss/zero-delay/fail-safe invariants for one RFID gate
pass-through, evaluated against a `MovementManifest` read model that is kept
locally warm by a `ManifestSyncConsumer` subscribing to canonical event
topics (EDA transport half of the decision).

It shows:
- `GateSession.RecordRead(epc)` -- deduped-but-never-dropped read intake;
  every unique EPC gets exactly one verdict, evaluated synchronously against
  whatever `IManifestCache` currently holds locally (no network call).
- `GateSession.Close()` -- guarded transition that throws
  `InvalidOperationException` if any recorded EPC has no verdict yet,
  making the "zero-loss" constraint a code-enforced invariant instead of a
  convention.
- `FailSafeMode` (`FailOpen` / `FailClosed`) -- resolved once per session at
  open time from the current `IManifestCache` state (manifest present, stale,
  or entirely missing) and stamped onto every verdict, so a fail-open pass is
  always visibly distinguishable from a verified pass in the audit trail.
- `IManifestCache` (port) -- the local, edge-resident read model the
  `GateSession` depends on; it is written to only by `ManifestSyncConsumer`,
  which subscribes to `manifest.created`/`manifest.updated` events partitioned
  by destination `site_id` -- the exact mechanism that makes inter-site
  manifest pre-positioning work without any synchronous registry call.
- `IGateEventPublisher` (port) -- publishes `gate.transfer.evaluated` once
  per closed session, feeding the same reconciliation-job pattern already
  used platform-wide for adapters, so a `FailOpen` pass is never a silent
  gap.
- Plain constructor DI throughout -- no MediatR, no AutoMapper, per this
  repository's .NET standards.

Architecturally, this is the boundary to reuse for any future RFID gate flow
that needs "evaluate a batch against a scoped expected-list, locally, with a
defined fail-safe behavior" -- new movement types should add a new
`MovementManifest` source, not a new gate-decisioning mechanism.
