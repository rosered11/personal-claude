---
id: D031
chosen_option: "PTL Task Saga -- Orchestrated Process Manager over an Event-Carried State Backbone"
problem_id: P026
tags: [warehouse-management, put-to-light, saga-pattern, event-driven-architecture, wms-sap-integration, mhe-plc-integration, partial-fulfillment, exception-handling]
related_snippets: [S031]
---

# Decision: PTL Task Saga -- Orchestrated Process Manager over an Event-Carried State Backbone

## Context

P026 requires replacing CMG's manual, Excel/file-based coordination of WMS, SAP,
the PTL/MHE hardware controller, and Marketplace with API-driven integration, while
preserving partial SO/STO creation and adding real-time validation for
allocation-vs-stock mismatches and cross-store carton mixing. The KB currently has no
prior warehouse/WMS-SAP-PTL integration entries (all 25 existing problems are OMS
microservices or ETL-pipeline domain), so this decision establishes a new institutional
precedent rather than extending an existing one.

## Options Considered

**Lens A -- Saga Pattern**: an explicit PTL Task/Box orchestrator owns a state machine
for each task from allocation import through PTL confirmation to SO/STO creation and
remaining-sync, issuing commands to WMS/SAP/PTL-MHE/Marketplace and applying
compensations (reject, hold, partial-allocate) when validation fails.

**Lens B -- Event-Driven Architecture**: every manual export/import step is replaced by
a shared event bus (StockUpdated, AllocationImported, PtlTaskConfirmed, SoStoCreated,
RemainingUpdated, MarketplaceStatusChanged); each system reacts independently to the
events it cares about, with no central owner of the end-to-end sequence.

Both architects agreed the two lenses genuinely contrast: Lens A centralizes
orchestration and compensation control; Lens B decentralizes into choreography and
pure coupling reduction.

## Decision

Adopt **Lens A (Saga Pattern)** as the primary coordination structure, with Lens B's
event bus folded in as the saga's transport/notification layer rather than rejected
outright.

A central **PTL Task Orchestrator** owns an explicit state machine per task/box
(`AllocationImported -> TaskGenerated -> SentToPtl -> Confirmed -> SoStoRequested ->
SoStoCreated -> RemainingSynced -> Completed`, with `Rejected` and `OnHold` as
compensating states). This orchestrator is the single place that:

- Enforces "1 order = 1 box = 1 invoice" and "only 1 active box per PLT slot per time
  period" as state-transition guards, instead of relying on manual reconciliation
  across systems that don't share this invariant today.
- Evaluates allocation-vs-stock mismatches (both directions) at the point a task is
  confirmed, transitioning to `OnHold` for review rather than silently proceeding or
  failing.
- Rejects (returns an explicit error to the PTL controller) any task/carton mixing
  items from more than one store, synchronously, before it can reach `Confirmed`.
- Drives partial SO/STO creation by tracking readiness at task/LPN granularity, so a
  subset of an order's tasks can reach `SoStoRequested` without waiting for every task
  under the same order/allocation to complete.

Communication with WMS, SAP, the PTL/MHE controller, and Marketplace is carried over an
asynchronous event bus (event-carried state transfer), exactly as Lens B proposed:
`StockUpdated` / `RemainingUpdated` from WMS, `AllocationImported` into the FC,
`PtlTaskConfirmed` from the PTL/MHE controller, `SoStoCreated` acknowledgement from
SAP, and `MarketplaceStatusChanged` fan-out. This directly replaces every one of the
7 manual/❌ file-exchange touchpoints identified in the AS-IS diagram (slide 2) with an
event, while keeping the saga -- not any individual event consumer -- as the sole
decision-maker for the cross-cutting business rules.

The existing "Auto Sync -- MKP status" automation (already working end-to-end in the
AS-IS flow) is deliberately left as pure choreography; it needs no cross-system
invariant enforcement and should not be pulled into the saga's scope.

## Consequences

**Accepted trade-offs**:
- The orchestrator becomes a new, stateful, critical-path service -- if it is
  unavailable, no new PTL tasks progress, unlike a pure event mesh where systems could
  buffer independently. This is accepted because the alternative (pure choreography)
  has no natural place to enforce "1 order = 1 box = 1 invoice" or reject mixed-store
  cartons synchronously, and would have re-introduced an ad hoc orchestrator by another
  name to cover exactly those rules.
- SAP, the PTL controller, and Marketplace must expose (or be wrapped with) APIs the
  saga can call and await responses from; any system that only offers polling or
  webhook-style interfaces today will need an adapter.
- A new saga state store (task/box lifecycle table) must be operated, backed up, and
  reconciled against WMS/SAP/PTL as the systems of record -- it is a coordination layer,
  not a new system of record.
- The warehouse/PTL engineering team will need to learn saga/state-machine discipline
  (compensating transactions, idempotent step retries) if their prior experience is
  mostly batch/export-import jobs.

**Benefits**:
- Every hard constraint in P026 (order/box/invoice invariant, single active box per
  slot, partial SO/STO, mixed-carton rejection, stock/allocation mismatch handling) maps
  directly onto an explicit saga state or guard, giving warehouse ops a single place to
  trace "where is this task right now across 4 systems" -- replacing today's manual
  Excel-based tracing.
- Retry and idempotency logic for SAP SO/STO creation and PTL task confirmation lives
  once, inside the saga, instead of being reimplemented per system.
- The event-bus transport keeps the loose-coupling and independent-scaling benefits of
  Lens B (WMS, SAP, and the PTL controller do not need direct knowledge of each other),
  so the decision is not "orchestration instead of decoupling" but "orchestration for
  invariants, events for transport."

**Confidence**: high. The spec explicitly enumerates business rules that map cleanly to
saga states/guards, and the AS-IS Marketplace auto-sync shows event-style automation
already working well for the one part of the flow that has no cross-cutting invariant
to enforce -- reinforcing that the split (saga for invariants, events for transport) is
grounded in the spec rather than a generic pattern choice.
