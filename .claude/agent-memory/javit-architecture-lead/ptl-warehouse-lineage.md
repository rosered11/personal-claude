---
name: PTL (Put-to-Light) Warehouse Lineage -- Separate from Sprint-OMS
description: inbox/push-to-light/ is a distinct consultation lineage (CMG's PTL warehouse system) unrelated to Sprint-OMS/inbox/oms; first entry P026/D031/S031
type: project
---

`inbox/push-to-light/req.md` + `inbox/push-to-light/spec-extracted.md` describe CMG's
Put-to-Light (PTL) warehouse process (WMS/SAP/PTL-MHE-controller/Marketplace
integration) -- a completely different system from Sprint-OMS (the source of
P013-P025/D018-D030 via `inbox/oms/req.md`). Do not conflate the two lineages: PTL has
its own KB anchor at P026/D031/S031 (chosen: Saga Pattern as primary orchestrator,
Event-Driven Architecture folded in as transport).

**Why:** this repo now has two active, unrelated consultation threads distinguished
only by inbox subfolder (`inbox/oms/...` vs `inbox/push-to-light/...`); the "same repo,
different lineage" confusion already happened once before with `inbox/oms/req.md`
being reused across genuinely different problems (see inbox-oms-req-path-reused.md) --
the risk here is the opposite direction: assuming a new inbox path means a new domain
when actually checking should always be done by reading the actual file content.

**How to apply:** When a new PTL/warehouse problem arrives (from `inbox/push-to-light/`
or similarly named paths), check tag overlap against P026 specifically, not against the
OMS/ETL corpus. The source spec for this lineage arrives as a `.pptx` file that must be
pre-extracted to a `spec-extracted.md` text file before it can be read (pptx is not a
directly readable format in this harness) -- if a future PTL consultation only supplies
a raw `.pptx` with no text extract, ask the user for an extraction rather than guessing
its contents.
