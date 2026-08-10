---
name: Recurring Lens-Pairing Patterns Across Consultations
description: Which lens pairs, and which "blend by X" framings, have recurred across KB decisions -- useful for picking a fresh, illuminating pair and recognizing when a lens should be evaluated-then-rejected rather than folded in
type: project
---

Across this KB's consultations, lens pairs tend to fall into a small number of
recurring "blend by ___" shapes rather than being purely domain-specific:

- **Blend by layer** (D030: Hexagonal + Service Mesh) -- each lens governs a
  different architectural altitude (application/compile-time vs. transport/network);
  neither could produce the other's finding.
- **Blend by decision-vs-transport** (D031: Saga + Event-Driven; D032: DDD + EDA) --
  one lens owns *who decides / who enforces the invariant*, the other owns *how data
  physically moves*. This shape has recurred the most (2 of 3 RFID/PTL-domain
  decisions so far).
- **Evaluate-and-reject, not blend** (D033: Hexagonal over EDA) -- the first case
  where the non-primary lens's *core proposed mechanism* was seriously evaluated and
  then explicitly rejected (not folded in) because it was in direct structural
  tension with hard constraints (statelessness, firewall traversal), while only a
  narrower underlying value from that lens (at-least-once reliability) survived,
  repositioned as an internal concern the winner never had to touch.

For **transport/integration-protocol-shaped problems** specifically (not
invariant-ownership problems), Event-Driven Architecture vs. Hexagonal Architecture is
a strong contrasting pair: EDA naturally proposes "extend the existing message-broker
fabric to this new hop," Hexagonal naturally proposes "wrap this hop in an explicit
synchronous port/adapter contract." The real deciding factor in both cases seen so far
has been *operational* constraints (per-client session state at fleet scale,
firewall/proxy traversal) rather than raw feature comparison -- when a problem's hard
constraints include either of those two things, expect Hexagonal's stateless-API-port
framing to have a structural advantage over a broker-session-based EDA proposal.

**Why:** Tracking this avoids two failure modes: (1) mechanically reusing the same
lens pair for every problem on a given platform just because it worked last time (KB
dedup already discourages exact repeats), and (2) treating every two-lens decision as
"pick the best, fold the rest in" when sometimes the correct move is a clean rejection
of the non-primary lens's core mechanism while still crediting its underlying value.

**How to apply:** Before invoking lens-determiner, mentally classify the problem as
invariant-ownership-shaped (favors DDD/Saga vs EDA "blend by decision-vs-transport"),
layer-shaped (favors Hexagonal vs Service Mesh/Microservices "blend by layer"), or
transport/protocol-shaped (favors EDA vs Hexagonal, expect operational constraints --
not features -- to decide the winner, and expect a real chance of outright rejection
rather than a blend). This is a hint for reviewing lens-determiner's output critically,
not a rule to hand it directly -- still let lens-determiner make the explicit call from
the problem JSON and KB search results.
