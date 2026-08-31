---
id: P031
title: "Container-Level EPC (SSCC) Modeling With a Queryable Relationship to Item-Level EPCs"
date: 2026-08-24
tags: [rfid, edge-computing, gate-verification, manifest-sync, offline-first, domain-driven-design, warehouse-management, sscc]
related_decisions: [D036]
related_snippets: [S036]
---

# Container-Level EPC (SSCC) Modeling With a Queryable Relationship to Item-Level EPCs

## Problem

The RFID Event Platform's EPC decoding path (`IEpcGtinResolver`, D032
Addendum 10) only supports GTIN-bearing schemes (SGTIN-96/198) and collapses
every other scheme -- including SSCC, the standard scheme for pallet/carton
logistics-unit tagging -- into a single `GateVerdict.UnsupportedScheme`
verdict at `GateSession.Evaluate()`. That design was correct when written,
because there was no known business need to do anything with a
container-level tag beyond safely not crashing on it; `manual/rfid-component-
reference.md` section "Appendix 6" explicitly flagged this as a
forward-looking risk, not a hypothetical one. A real warehouse site visit
has now confirmed a concrete, stated operational requirement that goes
beyond tolerate-and-ignore: the platform needs an EPC for the box/carton
itself, and that container EPC must have a queryable relationship to the
item-level EPCs packed inside it -- "what is inside this box" and "which box
is this item in" must both be answerable. Simply widening `UnsupportedScheme`
tolerance is insufficient; the platform must actively model the
container-to-contents relationship as persisted, queryable data.

## Root Cause

`GateSession`'s Header-validation logic and the Serialization DB schema were
both designed under the assumption that any non-GTIN-bearing scheme is
categorically out of scope for anything beyond safe rejection. There is
today no concept of "container identity" as a first-class entity, no
persisted representation of a container-contents relationship, and no
defined lifecycle stage (who publishes it, at what point in the supply
chain, and how it reaches an edge without a synchronous call) for this
pairing -- the platform has a well-proven pattern for propagating
*expected-list* data to the edge (`MovementManifest`/`IManifestCache`), but
has never needed a pattern for propagating a *composition* relationship
between two EPCs in different identity spaces.

## Summary

A real warehouse requirement, previously flagged only as a forward-looking
risk in the RFID platform's EPC-scheme documentation, has become an actual
stated need: model container-level EPCs (SSCC or equivalent) with a
queryable relationship to the item-level EPCs packed inside, without
breaking the existing item-only (SGTIN) enrichment path, the existing
`UnsupportedScheme` handling for genuinely out-of-scope schemes (GRAI/GIAI/
SGLN), or the platform's zero-loss/zero-delay/no-new-synchronous-central-
call invariants. This is the platform's fifth formal RFID Event Platform
consultation (after P027/D032, P028/D033, P029/D034, P030/D035), and the
first to extend `GateSession`'s Header-validation branch itself rather than
its manifest-resolution strategy.

## Context

- **Owning platform**: RFID Event Platform, same 6-layer event-driven
  platform documented in `manual/rfid-architecture-summary.md` and
  `manual/rfid-component-reference.md`.
- **Existing EPC scheme handling (D032 Addendum 10)**: `IEpcGtinResolver.
  ExtractGtin(epc)` reads the EPC Header first, dispatches SGTIN-96/198
  normally, and throws `UnsupportedEpcSchemeException` for everything else
  (SSCC, GRAI, GIAI, SGLN) -- `GateSession.Evaluate()` catches this and
  returns `GateVerdict.UnsupportedScheme`, which still counts as "evaluated"
  for zero-loss purposes but carries no further meaning.
- **`manual/rfid-component-reference.md` Appendix 6** documents all GS1 EPC
  schemes and states plainly that SSCC/GRAI/GIAI/SGLN carry no GTIN and
  structurally cannot join `item_master` -- this remains true and is not
  being revisited; the new requirement is to model container identity as its
  own parallel identity space, not to make SSCC join `item_master`.
