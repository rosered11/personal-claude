---
id: D033
chosen_option: "Stateless HTTPS/mTLS Batch Ingestion API (Hexagonal Port) as the Edge-to-Central WAN Transport, with the Platform's Existing At-Least-Once EDA Publish Pipeline Folded In Internally"
problem_id: P028
tags: [rfid, edge-computing, offline-first, transport-protocol, hexagonal-architecture, event-driven-architecture, batch-processing, idempotency, horizontal-scaling]
related_snippets: [S033]
---

# Decision: Stateless HTTPS/mTLS Batch Ingestion API (Hexagonal Port) as the Edge-to-Central WAN Transport

## Context

P028 needs a formally decided WAN transport protocol between edge agents (DC Site
Server, Store Gateway) and the central Ingestion Service, distinct from the
already-reserved local MQTT hop. kb-search against the existing 27 KB entries found one
meaningful precedent -- P027 (overlap_score ~0.3 on `rfid`/`edge-computing`/
`offline-first`) -- the same RFID Event Platform, but a different problem (gate-level
manifest verification, not transport protocol selection); this decision establishes a
new, second precedent for the platform rather than updating P027/D032.

## Options Considered

**Lens A -- Event-Driven Architecture**: Extend the platform's existing broker-based
event fabric across the WAN -- edge agents hold a persistent MQTT/AMQP session (QoS1 /
durable subscription) directly against a WAN-scoped broker tier, publishing batches to
a `site_id`-partitioned topic. Offline buffering and ordered replay become largely
broker-native (unacked messages persist in the durable session through an outage), and
delivery ack is the broker's own PUBACK/settlement, reusing the platform's already-
proven at-least-once + idempotent-`event_id` operational model end to end.

**Lens B -- Hexagonal Architecture**: Treat the WAN hop as an explicit, narrow "driving
port" into the platform core -- a stateless HTTPS batch API (`POST /v1/events/batch`),
authenticated via per-site mTLS client certificate, returning a synchronous per-
`event_id` ack array in the response body. The edge implements a driven adapter against
this single contract; internally, the Ingestion Service's already-documented pipeline
(validate envelope -> dedupe by `event_id` -> append to event store -> publish to
canonical topics) is entirely unchanged and untouched by this hop's protocol choice.

Both architects agreed on the real contrast: Lens A optimizes for transport homogeneity
by extending the platform's message-broker fabric all the way to the edge; Lens B
optimizes for boundary isolation by keeping the WAN hop a narrow, firewall-friendly
synchronous API contract, confining broker semantics to the platform's internal side.

## Decision

Adopt **Lens B (Hexagonal)** as the primary structure: a **stateless HTTPS/mTLS batch
ingestion API** is the sole edge-facing port for this hop, exactly matching the spec's
own description of the Ingestion Service as "the only edge-facing surface." Concretely:

- `POST /v1/events/batch` accepts a `{ site_id, events[] }` envelope where every event
  carries its edge-generated `event_id`; the endpoint returns a per-`event_id` ack
  array (`Accepted` / `DuplicateIgnored` / `Rejected`) in the same synchronous response
  -- the edge purges only the `event_id`s explicitly confirmed, directly satisfying the
  "edge must know for certain which batch was received" constraint without inventing a
  separate ack channel.
- The endpoint is genuinely stateless: no session affinity, no per-client connection
  state at the Ingestion Service or its infrastructure -- any replica behind a
  commodity HTTPS load balancer can serve any site, satisfying the "scale to campaign
  peak with no per-client session state" constraint directly.
- Auth is per-site mTLS client certificate (or OAuth2 client-credentials as a fallback),
  reusing HTTP mechanisms that retail/warehouse network and security teams already
  operate -- and HTTPS/443 traverses virtually every corporate firewall/proxy without
  special network exceptions, directly answering the multi-site firewall/proxy
  constraint that a broker port (1883/8883/5671) would put at real risk.
