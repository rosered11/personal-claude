---
id: D036
chosen_option: "Container Identity + Container-Contents Relational Model (DDD-Owned by GateSession/Serialization Service), With CQRS-Scoped Edge Fanout for the Reverse Lookup"
problem_id: P031
tags: [rfid, edge-computing, gate-verification, manifest-sync, domain-driven-design, warehouse-management, sscc, cqrs]
related_snippets: [S036]
---

# Decision: Container Identity + Container-Contents Relational Model, DDD-Owned With CQRS-Scoped Edge Fanout

## Context

P031 needs the platform to model container-level EPCs (SSCC or equivalent)
with a queryable relationship to the item-level EPCs packed inside, without
breaking `item_master`/SGTIN enrichment, without weakening `UnsupportedScheme`
handling for GRAI/GIAI/SGLN, and without introducing a new synchronous
central call at `GateSession`. kb-search against the existing 30 KB entries
found two meaningful precedents on the same platform -- P030 (~0.6 overlap:
rfid, edge-computing, gate-verification, manifest-sync, offline-first,
warehouse-management) and P027 (~0.6 overlap, same tag set, older) -- both
below the 0.8 UPDATE threshold, so this correctly became a new CREATE-mode
record rather than an update to either. This is the first RFID Event
Platform consultation to extend `GateSession`'s Header-validation branch
itself (D032 Addendum 10) rather than its manifest-resolution strategy
(D032 Addendum 5, D035).

## Options Considered

**Lens A -- Domain-Driven Design**: model container identity as a new,
parallel entity (`ShippingContainer`) with a `container_registry` /
`container_contents` table pair in the Serialization DB, owned by
Serialization Service exactly the way `epc_registry`/`movement_manifests`
already are. Published either by the supplier at ASN-submission time
(extending the existing supplier-facing API) or by the DC/store Tagging
Station App at repack time, converging on one `ContainerPackedEvent` shape
differentiated by `packed_by` the same way `MovementManifest.MovementType`
already differentiates its four sources. `GateSession.Evaluate()` gains a
third scheme branch (alongside SGTIN and the still-rejected GRAI/GIAI/SGLN
set): an SSCC read resolves the container's contents from a locally
pre-positioned cache (reusing the exact `IManifestCache`-style transport)
and returns a new `GateVerdict.ContainerRead`, unresolved container reads
falling back to the existing `FailSafeMode` path unchanged.

