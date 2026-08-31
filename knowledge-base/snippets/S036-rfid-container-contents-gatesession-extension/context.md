---
when_to_use: "Use when GateSession (D032) must evaluate a physical scan session containing a mix of item-level (SGTIN) and container-level (SSCC) EPCs, and the platform needs to answer both 'what is inside this container' (edge, zero-delay) and 'which container is this item in' (central, non-real-time) without introducing a new synchronous call on the gate-decision path."
related_problems: [P031]
related_decisions: [D036]
---

# Snippet: Container-Contents GateSession Extension -- Header Branch, Container Registry, Scoped Edge Fanout

This snippet demonstrates the D036 decision: `GateSession`'s Header-validation
logic (D032 Addendum 10) gains a third branch for SSCC container reads,
resolved against a locally pre-positioned `container_contents_by_container`
cache -- the same edge-fanout transport `MovementManifest` already uses --
while the reverse `item_to_container` lookup deliberately stays central-only
(the CQRS-borrowed fanout-scope discipline). `GateSession`'s zero-loss/
zero-delay/fail-safe invariants (D032) and its existing completeness
reconciliation (`ComputeMissingExpectedEpcs`, `ReconcileCountOnlyGtins`) are
untouched -- this file shows only what D036 adds.

It shows:
- **`EpcScheme` classification surfaced on `UnsupportedEpcSchemeException`**
  -- `Evaluate()` needs to distinguish an SSCC read (route to container
  resolution) from a genuinely out-of-scope read (GRAI/GIAI/SGLN, still
  `UnsupportedScheme`) without a second Header-parsing pass. The exception
  now carries the classified scheme so both branches can share one catch
  site.
- **`GateVerdict.ContainerRead`** -- a new verdict, resolved or unresolved,
  additive to the existing verdict set. An unresolved container (not yet
  synced, or genuinely unknown) falls back through the exact same
  `FailSafeMode` path every other unresolved lookup already uses -- no new
  fail-safe branch was introduced.
- **`IContainerContentsCache`** -- the edge-local read port `GateSession`
  depends on for the container -> contents direction only, populated by the
  same `ManifestSyncConsumer`-style poll transport already proven for
  `MovementManifest` (Kafka -> Site & Config Service -> Redis -> HTTPS/mTLS
  poll -> edge cache). There is deliberately no edge-side port for the
  reverse item -> container direction -- `GateSession` never needs it, so it
  is never pushed to the edge, kept as a central-only Query/Admin API
  concern instead.
- **`ContainerPackedEvent`** -- the single write-side fact published either
  by the supplier (extending the existing supplier-facing API, at ASN
  submission time) or the DC/store Tagging Station App (at repack time),
  carrying a declared item count and checksum validated at the write
  boundary before ever being cached -- reusing D032 Addendum 1's
  completeness-proof pattern rather than inventing a new validation idiom.
- **`GateSessionResult.ContainerReads`** -- container reads are reported as
  their own entries, alongside the existing `Verdicts`/`CountMismatches`/
  `MissingExpectedEpcs` fields, never expanded into synthetic per-item
  `Expected` verdicts for EPCs the antenna did not actually read.
- Plain constructor DI throughout -- no MediatR, no AutoMapper, per this
  repository's .NET standards.

**Known open item this snippet does not resolve (P031 Open Item 1,
highest priority)**: if item-level EPCs listed in a `MovementManifest` are
physically packed inside a sealed, RF-occluded container and only the
container tag is reliably read, those item EPCs will never receive an
`Expected` verdict and will appear in `ComputeMissingExpectedEpcs()` even
though the container's resolved contents account for them. A correct
implementation must cross-reference `ContainerReads` against
`MissingExpectedEpcs` before the WMS Adapter posts a shortage exception --
this cross-reference is not implemented in this snippet, and is flagged as
the highest-priority follow-up before pilot.

**Confidence**: medium -- see D036's confidence reasoning; the modeling and
transport-reuse approach is high-confidence, but the sealed-container
interaction with `MissingExpectedEpcs` is a real, newly-found gap that must
be closed first.
