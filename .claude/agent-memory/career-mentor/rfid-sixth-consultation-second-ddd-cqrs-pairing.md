---
name: RFID sixth consultation -- site-visit queue completed, second DDD-vs-CQRS pairing
description: P032/D037 is the third and last of three consultations queued from one warehouse site visit (after P030/D035, P031/D036); reused DDD-vs-CQRS a second time on a genuinely fresh axis -- verify axis freshness, not lens names, before treating a repeat pairing as reuse
type: project
---

P032/D037/S037 (2026-08-24) closes out a three-consultation queue from a single real
warehouse site visit: P030/D035 (zone receiving, no dock scheduling), P031/D036
(container/SSCC modeling), P032/D037 (location-scoped cycle count). All three are now
fully reflected in the roadmap's Current Focus narrative, Exposure Log, and Recent
Learning Opportunities sections.

P032/D037 is the platform's sixth RFID consultation and the second time this KB has
paired DDD against CQRS (after D036). When updating the roadmap for a repeated lens
pair, do not just note "seen before" -- name the specific triggering signal for each
occurrence and confirm they differ. D036's signal: a requirement naming two
asymmetrically-scoped query directions over one relationship. D037's signal: an
expected-EPC baseline with no external declaring document at all (self-asserted
platform state, not a manifest). These are different questions that happen to produce
the same lens pair -- worth flagging explicitly in the learning summary so the user
does not read "DDD vs CQRS again" as a sign of shallow/repetitive lens selection.

**Why:** the roadmap's Current Focus section had fallen one consultation behind (P031/
D036 had Exposure Log + Recent Learning Opportunities entries but no Current Focus
narrative paragraph) before this run -- backfilled alongside the new P032/D037 entry.
Worth checking Current Focus is in sync with the Exposure Log at the start of future
career-mentor invocations, since they can drift independently.

**How to apply:** when a lens pair recurs in this KB (DDD-vs-CQRS now has two
occurrences; watch for others), explicitly name and contrast the triggering signal for
each occurrence in both the roadmap narrative and the learning summary surfaced to the
user, rather than treating "same lens names" as sufficient novelty context on its own.
