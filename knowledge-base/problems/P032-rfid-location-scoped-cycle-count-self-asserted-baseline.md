---
id: P032
title: "Location-Scoped Cycle Count -- Deriving a Self-Asserted Expected-EPC Baseline From Platform-Owned State, Without a Synchronous Call"
date: 2026-08-24
tags: [rfid, edge-computing, offline-first, domain-driven-design, cqrs, warehouse-management, cycle-count, gate-verification]
related_decisions: [D037]
related_snippets: [S037]
---

# Location-Scoped Cycle Count -- Deriving a Self-Asserted Expected-EPC Baseline From Platform-Owned State

## Problem

The RFID Event Platform's only existing cycle-count capability
(`count.completed`, Event Processor flow #3, `manual/rfid-component-
reference.md` Appendix 4) compares what a handheld sled counts against
**organization/site-level book stock** -- a pure quantity reconciliation, not
an expected-EPC-list match. There is no concept today of "this specific
location (zone/bin/shelf) should contain these specific EPCs" as a baseline
to scan against, even though `count.completed`'s own payload already carries
a `zoneId` field that nothing currently uses for baselining -- it is passed
through to the central variance calculation but never compared against a
location-scoped expected set.

A real warehouse site visit -- the third and final consultation queued from
the same visit that produced P030/D035 and P031/D036 -- surfaced a concrete
requirement: **"ต้องทำระบบ scan cycle count by locations ด้วย โดยจะมีตั้งต้นว่า
location นี้มี epc ภายใต้ location นี้อะไรบ้าง"** -- a cycle-count flow scoped to
a specific physical location, with a starting baseline of "which EPCs are
expected to be at this location," not an aggregate site-wide book-stock
number.

This is structurally close to what `GateSession` already does (evaluate a
batch read against a locally-cached expected list, zero-loss, zero-delay,
fail-safe), but with a difference that must be resolved, not assumed away:
**the expected list's source is different.** Every existing `GateSession`
flow gets its expected list from an externally-declared document (ASN, Sales
Order, `MovementManifest`) published by a planning system ahead of time. A
location's expected-EPC baseline has no such document -- it must be derived
from **the platform's own last-known state**, which is a fundamentally
different kind of "expected list": one the platform asserts about itself,
not one an external actor declares.

## Root Cause

`epc_registry` (Serialization DB, component #14) tracks `site_id` and
lifecycle `status` (`encoded -> in_stock -> ... -> sold`, plus
`voided`/`returned`) but nothing below site-level -- no zone/bin/shelf
column exists at all. Every prior `GateSession`-family expected list
(`MovementManifest`, resolved via `movementRoundId`, dock-appointment
gate+window, or D035's staff-selected `ManifestId`) is sourced from an
externally-declared document known ahead of the physical event; none of the
platform's three existing manifest-resolution modes, nor D036's SSCC
container-read branch, has ever needed to derive its own expected list from
the platform's own accumulated state. There is structurally nothing in the
current schema or transport pipeline to build a location-scoped baseline
from, and no precedent for "the platform asserting an expected list about
itself" as opposed to "an external planning system declaring one."

## Summary

A real warehouse site visit asked for location-scoped cycle counting: scan a
specific zone/bin/shelf and get a verdict per EPC against "what should be
here," not just a site-wide quantity variance. This is the platform's sixth
formal RFID Event Platform consultation (after P027/D032, P028/D033,
P029/D034, P030/D035, P031/D036), and the third and last of three
consultations queued from one site visit (after P030/D035's zone-receiving
mode and P031/D036's container modeling). It is also the first RFID
consultation whose core design question is not "how does an expected list
reach the edge in time" but "how does an expected list get *produced* at
all, when there is no external document to source it from." It directly
interacts with P031/D036's still-open container-contents risk: a
location-scoped count is exactly the kind of session where a sealed
container's contents could be falsely reported missing from that location.

## Context

- **Owning platform**: RFID Event Platform, same 6-layer event-driven
  platform documented in `manual/rfid-architecture-summary.md` and
  `manual/rfid-component-reference.md`.
- **Existing cycle-count flow (site-wide, unchanged by this problem)**:
  a handheld/sled publishes `count.completed` (`countSessionId`, `siteId`,
  `zoneId`, `serializedEpcsCounted[]`, `countOnlyCountsByGtin[]`,
  `countedAt`) after a count session; Event Processor (central) computes
  variance against organization-wide book stock, because book stock is
  central data the edge does not have. This flow is explicitly **not**
  being replaced -- it stays exactly as-is; location-scoped counting is a
  new, narrower, additive capability.
- **`epc_registry` schema (component #14)**: `epc` (PK), `gtin`, `serial`,
  `sku` (denormalized), `status`, `site_id`, `tid`, `updated_at` -- no
  location column at any granularity.
- **`GateSession` family's existing expected-list sourcing**: internal/
  inter-site transfer resolves via `movementRoundId` (D032); inbound/
  outbound resolves via dock-appointment gate+window (D032 Addendum 5/6/7)
  or, at sites with no dock scheduling, staff-selected `ManifestId` (D035);
  SSCC container reads resolve via the container-contents cache (D036).
  Every one of these four paths reads a document some other system already
  declared (a movement plan, an ASN, a dock booking, a container-packing
  event) -- none derives its expected list from `epc_registry` itself.
- **`container_registry`/`container_contents` (D036, Serialization DB)**:
  models a container EPC's contents relationally, fanned out to the edge in
  the container -> contents direction only. P031 Open Item 1 (highest
  priority, still open platform-wide): items packed inside a sealed,
  RF-occluded container may never be individually read, and would falsely
  surface in `ComputeMissingExpectedEpcs()` (D032 Addendum 4) unless
  downstream logic cross-references `ContainerReads` first -- flagged, not
  implemented, by D036.
- **Same site visit, two prior consultations**: P030/D035 (a warehouse with
  no dock-scheduling concept, resolved via a third named `GateSession`
  resolution mode keyed on `ManifestId`) and P031/D036 (container/SSCC
  modeling, resolved via a DDD-owned relational model with CQRS-scoped edge
  fanout). Both are directly relevant precedent for this problem's two
  hardest questions: whether a new kind of expected list earns a new
  `GateSession` resolution mode (P030/D035's question, different axis) and
  how container-packed contents interact with a location's expected set
  (P031/D036's still-open risk, this problem's direct continuation of it).

## Clarified Scope (already decided, not open questions)

- Reuse `GateSession`'s zero-loss/zero-delay/fail-safe invariants and its
  existing verdict/reconciliation machinery (`MissingExpectedEpcs`,
  `CountMismatches`) as the starting point -- do not invent a parallel
  evaluation engine for this if the existing one can be extended.
- This is explicitly **not** the same problem as the existing site-wide
  book-stock variance flow (Event Processor, flow #3/`count.completed`) --
  that flow stays as-is; this is a new, narrower, location-scoped
  capability, not a replacement.
- Whatever location granularity is introduced must not require a
  synchronous central call at the moment of a location-count scan (same
  offline-first principle as every other edge flow).

## Constraints

| Rule | Detail |
|---|---|
| Zero-delay / zero-loss | The location-count session must evaluate at the edge, synchronously, exactly like every other `GateSession`-family flow. |
| Baseline source is internal, not external | Unlike every prior `GateSession` flow, the expected list here comes from the platform's own last-known-location state, not a planning document -- the design must address how/whether this "self-asserted" expected list is pre-positioned to the edge the same way external manifests are. |
| Location granularity must be introduced deliberately | Adding a location concept to `epc_registry` is a schema change with knock-on effects (every write path that currently only touches `status`/`site_id` needs to decide whether it also touches location) -- do not bolt this on implicitly. |
| Container interaction must be addressed | Per P031/D036, container contents may not be individually read -- a location count must not misreport container-packed items as "missing" from their location just because the antenna only read the container's SSCC, not each item inside. |
| Reuse over invention | Evaluate extending `GateSession` with a fourth/fifth resolution mode (alongside D032's `movementRoundId`, Addendum 5's `gate_id`+window, and D035's `ManifestId`) before proposing a separate evaluation mechanism. |

## Severity

high -- cycle counting is the platform's only mechanism for catching
physical inventory drift (shrink, misplacement, mis-putaway); a wrong
baseline-derivation design either produces a flood of false-missing
exceptions (if container-packed items are not handled) or silently
undercounts real loss (if the baseline itself is built incorrectly),
undermining the business value of the entire exercise either way.

## Affected Components

- RFID Event Platform -- Serialization DB (component #14 -- new
  `location_id` column on `epc_registry`, new `location_contents`
  projection table)
- RFID Event Platform -- Serialization Service (new location-stamping
  write path, new projection-materialization responsibility)
- RFID Event Platform -- Site & Config Service / edge cache transport
  (candidate reuse path for pre-positioning a location snapshot, parallel
  to but distinct from `IManifestCache`)
- RFID Event Platform -- Event Processor / `GateSession` family (candidate
  new sibling session type)
- RFID Event Platform -- Event Processor (central) -- existing
  `count.completed`/book-stock variance flow (flow #3): explicitly **not**
  modified by this problem, verified unaffected

## Open Items (identified during this consultation, logged 2026-08-24)

1. **Container-interaction check is implemented for this flow only, not
   platform-wide.** D037 implements the cross-reference P031/D036 flagged
   but never built (compare `MissingExpectedEpcs` against `ContainerReads`
   before flagging a shortage) for the new `LocationCountSession`
   specifically. The other three `GateSession` flows (internal/inter-site
   transfer, inbound ASN, outbound pick-verify) still do not implement this
   check -- P031 Open Item 1 remains open platform-wide until it is
   retrofitted there too.
2. **Location granularity (zone vs. bin vs. shelf) is deferred to a
   per-site config value, defaulting to zone-level**, mirroring D035's
   "config value, not a new system" discipline -- the actual granularity
   needed at any given site is an operations decision this consultation
   does not make.
3. **`location_contents` projection refresh cadence/staleness tolerance is
   not specified.** Unlike a `MovementManifest` (tied to one planning
   event, versioned, eventually consumed), a location snapshot is
   continuously live -- how fresh it must be before a count session trusts
   it is an operations-tuning question, the same class of gap D032's
   inter-site fail-safe-frequency tuning left open for manifest lead time.
4. **Full write-path audit is not exhaustive.** This decision specifies
   that `GateSession.Close()` for flows with a known destination location
   stamps `epc_registry.location_id`, but does not enumerate every existing
   write path (pick, ship, sale, void, return) and whether each should set,
   clear, or leave location untouched -- needed before implementation.
5. **A location baseline is only as accurate as write-path discipline.**
   An EPC physically relocated by a person with no scan event (a common
   real-world drift source) will not be reflected in `location_contents` --
   this is not a bug to fix, it is exactly the class of drift cycle
   counting exists to catch, but implementers must not over-trust the
   projection as ground truth.
6. **SGLN (location-identifying EPC scheme) remains unsupported.**
   `manual/rfid-component-reference.md` Appendix 6 documents SGLN-96
   ("Serialized Global Location Number") as a real GS1 scheme for
   dock-door/location signage, currently entirely out of scope for this
   platform. Physically RFID-tagging locations themselves (rather than
   relying on software-only location stamps) was not proposed or evaluated
   here -- noted as a possible future direction, not a gap in this decision.

Next step, when this moves out of design phase: resolve item 1's platform-
wide scope (retrofit the container cross-reference check to the other three
`GateSession` flows), since it is the same unresolved correctness risk
P031/D036 already flagged as highest priority, now only partially closed.
