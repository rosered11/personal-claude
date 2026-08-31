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

**Update (2026-08-17, D034)**: a fourth recurring shape has emerged --

- **Blend by consistency-tolerance-before-irreversible-action** (D034: Saga +
  Event-Driven) -- neither "who owns the invariant vs. how data moves" (D032's split)
  nor "which transport wins outright" (D033's split), but *how much eventual
  consistency a flow can tolerate before authorizing an action that cannot be undone*
  (a refund, in this case). Saga wins the orchestration/compensation half because
  something must be able to deny/hold the irreversible action, not just react after
  the fact; EDA is folded in as the transport for whichever verdict results, exactly
  the "blend by decision-vs-transport" shape already used in D031/D032 -- so this is
  best understood as a refinement of that shape (a new axis of *why* the split
  happens), not a wholly separate category from it.

**New sub-pattern worth tracking on its own: locality/evidence-scoped branching
within a single decision.** D034 additionally split its *own* chosen lens pairing by a
runtime condition (same-store vs. cross-store return) rather than applying one
resolution strategy uniformly across the whole problem -- most of the flow needed no
exception to the platform's synchronous-call principle at all; only the one leg with
zero local evidence did. This is a finer-grained move than picking a lens pair: before
locking in a blend, check whether the problem actually contains sub-cases with
different evidence/locality availability that would be better served by branching the
chosen lenses' application rather than applying them uniformly. Watch for whether this
becomes a recurring shape the way "blend by concern" did across D030/D031/D032.

**Update (2026-08-24, D035)**: a fifth recurring shape has emerged --

- **Premature-abstraction judgment** (D035: DDD in-aggregate extension vs Hexagonal
  strategy port) -- neither lens disagreed about *what* the system should be able to do
  (support N pluggable manifest-resolution mechanisms); they disagreed about *when* that
  capability earns a formal abstraction boundary versus staying an explicit branch in the
  aggregate that already owns the concern. This is a fundamentally different axis from
  the four above (none of which were about abstraction timing) and won't show up unless
  a problem involves extending an *already-twice-branched* mechanism a third time. DDD
  won by directly following the problem's own steer to reuse an already-proven pattern
  rather than build new abstraction for exactly 3 known, stable variants; Hexagonal's
  port insight was deferred with an explicitly named future trigger, not rejected --
  the same "fold in, don't discard" discipline as every other blend in this KB, just
  applied to a *when*, not a *what*.

**Also worth tracking**: D035 is this KB's second explicit YAGNI deferral (after D032
Addendum 3's manifest-chunking deferral) -- both share the shape "name the exact future
trigger condition, then explicitly decline to build for it now." When evaluating a
Hexagonal-port-vs-simpler-alternative pairing, check whether the problem is really
asking "should this be a port" or "should this be a port *yet*" -- the latter has a
different, narrower resolution (defer with a named trigger) than an outright reject.

**Update (2026-08-24, D036)**: a sixth recurring shape has emerged --

- **First-class modeled entity vs. bidirectional-indexing/query-shape problem**
  (D036: DDD vs CQRS) -- neither lens disagreed about what data needed to exist
  (a container-contents relationship); they disagreed about whether it needed an
  aggregate/invariant owner at all, or was purely a query-shape problem best solved
  by scoping which query directions get which fanout treatment. This axis only
  surfaces when a requirement explicitly names two distinct, asymmetric query
  directions (here: "what is inside this box" needed at the edge in real time, vs.
  "which box is this item in" needed only centrally, non-real-time) -- watch for that
  shape (a stated bidirectional lookup with different latency/locality needs per
  direction) as the trigger for considering CQRS as a contrasting lens, not just
  "read-heavy vs write-heavy" in the generic sense. DDD won as primary because the
  actual read/write volume didn't justify CQRS's fuller "two independently maintained
  projections" machinery (infrequent writes, simple indexed lookups), but CQRS's
  fanout-scope discipline (push only the direction actually needed at the edge) and
  write-boundary validation discipline were folded in anyway -- same "fold in, don't
  discard" pattern as every other blend in this KB, just applied to a *query-routing*
  question rather than a *decision-ownership* or *abstraction-timing* one.

**Running tally of axes seen so far, for quick reference when briefing lens-determiner:**
decision-vs-transport (D031/D032/D034), evaluate-and-reject (D033), layer (D030),
consistency-tolerance-before-irreversible-action (D034, a refinement of decision-vs-
transport), premature-abstraction/when (D035), query-shape/fanout-asymmetry (D036).
Before assuming a problem needs a *new* axis, check whether it is actually a variant
of one already on this list -- new axes have shown up roughly once per platform-
extension problem so far, not on every consultation.

**Update (2026-08-24, D037)**: a seventh recurring shape has emerged, and
the second time DDD-vs-CQRS has been used on this platform (after D036) --

- **Declared-document reuse vs. self-asserted materialized state**
  (D037: DDD in-aggregate-family reuse vs CQRS projection ownership) --
  distinct from D036's axis (whether a relationship needs an invariant
  owner, triggered by two named asymmetric query directions). D037's axis
  is triggered by a different signal: an "expected list" whose source is
  not an externally-declared document (every prior manifest on this
  platform) but the platform's own continuously-changing state about
  itself. Neither lens disagreed that `GateSession`'s invariant-enforcement
  *shape* should be reused (DDD's contribution); the genuine disagreement
  was implicit in the problem itself -- CQRS's read-model-from-events
  discipline turned out to be the only available answer to "how do you
  produce this kind of expected list at all," making it essential
  infrastructure rather than an optional companion insight, a stronger
  form of "fold in, don't reject" than any prior blend (the winning DDD
  design is not just enriched by CQRS, it is *unbuildable* without it).
  A second concrete lesson: CQRS's projection could resolve D036's
  still-open container-interaction risk *by construction* (joining
  container_contents at projection-build time) in a way DDD's own
  in-aggregate reuse could not have discovered on its own -- watch for this
  pattern again: a projection built with full central-database join access
  can sometimes structurally close a risk that edge-side cross-referencing
  can only patch procedurally.

**Running tally of axes seen so far, for quick reference when briefing
lens-determiner:** decision-vs-transport (D031/D032/D034), evaluate-and-
reject (D033), layer (D030), consistency-tolerance-before-irreversible-
action (D034, a refinement of decision-vs-transport), premature-abstraction/
when (D035), query-shape/fanout-asymmetry (D036), declared-document-vs-
self-asserted-state (D037, DDD-vs-CQRS used a second time -- check the
*axis*, not just the lens names, before assuming a repeat pairing is stale).
