---
when_to_use: "When an edge/IoT-style client population (potentially thousands of geographically distributed, variable-network-quality sites) must reliably deliver batched, at-least-once, idempotent events across a WAN to a stateless, horizontally-scaled central ingestion service, and firewall/proxy traversal at scale rules out a persistent broker-protocol connection."
related_problems: [P028]
related_decisions: [D033]
---

# Snippet: Stateless HTTPS Batch Ingestion Port + Edge Offline-Buffer Client

Demonstrates the D033 decision: the Ingestion Service exposes a single Hexagonal
driving port (`POST /v1/events/batch`) that is stateless, mTLS-authenticated, and
returns a synchronous per-`event_id` ack array so a caller knows exactly which events
were durably accepted. The edge-side `EdgeIngestionClient` pairs with a local, ordered
offline buffer: it only purges an event from the buffer once the server has explicitly
confirmed it in the response body, never on an ambiguous 2xx alone. Internally, the
endpoint hands accepted events to `IEventIngestionPipeline` -- the platform's existing
dedupe -> append-to-event-store -> publish-to-canonical-topics pipeline -- which stays
completely decoupled from, and unaffected by, this hop's transport protocol.

No MediatR, no AutoMapper, per repo standard -- explicit interfaces and manual mapping.