- **Existing reuse pattern**: `MovementManifest`/`IManifestCache`/
  `ManifestSyncConsumer` already demonstrate the platform's canonical
  pre-positioning transport (Kafka -> Site & Config Service -> Redis ->
  HTTPS/mTLS poll -> edge cache) for getting an expected-list to the correct
  edge ahead of a physical event, reused four times already (transfer,
  inbound ASN, outbound pick-verify, store backroom inbound) and once more
  for a new resolution key (D035's `ManifestId`-keyed zone receiving).
- **Serialization DB** (component #14, PostgreSQL) currently has six named
  tables (`epc_registry`, `serial_range`, `tid_registry`, `item_master`,
  `movement_manifests`, `pending_dock_appointments`) -- none represents
  container identity or container-contents relationships.
- **Supplier-facing API** (Serialization Service, D032 Addendum 3) already
  accepts serial-range requests and ASN submissions from external suppliers
  before goods leave the factory -- the only existing integration surface
  suppliers have with this platform.

## Clarified Scope (already decided, not open questions)

- SGTIN remains the only scheme with a GTIN; `item_master` enrichment logic
  is unaffected -- container EPCs are a parallel, distinct identity space,
  not a replacement or extension of item-level identity.
- Do not weaken or remove existing `UnsupportedScheme` handling for schemes
  genuinely out of scope here (GRAI, GIAI, SGLN) -- this problem is
  specifically about SSCC (or whatever scheme fits "container of items"),
  not "accept all non-SGTIN schemes now."
- Whatever is designed must fit existing invariants -- zero-loss/zero-delay
  at `GateSession`, no synchronous registry calls from site edge operations
  (except the one narrow, already-approved D034 cross-store-returns
  exception) -- a container lookup at a gate pass must not introduce a new
  synchronous central call on the critical path.

## Constraints

| Rule | Detail |
|---|---|
| Zero-delay | A gate read of a container EPC that needs to resolve "what is inside" must resolve from an edge-local cache/pre-positioned source, not a live central call -- matching every other `GateSession` lookup. |
| Zero-loss | A container-level read must still get a verdict like any other read -- it cannot silently vanish from the session's evaluation just because it is not a GTIN-bearing tag. |
| Reuse over invention | Evaluate whether the existing manifest/pre-positioning pattern (Kafka -> Site/Config Service -> edge cache) can carry container-contents data the same way it already carries `ExpectedEpcs`, before inventing a parallel sync mechanism. |
| No legacy changes | Legacy systems still must not need to know about RFID. |

## Severity

high -- this is now a stated operational requirement, not a hypothetical;
getting the container-identity/contents model wrong risks either breaking
the platform's proven zero-loss/zero-delay invariants on the highest-traffic
evaluation path (`GateSession`) or shipping a container-tracking feature
that cannot actually answer the two questions the business asked for.

## Affected Components

- RFID Event Platform -- Event Processor / `GateSession` (Header-validation
  and evaluation logic, D032 Addendum 10)
- RFID Event Platform -- Serialization Service (source of container identity
  and container-contents data; owns the new DB table(s))
- RFID Event Platform -- Serialization DB (component #14 -- new table(s)
  needed)
- RFID Event Platform -- Site & Config Service / `IManifestCache` /
  `ManifestSyncConsumer` (candidate reuse path for pre-positioning
  container-contents data to the edge)
- Supplier-facing API (candidate path for supplier-declared container
  packing at ASN submission time)
- Tagging Station App (candidate path for DC/store-side container
  assembly/repacking)

## Open Items (identified during this consultation, logged 2026-08-24)

1. **Highest priority -- possible false `MissingExpectedEpcs` signal for
   items inside a sealed, RF-occluded container.** If a `MovementManifest`
   expects certain item-level EPCs and those items are physically packed
   inside a sealed SSCC container where only the container tag is reliably
   read at the gate (a realistic RF-occlusion scenario, distinct from P027
   Open Item #1's generic miss-read case), those item EPCs will never
   receive an `Expected` verdict and will surface in
   `ComputeMissingExpectedEpcs()` (D032 Addendum 4) even though they are
   physically present and accounted for via the container's resolved
   contents. Left unresolved, this would cause the WMS Adapter to flag a
   false shortage exception on every sealed-container pass. This decision's
   `GateSessionResult` extension must be cross-referenced against
   `MissingExpectedEpcs` before implementation is considered complete --
   flagged here, not resolved by D036 itself.
2. **Container seal/immutability policy is not defined.** Whether a
   container's declared contents can change after it is sealed/published
   (partial unpack, re-pack, damaged-item removal) is an unanswered business
   rule, not an architectural gap -- needs an operations answer before
   `container_contents` write semantics (append-only vs versioned vs
   mutable) can be finalized.
3. **DC/store Tagging Station App container-assembly workflow UX is
   unspecified.** "Scan a container tag, then scan/confirm each item packed
   inside" is assumed as the DC/store-side capture mechanism but has no
   defined UI/hardware flow, unlike the supplier-ASN path which reuses an
   already-specified API shape.
4. **Container-contents completeness-proof schema (count/checksum, D032
   Addendum 1 pattern) needs concrete field definitions** before
   implementation -- the pattern is adopted by reference in D036 but the
   exact fields mirroring `ExpectedEpcCount`/`ExpectedEpcsChecksum` for a
   container payload are not yet specified.

Next step, when this moves out of design phase: resolve item 1 first, since
it directly threatens the correctness of an already-shipped invariant
(`MissingExpectedEpcs`/GRN posting) the moment container tagging goes live
at any site that also has active `MovementManifest`/ASN flows.