- Internally, the endpoint does not reinvent the platform's reliability model: once an
  event is validated and deduped, it is appended to the event store and published to
  the existing canonical event topics through the platform's already-proven internal
  event-driven pipeline (Lens A's insight, folded in rather than rejected) -- the WAN
  hop's protocol choice is deliberately decoupled from, and has zero impact on, how the
  platform distributes events internally.
- To avoid the batch endpoint becoming a synchronous bottleneck at 7.7/11.11 peak, the
  handler processes the batch's events with bounded internal concurrency and returns as
  soon as durable envelope-level acceptance (dedup + event-store append) is confirmed --
  it does not block the edge's ack on downstream topic-consumer processing.

## Consequences

**Accepted trade-offs**:
- The edge is responsible for its own retry/backoff on ambiguous failures (e.g. a
  request timeout with unknown server-side outcome) -- the protocol does not give
  delivery guarantees "for free" the way a broker's persistent session would; this is
  mitigated by full at-least-once idempotency already being a hard platform-wide
  requirement regardless of transport, so a blind retry is always safe.
- Batch chunking and back-off for very large offline-replay bursts (e.g. after a >24h
  outage) must be explicitly designed into the API contract (bounded batch size,
  client-side pacing) rather than inherited implicitly from a broker's flow control.
- No native pub/sub decoupling exists on the WAN hop itself -- any future consumer that
  wants raw edge event streams must still go through the Ingestion Service, though this
  is already true today given the platform's "only edge-facing surface" principle.

**Benefits**:
- Satisfies every constraint in the Clarified Scope without a workaround: at-least-once
  + idempotent `event_id` (via the ack array), >=24h offline tolerance with ordered
  replay (edge-local buffer, unaffected by transport choice), stateless horizontal
  scaling (no session state, ever), batch-oriented (native to the contract), multi-site
  uniformity (identical HTTPS contract for DC and Store), no MQTT-hop conflation
  (a completely different protocol and operational model), and a reliable ack (explicit
  per-`event_id` verdicts in the synchronous response).
- Firewall/proxy traversal risk -- explicitly called out as a constraint for sites
  distributed across retail and warehouse locations -- is close to zero with
  HTTPS/443, versus a real and recurring operational risk with broker ports at large
  retail-chain scale.
- Reuses the platform's own literal architecture ("the only edge-facing surface") as a
  Hexagonal port/adapter boundary rather than introducing a new mental model -- the
  internal event-driven pipeline (already proven in P027/D032 and the base platform
  design) is entirely untouched by this decision.

**Rejected -- MQTT/AMQP over WAN (Lens A's proposed transport)**: rejected as the
primary transport, not because at-least-once/ordered-replay semantics were wrong --
Lens A's reliability instincts were correct and are reused internally -- but because a
persistent per-site broker session, multiplied across DC *and* thousands of Store
Gateways, reintroduces exactly the per-client session state the Ingestion Service is
explicitly designed to avoid, and raw broker ports are materially more likely to be
blocked or require special allowances on retail store networks than HTTPS/443. Reusing
broker semantics for this hop also risks the operational-model conflation the platform
constraints explicitly rule out -- on-call engineers would need to reason about two
differently-scoped MQTT/AMQP deployments (local hop vs. WAN hop) rather than one clearly
bounded transport per hop.

**Also considered and set aside -- gRPC streaming**: named as a candidate in the
original problem but not developed as a separate lens, since it shares Lens A's core
weakness for this hop (a long-lived connection that many corporate/retail proxies
handle poorly) without adding meaningful benefit over a stateless batch REST contract
for a workload that is explicitly batch-oriented and tolerant of WAN latency --
streaming's main advantage (low per-message overhead for a continuous stream) does not
match the shape of this problem.

**Confidence**: high. The Clarified Scope's hard constraints (stateless/no per-client
session state, multi-site firewall traversal, explicit reliable ack, must not reuse the
local MQTT operational model) map almost without friction onto Lens B, while Lens A's
proposed transport is in direct, structural tension with two of those constraints
(statelessness, firewall traversal) rather than merely a matter of preference. This
mirrors the platform's existing self-description of the Ingestion Service as "the only
edge-facing surface" -- a Hexagonal port was already implied by the platform's own
documentation before this consultation ran; this decision makes that implicit boundary
explicit and protocol-concrete.
