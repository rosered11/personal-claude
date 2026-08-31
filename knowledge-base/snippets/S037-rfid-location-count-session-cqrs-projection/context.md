---
when_to_use: "Use when a location-scoped cycle count needs an expected-EPC baseline that the platform must derive from its own last-known state (epc_registry), not an externally-declared document -- and that baseline must correctly account for items packed inside a sealed, individually-unread container sitting at that location (D036 container-contents interaction)."
related_problems: [P032]
related_decisions: [D037]
---

# Snippet: LocationCountSession -- GateSession-Sibling Aggregate + Container-Aware CQRS Projection

This snippet demonstrates the D037 decision: a new `LocationCountSession`
type, structurally a sibling to `GateSession` rather than a fifth
`GateSession.OpenForXxx` resolution mode, reusing `GateSession`'s proven
zero-loss/zero-delay/fail-safe invariant *shape* while resolving its
expected-list baseline through a genuinely new mechanism -- a continuously
materialized, container-aware CQRS projection (`location_contents`) -- since
a self-asserted location snapshot is a structurally different kind of
"expected list" than any declared `MovementManifest`.

It shows:
- **`LocationContentsProjectionSql`** -- the CQRS half: an illustrative
  projection-build query run centrally (Serialization DB), folding two
  sources via `UNION ALL` -- EPCs stamped directly at a location, and EPCs
  packed inside a container that is itself stamped at that location, joined
  against D036's `container_contents` table for free since both tables live
  in the same PostgreSQL database. This is what closes P031 Open Item 1 for
  this flow **by construction**, at the baseline's source, rather than
  requiring every downstream consumer to remember a cross-reference rule.
- **`LocationContentsSnapshot`** -- the versioned, checksum-validated
  projection output, reusing D032 Addendum 1's completeness-proof pattern
  verbatim.
- **`ILocationContentsCache`** -- the edge-local read port, deliberately
  parallel in *shape* to `IManifestCache` but a distinct interface, because
  a location snapshot has no `Created -> Distributed -> Active ->
  Consumed/Expired` lifecycle -- it is continuously refreshed, never
  "consumed." Fanout is site-scoped, reusing D036's asymmetric-fanout
  discipline.
- **`LocationCountSession`** -- the DDD half: `RecordRead` (zero-loss),
  synchronous evaluation (zero-delay), explicit `FailSafeMode`, all reused
  field-for-field from `GateSession`'s proven shape (S032).
- **`ComputeMissingExpectedEpcs`** -- the enforcement half of closing P031
  Open Item 1: an expected EPC missing its own `Expected` verdict is
  suppressed from the missing-list only if it is container-resolved in the
  baseline **and** that specific container was itself read this session --
  never suppressed just because it is theoretically container-packed
  somewhere. This is the first implementation of the cross-reference D036
  flagged but never built.
- **`IEpcLocationWriter`** -- the write-side stamping port, reusing the
  existing `GateSessionResult`-shape event consumer (Appendix 4 group 1)
  rather than introducing a new write path per flow.
- Plain constructor DI throughout -- no MediatR, no AutoMapper, per this
  repository's .NET standards.

**Known open item this snippet does not resolve (P032 Open Item 1,
platform-wide scope)**: the container cross-reference implemented here is
scoped to `LocationCountSession` only. The three pre-existing `GateSession`
flows (internal/inter-site transfer, inbound ASN, outbound pick-verify)
still do not implement this check against their own `MissingExpectedEpcs`
-- P031/D036 Open Item 1 remains open for those flows until retrofitted.

**Confidence**: medium -- see D037's confidence reasoning; the
invariant-reuse structure and transport reuse are high-confidence, but this
is the platform's first continuously-materialized projection with no prior
operational tuning experience (refresh cadence, staleness tolerance).
