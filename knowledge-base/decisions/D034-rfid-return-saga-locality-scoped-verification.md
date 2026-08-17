---
id: D034
chosen_option: "ReturnSaga Process (Saga Pattern) with Locality-Scoped Verification -- Local-Only Verdict for Same-Store Returns, a Bounded/Fail-Safe Synchronous Checkpoint for Cross-Store Returns, and Event-Driven Paid-EPC Cache Invalidation Routed to the Originating Store"
problem_id: P029
tags: [rfid, returns, fraud-prevention, saga-pattern, event-driven-architecture, offline-first, cache-invalidation, retail, loss-prevention, state-machine]
related_snippets: [S034]
---

# Decision: ReturnSaga -- Locality-Scoped Verification with Event-Driven Paid-EPC Cache Invalidation

## Context

P029 needs a return/refund flow for the RFID Event Platform that (1) closes
a real loss-prevention gap -- the paid-EPC cache has an add-on-sale path but
no remove-on-return path -- and (2) resolves a genuine structural tension:
cross-store return validation needs proof of what a *different* site did
(was this EPC really sold, and where), while the platform's core principle,
applied consistently in checkout, EAS, and GateSession (P027/D032), is no
synchronous registry call from site operations.

kb-search against the existing 28 KB entries found P027 (~0.28 overlap on
`rfid`/`edge-computing`/`offline-first`/`event-driven-architecture`) and P028
(~0.19 overlap on `rfid`/`edge-computing`/`offline-first`) as the only
meaningful precedents -- both well below the 0.8 UPDATE threshold, and both
genuinely distinct problems (gate/manifest verification; WAN transport
protocol) on the same platform. This correctly becomes a new CREATE-mode
record, the platform's third.

## Options Considered

**Lens A -- Event-Driven Architecture.** Never break the no-synchronous-call
rule, for any return, anywhere. Accept every return locally and
unconditionally using only local evidence (receipt, staff judgment); publish
a `return.requested` event carrying the scanned EPC; let Serialization
Service perform the *only* real validation, asynchronously, after the fact
(does `epc_registry.status == sold`? does `tid_registry` binding match?); if
invalid, publish an exception onto the same reconciliation-job pattern
already used platform-wide (adapter drift detection, D032 Addendum 4's
`MissingExpectedEpcs`) for Loss Prevention to investigate later. Paid-EPC
cache invalidation piggybacks on whatever verdict eventually lands,
propagated the same way manifests are pre-positioned today (Kafka,
partitioned by `site_id`, HTTPS/mTLS poll at the edge -- never a broker
connection across the WAN).

**Lens B -- Saga Pattern.** Model a return as an explicit multi-step process
with a real compensating action, not a single event-reaction. A `ReturnSaga`
opens per return attempt and must reach one of three named outcomes
(`Approved` / `FraudHold` / `Rejected`) before a refund is authorized --
mirroring why D031 (PTL) chose Saga over pure choreography: something has to
own visibility over the *whole* sequence (scan -> verify -> inspect -> refund
decision) and be able to *compensate* (deny the refund, quarantine the item,
route to Loss Prevention) rather than just react to isolated events. For the
cross-store case specifically, the saga is allowed one narrow, explicit,
bounded synchronous checkpoint against Serialization Service before
authorizing refund -- accepting a deliberate, scoped exception to the
platform's no-sync-call rule, justified by the fact that unlike every other
flow this rule protects (checkout, EAS, gate passes), a return has a human
and the physical item both still present at the counter with the refund not
yet issued, so there is no throughput-critical path being blocked, only a
bounded wait before money moves.

Both architects agreed these genuinely contrast on a dimension not yet seen
in this platform's KB: not "who owns the invariant vs. how does data move"
(D032's split) and not "which transport wins outright" (D033's split), but
**how much eventual consistency a return can tolerate before authorizing an
irreversible action (refund)** -- EDA says "always eventual, verify after the
money moves"; Saga says "gate the money behind one bounded checkpoint,
specifically for the one leg (cross-store) that has no local evidence at
all."

## Decision

Adopt **Lens B (Saga Pattern)** as the primary structure for orchestrating a
return's steps and compensations, with **Lens A's event-driven mechanism
folded in** as (a) the *only* path for same-store returns, and (b) the
transport that propagates the resulting verdict to the correct store's
paid-EPC cache in every case. This is not a rejection of EDA's core
mechanism (contrast D033, where EDA's transport was structurally
incompatible with the WAN hop) -- it is the same "blend by decision-vs-
transport" shape already proven in D031 and D032, applied here to a third
distinct axis (verification confidence vs. eventual consistency) rather than
mechanically reused for its own sake.

