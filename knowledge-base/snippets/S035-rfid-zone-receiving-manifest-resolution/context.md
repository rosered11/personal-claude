---
when_to_use: "Use when an existing gate/checkpoint-style manifest-resolution mechanism (D032's GateSession/IManifestCache) needs a third correlation strategy for a physical location that has no scheduling trigger to key off of -- specifically, when staff at a general receiving/staging zone must select which pending delivery a scan session belongs to from a short list, rather than the session being triggered by a scheduled physical event (a dock appointment) or a pre-assigned round id."
related_problems: [P030, P027]
related_decisions: [D035, D032]
---

# Snippet: Zone-Receiving Manifest Resolution -- Staff-Selected ManifestId as a Third GateSession Correlation Mode

This snippet demonstrates the D035 decision: a third, explicit `GateSession`
resolution path for warehouses with no dock-scheduling system, alongside the
two modes S032 already established (`movementRoundId` for internal/inter-site
transfer, `GetActiveManifestForGate` for dock-scheduled inbound/outbound
per D032 Addendum 5). It extends S032's types rather than replacing them --
`GateSession`'s zero-loss/zero-delay/fail-safe invariants (D032) and its
completeness reconciliation (`ComputeMissingExpectedEpcs`,
`ReconcileCountOnlyGtins`, D032 Addendum 3/4) are untouched by this decision.

It shows:

- **`GateSession.OpenForZoneReceiving(...)`** -- a new named factory method,
  not a new invariant. Resolves through the *already-generic*
  `IManifestCache.GetActiveManifestFor(siteId, manifestId)` -- the exact
  same port method `movementRoundId`-based transfer sessions already use,
  since both are "resolve by an opaque, ops-known key," not a
  physically-scheduled one. No changes to `GetActiveManifestFor` or
  `GetActiveManifestForGate` themselves; D032 Addendum 5/6/7's
  dock-appointment path is entirely untouched and remains available per
  site via the new `CorrelationMode` config value.
- **Resolution key is `ManifestId`, not `PoRef`.** A single PO can have
  multiple concurrent partial-delivery manifests (D032 Addendum 9's
  `PoRef`, shared across partials); resolving by bare `PoRef` would be
  ambiguous among them, which would violate the "resolve per-partial-
  delivery, not per-PO-total" constraint. `IManifestCache.
  GetPendingManifestsByPoRef(siteId, poRef)` is a **read-only convenience
  lookup** -- not a new correlation mechanism -- that a receiving-zone app
  uses to show staff which specific delivery(s) are outstanding for a PO,
  so staff select/scan a `ManifestId`. Ambiguity (more than one pending
  match) is never auto-resolved -- the same "ambiguous == absent, fail-safe,
  don't guess" principle D032 Addendum 5 established for overlapping dock
  windows, applied here to overlapping partial deliveries instead.
- **`MovementManifest.ConsumedAt`** -- a new nullable field, set by
  `GateSession.Close()` (via the `IManifestConsumptionMarker` port) once a
  zone-receiving session referencing that manifest closes successfully.
  This is what keeps `GetPendingManifestsByPoRef` from re-surfacing
  already-processed partials indefinitely, and gives the "Created ->
  Distributed -> Active -> Consumed/Expired" lifecycle language the
  original D032 decision text described, but never wired to a real field,
  an actual implementation.
- **`FailSafeMode` fallback is unchanged.** No `manifestId` supplied, or a
  supplied key that fails to resolve (typo, expired, already consumed),
  funnels into exactly the same `FailOpen`/`FailClosed` path every other
  `GateSession` resolution mode already has -- this snippet adds zero new
  fail-safe logic, only a third way to reach the existing one.
- **`CorrelationMode` as an explicit, auditable per-site value** -- not a
  hardcoded assumption. `SiteCorrelationConfig` (delivered through the same
  Site & Config Service heartbeat-push mechanism edges already use for
  every other piece of config) names which of the two inbound resolution
  modes (`DockAppointment` | `ZoneReceiving`) a site's receiving-zone app
  should call. This is the mechanism that lets D032 Addendum 5/6/7 keep
  serving dock-scheduled sites at full value while this warehouse (and any
  future confirmed no-dock-scheduling site) uses the new mode -- selected by
  config, not by code branching on a site list.

**Deliberately not built here**: a formal `IManifestResolutionStrategy` port
abstracting all three modes. D035 names the trigger for that refactor
explicitly (a confirmed fourth resolution mode) rather than building it
speculatively for three known, stable modes today -- see D035's
"Deliberately deferred" section. If you are extending this snippet with a
fourth mode, that is the signal to promote `OpenForZoneReceiving` /
`GetActiveManifestForGate` / the `movementRoundId` branch in the original
`GateSession` constructor (S032) into real strategy adapters instead of
adding a fourth factory method here.

**Confidence**: high on the resolution-key/ambiguity design (directly forced
by the per-partial-delivery constraint, and a direct reapplication of an
already-proven platform principle). Medium on deferring the strategy-port
refactor -- correct for three modes and one validated site, revisit without
hesitation once a fourth mode is confirmed needed.
