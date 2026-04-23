---
name: Recurring Architectural Tensions
description: Common trade-off pairs that appear repeatedly in this codebase — helps synthesizer identify which tension the decision is resolving
type: project
---

# Recurring Architectural Tensions

From KOS D# decisions and I# incidents. When synthesizing between two lens options, identify which tension applies — it clarifies why one option wins over the other.

---

## Simplicity vs Safety

**Applies to:** ETL transaction scope, Saga vs local TX

- Per-batch commit loop is simple (while + BeginTransactionAsync inside) and safe (TX hold bounded)
- Single-job TX is simpler to read but fails at scale
- Saga is complex (compensating transactions) but safe for multi-service flows

**Resolution rule:** When safety means preventing data loss or corruption, choose safety over simplicity even with higher implementation cost.

**KB precedent:** D003, D012

---

## Performance vs Correctness

**Applies to:** EF Core eager load vs lazy load, batch size

- Eager loading (Include chains) is correct (no N+1) but slightly more complex query
- Lazy loading is simple but incorrect at scale (N+1 is guaranteed)
- Large batch size is faster but risks OOM and TX timeout
- Small batch size is correct but may be too slow for volume

**Resolution rule:** When the performance choice introduces N+1 or OOM risk, correctness wins. Optimize the correct path, not the incorrect one.

**KB precedent:** D001, D005

---

## Memory vs Throughput

**Applies to:** Batch size selection, ChangeTracker management

- Large batch = high throughput, high memory, OOM risk
- Small batch = low memory, more TX overhead, lower throughput
- 10K batch = empirically validated balance for EF Core on this stack

**Resolution rule:** Choose the batch size that stays below OOM threshold while meeting throughput SLA. 10K is the validated default.

**KB precedent:** D005

---

## Flexibility vs Predictability

**Applies to:** Real-time connection strategy, rate limiting algorithm

- WebSocket: flexible (bidirectional, any latency), complex (stateful, sticky sessions)
- SSE: predictable (server-push only), simpler, proxy-friendly
- Token Bucket: flexible (burst-tolerant), less predictable under burst
- Sliding Window Counter: predictable (strict per-window), no burst tolerance

**Resolution rule:** If the use case requires bidirectional or true real-time, choose flexibility. If server-push with occasional staleness is acceptable, choose predictability.

**KB precedent:** D011, D013

---

## Coordination vs Independence

**Applies to:** ID generation, distributed transactions

- Snowflake: requires worker ID coordination (Redis INCR or config), but IDs are time-sortable
- UUID v4: fully independent (no coordination), but random (index fragmentation, not sortable)
- 2PC: strong coordination, blocking failure mode
- Saga: independent compensating transactions, eventual consistency

**Resolution rule:** When time-sortability or strict ordering matters, accept coordination cost. When randomness is a security property, choose independence.

**KB precedent:** D012, D014