### Locality decides how a return's verdict is reached

- **Same-store return** (item being returned to the store that sold it):
  the store's own paid-EPC cache is itself sufficient local proof -- if the
  EPC is present in this store's cache, it was sold here. `ReturnSaga`
  resolves entirely from local state, zero network, exactly matching
  checkout/EAS's existing offline guarantee. No exception to the no-sync-call
  rule is needed or introduced.
- **Cross-store return** (item being returned to a *different* store than
  the one that sold it): the receiving store's cache structurally cannot
  contain proof -- it never held this EPC. `ReturnSaga` performs one bounded
  synchronous verification call to Serialization Service (`epc_registry`
  status + `tid_registry` binding check for `Serialized`-mode, high-value
  SKUs). This is the platform's first and only deliberate, explicit exception
  to "no synchronous registry calls from site operations," scoped narrowly:
  it applies only to the cross-store return leg, never to checkout, EAS, or
  gate flows, which are completely unaffected.

### A third fail-safe outcome, not just FailOpen/FailClosed

GateSession's `FailSafeMode` (Verified/FailOpen/FailClosed, D032) assumes the
goods have already physically left custody by the time failure is detected --
FailOpen/FailClosed is a forced binary because there is no "pause and hold"
option once a truck is through the gate. A return is different: the customer
and the physical item are both still present at the counter, and the refund
has not yet been authorized. This makes a third option available that
GateSession's design space did not have:

- **`Verified`** -- central check succeeded; refund proceeds immediately.
- **`FailOpen`-equivalent** -- rejected outright as the default for the
  cross-store leg specifically, because it reopens exactly the fraud gap
  this consultation exists to close.
- **`FailClosed`-equivalent (deny outright)** -- rejected as the *default*
  too: a network blip should not strand a legitimate customer and a store
  employee at the counter on every WAN hiccup.
- **`PendingVerification` (chosen default)** -- the physical item is
  accepted (matching the platform's offline-first spirit: never block the
  employee from doing their job), but refund authorization is deferred:
  either auto-retried against Serialization Service within a bounded SLA
  (e.g. a few minutes), or escalated to an explicit manager-override
  workflow if the SLA lapses. The item is held in a quarantine sub-state
  (see below) and does **not** re-enter sellable stock or clear the
  originating store's paid-EPC cache until the verdict resolves one way or
  the other.

### `epc_registry` state transition

