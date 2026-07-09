---
name: Lens Pair History -- OMS Domain
description: Which lens pairs have been used for which OMS sub-problems, to avoid uninsightful repetition and to guide future OMS lens selection
type: project
---

OMS consultations so far and the lens pair used:
- P013/D018 (greenfield design): Domain-Driven Design vs CQRS -> blended (both needed)
- P014/D019 (aggregate extensions): Domain-Driven Design vs Saga Pattern -> DDD won, Saga's
  service-count threshold (3+) absorbed as a rule
- P015/D020 (full system architecture confirmation): Modular Monolith vs Microservices ->
  Modular Monolith confirmed
- P018/D023 (service-boundary-fix + BFF/gateway/observability layering proposal review):
  Microservices vs Strangler Fig -> Strangler Fig won as sequencing strategy; deliberately did NOT
  reuse Modular Monolith vs Microservices even though this is the same OMS lineage, because the
  KB overlap score was low (0.111, well under the 0.8 reuse-avoidance threshold) and the question
  here is migration sequencing, not internal boundary confirmation.

Rule for future OMS problems: use Modular Monolith vs Microservices for "is our internal module
boundary/deployment topology correct" questions. Use Microservices vs Strangler Fig for "how do we
safely evolve from a coupled-today state to a decoupled-target state" questions. These are
different tensions even within the same system lineage -- do not default to the same pair just
because the domain tag (oms) matches.
