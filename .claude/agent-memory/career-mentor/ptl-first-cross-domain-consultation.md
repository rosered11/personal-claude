---
name: PTL Consultation -- First Cross-Domain Application (P026/D031)
description: 2026-08-10 consultation was the first entirely outside the Sprint-OMS/ETL lineage; use it as evidence of pattern generalization, not a fresh-start domain requiring re-teaching from scratch
type: project
---

Consultation 9 (P026/D031/S031, CMG Put-to-Light warehouse integration) is the first
KB entry with no relationship to Sprint-OMS or the ETL pipelines that produced every
prior consultation. The user applied (via the pipeline) the Saga-vs-choreography
vocabulary first learned in P014/D019 to a domain (physical warehouse hardware
integration) that shares zero business vocabulary with OMS, and the reasoning
transferred cleanly -- the deciding factor was reframed from "service count" (the D019
rule) to "who owns the cross-cutting invariant," a generalization rather than a new
concept.

**Why:** this is the concrete evidence the Phase Progression Criteria's "Intermediate"
bar (pattern fluency that survives a change of domain) is being met, not just repeated
within one codebase.

**How to apply:** Do not re-teach Saga Pattern or Event-Driven Architecture basics from
scratch in future non-OMS consultations -- treat P026/D031 as proof those concepts are
already portable, and instead focus learning summaries on what's genuinely new about
the next domain (e.g., hardware/PLC integration specifics, warehouse operations
constraints) rather than repeating architectural-pattern fundamentals.