`sold -> returned` is triggered by `ReturnSaga` reaching a non-`Rejected`
outcome (immediately for same-store; on `Verified` or SLA-driven
auto-approval for cross-store). `returned` is transient, not terminal: in
the same transaction, an `InspectionOutcome` recorded at the counter (the
employee's condition check, unavoidable in any real return process) branches
it further --

- **`Resellable`** -> `store_stock` immediately, in the same transaction as
  `returned` -- no separate trigger needed, matching the platform's existing
  encoded-to-in_stock precedent (`manual/rfid-architecture-summary.md` §3):
  no new business event occurs between "returned" and "back on the floor"
  when the item passes inspection on the spot.
- **`Damaged`** -> a new terminal exception state, parallel to `voided`, that
  removes the item from resale entirely.
- **`FraudHold`** (forced whenever `ReturnSaga`'s outcome is `FraudHold`, or
  chosen by the employee independently of the saga's verdict) -> held
  pending Loss Prevention review; explicitly **not** resellable and, unlike
  the other two outcomes, does **not** clear the paid-EPC cache yet --
  ownership is disputed, so EAS should keep alarming on this EPC until LP
  resolves the case.

### Paid-EPC cache invalidation: always event-driven, routed by origin, never a broadcast

Once `ReturnSaga` reaches `Resellable` or `Damaged` (any outcome that
concludes the item is no longer legitimately "paid and in circulation"
without further dispute), Serialization Service publishes `epc.returned`,
partitioned by the **originating** `site_id` -- the store whose cache
actually added the EPC at sale time (`epc_registry.site_id` as of the last
`sold` transition), not the store where the return was physically processed.
This is a single targeted cache-prune, not a fleet-wide broadcast: only the
one store whose cache is actually wrong needs to hear about it, reusing the
exact partitioning and at-least-once/idempotent-`event_id` delivery every
other platform event already uses. The Store Gateway's existing
cache-update subscription (today only consumes `item.sold` to *add*) gains a
symmetric consumer for `epc.returned` to *remove* -- no new transport
mechanism, no new infrastructure.

**Accepted residual risk**: if the originating store's WAN is down when a
cross-store return completes elsewhere, the cache-prune event queues
(at-least-once, event-store replay) until that store reconnects. During that
window, a thief could in principle exploit the stale "still paid" entry at
the originating store specifically. This is the same class of bounded,
accepted offline-convergence delay the platform already accepts everywhere
else (manifest pre-positioning, config push) -- not a new category of risk,
but must be named explicitly rather than left implicit, per this platform's
established practice (see P027 Open Items).

### `tid_registry` check

Applied at return intake for `Serialized`-mode, high-value SKUs only (the
same SKUs bound at encode time) -- this is the platform's first live
consumer of `tid_registry`. Same-store returns check it from a locally
cached TID binding if the item was tagged in this store (backroom tagging
already exists as a store flow); cross-store returns fold the check into the
one bounded synchronous verification call already required for that leg, at
no extra round trip. A TID mismatch or missing binding forces `FraudHold`
unconditionally -- it is never silently accepted regardless of what the
EPC-level check found.

### `tracking_mode = CountOnly` handling

`CountOnly` GTINs still emit a uniquely-serialized EPC at the counter (SGTIN
requires this) -- `ReturnSaga` still reads and removes that specific EPC from
the paid-EPC cache exactly like any other return (cache entries are always
per-EPC, matching how checkout adds them per-EPC regardless of tracking
mode). Serialization Service does not flip a per-EPC state machine for these
GTINs; instead it decrements a GTIN-level quantity counter, symmetric to the
existing `CountOnly` ASN/gate reconciliation pattern
(`ReconcileCountOnlyGtins`, D032 Addendum 3). No `tid_registry` check applies
(CountOnly is, by definition, the low-value tier not worth that investment).

**New scoped mechanism this decision adds** (not present in any prior
consultation): because CountOnly intentionally does not persist a full
per-EPC lifecycle, there is otherwise *no ground truth at all* to validate a
CountOnly return against, for either locality. Serialization Service
retains a lightweight, short-lived `(epc, gtin, site_id, sold_at)` record for
`CountOnly` sales -- not a full state-machine row, just enough to answer "was
this EPC part of a legitimate sale within the return window" -- purged after
a configurable retention window matching store return policy (e.g. 30-90
days). Returns attempted after the window falls back to the store's existing
non-RFID exception process (receipt-based manual approval), which is already
how retail handles expired-return-window edge cases today -- not a new
architectural mechanism, just the existing business exception path applied
here too.

## Consequences

**Accepted trade-offs**:
- The platform's "no synchronous registry calls from site operations"
  principle is no longer absolute -- it now has exactly one named, scoped
  exception (cross-store return verification). This must be documented
  prominently wherever that principle is referenced elsewhere, so a future
  consultation does not mistake it for still being unconditional.
- Cross-store returns are measurably slower than same-store returns
  (bounded wait for a network round trip) and can, in the `PendingVerification`
  path, require a manager-override workflow -- a real operational cost, not
  just a code path.
- A new short-lived per-EPC record for `CountOnly` sales partially reverses
  that mode's core storage-saving premise, scoped narrowly (return-window
  duration only) and only for the returns use case -- not a full reversal of
  the Dual EPC Tracking Mode decision.

**Benefits**:
- Closes the actual loss-prevention gap this consultation exists to fix:
  every return, same-store or cross-store, now has an explicit, code-level
  path that removes the EPC from the correct store's paid-EPC cache -- no
  more permanently-stale "paid" entries surviving a return.
- The no-sync-call exception is narrow and auditable by construction
  (`ReturnSaga`'s locality branch), not a general erosion of the principle --
  checkout, EAS, and GateSession are completely untouched by this decision.
- `PendingVerification` as a third fail-safe outcome is a genuinely new
  contribution beyond GateSession's binary FailOpen/FailClosed, made
  possible specifically because a return keeps the customer and item present
  at the point of decision -- a distinction worth carrying into any future
  RFID flow that shares that same physical-presence property.
- `tid_registry` gets its first real consumer, closing a long-standing gap
  noted in prior consultations that it existed but nothing actually used it.

**Confidence**: high on the locality split (same-store needs no exception,
cross-store structurally cannot avoid one) and on routing cache invalidation
by originating `site_id` rather than broadcasting. Medium on the exact SLA
duration for `PendingVerification` auto-retry before manager escalation --
that is an operations-tuned parameter, not an architectural one, and needs a
real answer from Loss Prevention/store operations before pilot, the same
category of open item this KB has repeatedly logged for P027 (dock-appointment
grace windows, retention windows).
