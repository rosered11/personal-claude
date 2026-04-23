---
name: K-Concepts Background Knowledge
description: Selected K# foundational concepts from KOS — background for selecting appropriate lenses when problem tags match these domains
type: project
---

# K-Concepts Background for Lens Selection

When problem tags match these domains, use these K# summaries to understand the conceptual space before selecting lenses.

---

## K3: Consistent Hashing

**Domain:** Distributed caches, data partitioning, load balancing
**When relevant:** Tags `distributed`, `scalability`, `sharding`, `cache`
**Key insight:** Adding/removing nodes remaps only k/n keys (not all keys). Virtual nodes (100–200 per server) ensure even distribution. Use when server count is dynamic.

---

## K4: Rate Limiting Algorithms

**Domain:** API design, traffic control
**When relevant:** Tags `rate-limiting`, `api`, `scalability`
**Key insight:**
- Token Bucket → burst-friendly, parameters: capacity + refill_rate
- Sliding Window Counter → high accuracy without log storage
- Distributed: Redis Lua atomic scripts (see S010)

---

## K5: Snowflake ID Generation

**Domain:** Distributed unique IDs
**When relevant:** Tags `id-generation`, `distributed`, `snowflake`
**Key insight:** 64-bit: [timestamp 41b][datacenter 5b][machine 5b][sequence 12b]. Time-sortable. 4,096 IDs/ms per machine. Requires clock sync (NTP). Clock going backwards: wait until current > last. See S011 for Go implementation.

---

## K11: Distributed Transactions — 2PC, Saga, TC/C

**Domain:** Multi-service consistency
**When relevant:** Tags `distributed`, `transaction`, `saga`, `microservices`
**Selection matrix:**
- Single DB → local ACID transaction
- 2–3 services, eventual consistency OK → Saga
- Cross-DB, strict business rules → TC/C
- Payments → TC/C + idempotency keys + reconciliation

---

## K14: WebSocket vs SSE vs Polling

**Domain:** Real-time connections
**When relevant:** Tags `real-time`, `websocket`, `sse`, `polling`, `api`
**Selection matrix:**
- < 100ms, bidirectional → WebSocket
- 100ms–1s, server-push only → SSE
- > 1s acceptable or proxy-constrained → Long polling

---

## K16: Database Sharding Strategies

**Domain:** Horizontal DB partitioning
**When relevant:** Tags `database`, `scalability`, `sharding`, `distributed`
**Selection matrix:**
- Queries always include shard key → Hash sharding
- Need range queries → Range sharding
- Need data relocation → Directory sharding
- Compliance/latency per region → Geo sharding

---

## K18: Message Queue Internals (Kafka)

**Domain:** Event-driven systems
**When relevant:** Tags `kafka`, `event-driven`, `messaging`, `async`
**Key insight:** WAL + partitions + ISR. ACK levels: 0 (fire-and-forget), 1 (leader only), all (all ISR). Financial events → ack=all + min.insync.replicas=2. Each partition owned by one consumer per group. Kafka is pull-model (consumers control pace).

---

## K20: Idempotency in Distributed Systems

**Domain:** Resilience, payment safety
**When relevant:** Tags `idempotency`, `distributed`, `payment`, `retry`
**Selection matrix:**
- Payment/order creation → client-generated UUID key mandatory
- Kafka consumer → deduplication table with TTL
- State machine transition → conditional UPDATE (WHERE status='pending')

---

## K23: Double-Entry Ledger System

**Domain:** Financial systems
**When relevant:** Tags `payment`, `ledger`, `financial`, `audit`
**Key insight:** Every transfer = two entries (debit + credit). Sum of all entries always = 0. Store money as integer cents, never float. Append-only — never delete entries. Idempotency_key prevents duplicate ledger entries on retry.
