---
id: D035
chosen_option: "Manifest-Instance Resolution by Staff-Selected Delivery Reference (ManifestId) as a Third Named GateSession Resolution Mode, with D032 Addendum 5/6/7 Retained as a Per-Site-Configurable Alternative"
problem_id: P030
tags: [rfid, edge-computing, gate-verification, manifest-sync, domain-driven-design, hexagonal-architecture, offline-first, fail-safe, warehouse-management, inbound-receiving]
related_snippets: [S035]
---

# Decision: Manifest-Instance Resolution by Staff-Selected Delivery Reference, Retaining D032 Addendum 5/6/7 as a Per-Site Alternative

## Context

P030 confirms, from a real site visit, that D032 Addendum 5/6/7's dock-
appointment correlation mechanism cannot function at a warehouse with no
dock-scheduling concept -- `gate_id`/`ScheduledWindow` are never populated,
so `GetActiveManifestForGate` permanently resolves `null` and every inbound
session at that site falls back to `FailSafeMode`, universally. kb-search
against the existing 29 KB entries found P027 as by far the closest
precedent (shared tags: `rfid`, `edge-computing`, `gate-verification`,
`manifest-sync`, `offline-first`, `warehouse-management`, `fail-safe`,
`event-driven-architecture`), at an overlap score of roughly 0.6 -- high, but
below the 0.8 UPDATE threshold, consistent with how P028 and P029 were also
correctly treated as new CREATE-mode records despite being on the same
platform and sharing several tags with P027. This decision therefore creates
a new P030/D035/S035 record rather than overwriting P027/D032/S032, while
being explicit throughout about exactly which parts of D032 Addendum 5/6/7
it retains, demotes, and leaves untouched.

## Options Considered

**Lens A -- Domain-Driven Design (pragmatic in-aggregate extension)**: add a
third, explicit, named resolution path directly onto `GateSession`, following
the same "nullable key, branch in the constructor" shape Addendum 5 already
used successfully to add the gate+window path alongside `movementRoundId`.
Concretely, a new factory method resolves via the *already-generic*
`IManifestCache.GetActiveManifestFor(siteId, key)` -- the same port method
`movementRoundId` already uses, since both are "look up by an opaque,
ops-known key" rather than a physically-scheduled key. No change to
`IManifestCache`'s two existing resolution methods. The key supplied is the
specific delivery's `ManifestId` (not bare `PoRef`), selected/confirmed by
staff from a new lightweight picklist query, because a single PO can have
multiple concurrent partial-delivery manifests and `PoRef` alone would be
ambiguous among them -- the same "ambiguity must fail-safe, never guess"
principle Addendum 5 already established for overlapping dock windows.
Pros: zero new abstraction layer, directly reuses trusted, already-proven
code shape; every resolution mode stays visible and named right in
`GateSession`'s own construction, keeping the audit trail legible without
chasing a strategy implementation class. Cons: `GateSession`'s resolution
logic now branches three ways instead of two, and would branch a fourth time
if another operational model is later confirmed -- a form of accreting
complexity that has already happened once (Addendum 5 added the second
branch) and would repeat.

