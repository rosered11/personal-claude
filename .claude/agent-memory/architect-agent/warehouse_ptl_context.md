---
name: Warehouse/PTL (Put-to-Light) Integration Context
description: Domain facts for CMG's Put-to-Light warehouse system (P026/D031/S031) -- systems involved, hard invariants, and why this is a Saga-shaped problem
type: project
---

CMG's Put-to-Light (PTL) process integrates four independently-owned systems: WMS
(stock/remaining), SAP (PO/SO/STO master + creation), a PTL/MHE hardware controller
(Light + Merchandise; physical task execution, qty/carton/box confirmation), and a
Marketplace (order status sync). Source material for this domain arrives as PowerPoint
decks (`*.pptx`) that must be pre-extracted to text before Read can use them -- pptx is
not a directly readable format in this harness.

Hard invariants repeatedly seen in this domain's specs (drive lens choice toward Saga/
orchestration, not pure choreography):
- "1 order = 1 box = 1 invoice" -- a uniqueness invariant spanning the whole order.
- "Only 1 active box per PLT slot per time period" -- a slot-capacity invariant.
- Partial SO/STO creation must be supported (task/LPN-granularity completion, not
  order-granularity).
- Mixed-store cartons must be rejected synchronously (an immediate error back to the
  hardware controller), not just logged or flagged asynchronously.
- Allocation-vs-stock mismatches (both stock > allocation and stock < allocation) need
  an explicit hold/exception state, not a silent pass or failure.

**Why this matters for lens selection:** none of these invariants can be enforced by a
single event consumer reacting to one event type -- each requires visibility across
multiple systems' local state or an immediate synchronous answer. This is the concrete
tell that a problem in this domain wants a Saga/orchestrator, with an event bus used
only as the transport between the orchestrator and WMS/SAP/PTL/Marketplace (event-
carried state transfer), not as the sole coordination mechanism.

**Stack default for this domain:** no confirmed tech stack was given in the P026 spec
extract (unlike Sprint-OMS, which is a known .NET/PostgreSQL/Kubernetes codebase) --
defaulted to C# for the code snippet based on this repo's overall .NET-heavy pattern
library, not on direct evidence of the actual WMS/PTL implementation language. Flag
this assumption explicitly if a future PTL consultation reveals a different stack.
