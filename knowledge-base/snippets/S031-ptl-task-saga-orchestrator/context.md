---
when_to_use: "Use when a business process must coordinate a partial-completion, multi-step lifecycle across several independently-owned external systems (here: WMS, SAP, a PTL/MHE hardware controller, and a Marketplace), where hard invariants (uniqueness, single-active-slot, mixed-source rejection) and exception paths (quantity mismatches) must be enforced centrally, while the actual system-to-system messaging is still carried over an async event bus."
related_problems: [P026]
related_decisions: [D031]
---

# Snippet: PTL Task Saga Orchestrator

This snippet demonstrates the D031 decision: an explicit state machine
(`PtlTaskSaga`) that owns the lifecycle of a single Put-to-Light task/box from
allocation import through PTL confirmation to SO/STO creation and remaining-sync.

It shows:

- A `PtlTaskState` enum covering the happy path plus the two compensating states
  (`Rejected`, `OnHold`) required by P026's exception-handling constraints.
- `ConfirmFromPtlController(...)`, which enforces the "no mixed-store cartons" rule
  synchronously and returns an explicit rejection rather than silently accepting or
  only logging an event -- this is the concrete reason the pure Event-Driven lens was
  not chosen outright: a listener reacting to an already-published event cannot refuse
  the hardware controller's confirmation call in time.
- `EvaluateAllocationVsStock(...)`, which implements the two-directional
  allocation-vs-stock mismatch check as an explicit `OnHold` transition instead of a
  silent pass-through or failure.
- `TryRequestPartialSoSto(...)`, which allows a task to progress to SO/STO creation
  independently of sibling tasks under the same order/allocation, directly
  implementing the "partial SO/STO creation" requirement.
- Ports (`ISapClient`, `IPtlControllerClient`, `IWmsClient`, `IEventBus`) injected via
  plain constructor DI -- no MediatR, no AutoMapper, per this repository's .NET
  standards -- with the saga publishing/consuming integration events
  (`StockUpdated`, `AllocationImported`, `PtlTaskConfirmed`, `SoStoCreated`,
  `RemainingUpdated`) as its transport layer, per the event-carried-state-transfer
  half of the D031 decision.

Architecturally, this is the boundary to reuse for any future PTL/warehouse
consultation: the saga is the *only* place cross-system invariants and compensations
should be added; new WMS/SAP/PTL/Marketplace touchpoints should be modeled as new
event subscriptions or new saga states, not as bespoke logic bolted onto an individual
system's adapter.