**Lens B -- Hexagonal Architecture (resolution-strategy port)**: introduce a
formal `IManifestResolutionStrategy` port with adapters for each known mode
(`RoundIdResolutionStrategy`, `DockAppointmentGateWindowResolutionStrategy`
wrapping Addendum 5 unchanged, and a new `ZoneStaffSelectedManifestStrategy`).
`GateSession`'s constructor takes a single strategy instance chosen by the
caller, decoupling the aggregate entirely from knowing how many resolution
mechanisms exist. Each site's config (delivered via the existing Site &
Config Service heartbeat push) names which strategy its inbound flow uses.
Pros: adding a future 4th/5th mode requires zero changes to `GateSession`
itself, only a new adapter class -- directly answers the Clarified Scope's
forward-looking requirement to support dock-scheduled and non-dock-scheduled
DCs simultaneously as a first-class extensibility point, not an incidental
side effect; each strategy becomes independently unit-testable; matches this
platform's own precedent of reaching for a Hexagonal port exactly where
"which external process supplies this" is expected to vary (D033's ingestion
transport port, D030's secrets port). Cons: for exactly three known, stable
modes today, this is a real abstraction investment (new interface, three
adapter classes, a config-driven DI/lookup wiring layer) to solve a problem
the codebase has so far solved successfully with a two-way nullable branch;
also somewhat obscures the audit-trail clarity Addendum 5 valued, since
"which mode ran" now requires inspecting which concrete adapter executed
rather than reading it directly off the constructor call.

Both architects agreed this is a genuinely different kind of split than
prior RFID decisions: not "who owns the invariant vs. how data moves"
(D032), not "which transport wins outright" (D033), and not "how much
eventual consistency before an irreversible action" (D034) -- this is a
premature-abstraction question: when does a third known variant of the same
concern earn a formal strategy interface versus staying an explicit branch
in the aggregate that already owns the concern.

## Decision

Adopt **Lens A (DDD/pragmatic extension)** as primary, directly because the
Clarified Scope's own steer ("evaluate whether the same pattern... is the
right fit here, rather than inventing something new") and the platform's
demonstrated precedent (Addendum 5 already added a second branch this exact
way, successfully, with no reported problems) both point the same direction.
**Lens B's insight is folded in as an explicit, named future trigger, not
rejected** -- see "Deliberately deferred" below.

**1. What reference opens a session at the zone.** `GateSession` gains a
third resolution mode via a new factory method,
`GateSession.OpenForZoneReceiving(sessionId, siteId, manifestId, openedAt,
manifestCache, gtinResolver, fallbackModeWhenNoManifest)`, resolving through
the existing `IManifestCache.GetActiveManifestFor(siteId, manifestId)` --
literally the same port method `movementRoundId` already uses. The supplied
key is the specific delivery's **`ManifestId`**, not bare `PoRef`: a PO can
have multiple concurrent partial-delivery manifests (explicitly required by
the "resolve per-partial-delivery, not per-PO-total" constraint), so `PoRef`
alone would be ambiguous among them in exactly the way Addendum 5 already
taught this platform to treat as fail-safe, not guessable.

A new read-only convenience query, `IManifestCache.GetPendingManifestsByPoRef
(siteId, poRef)`, lets a receiving-zone app show staff which specific
delivery(s) are outstanding for a PO they select -- **this is a lookup, not a
new correlation mechanism or a scheduling system**. If exactly one manifest
matches, the app may auto-select without staff intervention; if more than
one match exists, selection is mandatory and never silently guessed, the
identical principle Addendum 5 established for overlapping dock windows.

A new field, `MovementManifest.ConsumedAt` (nullable), is set once a
`GateSession` opened against that manifest successfully `Close()`s -- this
is what keeps `GetPendingManifestsByPoRef` from re-surfacing already-
processed partials indefinitely, and gives the "Created -> Distributed ->
Active -> Consumed/Expired" lifecycle language the original D032 decision
text already described, but never actually wired up as a field, a real
implementation.

`FailSafeMode` fallback is completely unchanged: no reference supplied, or a
supplied `ManifestId` that doesn't resolve (typo, expired, already
consumed), funnels into the exact same `FailOpen`/`FailClosed` path every
other resolution mode already has.

**2. D032 Addendum 5/6/7's treatment: retained as one of three supported
resolution paths, demoted from "the" mechanism to one-of-N, not superseded.**
Per the Clarified Scope's explicit instruction not to assume this
generalizes to every DC, Addendum 5/6/7 is **not** deprecated or removed.
Sites confirmed to run real WMS/TMS dock scheduling keep using it entirely
unchanged (WMS Adapter reverse-sync, `PendingDockAppointment` staging,
`PoRef` join, `GetActiveManifestForGate` resolution). A new per-site config
value, `inbound_correlation_mode` (`DockAppointment` | `ZoneReceiving`),
delivered through the existing Site & Config Service heartbeat-push
mechanism -- a config value, not a new system, satisfying the "no
re-introduction of a scheduling system" constraint -- selects which mode a
given site's receiving-zone app uses. This warehouse (and any other site
later confirmed to lack dock scheduling) is configured to `ZoneReceiving`.

