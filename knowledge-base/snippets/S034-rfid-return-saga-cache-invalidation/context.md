---
when_to_use: "Use when a reverse/compensating flow (return, cancellation, reversal) needs a verdict before an irreversible downstream action (refund, resupply) is authorized, and where verification confidence differs by locality -- some cases can be proven from purely local state, others structurally require knowledge of what a remote site did, creating tension with a platform's existing no-synchronous-call principle."
related_problems: [P029]
related_decisions: [D034]
---

# Snippet: ReturnSaga -- Locality-Scoped Verification with Event-Driven Paid-EPC Cache Invalidation

This snippet demonstrates the D034 decision: a `ReturnSaga` (Saga Pattern)
that owns the multi-step verify -> inspect -> refund-authorize/compensate
sequence for an RFID store return, with **locality** deciding how the
verification step is reached -- `SameStore` resolves entirely from local
state (the paid-EPC cache itself is sufficient proof), `CrossStore` performs
the platform's first and only deliberate, bounded exception to "no
synchronous registry calls from site operations." Event-Driven Architecture
is folded in, not rejected, as the transport that routes the resulting
paid-EPC cache invalidation to the correct store.

It shows:
- **Locality-scoped verification (the core of D034)** -- `ReturnSaga.VerifyAsync`
  branches on `ReturnLocality`. `VerifySameStore()` checks only
  `IPaidEpcCache.Contains(epc)` and, for high-value SKUs, a locally cached
  TID binding (`ILocalTidBindingCache`) -- zero network, matching
  checkout/EAS's existing offline guarantee exactly. `VerifyCrossStoreAsync()`
  is the platform's one deliberate exception to "no synchronous registry
  calls from site operations," scoped narrowly to this one leg of this one
  flow -- it never touches checkout, EAS, or `GateSession`.
- **`ReturnVerdict.PendingVerification` -- a third fail-safe outcome
  `GateSession`'s `FailSafeMode` (Verified/FailOpen/FailClosed, D032) never
  needed.** GateSession's binary choice exists because by the time a gate
  failure is detected, the goods have already left custody -- there is no
  "pause and hold" option. A return is different: the customer and the
  physical item are both still present, and the refund has not yet been
  authorized. `VerifyCrossStoreAsync` treats both an explicit non-match
  result and a timeout/unreachable central service identically as
  `PendingVerification`, never a silent `Verified` and never a blind
  `Rejected` -- the item is accepted (offline-first spirit preserved) but
  the refund is deferred pending retry or manager override.
- **Compensation, not just reaction (the Saga half of the decision)** --
  `RecordInspection()` forces `InspectionOutcome.FraudHold` whenever
  `VerifyAsync` returned `Rejected`, regardless of what the employee
  selected: a failed verification cannot be silently downgraded by the
  counter interaction, only escalated by it. This is the compensating
  action a pure event-reaction (Lens A alone) could not express as cleanly
  -- the saga explicitly gates refund authorization behind the verdict.
- **`Close()` routes cache invalidation by locality, and by *origin*, never
  by broadcast.** `SameStore` prunes `IPaidEpcCache` in-process,
  immediately, mirroring the existing POS-bridge "mark sold -> update
  cache" pattern in reverse. `CrossStore` publishes `EpcReturnedEvent`
  carrying `OriginatingSiteId` -- the store whose cache actually added this
  EPC at sale time, not the store where the return was physically
  processed, and not every store. `FraudHold` deliberately triggers **no**
  cache mutation and **no** event at all: ownership is disputed, so EAS
  must keep alarming on this EPC until Loss Prevention resolves the case --
  silently clearing the cache here would quietly reopen the exact gap this
  decision exists to close.
- **`StoreGatewayReturnCacheInvalidator`** -- the symmetric counterpart to
  the platform's existing POS-bridge cache writer (which already consumes
  `item.sold` to *add*). Delivered via the same site_id-partitioned,
  at-least-once, idempotent-`event_id` pipeline every other cross-site event
  in this platform already uses (D032 Addendum 2's HTTPS/mTLS poll, never a
  direct broker subscription across the WAN) -- no new transport mechanism.
- **`ISoldEpcLedger` -- the new mechanism this decision adds for
  `CountOnly` GTINs.** `CountOnly` deliberately does not persist a full
  per-EPC lifecycle (Dual EPC Tracking Mode decision), which otherwise
  leaves a `CrossStore` return of a `CountOnly` item with zero ground truth
  to check against, for either locality. `SoldEpcLedgerEntry` is
  intentionally lightweight and short-lived (retention window matching
  store return policy, e.g. 30-90 days) -- not a reversal of `CountOnly`'s
  storage-saving premise, just enough evidence to answer "was this EPC part
  of a legitimate sale within the return window." An expired or missing
  entry resolves to `PendingVerification`, not an outright deny, falling
  back to the store's existing non-RFID exception process (receipt-based
  manual approval) if the SLA lapses.
- Plain constructor DI throughout -- no MediatR, no AutoMapper, per this
  repository's .NET standards.

Architecturally, this is the boundary to reuse for any future RFID (or
non-RFID) reverse/compensating flow that needs "verify locally when
possible, fall back to one bounded remote checkpoint only when local
evidence structurally cannot exist, and never let a failed or ambiguous
verification silently authorize the irreversible downstream action." New
reverse flows should add a new `ReturnLocality`-style branch and a new
central verifier implementation, not a new saga mechanism.

**Relationship to S032 (`GateSession`)**: both are session/saga-shaped
objects owning an invariant over a bounded interaction, and both were
deliberately compared during lens selection. `GateSession` was not reused
directly because it has no natural "expected list" analog for returns (there
is no pre-existing manifest of "items about to be returned" the way there is
for a movement round or a sales order) -- returns needed a genuine multi-step
process with a compensating action (Saga), not a single-point-in-time
batch-evaluation aggregate (DDD invariant enforcement). The two patterns are
complementary, not competing, in this platform's growing vocabulary: DDD for
"evaluate a batch against a scoped expected list, once," Saga for "walk a
multi-step process to one of several outcomes, with compensation on
failure."

**Confidence**: high on the locality split and on routing cache invalidation
by originating `site_id`. Medium on the exact `PendingVerification` retry
SLA before manager escalation -- an operations-tuned parameter, not resolved
by this snippet (see D034 Confidence note and P029 for the open items this
consultation logged).
