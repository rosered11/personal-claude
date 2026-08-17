---
name: RFID third consultation -- locality-scoped branching as a new reasoning depth
description: P029/D034 (returns flow) is the RFID lineage's third entry; the notable learning-journey signal is a within-flow branch (same-store vs cross-store) rather than a single lens answer for the whole problem
type: project
---

P029/D034/S034 (2026-08-17, RFID store returns) is the third RFID Event Platform
consultation, after P027/D032 (gate/manifest verification, DDD+EDA) and P028/D033
(WAN transport, Hexagonal+EDA). The user's reasoning here went a level deeper than
either prior RFID decision: instead of picking one lens pairing and applying it
uniformly to the whole problem, the decision (Saga primary, EDA folded in) was itself
split at runtime by a locality condition (same-store vs. cross-store return) -- most of
the flow needed no exception to the platform's "no synchronous registry calls"
principle at all, only the one leg with zero local evidence did.

**Why:** this is a materially different skill from "pick the right lens pair" (D032,
D033) -- it's "recognize that a single flow can contain sub-cases with different
evidence availability, and let the *instance*, not the whole flow, decide which
consistency guarantee applies." Worth watching for whether this generalizes into a
named skill (tentatively: "evidence-locality branching") across future consultations,
the same way "blend by concern" (D030/D031/D032) became a recognized recurring pattern
across three prior domains.

**How to apply:** in the next RFID (or any) consultation, check whether the user
already anticipates a within-flow branch before the pipeline surfaces it, the same way
prior roadmap entries tracked whether they anticipated which lens would win outright.
If they start naming "which specific case needs the stronger guarantee" unprompted,
that is a concrete Advanced-phase signal (spotting where a single architectural
principle should NOT apply uniformly) worth flagging explicitly in Current Focus.