**Lens B -- CQRS**: treat container packing as a single, deliberately dumb
write-side event (`ContainerPackedEvent`, validated at the write boundary
with a count/checksum completeness proof reusing D032 Addendum 1's pattern),
with no aggregate or invariant owner, feeding two independently-scoped read
projections: `container_contents_by_container` (pushed to the edge,
answering "what is inside this box" at zero-delay -- the only direction
`GateSession` actually needs in real time) and a central-only
`item_to_container` index (answering "which box is this item in" as a
Query/Admin API-style read, never pre-positioned to every site's offline
SQLite store, since nothing in the problem's constraints requires that
reverse direction at zero-delay).

Both architects agreed on the edge-facing wire behavior (same verdict shape,
same fallback path) -- the real contrast is data ownership and fanout scope:
Lens A frames the container-contents relationship as a first-class modeled
entity with an implicit invariant surface; Lens B frames it as two
independently-scaled projections over one validated write-fact, with no
single owner of "is this relationship correct," and deliberately withholds
one of the two directions from ever reaching the edge.

## Decision

Adopt **Lens A (DDD)** as the primary data-ownership and modeling structure,
with **Lens B's two sharpest insights folded in, not rejected**:

1. **Container identity and contents are modeled relationally, owned by
   Serialization Service**, exactly like every other identity table this
   platform already owns -- not because DDD demands an aggregate class, but
   because the actual read/write shape here (infrequent container-seal
   writes, simple indexed lookups in both directions) does not justify
   Lens B's fuller "two independently maintained projections from an event
   stream" machinery. A single `container_contents` table with a composite
   primary key (`container_epc`, `item_epc`) and a secondary index on
   `item_epc` answers both stated query directions without inventing a
   second maintained projection.
2. **Lens B's fanout-scope discipline is adopted exactly as proposed.** Only
   the container -> contents direction is pre-positioned to the edge (via
   the existing manifest pre-positioning transport, reused unchanged); the
   reverse item -> container lookup stays central-only, served by Query/
   Admin API. `GateSession` never needs "which box is this item in," so it
   never receives it -- this is a direct, deliberate rejection of "push
   everything everywhere," the same discipline this platform has not had to
   apply before (every existing manifest type today is pushed to every
   subscribing edge uniformly).
3. **Lens B's write-boundary validation discipline is adopted.**
   `ContainerPackedEvent` carries a declared item count and checksum,
   checked before ever being trusted -- reusing D032 Addendum 1's
   completeness-proof pattern rather than inventing a new validation idiom.

**GateSession's Header-validation/evaluation logic (D032 Addendum 10)
changes as follows.** `IEpcGtinResolver`'s scheme classification is
extended so `Evaluate()` can distinguish an SSCC read from a genuinely
out-of-scope read (GRAI/GIAI/SGLN) without a second Header-parsing pass:
SGTIN-96/198 is unchanged; SSCC now resolves against the locally-cached
`container_contents_by_container` projection and returns a new
`GateVerdict.ContainerRead` (itself branching into a resolved sub-case,
carrying the cached contents count, and an unresolved sub-case, identical in
shape and fail-safe behavior to "no manifest cached yet"); GRAI/GIAI/SGLN
continue to throw `UnsupportedEpcSchemeException` -> `GateVerdict.
UnsupportedScheme`, entirely unchanged, per the Clarified Scope's explicit
instruction not to weaken that path. Both changes are additive branches
inside `Evaluate()`, not a rewrite of it.

**What GateSession reports for a mixed item+container read in one
session.** `GateSessionResult` gains a `ContainerReads` list, populated
alongside the existing `Verdicts`/`CountMismatches`/`MissingExpectedEpcs`
fields -- a container read is reported as its own entry, never expanded
into synthetic per-item `Expected` verdicts for the items it claims to
contain. Fabricating "evaluated" status for item EPCs the antenna never
actually read would itself be a zero-loss violation of a new kind
(claiming evaluation that did not happen); a session that reads both a
container tag and some of its individual item tags simply reports both,
side by side, exactly as observed.

## Consequences

**Accepted trade-offs**:
- `GateSession` gains a third meaningfully-branching scheme path (alongside
  SGTIN and rejected-schemes), continuing to grow the platform's single
  busiest evaluation path -- the same accepted trade-off already logged for
  every prior `GateSession` extension (D032 original, Addendum 8's flow #9,
  D035's third resolution mode).
- Container seal/immutability semantics are explicitly not decided here --
  logged as P031 Open Item 2 -- meaning `container_contents` write
  semantics (append-only vs versioned) cannot be finalized in code until
  operations answers that question.
- A genuine, first-discovered correctness gap was found rather than solved:
  items packed inside a sealed, RF-occluded container may never be
  individually read, and would surface as a false `MissingExpectedEpcs`
  signal unless downstream logic cross-references `ContainerReads` against
  `MissingExpectedEpcs` before flagging a shortage -- logged as P031 Open
  Item 1, highest priority, not resolved by this decision.

**Benefits**:
- Reuses the platform's proven `GateSession`-generalization playbook (new
  branch/new verdict, no change to the zero-loss/zero-delay/fail-safe
  invariants themselves) for the fourth time.
- Reuses 100% of existing transport infrastructure (supplier-facing API,
  Tagging Station App, Kafka -> Site & Config Service -> Redis -> HTTPS/mTLS
  poll -> edge cache) -- no new integration surface, directly satisfying the
  Clarified Scope's "reuse over invention" instruction.
- The CQRS-borrowed fanout-scope discipline keeps every site's offline
  SQLite store lean -- only the query direction `GateSession` actually needs
  in real time is ever pushed to the edge, avoiding unnecessary edge-cache
  bloat for a query direction (item -> container) nothing at the edge
  requires at zero-delay.
- `item_master`/SGTIN enrichment, and `UnsupportedScheme` handling for
  GRAI/GIAI/SGLN, are both structurally untouched -- container identity is a
  fully parallel table set, not a modification to any existing table or
  code path.

**Confidence**: medium. High confidence in the modeling approach and the
transport reuse (directly extends four times-validated patterns); confidence
is capped at medium because this consultation surfaced a real,
previously-unknown correctness risk (P031 Open Item 1, the sealed-container/
`MissingExpectedEpcs` interaction) that must be closed before pilot, and
because the container seal/immutability policy remains an open business
question this decision deliberately does not resolve.
