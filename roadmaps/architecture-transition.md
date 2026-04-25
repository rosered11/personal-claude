# Architecture Transition Roadmap
**Goal:** Backend Developer → Software Architecture Specialist
**Current Phase:** Foundation
**Last Updated:** 2026-04-25
**Consultation Count:** 0

---

## Current Focus
Build foundational pattern literacy and systems thinking by working through real architecture problems with the team. Each consultation is a learning opportunity — the goal right now is exposure breadth across the core architectural domains.

## Skill Domains

### Distributed Systems
- [ ] CAP theorem and consistency models — foundational for every distributed design decision
- [ ] Failure modes: partial failures, network partitions, cascading failures
- [ ] Idempotency and exactly-once semantics
- [ ] Backpressure and flow control

### Data Architecture Patterns
- [ ] CQRS (Command Query Responsibility Segregation) — separating reads from writes
- [ ] Event Sourcing — state as a sequence of events
- [ ] Database per service pattern
- [ ] Polyglot persistence

### System Design Fundamentals
- [ ] Load balancing strategies and tradeoffs
- [ ] Caching layers: CDN, application, database
- [ ] API gateway patterns
- [ ] Service discovery and health checking

### Architectural Patterns (Structural)
- [ ] Hexagonal Architecture (Ports & Adapters)
- [ ] Saga Pattern — distributed transaction coordination
- [ ] Strangler Fig — incremental legacy migration
- [ ] Domain-Driven Design: bounded contexts, aggregates, ubiquitous language

### Event-Driven Architecture
- [ ] Message brokers: Kafka, RabbitMQ — when to use each
- [ ] Event schema design and evolution
- [ ] Dead letter queues and poison pill handling
- [ ] Choreography vs. orchestration

### Organizational & Communication Skills
- [ ] Architecture Decision Records (ADRs) — how to document decisions
- [ ] Communicating tradeoffs to non-technical stakeholders
- [ ] Leading design reviews and RFC processes
- [ ] Defining and measuring non-functional requirements

### Cloud & Infrastructure
- [ ] Managed services vs. self-hosted tradeoffs
- [ ] Multi-region and disaster recovery patterns
- [ ] Infrastructure as Code concepts
- [ ] Cost modeling for architecture decisions

---

## Exposure Log (concepts encountered in consultations)

| Concept | First Seen | KB Ref | Skill Domain | Priority |
|---------|------------|--------|--------------|----------|

*(No consultations yet — log will populate as the team processes problems.)*

---

## Recent Learning Opportunities

*(Will appear here after each consultation pipeline run.)*

---

## Phase Progression Criteria

- **Foundation → Intermediate:** Can explain 5+ patterns with their tradeoffs; has encountered problems across 3+ distinct domains; no longer needs to look up what a pattern does before engaging with it
- **Intermediate → Advanced:** Anticipates which lenses will be selected before seeing them; spots org-level and team-topology constraints in problems; can run a design review solo
