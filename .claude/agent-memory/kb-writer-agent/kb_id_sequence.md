---
name: KB ID Sequence
description: Current highest P/D/S IDs in the knowledge base, to avoid re-scanning the whole filesystem on every write
type: project
---

As of 2026-07-09: highest IDs are P018, D023, S023 (OMS Service-Boundary Coupling /
Strangler Fig Facade-First Migration, from inbox/oms/oms-architect-review.md). Next CREATE-mode
IDs should start at P019 / D024 / S024. Note: P/D/S numbering is not always in lockstep across
categories (e.g. D010-D014 have no matching P-number; S004/S006/S007/S013 do not exist) --
always scan all three directories independently rather than assuming P_n implies D_n and S_n
both exist.