**3. Per-partial-delivery completeness keeps working unchanged, by
construction.** Each partial delivery is already its own `MovementManifest`/
`ManifestId` (D032 Addendum 3's per-ASN manifest creation, `PoRef` shared
across partials per Addendum 9). This decision's entire job was making sure
`GateSession` resolves the *correct* one of potentially several manifests
sharing a `PoRef` -- not changing how `Close()`, `ComputeMissingExpectedEpcs()`,
or `ReconcileCountOnlyGtins()` evaluate a session once it is open. The one
genuine new risk this decision had to close was resolving to the *wrong*
manifest instance (an already-consumed prior partial, or a not-yet-arrived
future one) if resolution were naively done by bare `PoRef` -- closed by
keying on `ManifestId` with `ConsumedAt` filtering, not by any change to the
zero-loss/zero-delay/fail-safe/completeness invariants themselves.

**Deliberately deferred, not rejected**: promoting the three now-named
resolution paths (`movementRoundId`, dock-appointment gate+window, and this
new zone-selected `ManifestId`) into a formal `IManifestResolutionStrategy`
port. The trigger for revisiting this is named explicitly rather than left
implicit: **the moment a confirmed fourth resolution mode is needed** (a
second site visit surfacing a third distinct operational model beyond
dock-scheduled and zone-receiving), promote to Lens B's strategy-port design
rather than adding a fourth branch to `GateSession`'s constructor. This is
the same YAGNI stance the platform has already taken once before (D032
Addendum 3 explicitly declined to build speculative manifest chunking until
a real message-size problem was confirmed) -- premature now, for three
known, stable, already-well-understood modes serving one validated site,
but not a door closed permanently.

## Consequences

**Accepted trade-offs**:
- `GateSession` construction now has three named paths instead of two --
  modestly more surface area to reason about and test, though each path
  remains simple and independently testable, and the strategy-port
  refactor trigger is named explicitly for when a fourth path arrives.
- Depends on a receiving-zone application that can query
  `GetPendingManifestsByPoRef` and present a picklist to staff -- this
  decision defines the platform-side contract, not the actual zone UI,
  which is a separate build item outside RFID Event Platform's own scope.
- `ConsumedAt` introduces a small new write path that must be set reliably
  on every successful `Close()` via the zone-receiving mode, or already-
  processed manifests will keep reappearing in the picklist -- a UX/ops
  annoyance, not a correctness break, since zero-loss/fail-safe are
  untouched either way.
- A new per-site `inbound_correlation_mode` config flag must be set
  correctly per DC; getting it wrong (e.g. defaulting a no-dock-scheduling
  site to `DockAppointment`) reproduces the exact 100% fail-safe-fallback
  failure this decision exists to fix, just relocated to a config error.

**Benefits**:
- No new abstraction layer, no new port/adapter machinery, and only one new
  read-only `IManifestCache` query -- directly satisfies "reuse existing
  patterns... rather than inventing something new," the platform's design
  principle since Addendum 5.
- Fail-safe behavior is provably unchanged across all three resolution
  modes: every path still funnels through the same nullable-resolution-
  result to `FailSafeMode` fallback `GateSession` has always had.
- Zero-loss/zero-delay/completeness invariants (D032 Addendum 1 through 4)
  are entirely untouched -- this decision only changes which manifest gets
  bound to a session, never how a bound session is evaluated once open.
- D032 Addendum 5/6/7's real engineering investment (WMS Adapter reverse-
  sync, `PendingDockAppointment` staging, `PoRef` join) is preserved at
  full value for sites that genuinely have dock scheduling, rather than
  discarded on the strength of one site's finding that the Clarified Scope
  itself warns should not be assumed universal.
- Directly closes P027 Open Item #12 ("every inbound PO is assumed to get a
  dock appointment -- unvalidated") -- now validated false for one real
  site, and generalized into a second, equally-supported resolution mode
  rather than left as an unresolved risk note.

**Confidence**: high on the resolution-key design (`ManifestId` over bare
`PoRef`) -- directly forced by the "per-partial-delivery, not per-PO-total"
constraint, and mirrors Addendum 5's already-proven ambiguity-handling
principle exactly, not a new judgment call. Medium on the YAGNI-over-
strategy-port call: correct given only three known modes and one validated
site, but should be revisited without hesitation the moment a second
no-dock-scheduling site or a third distinct operational model is confirmed,
since the DDD option's stated cost (accreting constructor branches) is real
and would compound if left unaddressed past that point.
